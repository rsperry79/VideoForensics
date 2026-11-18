# VideoForensics full gate

Used by the global `full-gate` skill. Ask the user to confirm first. Then:

1. Ensure `wip` contains `dev`; sync the work branch with `wip`.
2. `dotnet list VideoForensics.sln package --outdated`. Report the flagged packages. Do not bump them: versions stay pinned to what the repo uses until the user asks for an upgrade.
3. `dotnet clean` then `dotnet build`. Fix every warning, error, and notice.
4. `dotnet test` the full suite. Fix failures.
5. `npm ci` then `npm run check`. Fix every error.
