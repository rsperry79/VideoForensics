namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Operator UI preferences, with DTO signatures. Client hosts use this interface.</summary>
    public interface IOperatorPreferencesRepository
    {
        /// <summary>Gets an operator's preferences, or null.</summary>
        Task<OperatorPreferencesDto?> GetAsync(Guid operatorId, CancellationToken ct);

        /// <summary>Creates or replaces an operator's preferences.</summary>
        Task UpsertAsync(OperatorPreferencesDto preferences, CancellationToken ct);

        /// <summary>Sets the layout mode and whether it is locked, and returns the stored preferences.</summary>
        Task<OperatorPreferencesDto> SetUiModeAsync(Guid operatorId, string uiMode, bool locked, CancellationToken ct);

        /// <summary>Sets whether the encryption warning is dismissed.</summary>
        Task SetEncryptionWarningDismissedAsync(Guid operatorId, bool dismissed, CancellationToken ct);
    }
}
