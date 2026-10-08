using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using VideoForensics.Hosting;
using VideoForensics.Hosting.BackgroundServices;
using VideoForensics.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Hosting.Tests
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

            _ = services.AddVideoForensicsScheduledSync(Config(("ScheduledTasks:TickSeconds", "30")));

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
        public void AddVideoForensicsScheduledSync_RingConfigured_BuildsWithScopeAndBuildValidationAndBindsOptions()
        {
            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddVideoForensicsScheduledSync(Config(
                ("ScheduledTasks:EnabledProviders:0", "Ring"),
                ("ScheduledTasks:TickSeconds", "15")));

            using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            ScheduledTasksOptions options = sp.GetRequiredService<IOptionsMonitor<ScheduledTasksOptions>>().CurrentValue;
            Assert.Equal(["Ring"], options.EnabledProviders);
            Assert.Equal(15, options.TickSeconds);
            Assert.Contains(sp.GetServices<IHostedService>(), s => s is ScheduledSyncService);
        }

        [Fact]
        public void ScheduledTasksOptions_Defaults_AreDisabledWithSixtySecondTick()
        {
            var options = new ScheduledTasksOptions();

            Assert.Empty(options.EnabledProviders);
            Assert.Equal(60, options.TickSeconds);
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
        }
    }
}