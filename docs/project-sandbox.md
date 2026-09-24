---
package: ProjectSandbox
---

# ProjectSandbox

Security helper that confines file and command tools to `MCP_PROJECT_ROOT`.

## Public surface

- `Root` — absolute sandbox directory.
- `ResolveSafePath(path)` — resolves a path and throws if it escapes the root.
- `IsUnderRoot(fullPath)` — containment check.
- `ToRelative(fullPath)` — repo-relative path with `/` separators.

## Security rules

- Tools must never read or write outside the sandbox.
- Directories `bin`, `obj`, `.git` are skipped by search helpers.
- Command execution uses a fixed whitelist (`dotnet-build`, `dotnet-test`, …).
