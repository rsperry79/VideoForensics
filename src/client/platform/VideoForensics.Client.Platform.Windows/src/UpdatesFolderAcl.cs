using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Locks down the updates folder. The service applies whatever is staged there as SYSTEM, so anyone who can write to
    /// the folder can run code as SYSTEM. In release mode only SYSTEM and Administrators may write; dev mode adds the
    /// developer explicitly.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class UpdatesFolderAcl
    {
        /// <summary>
        /// Creates the folder if needed and replaces its ACL: inheritance off, full control for SYSTEM and Administrators,
        /// and modify rights for each of <paramref name="extraWriters"/>. Existing entries from the parent (for example
        /// ProgramData's "Users can create files") are removed.
        /// </summary>
        public static void Ensure(string path, IReadOnlyCollection<SecurityIdentifier>? extraWriters = null)
        {
            _ = Directory.CreateDirectory(path);

            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            AddRule(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl);
            AddRule(security, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl);
            foreach (SecurityIdentifier writer in extraWriters ?? [])
            {
                AddRule(security, writer, FileSystemRights.Modify);
            }

            new DirectoryInfo(path).SetAccessControl(security);
        }

        private static void AddRule(DirectorySecurity security, SecurityIdentifier sid, FileSystemRights rights) =>
            security.AddAccessRule(new FileSystemAccessRule(
                sid,
                rights,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
    }
}
