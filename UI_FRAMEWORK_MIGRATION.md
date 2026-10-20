# UI framework migration

The application consumes SignalNotNoise.UI.Wpf and SignalNotNoise.UI from a pinned,
local prerelease feed in `vendor/ui-framework`. `NuGet.Config` includes this feed
and NuGet.org; no absolute reference to a developer's framework checkout is required.
The old WPF-UI package, FluentWindow chrome and its application dictionaries are removed.
The custom framework still uses WPF as its Windows renderer.

## Declarative surfaces

- Main workspace: C#-owned controls, declarative toolbar/course and assignment pickers, keyed file tabs, empty state, panel navigation, tool actions and status. Native layout adapters provide constrained vertical fill, horizontal tab scrolling and splitters, which the pinned framework cannot express.
- Settings: bound check patterns, violation terms, execution environment and panel visibility.
- AI-provider settings: bound provider selection, masked API keys, model refresh, connection test,
  execution configuration and requirements.
- Assignment setup and rubric import: retained inputs, editable rubric rows, explicit saved-assignment
  selection, finite positive point validation, isolated editing until Save.
- Section grading: named section and rubric toggles.
- Inline review cards: expandable feedback, read-only suggested code, approve/regenerate/reject events.
- Rubric and editable feedback panels: retained drafts, copy formats and export.
- Job queue: keyed virtualized rows, selected result, cancellation and free-form requests.
- Extraction: declarative operation cards with retained native progress bars and reliable cancellation.

`ReviewTheme` supplies the app palette and explicit inherited host colors.
`ReviewWindow` owns/disposes its ViewHost. MainWindow owns its panel hosts and disposes them on
replacement or close. Removing an inline review disposes its host; closing a tab alone does not
delete saved feedback. Domain services and database schemas are unchanged by the UI conversion.

## Native islands

AvalonEdit, the file tree/context menus, resize splitters, pinned workspace tabs, colored bounded
RichTextBox console, student/violation lists and progress bars remain native WPF inside the framework-owned workspace/window hosts. Programming results use the declarative GradingView, retaining native HTML editors and score sliders. Native TabControl retains independent panel content; the visible navigation uses framework buttons.
They retain existing selection, keyboard, editing, automation and output-retention behavior.
They are deliberate interoperability boundaries, not new framework primitives. Do not replace
these with simplified mock controls merely to remove XAML.

## Framework coordination

Report framework bugs and missing features to the task **Find better Windows projects**
(`01a0b5ac-e353-71c0-99ee-be4bb74a6007`) in the CUI project. The user explicitly requested this
coordination. Reports sent during migration:

- Native-control hosting/lifetime: implemented by the framework task and consumed through WpfUI.Native in local.2.
- Editable Picker: app currently pairs text fields with saved-option pickers.
- TextField Enter/submit callback: Settings routes Enter through WPF keyboard events.
- Tree/context menu/splitter/docking/progress primitives, vertical fill, horizontal scrolling and tooltip modifiers: reported missing; focused native adapters retain the required behavior.
- Standalone dark-host text: ThemeStyles styles controls but does not supply host colors;
  confirmed by framework maintainer and handled explicitly in ReviewTheme.

UI.Text already wraps when given finite width; no wrapping defect was found.

## Verification

`FrameworkMigrationTests` creates real native controls on STA threads, checks input retention,
provider switching, review actions, host disposal, native workspace/menu preservation, retained progress identity,
and individual/all extraction cancellation with safe late updates.
Offscreen PNGs are generated under `artifacts/ui-migration` for visual inspection. These checks
do not establish live-screen keyboard/IME or screen-reader coverage. Existing service tests
continue to cover execution, persistence, queues and grading formatting.

Current local.3 packages pass functional checks and fix rich Button content upstream.
The full-list and virtualized performance scenarios passed, but themed update allocations
exceeded the accepted budget (+7.81% versus 6%; update time +9.87%). See current
vendor/ui-framework/VALIDATION.md, performance.json and provenance.json. These remain
local integration packages. Earlier local.2 records are retained in history/local.2.

