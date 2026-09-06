using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for per-operator UI preferences (theme mode, language). One row per OperatorId.</summary>
    public interface IOperatorPreferencesRepository
    {
        /// <summary>Gets the preferences row for an operator, or null if none has been saved yet.</summary>
        Task<OperatorPreferences?> GetAsync(Guid operatorId, CancellationToken ct);

        /// <summary>Inserts or updates the preferences row for the given operator (keyed by OperatorId).</summary>
        Task UpsertAsync(OperatorPreferences preferences, CancellationToken ct);
    }
}
