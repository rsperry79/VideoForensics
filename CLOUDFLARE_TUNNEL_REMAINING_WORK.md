	# Cloudflare Tunnel Redesign — Remaining Work

	The Cloudflare tunnel redesign has been partially implemented using CloudflaredKit and encrypted token storage. The following work remains before the redesign is complete.

	## 1. Replace the Network Settings Cloudflare UI

	Update `src/client/ui/VideoForensics.Ui.Shared/src/Pages/NetworkSettings.razor`.

	- Remove the obsolete quick-tunnel controls.
	- Remove Cloudflare CLI login controls.
	- Remove named-tunnel creation and selection controls based on local CLI state.
	- Remove cloudflared installation/path status controls.
	- Add a hideable Cloudflare setup card or popover.
	- Present one setup step at a time:
	  1. Enter the Cloudflare Account API token.
	  2. Validate the token and load available zones/domains.
	  3. Enter the hostname prefix.
	  4. Select the domain from the loaded domain dropdown.
	  5. Enter the permanent Cloudflare tunnel token.
	  6. Display the resulting FQDN and save the configuration.
	  7. Start the permanent tunnel.
	- Place the hostname input immediately before the domain dropdown so the resulting FQDN is clear to the user:
	  - `hostname-prefix` + `.` + `selected-domain`
	- Show the connected/configured state without displaying either token.
	- Allow the wizard to be reopened after it has been hidden.
	- Display clear validation and API errors.
	- Keep the surrounding network-tier and passkey settings intact.

	## 2. Reconcile Remote Access UI Calls with the New API

	Verify that all calls from `NetworkSettings.razor` use the new routes in `RemoteAccessEndpoints`:

	- `GET /api/remote-access/status`
	- `POST /api/remote-access/token`
	- `GET /api/remote-access/zones`
	- `POST /api/remote-access/tunnel`
	- `POST /api/remote-access/start`
	- `POST /api/remote-access/stop`

	Remove references to the deleted routes:

	- `/login`
	- `/named-tunnels`
	- `/named-tunnel/create`
	- `/named-tunnel/route-dns`
	- `/quick-tunnel/start`
	- `/named-tunnel/start`

	## 3. Complete Cloudflare Tunnel Configuration

	The current API stores the tunnel token and FQDN and starts CloudflaredKit. Verify whether the setup flow also needs to create or configure the Cloudflare tunnel through the Cloudflare API.

	If tunnel creation is required, add API operations for:

	- Creating a tunnel under the selected Cloudflare account.
	- Configuring the tunnel ingress to the local application.
	- Creating or updating the DNS CNAME record for the selected FQDN.
	- Returning and securely storing the generated Cloudflared tunnel token.

	The Cloudflare Account API token and the Cloudflared tunnel token are separate credentials and must remain separately encrypted.

	## 4. Persist and Apply the FQDN Configuration

	Verify that saving `InternetServerUrl` is sufficient for all server components.

	- Confirm that the configured FQDN is used by `WebAuthnOriginPolicy`.
	- Confirm that `ServerAddressResolver` uses the configured hostname as the WebAuthn RP ID where appropriate.
	- Confirm that the configured origin is included in the Fido2 allowed-origin set.
	- Confirm that paired clients receive the configured public URL.
	- Add or update tests for the final configured FQDN.

	## 5. Remove Obsolete Cloudflared Configuration

	Search the complete repository and remove remaining legacy cloudflared configuration where it is no longer needed.

	- Remove `CloudflaredPath` from `IForensicsConfiguration` and its implementation if no consumers remain.
	- Remove cloudflared path configuration endpoints and UI.
	- Remove obsolete cloudflared setup code from `SoftwarePathsEndpoints`.
	- Remove obsolete CLI-only tests and replace them with CloudflaredKit adapter tests.
	- Remove the `CliWrap` package from `VideoForensics.Hosting.csproj` after confirming no other hosting code uses it.
	- Remove any installer references that bundle, install, or configure cloudflared.
	- Keep only explanatory installer text that directs users to configure the tunnel after installation, if still useful.

	## 6. Handle CloudflaredKit Runtime Configuration Carefully

	Review the current CloudflaredKit adapter.

	- Confirm that runtime tunnel-token configuration is supported by the installed CloudflaredKit version.
	- Confirm that only permanent-token mode is used.
	- Ensure no code path starts a TryCloudflare quick tunnel.
	- Handle unexpected tunnel exits and expose a useful failure state.
	- Ensure `StopAsync` does not raise a stale unexpected-exit notification.
	- Consider whether the token must be configured before service initialization rather than through the options monitor cache.
	- Add logging that never writes API tokens or tunnel tokens.

	## 7. Improve API Security and Validation

	Review the new remote-access endpoints.

	- Keep all endpoints protected by the existing SuperAdmin authorization policy.
	- Keep tunnel start protected by the step-up authentication filter.
	- Validate hostname labels and domains consistently.
	- Reject domains that were not returned by the validated Cloudflare API token.
	- Avoid returning secrets in response bodies, logs, exceptions, or audit records.
	- Add audit events for token configuration, FQDN changes, tunnel start, and tunnel stop.
	- Consider clearing previously stored tokens when the user explicitly disconnects Cloudflare.

	## 8. Add Automated Tests

	Add or update tests for:

	- `CloudflareTokenStore`
	  - API token encryption and retrieval
	  - Tunnel token encryption and retrieval
	  - Clearing both tokens
	  - Empty-token rejection
	- `CloudflareApiClient`
	  - Successful zone response
	  - Invalid-token response
	  - Cloudflare API error response
	  - Empty or malformed zone data
	- `CloudflaredTunnelService`
	  - Permanent tunnel start
	  - Duplicate start behavior
	  - Stop behavior
	  - Failed start state
	  - Cancellation behavior
	- Remote-access endpoints
	  - Token validation
	  - Zone listing
	  - FQDN construction
	  - Invalid hostname/domain rejection
	  - Network-tier restrictions
	  - Step-up requirement for tunnel start
	- WebAuthn configuration
	  - Final configured FQDN is accepted as an origin
	  - Final FQDN is used for RP ID resolution where applicable

	## 9. Build and Runtime Verification

	After the UI and cleanup are complete:

	- Stop the active debug process before validating files that are locked by Visual Studio.
	- Restore packages.
	- Build the affected Hosting and WebApp projects.
	- Run the relevant unit tests.
	- Run the WebAuthn origin/RP-ID regression tests.
	- Verify the wizard manually in the Blazor application.
	- Verify that the domain dropdown is populated from Cloudflare zones.
	- Verify that the constructed FQDN is saved correctly.
	- Verify that the permanent tunnel starts without exposing either token.
	- Verify that stopping the tunnel updates the UI state.
	- Verify that a tunnel process failure displays a useful error and does not silently leave the UI in a running state.

	## Current Known Validation Note

	The hosting project built successfully in a fresh compiler process. The WebApp build also completed, but Visual Studio reported file-lock warnings because the application was still running under the debugger. A clean build should be performed after stopping the debug session.
