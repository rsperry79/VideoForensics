using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Client.Hosting;

namespace VideoForensics.Client.WebApp.Services;

/// <summary>
/// Runs the startup initialization after the host has started, so the service reports Running at once. Each step is
/// unchanged from when it ran before app.Run(). It uses CancellationToken.None so a shutdown never interrupts a migration.
/// </summary>
public sealed class StartupInitializationService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly StartupInitializationGate _gate;
    private readonly ILogger<StartupInitializationService> _logger;

    public StartupInitializationService(
        IServiceProvider services,
        StartupInitializationGate gate,
        ILogger<StartupInitializationService> logger)
    {
        _services = services;
        _gate = gate;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync(_services, _logger, CancellationToken.None);
            _logger.LogInformation("Database initialization (DB, Events backfill, config) completed.");
        }
        catch (Exception ex)
        {
            // Degraded, not fatal: the same policy as before the move. The gate still opens so the site can answer.
            _logger.LogCritical("Database initialization failed. Continuing startup in a degraded state.: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
        }
        finally
        {
            _gate.MarkReady();
        }
    }
}
