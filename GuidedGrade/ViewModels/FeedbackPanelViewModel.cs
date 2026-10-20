using UI_Framework;
using GuidedGrade.Views;
namespace GuidedGrade.ViewModels;
internal sealed class FeedbackPanelViewModel(string studentName, string assignmentTitle, Func<View> savedReviews, Binding<string> draft, State<double> height, State<int> format, State<string> copyLabel, State<bool> showResults, GradingView results, Action copy, Action export, Func<View> earlierReviews)
{
 internal Func<View> EarlierReviews { get; } = earlierReviews;
 internal string StudentName { get; } = studentName;
 internal string AssignmentTitle { get; } = assignmentTitle;
 internal Func<View> SavedReviews { get; } = savedReviews;
 internal Binding<string> Draft { get; } = draft;
 internal State<double> Height { get; } = height;
 internal State<int> Format { get; } = format;
 internal State<string> CopyLabel { get; } = copyLabel;
 internal State<bool> ShowResults { get; } = showResults;
 internal GradingView Results { get; } = results;
 internal Action Copy { get; } = copy;
 internal Action Export { get; } = export;
}
