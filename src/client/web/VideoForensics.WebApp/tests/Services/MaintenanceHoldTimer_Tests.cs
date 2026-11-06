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

            _ = Assert.NotNull(token);
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
            _ = Assert.NotNull(timer.TryStart(() => { }));
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
                _ = expired.TrySetResult();
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

        [Fact]
        public void TryReserve_WhileIdle_RecordsHoldStartTime()
        {
            var clock = new FakeClock(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30), TimeSpan.FromHours(4), clock);

            _ = timer.TryReserve(() => { });

            Assert.Equal(clock.Now, timer.HoldStartedAt);
        }

        [Fact]
        public void Renew_WithinFourHourCap_ReturnsTrue()
        {
            var clock = new FakeClock(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30), TimeSpan.FromHours(4), clock);
            Guid token = timer.TryStart(() => { })!.Value;
            clock.Now = clock.Now.AddHours(3);

            Assert.Equal(HoldRenewResult.Renewed, timer.RenewOutcome(token));
        }

        [Fact]
        public void Renew_PastFourHourCap_ReturnsMaxAgeReached()
        {
            var clock = new FakeClock(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30), TimeSpan.FromHours(4), clock);
            Guid token = timer.TryStart(() => { })!.Value;
            clock.Now = clock.Now.AddHours(4).AddMinutes(1);

            Assert.Equal(HoldRenewResult.MaxAgeReached, timer.RenewOutcome(token));
            Assert.False(timer.Renew(token));
        }

        [Fact]
        public void Renew_WrongToken_ReturnsNotActive()
        {
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30));
            _ = timer.TryStart(() => { });

            Assert.Equal(HoldRenewResult.NotActive, timer.RenewOutcome(Guid.NewGuid()));
        }

        [Fact]
        public async Task ArmSafetyTimer_AfterReserve_DoesNotExpireBeforeArmed()
        {
            int calls = 0;
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMilliseconds(20));
            Guid token = timer.TryReserve(() => calls++)!.Value;

            await Task.Delay(100);
            Assert.Equal(0, calls);

            Assert.True(timer.ArmSafetyTimer(token));
            await Task.Delay(150);
            Assert.Equal(1, calls);
        }

        [Fact]
        public void ArmSafetyTimer_WrongToken_ReturnsFalse()
        {
            var timer = new MaintenanceHoldTimer(TimeSpan.FromMinutes(30));
            _ = timer.TryReserve(() => { });

            Assert.False(timer.ArmSafetyTimer(Guid.NewGuid()));
        }

        private sealed class FakeClock(DateTimeOffset start) : TimeProvider
        {
            public DateTimeOffset Now { get; set; } = start;

            public override DateTimeOffset GetUtcNow()
            {
                return Now;
            }
        }
    }
}
