using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Sqlite.DependencyInjection;

using Xunit;

namespace VideoForensics.Data.Database.Sqlite.Tests
{
    /// <summary>Tests for Phase 0 security hardening data layer (Operator extensions, LockoutPolicySettings, TwoFactorRoleRequirement, BannedIpRange).</summary>
    public class SecurityHardeningPhase0Tests
    {
        private string GetTempDbPath()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(tempDir);
            return Path.Combine(tempDir, "test.db");
        }

        /// <summary>Verifies Operator security properties have correct defaults.</summary>
        [Fact]
        public async Task Operator_SecurityProperties_HaveCorrectDefaults()
        {
            // Arrange
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var services = new ServiceCollection();
                _ = services.AddVideoForensicsSqlite(dbPath);

                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();

                // Initialize database
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    await context.Database.MigrateAsync();
                }

                // Act - Create an operator and verify security property defaults
                var operatorId = Guid.NewGuid();
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    context.Operators.Add(new Operator
                    {
                        Id = operatorId,
                        DisplayName = "Test Operator",
                        Username = "testoper",
                        Email = "test@example.com",
                        FirstName = "Test",
                        LastName = "Operator",
                        CreatedAtUtc = DateTime.UtcNow,
                        SecurityStamp = Guid.NewGuid(),
                        Role = OperatorRole.ReadOnly,
                        Active = true,
                        IsApproved = true
                    });
                    await context.SaveChangesAsync();
                }

                // Assert - Retrieve and verify defaults
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    Operator? op = await context.Operators.FirstOrDefaultAsync(o => o.Id == operatorId);
                    Assert.NotNull(op);
                    Assert.False(op.IsPrimarySuperAdmin, "IsPrimarySuperAdmin should default to false");
                    Assert.Equal(0, op.FailedLoginAttemptCount);
                    Assert.Null(op.LockedOutUntilUtc);
                    Assert.Equal(TwoFactorRequirementOverride.Inherit, op.TwoFactorRequirementOverride);
                }

                await provider.DisposeAsync();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        System.GC.Collect();
                        System.GC.WaitForPendingFinalizers();
                        try
                        { Directory.Delete(tempDir, recursive: true); }
                        catch { }
                    }
                }
            }
        }

        /// <summary>Verifies LockoutPolicySettings can be inserted and retrieved.</summary>
        [Fact]
        public async Task LockoutPolicySettings_CanInsertAndRetrieve()
        {
            // Arrange
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var services = new ServiceCollection();
                _ = services.AddVideoForensicsSqlite(dbPath);

                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();

                // Initialize database
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    await context.Database.MigrateAsync();
                }

                // Act - Create a LockoutPolicySettings row
                var settingsId = Guid.NewGuid();
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    context.LockoutPolicySettings.Add(new LockoutPolicySettings
                    {
                        Id = settingsId,
                        MaxFailedAttempts = 5,
                        LockoutDurationMinutes = 15,
                        BlockedCountryCodes = "KP,IR",
                        FailClosedOnLookupError = false,
                        UpdatedAtUtc = DateTime.UtcNow,
                        UpdatedByOperatorId = null
                    });
                    await context.SaveChangesAsync();
                }

                // Assert - Retrieve and verify
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    LockoutPolicySettings? settings = await context.LockoutPolicySettings.FirstOrDefaultAsync(s => s.Id == settingsId);
                    Assert.NotNull(settings);
                    Assert.Equal(5, settings.MaxFailedAttempts);
                    Assert.Equal(15, settings.LockoutDurationMinutes);
                    Assert.Equal("KP,IR", settings.BlockedCountryCodes);
                    Assert.False(settings.FailClosedOnLookupError);
                }

                await provider.DisposeAsync();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        System.GC.Collect();
                        System.GC.WaitForPendingFinalizers();
                        try
                        { Directory.Delete(tempDir, recursive: true); }
                        catch { }
                    }
                }
            }
        }

        /// <summary>Verifies TwoFactorRoleRequirement can be inserted and unique constraint on Role is enforced.</summary>
        [Fact]
        public async Task TwoFactorRoleRequirement_UniqueConstraintOnRoleEnforced()
        {
            // Arrange
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var services = new ServiceCollection();
                _ = services.AddVideoForensicsSqlite(dbPath);

                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();

                // Initialize database
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    await context.Database.MigrateAsync();
                }

                // Act & Assert - Insert one TwoFactorRoleRequirement for Admin role
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    context.TwoFactorRoleRequirements.Add(new TwoFactorRoleRequirement
                    {
                        Id = Guid.NewGuid(),
                        Role = OperatorRole.Admin,
                        RequireTwoFactor = true,
                        UpdatedAtUtc = DateTime.UtcNow,
                        UpdatedByOperatorId = null
                    });
                    await context.SaveChangesAsync();
                }

                // Act & Assert - Try to insert duplicate Admin role, should throw
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    context.TwoFactorRoleRequirements.Add(new TwoFactorRoleRequirement
                    {
                        Id = Guid.NewGuid(),
                        Role = OperatorRole.Admin,
                        RequireTwoFactor = false,
                        UpdatedAtUtc = DateTime.UtcNow,
                        UpdatedByOperatorId = null
                    });

                    var exception = await Assert.ThrowsAnyAsync<DbUpdateException>(
                        async () => await context.SaveChangesAsync()
                    );
                    Assert.NotNull(exception);
                }

                await provider.DisposeAsync();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        System.GC.Collect();
                        System.GC.WaitForPendingFinalizers();
                        try
                        { Directory.Delete(tempDir, recursive: true); }
                        catch { }
                    }
                }
            }
        }

        /// <summary>Verifies BannedIpRange can be inserted and retrieved.</summary>
        [Fact]
        public async Task BannedIpRange_CanInsertAndRetrieve()
        {
            // Arrange
            string dbPath = GetTempDbPath();
            string tempDir = Path.GetDirectoryName(dbPath)!;

            try
            {
                var services = new ServiceCollection();
                _ = services.AddVideoForensicsSqlite(dbPath);

                ServiceProvider provider = services.BuildServiceProvider();
                IDbContextFactory<VideoForensicsDbContext> factory = provider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();

                // Initialize database and create an operator for FK
                var operatorId = Guid.NewGuid();
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    await context.Database.MigrateAsync();
                    context.Operators.Add(new Operator
                    {
                        Id = operatorId,
                        DisplayName = "Test Admin",
                        Username = "admin",
                        Email = "admin@example.com",
                        FirstName = "Admin",
                        LastName = "User",
                        CreatedAtUtc = DateTime.UtcNow,
                        SecurityStamp = Guid.NewGuid(),
                        Role = OperatorRole.SuperAdmin,
                        Active = true,
                        IsApproved = true
                    });
                    await context.SaveChangesAsync();
                }

                // Act - Create a BannedIpRange
                var banId = Guid.NewGuid();
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    context.BannedIpRanges.Add(new BannedIpRange
                    {
                        Id = banId,
                        CidrRange = "203.0.113.0/24",
                        Reason = "Malicious activity",
                        CreatedAtUtc = DateTime.UtcNow,
                        CreatedByOperatorId = operatorId,
                        ExpiresAtUtc = null
                    });
                    await context.SaveChangesAsync();
                }

                // Assert - Retrieve and verify
                await using (VideoForensicsDbContext context = await factory.CreateDbContextAsync())
                {
                    BannedIpRange? ban = await context.BannedIpRanges.FirstOrDefaultAsync(b => b.Id == banId);
                    Assert.NotNull(ban);
                    Assert.Equal("203.0.113.0/24", ban.CidrRange);
                    Assert.Equal("Malicious activity", ban.Reason);
                    Assert.Equal(operatorId, ban.CreatedByOperatorId);
                    Assert.Null(ban.ExpiresAtUtc);
                }

                await provider.DisposeAsync();
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                    catch
                    {
                        System.GC.Collect();
                        System.GC.WaitForPendingFinalizers();
                        try
                        { Directory.Delete(tempDir, recursive: true); }
                        catch { }
                    }
                }
            }
        }
    }
}
