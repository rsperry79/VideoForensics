using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Text.Json;
using System.Text.Json.Serialization;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Hosting;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

namespace VideoForensics.Providers.Ring.SelfTester
{
    internal static class Program
    {
        /// <summary>Marker type for the initialization logger's category name - Program itself is static and can't be used as a generic type argument.</summary>
        private sealed class SelfTesterLogCategory
        {
        }

        /// <summary>
        /// Console logger matching the tool's quiet-by-default CLI output: only warnings/errors are
        /// printed, everything else (Info-level DI/repository logging) is suppressed.
        /// </summary>
        private sealed class ConsoleWarningErrorLoggerProvider : ILoggerProvider
        {
            public ILogger CreateLogger(string categoryName)
            {
                return new ConsoleWarningErrorLogger();
            }

            public void Dispose()
            {
            }

            private sealed class ConsoleWarningErrorLogger : ILogger
            {
                public IDisposable BeginScope<TState>(TState state) where TState : notnull
                {
                    return null!;
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return logLevel is LogLevel.Error or LogLevel.Warning;
                }

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                {
                    if (IsEnabled(logLevel))
                    {
                        Console.Error.WriteLine($"[{logLevel}] {formatter(state, exception)}");
                    }
                }
            }
        }

        /// <summary>
        /// Builds the same DI composition root every other VideoForensics host uses (WebApp, the
        /// legacy console app) - see VideoForensicsHostingExtensions. This is what makes SelfTester
        /// share RingAuthService's database-backed credential storage (and its DAPI-protected
        /// encryption keys) with the rest of the app, instead of hand-rolling its own registrations
        /// that could silently drift out of sync (as happened before: a bare AddDataProtection() with
        /// no fixed key-ring path meant a token saved by one run couldn't reliably be decrypted by
        /// the next).
        /// </summary>
        private static IServiceProvider BuildServiceProvider(string? dbPath)
        {
            var services = new ServiceCollection();
            _ = services.AddLogging(b => b.AddProvider(new ConsoleWarningErrorLoggerProvider()));

            // Same fixed key-ring location and DAPI protection as VideoForensics.WebApp/MauiApp -
            // must match exactly, or a refresh token saved by one host can't be decrypted by another.
            string dataProtectionKeyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "keys");
            _ = Directory.CreateDirectory(dataProtectionKeyPath);
            IDataProtectionBuilder dataProtectionBuilder = services.AddDataProtection()
                .SetApplicationName("VideoForensics")
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
            // DPAPI is Windows-only; this app is platform-agnostic (see PlatformDirectoryService/
            // CredentialEncryptionFactory for the same split), so non-Windows hosts fall back to
            // ASP.NET Core's unencrypted-on-disk key-ring default, protected only by filesystem
            // permissions on the ProgramData-equivalent directory above.
            if (OperatingSystem.IsWindows())
            {
                _ = dataProtectionBuilder.ProtectKeysWithDpapi();
            }

            _ = services.AddVideoForensicsDataLayer(dbPath);
            _ = services.AddVideoForensicsServerCore("Ring");

            return services.BuildServiceProvider();
        }

