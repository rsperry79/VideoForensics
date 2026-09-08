using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Alerts;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Syncfusion.Blazor;

using System.Diagnostics;
using System.Net.Http;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting;
using VideoForensics.Hosting.ServerDiscovery;
using VideoForensics.MauiApp.AppLock;
using VideoForensics.MauiApp.ServerDiscovery;

namespace VideoForensics.MauiApp
{
    public static class MauiProgram
    {
        public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
        {
#if DEBUG
            // During local development, ensure VideoForensics.WebApp is running before continuing.
            // This is a DEBUG-only convenience: a developer can simply run the MAUI app in the debugger
            // and it will automatically launch the web server if it isn't already listening.
            // This eliminates the need to manually start two separate projects during development.
            // If the server is unreachable or fails to start, the app still proceeds with normal
            // server-resolution logic, allowing graceful degradation for non-admin or resource-constrained
            // development scenarios.
            EnsureWebAppRunningForDebugAsync()
                .GetAwaiter()
                .GetResult();
#endif

            var syncfusionLicenseKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "syncfusion-license.key");
            if (File.Exists(syncfusionLicenseKeyPath))
            {
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(File.ReadAllText(syncfusionLicenseKeyPath).Trim());
            }

            var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddSyncfusionBlazor();

            // Register file-based logging - there's no console to log to in a MAUI app. Log file
            // lands under %ProgramData%/VideoForensics/logs, matching the console app's pattern
            // (src/client/VideoForensics/Program.cs).
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics");
            Directory.CreateDirectory(configDir);
            var logFilePath = Path.Combine(configDir, "logs", $"videoforensics-maui-{DateTime.Now:yyyy-MM-dd}.log");
            builder.Logging.SetMinimumLevel(LogLevel.Information);
            builder.Logging.AddProvider(new VideoForensics.MauiApp.Logging.FileLoggerProvider(logFilePath, LogLevel.Information));

            // AddVideoForensicsDataLayer() -> AddVideoForensicsDatabase() registers a bare
            // AddDataProtection() for CredentialEncryptionProvider. The first time anything resolves
            // IDataProtectionProvider, DataProtectionOptionsSetup.Configure unconditionally computes an
            // application discriminator via internal type HostingApplicationDiscriminator, which reads
            // IHostEnvironment.ContentRootPath - MAUI's MauiHostEnvironment doesn't implement that
            // property and throws NotImplementedException. HostingApplicationDiscriminator is internal
            // with no public seam (confirmed: no IApplicationDiscriminator interface exists in this
            // package to substitute), and chaining .SetApplicationName() after the fact does NOT help -
            // that Configure delegate is registered after DataProtectionOptionsSetup's and only runs (if
            // it even gets a chance to) once the eager `??=` computation above has already thrown. The
            // only working seam is IHostEnvironment itself: register a fixed replacement AFTER MAUI's
            // builder has already added its own (last registration for a service type wins in
            // Microsoft.Extensions.DependencyInjection - the same rule this file already relies on for
            // IAppLockPreferencesStore below) so HostingApplicationDiscriminator reads our ContentRootPath
            // instead. Nothing else in this app depends on IHostEnvironment, so overriding it wholesale
            // is safe.
            builder.Services.AddSingleton<IHostEnvironment>(new FixedHostEnvironment(configDir));

