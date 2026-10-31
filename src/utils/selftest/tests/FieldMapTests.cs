using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    public class FieldMapTests
    {
        private static FieldMap Map(params (string Key, string Value)[] entries)
        {
            string body = string.Join(",", entries.Select(e => $"\"{e.Key}\":\"{e.Value}\""));
            return FieldMap.Parse("{" + body + "}");
        }

        [Fact]
        public void Parse_ColumnRowsAndNotData_AreClassified()
        {
            FieldMap map = Map(("e:a", "Devices.Name"), ("e:b", "Rows:RingAccountProfileFlags"), ("e:c", "NotData:error body"));

            Assert.Equal(new FieldTarget(FieldTargetKind.Column, "Devices", "Name", null), map.Resolve("e", "a"));
            Assert.Equal(new FieldTarget(FieldTargetKind.Rows, "RingAccountProfileFlags", null, null), map.Resolve("e", "b"));
            Assert.Equal(new FieldTarget(FieldTargetKind.NotData, null, null, "error body"), map.Resolve("e", "c"));
        }

        [Theory]
        [InlineData("NoDotHere")]
        [InlineData("NotData:")]
        [InlineData("Rows:")]
        [InlineData("")]
        [InlineData("A.B.C")]
        public void Parse_MalformedTarget_Throws(string value)
        {
            Assert.Throws<FormatException>(() => Map(("e:a", value)));
        }

        [Fact]
        public void Parse_KeyWithoutEndpointSeparator_Throws()
        {
            Assert.Throws<FormatException>(() => FieldMap.Parse("""{"nokey":"Devices.Name"}"""));
        }

        [Fact]
        public void Resolve_UnknownPath_ReturnsNull()
        {
            Assert.Null(Map(("e:a", "Devices.Name")).Resolve("e", "other"));
            Assert.Null(Map(("e:a", "Devices.Name")).Resolve("other", "a"));
        }

        [Fact]
        public void Resolve_SingleSegmentWildcard_MatchesOneSegmentOnly()
        {
            FieldMap map = Map(("e:f.*.enabled", "T.C"));

            Assert.NotNull(map.Resolve("e", "f.human.enabled"));
            Assert.Null(map.Resolve("e", "f.human.mode.enabled"));
            Assert.Null(map.Resolve("e", "f.enabled"));
        }

        [Fact]
        public void Resolve_WildcardBeforeArrayMarker_MatchesNameWithBrackets()
        {
            FieldMap map = Map(("e:a.*[]", "T.C"));

            Assert.NotNull(map.Resolve("e", "a.liveView[]"));
            Assert.Null(map.Resolve("e", "a.liveView"));
        }

        [Fact]
        public void Resolve_SubtreeWildcard_MatchesEveryDescendantAndEmptyContainer()
        {
            FieldMap map = Map(("e:p.features.**", "Rows:T"));

            Assert.NotNull(map.Resolve("e", "p.features.x"));
            Assert.NotNull(map.Resolve("e", "p.features.rs.a.b[]"));
            Assert.NotNull(map.Resolve("e", "p.features{}"));
            Assert.Null(map.Resolve("e", "p.other"));
        }

        [Fact]
        public void Resolve_ExactEntryBeatsWildcard()
        {
            FieldMap map = Map(("e:p.features.**", "Rows:Flags"), ("e:p.features.known", "Typed.Col"));

            Assert.Equal("Typed", map.Resolve("e", "p.features.known")!.Table);
            Assert.Equal("Flags", map.Resolve("e", "p.features.other")!.Table);
        }

        [Fact]
        public void HasEntryBelow_EntryUnderThePath_IsTrue()
        {
            FieldMap map = Map(("e:a.b.c", "T.C"), ("e:x[].y", "T.C"), ("e:w.*.v", "T.C"));

            Assert.True(map.HasEntryBelow("e", "a.b"));
            Assert.True(map.HasEntryBelow("e", "a"));
            Assert.True(map.HasEntryBelow("e", "x"));
            Assert.True(map.HasEntryBelow("e", "w.anything"));
            Assert.False(map.HasEntryBelow("e", "a.b.c"));
            Assert.True(Map(("e:cv.tags[]", "T.C")).HasEntryBelow("e", "cv.tags"));
            Assert.False(Map(("e:cv.tags", "T.C")).HasEntryBelow("e", "cv.tags"));
            Assert.False(map.HasEntryBelow("e", "z"));
            Assert.False(map.HasEntryBelow("other", "a"));
        }

        [Fact]
        public void HasStoredEntries_OnlyNotDataEntries_IsFalse()
        {
            FieldMap map = Map(("only:a", "NotData:why"), ("both:a", "NotData:why"), ("both:b", "T.C"));

            Assert.False(map.HasStoredEntries("only"));
            Assert.True(map.HasStoredEntries("both"));
            Assert.False(map.HasStoredEntries("none"));
        }

        [Fact]
        public void LoadEmbedded_CheckedInMap_ParsesWithoutJsonColumns()
        {
            FieldMap map = FieldMap.LoadEmbedded();

            Assert.NotEmpty(map.Entries);
            Assert.DoesNotContain(map.Entries.Values, t => t.Column != null && t.Column.EndsWith("Json", StringComparison.Ordinal));
        }
    }
}
