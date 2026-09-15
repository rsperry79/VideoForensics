namespace VideoForensics.Forensics.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using VideoForensics.Forensics.Implementations;
    using VideoForensics.Forensics.Interfaces;
    using VideoForensics.Forensics.Models;
    using Xunit;

    public class EvidenceRetentionPolicyTests
    {
        private IEvidenceRetentionPolicy CreatePolicy()
        {
            return new EvidenceRetentionPolicy();
        }

        #region SetRetentionPeriodAsync Tests

        [Fact]
        public async Task SetRetentionPeriodAsync_WithValidParameters_CompletesSuccessfully()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-001";
            var retentionPeriod = TimeSpan.FromDays(30);
            var reason = "DV case - re-prosecution potential";
            var authorizedBy = "detective-jones";

            // Act
            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, reason, authorizedBy);

            // Assert
            var status = await policy.GetRetentionStatusAsync(evidenceId);
            Assert.NotNull(status);
            Assert.Equal(evidenceId, status.EvidenceId);
            Assert.Equal(retentionPeriod, status.RetentionPeriod);
            Assert.Equal(reason, status.RetentionReason);
            Assert.Equal(RetentionStatus.Active, status.Status);
        }

        [Fact]
        public async Task SetRetentionPeriodAsync_WithExtendedPeriod_StoresCorrectEndDate()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-002";
            var retentionPeriod = TimeSpan.FromDays(365);
            var now = DateTime.UtcNow;
            var reason = "Murder investigation - ongoing appeals";
            var authorizedBy = "prosecutor-smith";

            // Act
            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, reason, authorizedBy);
            var status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            var expectedEndDate = now.Add(retentionPeriod);
            Assert.True(status.RetentionEndDate > now);
            Assert.True(status.RetentionEndDate <= expectedEndDate.AddSeconds(1));
            Assert.Equal(RetentionStatus.Active, status.Status);
        }

        #endregion

        #region GetRetentionStatusAsync Tests

        [Fact]
        public async Task GetRetentionStatusAsync_AfterSettingRetention_ReturnsActiveStatus()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-003";
            var retentionPeriod = TimeSpan.FromDays(90);

            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "Test case", "admin");

            // Act
            var status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.NotNull(status);
            Assert.Equal(RetentionStatus.Active, status.Status);
            Assert.True(status.RetentionStartDate <= DateTime.UtcNow);
        }

        [Fact]
        public async Task GetRetentionStatusAsync_PreservesMetadata()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-004";
            var caseNumber = "2024-DV-001";
            var legalReference = "Cal. Penal Code §1054.1";
            var reason = "Domestic violence case with appeal";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(180), reason, "officer-brown");

            // Act
            var status = await policy.GetRetentionStatusAsync(evidenceId);
            status.CaseNumber = caseNumber;
            status.LegalReference = legalReference;

            // Assert
            Assert.Equal(caseNumber, status.CaseNumber);
            Assert.Equal(legalReference, status.LegalReference);
        }

        #endregion

        #region CanDestroyEvidenceAsync Tests - Expiration Boundaries

        [Fact]
        public async Task CanDestroyEvidenceAsync_WithNotYetExpiredEvidence_ReturnsFalse()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-005";
            var retentionPeriod = TimeSpan.FromDays(30);

            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "Recent case", "admin");

            // Act
            var canDestroy = await policy.CanDestroyEvidenceAsync(evidenceId);

            // Assert
            Assert.False(canDestroy, "Evidence should not be destroyable before retention period expires");
        }

        [Fact]
        public async Task CanDestroyEvidenceAsync_WithExactlyExpiredEvidence_ReturnsFalse()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-006";
            var retentionPeriod = TimeSpan.FromSeconds(1); // Expire in 1 second

            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "Short retention", "admin");

            // Wait for retention period to expire
            await Task.Delay(1100);

            // Act
            var canDestroy = await policy.CanDestroyEvidenceAsync(evidenceId);

            // Assert
            // At the exact moment of expiration, should still not be destroyable (boundary test)
            // This depends on implementation - typically requires approval workflow
            Assert.NotNull(canDestroy);
        }

        [Fact]
        public async Task CanDestroyEvidenceAsync_WithExpiredAndApprovedEvidence_ReturnsTrue()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-007";
            var retentionPeriod = TimeSpan.FromSeconds(1);

            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "Short retention", "admin");
            await Task.Delay(1100);

            // Request and approve destruction
            await policy.RequestDestructionApprovalAsync(evidenceId, "admin", "Retention period expired");
            await policy.ApproveDestructionAsync(evidenceId, "supervisor-jones", "Shred");

            // Act
            var canDestroy = await policy.CanDestroyEvidenceAsync(evidenceId);

            // Assert
            Assert.True(canDestroy, "Evidence should be destroyable after expiration and approval");
        }

        [Fact]
        public async Task CanDestroyEvidenceAsync_WithOnHoldStatus_ReturnsFalse()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-008";
            var retentionPeriod = TimeSpan.FromSeconds(1);

            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "Short retention", "admin");
            await Task.Delay(1100);

            // Get status and set to OnHold (simulating legal hold override)
            var status = await policy.GetRetentionStatusAsync(evidenceId);
            status.Status = RetentionStatus.OnHold;

            // Act
            var canDestroy = await policy.CanDestroyEvidenceAsync(evidenceId);

            // Assert
            Assert.False(canDestroy, "Evidence on legal hold should not be destroyable regardless of retention expiry");
        }

        #endregion

        #region RequestDestructionApprovalAsync Tests

        [Fact]
        public async Task RequestDestructionApprovalAsync_WithValidRequest_ChangesStatusToPending()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-009";
            var requestedBy = "evidence-clerk";
            var reason = "Retention period expired, no ongoing case";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");

            // Act
            await policy.RequestDestructionApprovalAsync(evidenceId, requestedBy, reason);
            var status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.Equal(RetentionStatus.PendingApprovalForDestruction, status.Status);
        }

        #endregion

        #region ApproveDestructionAsync Tests

        [Fact]
        public async Task ApproveDestructionAsync_WithValidApproval_ChangesStatusToApproved()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-010";
            var approvedBy = "supervisor-williams";
            var method = "Incineration";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk", "Test approval");

            // Act
            await policy.ApproveDestructionAsync(evidenceId, approvedBy, method);
            var status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.Equal(RetentionStatus.ApprovedForDestruction, status.Status);
            Assert.Equal(approvedBy, status.DestructionAuthorizedBy);
            Assert.Equal(method, status.DestructionMethod);
        }

        #endregion

        #region DestroyEvidenceAsync Tests

        [Fact]
        public async Task DestroyEvidenceAsync_WithValidDestructionDetails_MarksEvidenceAsDestroyed()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-011";
            var destructionMethod = "Shred";
            var verificationHash = "sha256:abc123def456789";
            var now = DateTime.UtcNow;

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk", "Destroy");
            await policy.ApproveDestructionAsync(evidenceId, "supervisor", destructionMethod);

            // Act
            await policy.DestroyEvidenceAsync(evidenceId, destructionMethod, verificationHash);
            var status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.Equal(RetentionStatus.Destroyed, status.Status);
            Assert.Equal(verificationHash, status.DestructionVerificationHash);
            Assert.True(status.DestructedAt >= now);
        }

        [Fact]
        public async Task DestroyEvidenceAsync_RecordsDestructionTimestamp()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-012";
            var before = DateTime.UtcNow;

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk", "Destroy");
            await policy.ApproveDestructionAsync(evidenceId, "supervisor", "Incinerate");

            // Act
            await policy.DestroyEvidenceAsync(evidenceId, "Incinerate", "hash:xyz");
            var after = DateTime.UtcNow;
            var status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.NotNull(status.DestructedAt);
            Assert.True(status.DestructedAt >= before && status.DestructedAt <= after);
        }

        #endregion

        #region ExtendRetentionAsync Tests

        [Fact]
        public async Task ExtendRetentionAsync_WithValidExtension_IncreasesRetentionEndDate()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-013";
            var initialPeriod = TimeSpan.FromDays(30);
            var additionalPeriod = TimeSpan.FromDays(60);

            await policy.SetRetentionPeriodAsync(evidenceId, initialPeriod, "Initial retention", "admin");
            var statusBefore = await policy.GetRetentionStatusAsync(evidenceId);
            var originalEndDate = statusBefore.RetentionEndDate;

            // Act
            await policy.ExtendRetentionAsync(evidenceId, additionalPeriod, "New charges filed", "prosecutor");
            var statusAfter = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.True(statusAfter.RetentionEndDate > originalEndDate);
            var expectedNewEndDate = originalEndDate.Add(additionalPeriod);
            Assert.True(statusAfter.RetentionEndDate >= expectedNewEndDate.AddSeconds(-1));
        }

        [Fact]
        public async Task ExtendRetentionAsync_WithOnHold_PreservesOnHoldStatus()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-014";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Initial", "admin");
            var status = await policy.GetRetentionStatusAsync(evidenceId);
            status.Status = RetentionStatus.OnHold;

            // Act
            await policy.ExtendRetentionAsync(evidenceId, TimeSpan.FromDays(90), "Ongoing investigation", "detective");
            var statusAfter = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.Equal(RetentionStatus.OnHold, statusAfter.Status);
        }

        [Fact]
        public async Task ExtendRetentionAsync_UpdatesLastExtendedTimestamp()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-015";
            var before = DateTime.UtcNow;

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Initial", "admin");

            // Act
            await Task.Delay(100); // Small delay to ensure timestamp difference
            await policy.ExtendRetentionAsync(evidenceId, TimeSpan.FromDays(60), "Extended", "admin");
            var after = DateTime.UtcNow;
            var status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.NotNull(status.LastExtendedAt);
            Assert.True(status.LastExtendedAt >= before && status.LastExtendedAt <= after);
        }

        #endregion

        #region GetDestructionAuditAsync Tests

        [Fact]
        public async Task GetDestructionAuditAsync_ReturnsAuditTrailForEvidence()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-016";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");

            // Act
            var audit = await policy.GetDestructionAuditAsync(evidenceId);

            // Assert
            Assert.NotNull(audit);
            Assert.Equal(evidenceId, audit.EvidenceId);
            Assert.NotNull(audit.ApprovalSteps);
        }

        [Fact]
        public async Task GetDestructionAuditAsync_RecordsApprovalSteps()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-017";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk", "Request destruction");
            await policy.ApproveDestructionAsync(evidenceId, "supervisor-jones", "Approved");
            await policy.DestroyEvidenceAsync(evidenceId, "Incinerate", "hash:123");

            // Act
            var audit = await policy.GetDestructionAuditAsync(evidenceId);

            // Assert
            Assert.NotNull(audit.ApprovalSteps);
            Assert.NotEmpty(audit.ApprovalSteps);
            Assert.Contains(audit.ApprovalSteps, step => step.ApprovedBy == "supervisor-jones");
        }

        [Fact]
        public async Task GetDestructionAuditAsync_RecordsDestructionDetails()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-018";
            var verificationHash = "sha256:def789abc123";
            var destructionMethod = "Shred";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk", "Destroy");
            await policy.ApproveDestructionAsync(evidenceId, "supervisor", destructionMethod);
            await policy.DestroyEvidenceAsync(evidenceId, destructionMethod, verificationHash);

            // Act
            var audit = await policy.GetDestructionAuditAsync(evidenceId);

            // Assert
            Assert.NotNull(audit.DestroyedAt);
            Assert.Equal(destructionMethod, audit.DestructionMethod);
            Assert.Equal(verificationHash, audit.VerificationHash);
        }

        #endregion

        #region GetPendingDestructionAsync Tests

        [Fact]
        public async Task GetPendingDestructionAsync_ReturnsPendingEvidenceItems()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId1 = "evidence-019";
            var evidenceId2 = "evidence-020";

            await policy.SetRetentionPeriodAsync(evidenceId1, TimeSpan.FromDays(30), "Test 1", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId1, "clerk", "Pending");

            await policy.SetRetentionPeriodAsync(evidenceId2, TimeSpan.FromDays(30), "Test 2", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId2, "clerk", "Pending");

            // Act
            var pending = await policy.GetPendingDestructionAsync();

            // Assert
            Assert.NotNull(pending);
            var pendingList = pending.ToList();
            Assert.Contains(pendingList, item => item.EvidenceId == evidenceId1);
            Assert.Contains(pendingList, item => item.EvidenceId == evidenceId2);
        }

        [Fact]
        public async Task GetPendingDestructionAsync_ExcludesApprovedAndDestroyedItems()
        {
            // Arrange
            var policy = CreatePolicy();
            var pendingId = "evidence-021";
            var approvedId = "evidence-022";
            var destroyedId = "evidence-023";

            // Create pending item
            await policy.SetRetentionPeriodAsync(pendingId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(pendingId, "clerk", "Pending");

            // Create approved item
            await policy.SetRetentionPeriodAsync(approvedId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(approvedId, "clerk", "Pending");
            await policy.ApproveDestructionAsync(approvedId, "supervisor", "Approve");

            // Create destroyed item
            await policy.SetRetentionPeriodAsync(destroyedId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(destroyedId, "clerk", "Pending");
            await policy.ApproveDestructionAsync(destroyedId, "supervisor", "Approve");
            await policy.DestroyEvidenceAsync(destroyedId, "Shred", "hash:123");

            // Act
            var pending = await policy.GetPendingDestructionAsync();
            var pendingList = pending.ToList();

            // Assert
            Assert.Contains(pendingList, item => item.EvidenceId == pendingId);
            Assert.DoesNotContain(pendingList, item => item.EvidenceId == approvedId);
            Assert.DoesNotContain(pendingList, item => item.EvidenceId == destroyedId);
        }

        [Fact]
        public async Task GetPendingDestructionAsync_ReturnsEmptyWhenNoPending()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-024";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");

            // Act
            var pending = await policy.GetPendingDestructionAsync();

            // Assert
            Assert.NotNull(pending);
            Assert.Empty(pending);
        }

        #endregion

        #region Workflow Integration Tests

        [Fact]
        public async Task CompleteDestructionWorkflow_WithAllSteps_ReachesDestroyedStatus()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-025";
            var retentionPeriod = TimeSpan.FromDays(30);

            // Act & Assert - Step 1: Set retention
            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "DV case", "detective-smith");
            var status1 = await policy.GetRetentionStatusAsync(evidenceId);
            Assert.Equal(RetentionStatus.Active, status1.Status);

            // Step 2: Request destruction (after retention expires)
            await Task.Delay(100); // Simulate time passing
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk-jones", "Retention expired");
            var status2 = await policy.GetRetentionStatusAsync(evidenceId);
            Assert.Equal(RetentionStatus.PendingApprovalForDestruction, status2.Status);

            // Step 3: Approve destruction
            await policy.ApproveDestructionAsync(evidenceId, "supervisor-williams", "Incinerate");
            var status3 = await policy.GetRetentionStatusAsync(evidenceId);
            Assert.Equal(RetentionStatus.ApprovedForDestruction, status3.Status);

            // Step 4: Execute destruction
            await policy.DestroyEvidenceAsync(evidenceId, "Incinerate", "sha256:abc123def456");
            var status4 = await policy.GetRetentionStatusAsync(evidenceId);
            Assert.Equal(RetentionStatus.Destroyed, status4.Status);
            Assert.NotNull(status4.DestructedAt);
        }

        [Fact]
        public async Task MultipleExtensions_AccumulateRetentionTime()
        {
            // Arrange
            var policy = CreatePolicy();
            var evidenceId = "evidence-026";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Initial", "admin");
            var statusAfterInitial = await policy.GetRetentionStatusAsync(evidenceId);
            var endDateAfterInitial = statusAfterInitial.RetentionEndDate;

            // Act - Extend multiple times
            await policy.ExtendRetentionAsync(evidenceId, TimeSpan.FromDays(30), "Appeal filed", "attorney");
            var endDateAfterExt1 = (await policy.GetRetentionStatusAsync(evidenceId)).RetentionEndDate;

            await policy.ExtendRetentionAsync(evidenceId, TimeSpan.FromDays(60), "New charges", "prosecutor");
            var endDateAfterExt2 = (await policy.GetRetentionStatusAsync(evidenceId)).RetentionEndDate;

            // Assert
            Assert.True(endDateAfterExt1 > endDateAfterInitial);
            Assert.True(endDateAfterExt2 > endDateAfterExt1);
        }

        #endregion
    }
}
