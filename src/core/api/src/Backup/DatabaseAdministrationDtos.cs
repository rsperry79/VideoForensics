namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Physical database layout and record counts for the Database page.</summary>
    public record DatabaseStatsDto(
        string Engine,
        string Location,
        long FileBytes,
        long PageCount,
        long FreePageCount,
        long PageSize,
        string JournalMode,
        int MediaItemCount,
        int PurgedMediaItemCount,
        int ActionLogEntryCount);

    /// <summary>A media row whose file is not on disk.</summary>
    public record MissingMediaDto(Guid MediaItemId, string FileName, string ResolvedPath);

    /// <summary>Action log chain verification. <c>Break</c> is "None" when the chain verifies.</summary>
    public record ChainCheckDto(bool IsValid, int EntriesChecked, string Break, Guid? FailedEntryId, DateTime? FailedEntryTimestampUtc);

    /// <summary>Validation findings. Validation changes nothing; purge and chain repair are separate, confirmed calls.</summary>
    public record DatabaseValidationDto(IReadOnlyList<MissingMediaDto> MissingMedia, ChainCheckDto Chain, bool IsClean);

    /// <summary>Request to purge media rows that validation found without files.</summary>
    /// <param name="MediaItemIds">Rows to purge. Rows under legal hold, or whose file has come back, are skipped.</param>
    public record PurgeMissingRequest(IReadOnlyList<Guid> MediaItemIds);

    /// <summary>Number of rows purged.</summary>
    public record PurgeMissingResultDto(int Purged);

    /// <summary>The engine's integrity verdict. "ok" when the database file is sound.</summary>
    public record RepairVerdictDto(string Verdict);

    /// <summary>Outcome of a chain repair. <c>Unrepairable</c> is set when an altered entry was found and not bridged.</summary>
    public record ChainRepairResultDto(int BridgesAdded, ChainCheckDto? Unrepairable);
}
