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
    /// <summary>
    /// Registers the Blazor UI state for the WebApp's circuits: layout chrome, review and signal page state, and the
    /// localization and render-mode services. Called from Program.cs in the same place the block used to be, so the
    /// registration order does not change.
    /// </summary>
    internal static class WebAppUiStateRegistration
    {
        /// <summary>Adds the circuit-scoped UI state and localization services.</summary>
        internal static IServiceCollection AddWebAppUiState(this IServiceCollection services)
        {
            // Docked-layout chrome state (plan: docked MAUI/Blazor layout) - collapse state (device-local),
            // right-panel page-context slot, and per-operator theme/culture, all circuit-scoped like
            // PairedSessionState above.
            services.AddScoped<LayoutPreferencesState>();
            services.AddScoped<RightPanelContentService>();
            services.AddScoped<RightPanelFilterStore>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Filters.ProviderAccountFilterState>();
            services.AddScoped<LeftPanelContentService>();
            services.AddScoped<InspectorState>();
            services.TryAddSingleton(TimeProvider.System);
            services.AddScoped<ScopeState>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewPageState>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewPopupState>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewSettingsStore>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewCaseMode>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewSelection>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewTimelineModel>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewStillCommand>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Contracts.IReviewClipLoader, VideoForensics.Client.Ui.Shared.Services.Review.ReviewClipLoader>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Contracts.IReviewEventLoader, VideoForensics.Client.Ui.Shared.Services.Review.ReviewEventLoader>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Contracts.IReviewDetailLoader, VideoForensics.Client.Ui.Shared.Services.Review.ReviewDetailLoader>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewUrlCache>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Signal.SignalDrillTrail>();
            services.AddScoped<DeviceOwnershipService>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Contracts.ISignalAnalysisService, VideoForensics.Client.Hosting.Signal.SignalAnalysisService>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Contracts.IBitrateAnalysisService, VideoForensics.Client.Hosting.Signal.BitrateAnalysisService>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Contracts.IMediaBitrateSeriesSource, VideoForensics.Client.Hosting.Signal.LocalMediaBitrateSeriesSource>();
            services.AddScoped<IQueryApiFilterState, QueryApiFilterState>();
            services.AddScoped<CaseState>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.PendingEvidenceTags>();
            services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Review.ReviewEvidenceTagger>();
            services.AddScoped<ThemePreferenceService>();
            services.AddScoped<IUiModeService, UiModeService>();
            services.AddScoped<SimpleHomeBuilder>(sp => new SimpleHomeBuilder(
                sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<IMediaContentUrlProvider>(),
                sp.GetRequiredService<IStringLocalizer<VideoForensics.Client.Ui.Shared.Resources.SharedResources>>()));
            services.AddScoped<IViewportService, DefaultViewportService>();
            services.AddSingleton<VideoForensics.Client.Ui.Shared.Services.IMediaPlaybackPolicy, VideoForensics.Client.Ui.Shared.Services.DefaultMediaPlaybackPolicy>();
            services.AddSingleton<ICultureSwitcher, CultureSwitcher>();
            services.AddLocalization();
            return services;
        }
    }
}
