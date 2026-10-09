# Output memory limits

October 6 nested-path matching: recursive batch resolution lazily enumerates each student's files per pattern, retaining only the current newest matching path and timestamp while scanning all candidates. The framework preview retains the resolved path list and any skip reason. Deep/large submissions can still make preview slow; recursive resolution does not load source contents or follow reparse points.

October 6 batch review: the active batch captures student metadata, file paths and review-version keys for all loaded students but loads source only when each student's job runs. It occupies one solution queue slot. The designer preview and progress text have no independent student/text count limit; progress retains per-student outcome lines until replaced by the next batch. Saved section comments retain the existing cache/draft lifetime. There is no durable batch resume or total-memory bound.

The three output paths now enforce limits while receiving data. These limits work alongside the [AI test queue](AI_TEST_QUEUE.md); they do not impose a total RAM limit on the app, Ollama, or a student program.

## Limits and behavior

| Path | Retained data | When full |
| --- | --- | --- |
| Interactive stdout / stderr | Latest 64,000 characters per stream | Discard oldest characters; keep reading and retain later prompts |
| ConPTY console | Latest 64,000 characters | Same tail policy, shared by local execution and the published runner |
| Build stdout / stderr | First 8,000 characters per stream | Drain and discard remaining characters until EOF |
| Pending terminal updates | 16,000 characters and 128 chunks | Drop oldest chunks; a single oversized chunk keeps its tail |
| Displayed terminal history | 100,000 characters and 256 text runs | Remove oldest runs; disable undo storage |

Truncation adds a fixed `...[output truncated]...` notice outside the retained-character budget. The terminal flushes pending output every 100 ms at background dispatcher priority, in a single edit batch with one scroll per batch. A busy UI may flush later, but its pending mailbox stays bounded. Diagnostic messages may explicitly flush the mailbox before displaying a completion or failure.

Limits are **characters**, not file bytes or total process memory. WPF formatting objects, transient snapshots, reports, source files, model responses and VM memory have additional costs. Build capture retains the beginning of a stream to preserve existing behavior; errors at its end can be omitted. Console capture retains the end to keep the current prompt visible. Reports label these console sections as bounded captures rather than claiming to contain full output.

## Implementation

- `OutputLimits.cs` centralizes the budgets.
- `BoundedTextBuffer.cs` uses a fixed character array. Its absolute write counter keeps advancing after the array fills, so polling still detects activity. Consumption cursors do not repeat old output and report when unread text was evicted.
- `InteractiveProcessSession.cs` uses those buffers for redirected output and ConPTY. ConPTY decoding preserves UTF-8 sequences split across pipe reads.
- `ProcessRunner.CaptureOutputAsync` reads with a 4,096-character scratch buffer and continues draining both pipes after the retention limit. Process cleanup also kills the child on caller cancellation or stdin failure.
- `BoundedConsoleProgress.cs` receives producer updates without posting dispatcher work per chunk. It bounds both text and object count, preserves output categories, and ignores producers after disposal.
- `RuntimeTerminalPresenter.cs` drains that mailbox on the UI thread. It bounds the document, disables undo, resets between jobs and stops its timer when the window closes. Each job gets a new mailbox, so late output from a previous job is ignored.
- `GuidedGrade.Runner.csproj` compiles the same buffer and limit sources into the guest worker. The JSON protocol preserves truncation notices. **Republish the runner and point execution settings at the updated publish folder** to apply the changes inside Hyper-V; an older published worker does not gain the changes from rebuilding the desktop app alone.

## Verification

The regression tests exercise a generated 20-million-character stream with an allocation ceiling, simultaneous flooded build pipes, redirected/ConPTY floods followed by interactive input, continuous activity after buffer saturation, ring-buffer cursor rollover, mailbox chunk limits, and real WPF document/undo behavior on an STA thread. Runner-protocol tests build and interact with a temporary console application in normal and flooded-output modes without LLM credentials.

```powershell
dotnet test "GuidedGrade.Tests/GuidedGrade.Tests.csproj" --filter "FullyQualifiedName~OutputMemoryTests|FullyQualifiedName~RuntimeTerminalPresenterTests|FullyQualifiedName~InteractiveProcessSessionTests|FullyQualifiedName~ProcessRunnerTests|FullyQualifiedName~ConsoleDriverAgentTests|FullyQualifiedName~AiTestQueueTests|FullyQualifiedName~RunnerWorkerTests"
```

The worker-protocol tests run the worker locally; they do not validate a real Hyper-V guest deployment or live model calls.

The agent also maintains a fixed 120-by-30-character `ConsoleScreen` projection for each output stream. See [console interaction](CONSOLE_INTERACTION.md) for prompt selection, terminal-control handling, input validation and cancellation behavior.

## Job queue memory

