namespace VideoForensics.Forensics.Tests
{
    public class ForensicAnalyzerTests
    {
        private readonly IForensicAnalyzer _analyzer = new ForensicAnalyzer();

        #region AnalyzeEvidenceAsync Tests

        [Fact]
        public async Task AnalyzeEvidenceAsync_WithValidEvidence_ReturnsFinding()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "test-device",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion"
            };

            // Act
            ForensicAnalysisResult result = await _analyzer.AnalyzeEvidenceAsync(evidence);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Finding);
            Assert.Equal(evidence.EvidenceId, result.EvidenceId);
            Assert.Contains("motion", result.Finding, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(AnalysisSeverity.Info, result.Severity);
        }

        [Fact]
        public async Task AnalyzeEvidenceAsync_WithNoEventType_ReturnsFinding()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "test-device",
                EventTimestamp = DateTime.UtcNow,
                EventType = null
            };

            // Act
            ForensicAnalysisResult result = await _analyzer.AnalyzeEvidenceAsync(evidence);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Finding);
            Assert.Contains("No event type", result.Finding);
            Assert.Equal(AnalysisSeverity.Info, result.Severity);
        }

        [Fact]
        public async Task AnalyzeEvidenceAsync_WithTamperingEventType_ReturnsWarning()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "test-device",
                EventTimestamp = DateTime.UtcNow,
                EventType = "tampering"
            };

            // Act
            ForensicAnalysisResult result = await _analyzer.AnalyzeEvidenceAsync(evidence);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(AnalysisSeverity.Warning, result.Severity);
            Assert.Contains("Suspicious", result.Finding);
            Assert.NotNull(result.Recommendation);
        }

        [Fact]
        public async Task AnalyzeEvidenceAsync_WithCriticalEventType_ReturnsCritical()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "test-device",
                EventTimestamp = DateTime.UtcNow,
                EventType = "critical-alert"
            };

            // Act
            ForensicAnalysisResult result = await _analyzer.AnalyzeEvidenceAsync(evidence);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(AnalysisSeverity.Critical, result.Severity);
            Assert.Contains("Critical", result.Finding);
        }

        [Fact]
        public async Task AnalyzeEvidenceAsync_WithExtractedData_RecordsMetadata()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "test-device",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                ExtractedData = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["confidence"] = 0.95
                }
            };

            // Act
            ForensicAnalysisResult result = await _analyzer.AnalyzeEvidenceAsync(evidence);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("has-extracted-data", result.Tags);
            Assert.True(result.AnalysisData.ContainsKey("extracted-fields-count"));
            Assert.Equal(2, result.AnalysisData["extracted-fields-count"]);
        }

        [Fact]
        public async Task AnalyzeEvidenceAsync_WithChecksums_RecordsIntegrity()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "test-device",
                EventTimestamp = DateTime.UtcNow,
                EventType = "motion",
                Checksums = new Dictionary<string, string>
                {
                    ["sha256"] = "abc123def456",
                    ["md5"] = "xyz789"
                }
            };

            // Act
            ForensicAnalysisResult result = await _analyzer.AnalyzeEvidenceAsync(evidence);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("integrity-verified", result.Tags);
            Assert.True(result.AnalysisData.ContainsKey("checksum-types"));
            string checksumTypes = (string)result.AnalysisData["checksum-types"];
            Assert.Contains("sha256", checksumTypes);
            Assert.Contains("md5", checksumTypes);
        }

        [Fact]
        public async Task AnalyzeEvidenceAsync_WithNullEvidence_ThrowsArgumentNullException()
        {
            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentNullException>(
                () => _analyzer.AnalyzeEvidenceAsync(null!));
        }

        [Fact]
        public async Task AnalyzeEvidenceAsync_WithEventTypeTag_IncludesTagInResult()
        {
            // Arrange
            var evidence = new EvidenceMetadata
            {
                SourceDeviceId = "test-device",
                EventTimestamp = DateTime.UtcNow,
                EventType = "alarm"
            };

            // Act
            ForensicAnalysisResult result = await _analyzer.AnalyzeEvidenceAsync(evidence);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("alarm", result.Tags);
        }

        #endregion

        #region DetectAnomaliesAsync Tests

        [Fact]
        public async Task DetectAnomaliesAsync_WithEmptySequence_ReturnsEmpty()
        {
            // Arrange
            var sequence = new List<EvidenceMetadata>();

            // Act
            IEnumerable<ForensicAnalysisResult> results = await _analyzer.DetectAnomaliesAsync(sequence);

            // Assert
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public async Task DetectAnomaliesAsync_WithSingleItem_ReturnsEmpty()
        {
            // Arrange
            var sequence = new List<EvidenceMetadata>
            {
                new() {
                    SourceDeviceId = "device-1",
                    EventTimestamp = DateTime.UtcNow,
                    EventType = "motion"
                }
            };

            // Act
            IEnumerable<ForensicAnalysisResult> results = await _analyzer.DetectAnomaliesAsync(sequence);

            // Assert
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public async Task DetectAnomaliesAsync_WithRapidSequence_DetectsAnomaly()
        {
            // Arrange
            DateTime baseTime = DateTime.UtcNow;
            var sequence = new List<EvidenceMetadata>
            {
                new() {
                    SourceDeviceId = "device-1",
                    EventTimestamp = baseTime,
                    EventType = "motion"
                },
                new() {
                    SourceDeviceId = "device-1",
                    EventTimestamp = baseTime.AddSeconds(2),
                    EventType = "motion"
                }
            };

            // Act
            IEnumerable<ForensicAnalysisResult> results = await _analyzer.DetectAnomaliesAsync(sequence);

            // Assert
            Assert.NotNull(results);
            Assert.NotEmpty(results);
            ForensicAnalysisResult? rapidAnomaly = results.FirstOrDefault(r => r.Tags.Contains("rapid-succession"));
            Assert.NotNull(rapidAnomaly);
            Assert.Equal(AnalysisSeverity.Warning, rapidAnomaly.Severity);
            Assert.Contains("Rapid event sequence", rapidAnomaly.Finding);
        }

        [Fact]
        public async Task DetectAnomaliesAsync_WithNormalTimeGap_NoAnomaly()
        {
            // Arrange
            DateTime baseTime = DateTime.UtcNow;
            var sequence = new List<EvidenceMetadata>
            {
                new() {
                    SourceDeviceId = "device-1",
                    EventTimestamp = baseTime,
                    EventType = "motion"
                },
                new() {
                    SourceDeviceId = "device-1",
                    EventTimestamp = baseTime.AddMinutes(5),
                    EventType = "motion"
                }
            };

            // Act
            IEnumerable<ForensicAnalysisResult> results = await _analyzer.DetectAnomaliesAsync(sequence);

            // Assert
            Assert.NotNull(results);
            // No temporal anomalies expected with 5 minute gap
            var temporalAnomalies = results.Where(r => r.Tags.Contains("temporal-anomaly")).ToList();
            Assert.Empty(temporalAnomalies);
        }

        [Fact]
        public async Task DetectAnomaliesAsync_WithEventTypeClustering_DetectsPattern()
        {
            // Arrange
            DateTime baseTime = DateTime.UtcNow;
            var sequence = new List<EvidenceMetadata>
            {
                new() {
                    SourceDeviceId = "device-1",
                    EventTimestamp = baseTime,
                    EventType = "motion"
                },
                new() {
                    SourceDeviceId = "device-1",
                    EventTimestamp = baseTime.AddSeconds(60),
                    EventType = "motion"
                },
                new() {
                    SourceDeviceId = "device-1",
                    EventTimestamp = baseTime.AddSeconds(120),
                    EventType = "motion"
                },
                new() {
                    SourceDeviceId = "device-1",
                    EventTimestamp = baseTime.AddSeconds(180),
                    EventType = "motion"
                }
            };

            // Act
            IEnumerable<ForensicAnalysisResult> results = await _analyzer.DetectAnomaliesAsync(sequence);

            // Assert
            Assert.NotNull(results);
            ForensicAnalysisResult? clusteringAnomaly = results.FirstOrDefault(r => r.Tags.Contains("event-clustering"));
            Assert.NotNull(clusteringAnomaly);
            Assert.Contains("multiple events", clusteringAnomaly.Finding, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("4", clusteringAnomaly.Finding);
        }

        [Fact]
        public async Task DetectAnomaliesAsync_WithNullSequence_ThrowsArgumentNullException()
        {
            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentNullException>(
                () => _analyzer.DetectAnomaliesAsync(null!));
        }

        [Fact]
        public async Task DetectAnomaliesAsync_WithMixedEventTypes_OnlyClusterDuplicateTypes()
        {
            // Arrange
            DateTime baseTime = DateTime.UtcNow;
            var sequence = new List<EvidenceMetadata>
            {
                new() { EventTimestamp = baseTime, EventType = "motion" },
                new() { EventTimestamp = baseTime.AddSeconds(60), EventType = "sound" },
                new() { EventTimestamp = baseTime.AddSeconds(120), EventType = "motion" },
                new() { EventTimestamp = baseTime.AddSeconds(180), EventType = "motion" }
            };

            // Act
            IEnumerable<ForensicAnalysisResult> results = await _analyzer.DetectAnomaliesAsync(sequence);

            // Assert
            Assert.NotNull(results);
            var clusteringAnomalies = results.Where(r => r.Tags.Contains("event-clustering")).ToList();
            // Should only detect clustering for "motion" (3 occurrences > 2), not "sound" (1)
            ForensicAnalysisResult? motionClustering = clusteringAnomalies.FirstOrDefault(r => r.Tags.Contains("motion"));
            Assert.NotNull(motionClustering);
        }

        [Fact]
        public async Task DetectAnomaliesAsync_WithNullEventTimestamps_HandlesGracefully()
        {
            // Arrange
            var sequence = new List<EvidenceMetadata>
            {
                new() { EventTimestamp = null, EventType = "motion" },
                new() { EventTimestamp = null, EventType = "motion" }
            };

            // Act
            IEnumerable<ForensicAnalysisResult> results = await _analyzer.DetectAnomaliesAsync(sequence);

            // Assert
            Assert.NotNull(results);
            // Should not crash and may detect event type clustering
        }

        #endregion

        #region GetAnalysisReportAsync Tests

        [Fact]
        public async Task GetAnalysisReportAsync_WithValidResults_ReturnsReport()
        {
            // Arrange
            var results = new List<ForensicAnalysisResult>
            {
                new() {
                    Finding = "Motion detected",
                    Severity = AnalysisSeverity.Info,
                    Recommendation = "Review footage"
                }
            };

            // Act
            ForensicAnalysisReport report = await _analyzer.GetAnalysisReportAsync(results);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(1, report.Findings.Count);
            Assert.NotNull(report.Summary);
            Assert.Contains("1 findings", report.Summary);
        }

        [Fact]
        public async Task GetAnalysisReportAsync_WithEmptyResults_ReturnsEmptyReport()
        {
            // Arrange
            var results = new List<ForensicAnalysisResult>();

            // Act
            ForensicAnalysisReport report = await _analyzer.GetAnalysisReportAsync(results);

            // Assert
            Assert.NotNull(report);
            Assert.Empty(report.Findings);
            Assert.Contains("No findings", report.Summary);
        }

        [Fact]
        public async Task GetAnalysisReportAsync_WithAnalysisType_IncludesInReport()
        {
            // Arrange
            var results = new List<ForensicAnalysisResult>
            {
                new() { Finding = "Test finding", Severity = AnalysisSeverity.Info }
            };
            string analysisType = "temporal-pattern-analysis";

            // Act
            ForensicAnalysisReport report = await _analyzer.GetAnalysisReportAsync(results, analysisType);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(analysisType, report.AnalysisType);
        }

        [Fact]
        public async Task GetAnalysisReportAsync_WithMixedSeverities_CountsCorrectly()
        {
            // Arrange
            var results = new List<ForensicAnalysisResult>
            {
                new() { Finding = "Critical issue", Severity = AnalysisSeverity.Critical },
                new() { Finding = "Warning issue", Severity = AnalysisSeverity.Warning },
                new() { Finding = "Warning issue 2", Severity = AnalysisSeverity.Warning },
                new() { Finding = "Info issue", Severity = AnalysisSeverity.Info }
            };

            // Act
            ForensicAnalysisReport report = await _analyzer.GetAnalysisReportAsync(results);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(4, report.Findings.Count);
            Assert.Contains("1 critical", report.Summary);
            Assert.Contains("2 warnings", report.Summary);
            Assert.Contains("1 informational", report.Summary);
            Assert.Equal(1, (int)report.Metadata["critical-findings"]);
            Assert.Equal(2, (int)report.Metadata["warning-findings"]);
        }

        [Fact]
        public async Task GetAnalysisReportAsync_WithNullResults_ThrowsArgumentNullException()
        {
            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentNullException>(
                () => _analyzer.GetAnalysisReportAsync(null!));
        }

        [Fact]
        public async Task GetAnalysisReportAsync_GeneratedAtIsUtcNow()
        {
            // Arrange
            var results = new List<ForensicAnalysisResult>();
            DateTime beforeGeneration = DateTime.UtcNow;

            // Act
            ForensicAnalysisReport report = await _analyzer.GetAnalysisReportAsync(results);
            DateTime afterGeneration = DateTime.UtcNow;

            // Assert
            Assert.NotNull(report);
            Assert.NotNull(report.ReportId);
            Assert.True(report.GeneratedAt >= beforeGeneration);
            Assert.True(report.GeneratedAt <= afterGeneration);
        }

        [Fact]
        public async Task GetAnalysisReportAsync_ReportHasUniqueId()
        {
            // Arrange
            var results = new List<ForensicAnalysisResult>();

            // Act
            ForensicAnalysisReport report1 = await _analyzer.GetAnalysisReportAsync(results);
            ForensicAnalysisReport report2 = await _analyzer.GetAnalysisReportAsync(results);

            // Assert
            Assert.NotNull(report1.ReportId);
            Assert.NotNull(report2.ReportId);
            Assert.NotEqual(report1.ReportId, report2.ReportId);
        }

        [Fact]
        public async Task GetAnalysisReportAsync_PopulatesMetadata()
        {
            // Arrange
            var results = new List<ForensicAnalysisResult>
            {
                new() { Severity = AnalysisSeverity.Critical },
                new() { Severity = AnalysisSeverity.Warning },
                new() { Severity = AnalysisSeverity.Info }
            };

            // Act
            ForensicAnalysisReport report = await _analyzer.GetAnalysisReportAsync(results);

            // Assert
            Assert.NotNull(report.Metadata);
            Assert.Equal(3, (int)report.Metadata["total-findings"]);
            Assert.Equal(1, (int)report.Metadata["critical-findings"]);
            Assert.Equal(1, (int)report.Metadata["warning-findings"]);
        }

        [Fact]
        public async Task GetAnalysisReportAsync_WithMultipleCritical_SummarizesCorrectly()
        {
            // Arrange
            var results = new List<ForensicAnalysisResult>
            {
                new() { Severity = AnalysisSeverity.Critical },
                new() { Severity = AnalysisSeverity.Critical },
                new() { Severity = AnalysisSeverity.Warning }
            };

            // Act
            ForensicAnalysisReport report = await _analyzer.GetAnalysisReportAsync(results);

            // Assert
            Assert.NotNull(report);
            Assert.Contains("2 critical", report.Summary);
            Assert.Contains("1 warning", report.Summary);
        }

        #endregion
    }
}
