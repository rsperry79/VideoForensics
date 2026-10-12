using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class TwoFactorRoleRequirementRepositoryTests : RepositoryTestBase
    {
        private TwoFactorRoleRequirementRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new TwoFactorRoleRequirementRepository(Fixture.Factory, CreateLogger<TwoFactorRoleRequirementRepository>());
        }

        [Fact]
        public async Task GetAllAsync_WhenEmpty_ReturnsSynthesizedDefaults()
        {
            // Act: Get all requirements when table is empty
            IReadOnlyList<TwoFactorRoleRequirement> requirements = await _repository.GetAllAsync(CancellationToken.None);

            // Assert: Should return four synthesized rows (one per role) with RequireTwoFactor=true
            Assert.NotNull(requirements);
            Assert.Equal(4, requirements.Count);
            
            var readOnlyReq = requirements.FirstOrDefault(r => r.Role == OperatorRole.ReadOnly);
            Assert.NotNull(readOnlyReq);
            Assert.True(readOnlyReq.RequireTwoFactor);

            var reviewReq = requirements.FirstOrDefault(r => r.Role == OperatorRole.Review);
            Assert.NotNull(reviewReq);
            Assert.True(reviewReq.RequireTwoFactor);

            var adminReq = requirements.FirstOrDefault(r => r.Role == OperatorRole.Admin);
            Assert.NotNull(adminReq);
            Assert.True(adminReq.RequireTwoFactor);

            var superAdminReq = requirements.FirstOrDefault(r => r.Role == OperatorRole.SuperAdmin);
            Assert.NotNull(superAdminReq);
            Assert.True(superAdminReq.RequireTwoFactor);
        }

        [Fact]
        public async Task GetRequirementForRoleAsync_WhenEmpty_ReturnsTrueDefault()
        {
            // Act: Get requirement for a specific role when table is empty
            bool requirement = await _repository.GetRequirementForRoleAsync(OperatorRole.Admin, CancellationToken.None);

            // Assert: Should return true (secure-by-default)
            Assert.True(requirement);
        }

        [Fact]
        public async Task UpsertAsync_WhenEmpty_InsertsNewRow()
        {
            // Arrange: Create a requirement for Admin role with RequireTwoFactor=false
            var requirement = new TwoFactorRoleRequirement
            {
                Id = Guid.NewGuid(),
                Role = OperatorRole.Admin,
                RequireTwoFactor = false,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = null
            };

            // Act: Upsert the requirement
            await _repository.UpsertAsync(requirement, CancellationToken.None);

            // Assert: Verify it was persisted and GetRequirementForRoleAsync returns false
            bool result = await _repository.GetRequirementForRoleAsync(OperatorRole.Admin, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task UpsertAsync_WhenExists_UpdatesExistingRow()
        {
            // Arrange: Insert initial requirement for Review role with RequireTwoFactor=true
            var initialReq = new TwoFactorRoleRequirement
            {
                Id = Guid.NewGuid(),
                Role = OperatorRole.Review,
                RequireTwoFactor = true,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = null
            };
            await _repository.UpsertAsync(initialReq, CancellationToken.None);

            // Act: Upsert with updated value (RequireTwoFactor=false)
            var updatedReq = new TwoFactorRoleRequirement
            {
                Id = Guid.NewGuid(), // Different Id is okay - repository updates by role, not id
                Role = OperatorRole.Review,
                RequireTwoFactor = false,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = Guid.NewGuid()
            };
            await _repository.UpsertAsync(updatedReq, CancellationToken.None);

            // Assert: Verify it was updated (still one row, not two)
            IReadOnlyList<TwoFactorRoleRequirement> allReqs = await _repository.GetAllAsync(CancellationToken.None);
            var reviewRows = allReqs.Where(r => r.Role == OperatorRole.Review).ToList();
            Assert.Single(reviewRows); // Only one row for Review role
            Assert.False(reviewRows[0].RequireTwoFactor);
        }

        [Fact]
        public async Task GetRequirementForRoleAsync_WithPersistedValue_ReturnsPersistedValue()
        {
            // Arrange: Insert a requirement for SuperAdmin with RequireTwoFactor=false
            var requirement = new TwoFactorRoleRequirement
            {
                Id = Guid.NewGuid(),
                Role = OperatorRole.SuperAdmin,
                RequireTwoFactor = false,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByOperatorId = null
            };
            await _repository.UpsertAsync(requirement, CancellationToken.None);

            // Act: Get requirement for SuperAdmin
            bool result = await _repository.GetRequirementForRoleAsync(OperatorRole.SuperAdmin, CancellationToken.None);

            // Assert: Should return the persisted value (false), not the default (true)
            Assert.False(result);
        }
    }
}
