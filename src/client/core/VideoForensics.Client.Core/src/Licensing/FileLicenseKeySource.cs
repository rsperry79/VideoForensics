using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Core.Licensing
{
    /// <summary>
    /// Reads <c>syncfusion-license.key</c> from a directory. Used with the platform data root, so an IT admin can
    /// override the baked-in key by dropping a file there without rebuilding the app.
    /// </summary>
    public sealed class FileLicenseKeySource : ILicenseKeySource
    {
        private const string FileName = "syncfusion-license.key";

        private readonly string _directory;

        /// <param name="directory">Folder that holds the key file. The file itself is optional.</param>
        public FileLicenseKeySource(string directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            _directory = directory;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Synchronous on purpose: this runs once at startup, before the host has a running request pipeline,
        /// and the registrar's entry point is synchronous.
        /// </remarks>
        public string? ReadKey()
        {
            string path = Path.Combine(_directory, FileName);
            if (!File.Exists(path))
            {
                return null;
            }

            string trimmed = File.ReadAllText(path).Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
