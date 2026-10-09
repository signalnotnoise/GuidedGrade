# Feedback workspace

Class folder discovery (October 7, 2026): Assignment setup includes "Use folder
names for this class". It defaults off for every class, including PG1 and DSA,
retaining Last_First-ID parsing. Enabled classes list all immediate subfolders
using their literal names and blank student IDs. The preference is saved per
case-insensitive course name in assignments.db's CourseFolderSettings table and
shared by all assignments in that class. Saving setup or switching classes reloads
an already-open folder list, preserving the selected folder when it remains present.
Folder paths still identify feedback/grades; this setting does not rename files
or change lab scoping.

Reviewed October 6, 2026.

## Student grades

The submissions list shows an Assignment percentage and a Total percentage beside each name. Assignment refers to the selected assignment and active lab (named above the list); changing the active lab updates every student's badge for that matching lab folder. A dash means no recorded grade, not zero. The bottom bar beside violations shows assignment points/percentage and the selected student's course total. Click the assignment grade to enter, edit, or clear the final grade after selecting an assignment and a student file.

Grades are instructor-entered, local records, separate from AI suggestions and programming log scores. They persist in `assignments.db`'s `StudentGrades` table, keyed by student folder, course, assignment title, and lab root. Saving replaces that scope's grade; clearing removes only that grade. Queued feedback cannot overwrite it. Grade records are never included in model prompts. Renaming/moving student folders or renaming courses/assignments does not migrate recorded grades.

The running total sums earned and possible points over all recorded submission grades for that student in the current course, across assignments/labs. It excludes ungraded work and other courses; it is not an average of percentages or a category-weighted final course grade. Maximum points are saved with each grade, so changing a rubric later does not silently rescale prior grades. Multiple recorded labs under one assignment each contribute their saved points.

## Workflow
Selecting a student now reopens their most recent existing reviewed file for the currently selected assignment. Clicking Comments without an open file does the same. The Comments panel lists matching saved review files; Job queue also offers **Open selected student's saved review**. These routes use saved review context and never show another assignment's or student's feedback. A batch completion refreshes the current review or opens a saved file when none is selected, provided the assignment still matches. Overall-review cards remain visible near the viewport's first visible line when source line 1 scrolls off-screen; ordinary section cards retain their source-line anchors.

Batch file lists support a leading `**/` for extra submission nesting. Preview resolves and displays the exact per-student paths before queuing; the most recently modified matching file is selected, with alphabetical path order breaking equal-date ties. Comments remain keyed to the resolved physical student/lab/file context, using the same review routing as opening that file manually.

**Batch review** applies the same relative file list to all loaded students. It captures assignment/lab/student routing before enqueueing, clears existing reviews on that student's selected files, optionally builds/runs once, and requests one combined overall review across the selected files. Build/run defaults off; automatic approval defaults on. The shared result is saved on each selected file and added once to that student's feedback before continuing. Pending mode saves inline comments without draft import until approval. Replacement removes intact old generated draft blocks while preserving instructor-edited text. No automatic numeric final grade is recorded. Approved records restore when their files are reopened; editable drafts persist in the assignments database.

Select an assignment and student, then review submitted files beside the rubric or feedback panel. Overall AI feedback reviews the checked files together and routes its result to the draft key captured before the request. Section feedback for the selected file also populates the panel. Opening a file imports approved saved section feedback once per section per session. Rejected feedback is excluded from this import. New results append rather than replacing instructor edits.

The draft key encodes student folder, course, assignment title, and lab folder as a JSON array to avoid separator collisions. Section import identities additionally include file, line range, and section name. Generated sections are marked imported so approval followed by reopening does not append a duplicate. Saved and generated sections share SavedFeedbackText formatting, including suggested code.

## Editing and copying
Approving an inline comment persists its approval and immediately imports it into the matching Comments draft. The existing editor binding refreshes even when the approved comment comes from another file in the same lab. Already-imported comments are not duplicated and instructor edits remain intact. Saved pending comments remain inline until approved; approved comments restore on reopening. Windows file-path capitalization does not change comment lookup or review-context matching. A file absent from the memory cache is not treated as a request to delete its saved reviews.