The solution scheduler and shared LLM scheduler each accept 50 active/pending jobs and retain 50 completed snapshots. Snapshots keep at most 160 title characters and 32,000 result characters plus a truncation marker. Finished snapshots do not retain delegates or completion tasks. Pending LLM jobs retain prompts; provider responses are still read in full before the queue display is truncated. User-authored free-form input is limited to 32,000 characters. The queue panel polls snapshots every 300 ms. These are not total application RAM limits.

## Remaining memory work

The feedback cache retains at most 128 files, with the active file pinned. Draft text caches retain 64 contexts and persist in SQLite. Source/text reads reject files over 2 MiB without truncating input; settings use a 1 MiB read limit. Folder trees remain eager but stop at 5,000 items or 32 levels and skip reparse points. Binary fslog decoding retains its separate existing limit. Local models and student programs can consume RAM independently of captured output. Those remain separate improvements; see the [knowledge graph](KNOWLEDGE_GRAPH.md) for their relationships to the rest of the app.

### Ollama GPU-memory recovery (September 16, 2026)

An explicit CUDA/GPU out-of-memory HTTP server error triggers one CPU retry within the same queued request. Cancellation still applies, and no subsequent job starts during this retry. The same prompt and response schema are preserved. CPU fallback can be slower and consume host RAM; it does not change saved provider settings or impose a RAM budget. Failed jobs retain the server explanation (up to 4,000 characters); unrelated HTTP errors are not retried.

Clear review removes the selected file's in-memory comment list and persisted rows. It retains the combined draft/import markers and stores one review-generation counter per cleared file for the window lifetime so active section requests cannot restore cleared comments. The generation cache retains 4,096 file paths. Evicted in-flight captures become stale and cannot publish feedback; capturing the file again receives a new unique token.

UI-framework migration: window and panel hosts are explicitly disposed on close/replacement; removed inline cards release their subscriptions. Queue visuals use keyed VirtualList rows with a finite viewport; retained snapshots keep the scheduler's existing count/text limits. Read-only suggested-code and job-result editors disable undo; the editable feedback draft has a 100-operation native undo limit. Native RichTextBox terminal/mailbox budgets remain unchanged. Reactive text state and WPF text content can hold separate copies; this migration does not establish a lower process-memory ceiling or clear accumulated drafts.

Extraction workers own linked cancellation sources and dispose them in finally. Closing the framework dialog cancels active workers, disposes its global source and ViewHost, and releases native progress bars; cached token structs stay readable while workers unwind. Late completion/progress updates are ignored after close.

Visual refinement: collapsed inline cards do not mount their detailed text editors until expanded;
approval collapses those details. Hosts own scoped native theme dictionaries alongside framework styles.
No process-memory improvement has been measured.

September 23 shell conversion: MainWindow explicitly owns and disposes all shell ViewHosts,
the root layout host, panel hosts and GradingView. Framework StateList stores file-tab paths
rather than native button objects. Source/console documents and native tree selection retain
their control identity through unrelated reactive updates. Native sizing adapters do not add
timers; the existing queue timer stops on window close. Code-built theme dictionaries replace
XAML resources without changing the documented process-memory limitations.

Overall reviews now retain one current overall card per reviewed file and student/lab/assignment context in the comment cache/SQLite,
plus the existing appended submission draft. Multi-file overall reports are copied to each checked
file; no report-size or cache-eviction budget is introduced. Pending jobs capture file paths and
review-generation numbers so cleared files cannot be repopulated by those jobs.

## Provider HTTP transport ownership

Azure and Ollama each retain one process-lifetime HTTP client, shared across service instances and request paths. They do not create a connection pool per grading request. Connections have a five-minute pooled lifetime; cookie storage is disabled. Azure credentials remain on each request, not shared default headers. Requests/responses are disposed after use, including Ollama availability checks. Injected test clients are caller-owned. This bounds the number of client pools, not total response size or model memory.

October 5 review isolation: each saved/cache comment retains a local ReviewContext string, and draft keys also include the lab folder. The file cache retains assignment context but now evicts older inactive files at 128 entries. Batch snapshots retain file paths, rubric copies and identifier-redaction inputs. Runtime reports are excluded from grading prompts; bounded redacted console context is sent only for interactive next-input choices. No total-memory improvement has been measured.

October 6 student grades: MainWindow owns a window-lifetime dictionary loaded from StudentGrades in assignments.db. Each record contains a review-context key and two point values; records have no count limit or eviction. GradeTotals scans these records for current-course totals. StudentGradeRow hosts use the pinned framework inside native ListBox virtualization; hosts dispose on unload or data-context replacement. The modal grade editor disposes its host on close. No process-memory reduction is claimed.

