using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services;

/// <summary>Picks the camera the Live view page starts with.</summary>
public static class LiveViewDeviceSelection
{
    /// <summary>
    /// The picker value for <paramref name="requested"/> (the camera the review page was showing), or null when nothing was
    /// requested or the camera is not among <paramref name="devices"/>.
    /// </summary>
    public static string? Initial(IEnumerable<Device> devices, Guid? requested)
    {
        return requested is Guid id && devices.Any(device => device.Id == id) ? id.ToString() : null;
    }
}
