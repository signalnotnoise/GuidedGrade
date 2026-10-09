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
        private void ListBoxItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item)
            {
                item.IsSelected = true;
                e.Handled = false;
            }
        }

        // --- Context Menu (Assignments ListBox) ----------------------------------

        // NOTE: This method is no longer used - tree view has its own context menu
        /*
        private void OpenAssignmentInFileExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxAssignments.SelectedItem is not Assignment assignment) return;

            if (!string.IsNullOrEmpty(assignment.Folder) && Directory.Exists(assignment.Folder))
                Process.Start("explorer.exe", assignment.Folder);
            else
                System.Windows.MessageBox.Show(
                    "Invalid folder path or folder does not exist.", "Error");
        }
        */


        // --- Open in New Window -----------------------------------------------

        private void OpenNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // new FormCodeView(codeEditor.Text, "test.cpp").Show();
        }

    }
}
