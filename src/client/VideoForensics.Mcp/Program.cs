using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using ModelContextProtocol.Client;

using System.Net.Http.Headers;
using System.Text.Json;

using VideoForensics.Hosting.ServerDiscovery;
using VideoForensics.Mcp.ServerDiscovery;

namespace VideoForensics.Mcp
{
    /// <summary>
    /// VideoForensics MCP bridge entry point. This process has no business logic or data access of
    /// its own — per CLAUDE.md's client/server split rules (which name VideoForensics.Mcp explicitly
    /// as a "client host" that may not reference providers-common/providers-core/any concrete
    /// provider/data.*, or bootstrap AddVideoForensicsDataLayer()/AddVideoForensicsServerCore()), it
    /// is a pure stdio&lt;-&gt;HTTP proxy: every MCP request received over stdio from Claude Desktop is
    /// forwarded to the server's own MCP tool implementations at VideoForensics.WebApp's /mcp endpoint
    /// (see VideoForensics.WebApp.Mcp.Tools.*), and the response is relayed back unchanged.
    ///
    /// EXTERNAL DOCUMENTATION:
    /// - E2E testing guide: see _docs_external/E2E_TESTING_GUIDE.md
    /// - Claude Desktop setup: see _docs_external/README_CLAUDE_DESKTOP.md
    /// - Main README: see README.md in this directory
    /// </summary>
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            _ = builder.Logging.SetMinimumLevel(LogLevel.Information);

            // Resolve the server address the same way MAUI does (mDNS first, cached Internet URL as
            // fallback) - never a client-persisted local address, per CLAUDE.md. A placeholder is used
            // if resolution fails so the stdio process still starts (Claude Desktop sees a running, if
            // unreachable, MCP server) rather than crashing outright.
            var settingsStore = new FileServerLocationSettingsStore();
            var resolver = new ServerLocationResolver(settingsStore, Microsoft.Extensions.Logging.Abstractions.NullLogger<ServerLocationResolver>.Instance);
            Uri serverUri;
            try
            {
                serverUri = await resolver.ResolveServerAddressAsync(CancellationToken.None);
            }
            catch (ServerNotReachableException)
            {
                serverUri = new Uri("https://localhost:5162");
            }

            // The paired-device bearer token that authorizes this bridge against the server's /mcp
            // endpoint (VideoForensics.WebApp/Program.cs: app.MapMcp("/mcp").RequireAuthorization()).
            // Obtained via device-code pairing and stored locally; no config-based placeholder.
            var apiKeyStore = new FileApiKeyStore();
            string? apiKey = apiKeyStore.GetApiKey();

            ILoggerFactory loggerFactory = LoggerFactory.Create(lb => lb.SetMinimumLevel(LogLevel.Information));

            if (apiKey is null)
            {
                apiKey = await PairViaDeviceCodeAsync(serverUri, loggerFactory.CreateLogger("DeviceCodePairing"), CancellationToken.None);
                apiKeyStore.SetApiKey(apiKey);
            }

            var httpClient = new HttpClient { BaseAddress = serverUri };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Create the downstream McpClient that will proxy requests to the server's /mcp endpoint.
            var httpClientTransportOptions = new HttpClientTransportOptions
            {
                Endpoint = new Uri(serverUri, "/mcp")
            };
            var httpClientTransport = new HttpClientTransport(httpClientTransportOptions, httpClient, loggerFactory);
            McpClient downstream = await McpClient.CreateAsync(httpClientTransport, null, loggerFactory, CancellationToken.None);

            _ = builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithListToolsHandler(async (request, ct) => await downstream.ListToolsAsync(request.Params, ct))
                .WithCallToolHandler(async (request, ct) => await downstream.CallToolAsync(request.Params, ct))
                .WithListResourcesHandler(async (request, ct) => await downstream.ListResourcesAsync(request.Params, ct))
                .WithReadResourceHandler(async (request, ct) => await downstream.ReadResourceAsync(request.Params, ct))
                .WithListPromptsHandler(async (request, ct) => await downstream.ListPromptsAsync(request.Params, ct))
                .WithGetPromptHandler(async (request, ct) => await downstream.GetPromptAsync(request.Params, ct));

