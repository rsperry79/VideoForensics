---
paths:
  - ".github/workflows/**"
  - "version.json"
  - "deploy/**"
---

# CI, build, test, and release (required)

Before editing any workflow, read `.claude/skills/ci-release/SKILL.md`. Its rules are required:

- The release version comes from NBGV (`version.json`). Do not hand-create version tags. The workflow creates them.
- Secrets: `SYNCFUSION_LICENSE_KEY`, `ANTHROPIC_API_KEY`, `GITHUB_TOKEN`, from `secrets.*` only.
- A workflow change is verified by a PR run in CI, not by a local build alone.
