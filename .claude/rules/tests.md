---
paths:
  - "**/tests/**/*.cs"
---

# Tests

- Location: `<Project>/tests/<Feature>Tests.cs`. Test project sits under the same solution folder as its implementation.
- Framework: xUnit v3. Add Moq only where mocking is needed.
- Naming: `<Class>_<Scenario>_<Expected>()`.
- Coverage targets: interfaces 100%, business logic >80%, integrations >70%.
- TDD: write the test first and record its failing run before implementing.
- Local runs are scoped: new tests plus tests for touched code, via `dotnet test --filter`. Do not run whole suites locally; CI covers the rest.
