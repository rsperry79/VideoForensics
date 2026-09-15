using System;
using VideoForensics.Forensics.Implementations;
using VideoForensics.Forensics.Interfaces;
using VideoForensics.Forensics.Models;

namespace VideoForensics.Forensics.Tests
{
    public class ChainOfCustodyLoggerTests
    {
        private readonly IChainOfCustodyLogger _logger;

        public ChainOfCustodyLoggerTests()
        {
            _logger = new ChainOfCustodyLogger();
        }

        #region LogEvidenceReceptionAsync Tests

        [Fact]
        public async Task LogEvidenceReceptionAsync_WithValidData_Succeeds()
        {
            // Arrange
            var evidenceId = "evidence-123";
            var handler = "officer-001";

            // Act
            await _logger.LogEvidenceReceptionAsync(evidenceId, handler);

            // Assert
            var custody = await _logger.GetChainOfCustodyAsync(evidenceId);
            Assert.NotNull(custody);
            var entries = custody.ToList();
            Assert.Single(entries);
            Assert.Equal(evidenceId, entries[0].EvidenceId);
            Assert.Equal(handler, entries[0].Handler);
            Assert.Equal("reception", entries[0].Action);
        }

        [Fact]
        public async Task LogEvidenceReceptionAsync_MultipleHandlers_CreatesMultipleEntries()
        {
            // Arrange
            var evidenceId = "evidence-456";
            var handler1 = "officer-001";
            var handler2 = "officer-002";

            // Act
            await _logger.LogEvidenceReceptionAsync(evidenceId, handler1);
            await _logger.LogEvidenceReceptionAsync(evidenceId, handler2);

            // Assert
            var custody = await _logger.GetChainOfCustodyAsync(evidenceId);
            var entries = custody.ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal(handler1, entries[0].Handler);
            Assert.Equal(handler2, entries[1].Handler);
        }

