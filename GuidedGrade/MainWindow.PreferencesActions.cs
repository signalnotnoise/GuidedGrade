using GuidedGrade.ViewModels;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using GuidedGrade.Models;
using GuidedGrade.Services;
using GuidedGrade.Views;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;


namespace GuidedGrade
{
    public partial class MainWindow
    {
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new Windows.SettingsWindow(_openedDirectoryPath)
            {
                Owner = this
            };

            if (settingsWindow.ShowDialog() == true && settingsWindow.SettingsSaved)
            {
                _panelPreferences = Services.WorkspacePanelPreferences.Load();
                ApplyPanelPreferences();
                // Refresh violations status with new settings
                UpdateViolationsStatus();
            }
        }

        private void LLMSettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var llmSettingsWindow = new Windows.LLMSettingsWindow
            {
                Owner = this
            };
            llmSettingsWindow.ShowDialog();
        }

        // --- File Tabs --------------------------------------------------------

    }
}
