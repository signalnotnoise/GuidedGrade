using GuidedGrade.Models;

namespace GuidedGrade.Services;

/// <summary>
/// Local execution requires saved authorization or fresh user consent; isolation failures never authorize it.
/// </summary>
internal static class SubmissionExecutionPolicy
{
    public const string LocalWarning =
        "This will build and run student code directly on your computer with your Windows permissions. " +
        "Build scripts and programs can read, change, or delete your files and access your network. " +
        "Copying the submission does not isolate it. Only continue if you trust this submission.\n\nContinue locally?";

    public static bool IsLocalAuthorized(SubmissionExecutionMode mode, Func<string, bool>? confirm, string path)
        => mode == SubmissionExecutionMode.Local && confirm?.Invoke(path + "\n\n" + LocalWarning) == true;

    public static bool IsLocalAuthorized(LLMSettings settings, Func<string, bool>? confirm, string path)
        => settings.ExecutionMode == SubmissionExecutionMode.Local &&
           (!settings.ConfirmLocalExecution || IsLocalAuthorized(settings.ExecutionMode, confirm, path));

    public const string LocalDeclined = "Local execution was not authorized. No student code was executed.";
    public const string NoRuntimeDeduction = "Runtime testing is unavailable or incomplete. Do not deduct points for this; grade from source only.";
}
