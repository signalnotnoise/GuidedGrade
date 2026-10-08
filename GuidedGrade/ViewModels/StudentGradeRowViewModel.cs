using GuidedGrade.Services;
namespace GuidedGrade.ViewModels;
internal sealed class StudentGradeRowViewModel(string name, StudentGrade? assignment, StudentGrade? total)
{
 internal string Name { get; } = name;
 internal StudentGrade? Assignment { get; } = assignment;
 internal StudentGrade? Total { get; } = total;
}
