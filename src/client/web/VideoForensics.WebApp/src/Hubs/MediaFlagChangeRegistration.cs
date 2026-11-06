using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.WebApp.Services;

namespace VideoForensics.Client.WebApp.Hubs
{
    /// <summary>Server registrations for media-flag change publishing and the in-process source the WebApp's pages read.</summary>
    public static class MediaFlagChangeRegistration
    {
        /// <summary>
        /// Registers the bus (one per process), the notifier the flag endpoints call, and the scoped source each Blazor circuit
        /// subscribes to. Called from Program.cs, so a test can check the same wiring the server uses.
        /// </summary>
        public static IServiceCollection AddMediaFlagChangeServices(this IServiceCollection services)
        {
            _ = services.AddSingleton<MediaFlagChangedBus>();
            _ = services.AddSingleton<IMediaFlagChangeNotifier, LiveMediaFlagNotifier>();
            _ = services.AddScoped<IMediaFlagChangedSource, LocalMediaFlagChangedSource>();
            return services;
        }
    }
}