Deleted obsolete form/extraction XAML and private unreferenced grading helpers; retained specialized native
views, resources, runtime scripts, domain services and user data. Build outputs and test captures
are ignored, not source dependencies.

Existing navigation limitation found during cleanup: the ZIP progress service had callers only in
commented-out selection handlers. Those obsolete handlers/helper were removed. The migrated
extraction dialog/service is tested, but current tree navigation does not invoke it automatically.

Populated sample snapshots can be reproduced with `SampleDirectoryRendersRealFilesAndMenus`
and the real files in `Demo/Submissions`; see `Demo/README.md`. The sample uses temporary
review storage and labels seeded comments as demo data. The framework maintainer confirmed that the implicit Button theme only presents string content.
The earlier rich-content file tabs used the fixed local.3 ContentPresenter. File tabs now use
keyed declarative filename/close buttons with normal-size close targets and accessible names;
neither FileTabButtonStyle nor CompactCloseButton remains. The compact-padding framework gap
was reported upstream; no package bytes or performance claims changed.

Visual refinement: grouped workspace actions and course/assignment pickers, resizable 260px starting file tree, coordinated dark
surfaces and code-built native menu/scrollbar styles (`Presentation/NativeTheme.cs`). Inline reviews
start collapsed and collapse after approval; expanding preserves the same actions and bounded details.
Five focused migration UI checks pass against the populated demo and form captures. Native keyboard
and automation semantics remain WPF-owned; this is not a claim of a full live accessibility audit.

Dense visual checks include a manually seeded multi-paragraph editable feedback draft and an
expanded inline review beside real demo source files (demo-feedback.png / demo-expanded-review.png).
These are actual-control renders with synthetic content, not live desktop screenshots.

Final local.3 app validation: all 171 tests passed in the isolated DemoSnapshot configuration
(the previously launched Release app remained open). Superseded local.2 binaries were removed
after success; historical validation evidence remains.

## Shell conversion completed — September 23, 2026

No application-owned XAML remains. App.cs owns the STA entry point and shutdown policy;
MainWindow.cs no longer uses generated fields or InitializeComponent. MainWindow.NativeControls.cs
owns specialist controls and native templates; MainWindow.Framework.cs composes framework views
and narrowly scoped native layout infrastructure. ReviewTheme uses NativeTheme.cs instead of a
XAML resource dictionary. See [the completed handoff](XAML_REMOVAL_HANDOFF.md).

ShellMigration validation: warning-as-error build, all 187 tests passed, and regenerated actual-control
captures for populated files, feedback, expanded reviews, file/build menus and grading results.
File-close tests verify inactive-tab closure cannot select that tab; console documents and edited
drafts survive panel changes, and the root host is disposed on close. Visual evidence is offscreen
WPF rendering with synthetic demo content, not desktop screenshots or a live accessibility audit.

Grading-view cleanup: the framework GradingView.cs is now the sole grading implementation. Obsolete GradeView.xaml and GradeView.xaml.cs were removed after the framework view and tests passed. A result-switch binding now tolerates reactive teardown while old TestResult keys are cleared. Debug output may be locked by a running app; DemoSnapshot validation passed.

## Toolbar layout restored — September 23, 2026

Restored File/Settings menus beside saved course/assignment pickers, plus the original heading and four numbered workflow actions. Menu uses a stable WPF instance through WpfUI.Native because local.3 lacks an application-menu primitive; this gap was reported to the framework task. Buttons and pickers remain declarative. Newer right-side panels remain available.


October 9, 2026 shell extraction: ReviewWorkspaceService now owns the 128-entry active-file-pinned comment cache, fresh storage reloads, stale-capture checks, inline completion routing/pinned replacement and persisted rubric grade calculation. The window retains UI events, legacy prose completion and draft publication. WorkspaceShellView/ViewModel own layout composition using stable native adapters; InlineCommentView/ViewModel own reusable card rendering/state; StudentGradeEditView/ViewModel/Window own the grade editor and validation. MainWindow owns/disposes hosts and retains specialized control instances. Native drag/drop remains confined to the current editor. Cross-file relocation remains available through Modify. No vendor package changes. Unused legacy tab/log helpers were removed after reference checks; active assignment-scoped log loading and recorded build counts are unchanged.


