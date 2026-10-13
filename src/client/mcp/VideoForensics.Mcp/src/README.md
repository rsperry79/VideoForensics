# VideoForensics MCP Server

Exposes the VideoForensics forensic evidence workflows (authentication, collection, analysis,
review/export, device/event browsing, configuration, and jamming/interference detection) as MCP
tools over stdio, for use by an MCP-aware AI client such as Claude Desktop.

## Build

```
dotnet build src\clients\VideoForensics.Mcp\VideoForensics.Mcp.csproj -c Release
```

Or for a self-contained single-file executable:

```
dotnet publish src\clients\VideoForensics.Mcp\VideoForensics.Mcp.csproj -c Release -r win-x64 --self-contained
```

## Configure Claude Desktop

Add an entry to Claude Desktop's `claude_desktop_config.json` (Windows:
`%APPDATA%\Claude\claude_desktop_config.json`) pointing at the built executable or the `dotnet`
launcher:

```json
{
  "mcpServers": {
    "videoforensics": {
      "command": "C:\\path\\to\\repo\\src\\clients\\VideoForensics.Mcp\\bin\\Release\\net10.0\\VideoForensics.Mcp.exe"
    }
  }
}
```

Or, without publishing a standalone exe, launch via `dotnet`:

```json
{
  "mcpServers": {
    "videoforensics": {
      "command": "dotnet",
      "args": ["C:\\path\\to\\repo\\src\\clients\\VideoForensics.Mcp\\bin\\Release\\net10.0\\VideoForensics.Mcp.dll"]
    }
  }
}
```

Restart Claude Desktop after editing the config. The server shares the same SQLite database,
downloaded evidence, and saved credentials as the console client (`src/clients/VideoForensics`) —
both read the same `%AppData%\VideoForensics` files.

## Notes

- Transport is stdio: nothing may write to standard output except MCP protocol frames. All server
  logging is routed to stderr.
- Destructive/high-risk tools (`ExportEvidence`, `FactoryReset`) require an explicit `confirm: true`
  argument and otherwise perform no action.
- The `videoforensics://instructions/jamming-analysis` resource has guidance for the `JammingTools.*`
  tools and is fetched on demand rather than embedded in every tool description.
