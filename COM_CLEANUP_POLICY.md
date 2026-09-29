# Application-owned COM cleanup

The application creates one packaged `WpfComCleanupPolicy` on its STA before
constructing `MainWindow`. The policy is supplied by
`SignalNotNoise.UI.Wpf` `0.1.0-alpha.3-local.2`; the previous app-local prototype
has been removed.

Dispatcher operation completion coalesces cleanup requests at ContextIdle.
`MainWindow.Closed` disposes shell, panel, grading, and terminal owners before
`App.OnExit` drains cleanup and detaches its hook. Cleanup failures remain counted
after recovery, are recorded in
`%LOCALAPPDATA%\GuidedGrade\Diagnostics\com-cleanup.log`, and cause a
nonzero exit code. Logging does not open a modal dialog from a cleanup callback.
Before the renamed diagnostics path is used, `AppDataPaths` migrates both local
legacy roots: `LabFeedbackWPF` and `Lab Feedback WPF`. The spaced root contains
the old `Diagnostics/com-cleanup.log`. Existing GuidedGrade files take precedence;
conflicting legacy logs remain in their original locations and are traced.

Construction disables CLR eager COM-wrapper cleanup for the application STA.
That setting cannot be reversed during the thread's lifetime. Input methods and
accessibility remain enabled.

## Interactive acceptance still required

Run these against a disposable demo copy:

- Type, select, replace, undo and redo in editable feedback and assignment fields.
  Switch panels and files; confirm selection, caret and retained editor state.
- Use the installed IME to start, update, commit and cancel composition; switch
  input languages mid-session. Confirm no duplicated, missing or premature text.
- With the actual screen reader, navigate fields and controls, read their names,
  change checkbox state and confirm focus and text-change announcements.
- Open and close review panels and windows repeatedly, then close the app. Confirm
  normal exit, no cleanup error log and no lingering app process.

The diagnostic consumer comparison uses isolated synthetic data: 1,000 students,
1,000 files, 1,000 editor lines, and 50 layout operations. It records
whole-process duration including shutdown separately from UI-thread update
measurements. That layout workload does not establish typing, IME, or
screen-reader behavior.

## September 26 automated results

The application Release build completed with zero warnings and all 189 tests
passed after replacing the local policy with the packaged API.

The 15-sample package comparison between `0.1.0-alpha.3-local.1` and
`0.1.0-alpha.3-local.2` passed all 28 checks. Window and splitter mount medians
rose 4.37% and 5.19%, within the unchanged 10% budget. Window, splitter, and
virtualized-tree updates improved 1.37%, 2.61%, and 2.02%; full-tree updates rose
1.53%. Allocation checks passed, and all 128 measured/warmup processes completed.

The framework's seven-sample release gate also passed all 32 metrics against the
published baseline, including editor update time -2.08% and UI-thread allocation
-3.29%. The standalone native reproduction reduced final updates from
12.73-13.38 seconds to 0.19-0.22 seconds with the packaged policy.

Evidence is retained in the framework repository under
`docs/performance-evidence/2026-09-26-editor-cleanup-release`.
