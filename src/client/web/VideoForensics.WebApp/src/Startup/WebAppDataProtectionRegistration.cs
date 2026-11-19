using CloudflaredKit;
using Fido2NetLib;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Syncfusion.Blazor;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Client.Platform.Windows;
using VideoForensics.Client.Ui.Shared;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Formatting;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Inspector;
using VideoForensics.Client.Ui.Shared.Services.QueryApi;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Chat;
using VideoForensics.Client.WebApp.Components;
using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Client.WebApp.Discovery;
using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Client.WebApp.Infrastructure;
using VideoForensics.Client.WebApp.Mcp;
using VideoForensics.Client.WebApp.Mcp.Tools;
using VideoForensics.Client.WebApp.RateLimiting;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Client.WebApp.Services.CircuitGuards;
using VideoForensics.Core.Helpers.Platform;
using SperryDev.Logger.DependencyInjection;
using SperryDev.Logger.Services;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Client.WebApp.Startup
{
    /// <summary>Data protection registrations (session, step-up and SMTP-password keys), moved out of Program.cs.</summary>
    internal static class WebAppDataProtectionRegistration
    {
        /// <summary>Persists data-protection keys under the Keys folder and wraps them (TPM, else DPAPI on Windows).</summary>
        /// <returns>A description of how the key ring is wrapped, for the startup log.</returns>
        internal static string AddWebAppDataProtection(this IServiceCollection services, StorageLocationProvider storageProvider)
        {
        // Session tokens, step-up tokens, and the SMTP password (SessionTokenService, StepUpAuthService,
        // SmtpPasswordStore) are all IDataProtector-protected. AddDataProtection() alone relies on ASP.NET
        // Core's default key-storage heuristic (usually %LOCALAPPDATA%\ASP.NET\DataProtection-Keys on
        // Windows, keyed by content-root path) - explicit here so a signed-in session actually survives a
        // server restart instead of depending on that heuristic continuing to resolve the same way. Keys
        // live next to the app's own database rather than the OS default location, matching how every
        // other piece of this app's persistent state is already rooted at %ProgramData%\VideoForensics.
        string dataProtectionKeyPath = storageProvider.GetDefaultRoot(StorageCategory.Keys);
        Directory.CreateDirectory(dataProtectionKeyPath);
        IDataProtectionBuilder dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName("VideoForensics")
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
        // DPAPI encrypts the key-ring file at rest but is Windows-only (this app is platform-agnostic -
        // see PlatformDirectoryService/CredentialEncryptionFactory for the same Windows/other-OS split).
        // The WebApp may run interactively during development and as LocalSystem in production. Use
        // machine scope so a key ring created by one of those identities remains usable by the service.
        // Access to the key directory is still restricted by its filesystem ACL.
        // When the TPM is usable, the key ring is encrypted to a TPM-held certificate, so only this machine's TPM can
        // decrypt it. Otherwise DPAPI (machine scope) protects it.
        string dataProtectionWrapping = "not Windows";
        if (OperatingSystem.IsWindows())
        {
            // A machine that already has the TPM key never falls back to DPAPI: the start is retried with waits, then refused.
            X509Certificate2? tpmCertificate = VideoForensics.Client.Platform.Windows.KeyManagement.DataProtectionKeyWrapping.ResolveTpmCertificate(
                VideoForensics.Client.Platform.Windows.KeyManagement.TpmDataProtectionCertificate.TryLoadOrCreate,
                VideoForensics.Client.Platform.Windows.KeyManagement.TpmDataProtectionCertificate.KeyExists,
                Thread.Sleep,
                [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)]);
            _ = tpmCertificate is not null
                ? dataProtectionBuilder.ProtectKeysWithCertificate(tpmCertificate)
                : dataProtectionBuilder.ProtectKeysWithDpapi(protectToLocalMachine: true);

            // Logged at startup: a restart that falls back to DPAPI cannot later read keys wrapped by the TPM certificate.
            bool elevated = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            bool tpmProbeAvailable = new VideoForensics.Client.Platform.Windows.KeyManagement.TpmKeyStorageProvider().IsAvailable;
            dataProtectionWrapping = VideoForensics.Client.Platform.Windows.KeyManagement.DataProtectionKeyWrapping.Describe(
                tpmCertificate is not null, tpmProbeAvailable, elevated);
        }
            return dataProtectionWrapping;
        }
    }
}
