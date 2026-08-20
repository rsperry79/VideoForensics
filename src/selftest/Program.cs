using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using KoenZomers.Ring.Api;
using KoenZomers.Ring.Api.Utils;

namespace Ring.Api.SelfTester
{
    internal static class Program
    {
        private static readonly ICredentialStore credentialStore = new CredentialStore();

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

            var credentials = CredentialResolver.Resolve(options.RefreshToken, options.UserName, options.Password);
            if (credentials == null)
            {
                WriteNoCredentialsError();
                return 2;
            }

            Session session;
            try
            {
                session = await AuthenticateAsync(credentials);
                PersistRotatedRefreshToken(session, credentials);
            }
            catch (KoenZomers.Ring.Api.Exceptions.TwoFactorAuthenticationRequiredException)
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
                index = await runner.RunAsync(runOptions, credentials.Source);
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

            return index.Summary.Failed > 0 ? 1 : 0;
        }

        private static async Task<Session> AuthenticateAsync(ResolvedCredentials credentials)
        {
            if (!string.IsNullOrWhiteSpace(credentials.RefreshToken))
            {
                return await Session.GetSessionByRefreshToken(credentials.RefreshToken);
            }

            var session = new Session(credentials.UserName!, credentials.Password!);
            await session.Authenticate();
            return session;
        }

        /// <summary>
        /// Ring rotates the refresh token on every use - the one that authenticated this run is
        /// already spent. Without saving the new one back, the shared credentials file goes stale
        /// after exactly one successful run and every run after that fails until --auth is redone.
        /// Merges into whatever's already on disk (preferring what this run actually used) rather
        /// than overwriting wholesale, so a run authenticated via --refresh-token/env var doesn't
        /// blow away a stored username/password.
        /// </summary>
        private static void PersistRotatedRefreshToken(Session session, ResolvedCredentials usedThisRun)
        {
            var newRefreshToken = session.OAuthToken?.RefreshToken;
            if (string.IsNullOrEmpty(newRefreshToken))
            {
                return;
            }

            var existing = credentialStore.Load(CredentialResolver.AuthPath);
            if (existing.RefreshToken == newRefreshToken)
            {
                return;
            }

            credentialStore.Save(CredentialResolver.AuthPath, new RingCredentials
            {
                UserName = usedThisRun.UserName ?? existing.UserName,
                Password = usedThisRun.Password ?? existing.Password,
                RefreshToken = newRefreshToken
            });
        }

        private const string ReadmePointer = "See external/Ring.Api/README.md (\"Authenticating for local tooling\") for details.";

        private static void WriteNoCredentialsError()
        {
            Console.Error.WriteLine("Error: no credentials found. Run 'dotnet run -- --auth' to authenticate interactively and");
            Console.Error.WriteLine("save a reusable refresh token (handles two-factor accounts too), or provide");
            Console.Error.WriteLine("--username/--password or --refresh-token.");
            Console.Error.WriteLine(ReadmePointer);
        }

        /// <summary>
        /// Interactive one-time login: prompts for credentials, authenticates via
        /// KoenZomers.Ring.Api.InteractiveAuth (handling a 2FA challenge if one comes back), and
        /// saves the result to the shared credentials file via CredentialStore. This is the console
        /// I/O half of that flow - InteractiveAuth itself knows nothing about Console, so the same
        /// authenticate-with-2FA-retry logic is reusable by anything else (tests included) without
        /// going through this executable.
        /// </summary>
        private static async Task<int> RunInteractiveAuthAsync(CliOptions options)
        {
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
