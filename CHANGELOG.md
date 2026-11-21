# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Media review (`/media`): the tabs, time range, camera selector and pending tags moved from the page header to the right sidebar, the left navigation collapses on the page, and the vertical timeline fills its column, follows playback, shows the time under the pointer and seeks when a drag ends. The Filters button is gone. `?tab=` links still work.

### Removed

- Unused Evidence view code (the device time grid, snapshot strip, RSSI plot, evidence loader and view model, time-lapse coverage helper) and the unlinked `/media/dates-filter` page, with their tests.

### Added

- GitHub Actions CI pipeline running build and test on every pull request and push to `main`, `dev`, and `wip` branches.
- Automated versioning via Nerdbank.GitVersioning, encoding branch/tag information into build numbers and assembly versions.
- Three-tier branch strategy: feature branches land on `wip`, `wip` is PR'd into `dev` for integration testing, and `dev` is PR'd into `main` for release.
- Stable and Testing release channels: Stable releases are tagged (`vX.Y.Z`) off the `main` branch; Testing is a rolling prerelease at the `testing` tag, rebuilt on every push to `dev`.
- Update-check feature allowing users to manually check for available updates or automatically download and install them, with configurable notify-only or auto-install behavior per-installation.
- Thin installer bootstrap scripts (`deploy/install.ps1` for Windows, `deploy/install.sh` for Debian/Ubuntu) that fetch the current release for a chosen channel at runtime, enabling single-command installation.
- CodeQL static analysis scanning for security vulnerabilities, run on every pull request.
- Dependabot automated dependency management and security update pull requests.
- Dependency Review action enforcing license and security policy on new dependencies.
- Comprehensive documentation: `README.md` describing the project and installation, `CHANGELOG.md` (this file) tracking version history, `LICENSE` file stating proprietary status, and `CREDITS.md` acknowledging third-party components.
- Flagged-media review for Signal and Bit Rate reports: a flag table, review pages that play each flagged clip, keep and dismiss actions with a reason, adding a clip or the RSSI window to a case, clip navigation showing the flag's position in the list, and a playback timeline with a marker for each flag. Bit Rate flags are backfilled for the previous 7 days.
