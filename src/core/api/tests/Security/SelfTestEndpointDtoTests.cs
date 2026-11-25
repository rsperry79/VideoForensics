using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    public class SelfTestEndpointDtoTests
    {
        [Fact]
        public void SelfTestEndpointDto_WithProvider_ExposesProvider()
        {
            var dto = new SelfTestEndpointDto("k", "Name", "Desc", "Session", "GET", "/p", "None", false, false, "Ring");

            Assert.Equal("Ring", dto.Provider);
        }
    }
}
