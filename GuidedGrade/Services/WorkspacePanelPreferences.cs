using System.IO;
using System.Text.Json;

namespace GuidedGrade.Services;

public sealed class WorkspacePanelPreferences
{
    public bool ShowComments { get; set; } = true;
    public bool ShowQueue { get; set; } = true;
    public bool LightMode { get; set; }
    public string LogFileName { get; set; } = "*";
    public string LogFileExtension { get; set; } = ".fslog";
    public string LogPattern => LogFileName.Trim() + "." + LogFileExtension.Trim().TrimStart('.');
    public bool MatchesLog(string path) => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(LogPattern, Path.GetFileName(path), ignoreCase: true);
    public void ValidateLogSettings()
    {
        if (string.IsNullOrWhiteSpace(LogFileName) || LogFileName.IndexOfAny(['/', '\\', ':', '"', '<', '>', '|', '?']) >= 0 || LogFileName.Contains('.'))
            throw new ArgumentException("Enter a log filename without a path or extension; use * for any name.");
        var extension = LogFileExtension.Trim().TrimStart('.');
        if (extension.Length == 0 || !extension.All(char.IsLetterOrDigit))
            throw new ArgumentException("Enter a log extension such as .fslog or .log.");
    }
    private static string FilePath => Path.Combine(AppDataPaths.RoamingDirectory, "workspace-panels.json");

    public static WorkspacePanelPreferences Load()
    {
        try { return JsonSerializer.Deserialize<WorkspacePanelPreferences>(BoundedTextReader.Read(FilePath)) ?? new(); }
        catch (IOException) { return new(); }
        catch (UnauthorizedAccessException) { return new(); }
        catch (JsonException) { return new(); }
    }

    public void Save()
    {
        ValidateLogSettings();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
    }
}
