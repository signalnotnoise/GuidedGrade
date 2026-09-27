using System.Net.Http;
using System.Text.Json;

namespace GuidedGrade.Services;

internal static class LlmHttpErrors
{
    public static string Detail(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String) body = error.GetString() ?? "";
                else if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                    body = message.GetString() ?? "";
            }
        }
        catch (JsonException) { }
        catch (InvalidOperationException) { }
        return string.IsNullOrWhiteSpace(body) ? "The server returned no error details." : body[..Math.Min(body.Length, 4000)];
    }

    public static HttpRequestException Create(string provider, HttpResponseMessage response, string body, string context = "")
        => new($"{provider} returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). {context}\n{Detail(body)}",
            null, response.StatusCode);

    public static bool IsGpuMemoryFailure(string body)
    {
        var detail = Detail(body);
        return (detail.Contains("CUDA", StringComparison.OrdinalIgnoreCase) || detail.Contains("GPU", StringComparison.OrdinalIgnoreCase))
            && (detail.Contains("out of memory", StringComparison.OrdinalIgnoreCase) || detail.Contains("out-of-memory", StringComparison.OrdinalIgnoreCase));
    }
}
