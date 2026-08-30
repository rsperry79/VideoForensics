using System;
using System.Collections.Generic;
using System.Linq;

namespace VideoForensics.Providers.Ring.SelfTester
{
    /// <summary>
    /// Parsed command-line options. Kept as a plain hand-rolled parser (no external CLI
    /// dependency) so this tool stays trivially invokable from a shell script or an AI agent's
    /// tool-call layer without pulling in System.CommandLine.
    /// </summary>
    internal sealed class CliOptions
    {
        public bool ShowHelp;
        public bool ListEndpoints;
        public bool ListEndpointsJson;
        public bool InteractiveAuth;
        public List<string> Endpoints { get; } = new();
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
            var o = new CliOptions();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg.ToLowerInvariant())
                {
                    case "-h":
                    case "--help":
                        o.ShowHelp = true;
                        break;

                    case "--list":
                    case "--list-endpoints":
                        o.ListEndpoints = true;
                        break;

                    case "--list-endpoints-json":
                        o.ListEndpoints = true;
                        o.ListEndpointsJson = true;
                        break;

                    case "--auth":
                        o.InteractiveAuth = true;
                        break;

                    case "--endpoints":
                        if (!TryTakeValue(args, ref i, arg, out var epValue, out var epErr)) return (null, epErr);
                        o.Endpoints.AddRange(epValue!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        break;

                    case "--all":
                        o.Endpoints.Add("all");
                        break;

                    case "--output-dir":
                        if (!TryTakeValue(args, ref i, arg, out var outValue, out var outErr)) return (null, outErr);
                        o.OutputDir = outValue;
                        break;

                    case "--location-id":
                        if (!TryTakeValue(args, ref i, arg, out var locValue, out var locErr)) return (null, locErr);
                        if (!Guid.TryParse(locValue, out var locGuid)) return (null, $"--location-id value '{locValue}' is not a valid GUID");
                        o.LocationId = locGuid;
                        break;

                    case "--doorbot-id":
                        if (!TryTakeValue(args, ref i, arg, out var dbValue, out var dbErr)) return (null, dbErr);
                        if (!long.TryParse(dbValue, out var dbId)) return (null, $"--doorbot-id value '{dbValue}' is not a valid integer");
                        o.DoorbotId = dbId;
                        break;

                    case "--chime-id":
                        if (!TryTakeValue(args, ref i, arg, out var chValue, out var chErr)) return (null, chErr);
                        if (!long.TryParse(chValue, out var chId)) return (null, $"--chime-id value '{chValue}' is not a valid integer");
                        o.ChimeId = chId;
                        break;

                    case "--history-limit":
                        if (!TryTakeValue(args, ref i, arg, out var hlValue, out var hlErr)) return (null, hlErr);
                        if (!int.TryParse(hlValue, out var hl) || hl <= 0) return (null, $"--history-limit value '{hlValue}' must be a positive integer");
                        o.HistoryLimit = hl;
                        break;

                    case "--destructive":
                        o.Destructive = true;
                        break;

                    case "--no-physical":
                        o.NoPhysical = true;
                        break;

                    case "--siren-duration-seconds":
                        if (!TryTakeValue(args, ref i, arg, out var sdValue, out var sdErr)) return (null, sdErr);
                        if (!int.TryParse(sdValue, out var sd) || sd <= 0) return (null, $"--siren-duration-seconds value '{sdValue}' must be a positive integer");
                        o.SirenDurationSeconds = sd;
                        break;

                    case "--volume-level":
                        if (!TryTakeValue(args, ref i, arg, out var volValue, out var volErr)) return (null, volErr);
                        if (!int.TryParse(volValue, out var vol) || vol < 0) return (null, $"--volume-level value '{volValue}' must be a non-negative integer");
                        o.VolumeLevel = vol;
                        break;

                    case "--chime-type-value":
                        if (!TryTakeValue(args, ref i, arg, out var ctValue, out var ctErr)) return (null, ctErr);
                        if (!int.TryParse(ctValue, out var ct) || ct is < 0 or > 2) return (null, $"--chime-type-value value '{ctValue}' must be 0, 1 or 2");
                        o.ChimeTypeValue = ct;
                        break;

                    case "--dnd-seconds":
                        if (!TryTakeValue(args, ref i, arg, out var dndValue, out var dndErr)) return (null, dndErr);
                        if (!int.TryParse(dndValue, out var dnd) || dnd <= 0) return (null, $"--dnd-seconds value '{dndValue}' must be a positive integer");
                        o.DndSeconds = dnd;
                        break;

                    case "--location-mode-value":
                        if (!TryTakeValue(args, ref i, arg, out var lmValue, out var lmErr)) return (null, lmErr);
                        if (lmValue is not ("home" or "away" or "disarmed")) return (null, $"--location-mode-value value '{lmValue}' must be one of: home, away, disarmed");
                        o.LocationModeValue = lmValue;
                        break;

                    case "--ding-id":
                        if (!TryTakeValue(args, ref i, arg, out var dingValue, out var dingErr)) return (null, dingErr);
                        o.DingId = dingValue;
                        break;

                    case "--asset-uuid":
                        if (!TryTakeValue(args, ref i, arg, out var assetValue, out var assetErr)) return (null, assetErr);
                        o.AssetUuid = assetValue;
                        break;

                    case "--push-token":
                        if (!TryTakeValue(args, ref i, arg, out var pushValue, out var pushErr)) return (null, pushErr);
                        o.PushToken = pushValue;
                        break;

                    case "--username":
                        if (!TryTakeValue(args, ref i, arg, out var userValue, out var userErr)) return (null, userErr);
                        o.UserName = userValue;
                        break;

                    case "--password":
                        if (!TryTakeValue(args, ref i, arg, out var passValue, out var passErr)) return (null, passErr);
                        o.Password = passValue;
                        break;

                    case "--refresh-token":
                        if (!TryTakeValue(args, ref i, arg, out var rtValue, out var rtErr)) return (null, rtErr);
                        o.RefreshToken = rtValue;
                        break;

                    case "--quiet":
                        o.Quiet = true;
                        break;

                    case "--verify-db":
                        o.VerifyDb = true;
                        break;

                    case "--db-path":
                        if (!TryTakeValue(args, ref i, arg, out var dbPathValue, out var dbPathErr)) return (null, dbPathErr);
                        o.DbPath = dbPathValue;
                        break;

                    default:
                        return (null, $"Unrecognized argument '{arg}'. Use --help to see available switches.");
                }
            }

