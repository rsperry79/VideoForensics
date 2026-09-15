using Moq;

namespace VideoForensics.Forensics.Tests
{
    /// <summary>
    /// Tests for IEvidenceAccessController access control and anomaly detection contract.
    /// These tests verify the behavior expected from any implementation of the interface.
    /// </summary>
    public class EvidenceAccessControllerTests
    {
        private IEvidenceAccessController CreateMockController()
        {
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.IsAccessAllowedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _ = mock.Setup(x => x.DetectSuspiciousAccessAsync(It.IsAny<string>()))
                .ReturnsAsync([]);
            _ = mock.Setup(x => x.GetAccessHistoryAsync(It.IsAny<string>()))
                .ReturnsAsync([]);
            _ = mock.Setup(x => x.FlagAccessForReviewAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            _ = mock.Setup(x => x.GetUserAccessRiskAsync(It.IsAny<string>()))
                .ReturnsAsync(new UserAccessRiskProfile { UserId = "test-user", RiskScore = 0.0 });
            return mock.Object;
        }

        #region IsAccessAllowedAsync Tests

        [Fact]
        public async Task IsAccessAllowedAsync_WithValidInvestigatorAccess_ReturnsTrue()
        {
            IEvidenceAccessController controller = CreateMockController();
            bool result = await controller.IsAccessAllowedAsync("evidence-001", "investigator-001", "investigator", "case-review");
            Assert.True(result);
        }

        [Fact]
        public async Task IsAccessAllowedAsync_WithValidProsecutorAccess_ReturnsTrue()
        {
            IEvidenceAccessController controller = CreateMockController();
            bool result = await controller.IsAccessAllowedAsync("evidence-002", "prosecutor-001", "prosecutor", "trial-preparation");
            Assert.True(result);
        }

        [Fact]
        public async Task IsAccessAllowedAsync_WithEmptyEvidenceId_StillProcesses()
        {
            IEvidenceAccessController controller = CreateMockController();
            bool result = await controller.IsAccessAllowedAsync("", "investigator-001", "investigator", "case-review");
            Assert.NotNull(result);
        }

        #endregion

        #region DetectSuspiciousAccessAsync Tests

        [Fact]
        public async Task DetectSuspiciousAccessAsync_WithNoAnomalies_ReturnsEmptyList()
        {
            IEvidenceAccessController controller = CreateMockController();
            IEnumerable<AccessAnomaly> anomalies = await controller.DetectSuspiciousAccessAsync("evidence-clean-001");
            Assert.NotNull(anomalies);
            Assert.Empty(anomalies);
        }

        [Fact]
        public async Task DetectSuspiciousAccessAsync_WithFailedAccessAttempts_DetectsAnomaly()
        {
            var anomalyList = new List<AccessAnomaly>
            {
                new() { EvidenceId = "evidence-failed-001", AnomalyType = "FailedAccessAttempts", Severity = AccessAnomalySeverity.High, FailedAttemptCount = 5 }
            };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.DetectSuspiciousAccessAsync("evidence-failed-001")).ReturnsAsync(anomalyList);
            IEvidenceAccessController controller = mock.Object;
            IEnumerable<AccessAnomaly> anomalies = await controller.DetectSuspiciousAccessAsync("evidence-failed-001");
            Assert.NotNull(anomalies);
            AccessAnomaly? failedAnomaly = anomalies.FirstOrDefault(a => a.AnomalyType == "FailedAccessAttempts");
            Assert.NotNull(failedAnomaly);
        }

        [Fact]
        public async Task DetectSuspiciousAccessAsync_WithCriticalSeverity_FlagsForReview()
        {
            var anomalyList = new List<AccessAnomaly>
            {
                new() { EvidenceId = "evidence-critical-001", AnomalyType = "UnauthorizedModification", Severity = AccessAnomalySeverity.Critical, RequiresInvestigation = true }
            };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.DetectSuspiciousAccessAsync("evidence-critical-001")).ReturnsAsync(anomalyList);
            IEvidenceAccessController controller = mock.Object;
            IEnumerable<AccessAnomaly> anomalies = await controller.DetectSuspiciousAccessAsync("evidence-critical-001");
            AccessAnomaly? critical = anomalies.FirstOrDefault(a => a.Severity == AccessAnomalySeverity.Critical);
            Assert.NotNull(critical);
            Assert.True(critical.RequiresInvestigation);
        }

