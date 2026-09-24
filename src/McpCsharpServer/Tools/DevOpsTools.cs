using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using McpCsharpServer.Services;
using ModelContextProtocol.Server;

namespace McpCsharpServer.Tools;

[McpServerToolType]
public sealed class DevOpsTools(ProjectSandbox sandbox, ToolInvocationLogger invocationLogger)
{
    /// <summary>
    /// Whitelist of safe commands. Keys are what the agent passes; values are argv.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, AllowedCommand> Allowed = new Dictionary<string, AllowedCommand>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["dotnet-build"] = new("dotnet", ["build"], "Build the solution/project under the sandbox."),
        ["dotnet-test"] = new("dotnet", ["test", "--no-restore"], "Run tests (no restore)."),
        ["dotnet-restore"] = new("dotnet", ["restore"], "Restore NuGet packages."),
        ["list-allowed"] = new("list", [], "Return the whitelist without executing anything.")
    };

    [McpServerTool(Name = "run_allowed_command", UseStructuredContent = true),
     Description(
         "Runs a whitelisted DevOps command inside the project sandbox. " +
         "Allowed keys: dotnet-build, dotnet-test, dotnet-restore, list-allowed. " +
         "Arbitrary shell input is rejected.")]
    public async Task<RunCommandResult> RunAllowedCommand(
        [Description("Whitelist key, e.g. dotnet-build or list-allowed.")] string commandKey,
        CancellationToken cancellationToken = default)
    {
        const string tool = "run_allowed_command";
        invocationLogger.LogStart(tool, new { commandKey });

        try
        {
            if (string.IsNullOrWhiteSpace(commandKey))
            {
                throw new ArgumentException("commandKey is required.");
            }

            if (!Allowed.TryGetValue(commandKey.Trim(), out var cmd))
            {
                throw new UnauthorizedAccessException(
                    $"Command '{commandKey}' is not whitelisted. Use list-allowed to see permitted keys.");
            }

            if (cmd.Executable.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                var list = new RunCommandResult(
                    Status: "success",
                    CommandKey: commandKey,
                    Executable: "list-allowed",
                    Arguments: [],
                    ExitCode: 0,
                    StdOut: string.Join('\n', Allowed.Select(kv => $"{kv.Key}: {kv.Value.Description}")),
                    StdErr: "",
                    WorkingDirectory: sandbox.Root);

                invocationLogger.LogSuccess(tool, "listed whitelist");
                return list;
            }

            var psi = new ProcessStartInfo
            {
                FileName = cmd.Executable,
                WorkingDirectory = sandbox.Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in cmd.Args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi)
                                ?? throw new InvalidOperationException("Failed to start process.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stdout = Truncate(await stdoutTask, 8000);
            var stderr = Truncate(await stderrTask, 4000);
            var status = process.ExitCode == 0 ? "success" : "error";

            var result = new RunCommandResult(
                Status: status,
                CommandKey: commandKey,
                Executable: cmd.Executable,
                Arguments: cmd.Args,
                ExitCode: process.ExitCode,
                StdOut: stdout,
                StdErr: stderr,
                WorkingDirectory: sandbox.Root,
                Error: process.ExitCode == 0 ? null : $"Process exited with code {process.ExitCode}");

            if (process.ExitCode == 0)
            {
                invocationLogger.LogSuccess(tool, $"exitCode=0 key={commandKey}");
            }
            else
            {
                invocationLogger.LogError(tool, $"exitCode={process.ExitCode}");
            }

            return result;
        }
        catch (Exception ex)
        {
            invocationLogger.LogError(tool, ex.Message);
            return new RunCommandResult(
                Status: "error",
                CommandKey: commandKey,
                Executable: "",
                Arguments: [],
                ExitCode: -1,
                StdOut: "",
                StdErr: "",
                WorkingDirectory: sandbox.Root,
                Error: ex.Message);
        }
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "\n…(truncated)…";

    private sealed record AllowedCommand(string Executable, string[] Args, string Description);
}

public sealed record RunCommandResult(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("commandKey")] string CommandKey,
    [property: JsonPropertyName("executable")] string Executable,
    [property: JsonPropertyName("arguments")] IReadOnlyList<string> Arguments,
    [property: JsonPropertyName("exitCode")] int ExitCode,
    [property: JsonPropertyName("stdout")] string StdOut,
    [property: JsonPropertyName("stderr")] string StdErr,
    [property: JsonPropertyName("workingDirectory")] string WorkingDirectory,
    [property: JsonPropertyName("error")] string? Error = null);