            var dataProtectionKeyPath = Path.Combine(configDir, "keys");
            Directory.CreateDirectory(dataProtectionKeyPath);
            builder.Services.AddDataProtection()
                .SetApplicationName("VideoForensics")
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));

            // Cutover to HTTP-backed remote repositories (Milestone 5 completion). MAUI now talks
            // exclusively to a remote server's Minimal API instead of owning its own local SQLite DB
            // and Ring provider access. See VideoForensicsHostingExtensions.AddVideoForensicsClientApi()
            // for the 17 Remote* implementations that replace the local-data and provider-access
            // registrations that previous milestones used.
            // Milestone 6: Implement dynamic mDNS-based local discovery (Milestone 6 completion).
            // MAUI now discovers the server via mDNS on the local network, falling back to a cached
            // Internet URL for remote users who are not on the LAN.
            builder.Services.AddSingleton<IServerLocationSettingsStore, MauiServerLocationSettingsStore>();
            builder.Services.AddSingleton<IServerLocationResolver>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ServerLocationResolver>>();
                var settingsStore = sp.GetRequiredService<IServerLocationSettingsStore>();
                return new ServerLocationResolver(settingsStore, logger);
            });

            // Resolve server address synchronously at app startup. Since IServerLocationResolver
            // internally times out after 3 seconds, blocking the synchronous CreateMauiApp() here is
            // acceptable - the resolver won't hang indefinitely.
            // If ServerNotReachableException is thrown (no local server + no cached URL), we still
            // register AddVideoForensicsClientApi with a placeholder URI so the app doesn't crash on
            // startup. The UI can later show a "pairing required" state when the placeholder fails
            // to connect.
            Uri serverUri;
            bool serverResolutionFailed = false;
            try
            {
                var resolver = new ServerLocationResolver(
                    new MauiServerLocationSettingsStore(),
                    new Microsoft.Extensions.Logging.Abstractions.NullLogger<ServerLocationResolver>());
                serverUri = resolver.ResolveServerAddressAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (ServerNotReachableException)
            {
                // No server found locally and no cached URL - use placeholder
                serverUri = new Uri("https://localhost:7004");
                serverResolutionFailed = true;
            }

            builder.Services.AddVideoForensicsClientApi(serverUri);

            // Override the default server location services with MAUI-specific implementations.
            // AddVideoForensicsClientApi registered the defaults above via AddServerLocationServices,
            // but MAUI needs to track the actual resolved address and connectivity state.
            // Last registration wins in DI, so these override the defaults.
            builder.Services.AddSingleton<VideoForensics.Ui.Shared.Services.IServerLocationInformationService, VideoForensics.MauiApp.Services.MauiServerLocationInformationService>();
            builder.Services.AddSingleton<VideoForensics.Ui.Shared.Services.IServerConnectivityState, VideoForensics.MauiApp.Services.MauiServerConnectivityState>();

            // Client-side WebAuthn ceremony driver + circuit-scoped paired-device session (plan
            // §5.1/§5.11), mirroring VideoForensics.WebApp/Program.cs - MainLayout.razor (rendered for
            // every page, MAUI included) @injects PairedSessionState, so without this registration
            // BlazorWebView fails to instantiate MainLayout at all and the app hangs on the static
            // "Loading..." placeholder in wwwroot/index.html forever. WebAuthnClient is needed by the
            // individual Security*/Pair/NetworkSettings/DeviceSignIn pages that share this UI project.
            builder.Services.AddScoped<VideoForensics.Ui.Shared.Services.PairedSessionState>();
            builder.Services.AddScoped<VideoForensics.Ui.Shared.Services.WebAuthnClient>();

            // Docked-layout chrome state (plan: docked MAUI/Blazor layout) - collapse state
            // (device-local), right-panel page-context slot, and per-operator theme/culture, all
            // circuit-scoped like PairedSessionState above.
            builder.Services.AddScoped<VideoForensics.Ui.Shared.Services.LayoutPreferencesState>();
            builder.Services.AddScoped<VideoForensics.Ui.Shared.Services.RightPanelContentService>();
            builder.Services.AddScoped<VideoForensics.Ui.Shared.Services.ThemePreferenceService>();
            builder.Services.AddSingleton<VideoForensics.Ui.Shared.Services.ICultureSwitcher, VideoForensics.Ui.Shared.Services.CultureSwitcher>();
            builder.Services.AddLocalization();

            // MainLayout.razor's shared <RadzenComponents> needs a render mode decision too - MAUI's
            // BlazorWebView has no ASP.NET Core render-mode infrastructure at all (it renders through
            // its own native IPC channel) and throws "the current platform does not support the
            // ServerRenderMode" the instant MainLayout renders if @rendermode is set to anything.
            // NullBlazorRenderModeProvider tells MainLayout to pass null instead - see
            // IBlazorRenderModeProvider and VideoForensics.WebApp/Program.cs's InteractiveServer one.
            builder.Services.AddSingleton<VideoForensics.Ui.Shared.Services.IBlazorRenderModeProvider, VideoForensics.Ui.Shared.Services.NullBlazorRenderModeProvider>();

            // CommunityToolkit.Maui's UseMauiCommunityToolkit() does NOT auto-register IFileSaver in DI -
            // it only exposes the static FileSaver.Default singleton. Registering it explicitly here is
            // required, or resolving MauiFileDialogService (which takes IFileSaver via constructor
            // injection) throws InvalidOperationException the moment the Import/Export page is
            // constructed, which blocks that page from loading at all.
            builder.Services.AddSingleton<CommunityToolkit.Maui.Storage.IFileSaver>(CommunityToolkit.Maui.Storage.FileSaver.Default);

            // Native "Save As" dialog for the backup-export feature (Import/Export page) - MAUI has a
            // real OS file-save dialog via CommunityToolkit.Maui's IFileSaver, unlike the Web host which
            // has to trigger a browser download instead. See WebFileDialogService for that side.
            builder.Services.AddSingleton<VideoForensics.Ui.Shared.Services.IFileDialogService, VideoForensics.MauiApp.Services.MauiFileDialogService>();

            // Local filesystem folder browser for picking the import "media root" folder (Import/Export
            // page) - shared implementation, works identically on MAUI and Web since both processes run
            // on the same local machine as the user in this app's deployment model.
            builder.Services.AddSingleton<VideoForensics.Ui.Shared.Services.IDirectoryBrowserService, VideoForensics.Ui.Shared.Services.DirectoryBrowserService>();

            // Local app-lock (plan §5.9) - overrides the no-op IAppLockPreferencesStore default that
            // AddVideoForensicsClientApi() would have registered for every host. Windows-only for now,
            // matching this MAUI target's own scope. (Note: AddVideoForensicsClientApi() does not register
            // this, so we must do so here.)
            builder.Services.AddSingleton<ILocalAuthGate, FingerprintLocalAuthGate>();
            builder.Services.AddSingleton<IAppLockPreferencesStore, MauiAppLockPreferencesStore>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // Start the live hub connection for real-time download progress and urgent security events.
            // Fire-and-forget: the hub starts asynchronously in the background and errors are logged
            // (not allowed to crash app startup).
            _ = Task.Run(async () =>
            {
                try
                {
                    var hubConnection = app.Services.GetRequiredService<ILiveHubConnection>();

                    // Subscribe to urgent security events and show them as toasts before starting the hub,
                    // so no events are missed. The event handler fires async work (Toast.Show) via fire-and-forget.
                    hubConnection.UrgentEventReceived += urgentEvent =>
                    {
                        string message = urgentEvent.Details ?? urgentEvent.EventType ?? "Security event";
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Toast.Make(message).Show();
                            }
                            catch
                            {
                                // Toast failure should not crash the app
                            }
                        });
                    };

                    await hubConnection.StartAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    try
                    {
                        var logger = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("MauiApp");
                        logger.LogError(ex, "Failed to start live hub connection");
                    }
                    catch
                    {
                        // Logging failure should not crash app startup
                    }
                }
            });

            // Populate server location info and connectivity state based on whether resolution succeeded.
            // The IServerLocationInformationService and IServerConnectivityState singletons have already
            // been registered above, so we can resolve and update them here.
            var locationInfoService = app.Services.GetRequiredService<VideoForensics.Ui.Shared.Services.IServerLocationInformationService>() as VideoForensics.MauiApp.Services.MauiServerLocationInformationService;
            var connectivityState = app.Services.GetRequiredService<VideoForensics.Ui.Shared.Services.IServerConnectivityState>();

            if (locationInfoService is not null)
            {
                locationInfoService.CurrentServerAddress = serverUri;
                // Determine if the current address is local or from cache:
                // If resolution failed, we're using a placeholder and connectivity is unreachable.
                // Otherwise, check if it's a local address by examining the resolved URI.
                if (serverResolutionFailed)
                {
                    locationInfoService.IsLocalAddress = null;
                    connectivityState.MarkAsUnreachable();
                }
                else
                {
                    // The address was successfully resolved - determine if it's local or cached Internet.
                    // A local mDNS-discovered address typically contains the device hostname and uses a
                    // high port number (not the default 7004). A cached Internet URL uses the default port.
                    locationInfoService.IsLocalAddress = serverUri.Port != 7004;
                }
            }

            return app;
        }

