---
paths:
  - "src/client/**"
---

# Client-side libraries

- Before hand-rolling client code (UI, JS, CSS, browser or device helpers), check for an existing client-side library that meets the same requirements: maintained, secure, fits the need.
- Prefer the library. Check for a newer or patched version before writing a workaround.
- Prefer TypeScript libraries. When a library has no TS types, use typed definitions (`@types/*`) where available.
- Before writing any Syncfusion code, query the Syncfusion MCP (`sf-blazor-mcp` for Blazor, `sf-maui-mcp` for MAUI) for current API usage and docs.
- Check the license is compatible before adopting it.
