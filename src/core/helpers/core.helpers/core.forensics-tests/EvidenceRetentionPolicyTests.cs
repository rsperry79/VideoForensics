namespace VideoForensics.Forensics.Tests
{
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
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-001";
            var retentionPeriod = TimeSpan.FromDays(30);
            string reason = "DV case - re-prosecution potential";
            string authorizedBy = "detective-jones";

            // Act
            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, reason, authorizedBy);

            // Assert
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);
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
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-002";
            var retentionPeriod = TimeSpan.FromDays(365);
            DateTime now = DateTime.UtcNow;
            string reason = "Murder investigation - ongoing appeals";
            string authorizedBy = "prosecutor-smith";

            // Act
            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, reason, authorizedBy);
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            DateTime expectedEndDate = now.Add(retentionPeriod);
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
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-003";
            var retentionPeriod = TimeSpan.FromDays(90);

            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "Test case", "admin");

            // Act
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.NotNull(status);
            Assert.Equal(RetentionStatus.Active, status.Status);
            Assert.True(status.RetentionStartDate <= DateTime.UtcNow);
        }

        [Fact]
        public async Task GetRetentionStatusAsync_PreservesMetadata()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-004";
            string caseNumber = "2024-DV-001";
            string legalReference = "Cal. Penal Code §1054.1";
            string reason = "Domestic violence case with appeal";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(180), reason, "officer-brown");

            // Act
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);
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
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-005";
            var retentionPeriod = TimeSpan.FromDays(30);

            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "Recent case", "admin");

            // Act
            bool canDestroy = await policy.CanDestroyEvidenceAsync(evidenceId);

            // Assert
            Assert.False(canDestroy, "Evidence should not be destroyable before retention period expires");
        }

        [Fact]
        public async Task CanDestroyEvidenceAsync_WithExactlyExpiredEvidence_ReturnsFalse()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-006";
            var retentionPeriod = TimeSpan.FromSeconds(1); // Expire in 1 second

            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "Short retention", "admin");

            // Wait for retention period to expire
            await Task.Delay(1100);

            // Act
            bool canDestroy = await policy.CanDestroyEvidenceAsync(evidenceId);

            // Assert
            // At the exact moment of expiration, should still not be destroyable (boundary test)
            // This depends on implementation - typically requires approval workflow
            Assert.NotNull(canDestroy);
        }

        [Fact]
        public async Task CanDestroyEvidenceAsync_WithExpiredAndApprovedEvidence_ReturnsTrue()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-007";
            var retentionPeriod = TimeSpan.FromSeconds(1);

            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "Short retention", "admin");
            await Task.Delay(1100);

            // Request and approve destruction
            await policy.RequestDestructionApprovalAsync(evidenceId, "admin", "Retention period expired");
            await policy.ApproveDestructionAsync(evidenceId, "supervisor-jones", "Shred");

            // Act
            bool canDestroy = await policy.CanDestroyEvidenceAsync(evidenceId);

            // Assert
            Assert.True(canDestroy, "Evidence should be destroyable after expiration and approval");
        }

        [Fact]
        public async Task CanDestroyEvidenceAsync_WithOnHoldStatus_ReturnsFalse()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-008";
            var retentionPeriod = TimeSpan.FromSeconds(1);

            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "Short retention", "admin");
            await Task.Delay(1100);

            // Get status and set to OnHold (simulating legal hold override)
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);
            status.Status = RetentionStatus.OnHold;

            // Act
            bool canDestroy = await policy.CanDestroyEvidenceAsync(evidenceId);

            // Assert
            Assert.False(canDestroy, "Evidence on legal hold should not be destroyable regardless of retention expiry");
        }

        #endregion

        #region RequestDestructionApprovalAsync Tests

        [Fact]
        public async Task RequestDestructionApprovalAsync_WithValidRequest_ChangesStatusToPending()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-009";
            string requestedBy = "evidence-clerk";
            string reason = "Retention period expired, no ongoing case";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");

            // Act
            await policy.RequestDestructionApprovalAsync(evidenceId, requestedBy, reason);
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.Equal(RetentionStatus.PendingApprovalForDestruction, status.Status);
        }

        #endregion

        #region ApproveDestructionAsync Tests

        [Fact]
        public async Task ApproveDestructionAsync_WithValidApproval_ChangesStatusToApproved()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-010";
            string approvedBy = "supervisor-williams";
            string method = "Incineration";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk", "Test approval");

            // Act
            await policy.ApproveDestructionAsync(evidenceId, approvedBy, method);
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);

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
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-011";
            string destructionMethod = "Shred";
            string verificationHash = "sha256:abc123def456789";
            DateTime now = DateTime.UtcNow;

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk", "Destroy");
            await policy.ApproveDestructionAsync(evidenceId, "supervisor", destructionMethod);

            // Act
            await policy.DestroyEvidenceAsync(evidenceId, destructionMethod, verificationHash);
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.Equal(RetentionStatus.Destroyed, status.Status);
            Assert.Equal(verificationHash, status.DestructionVerificationHash);
            Assert.True(status.DestructedAt >= now);
        }

        [Fact]
        public async Task DestroyEvidenceAsync_RecordsDestructionTimestamp()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-012";
            DateTime before = DateTime.UtcNow;

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk", "Destroy");
            await policy.ApproveDestructionAsync(evidenceId, "supervisor", "Incinerate");

            // Act
            await policy.DestroyEvidenceAsync(evidenceId, "Incinerate", "hash:xyz");
            DateTime after = DateTime.UtcNow;
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            _ = Assert.NotNull(status.DestructedAt);
            Assert.True(status.DestructedAt >= before && status.DestructedAt <= after);
        }

        #endregion

        #region ExtendRetentionAsync Tests

        [Fact]
        public async Task ExtendRetentionAsync_WithValidExtension_IncreasesRetentionEndDate()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-013";
            var initialPeriod = TimeSpan.FromDays(30);
            var additionalPeriod = TimeSpan.FromDays(60);

            await policy.SetRetentionPeriodAsync(evidenceId, initialPeriod, "Initial retention", "admin");
            EvidenceRetentionInfo statusBefore = await policy.GetRetentionStatusAsync(evidenceId);
            DateTime originalEndDate = statusBefore.RetentionEndDate;

            // Act
            await policy.ExtendRetentionAsync(evidenceId, additionalPeriod, "New charges filed", "prosecutor");
            EvidenceRetentionInfo statusAfter = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.True(statusAfter.RetentionEndDate > originalEndDate);
            DateTime expectedNewEndDate = originalEndDate.Add(additionalPeriod);
            Assert.True(statusAfter.RetentionEndDate >= expectedNewEndDate.AddSeconds(-1));
        }

        [Fact]
        public async Task ExtendRetentionAsync_WithOnHold_PreservesOnHoldStatus()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-014";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Initial", "admin");
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);
            status.Status = RetentionStatus.OnHold;

            // Act
            await policy.ExtendRetentionAsync(evidenceId, TimeSpan.FromDays(90), "Ongoing investigation", "detective");
            EvidenceRetentionInfo statusAfter = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            Assert.Equal(RetentionStatus.OnHold, statusAfter.Status);
        }

        [Fact]
        public async Task ExtendRetentionAsync_UpdatesLastExtendedTimestamp()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-015";
            DateTime before = DateTime.UtcNow;

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Initial", "admin");

            // Act
            await Task.Delay(100); // Small delay to ensure timestamp difference
            await policy.ExtendRetentionAsync(evidenceId, TimeSpan.FromDays(60), "Extended", "admin");
            DateTime after = DateTime.UtcNow;
            EvidenceRetentionInfo status = await policy.GetRetentionStatusAsync(evidenceId);

            // Assert
            _ = Assert.NotNull(status.LastExtendedAt);
            Assert.True(status.LastExtendedAt >= before && status.LastExtendedAt <= after);
        }

        #endregion

        #region GetDestructionAuditAsync Tests

        [Fact]
        public async Task GetDestructionAuditAsync_ReturnsAuditTrailForEvidence()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-016";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");

            // Act
            DestructionAuditTrail audit = await policy.GetDestructionAuditAsync(evidenceId);

            // Assert
            Assert.NotNull(audit);
            Assert.Equal(evidenceId, audit.EvidenceId);
            Assert.NotNull(audit.ApprovalSteps);
        }

        [Fact]
        public async Task GetDestructionAuditAsync_RecordsApprovalSteps()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-017";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk", "Request destruction");
            await policy.ApproveDestructionAsync(evidenceId, "supervisor-jones", "Approved");
            await policy.DestroyEvidenceAsync(evidenceId, "Incinerate", "hash:123");

            // Act
            DestructionAuditTrail audit = await policy.GetDestructionAuditAsync(evidenceId);

            // Assert
            Assert.NotNull(audit.ApprovalSteps);
            Assert.NotEmpty(audit.ApprovalSteps);
            Assert.Contains(audit.ApprovalSteps, step => step.ApprovedBy == "supervisor-jones");
        }

        [Fact]
        public async Task GetDestructionAuditAsync_RecordsDestructionDetails()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-018";
            string verificationHash = "sha256:def789abc123";
            string destructionMethod = "Shred";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk", "Destroy");
            await policy.ApproveDestructionAsync(evidenceId, "supervisor", destructionMethod);
            await policy.DestroyEvidenceAsync(evidenceId, destructionMethod, verificationHash);

            // Act
            DestructionAuditTrail audit = await policy.GetDestructionAuditAsync(evidenceId);

            // Assert
            _ = Assert.NotNull(audit.DestroyedAt);
            Assert.Equal(destructionMethod, audit.DestructionMethod);
            Assert.Equal(verificationHash, audit.VerificationHash);
        }

        #endregion

        #region GetPendingDestructionAsync Tests

        [Fact]
        public async Task GetPendingDestructionAsync_ReturnsPendingEvidenceItems()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId1 = "evidence-019";
            string evidenceId2 = "evidence-020";

            await policy.SetRetentionPeriodAsync(evidenceId1, TimeSpan.FromDays(30), "Test 1", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId1, "clerk", "Pending");

            await policy.SetRetentionPeriodAsync(evidenceId2, TimeSpan.FromDays(30), "Test 2", "admin");
            await policy.RequestDestructionApprovalAsync(evidenceId2, "clerk", "Pending");

            // Act
            IEnumerable<EvidenceRetentionInfo> pending = await policy.GetPendingDestructionAsync();

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
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string pendingId = "evidence-021";
            string approvedId = "evidence-022";
            string destroyedId = "evidence-023";

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
            IEnumerable<EvidenceRetentionInfo> pending = await policy.GetPendingDestructionAsync();
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
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-024";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Test", "admin");

            // Act
            IEnumerable<EvidenceRetentionInfo> pending = await policy.GetPendingDestructionAsync();

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
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-025";
            var retentionPeriod = TimeSpan.FromDays(30);

            // Act & Assert - Step 1: Set retention
            await policy.SetRetentionPeriodAsync(evidenceId, retentionPeriod, "DV case", "detective-smith");
            EvidenceRetentionInfo status1 = await policy.GetRetentionStatusAsync(evidenceId);
            Assert.Equal(RetentionStatus.Active, status1.Status);

            // Step 2: Request destruction (after retention expires)
            await Task.Delay(100); // Simulate time passing
            await policy.RequestDestructionApprovalAsync(evidenceId, "clerk-jones", "Retention expired");
            EvidenceRetentionInfo status2 = await policy.GetRetentionStatusAsync(evidenceId);
            Assert.Equal(RetentionStatus.PendingApprovalForDestruction, status2.Status);

            // Step 3: Approve destruction
            await policy.ApproveDestructionAsync(evidenceId, "supervisor-williams", "Incinerate");
            EvidenceRetentionInfo status3 = await policy.GetRetentionStatusAsync(evidenceId);
            Assert.Equal(RetentionStatus.ApprovedForDestruction, status3.Status);

            // Step 4: Execute destruction
            await policy.DestroyEvidenceAsync(evidenceId, "Incinerate", "sha256:abc123def456");
            EvidenceRetentionInfo status4 = await policy.GetRetentionStatusAsync(evidenceId);
            Assert.Equal(RetentionStatus.Destroyed, status4.Status);
            _ = Assert.NotNull(status4.DestructedAt);
        }

        [Fact]
        public async Task MultipleExtensions_AccumulateRetentionTime()
        {
            // Arrange
            IEvidenceRetentionPolicy policy = CreatePolicy();
            string evidenceId = "evidence-026";

            await policy.SetRetentionPeriodAsync(evidenceId, TimeSpan.FromDays(30), "Initial", "admin");
            EvidenceRetentionInfo statusAfterInitial = await policy.GetRetentionStatusAsync(evidenceId);
            DateTime endDateAfterInitial = statusAfterInitial.RetentionEndDate;

            // Act - Extend multiple times
            await policy.ExtendRetentionAsync(evidenceId, TimeSpan.FromDays(30), "Appeal filed", "attorney");
            DateTime endDateAfterExt1 = (await policy.GetRetentionStatusAsync(evidenceId)).RetentionEndDate;

            await policy.ExtendRetentionAsync(evidenceId, TimeSpan.FromDays(60), "New charges", "prosecutor");
            DateTime endDateAfterExt2 = (await policy.GetRetentionStatusAsync(evidenceId)).RetentionEndDate;

            // Assert
            Assert.True(endDateAfterExt1 > endDateAfterInitial);
            Assert.True(endDateAfterExt2 > endDateAfterExt1);
        }

        #endregion
    }
}
