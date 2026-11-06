---
name: full-gate
description: VideoForensics full gate before opening a PR to dev or main: sync wip, bump outdated packages, clean build, full dotnet test, npm check. Use when opening a PR to dev or main or when asked to run the full gate.
---

# Full gate

Ask the user to confirm first. Then:

1. Ensure `wip` contains `dev`; sync the work branch with `wip`.
2. `dotnet list VideoForensics.sln package --outdated`. Bump each flagged package to `Latest` in every `.csproj` that uses it, same version everywhere.
3. `dotnet clean` then `dotnet build`. Fix every warning, error, and notice.
4. `dotnet test` the full suite. Fix failures.
5. `npm ci` then `npm run check`. Fix every error.
