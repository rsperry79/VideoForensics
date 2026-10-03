using System.CommandLine;
using System.CommandLine.Parsing;

namespace VideoForensics.Providers.Ring.SelfTester
{
    /// <summary>
    /// Parsed command-line options using System.CommandLine for modern, standardized CLI argument parsing.
    /// Provides type validation, built-in help text generation, and reduced boilerplate compared to manual parsing.
    /// </summary>
    internal sealed class CliOptions
    {
        public bool ShowHelp;
        public bool ListEndpoints;
        public bool ListEndpointsJson;
        public bool InteractiveAuth;
        public List<string> Endpoints { get; } = [];
        public string? OutputDir;
        public Guid? LocationId;
        public long? DoorbotId;
        public long? ChimeId;
        public int HistoryLimit = 5;
        public bool Destructive;
        public bool NoPhysical;
        public int SirenDurationSeconds = 2;
        public int? VolumeLevel;
        public int? ChimeTypeValue;
        public int DndSeconds = 60;
        public string? LocationModeValue;
        public string? DingId;
        public string? AssetUuid;
        public string? PushToken;
        public string? UserName;
        public string? Password;
        public string? RefreshToken;
        public bool Quiet;
        public bool VerifyDb;
        public string? DbPath;

        public static (CliOptions? options, string? error) Parse(string[] args)
        {
            var rootCommand = new RootCommand("Ring API SelfTester - API validation and smoke testing");

            // Flags (boolean options)
            var helpOption = new Option<bool>(new[] { "-h", "--help" }, "Show help text");
            var listOption = new Option<bool>(new[] { "--list", "--list-endpoints" }, "List available endpoints and exit");
            var listJsonOption = new Option<bool>("--list-endpoints-json", "List endpoints as JSON and exit");
            var authOption = new Option<bool>("--auth", "Interactive one-time login");
            var allOption = new Option<bool>("--all", "Run all non-destructive endpoints");
            var destructiveOption = new Option<bool>("--destructive", "Include destructive endpoints");
            var noPhysicalOption = new Option<bool>("--no-physical", "Exclude physical endpoints when --destructive is set");
            var quietOption = new Option<bool>("--quiet", "Suppress narration output");
            var verifyDbOption = new Option<bool>("--verify-db", "Verify database completeness after run");

            // Options with values
            var endpointsOption = new Option<string?>("--endpoints", "Comma-separated endpoint keys to run");
            var outputDirOption = new Option<string?>("--output-dir", "Output directory for results");
            var locationIdOption = new Option<string?>("--location-id", "Location ID (GUID) for location-scoped endpoints");
            var doorbotIdOption = new Option<string?>("--doorbot-id", "Doorbot ID for doorbot-scoped endpoints");
            var chimeIdOption = new Option<string?>("--chime-id", "Chime ID for chime-scoped endpoints");
            var historyLimitOption = new Option<string?>("--history-limit", "Max history items to request (default: 5)");
            var sirenDurationOption = new Option<string?>("--siren-duration-seconds", "Siren duration in seconds (default: 2)");
            var volumeLevelOption = new Option<string?>("--volume-level", "Volume level (0-11)");
            var chimeTypeOption = new Option<string?>("--chime-type-value", "Chime type value (0=Mechanical, 1=Digital, 2=Not Present)");
            var dndSecondsOption = new Option<string?>("--dnd-seconds", "Do-not-disturb duration in seconds (default: 60)");
            var locationModeOption = new Option<string?>("--location-mode-value", "Location mode: home, away, or disarmed");
            var dingIdOption = new Option<string?>("--ding-id", "Ding ID for recording sharing");
            var assetUuidOption = new Option<string?>("--asset-uuid", "Asset UUID for alarm triggering");
            var pushTokenOption = new Option<string?>("--push-token", "Push notification token");
            var usernameOption = new Option<string?>("--username", "Ring username/email");
            var passwordOption = new Option<string?>("--password", "Ring password");
            var refreshTokenOption = new Option<string?>("--refresh-token", "OAuth refresh token");
            var dbPathOption = new Option<string?>("--db-path", "SQLite database path");

            rootCommand.AddOption(helpOption);
            rootCommand.AddOption(listOption);
            rootCommand.AddOption(listJsonOption);
            rootCommand.AddOption(authOption);
            rootCommand.AddOption(allOption);
            rootCommand.AddOption(endpointsOption);
            rootCommand.AddOption(outputDirOption);
            rootCommand.AddOption(locationIdOption);
            rootCommand.AddOption(doorbotIdOption);
            rootCommand.AddOption(chimeIdOption);
            rootCommand.AddOption(historyLimitOption);
            rootCommand.AddOption(destructiveOption);
            rootCommand.AddOption(noPhysicalOption);
            rootCommand.AddOption(sirenDurationOption);
            rootCommand.AddOption(volumeLevelOption);
            rootCommand.AddOption(chimeTypeOption);
            rootCommand.AddOption(dndSecondsOption);
            rootCommand.AddOption(locationModeOption);
            rootCommand.AddOption(dingIdOption);
            rootCommand.AddOption(assetUuidOption);
            rootCommand.AddOption(pushTokenOption);
            rootCommand.AddOption(usernameOption);
            rootCommand.AddOption(passwordOption);
            rootCommand.AddOption(refreshTokenOption);
            rootCommand.AddOption(quietOption);
            rootCommand.AddOption(verifyDbOption);
            rootCommand.AddOption(dbPathOption);

            CliOptions? parsedOptions = null;
            string? parseError = null;

            rootCommand.SetHandler(
                (help, list, listJson, auth, all, endpoints, outputDir, locationId, doorbotId, chimeId,
                 historyLimit, destructive, noPhysical, sirenDuration, volumeLevel, chimeType, dndSeconds,
                 locationMode, dingId, assetUuid, pushToken, username, password, refreshToken, quiet, verifyDb, dbPath) =>
                {
                    var o = new CliOptions();
                    o.ShowHelp = help;
                    o.ListEndpoints = list;
                    o.ListEndpointsJson = listJson;
                    o.InteractiveAuth = auth;

                    // Parse endpoints
                    if (all)
                    {
                        o.Endpoints.Add("all");
                    }
                    else if (!string.IsNullOrEmpty(endpoints))
                    {
                        o.Endpoints.AddRange(endpoints.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    }

                    o.OutputDir = outputDir;

                    // Parse LocationId
                    if (!string.IsNullOrEmpty(locationId))
                    {
                        if (!Guid.TryParse(locationId, out Guid locGuid))
                        {
                            parseError = $"--location-id value '{locationId}' is not a valid GUID";
                            return;
                        }
                        o.LocationId = locGuid;
                    }

                    // Parse DoorbotId
                    if (!string.IsNullOrEmpty(doorbotId))
                    {
                        if (!long.TryParse(doorbotId, out long dbId))
                        {
                            parseError = $"--doorbot-id value '{doorbotId}' is not a valid integer";
                            return;
                        }
                        o.DoorbotId = dbId;
                    }

                    // Parse ChimeId
                    if (!string.IsNullOrEmpty(chimeId))
                    {
                        if (!long.TryParse(chimeId, out long chId))
                        {
                            parseError = $"--chime-id value '{chimeId}' is not a valid integer";
                            return;
                        }
                        o.ChimeId = chId;
                    }

                    // Parse HistoryLimit
                    if (!string.IsNullOrEmpty(historyLimit))
                    {
                        if (!int.TryParse(historyLimit, out int hl) || hl <= 0)
                        {
                            parseError = $"--history-limit value '{historyLimit}' must be a positive integer";
                            return;
                        }
                        o.HistoryLimit = hl;
                    }

                    o.Destructive = destructive;
                    o.NoPhysical = noPhysical;

                    // Parse SirenDurationSeconds
                    if (!string.IsNullOrEmpty(sirenDuration))
                    {
                        if (!int.TryParse(sirenDuration, out int sd) || sd <= 0)
                        {
                            parseError = $"--siren-duration-seconds value '{sirenDuration}' must be a positive integer";
                            return;
                        }
                        o.SirenDurationSeconds = sd;
                    }

                    // Parse VolumeLevel
                    if (!string.IsNullOrEmpty(volumeLevel))
                    {
                        if (!int.TryParse(volumeLevel, out int vol) || vol < 0)
                        {
                            parseError = $"--volume-level value '{volumeLevel}' must be a non-negative integer";
                            return;
                        }
                        o.VolumeLevel = vol;
                    }

                    // Parse ChimeTypeValue
                    if (!string.IsNullOrEmpty(chimeType))
                    {
                        if (!int.TryParse(chimeType, out int ct) || ct is < 0 or > 2)
                        {
                            parseError = $"--chime-type-value value '{chimeType}' must be 0, 1 or 2";
                            return;
                        }
                        o.ChimeTypeValue = ct;
                    }

                    // Parse DndSeconds
                    if (!string.IsNullOrEmpty(dndSeconds))
                    {
                        if (!int.TryParse(dndSeconds, out int dnd) || dnd <= 0)
                        {
                            parseError = $"--dnd-seconds value '{dndSeconds}' must be a positive integer";
                            return;
                        }
                        o.DndSeconds = dnd;
                    }

                    // Parse LocationModeValue
                    if (!string.IsNullOrEmpty(locationMode))
                    {
                        if (locationMode is not ("home" or "away" or "disarmed"))
                        {
                            parseError = $"--location-mode-value value '{locationMode}' must be one of: home, away, disarmed";
                            return;
                        }
                        o.LocationModeValue = locationMode;
                    }

                    o.DingId = dingId;
                    o.AssetUuid = assetUuid;
                    o.PushToken = pushToken;
                    o.UserName = username;
                    o.Password = password;
                    o.RefreshToken = refreshToken;
                    o.Quiet = quiet;
                    o.VerifyDb = verifyDb;
                    o.DbPath = dbPath;

                    // Default to "all" if no endpoints specified
                    if (o.Endpoints.Count == 0)
                    {
                        o.Endpoints.Add("all");
                    }

                    parsedOptions = o;
                },
                helpOption, listOption, listJsonOption, authOption, allOption, endpointsOption, outputDirOption,
                locationIdOption, doorbotIdOption, chimeIdOption, historyLimitOption, destructiveOption, noPhysicalOption,
                sirenDurationOption, volumeLevelOption, chimeTypeOption, dndSecondsOption, locationModeOption,
                dingIdOption, assetUuidOption, pushTokenOption, usernameOption, passwordOption, refreshTokenOption,
                quietOption, verifyDbOption, dbPathOption);

            try
            {
                var parseResult = rootCommand.Parse(args);

                // Check for parse errors
                if (parseResult.Errors.Count > 0)
                {
                    parseError = parseResult.Errors[0].Message;
                }

                // Invoke the handler
                if (parseError == null)
                {
                    parseResult.Invoke();
                }
            }
            catch (Exception ex)
            {
                parseError = ex.Message;
            }

            if (parseError != null)
            {
                return (null, parseError);
            }

            return (parsedOptions, null);
        }

        public const string HelpText = """
        Ring API SelfTester - API validation and smoke testing

        Calls Ring API endpoints through the Ring.Ring.Api client, writes each raw HTTP
        response to its own file, and writes an index.json describing every call made (function
        called, HTTP method + path, target ids, status code, and a relative link to the result
        file). Intended to be run by an AI agent to detect when the live Ring API has drifted from
        what the client expects.

        By default only non-destructive (read-only) endpoints run. Endpoints that mutate account or
        device state require --destructive; endpoints that additionally trigger real hardware
        (light, siren, chime speaker, camera shutter) are further excluded by --no-physical.

        USAGE:
          Ring.Api.SelfTester --auth
          Ring.Api.SelfTester [--list | --list-endpoints-json] [--endpoints <csv>] [--output-dir <path>]
                     [--location-id <guid>] [--doorbot-id <id>] [--chime-id <id>]
                     [--history-limit <n>] [--destructive [--no-physical] [destructive-endpoint options]]
                     [--username <user> --password <pass> | --refresh-token <token>] [--quiet]

        DISCOVERY:
          --list                    Print the available endpoint keys and descriptions as
                                     human-readable text and exit (no auth/network required).
                                     Each entry shows whether it's destructive/physical.
          --list-endpoints-json     Same, but as JSON on stdout (for programmatic consumption).

        AUTHENTICATION SETUP:
          --auth                    Interactive one-time login: prompts for your Ring username and
                                     password (masked), handles a two-factor code challenge if your
                                     account requires one, then saves the resulting refresh token to
                                     the VideoForensics database and marks the account "active"
                                     (same as signing in through the desktop app's UI).
                                     Every other SelfTester run picks this up automatically afterward.
                                     Run this first if you see a "no credentials found" or
                                     "requires two-factor authentication" error. Ignores --endpoints and
                                     every other run option; exits immediately after saving.

        SELECTION:
          --endpoints <csv>         Comma-separated endpoint keys to run. Default: all
                                     non-destructive endpoints. Run --list to see valid keys.
                                     Naming a destructive key here requires --destructive too, or
                                     the tool exits with an error instead of silently skipping it.
          --all                     Explicitly run every non-destructive endpoint (same as the
                                     default). Combine with --destructive to also run every
                                     destructive endpoint.
          --destructive             Include endpoints that mutate account/device state. Required
                                     to run any endpoint marked [destructive] in --list. Off by
                                     default - this tool never mutates state unless you opt in.
          --no-physical             When --destructive is set, additionally exclude endpoints that
                                     trigger real hardware (light, siren, chime speaker, camera
                                     shutter) - see [physical] in --list.
          --location-id <guid>      Restrict location-scoped endpoints to this location only.
          --doorbot-id <id>         Restrict doorbot-scoped endpoints to this doorbot only.
          --chime-id <id>           Restrict chime-scoped endpoints to this chime only.
          --history-limit <n>       Max history items to request. Default: 5.

        DESTRUCTIVE ENDPOINT OPTIONS (only used by the endpoint that needs them; see --list):
          --siren-duration-seconds <n>   How long set-siren sounds for before turning back off.
                                          Default: 2.
          --volume-level <0-11>          Required by set-volume. No default - persists until
                                          changed again, so it's never guessed.
          --chime-type-value <0|1|2>     Required by set-chime-type (0=Mechanical, 1=Digital,
                                          2=Not Present). No default - persists.
          --dnd-seconds <n>               How long set-do-not-disturb snoozes a chime for.
                                          Default: 60. Self-reverts after this many seconds.
          --location-mode-value <mode>   Required by set-location-mode: home, away or disarmed.
                                          No default - this arms/disarms real security state.
          --ding-id <id>                 Required by share-recording: the doorbot history event id
                                          to create a public share link for. No default.
          --asset-uuid <uuid>             Required by trigger-alarm: the monitored asset to sound a
                                          real panic alarm for. No default - discover via
                                          account-monitoring-status.
          --push-token <token>            Required by register-push-receiver: the push notification
                                          token to register for this account. No default.

        OUTPUT:
          --output-dir <path>       Directory to write index.json and result files into.
                                     Default: %ProgramData%\VideoForensics\SelfTesterResults\<UTC-timestamp>
          --quiet                   Suppress narration; only the final index.json path is printed.

        DATABASE COMPLETENESS CHECK:
          --verify-db               After the run, fetch this account's live devices/locations
                                     from the Ring API and cross-check each one against the
                                     VideoForensics app's own SQLite database (matched by provider
                                     device/location id) - flags anything Ring reports that never
                                     got persisted locally. Writes db-completeness.json alongside
                                     index.json; does not affect the exit code (a device/location
                                     that simply hasn't been downloaded yet is expected, not a
                                     failure - this is a completeness report, not a pass/fail gate).
          --db-path <path>          SQLite database file to use. Default:
                                     %ProgramData%\VideoForensics\videoforensics.db (same file the
                                     main VideoForensics app uses). Applies to credential storage and
                                     --verify-db alike.

        CREDENTIALS (first match wins):
          The active account's saved refresh token - the same IForensicsConfiguration.ActiveProviderAccountId
          "current account" the desktop UI's account switcher sets, restored via RingAuthService
          (the same database-backed auth code the WebApp and legacy console app use). If no account
          is marked active, falls back to the most-recently-authenticated Ring account in the database.
          --username / --password   Explicit account credentials (used only if no saved account restores).
          --refresh-token           An OAuth refresh token from a prior session (used only if no saved account restores).
          RING_USERNAME / RING_PASSWORD / RING_REFRESH_TOKEN environment variables (same fallback tier).

        EXIT CODES:
          0  every requested call succeeded (or --list was used)
          1  authenticated, but one or more calls failed (includes destructive calls skipped for
             missing a required value - see each call's "error" in index.json)
          2  fatal error: bad arguments, or could not authenticate at all
        """;
    }
}

