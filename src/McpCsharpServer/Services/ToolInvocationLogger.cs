using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace McpCsharpServer.Services;

/// <summary>
/// Writes explicit tool invocation traces required by the assignment:
/// tool name, input parameters (no secrets), success/error status.
/// </summary>
public sealed class ToolInvocationLogger(ILogger<ToolInvocationLogger> logger)
{
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "api_key", "apikey", "token", "secret", "authorization", "connectionstring"
    };

    public void LogStart(string toolName, object parameters)
    {
        var safe = Sanitize(parameters);
        logger.LogInformation(
            "TOOL_CALL start tool={Tool} params={Params}",
            toolName,
            safe);
    }

    public void LogSuccess(string toolName, string summary)
    {
        logger.LogInformation(
            "TOOL_CALL status=success tool={Tool} summary={Summary}",
            toolName,
            summary);
    }

    public void LogError(string toolName, string error)
    {
        logger.LogError(
            "TOOL_CALL status=error tool={Tool} error={Error}",
            toolName,
            error);
    }

    private static string Sanitize(object parameters)
    {
        try
        {
            using var doc = JsonSerializer.SerializeToDocument(parameters);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return doc.RootElement.ToString();
            }

            var dict = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                dict[prop.Name] = SecretKeys.Contains(prop.Name)
                    ? "***"
                    : prop.Value.Clone();
            }

            return JsonSerializer.Serialize(dict);
        }
        catch
        {
            return "<unserializable>";
        }
    }
}
