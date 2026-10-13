using GuidedGrade.Services;
using UI_Framework;
namespace GuidedGrade.ViewModels;
internal sealed class ClassSettingsViewModel(AssignmentPersistenceService persistence)
{
    internal State<bool> UseFolderNames { get; } = new(false);
    internal State<string> Course { get; } = new("");
    internal State<string> ReviewRules { get; } = new("");
    internal void Load(string course) { Course.Value = course.Trim(); UseFolderNames.Value = persistence.UseFolderNames(course); ReviewRules.Value = persistence.LoadCourseReviewRules(course); }
    internal void AddCppRules() { if (!Course.Value.Equals("PG2", StringComparison.OrdinalIgnoreCase)) return; ReviewRules.Value = "No lambda expressions are permitted.\nFunctions require declarations in headers and definitions in .cpp files, except getters and setters.\nStarting with Part B, and for subsequent coursework, use references and const correctly according to the assignment requirements. Do not apply this rule to Part A-only work."; }
    internal void Save(string course) { persistence.SaveFolderNames(course, UseFolderNames.Value); persistence.SaveCourseReviewRules(course, ReviewRules.Value); Load(course); }
}