The right panel is an editable session draft. Copy uses the current edited text. TXT removes supported Markdown headings/emphasis/fences; Markdown retains the source. HTML emits escaped markup with inline styles: green strengths, amber warnings/suggestions, red issues. Category recognition uses explicit headings, never inferred severity. Code blocks remain literal. HTML copy currently places markup on the plain-text clipboard, not a rich HTML clipboard payload. Export writes a text file.

## Storage and limits
Assignments and original section feedback persist separately in SQLite under %APPDATA%/GuidedGrade. On access after the product rename, `AppDataPaths` migrates `LabFeedbackWPF`, followed by `Lab Feedback WPF`, into the GuidedGrade directory for the requested roaming or local root. Missing files move while conflicting files remain untouched in their original locations; existing GuidedGrade files take precedence. Edited drafts autosave to the ReviewDrafts table in the assignments database, separately from approved section records. The draft cache retains 64 contexts; import markers remain session-local and retain at most 4,096 identities. Export remains available.

## Tool panels
MainWindow.ToolsPanel owns Console/Violations tab selection and show/hide behavior. Collapsing sets the content row to zero height. Captured output is retained within the existing bounded terminal buffers. Starting a new run selects and opens Console.

## Review findings still open
- Durable edited-draft storage and an unsaved-draft close flow are needed to prevent lost edits.
- Rejected or regenerated source feedback does not reconcile text already copied into an edited draft. The draft intentionally preserves edits; reconciliation needs an explicit review workflow.
- Draft retention needs a bounded, durable store. HTML supports a small Markdown subset, not tables or full CommonMark.

## Verification
Formatter regression tests cover HTML escaping, category colors, code preservation, and TXT/Markdown output. Build checks compile XAML and handler wiring. No live clipboard/editor integration or visual UI validation was performed in this review.

## September 16 fixes
Section requests now capture their draft destination before awaiting work, including queued runtime grading and multi-file batches. Completed results append to that destination even when another file is selected; visible publication checks the current draft key. Approval updates stored review status without publishing a second draft entry. SuggestedScore is double; parsing uses an invariant decimal separator, and SQLite reads both historical integer and fractional values as double. Existing SQLite INTEGER-affinity columns accept fractional values without rebuilding the table. Rubric import, setup submission, and persistence reject nonfinite or nonpositive maximum points. Manual GUI switching/approval validation remains outstanding.

## AI job queue (September 16, 2026)

The status-bar queue indicator opens an independent **Job queue** tab in the right panel. It lists solution tests and shared provider requests, supports cancellation and free-form tasks, and retains bounded result history. Assignment analysis and grading requests take priority over waiting general tasks. Free-form tasks send user-entered text at the user's discretion and keep results in the queue panel. Cancelling one model request does not cancel an entire multi-request grading batch. See [AI job queues](AI_TEST_QUEUE.md).

## Pinned tabs (September 17, 2026)

Use the right-side **Comments**, **Job queue**, and **Rubric** tabs to switch panels. Collapsing the panel leaves the tabs available. Each panel owns separate content, so opening feedback or the rubric cannot remove the queue. **Settings ÃƒÂ¢Ã¢â‚¬Â Ã¢â‚¬â„¢ Pinned workspace tabs** saves Comments and Job queue visibility for the current user across restarts. Both are shown by default. Hiding a tab preserves drafts and running jobs; hiding Job queue also hides its status-bar shortcut. Inline feedback stays within the editor viewport.

### File navigation and solution actions (September 20, 2026)

Right-click any file or folder to Open in File Explorer (files are selected). A solution's Programming tools submenu offers Build, Build and Run, and Run without rebuilding. Settings > Build and run on saves Local computer or VM (Hyper-V) for future actions. Both settings windows expose the separate local-confirmation checkbox; VM configuration remains in AI Provider settings. Local manual builds preserve output in the submission folder. VM Run needs existing uploaded output; disposable VM build output is not retained between actions.

### Explicit feedback scope (September 20, 2026)

