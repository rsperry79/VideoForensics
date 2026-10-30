using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Why a hash chain failed to verify.</summary>
    public enum ChainBreakKind
    {
        /// <summary>No break: every entry verified.</summary>
        None,

        /// <summary>An entry's PreviousEntryHash does not match the entry before it. Entries were inserted, removed or reordered.</summary>
        PreviousHashMismatch,

        /// <summary>An entry's stored EntryHash does not match its contents. The entry was altered after it was written.</summary>
        EntryHashMismatch
    }

    /// <summary>Outcome of verifying the action log hash chain, with the location of the first break when there is one.</summary>
    /// <param name="IsValid">True when every entry verifies.</param>
    /// <param name="EntriesChecked">Entries that verified before the break, or all entries when the chain is valid.</param>
    /// <param name="Break">The kind of break, or <see cref="ChainBreakKind.None"/> when valid.</param>
    /// <param name="FailedEntryId">The first entry that failed, or null when valid.</param>
    /// <param name="FailedEntryTimestampUtc">When the first failing entry was recorded, or null when valid.</param>
    public sealed record ChainIntegrityResult(
        bool IsValid,
        int EntriesChecked,
        ChainBreakKind Break,
        Guid? FailedEntryId,
        DateTime? FailedEntryTimestampUtc)
    {
        /// <summary>A verified chain covering <paramref name="entriesChecked"/> entries.</summary>
        public static ChainIntegrityResult Valid(int entriesChecked) =>
            new(true, entriesChecked, ChainBreakKind.None, null, null);

        /// <summary>A chain that broke at <paramref name="failedEntry"/> after <paramref name="entriesChecked"/> good entries.</summary>
        public static ChainIntegrityResult Broken(int entriesChecked, ChainBreakKind kind, ActionLogEntry failedEntry) =>
            new(false, entriesChecked, kind, failedEntry.Id, failedEntry.TimestampUtc);
    }
}
