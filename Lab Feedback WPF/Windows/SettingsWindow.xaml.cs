using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Lab_Feedback_WPF.Services;

namespace Lab_Feedback_WPF.Windows
{
    public partial class SettingsWindow : Window
    {
        private readonly ViolationsConfigService _configService;
        private readonly string? _openedDirectoryPath;

        public List<string> Violations { get; private set; }
        public List<string> FilePatterns { get; private set; }
        public bool SettingsSaved { get; private set; }

        public SettingsWindow(string? openedDirectoryPath = null)
        {
            InitializeComponent();

            _openedDirectoryPath = openedDirectoryPath;
            _configService = new ViolationsConfigService();

            // Load current settings
            Violations = _configService.LoadViolations(openedDirectoryPath);
            FilePatterns = new List<string>(_configService.FilePatterns);

            LoadLists();
        }

        private void LoadLists()
        {
            lstViolations.ItemsSource = null;
            lstViolations.ItemsSource = Violations;

            lstFilePatterns.ItemsSource = null;
            lstFilePatterns.ItemsSource = FilePatterns;
        }

        private void btnAddViolation_Click(object sender, RoutedEventArgs e)
        {
            AddViolation();
        }

        private void txtNewViolation_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddViolation();
                e.Handled = true;
            }
        }

        private void AddViolation()
        {
            var text = txtNewViolation.Text.Trim();
            if (!string.IsNullOrEmpty(text) && !Violations.Contains(text))
            {
                Violations.Add(text);
                LoadLists();
                txtNewViolation.Clear();
            }
        }

        private void btnRemoveViolation_Click(object sender, RoutedEventArgs e)
        {
            if (lstViolations.SelectedItem != null)
            {
                Violations.Remove((string)lstViolations.SelectedItem);
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

        private void btnAddPattern_Click(object sender, RoutedEventArgs e)
        {
            AddPattern();
        }

        private void txtNewPattern_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddPattern();
                e.Handled = true;
            }
        }

        private void AddPattern()
        {
            var text = txtNewPattern.Text.Trim();
            if (!string.IsNullOrEmpty(text) && !FilePatterns.Contains(text))
            {
                FilePatterns.Add(text);
                LoadLists();
                txtNewPattern.Clear();
            }
        }

        private void btnRemovePattern_Click(object sender, RoutedEventArgs e)
        {
            if (lstFilePatterns.SelectedItem != null)
            {
                FilePatterns.Remove((string)lstFilePatterns.SelectedItem);
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
                    "No directory opened. Settings will be applied for this session only.",
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
