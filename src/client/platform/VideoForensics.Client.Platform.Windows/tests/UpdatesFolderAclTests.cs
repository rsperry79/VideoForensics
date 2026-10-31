using System.Security.AccessControl;
using System.Security.Principal;

using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class UpdatesFolderAclTests
    {
        private static string NewTempPath() => Path.Combine(Path.GetTempPath(), "vf-acl-tests", Guid.NewGuid().ToString("N"));

        private static List<(string Sid, FileSystemRights Rights)> Rules(string path)
        {
            AuthorizationRuleCollection rules = new DirectoryInfo(path).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));
            return rules.Cast<FileSystemAccessRule>().Select(r => (r.IdentityReference.Value, r.FileSystemRights)).ToList();
        }

        [Fact]
        public void UpdatesFolderAcl_Ensure_GrantsOnlySystemAndAdministrators()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string path = NewTempPath();
            try
            {
                UpdatesFolderAcl.Ensure(path);

                List<(string Sid, FileSystemRights Rights)> rules = Rules(path);
                Assert.Equal(2, rules.Count);
                Assert.Contains(rules, r => r.Sid == "S-1-5-18" && r.Rights == FileSystemRights.FullControl);
                Assert.Contains(rules, r => r.Sid == "S-1-5-32-544" && r.Rights == FileSystemRights.FullControl);
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Fact]
        public void UpdatesFolderAcl_Ensure_DisablesInheritance()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string path = NewTempPath();
            try
            {
                UpdatesFolderAcl.Ensure(path);

                Assert.True(new DirectoryInfo(path).GetAccessControl().AreAccessRulesProtected);
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Fact]
        public void UpdatesFolderAcl_Ensure_ExtraWriterGetsModifyNotFullControl()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string path = NewTempPath();
            var writer = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            try
            {
                UpdatesFolderAcl.Ensure(path, [writer]);

                Assert.Contains(Rules(path), r => r.Sid == writer.Value && (r.Rights & FileSystemRights.Modify) == FileSystemRights.Modify && (r.Rights & FileSystemRights.ChangePermissions) == 0);
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Fact]
        public void UpdatesFolderAcl_Ensure_CalledTwice_ReplacesRulesInsteadOfAccumulating()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string path = NewTempPath();
            var writer = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            try
            {
                UpdatesFolderAcl.Ensure(path, [writer]);
                UpdatesFolderAcl.Ensure(path);

                Assert.DoesNotContain(Rules(path), r => r.Sid == writer.Value);
            }
            finally
            {
                Cleanup(path);
            }
        }

        private static void Cleanup(string path)
        {
            try
            {
                // The test folder's ACL excludes the current user's inherited rights; take ownership back to delete it.
                Directory.Delete(path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort temp cleanup.
            }
        }
    }
}
