# Ring API host map

Ring does not document its private APIs. The map below comes from the reference client
[dgreif/ring ring-client-api](https://github.com/dgreif/ring/tree/main/packages/ring-client-api)
(`rest-client.ts`, `location.ts`, `ring-intercom.ts`) and from read-only probes run on 2026-10-16.

Code home: `src/providers/ring/core/src/RingEndpointOptions.cs`. Every base URL is overridable,
and the self-tester exposes `--<name>-base-url` flags for each.

## Hosts and what they serve

| Base URL | Serves | Evidence |
|---|---|---|
| `https://prd-api-us.prd.rings.solutions/api/v1/` | App API: location mode, monitoring, signalling tickets | Mode 200, monitoring 200, signalling 400 "No authorized devices" on a camera-only location. Same path on `api.ring.com/api/v1/` returns 404. |
| `https://api.ring.com/clients_api/` | Clients API: devices, profile, dings, locations/events, snapshots | Default. `ring_devices` and `profile` return 404 on the app host. |
| `https://api.ring.com/devices/v1/` | Device settings, locations list | Default. `devices/v1/locations` returns 404 on the app host. |
| `https://api.ring.com/commands/v1/` | Device commands (intercom unlock, device RPC) | Default. Not probed on other hosts (no read-only tester endpoint). |
| `https://api.ring.com/groups/v1/` | Light groups | Default. Not probed on other hosts (no read-only tester endpoint). |
| `https://api.ring.com/evm/v2/history/locations/` | Location history | Default. `evm/v2/history/locations/{id}` returns 404 on the app host. |
| `https://api.ring.com/integrations/` | Amazon Key lock associations | Default. `amazonkey/v2/devices/lock_associations` returns 404 on the app host. |
| `https://api.ring.com/recordings/public/` | Recordings | Default. Not probed on other hosts (no read-only tester endpoint). |
| `https://app-snaps.ring.com/` | Snapshot delivery | Default. Separate host. |

## Endpoints still to check

- `rs/history` on the app host. Not implemented. Location history currently uses `evm/v2`.
- `trigger-alarm` (`rs/monitoring/accounts/{id}/assets/{uuid}/userAlarm`). Moved to the app host
  but not probed. It is destructive and needs explicit approval before any live call.
- Groups, commands, recordings and app-snaps on the app host. Not probed. No read-only tester endpoint exists yet.

## Known dead or account-dependent endpoints

- `clients_api/locations/{id}` (location details): 404 on every account. The library defines it but never calls it.
  Removed from the self-tester.
- Alarm-only endpoints (mode, monitoring, signalling) need an alarm hub or an authorised device.
  The self-tester marks them not applicable for camera-only accounts.
- `clients_api/locations/{id}/invitations`: 404 on the test account. The reference client has no
  invitations endpoint, so its status is unknown.

## Re-running the probes

Read-only probes against the app host use the self-tester override flags, for example:

```bash
dotnet run --project src/utils/selftest/src/VideoForensics.Utils.SelfTest.csproj --no-build -- --endpoints signalling-ticket --location-id <guid> --app-api-base-url https://prd-api-us.prd.rings.solutions/api/v1/
```

Results are written to `C:\ProgramData\VideoForensics\SelfTesterResults\<timestamp>\index.json`.