The file-tree context menu separates **Overall feedback for checked files** from **Section-by-section comments for checked files**. Both exclude solution metadata. Overall feedback always produces one combined review: with a selected assignment it uses that assignment's requirements and full rubric, asks for one evidence-based score per criterion and one total, and avoids grading every file against the entire rubric independently. Without rubric items it requests qualitative feedback without inventing a grade. Without an assignment it uses the configured default requirements. Requirements/rubric instructions and the draft destination are captured before asynchronous work; a file-read failure stops the overall request rather than grading an incomplete input set. Overall feedback is added to the editable draft and a persistent overall card on each captured file; it is not automatically applied to grade totals.

Section comments require a selected assignment with rubric items. Files with no detected functions are skipped and listed in the completion message, with guidance to use overall feedback or Grade Selected Section. This explicit menu path no longer silently falls back to whole-file grading. The existing function detector still requires a signature and opening brace on the same line; runtime AI-test grading retains its existing whole-file fallback; unused current-file helpers were removed during migration. The editor's Grade Selected Section action still grades the highlighted text.

Manual solution action results appear in the scrollable Console panel, with a window-title indication when finished, rather than a large modal build log. Native MSBuild output paths use quoted Windows separators so post-build directory-copy commands can recognize their destinations.

### Clear review (September 20, 2026)

Right-click a file in the tree and choose **Clear review** to delete all of its saved section comments (pending, approved, and rejected), remove the file's in-memory comment cache, and clear its visible inline comments if open. The command applies to the right-clicked file, not all checked files; folders do not expose it. Database deletion succeeds before the UI/cache is changed. Reopening the file does not restore deleted comments. In-flight section analyses/regenerations capture a per-file review generation and discard their results when that file is cleared before publication; new requests can generate new reviews. Already queued work that has not begun reviewing the file can still create new comments when it starts.

The combined editable feedback draft and exports are preserved: they can contain instructor edits and material from multiple files and are not stored as file-specific comments. The menu tooltip states this scope. Existing import markers are retained to avoid reimporting previously copied feedback into that preserved draft.

### Framework-backed workspace (September 20, 2026)

Workspace actions, rubric and feedback panels, settings/assignment dialogs, inline review cards and the job queue now use the custom UI framework. The feedback editor retains its native text control across reactive updates; copy/export operate on the captured student/assignment draft. Rubric navigation no longer discards the active feedback-host reference. Queue rows are keyed and virtualized, with explicit Details and Cancel actions and a bounded read-only result editor. Inline review details scroll within the editor overlay height cap, keeping review actions reachable. Native file menus, AvalonEdit, splitters, console and programming-results controls remain interoperability islands. UI_FRAMEWORK_MIGRATION.md describes lifecycle and coverage. Offscreen visual/interaction checks supplement the earlier formatter tests; live clipboard/IME verification remains outstanding.

### Compact review presentation (September 20, 2026)

Inline reviews start as collapsed headers to leave source code visible. Click a header to expand
its scrollable details and approve/regenerate/reject actions; approval collapses the card again.
The editable submission draft stays in the Comments panel. The toolbar restores File and Settings
menus beside the saved course/assignment pickers. File contains Open Submissions Folder and Exit;
Settings contains Programming Checks, AI Provider and Setup Assignment. Below, Assignment review
presents numbered Assignment, Open submissions, Review with rubric and Feedback actions
(restored September 23, 2026). Right-side Comments, Job queue and Rubric actions remain available when panels collapse.
File tabs have separate named close buttons; Console/Violations actions toggle the retained tool panels.
File menus and scrollbars share the dark palette; the file tree is resizable.

September 23 shell conversion: application layout and navigation are created in C#, with no XAML
startup or generated fields. Framework state owns the file tabs, selections, panel indicators and
status labels. Specialist tree/source/console controls retain their instances across updates.
The programming-results disclosure is a framework toggle and preserves the grading view.

### Overall file review completion (September 20, 2026)

