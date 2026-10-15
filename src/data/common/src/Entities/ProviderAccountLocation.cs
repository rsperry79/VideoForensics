namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Junction linking a globally unique <see cref="Location"/> to a <see cref="ProviderAccount"/> that can see it.</summary>
    public class ProviderAccountLocation
    {
        /// <summary>Gets or sets the provider account ID.</summary>
        public Guid ProviderAccountId { get; set; }

        /// <summary>Gets or sets the location ID.</summary>
        public Guid LocationId { get; set; }

        /// <summary>Gets or sets the linked provider account.</summary>
        public ProviderAccount? ProviderAccount { get; set; }

        /// <summary>Gets or sets the linked location.</summary>
        public Location? Location { get; set; }
    }
}
