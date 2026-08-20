# Ring.Api.SelfTester

A tiny CLI that calls endpoints of the Ring API through the `KoenZomers.Ring.Api` client, saves
every raw HTTP response to disk, and writes an `index.json` describing the whole run.

By default it only calls **non-destructive** (read-only) endpoints. It never mutates account or
device state unless you explicitly pass `--destructive`.

## Why this exists

Ring's API is undocumented and unofficial. It changes shape without notice, and when it does,
`KoenZomers.Ring.Api`'s entity classes silently stop matching what the server actually returns -
fields go missing, get renamed, or new ones show up. There's no official spec to diff against, so
the only way to notice drift is to look at a real response.

SelfTester exists to make that check cheap and repeatable: run it against a live account, and it
captures exactly what Ring sent back for each endpoint - not the deserialized C# object, the raw
JSON body - next to a record of what was called and how it went. Point a human or an AI agent at
the output to see whether the client's entity classes still match reality.

## Quick start

```powershell
cd external/RingApi/src/selftest
dotnet run -- --list                 # see what it can call, no auth needed
dotnet run -- --auth                 # one-time interactive login (handles 2FA), see below
dotnet run -- --all                  # run every non-destructive endpoint, write results next to a fresh index.json
```

## Authentication

`--auth` is the normal way to get this tool (and the Ring.Api integration tests) working: it prompts
for your Ring username/password, walks through a two-factor code challenge if your account needs
one, and saves a reusable refresh token to a shared, encrypted credentials file at
`%AppData%\RingVideosData\auth.json` via `KoenZomers.Ring.Api.CredentialStore`. Every run after
that - by this tool or integration tests - picks it up automatically with no further prompts. See 
`external/Ring.Api/README.md` ("Authenticating for local tooling") for the full picture; the 2FA 
retry logic itself lives in `Api/InteractiveAuth.cs`, independent of this console app, so it's not 
tied to running SelfTester specifically.

Without `--auth`, credentials resolve in this order: `--username`/`--password` or `--refresh-token`
on the command line, then `RING_USERNAME`/`RING_PASSWORD`/`RING_REFRESH_TOKEN` environment
variables, then the same shared `auth.json`. A run with none of these available exits with a clear
error pointing back to `--auth` - it never hangs waiting for input it can't get non-interactively.
Run `dotnet run -- --help` for the full switch list.

## Destructive and physical endpoints

Two safety gates, layered:

- **`--destructive`** unlocks endpoints that mutate account or device state - volume, chime type,
  do-not-disturb, motion detection, night mode, location arm/away/disarm mode, requesting a fresh
  snapshot, and creating a shared recording link. Without this flag, `--all` runs only the
  non-destructive set (the previous, and still default, behavior), and naming a destructive key
  explicitly via `--endpoints` is a hard error rather than a silent skip.
- **`--no-physical`**, layered on top of `--destructive`, additionally excludes the endpoints that
  trigger real-world hardware: the floodlight/spotlight, the siren, a chime's speaker, and the
  camera shutter (`update-snapshot`). Use `--destructive --no-physical` to exercise
  state-mutating-but-silent endpoints (volume, chime type, DND, motion detection, night mode,
  location mode) without anything visibly or audibly happening near the device.

`--list` / `--list-endpoints-json` tag every entry so you can see which bucket it's in before
choosing. A few destructive endpoints require an extra value with no safe default (e.g.
`--volume-level`, `--location-mode-value`) - see `--help` for the full set; they're skipped
(recorded as a failed call with an explanatory error, not silently ignored) if the value is
missing. Where a safe default exists, mutating calls are written to self-revert (e.g. the light and
siren toggle on then immediately back off; do-not-disturb snoozes for a bounded duration).

```powershell
dotnet run -- --destructive --endpoints set-volume --volume-level 5 --doorbot-id 12345
dotnet run -- --destructive --no-physical --all     # every mutating-but-silent endpoint
```

## Notes for an AI agent driving this tool

- **Discover before running.** Call `--list-endpoints-json` first if you don't already know the
  endpoint keys - it's free (no auth, no network) and returns the exact set valid for
  `--endpoints`. Don't guess keys.
- **Read `index.json`, not the console output.** Every run prints the absolute path to `index.json`
  as its last line of stdout - capture that line, then read the file. It is the single source of
  truth for the run: per-call status, the exact `Session` method and API path invoked, and a
  `bodyFile` (relative to `index.json`'s own directory) pointing at the raw response for that
  call. Console narration is for a human watching live; don't parse it.
- **Check `summary` first.** `index.json`'s top-level `summary.failed` tells you immediately
  whether anything needs attention before you walk the full `calls` array.
- **A failed call still gets a record.** If a call errored, `calls[].success` is `false` and
  `calls[].error` has the exception message - there's no result file for that entry. This is the
  primary signal of API drift: an endpoint that used to work now throwing on deserialization, or
  returning an unexpected status code, means the entity class for that endpoint needs updating.
- **To evaluate whether a client entity class still matches Ring**, open the `bodyFile` for the
  endpoint in question and diff its keys against the corresponding class in `Api/Entities/`. A
  field present in the raw JSON but absent from the class (or vice versa) is drift even when the
  call itself "succeeded" (deserialization only fails loudly on type mismatches, not on
  missing/extra fields).
- **Exit codes are meaningful and safe to branch on**: `0` = every requested call succeeded (or
  `--list`/`--help` was used), `1` = authenticated fine but at least one call failed, `2` = fatal -
  bad arguments or couldn't authenticate at all. `2` means don't bother reading `index.json`; it
  wasn't written.
- **Two-factor accounts**: a normal run cannot complete an interactive 2FA challenge and will exit
  `2` with a message saying so rather than hang waiting on input. Don't attempt to work around this
  yourself - tell the user to run `dotnet run -- --auth` once (it handles 2FA interactively and
  saves a reusable refresh token), or supply `--refresh-token` explicitly if they already have one.
- **Scoped endpoints fan out automatically.** Endpoints that need a location or doorbot id (see
  `Scope` in `--list-endpoints-json`) run once per location/doorbot discovered from the account,
  unless narrowed with `--location-id`/`--doorbot-id` - expect more than one `calls` entry for
  those keys in a normal run.
- **Every run is additive-safe.** Each run writes to its own timestamped output directory by
  default (`--output-dir` to control it), so repeated runs never overwrite each other and can be
  diffed against one another over time to see when a response shape changed.
- **Never pass `--destructive` on your own initiative.** It's off by default specifically so
  drift-checking runs can't accidentally flip a light, sound a siren, or arm/disarm a location.
  Only add it when the user has explicitly asked to exercise mutating endpoints, and prefer
  `--no-physical` alongside it unless they specifically asked for hardware to visibly/audibly
  trigger.