            if (o.Endpoints.Count == 0)
            {
                o.Endpoints.Add("all");
            }

            return (o, null);
        }

        private static bool TryTakeValue(string[] args, ref int i, string flag, out string? value, out string? error)
        {
            if (i + 1 >= args.Length)
            {
                value = null;
                error = $"Missing value for {flag}";
                return false;
            }
            i++;
            value = args[i];
            error = null;
            return true;
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
                                     the shared credentials file at %AppData%\RingVideosData\auth.json
                                     via Ring.Ring.Api's CredentialStore. Every other SelfTester
                                     run picks this up automatically afterward - see external/Ring.Api/README.md
                                     ("Authenticating for local tooling") for details. Run this
                                     first if you see a "no credentials found" or "requires
                                     two-factor authentication" error. Ignores --endpoints and every
                                     other run option; exits immediately after saving.

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
                                     Default: ./SelfTesterResults/<UTC-timestamp>
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
          --db-path <path>          SQLite database file to check against. Default:
                                     %AppData%\VideoForensics\videoforensics.db (same file the
                                     main VideoForensics app uses). Only meaningful with --verify-db.

        CREDENTIALS (first match wins):
          --username / --password   Explicit account credentials.
          --refresh-token           An OAuth refresh token from a prior session.
          RING_USERNAME / RING_PASSWORD / RING_REFRESH_TOKEN environment variables.
          Otherwise: auto-discovered from the RingVideos app's saved, encrypted credentials on this
          machine (%AppData%\RingVideosData\auth.json), if present and decryptable.

        EXIT CODES:
          0  every requested call succeeded (or --list was used)
          1  authenticated, but one or more calls failed (includes destructive calls skipped for
             missing a required value - see each call's "error" in index.json)
          2  fatal error: bad arguments, or could not authenticate at all
        """;
    }
}

