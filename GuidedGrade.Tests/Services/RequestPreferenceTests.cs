using System.Text.Json;
using GuidedGrade.Models;
using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class RequestPreferenceTests
{
    [TestMethod]
    public void SavedPreferencesRoundTripAndOlderSettingsKeepDefaults()
    {
        var settings = new LLMSettings { ConsoleModelWaitSeconds = 75, ConfirmGrading = false, ConfirmLocalExecution = false };
        var saved = JsonSerializer.Serialize(settings);
        var loaded = JsonSerializer.Deserialize<LLMSettings>(saved)!;
        Assert.AreEqual(TimeSpan.FromSeconds(75), loaded.ConsoleModelWaitTimeout);
        Assert.IsFalse(loaded.ConfirmGrading);
        Assert.IsFalse(loaded.ConfirmLocalExecution);
        var legacy = JsonSerializer.Deserialize<LLMSettings>("{}")!;
        Assert.AreEqual(TimeSpan.FromSeconds(30), legacy.ConsoleModelWaitTimeout);
        Assert.IsTrue(legacy.ConfirmGrading);
        Assert.IsTrue(legacy.ConfirmLocalExecution);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(91)]
    [DataRow(int.MaxValue)]
    public void InvalidSavedTimeoutFallsBackToThirtySeconds(int seconds) =>
        Assert.AreEqual(TimeSpan.FromSeconds(30), new LLMSettings { ConsoleModelWaitSeconds = seconds }.ConsoleModelWaitTimeout);

    [TestMethod]
    public void GradingConfirmationCanBeSkippedAcceptedOrDeclined()
    {
        var settings = new LLMSettings { ConfirmGrading = false };
        Assert.IsTrue(GradingConfirmation.IsAuthorized(settings, () => throw new AssertFailedException("Must not prompt.")));
        settings.ConfirmGrading = true;
        var calls = 0;
        Assert.IsFalse(GradingConfirmation.IsAuthorized(settings, () => { calls++; return false; }));
        Assert.AreEqual(1, calls);
        Assert.IsTrue(GradingConfirmation.IsAuthorized(settings, () => true));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BuildAndRunRespectSavedLocalConfirmation(bool confirm)
    {
        var settings = new LLMSettings { ExecutionMode = SubmissionExecutionMode.Local, ConfirmLocalExecution = confirm };
        var calls = 0;
        var service = new SubmissionExecutionService(settings, _ => { calls++; return false; });
        // An absent submission verifies authorization without launching student code.
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.sln");
        var build = await service.BuildOnlyAsync(path);
        var run = await service.LaunchAsync(path);
        Assert.AreEqual(confirm ? 2 : 0, calls);
        Assert.AreEqual(confirm, build == SubmissionExecutionPolicy.LocalDeclined);
        Assert.AreEqual(confirm, run == SubmissionExecutionPolicy.LocalDeclined);
    }
}
