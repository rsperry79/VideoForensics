# Plan: every Ring API field in a structured DB column

Rule: a value kept only in a `*Json` column is NOT in the DB. Every response field needs a typed column or a child row. Generic concepts go in provider-agnostic tables; Ring-only concepts go in `Ring*` tables, preferably 1:1 child tables, not more columns on `Devices` (already 77 columns).

Reviewed by a Sonnet agent; its corrections are folded in below.

## Done (all merged to `wip`)
- Selftester ignores `*Json` columns and reports unmapped response fields per endpoint (name matcher, see Batch 0).
- `account-monitoring-status`: `RingAccountMonitoringStatus` + `RingAccountMonitoringPlan`.
- Device `features`: `RingDeviceFeatureState`, `RingDeviceFeatureReason` (renamed with a data-preserving `RenameTable` migration, plus unstored-value logging in the features parser; null values are not logged), 20 `DeviceFeatures` columns.
- Batch B: generic `DeviceReportedHealth` + `DeviceReportedBattery`, Ring-only `RingDeviceHealthExtras` and `RingDeviceDetails` (1:1, replaces the Ring-only columns first added to `Devices`), alerts connection row, `RingUnstoredValueLog` (Info, one line per device per sync: key + JSON type, never the value).
- Health units: `StatusAtUtc` (`status_time` is a 32768 Hz tick count since the Unix epoch; verified against `last_update_time`) and `BatteryTempCelsius` (`battery_temp` assumed deci-Kelvin) beside the raw values; factors live in `RingHealthUnits`; out-of-range values store null and are logged.
- Light events: `DeviceLightEvent` (`IsOn`, `Source` Observed|Command, `SecondsRemaining`); `led_status` object and plain `'on'`/`'off'` strings handled; `Devices.LedStatus` and `Devices.LedSecondsRemaining` dropped. Consumers listing light-on events filter `IsOn = true`.

## Status (2026-10-31, `wip` b4bf8f0f)
Checker on the saved 2026-10-31 run: 136 unmapped paths (from 665), all on `devices` (zones, presets, snooze). Every other endpoint is at 0 unmapped. In flight: branch `feature/db-zones` (doorbot zones/presets/zone_settings, new `RingDeviceIgnoreZone*` tables, `advanced_motion_zones_*` columns, array paths; `snooze_settings`/`motion_snooze` stay `NotData` because they are null in every sample). Next: drop the `*Json` columns on `Devices` (needs a decision, see below), the checker value comparison, then verification on a migrated DB copy. Subagent cost: about 210-390k tokens per batch.

## Open decisions and follow-ups
- **2026-11-01 check (also in `plans/todo.md`):** confirm the `battery_temp` unit by comparing stored readings with hourly ambient temperature (deci-Kelvin moves ~10 raw units per °C, hundredths of °C ~100). On 2026-10-30 02:56 UTC the Front Door read 2921 with ambient 5.6 °C (42 °F): deci-Kelvin gives 66 °F, hundredths of °C 85 °F. If the slope says otherwise, change `RingHealthUnits` only.
- `wifi_name` (SSID) is stored in `RingDeviceHealthExtras`; personal-ish data, kept because every field is wanted. Revisit if exports or reports should redact it.
- Light-event limits: a quick on, off, on inside one reported seconds window inserts no second event; Command events never count as "on" (a command followed by a sync that sees the light on gives two rows); `SetLight` returns no duration.
- Existing `*Json` columns on `Devices` (`MetadataJson`, `SettingsMetadataJson`, `MotionSnoozeJson`, `LightSettingsMetadataJson`) and `RingLocationModeNotYetParticipating.ValueJson` still exist and are still written. `SettingsMetadataJson` is the remainder for settings keys with no typed home, i.e. a fallback for unknown keys: DECISION NEEDED before dropping (CLAUDE.md asks before fallback code): drop it and rely on the unstored-value log, or keep unknown keys somewhere typed.
- `GetSharedUsers`, `GetLinkedChimeDoorbots`, `GetDoorbotSnapshotTimestamp` are called only by the selftester; the new tables, mapper and writer methods have no sync caller. DECISION NEEDED: add them to sync or leave the tables unused.
- The map covers `doorbot-health` / `chime-health` through the devices-response health object, but sync never calls those endpoints.
- No SQLite round-trip test exists for the small-endpoint writer methods; a wrong type in the location-metadata path is dropped without a log note.
- Snapshot timestamp unit is unknown (stored as sent).
- `backend_settings.paid_features` duplicates the concept in `RingDevicePaidFeatures`; `RingDeviceGeneralSettings` sits beside `DeviceGeneralSettings`; rename or merge later.
- Flaky test: `CaseRepositoryNumberingTests.CreateAsync_ConcurrentParallelCreatesAllSucceed` fails intermittently in full `Data.Database.Tests` runs and passes alone; not caused by these batches, not yet proven on a clean `wip`. Fix or quarantine it separately.

