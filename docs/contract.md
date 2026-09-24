# Tool outputs contract

All tools return a **structured JSON object** (MCP `structuredContent`), not plain prose.

## Common fields

| Field | Type | Meaning |
|-------|------|---------|
| `status` | `string` | `success` or `error` |
| `error` | `string?` | Present when `status` is `error` |

## `lookup_docs`

```json
{
  "status": "success",
  "query": "ProjectSandbox",
  "totalIndexed": 3,
  "results": [
    {
      "title": "ProjectSandbox",
      "package": "ProjectSandbox",
      "path": "docs/project-sandbox.md",
      "score": 4,
      "excerpt": "Security helper that confines…",
      "link": "file://docs/project-sandbox.md"
    }
  ]
}
```

## `search_project`

```json
{
  "status": "success",
  "query": "AddMcpServer",
  "sandboxRoot": "D:/Applications/Otus/mcp-csharp-server",
  "matchCount": 1,
  "matches": [
    { "path": "src/McpCsharpServer/Program.cs", "line": 28, "text": ".AddMcpServer()" }
  ]
}
```

## `list_public_api`

```json
{
  "status": "success",
  "typeQuery": "DocsTools",
  "typeCount": 1,
  "types": [
    {
      "typeName": "DocsTools",
      "path": "src/McpCsharpServer/Tools/DocsTools.cs",
      "publicMethods": ["LookupDocs"]
    }
  ]
}
```

## `run_allowed_command`

```json
{
  "status": "success",
  "commandKey": "list-allowed",
  "executable": "list-allowed",
  "arguments": [],
  "exitCode": 0,
  "stdout": "dotnet-build: Build the solution…",
  "stderr": "",
  "workingDirectory": "D:/Applications/Otus/mcp-csharp-server"
}
```

Rejected (non-whitelist) commands return `status: error` with an explanatory `error` message.