Overall feedback now attaches a persistent review at line 1 of each checked file as well as appending
once to the captured submission draft. Expand the collapsed Overall file review header to read it.
For multiple checked files, the same combined report is explicitly labeled as such on each file.
A later overall review replaces that file's earlier overall card while preserving section reviews.
Files cleared after the job starts are skipped; moving to another file does not retarget the job.
Source files are not edited. The overall card has no numeric score and is rerun from the Overall
feedback menu rather than the section regeneration action. Existing database rows retain their
section-review behavior through the default IsOverallReview=0 migration.

### File-tree virtualization

The submitted-files tree recycles offscreen row containers while retaining folder expansion, selected-file and analysis-checkbox state in the file models. Restoring the selected row does not reopen the active file or reset the editor caret. The same context menus and file-open actions remain attached to the native tree. Large-tree performance measurements and coverage limits are documented in FILE_TREE_PERFORMANCE.md.

### Queued review isolation and privacy (October 5, 2026)

Each result carries its captured student/lab/assignment key into SQLite, cache replacement, inline display and draft import. Lab folders are the immediate directories below the student folder; nested source folders share a lab. Checking files from multiple labs in one overall/section request now requires correcting the selection. Solution tests use only checked files inside the selected solution's lab. Student changes detach old tabs; file and assignment changes refresh the current feedback view. Late results never select a different file or scroll the new file.

Existing records are retained with unknown (empty) context and are shown only when no assignment is selected. They are never automatically assigned to the current rubric. Clear review still clears every context for the selected file; it preserves edited drafts. New contexts retain separate records even when section names and line ranges match.

Only assignment criteria and sanitized file contents are used as variable model context, with generic file labels and fixed review instructions. Original filenames, student metadata, assignment title/course, routing keys, type indexes and execution reports stay local. Known-identifier redaction is best effort for arbitrary personal information inside source text. Console testing may send redacted console output and testing instructions to obtain a next input, which is validated and written to the running program. Full runtime reports remain excluded from grading prompts. Free-form tasks are a separate, explicit user-authored request.

### Grading confirmations (October 5, 2026)

Settings and AI Provider settings expose Ask before grading. When enabled (the default), overall feedback, section batches, selected-section grading, regeneration and a solution test that will grade files ask once before starting or queuing the request. When disabled, these requests start without the previous Analysis Starting popup. Selecting a section/rubric, validation errors and completion messages remain available. This preference is independent of Ask before each local build or run.

October 7 structured assignment setup source review: AssignmentSetupWindow hosts AssignmentSetupView and AssignmentSetupViewModel. Instructions, rubric, deductions, feedback preferences and class settings each have dedicated View and ViewModel classes. Saved assignment selection, grading-prompt import preview and rubric-only import also use separate views/view models. Setup uses the pinned declarative framework. Prompt import is local and recognizes points-first rows such as **10pts:** and **-10pt deduction:**; it previews replacement, rejects stale previews, removes recognized boilerplate and preserves other instruction text for review. Arbitrary prompt formats are not semantically interpreted.

AssignmentOptions in assignments.db stores deduction rules and feedback preferences alongside legacy SavedAssignments; the two assignment writes are transactional. Legacy records default to an empty deduction list and direct-address feedback. Class folder settings remain shared across the class. Queued snapshots deep-copy deduction/preference data. Overall prompts use the structured preferences; section prompts retain their existing parseable section-response format while receiving tone/evidence/deduction rules. Policy violations and starter-code removal imports require instructor confirmation: the model is asked to flag them rather than apply penalties automatically. These are prompt instructions, not a computed-score enforcement engine; grades remain instructor-entered. No class/title/name metadata is added to model requests.

The review workspace uses a compact toolbar and horizontal panel selector. Feedback, rubric and queue rendering live in separate Views/ViewModels files; the MainWindow coordinator retains draft persistence and captured review identity. Rubric instructions are collapsed by default so criteria and deductions remain visible. Console and violations use separate native-adapter views/models.

Successful database writes refresh the affected persisted snapshot before notifying bindings: all grade records for running totals, the affected file for saved comments, and assignment/course selectors after setup. Approval restores persisted approved feedback without replacing unsaved draft edits. This is refresh after application writes, not external database monitoring.