#if DEBUG
        /// <summary>
        /// Ensures VideoForensics.WebApp is running on localhost:5162 for DEBUG builds.
        /// This is a local-development convenience only: the method probes the health endpoint,
        /// launches the server via dotnet run if unreachable, and waits for it to be ready.
        /// Failures are logged as warnings but never block app startup.
        /// </summary>
        private static async Task EnsureWebAppRunningForDebugAsync()
        {
            const string webAppHealthUrl = "http://localhost:5162/healthz";
            const int probeTimeoutMs = 1500;
            const int pollIntervalMs = 500;
            const int maxPollDurationMs = 15000;

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = TimeSpan.FromMilliseconds(probeTimeoutMs);

                    // Probe health endpoint to see if server is already running
                    try
                    {
                        var response = await client.GetAsync(webAppHealthUrl, HttpCompletionOption.ResponseHeadersRead);
                        if (response.IsSuccessStatusCode)
                        {
                            // Server is already running
                            return;
                        }
                    }
                    catch (HttpRequestException)
                    {
                        // Server is not running, proceed to launch it
                    }
                    catch (OperationCanceledException)
                    {
                        // Probe timed out, server may not be running
                    }

                    // Server is not responding; launch VideoForensics.WebApp
                    string solutionRoot = FindSolutionRoot();
                    if (string.IsNullOrEmpty(solutionRoot))
                    {
                        System.Diagnostics.Debug.WriteLine("DEBUG: Could not find solution root to launch WebApp");
                        return;
                    }

                    string webAppProjectPath = Path.Combine(
                        solutionRoot,
                        "src", "client", "web", "VideoForensics.WebApp", "VideoForensics.WebApp.csproj");

                    if (!File.Exists(webAppProjectPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"DEBUG: WebApp project not found at {webAppProjectPath}");
                        return;
                    }

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = $"run --project \"{webAppProjectPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = false,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    };

                    try
                    {
                        System.Diagnostics.Process.Start(psi);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"DEBUG: Failed to start WebApp process: {ex.Message}");
                        return;
                    }

                    // Poll health endpoint until server is ready or timeout expires
                    var pollStart = DateTime.UtcNow;
                    while ((DateTime.UtcNow - pollStart).TotalMilliseconds < maxPollDurationMs)
                    {
                        await Task.Delay(pollIntervalMs);

                        try
                        {
                            var response = await client.GetAsync(webAppHealthUrl, HttpCompletionOption.ResponseHeadersRead);
                            if (response.IsSuccessStatusCode)
                            {
                                // Server is ready
                                return;
                            }
                        }
                        catch (HttpRequestException)
                        {
                            // Still not ready, continue polling
                        }
                        catch (OperationCanceledException)
                        {
                            // Poll timed out, continue trying
                        }
                    }

                    System.Diagnostics.Debug.WriteLine("DEBUG: WebApp did not become ready within timeout");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: EnsureWebAppRunningForDebugAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Walks up the directory tree from the current AppContext.BaseDirectory to find VideoForensics.sln.
        /// Returns the directory containing the solution file, or null if not found.
        /// </summary>
        private static string FindSolutionRoot()
        {
            var currentDir = new DirectoryInfo(AppContext.BaseDirectory);

            while (currentDir != null)
            {
                var solutionFile = Path.Combine(currentDir.FullName, "VideoForensics.sln");
                if (File.Exists(solutionFile))
                {
                    return currentDir.FullName;
                }

                currentDir = currentDir.Parent;
            }

            return null;
        }
#endif

        // Minimal stand-in for the IHostEnvironment MAUI can't fully implement (its ContentRootPath
        // getter throws NotImplementedException) - see the comment above where this is registered.
        private sealed class FixedHostEnvironment : IHostEnvironment
        {
            public FixedHostEnvironment(string contentRootPath)
            {
                ContentRootPath = contentRootPath;
                ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
            }

            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = "VideoForensics.MauiApp";
            public string ContentRootPath { get; set; }
            public IFileProvider ContentRootFileProvider { get; set; }
        }
    }
}
