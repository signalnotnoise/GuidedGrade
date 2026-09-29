using GuidedGrade.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GuidedGrade.Tests.Services;

[TestClass]
public class ConsoleInteractionTests
{
    [TestMethod]
    public void ScreenReconstructsRedrawAndSplitControlSequences()
    {
        var screen = new ConsoleScreen();
        screen.Feed("\u001b]0;private terminal title\a\u001b[2J\u001b[HOld menu: 1 Buy 2 Sell 3 Leave");
        screen.Feed("\u001b[2J\u001b[H" + string.Join('\n', Enumerable.Repeat("Inventory Health Potion 10", 16)));
        screen.Feed("\u001b[20;");
        screen.Feed("5HWhich item would you like to sell?\u001b[21;5H");
        var snapshot = screen.PromptContext();
        StringAssert.Contains(snapshot, "Which item would you like to sell?");
        Assert.IsFalse(snapshot.Contains("Old menu") || snapshot.Contains("private terminal title") || snapshot.Contains('\u001b'));
        Assert.IsTrue(screen.Snapshot().Length <= 3630);
        screen.Feed("\u001b[20;5H\u001b[KYou do not have that item!");
        StringAssert.Contains(screen.Snapshot(), "You do not have that item!");
        Assert.IsFalse(screen.Snapshot().Contains("Which item"));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("q")]
    [DataRow("{bad json}")]
    [DataRow("{\"action\":\"type\"}")]
    [DataRow("{\"action\":\"type\",\"input\":\"1\\n2\"}")]
    public void InvalidReplyStopsWithoutGuessing(string reply)
    {
        var action = ConsoleDriverAgent.Parse(reply, 9);
        Assert.AreEqual("stop", action.Action);
        Assert.AreEqual("", action.ResolvedInput);
    }

    [TestMethod]
    public void JsonBracesInReasonAndTrailingNewlineAreHandled()
    {
        var action = ConsoleDriverAgent.Parse("""{"action":"type","input":"1\n","reason":"answer the {menu} with \"1\""}""", 2);
        Assert.AreEqual("type", action.Action);
        Assert.AreEqual("1", action.ResolvedInput);
        Assert.AreEqual("1\r", InteractiveProcessSession.NormalizePtyInput(action.ResolvedInput));
        Assert.AreEqual("1\r", InteractiveProcessSession.NormalizePtyInput("1\n"));
        Assert.AreEqual("1\r", InteractiveProcessSession.NormalizePtyInput("1\r\n"));
    }

    [TestMethod]
    public void PromptKeepsNewestContextWhenHistoryExceedsBudget()
    {
        var prompt = ConsoleDriverAgent.BuildPrompt("requirements", "source",
            "OLD_FIRST_MENU" + new string('x', 18000) + "\n# CURRENT CONSOLE\nWhich item would you like to sell?", 8);
        StringAssert.Contains(prompt, "Which item would you like to sell?");
        Assert.IsFalse(prompt.Contains("OLD_FIRST_MENU"));
    }

    [TestMethod]
    public async Task CurrentItemPromptSurvivesLongHistoryAndAgentDoesNotTypeFallback()
    {
        var menu = "\u001b[2J\u001b[H" + string.Join('\n', Enumerable.Repeat(new string('x', 100), 18))
            + "\u001b[20;1H1 Buy / 2 Sell / 3 Leave";
        var item = "\u001b[2J\u001b[H" + string.Join('\n', Enumerable.Repeat(new string('y', 100), 18))
            + "\u001b[20;1HWhich item would you like to sell?";
        var session = new ScriptedSession(menu, item);
        var calls = 0;
        var result = await ConsoleDriverAgent.DriveCoreAsync(session, (context, turn, token) =>
        {
            var prompt = ConsoleDriverAgent.BuildPrompt("Test buying and selling", "source", context, turn);
            Assert.IsFalse(prompt.Contains('\u001b'));
            calls++;
            if (turn == 1) return Task.FromResult(ConsoleDriverAgent.Parse("""{"action":"type","input":"2\n"}""", turn));
            StringAssert.Contains(prompt, "Which item would you like to sell?");
            Assert.IsTrue(prompt.LastIndexOf("Which item", StringComparison.Ordinal) > prompt.IndexOf("# CURRENT CONSOLE", StringComparison.Ordinal));
            return Task.FromResult(ConsoleDriverAgent.Parse("unreadable model response", turn));
        }, 12);
        CollectionAssert.AreEqual(new[] { "2" }, session.Inputs);
        Assert.AreEqual(3, calls); // One correction attempt, without sending more input.
        Assert.IsTrue(result.StoppedByRunner);
        Assert.IsFalse(result.Crashed);
        Assert.IsTrue(result.Findings.Any(f => f.StartsWith("INCONCLUSIVE:")));
    }

