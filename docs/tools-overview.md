---
package: McpCsharpServer.Tools
---

# Server tools overview

This MCP server exposes four tools:

| Tool | Purpose |
|------|---------|
| `lookup_docs` | Local markdown documentation search (Context7-style). |
| `search_project` | Substring search across project files. |
| `list_public_api` | Discover public C# types/methods from sources. |
| `run_allowed_command` | Whitelisted `dotnet` commands only. |

Each tool returns a structured JSON object with a `status` field (`success` | `error`).
