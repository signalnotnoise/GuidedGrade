# Experimental application-owned COM cleanup

The application keeps normal CLR cleanup by default. To evaluate the alternative,
launch `Lab Feedback WPF.exe --experimental-com-cleanup`. Close and restart without
that argument to return to normal behavior: the CLR setting cannot be reversed on
a running UI thread. No framework package or package pin changes are required.

`App` owns the policy before constructing `MainWindow`. Dispatcher operation
completion coalesces requests at ContextIdle. `MainWindow.Closed` disposes the
shell, panel, grading and terminal owners before `App.OnExit` drains cleanup and
detaches its hook. Cleanup failures are counted even after recovery, recorded in
`%LOCALAPPDATA%\Lab Feedback WPF\Diagnostics\com-cleanup.log`, and cause a nonzero
exit code. Logging does not open a modal dialog in a cleanup callback.

The implementation is an application adaptation of ui-framework commit c39d4ae,
`samples/ComCleanupLifecycle/ApplicationComCleanupPolicy.cs`, adding persistent
failure counting and error reporting. It is experimental, not a package API or a
performance-cleared production default. Neither input methods nor accessibility
are disabled. Automated editor tests are not a substitute for actual composition
and assistive-technology testing.

## Interactive acceptance (pending)

Run these in both ordinary and experimental modes against a disposable demo copy:

- Type, select, replace, undo and redo in editable feedback and assignment fields.
  Switch panels and files; confirm selection, caret and retained editor state.
- Use the installed IME to start, update, commit and cancel composition; switch
  input languages mid-session. Confirm no duplicated, missing or premature text.
- With the actual screen reader, navigate fields and controls, read their names,
  change checkbox state and confirm focus and text-change announcements.
- Open and close review panels and windows repeatedly, then close the app. Confirm
  normal exit, no cleanup error log and no lingering app process.

The diagnostic consumer comparison uses isolated synthetic data (1,000 students,
1,000 files, 1,000 editor lines and 50 layout operations). It compares the same
application-owned policy on/off and records whole-process duration including
shutdown separately from UI-thread update measurements. That layout workload does
not establish a typing/IME speedup or replace the framework release gate.

## September 25 automated results

All 190 consumer tests passed, including the new cleanup failure-history and
initialized native TextBox selection/undo checks. The initial test's unattached
TextBox did not record undo; attaching it to a native source and grouping the edit
made the fixture exercise initialized WPF behavior. This is programmatic editing,
not an assertion that real keyboard input or IME composition was tested.

The actual-consumer comparison completed 16 fresh processes: three alternating
measured pairs plus one warmup pair in each of clean and probe modes. In clean
mode, default/policy startup medians were 646.79/634.73 ms, update medians
640.89/665.27 ms (+3.80%), UI-thread update allocations 62,111,976/62,172,968 bytes
(+0.10%), and whole-process medians including shutdown 1,741.47/1,747.51 ms
(+0.35%). Both realized 15 students and 19 files and passed scrolling, file-state,
selection and retained-document checks. No process reported a cleanup failure.

Probe-mode policy startup was substantially slower (1,204.94 versus 657.73 ms);
its update medians were 693.79 versus 697.39 ms with overlapping ranges. These
instrumented results are not a clean speed estimate. The policy is therefore
kept default-off; this consumer resizing workload does not reproduce the separate
1,000-TextBox replacement stall and does not establish an app-wide speedup.

Raw evidence and exact source snapshots are retained in the framework workspace
at `artifacts/consumer-cleanup-comparison-2026-09-25`, with a reviewable copy in
`docs/performance-evidence/2026-09-25-consumer-cleanup`. The original framework
baseline, release budgets and consumer package pin are unchanged.
