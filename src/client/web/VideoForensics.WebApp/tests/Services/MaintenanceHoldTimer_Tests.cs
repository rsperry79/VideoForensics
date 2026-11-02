using VideoForensics.Client.WebApp.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class MaintenanceHoldTimer_Tests
    {
        [Fact]
        public void TryStart_WhileIdle_ReturnsToken()
        {
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30));

            Guid? token = timer.TryStart(() => { });

            Assert.NotNull(token);
            Assert.NotEqual(Guid.Empty, token.Value);
        }

        [Fact]
        public void TryStart_WhileHoldActive_ReturnsNull()
        {
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30));
            _ = timer.TryStart(() => { });

            Assert.Null(timer.TryStart(() => { }));
        }

        [Fact]
        public void TryEnd_WrongToken_ReturnsFalseAndDoesNotRelease()
        {
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30));
            _ = timer.TryStart(() => { });
            bool released = false;

            bool ended = timer.TryEnd(Guid.NewGuid(), () => released = true);

            Assert.False(ended);
            Assert.False(released);
        }

        [Fact]
        public void TryEnd_ActiveToken_ReleasesAndFreesTheSlot()
        {
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30));
            Guid token = timer.TryStart(() => { })!.Value;
            bool released = false;

            bool ended = timer.TryEnd(token, () => released = true);

            Assert.True(ended);
            Assert.True(released);
            Assert.NotNull(timer.TryStart(() => { }));
        }

        [Fact]
        public void Renew_ActiveToken_ReturnsTrue()
        {
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30));
            Guid token = timer.TryStart(() => { })!.Value;

            Assert.True(timer.Renew(token));
        }

        [Fact]
        public void Renew_WrongToken_ReturnsFalse()
        {
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30));
            _ = timer.TryStart(() => { });

            Assert.False(timer.Renew(Guid.NewGuid()));
        }

        [Fact]
        public async Task TryStart_Expires_RunsCallbackOnce()
        {
            var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls = 0;
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMilliseconds(20));
            _ = timer.TryStart(() =>
            {
                calls++;
                expired.TrySetResult();
            });

            await expired.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, calls);
        }

        [Fact]
        public async Task TryEnd_BeforeExpiry_PreventsCallback()
        {
            int calls = 0;
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMilliseconds(50));
            Guid token = timer.TryStart(() => calls++)!.Value;
            Assert.True(timer.TryEnd(token, () => { }));

            await Task.Delay(150);

            Assert.Equal(0, calls);
        }
    }
}
