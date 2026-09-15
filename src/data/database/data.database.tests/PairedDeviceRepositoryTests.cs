using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Tests for PairedDeviceRepository - security-sensitive repository for paired device credentials.</summary>
    public class PairedDeviceRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private PairedDeviceRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new PairedDeviceRepository(
                _fixture.Factory,
                loggerFactory.CreateLogger<PairedDeviceRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        #region Test Helpers

        /// <summary>Build a paired device with sensible defaults.</summary>
        private static PairedDevice BuildPairedDevice(
            Guid? operatorId = null,
            string? deviceName = null,
            OperatorRole role = OperatorRole.Admin,
            string? webAuthnCredentialId = null,
            byte[]? webAuthnPublicKey = null,
            string? fallbackApiKeyHash = null,
            string? pinnedCertificateFingerprint = null,
            DateTime? pairedAtUtc = null,
            DateTime? revokedAtUtc = null,
            string? revokedReason = null)
        {
            return new PairedDevice
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId ?? Guid.NewGuid(),
                DeviceName = deviceName ?? $"Device_{Guid.NewGuid():N}",
                Role = role,
                WebAuthnCredentialId = webAuthnCredentialId,
                WebAuthnPublicKey = webAuthnPublicKey,
                WebAuthnSignCount = 0,
                FallbackApiKeyHash = fallbackApiKeyHash,
                PinnedCertificateFingerprint = pinnedCertificateFingerprint ?? "fingerprint_placeholder",
                PairedAtUtc = pairedAtUtc ?? DateTime.UtcNow,
                LastSeenAtUtc = null,
                LastSeenIp = null,
                LastSeenTier = null,
                RevokedAtUtc = revokedAtUtc,
                RevokedReason = revokedReason
            };
        }

        #endregion

        #region GetAsync Tests

        [Fact]
        public async Task GetAsync_WithValidId_ReturnsPairedDevice()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            PairedDevice device = BuildPairedDevice(
                operatorId: operatorId,
                deviceName: "TestDevice",
                webAuthnCredentialId: "cred_123");
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(device.Id, retrieved.Id);
            Assert.Equal(operatorId, retrieved.OperatorId);
            Assert.Equal("TestDevice", retrieved.DeviceName);
            Assert.Equal(OperatorRole.Admin, retrieved.Role);
        }

        [Fact]
        public async Task GetAsync_WithNonexistentId_ReturnsNull()
        {
            // Act
            PairedDevice? retrieved = await _repository.GetAsync(Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task GetAsync_WithRevokedDevice_ReturnsDevice()
        {
            // Arrange - GetAsync returns devices regardless of revocation status
            var operatorId = Guid.NewGuid();
            PairedDevice device = BuildPairedDevice(
                operatorId: operatorId,
                webAuthnCredentialId: "cred_123",
                revokedAtUtc: DateTime.UtcNow,
                revokedReason: "Manual revocation");
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);

            // Assert
            Assert.NotNull(retrieved);
            _ = Assert.NotNull(retrieved.RevokedAtUtc);
            Assert.False(retrieved.IsActive);
        }

        #endregion

        #region GetByWebAuthnCredentialIdAsync Tests

        [Fact]
        public async Task GetByWebAuthnCredentialIdAsync_WithActiveDevice_ReturnsPairedDevice()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            const string credentialId = "webauthn_cred_abc123";
            PairedDevice device = BuildPairedDevice(
                operatorId: operatorId,
                deviceName: "WebAuthnDevice",
                webAuthnCredentialId: credentialId,
                webAuthnPublicKey: new byte[] { 1, 2, 3, 4 });
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            PairedDevice? retrieved = await _repository.GetByWebAuthnCredentialIdAsync(credentialId, CancellationToken.None);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(device.Id, retrieved.Id);
            Assert.Equal(credentialId, retrieved.WebAuthnCredentialId);
        }

        [Fact]
        public async Task GetByWebAuthnCredentialIdAsync_WithRevokedDevice_ReturnsNull()
        {
            // Arrange - revoked devices should not be returned
            var operatorId = Guid.NewGuid();
            const string credentialId = "webauthn_cred_revoked";
            PairedDevice device = BuildPairedDevice(
                operatorId: operatorId,
                webAuthnCredentialId: credentialId,
                revokedAtUtc: DateTime.UtcNow,
                revokedReason: "Device compromised");
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            PairedDevice? retrieved = await _repository.GetByWebAuthnCredentialIdAsync(credentialId, CancellationToken.None);

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task GetByWebAuthnCredentialIdAsync_WithNonexistentCredential_ReturnsNull()
        {
            // Act
            PairedDevice? retrieved = await _repository.GetByWebAuthnCredentialIdAsync(
                "nonexistent_cred", CancellationToken.None);

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task GetByWebAuthnCredentialIdAsync_MultipleDevices_ReturnsCorrectOne()
        {
            // Arrange
            var operatorId1 = Guid.NewGuid();
            var operatorId2 = Guid.NewGuid();
            const string credentialId1 = "cred_1";
            const string credentialId2 = "cred_2";

            PairedDevice device1 = BuildPairedDevice(operatorId: operatorId1, webAuthnCredentialId: credentialId1);
            PairedDevice device2 = BuildPairedDevice(operatorId: operatorId2, webAuthnCredentialId: credentialId2);

            _ = await _repository.AddAsync(device1, CancellationToken.None);
            _ = await _repository.AddAsync(device2, CancellationToken.None);

            // Act
            PairedDevice? retrieved = await _repository.GetByWebAuthnCredentialIdAsync(credentialId2, CancellationToken.None);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(device2.Id, retrieved.Id);
        }

        #endregion

        #region GetByFallbackApiKeyHashAsync Tests

        [Fact]
        public async Task GetByFallbackApiKeyHashAsync_WithActiveDevice_ReturnsPairedDevice()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            const string apiKeyHash = "sha256_abc123_hash";
            PairedDevice device = BuildPairedDevice(
                operatorId: operatorId,
                deviceName: "FallbackDevice",
                fallbackApiKeyHash: apiKeyHash);
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            PairedDevice? retrieved = await _repository.GetByFallbackApiKeyHashAsync(apiKeyHash, CancellationToken.None);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(device.Id, retrieved.Id);
            Assert.Equal(apiKeyHash, retrieved.FallbackApiKeyHash);
        }

        [Fact]
        public async Task GetByFallbackApiKeyHashAsync_WithRevokedDevice_ReturnsNull()
        {
            // Arrange - revoked devices should not be returned
            var operatorId = Guid.NewGuid();
            const string apiKeyHash = "sha256_revoked_hash";
            PairedDevice device = BuildPairedDevice(
                operatorId: operatorId,
                fallbackApiKeyHash: apiKeyHash,
                revokedAtUtc: DateTime.UtcNow,
                revokedReason: "Key compromised");
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            PairedDevice? retrieved = await _repository.GetByFallbackApiKeyHashAsync(apiKeyHash, CancellationToken.None);

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task GetByFallbackApiKeyHashAsync_WithNonexistentHash_ReturnsNull()
        {
            // Act
            PairedDevice? retrieved = await _repository.GetByFallbackApiKeyHashAsync(
                "nonexistent_hash", CancellationToken.None);

            // Assert
            Assert.Null(retrieved);
        }

        #endregion

        #region AddAsync Tests

        [Fact]
        public async Task AddAsync_WithValidDevice_CreatesDevice()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            PairedDevice device = BuildPairedDevice(
                operatorId: operatorId,
                deviceName: "NewDevice",
                role: OperatorRole.ReadOnly,
                webAuthnCredentialId: "cred_new");

            // Act
            PairedDevice added = await _repository.AddAsync(device, CancellationToken.None);

            // Assert
            Assert.Equal(device.Id, added.Id);
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("NewDevice", retrieved.DeviceName);
            Assert.Equal(OperatorRole.ReadOnly, retrieved.Role);
        }

        [Fact]
        public async Task AddAsync_StoresAllFields()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;
            byte[] publicKey = new byte[] { 5, 6, 7, 8 };
            const string fingerprint = "custom_fingerprint";

            PairedDevice device = BuildPairedDevice(
                operatorId: operatorId,
                deviceName: "FullDevice",
                role: OperatorRole.SuperAdmin,
                webAuthnCredentialId: "full_cred",
                webAuthnPublicKey: publicKey,
                pinnedCertificateFingerprint: fingerprint,
                pairedAtUtc: now);

            // Act
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Assert
            VideoForensicsDbContext ctx = _fixture.Factory.CreateDbContext();
            PairedDevice? stored = await ctx.PairedDevices.FirstOrDefaultAsync(d => d.Id == device.Id);

            Assert.NotNull(stored);
            Assert.Equal(operatorId, stored.OperatorId);
            Assert.Equal("FullDevice", stored.DeviceName);
            Assert.Equal(OperatorRole.SuperAdmin, stored.Role);
            Assert.Equal("full_cred", stored.WebAuthnCredentialId);
            Assert.Equal(publicKey, stored.WebAuthnPublicKey);
            Assert.Equal(fingerprint, stored.PinnedCertificateFingerprint);
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_ModifiesExistingDevice()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice(deviceName: "Original");
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            device.DeviceName = "Updated";
            device.Role = OperatorRole.ReadOnly;
            await _repository.UpdateAsync(device, CancellationToken.None);

            // Assert
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("Updated", retrieved.DeviceName);
            Assert.Equal(OperatorRole.ReadOnly, retrieved.Role);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesLastSeenFields()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice();
            _ = await _repository.AddAsync(device, CancellationToken.None);

            DateTime now = DateTime.UtcNow;
            device.LastSeenAtUtc = now;
            device.LastSeenIp = "192.168.1.100";
            device.LastSeenTier = NetworkTier.Network;

            // Act
            await _repository.UpdateAsync(device, CancellationToken.None);

            // Assert
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(now, retrieved.LastSeenAtUtc);
            Assert.Equal("192.168.1.100", retrieved.LastSeenIp);
            Assert.Equal(NetworkTier.Network, retrieved.LastSeenTier);
        }

        [Fact]
        public async Task UpdateAsync_CanSetRevokedStatus()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice();
            _ = await _repository.AddAsync(device, CancellationToken.None);

            DateTime now = DateTime.UtcNow;
            device.RevokedAtUtc = now;
            device.RevokedReason = "Manual update";

            // Act
            await _repository.UpdateAsync(device, CancellationToken.None);

            // Assert
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            _ = Assert.NotNull(retrieved.RevokedAtUtc);
            Assert.Equal("Manual update", retrieved.RevokedReason);
        }

        #endregion

        #region ListAsync Tests

        [Fact]
        public async Task ListAsync_ReturnsAllDevices()
        {
            // Arrange
            var operatorId1 = Guid.NewGuid();
            var operatorId2 = Guid.NewGuid();

            PairedDevice device1 = BuildPairedDevice(operatorId: operatorId1, deviceName: "Device1");
            PairedDevice device2 = BuildPairedDevice(operatorId: operatorId2, deviceName: "Device2");
            PairedDevice device3 = BuildPairedDevice(operatorId: operatorId1, deviceName: "Device3");

            _ = await _repository.AddAsync(device1, CancellationToken.None);
            _ = await _repository.AddAsync(device2, CancellationToken.None);
            _ = await _repository.AddAsync(device3, CancellationToken.None);

            // Act
            IReadOnlyList<PairedDevice> list = await _repository.ListAsync(CancellationToken.None);

            // Assert
            Assert.Equal(3, list.Count);
        }

        [Fact]
        public async Task ListAsync_SortsByPairedAtUtcDescending()
        {
            // Arrange
            DateTime baseTime = DateTime.UtcNow;

            PairedDevice device1 = BuildPairedDevice(deviceName: "Device1", pairedAtUtc: baseTime.AddMinutes(-2));
            PairedDevice device2 = BuildPairedDevice(deviceName: "Device2", pairedAtUtc: baseTime);
            PairedDevice device3 = BuildPairedDevice(deviceName: "Device3", pairedAtUtc: baseTime.AddMinutes(-1));

            _ = await _repository.AddAsync(device1, CancellationToken.None);
            _ = await _repository.AddAsync(device2, CancellationToken.None);
            _ = await _repository.AddAsync(device3, CancellationToken.None);

            // Act
            IReadOnlyList<PairedDevice> list = await _repository.ListAsync(CancellationToken.None);

            // Assert - should be ordered by PairedAtUtc descending (most recent first)
            Assert.Equal(3, list.Count);
            Assert.Equal("Device2", list[0].DeviceName);
            Assert.Equal("Device3", list[1].DeviceName);
            Assert.Equal("Device1", list[2].DeviceName);
        }

        [Fact]
        public async Task ListAsync_IncludesRevokedDevices()
        {
            // Arrange
            PairedDevice device1 = BuildPairedDevice(deviceName: "Active");
            PairedDevice device2 = BuildPairedDevice(
                deviceName: "Revoked",
                revokedAtUtc: DateTime.UtcNow,
                revokedReason: "Test");

            _ = await _repository.AddAsync(device1, CancellationToken.None);
            _ = await _repository.AddAsync(device2, CancellationToken.None);

            // Act
            IReadOnlyList<PairedDevice> list = await _repository.ListAsync(CancellationToken.None);

            // Assert - ListAsync returns all devices, revoked or not
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task ListAsync_WithNoDevices_ReturnsEmpty()
        {
            // Act
            IReadOnlyList<PairedDevice> list = await _repository.ListAsync(CancellationToken.None);

            // Assert
            Assert.Empty(list);
        }

        #endregion

        #region ListForOperatorAsync Tests

        [Fact]
        public async Task ListForOperatorAsync_ReturnsDevicesForOperator()
        {
            // Arrange
            var operatorId1 = Guid.NewGuid();
            var operatorId2 = Guid.NewGuid();

            PairedDevice device1 = BuildPairedDevice(operatorId: operatorId1, deviceName: "Op1Device1");
            PairedDevice device2 = BuildPairedDevice(operatorId: operatorId1, deviceName: "Op1Device2");
            PairedDevice device3 = BuildPairedDevice(operatorId: operatorId2, deviceName: "Op2Device1");

            _ = await _repository.AddAsync(device1, CancellationToken.None);
            _ = await _repository.AddAsync(device2, CancellationToken.None);
            _ = await _repository.AddAsync(device3, CancellationToken.None);

            // Act
            IReadOnlyList<PairedDevice> list = await _repository.ListForOperatorAsync(operatorId1, CancellationToken.None);

            // Assert
            Assert.Equal(2, list.Count);
            Assert.All(list, d => Assert.Equal(operatorId1, d.OperatorId));
        }

        [Fact]
        public async Task ListForOperatorAsync_SortsByPairedAtUtcDescending()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            DateTime baseTime = DateTime.UtcNow;

            PairedDevice device1 = BuildPairedDevice(operatorId: operatorId, deviceName: "Device1", pairedAtUtc: baseTime.AddMinutes(-1));
            PairedDevice device2 = BuildPairedDevice(operatorId: operatorId, deviceName: "Device2", pairedAtUtc: baseTime);

            _ = await _repository.AddAsync(device1, CancellationToken.None);
            _ = await _repository.AddAsync(device2, CancellationToken.None);

            // Act
            IReadOnlyList<PairedDevice> list = await _repository.ListForOperatorAsync(operatorId, CancellationToken.None);

            // Assert - most recent first
            Assert.Equal(2, list.Count);
            Assert.Equal("Device2", list[0].DeviceName);
            Assert.Equal("Device1", list[1].DeviceName);
        }

        [Fact]
        public async Task ListForOperatorAsync_IncludesRevokedDevices()
        {
            // Arrange
            var operatorId = Guid.NewGuid();

            PairedDevice device1 = BuildPairedDevice(operatorId: operatorId, deviceName: "Active");
            PairedDevice device2 = BuildPairedDevice(
                operatorId: operatorId,
                deviceName: "Revoked",
                revokedAtUtc: DateTime.UtcNow,
                revokedReason: "Test");

            _ = await _repository.AddAsync(device1, CancellationToken.None);
            _ = await _repository.AddAsync(device2, CancellationToken.None);

            // Act
            IReadOnlyList<PairedDevice> list = await _repository.ListForOperatorAsync(operatorId, CancellationToken.None);

            // Assert - includes revoked devices
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public async Task ListForOperatorAsync_WithNonexistentOperator_ReturnsEmpty()
        {
            // Act
            IReadOnlyList<PairedDevice> list = await _repository.ListForOperatorAsync(Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.Empty(list);
        }

        #endregion

        #region RevokeAsync Tests

        [Fact]
        public async Task RevokeAsync_SetsRevokedFields()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice(webAuthnCredentialId: "cred_to_revoke");
            _ = await _repository.AddAsync(device, CancellationToken.None);

            DateTime beforeRevoke = DateTime.UtcNow;
            const string reason = "User requested revocation";

            // Act
            await _repository.RevokeAsync(device.Id, reason, CancellationToken.None);

            // Assert
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            _ = Assert.NotNull(retrieved.RevokedAtUtc);
            Assert.True(retrieved.RevokedAtUtc >= beforeRevoke);
            Assert.Equal(reason, retrieved.RevokedReason);
            Assert.False(retrieved.IsActive);
        }

        [Fact]
        public async Task RevokeAsync_MakesDeviceInactiveForWebAuthnLookup()
        {
            // Arrange
            const string credentialId = "cred_revoke_test";
            PairedDevice device = BuildPairedDevice(webAuthnCredentialId: credentialId);
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            await _repository.RevokeAsync(device.Id, "Testing revocation", CancellationToken.None);

            // Assert - GetByWebAuthnCredentialIdAsync should not return revoked device
            PairedDevice? retrieved = await _repository.GetByWebAuthnCredentialIdAsync(credentialId, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task RevokeAsync_MakesDeviceInactiveForApiKeyLookup()
        {
            // Arrange
            const string apiKeyHash = "hash_revoke_test";
            PairedDevice device = BuildPairedDevice(fallbackApiKeyHash: apiKeyHash);
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            await _repository.RevokeAsync(device.Id, "Testing revocation", CancellationToken.None);

            // Assert - GetByFallbackApiKeyHashAsync should not return revoked device
            PairedDevice? retrieved = await _repository.GetByFallbackApiKeyHashAsync(apiKeyHash, CancellationToken.None);
            Assert.Null(retrieved);
        }

        [Fact]
        public async Task RevokeAsync_WithNonexistentDevice_DoesNotThrow()
        {
            // Act & Assert - should not throw
            await _repository.RevokeAsync(Guid.NewGuid(), "Nonexistent device", CancellationToken.None);
        }

        [Fact]
        public async Task RevokeAsync_WithAlreadyRevokedDevice_UpdatesReason()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice(
                revokedAtUtc: DateTime.UtcNow,
                revokedReason: "Original reason");
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            await _repository.RevokeAsync(device.Id, "New reason", CancellationToken.None);

            // Assert
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal("New reason", retrieved.RevokedReason);
        }

        #endregion

        #region RevokeAllForOperatorAsync Tests

        [Fact]
        public async Task RevokeAllForOperatorAsync_RevokesAllActiveDevices()
        {
            // Arrange
            var operatorId = Guid.NewGuid();

            PairedDevice device1 = BuildPairedDevice(operatorId: operatorId, deviceName: "Device1");
            PairedDevice device2 = BuildPairedDevice(operatorId: operatorId, deviceName: "Device2");

            _ = await _repository.AddAsync(device1, CancellationToken.None);
            _ = await _repository.AddAsync(device2, CancellationToken.None);

            const string reason = "Operator deactivated";

            // Act
            IReadOnlyList<Guid> revokedIds = await _repository.RevokeAllForOperatorAsync(operatorId, reason, CancellationToken.None);

            // Assert
            Assert.Equal(2, revokedIds.Count);
            Assert.Contains(device1.Id, revokedIds);
            Assert.Contains(device2.Id, revokedIds);

            IReadOnlyList<PairedDevice> devices = await _repository.ListForOperatorAsync(operatorId, CancellationToken.None);
            Assert.All(devices, d => Assert.NotNull(d.RevokedAtUtc));
            Assert.All(devices, d => Assert.Equal(reason, d.RevokedReason));
        }

        [Fact]
        public async Task RevokeAllForOperatorAsync_OnlyRevokesActiveDevices()
        {
            // Arrange
            var operatorId = Guid.NewGuid();

            PairedDevice activeDevice = BuildPairedDevice(operatorId: operatorId, deviceName: "Active");
            PairedDevice revokedDevice = BuildPairedDevice(
                operatorId: operatorId,
                deviceName: "AlreadyRevoked",
                revokedAtUtc: DateTime.UtcNow,
                revokedReason: "Previously revoked");

            _ = await _repository.AddAsync(activeDevice, CancellationToken.None);
            _ = await _repository.AddAsync(revokedDevice, CancellationToken.None);

            // Act
            IReadOnlyList<Guid> revokedIds = await _repository.RevokeAllForOperatorAsync(operatorId, "New revocation", CancellationToken.None);

            // Assert - only the active device should be in the returned list
            _ = Assert.Single(revokedIds);
            Assert.Contains(activeDevice.Id, revokedIds);
            Assert.DoesNotContain(revokedDevice.Id, revokedIds);
        }

        [Fact]
        public async Task RevokeAllForOperatorAsync_DoesNotAffectOtherOperators()
        {
            // Arrange
            var operatorId1 = Guid.NewGuid();
            var operatorId2 = Guid.NewGuid();

            PairedDevice device1 = BuildPairedDevice(operatorId: operatorId1, deviceName: "Op1Device");
            PairedDevice device2 = BuildPairedDevice(operatorId: operatorId2, deviceName: "Op2Device");

            _ = await _repository.AddAsync(device1, CancellationToken.None);
            _ = await _repository.AddAsync(device2, CancellationToken.None);

            // Act
            _ = await _repository.RevokeAllForOperatorAsync(operatorId1, "Revoke operator 1", CancellationToken.None);

            // Assert
            PairedDevice? op1Device = await _repository.GetAsync(device1.Id, CancellationToken.None);
            PairedDevice? op2Device = await _repository.GetAsync(device2.Id, CancellationToken.None);

            _ = Assert.NotNull(op1Device.RevokedAtUtc);
            Assert.Null(op2Device.RevokedAtUtc);
        }

        [Fact]
        public async Task RevokeAllForOperatorAsync_WithNonexistentOperator_ReturnsEmpty()
        {
            // Act
            IReadOnlyList<Guid> revokedIds = await _repository.RevokeAllForOperatorAsync(
                Guid.NewGuid(), "Nonexistent operator", CancellationToken.None);

            // Assert
            Assert.Empty(revokedIds);
        }

        #endregion

        #region RecordSuccessfulAuthAsync Tests

        [Fact]
        public async Task RecordSuccessfulAuthAsync_UpdatesSignCount()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice(webAuthnCredentialId: "cred_auth");
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            await _repository.RecordSuccessfulAuthAsync(
                device.Id,
                newSignCount: 42,
                sourceIp: "192.168.1.50",
                tier: NetworkTier.Local,
                CancellationToken.None);

            // Assert
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(42U, retrieved.WebAuthnSignCount);
        }

        [Fact]
        public async Task RecordSuccessfulAuthAsync_UpdatesLastSeenFields()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice();
            _ = await _repository.AddAsync(device, CancellationToken.None);

            DateTime beforeAuth = DateTime.UtcNow;
            const string sourceIp = "203.0.113.42";

            // Act
            await _repository.RecordSuccessfulAuthAsync(
                device.Id,
                newSignCount: 1,
                sourceIp: sourceIp,
                tier: NetworkTier.Internet,
                CancellationToken.None);

            // Assert
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            _ = Assert.NotNull(retrieved.LastSeenAtUtc);
            Assert.True(retrieved.LastSeenAtUtc >= beforeAuth);
            Assert.Equal(sourceIp, retrieved.LastSeenIp);
            Assert.Equal(NetworkTier.Internet, retrieved.LastSeenTier);
        }

        [Fact]
        public async Task RecordSuccessfulAuthAsync_WithDifferentNetworkTiers()
        {
            // Arrange
            PairedDevice device1 = BuildPairedDevice(deviceName: "Device1");
            PairedDevice device2 = BuildPairedDevice(deviceName: "Device2");
            PairedDevice device3 = BuildPairedDevice(deviceName: "Device3");

            _ = await _repository.AddAsync(device1, CancellationToken.None);
            _ = await _repository.AddAsync(device2, CancellationToken.None);
            _ = await _repository.AddAsync(device3, CancellationToken.None);

            // Act - record auth from different tiers
            await _repository.RecordSuccessfulAuthAsync(device1.Id, 1, "10.0.0.1", NetworkTier.Local, CancellationToken.None);
            await _repository.RecordSuccessfulAuthAsync(device2.Id, 2, "192.168.1.1", NetworkTier.Network, CancellationToken.None);
            await _repository.RecordSuccessfulAuthAsync(device3.Id, 3, "203.0.113.1", NetworkTier.Internet, CancellationToken.None);

            // Assert
            PairedDevice? dev1 = await _repository.GetAsync(device1.Id, CancellationToken.None);
            PairedDevice? dev2 = await _repository.GetAsync(device2.Id, CancellationToken.None);
            PairedDevice? dev3 = await _repository.GetAsync(device3.Id, CancellationToken.None);

            Assert.Equal(NetworkTier.Local, dev1.LastSeenTier);
            Assert.Equal(NetworkTier.Network, dev2.LastSeenTier);
            Assert.Equal(NetworkTier.Internet, dev3.LastSeenTier);
        }

        [Fact]
        public async Task RecordSuccessfulAuthAsync_WithNullIp()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice();
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act
            await _repository.RecordSuccessfulAuthAsync(
                device.Id,
                newSignCount: 1,
                sourceIp: null,
                tier: NetworkTier.Local,
                CancellationToken.None);

            // Assert
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Null(retrieved.LastSeenIp);
            Assert.Equal(NetworkTier.Local, retrieved.LastSeenTier);
        }

        [Fact]
        public async Task RecordSuccessfulAuthAsync_WithNonexistentDevice_DoesNotThrow()
        {
            // Act & Assert - should not throw
            await _repository.RecordSuccessfulAuthAsync(
                Guid.NewGuid(),
                newSignCount: 1,
                sourceIp: "127.0.0.1",
                tier: NetworkTier.Local,
                CancellationToken.None);
        }

        [Fact]
        public async Task RecordSuccessfulAuthAsync_IncrementingSignCount()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice();
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act - simulate multiple authentications with increasing sign count
            await _repository.RecordSuccessfulAuthAsync(device.Id, 1, "10.0.0.1", NetworkTier.Local, CancellationToken.None);
            await _repository.RecordSuccessfulAuthAsync(device.Id, 5, "10.0.0.1", NetworkTier.Local, CancellationToken.None);
            await _repository.RecordSuccessfulAuthAsync(device.Id, 10, "10.0.0.1", NetworkTier.Local, CancellationToken.None);

            // Assert
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.Equal(10U, retrieved.WebAuthnSignCount);
        }

        #endregion

        #region Security and Integration Tests

        [Fact]
        public async Task PairedDevice_IsActiveProperty_ReflectsRevokedStatus()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice();
            _ = await _repository.AddAsync(device, CancellationToken.None);

            // Act & Assert - initially active
            PairedDevice? active = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(active);
            Assert.True(active.IsActive);

            // Act - revoke
            await _repository.RevokeAsync(device.Id, "Test", CancellationToken.None);

            // Assert - now inactive
            PairedDevice? revoked = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(revoked);
            Assert.False(revoked.IsActive);
        }

        [Fact]
        public async Task MultipleCredentialTypes_WebAuthnAndFallbackCanCoexist()
        {
            // Arrange
            var operatorId = Guid.NewGuid();

            PairedDevice webAuthnDevice = BuildPairedDevice(
                operatorId: operatorId,
                deviceName: "WebAuthn",
                webAuthnCredentialId: "cred_webauthn",
                webAuthnPublicKey: new byte[] { 1, 2, 3 });

            PairedDevice fallbackDevice = BuildPairedDevice(
                operatorId: operatorId,
                deviceName: "Fallback",
                fallbackApiKeyHash: "hash_fallback");

            _ = await _repository.AddAsync(webAuthnDevice, CancellationToken.None);
            _ = await _repository.AddAsync(fallbackDevice, CancellationToken.None);

            // Act
            PairedDevice? retrievedWebAuthn = await _repository.GetByWebAuthnCredentialIdAsync("cred_webauthn", CancellationToken.None);
            PairedDevice? retrievedFallback = await _repository.GetByFallbackApiKeyHashAsync("hash_fallback", CancellationToken.None);

            // Assert
            Assert.NotNull(retrievedWebAuthn);
            Assert.NotNull(retrievedFallback);
            Assert.NotEqual(retrievedWebAuthn.Id, retrievedFallback.Id);
        }

        [Fact]
        public async Task RevokeAsync_AndRecordSuccessfulAuthAsync_OnRevokedDevice()
        {
            // Arrange
            PairedDevice device = BuildPairedDevice();
            _ = await _repository.AddAsync(device, CancellationToken.None);

            await _repository.RevokeAsync(device.Id, "Compromised", CancellationToken.None);

            _ = DateTime.UtcNow;

            // Act - attempt to record auth on revoked device (should still work, but device remains revoked)
            await _repository.RecordSuccessfulAuthAsync(device.Id, 99, "192.168.1.1", NetworkTier.Network, CancellationToken.None);

            // Assert
            PairedDevice? retrieved = await _repository.GetAsync(device.Id, CancellationToken.None);
            Assert.NotNull(retrieved);
            _ = Assert.NotNull(retrieved.RevokedAtUtc);
            Assert.Equal(99U, retrieved.WebAuthnSignCount);
            Assert.Equal("192.168.1.1", retrieved.LastSeenIp);
        }

        #endregion
    }
}