        private static readonly JsonSerializerOptions IndexJsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static async Task<int> Main(string[] args)
        {
            (CliOptions? options, string? parseError) = CliOptions.Parse(args);
            if (parseError != null)
            {
                Console.Error.WriteLine($"Error: {parseError}");
                Console.Error.WriteLine();
                Console.Error.WriteLine(CliOptions.HelpText);
                return 2;
            }

            if (options!.ShowHelp)
            {
                Console.WriteLine(CliOptions.HelpText);
                return 0;
            }

            if (options.ListEndpoints)
            {
                PrintEndpointList(options.ListEndpointsJson);
                return 0;
            }

            if (options.InteractiveAuth)
            {
                return await RunInteractiveAuthAsync(options);
            }

            EndpointRegistry.CurrentHistoryLimit = options.HistoryLimit;
            EndpointRegistry.SirenDurationSeconds = options.SirenDurationSeconds;
            EndpointRegistry.VolumeLevel = options.VolumeLevel;
            EndpointRegistry.ChimeTypeValue = options.ChimeTypeValue;
            EndpointRegistry.DndSeconds = options.DndSeconds;
            EndpointRegistry.LocationModeValue = options.LocationModeValue;
            EndpointRegistry.DingId = options.DingId;
            EndpointRegistry.AssetUuid = options.AssetUuid;
            EndpointRegistry.PushToken = options.PushToken;

            // Same DI composition root as WebApp/the legacy console app, so credentials and their
            // encryption keys are shared with the rest of the app instead of living in a SelfTester-only silo.
            IServiceProvider serviceProvider = BuildServiceProvider(options.DbPath);

            ILogger<SelfTesterLogCategory> initLogger = serviceProvider.GetRequiredService<ILogger<SelfTesterLogCategory>>();
            try
            {
                await VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync(serviceProvider, initLogger, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: database initialization failed: {ex.Message}");
                return 2;
            }

            using IServiceScope authScope = serviceProvider.CreateScope();
            IProviderAuthService authService = authScope.ServiceProvider.GetRequiredService<IProviderAuthService>();
            ISessionProvider sessionProvider = serviceProvider.GetRequiredService<ISessionProvider>();

            // "The currently active account" mirrors the UI's own account switcher: the single
            // IForensicsConfiguration.ActiveProviderAccountId setting persisted in AppSettings, loaded
            // by InitializeVideoForensicsDataAsync above. Falls back to RingAuthService's own
            // most-recently-authenticated-Ring-account heuristic when nothing has been selected yet.
            IForensicsConfiguration forensicsConfig = serviceProvider.GetRequiredService<IForensicsConfiguration>();
            Guid? activeAccountId = forensicsConfig.ActiveProviderAccountId;

            Session session;
            try
            {
                bool restored = activeAccountId.HasValue
                    ? await authService.RestoreFromSavedCredentialsAsync(activeAccountId)
                    : await authService.RestoreFromSavedCredentialsAsync();

                if (!restored)
                {
                    // If no saved credentials, try explicit options
                    ResolvedCredentials? credentials = CredentialResolver.Resolve(options.RefreshToken, options.UserName, options.Password);
                    if (credentials == null)
                    {
                        WriteNoCredentialsError();
                        return 2;
                    }

                    bool result = await AuthenticateWithCredentialsAsync(credentials, authService);
                    if (!result)
                    {
                        Console.Error.WriteLine("Authentication failed");
                        return 2;
                    }
                }

                session = sessionProvider.GetSession();
                if (session == null)
                {
                    Console.Error.WriteLine("No session established");
                    return 2;
                }
            }
            catch (VideoForensics.Providers.Ring.Exceptions.TwoFactorAuthenticationRequiredException)
            {
                Console.Error.WriteLine("Error: this account requires two-factor authentication, which this non-interactive run");
                Console.Error.WriteLine("cannot complete. Run 'dotnet run -- --auth' once instead - it prompts for the 2FA code and");
                Console.Error.WriteLine("saves a reusable refresh token for every run after that.");
                Console.Error.WriteLine(ReadmePointer);
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: authentication failed: {ex.Message}");
                Console.Error.WriteLine("If this keeps happening, run 'dotnet run -- --auth' to re-authenticate from scratch.");
                Console.Error.WriteLine(ReadmePointer);
                return 2;
            }

            // Pick output directory under ProgramData, matching LocalRingSelfTestService's pattern; must not default into the source tree
            string outputDir = options.OutputDir ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "VideoForensics",
                "SelfTesterResults",
                DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"));
            _ = Directory.CreateDirectory(outputDir);

            IndexDocument index;
            try
            {
                var runner = new Runner(session, outputDir, options.Quiet);
                var runOptions = new RunOptions(
                    options.Endpoints,
                    options.Destructive,
                    options.NoPhysical,
                    options.LocationId,
                    options.DoorbotId,
                    options.ChimeId);
                index = await runner.RunAsync(runOptions, "RingAuthService");
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 2;
            }

            string indexPath = Path.Combine(outputDir, "index.json");
            await File.WriteAllTextAsync(indexPath, JsonSerializer.Serialize(index, IndexJsonOptions));

            if (!options.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine($"{index.Summary.Succeeded}/{index.Summary.TotalCalls} calls succeeded.");
            }

            Console.WriteLine(indexPath);

            if (options.VerifyDb)
            {
                await RunDbCompletenessCheckAsync(session, options, outputDir);
            }

            return index.Summary.Failed > 0 ? 1 : 0;
        }

        /// <summary>
        /// Fetches this account's live devices/locations directly (independent of whatever
        /// endpoints --endpoints selected, so --verify-db works regardless of the run's scope) and
        /// cross-checks each against the VideoForensics app's own SQLite database. Never affects
        /// the process exit code - a device/location that legitimately hasn't been downloaded yet
        /// is expected, not a test failure.
        /// </summary>
        private static async Task RunDbCompletenessCheckAsync(Session session, CliOptions options, string outputDir)
        {
            // Same default as AddVideoForensicsSqlite: %ProgramData%\VideoForensics\videoforensics.db.
            string dbPath = options.DbPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "VideoForensics", "videoforensics.db");

            if (!options.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine($"Checking database completeness against {dbPath} ...");
            }

            if (!File.Exists(dbPath))
            {
                Console.Error.WriteLine($"Warning: --verify-db requested but database file not found at {dbPath}. Skipping (run the main VideoForensics app at least once first, or pass --db-path).");
                return;
            }

            Devices? devices;
            System.Collections.Generic.List<VideoForensics.Providers.Ring.Entities.Location>? locations;
            try
            {
                devices = await session.GetRingDevices();
                locations = await session.GetLocations();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: --verify-db could not fetch devices/locations from Ring: {ex.Message}. Skipping.");
                return;
            }

            DbCompletenessReport report;
            try
            {
                report = await DbCompletenessChecker.CheckAsync(devices, locations, dbPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: --verify-db could not query the database: {ex.Message}. Skipping.");
                return;
            }

            string reportPath = Path.Combine(outputDir, "db-completeness.json");
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, IndexJsonOptions));

