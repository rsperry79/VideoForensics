using System.Net.Http.Json;

using VideoForensics.Client.Ui.Shared.Layout;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Services;

/// <summary>Answers whether the current operator may edit provider accounts, for the account pages' edit controls.</summary>
public interface IProviderAccountEditAccess
{
    /// <summary>True when the signed-in operator may use the provider-account edit controls under the stored setting.</summary>
    Task<bool> CanEditAsync(CancellationToken ct);
}

/// <summary>
/// Reads the stored edit level from the permissions endpoint and applies it to the current session. A signed-out
/// circuit, or a failed read, reports false: the controls stay hidden and the server still enforces the real rule.
/// </summary>
public sealed class ProviderAccountEditAccess(ISelfApiHttpClientFactory clients, PairedSessionState session) : IProviderAccountEditAccess
{
    private const string PermissionsPath = "/api/v1/permissions/provider-accounts";

    public async Task<bool> CanEditAsync(CancellationToken ct)
    {
        await session.EnsureLoadedAsync();
        if (!session.IsSignedIn)
        {
            return false;
        }

        try
        {
            HttpClient client = clients.CreateClient();
            ProviderPermissionsDto? result = await client.GetFromJsonAsync<ProviderPermissionsDto>(PermissionsPath, ct);
            return ProviderAccountEditGate.CanEdit(result?.ProviderAccountEditAccess, NavContext.FromSession(session, appLockSupported: false));
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
