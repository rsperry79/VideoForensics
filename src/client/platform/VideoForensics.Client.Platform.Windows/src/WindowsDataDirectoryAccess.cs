using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Grants the built-in Users group Modify rights on a data directory. Needed because %ProgramData% is machine-wide and
    /// new subfolders are not writable by normal users by default.
    /// </summary>
    public sealed class WindowsDataDirectoryAccess : IDataDirectoryAccess
    {
        /// <inheritdoc />
        public void GrantUsersModify(string directory)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var dirInfo = new DirectoryInfo(directory);
            DirectorySecurity acl = dirInfo.GetAccessControl();
            var usersIdentity = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            acl.AddAccessRule(
                new FileSystemAccessRule(
                    usersIdentity,
                    FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
            dirInfo.SetAccessControl(acl);
        }
    }
}