October 9, 2026 document search: Ctrl+F opens search in the active source editor or focused log panel. The two queries/highlights are independent. AvalonEdit's retained native SearchPanel highlights matches and provides next/previous controls plus Escape/close. Log history rows matching source text or entry labels become bold/gold; if the current snapshot does not match, the first matching snapshot is selected. Loading another log clears its query. DocumentSearchView/ViewModel are reusable separate files; SearchBarView and SearchBarViewModel provide the compact rounded search field and quiet navigation/close icons through the pinned framework. SearchBarNativeAdapter preserves the native editing content host within that field. Both panels uninstall on window close. No source text is sent to an LLM by search, and search does not change saved review records.

October 10, 2026 search styling: the default search widget is replaced by a reusable pinned-framework SearchBarView/ViewModel, with rounded surface, query field and quiet previous/next/close icons. Enter, Shift+Enter and Escape remain supported. The scoped native field adapter fixes inherited content-host clipping. Local rendered and functional checks pass; package bytes and versions are unchanged.

October 10, 2026 dialog overflow: ReviewWindow uses reusable DialogViewportView/ViewModel to preserve scrolling around pinned-framework forms. Startup content measurement grows dialogs within the primary screen work area; overflow and action rows remain reachable when resized smaller. A reduced-height assignment render/test verifies Save is reachable after scrolling. Existing inner editors remain independently scrollable. No vendor package bytes changed.

October 10, 2026 bounds regression correction: DialogViewportView disables horizontal scrolling so the framework form receives a finite available width; vertical overflow remains scrollable. The previous horizontal Auto setting measured forms with unlimited width and oversized picker popups. Shared dialogs now receive 4-DIP outer padding with superseded form-level 20/22-DIP padding removed. Batch review and the workspace shell also use 4-DIP outer spacing. A long-input, reduced-viewport test opens the assignment section picker and checks both control and popup widths against the viewport, plus reachability of Save. Package versions/bytes are unchanged.

October 10, 2026 panel/appearance update: left/right toolbar icons collapse retained panels; Open Submissions Folder remains in File. Feedback/Queue/Rubric tabs live inside the right panel. AppearanceSettingsView/ViewModel saves Dark/Light through WorkspacePanelPreferences.LightMode, default dark, and applies theme tokens to framework hosts and native workspace surfaces. Host tracking uses weak references; MainWindow releases its theme subscription on close. Toggle/theme renders and retained-editor tests verify both modes; vendor bytes remain unchanged.

October 10 panel icons: reusable PanelIconView/ViewModel provide vector glyphs through the native adapter (package has no panel icon primitive). The left toggle is first in the toolbar; both icons retain shared quiet styling, hints and theme colors. Dark/light render and toggle tests pass.

Panel icon styling refinement: softened vector outlines/corners, muted theme ink and reduced accent fill. Icon-only toolbar buttons have a rounded hover surface and keyboard focus outline. Light/dark rendered checks and retained-panel toggle test pass.

October 11, 2026 log recognition: Settings > Programming Checks > Source history logs saves filename and extension in workspace-panel preferences, default *.fslog. Matching tree files open in Logs without loading binary content into the source editor. Picker/automatic discovery share the matcher; explicit assignment log paths retain precedence. Extensions do not add new decoders: FsLogReader content validation and existing bounds remain; invalid log data clears counts and displays a diagnostic.

October 11, 2026 log selection contrast (source reviewed): LogSnapshotStyle owns the ListBoxItem selection surface through a bound native template, preventing OS inactive-selection backgrounds from overriding the selected foreground/background pair. Selected log rows remain white on deep blue with or without keyboard focus. Normal and search-match foregrounds use theme-aware contrast; ApplyWorkspaceColors rebuilds the snapshot style when appearance changes. BuildHistoryView now uses the theme surface instead of a fixed dark fill. Real-window inactive-selection renders/tests pass in light and dark modes.
