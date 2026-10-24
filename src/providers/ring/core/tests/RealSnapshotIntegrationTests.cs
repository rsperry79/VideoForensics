using VideoForensics.Providers.Ring.Core.Tests.Mocks;
using VideoForensics.Providers.Ring.Testing.Mocks;

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

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        [Fact]
        [Trait("Description", "Real API: captures the latest snapshot from the first doorbot with the saved credentials")]
        public async Task RealSnapshot_CapturesJpegFromFirstDoorbot()
        {
            Mocks.RealSessionHelper.RequireLive();
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
                return;
            }

            Session session = await RealSessionHelper.CreateAuthenticatedSessionAsync();
            Entities.Devices devices = await session.GetRingDevices();
            // Pick the doorbot by name. The first one on the list can be inactive (no snapshot, HTTP 404).
            string wantedName = Environment.GetEnvironmentVariable("RING_TEST_DOORBOT") ?? "Front Door";
            Entities.Doorbot? doorbot = devices.Doorbots.FirstOrDefault(d => string.Equals(d.Description, wantedName, StringComparison.OrdinalIgnoreCase));
            if (doorbot is null)
            {
                string available = string.Join(", ", devices.Doorbots.Select(d => d.Description));
                Assert.Skip($"No doorbot named '{wantedName}' on this account (set RING_TEST_DOORBOT). Available: {available}");
                return;
            }

            // Ring only serves a snapshot after a fresh one is requested, as the production path does
            // (RingVideoService.GetSnapshot). Without this the latest image can be missing and Ring answers 404.
            await session.UpdateSnapshot(doorbot.Id);

            // The status-aware call: GetLatestSnapshot(doorbot) returns the body even on a 404, which would
            // read "Not Found" as image bytes.
            // Ring builds the snapshot after the request, so the first read can still be 404. Retry only that status,
            // with growing waits, and report the last status if it never appears.
            const int MaxAttempts = 4;
            HttpUtility.DownloadResult result = await session.GetLatestSnapshotWithStatus(doorbot.Id);
            for (int attempt = 2; attempt <= MaxAttempts && result.Status == System.Net.HttpStatusCode.NotFound; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * (attempt - 1)));
                result = await session.GetLatestSnapshotWithStatus(doorbot.Id);
            }
            string preview = System.Text.Encoding.UTF8.GetString(result.Bytes, 0, Math.Min(result.Bytes.Length, 200));

            Assert.True(result.Status == System.Net.HttpStatusCode.OK,
                $"Ring returned HTTP {(int)result.Status} for doorbot {doorbot.Id}. Body: {preview}");
            Assert.True(result.Bytes.Length > 0, "Ring returned no snapshot bytes.");
            // JPEG start-of-image marker, the same check the scheduler's stored file relies on.
            Assert.True(result.Bytes[0] == 0xFF && result.Bytes[1] == 0xD8,
                $"Ring returned {result.Bytes.Length} bytes that do not start with a JPEG marker. First bytes as text: {preview}");
        }
    }
}
