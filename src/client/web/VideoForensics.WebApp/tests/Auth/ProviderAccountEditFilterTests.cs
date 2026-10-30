using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Auth
{
    public class ProviderAccountEditFilter_Tests
    {
        private static DefaultHttpContext ContextFor(OperatorRole role)
        {
            var identity = new ClaimsIdentity(new[] { new Claim(VideoForensicsClaimTypes.Role, role.ToString()) }, "test");
            return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        }

        private static ProviderAccountEditFilter FilterRequiring(AccessPolicy required)
        {
            var policy = new Mock<IProviderAccountEditPolicy>();
            _ = policy.Setup(p => p.GetRequiredPolicyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(required);
            return new ProviderAccountEditFilter(policy.Object);
        }

        private static async Task<object?> InvokeAsync(ProviderAccountEditFilter filter, HttpContext httpContext)
        {
            var context = new DefaultEndpointFilterInvocationContext(httpContext);
            return await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("ran"));
        }

        [Fact]
        public async Task ProviderAccountEditFilter_SuperAdminOnly_AdminCaller_Returns403AndDoesNotRunEndpoint()
        {
            object? result = await InvokeAsync(FilterRequiring(AccessPolicy.SuperAdmin), ContextFor(OperatorRole.Admin));

            IStatusCodeHttpResult status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        }

        [Fact]
        public async Task ProviderAccountEditFilter_SuperAdminOnly_SuperAdminCaller_RunsEndpoint()
        {
            object? result = await InvokeAsync(FilterRequiring(AccessPolicy.SuperAdmin), ContextFor(OperatorRole.SuperAdmin));

            Assert.Equal("ran", result);
        }

        [Fact]
        public async Task ProviderAccountEditFilter_AdminLevel_AdminCaller_RunsEndpoint()
        {
            object? result = await InvokeAsync(FilterRequiring(AccessPolicy.Admin), ContextFor(OperatorRole.Admin));

            Assert.Equal("ran", result);
        }

        [Fact]
        public async Task ProviderAccountEditFilter_NoRoleClaim_Returns403()
        {
            // A caller with no readable role is denied rather than treated as Admin.
            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

            object? result = await InvokeAsync(FilterRequiring(AccessPolicy.Admin), httpContext);

            IStatusCodeHttpResult status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
            Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
        }
    }
}
