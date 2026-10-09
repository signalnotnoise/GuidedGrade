using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GuidedGrade.Models;
namespace GuidedGrade.Services;
internal static class ProtectedSettingsStore
{
    private static string Protect(string text) => string.IsNullOrEmpty(text) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(text), null, DataProtectionScope.CurrentUser));
    private static string Unprotect(string text) => string.IsNullOrEmpty(text) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(text), null, DataProtectionScope.CurrentUser));
    internal static void Save(string path, LLMSettings settings)
    {
        settings.AzureApiKeyProtected = Protect(settings.AzureApiKey);
        settings.OpenAIApiKeyProtected = Protect(settings.OpenAIApiKey);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, json); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    internal static LLMSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        var json = BoundedTextReader.Read(path, 1024 * 1024);
        var settings = JsonSerializer.Deserialize<LLMSettings>(json) ?? new();
        using var doc = JsonDocument.Parse(json);
        bool legacy = doc.RootElement.TryGetProperty("AzureApiKey", out _) || doc.RootElement.TryGetProperty("OpenAIApiKey", out _);
        string ReadKey(string name, string encrypted)
        {
            if (!string.IsNullOrEmpty(encrypted))
            {
                try { return Unprotect(encrypted); }
                catch (Exception ex) when (ex is CryptographicException or FormatException)
                { settings.ConfigurationWarning = "Saved API credentials cannot be unlocked by this Windows user. Re-enter the key in AI Provider settings."; return ""; }
            }
            if (doc.RootElement.TryGetProperty(name, out var key) && key.ValueKind == JsonValueKind.String) { legacy = true; return key.GetString() ?? ""; }
            return "";
        }
        settings.AzureApiKey = ReadKey("AzureApiKey", settings.AzureApiKeyProtected);
        settings.OpenAIApiKey = ReadKey("OpenAIApiKey", settings.OpenAIApiKeyProtected);
        if (legacy && settings.ConfigurationWarning.Length == 0) Save(path, settings);
        return settings;
    }
}
