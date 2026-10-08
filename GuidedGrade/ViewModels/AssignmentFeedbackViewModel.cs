using GuidedGrade.Models;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class AssignmentFeedbackViewModel
{
    internal State<int> Detail { get; } = new(3);
    internal State<string> ReadingLevel { get; } = new("High school");
    internal State<bool> Direct { get; } = new(true);
    internal State<bool> Breakdown { get; } = new(true);
    internal State<bool> Deductions { get; } = new(true);
    internal State<bool> Grade { get; } = new(true);
    internal State<bool> Feedback { get; } = new(true);
    internal void Load(AssignmentFeedbackOptions value) { Detail.Value = Math.Clamp(value.DetailLevel, 1, 5); ReadingLevel.Value = value.ReadingLevel; Direct.Value = value.AddressDirectly; Breakdown.Value = value.IncludeScoreBreakdown; Deductions.Value = value.IncludeDeductions; Grade.Value = value.IncludeFinalGrade; Feedback.Value = value.IncludeFeedback; }
    internal AssignmentFeedbackOptions Build() => new() { DetailLevel = Detail.Value, ReadingLevel = ReadingLevel.Value, AddressDirectly = Direct.Value, IncludeScoreBreakdown = Breakdown.Value, IncludeDeductions = Deductions.Value, IncludeFinalGrade = Grade.Value, IncludeFeedback = Feedback.Value };
}