The top menus, actions, selectors and panel navigation share one toolbar. File/panel/tool tabs use a flat underline selection style, distinct from action buttons. Assignment setup is available from Settings and Edit assignment in the rubric panel. Scrollbars are eight pixels wide and button content is centered.

Course and Assignment selectors now use the same compact menu presentation as File and Settings. Their labels include the current selection and their submenu entries show a checkmark.

Review appears in the toolbar when an assignment is selected. Selection review requires highlighted editor text. Overall review and Clear reviews require checked source files in Submitted files. Clear all reviews removes saved reviews for the selected course/assignment across students, preserving other assignments, grades and legacy comments without assignment identity. Clearing invalidates in-flight file results and removes intact generated feedback blocks while retaining edited draft text.

Older saved sections without assignment identity are visible for the exact selected student file, labeled Earlier review / assignment not recorded. Under a selected assignment they are read-only and listed separately in Feedback, without being imported into its draft or grade. Reopening files reloads persisted comment records to avoid stale caches.

October 8 grading prompt import correction (source reviewed): AssignmentPromptImporter supports both points-first and label-first colon rows, including criterion names containing colons, optional trailing point units and negative penalties. Malformed-row detection requires a numeric points prefix, so numbered prose mentioning points is retained rather than rejected. Output/feedback/response format headings delimit output instructions. The importer remains a deterministic text parser, not semantic rubric extraction.

October 8 COP2334 prompt format verification: label-first rubric rows also accept percentage / explicit point pairs such as 15% / 15 pts, using the explicit point value. Multi-file requirement prose and unspecified deduction notes remain instructions; no numeric penalty is invented. Output-format sections and following submission placeholders are excluded. Instructor-role preambles are stripped from assignment requirements.

Grading navigation (source reviewed 2026-10-08): Overall and section prompts use TODO:// section labels and section-named methods as hints for matching rubric criteria to source. They require implementation evidence across supplied files and do not award or deduct points solely from TODO markers or names.

Source reviewed 2026-10-08: BatchReviewPlan.ProcessStudentAsync clears each selected file, optionally builds/runs once, then submits all selected file contents in one combined overall review per student. One shared review is attached to the selected files through CompleteOverallFileReview. Manual overall review already combines checked files. Overall prompt rules mark unavailable cross-file evidence unverified, withhold numeric final grades for unverifiable criteria, and distinguish rubric scores from configured penalties. These are model instructions, not deterministic score validation; only selected files are supplied, so instructors must select the relevant headers and implementations.

Source reviewed 2026-10-08: The bottom Logs tab uses LogPanelView/LogPanelViewModel through the pinned framework native adapter for source history list/text controls. FsLogReader implements the supplied ResultsDecoder v2 FSLG layout: named files, Unix-second timestamp groups, uint32-sized snapshots with byte-offset-128 decoding using the local ANSI code page. It validates signatures, versions, chunk boundaries and limits (32 MB input, 256 names, 10,000 snapshots). The viewer retains one loaded document as decoded strings, releases its stream after loading, and clears old content on assignment/student refresh. Historical timestamps and source are local viewer data and are not added to LLM payloads or treated as build counts/grades. Log options uses native menu styling alongside the bottom tab navigation.
Assignment setup stores optional relative ReviewFilePaths and LogFilePath in AssignmentOptions JSON; existing records default to empty. Saved paths are validated (optional **/ prefix, no absolute/parent paths), copied into queued snapshots, and prefill batch file selection. Logs resolves the saved .fslog path within the current student folder using newest-match resolution. Without a configured path, it searches the nearest project/solution directory of the active file; no project context yields no automatic log. Manual Open remains available. A configured assignment log can load without opening a source file. Only selected review-file contents and grading criteria are sent to models; path configuration and decoded log history are not sent.

Source reviewed 2026-10-08: WorkspaceStatusView and BuildGradeStatus use reusable ToolbarActionView/ToolbarActionViewModel for flat, keyboard-accessible queue, assignment-grade and violations actions; ToolbarLabelView aligns passive build and course-total labels to the same 32-pixel row. Native adapters preserve existing actions and grade enabled/color state without outlined button backgrounds.

