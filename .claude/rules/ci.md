---
paths:
  - ".github/workflows/**"
  - "version.json"
  - "deploy/**"
---

# CI, build, test, and release (required)

Before editing any workflow, read `.claude/skills/ci-release/SKILL.md`. Its rules are required:

- Tests run in CI on every push and pull request to `dev` and `main`, after build. A failing test blocks publishing.
- Publishing (version, NuGet packages, installers, GitHub release) runs only on push to `dev` or `main`. Pull requests never publish.
- The release version comes from NBGV (`version.json`). Do not hand-create version tags. The workflow creates them.
- Secrets (`SYNCFUSION_LICENSE_KEY`, `ANTHROPIC_API_KEY`, `GITHUB_TOKEN`) come only from `secrets.*`. Never write a key into a workflow, script, or log.
- A workflow change is verified by a PR run in CI, not by a local build alone.
