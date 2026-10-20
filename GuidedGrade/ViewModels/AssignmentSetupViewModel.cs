using GuidedGrade.Models;
using GuidedGrade.Services;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class AssignmentSetupViewModel
{
    private readonly AssignmentPersistenceService _persistence;
    internal AssignmentDetailsViewModel Details { get; } = new();
    internal AssignmentRubricViewModel Rubric { get; } = new();
    internal AssignmentDeductionsViewModel Deductions { get; } = new();
    internal AssignmentFeedbackViewModel Feedback { get; } = new();
    internal ClassSettingsViewModel Class { get; }
    internal State<int> Page { get; } = new(0);
    internal State<string> Error { get; } = new("");
    internal AssignmentSetupViewModel(AssignmentPersistenceService persistence, GradingAssignment? initial) { _persistence = persistence; Class = new(persistence); if (initial != null) Load(initial); else SetCourse("General"); }
    internal void SetCourse(string value) { Details.Course.Value = value; Class.Load(string.IsNullOrWhiteSpace(value) ? "General" : value.Trim()); }
    internal void Load(GradingAssignment value) { SetCourse(value.Course); Details.Title.Value = value.Title; Details.ReviewFiles.Value = string.Join("\n", value.ReviewFilePaths); Details.LogPath.Value = value.LogFilePath; Import(value); Error.Value = ""; }
    internal void Import(GradingAssignment value) { Details.Requirements.Value = value.Requirements; Rubric.Load(value.Rubric); Deductions.Load(value.Deductions); Feedback.Load(value.FeedbackOptions); }
    internal GradingAssignment Save()
    {
        if (string.IsNullOrWhiteSpace(Details.Title.Value)) throw new ArgumentException("Enter an assignment title.");
        var rubric = Rubric.Build();
        if (rubric.Count == 0) throw new ArgumentException("Add at least one rubric criterion.");
        var result = new GradingAssignment { Course = string.IsNullOrWhiteSpace(Details.Course.Value) ? "General" : Details.Course.Value.Trim(), Title = Details.Title.Value.Trim(), Requirements = Details.Requirements.Value, ReviewFilePaths = Details.ReviewFiles.Value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), LogFilePath = Details.LogPath.Value.Trim(), Rubric = rubric, Deductions = Deductions.Build(), FeedbackOptions = Feedback.Build() };
        _persistence.SaveAssignment(result);
        Class.Save(result.Course);
        var persisted = _persistence.LoadAssignment(result.Course, result.Title)
            ?? throw new InvalidOperationException("The saved assignment could not be reloaded.");
        Load(persisted);
        return persisted;
    }
}