            using IHost host = builder.Build();
            ILogger<Program> logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("VideoForensics MCP bridge ready, proxying to {ServerUri}", serverUri);

            await host.RunAsync();
        }

        /// <summary>
        /// Perform device-code pairing to obtain an API key for authenticating against the server's /mcp endpoint.
        /// Prints a human-readable verification URI and polls until approved, expired, or deadline exceeded.
        /// </summary>
        /// <remarks>
        /// If pairing fails (expires before approval or deadline exceeded), this method throws an
        /// InvalidOperationException and the caller lets it propagate, crashing the MCP process.
        /// Claude Desktop's process-restart/backoff behavior is the right way to surface a failed
        /// pairing attempt to the human.
        /// </remarks>
        private static async Task<string> PairViaDeviceCodeAsync(Uri serverUri, ILogger logger, CancellationToken ct)
        {
            using var httpClient = new HttpClient();

            // Step 1: Request a device code
            var deviceCodeEndpoint = new Uri(serverUri, "/api/v1/pairing/device-code");
            using HttpResponseMessage deviceCodeResponse = await httpClient.PostAsync(deviceCodeEndpoint, new StringContent(""), ct);
            _ = deviceCodeResponse.EnsureSuccessStatusCode();

            string deviceCodeContent = await deviceCodeResponse.Content.ReadAsStringAsync(ct);
            using var deviceCodeDoc = JsonDocument.Parse(deviceCodeContent);
            JsonElement deviceCodeRoot = deviceCodeDoc.RootElement;

            string deviceCode = deviceCodeRoot.GetProperty("deviceCode").GetString()
                ?? throw new InvalidOperationException("Device code response missing 'deviceCode'");
            string userCode = deviceCodeRoot.GetProperty("userCode").GetString()
                ?? throw new InvalidOperationException("Device code response missing 'userCode'");
            string verificationUri = deviceCodeRoot.GetProperty("verificationUri").GetString()
                ?? throw new InvalidOperationException("Device code response missing 'verificationUri'");
            int expiresInSeconds = deviceCodeRoot.GetProperty("expiresInSeconds").GetInt32();
            int intervalSeconds = deviceCodeRoot.GetProperty("intervalSeconds").GetInt32();

            // Combine server URI with the relative verification URI to form an absolute URL
            var absoluteVerificationUri = new Uri(serverUri, verificationUri.TrimStart('/'));
            string verificationUrl = $"{absoluteVerificationUri}?code={userCode}";

            // Log the verification instruction
            logger.LogInformation("To authorize this MCP bridge, open {VerificationUrl} in a browser and approve it (code expires in {ExpiresInSeconds}s).",
                verificationUrl, expiresInSeconds);

            // Step 2: Poll for approval
            var pollEndpoint = new Uri(serverUri, $"/api/v1/pairing/device-code/{deviceCode}/poll");
            DateTime deadline = DateTime.UtcNow.AddSeconds(expiresInSeconds);

            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);

                using HttpResponseMessage pollResponse = await httpClient.PostAsync(pollEndpoint, new StringContent(""), ct);
                string pollContent = await pollResponse.Content.ReadAsStringAsync(ct);

                using var pollDoc = JsonDocument.Parse(pollContent);
                JsonElement pollRoot = pollDoc.RootElement;

                string status = pollRoot.GetProperty("status").GetString()
                    ?? throw new InvalidOperationException("Poll response missing 'status'");

                if (status == "approved")
                {
                    string apiKey = pollRoot.GetProperty("apiKey").GetString()
                        ?? throw new InvalidOperationException("Approved response missing 'apiKey'");
                    logger.LogInformation("Device-code pairing approved; obtained API key.");
                    return apiKey;
                }

                if (status == "expired")
                {
                    throw new InvalidOperationException("Device-code pairing expired before it was approved.");
                }

                // status == "pending", keep polling
            }

            throw new InvalidOperationException("Device-code pairing expired before it was approved.");
        }
    }
}
