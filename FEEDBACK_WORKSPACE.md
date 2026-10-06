# Feedback workspace

Reviewed October 6, 2026.

## Student grades

The submissions list shows an Assignment percentage and a Total percentage beside each name. Assignment refers to the selected assignment and active lab (named above the list); changing the active lab updates every student's badge for that matching lab folder. A dash means no recorded grade, not zero. The bottom bar beside violations shows assignment points/percentage and the selected student's course total. Click the assignment grade to enter, edit, or clear the final grade after selecting an assignment and a student file.

Grades are instructor-entered, local records, separate from AI suggestions and programming log scores. They persist in `assignments.db`'s `StudentGrades` table, keyed by student folder, course, assignment title, and lab root. Saving replaces that scope's grade; clearing removes only that grade. Queued feedback cannot overwrite it. Grade records are never included in model prompts. Renaming/moving student folders or renaming courses/assignments does not migrate recorded grades.

The running total sums earned and possible points over all recorded submission grades for that student in the current course, across assignments/labs. It excludes ungraded work and other courses; it is not an average of percentages or a category-weighted final course grade. Maximum points are saved with each grade, so changing a rubric later does not silently rescale prior grades. Multiple recorded labs under one assignment each contribute their saved points.

## Workflow
Selecting a student now reopens their most recent existing reviewed file for the currently selected assignment. Clicking Comments without an open file does the same. The Comments panel lists matching saved review files; Job queue also offers **Open selected student's saved review**. These routes use saved review context and never show another assignment's or student's feedback. A batch completion refreshes the current review or opens a saved file when none is selected, provided the assignment still matches. Overall-review cards remain visible near the viewport's first visible line when source line 1 scrolls off-screen; ordinary section cards retain their source-line anchors.

Batch file lists support a leading `**/` for extra submission nesting. Preview resolves and displays the exact per-student paths before queuing; the most recently modified matching file is selected, with alphabetical path order breaking equal-date ties. Comments remain keyed to the resolved physical student/lab/file context, using the same review routing as opening that file manually.

**Batch review** applies the same relative file list to all loaded students. It captures assignment/lab/student routing before enqueueing, clears existing reviews on that student's selected files, optionally builds/runs once, and requests one overall review per file sequentially. Build/run defaults off; automatic approval defaults on. Each file result is saved and added to that student's feedback before continuing. Pending mode saves inline comments without draft import until approval. Replacement removes intact old generated draft blocks while preserving instructor-edited text. No automatic numeric final grade is recorded. Approved records restore when their files are reopened; editable drafts remain session-only.

Select an assignment and student, then review submitted files beside the rubric or feedback panel. Overall AI feedback reviews the checked files together and routes its result to the draft key captured before the request. Section feedback for the selected file also populates the panel. Opening a file imports approved saved section feedback once per section per session. Rejected feedback is excluded from this import. New results append rather than replacing instructor edits.

The draft key encodes student folder, course, assignment title, and lab folder as a JSON array to avoid separator collisions. Section import identities additionally include file, line range, and section name. Generated sections are marked imported so approval followed by reopening does not append a duplicate. Saved and generated sections share SavedFeedbackText formatting, including suggested code.

## Editing and copying
Approving an inline comment persists its approval and immediately imports it into the matching Comments draft. The existing editor binding refreshes even when the approved comment comes from another file in the same lab. Already-imported comments are not duplicated and instructor edits remain intact. Saved pending comments remain inline until approved; approved comments restore on reopening. Windows file-path capitalization does not change comment lookup or review-context matching. A file absent from the memory cache is not treated as a request to delete its saved reviews.

The right panel is an editable session draft. Copy uses the current edited text. TXT removes supported Markdown headings/emphasis/fences; Markdown retains the source. HTML emits escaped markup with inline styles: green strengths, amber warnings/suggestions, red issues. Category recognition uses explicit headings, never inferred severity. Code blocks remain literal. HTML copy currently places markup on the plain-text clipboard, not a rich HTML clipboard payload. Export writes a text file.

## Storage and limits
Assignments and original section feedback persist separately in SQLite under %APPDATA%/GuidedGrade. On access after the product rename, `AppDataPaths` migrates `LabFeedbackWPF`, followed by `Lab Feedback WPF`, into the GuidedGrade directory for the requested roaming or local root. Missing files move while conflicting files remain untouched in their original locations; existing GuidedGrade files take precedence. Edited drafts are NOT autosaved or written back to approved section records. Export before closing. Draft/import dictionaries currently grow with the number of reviewed submissions; no eviction or durable draft store exists yet.

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

Use the right-side **Comments**, **Job queue**, and **Rubric** tabs to switch panels. Collapsing the panel leaves the tabs available. Each panel owns separate content, so opening feedback or the rubric cannot remove the queue. **Settings → Pinned workspace tabs** saves Comments and Job queue visibility for the current user across restarts. Both are shown by default. Hiding a tab preserves drafts and running jobs; hiding Job queue also hides its status-bar shortcut. Inline feedback stays within the editor viewport.

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
