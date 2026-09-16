# Feedback workspace

Reviewed September 15, 2026.

## Workflow
Select an assignment and student, then review submitted files beside the rubric or feedback panel. Whole-file AI analysis routes its result to the draft key captured before the request. Section feedback for the selected file also populates the panel. Opening a file imports approved saved section feedback once per section per session. Rejected feedback is excluded from this import. New results append rather than replacing instructor edits.

The draft key encodes student folder, course, and assignment title as a JSON array to avoid separator collisions. Section import identities additionally include file, line range, and section name. Generated sections are marked imported so approval followed by reopening does not append a duplicate. Saved and generated sections share SavedFeedbackText formatting, including suggested code.

## Editing and copying
The right panel is an editable session draft. Copy uses the current edited text. TXT removes supported Markdown headings/emphasis/fences; Markdown retains the source. HTML emits escaped markup with inline styles: green strengths, amber warnings/suggestions, red issues. Category recognition uses explicit headings, never inferred severity. Code blocks remain literal. HTML copy currently places markup on the plain-text clipboard, not a rich HTML clipboard payload. Export writes a text file.

## Storage and limits
Assignments and original section feedback persist separately in SQLite under %APPDATA%/LabFeedbackWPF. Edited drafts are NOT autosaved or written back to approved section records. Export before closing. Draft/import dictionaries currently grow with the number of reviewed submissions; no eviction or durable draft store exists yet.

## Tool panels
MainWindow.ToolsPanel owns Console/Violations tab selection and show/hide behavior. Collapsing sets the content row to zero height. Captured output is retained within the existing bounded terminal buffers. Starting a new run selects and opens Console.

## Review findings still open
- Durable edited-draft storage and an unsaved-draft close flow are needed to prevent lost edits.
- Draft destination routing captures the initiating context; the underlying file-keyed SQLite records still lack assignment identity.
- SQLite section records are keyed by file rather than assignment; reusing one file under different assignments can import old feedback.
- Rejected or regenerated source feedback does not reconcile text already copied into an edited draft. The draft intentionally preserves edits; reconciliation needs an explicit review workflow.
- Draft retention needs a bounded, durable store. HTML supports a small Markdown subset, not tables or full CommonMark.

## Verification
Formatter regression tests cover HTML escaping, category colors, code preservation, and TXT/Markdown output. Build checks compile XAML and handler wiring. No live clipboard/editor integration or visual UI validation was performed in this review.

## September 16 fixes
Section requests now capture their draft destination before awaiting work, including queued runtime grading and multi-file batches. Completed results append to that destination even when another file is selected; visible publication checks the current draft key. Approval updates stored review status without publishing a second draft entry. SuggestedScore is double; parsing uses an invariant decimal separator, and SQLite reads both historical integer and fractional values as double. Existing SQLite INTEGER-affinity columns accept fractional values without rebuilding the table. Rubric import, setup submission, and persistence reject nonfinite or nonpositive maximum points. Manual GUI switching/approval validation remains outstanding.
