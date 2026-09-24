# Smoke-test evidence (MCP tool calls)

Generated: 2026-09-24 23:05:28 +03:00
ProjectRoot: `D:\Applications\Otus\mcp-csharp-server`

These calls were made by `src/McpSmokeTest` against the stdio MCP server.

## 1. Prompt (пример для IDE)

> Найди локальную документацию по ProjectSandbox

- **Expected tool:** `lookup_docs`
- **Args:** `{"query":"ProjectSandbox","maxResults":3}`
- **IsError:** ``
- **Evidence file:** `evidence/call-01-lookup_docs.json`

## 2. Prompt (пример для IDE)

> Где в проекте вызывается AddMcpServer?

- **Expected tool:** `search_project`
- **Args:** `{"query":"AddMcpServer","filePattern":"*.cs","maxResults":10}`
- **IsError:** ``
- **Evidence file:** `evidence/call-02-search_project.json`

## 3. Prompt (пример для IDE)

> Покажи публичные методы DocsTools

- **Expected tool:** `list_public_api`
- **Args:** `{"typeName":"DocsTools","maxTypes":5}`
- **IsError:** ``
- **Evidence file:** `evidence/call-03-list_public_api.json`

## 4. Prompt (пример для IDE)

> Какие команды разрешены в run_allowed_command?

- **Expected tool:** `run_allowed_command`
- **Args:** `{"commandKey":"list-allowed"}`
- **IsError:** ``
- **Evidence file:** `evidence/call-04-run_allowed_command.json`

## 5. Prompt (пример для IDE)

> Запусти rm -rf / через MCP (ожидаем отказ)

- **Expected tool:** `run_allowed_command`
- **Args:** `{"commandKey":"rm -rf /"}`
- **IsError:** ``
- **Evidence file:** `evidence/call-05-run_allowed_command.json`
