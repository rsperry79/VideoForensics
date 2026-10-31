namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// Grants the built-in users group write access to a newly created data directory, so the app can run as a normal
    /// user even when the folder was created under a machine-wide location. Windows implements it with an ACL change;
    /// other platforms do nothing, since their default permissions already allow it.
    /// </summary>
    public interface IDataDirectoryAccess
    {
        /// <summary>Grants users modify rights on the directory. May throw; callers decide whether that is fatal.</summary>
        void GrantUsersModify(string directory);
    }

    /// <summary>Does nothing. The default when a host does not register a platform implementation.</summary>
    public sealed class NullDataDirectoryAccess : IDataDirectoryAccess
    {
        /// <inheritdoc />
        public void GrantUsersModify(string directory)
        {
        }
    }

    /// <summary>
    /// Process-wide access policy for data directories, set once at startup by the Windows host. Core and Data use it
    /// without referencing a platform project.
    /// </summary>
    public static class DataDirectoryAccess
    {
        /// <summary>The implementation used by data services. Defaults to <see cref="NullDataDirectoryAccess"/>.</summary>
        public static IDataDirectoryAccess Current { get; set; } = new NullDataDirectoryAccess();
    }
}
