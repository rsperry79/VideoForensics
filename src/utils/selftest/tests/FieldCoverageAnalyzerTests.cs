using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    public sealed class FieldCoverageAnalyzerTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "vf-selftest-" + Guid.NewGuid().ToString("N"));

        public FieldCoverageAnalyzerTests()
        {
            _ = Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            Directory.Delete(_dir, recursive: true);
        }

        private static readonly DbSchema Schema = new(new Dictionary<string, IReadOnlySet<string>>
        {
            ["Devices"] = new HashSet<string> { "Name", "Address", "MetadataJson" },
            ["Flags"] = new HashSet<string> { "Id" },
        });

        private static FieldMap Map(params (string Key, string Value)[] entries)
        {
            return FieldMap.Parse("{" + string.Join(",", entries.Select(e => $"\"{e.Key}\":\"{e.Value}\"")) + "}");
        }

        private void Write(string name, string json)
        {
            File.WriteAllText(Path.Combine(_dir, name), json);
        }

        private void WriteIndex(params (string Endpoint, string File, bool Success, int Status)[] calls)
        {
            string items = string.Join(",", calls.Select(c =>
                $$"""{"Endpoint":"{{c.Endpoint}}","Success":{{(c.Success ? "true" : "false")}},"ResultFile":"{{c.File}}","HttpCalls":[{"StatusCode":{{c.Status}},"BodyFile":"{{c.File}}"}]}"""));
            Write("index.json", "{\"Calls\":[" + items + "]}");
        }

        [Fact]
        public void Analyze_PathWithNoEntry_IsReportedUnmappedPerEndpoint()
        {
            Write("things_1_20260101T000000Z.json", """{"items":[{"name":"a","extra":1},{"name":"b","extra":2}]}""");
            FieldMap map = Map(("things:items[].name", "Devices.Name"));

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, map, Schema);

            Assert.Equal(["items[].extra"], report.UnmappedByEndpoint["things"]);
            Assert.Equal(1, report.UnmappedPathCount);
        }

        [Fact]
        public void Analyze_EveryPathMapped_ReportsNothing()
        {
            Write("things_1_20260101T000000Z.json", """{"name":"a","tags":["x"],"empty":[]}""");
            FieldMap map = Map(("things:name", "Devices.Name"), ("things:tags[]", "Rows:Flags"), ("things:empty[]", "NotData:always empty"));

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, map, Schema);

            Assert.Empty(report.UnmappedByEndpoint);
            Assert.Empty(report.StaleEntries);
            Assert.Empty(report.EndpointsWithoutMapEntries);
            Assert.Equal(0, report.UnmappedPathCount);
        }

        [Fact]
        public void Analyze_NotDataEntry_CountsAsMapped()
        {
            Write("things_1_20260101T000000Z.json", """{"error":860005,"msg":"No authorized devices"}""");
            FieldMap map = Map(("things:error", "NotData:error body"), ("things:msg", "NotData:error body"), ("things:other", "Devices.Name"));

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, map, Schema);

            Assert.Empty(report.UnmappedByEndpoint);
        }

        [Fact]
        public void Analyze_StaleEntries_ReportUnknownTableUnknownColumnAndJsonColumn()
        {
            FieldMap map = Map(
                ("e:ok", "Devices.Name"),
                ("e:missingTable", "Gone.Name"),
                ("e:missingColumn", "Devices.Renamed"),
                ("e:json", "Devices.MetadataJson"),
                ("e:rows", "Rows:AlsoGone"),
                ("e:rowsOk", "Rows:Flags"),
                ("e:notData", "NotData:fine"));

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, map, Schema);

            Assert.Equal(
                ["e:json", "e:missingColumn", "e:missingTable", "e:rows"],
                report.StaleEntries.Select(s => s.Key).OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal(4, report.StaleEntryCount);
        }

        [Fact]
        public void Analyze_CallMarkedFailedInIndex_IsSkippedNotReported()
        {
            Write("signalling-ticket_1_20260101T000000Z.json", """{"error":860005,"msg":"No authorized devices"}""");
            Write("things_1_20260101T000000Z.json", """{"name":"a"}""");
            WriteIndex(("signalling-ticket", "signalling-ticket_1_20260101T000000Z.json", false, 400), ("things", "things_1_20260101T000000Z.json", true, 200));
            FieldMap map = Map(("things:name", "Devices.Name"));

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, map, Schema);

            Assert.Empty(report.UnmappedByEndpoint);
            Assert.Empty(report.EndpointsWithoutMapEntries);
            SkippedResponseFile skipped = Assert.Single(report.SkippedFiles);
            Assert.Equal("signalling-ticket_1_20260101T000000Z.json", skipped.File);
        }

        [Fact]
        public void Analyze_ErrorStatusBodyOfPassedCall_IsSkipped()
        {
            Write("things_1_20260101T000000Z.json", """{"boom":true}""");
            WriteIndex(("things", "things_1_20260101T000000Z.json", true, 500));

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, Map(("things:name", "Devices.Name")), Schema);

            Assert.Empty(report.UnmappedByEndpoint);
            _ = Assert.Single(report.SkippedFiles);
        }

        [Fact]
        public void Analyze_NoIndexFile_ProcessesEveryResponseFile()
        {
            Write("things_1_20260101T000000Z.json", """{"extra":1}""");

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, Map(("things:name", "Devices.Name")), Schema);

            Assert.Equal(["extra"], report.UnmappedByEndpoint["things"]);
            Assert.Empty(report.SkippedFiles);
        }

        [Fact]
        public void Analyze_IndexAndReportFiles_AreNotTreatedAsResponses()
        {
            Write("index.json", """{"Calls":[]}""");
            Write("db-completeness.json", """{"DbPath":"x"}""");
            Write("notes.txt", "ignored");

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, Map(("things:name", "Devices.Name")), Schema);

            Assert.Empty(report.UnmappedByEndpoint);
            Assert.Empty(report.EndpointsWithoutMapEntries);
        }

        [Fact]
        public void Analyze_EndpointWithDataAndNoStoredEntries_IsReported()
        {
            Write("orphan_1_20260101T000000Z.json", """{"a":1}""");
            Write("empty_1_20260101T000000Z.json", "[]");
            Write("notdata_1_20260101T000000Z.json", """{"err":1}""");
            FieldMap map = Map(("empty:[]", "NotData:empty list"), ("notdata:err", "NotData:error body"));

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, map, Schema);

            Assert.Equal(["orphan"], report.EndpointsWithoutMapEntries);
        }

        [Fact]
        public void Analyze_PathNullInEverySample_IsReportedEvenWhenMapped()
        {
            Write("things_1_20260101T000000Z.json", """{"name":null,"address":"x"}""");
            Write("things_2_20260101T000001Z.json", """{"name":null,"address":null}""");
            FieldMap map = Map(("things:name", "Devices.Name"), ("things:address", "Devices.Address"));

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, map, Schema);

            Assert.Equal(["name"], report.NullInSampleByEndpoint["things"]);
            Assert.Equal(1, report.NullInSamplePathCount);
            Assert.Empty(report.UnmappedByEndpoint);
        }

        [Fact]
        public void Analyze_NullPathWhoseChildrenAreMapped_CountsAsMappedButStillNullInSample()
        {
            Write("things_1_20260101T000000Z.json", """{"placement":null}""");
            FieldMap map = Map(("things:placement.room", "Devices.Name"));

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, map, Schema);

            Assert.Empty(report.UnmappedByEndpoint);
            Assert.Equal(["placement"], report.NullInSampleByEndpoint["things"]);
        }

        [Fact]
        public void Analyze_NullPathWithNoEntryBelow_IsUnmapped()
        {
            Write("things_1_20260101T000000Z.json", """{"snooze":null}""");

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, Map(("things:name", "Devices.Name")), Schema);

            Assert.Equal(["snooze"], report.UnmappedByEndpoint["things"]);
        }

        [Fact]
        public void Analyze_PathsFromSeveralFilesOfOneEndpoint_AreMergedAndDeduplicated()
        {
            Write("things_1_20260101T000000Z.json", """{"items":[{"extra":1}]}""");
            Write("things_2_20260101T000001Z.json", """{"items":[{"extra":2},{"extra":3}]}""");

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, Map(("things:name", "Devices.Name")), Schema);

            Assert.Equal(["items[].extra"], report.UnmappedByEndpoint["things"]);
        }

        [Fact]
        public void Analyze_ValueVerifierSupplied_ItsMismatchesAreCarried()
        {
            var mismatch = new FieldValueMismatch("things:name", "a", "b");

            FieldCoverageReport report = FieldCoverageAnalyzer.Analyze(_dir, Map(("things:name", "Devices.Name")), Schema, new FixedVerifier(mismatch));

            Assert.Equal([mismatch], report.ValueMismatches);
        }

        [Fact]
        public void Analyze_NoValueVerifier_ReportsNoMismatches()
        {
            Assert.Empty(FieldCoverageAnalyzer.Analyze(_dir, Map(("things:name", "Devices.Name")), Schema).ValueMismatches);
        }

        private sealed class FixedVerifier(FieldValueMismatch mismatch) : IFieldValueVerifier
        {
            public IReadOnlyList<FieldValueMismatch> Verify(FieldMap map, string outputDir)
            {
                return [mismatch];
            }
        }
    }
}
