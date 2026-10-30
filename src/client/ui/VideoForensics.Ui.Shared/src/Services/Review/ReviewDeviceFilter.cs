using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// Camera visibility for the review page's Provider, Account and Location filters. Delegates the ownership decision to
/// <see cref="ScopeRailRules.VisibleDevices"/>, so the review page and the scope rail agree on which devices a
/// provider or account owns.
/// </summary>
public static class ReviewDeviceFilter
{
    /// <summary>
    /// The camera ids left after the Provider and Account filters, in the order given. With no ownership data
    /// (the load failed) every camera stays visible, so a failed lookup never hides the page's cameras.
    /// </summary>
    public static IReadOnlyList<Guid> VisibleDeviceIds(
        IReadOnlyList<Guid> allDeviceIds,
        IReadOnlyList<DeviceOwnership>? ownership,
        string? providerFilter,
        Guid? accountFilter,
        Guid? locationFilter = null)
    {
        if (ownership is null)
        {
            return allDeviceIds.ToList();
        }

        // The scope rule reads only each device's id, so a stub per id is enough to reuse it.
        IEnumerable<Device> devices = allDeviceIds.Select(id => new Device
        {
            Id = id,
            ProviderDeviceId = string.Empty,
            Name = string.Empty,
            Type = string.Empty,
        });

        return ScopeRailRules.VisibleDevices(devices, ownership, providerFilter, accountFilter, locationFilter)
            .Select(device => device.Id)
            .ToList();
    }

    /// <summary>
    /// Keeps only the selected cameras that are still visible, in their selected order. A camera is never added here;
    /// the caller decides what to select when nothing is left.
    /// </summary>
    public static IReadOnlyList<Guid> ClampCameras(IReadOnlyList<Guid> selected, IReadOnlyList<Guid> visible)
    {
        var allowed = visible.ToHashSet();
        return selected.Where(allowed.Contains).ToList();
    }
}
