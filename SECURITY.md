# Security Policy

VideoForensics handles chain-of-custody evidence, camera credentials, and operator
authentication data. Security issues are taken seriously and prioritized accordingly.

## Supported Versions

| Channel | Branch | Support |
| --- | --- | --- |
| Stable (`vX.Y.Z` tags) | `main` | Security fixes always backported here first |
| Testing | `dev` | Fixes land here as part of normal development; not guaranteed hardened |
| `wip` / feature branches | `wip`, `claude/*`, etc. | No support — do not run these in production |

Only the most recent Stable release receives security fixes. If you're running an older
tagged release, update to the latest before reporting an issue that may already be fixed.

## Reporting a Vulnerability

**Do not open a public GitHub issue for security vulnerabilities.**

Report privately using one of these two channels:

1. **Preferred:** [GitHub Private Vulnerability Reporting](https://github.com/rsperry79/VideoForensics/security/advisories/new)
   (Security tab → "Report a vulnerability"). This keeps the report and any discussion
   private until a fix ships.
2. **Email:** rsperry79@gmail.com — include "VideoForensics Security" in the subject line.

Please include, as applicable:
- A description of the vulnerability and its potential impact (e.g. auth bypass,
  privilege escalation, path traversal, credential exposure, chain-of-custody tampering)
- Steps to reproduce, or a minimal proof-of-concept
- The affected version/commit and deployment configuration (Windows Service vs. Debian
  package, network tier: Local/Network/Internet)
- Whether the issue requires local machine access, network access, or is remotely
  exploitable over the internet-facing tier (Cloudflare Tunnel)

## Response Expectations

- **Acknowledgment:** within 5 business days
- **Initial assessment** (severity, affected versions): within 10 business days
- **Fix or mitigation:** timeline depends on severity — critical issues (remote,
  unauthenticated, or affecting evidence integrity/chain-of-custody) are prioritized
  for an out-of-band patch release rather than waiting for the next scheduled release

## Disclosure Policy

This project follows coordinated disclosure. Please give us a reasonable window to ship
a fix before any public disclosure. We'll credit reporters (unless you prefer to remain
anonymous) in the release notes / a GitHub Security Advisory once a fix is published.

## Scope

In scope:
- The WebApp (ASP.NET Core Blazor Server + REST API), MAUI desktop client, and MCP bridge
- Authentication/authorization (WebAuthn passkey pairing, password login, session/step-up
  tokens, role and network-tier gating)
- The Windows/Debian installers and their handling of credentials, file permissions, and
  data directories
- Provider integrations (Ring, Wyze, Uniview) as they relate to credential handling and
  data exposure within this codebase

Out of scope:
- Vulnerabilities in third-party dependencies without a demonstrated, VideoForensics-specific
  exploit path (report those upstream; we do track and apply dependency updates)
- Vulnerabilities in a provider's own cloud service (Ring/Wyze/Uniview) unrelated to how
  this app integrates with it
- Social engineering, physical access attacks, or issues requiring an already-compromised
  operator account with legitimate SuperAdmin privileges

## Automated Scanning

This repository runs GitHub CodeQL analysis on every push, and applies its automated
Copilot Autofix suggestions where applicable after review. Findings from automated
scanning are handled through the same private reporting/fix process as external reports,
not as public issues, until a fix has shipped.
