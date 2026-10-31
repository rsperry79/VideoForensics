
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using System.Net.Http.Json;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Ui.Shared.Pages;
/// <summary>
/// Shared state for the account pages (Details, Sync, Cases): loads the account and its user, and makes the
/// paired-device authenticated GET calls. Each tab is its own routed page, so each loads the account itself.
/// </summary>
public abstract class AccountTabPageBase : ComponentBase
{
    [Parameter]
    public Guid Id { get; set; }

    [Inject]
    protected IProviderAccountRepository ProviderAccountRepository { get; set; } = null!;

    [Inject]
    protected IUserRepository UserRepository { get; set; } = null!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    protected HttpClient HttpClient { get; set; } = null!;

    [Inject]
    protected PairedSessionState SessionState { get; set; } = null!;

    [Inject]
    protected IStringLocalizer<AccountDetails> L { get; set; } = null!;

    [Inject]
    protected IProviderAccountEditAccess EditAccess { get; set; } = null!;

    protected ProviderAccountEntity? Account { get; set; }

    protected string? AccountEmail { get; set; }

    protected bool IsLoading { get; set; } = true;

    protected string? StatusMessage { get; set; }

    /// <summary>
    /// True when the stored edit setting lets this operator change provider accounts. Gates the Remove, Reauthenticate
    /// and Sync actions; the server enforces the same rule, so a hidden control is a convenience, not the security.
    /// </summary>
    protected bool CanEditAccount { get; private set; }

    /// <summary>
    /// Loads the account and, when it exists, the provider login (the account's name). A failed user lookup is
    /// tolerated; a failed account lookup is shown as a status message.
    /// </summary>
    protected async Task LoadAccountAsync()
    {
        IsLoading = true;
        StatusMessage = null;
        CanEditAccount = await EditAccess.CanEditAsync(CancellationToken.None);
        try
        {
            Account = await ProviderAccountRepository.GetAsync(Id, CancellationToken.None);

            if (Account is not null)
            {
                try
                {
                    // The provider login (email for Ring) is the account's name.
                    User? user = await UserRepository.GetAsync(Account.UserId, CancellationToken.None);
                    AccountEmail = user?.ProviderUserKey;
                }
                catch
                {
                    // Fall back to ProviderName if user lookup fails
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = L["ErrorLoadingAccount", ex.Message];
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// GET against the account API with the paired-device bearer token. The injected HttpClient has
    /// no base address or auth header in production, so the absolute URI and header are set per request.
    /// </summary>
    protected async Task<T?> GetApiAsync<T>(string relativePath)
    {
        await SessionState.EnsureLoadedAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(NavigationManager.BaseUri), relativePath));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SessionState.SessionToken);
        using HttpResponseMessage response = await HttpClient.SendAsync(request, CancellationToken.None);
        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(CancellationToken.None);
    }
}
