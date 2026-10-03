namespace VideoForensics.Providers.Ring.SelfTester
{
    /// <summary>
    /// Parsed command-line options using simple manual parsing.
    /// Provides type validation and reduced boilerplate compared to raw args[].
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
            var options = new CliOptions();
            var i = 0;

            while (i < args.Length)
            {
                var arg = args[i];

                switch (arg)
                {
                    case "-h" or "--help":
                        options.ShowHelp = true;
                        break;

                    case "--list" or "--list-endpoints":
                        options.ListEndpoints = true;
                        break;

                    case "--list-endpoints-json":
                        options.ListEndpoints = true;
                        options.ListEndpointsJson = true;
                        break;

                    case "--auth":
                        options.InteractiveAuth = true;
                        break;

                    case "--all":
                        options.Endpoints.Add("all");
                        break;

                    case "--endpoints":
                        if (i + 1 >= args.Length) return (null, "Missing value for --endpoints");
                        var endpoints = args[++i];
                        options.Endpoints.AddRange(endpoints.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        break;

                    case "--output-dir":
                        if (i + 1 >= args.Length) return (null, "Missing value for --output-dir");
                        options.OutputDir = args[++i];
                        break;

                    case "--location-id":
                        if (i + 1 >= args.Length) return (null, "Missing value for --location-id");
                        if (!Guid.TryParse(args[++i], out var locGuid))
                            return (null, $"--location-id value '{args[i]}' is not a valid GUID");
                        options.LocationId = locGuid;
                        break;

                    case "--doorbot-id":
                        if (i + 1 >= args.Length) return (null, "Missing value for --doorbot-id");
                        if (!long.TryParse(args[++i], out var dbId))
                            return (null, $"--doorbot-id value '{args[i]}' is not a valid integer");
                        options.DoorbotId = dbId;
                        break;

                    case "--chime-id":
                        if (i + 1 >= args.Length) return (null, "Missing value for --chime-id");
                        if (!long.TryParse(args[++i], out var chId))
                            return (null, $"--chime-id value '{args[i]}' is not a valid integer");
                        options.ChimeId = chId;
                        break;

                    case "--history-limit":
                        if (i + 1 >= args.Length) return (null, "Missing value for --history-limit");
                        if (!int.TryParse(args[++i], out var hl) || hl <= 0)
                            return (null, $"--history-limit value '{args[i]}' must be a positive integer");
                        options.HistoryLimit = hl;
                        break;

                    case "--destructive":
                        options.Destructive = true;
                        break;

                    case "--no-physical":
                        options.NoPhysical = true;
                        break;

                    case "--siren-duration-seconds":
                        if (i + 1 >= args.Length) return (null, "Missing value for --siren-duration-seconds");
                        if (!int.TryParse(args[++i], out var sd) || sd <= 0)
                            return (null, $"--siren-duration-seconds value '{args[i]}' must be a positive integer");
                        options.SirenDurationSeconds = sd;
                        break;

                    case "--volume-level":
                        if (i + 1 >= args.Length) return (null, "Missing value for --volume-level");
                        if (!int.TryParse(args[++i], out var vol) || vol < 0)
                            return (null, $"--volume-level value '{args[i]}' must be a non-negative integer");
                        options.VolumeLevel = vol;
                        break;

                    case "--chime-type-value":
                        if (i + 1 >= args.Length) return (null, "Missing value for --chime-type-value");
                        if (!int.TryParse(args[++i], out var ct) || ct is < 0 or > 2)
                            return (null, $"--chime-type-value value '{args[i]}' must be 0, 1 or 2");
                        options.ChimeTypeValue = ct;
                        break;

                    case "--dnd-seconds":
                        if (i + 1 >= args.Length) return (null, "Missing value for --dnd-seconds");
                        if (!int.TryParse(args[++i], out var dnd) || dnd <= 0)
                            return (null, $"--dnd-seconds value '{args[i]}' must be a positive integer");
                        options.DndSeconds = dnd;
                        break;

                    case "--location-mode-value":
                        if (i + 1 >= args.Length) return (null, "Missing value for --location-mode-value");
                        var locationMode = args[++i];
                        if (locationMode is not ("home" or "away" or "disarmed"))
                            return (null, $"--location-mode-value value '{locationMode}' must be one of: home, away, disarmed");
                        options.LocationModeValue = locationMode;
                        break;

                    case "--ding-id":
                        if (i + 1 >= args.Length) return (null, "Missing value for --ding-id");
                        options.DingId = args[++i];
                        break;

                    case "--asset-uuid":
                        if (i + 1 >= args.Length) return (null, "Missing value for --asset-uuid");
                        options.AssetUuid = args[++i];
                        break;

                    case "--push-token":
                        if (i + 1 >= args.Length) return (null, "Missing value for --push-token");
                        options.PushToken = args[++i];
                        break;

                    case "--username":
                        if (i + 1 >= args.Length) return (null, "Missing value for --username");
                        options.UserName = args[++i];
                        break;

                    case "--password":
                        if (i + 1 >= args.Length) return (null, "Missing value for --password");
                        options.Password = args[++i];
                        break;

                    case "--refresh-token":
                        if (i + 1 >= args.Length) return (null, "Missing value for --refresh-token");
                        options.RefreshToken = args[++i];
                        break;

                    case "--quiet":
                        options.Quiet = true;
                        break;

                    case "--verify-db":
                        options.VerifyDb = true;
                        break;

                    case "--db-path":
                        if (i + 1 >= args.Length) return (null, "Missing value for --db-path");
                        options.DbPath = args[++i];
                        break;

                    default:
                        if (arg.StartsWith("--") || arg.StartsWith("-"))
                            return (null, $"Unrecognized argument: {arg}");
                        break;
                }

                i++;
            }

            // Default to "all" if no endpoints specified
            if (options.Endpoints.Count == 0)
            {
                options.Endpoints.Add("all");
            }

            return (options, null);
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
