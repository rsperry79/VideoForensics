using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Auth;
using VideoForensics.Providers.Ring.Auth.Implementations;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Utils;
using VideoForensics.Providers.Ring.Services;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Common.Entities;
using Microsoft.Extensions.Logging;

namespace VideoForensics.Providers.Ring.SelfTester
{
    internal static class Program
    {
        // Simple logger for RingAuthService
        private class ConsoleLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                if (logLevel == LogLevel.Error || logLevel == LogLevel.Warning)
                {
                    Console.Error.WriteLine($"[{logLevel}] {message}");
                }
            }
        }

        private static readonly JsonSerializerOptions IndexJsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static async Task<int> Main(string[] args)
        {
            var (options, parseError) = CliOptions.Parse(args);
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

            // Use RingAuthService (same as main app) to load credentials
            var logger = new ConsoleLogger();
            var sessionProvider = new SessionProvider();
            var credentialStore = new CredentialStore();
            var authService = new RingAuthService(logger, sessionProvider, credentialStore);

            Session session;
            try
            {
                // Try to restore from saved credentials (file-based or database)
                var restored = await authService.RestoreFromSavedCredentialsAsync();
                if (!restored)
                {
                    // If no saved credentials, try explicit options
                    var credentials = CredentialResolver.Resolve(options.RefreshToken, options.UserName, options.Password);
                    if (credentials == null)
                    {
                        WriteNoCredentialsError();
                        return 2;
                    }
                    var result = await AuthenticateWithCredentialsAsync(credentials, authService);
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

            var outputDir = options.OutputDir ?? Path.Combine("SelfTesterResults", DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"));
            Directory.CreateDirectory(outputDir);

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

            var indexPath = Path.Combine(outputDir, "index.json");
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
            var dbPath = options.DbPath;

            // If no path specified, try ProgramData first, then AppData
            if (dbPath == null)
            {
                var programDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics", "videoforensics.db");
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoForensics", "videoforensics.db");

                dbPath = File.Exists(programDataPath) ? programDataPath : appDataPath;
            }

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

            var reportPath = Path.Combine(outputDir, "db-completeness.json");
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, IndexJsonOptions));

            if (!options.Quiet)
            {
                foreach (var missing in report.Devices.Where(d => !d.FoundInDb))
                {
                    Console.WriteLine($"   MISSING device: {missing.Kind} {missing.ProviderId} ({missing.Name ?? "(unnamed)"})");
                }
                foreach (var missing in report.Locations.Where(l => !l.FoundInDb))
                {
                    Console.WriteLine($"   MISSING location: {missing.ProviderId} ({missing.Name ?? "(unnamed)"})");
                }
                Console.WriteLine($"Devices: {report.Devices.Count - report.MissingDeviceCount}/{report.Devices.Count} found in DB. " +
                    $"Locations: {report.Locations.Count - report.MissingLocationCount}/{report.Locations.Count} found in DB.");
            }
            Console.WriteLine(reportPath);
        }

        private static async Task<bool> AuthenticateWithCredentialsAsync(ResolvedCredentials credentials, IProviderAuthService authService)
        {
            if (credentials.RefreshToken != null)
            {
                // Use refresh token
                var session = await Session.GetSessionByRefreshToken(credentials.RefreshToken);
                if (session?.OAuthToken != null)
                {
                    return true;
                }
            }

            if (credentials.UserName != null && credentials.Password != null)
            {
                // Use username/password
                var result = await authService.AuthenticateAsync(credentials.UserName, credentials.Password);
                return result.Success;
            }

            return false;
        }

        /// <summary>
        /// Migrates credentials from auth.json file to the database if they exist and haven't been migrated yet.
        /// </summary>
        private static async Task MigrateAuthJsonToDbAsync(CliOptions options)
        {
            try
            {
                var authJsonPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoForensics", "auth.json");

                if (!File.Exists(authJsonPath))
                {
                    return;
                }

                var dbPath = options.DbPath;
                if (dbPath == null)
                {
                    var programDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "VideoForensics", "videoforensics.db");
                    var appDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "VideoForensics", "videoforensics.db");
                    dbPath = File.Exists(programDataPath) ? programDataPath : appDataPath;
                }

                if (!File.Exists(dbPath))
                {
                    return;
                }

                // Try to load credentials from auth.json
                var authJsonContent = await File.ReadAllTextAsync(authJsonPath);
                var authJsonDoc = JsonDocument.Parse(authJsonContent);
                var authRoot = authJsonDoc.RootElement;

                var username = authRoot.TryGetProperty("UserName", out var userProp) ? userProp.GetString() : null;
                if (string.IsNullOrEmpty(username))
                {
                    return;
                }

                var optionsBuilder = new DbContextOptionsBuilder<VideoForensicsDbContext>();
                optionsBuilder.UseSqlite($"Data Source={dbPath};Pooling=true;Cache=Shared",
                    b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite"));

                await using var db = new VideoForensicsDbContext(optionsBuilder.Options);

                var ringAccount = await db.RingAccounts.FirstOrDefaultAsync();
                if (ringAccount == null)
                {
                    return;
                }

                // Check if credentials already migrated
                var existingCred = await db.Credentials.FirstOrDefaultAsync(
                    c => c.ProviderAccountId == ringAccount.ProviderAccountId && c.CredentialType == "RefreshToken");

                if (existingCred != null)
                {
                    return; // Already migrated
                }

                // Try to migrate the refresh token
                if (authRoot.TryGetProperty("RefreshToken", out var tokenProp))
                {
                    var encryptedToken = tokenProp.GetString();
                    if (!string.IsNullOrEmpty(encryptedToken))
                    {
                        var decrypted = await DecryptCredentialAsync(encryptedToken);
                        if (!string.IsNullOrEmpty(decrypted))
                        {
                            var encryptWithAes = new AesEncryption();
                            var aesEncrypted = encryptWithAes.Encrypt(decrypted);

                            if (!string.IsNullOrEmpty(aesEncrypted))
                            {
                                var credential = new Credential
                                {
                                    Id = Guid.NewGuid(),
                                    ProviderAccountId = ringAccount.ProviderAccountId,
                                    CredentialType = "RefreshToken",
                                    EncryptedValue = aesEncrypted,
                                    EncryptionProvider = "AES-256",
                                    CreatedUtc = DateTime.UtcNow
                                };
                                db.Credentials.Add(credential);
                                await db.SaveChangesAsync();
                                Console.Error.WriteLine("Migrated refresh token from auth.json to database");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Debug: Migration from auth.json failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Persists the authenticated refresh token to the database for future use.
        /// </summary>
        private static async Task PersistRefreshTokenToDbAsync(Session session, CliOptions options)
        {
            try
            {
                var newRefreshToken = session.OAuthToken?.RefreshToken;
                if (string.IsNullOrEmpty(newRefreshToken))
                {
                    return;
                }

                var dbPath = options.DbPath;
                if (dbPath == null)
                {
                    var programDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "VideoForensics", "videoforensics.db");
                    var appDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "VideoForensics", "videoforensics.db");
                    dbPath = File.Exists(programDataPath) ? programDataPath : appDataPath;
                }

                if (!File.Exists(dbPath))
                {
                    return;
                }

                var optionsBuilder = new DbContextOptionsBuilder<VideoForensicsDbContext>();
                optionsBuilder.UseSqlite($"Data Source={dbPath};Pooling=true;Cache=Shared",
                    b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite"));

                await using var db = new VideoForensicsDbContext(optionsBuilder.Options);

                var ringAccount = await db.RingAccounts.FirstOrDefaultAsync();
                if (ringAccount == null)
                {
                    return;
                }

                // Encrypt the refresh token
                var aesEncryption = new AesEncryption();
                var encryptedToken = aesEncryption.Encrypt(newRefreshToken);

                if (string.IsNullOrEmpty(encryptedToken))
                {
                    return;
                }

                // Store or update the credential
                var credential = await db.Credentials.FirstOrDefaultAsync(
                    c => c.ProviderAccountId == ringAccount.ProviderAccountId && c.CredentialType == "RefreshToken");

                if (credential == null)
                {
                    credential = new Credential
                    {
                        Id = Guid.NewGuid(),
                        ProviderAccountId = ringAccount.ProviderAccountId,
                        CredentialType = "RefreshToken",
                        EncryptedValue = encryptedToken,
                        EncryptionProvider = "AES-256",
                        CreatedUtc = DateTime.UtcNow
                    };
                    db.Credentials.Add(credential);
                }
                else
                {
                    credential.EncryptedValue = encryptedToken;
                    credential.RotatedUtc = DateTime.UtcNow;
                    db.Credentials.Update(credential);
                }

                await db.SaveChangesAsync();
                Console.Error.WriteLine("Refresh token persisted to database");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to persist refresh token to database: {ex.Message}");
            }
        }

        /// <summary>
        /// Attempts to load stored refresh token from the VideoForensics database as a fallback
        /// when file-based credential resolution fails. Checks ProgramData first, then AppData.
        /// Also migrates credentials from auth.json to the database on first use.
        /// </summary>
        private static async Task<ResolvedCredentials?> TryLoadCredentialsFromDbAsync(CliOptions options)
        {
            // First, try to migrate credentials from auth.json to the database
            await MigrateAuthJsonToDbAsync(options);

            var dbPath = options.DbPath;

            // If no path specified, try ProgramData first, then AppData
            if (dbPath == null)
            {
                var programDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "VideoForensics", "videoforensics.db");
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoForensics", "videoforensics.db");

                dbPath = File.Exists(programDataPath) ? programDataPath : appDataPath;
            }

            if (!File.Exists(dbPath))
            {
                return null;
            }

            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<VideoForensicsDbContext>();
                optionsBuilder.UseSqlite($"Data Source={dbPath};Pooling=true;Cache=Shared",
                    b => b.MigrationsAssembly("VideoForensics.Data.Database.Sqlite"));

                await using var db = new VideoForensicsDbContext(optionsBuilder.Options);

                var ringAccounts = await db.RingAccounts.ToListAsync();
                if (ringAccounts.Count == 0)
                {
                    return null;
                }

                var ringAccount = ringAccounts.First();
                Console.Error.WriteLine($"Found Ring account: {ringAccount.AccountEmail} (ID: {ringAccount.ProviderAccountId})");

                var credentials = await db.Credentials
                    .Where(c => c.ProviderAccountId == ringAccount.ProviderAccountId)
                    .ToListAsync();

                Console.Error.WriteLine($"Found {credentials.Count} credentials for this account");

                var refreshTokenCred = credentials.FirstOrDefault(c => c.CredentialType == "RefreshToken");

                if (refreshTokenCred == null)
                {
                    Console.Error.WriteLine("No RefreshToken credential found");
                    return null;
                }

                Console.Error.WriteLine($"Found RefreshToken credential, attempting decryption...");

                // Decrypt the refresh token
                var decrypted = await DecryptCredentialAsync(refreshTokenCred.EncryptedValue);
                if (string.IsNullOrEmpty(decrypted))
                {
                    Console.Error.WriteLine("Failed to decrypt RefreshToken");
                    return null;
                }

                Console.Error.WriteLine($"Successfully loaded credentials from database: {ringAccount.AccountEmail}");
                return new ResolvedCredentials(
                    ringAccount.AccountEmail,
                    null,
                    decrypted,
                    $"database:{dbPath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading credentials from database: {ex.Message}");
                Console.Error.WriteLine($"Stack: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Decrypts a credential value using the configured encryption provider.
        /// </summary>
        private static async Task<string?> DecryptCredentialAsync(string encryptedValue)
        {
            try
            {
                // Try AES decryption (cross-platform)
                var aesEncryption = new AesEncryption();
                var decrypted = aesEncryption.Decrypt(encryptedValue);
                if (!string.IsNullOrEmpty(decrypted))
                {
                    return decrypted;
                }

                // Try DPAPI if AES fails (Windows-only)
                if (OperatingSystem.IsWindows())
                {
                    var dpapi = new WindowsDpapiEncryption();
                    decrypted = dpapi.Decrypt(encryptedValue);
                    if (!string.IsNullOrEmpty(decrypted))
                    {
                        return decrypted;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
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
        /// Interactive one-time login: prompts for credentials, authenticates via
        /// Ring.Api.InteractiveAuth (handling a 2FA challenge if one comes back), and
        /// saves the result to the shared credentials file via CredentialStore. This is the console
        /// I/O half of that flow - InteractiveAuth itself knows nothing about Console, so the same
        /// authenticate-with-2FA-retry logic is reusable by anything else (tests included) without
        /// going through this executable.
        /// </summary>
        private static async Task<int> RunInteractiveAuthAsync(CliOptions options)
        {
            var credentialStore = new CredentialStore();
            Console.WriteLine("Ring interactive login - saves a reusable refresh token so future runs");
            Console.WriteLine($"don't need this again. Credentials are written to:\n  {CredentialResolver.AuthPath}\n");

            var userName = options.UserName;
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

            var password = options.Password;
            if (string.IsNullOrWhiteSpace(password))
            {
                password = ReadPassword("Ring password: ");
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                Console.Error.WriteLine("Error: a password is required.");
                return 2;
            }

            Session session;
            try
            {
                session = await InteractiveAuth.AuthenticateAsync(userName, password, async () =>
                {
                    Console.WriteLine();
                    Console.WriteLine("Two-factor authentication is enabled on this account - Ring just sent a code via text/e-mail.");
                    Console.Write("Enter the code: ");
                    await Task.CompletedTask;
                    return Console.ReadLine() ?? "";
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: authentication failed: {ex.Message}");
                return 2;
            }

            if (session.OAuthToken?.RefreshToken == null)
            {
                Console.Error.WriteLine("Error: authentication reported success but no refresh token was returned - nothing was saved.");
                return 2;
            }

            credentialStore.Save(CredentialResolver.AuthPath, new RingCredentials
            {
                UserName = userName,
                Password = password,
                RefreshToken = session.OAuthToken.RefreshToken
            });

            Console.WriteLine();
            Console.WriteLine($"Authenticated and saved credentials to {CredentialResolver.AuthPath}.");
            Console.WriteLine("Future SelfTester runs will use this automatically.");
            return 0;
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

            var password = "";
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
            foreach (var e in EndpointRegistry.All)
            {
                var tags = string.Concat(e.Destructive ? " [destructive]" : "", e.Physical ? " [physical]" : "");
                Console.WriteLine($"  {e.Key,-22} {e.DisplayName}{tags}");
                Console.WriteLine($"  {"",22} {e.SessionMethod} -> {e.HttpMethod} {e.ApiPath}");
                Console.WriteLine($"  {"",22} {e.Description}");
                Console.WriteLine();
            }
        }
    }
}

