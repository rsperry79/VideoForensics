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
    /// <summary>Certificate and transport registrations (Let's Encrypt, Internet-hostname certificate, LAN redirect), moved out of Program.cs.</summary>
    internal static class WebAppCertificateRegistration
    {
        /// <summary>Adds the trusted-certificate services and the LAN-to-https redirect options.</summary>
        internal static IServiceCollection AddWebAppCertificates(this IServiceCollection services, CertificateHolder certificateHolder, int? httpsPort)
        {
        // Trusted certificate for the Internet hostname, issued through Let's Encrypt DNS-01 with the stored
        // Cloudflare token. Acme:UseStaging (default true) keeps issuance on the staging CA until it is switched off.
        services.AddSingleton(certificateHolder);
        services.AddSingleton(new LocalTransportOptions(httpsPort, () => certificateHolder.Current is not null));
        services.AddSingleton<IAcmeCertificateIssuer, CertesAcmeCertificateIssuer>();
        services.AddScoped<ILetsEncryptCertificateStore, LetsEncryptCertificateStore>();
        services.AddScoped<LetsEncryptCertificateManager>();
        services.AddHostedService<LetsEncryptRenewalService>();
        services.AddHostedService<CloudflareTunnelAutostart>();
            return services;
        }
    }
}
