using System.CommandLine;

namespace VideoForensics.Utils.SelfTest
{
    /// <summary>
    /// Parsed command-line options using simple manual parsing.
    /// Provides type validation and reduced boilerplate compared to raw args[].
    /// </summary>
    internal sealed class CliOptions
    {
        /// <summary>Default SQLite path: the app's Database storage root plus videoforensics.db.
        /// Uses the same provider as the app so the default cannot drift.</summary>
        internal static string DefaultDatabasePath()
        {
            return Path.Combine(
            new VideoForensics.Core.Helpers.Platform.StorageLocationProvider()
                .GetDefaultRoot(VideoForensics.Core.Helpers.Platform.StorageCategory.Database),
            "videoforensics.db");
        }

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
        public string? CheckDir;
        public bool ListUnknown;
        public bool StripRawPayload;
        public string? DumpFile;
        public bool Yes;
        public Uri? ClientsApiBaseUrl;
        public Uri? AppApiBaseUrl;
        public Uri? ModeApiBaseUrl;
        public Uri? MonitoringApiBaseUrl;
        public Uri? DevicesApiBaseUrl;
        public Uri? CommandsApiBaseUrl;
        public Uri? GroupsApiBaseUrl;
        public Uri? EvmApiBaseUrl;
        public Uri? IntegrationsApiBaseUrl;
        public Uri? RecordingsApiBaseUrl;
        public Uri? AppSnapsApiBaseUrl;

        /// <summary>
        /// Maps the host-override flags to a <see cref="VideoForensics.Providers.Ring.RingEndpointOptions"/>,
        /// changing only the overridden properties. Returns null when no override was given so callers keep
        /// the production defaults.
        /// </summary>
        public VideoForensics.Providers.Ring.RingEndpointOptions? ToEndpointOptions()
        {
            if (ClientsApiBaseUrl is null && AppApiBaseUrl is null && ModeApiBaseUrl is null && MonitoringApiBaseUrl is null
                && DevicesApiBaseUrl is null
                && CommandsApiBaseUrl is null
                && GroupsApiBaseUrl is null
                && EvmApiBaseUrl is null
                && IntegrationsApiBaseUrl is null
                && RecordingsApiBaseUrl is null
                && AppSnapsApiBaseUrl is null)
            {
                return null;
            }

            var endpoints = new VideoForensics.Providers.Ring.RingEndpointOptions();
            if (ClientsApiBaseUrl is not null)
            {
                endpoints.ClientsApiBaseUrl = ClientsApiBaseUrl;
            }

            if (AppApiBaseUrl is not null)
            {
                endpoints.AppApiBaseUrl = AppApiBaseUrl;
            }

            if (ModeApiBaseUrl is not null)
            {
                endpoints.ModeApiBaseUrl = ModeApiBaseUrl;
            }

            if (MonitoringApiBaseUrl is not null)
            {
                endpoints.MonitoringApiBaseUrl = MonitoringApiBaseUrl;
            }

            if (DevicesApiBaseUrl is not null)
            {
                endpoints.DevicesApiBaseUrl = DevicesApiBaseUrl;
            }

            if (CommandsApiBaseUrl is not null)
            {
                endpoints.CommandsApiBaseUrl = CommandsApiBaseUrl;
            }

            if (GroupsApiBaseUrl is not null)
            {
                endpoints.GroupsApiBaseUrl = GroupsApiBaseUrl;
            }

            if (EvmApiBaseUrl is not null)
            {
                endpoints.EvmApiBaseUrl = EvmApiBaseUrl;
            }

            if (IntegrationsApiBaseUrl is not null)
            {
                endpoints.IntegrationsApiBaseUrl = IntegrationsApiBaseUrl;
            }

            if (RecordingsApiBaseUrl is not null)
            {
                endpoints.RecordingsApiBaseUrl = RecordingsApiBaseUrl;
            }

            if (AppSnapsApiBaseUrl is not null)
            {
                endpoints.AppSnapsApiBaseUrl = AppSnapsApiBaseUrl;
            }

            return endpoints;
        }

        private static bool TryParseHttpUrl(string value, out Uri? uri)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed)
                && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
            {
                uri = parsed;
                return true;
            }

