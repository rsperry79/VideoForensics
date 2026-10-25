using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

using VideoForensics.Client.Platform.Windows.Logging;

using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class WindowsLoggingProvidersTests
    {
        /// <summary>Verifies that AddEventLog registers the Event Log logger provider.</summary>
        [Fact]
        public void AddEventLog_Always_RegistersEventLogProvider()
        {
            var services = new ServiceCollection();
            _ = services.AddLogging(builder => WindowsLoggingProviders.AddEventLog(builder));

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.Contains(provider.GetServices<ILoggerProvider>(), p => p is EventLogLoggerProvider);
        }
    }
}
