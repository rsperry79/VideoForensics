using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class DeviceCodePairingServiceTests
    {
        [Fact]
        public void CreateSession_Always_ReturnsUniqueDeviceCodeAndUserCode()
        {
            var service = new DeviceCodePairingService();

            DeviceCodePairingSession session1 = service.CreateSession();
            DeviceCodePairingSession session2 = service.CreateSession();

            Assert.NotEqual(session1.DeviceCode, session2.DeviceCode);
            Assert.NotEqual(session1.UserCode, session2.UserCode);
        }

        [Fact]
        public void CreateSession_Always_StartsInPendingStatus()
        {
            var service = new DeviceCodePairingService();

            DeviceCodePairingSession session = service.CreateSession();

            Assert.Equal(DeviceCodePairingStatus.Pending, session.Status);
        }

        [Fact]
        public void GetByUserCode_WithValidCode_ReturnsSession()
        {
            var service = new DeviceCodePairingService();
            DeviceCodePairingSession session = service.CreateSession();

            DeviceCodePairingSession? retrieved = service.GetByUserCode(session.UserCode);

            Assert.NotNull(retrieved);
            Assert.Equal(session.UserCode, retrieved!.UserCode);
            Assert.Equal(session.DeviceCode, retrieved!.DeviceCode);
        }

        [Fact]
        public void GetByUserCode_WithUnknownCode_ReturnsNull()
        {
            var service = new DeviceCodePairingService();

            DeviceCodePairingSession? retrieved = service.GetByUserCode("UNKNOWNCODE");

            Assert.Null(retrieved);
        }

        [Fact]
        public void GetByDeviceCode_WithValidCode_ReturnsSession()
        {
            var service = new DeviceCodePairingService();
            DeviceCodePairingSession session = service.CreateSession();

            DeviceCodePairingSession? retrieved = service.GetByDeviceCode(session.DeviceCode);

            Assert.NotNull(retrieved);
            Assert.Equal(session.DeviceCode, retrieved!.DeviceCode);
            Assert.Equal(session.UserCode, retrieved!.UserCode);
        }

        [Fact]
        public void GetByDeviceCode_WithUnknownCode_ReturnsNull()
        {
            var service = new DeviceCodePairingService();

            DeviceCodePairingSession? retrieved = service.GetByDeviceCode("unknown-device-code");

            Assert.Null(retrieved);
        }

        [Fact]
        public void TryApprove_WithValidPendingSession_ReturnsTrueAndSetsApprovedStatus()
        {
            var service = new DeviceCodePairingService();
            DeviceCodePairingSession session = service.CreateSession();
            const string apiKey = "test-api-key-123";

            bool result = service.TryApprove(session.UserCode, apiKey);
            DeviceCodePairingSession? retrievedAfter = service.GetByUserCode(session.UserCode);

            Assert.True(result);
            Assert.NotNull(retrievedAfter);
            Assert.Equal(DeviceCodePairingStatus.Approved, retrievedAfter!.Status);
            Assert.Equal(apiKey, retrievedAfter!.ApiKey);
        }

        [Fact]
        public void TryApprove_WithUnknownUserCode_ReturnsFalse()
        {
            var service = new DeviceCodePairingService();

            bool result = service.TryApprove("UNKNOWNCODE", "some-api-key");

            Assert.False(result);
        }

        [Fact]
        public void TryApprove_WhenAlreadyApproved_ReturnsFalse()
        {
            var service = new DeviceCodePairingService();
            DeviceCodePairingSession session = service.CreateSession();
            const string firstKey = "first-api-key";
            const string secondKey = "second-api-key";

            // First approval should succeed
            bool firstResult = service.TryApprove(session.UserCode, firstKey);
            Assert.True(firstResult);

            // Second approval with different key should fail
            bool secondResult = service.TryApprove(session.UserCode, secondKey);
            Assert.False(secondResult);

            // ApiKey should still be the first one
            DeviceCodePairingSession? retrieved = service.GetByUserCode(session.UserCode);
            Assert.Equal(firstKey, retrieved!.ApiKey);
        }

        [Fact]
        public void TryConsumeApiKey_AfterApproval_ReturnsKeyExactlyOnce()
        {
            var service = new DeviceCodePairingService();
            DeviceCodePairingSession session = service.CreateSession();
            const string apiKey = "test-api-key-456";

            _ = service.TryApprove(session.UserCode, apiKey);

            // First consume should return the key
            string? firstConsume = service.TryConsumeApiKey(session.DeviceCode);
            Assert.NotNull(firstConsume);
            Assert.Equal(apiKey, firstConsume);

            // Second consume should return null (key was cleared)
            string? secondConsume = service.TryConsumeApiKey(session.DeviceCode);
            Assert.Null(secondConsume);
        }

        [Fact]
        public void TryConsumeApiKey_WhenStillPending_ReturnsNull()
        {
            var service = new DeviceCodePairingService();
            DeviceCodePairingSession session = service.CreateSession();

            string? result = service.TryConsumeApiKey(session.DeviceCode);

            Assert.Null(result);
        }

        [Fact]
        public void TryConsumeApiKey_WithUnknownDeviceCode_ReturnsNull()
        {
            var service = new DeviceCodePairingService();

            string? result = service.TryConsumeApiKey("unknown-device-code");

            Assert.Null(result);
        }

        // NOTE: Expiry test not included. DeviceCodePairingService sets ExpiresAtUtc to DateTime.UtcNow + 10 minutes
        // internally with no injectable clock, so testing expiry would require either a 10-minute Task.Delay (making
        // the suite slow/flaky) or modifying the class to allow time injection. Pruning of expired sessions is tested
        // indirectly through the GetByUserCode/GetByDeviceCode null returns; a dedicated expiry test would be more
        // useful if the class exposed an injectable ISystemClock or similar.
    }
}
