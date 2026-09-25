# XAML removal — completed September 23, 2026

The main task completed the previously handed-off shell conversion. No application-owned
`.xaml` files remain, and the application does not load embedded XAML strings.

| Former source | Implemented replacement |
| --- | --- |
| App.xaml / App.xaml.cs | App.cs: explicit STA entry point, main window creation, shutdown policy. |
| MainWindow.xaml / MainWindow.xaml.cs | MainWindow.cs, MainWindow.Framework.cs and MainWindow.NativeControls.cs: owned controls/events and declarative toolbar, pickers, file tabs, empty state, panel actions and status. |
| Views/GradeView.xaml / GradeView.xaml.cs | Views/GradingView.cs: declarative grading, deductions and remarks with retained specialized editors/sliders. |
| Presentation/NativeTheme.xaml | Presentation/NativeTheme.cs: scoped code-built native templates/resources, applied by ReviewTheme. |

The pinned framework has no vertical fill, splitter/docking, horizontal-scroll, tree/menu
or progress primitives. Focused native layout adapters and specialist controls preserve these
behaviors. This is a framework composition with explicit native boundaries, not a replacement
renderer. Framework API gaps were reported to **Find better Windows projects**
(`01a0b5ac-e353-71c0-99ee-be4bb74a6007`). Package version/bytes remain unchanged; local.3
is still a local integration package with the documented failing allocation performance gate.

Validation: warning-as-error build and all 187 tests pass in ShellMigration. Tests cover source
navigation, overall-review persistence and Clear review, explicit file/solution menus, draft and
console retention, independent file-tab closure, grading controls and disposal. Actual-control
offscreen captures are in artifacts/ui-migration, including populated demo files/feedback,
expanded reviews, file/solution menus and grading results. These use fictitious demo content
and are not live desktop screenshots or a complete keyboard/IME/accessibility audit.

The `.xshd` files remain because the source and HTML editors actively use them. Runtime scripts,
package provenance/history, user databases and unrelated working changes are preserved.
