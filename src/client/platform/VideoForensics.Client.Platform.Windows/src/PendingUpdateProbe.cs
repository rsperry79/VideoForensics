namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Reports whether a staged update is still waiting in the updates folder. Mirrors the package selection in
    /// StartupUpdateApplier (Hosting): this project does not reference Hosting, and the launcher only needs a yes/no.
    /// </summary>
    public static class PendingUpdateProbe
    {
        /// <summary>
        /// True when a subfolder holding a manifest.json exists that is not a staging folder (leading '.'),
        /// the backup folder ("previous"), or a rejected package (".rejected").
        /// </summary>
        public static bool HasPending(string updatesRoot)
        {
            if (!Directory.Exists(updatesRoot))
            {
                return false;
            }

            return Directory.EnumerateDirectories(updatesRoot).Any(d =>
            {
                string name = Path.GetFileName(d);
                return !name.StartsWith('.')
                    && !name.Equals("previous", StringComparison.OrdinalIgnoreCase)
                    && !name.EndsWith(".rejected", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(Path.Combine(d, "manifest.json"));
            });
        }
    }
}
