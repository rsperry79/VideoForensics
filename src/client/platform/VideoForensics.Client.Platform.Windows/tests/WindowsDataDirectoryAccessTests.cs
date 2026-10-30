using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class WindowsDataDirectoryAccessTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "vf-acl-" + Guid.NewGuid().ToString("N"));

        public WindowsDataDirectoryAccessTests()
        {
            _ = Directory.CreateDirectory(_directory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        /// <summary>Verifies that GrantUsersModify adds a Modify rule for the built-in Users group on the directory.</summary>
        [Fact]
        public void GrantUsersModify_Windows_AddsUsersModifyRule()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            new WindowsDataDirectoryAccess().GrantUsersModify(_directory);

            var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            DirectorySecurity acl = new DirectoryInfo(_directory).GetAccessControl();
            Assert.Contains(
                acl.GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>(),
                rule => rule.IdentityReference.Equals(users) && rule.FileSystemRights.HasFlag(FileSystemRights.Modify));
        }
    }
}
