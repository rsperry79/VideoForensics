using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Hosting.Tests
{
    public class RealtimeLiveViewSessionSourceTests
    {
        private static readonly Uri ServerAddress = new("http://127.0.0.1:1");
        private static readonly DateTime Start = new(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);

        private static IServiceProvider CreateServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new PairedSessionState(new Mock<IJSRuntime>().Object));
            return services.BuildServiceProvider();
        }

        private static RealtimeHub CreateHub(ConcurrentQueue<(string Method, Guid SessionId)> calls)
        {
            return new RealtimeHub(ServerAddress, CreateServices(), _ => Task.CompletedTask,
                (method, sessionId, _) =>
                {
                    calls.Enqueue((method, sessionId));
                    return Task.CompletedTask;
                });
        }

        [Fact]
        public async Task RealtimeLiveViewSessionSource_PushReceived_MapsDtoToEntity()
        {
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHub(calls);
            var source = new RealtimeLiveViewSessionSource(hub);
            var received = new List<LiveViewSession>();
            using IDisposable subscription = source.SessionChanged.Subscribe(received.Add);
            var sessionId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var dto = new LiveViewSessionDto(
                sessionId, deviceId, "JammingSuspected", "Active", Start, null, Start, true, Start,
                "sustained", operatorId, null, "provider-ref");

            hub.EmitLiveViewSessionChanged(dto);

            LiveViewSession mapped = Assert.Single(received);
            Assert.Equal(sessionId, mapped.Id);
            Assert.Equal(deviceId, mapped.DeviceId);
            Assert.Equal(LiveViewTriggerReason.JammingSuspected, mapped.TriggerReason);
            Assert.Equal(LiveViewSessionState.Active, mapped.State);
            Assert.True(mapped.IsSustained);
            Assert.Equal(operatorId, mapped.OperatorId);
            Assert.Equal("provider-ref", mapped.ProviderSessionRef);
        }

        [Fact]
        public async Task Telemetry_MapsDtoToEntity()
        {
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHub(calls);
            var source = new RealtimeLiveViewSessionSource(hub);
            var received = new List<LiveViewTelemetrySample>();
            using IDisposable subscription = source.Telemetry.Subscribe(received.Add);
            var dto = new LiveViewTelemetrySampleDto(
                Guid.NewGuid(), Guid.NewGuid(), Start,
                FractionLost: 255, CumulativePacketsLost: 42, JitterTicks: 1200u, BitrateBps: 4_000_000L, InterferenceScore: 0.87);

            hub.EmitLiveViewTelemetry(dto);

            LiveViewTelemetrySample mapped = Assert.Single(received);
            Assert.Equal(dto.Id, mapped.Id);
            Assert.Equal(dto.SessionId, mapped.SessionId);
            Assert.Equal(Start, mapped.CapturedAtUtc);
            Assert.Equal((byte?)255, mapped.FractionLost);
            Assert.Equal(42, mapped.CumulativePacketsLost);
            Assert.Equal(1200u, mapped.JitterTicks);
            Assert.Equal(4_000_000L, mapped.BitrateBps);
            Assert.Equal(0.87, mapped.InterferenceScore);
        }

        [Fact]
        public async Task Telemetry_MapsNullFieldsToNull()
        {
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHub(calls);
            var source = new RealtimeLiveViewSessionSource(hub);
            var received = new List<LiveViewTelemetrySample>();
            using IDisposable subscription = source.Telemetry.Subscribe(received.Add);
            var dto = new LiveViewTelemetrySampleDto(
                Guid.NewGuid(), Guid.NewGuid(), Start,
                FractionLost: null, CumulativePacketsLost: null, JitterTicks: null, BitrateBps: null, InterferenceScore: null);

            hub.EmitLiveViewTelemetry(dto);

            LiveViewTelemetrySample mapped = Assert.Single(received);
            Assert.Null(mapped.FractionLost);
            Assert.Null(mapped.CumulativePacketsLost);
            Assert.Null(mapped.JitterTicks);
            Assert.Null(mapped.BitrateBps);
            Assert.Null(mapped.InterferenceScore);
        }

        [Fact]
        public async Task Telemetry_ForwardsRealtimeHubEmissions()
        {
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHub(calls);
            var source = new RealtimeLiveViewSessionSource(hub);
            var received = new List<LiveViewTelemetrySample>();
            using IDisposable subscription = source.Telemetry.Subscribe(received.Add);

            hub.EmitLiveViewTelemetry(new LiveViewTelemetrySampleDto(Guid.NewGuid(), Guid.NewGuid(), Start, null, null, null, null, null));
            hub.EmitLiveViewTelemetry(new LiveViewTelemetrySampleDto(Guid.NewGuid(), Guid.NewGuid(), Start, null, null, null, null, 0.1));

            Assert.Equal(2, received.Count);
            Assert.Equal(0.1, received[1].InterferenceScore);
        }

        [Fact]
        public async Task RealtimeLiveViewSessionSource_SubscribeAsync_ForwardsToHub()
        {
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHub(calls);
            await hub.StartAsync(CancellationToken.None);
            var source = new RealtimeLiveViewSessionSource(hub);
            var sessionId = Guid.NewGuid();

            await source.SubscribeAsync(sessionId, CancellationToken.None);

            (string method, Guid invokedId) = Assert.Single(calls);
            Assert.Equal("SubscribeLiveView", method);
            Assert.Equal(sessionId, invokedId);
        }

        [Fact]
        public async Task RealtimeLiveViewSessionSource_UnsubscribeAsync_ForwardsToHub()
        {
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHub(calls);
            await hub.StartAsync(CancellationToken.None);
            var source = new RealtimeLiveViewSessionSource(hub);
            var sessionId = Guid.NewGuid();

            await source.UnsubscribeAsync(sessionId, CancellationToken.None);

            (string method, Guid invokedId) = Assert.Single(calls);
            Assert.Equal("UnsubscribeLiveView", method);
            Assert.Equal(sessionId, invokedId);
        }

        [Fact]
        public async Task RealtimeLiveViewSessionSource_HubNotConnected_IsConnectedFalse()
        {
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHub(calls);
            var source = new RealtimeLiveViewSessionSource(hub);

            Assert.False(source.IsConnected);
        }

        [Fact]
        public async Task RealtimeLiveViewSessionSource_HubConnected_IsConnectedTrue()
        {
            var calls = new ConcurrentQueue<(string Method, Guid SessionId)>();
            await using var hub = CreateHub(calls);
            await hub.StartAsync(CancellationToken.None);
            var source = new RealtimeLiveViewSessionSource(hub);

            Assert.True(source.IsConnected);
        }
    }
}
