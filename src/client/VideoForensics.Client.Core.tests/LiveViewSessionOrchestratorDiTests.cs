using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Forensics;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Interfaces;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    /// <summary>
    /// Guards against the captive-dependency regression where the singleton orchestrator captured
    /// scoped repositories in its constructor. Built with scope validation enabled, the same way
    /// ASP.NET Core Development hosts build their container, so the failure surfaces in unit tests.
    /// </summary>
    public class LiveViewSessionOrchestratorDiTests
    {
        [Fact]
        public void LiveViewSessionOrchestrator_SingletonWithScopedDependencies_BuildsUnderScopeValidation()
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton<ILogger<LiveViewSessionOrchestrator>>(NullLogger<LiveViewSessionOrchestrator>.Instance);
            _ = services.AddSingleton(Mock.Of<ILiveViewInterferenceScorer>());
            _ = services.AddSingleton(Mock.Of<IForensicsConfiguration>());

            // Scoped in production (see data.database DI registrations and AddVideoForensicsServerCore).
            _ = services.AddScoped(_ => Mock.Of<ILiveViewSessionRepository>());
            _ = services.AddScoped(_ => Mock.Of<IDeviceRepository>());
            _ = services.AddScoped(_ => Mock.Of<IProviderApiBudgetGuard>());
            _ = services.AddScoped(_ => Mock.Of<INotificationDispatcher>());

            _ = services.AddSingleton<ILiveViewSessionService, LiveViewSessionOrchestrator>();

            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true,
            });

            Assert.IsType<LiveViewSessionOrchestrator>(provider.GetRequiredService<ILiveViewSessionService>());
        }
    }
}
