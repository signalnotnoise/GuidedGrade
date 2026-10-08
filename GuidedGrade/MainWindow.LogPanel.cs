using System.IO;
using System.Windows;
using System.Windows.Controls;
using GuidedGrade.ViewModels;
using Microsoft.Win32;
namespace GuidedGrade;
public partial class MainWindow
{
    private readonly UI_Framework.State<bool> _logSelected = new(false);
    private readonly LogPanelViewModel _logModel = new();
    private readonly Border _logPanel = new();
    private void OpenLogFile()
    {
        var dialog = new OpenFileDialog { Filter = "Source history logs (*.fslog)|*.fslog", Title = "Open source history log" };
        if (_selectedTabButton?.Tag is string path) dialog.InitialDirectory = Path.GetDirectoryName(path);
        if (dialog.ShowDialog(this) == true) { _logModel.Load(dialog.FileName); _logSelected.Value = true; SetToolsPanelVisible(true); }
    }
    private void SelectLogPanel()
    {
        LoadAssignmentLog();
        _logSelected.Value = true;
        SetToolsPanelVisible(true);
    }
    private void LoadAssignmentLog()
    {
        _logModel.Clear("No log available for the current assignment. Select a student and assignment file.");
        if (_currentAssignment == null || listBoxStudents.SelectedItem is not Models.Student { Folder: not null } student) return;
        if (!string.IsNullOrWhiteSpace(_currentAssignment.LogFilePath))
        {
            try
            {
                var result = Services.BatchReviewPlan.Resolve(student.Folder, Services.BatchReviewPlan.ValidatePattern(_currentAssignment.LogFilePath), "Assignment log missing");
                if (result.Error == null && Services.ReviewContext.Contains(student.Folder, result.Path)) { _logModel.Load(result.Path); }
                else _logModel.Clear(result.Error ?? "Log path is outside the submission.");
            }
            catch (ArgumentException ex) { _logModel.Clear(ex.Message); }
            return;
        }
        if (_selectedTabButton?.Tag is not string path) return;
        var root = Path.GetDirectoryName(path);
        while (root != null && Services.ReviewContext.Contains(student.Folder, root) && !Directory.EnumerateFiles(root, "*.sln").Any() && !Directory.EnumerateFiles(root, "*.vcxproj").Any())
        {
            if (string.Equals(root, student.Folder, StringComparison.OrdinalIgnoreCase)) { root = null; break; }
            root = Path.GetDirectoryName(root);
        }
        if (root == null || !Services.ReviewContext.Contains(student.Folder, root)) return;
        try
        {
            var log = Directory.EnumerateFiles(root, "*.fslog", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false })
                .OrderByDescending(File.GetLastWriteTimeUtc).ThenBy(p => p, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (log != null) { _logModel.Load(log); }
            else _logModel.Clear("No .fslog found in the current assignment folder.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _logModel.Clear("Cannot search assignment logs: " + ex.Message); }
    }
    private Menu LogOptions()
    {
        var menu = new Menu(); var options = new MenuItem { Header = "⋯", ToolTip = "Log options: open or reload assignment history" };
        System.Windows.Automation.AutomationProperties.SetName(options, "Log options");
        var open = new MenuItem { Header = "Open .fslog…" }; open.Click += (_, _) => OpenLogFile();
        var nearby = new MenuItem { Header = "Reload current assignment log" };
        nearby.Click += (_, _) => SelectLogPanel();
        options.Items.Add(open); options.Items.Add(nearby); menu.Items.Add(options); return menu;
    }
}
