using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Moq;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Services;
using VideoForensics.WebApp.Api;

using Xunit;

namespace VideoForensics.WebApp.Tests.Api
{
    /// <summary>
    /// Route-level tests for the account cases read endpoint. Handlers are invoked directly from the endpoint
    /// data source, so authorization filters are not evaluated here.
    /// </summary>
    public class AccountEndpointsCasesTests
    {
        private const string CasesRoute = "/api/v1/accounts/provider-accounts/{id:guid}/cases";

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
            _ = builder.Services.AddSingleton(Mock.Of<VideoForensics.Client.Common.Contracts.IAccountExportService>());
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
            ICaseRepository cases,
            Guid accountId,
            CancellationToken requestAborted = default)
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton(cases);
            _ = services.AddLogging();
            _ = services.AddOptions();
            _ = services.Configure<JsonOptions>(_ => { });

            var context = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider(),
                RequestAborted = requestAborted
            };
            context.Response.Body = new MemoryStream();
            context.Request.RouteValues["id"] = accountId.ToString();

            await handler(context);
            return context;
        }

        private static ForensicCase CreateCase(string caseNumber) => new()
        {
            Id = Guid.NewGuid(),
            CaseNumber = caseNumber,
            Title = "Driveway motion",
            Status = CaseStatus.Open,
            CreatedBy = "admin",
            CreatedAtUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            ScopeFromUtc = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)
        };

        [Fact]
        public void MapAccountEndpoints_CasesRoute_IsRegistered()
        {
            WebApplication app = BuildApp();

            RequestDelegate handler = FindHandler(app, CasesRoute);

            Assert.NotNull(handler);
        }

        [Fact]
        public async Task AccountCases_AccountWithCases_ReturnsOkWithAccountCaseDtos()
        {
            WebApplication app = BuildApp();
            var accountId = Guid.NewGuid();
            ForensicCase caseEntity = CreateCase("detected-2026-10-01-12-00-0");
            var cases = new Mock<ICaseRepository>();
            cases.Setup(c => c.ListForProviderAccountAsync(accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ForensicCase> { caseEntity });

            DefaultHttpContext context = await InvokeAsync(FindHandler(app, CasesRoute), cases.Object, accountId);

            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            cases.Verify(c => c.ListForProviderAccountAsync(accountId, It.IsAny<CancellationToken>()), Times.Once);

            context.Response.Body.Position = 0;
            List<AccountCaseDto>? dtos = await JsonSerializer.DeserializeAsync<List<AccountCaseDto>>(
                context.Response.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.NotNull(dtos);
            AccountCaseDto dto = Assert.Single(dtos!);
            Assert.Equal(caseEntity.Id, dto.Id);
            Assert.Equal(caseEntity.CaseNumber, dto.CaseNumber);
            Assert.Equal("Detected", dto.CaseType);
            Assert.Equal("Open", dto.Status);
        }

        [Fact]
        public async Task AccountCases_CancelledRequest_PropagatesOperationCanceledException()
        {
            WebApplication app = BuildApp();
            var accountId = Guid.NewGuid();
            var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var cases = new Mock<ICaseRepository>();
            cases.Setup(c => c.ListForProviderAccountAsync(accountId, It.IsAny<CancellationToken>()))
                .Returns((Guid _, CancellationToken ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult<IReadOnlyList<ForensicCase>>(new List<ForensicCase>());
                });

            _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
                InvokeAsync(FindHandler(app, CasesRoute), cases.Object, accountId, cts.Token));
        }
    }
}
