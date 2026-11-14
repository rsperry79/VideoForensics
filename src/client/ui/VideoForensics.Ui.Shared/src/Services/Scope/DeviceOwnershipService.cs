using VideoForensics.Data.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Scope;

/// <summary>What the Provider and Account filters need to know about the operator's provider accounts.</summary>
/// <param name="Accounts">Every provider account, in repository order.</param>
/// <param name="AccountNames">Operator-facing label per account: the user's display name, else the provider name.</param>
/// <param name="ProviderNames">Distinct provider names, sorted, for the Provider filter.</param>
/// <param name="Ownership">Device-to-account links derived from location membership.</param>
/// <param name="Locations">Distinct provider locations across the accounts, sorted by name, for the Location filter.</param>
/// <param name="LoadError">The exception that stopped the load, or null. Callers that must not hide devices on failure check this.</param>
public sealed record DeviceOwnershipSnapshot(
    IReadOnlyList<ProviderAccountDto> Accounts,
    IReadOnlyDictionary<Guid, string> AccountNames,
    IReadOnlyList<string> ProviderNames,
    IReadOnlyList<DeviceOwnership> Ownership,
    IReadOnlyList<FilterLocation>? Locations = null,
    Exception? LoadError = null)
{
    /// <summary>The Location filter's choices. Empty when the snapshot carries none.</summary>
    public IReadOnlyList<FilterLocation> Locations { get; init; } = Locations ?? Array.Empty<FilterLocation>();
}

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
    public async Task<DeviceOwnershipSnapshot> LoadAsync(IReadOnlyList<DeviceDto> devices, CancellationToken ct)
    {
        try
        {
            var accounts = (await _accounts.ListAsync(ct)).ToList();

            var locationIdsByAccount = new Dictionary<Guid, IReadOnlyList<Guid>>();
            var locationsById = new Dictionary<Guid, FilterLocation>();
            foreach (ProviderAccountDto? account in accounts)
            {
                IReadOnlyList<LocationRecordDto> locations = await _locations.ListByProviderAccountIdAsync(account.Id, ct);
                locationIdsByAccount[account.Id] = locations.Select(l => l.Id).ToList();
                foreach (LocationRecordDto location in locations)
                {
                    // A location shared by several accounts is listed once.
                    _ = locationsById.TryAdd(
                        location.Id,
                        new FilterLocation(location.Id, string.IsNullOrWhiteSpace(location.Name) ? location.ProviderLocationId : location.Name));
                }
            }

            IReadOnlyList<DeviceOwnership> ownership = ScopeDeviceFilter.BuildOwnership(devices, accounts, locationIdsByAccount);
            var providerNames = accounts
                .Select(a => a.ProviderName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            IReadOnlyDictionary<Guid, string> names = await LoadAccountNamesAsync(accounts, ct);
            var filterLocations = locationsById.Values
                .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new DeviceOwnershipSnapshot(accounts, names, providerNames, ownership, filterLocations);
        }
        catch (Exception ex)
        {
            return new DeviceOwnershipSnapshot(
                Array.Empty<ProviderAccountDto>(),
                new Dictionary<Guid, string>(),
                Array.Empty<string>(),
                Array.Empty<DeviceOwnership>(),
                Array.Empty<FilterLocation>(),
                ex);
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadAccountNamesAsync(
        IReadOnlyList<ProviderAccountDto> accounts,
        CancellationToken ct)
    {
        var names = new Dictionary<Guid, string>();
        foreach (ProviderAccountDto account in accounts)
        {
            string name = account.ProviderName;
            try
            {
                UserDto? user = await _users.GetAsync(account.UserId, ct);
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
