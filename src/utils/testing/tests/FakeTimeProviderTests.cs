using Xunit;

namespace VideoForensics.Utils.Testing.Tests
{
    /// <summary>The fake clock returns the time it was given and moves only when told to.</summary>
    public class FakeTimeProviderTests
    {
        [Fact]
        public void GetUtcNow_WithFixedTime_ReturnsThatTimeAsUtc()
        {
            var clock = new FakeTimeProvider(new DateTime(2026, 10, 7, 14, 35, 42, DateTimeKind.Utc));

            Assert.Equal(new DateTimeOffset(2026, 10, 7, 14, 35, 42, TimeSpan.Zero), clock.GetUtcNow());
        }

        [Fact]
        public void GetUtcNow_DoesNotAdvanceOnItsOwn()
        {
            var clock = new FakeTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            DateTimeOffset first = clock.GetUtcNow();
            DateTimeOffset second = clock.GetUtcNow();

            Assert.Equal(first, second);
        }

        [Fact]
        public void SetUtcNow_MovesTheClock()
        {
            var clock = new FakeTimeProvider(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            clock.SetUtcNow(new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc));

            Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 5, 0, TimeSpan.Zero), clock.GetUtcNow());
        }
    }
}
