namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Type of evidence item pinned to a forensic case.</summary>
    public enum CaseItemKind
    {
        /// <summary>An event detected by a device.</summary>
        Event = 0,

        /// <summary>A downloaded media file.</summary>
        Media = 1
    }
}
