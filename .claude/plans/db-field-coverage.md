# Plan: every Ring API field in a structured DB column

Rule: a value kept only in a `*Json` column is NOT in the DB. Every response field needs a typed column or a child row. Generic concepts go in provider-agnostic tables; Ring-only concepts go in `Ring*` tables, preferably 1:1 child tables, not more columns on `Devices` (already 77 columns).

Reviewed by a Sonnet agent; its corrections are folded in below.

## Done
- Selftester ignores `*Json` columns and reports unmapped response fields per endpoint (name matcher, see Batch 0).
- `account-monitoring-status`: `RingAccountMonitoringStatus` + `RingAccountMonitoringPlan` (merged to `wip`).
- Device `features`: `DeviceFeatureState`, `DeviceFeatureReason`, 20 `DeviceFeatures` columns (merged to `wip`).

## In flight
- Batch B (branch `feature/db-device-fields`, uncommitted): generic `DeviceReportedHealth` + `DeviceReportedBattery`, Ring-only `RingDeviceHealthExtras`, top-level device scalars, alerts, led/siren seconds, logging of unstored values.
  - Correction before it merges: it added ~13 columns to `Devices`. Keep on `Devices` only provider-agnostic ones (`DeactivatedAtUtc`, `SharedAtUtc`, time zone). Move Ring-only ones (`SchemaId`, `DeviceResourceId`, `RingNetId`, `ActiveScheduleUuid`, `FlickElimRecommendedMode`, `ExtPowerState`, `EncryptionGroupId`, `AccountGroupRevisionId`, `IsSidewalkGateway`) to a 1:1 `RingDeviceDetails`.
  - `snooze_settings` / `motion_snooze`: model as rows or typed columns, not a text column that drops objects. Ask before adding any fallback shim (CLAUDE.md backwards-compat rule).
  - Decide the source of truth between `DeviceReportedHealth` (devices response) and the time-series `DeviceHealths` (health endpoint); they overlap on battery, rssi, firmware. Default: `DeviceHealths` stays the time series, `DeviceReportedHealth` is the latest devices-response state; document it in the entity comments.

## Rules for every batch
- Failing test first (record the red run), sanitized fixture, client model keeps unknown keys, parser/mapper, persister replace-on-sync (null object leaves rows untouched), EF config, `dotnet ef migrations add`.
- Before `migrations add`: merge the latest `wip`. If the snapshot conflicts, delete the new migration and Designer, restore the snapshot from `wip`, regenerate. Never hand-merge the snapshot.
- Lite gate: build touched projects, run the full provider, database, sqlite migration and Ring common test projects. Then commit, merge to `wip`, push `origin wip`.
- Types for fields null in every sample are guesses: use nullable string unless the shape is evident, and log the first non-null value (Info, device name not GUID, message and exception type only, no stack trace; Error level on a parser exception) so the guess can be fixed.
- No new `*Json` column or `MetadataJson` write. If a client model change breaks the Ring API tester or MAUI, update CLAUDE.md and the commit message.
- Existing `*Json` columns on `Devices` (`MetadataJson`, `SettingsMetadataJson`, `MotionSnoozeJson`, `LightSettingsMetadataJson`) conflict with the providers rule. Each batch that stores a group's keys in rows must stop writing that group to the JSON column; final cleanup drops the columns.

## Order
1. **Batch B** merged with the corrections above.
2. **Rename** `DeviceFeatureState`/`DeviceFeatureReason` to `RingDeviceFeatureState`/`RingDeviceFeatureReason` (EF may scaffold drop/create; hand-edit the migration to `RenameTable` + `RenameIndex`; this is the one allowed migration hand-edit, not the snapshot). Do it before C so later batches don't reference the old names. Add the features-parser logging here.
3. **Batch 0: replace the checker** (before C, so C-I are measured correctly).
   - Explicit `endpoint JSON path -> table.column` map, checked in as a fixture; match by path, not leaf name.
   - Include arrays of scalars; skip failed-call error bodies (`signalling-ticket` `error`); report endpoints that returned data but have no map entry.
   - Re-baseline from a fresh sync on a migrated copy of the live DB. The saved baseline (428) predates the merged batches and counts false positives: fields already stored (`detection_types.*` in `DeviceDetectionTypes`, `lu`/`md` in `RingLocationModes`, `advanced_pir_motion_zones` vs `PirSettingsSensitivity1-3`, `time_zone` vs `TimeZoneId`, `address1/zip_code` vs `LocationMetadata`).
   - Report three counts: unmapped paths, value mismatches, null-in-sample paths (type unverified).
4. **Batch C** device settings scalars: map by path from the new baseline (88 unique leaf names under `settings`; do not sum group counts, they overlap). Generic columns where provider-agnostic, Ring-only in `RingDeviceSettings` (1:1).
5. **Batch D** `RingDevicePaidFeatures` (1:1): `cv_paid_features` (15) + `other_paid_features` (16), prefixes `CvPaid*`/`OtherPaid*`. One table also serves `device-settings` `backend_settings.paid_features` (~45 flags).
6. **Batch E** `DeviceNetworkSettings` (generic, 6) + `RingDeviceNetworkDiagnosis` (10).
7. **Batch F** `device-settings` endpoint: not flat; 26 top-level groups, 146 distinct leaf names, 55 shared with `devices.settings`, 91 device-settings-only (e.g. `rda_enabled`, `protocol`, `retry_interval`, `host`, `port`, `wrapup_domain`, `zone_mask`, `utc_offset`, `clip_length_min/max`, `lite_24x7_*`, `ome_resolution_p`, `ptz_setup_complete`, `sensitivity_1..3`, `rogers_vqa`). Map by path per group; zones already map to `DeviceAdvancedMotionZones` + vertices.
8. **Batch G** `locations`: renames of existing columns (`address1`/`StreetAddress`, `zip_code`/`PostalCode`, `timezone`/`TimeZoneId`, `cross_street`) go in `LocationMetadata` (generic). Ring-only (`guard_patrol`, `business_command_center`, `vm_*`, `user_verified`, `geo_service_verified`, `is_jobsite`, `location_type`, `location_resource_id`, `private_guard_response`) go in `RingLocationDetails`.
9. **Batch H/I** events and small endpoints: most are already stored (`RingEventCv*`, `RingEventDevicePlacements`, `Geolocations`, `LastLocations`, `Sirens`; several empty only because samples are empty). First map every key to its existing table; add columns only for real gaps: `ding_id_str`, `key_moments`, `map_visualization`, `radar_visualization`, `potential_gap_visualization`, `objects_per_lens`, `custom_metadata`, `cek_transfer_recipient_response`, `session_duration`, `timestamp`, `call_alerts`, `motion_alerts`, `device_groups`, `permissions`, `verified`, `object_type`.

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
