namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Why a <see cref="MediaFlag"/> was dismissed. Stored by name.</summary>
    public enum MediaFlagDismissReason
    {
        FalsePositive,
        CameraMaintenance,
        WeatherOrLighting,
        Duplicate,
        Other
    }
}
