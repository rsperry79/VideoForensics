
using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting;
using VideoForensics.Hosting.Contracts;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class LocalSelfTestStatusSourceTests
    {
        // A period far longer than any test runs, so only the explicit SampleOnceAsync calls sample.
        private static readonly TimeSpan NeverTicks = TimeSpan.FromHours(1);
        private static readonly DateTime Started = new(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);

        /// <summary>Counts GetStatusAsync calls, so a test can observe whether sampling is running.</summary>
        private sealed class CallCounter
        {
            private int _count;

            public int Count => Volatile.Read(ref _count);

            public void Increment() => Interlocked.Increment(ref _count);
        }

        private static Mock<IRingSelfTestService> BuildService(Func<SelfTestStatusDto> status, CallCounter? calls = null)
        {
            var service = new Mock<IRingSelfTestService>();
            service.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    calls?.Increment();
                    return status();
                });
            return service;
        }

        private static LocalSelfTestStatusSource BuildSource(IRingSelfTestService service, TimeSpan? interval = null)
        {
            return new LocalSelfTestStatusSource(
                service,
                new SelfTestStatusChangeDetector(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalSelfTestStatusSource>.Instance,
                interval ?? NeverTicks);
        }

        [Fact]
        public async Task LocalSelfTestStatusSource_SampleOnce_EmitsFirstStatus()
        {
            var expected = new SelfTestStatusDto(SelfTestRunStatus.Running, Started);
            using LocalSelfTestStatusSource source = BuildSource(BuildService(() => expected).Object);
            var received = new List<SelfTestStatusDto>();
            using IDisposable subscription = source.Status.Subscribe(received.Add);

            await source.SampleOnceAsync(CancellationToken.None);

            Assert.Single(received);
            Assert.Same(expected, received[0]);
        }

        [Fact]
        public async Task LocalSelfTestStatusSource_SampleOnceUnchanged_EmitsOnlyFirstStatus()
        {
            // Each call returns a new instance with equal values, so the comparison must be by value, not reference.
            using LocalSelfTestStatusSource source = BuildSource(
                BuildService(() => new SelfTestStatusDto(SelfTestRunStatus.Running, Started)).Object);
            var received = new List<SelfTestStatusDto>();
            using IDisposable subscription = source.Status.Subscribe(received.Add);

            await source.SampleOnceAsync(CancellationToken.None);
            await source.SampleOnceAsync(CancellationToken.None);
            await source.SampleOnceAsync(CancellationToken.None);

            Assert.Single(received);
        }

        [Fact]
        public async Task LocalSelfTestStatusSource_SampleOnceChanged_EmitsEachChange()
        {
            SelfTestStatusDto current = new(SelfTestRunStatus.Running, Started);
            using LocalSelfTestStatusSource source = BuildSource(BuildService(() => current).Object);
            var received = new List<SelfTestStatusDto>();
            using IDisposable subscription = source.Status.Subscribe(received.Add);

            await source.SampleOnceAsync(CancellationToken.None);
            current = new SelfTestStatusDto(SelfTestRunStatus.Completed, Started, Started.AddSeconds(9));
            await source.SampleOnceAsync(CancellationToken.None);

            Assert.Equal(2, received.Count);
            Assert.Equal(SelfTestRunStatus.Completed, received[1].Status);
        }

        [Fact]
        public async Task LocalSelfTestStatusSource_LateSubscriber_ReplaysLatestStatus()
        {
            var expected = new SelfTestStatusDto(SelfTestRunStatus.Failed, Started, Started.AddSeconds(2), "boom");
            using LocalSelfTestStatusSource source = BuildSource(BuildService(() => expected).Object);
            await source.SampleOnceAsync(CancellationToken.None);

            var received = new List<SelfTestStatusDto>();
            using IDisposable subscription = source.Status.Subscribe(received.Add);

            Assert.Single(received);
            Assert.Equal("boom", received[0].Error);
        }

        [Fact]
        public async Task LocalSelfTestStatusSource_SampleFails_SkipsTickAndEmitsNextSample()
        {
            var service = new Mock<IRingSelfTestService>();
            int calls = 0;
            service.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    calls++;
                    if (calls == 1)
                    {
                        throw new InvalidOperationException("transient");
                    }
                    return new SelfTestStatusDto(SelfTestRunStatus.Idle);
                });
            using LocalSelfTestStatusSource source = BuildSource(service.Object);
            var received = new List<SelfTestStatusDto>();
            using IDisposable subscription = source.Status.Subscribe(received.Add);

            await source.SampleOnceAsync(CancellationToken.None);
            await source.SampleOnceAsync(CancellationToken.None);

            Assert.Single(received);
            Assert.Equal(SelfTestRunStatus.Idle, received[0].Status);
        }

        [Fact]
        public async Task LocalSelfTestStatusSource_FirstSubscription_StartsSamplingLoop()
        {
            var calls = new CallCounter();
            var service = BuildService(() => new SelfTestStatusDto(SelfTestRunStatus.Idle), calls);
            using LocalSelfTestStatusSource source = BuildSource(service.Object, TimeSpan.FromMilliseconds(10));

            // Nothing samples before the first subscriber arrives.
            await Task.Delay(50);
            Assert.Equal(0, calls.Count);

            using IDisposable subscription = source.Status.Subscribe(_ => { });

            await WaitUntilAsync(() => calls.Count > 0, TimeSpan.FromSeconds(5));
            Assert.True(calls.Count > 0);
        }

        [Fact]
        public async Task LocalSelfTestStatusSource_Dispose_StopsSamplingLoop()
        {
            var calls = new CallCounter();
            var service = BuildService(() => new SelfTestStatusDto(SelfTestRunStatus.Idle), calls);
            var source = BuildSource(service.Object, TimeSpan.FromMilliseconds(10));
            using IDisposable subscription = source.Status.Subscribe(_ => { });
            await WaitUntilAsync(() => calls.Count > 0, TimeSpan.FromSeconds(5));

            source.Dispose();
            // A sample already in flight may still finish after Dispose, so let it settle before taking the baseline.
            await Task.Delay(100);
            int afterDispose = calls.Count;
            await Task.Delay(150);

            Assert.Equal(afterDispose, calls.Count);
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }
        }
    }
}
