---
package: ModelContextProtocol
---

# ModelContextProtocol (C# SDK)

Official .NET SDK for building MCP servers and clients.

## Key concepts

- **Transport:** stdio (local IDE process) or HTTP (remote).
- **Tools:** named operations with JSON schemas for inputs/outputs.
- **Hosting:** `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`.

## Typical setup

```csharp
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
```

## Links

- NuGet: https://www.nuget.org/packages/ModelContextProtocol
- Docs: https://csharp.sdk.modelcontextprotocol.io/
