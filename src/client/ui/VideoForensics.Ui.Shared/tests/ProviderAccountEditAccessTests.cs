using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class ProviderAccountEditAccessTests
{
    private const string PermissionsPath = "/api/v1/permissions/provider-accounts";

    private static readonly Uri BaseAddress = new("https://localhost");

    private static async Task<PairedSessionState> SessionAsync(string? role)
    {
        var session = new PairedSessionState(Mock.Of<IJSRuntime>());
        if (role is not null)
        {
            await session.SetAsync("session-token", Guid.NewGuid(), role);
            session.SetServerSessionKind(localTier: true, passkeySession: true, passwordSession: false);
        }

        return session;
    }

    private static (ProviderAccountEditAccess Access, StubHttpMessageHandler Handler) Build(PairedSessionState session)
    {
        var handler = new StubHttpMessageHandler();
        var factory = new Mock<ISelfApiHttpClientFactory>();
        _ = factory.Setup(f => f.CreateClient()).Returns(() => new HttpClient(handler) { BaseAddress = BaseAddress });
        return (new ProviderAccountEditAccess(factory.Object, session), handler);
    }

    [Fact]
    public async Task CanEditAsync_DefaultLevelAndAdminSession_ReturnsTrue()
    {
        (ProviderAccountEditAccess access, StubHttpMessageHandler handler) = Build(await SessionAsync("Admin"));
        handler.Bodies[PermissionsPath] = new ProviderPermissionsDto(ProviderEditAccessLevels.Admin);

        Assert.True(await access.CanEditAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CanEditAsync_SuperAdminLevelAndAdminSession_ReturnsFalse()
    {
        (ProviderAccountEditAccess access, StubHttpMessageHandler handler) = Build(await SessionAsync("Admin"));
        handler.Bodies[PermissionsPath] = new ProviderPermissionsDto(ProviderEditAccessLevels.SuperAdmin);

        Assert.False(await access.CanEditAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CanEditAsync_SuperAdminLevelAndSuperAdminSession_ReturnsTrue()
    {
        (ProviderAccountEditAccess access, StubHttpMessageHandler handler) = Build(await SessionAsync("SuperAdmin"));
        handler.Bodies[PermissionsPath] = new ProviderPermissionsDto(ProviderEditAccessLevels.SuperAdmin);

        Assert.True(await access.CanEditAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CanEditAsync_SettingReadFails_ReturnsFalse()
    {
        // When the level cannot be read, the controls stay hidden; the server still enforces the real rule.
        (ProviderAccountEditAccess access, StubHttpMessageHandler handler) = Build(await SessionAsync("Admin"));
        handler.FailingPaths.Add(PermissionsPath);

        Assert.False(await access.CanEditAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CanEditAsync_NotSignedIn_ReturnsFalseWithoutCallingServer()
    {
        (ProviderAccountEditAccess access, StubHttpMessageHandler handler) = Build(await SessionAsync(role: null));

        Assert.False(await access.CanEditAsync(CancellationToken.None));
        Assert.Empty(handler.Sent);
    }
}
