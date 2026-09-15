namespace VideoForensics.Forensics.Tests
{
    public class EvidencePiiRedactorTests
    {
        private readonly IEvidencePiiRedactor _redactor = new EvidencePiiRedactor();

        #region RedactChainOfCustodyAsync Tests

        [Fact]
        public async Task RedactChainOfCustodyAsync_WithDefaultRedactionOptions_LeavesReportUnchanged()
        {
            // Arrange
            var report = new ChainOfCustodyReport
            {
                ReportId = "report-001",
                EvidenceId = "evidence-001",
                SignedByOfficer = "Officer John Doe"
            };
            var options = new RedactionOptions();

            // Act
            ChainOfCustodyReport redacted = await _redactor.RedactChainOfCustodyAsync(report, options);

            // Assert
            Assert.NotNull(redacted);
            Assert.Equal(report.SignedByOfficer, redacted.SignedByOfficer);
            Assert.NotSame(report, redacted); // Should be cloned
        }

        [Fact]
        public async Task RedactChainOfCustodyAsync_WithHandlerRedactionEnabled_RedactsOfficerName()
        {
            // Arrange
            var report = new ChainOfCustodyReport
            {
                ReportId = "report-002",
                EvidenceId = "evidence-002",
                SignedByOfficer = "Officer Jane Smith"
            };
            var options = new RedactionOptions
            {
                RedactHandlerNames = true,
                RedactAccessLogs = true
            };

            // Act
            ChainOfCustodyReport redacted = await _redactor.RedactChainOfCustodyAsync(report, options);

            // Assert
            Assert.Equal("[REDACTED]", redacted.SignedByOfficer);
        }

        [Fact]
        public async Task RedactChainOfCustodyAsync_WithHandlerAndAccessLogs_RedactsCustodyHistory()
        {
            // Arrange
            var report = new ChainOfCustodyReport
            {
                ReportId = "report-003",
                EvidenceId = "evidence-003",
                SignedByOfficer = "Officer Bob Johnson",
                CustodyHistory =
                [
                    new ChainOfCustodyEntry
                    {
                        Handler = "Detective Smith",
                        Notes = "Reviewed evidence"
                    },
                    new ChainOfCustodyEntry
                    {
                        Handler = "Analyst Jones",
                        Notes = "Processed samples"
                    }
                ]
            };
            var options = new RedactionOptions
            {
                RedactHandlerNames = true,
                RedactAccessLogs = true
            };

            // Act
            ChainOfCustodyReport redacted = await _redactor.RedactChainOfCustodyAsync(report, options);

            // Assert
            Assert.All(redacted.CustodyHistory, entry => Assert.Equal("[REDACTED]", entry.Handler));
            Assert.All(redacted.CustodyHistory, entry => Assert.Equal("[REDACTED]", entry.Notes));
        }

        [Fact]
        public async Task RedactChainOfCustodyAsync_WithCustomReplacementString_UsesCustomToken()
        {
            // Arrange
            var report = new ChainOfCustodyReport
            {
                ReportId = "report-004",
                EvidenceId = "evidence-004",
                SignedByOfficer = "Officer Davis"
            };
            var options = new RedactionOptions
            {
                RedactHandlerNames = true,
                ReplacementString = "[REMOVED]"
            };

            // Act
            ChainOfCustodyReport redacted = await _redactor.RedactChainOfCustodyAsync(report, options);

            // Assert
            Assert.Equal("[REMOVED]", redacted.SignedByOfficer);
        }

        [Fact]
        public async Task RedactChainOfCustodyAsync_WithTimestampRedaction_ClearsTimestamps()
        {
            // Arrange
            DateTime now = DateTime.UtcNow;
            var report = new ChainOfCustodyReport
            {
                ReportId = "report-005",
                EvidenceId = "evidence-005",
                GeneratedAt = now,
                EvidenceInitiallyReceived = now.AddDays(-1),
                EvidenceLastAccessed = now.AddHours(-1)
            };
            var options = new RedactionOptions
            {
                RedactTimestamps = true
            };

            // Act
            ChainOfCustodyReport redacted = await _redactor.RedactChainOfCustodyAsync(report, options);

            // Assert
            Assert.Equal(DateTime.MinValue, redacted.GeneratedAt);
            Assert.Null(redacted.EvidenceInitiallyReceived);
            Assert.Null(redacted.EvidenceLastAccessed);
        }

        #endregion

        #region RedactValidationReportAsync Tests

        [Fact]
        public async Task RedactValidationReportAsync_WithHandlerRedaction_RedactsValidatedBy()
        {
            // Arrange
            var report = new EvidenceValidationReport
            {
                ReportId = "report-006",
                EvidenceId = "evidence-006",
                ValidatedBy = "John Validator",
                SignedByOfficer = "Officer Mary Williams"
            };
            var options = new RedactionOptions
            {
                RedactHandlerNames = true
            };

            // Act
            EvidenceValidationReport redacted = await _redactor.RedactValidationReportAsync(report, options);

            // Assert
            Assert.Equal("[REDACTED]", redacted.ValidatedBy);
            Assert.Equal("[REDACTED]", redacted.SignedByOfficer);
        }

        [Fact]
        public async Task RedactValidationReportAsync_WithAllRedactionsEnabled_RedactsMultipleFields()
        {
            // Arrange
            var report = new EvidenceValidationReport
            {
                ReportId = "report-007",
                EvidenceId = "evidence-007",
                ValidatedBy = "Jane Validator",
                SignedByOfficer = "Officer Mary Williams",
                CertificationStatement = "Certified by examiner",
                AllErrors = ["Error 1", "Error 2"],
                AllWarnings = ["Warning 1"]
            };
            var options = new RedactionOptions
            {
                RedactVictimName = true,
                RedactPhoneNumber = true,
                RedactAddress = true,
                RedactEmail = true,
                RedactHandlerNames = true
            };

            // Act
            EvidenceValidationReport redacted = await _redactor.RedactValidationReportAsync(report, options);

            // Assert
            Assert.Equal("[REDACTED]", redacted.ValidatedBy);
            Assert.Equal("[REDACTED]", redacted.CertificationStatement);
            Assert.All(redacted.AllErrors, error => Assert.Equal("[REDACTED]", error));
            Assert.All(redacted.AllWarnings, warning => Assert.Equal("[REDACTED]", warning));
        }

        #endregion

        #region RedactAnalysisReportAsync Tests

        [Fact]
        public async Task RedactAnalysisReportAsync_WithVictimRedaction_RedactsSummary()
        {
            // Arrange
            var report = new ForensicAnalysisReport
            {
                ReportId = "report-008",
                AnalyzedEvidenceId = "evidence-008",
                Summary = "Analysis of evidence involving John Smith at 123 Main St.",
                SignedByOfficer = "Officer Michael Brown"
            };
            var options = new RedactionOptions
            {
                RedactVictimName = true
            };

            // Act
            ForensicAnalysisReport redacted = await _redactor.RedactAnalysisReportAsync(report, options);

            // Assert
            Assert.Equal("[REDACTED]", redacted.Summary);
        }

        [Fact]
        public async Task RedactAnalysisReportAsync_WithHandlerRedaction_RedactsOfficer()
        {
            // Arrange
            var report = new ForensicAnalysisReport
            {
                ReportId = "report-009",
                AnalyzedEvidenceId = "evidence-009",
                Summary = "Analysis results",
                SignedByOfficer = "Officer Michael Brown"
            };
            var options = new RedactionOptions
            {
                RedactHandlerNames = true
            };

            // Act
            ForensicAnalysisReport redacted = await _redactor.RedactAnalysisReportAsync(report, options);

            // Assert
            Assert.Equal("[REDACTED]", redacted.SignedByOfficer);
        }

        [Fact]
        public async Task RedactAnalysisReportAsync_RedactsFindingsContent()
        {
            // Arrange
            var report = new ForensicAnalysisReport
            {
                ReportId = "report-010",
                AnalyzedEvidenceId = "evidence-010",
                Summary = "Analysis",
                Findings =
                [
                    new ForensicAnalysisResult
                    {
                        Finding = "Victim was present",
                        Recommendation = "Contact victim immediately"
                    }
                ]
            };
            var options = new RedactionOptions
            {
                RedactVictimName = true
            };

            // Act
            ForensicAnalysisReport redacted = await _redactor.RedactAnalysisReportAsync(report, options);

            // Assert
            _ = Assert.Single(redacted.Findings);
            Assert.Equal("[REDACTED]", redacted.Findings[0].Finding);
            Assert.Equal("[REDACTED]", redacted.Findings[0].Recommendation);
        }

        #endregion

        #region RedactSignalAnomalyReportAsync Tests

        [Fact]
        public async Task RedactSignalAnomalyReportAsync_WithDeviceSerialRedaction_RedactsSerials()
        {
            // Arrange
            var report = new SignalAnomalyReport
            {
                ReportId = "report-011",
                TotalEventsAnalyzed = 75,
                CameraProfiles =
                [
                    new CameraSignalProfile { CameraId = "ABC123XYZ" }
                ]
            };
            var options = new RedactionOptions
            {
                RedactDeviceSerialNumbers = true
            };

            // Act
            SignalAnomalyReport redacted = await _redactor.RedactSignalAnomalyReportAsync(report, options);

            // Assert
            _ = Assert.Single(redacted.CameraProfiles);
            Assert.Equal("[REDACTED]", redacted.CameraProfiles[0].CameraId);
        }

        [Fact]
        public async Task RedactSignalAnomalyReportAsync_WithTimestampRedaction_ClearsGeneratedAt()
        {
            // Arrange
            DateTime now = DateTime.UtcNow;
            var report = new SignalAnomalyReport
            {
                ReportId = "report-012",
                GeneratedAt = now,
                TotalEventsAnalyzed = 100
            };
            var options = new RedactionOptions
            {
                RedactTimestamps = true
            };

            // Act
            SignalAnomalyReport redacted = await _redactor.RedactSignalAnomalyReportAsync(report, options);

            // Assert
            Assert.Equal(DateTime.MinValue, redacted.GeneratedAt);
        }

        [Fact]
        public async Task RedactSignalAnomalyReportAsync_WithAddressRedaction_RedactsRiskAssessment()
        {
            // Arrange
            var report = new SignalAnomalyReport
            {
                ReportId = "report-013",
                TotalEventsAnalyzed = 50,
                RiskAssessment = "High risk at location 456 Oak Avenue.",
                Recommendations = "Install additional security at 456 Oak Avenue."
            };
            var options = new RedactionOptions
            {
                RedactAddress = true
            };

            // Act
            SignalAnomalyReport redacted = await _redactor.RedactSignalAnomalyReportAsync(report, options);

            // Assert
            Assert.Equal("[REDACTED]", redacted.RiskAssessment);
            Assert.Equal("[REDACTED]", redacted.Recommendations);
        }

        #endregion

        #region GenerateAnonymizedReportAsync Tests

        [Fact]
        public async Task GenerateAnonymizedReportAsync_WithChainOfCustodyReport_AnonymizesAllStrings()
        {
            // Arrange
            var report = new ChainOfCustodyReport
            {
                ReportId = "report-014",
                EvidenceId = "evidence-014",
                SignedByOfficer = "Officer Patricia Clark",
                LegalStatement = "Legal statement text"
            };

            // Act
            ChainOfCustodyReport anonymized = await _redactor.GenerateAnonymizedReportAsync(report);

            // Assert
            Assert.Equal("[REDACTED]", anonymized.SignedByOfficer);
            Assert.Equal("[REDACTED]", anonymized.LegalStatement);
        }

        [Fact]
        public async Task GenerateAnonymizedReportAsync_WithValidationReport_AnonymizesAllStrings()
        {
            // Arrange
            var report = new EvidenceValidationReport
            {
                ReportId = "report-015",
                EvidenceId = "evidence-015",
                ValidatedBy = "Dr. Robert Wilson",
                CertificationStatement = "Evidence validated by certified examiner."
            };

            // Act
            EvidenceValidationReport anonymized = await _redactor.GenerateAnonymizedReportAsync(report);

            // Assert
            Assert.Equal("[REDACTED]", anonymized.ValidatedBy);
            Assert.Equal("[REDACTED]", anonymized.CertificationStatement);
        }

        [Fact]
        public async Task GenerateAnonymizedReportAsync_WithAnalysisReport_AnonymizesAllStrings()
        {
            // Arrange
            var report = new ForensicAnalysisReport
            {
                ReportId = "report-016",
                AnalyzedEvidenceId = "evidence-016",
                Summary = "Findings from analysis.",
                AnalysisType = "Forensic Analysis"
            };

            // Act
            ForensicAnalysisReport anonymized = await _redactor.GenerateAnonymizedReportAsync(report);

            // Assert
            Assert.Equal("[REDACTED]", anonymized.Summary);
            Assert.Equal("[REDACTED]", anonymized.AnalysisType);
        }

        [Fact]
        public async Task GenerateAnonymizedReportAsync_WithSignalAnomalyReport_AnonymizesAllStrings()
        {
            // Arrange
            var report = new SignalAnomalyReport
            {
                ReportId = "report-017",
                TotalEventsAnalyzed = 200,
                RiskAssessment = "Critical risk detected",
                Recommendations = "Immediate action required"
            };

            // Act
            SignalAnomalyReport anonymized = await _redactor.GenerateAnonymizedReportAsync(report);

            // Assert
            Assert.Equal("[REDACTED]", anonymized.RiskAssessment);
            Assert.Equal("[REDACTED]", anonymized.Recommendations);
        }

        #endregion

        #region RedactReportsAsync Tests

        [Fact]
        public async Task RedactReportsAsync_WithEmptyCollection_ReturnsEmptyCollection()
        {
            // Arrange
            var reports = new List<ChainOfCustodyReport>();
            var options = new RedactionOptions();

            // Act
            IEnumerable<ChainOfCustodyReport> redacted = await _redactor.RedactReportsAsync(reports, options);

            // Assert
            Assert.Empty(redacted);
        }

        [Fact]
        public async Task RedactReportsAsync_WithMultipleChainOfCustodyReports_RedactsAll()
        {
            // Arrange
            var reports = new List<ChainOfCustodyReport>
            {
                new() {
                    ReportId = "report-018",
                    EvidenceId = "evidence-018",
                    SignedByOfficer = "Officer Daniel Taylor"
                },
                new() {
                    ReportId = "report-019",
                    EvidenceId = "evidence-019",
                    SignedByOfficer = "Officer Karen White"
                }
            };
            var options = new RedactionOptions
            {
                RedactHandlerNames = true
            };

            // Act
            var redacted = (await _redactor.RedactReportsAsync(reports, options)).ToList();

            // Assert
            Assert.Equal(2, redacted.Count);
            Assert.All(redacted, r => Assert.Equal("[REDACTED]", r.SignedByOfficer));
        }

        [Fact]
        public async Task RedactReportsAsync_WithMultipleValidationReports_RedactsAll()
        {
            // Arrange
            var reports = new List<EvidenceValidationReport>
            {
                new() {
                    ReportId = "report-020",
                    EvidenceId = "evidence-020",
                    ValidatedBy = "Tech Smith"
                },
                new() {
                    ReportId = "report-021",
                    EvidenceId = "evidence-021",
                    ValidatedBy = "Tech Jones"
                },
                new() {
                    ReportId = "report-022",
                    EvidenceId = "evidence-022",
                    ValidatedBy = "Tech Anderson"
                }
            };
            var options = new RedactionOptions
            {
                RedactHandlerNames = true
            };

            // Act
            var redacted = (await _redactor.RedactReportsAsync(reports, options)).ToList();

            // Assert
            Assert.Equal(3, redacted.Count);
            Assert.All(redacted, r => Assert.Equal("[REDACTED]", r.ValidatedBy));
        }

        [Fact]
        public async Task RedactReportsAsync_WithAnalysisReports_FullyAnonymizes()
        {
            // Arrange
            var reports = new List<ForensicAnalysisReport>
            {
                new() {
                    ReportId = "report-023",
                    AnalyzedEvidenceId = "evidence-023",
                    Summary = "Analysis 1"
                },
                new() {
                    ReportId = "report-024",
                    AnalyzedEvidenceId = "evidence-024",
                    Summary = "Analysis 2"
                }
            };
            var options = new RedactionOptions
            {
                FullyAnonymize = true
            };

            // Act
            var redacted = (await _redactor.RedactReportsAsync(reports, options)).ToList();

            // Assert
            Assert.Equal(2, redacted.Count);
            Assert.All(redacted, r => Assert.Equal("[REDACTED]", r.Summary));
        }

        #endregion

        #region RedactionOptions Variations Tests

        [Fact]
        public async Task RedactChainOfCustodyAsync_WithFullyAnonymizeOption_RedactsAllStrings()
        {
            // Arrange
            var report = new ChainOfCustodyReport
            {
                ReportId = "report-025",
                EvidenceId = "evidence-025",
                SignedByOfficer = "Officer Name",
                LegalStatement = "Some legal statement"
            };
            var options = new RedactionOptions
            {
                FullyAnonymize = true
            };

            // Act
            ChainOfCustodyReport redacted = await _redactor.RedactChainOfCustodyAsync(report, options);

            // Assert
            Assert.Equal("[REDACTED]", redacted.SignedByOfficer);
            Assert.Equal("[REDACTED]", redacted.LegalStatement);
        }

        [Fact]
        public async Task RedactValidationReportAsync_WithCustomReplacementToken_UsesToken()
        {
            // Arrange
            var report = new EvidenceValidationReport
            {
                ReportId = "report-026",
                EvidenceId = "evidence-026",
                ValidatedBy = "Tech Name",
                CertificationStatement = "Certified statement"
            };
            var options = new RedactionOptions
            {
                RedactHandlerNames = true,
                ReplacementString = "***CLASSIFIED***"
            };

            // Act
            EvidenceValidationReport redacted = await _redactor.RedactValidationReportAsync(report, options);

            // Assert
            Assert.Equal("***CLASSIFIED***", redacted.ValidatedBy);
        }

        [Fact]
        public async Task RedactAnalysisReportAsync_WithCustomReplacementToken_UsesToken()
        {
            // Arrange
            var report = new ForensicAnalysisReport
            {
                ReportId = "report-027",
                AnalyzedEvidenceId = "evidence-027",
                Summary = "Original summary"
            };
            var options = new RedactionOptions
            {
                RedactVictimName = true,
                ReplacementString = "***CLASSIFIED***"
            };

            // Act
            ForensicAnalysisReport redacted = await _redactor.RedactAnalysisReportAsync(report, options);

            // Assert
            Assert.Equal("***CLASSIFIED***", redacted.Summary);
        }

        #endregion
    }
}
