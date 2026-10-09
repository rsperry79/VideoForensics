using System.Collections.Concurrent;
using System.Net;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Api.Contracts;
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
        public async Task RealtimeHub_StopDuringInitialConnect_DoesNotPublishConnectedOrStartReconnect()
        {
            // StopAsync arrives while the first connect is still in flight. When that connect later succeeds, the hub
            // must not report Connected, and must not start a reconnect loop either.
            var connectGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int connectCalls = 0;
            await using var hub = new RealtimeHub(ServerAddress, CreateServices(), async _ =>
            {
                connectCalls++;
                await connectGate.Task;
            });
            var states = new List<ConnectionState>();
            using IDisposable subscription = hub.Connection.Subscribe(states.Add);

            Task startTask = hub.StartAsync(CancellationToken.None);
            await hub.StopAsync();
            connectGate.SetResult();
            await startTask;
            await Task.Delay(TimeSpan.FromMilliseconds(1500)); // the first reconnect backoff is 1s; a loop would retry here

            Assert.DoesNotContain(ConnectionState.Connected, states);
            Assert.DoesNotContain(ConnectionState.Reconnecting, states);
            Assert.Equal(ConnectionState.Disconnected, states[^1]);
            Assert.Equal(1, connectCalls);
        }

        [Fact]
        public async Task RealtimeHub_StopDuringInitialConnectThatFails_DoesNotStartReconnectLoop()
        {
            // The first connect fails transiently after StopAsync was requested. The failure branch must not start the
            // background retry loop, so no second connect attempt happens.
            var connectGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int connectCalls = 0;
            await using var hub = new RealtimeHub(ServerAddress, CreateServices(), async _ =>
            {
                connectCalls++;
                await connectGate.Task;
                throw new InvalidOperationException("Server not reachable yet");
            });
            var states = new List<ConnectionState>();
            using IDisposable subscription = hub.Connection.Subscribe(states.Add);

            Task startTask = hub.StartAsync(CancellationToken.None);
            await hub.StopAsync();
            connectGate.SetResult();
            await startTask;
            await Task.Delay(TimeSpan.FromMilliseconds(1500));

            Assert.DoesNotContain(ConnectionState.Reconnecting, states);
            Assert.Equal(ConnectionState.Disconnected, states[^1]);
            Assert.Equal(1, connectCalls);
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

        [Fact]
        public async Task LiveViewSessionChanged_PushReceived_EmitsOnObservable()
        {
            // The On<> binding routes the push through EmitLiveViewSessionChanged; the seam stands in for the wire.
            await using var hub = new RealtimeHub(ServerAddress, CreateServices(), _ => Task.CompletedTask);
            var received = new List<LiveViewSessionDto>();
            using IDisposable subscription = hub.LiveViewSessionChanged.Subscribe(received.Add);
            LiveViewSessionDto dto = SampleSession();

            hub.EmitLiveViewSessionChanged(dto);

            LiveViewSessionDto single = Assert.Single(received);
            Assert.Equal(dto.Id, single.Id);
            Assert.Equal(dto.State, single.State);
        }

        [Fact]
        public async Task SubscribeLiveViewAsync_Connected_InvokesSubscribeLiveView()
        {
            Guid sessionId = Guid.NewGuid();
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHubWithInvoke(calls);
            await hub.StartAsync(CancellationToken.None);

            await hub.SubscribeLiveViewAsync(sessionId, CancellationToken.None);

            (string method, Guid invokedId) = Assert.Single(calls);
            Assert.Equal("SubscribeLiveView", method);
            Assert.Equal(sessionId, invokedId);
        }

        [Fact]
        public async Task UnsubscribeLiveViewAsync_Connected_InvokesUnsubscribeLiveView()
        {
            Guid sessionId = Guid.NewGuid();
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHubWithInvoke(calls);
            await hub.StartAsync(CancellationToken.None);
            await hub.SubscribeLiveViewAsync(sessionId, CancellationToken.None);

            await hub.UnsubscribeLiveViewAsync(sessionId, CancellationToken.None);

            var recorded = calls.ToArray();
            Assert.Equal(2, recorded.Length);
            Assert.Equal(("UnsubscribeLiveView", sessionId), recorded[1]);
        }

        [Fact]
        public async Task Reconnect_ReplaysTrackedSubscriptions()
        {
            // The reconnect loop runs through the connect seam. After the server drops the connection, group membership
            // is gone, so the tracked session must be re-subscribed once the connection is back.
            Guid sessionId = Guid.NewGuid();
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHubWithInvoke(calls);
            await hub.StartAsync(CancellationToken.None);
            await hub.SubscribeLiveViewAsync(sessionId, CancellationToken.None);
            (Task reconnected, IDisposable watch) = WatchForReconnect(hub);

            await hub.OnConnectionClosedAsync(new HttpRequestException("connection reset"));
            await reconnected.WaitAsync(TimeSpan.FromSeconds(10));
            watch.Dispose();

            Assert.Equal(2, calls.Count);
            Assert.All(calls, call => Assert.Equal(("SubscribeLiveView", sessionId), call));
        }

        [Fact]
        public async Task Reconnected_TrackedSubscriptions_ReplaysThem()
        {
            // Drives the built-in HubConnection.Reconnected handler through HandleReconnectedAsync. That handler is the
            // only path that restores group membership after SignalR's own automatic reconnect.
            Guid first = Guid.NewGuid();
            Guid second = Guid.NewGuid();
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHubWithInvoke(calls);
            await hub.StartAsync(CancellationToken.None);
            await hub.SubscribeLiveViewAsync(first, CancellationToken.None);
            await hub.SubscribeLiveViewAsync(second, CancellationToken.None);

            await hub.HandleReconnectedAsync();

            var replayed = calls.Skip(2).ToArray();
            Assert.Equal(2, replayed.Length);
            Assert.All(replayed, call => Assert.Equal("SubscribeLiveView", call.Method));
            Assert.Equal(new[] { first, second }.OrderBy(id => id), replayed.Select(call => call.SessionId).OrderBy(id => id));
        }

        [Fact]
        public async Task SubscribeLiveViewAsync_HubException_IsSwallowedAndNotReplayed()
        {
            Guid sessionId = Guid.NewGuid();
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHubWithInvoke(calls, method => method == "SubscribeLiveView"
                ? Task.FromException(new HubException("Unknown live-view session"))
                : Task.CompletedTask);
            await hub.StartAsync(CancellationToken.None);

            // An unknown session makes the server throw a HubException. It must not escape the subscribe call.
            await hub.SubscribeLiveViewAsync(sessionId, CancellationToken.None);

            (Task reconnected, IDisposable watch) = WatchForReconnect(hub);
            await hub.OnConnectionClosedAsync(new HttpRequestException("connection reset"));
            await reconnected.WaitAsync(TimeSpan.FromSeconds(10));
            watch.Dispose();

            // Only the rejected attempt was sent; the bad id was dropped and not replayed on reconnect.
            Assert.Single(calls);
        }

        [Fact]
        public async Task SubscribeLiveViewAsync_NotConnected_TracksAndSendsOnConnect()
        {
            Guid sessionId = Guid.NewGuid();
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHubWithInvoke(calls);

            await hub.SubscribeLiveViewAsync(sessionId, CancellationToken.None);
            Assert.Empty(calls);

            await hub.StartAsync(CancellationToken.None);

            (string method, Guid invokedId) = Assert.Single(calls);
            Assert.Equal("SubscribeLiveView", method);
            Assert.Equal(sessionId, invokedId);
        }

        private static RealtimeHub CreateHubWithInvoke(
            ConcurrentQueue<(string Method, Guid SessionId)> calls,
            Func<string, Task>? outcome = null)
        {
            return new RealtimeHub(ServerAddress, CreateServices(), _ => Task.CompletedTask,
                (method, sessionId, _) =>
                {
                    calls.Enqueue((method, sessionId));
                    return outcome?.Invoke(method) ?? Task.CompletedTask;
                });
        }

        // Completes once the hub has reported Reconnecting and then Connected again.
        private static (Task Reconnected, IDisposable Subscription) WatchForReconnect(RealtimeHub hub)
        {
            var reconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool reconnecting = false;
            IDisposable subscription = hub.Connection.Subscribe(state =>
            {
                if (state == ConnectionState.Reconnecting)
                {
                    reconnecting = true;
                }
                else if (state == ConnectionState.Connected && reconnecting)
                {
                    reconnected.TrySetResult();
                }
            });
            return (reconnected.Task, subscription);
        }

        private static LiveViewSessionDto SampleSession() => new(
            Guid.NewGuid(), Guid.NewGuid(), "Manual", "Active",
            new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc), null,
            new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc), false, null, null, null, null, null);
    }
}
