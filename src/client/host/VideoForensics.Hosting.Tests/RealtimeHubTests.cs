using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Hosting.Contracts;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Hosting.Tests
{
    public class RealtimeHubTests
    {
        private static readonly Uri ServerAddress = new("http://127.0.0.1:1");

        private static IServiceProvider CreateServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new PairedSessionState(new Mock<IJSRuntime>().Object));
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task RealtimeHub_FirstStartUnreachable_DoesNotThrowAndEntersReconnecting()
        {
            // Unreachable server: the real connect is refused. A transient failure must not throw out of StartAsync
            // (MAUI launches before the server is up); it must enter Reconnecting and retry in the background.
            await using var hub = new RealtimeHub(ServerAddress, CreateServices());
            var states = new List<ConnectionState>();
            using IDisposable subscription = hub.Connection.Subscribe(states.Add);

            await hub.StartAsync(CancellationToken.None);

            Assert.Equal(ConnectionState.Reconnecting, states[^1]);
        }

        [Fact]
        public async Task RealtimeHub_InitialConnectFailsTransiently_ReconnectsAndReachesConnected()
        {
            int connectCalls = 0;
            await using var hub = new RealtimeHub(ServerAddress, CreateServices(), _ =>
            {
                connectCalls++;
                if (connectCalls == 1)
                {
                    throw new InvalidOperationException("Server not reachable yet");
                }

                return Task.CompletedTask;
            });
            var states = new List<ConnectionState>();
            var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using IDisposable subscription = hub.Connection.Subscribe(state =>
            {
                states.Add(state);
                if (state == ConnectionState.Connected)
                {
                    connected.TrySetResult();
                }
            });

            await hub.StartAsync(CancellationToken.None);
            await connected.Task.WaitAsync(TimeSpan.FromSeconds(10));

            int reconnectingIndex = states.IndexOf(ConnectionState.Reconnecting);
            int connectedIndex = states.IndexOf(ConnectionState.Connected);
            Assert.True(reconnectingIndex >= 0 && connectedIndex > reconnectingIndex,
                $"Expected Reconnecting before Connected; saw [{string.Join(", ", states)}]");
            Assert.Equal(2, connectCalls);
        }

        [Fact]
        public async Task RealtimeHub_InitialConnectReturns401_EmitsAuthFailedAndDoesNotRetry()
        {
            int connectCalls = 0;
            await using var hub = new RealtimeHub(ServerAddress, CreateServices(), _ =>
            {
                connectCalls++;
                return Task.FromException(new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));
            });
            var states = new List<ConnectionState>();
            using IDisposable subscription = hub.Connection.Subscribe(states.Add);

            _ = await Assert.ThrowsAsync<HttpRequestException>(() => hub.StartAsync(CancellationToken.None));
            await Task.Delay(TimeSpan.FromMilliseconds(1500)); // the first backoff delay is 1s; a retry would land here

            Assert.Equal(ConnectionState.AuthFailed, states[^1]);
            Assert.DoesNotContain(ConnectionState.Reconnecting, states);
            Assert.Equal(1, connectCalls);
        }

        [Fact]
        public async Task RealtimeHub_InitialConnectCancelled_PublishesDisconnectedAndDoesNotRetry()
        {
            // A cancelled caller must not start a background retry loop.
            int connectCalls = 0;
            await using var hub = new RealtimeHub(ServerAddress, CreateServices(), ct =>
            {
                connectCalls++;
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });
            var states = new List<ConnectionState>();
            using IDisposable subscription = hub.Connection.Subscribe(states.Add);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hub.StartAsync(cts.Token));
            await Task.Delay(TimeSpan.FromMilliseconds(1500));

            Assert.Equal(ConnectionState.Disconnected, states[^1]);
            Assert.DoesNotContain(ConnectionState.Reconnecting, states);
            Assert.Equal(1, connectCalls);
        }

        [Fact]
        public async Task RealtimeHub_ReconnectLoopThrows_LogsAndPublishesDisconnected()
        {
            // Drives the loop through the seam: the first connect fails transiently, so the loop runs. Its second
            // connect returns 401 and then publishing AuthFailed throws (a subscriber fault). That exception escapes
            // the loop body, so the wrapper must publish Disconnected to the other subscriber.
            int connectCalls = 0;
            await using var hub = new RealtimeHub(ServerAddress, CreateServices(), _ =>
            {
                connectCalls++;
                if (connectCalls == 1)
                {
                    throw new InvalidOperationException("Server not reachable yet");
                }

                return Task.FromException(new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized));
            });
            using IDisposable faulty = hub.Connection.Subscribe(state =>
            {
                if (state == ConnectionState.AuthFailed)
                {
                    throw new InvalidOperationException("Subscriber fault on AuthFailed");
                }
            });
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using IDisposable observer = hub.Connection.Subscribe(state =>
            {
                if (state == ConnectionState.Disconnected)
                {
                    disconnected.TrySetResult();
                }
            });

            await hub.StartAsync(CancellationToken.None);
            await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task RealtimeHub_StartCalledTwice_ConnectsOnce()
        {
            int connectCalls = 0;
            await using var hub = new RealtimeHub(ServerAddress, CreateServices(), _ =>
            {
                connectCalls++;
                return Task.CompletedTask;
            });

            await hub.StartAsync(CancellationToken.None);
            await hub.StartAsync(CancellationToken.None);

            Assert.Equal(1, connectCalls);
        }

        [Fact]
        public async Task RealtimeHub_Closed401_EmitsAuthFailedAndDoesNotRestart()
        {
            int connectCalls = 0;
            await using var hub = new RealtimeHub(ServerAddress, CreateServices(), _ =>
            {
                connectCalls++;
                return Task.CompletedTask;
            });
            var states = new List<ConnectionState>();
            using IDisposable subscription = hub.Connection.Subscribe(states.Add);
            await hub.StartAsync(CancellationToken.None);
            var unauthorized = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);

            await hub.OnConnectionClosedAsync(unauthorized);

            Assert.Equal(ConnectionState.AuthFailed, states[^1]);
            Assert.DoesNotContain(ConnectionState.Reconnecting, states);
            Assert.Equal(1, connectCalls); // only the initial StartAsync; the 401 close must not reconnect
        }

        [Fact]
        public async Task RealtimeHub_StartRejectedWith401_EmitsAuthFailedAndRethrows()
        {
            await using var hub = new RealtimeHub(ServerAddress, CreateServices(), _ =>
                Task.FromException(new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized)));
            var states = new List<ConnectionState>();
            using IDisposable subscription = hub.Connection.Subscribe(states.Add);

            _ = await Assert.ThrowsAsync<HttpRequestException>(() => hub.StartAsync(CancellationToken.None));

            Assert.Equal(ConnectionState.AuthFailed, states[^1]);
        }
    }
}
