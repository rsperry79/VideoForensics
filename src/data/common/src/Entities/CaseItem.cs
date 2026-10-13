namespace VideoForensics.Data.Common.Entities
{
    /// <summary>An evidence item (event or media) pinned to a forensic case, supporting soft-removal for chain-of-custody.</summary>
    public class CaseItem
    {
        /// <summary>Gets or sets the item ID.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the ID of the case this item is pinned to (foreign key, cascade delete).</summary>
        public Guid CaseId { get; set; }

        /// <summary>Gets or sets the kind of evidence (Event or Media), stored as string in database.</summary>
        public CaseItemKind Kind { get; set; }

        /// <summary>Gets or sets the ID of the event if Kind is Event; null if Kind is Media.</summary>
        public Guid? EventId { get; set; }

        /// <summary>Gets or sets the ID of the media item if Kind is Media; null if Kind is Event.</summary>
        public Guid? MediaItemId { get; set; }

        /// <summary>Gets or sets the reason this item was pinned to the case (required, max 1024 chars).</summary>
        public required string Reason { get; set; }

        /// <summary>Gets or sets the username who pinned this item to the case (required, max 256 chars).</summary>
        public required string AddedBy { get; set; }

        /// <summary>Gets or sets the UTC timestamp when this item was pinned.</summary>
        public DateTime AddedAtUtc { get; set; }

        /// <summary>Gets or sets a snapshot of the media item's Sha256Hash at the time of pinning, for later integrity comparison (max 64 chars). Only populated for Media items.</summary>
        public string? MediaSha256AtAdd { get; set; }

        /// <summary>Gets or sets the username who removed this item from the case, if soft-removed.</summary>
        public string? RemovedBy { get; set; }

        /// <summary>Gets or sets the UTC timestamp when this item was soft-removed.</summary>
        public DateTime? RemovedAtUtc { get; set; }

        /// <summary>Gets or sets the reason this item was removed from the case (max 1024 chars).</summary>
        public string? RemovalReason { get; set; }
    }
}
