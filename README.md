## Как IDE/агент подключается к MCP

Cursor (или другой MCP-клиент) читает конфигурацию серверов (здесь — `.cursor/mcp.json`), запускает указанный процесс (`dotnet run …`) и обменивается JSON-RPC сообщениями по **stdio**: stdin/stdout. Протокол MCP объявляет доступные **tools** (имя, описание, JSON Schema входных параметров). Агент выбирает tool по смыслу пользовательского запроса и вызывает его; сервер выполняет код и возвращает структурированный результат.

В этом проекте **tool** — это метод, помеченный `[McpServerTool]`: именованная операция с типизированными параметрами и структурированным JSON-ответом (`status`, данные, опционально `error`). Логи вызовов пишутся в **stderr**, чтобы не портить MCP-поток на stdout.

## Быстрый старт

Требования: [.NET 9 SDK](https://dotnet.microsoft.com/download).

```powershell
cd D:\Applications\Otus\mcp-csharp-server
copy .env.example .env
dotnet restore
dotnet build
```

### Зависимости

Зафиксированы в `src/McpCsharpServer/McpCsharpServer.csproj` (NuGet):

- `ModelContextProtocol` 2.2.0
- `Microsoft.Extensions.Hosting` / `Logging`

## Инструменты

| Tool | Назначение | Ограничения |
|------|------------|-------------|
| `lookup_docs` | Поиск по `docs/*.md` | Только локальные markdown |
| `search_project` | Поиск строк в файлах | Только `MCP_PROJECT_ROOT`; skip `bin`/`obj`/`.git` |
| `list_public_api` | Публичные типы/методы из `.cs` | Только sandbox |
| `run_allowed_command` | `dotnet-build` / `dotnet-test` / `dotnet-restore` / `list-allowed` | Whitelist; произвольный shell запрещён |

Контракт ответов: [docs/contract.md](docs/contract.md).

## Подключение в Cursor (2–5 шагов)

1. Откройте папку `mcp-csharp-server` как workspace в Cursor.
2. Убедитесь, что файл [`.cursor/mcp.json`](.cursor/mcp.json) на месте (уже в репозитории).
3. Выполните `dotnet build` один раз, чтобы проект собирался.
4. Cursor → **Settings → MCP** (или перезагрузка окна): сервер `mcp-csharp-server` должен появиться и стать зелёным.
5. В чате агента задайте запрос вроде «Найди локальную документацию по ProjectSandbox» — агент должен вызвать `lookup_docs`.

Пример конфигурации (фрагмент):

```json
{
  "mcpServers": {
    "mcp-csharp-server": {
      "command": "dotnet",
      "args": ["run", "--project", "src/McpCsharpServer/McpCsharpServer.csproj"],
      "cwd": "${workspaceFolder}",
      "env": { "MCP_PROJECT_ROOT": "${workspaceFolder}" }
    }
  }
}
```

## Логи и отладка

На каждый вызов tool сервер пишет в stderr строки вида:

```text
TOOL_CALL start tool=lookup_docs params={"query":"ProjectSandbox","maxResults":3}
TOOL_CALL status=success tool=lookup_docs summary=hits=1
```

Реализация логгера: `src/McpCsharpServer/Services/ToolInvocationLogger.cs`.

## Smoke-test без IDE

```powershell
dotnet build
dotnet run --project src/McpSmokeTest
```

Артефакты вызовов появятся в `evidence/` (JSON + краткий отчёт).

## Проверочные запросы (для IDE)

| # | Запрос пользователя | Ожидаемый tool | Подтверждение |
|---|---------------------|----------------|---------------|
| 1 | Найди локальную документацию по ProjectSandbox | `lookup_docs` | `evidence/call-01-lookup_docs.json` |
| 2 | Где в проекте вызывается AddMcpServer? | `search_project` | `evidence/call-02-search_project.json` |
| 3 | Покажи публичные методы DocsTools | `list_public_api` | `evidence/call-03-list_public_api.json` |
| 4 | Какие команды разрешены в run_allowed_command? | `run_allowed_command` | `evidence/call-04-run_allowed_command.json` |
| 5 | Запусти `rm -rf /` через MCP | `run_allowed_command` → error | `evidence/call-05-run_allowed_command.json` |

Минимум 3 из них — реальные успешые вызовы MCP-tool; №5 демонстрирует отказ по security policy.

---

## Подтверждения ссылками на код (для отчёта)

### 1. MCP-сервер поднимается и регистрирует tools

- Старт хоста + stdio transport + discovery: [`Program.cs:L7–L35`](src/McpCsharpServer/Program.cs) (`AddMcpServer` / `WithStdioServerTransport` / `WithToolsFromAssembly` — L25–L28).
- Регистрация tool-классов через `[McpServerToolType]` / `[McpServerTool]`:
  - [`DocsTools.cs:L8–L56`](src/McpCsharpServer/Tools/DocsTools.cs)
  - [`ProjectTools.cs:L9–L191`](src/McpCsharpServer/Tools/ProjectTools.cs)
  - [`DevOpsTools.cs:L9–L127`](src/McpCsharpServer/Tools/DevOpsTools.cs)

### 2. Инструменты + debug-логи

| Tool | Реализация | Логи start/success/error |
|------|------------|--------------------------|
| `lookup_docs` | `DocsTools.cs:L11–L56` | `ToolInvocationLogger.cs:L17–L40` + `DocsTools.cs:L20` |
| `search_project` | `ProjectTools.cs:L22–L102` | `ProjectTools.cs:L33` |
| `list_public_api` | `ProjectTools.cs:L104–L191` | `ProjectTools.cs:L113` |
| `run_allowed_command` | `DevOpsTools.cs:L24–L127` | `DevOpsTools.cs:L34` |

Пример вывода лога:

```text
info: McpCsharpServer.Services.ToolInvocationLogger[0]
      TOOL_CALL start tool=search_project params={"query":"AddMcpServer","filePattern":"*.cs","maxResults":10}
info: McpCsharpServer.Services.ToolInvocationLogger[0]
      TOOL_CALL status=success tool=search_project summary=matches=1
```

### 3. Агент / клиент вызывает нужный tool

Пример запроса: «Найди локальную документацию по ProjectSandbox» → ожидаемо `lookup_docs`.

Фактическое подтверждение после `dotnet run --project src/McpSmokeTest`: файл `evidence/call-01-lookup_docs.json` и сводка `evidence/README.md`. После подключения в Cursor можно дополнительно сохранить скриншот панели MCP / trace в `evidence/ide-screenshot.png` (по желанию).

### 4. Контракт результатов

Описан в [`docs/contract.md`](docs/contract.md).

## Безопасность

- Файловые операции только внутри `MCP_PROJECT_ROOT` (`ProjectSandbox`).
- Команды — только whitelist; произвольный shell отвергается.
- Секреты в логах маскируются (`password`, `api_key`, `token`, …).
