using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services
{
    /// <summary>
    /// The Database endpoints take <see cref="MaintenanceCoordinator"/> as a parameter. When it is not registered, ASP.NET cannot
    /// infer where the parameter comes from, and the whole host fails to start.
    /// </summary>
    public class MaintenanceServiceCollectionExtensionsTests
    {
        private static ServiceCollection BaseServices()
        {
            var services = new ServiceCollection();
            _ = services.AddLogging();
            _ = services.AddScoped(_ => Mock.Of<IDatabaseMaintenanceService>());
            return services;
        }

        [Fact]
        public void AddServerMaintenance_RegistersCoordinatorAsAServiceForEndpointParameters()
        {
            ServiceCollection services = BaseServices();
            _ = services.AddServerMaintenance();
            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

            // This is the question minimal APIs ask when they infer a parameter's source.
            Assert.True(provider.GetRequiredService<IServiceProviderIsService>().IsService(typeof(MaintenanceCoordinator)));

            using IServiceScope scope = provider.CreateScope();
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<MaintenanceCoordinator>());
        }

        [Fact]
        public void AddServerMaintenance_SharesOneGateBetweenTheConcreteTypeAndTheInterface()
        {
            ServiceCollection services = BaseServices();
            _ = services.AddServerMaintenance();
            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.Same(provider.GetRequiredService<MaintenanceGate>(), provider.GetRequiredService<IMaintenanceGate>());
        }

        [Fact]
        public async Task MapDatabaseEndpoints_WithMaintenanceRegistered_BuildsEveryEndpoint()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();
            _ = builder.Services.AddAuthorization();
            _ = builder.Services.AddScoped(_ => Mock.Of<IDatabaseMaintenanceService>());
            _ = builder.Services.AddScoped(_ => Mock.Of<IDatabaseAdministrationService>());
            _ = builder.Services.AddServerMaintenance();

            WebApplication app = builder.Build();
            app.MapDatabaseEndpoints();
            await app.StartAsync();
            try
            {
                // Reading the endpoints is what runs parameter inference, the step that failed at startup.
                Exception? ex = Record.Exception(() => _ = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.Count);
                Assert.Null(ex);
            }
            finally
            {
                await app.StopAsync();
                await app.DisposeAsync();
            }
        }
    }
}
