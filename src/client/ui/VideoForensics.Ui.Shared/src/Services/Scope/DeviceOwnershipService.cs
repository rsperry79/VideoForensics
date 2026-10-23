using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Scope;

/// <summary>What the Provider and Account filters need to know about the operator's provider accounts.</summary>
/// <param name="Accounts">Every provider account, in repository order.</param>
/// <param name="AccountNames">Operator-facing label per account: the user's display name, else the provider name.</param>
/// <param name="ProviderNames">Distinct provider names, sorted, for the Provider filter.</param>
/// <param name="Ownership">Device-to-account links derived from location membership.</param>
public sealed record DeviceOwnershipSnapshot(
    IReadOnlyList<ProviderAccount> Accounts,
    IReadOnlyDictionary<Guid, string> AccountNames,
    IReadOnlyList<string> ProviderNames,
    IReadOnlyList<DeviceOwnership> Ownership);

/// <summary>
/// Loads the provider accounts, their locations and display names once, so every page that offers the
/// Provider/Account filters builds the same device ownership. Filtering itself stays in
/// <see cref="ScopeDeviceFilter"/>.
/// </summary>
public sealed class DeviceOwnershipService
{
    private readonly IProviderAccountRepository _accounts;
    private readonly ILocationRepository _locations;
    private readonly IUserRepository _users;

    public DeviceOwnershipService(
        IProviderAccountRepository accounts,
        ILocationRepository locations,
        IUserRepository users)
    {
        _accounts = accounts;
        _locations = locations;
        _users = users;
    }

    /// <summary>
    /// Builds the snapshot for <paramref name="devices"/>. A failure while reading accounts or locations yields
    /// an empty snapshot, so callers keep showing every device instead of hiding the list.
    /// </summary>
    public async Task<DeviceOwnershipSnapshot> LoadAsync(IReadOnlyList<Device> devices, CancellationToken ct)
    {
        try
        {
            var accounts = (await _accounts.ListAsync(ct)).ToList();

            var locationIdsByAccount = new Dictionary<Guid, IReadOnlyList<Guid>>();
            foreach (ProviderAccount? account in accounts)
            {
                IReadOnlyList<Location> locations = await _locations.ListByProviderAccountIdAsync(account.Id, ct);
                locationIdsByAccount[account.Id] = locations.Select(l => l.Id).ToList();
            }

            IReadOnlyList<DeviceOwnership> ownership = ScopeDeviceFilter.BuildOwnership(devices, accounts, locationIdsByAccount);
            var providerNames = accounts
                .Select(a => a.ProviderName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            IReadOnlyDictionary<Guid, string> names = await LoadAccountNamesAsync(accounts, ct);
            return new DeviceOwnershipSnapshot(accounts, names, providerNames, ownership);
        }
        catch
        {
            return new DeviceOwnershipSnapshot(
                Array.Empty<ProviderAccount>(),
                new Dictionary<Guid, string>(),
                Array.Empty<string>(),
                Array.Empty<DeviceOwnership>());
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadAccountNamesAsync(
        IReadOnlyList<ProviderAccount> accounts,
        CancellationToken ct)
    {
        var names = new Dictionary<Guid, string>();
        foreach (ProviderAccount account in accounts)
        {
            string name = account.ProviderName;
            try
            {
                User? user = await _users.GetAsync(account.UserId, ct);
                if (user is not null)
                {
                    name = user.DisplayName;
                }
            }
            catch
            {
                // Keep the provider name when the user lookup fails.
            }

            names[account.Id] = name;
        }

        return names;
    }
}
