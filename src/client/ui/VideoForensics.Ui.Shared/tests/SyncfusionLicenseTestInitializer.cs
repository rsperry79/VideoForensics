using System.Runtime.CompilerServices;

using VideoForensics.Client.Core.Licensing;

namespace VideoForensics.Ui.Shared.Tests
{
    /// <summary>
    /// Registers the Syncfusion Blazor license at test assembly load time.
    /// This test project renders real Syncfusion components (SfButton, SfTextBox, etc.) via bUnit
    /// without referencing VideoForensics.Hosting, so it registers through SyncfusionLicenseRegistrar in Client.Core
    /// (already a transitive dependency via Ui.Shared) as a module initializer.
    /// </summary>
    public static class SyncfusionLicenseTestInitializer
    {
        [ModuleInitializer]
        public static void RegisterSyncfusionLicense()
        {
            // Single home for license resolution lives in Client.Core (same file/env/baked-in priority as the app).
            new SyncfusionLicenseRegistrar().RegisterIfPresent();
        }
    }
}
