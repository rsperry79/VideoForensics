# VideoForensics Development Guidelines

See the **[full Project Guidelines](https://claude.ai/code/artifact/c4e91efe-643f-4fb7-947e-c85323288912)** for all conventions, including:
- Core Principles & Project Structure
- Code Standards (async, secrets, validation, logging)
- Data Requirements (schema design, no JSON blobs)
- Testing (TDD, xUnit, coverage, scoped runs)
- Providers, Client-Server, UI, Admin Setup, Syncfusion, Git Workflow, Local SDK

---

## Quick Reminders

- **Every `.csproj` references `Microsoft.CodeAnalysis` 5.9.0** exactly
- **Interfaces in `Contracts/` folders** with xUnit + Moq tests
- **Async/await + `CancellationToken`** for all I/O
- **No vendor SDK outside service layers**
- **Do NOT read `docs/` directory** unless explicitly asked
- **Delegate implementation to Haiku subagents** (Sonnet plans only)
- **Lite gate (routine):** incremental build + scoped tests
- **Full gate (before PR):** clean rebuild + NuGet updates + full tests