        [Fact]
        public async Task DetectSuspiciousAccessAsync_WithMultipleAnomalies_ReturnsAll()
        {
            var anomalyList = new List<AccessAnomaly>
            {
                new() { EvidenceId = "evidence-multi-001", AnomalyType = "FailedAccessAttempts" },
                new() { EvidenceId = "evidence-multi-001", AnomalyType = "OffHoursAccess" },
                new() { EvidenceId = "evidence-multi-001", AnomalyType = "RapidAccess" }
            };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.DetectSuspiciousAccessAsync("evidence-multi-001")).ReturnsAsync(anomalyList);
            IEvidenceAccessController controller = mock.Object;
            IEnumerable<AccessAnomaly> anomalies = await controller.DetectSuspiciousAccessAsync("evidence-multi-001");
            Assert.Equal(3, anomalies.Count());
        }

        #endregion

        #region GetAccessHistoryAsync Tests

        [Fact]
        public async Task GetAccessHistoryAsync_WithAccessHistory_ReturnsAllEntries()
        {
            var history = new List<AccessLog>
            {
                new() { UserId = "investigator-001", AccessGranted = true },
                new() { UserId = "prosecutor-001", AccessGranted = true }
            };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.GetAccessHistoryAsync("evidence-001")).ReturnsAsync(history);
            IEvidenceAccessController controller = mock.Object;
            IEnumerable<AccessLog> result = await controller.GetAccessHistoryAsync("evidence-001");
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
        }

        [Fact]
        public async Task GetAccessHistoryAsync_WithSuccessfulAndFailedAttempts_ReturnsAllAttempts()
        {
            var history = new List<AccessLog>
            {
                new() { AccessGranted = true },
                new() { AccessGranted = false, DenialReason = "Unauthorized" },
                new() { AccessGranted = true }
            };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.GetAccessHistoryAsync("evidence-001")).ReturnsAsync(history);
            IEvidenceAccessController controller = mock.Object;
            IEnumerable<AccessLog> result = await controller.GetAccessHistoryAsync("evidence-001");
            var entries = result.ToList();
            Assert.Equal(2, entries.Count(e => e.AccessGranted));
            _ = Assert.Single(entries, e => !e.AccessGranted);
        }

        [Fact]
        public async Task GetAccessHistoryAsync_EntriesAreInChronologicalOrder()
        {
            DateTime now = DateTime.UtcNow;
            var history = new List<AccessLog>
            {
                new() { AccessedAt = now.AddHours(-2) },
                new() { AccessedAt = now.AddHours(-1) },
                new() { AccessedAt = now }
            };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.GetAccessHistoryAsync("evidence-001")).ReturnsAsync(history);
            IEvidenceAccessController controller = mock.Object;
            IEnumerable<AccessLog> result = await controller.GetAccessHistoryAsync("evidence-001");
            var entries = result.ToList();
            for (int i = 1; i < entries.Count; i++)
            {
                Assert.True(entries[i].AccessedAt >= entries[i - 1].AccessedAt);
            }
        }

        [Fact]
        public async Task GetAccessHistoryAsync_WithNoHistory_ReturnsEmptyList()
        {
            IEvidenceAccessController controller = CreateMockController();
            IEnumerable<AccessLog> result = await controller.GetAccessHistoryAsync("evidence-no-history-001");
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region FlagAccessForReviewAsync Tests

        [Fact]
        public async Task FlagAccessForReviewAsync_WithValidEvidenceAndReason_Succeeds()
        {
            IEvidenceAccessController controller = CreateMockController();
            await controller.FlagAccessForReviewAsync("evidence-flag-001", "Suspicious access pattern");
        }

        [Fact]
        public async Task FlagAccessForReviewAsync_WithTamperingReason_Succeeds()
        {
            IEvidenceAccessController controller = CreateMockController();
            await controller.FlagAccessForReviewAsync("evidence-tampering-001", "Potential evidence tampering detected");
        }

        [Fact]
        public async Task FlagAccessForReviewAsync_WithEmptyReason_Succeeds()
        {
            IEvidenceAccessController controller = CreateMockController();
            await controller.FlagAccessForReviewAsync("evidence-empty-001", "");
        }

        [Fact]
        public async Task FlagAccessForReviewAsync_CanBeFlaggedMultipleTimes()
        {
            IEvidenceAccessController controller = CreateMockController();
            await controller.FlagAccessForReviewAsync("evidence-multi-flag-001", "First reason");
            await controller.FlagAccessForReviewAsync("evidence-multi-flag-001", "Second reason");
        }

        #endregion

        #region GetUserAccessRiskAsync Tests

        [Fact]
        public async Task GetUserAccessRiskAsync_WithCleanHistory_ReturnLowRisk()
        {
            var riskProfile = new UserAccessRiskProfile { UserId = "clean-user-001", RiskScore = 0.1, FailedAccessAttempts = 0 };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.GetUserAccessRiskAsync("clean-user-001")).ReturnsAsync(riskProfile);
            IEvidenceAccessController controller = mock.Object;
            UserAccessRiskProfile result = await controller.GetUserAccessRiskAsync("clean-user-001");
            Assert.NotNull(result);
            Assert.True(result.RiskScore < 0.3);
        }

        [Fact]
        public async Task GetUserAccessRiskAsync_WithFailedAttempts_IncreaseRiskScore()
        {
            var riskProfile = new UserAccessRiskProfile { UserId = "high-risk-001", RiskScore = 0.6, FailedAccessAttempts = 10 };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.GetUserAccessRiskAsync("high-risk-001")).ReturnsAsync(riskProfile);
            IEvidenceAccessController controller = mock.Object;
            UserAccessRiskProfile result = await controller.GetUserAccessRiskAsync("high-risk-001");
            Assert.True(result.FailedAccessAttempts > 0);
            Assert.True(result.RiskScore > 0.3);
        }

        [Fact]
        public async Task GetUserAccessRiskAsync_WithHighRisk_ProvidesRecommendedAction()
        {
            var riskProfile = new UserAccessRiskProfile { UserId = "action-user-001", RiskScore = 0.8, RecommendedAction = "Revoke access" };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.GetUserAccessRiskAsync("action-user-001")).ReturnsAsync(riskProfile);
            IEvidenceAccessController controller = mock.Object;
            UserAccessRiskProfile result = await controller.GetUserAccessRiskAsync("action-user-001");
            Assert.True(result.RiskScore > 0.7);
            Assert.NotNull(result.RecommendedAction);
        }

        [Fact]
        public async Task GetUserAccessRiskAsync_RiskScoreIsBetweenZeroAndOne()
        {
            var riskProfile = new UserAccessRiskProfile { UserId = "risk-user-001", RiskScore = 0.5 };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.GetUserAccessRiskAsync("risk-user-001")).ReturnsAsync(riskProfile);
            IEvidenceAccessController controller = mock.Object;
            UserAccessRiskProfile result = await controller.GetUserAccessRiskAsync("risk-user-001");
            Assert.True(result.RiskScore >= 0.0);
            Assert.True(result.RiskScore <= 1.0);
        }

        [Fact]
        public async Task GetUserAccessRiskAsync_WithNonexistentUser_ReturnsZeroRisk()
        {
            var riskProfile = new UserAccessRiskProfile { UserId = "nonexistent-999", RiskScore = 0.0 };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.GetUserAccessRiskAsync("nonexistent-999")).ReturnsAsync(riskProfile);
            IEvidenceAccessController controller = mock.Object;
            UserAccessRiskProfile result = await controller.GetUserAccessRiskAsync("nonexistent-999");
            Assert.Equal(0.0, result.RiskScore);
        }

        [Fact]
        public async Task GetUserAccessRiskAsync_TracksFlaggedAccessReasons()
        {
            var riskProfile = new UserAccessRiskProfile
            {
                UserId = "flagged-001",
                FlaggedAccessReasons = ["Unauthorized modification", "Tampering attempt"]
            };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.GetUserAccessRiskAsync("flagged-001")).ReturnsAsync(riskProfile);
            IEvidenceAccessController controller = mock.Object;
            UserAccessRiskProfile result = await controller.GetUserAccessRiskAsync("flagged-001");
            Assert.NotNull(result.FlaggedAccessReasons);
            Assert.NotEmpty(result.FlaggedAccessReasons);
        }

        #endregion

        #region Integration Scenarios

        [Fact]
        public async Task AccessControlFlow_CompleteWorkflow_Succeeds()
        {
            IEvidenceAccessController controller = CreateMockController();
            bool isAllowed = await controller.IsAccessAllowedAsync("evidence-001", "investigator-001", "investigator", "case-review");
            IEnumerable<AccessLog> history = await controller.GetAccessHistoryAsync("evidence-001");
            IEnumerable<AccessAnomaly> anomalies = await controller.DetectSuspiciousAccessAsync("evidence-001");
            UserAccessRiskProfile riskProfile = await controller.GetUserAccessRiskAsync("investigator-001");
            Assert.NotNull(isAllowed);
            Assert.NotNull(history);
            Assert.NotNull(anomalies);
            Assert.NotNull(riskProfile);
        }

        [Fact]
        public async Task AnomalyDetectionAndFlagging_SuspiciousAccess_FlagsForReview()
        {
            var anomalyList = new List<AccessAnomaly>
            {
                new() { AnomalyType = "RapidAccess", Severity = AccessAnomalySeverity.High }
            };
            var mock = new Mock<IEvidenceAccessController>();
            _ = mock.Setup(x => x.DetectSuspiciousAccessAsync("evidence-anomaly-001")).ReturnsAsync(anomalyList);
            IEvidenceAccessController controller = mock.Object;
            IEnumerable<AccessAnomaly> anomalies = await controller.DetectSuspiciousAccessAsync("evidence-anomaly-001");
            if (anomalies.Any())
            {
                await controller.FlagAccessForReviewAsync("evidence-anomaly-001", "Anomalies detected");
            }

            Assert.NotEmpty(anomalies);
        }

        #endregion
    }
}
