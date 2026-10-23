using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Scope;

/// <summary>A device's link to the provider account that can see it, via the device's location.</summary>
public sealed record DeviceOwnership(Guid DeviceId, Guid ProviderAccountId, string ProviderName);

/// <summary>The devices left after a Provider/Account filter, and whether any filter is set.</summary>
public sealed record DeviceFilterResult(IReadOnlyList<Device> Devices, bool IsFiltered);

/// <summary>
/// Narrows the device list for the right-rail Provider and Account filters. Devices belong to accounts
/// only through their location (a location can be shared by several accounts), so ownership is derived
/// by joining each account's locations to the devices at those locations.
/// </summary>
public static class ScopeDeviceFilter
{
    /// <summary>
    /// Builds one <see cref="DeviceOwnership"/> per (device, account) pair where the device's location is
    /// linked to that account. Accounts missing from <paramref name="accounts"/> are ignored.
    /// </summary>
    public static IReadOnlyList<DeviceOwnership> BuildOwnership(
        IEnumerable<Device> devices,
        IEnumerable<ProviderAccount> accounts,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> locationIdsByAccount)
    {
        var accountById = accounts.ToDictionary(a => a.Id);

        // Invert account -> locations into location -> accounts so each device is a single lookup.
        var accountsByLocation = new Dictionary<Guid, List<ProviderAccount>>();
        foreach ((Guid accountId, IReadOnlyList<Guid>? locationIds) in locationIdsByAccount)
        {
            if (!accountById.TryGetValue(accountId, out ProviderAccount? account))
            {
                continue;
            }

            foreach (Guid locationId in locationIds)
            {
                if (!accountsByLocation.TryGetValue(locationId, out List<ProviderAccount>? list))
                {
                    list = [];
                    accountsByLocation[locationId] = list;
                }

                list.Add(account);
            }
        }

        var result = new List<DeviceOwnership>();
        foreach (Device device in devices)
        {
            if (!accountsByLocation.TryGetValue(device.LocationId, out List<ProviderAccount>? owners))
            {
                continue;
            }

            foreach (ProviderAccount owner in owners)
            {
                result.Add(new DeviceOwnership(device.Id, owner.Id, owner.ProviderName));
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the devices visible under the filters. A null filter matches everything; with both null the
    /// input is returned unchanged. A device with no ownership row is excluded whenever a filter is set.
    /// </summary>
    public static IReadOnlyList<Device> Filter(
        IEnumerable<Device> devices,
        IEnumerable<DeviceOwnership> ownership,
        string? providerName,
        Guid? providerAccountId)
    {
        if (providerName is null && providerAccountId is null)
        {
            return devices.ToList();
        }

        var allowed = ownership
            .Where(o => providerName is null || string.Equals(o.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))
            .Where(o => providerAccountId is null || o.ProviderAccountId == providerAccountId.Value)
            .Select(o => o.DeviceId)
            .ToHashSet();

        return devices.Where(d => allowed.Contains(d.Id)).ToList();
    }

    /// <summary>Accounts offered by the Account filter: all of them, or only those of <paramref name="providerName"/>.</summary>
    public static IReadOnlyList<ProviderAccount> AccountsForProvider(IEnumerable<ProviderAccount> accounts, string? providerName)
    {
        return providerName is null
            ? accounts.ToList()
            : accounts.Where(a => string.Equals(a.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