October 6 batch replacement update: batches now retain only the current whole-file request/response during sequential overall generation instead of invoking section grading. ClearBatchFileReview temporarily loads saved reviews to reconcile intact generated draft blocks; caches for cleared files are removed and import markers reset. Instructor-edited drafts now persist to SQLite after edits; a 64-context cache evicts older text, and each draft has a 1 Mi-character ceiling. Build/run remains optional; no process-memory bound is claimed.

October 6 saved-review navigation: each lookup materializes distinct non-rejected file/context locations, filters to the selected student and assignment, and releases the temporary list after rendering/navigation. There is no new long-lived location cache or pagination; lookup cost grows with saved review locations. Inline overall-card viewport positioning does not duplicate comment payloads.

Source reviewed 2026-10-08: The bottom Logs tab uses LogPanelView/LogPanelViewModel through the pinned framework native adapter for source history list/text controls. FsLogReader implements the supplied ResultsDecoder v2 FSLG layout: named files, Unix-second timestamp groups, uint32-sized snapshots with byte-offset-128 decoding using the local ANSI code page. It validates signatures, versions, chunk boundaries and limits (32 MB input, 256 names, 10,000 snapshots). The viewer retains one loaded document as decoded strings, releases its stream after loading, and clears old content on assignment/student refresh. Historical timestamps and source are local viewer data and are not added to LLM payloads or treated as build counts/grades. Log options uses native menu styling alongside the bottom tab navigation.
Assignment setup stores optional relative ReviewFilePaths and LogFilePath in AssignmentOptions JSON; existing records default to empty. Saved paths are validated (optional **/ prefix, no absolute/parent paths), copied into queued snapshots, and prefill batch file selection. Logs resolves the saved .fslog path within the current student folder using newest-match resolution. Without a configured path, it searches the nearest project/solution directory of the active file; no project context yields no automatic log. Manual Open remains available. A configured assignment log can load without opening a source file. Only selected review-file contents and grading criteria are sent to models; path configuration and decoded log history are not sent.

Source reviewed 2026-10-08: FsLogSnapshot preserves its serialized timestamp-group ordinal as BuildNumber; FsLogDocument.BuildCount counts those groups, including groups sharing a timestamp, rather than multiplying by file count. FileHandler.ParseFile now decodes fslog instead of parsing binary data as build-output text. LogPanel shows high-contrast source/history, recorded build counts, elapsed span, largest gap and a BuildHistoryView/BuildHistoryViewModel timeline with hover details and click-to-select snapshots. Timeline time spacing includes breaks, not measured active work time. ANSI decoded history remains local and one bounded log is retained; timeline renders marks without creating a control per build.
CourseReviewRules stores instructor-defined rules keyed by course in SQLite, loaded into assignments and immutable review snapshots. Assignment setup Class settings offers a PG2-only preset for no lambdas, header/.cpp separation with getter/setter exceptions and reference/const use from Part B. No preset is automatically enabled. Shared grading instructions ask for verified source evidence and scope checks; they are model review guidance, not deterministic C++ AST checks and do not add entries to the lexical Violations count automatically. Numeric penalties require separately configured assignment deductions.

Source reviewed 2026-10-09: Both manual and batch multi-file rubric reviews use OverallReviewService and OverallReviewResult rather than accepting free-form model totals. Criterion IDs map to exact configured rubric rows; C# rejects missing/duplicate rows, invalid ranges and invented deduction IDs, calculates totals from earned points and configured penalties, and keeps instructor-confirmation penalties pending. Verified findings require exact source excerpts from the indicated supplied file; unsupported findings become unverified and withhold the final grade. Renderer enforces saved brief paragraph/justification word caps and enabled output sections. Source-quote matching proves the excerpt exists, not the correctness of model interpretation; semantic review still requires instructor oversight. No-rubric qualitative requests retain the text path.
Ollama combined structured reviews explicitly set temperature zero and estimate context needs from prompt characters plus output headroom, with an 8,192-token minimum and 32,768-token ceiling. Oversize estimates reject the request before HTTP; CPU fallback retains the context setting. Estimates are not tokenizer-accurate and do not prove all code is attended to. Azure receives the same JSON contract through its existing completion transport; schema enforcement is local validation, not provider constrained decoding. File payloads retain anonymous IDs/extensions and explicit end boundaries; no student paths or log history are added.
Replacing an overall file review removes matching intact old generated draft blocks, including legacy combined-report prefixes, before presenting the new review. Instructor-edited blocks remain. Multi-file completion persists one shared result per selected file for discovery while the side panel imports the shared report once.


October 9, 2026 maintenance source review: combined reviews accept at most 32 files and 8 MiB of UTF-8 source before sanitization. Reads reject oversized input, including files that grow during a read, before any model request. Reports, provider responses, regex working strings, persisted database rows and the batch progress list still have costs outside these limits. No total process RAM ceiling is claimed. Draft-save failures remain visible and trigger an unsaved-close confirmation. Restored review import markers remain session-local and bounded to 4,096 entries; edited draft text itself is durable.
