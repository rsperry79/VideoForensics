using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Core.Services
{
    /// <summary>
    /// Stages backup exports under <c>VideoForensics\Backups</c> in the user's documents folder.
    /// The documents root is injectable so the path rule can be tested without touching the real folder.
    /// </summary>
    public sealed class UserDocumentsBackupStagingLocator : IBackupStagingLocator
    {
        private readonly Func<string> _documentsRoot;

        /// <summary>Uses the current user's documents folder.</summary>
        public UserDocumentsBackupStagingLocator()
            : this(() => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
        {
        }

        /// <param name="documentsRoot">Returns the documents folder. Called on every request, so a changed root is picked up.</param>
        public UserDocumentsBackupStagingLocator(Func<string> documentsRoot)
        {
            _documentsRoot = documentsRoot ?? throw new ArgumentNullException(nameof(documentsRoot));
        }

        /// <inheritdoc />
        public string GetStagingDirectory()
        {
            return Path.Combine(_documentsRoot(), "VideoForensics", "Backups");
        }
    }
}
