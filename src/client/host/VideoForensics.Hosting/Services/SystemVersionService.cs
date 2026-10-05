using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Services
{
    /// <summary>
    /// Provides system version information including semantic version, build timestamp, and release channel.
    /// This service reads version metadata from assembly attributes, environment variables, and configuration.
    /// </summary>
    public class SystemVersionService : ISystemVersionProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SystemVersionService> _logger;

        /// <summary>
        /// Initializes a new instance of the SystemVersionService.
        /// </summary>
        /// <param name="configuration">Configuration provider for reading the release channel setting.</param>
        /// <param name="logger">Logger for diagnostic output.</param>
        public SystemVersionService(IConfiguration configuration, ILogger<SystemVersionService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the current semantic version of the system, extracted from the assembly's informational version attribute.
        /// </summary>
        /// <param name="ct">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the semantic version string (e.g., "1.0.5-alpha.1+build.23").</returns>
        public Task<string> GetCurrentVersionAsync(CancellationToken ct)
        {
            try
            {
                var attribute = typeof(SystemVersionService).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

                if (attribute?.InformationalVersion != null)
                {
                    _logger.LogDebug("Retrieved version from assembly attribute: {Version}", attribute.InformationalVersion);
                    return Task.FromResult(attribute.InformationalVersion);
                }

                // Fallback to AssemblyVersion if informational version is not available
                string? fallbackVersion = typeof(SystemVersionService).Assembly.GetName().Version?.ToString();
                if (fallbackVersion != null)
                {
                    _logger.LogDebug("Retrieved fallback version from assembly version: {Version}", fallbackVersion);
                    return Task.FromResult(fallbackVersion);
                }

                _logger.LogWarning("Could not determine system version from assembly");
                return Task.FromResult("unknown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system version");
                return Task.FromResult("unknown");
            }
        }

        /// <summary>
        /// Gets the build timestamp indicating when this version was compiled.
        /// Attempts to read from the BUILD_TIMESTAMP environment variable first, then falls back to the assembly file write time.
        /// </summary>
        /// <param name="ct">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the build timestamp in UTC, or null if unavailable.</returns>
        public Task<DateTime?> GetBuildTimestampAsync(CancellationToken ct)
        {
            try
            {
                // First, try to read from environment variable set by CI
                string? buildTimestampEnv = Environment.GetEnvironmentVariable("BUILD_TIMESTAMP");
                if (!string.IsNullOrWhiteSpace(buildTimestampEnv) && DateTime.TryParse(buildTimestampEnv, out DateTime timestamp))
                {
                    _logger.LogDebug("Retrieved build timestamp from environment variable: {Timestamp:O}", timestamp);
                    return Task.FromResult((DateTime?)timestamp);
                }

                // Fallback to assembly file write time
                string assemblyPath = typeof(SystemVersionService).Assembly.Location;
                if (!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath))
                {
                    DateTime writeTime = File.GetLastWriteTimeUtc(assemblyPath);
                    _logger.LogDebug("Retrieved build timestamp from assembly file write time: {Timestamp:O}", writeTime);
                    return Task.FromResult((DateTime?)writeTime);
                }

                _logger.LogWarning("Could not determine build timestamp");
                return Task.FromResult<DateTime?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving build timestamp");
                return Task.FromResult<DateTime?>(null);
            }
        }

        /// <summary>
        /// Gets the release channel of the current version (dev, testing, or stable).
        /// Reads from configuration using the "ReleaseChannel" key, defaulting to "dev" if not specified.
        /// </summary>
        /// <param name="ct">Cancellation token for the async operation.</param>
        /// <returns>A task representing the asynchronous operation, containing the release channel name.</returns>
        public Task<string> GetReleaseChannelAsync(CancellationToken ct)
        {
            try
            {
                string channel = _configuration["ReleaseChannel"] ?? "dev";
                _logger.LogDebug("Retrieved release channel from configuration: {Channel}", channel);
                return Task.FromResult(channel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving release channel");
                return Task.FromResult("dev");
            }
        }
    }
}
