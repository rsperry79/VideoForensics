using System.Security.Cryptography;
using System.Text;

using VideoForensics.Client.Hosting.Updates;

namespace VideoForensics.Client.Hosting.Tests.Updates
{
    /// <summary>Builds update packages and install folders under a temp root that is deleted on dispose.</summary>
    internal sealed class UpdateTestFolder : IDisposable
    {
        public UpdateTestFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), "vf-update-tests", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string NewDir(string name)
        {
            string path = Path.Combine(Root, name);
            _ = Directory.CreateDirectory(path);
            return path;
        }

        public static void WriteFile(string dir, string relative, string content)
        {
            string path = Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        /// <summary>Writes the files and a matching manifest.json; returns the manifest.</summary>
        public static UpdateManifest BuildPackage(string dir, string version, IDictionary<string, string> files)
        {
            var entries = new List<UpdateManifestFile>();
            foreach (KeyValuePair<string, string> file in files)
            {
                WriteFile(dir, file.Key, file.Value);
                byte[] bytes = Encoding.UTF8.GetBytes(file.Value);
                entries.Add(new UpdateManifestFile(file.Key, Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length));
            }

            var manifest = new UpdateManifest(version, entries);
            File.WriteAllText(Path.Combine(dir, UpdateManifest.FileName), manifest.ToJson());
            return manifest;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort temp cleanup; a locked file must not fail the test.
            }
        }
    }
}
