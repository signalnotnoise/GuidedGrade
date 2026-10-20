using GuidedGrade.Services;
namespace GuidedGrade.ViewModels;
internal sealed class GradeBadgeViewModel(StudentGrade? grade, string label)
{
 internal string Text => label + " " + (grade == null ? "—" : $"{grade.Percentage:0.#}%");
 internal string Color => grade == null ? "#AAB8C8" : "#83DECF";
 internal string Background => grade == null ? "#29323E" : "#173C3B";
}
