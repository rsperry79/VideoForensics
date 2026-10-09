using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class LocalLiveViewSessionSourceTests
    {
        [Fact]
        public void LocalLiveViewSessionSource_Always_ReportsNotConnected()
        {
            var source = new LocalLiveViewSessionSource();

            Assert.False(source.IsConnected);
        }

        [Fact]
        public async Task LocalLiveViewSessionSource_SubscribeAndUnsubscribe_CompleteWithoutEmitting()
        {
            var source = new LocalLiveViewSessionSource();
            var received = new List<LiveViewSession>();
            using IDisposable subscription = source.SessionChanged.Subscribe(received.Add);

            await source.SubscribeAsync(Guid.NewGuid(), CancellationToken.None);
            await source.UnsubscribeAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.Empty(received);
        }
    }
}