            if (!options.Quiet)
            {
                foreach (DbCompletenessRecord? missing in report.Devices.Where(d => !d.FoundInDb))
                {
                    Console.WriteLine($"   MISSING device: {missing.Kind} {missing.ProviderId} ({missing.Name ?? "(unnamed)"})");
                }

                foreach (DbCompletenessRecord? missing in report.Locations.Where(l => !l.FoundInDb))
                {
                    Console.WriteLine($"   MISSING location: {missing.ProviderId} ({missing.Name ?? "(unnamed)"})");
                }

                Console.WriteLine($"Devices: {report.Devices.Count - report.MissingDeviceCount}/{report.Devices.Count} found in DB. " +
                    $"Locations: {report.Locations.Count - report.MissingLocationCount}/{report.Locations.Count} found in DB.");

                // Display metadata capture statistics
                if (report.TotalEvents > 0 || report.TotalMediaItems > 0)
                {
                    Console.WriteLine($"Metadata capture: {report.MetadataCompleteness}");
                    if (report.DevicesWithMetadata > 0 || report.LocationsWithMetadata > 0)
                    {
                        Console.WriteLine($"  Devices with metadata: {report.DevicesWithMetadata}, Locations: {report.LocationsWithMetadata}");
                    }
                }
            }

            Console.WriteLine(reportPath);
        }

        private static async Task<bool> AuthenticateWithCredentialsAsync(ResolvedCredentials credentials, IProviderAuthService authService)
        {
            if (credentials.RefreshToken != null)
            {
                // Use refresh token
                Session session = await Session.GetSessionByRefreshToken(credentials.RefreshToken);
                if (session?.OAuthToken != null)
                {
                    return true;
                }
            }

            if (credentials.UserName != null && credentials.Password != null)
            {
                // Use username/password
                AuthResult result = await authService.AuthenticateAsync(credentials.UserName, credentials.Password);
                return result.Success;
            }

            return false;
        }

        private const string ReadmePointer = "Run 'dotnet run -- --auth' first to set up authentication.";

        private static void WriteNoCredentialsError()
        {
            Console.Error.WriteLine("Error: no credentials found. Run 'dotnet run -- --auth' to authenticate interactively and");
            Console.Error.WriteLine("save a reusable refresh token (handles two-factor accounts too), or provide");
            Console.Error.WriteLine("--username/--password or --refresh-token.");
            Console.Error.WriteLine(ReadmePointer);
        }

        /// <summary>
        /// Interactive one-time login: prompts for credentials, authenticates via RingAuthService
        /// (handling a 2FA challenge if one comes back), and saves the result to the database - the
        /// same IProviderAuthService/ICredentialRepository path the WebApp and legacy console app use.
        /// Also makes the authenticated account "the active account" (IForensicsConfiguration.ActiveProviderAccountId),
        /// matching what the UI's AuthForm.razor does after a sign-in.
        /// </summary>
        private static async Task<int> RunInteractiveAuthAsync(CliOptions options)
        {
            Console.WriteLine("Ring interactive login - saves credentials to database for future runs");
            Console.WriteLine("and handles two-factor authentication.\n");

            string? userName = options.UserName;
            if (string.IsNullOrWhiteSpace(userName))
            {
                Console.Write("Ring username/email: ");
                userName = Console.ReadLine();
            }

            if (string.IsNullOrWhiteSpace(userName))
            {
                Console.Error.WriteLine("Error: a username is required.");
                return 2;
            }

            string? password = options.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                password = ReadPassword("Ring password: ");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                Console.Error.WriteLine("Error: a password is required.");
                return 2;
            }

            try
            {
                // Same DI composition root as the normal (non-auth) run and every other host
                // (WebApp, the legacy console app) - see BuildServiceProvider.
                IServiceProvider serviceProvider = BuildServiceProvider(options.DbPath);

                ILogger<SelfTesterLogCategory> initLogger = serviceProvider.GetRequiredService<ILogger<SelfTesterLogCategory>>();
                await VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync(serviceProvider, initLogger, CancellationToken.None);

                using IServiceScope scope = serviceProvider.CreateScope();
                IProviderAuthService authService = scope.ServiceProvider.GetRequiredService<IProviderAuthService>();

                // Authenticate and save to database
                AuthResult result = await authService.AuthenticateWithTwoFactorAsync(
                    userName,
                    password,
                    async () =>
                    {
                        Console.WriteLine();
                        Console.WriteLine("Two-factor authentication is enabled on this account - Ring just sent a code via text/e-mail.");
                        Console.Write("Enter the code: ");
                        await Task.CompletedTask;
                        return Console.ReadLine() ?? "";
                    });

                if (!result.Success)
                {
                    Console.Error.WriteLine($"Error: {result.ErrorMessage}");
                    return 2;
                }

                // Make this the active account, exactly like AuthForm.razor does after a UI sign-in -
                // so a normal (non-auth) SelfTester run picks it up via IForensicsConfiguration.ActiveProviderAccountId.
                IForensicsConfiguration forensicsConfig = serviceProvider.GetRequiredService<IForensicsConfiguration>();
                if (result.ProviderAccountId.HasValue && forensicsConfig.ActiveProviderAccountId != result.ProviderAccountId)
                {
                    forensicsConfig.ActiveProviderAccountId = result.ProviderAccountId;
                    IForensicsConfigurationService configService = scope.ServiceProvider.GetRequiredService<IForensicsConfigurationService>();
                    await configService.SaveConfigurationAsync(forensicsConfig);
                }

                Console.WriteLine();
                Console.WriteLine($"Authenticated and saved credentials to database.");
                Console.WriteLine("Future SelfTester runs will use this automatically.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: authentication failed: {ex.Message}");
                return 2;
            }
        }

        /// <summary>
        /// Reads a line from the console without echoing it, showing '*' per character typed.
        /// Falls back to a plain, unmasked ReadLine when stdin is redirected (piped input, most CI
        /// runners) - Console.ReadKey throws there rather than just not masking, and this needs to
        /// still work non-interactively rather than crash.
        /// </summary>
        private static string ReadPassword(string prompt)
        {
            Console.Write(prompt);

            if (Console.IsInputRedirected)
            {
                return Console.ReadLine() ?? "";
            }

            string password = "";
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password[..^1];
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password += key.KeyChar;
                    Console.Write('*');
                }
            } while (key.Key != ConsoleKey.Enter);
            Console.WriteLine();
            return password;
        }

        private static void PrintEndpointList(bool asJson)
        {
            if (asJson)
            {
                var payload = EndpointRegistry.All.Select(e => new
                {
                    e.Key,
                    e.DisplayName,
                    e.Description,
                    e.SessionMethod,
                    e.HttpMethod,
                    e.ApiPath,
                    Scope = e.Scope.ToString(),
                    e.Destructive,
                    e.Physical
                });
                Console.WriteLine(JsonSerializer.Serialize(payload, IndexJsonOptions));
                return;
            }

            Console.WriteLine("Available endpoints (pass to --endpoints as a comma-separated list, or use --all).");
            Console.WriteLine("[destructive] endpoints require --destructive. [physical] ones also trigger real hardware");
            Console.WriteLine("and are excluded by --no-physical even when --destructive is set.");
            Console.WriteLine();
            foreach (EndpointDescriptor e in EndpointRegistry.All)
            {
                string tags = string.Concat(e.Destructive ? " [destructive]" : "", e.Physical ? " [physical]" : "");
                Console.WriteLine($"  {e.Key,-22} {e.DisplayName}{tags}");
                Console.WriteLine($"  {"",22} {e.SessionMethod} -> {e.HttpMethod} {e.ApiPath}");
                Console.WriteLine($"  {"",22} {e.Description}");
                Console.WriteLine();
            }
        }
    }
}

