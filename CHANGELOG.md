# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- GitHub Actions CI pipeline running build and test on every pull request and push to `main` and `wip` branches.
- Automated versioning via Nerdbank.GitVersioning, encoding branch/tag information into build numbers and assembly versions.
- Stable and Dev release channels: Stable releases are tagged (`vX.Y.Z`) off the `main` branch; Dev is a rolling prerelease at the `dev` tag, rebuilt on every push to `wip`.
- Update-check feature allowing users to manually check for available updates or automatically download and install them, with configurable notify-only or auto-install behavior per-installation.
- Thin installer bootstrap scripts (`deploy/install.ps1` for Windows, `deploy/install.sh` for Debian/Ubuntu) that fetch the current release for a chosen channel at runtime, enabling single-command installation.
- CodeQL static analysis scanning for security vulnerabilities, run on every pull request.
- Dependabot automated dependency management and security update pull requests.
- Dependency Review action enforcing license and security policy on new dependencies.
- Comprehensive documentation: `README.md` describing the project and installation, `CHANGELOG.md` (this file) tracking version history, `LICENSE` file stating proprietary status, and `CREDITS.md` acknowledging third-party components.
