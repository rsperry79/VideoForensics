using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

using System.Security.Claims;

using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class SuperAdminLocalOrPasskeyPolicyTests
    {
        private static async Task<bool> AuthorizeAsync(string role, string tier, bool passkey)
        {
            ServiceCollection services = new();
            _ = services.AddLogging();
            _ = services.AddAuthorization(o => o.AddVideoForensicsPolicies());
            _ = services.AddSingleton<IAuthorizationHandler, MinimumRoleHandler>();
            _ = services.AddSingleton<IAuthorizationHandler, RequireLocalTierHandler>();
            _ = services.AddSingleton<IAuthorizationHandler, RequireLocalOrPasskeyHandler>();
            using ServiceProvider sp = services.BuildServiceProvider();

            List<Claim> claims =
            [
                new Claim(VideoForensicsClaimTypes.Role, role),
                new Claim(VideoForensicsClaimTypes.NetworkTier, tier)
            ];
            if (passkey)
            {
                claims.Add(new Claim(VideoForensicsClaimTypes.CredentialKind, "OperatorPasskey"));
            }

            ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
            AuthorizationResult result = await sp.GetRequiredService<IAuthorizationService>()
                .AuthorizeAsync(user, null, VideoForensicsPolicies.SuperAdminLocalOrPasskey);
            return result.Succeeded;
        }

        [Fact]
        public async Task SuperAdminLocalOrPasskey_SuperAdminPasskeyNonLocalTier_Succeeds()
        {
            Assert.True(await AuthorizeAsync("SuperAdmin", "Internet", passkey: true));
        }

        [Fact]
        public async Task SuperAdminLocalOrPasskey_SuperAdminNonLocalNoPasskey_Fails()
        {
            Assert.False(await AuthorizeAsync("SuperAdmin", "Internet", passkey: false));
        }

        [Fact]
        public async Task SuperAdminLocalOrPasskey_SuperAdminLocalNoPasskey_Succeeds()
        {
            Assert.True(await AuthorizeAsync("SuperAdmin", "Local", passkey: false));
        }

        [Fact]
        public async Task SuperAdminLocalOrPasskey_AdminPasskeyNonLocal_Fails()
        {
            Assert.False(await AuthorizeAsync("Admin", "Internet", passkey: true));
        }
    }
}
