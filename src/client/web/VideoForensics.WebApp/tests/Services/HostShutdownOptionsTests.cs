using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using VideoForensics.Client.WebApp.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    public class HostShutdownOptionsTests
    {
        [Fact]
        public void HostShutdownOptions_Apply_SetsShutdownTimeout()
        {
            var services = new ServiceCollection();

            HostShutdownOptions.Apply(services);

            HostOptions options = services.BuildServiceProvider().GetRequiredService<IOptions<HostOptions>>().Value;
            Assert.Equal(HostShutdownOptions.ShutdownTimeout, options.ShutdownTimeout);
        }

        [Fact]
        public void HostShutdownOptions_ShutdownTimeout_ExceedsHostDefault()
        {
            // The host default is 30 seconds; a stop that runs past it cancels the stop token and crashes the process.
            Assert.True(HostShutdownOptions.ShutdownTimeout > TimeSpan.FromSeconds(30));
        }
    }
}
