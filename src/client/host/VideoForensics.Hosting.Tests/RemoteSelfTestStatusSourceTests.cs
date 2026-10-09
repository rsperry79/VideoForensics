using System.Reactive.Subjects;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Hosting.Remote;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class RemoteSelfTestStatusSourceTests
    {
        private static readonly DateTime Started = new(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);

        private static Mock<IRealtimeStore> BuildStore(Subject<SelfTestStatusDto> subject)
        {
            var store = new Mock<IRealtimeStore>();
            store.SetupGet(s => s.SelfTestStatus).Returns(subject);
            store.SetupGet(s => s.LatestSelfTestStatus).Returns((SelfTestStatusDto?)null);
            return store;
        }

        [Fact]
        public void RemoteSelfTestStatusSource_Status_ForwardsStoreEmissionsUnchanged()
        {
            var subject = new Subject<SelfTestStatusDto>();
            var source = new RemoteSelfTestStatusSource(BuildStore(subject).Object);
            var running = new SelfTestStatusDto(SelfTestRunStatus.Running, Started);

            var received = new List<SelfTestStatusDto>();
            using IDisposable subscription = source.Status.Subscribe(received.Add);
            subject.OnNext(running);

            Assert.Single(received);
            Assert.Same(running, received[0]);
        }

        [Fact]
        public void RemoteSelfTestStatusSource_Status_SilentUntilStoreEmits()
        {
            var subject = new Subject<SelfTestStatusDto>();
            var source = new RemoteSelfTestStatusSource(BuildStore(subject).Object);

            var received = new List<SelfTestStatusDto>();
            using IDisposable subscription = source.Status.Subscribe(received.Add);

            Assert.Empty(received);
        }

        [Fact]
        public void RemoteSelfTestStatusSource_Status_DisposedSubscriptionStopsDelivery()
        {
            var subject = new Subject<SelfTestStatusDto>();
            var source = new RemoteSelfTestStatusSource(BuildStore(subject).Object);

            var received = new List<SelfTestStatusDto>();
            IDisposable subscription = source.Status.Subscribe(received.Add);
            subscription.Dispose();
            subject.OnNext(new SelfTestStatusDto(SelfTestRunStatus.Completed, Started, Started.AddSeconds(5)));

            Assert.Empty(received);
        }
    }
}
