# Phase 3: Project Reorganization (Utils Folder Consolidation)

## Scope
Reorganize all CLI tools under unified `src/utils` folder with consistent naming and namespace updates.

## Changes by Item

### Item 3: Reorganize Utils Folder

**Migration plan:**

```
Before:
  src/selftest/VideoForensics.Providers.Ring.SelfTester.csproj
  src/tools/DbSetup/DbSetup.csproj
  src/tools/DbRepair/DbRepair.csproj
  src/tools/VideoForensics.Diagnostics/VideoForensics.Diagnostics.csproj

After:
  src/utils/
    selftest/
      VideoForensics.Utils.SelfTest.csproj
    setup/
      VideoForensics.Utils.DbSetup.csproj
    repair/
      VideoForensics.Utils.DbRepair.csproj
    diagnostics/
      VideoForensics.Utils.Diagnostics.csproj
    logger/
      VideoForensics.Utils.LoggerViewer.csproj (Phase 4)
```

**Namespace updates:**
- `VideoForensics.Providers.Ring.SelfTester` → `VideoForensics.Utils.SelfTest`
- `VideoForensics.DbSetup` → `VideoForensics.Utils.DbSetup`
- `VideoForensics.DbRepair` → `VideoForensics.Utils.DbRepair`
- `VideoForensics.Diagnostics` → `VideoForensics.Utils.Diagnostics`

**Files to modify:**
1. Move and rename .csproj files as listed above
2. Update RootNamespace in each .csproj
3. Update all `using` statements in .cs files
4. Update VideoForensics.sln project references
5. Update any build scripts that reference old paths
6. Verify AssemblyName matches new namespace if present in .csproj

## Testing Checklist

- [ ] Each tool compiles from new location
  - [ ] SelfTest compiles and runs from src/utils/selftest/
  - [ ] DbSetup compiles and runs from src/utils/setup/
  - [ ] DbRepair compiles and runs from src/utils/repair/
  - [ ] Diagnostics compiles and runs from src/utils/diagnostics/

- [ ] Namespace updates correct
  - [ ] All class definitions use new namespaces
  - [ ] All imports reference new namespaces
  - [ ] No "using VideoForensics.Providers.Ring.SelfTester" left in codebase
  - [ ] No "using VideoForensics.DbSetup" (old name) left in codebase

- [ ] Solution-wide
  - [ ] VideoForensics.sln project references updated
  - [ ] No broken project references in dependent projects
  - [ ] `dotnet build` succeeds on entire solution (incremental)
  - [ ] `dotnet test` succeeds for any sibling `tests/` projects

- [ ] Old folders removed
  - [ ] src/selftest/ deleted
  - [ ] src/tools/ deleted

## Dependencies
- None (independent reorganization)

## Next Phase
Phase 4: Logger Viewer & Named Pipe Logging Infrastructure
