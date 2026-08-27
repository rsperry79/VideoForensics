namespace VideoForensics.Client.Common
{
    /// <summary>Supported video provider types.</summary>
    public enum ProviderType
    {
        Ring = 0,
        Wyze = 1,
        // Future providers can be added here
    }

    public static class ProviderTypeExtensions
    {
        public static string DisplayName(this ProviderType provider) => provider switch
        {
            ProviderType.Ring => "Ring",
            ProviderType.Wyze => "Wyze",
            _ => provider.ToString()
        };
    }
}
