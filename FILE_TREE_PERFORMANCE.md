# File-tree performance, September 24, 2026

The submitted-files tree now uses recycling virtualization at the root and nested folder levels. FileSystemItem retains selection, expansion and analysis-checkbox state through bindings. The selected-file handler skips reopening an already-active path so restoring a recycled row does not reset the AvalonEdit document, caret or loaded review state.

## Measurements

Seven alternating fresh-process pairs compared this app's normal virtualized tree with the same final app forced to render the full tree. Both retain 1,000 logical students, 1,000 files and a 1,000-line editor through 50 identical window/navigation-pane resizes. Each side has one discarded warmup. A separate seven-pair instrumented campaign retains layout attribution. The diagnostic constructs the actual MainWindow with synthetic data and separate test databases in a hidden WPF window source.

| Clean median | Full tree | Virtualized | Change |
| --- | ---: | ---: | ---: |
| Startup/setup time | 1,847.18 ms | 722.04 ms | -60.91% |
| Startup/setup UI-thread allocations | 113,812,896 bytes | 14,851,112 bytes | -86.95% |
| 50 update operations | 925.78 ms | 735.10 ms | -20.60% |
| Update UI-thread allocations | 82,221,896 bytes | 62,088,376 bytes | -24.49% |
| Realized file rows | 1,000 | 19 | all logical files retained |

Update ranges were 848.69–1,337.88 ms full and 618.62–797.99 ms virtualized. Startup includes app construction, synthetic data, SQLite schema initialization and layout; it is not a prediction for every user project. Component body counts were not instrumented. File arrangement decreased substantially, while measurement bookkeeping and the explicit-layout phase increased. Total update time and allocations improved; virtualization is not cost-free.

[Raw reports, source manifest and summary](vendor/ui-framework/performance/file-tree-2026-09-24/summary.json) retain the final integrated comparison. Full source snapshots and the earlier failed experiments are also retained in the UI Framework workspace under artifacts/performance. The framework repository's tools/Test-ConsumerLayout.ps1 reproduces the comparison with -ConsumerProject, -CompareFileVirtualization, -Samples 7 and a fresh -OutputDirectory.

## Regression coverage

The full app suite passed 188 tests. FileTreeVirtualizationTests covers bounded realization, nested folder expansion/collapse, selected rows scrolled away and back, checkbox state and recycled-row isolation, UI Automation selection/expansion patterns, opening a real temporary file, retained document/caret state and host disposal. The focused test was then strengthened with 302 children in an expanded nested folder and passed again. These automated checks do not constitute a manual keyboard/screen-reader audit.

Simply enabling virtualization lost the selected row's visual state; binding IsSelected to the model fixed it. Restoring that selection could reopen the active file and reset the caret; the active-path guard fixed that separately. Neither failing candidate is the delivered behavior.

The file-tree benchmark itself did not change framework package bytes and used SignalNotNoise.UI.Wpf 0.1.0-alpha.2-local.3; the application now pins 0.1.0-alpha.3-local.2 for the separately validated cleanup integration. These file-tree results demonstrate an application control optimization, not public NuGet release clearance. See vendor/ui-framework/VALIDATION.md for the later editor-stall validation and remaining manual acceptance checks.
