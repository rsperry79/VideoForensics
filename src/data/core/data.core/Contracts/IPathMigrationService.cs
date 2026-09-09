namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Manages migration of media files from legacy user directories to ProgramData.</summary>
    public interface IPathMigrationService
    {
        /// <summary>Migrates existing media from legacy locations to ProgramData and updates database records.</summary>
        Task MigrateToNewStorageAsync(CancellationToken cancellationToken = default);
    }
}
