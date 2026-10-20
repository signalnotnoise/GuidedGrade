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
        // --- Folder / Student Loading -----------------------------------------

        private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            var result = dialog.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK) return;

            // Store the opened directory path for violations config loading
            _openedDirectoryPath = dialog.SelectedPath;

            Debug.WriteLine($"Loading students from: {_openedDirectoryPath}");

            ReloadSubmissionFolders();
        }

        private void ReloadSubmissionFolders()
        {
            if (string.IsNullOrWhiteSpace(_openedDirectoryPath)) return;
            var selectedFolder = (listBoxStudents.SelectedItem as Student)?.Folder;
            ClearFileTabs();
            var loaded = SubmissionFolderLoader.LoadStudents(_openedDirectoryPath,
                _assignmentPersistenceService.UseFolderNames(_currentAssignment?.Course));
            var students = loaded.Students;
            if (loaded.Warnings.Count > 0)
                MessageBox.Show(string.Join("\n", loaded.Warnings), "Submission folders", MessageBoxButton.OK, MessageBoxImage.Warning);
            listBoxStudents.Items.Clear();

            foreach (var student in students)
            {
                Debug.WriteLine($"Adding student: {student.FullName} | Folder: {student.Folder}");
                listBoxStudents.Items.Add(student);
            }

            // Clear tree view until a student is selected
            fileTreeView.Items.Clear();
            listBoxStudents.SelectedItem = students.FirstOrDefault(student =>
                string.Equals(student.Folder, selectedFolder, StringComparison.OrdinalIgnoreCase));
        }

        private void ListBoxStudents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearFileTabs();
            if (listBoxStudents.SelectedItem is not Student student) return;
            CloseSidePanel();
            var path = student.Folder;
            if (path == null) return;

            Debug.WriteLine($"Student selected: {student.FullName}, Path: {path}");

            // Populate tree view with selected student's directory structure
            PopulateTreeView(path);
            OpenSavedReviewForSelectedStudent();
        }

    }
}
