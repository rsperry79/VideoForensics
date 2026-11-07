using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.BackgroundServices;
using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Client.Hosting.Tests
{
    public class ScheduledSyncRegistrationTests
    {
        private static IConfiguration Config(params (string Key, string Value)[] values)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
                .Build();
        }

        [Fact]
        public void AddVideoForensicsScheduledSync_NoSection_RegistersNothing()
        {
            var services = new ServiceCollection();

            _ = services.AddVideoForensicsScheduledSync(Config());

            Assert.Empty(services);
        }

        [Fact]
        public void AddVideoForensicsScheduledSync_EmptyProviderList_RegistersNothing()
        {
            var services = new ServiceCollection();

            _ = services.AddVideoForensicsScheduledSync(Config());

            Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService));
            Assert.DoesNotContain(services, d => d.ServiceType == typeof(IEventAndConfigService));
            Assert.DoesNotContain(services, d => d.ServiceType == typeof(IProviderAuthService));
        }

        [Fact]
        public void AddVideoForensicsScheduledSync_RingConfigured_RegistersHostedServiceAndKeyedEventService()
        {
            var services = new ServiceCollection();

            _ = services.AddVideoForensicsScheduledSync(Config(("ScheduledTasks:EnabledProviders:0", "Ring")));

            Assert.Contains(services, d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ScheduledSyncService));
            Assert.Contains(services, d => d.ServiceType == typeof(IEventAndConfigService) && d.IsKeyedService && Equals(d.ServiceKey, "Ring"));
            Assert.Contains(services, d => d.ServiceType == typeof(IProviderAuthService) && d.IsKeyedService && Equals(d.ServiceKey, "Ring"));
        }

        [Fact]
        public void AddVideoForensicsScheduledSync_UnknownProvider_Throws()
        {
            var services = new ServiceCollection();

            _ = Assert.Throws<InvalidOperationException>(() =>
                services.AddVideoForensicsScheduledSync(Config(("ScheduledTasks:EnabledProviders:0", "Nope"))));
        }

        [Fact]
        public void AddVideoForensicsScheduledSync_TimeProviderAlreadyRegistered_KeepsExistingRegistration()
        {
            var services = new ServiceCollection();
            var custom = new FixedTimeProvider();
            _ = services.AddSingleton<TimeProvider>(custom);

            _ = services.AddVideoForensicsScheduledSync(Config(("ScheduledTasks:EnabledProviders:0", "Ring")));

            using ServiceProvider sp = services.BuildServiceProvider();
            Assert.Same(custom, sp.GetRequiredService<TimeProvider>());
        }

        [Fact]
        public void AddVideoForensicsScheduledSync_RingConfigured_BuildsWithScopeAndReadsTickFromConfiguration()
        {
            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddSingleton<IForensicsConfiguration>(new ForensicsConfiguration { ScheduledSyncTickSeconds = 15 });
            _ = services.AddVideoForensicsScheduledSync(Config(("ScheduledTasks:EnabledProviders:0", "Ring")));

            using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            ScheduledTasksOptions options = sp.GetRequiredService<IOptionsMonitor<ScheduledTasksOptions>>().CurrentValue;
            Assert.Equal(["Ring"], options.EnabledProviders);
            ScheduledSyncService service = sp.GetServices<IHostedService>().OfType<ScheduledSyncService>().Single();
            Assert.Equal(TimeSpan.FromSeconds(15), service.CurrentTickInterval);
        }

        [Fact]
        public void ScheduledTasksOptions_Defaults_AreDisabled()
        {
            var options = new ScheduledTasksOptions();

            Assert.Empty(options.EnabledProviders);
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
        }
    }
}