    [TestMethod]
    public async Task WaitActionSendsNoInputAndCallerCancellationCleansUp()
    {
        var session = new ScriptedSession("Preparing...", "Still preparing...");
        using var cts = new CancellationTokenSource();
        var calls = 0;
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            await ConsoleDriverAgent.DriveCoreAsync(session, (_, turn, token) =>
            {
                if (++calls == 1) return Task.FromResult(new ConsoleAgentAction { Action = "wait" });
                cts.Cancel();
                return Task.FromCanceled<ConsoleAgentAction>(token);
            }, 12, cancellationToken: cts.Token));
        Assert.AreEqual(0, session.Inputs.Count);
        Assert.IsTrue(session.HasExited);
    }

    [TestMethod]
    public async Task MenuLabelBecomesOnlyItsKeyAndBlankRedrawDoesNotTriggerInput()
    {
        var session = new ScriptedSession("\u001b[2J\u001b[H1) Buy\n2) Sell\n3) Leave\n_",
            "\u001b[2J\u001b[H", "\u001b[2J\u001b[HWhich item would you like to buy?\n_");
        var turns = new List<int>();
        await ConsoleDriverAgent.DriveCoreAsync(session, (_, turn, _) =>
        {
            turns.Add(turn);
            return Task.FromResult(new ConsoleAgentAction
            {
                Action = turn >= 3 ? "stop" : "type", Input = "Health Potion"
            });
        }, 8);
        CollectionAssert.AreEqual(new[] { "1", "Health Potion" }, session.Inputs);
        CollectionAssert.AreEqual(new[] { 2, 3 }, turns); // Menu is deterministic; blank redraw uses no action turn.
    }

    [TestMethod]
    public async Task HighScoreBatchIsCorrectedBeforeAnyInputIsSent()
    {
        const string question = "Enter each score you want to save.";
        var source = new string(' ', 6000) + "\nstd::cout << \"" + question + "\";\ngetline(std::cin, value);\nint score = stoi(value);";
        var excerpt = ConsoleSourceContext.Build(source);
        StringAssert.Contains(excerpt, "getline");
        StringAssert.Contains(excerpt, "stoi");
        Assert.IsTrue(ConsoleInputPolicy.ExpectsInteger(question, source));
        var session = new ScriptedSession(question);
        var calls = 0;
        await ConsoleDriverAgent.DriveCoreAsync(session, (context, turn, _) =>
        {
            calls++;
            if (turn > 1) return Task.FromResult(new ConsoleAgentAction { Action = "stop" });
            if (calls == 2)
            {
                Assert.AreEqual(0, session.Inputs.Count);
                StringAssert.Contains(context, "one integer per line");
            }
            return Task.FromResult(new ConsoleAgentAction { Input = calls == 1 ? "10 20 30 40 50" : "10" });
        }, 8, programSource: source);
        CollectionAssert.AreEqual(new[] { "10" }, session.Inputs);
    }

    [TestMethod]
    public async Task InvalidJsonIsRetriedOnceWithoutTypingTheInvalidReply()
    {
        var session = new ScriptedSession("Your name?");
        var calls = 0;
        await ConsoleDriverAgent.DriveCoreAsync(session, (context, turn, _) =>
        {
            calls++;
            if (turn > 1) return Task.FromResult(new ConsoleAgentAction { Action = "stop" });
            return Task.FromResult(ConsoleDriverAgent.Parse(calls == 1 ? "not JSON" : "{\"action\":\"type\",\"input\":\"Test\"}", turn));
        }, 8);
        CollectionAssert.AreEqual(new[] { "Test" }, session.Inputs);
        Assert.AreEqual(3, calls);
    }

    [TestMethod]
    public void MenuPolicyAcceptsFirstOfFourOptionsAndLeavesItemNamesAlone()
    {
        var screen = new ConsoleScreen();
        screen.Feed("Welcome\n1. Create Vector\n2. Enter High Scores\n3. Print High Scores\n4. Leave\nOption Choice: ");
        Assert.IsTrue(ConsoleInputPolicy.TryResolve("1. Create Vector", screen.CursorContext(), out var value, out _));
        Assert.AreEqual("1", value);
        Assert.IsTrue(ConsoleInputPolicy.TryResolve("Health Potion", "Type the item name:", out value, out _));
        Assert.AreEqual("Health Potion", value);
        Assert.IsFalse(ConsoleInputPolicy.TryResolve("1) Buy extra", "1) Buy\n2) Sell\n3) Leave", out _, out _));
    }

    [TestMethod]
    public async Task RepeatedQuestionCollectsFiveAnswersWithoutFalseLoopStop()
    {
        var session = new ScriptedSession("Enter each score you want to save.");
        var result = await ConsoleDriverAgent.DriveCoreAsync(session, (_, turn, _) =>
            Task.FromResult(new ConsoleAgentAction { Action = turn > 5 ? "stop" : "type", Input = "10" }), 8);
        Assert.AreEqual(5, session.Inputs.Count);
        Assert.IsFalse(result.Findings.Any(f => f.Contains("repeated console output", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void SolutionContextDiscoversInputHelperWithoutCheckedFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "GuidedGradeTests", Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "Code");
        Directory.CreateDirectory(nested);
        try
        {
            var primary = Path.Combine(nested, "main.cpp");
            File.WriteAllText(primary, "#include \"Methods.hpp\"\nint main() { return 0; }");
            File.WriteAllText(Path.Combine(nested, "Methods.hpp"), "void ReadScore() { getline(std::cin, value); auto score = stoi(value); }");
            var submission = new RunnableSubmission
            {
                Kind = SubmissionKind.VisualStudioSolution, RootDirectory = root,
                EntryPath = Path.Combine(root, "Test.sln"), SourceFiles = new List<string> { primary }
            };
            var source = SubmissionExecutionService.BuildPlannerSource(submission, null, null);
            StringAssert.Contains(source, "Methods.hpp");
            StringAssert.Contains(ConsoleSourceContext.Build(source), "stoi(value)");
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task StalledModelIsCancelledAndStudentStoppedWithoutInputOrFailure()
    {
        var session = new ScriptedSession("Your name?");
        CancellationToken requestToken = default;
        var pending = new TaskCompletionSource<ConsoleAgentAction>();
        var result = await ConsoleDriverAgent.DriveCoreAsync(session, (_, _, token) =>
        {
            requestToken = token;
            return pending.Task; // Even an uncooperative provider must not hold the runner open.
        }, 8, modelResponseTimeout: TimeSpan.FromMilliseconds(50));
        Assert.IsTrue(requestToken.IsCancellationRequested);
        Assert.IsTrue(session.HasExited);
        Assert.IsTrue(result.StoppedByRunner);
        Assert.AreEqual(0, session.Inputs.Count);
        Assert.IsFalse(result.TimedOut); // Separate from the overall interaction budget.
        Assert.IsFalse(result.Findings.Any(f => f.StartsWith("FAILURE:") || f.StartsWith("CRASH:")));
        StringAssert.Contains(result.Transcript, "[agent waiting]");
        StringAssert.Contains(result.Transcript, "[model timeout]");
        Assert.IsTrue(result.Findings.Any(f => f.Contains("model timeout")));
        pending.SetResult(new ConsoleAgentAction { Input = "late" });
        Assert.AreEqual(0, session.Inputs.Count);
    }

    [TestMethod]
    public async Task SentInputsSurviveLargeScreenRedrawsInDecisionContext()
    {
        var session = new ScriptedSession("Your name?", string.Join("\n", Enumerable.Repeat(new string('X', 100), 29)));
        await ConsoleDriverAgent.DriveCoreAsync(session, (context, turn, _) =>
        {
            if (turn == 1) return Task.FromResult(new ConsoleAgentAction { Input = "TestName" });
            StringAssert.Contains(context, "# INPUTS ALREADY SENT");
            StringAssert.Contains(context, "Turn 1: TestName");
            return Task.FromResult(new ConsoleAgentAction { Action = "stop" });
        }, 2);
    }

    [TestMethod]
    public async Task MenuCoverageSelectsBuySellThenLeaveAndUsesModelOnlyForItems()
    {
        const string menu = "\u001b[2J\u001b[H1) Buy\n2) Sell\n3) Leave\n_";
        var session = new ScriptedSession(menu, "\u001b[2J\u001b[HWhich item to buy?", menu,
            "\u001b[2J\u001b[HWhich item to sell?", menu);
        var calls = 0;
        var result = await ConsoleDriverAgent.DriveCoreAsync(session, (_, _, _) =>
        {
            calls++;
            return Task.FromResult(new ConsoleAgentAction { Input = "Health Potion" });
        }, 5);
        CollectionAssert.AreEqual(new[] { "1", "Health Potion", "2", "Health Potion", "3" }, session.Inputs);
        Assert.AreEqual(2, calls);
        StringAssert.Contains(result.Transcript, "Selected: 3: Leave");
    }

    [TestMethod]
    public void MenuCoverageDoesNotOverrideFollowupOrShareSelectionsAcrossMenus()
    {
        var coverage = new ConsoleMenuCoverage();
        Assert.AreEqual("1", coverage.Choose("1) Buy\n2) Leave")!.Input);
        coverage.RecordSent();
        Assert.IsNull(coverage.Choose("1) Buy\n2) Leave\nWhich item name?"));
        Assert.AreEqual("1", coverage.Choose("1) Print\n2) Back")!.Input);
        Assert.IsTrue(coverage.Summary().Any(x => x.Contains("UNTESTED: 2: Leave")));
    }

    [TestMethod]
    public async Task LongMenuIncludesEarlyOptionsAndLeavesLast()
    {
        var menu = "\u001b[2J\u001b[HMain Menu\n" + string.Join("\n", Enumerable.Range(1, 10).Select(i => $"{i}) Action {i}")) + "\n11) Exit\nOption Choice: >";
        var session = new ScriptedSession(menu);
        await ConsoleDriverAgent.DriveCoreAsync(session, (_, _, _) => throw new AssertFailedException("Menu must be planned"), 11);
        CollectionAssert.AreEqual(Enumerable.Range(1, 11).Select(i => i.ToString()).ToArray(), session.Inputs);
    }

    [TestMethod]
    public void IdenticalOptionsUnderDifferentHeadingsHaveIndependentCoverage()
    {
        var coverage = new ConsoleMenuCoverage();
        Assert.AreEqual("1", coverage.Choose("Books\n1) Add\n2) Back\nChoice:")!.Input);
        coverage.RecordSent();
        Assert.AreEqual("1", coverage.Choose("Movies\n1) Add\n2) Back\nChoice:")!.Input);
        coverage.RecordSent();
        Assert.AreEqual("2", coverage.Choose("Books\n1) Add\n2) Back\nChoice:")!.Input);
    }

    [TestMethod]
    public async Task ContinuousOutputWithoutInputStillStopsAtObservationLimit()
    {
        var session = new ScriptedSession("\u001b[2J\u001b[HEnter a value:") { Flowing = true };
        var calls = 0;
        var result = await ConsoleDriverAgent.DriveCoreAsync(session, (_, turn, _) =>
        {
            calls++;
            return Task.FromResult(new ConsoleAgentAction { Action = calls == 1 ? "wait" : turn <= 15 ? "type" : "stop", Input = "10" });
        }, 64);
        // A flowing observation before the first input would trip the no-progress guard;
        // this case separately verifies that every successful input resets that guard.
        Assert.AreEqual(0, session.Inputs.Count);
        Assert.IsTrue(result.Findings.Any(x => x.Contains("continuous output")));
    }

    [TestMethod]
    public async Task ContinuousRedrawWithProgressDoesNotStopAtSecondWindow()
    {
        var session = new ScriptedSession("\u001b[2J\u001b[HEnter a value:") { Flowing = true };
        await ConsoleDriverAgent.DriveCoreAsync(session, (_, turn, _) => Task.FromResult(new ConsoleAgentAction
        { Action = turn <= 15 ? "type" : "stop", Input = "10" }), 64);
        Assert.AreEqual(15, session.Inputs.Count);
    }

    [TestMethod]
    public async Task ModelWaitDoesNotConsumeInputBudget()
    {
        var session = new ScriptedSession("Enter a value:");
        var calls = 0;
        await ConsoleDriverAgent.DriveCoreAsync(session, (_, _, _) => Task.FromResult(new ConsoleAgentAction
        { Action = ++calls == 1 ? "wait" : "type", Input = "10" }), 1);
        Assert.AreEqual(1, session.Inputs.Count);
        Assert.AreEqual(2, calls);
    }

    private sealed class ScriptedSession(params string[] output) : IInteractiveConsoleSession
    {
        private int _poll;
        public bool Flowing { get; init; }
        public List<string> Inputs { get; } = new();
        public bool Started => true;
        public string? Error => null;
        public bool HasExited { get; private set; }
        public int? ExitCode => HasExited ? -1 : null;
        public string StandardOutput { get; private set; } = "";
        public string StandardError => "";
        public Task<ConsoleSlice> WaitForIdleAsync(TimeSpan idle, TimeSpan window, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StandardOutput = output[Math.Min(_poll++, output.Length - 1)];
            return Task.FromResult(new ConsoleSlice { StandardOutput = StandardOutput, Exited = HasExited, OutputStillFlowing = Flowing });
        }
        public Task WriteInputAsync(string text, CancellationToken cancellationToken = default) { Inputs.Add(text); return Task.CompletedTask; }
        public void CloseInput() { }
        public void Kill() => HasExited = true;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
