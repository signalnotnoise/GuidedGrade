using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using UI_Framework;
using static UI_Framework.UI;
using GuidedGrade.Presentation;
using GuidedGrade.Services;
using GuidedGrade.Models;

namespace GuidedGrade.Windows
{
    public sealed class SettingsWindow : ReviewWindow
    {
        private readonly ViolationsConfigService _configService;
        private readonly string? _openedDirectoryPath;

        public List<string> Violations { get; private set; }
        public List<string> FilePatterns { get; private set; }
        public bool SettingsSaved { get; private set; }

        public SettingsWindow(string? openedDirectoryPath = null)
        {
            Title = "Settings"; Width = 720; Height = 810; MinWidth = 520; MinHeight = 450;

            _openedDirectoryPath = openedDirectoryPath;
            _configService = new ViolationsConfigService();

            // Load current settings
            Violations = _configService.LoadViolations(openedDirectoryPath);
            FilePatterns = new List<string>(_configService.FilePatterns);

            var settings = LLMSettings.Load();
            _execution.Value = settings.ExecutionMode == SubmissionExecutionMode.Local ? 1 : 0;
            _confirmLocal.Value = settings.ConfirmLocalExecution;
            _confirmGrading.Value = settings.ConfirmGrading;
            var panels = WorkspacePanelPreferences.Load();
            _comments.Value = panels.ShowComments;
            _queue.Value = panels.ShowQueue;
            LoadLists();
            ShowView(BuildView);
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key != System.Windows.Input.Key.Enter || e.OriginalSource is not System.Windows.Controls.TextBox input) return;
                var label = System.Windows.Automation.AutomationProperties.GetName(input);
                if (label == "New violation") { AddViolation(); e.Handled = true; }
                if (label == "New file pattern") { AddPattern(); e.Handled = true; }
            };
        }

        private readonly State<string> _newViolation = new("");
        private readonly State<string> _newPattern = new("");
        private readonly State<int> _violationIndex = new(-1);
        private readonly State<int> _patternIndex = new(-1);
        private readonly State<int> _execution = new(0);
        private readonly State<bool> _confirmLocal = new(true);
        private readonly State<bool> _confirmGrading = new(true);
        private readonly State<bool> _comments = new(true);
        private readonly State<bool> _queue = new(true);
        private readonly State<int> _revision = new(0);

        private View BuildView()
        {
            _ = _revision.Value;
            return Scroll(VStack(
                Text("Programming checks").FontSize(22),
                Text("Violation terms"), Picker(Violations, _violationIndex).AccessibilityLabel("Violation terms"),
                FlexRow(TextField(_newViolation).AccessibilityLabel("New violation"), Button("Add", AddViolation).Flex(0)),
                HStack(Button("Remove selected", () => btnRemoveViolation_Click(this, new())).IsEnabled(_violationIndex.Value >= 0),
                    Button("Reset terms", () => btnResetViolations_Click(this, new()))).Spacing(8),
                Text("File patterns"), Picker(FilePatterns, _patternIndex).AccessibilityLabel("File patterns"),
                FlexRow(TextField(_newPattern).AccessibilityLabel("New file pattern"), Button("Add", AddPattern).Flex(0)),
                HStack(Button("Remove selected", () => btnRemovePattern_Click(this, new())).IsEnabled(_patternIndex.Value >= 0),
                    Button("Reset patterns", () => btnResetPatterns_Click(this, new()))).Spacing(8),
                Text("Build and run on"), Picker(new[] { "VM (Hyper-V)", "Local computer" }, _execution).AccessibilityLabel("Execution environment"),
                Toggle("Ask before each local build or run", _confirmLocal),
                Toggle("Ask before grading", _confirmGrading),
                Text("Local code uses your Windows permissions. Configure model timeouts and the VM in AI Provider settings.").FontSize(13),
                Toggle("Show Comments", _comments), Toggle("Show Job queue", _queue),
                Text("Hiding a tab keeps feedback and running jobs.").FontSize(13),
                HStack(Button("Save", () => btnSave_Click(this, new())).ButtonStyle(ButtonStyleKind.Primary),
                    Button("Cancel", () => btnCancel_Click(this, new()))).Spacing(8)
            ).Spacing(12).Padding(20));
        }

        private void LoadLists()
        {
            _violationIndex.Value = -1;
            _patternIndex.Value = -1;
            _revision.Value++;
        }

        private void AddViolation()
        {
            var text = _newViolation.Value.Trim();
            if (!string.IsNullOrEmpty(text) && !Violations.Contains(text))
            {
                Violations.Add(text);
                LoadLists();
                _newViolation.Value = "";
            }
        }

        private void btnRemoveViolation_Click(object sender, RoutedEventArgs e)
        {
            if (_violationIndex.Value >= 0 && _violationIndex.Value < Violations.Count)
            {
                Violations.RemoveAt(_violationIndex.Value);
                LoadLists();
            }
        }

        private void btnResetViolations_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset violations to defaults? This will remove all custom violations.",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Violations = _configService.GetDefaultViolations();
                LoadLists();
            }
        }

        private void AddPattern()
        {
            var text = _newPattern.Value.Trim();
            if (!string.IsNullOrEmpty(text) && !FilePatterns.Contains(text))
            {
                FilePatterns.Add(text);
                LoadLists();
                _newPattern.Value = "";
            }
        }

        private void btnRemovePattern_Click(object sender, RoutedEventArgs e)
        {
            if (_patternIndex.Value >= 0 && _patternIndex.Value < FilePatterns.Count)
            {
                FilePatterns.RemoveAt(_patternIndex.Value);
                LoadLists();
            }
        }

        private void btnResetPatterns_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset file patterns to defaults? This will restore the default patterns.",
                "Confirm Reset",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                FilePatterns = new List<string> { "*.cpp", "*.h", "*.cs", "*.c", "*.hpp" };
                LoadLists();
            }
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var execution = LLMSettings.Load();
                execution.ExecutionMode = _execution.Value == 1 ? SubmissionExecutionMode.Local : SubmissionExecutionMode.HyperV;
                execution.ConfirmLocalExecution = _confirmLocal.Value;
                execution.ConfirmGrading = _confirmGrading.Value;
                execution.Save();
                new WorkspacePanelPreferences { ShowComments = _comments.Value == true, ShowQueue = _queue.Value == true }.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save workspace preferences: {ex.Message}", "Settings");
                return;
            }
            // Save to .violations file in the opened directory
            if (!string.IsNullOrEmpty(_openedDirectoryPath))
            {
                try
                {
                    var configPath = System.IO.Path.Combine(_openedDirectoryPath, ".violations");
                    SaveConfigFile(configPath);
                    SettingsSaved = true;
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error saving settings: {ex.Message}",
                        "Save Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show(
                    "Workspace preferences saved. Violation settings require an open folder to persist.",
                    "No Directory",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                SettingsSaved = true;
                DialogResult = true;
                Close();
            }
        }

        private void SaveConfigFile(string filePath)
        {
            var lines = new List<string>
            {
                "# Violations Configuration File",
                "# Each line represents a term to flag as a violation in student code",
                "# Lines starting with # are comments",
                "",
                "# Violation Terms"
            };

            lines.AddRange(Violations);

            lines.Add("");
            lines.Add("# File Patterns Section");
            lines.Add("# Specify which files should be scanned for violations");
            lines.Add("# Supports wildcards: * (any characters) and ? (single character)");
            lines.Add("");
            lines.Add("[FILES]");
            lines.AddRange(FilePatterns);

            System.IO.File.WriteAllLines(filePath, lines);
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            SettingsSaved = false;
            DialogResult = false;
            Close();
        }
    }
}
