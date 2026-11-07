---
paths:
  - "**/tests/**/*.cs"
---

# Tests

- Location: `<Project>/tests/<Feature>Tests.cs`, under the implementation's solution folder.
- Framework: xUnit v3. Add Moq only where mocking is needed.
- Naming: `<Class>_<Scenario>_<Expected>()`.
- Coverage targets: interfaces 100%, business logic >80%, integrations >70%.
- Local runs: new tests plus tests of touched code, via `dotnet test --filter`. Whole suites are CI's job.
- Filter by namespace or class substring (`FullyQualifiedName~QueryApi`), never file name: `~QueryApiPageTests` matches zero tests and exits 8. Check the summary total is above zero.
- Run `dotnet build -m:1`, then `dotnet test --no-build --filter ...`. Use `--output normal`; `--logger console` is rejected. Data.Core: `dotnet test --project <csproj>`.
- bUnit: register all services before the first resolve. Derive from `BunitContext`; a base context that resolves in its constructor blocks later registrations.
- Don't run the full Data.Database project while other dotnet builds run. A lone failure of the concurrent numbering test under load: rerun once, then treat as environmental.
- No hard-coded ProgramData paths in tests. Resolve via `StorageLocationProvider`, and point pointer or settings stores at a throwaway file.