            uri = null;
            return false;
        }

        /// <summary>
        /// Parses the selftest command line with System.CommandLine. Each option's custom parser applies its value to the
        /// returned options and reports the same error text the hand-written parser used, so Program's exit-code rules and
        /// the characterization tests still hold. A bare word that is not an option is ignored, as before.
        /// </summary>
        public static (CliOptions? options, string? error) Parse(string[] args)
        {
            var options = new CliOptions();
            var words = new Argument<string[]>("words") { Arity = ArgumentArity.ZeroOrMore, Hidden = true };
            RootCommand root = BuildRoot(options, words);
            ParseResult result = root.Parse(args);

            // A dashed word that matched no option is unrecognized. Other bare words are ignored, as they always were.
            foreach (string word in result.GetValue(words) ?? [])
            {
                if (word.StartsWith('-'))
                {
                    return (null, $"Unrecognized argument: {word}");
                }
            }

            // -h and --help are the library's own help option; a help token on the command line is a help request.
            if (result.Tokens.Any(t => t.Value is "-h" or "--help"))
            {
                options.ShowHelp = true;
            }

            if (result.Errors.Count > 0)
            {
                return (null, TranslateError(result.Errors[0].Message));
            }

            if (options.Endpoints.Count == 0)
            {
                options.Endpoints.Add("all");
            }

            return (options, null);
        }

        /// <summary>Turns a System.CommandLine missing-argument message into the selftest's own wording.</summary>
        private static string TranslateError(string message)
        {
            const string MissingPrefix = "Required argument missing for option: '";
            if (message.StartsWith(MissingPrefix, StringComparison.Ordinal))
            {
                int start = MissingPrefix.Length;
                int end = message.IndexOf('\'', start);
                if (end > start)
                {
                    return $"Missing value for {message[start..end]}";
                }
            }

            // A value option given twice. The library reports it as "Option '--x' expects a single argument ...".
            const string RepeatPrefix = "Option '";
            const string RepeatSuffix = "' expects a single argument";
            if (message.StartsWith(RepeatPrefix, StringComparison.Ordinal) && message.Contains(RepeatSuffix, StringComparison.Ordinal))
            {
                int start = RepeatPrefix.Length;
                int end = message.IndexOf('\'', start);
                if (end > start)
                {
                    return $"{message[start..end]} may be given only once";
                }
            }

            return message;
        }

        private static RootCommand BuildRoot(CliOptions model, Argument<string[]> words)
        {
            var root = new RootCommand("Ring API SelfTester - API validation and smoke testing");
            root.Arguments.Add(words);

            AddFlag(root, model, ["--list", "--list-endpoints"], o => o.ListEndpoints = true);
            AddFlag(root, model, ["--list-endpoints-json"], o => { o.ListEndpoints = true; o.ListEndpointsJson = true; });
            AddFlag(root, model, ["--auth"], o => o.InteractiveAuth = true);
            AddFlag(root, model, ["--all"], o => o.Endpoints.Add("all"));
            AddFlag(root, model, ["--destructive"], o => o.Destructive = true);
            AddFlag(root, model, ["--no-physical"], o => o.NoPhysical = true);
            AddFlag(root, model, ["--quiet"], o => o.Quiet = true);
            AddFlag(root, model, ["--verify-db"], o => o.VerifyDb = true);
            AddFlag(root, model, ["--list-unknown"], o => o.ListUnknown = true);
            AddFlag(root, model, ["--strip-raw-payload"], o => o.StripRawPayload = true);
            AddFlag(root, model, ["--yes"], o => o.Yes = true);

            AddValue(root, model, "--endpoints", (o, v) =>
            {
                o.Endpoints.AddRange(v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                return null;
            });
            AddValue(root, model, "--output-dir", (o, v) => { o.OutputDir = v; return null; });
            AddValue(root, model, "--location-id", (o, v) =>
            {
                if (!Guid.TryParse(v, out Guid id))
                {
                    return $"--location-id value '{v}' is not a valid GUID";
                }

                o.LocationId = id;
                return null;
            });
            AddValue(root, model, "--doorbot-id", (o, v) =>
            {
                if (!long.TryParse(v, out long id))
                {
                    return $"--doorbot-id value '{v}' is not a valid integer";
                }

                o.DoorbotId = id;
                return null;
            });
            AddValue(root, model, "--chime-id", (o, v) =>
            {
                if (!long.TryParse(v, out long id))
                {
                    return $"--chime-id value '{v}' is not a valid integer";
                }

                o.ChimeId = id;
                return null;
            });
            AddValue(root, model, "--history-limit", (o, v) =>
            {
                if (!int.TryParse(v, out int n) || n <= 0)
                {
                    return $"--history-limit value '{v}' must be a positive integer";
                }

                o.HistoryLimit = n;
                return null;
            });
            AddValue(root, model, "--siren-duration-seconds", (o, v) =>
            {
                if (!int.TryParse(v, out int n) || n <= 0)
                {
                    return $"--siren-duration-seconds value '{v}' must be a positive integer";
                }

                o.SirenDurationSeconds = n;
                return null;
            });
            AddValue(root, model, "--volume-level", (o, v) =>
            {
                if (!int.TryParse(v, out int n) || n < 0)
                {
                    return $"--volume-level value '{v}' must be a non-negative integer";
                }

                o.VolumeLevel = n;
                return null;
            });
            AddValue(root, model, "--chime-type-value", (o, v) =>
            {
                if (!int.TryParse(v, out int n) || n is < 0 or > 2)
                {
                    return $"--chime-type-value value '{v}' must be 0, 1 or 2";
                }

                o.ChimeTypeValue = n;
                return null;
            });
            AddValue(root, model, "--dnd-seconds", (o, v) =>
            {
                if (!int.TryParse(v, out int n) || n <= 0)
                {
                    return $"--dnd-seconds value '{v}' must be a positive integer";
                }

                o.DndSeconds = n;
                return null;
            });
            AddValue(root, model, "--location-mode-value", (o, v) =>
            {
                if (v is not ("home" or "away" or "disarmed"))
                {
                    return $"--location-mode-value value '{v}' must be one of: home, away, disarmed";
                }

                o.LocationModeValue = v;
                return null;
            });
            AddValue(root, model, "--ding-id", (o, v) => { o.DingId = v; return null; });
            AddValue(root, model, "--asset-uuid", (o, v) => { o.AssetUuid = v; return null; });
            AddValue(root, model, "--push-token", (o, v) => { o.PushToken = v; return null; });
            AddValue(root, model, "--username", (o, v) => { o.UserName = v; return null; });
            AddValue(root, model, "--password", (o, v) => { o.Password = v; return null; });
            AddValue(root, model, "--refresh-token", (o, v) => { o.RefreshToken = v; return null; });
            AddValue(root, model, "--check-dir", (o, v) => { o.CheckDir = v; return null; });
            AddValue(root, model, "--dump-file", (o, v) => { o.DumpFile = v; return null; });
            AddValue(root, model, "--db-path", (o, v) => { o.DbPath = v; return null; });

            AddUrl(root, model, "--clients-api-base-url", (o, u) => o.ClientsApiBaseUrl = u);
            AddUrl(root, model, "--app-api-base-url", (o, u) => o.AppApiBaseUrl = u);
            AddUrl(root, model, "--mode-api-base-url", (o, u) => o.ModeApiBaseUrl = u);
            AddUrl(root, model, "--monitoring-api-base-url", (o, u) => o.MonitoringApiBaseUrl = u);
            AddUrl(root, model, "--devices-api-base-url", (o, u) => o.DevicesApiBaseUrl = u);
            AddUrl(root, model, "--commands-api-base-url", (o, u) => o.CommandsApiBaseUrl = u);
            AddUrl(root, model, "--groups-api-base-url", (o, u) => o.GroupsApiBaseUrl = u);
            AddUrl(root, model, "--evm-api-base-url", (o, u) => o.EvmApiBaseUrl = u);
            AddUrl(root, model, "--integrations-api-base-url", (o, u) => o.IntegrationsApiBaseUrl = u);
            AddUrl(root, model, "--recordings-api-base-url", (o, u) => o.RecordingsApiBaseUrl = u);
            AddUrl(root, model, "--app-snaps-api-base-url", (o, u) => o.AppSnapsApiBaseUrl = u);

            return root;
        }

        private static void AddFlag(RootCommand root, CliOptions model, string[] names, Action<CliOptions> apply)
        {
            var option = new Option<bool>(names[0]) { Arity = ArgumentArity.Zero };
            for (int i = 1; i < names.Length; i++)
            {
                option.Aliases.Add(names[i]);
            }

            option.CustomParser = _ =>
            {
                apply(model);
                return true;
            };
            root.Options.Add(option);
        }

        /// <summary>Registers a value option. <paramref name="apply"/> returns the error text, or null when the value was accepted.</summary>
        private static void AddValue(RootCommand root, CliOptions model, string name, Func<CliOptions, string, string?> apply)
        {
            // A collection-typed option accepts the same option more than once, as the hand-written parser did.
            var option = new Option<string[]>(name) { Arity = ArgumentArity.ExactlyOne };
            option.CustomParser = result =>
            {
                string token = result.Tokens[0].Value;
                string? error = apply(model, token);
                if (error is not null)
                {
                    result.AddError(error);
                }

                return [token];
            };
            root.Options.Add(option);
        }

        private static void AddUrl(RootCommand root, CliOptions model, string name, Action<CliOptions, Uri> set)
        {
            AddValue(root, model, name, (o, v) =>
            {
                if (!TryParseHttpUrl(v, out Uri? uri))
                {
                    return $"{name} value '{v}' is not a valid absolute URL";
                }

                set(o, uri!);
                return null;
            });
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
          Ring.Api.SelfTester --list-unknown [--db-path <path>] [--output-dir <path>]
          Ring.Api.SelfTester --strip-raw-payload --db-path <file> --dump-file <ndjson> --yes
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
                                     got persisted locally. Also checks every leaf path of the run's
                                     saved responses against the explicit path map
                                     src/utils/selftest/src/field-map.json (endpoint:json.path ->
                                     Table.Column, Rows:Table or NotData:reason; array indexes are
                                     written []). Failed calls are skipped. Reports (a) unmapped paths
                                     per endpoint, (b) stale map entries whose Table.Column is not in
                                     the EF model (or is a *Json column), (c) endpoints that returned
                                     data but have no map entry and (d) paths that were null in every
                                     sample (type unverified). Writes db-completeness.json alongside
                                     index.json; does not affect the exit code (a device/location
                                     that simply hasn't been downloaded yet is expected, not a
                                     failure - this is a completeness report, not a pass/fail gate).
          --check-dir <path>        Run only that check, offline, on a saved run directory: no Ring
                                     calls, no credentials. Reads the saved devices/locations files
                                     for the presence check and every response file for the path
                                     check. Uses --db-path (or the default database). Writes
                                     db-completeness.json into --output-dir if given, else into
                                     <path>. Exit code 0 unless the directory or database is missing.
          --db-path <path>          SQLite database file to use. Default:
                                     %ProgramData%\VideoForensics\Database\videoforensics.db (same file the
                                     main VideoForensics app uses). Applies to credential storage and
                                     --verify-db alike.

        STRIP RAW PAYLOAD (opt-in maintenance, never automatic):
          --strip-raw-payload       Rewrites Events.MetadataJson and MediaItems.MetadataJson so a raw Ring event payload
                                     keeps only its keys with no structured home (null when none). App markers such as
                                     {"type":"snapshot"} are left as they are. Needs all three: --db-path <file>,
                                     --dump-file <existing non-empty ndjson of the current raw payloads> and --yes.
                                     Copies the database to <name>.pre-strip-<utc time>.db first, then updates in one
                                     transaction. Prints counts only. Stop the app first.

        UNKNOWN KEY LIST:
          --list-unknown            Read-only and offline: no Ring calls, no credentials. Lists every leaf
                                     JSON path held in the Ring-data JSON columns (Devices, DeviceCapabilities,
                                     Events, MediaItems, LocationMetadata, RingAccounts, DeviceConfigSnapshots,
                                     RingLocationModeNotYetParticipating) with its row count and value kinds.
                                     Values are never printed or written. ActionLogEntries.DetailsJson is
                                     skipped by design. Events and MediaItems paths are marked covered when
                                     field-map.json has the same path under any endpoint. Writes
                                     unknown-keys.json and unknown-keys.md into --output-dir (default: a
                                     UTC-timestamped folder under SelfTesterResults). Uses --db-path (or the
                                     default database), opened read-only. Exit 2 if the database is missing.

        HOST OVERRIDES (probe a different Ring host without a code change; each takes an absolute
        http/https URL, unset flags keep the production defaults):
          --clients-api-base-url <url>      Override the Ring clients API base URL.
          --app-api-base-url <url>          Override the Ring app API base URL.
          --mode-api-base-url <url>         Override the Ring Location Mode API base URL.
          --monitoring-api-base-url <url>   Override the Ring Alarm monitoring accounts API base URL.
          --devices-api-base-url <url>      Override the Ring devices API base URL.
          --commands-api-base-url <url>     Override the Ring commands API base URL.
          --groups-api-base-url <url>       Override the Ring groups API base URL.
          --evm-api-base-url <url>          Override the Ring event-manager history API base URL.
          --integrations-api-base-url <url> Override the Ring integrations API base URL.
          --recordings-api-base-url <url>   Override the Ring recordings API base URL.
          --app-snaps-api-base-url <url>    Override the Ring app snapshots API base URL.

        CREDENTIALS (first match wins):
          The first active provider account.s saved refresh token, restored via RingAuthService
          (the same database-backed auth code the WebApp and legacy console app use). If no account
          is active, falls back to the most-recently-authenticated Ring account in the database.
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
