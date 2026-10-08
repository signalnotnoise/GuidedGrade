using UI_Framework;
using System.Windows.Controls;
namespace GuidedGrade.ViewModels;
internal sealed class WorkspaceToolbarViewModel(StateList<string> courses, StateList<string> assignments, State<int> courseIndex, State<int> assignmentIndex, Func<Menu> menu, Action<int> selectCourse, Action<int> selectAssignment, Action assignment, Action open, Action rubric, Action feedback, Action batch, Func<View> panelTabs, Func<View> reviewMenu)
{
 internal StateList<string> Courses { get; } = courses;
 internal StateList<string> Assignments { get; } = assignments;
 internal State<int> CourseIndex { get; } = courseIndex;
 internal State<int> AssignmentIndex { get; } = assignmentIndex;
 internal Func<Menu> Menu { get; } = menu;
 internal Action<int> SelectCourse { get; } = selectCourse;
 internal Action<int> SelectAssignment { get; } = selectAssignment;
 internal Action Assignment { get; } = assignment;
 internal Action Open { get; } = open;
 internal Action Rubric { get; } = rubric;
 internal Action Feedback { get; } = feedback;
 internal Func<View> ReviewMenu { get; } = reviewMenu;
 internal Func<View> PanelTabs { get; } = panelTabs;
 internal Action Batch { get; } = batch;
}
