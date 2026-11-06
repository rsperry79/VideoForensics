using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System;

namespace VideoForensics.Client.WebApp.Services;

/// <summary>
/// Gives the host more time to stop than the 30-second default. When a stop runs past the shutdown timeout the host
/// cancels its stop token, and the cancellation escapes WindowsServiceLifetime.StopAsync as an unhandled exception that
/// kills the process and writes a crash dump. A longer window lets slow background services finish first.
/// </summary>
public static class HostShutdownOptions
{
    public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Sets the host shutdown timeout on the container.</summary>
    public static void Apply(IServiceCollection services)
    {
        _ = services.Configure<HostOptions>(options => options.ShutdownTimeout = ShutdownTimeout);
    }
}
