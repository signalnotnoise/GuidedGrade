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
        private void LoadSavedCourseOptions()
        {
            var courses = _assignmentPersistenceService.GetCourseNames();
            _courses.ReplaceAll(courses);

            if (courses.Count > 0)
            {
                SelectCourse(0);
            }
        }

        private void LoadSavedAssignmentsForCourse(string course)
        {
            _assignments.Clear();
            _assignmentIndex.Value = -1;
            if (string.IsNullOrWhiteSpace(course))
                return;

            var assignments = _assignmentPersistenceService.GetAssignmentsByCourse(course);
            foreach (var assignment in assignments)
            {
                _assignments.Add(assignment.Title);
            }

            if (_assignments.Count > 0)
            {
                SelectAssignment(0);
            }
        }

        private void SelectCourse(int index)
        {
            _courseIndex.Value = index;
            if (index < 0 || index >= _courses.Count) return;
            LoadSavedAssignmentsForCourse(_courses[index]);
            ReloadSubmissionFolders();
        }

        private void SelectAssignment(int index)
        {
            _assignmentIndex.Value = index;
            if (index < 0 || index >= _assignments.Count || _courseIndex.Value < 0 || _courseIndex.Value >= _courses.Count) return;
            var selectedTitle = _assignments[index];
            var course = _courses[_courseIndex.Value];

            var assignment = _assignmentPersistenceService.LoadAssignment(course, selectedTitle);
            if (assignment == null)
                return;

            SetReviewAssignment(assignment);
            CloseSidePanel();

        }

        // --- Theme ------------------------------------------------------------

    }
}
