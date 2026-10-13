using GuidedGrade.Models;

namespace GuidedGrade.Services;

internal static class GradingConfirmation
{
    internal static bool IsAuthorized(LLMSettings settings, Func<bool> confirm) =>
        !settings.ConfirmGrading || confirm();
}
