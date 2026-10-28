using VideoForensics.Client.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Services
{
    public class ForensicsConfigurationContractTests
    {
        [Fact]
        public void IForensicsConfiguration_ActiveProviderAccountIdProperty_IsRemoved()
        {
            var property = typeof(IForensicsConfiguration).GetProperty("ActiveProviderAccountId");

            Assert.Null(property);
        }
    }
}
