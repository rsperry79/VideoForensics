# Pattern: single point of database access

Use this prompt to move a codebase to one database owner, where client hosts reach data only through DTOs on the server API.

## Prompt

```text
Goal: make the database project the single point of database access. Client hosts reach server data only through DTOs on the server's API.

Before any code:
1. Read the repo instructions, every path-scoped rule that matches the files you touch, and any workflow skill the repo provides.
2. Work in a git worktree on a new branch from the default branch. Do not edit the main checkout.
3. List your open questions and ask them before planning. Do not guess at them.

Target architecture:
- Data layer: only the database project owns the database context, entities, and repository implementations. Other projects must not reference data entities or data-layer repository interfaces.
- Contracts: each repository gets a DTO interface in the API contracts project. It keeps the name of the old data-layer interface. The data-layer interface is renamed to a write repository and stays server-side.
- Server adapter: a DTO repository in the mapping project implements the DTO interface by calling the write repository and converting with entity-to-DTO and DTO-to-entity extension methods. Enum DTOs mirror entity enums by member name and convert with Enum.Parse.
- API: each DTO operation is exposed on a versioned route. Validate input at the boundary.
- Client: the client host implements each DTO interface with an HTTP-backed remote class. Registrations go in the host's DI extensions: server registrations in the server block, client registrations in the client block.
- Client libraries (UI, bridges, CLI) reference only the contracts project and the client common project. Remove references to the data project and to the client core project from UI projects.
- Client common must not import data entities. Replace entity types in its interfaces with DTO types. Remove its project reference to the data project once it builds without it.

Method (one unit per type or repository, each committed locally):
1. Add architecture gate tests first: scan the csproj files and source of client projects for data-layer references. They should fail before the change and pass after.
2. Per unit: add the DTO, the mapping, the server DTO repository, the remote class, and the DI registrations. Update consumers and tests. Keep entity-facing methods where server callers need them.
3. Build serially, one build at a time. Parallel builds cause stale-output errors (CS0009). Run affected test suites with the test runner's no-build option.
4. Commit each unit locally. Write the commit message to a file and commit with it. Stage only that unit's files.

Rules:
- Do not push and do not open a PR until asked.
- No AI attribution in commits or PRs (no co-author trailer, no "generated with" line).
- Do not rewrite CRLF files with stream-editing tools. Read the file, then use the edit tool.
- Do not read docs or archive folders unless asked.
- Subagents: use the cheapest suitable model unless the user approves another.
- Keep the plan in a plan file and mark each unit done when committed.

Done when: the gate tests pass, client projects have no data-layer references, every affected suite passes (report counts and skips), and the branch has one commit per unit.
Final message: list the commits, test results, and open questions. Do not push.
```

## Notes
- Gate tests are the fastest way to prove the boundary holds. Write them first.
- Unit order: start with the types that have the fewest consumers, and leave shared contracts for last.
