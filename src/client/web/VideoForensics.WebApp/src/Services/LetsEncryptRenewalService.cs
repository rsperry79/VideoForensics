using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Checks the certificate every 12 hours and renews it inside the 30-day window. The first check runs at
    /// startup, which also loads any stored certificate into the holder Kestrel reads.
    /// </summary>
    public sealed class LetsEncryptRenewalService : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<LetsEncryptRenewalService> _logger;

        public LetsEncryptRenewalService(IServiceScopeFactory scopes, ILogger<LetsEncryptRenewalService> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using IServiceScope scope = _scopes.CreateScope();
                    var manager = scope.ServiceProvider.GetRequiredService<LetsEncryptCertificateManager>();
                    CertificateCheckResult result = await manager.EnsureCertificateAsync(force: false, stoppingToken);
                    if (!result.Success)
                    {
                        _logger.LogWarning("Certificate check skipped: {Message}", result.Message);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // Keep the loop alive: a failed issuance is retried on the next interval.
                    _logger.LogError(ex, "Certificate renewal failed; retrying in {Hours} hours", CheckInterval.TotalHours);
                }

                await Task.Delay(CheckInterval, stoppingToken);
            }
        }
    }
}
