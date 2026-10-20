namespace VideoForensics.Providers.Ring.Catalog;

/// <summary>The product family a Ring kind belongs to.</summary>
public enum RingDeviceFamily
{
    Doorbell,
    Camera,
    Chime,
}

/// <summary>
/// One catalog row. ModelName and Generation are null when no source ties the kind to a product name; the kind alone is still recorded.
/// </summary>
/// <param name="Kind">Raw Ring kind.</param>
/// <param name="Family">Product family.</param>
/// <param name="ModelName">Marketing model name, when known.</param>
/// <param name="Generation">Generation label as Ring names it, for example "2nd Gen", when known.</param>
/// <param name="IsWifiBridge">True when the device extends the Wi-Fi network for other Ring devices (the Chime Pro).</param>
public sealed record RingModelInfo(
    string Kind,
    RingDeviceFamily Family,
    string? ModelName,
    string? Generation,
    bool IsWifiBridge)
{
    /// <summary>True for families that carry a camera. Chimes have none.</summary>
    public bool HasCamera => Family is RingDeviceFamily.Doorbell or RingDeviceFamily.Camera;
}
