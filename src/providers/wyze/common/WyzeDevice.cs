namespace VideoForensics.Providers.Wyze;

/// <summary>Represents a Wyze camera device</summary>
public record WyzeDevice(
    string DeviceId,
    string MacAddress,
    string Status,
    string ModelName);
