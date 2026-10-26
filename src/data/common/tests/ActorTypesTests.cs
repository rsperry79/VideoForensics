using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Data.Common.Tests
{
    /// <summary>A service account is recorded as System, never as a human actor.</summary>
    public class ActorTypesTests
    {
        [Theory]
        [InlineData("DRAGONFIRE$")]
        [InlineData("SYSTEM")]
        [InlineData("LOCAL SERVICE")]
        [InlineData("NETWORK SERVICE")]
        [InlineData("system")]
        public void ForAccount_IsSystemForServiceAccounts(string userName)
        {
            Assert.Equal(ActorType.System, ActorTypes.ForAccount(userName));
        }

        [Theory]
        [InlineData("rsper")]
        [InlineData("Richard Sperry")]
        public void ForAccount_IsHumanForPeople(string userName)
        {
            Assert.Equal(ActorType.Human, ActorTypes.ForAccount(userName));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ForAccount_IsHumanWhenThereIsNoName(string? userName)
        {
            Assert.Equal(ActorType.Human, ActorTypes.ForAccount(userName));
        }
    }
}
