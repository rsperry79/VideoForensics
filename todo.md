# TODO

- [ ] Find the Ring app's Alerts Snooze call. The web app has no snooze, so capture the phone's traffic through a proxy, then toggle a snooze in the app and record the request. Garden is the test camera. The current API fields (`snooze_settings`, `motion_snooze`) stay null after the app snooze.
- [ ] `set-location-mode-settings`: keep refused until a write path is verified on a live location.
- [ ] Old `MetadataJson` rows: `timestamp` keys remain by design (write time, not `RecordedAtUtc`).