        [Fact]
        public async Task LogEvidenceReceptionAsync_WithNullEvidenceId_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.LogEvidenceReceptionAsync(null, "officer-001"));
        }

        [Fact]
        public async Task LogEvidenceReceptionAsync_WithEmptyEvidenceId_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.LogEvidenceReceptionAsync(string.Empty, "officer-001"));
        }

        [Fact]
        public async Task LogEvidenceReceptionAsync_WithNullHandler_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.LogEvidenceReceptionAsync("evidence-123", null));
        }

        [Fact]
        public async Task LogEvidenceReceptionAsync_WithEmptyHandler_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.LogEvidenceReceptionAsync("evidence-123", string.Empty));
        }

        #endregion

        #region LogCustodyTransferAsync Tests

        [Fact]
        public async Task LogCustodyTransferAsync_BetweenHandlers_CreatesTransferEntry()
        {
            // Arrange
            var evidenceId = "evidence-456";
            var fromHandler = "officer-001";
            var toHandler = "lab-tech-002";

            // Act
            await _logger.LogCustodyTransferAsync(evidenceId, fromHandler, toHandler);

            // Assert
            var custody = await _logger.GetChainOfCustodyAsync(evidenceId);
            var entries = custody.ToList();
            Assert.Single(entries);
            Assert.Equal(toHandler, entries[0].Handler);
            Assert.Contains("transfer", entries[0].Action);
            Assert.Contains(fromHandler, entries[0].Action);
        }

        [Fact]
        public async Task LogCustodyTransferAsync_MultipleTransfers_MaintainsChain()
        {
            // Arrange
            var evidenceId = "evidence-transfer-chain";
            var officer = "officer-001";
            var labTech = "lab-tech-002";
            var prosecutor = "prosecutor-003";

            // Act
            await _logger.LogCustodyTransferAsync(evidenceId, officer, labTech);
            await _logger.LogCustodyTransferAsync(evidenceId, labTech, prosecutor);

            // Assert
            var custody = await _logger.GetChainOfCustodyAsync(evidenceId);
            var entries = custody.ToList();
            Assert.Equal(2, entries.Count);
            Assert.Equal(labTech, entries[0].Handler);
            Assert.Equal(prosecutor, entries[1].Handler);
        }

        [Fact]
        public async Task LogCustodyTransferAsync_WithNullEvidenceId_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.LogCustodyTransferAsync(null, "officer", "lab-tech"));
        }

        [Fact]
        public async Task LogCustodyTransferAsync_WithNullFromHandler_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.LogCustodyTransferAsync("evidence-123", null, "lab-tech"));
        }

        [Fact]
        public async Task LogCustodyTransferAsync_WithNullToHandler_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.LogCustodyTransferAsync("evidence-123", "officer", null));
        }

        #endregion

        #region LogEvidenceAccessAsync Tests

        [Fact]
        public async Task LogEvidenceAccessAsync_WithValidData_CreatesAccessEntry()
        {
            // Arrange
            var evidenceId = "evidence-789";
            var action = "analyzed";
            var handler = "lab-tech-002";

            // Act
            await _logger.LogEvidenceAccessAsync(evidenceId, action, handler);

            // Assert
            var custody = await _logger.GetChainOfCustodyAsync(evidenceId);
            var entries = custody.ToList();
            Assert.Single(entries);
            Assert.Equal(action, entries[0].Action);
            Assert.Equal(handler, entries[0].Handler);
        }

        [Fact]
        public async Task LogEvidenceAccessAsync_MultipleAccesses_RecordsAll()
        {
            // Arrange
            var evidenceId = "evidence-access-multi";

            // Act
            await _logger.LogEvidenceAccessAsync(evidenceId, "examined", "tech-001");
            await _logger.LogEvidenceAccessAsync(evidenceId, "photographed", "tech-002");
            await _logger.LogEvidenceAccessAsync(evidenceId, "archived", "tech-003");

            // Assert
            var custody = await _logger.GetChainOfCustodyAsync(evidenceId);
            var entries = custody.ToList();
            Assert.Equal(3, entries.Count);
            Assert.Equal("examined", entries[0].Action);
            Assert.Equal("photographed", entries[1].Action);
            Assert.Equal("archived", entries[2].Action);
        }

        [Fact]
        public async Task LogEvidenceAccessAsync_WithNullAction_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.LogEvidenceAccessAsync("evidence-123", null, "tech-001"));
        }

        [Fact]
        public async Task LogEvidenceAccessAsync_WithEmptyAction_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.LogEvidenceAccessAsync("evidence-123", string.Empty, "tech-001"));
        }

        #endregion

        #region GetChainOfCustodyAsync Tests

        [Fact]
        public async Task GetChainOfCustodyAsync_WithNonExistentEvidence_ReturnsEmptyEnumerable()
        {
            // Arrange
            var evidenceId = "non-existent-evidence";

            // Act
            var custody = await _logger.GetChainOfCustodyAsync(evidenceId);

            // Assert
            Assert.NotNull(custody);
            Assert.Empty(custody);
        }

        [Fact]
        public async Task GetChainOfCustodyAsync_ReturnsChronologicalOrder()
        {
            // Arrange
            var evidenceId = "evidence-chrono";
            var handler1 = "officer-001";
            var handler2 = "lab-tech-002";
            var handler3 = "prosecutor-003";

            // Act
            await _logger.LogEvidenceReceptionAsync(evidenceId, handler1);
            // Small delay to ensure different timestamps
            await Task.Delay(10);
            await _logger.LogCustodyTransferAsync(evidenceId, handler1, handler2);
            await Task.Delay(10);
            await _logger.LogCustodyTransferAsync(evidenceId, handler2, handler3);

            // Assert
            var custody = await _logger.GetChainOfCustodyAsync(evidenceId);
            var entries = custody.ToList();
            Assert.Equal(3, entries.Count);
            for (int i = 1; i < entries.Count; i++)
            {
                Assert.True(entries[i].Timestamp >= entries[i - 1].Timestamp,
                    $"Entry {i} timestamp {entries[i].Timestamp} is before entry {i - 1} timestamp {entries[i - 1].Timestamp}");
            }
        }

        [Fact]
        public async Task GetChainOfCustodyAsync_WithNullEvidenceId_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.GetChainOfCustodyAsync(null));
        }

        [Fact]
        public async Task GetChainOfCustodyAsync_WithEmptyEvidenceId_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.GetChainOfCustodyAsync(string.Empty));
        }

        #endregion

        #region VerifyCustodyIntegrityAsync Tests

        [Fact]
        public async Task VerifyCustodyIntegrityAsync_WithNonExistentEvidence_ReturnsFalse()
        {
            // Arrange
            var evidenceId = "non-existent";

            // Act
            var isValid = await _logger.VerifyCustodyIntegrityAsync(evidenceId);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task VerifyCustodyIntegrityAsync_WithUnbrokenChain_ReturnsTrue()
        {
            // Arrange
            var evidenceId = "evidence-unbroken";
            await _logger.LogEvidenceReceptionAsync(evidenceId, "officer-001");
            await _logger.LogCustodyTransferAsync(evidenceId, "officer-001", "lab-tech-002");

            // Act
            var isValid = await _logger.VerifyCustodyIntegrityAsync(evidenceId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task VerifyCustodyIntegrityAsync_WithMultipleValidTransfers_ReturnsTrue()
        {
            // Arrange
            var evidenceId = "evidence-multi-transfer";
            await _logger.LogEvidenceReceptionAsync(evidenceId, "officer-001");
            await Task.Delay(10);
            await _logger.LogCustodyTransferAsync(evidenceId, "officer-001", "lab-tech-002");
            await Task.Delay(10);
            await _logger.LogCustodyTransferAsync(evidenceId, "lab-tech-002", "prosecutor-003");

            // Act
            var isValid = await _logger.VerifyCustodyIntegrityAsync(evidenceId);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task VerifyCustodyIntegrityAsync_WithNullEvidenceId_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.VerifyCustodyIntegrityAsync(null));
        }

        #endregion

        #region GetCustodyReportAsync Tests

        [Fact]
        public async Task GetCustodyReportAsync_WithValidChain_ReturnsCompleteReport()
        {
            // Arrange
            var evidenceId = "evidence-report";
            await _logger.LogEvidenceReceptionAsync(evidenceId, "officer-001");
            await _logger.LogCustodyTransferAsync(evidenceId, "officer-001", "lab-tech-002");

            // Act
            var report = await _logger.GetCustodyReportAsync(evidenceId);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(evidenceId, report.EvidenceId);
            Assert.NotEmpty(report.CustodyHistory);
            Assert.Equal(2, report.TotalAccessCount);
        }

        [Fact]
        public async Task GetCustodyReportAsync_AggregatesHandlersCorrectly()
        {
            // Arrange
            var evidenceId = "evidence-handlers";
            await _logger.LogEvidenceReceptionAsync(evidenceId, "officer-001");
            await _logger.LogCustodyTransferAsync(evidenceId, "officer-001", "lab-tech-002");
            await _logger.LogEvidenceAccessAsync(evidenceId, "analyzed", "lab-tech-002");
            await _logger.LogCustodyTransferAsync(evidenceId, "lab-tech-002", "prosecutor-003");

            // Act
            var report = await _logger.GetCustodyReportAsync(evidenceId);

            // Assert
            Assert.Equal(3, report.TotalHandlers); // officer-001, lab-tech-002, prosecutor-003
            Assert.Equal(4, report.TotalAccessCount);
        }

        [Fact]
        public async Task GetCustodyReportAsync_SetsInitialAndLastAccessTimes()
        {
            // Arrange
            var evidenceId = "evidence-timestamps";
            var beforeLogging = DateTime.UtcNow;
            await _logger.LogEvidenceReceptionAsync(evidenceId, "officer-001");
            await Task.Delay(10);
            await _logger.LogEvidenceAccessAsync(evidenceId, "analyzed", "lab-tech-002");
            var afterLogging = DateTime.UtcNow;

            // Act
            var report = await _logger.GetCustodyReportAsync(evidenceId);

            // Assert
            Assert.NotNull(report.EvidenceInitiallyReceived);
            Assert.NotNull(report.EvidenceLastAccessed);
            Assert.True(report.EvidenceInitiallyReceived >= beforeLogging);
            Assert.True(report.EvidenceLastAccessed <= afterLogging);
            Assert.True(report.EvidenceLastAccessed >= report.EvidenceInitiallyReceived);
        }

        [Fact]
        public async Task GetCustodyReportAsync_WithUnbrokenChain_SetsCustodyIntegrityVerifiedTrue()
        {
            // Arrange
            var evidenceId = "evidence-integrity";
            await _logger.LogEvidenceReceptionAsync(evidenceId, "officer-001");
            await _logger.LogCustodyTransferAsync(evidenceId, "officer-001", "lab-tech-002");

            // Act
            var report = await _logger.GetCustodyReportAsync(evidenceId);

            // Assert
            Assert.True(report.CustodyIntegrityVerified);
        }

        [Fact]
        public async Task GetCustodyReportAsync_WithNonExistentEvidence_ReturnEmptyReport()
        {
            // Arrange
            var evidenceId = "non-existent";

            // Act
            var report = await _logger.GetCustodyReportAsync(evidenceId);

            // Assert
            Assert.NotNull(report);
            Assert.Equal(evidenceId, report.EvidenceId);
            Assert.Empty(report.CustodyHistory);
            Assert.False(report.CustodyIntegrityVerified);
            Assert.Equal(0, report.TotalAccessCount);
        }

        [Fact]
        public async Task GetCustodyReportAsync_WithNullEvidenceId_ThrowsArgumentException()
        {
            // Arrange & Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _logger.GetCustodyReportAsync(null));
        }

        #endregion

        #region Complex Scenario Tests

        [Fact]
        public async Task ComplexScenario_FullChainOfCustody_MaintainsIntegrity()
        {
            // Arrange
            var evidenceId = "evidence-full-scenario";

            // Act - Simulate complete chain of custody
            await _logger.LogEvidenceReceptionAsync(evidenceId, "officer-john-doe");
            await Task.Delay(5);
            await _logger.LogCustodyTransferAsync(evidenceId, "officer-john-doe", "lab-tech-jane-smith");
            await Task.Delay(5);
            await _logger.LogEvidenceAccessAsync(evidenceId, "examined", "lab-tech-jane-smith");
            await Task.Delay(5);
            await _logger.LogEvidenceAccessAsync(evidenceId, "photographed", "lab-tech-jane-smith");
            await Task.Delay(5);
            await _logger.LogCustodyTransferAsync(evidenceId, "lab-tech-jane-smith", "prosecutor-bob-wilson");
            await Task.Delay(5);
            await _logger.LogEvidenceAccessAsync(evidenceId, "reviewed", "prosecutor-bob-wilson");

            // Assert - Verify chain
            var custody = await _logger.GetChainOfCustodyAsync(evidenceId);
            var entries = custody.ToList();
            Assert.Equal(6, entries.Count);

            var report = await _logger.GetCustodyReportAsync(evidenceId);
            Assert.True(report.CustodyIntegrityVerified);
            Assert.Equal(3, report.TotalHandlers);
            Assert.Equal(6, report.TotalAccessCount);
            Assert.NotNull(report.EvidenceInitiallyReceived);
            Assert.NotNull(report.EvidenceLastAccessed);
        }

        [Fact]
        public async Task MultipleEvidenceIds_MaintainsSeparateChains()
        {
            // Arrange
            var evidence1 = "evidence-001";
            var evidence2 = "evidence-002";

            // Act
            await _logger.LogEvidenceReceptionAsync(evidence1, "officer-001");
            await _logger.LogEvidenceReceptionAsync(evidence2, "officer-002");
            await _logger.LogCustodyTransferAsync(evidence1, "officer-001", "lab-tech-001");
            await _logger.LogCustodyTransferAsync(evidence2, "officer-002", "lab-tech-002");

            // Assert
            var custody1 = await _logger.GetChainOfCustodyAsync(evidence1);
            var custody2 = await _logger.GetChainOfCustodyAsync(evidence2);
            var entries1 = custody1.ToList();
            var entries2 = custody2.ToList();

            Assert.Equal(2, entries1.Count);
            Assert.Equal(2, entries2.Count);
            Assert.Equal("officer-001", entries1[0].Handler);
            Assert.Equal("officer-002", entries2[0].Handler);
            Assert.Equal("lab-tech-001", entries1[1].Handler);
            Assert.Equal("lab-tech-002", entries2[1].Handler);
        }

        #endregion
    }
}
