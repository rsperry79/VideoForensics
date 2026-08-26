namespace VideoForensics.Data.Common.Entities
{
    /// <summary>An entry in the hash-chained action log recording changes and operations.</summary>
    public class ActionLogEntry
    {
        public Guid Id { get; set; }
        public required string Actor { get; set; }
        public ActorType ActorType { get; set; }
        public required string Action { get; set; }
        public required string EntityType { get; set; }
        public Guid? EntityId { get; set; }
        public string? DetailsJson { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string? PreviousEntryHash { get; set; }
        public required string EntryHash { get; set; }
    }
}