## Rules for every batch
- Failing test first (record the red run), sanitized fixture, client model keeps unknown keys, parser/mapper, persister replace-on-sync (null object leaves rows untouched), EF config, `dotnet ef migrations add`.
- Before `migrations add`: merge the latest `wip`. If the snapshot conflicts, delete the new migration and Designer, restore the snapshot from `wip`, regenerate. Never hand-merge the snapshot.
- When two unmerged branches each add a migration: merge the first into the second, delete the second's migration and Designer, restore the snapshot from the FIRST branch's commit (not the merged one; a snapshot that already contains the second model makes `migrations add` produce an empty migration), `dotnet build`, then regenerate. `dotnet ef migrations remove` does not delete the files reliably. Verify with `InitialCreate_Schema_MatchesCurrentModel` and `dotnet ef migrations has-pending-model-changes`.
- Run MTP test projects with `dotnet test --project <csproj>` (global.json requires the Microsoft Testing Platform); include Data.Core tests whenever `VideoForensicsDataClient` or `DeviceDetailFields` change.
- Lite gate (a new entity name must also pass `Ui.Shared.Tests` and a `VideoForensics.WebApp` build; a name collision with a Razor page broke Ui.Shared tests through two merges): build touched projects, run the full provider, database, sqlite migration and Ring common test projects. Then commit, merge to `wip`, push `origin wip`.
- Types for fields null in every sample are guesses: use nullable string unless the shape is evident, and log the first non-null value (Info, device name not GUID, message and exception type only, no stack trace; Error level on a parser exception) so the guess can be fixed.
- No new `*Json` column or `MetadataJson` write. If a client model change breaks the Ring API tester or MAUI, update CLAUDE.md and the commit message.
- Existing `*Json` columns on `Devices` (`MetadataJson`, `SettingsMetadataJson`, `MotionSnoozeJson`, `LightSettingsMetadataJson`) conflict with the providers rule. Each batch that stores a group's keys in rows must stop writing that group to the JSON column; final cleanup drops the columns.

## Order
1. ~~Batch B~~, health units and light events: done.
2. ~~**Rename**~~ done: `DeviceFeatureState`/`DeviceFeatureReason` to `RingDeviceFeatureState`/`RingDeviceFeatureReason` (EF may scaffold drop/create; hand-edit the migration to `RenameTable` + `RenameIndex`; this is the one allowed migration hand-edit, not the snapshot). Do it before C so later batches don't reference the old names. Extend `RingUnstoredValueLog` coverage to the features parser here (unknown scalar keys directly under `features`, nested shapes that are skipped, non-scalar reasons, and first non-null values for guessed-type columns).
3. ~~**Batch 0: replace the checker**~~ done. `src/utils/selftest/src/field-map.json` maps `endpoint:path` to `Table.Column`, `Rows:Table` or `NotData:reason` (1386 entries; `*` and trailing `**` wildcards); `--check-dir <path>` runs it offline; a test project (45 tests) guards the map against EF renames. Value comparison against DB rows is still a stub (`IFieldValueVerifier`). Baseline on the 2026-10-31 run: 665 unmapped paths, 0 stale, 218 null-in-sample. By batch: C 233 (71 unique leaf names), D 105 (43), E 32 (16), F 209 (130), G 18, H/I 68 (doorbot-health 23, chime-health 15, shared-users 10, location-history 7, linked-chime-doorbots 5, other 8). Fully mapped: `profile`, `ringtones`, `light-groups`, `location-mode-settings`, `location-mode-sharing`, `account-monitoring-status`. Five endpoints return data but have no map entries and nothing persists them: `doorbot-health`, `chime-health`, `shared-users`, `linked-chime-doorbots`, `snapshot-timestamps`. `device-settings` cannot tell doorbot from stickup cam (its map is the union). Each later batch adds its map entries as it persists a group.
   Checker groups for the `devices` endpoint (unmapped paths per group, 2026-10-31 run; two device kinds so most counts are doubled): `settings` scalars 55; `cv_paid_features` 30 and `other_paid_features` 32 (batch D); `network_settings` 12 + `network_diagnosis` 20 (batch E); `advanced_pir_motion_zones` 12; `lite_24x7` 8; `video_settings` 8; `server_settings` 4; `sheila_settings` 4; `cv_settings.search_types.natural_language_search` 4; `alerts` 2; `audio_settings`, `concierge_settings.*`, `floodlight_settings`, `motion_settings`, `zone_settings`, `cv_settings` 1-2 each; top-level 8. Zone groups (`advanced_motion_zones.zoneN.vertexM`, `ignore_zones.zoneN`, `zone_settings.motion[]`) are unmapped although `DeviceAdvancedMotionZone*` / `DeviceZone*` tables exist: first check whether the persister fills them from these paths (then it is a map gap, add entries) or only from `device-settings` (then it is a real gap).
