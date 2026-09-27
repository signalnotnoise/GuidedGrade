using System.IO;
using System.Text.Json;

namespace GuidedGrade.Services;

public sealed class WorkspacePanelPreferences
{
    public bool ShowComments { get; set; } = true;
    public bool ShowQueue { get; set; } = true;
    private static string FilePath => Path.Combine(AppDataPaths.RoamingDirectory, "workspace-panels.json");

    public static WorkspacePanelPreferences Load()
    {
        try { return JsonSerializer.Deserialize<WorkspacePanelPreferences>(File.ReadAllText(FilePath)) ?? new(); }
        catch (IOException) { return new(); }
        catch (UnauthorizedAccessException) { return new(); }
        catch (JsonException) { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this));
    }
}