Source reviewed 2026-10-08: The bottom tools tabs and status actions share one ToolsPanelToolbarView row docked below panel content. Console, violation count and Logs navigation, icon log-options menu, queue/build/grade status and icon collapse action are consolidated. Navigation and toolbar actions expose tooltips and retained automation names. Violation count appears once in its navigation tab. Separate component views/view models remain.

Source reviewed 2026-10-08: FsLogSnapshot preserves its serialized timestamp-group ordinal as BuildNumber; FsLogDocument.BuildCount counts those groups, including groups sharing a timestamp, rather than multiplying by file count. FileHandler.ParseFile now decodes fslog instead of parsing binary data as build-output text. LogPanel shows high-contrast source/history, recorded build counts, elapsed span, largest gap and a BuildHistoryView/BuildHistoryViewModel timeline with hover details and click-to-select snapshots. Timeline time spacing includes breaks, not measured active work time. ANSI decoded history remains local and one bounded log is retained; timeline renders marks without creating a control per build.
CourseReviewRules stores instructor-defined rules keyed by course in SQLite, loaded into assignments and immutable review snapshots. Assignment setup Class settings offers a PG2-only preset for no lambdas, header/.cpp separation with getter/setter exceptions and reference/const use from Part B. No preset is automatically enabled. Shared grading instructions ask for verified source evidence and scope checks; they are model review guidance, not deterministic C++ AST checks and do not add entries to the lexical Violations count automatically. Numeric penalties require separately configured assignment deductions.

Source reviewed 2026-10-08: Bottom Builds visibility binds to LogPanelViewModel.IsLoaded and its count to RecordedBuildCount reactive state, independent of legacy parsed-result counters. Clear and load failures reset both states. Selection refresh resolves the current assignment log even when the log panel is hidden if a log was loaded, preventing prior-student counts from persisting. A successfully loaded empty log displays zero builds; absence/failure hides the indicator.

Source reviewed 2026-10-08: Assignment feedback settings include independently persisted DetailLevel (1 very brief to 5 thorough; default standard) and ReadingLevel (middle school, high school, college, technical; default high school). FeedbackDialView/FeedbackDialViewModel provides a rotary native-adapter control with drag, wheel and arrow/Home/End input; the reading-level menu uses toolbar menu styling. AssignmentFeedbackViewModel loads/saves these through AssignmentOptions feedback JSON, and ReviewContext snapshots them. Shared grading prompts describe language and explanation depth while preserving rubric/evidence requirements. Changes apply to subsequent generated reviews, not already-saved comments.

Source reviewed 2026-10-08: Very brief/Brief feedback prompt rules now limit each rubric justification to 12/20 words and the feedback paragraph to 35/60 words, forbid duplicate Detailed Review and Evidence/Comments blocks, and prioritize saved length preferences over verbose assignment output-format prose. OverallFeedbackPrompt repeats a final length check and asks models to retain exact rubric rows/maxima and reconcile totals. These are generation instructions, not a deterministic word-count or scoring validator. Dial changes must be saved with the assignment and apply to subsequent requests.

Source reviewed 2026-10-08: Overall feedback records are hidden from inline overlays and displayed in the Feedback side panel. Restore/import identity for overall reports uses assignment context, section label and report text, excluding file path, so the same combined report stored against several files restores once. Section-specific reviews retain file/line identity. Pending overall reports have a separate OverallReviewPanelView/ViewModel with approval action in the side panel. Per-file overall persistence is retained for file discovery and clearing; this is presentation/import deduplication, not a new assignment-review database schema.

