using System.Text.Json;

namespace VideoForensics.Client.Hosting.Updates
{
    /// <summary>One file in an update package. <paramref name="Path"/> is relative to the package root, with '/' separators.</summary>
    public sealed record UpdateManifestFile(string Path, string Sha256, long Size);

    /// <summary>Describes an update package: its version and every file it installs.</summary>
    public sealed record UpdateManifest(string Version, IReadOnlyList<UpdateManifestFile> Files)
    {
        /// <summary>File name of the manifest inside a package folder. Written last, so its presence marks the package complete.</summary>
        public const string FileName = "manifest.json";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        /// <summary>Serializes the manifest to JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

        /// <summary>Parses manifest JSON. Returns null when the text is not a well-formed manifest.</summary>
        public static UpdateManifest? TryParse(string json)
        {
            try
            {
                UpdateManifest? manifest = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions);
                return manifest is { Version.Length: > 0, Files: not null } ? manifest : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
