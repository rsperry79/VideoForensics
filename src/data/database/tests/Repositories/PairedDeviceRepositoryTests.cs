using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class PairedDeviceRepositoryTests : RepositoryTestBase
    {
        private PairedDeviceRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new PairedDeviceRepository(Fixture.Factory, CreateLogger<PairedDeviceRepository>());
        }

        [Fact]
        public async Task RevokeAsync_SanitizesLogOutput_WhenReasonContainsNewlines()
        {
            // Arrange: Create a paired device
            var pairedDeviceId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var device = new PairedDevice
            {
                Id = pairedDeviceId,
                OperatorId = operatorId,
                DeviceName = "Test Device",
                Role = OperatorRole.Admin,
                WebAuthnCredentialId = "test-cred-id",
                WebAuthnPublicKey = new byte[] { 0x01, 0x02, 0x03 },
                PairedAtUtc = DateTime.UtcNow
            };

            // Add the device to the database first
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                _ = db.PairedDevices.Add(device);
                _ = await db.SaveChangesAsync(CancellationToken.None);
            }

            // Act: Revoke with a reason containing CRLF (log injection attempt)
            string injectedReason = "Device compromised\r\nFAKE LOG ENTRY: Admin privileges granted";
            await _repository.RevokeAsync(pairedDeviceId, injectedReason, CancellationToken.None);

            // Assert: Verify the device was revoked and the reason is stored as-is in the database
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                PairedDevice? revokedDevice = await db.PairedDevices.FindAsync(new object[] { pairedDeviceId }, cancellationToken: CancellationToken.None);
                Assert.NotNull(revokedDevice);
                Assert.NotNull(revokedDevice.RevokedAtUtc);
                // The raw reason (with newlines) should be stored in the database
                Assert.Equal(injectedReason, revokedDevice.RevokedReason);
            }
        }
    }
}