Source reviewed 2026-10-09: Both manual and batch multi-file rubric reviews use OverallReviewService and OverallReviewResult rather than accepting free-form model totals. Criterion IDs map to exact configured rubric rows; C# rejects missing/duplicate rows, invalid ranges and invented deduction IDs, calculates totals from earned points and configured penalties, and keeps instructor-confirmation penalties pending. Verified findings require exact source excerpts from the indicated supplied file; unsupported findings become unverified and withhold the final grade. Renderer enforces saved brief paragraph/justification word caps and enabled output sections. Source-quote matching proves the excerpt exists, not the correctness of model interpretation; semantic review still requires instructor oversight. No-rubric qualitative requests retain the text path.
Ollama combined structured reviews explicitly set temperature zero and estimate context needs from prompt characters plus output headroom, with an 8,192-token minimum and 32,768-token ceiling. Oversize estimates reject the request before HTTP; CPU fallback retains the context setting. Estimates are not tokenizer-accurate and do not prove all code is attended to. Azure receives the same JSON contract through its existing completion transport; schema enforcement is local validation, not provider constrained decoding. File payloads retain anonymous IDs/extensions and explicit end boundaries; no student paths or log history are added.
Replacing an overall file review removes matching intact old generated draft blocks, including legacy combined-report prefixes, before presenting the new review. Instructor-edited blocks remain. Multi-file completion persists one shared result per selected file for discovery while the side panel imports the shared report once.

Source reviewed 2026-10-09: OverallReviewResult.SchemaFor constrains rubric row count and allowed IDs to the captured assignment. OverallReviewService retries one malformed structured response with numeric mismatch diagnostics using the original sanitized source, never re-echoing model text. If the second response is otherwise parseable but missing/duplicating rows, only uniquely returned criteria are retained; missing/conflicting rows become explicitly unverified, generic incomplete feedback replaces the model summary, and the final grade is withheld. Invalid JSON, impossible points or invented deductions still fail without publishing. No zero scores are fabricated for absent model findings.

Source reviewed 2026-10-09: After the single correction attempt, an invalid structured response is retained as raw editable feedback with a needs-instructor-review warning rather than discarded. Batch auto-approval is disabled for these drafts and withheld-grade results. They remain pending overall side-panel feedback for editing/approval; no numeric grade record is saved automatically. Transport failures still report errors. This supersedes earlier documentation stating all invalid second replies fail without publishing.

Source reviewed 2026-10-09: Invalid structured reviews are formatted as normal editable rubric/feedback drafts rather than raw JSON. ReviewWarningEnvelope separates normal display text from diagnostics for transport to completion; CompleteOverallFileReview strips the envelope and stores warning details in overall SectionFeedback.Issues using existing persistence. Warning-bearing results remain pending. ReviewWarningView/ViewModel displays a tooltip warning symbol and click-open details dialog next to the pending overall draft. Details identify missing/duplicate criteria, out-of-range score mapping and source quotes that do not match. Diagnostics remain outside exported student-facing feedback text. Unparseable replies retain editable prose with a separate warning. No suggested numeric final grade is manufactured for invalid drafts.

Source reviewed 2026-10-09: ReviewerRoleView/ReviewerRoleViewModel adds a per-assignment editable reviewer-role field and reset action under Feedback preferences. AssignmentFeedbackOptions.ReviewerRole persists in existing AssignmentOptions JSON, defaults to an experienced C++ instructor for older/blank settings, and is deep-copied into queued snapshots. Shared AssignmentGradingInstructions includes the role for manual, batch and section reviews; overall prompt sanitization covers its text. The role controls teaching perspective and tone; app-side rubric validation/calculation and privacy handling remain independent. Role changes apply only to subsequently generated reviews after saving the assignment.


October 9, 2026 maintenance: manual and batch overall reviews call `ReviewOrchestrator` to prepare one anonymous combined file packet, then `LlmCompletionService` routes it. Section grading shares the same provider route. Unsupported provider values fail explicitly. Feedback drafts are saved by assignment/student/lab context in the assignments database (`ReviewDrafts`), including instructor edits, and restored on reopening. Failed draft writes show “Draft not saved” and prompt before closing; export remains available. Drafts are limited to 1 Mi characters, and the in-memory draft cache retains 64 contexts. File preparation rejects missing/oversized source instead of silently grading a partial packet. Folder loading and code-section detection now live in independent services; multiline signatures are supported, with bounded regex execution. This is heuristic C++ section detection, not a full C++ parser.
