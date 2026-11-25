namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>A stored location (the server's record of a provider location).</summary>
    /// <param name="Id">Unique identifier of the location.</param>
    /// <param name="ProviderLocationId">The provider's identifier for the location.</param>
    /// <param name="Name">Display name of the location.</param>
    /// <param name="Address">Postal address, if known.</param>
    /// <param name="LastSyncedUtc">Last successful sync, in UTC; null if never synced.</param>
    /// <param name="SyncStatus">Current sync status.</param>
    public sealed record LocationRecordDto(
        Guid Id,
        string ProviderLocationId,
        string Name,
        string? Address,
        DateTime? LastSyncedUtc,
        SyncStatusDto SyncStatus);

    /// <summary>Per-operator UI preferences.</summary>
    /// <param name="Id">Unique identifier of the preferences row.</param>
    /// <param name="OperatorId">The operator these preferences belong to.</param>
    /// <param name="ThemeMode">Theme choice (for example System, Light or Dark).</param>
    /// <param name="CultureName">Culture name, or null for the default.</param>
    /// <param name="UiMode">Layout mode (for example Standard or Simple).</param>
    /// <param name="UiModeLocked">True when the operator has locked the layout mode.</param>
    /// <param name="TimeFormat">Time format choice.</param>
    /// <param name="EncryptionWarningDismissed">True when the encryption warning has been dismissed.</param>
    /// <param name="UpdatedAtUtc">When the preferences were last changed, in UTC.</param>
    public sealed record OperatorPreferencesDto(
        Guid Id,
        Guid OperatorId,
        string ThemeMode,
        string? CultureName,
        string UiMode,
        bool UiModeLocked,
        string TimeFormat,
        bool EncryptionWarningDismissed,
        DateTime UpdatedAtUtc);
}
