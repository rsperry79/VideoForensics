using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests
{
    public class AccountLinkedBusTests
    {
        [Fact]
        public void Publish_SubscriberRegistered_ReceivesId()
        {
            var bus = new AccountLinkedBus();
            var id = Guid.NewGuid();
            var received = new List<Guid>();

            using var subscription = bus.AccountLinked.Subscribe(received.Add);
            bus.Publish(id);

            Assert.Equal(new[] { id }, received);
        }

        [Fact]
        public void Publish_SubscriberDisposed_ReceivesNothing()
        {
            var bus = new AccountLinkedBus();
            var received = new List<Guid>();

            var subscription = bus.AccountLinked.Subscribe(received.Add);
            subscription.Dispose();
            bus.Publish(Guid.NewGuid());

            Assert.Empty(received);
        }

        [Fact]
        public void Subscribe_AfterPublish_DoesNotReceiveEarlierId()
        {
            var bus = new AccountLinkedBus();
            var received = new List<Guid>();

            bus.Publish(Guid.NewGuid());
            using var subscription = bus.AccountLinked.Subscribe(received.Add);

            Assert.Empty(received);
        }

        [Fact]
        public void Publish_TwoSubscribers_BothReceive()
        {
            var bus = new AccountLinkedBus();
            var id = Guid.NewGuid();
            var first = new List<Guid>();
            var second = new List<Guid>();

            using var s1 = bus.AccountLinked.Subscribe(first.Add);
            using var s2 = bus.AccountLinked.Subscribe(second.Add);
            bus.Publish(id);

            Assert.Equal(new[] { id }, first);
            Assert.Equal(new[] { id }, second);
        }
    }
}