4. ~~**Batch C**~~ done (`RingDeviceSettings` + `DeviceGeneralSettings`, `alerts.ota_status` as a `DeviceAlerts` row; 571 -> 474). Doorbot settings now fill the nine `Devices` setting columns like stickups, and the nine duplicate `RingDeviceSettings` columns were dropped (`DropDuplicateDeviceSettingColumns`). Not persisted yet, still unmapped on `devices` (about 136 paths): doorbot `advanced_motion_zones`, stickup zones 4+, `ignore_zones.*`, `zone_settings.*`, `motion_zones`, `advanced_motion_zones_enabled/type`, doorbot `live_view_presets[]` / `motion_snooze_presets[]`, `snooze_settings`, `motion_snooze`.
5. ~~**Batch D**~~ done (`RingDevicePaidFeatures`, 31 columns; 665 -> 571 unmapped with E): `RingDevicePaidFeatures` (1:1): `cv_paid_features` (15) + `other_paid_features` (16), prefixes `CvPaid*`/`OtherPaid*`. One table also serves `device-settings` `backend_settings.paid_features` (~45 flags).
6. ~~**Batch E**~~ done (`DeviceNetworkSettings`, `RingDeviceNetworkDiagnosis`; diagnosis column types are guesses, all null in samples): `DeviceNetworkSettings` (generic, 6) + `RingDeviceNetworkDiagnosis` (10).
7. ~~**Batch F**~~ done: 252 `device-settings` paths mapped (474 -> 222): 232 new typed columns in 14 tables (`RingDeviceAdvancedMotionSettings`, `RingDeviceBackendSettings`, `RingDeviceVideoSettings`, `RingDeviceMotionSettings`, ... plus snooze-profile and threshold-override child rows), 12 map-only, 5 empty objects as `NotData`. Open: `backend_settings.paid_features` duplicates the concept in `RingDevicePaidFeatures` (different source keys); `RingDeviceGeneralSettings` sits beside `DeviceGeneralSettings` with a different meaning (rename one).
8. ~~**Batch G**~~ done with H/I: `RingLocationDetails` (13 Ring-only keys), `LocationMetadata` +`StreetAddress2`/`CrossStreet` (country/lat/lon now filled).
9. ~~**Batch H/I**~~ done (222 -> 136): new `RingLocationSharedUsers` (+devices, +permissions), `RingChimeLinkedDoorbot`, `RingSnapshotTimestamp`, key-moments / objects-per-lens / visualization / cek columns, extra health columns; `doorbot-health` / `chime-health` mapped to the same targets as the devices-response health object; `notYetParticipatingInMode[]` and `events[]` as `NotData`.

## Working rules (cost and process)
- Subagents: `haiku` for mechanical batches whose spec is complete (tables, columns, parser entries, map entries, tests from a given list). `sonnet` only for design, migration-sensitive or review work, with the criterion named before spawning. Verify every result yourself.
- One agent at a time per shared file set; one `dotnet` build or test at a time; no full-suite runs while another build is active.
- Keep large JSON and file dumps out of the main thread (use scripts that print counts).
- Every batch updates `field-map.json` for the paths it persists and shows the unmapped count dropping.

## Known gaps
- Only one doorbot, one stickup cam and one chime exist in the samples; `authorized_doorbots`, `base_stations`, `beams`, `beams_bridges` and `other` are empty. Other device kinds are covered only by the unknown-key logging.
- `recording-url`, `ding-recording`, `ding-share-play` (need `--ding-id`) and the empty-array endpoints (`active-dings`, `amazon-key-locks`, `app-location-history`) have no sample data; rerun with a ding id and an account that returns data.
- Endpoints with no unmapped fields (`chime-health`, `ringtones`, `location-mode-settings`, `location-mode-sharing`) need the value check, not just the name check.

## Verification (proves "in the DB, not JSON")
1. Work on a copy of the live DB; run the app against the copy so migrations apply; never mutate the live baseline.
2. Unit test: every leaf path in each sanitized endpoint fixture, including empty arrays and arrays of scalars, has a map entry or an explicit "not data" reason.
3. Integration test: sync the sanitized fixture into a fresh SQLite DB with `*Json` columns blanked, read back every mapped column, assert equality with the JSON leaf (type-aware).
4. `--verify-db` repeats the value comparison against the real DB (three counts above). Done means unmapped = 0 and mismatches = 0.
5. Guard test: no new `*Json` column / `MetadataJson` write.

## Risks
- Wide `Devices` row: use 1:1 child tables.
- Client model changes (`Doorbot`/`StickupCam`/`Chime`): check Ring API tester and MAUI compile.
- Migrations are additive and strictly sequential (single snapshot): one batch at a time.
