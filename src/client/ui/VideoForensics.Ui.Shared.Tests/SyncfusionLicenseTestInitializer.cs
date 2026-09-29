using System.Runtime.CompilerServices;

namespace VideoForensics.Ui.Shared.Tests
{
    /// <summary>
    /// Registers the Syncfusion Blazor license at test assembly load time.
    /// This test project renders real Syncfusion components (SfButton, SfTextBox, etc.) via bUnit
    /// without referencing VideoForensics.Hosting, so we duplicate the license registration logic
    /// directly here as a module initializer instead of adding a project dependency.
    /// </summary>
    public static class SyncfusionLicenseTestInitializer
    {
        [ModuleInitializer]
        public static void RegisterSyncfusionLicense()
        {
            string syncfusionLicenseKeyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "VideoForensics",
                "syncfusion-license.key");

            if (File.Exists(syncfusionLicenseKeyPath))
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(
                    File.ReadAllText(syncfusionLicenseKeyPath).Trim());
            }
        }
    }
}
