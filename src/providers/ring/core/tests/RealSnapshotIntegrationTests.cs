using VideoForensics.Providers.Ring.Core.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Real Ring API check for the snapshot path, using the refresh token saved in the database (see RealSessionHelper).
    /// Calls Ring and uses provider budget, so it skips when no credentials are saved and is meant for manual runs.
    /// </summary>
    public class RealSnapshotIntegrationTests : IAsyncLifetime
    {
        private bool _credentialsAvailable;

        public ValueTask InitializeAsync()
        {
            _credentialsAvailable = RealSessionHelper.CredentialsAvailable();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        [Fact]
        [Trait("Description", "Real API: captures the latest snapshot from the first doorbot with the saved credentials")]
        public async Task RealSnapshot_CapturesJpegFromFirstDoorbot()
        {
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
                return;
            }

            Session session = await RealSessionHelper.CreateAuthenticatedSessionAsync();
            var devices = await session.GetRingDevices();
            var doorbot = devices.Doorbots.FirstOrDefault();
            if (doorbot is null)
            {
                Assert.Skip("This Ring account has no doorbot to capture a snapshot from.");
                return;
            }

            using Stream snapshot = await session.GetLatestSnapshot(doorbot);
            using var buffer = new MemoryStream();
            await snapshot.CopyToAsync(buffer);
            byte[] bytes = buffer.ToArray();

            Assert.True(bytes.Length > 0, "Ring returned no snapshot bytes.");
            // JPEG start-of-image marker, the same check the scheduler's stored file relies on.
            Assert.Equal(0xFF, bytes[0]);
            Assert.Equal(0xD8, bytes[1]);
        }
    }
}
