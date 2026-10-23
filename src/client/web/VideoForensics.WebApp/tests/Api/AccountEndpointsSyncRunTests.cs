using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using System.Text.Json;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// Route-level tests for the account sync-run read endpoints. Handlers are invoked directly from the
    /// endpoint data source, so authorization filters are not evaluated here.
    /// </summary>
    public class AccountEndpointsSyncRunTests
    {
        private const string ListRoute = "/api/v1/accounts/provider-accounts/{id:guid}/sync-runs";
        private const string DetailRoute = "/api/v1/accounts/provider-accounts/{id:guid}/sync-runs/{runId:guid}";

        private static WebApplication BuildApp()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();

            // Every type the account endpoints inject must be a registered service, otherwise route
            // inference treats it as a request body parameter.
            _ = builder.Services.AddSingleton(Mock.Of<IUserRepository>());
            _ = builder.Services.AddSingleton(Mock.Of<IProviderAccountRepository>());
            _ = builder.Services.AddSingleton(Mock.Of<ICaseRepository>());
            _ = builder.Services.AddSingleton(Mock.Of<IAccountSyncRunner>());
            _ = builder.Services.AddSingleton(Mock.Of<IRingAccountSyncState>());
            _ = builder.Services.AddSingleton(Mock.Of<IProviderSyncRunRepository>());
            _ = builder.Services.AddSingleton(Mock.Of<Common.Contracts.IAccountExportService>());
            WebApplication app = builder.Build();
            app.MapAccountEndpoints();
            return app;
        }

        private static RequestDelegate FindHandler(WebApplication app, string routeText)
        {
            return ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(ds => ds.Endpoints)
                .OfType<RouteEndpoint>()
                .Single(e => e.RoutePattern.RawText == routeText)
                .RequestDelegate!;
        }

        private static async Task<DefaultHttpContext> InvokeAsync(
            RequestDelegate handler,
            IProviderSyncRunRepository runs,
            Guid accountId,
            Guid? runId)
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton(runs);
            _ = services.AddLogging();
            _ = services.AddOptions();
            _ = services.Configure<JsonOptions>(_ => { });

            var context = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider()
            };
            context.Response.Body = new MemoryStream();
            context.Request.RouteValues["id"] = accountId.ToString();
            if (runId.HasValue)
            {
                context.Request.RouteValues["runId"] = runId.Value.ToString();
            }

            await handler(context);
            return context;
        }

        private static ProviderSyncRun CreateRun(Guid accountId, Guid? id = null)
        {
            return new()
            {
                Id = id ?? Guid.NewGuid(),
                ProviderAccountId = accountId,
                StartedUtc = DateTime.UtcNow.AddHours(-1),
                CompletedUtc = DateTime.UtcNow.AddHours(-1).AddMinutes(3),
                Success = false,
                ErrorMessage = "Credentials expired"
            };
        }

        [Fact]
        public void MapAccountEndpoints_SyncRunRoutes_AreRegistered()
        {
            WebApplication app = BuildApp();

            RequestDelegate list = FindHandler(app, ListRoute);
            RequestDelegate detail = FindHandler(app, DetailRoute);

            Assert.NotNull(list);
            Assert.NotNull(detail);
        }

        [Fact]
        public async Task SyncRunList_AccountWithRuns_ReturnsOkAndQueriesByAccount()
        {
            WebApplication app = BuildApp();
            var accountId = Guid.NewGuid();
            var runs = new Mock<IProviderSyncRunRepository>();
            _ = runs.Setup(r => r.ListByProviderAccountIdAsync(accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([CreateRun(accountId)]);

            DefaultHttpContext context = await InvokeAsync(FindHandler(app, ListRoute), runs.Object, accountId, null);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            runs.Verify(r => r.ListByProviderAccountIdAsync(accountId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SyncRunDetail_RunBelongsToAccount_ReturnsOkWithDetail()
        {
            WebApplication app = BuildApp();
            var accountId = Guid.NewGuid();
            ProviderSyncRun run = CreateRun(accountId);
            var runs = new Mock<IProviderSyncRunRepository>();
            _ = runs.Setup(r => r.GetAsync(run.Id, It.IsAny<CancellationToken>())).ReturnsAsync(run);

            DefaultHttpContext context = await InvokeAsync(FindHandler(app, DetailRoute), runs.Object, accountId, run.Id);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            context.Response.Body.Position = 0;
            ProviderSyncRunDetailDto? dto = await JsonSerializer.DeserializeAsync<ProviderSyncRunDetailDto>(
                context.Response.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(dto);
            Assert.Equal(run.Id, dto!.Id);
            Assert.Equal(accountId, dto.ProviderAccountId);
            Assert.Equal("Credentials expired", dto.ErrorMessage);
        }

        [Fact]
        public async Task SyncRunDetail_RunBelongsToOtherAccount_ReturnsNotFound()
        {
            WebApplication app = BuildApp();
            var requestedAccountId = Guid.NewGuid();
            ProviderSyncRun run = CreateRun(Guid.NewGuid());
            var runs = new Mock<IProviderSyncRunRepository>();
            _ = runs.Setup(r => r.GetAsync(run.Id, It.IsAny<CancellationToken>())).ReturnsAsync(run);

            DefaultHttpContext context = await InvokeAsync(FindHandler(app, DetailRoute), runs.Object, requestedAccountId, run.Id);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        }

        [Fact]
        public async Task SyncRunDetail_UnknownRun_ReturnsNotFound()
        {
            WebApplication app = BuildApp();
            var accountId = Guid.NewGuid();
            var runId = Guid.NewGuid();
            var runs = new Mock<IProviderSyncRunRepository>();
            _ = runs.Setup(r => r.GetAsync(runId, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderSyncRun?)null);

            DefaultHttpContext context = await InvokeAsync(FindHandler(app, DetailRoute), runs.Object, accountId, runId);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        }
    }
}
