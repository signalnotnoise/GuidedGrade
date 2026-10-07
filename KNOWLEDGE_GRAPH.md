# GuidedGrade dependency knowledge graph

Latest affected-path source review: October 7, 2026 (persistent student grades, active assignment/lab badges and current-course running totals; configurable console model wait and local/grading confirmations; queued-review student/lab/assignment isolation; criteria-and-source-only grading payloads; console-choice prompts and user-authored free-form tasks; PR #2 review: both legacy data roots, diagnostics conflict preservation and runner credential default; GuidedGrade product, project, assembly and namespace rename; automatic roaming/local product-data migration; process exit-code propagation; shared provider HTTP ownership; runner credential defaults and benchmark provenance; packaged application-owned COM cleanup, failure reporting and ordered shutdown; file-tree virtualization and active-file selection retention retained). Reviewed C# startup, shell composition, native control ownership, declarative file tabs/pickers/navigation/status, code-built native styles, panel state, persistence paths and disposal. All application-owned XAML has been removed. Nullable contracts and student folder validation were also reviewed; default discovery skips malformed entries; per-course folder-name mode lists all immediate folders, and missing Ollama content retains the existing no-response fallback.

Source-reviewed map of the working tree, updated September 23, 2026 (restored File/Settings menus and numbered workflow toolbar; UI-framework hosts/state/lifecycle, native dark styles, review toolbar and collapsed review cards, retained native islands and extraction cancellation, file Clear review persistence/cache invalidation, native post-build paths and Console diagnostics, explicit overall/section feedback menus, overall rubric prompts, saved execution preferences and run-only worker path reviewed; job scheduling, provider cancellation, GPU-memory fallback, pinned workspace tabs, visibility persistence and section detection reviewed). Arrows are labeled with the relationship: calls/uses, data flow, ownership, or implementation. This maps the application components and support tools rather than every method. Grouped nodes expand in the component tables below.

## Application overview

October 6, 2026 graph-tooling source review: [offline searchable file/project map](docs/knowledge-graph/graph.html)
and [canonical JSON](docs/knowledge-graph/graph.json) supplement this manually reviewed runtime map.
`scripts/update-knowledge-graph.py` extracts supported file nodes, directory membership,
relative documentation links and declared XML project/package references. It does not infer C# call paths.
The staged-input pre-commit hook updates only generated outputs; GitHub Actions validates freshness.
First-time setup stages the extractor and renderer together. If the renderer is absent
from the index, generation stops before writing outputs and explains staging or
`--worktree` preview options (source reviewed October 6, 2026).
Worktree previews include non-ignored new source files without staging existing application changes.
October 6 viewer source review: the HTML embeds pinned MIT Three.js 0.160.1
and its license from scripts/knowledge-graph-vendor, excluded from graph nodes.
It supports pan/zoom/fit, filters, selected-edge highlighting, relationship navigation
and neighborhood filtering. A native node selector/inspector works without WebGL.
The renderer and vendor assets use the same index/worktree input mode as extraction.

```mermaid
flowchart LR
    Index[Git index] --> Extract[Python graph extractor]
    Preview[Worktree preview] --> Extract
    Extract --> JSON[graph.json]
    JSON --> HTML[Offline graph.html]
    Vendor[Pinned Three.js and MIT license] --> HTML
    Hook[Pre-commit hook] --> Extract
    Hook --> Stage[Stage graph outputs only]
    CI[GitHub Actions] --> Check[Fixture tests and stale-output check]
```

```mermaid
flowchart LR
    Startup[App.cs STA entry] --> Shell[MainWindow.cs]
    Startup -->|creates packaged owner before controls| Cleanup[WpfComCleanupPolicy]
    Cleanup -->|ContextIdle / OnExit after window disposal| Drain[CLR COM cleanup]
    Cleanup -->|failures persist across retries| Diagnostics[Local diagnostics log / nonzero exit]
    Startup -->|returns Application.Run exit code| Exit[OS process status]
    Shell --> Views[Framework views and state]
    Views --> Actions[Toolbar / pickers / file tabs / navigation / status]
    Shell --> Adapter[Native sizing adapters]
    Adapter --> Hosts[Framework pane hosts]
    Hosts --> Native[Stable editor / tree / terminal / panel controls]
    Shell -->|closes and disposes| Lifetime[Shell / panel / grading ViewHosts]
```

```mermaid
flowchart TD
    UI["MainWindow · coordinates navigation and grading"]
    Import["Import · ZIPs, students and source files"]
    Editor["AvalonEdit · source and inline feedback"]
    Rules["Violation checks · configured source rules"]
    Queue["AiTestQueue · one solution test at a time"]
    Jobs["LlmJobQueue.Shared · assignments before general requests"]
    JobPanel["Right-side Job queue tab · counts, status, cancellation and free-form tasks"]
    Execute["SubmissionExecutionService · builds and tests"]
    Agent["ConsoleDriverAgent · reads prompts and types input"]
    Grade["SectionGradingService · rubric feedback"]
    Model["Ollama / Azure · model completions"]
    Store["SQLite · assignments, student grades and comments"]
    GradeRecords["GradePersistenceService · captured review scope"]
    Totals["GradeTotals · earned / possible for current course"]
    GradeUI["MainWindow.Grades · row badges, footer and grade editor"]
    UI --> GradeUI
    GradeUI -->|instructor saves or clears| GradeRecords
    GradeRecords -->|StudentGrades in assignments.db| Store
    GradeUI -->|cached records| Totals
    Totals -->|points and percentage| GradeUI
    Terminal["Terminal presenter · bounded live output"]
    Reports["Text reports · saved execution evidence"]
    Panels["Stable Comments / Job queue / Rubric tab host"]
    Preferences["WorkspacePanelPreferences · per-user JSON visibility"]
    UI -->|selects content without replacing host| Panels
    Panels -->|retains queue controls| JobPanel
    UI -->|loads and applies Settings choices| Preferences
    UI -->|loads| Import
    UI -->|displays| Editor
    Editor -->|Approve persists status and imports once| ApprovedDraft["Matching Comments draft and live editor binding"]
    UI -->|scans source| Rules
    UI -->|enqueues metadata| Queue
    UI --> BatchDesign["MainWindow.BatchReview · file-list designer and preview"]
    BatchDesign --> BatchPlan["BatchReviewPlan · per-student paths and sequential loop"]
    BatchPlan -->|optional leading **/; newest modified match within student folder| NestedPaths["Recursive suffix resolution · skip links; newest timestamp wins"]
    BatchDesign -->|one captured batch delegate| Queue
    BatchPlan -->|optional build/run once per student| Execute
    BatchPlan -->|clear selected file reviews, invalidate old responses| BatchClear["ClearBatchFileReview and ReviewDraftCleanup"]
    BatchPlan -->|one overall request per file, sequentially| BatchOverall["OverallFeedbackPrompt and CompleteOverallFileReview"]
    Queue -->|runs UI-supplied job| Execute
    Execute -->|drives session| Agent
    Agent -->|redacted console and testing instructions| Jobs
    JobPanel -->|user-authored free-form request| Jobs
    UI -->|shows snapshots and cancels jobs| JobPanel
    Jobs -->|one provider request at a time| Model
    Execute -->|reports output| Terminal
    UI -->|after execution: captured criteria and source only| Grade
    Grade -->|assignment request| Jobs
    Grade -->|feedback returned through UI| Editor
    UI -->|loads and saves| Store
    Store -->|reviewed-file locations ordered by saved time| SavedNavigation["Student selection / Comments links / queue open-review action"]
    SavedNavigation -->|exact student and assignment context| Editor
    UI -->|saves runtime evidence| Reports
```

`LlmJobQueue.Shared` serializes provider requests process-wide; assignments precede waiting general jobs without interrupting active requests. The right-side queue tab shows solution jobs and model requests separately; its status indicator counts both levels separately. `AiTestQueue` wraps another scheduler instance to preserve whole-test execution/disposal ordering and solution-path deduplication. Cancelling a solution propagates to execution and runtime grading. Cancelling an individual model request does not cancel unrelated requests or the entire grading batch.

`MainWindow` is the main coordination point. Queue jobs are delegates supplied by it: `AiTestQueue` does not itself reference execution or grading services. Runtime reports remain local. After execution and disposal, the delegate grades captured files using criteria and source only.

## Execution and isolation

```mermaid
flowchart TD
    Exec["SubmissionExecutionService · execution workflow"]
    Detect["RunnableSubmissionDetector · identifies runnable source"]
    Policy["SubmissionExecutionPolicy · saved local authorization or prompt"]
    Stage["AiTestStaging · working copy"]
    Build["SubmissionBuilder · selects compiler and command"]
    Process["ProcessRunner · drains build stdout/stderr"]
    Tools["MSBuild / dotnet / C++ / Java / Python"]
    Session["InteractiveProcessSession · child I/O and cleanup"]
    Contract["IInteractiveConsoleSession · common interface"]
    Agent["ConsoleDriverAgent · adaptive console interaction"]
    VM["HyperVRunner · packages inputs and sends requests"]
    Bridge["HyperVBridge.ps1 · creates and removes VM"]
    Worker["Runner Program.cs · guest JSON command loop"]
    Watch["HyperVWatchdog.ps1 · emergency VM cleanup"]
    Exec -->|detects submission| Detect
    Exec -->|local mode checks| Policy
    Exec -->|local AI testing copies| Stage
    Exec -->|manual local build/run uses original folder| Detect
    Exec -->|Run resolves existing output without building| Build
    Exec -->|inherits build workflow| Build
    Build -->|runs tool commands with Windows output paths| Process
    Exec -->|manual result via MainWindow| Diagnostics[Scrollable Console panel]
    Process -->|starts| Tools
    Exec -->|local mode starts| Session
    Exec -->|Hyper-V mode creates| VM
    VM -->|JSON over redirected pipes| Bridge
    Bridge -->|PowerShell Direct starts and relays| Worker
    Bridge -->|starts independent monitor| Watch
    Worker -->|build or resolve existing output command| Build
    Worker -->|start and poll commands| Session
    Session -.->|implements| Contract
    VM -.->|implements| Contract
    Exec -->|runs| Agent
    Agent -->|polls, types and stops through| Contract
```

The guest worker compiles shared execution sources directly; it does not load the WPF application. Local tests and Hyper-V tests share the same console capture implementation. The queue awaits service disposal, but emergency watchdog cleanup can outlive a failed bridge; this is not a machine-wide VM lock.

| Component | Responsibility and important dependencies |
| --- | --- |
| `SubmissionExecutionService` | Chooses local/Hyper-V execution, gathers source context, creates runtime reports; also exposes Build, Build and Run, and Run without compilation. Manual local actions use the original submission directory, so a later Run finds built output. |
| `SubmissionBuilder` | Chooses language-specific build commands and resolves runnable outputs. Uses `ProcessRunner`, the detector and installed toolchains. |
| `RunnableSubmissionDetector` | Finds solutions, projects, source languages and runnable outputs; filters irrelevant directories. |
| `AiTestStaging` | Copies local test inputs to a staging directory and rewrites original path references. |
| `NativeDependencyStager` | Finds/copies DLL dependencies and supplies extra runtime PATH directories. |
| `PEHeaderReader` | Determines whether a Windows executable uses the console subsystem, guiding ConPTY selection. |
| `InteractiveProcessSession` | Starts child processes, pumps redirected or ConPTY I/O, waits for idle, writes input and disposes native/process resources. |
| `IInteractiveConsoleSession` | Allows the console agent to use the local session and Hyper-V proxy through the same API. |
| `HyperVRunner` | Validates configuration, packages input archives and drives the bridge protocol; exposes guest status/output to the agent. |
| `GuidedGrade.Runner/Program.cs` | Handles build/start/poll/input/close/kill JSON commands in the worker process. |
| `ConsoleDriverAgent` | Uses local menu planning or a model choice from redacted console output and testing instructions, validates the result, and sends one input line to the running program. |
| `ConsoleScreen` | Maintains a bounded readable projection of cursor-positioned terminal output for the agent; separates current screen/cursor context from history. See [console interaction](CONSOLE_INTERACTION.md). |
| `ConsoleInputPolicy` | Converts displayed menu labels to keys and validates single integers when the matching C++ input-read source provides evidence; failed validation allows one model correction before stopping inconclusively. |
| `ConsoleSourceContext` | Retained source-excerpt utility with test coverage; console model requests use output and testing instructions instead of source excerpts. |
| `NativeExitCodes` | Classifies/describes Windows exit codes for reports. |
| `SubmissionExecutionPolicy` | Accepts the saved Local preference with ConfirmLocalExecution disabled, or asks for per-operation consent; the explicit local-test menu preserves that preference. Isolation failure never grants local execution. |
| `AdversarialInputPlanner` | Prompt/parser and default cases for adversarial inputs. Currently has no production caller in the WPF source; it is not the active console driver. |

## Output and memory ownership

```mermaid
flowchart TD
    Child["Student process · stdout/stderr or ConPTY"]
    Capture["InteractiveProcessSession · pumps output"]
    Ring["BoundedTextBuffer · 64,000-char tail per stream"]
    Agent["Console agent / report · bounded snapshots"]
    Mail["BoundedConsoleProgress · 16,000 chars / 128 chunks"]
    Render["RuntimeTerminalPresenter · 100 ms batches"]
    Doc["RichTextBox · 100,000 chars / 256 runs, no undo"]
    Compiler["Compiler process · build output"]
    Build["ProcessRunner · first 8,000 chars per pipe"]
    BuildReport["Build result · retained output and truncation notice"]
    Limits["OutputLimits · shared budgets"]
    Child -->|emits| Capture
    Capture -->|retains tail| Ring
    Ring -->|slices by absolute cursor| Agent
    Capture -->|local progress reports| Mail
    Mail -->|timer drains| Render
    Render -->|trims oldest history| Doc
    Compiler -->|pipes drained to EOF| Build
    Build -->|returns| BuildReport
    Limits -.->|configures| Ring
    Limits -.->|configures| Mail
    Limits -.->|configures| Render
    Limits -.->|configures| Build
```

For Hyper-V, the worker sends bounded output through its JSON protocol and `HyperVRunner` forwards it into the host mailbox. `AiTestQueue` bounds concurrent work and waiting-job count separately from these output budgets. See [output memory limits](MEMORY_LIMITS.md) for exact policies, tests, deployment notes and remaining risks.

## Grading, models and persistence

```mermaid
flowchart TD
    UI["MainWindow · assembles context and handles review"]
    Context["RelatedFileResolver · headers and companion source"]
    Grade["SectionGradingService · rubric prompt and parsed feedback"]
    Sanitize["StudentDataSanitizer · removes identifiers"]
    Agent["ConsoleDriverAgent · validated next input"]
    Jobs["LlmJobQueue.Shared · priority and cancellation"]
    Ollama["OllamaService · local model HTTP calls"]
    Azure["AzureOpenAIService · Azure HTTP calls"]
    Feedback["SectionFeedback · issues, suggestions and review state"]
    Overlay["InlineCommentLayer + Adorner · review controls"]
    Comments["CommentPersistenceService · SQLite comments"]
    Assignment["AssignmentPersistenceService · SQLite rubric library"]
    Settings["LLMSettings · provider, execution and model settings"]
    UI -->|resolves dependencies| Context
    UI -->|explicit section action: source and rubric only| Grade
    UI -->|explicit overall action: captured assignment and checked source files| Overall[OverallFeedbackPrompt]
    Overall -->|combined rubric review prompt| Ollama
    Overall -->|combined rubric review prompt| Azure
    Grade -->|sanitizes prompt context| Sanitize
    Errors["LlmHttpErrors · bounded server error details"]
    Ollama -->|GPU out-of-memory: one CPU retry in same job| Ollama
    Ollama -->|formats failed HTTP responses| Errors
    Azure -->|formats failed HTTP responses| Errors
    Ollama -->|enqueues HTTP delegate| Jobs
    Azure -->|enqueues HTTP delegate| Jobs
    Grade -->|selected provider| Ollama
    Grade -->|selected provider| Azure
    Screen["ConsoleScreen · readable current terminal state"]
    Agent -->|feeds received terminal updates| Screen
    Screen -->|screen and cursor context| Agent
    InputPolicy["ConsoleInputPolicy · menu keys and input validation"]
    Agent -->|checks before typing| InputPolicy
    Agent -->|redacted screen and testing instructions| Route[LlmCompletionService]
    UI -->|explicit user-authored free-form prompt| Route
    Route -->|selected provider| Ollama
    Route -->|selected provider| Azure
    Azure -->|request-local API key and URL| AzureHttp[Process-owned Azure HttpClient]
    Ollama -->|request-local URL and model| OllamaHttp[Process-owned Ollama HttpClient]
    Settings -.->|configures| Grade
    Settings -->|saved ConfirmGrading| Confirm[GradingConfirmation]
    UI -->|one confirmation per requested batch| Confirm
    Confirm -->|approved or prompting disabled| Grade
    Settings -->|ConsoleModelWaitSeconds within overall deadline| Agent
    Grade -->|returns| Feedback
    UI -->|renders feedback| Overlay
    UI -->|Clear review deletes file records| Comments
    UI -->|Clear review invalidates pending section publication| Generation[ReviewGeneration]
    UI -->|Clear review removes visible comments| Overlay
    Overlay -->|approve, reject, regenerate events| UI
    UI -->|loads and saves feedback| Comments
    UI -->|loads and saves assignment definitions| Assignment
    DataPaths["AppDataPaths · GuidedGrade roaming/local roots"] --> Comments
    DataPaths --> Assignment
    Legacy["LabFeedbackWPF and Lab Feedback WPF legacy directories"] -->|move missing data on access| DataPaths
```

| Component | Responsibility |
| --- | --- |
| `RelatedFileResolver` | Finds paired files/includes and extracts declared types so related definitions can be included in grading. |
| `OverallFeedbackPrompt` | Captures assignment requirements and rubric instructions for one combined review of checked files; requests criterion scores and one total, or qualitative feedback when no rubric exists. Includes sanitized source supplied by MainWindow. |
| `SectionGradingService` | Sanitizes source, builds a rubric-based prompt, calls the selected provider and parses `SectionFeedback`. |
| `StudentDataSanitizer` | Redacts student identifiers, emails and personal paths; supplies anonymous display names. |
| `LlmCompletionService` | Routes console-choice and user-authored free-form requests; section grading calls provider services directly. |
| `OllamaService` / `AzureOpenAIService` | HTTP adapters with one process-lifetime client per provider, reused across grading, console choices, user tasks and settings checks. Pooled connections have a five-minute lifetime; cookies are disabled. Azure API keys are request-local. Injected test clients remain caller-owned. Model memory is outside the WPF output budgets. |
| `AppDataPaths` | Owns `%APPDATA%/GuidedGrade` and `%LOCALAPPDATA%/GuidedGrade`; migrates both legacy product directories on access and preserves conflicting files in their original locations. |
| `CommentPersistenceService` | Stores feedback keyed to file, ReviewContext, section and line range, including review state, in `section-comments.db`. |
| `AssignmentPersistenceService` | Stores course/assignment requirements and rubric definitions in SQLite. |
| `InlineCommentLayer` | Positions feedback alongside AvalonEdit lines and relays review events. |
| `InlineCommentAdorner` | Owns a disposable framework ViewHost with expanded/approved state, bounded scrollable details and approve/regenerate/reject events. |

## Import, navigation and grading interface

```mermaid
flowchart TD
    UI["MainWindow · folder/file navigation"]
    Zip["ZipFileHandler · validates and extracts ZIPs"]
    Progress["ExtractionProgressDialog · progress and cancellation"]
    Student["Student / Assignment · submission folders"]
    File["FileHandler · discovers source, parses logs/results"]
    Tree["FileSystemItem · checked/expanded tree nodes"]
    Config["ViolationsConfigService · terms and file patterns"]
    Match["ViolationsMatcher · source matches and context"]
    Highlight["ViolationHighlighter · AvalonEdit backgrounds"]
    GradeView["GradingView · scores, deductions and feedback text"]
    UI -->|owns and disposes declarative panels| Host[ViewHost from SignalNotNoise.UI.Wpf]
    Forms[ReviewWindow settings and grading forms] -->|owns and disposes| Host
    Theme[ReviewTheme] -->|tokens and inherited colors| Host
    State[State and StateList] -->|reactive descriptions| Host
    Host -->|native WPF renderer| Native[Windows controls]
    UI -->|retains specialized native controls| Native
    Zip -->|reports extraction work| Progress
    UI -->|discovers folders| Student
    Student -->|folder parsing helpers| File
    UI -->|opens files and loads results| File
    UI -->|populates| Tree
    UI -->|loads rules| Config
    UI -->|passes rules and source| Match
    UI -->|passes matched lines| Highlight
    UI -->|loads results and student| GradeView
```

| UI or model | What it represents / does |
| --- | --- |
| `App.cs` | Explicit STA entry point creates Application/MainWindow and uses OnMainWindowClose shutdown. ReviewTheme supplies the app palette; no generated startup or XAML load remains. |
| `MainWindow` | Owns the editor, navigation, solution queue, grading commands, comment cache and runtime terminal. `MainWindow.JobQueue.cs` refreshes the independent right-side queue tab and count indicator, routes cancellation and submits user-authored free-form tasks. |
| `LlmJobQueue` | Thread-safe assignment/general priority scheduler, used process-wide by providers and separately by the solution wrapper. Bounded active/pending count and completed snapshots; cancellation tokens reach HTTP calls. |
| `SettingsWindow` | Edits violation terms and scanned file patterns. |
| `LLMSettingsWindow` | Edits/test-connects model providers and configures execution mode, worker folder and VM inputs. |
| `AssignmentSetupWindow` | Edits/saves course requirements and rubric items. |
| `SectionGradingDialog` | Selects rubric items for a section. |
| `ExtractionProgressDialog` | Framework operation cards with native progress bars; cancellation on close; worker-owned token sources. Sources: `Views/ExtractionProgressDialog.cs`, `Views/ExtractionOperation.cs`. |
| `GradingView` | Declarative score, deduction and remarks view in `Views/GradingView.cs`; native HTML editors and sliders retain specialized editing behavior. Produces feedback HTML. |
| `Student` | Student identity and folder, with folder discovery/parsing. Defaults to Last_First-ID parsing, skipping invalid folders. Per-course folder-name mode instead lists every immediate directory by its literal name with no fabricated ID. |
| `Assignment` | Submission assignment folder discovery and consolidation. Distinct from the rubric definition below. |
| `GradingAssignment` / `RubricItem` | Course, requirements, rubric criteria, points and grading totals. |
| `SectionFeedback` / `FeedbackReviewStatus` | Per-section findings, suggested code/score and pending/approved/rejected state. |
| `FileSystemItem` | File/folder tree state, selection flags and child nodes. |
| `LabResults` / `TestResult` | Imported test-result data from `AssignmentResults.cs`. |
| `Result` | Parsed legacy log summary item. |
| `Deduction` | Score reduction or automatic-zero choice. |
| `Violation` | A matched rule with line/context information. |
| `LLMSettings` / `LLMProvider` / `SubmissionExecutionMode` | JSON-backed model and execution preferences. |
| `InverseBoolToVisibilityConverter` | Converts view state to WPF visibility. |

## Projects, libraries and support tools

| Boundary | Connected components and purpose |
| --- | --- |
| `GuidedGrade.csproj` | Windows .NET 10 app. Uses pinned SignalNotNoise.UI.Wpf local packages for declarative surfaces, native WPF islands/AvalonEdit for specialized controls, Microsoft.Data.Sqlite/SQLitePCLRaw for persistence. |
| `GuidedGrade.Runner.csproj` | Separate worker executable. Compiles shared builder, detector, process/session, dependency, PE-header and bounded-output sources. |
| `GuidedGrade.Tests.csproj` | MSTest service, queue, output, WPF presenter and worker integration tests. |
| `HyperVBridge.ps1` | VM lifecycle, archive transfer and JSON request relay to guest worker. |
| `HyperVWatchdog.ps1` | Stops the exact owned VM if its bridge disappears or its lifetime expires. |
| `Inspect-RunnerHost.ps1` | Host diagnostics including virtualization and available memory. |
| `Setup-RunnerTemplate.ps1`, `Invoke-RunnerTemplateSetup.ps1`, `Install-RunnerGuestTools.ps1` | Prepare the reusable Windows guest/template and its development tools. |
| `New-RunnerAnswerMedia.ps1`, `Download-RunnerIso.py` | Setup support for unattended answer media and installation media. |
| `Resources/*.xshd` | AvalonEdit syntax/theme definitions. |
| `AI_TEST_QUEUE.md`, `MEMORY_LIMITS.md`, `EXECUTION_SAFETY.md` | Queue behavior, output budgets and runner deployment/consent documentation. |

This graph is a documentation snapshot. Update it when a service is added, a call path changes, or memory ownership moves. Remaining memory work is concentrated in `MainWindow`'s feedback cache/eager tree and whole-file reads in `FileHandler`/source-context construction.

### Menu coverage planner
ConsoleDriverAgent uses ConsoleScreen.MenuContext to supply visible rows through the cursor to ConsoleMenuCoverage. ConsoleMenuCoverage tracks selections per heading and option list, schedules exit choices last, and returns coverage summaries to the agent report. ConsoleInputPolicy validates the chosen key before InteractiveProcessSession sends it. Unrecognized prompts are sent to the model as redacted console context with testing instructions. Validated model input is sent to the running program; invalid replies, timeout or uncertainty stop inconclusively.


## Review workspace and feedback flow

```mermaid
flowchart LR
    Selection[Student + lab folder + assignment + file] --> Workspace[MainWindow.Workspace]
    SQLite[(section-comments.db)] --> Persistence[CommentPersistenceService]
    Persistence --> Load[LoadCommentsForFile]
    Load --> Filter[Match saved review context]
    Filter --> Approved[Approved feedback import]
    Approved --> Text[SavedFeedbackText]
    Selection --> Target[Draft destination captured before async work]
    Target --> Routing[Completion routing by captured draft key]
    Generated[Generated section feedback] --> Routing
    Routing --> Text
    Approval[Approve existing section] --> Persistence
    Approval --> Status[Update review status without appending draft text]
    Text --> Draft[Editable session draft]
    Analysis[Whole-file AI analysis] --> Draft
    Workspace --> Draft
    Draft --> Formatter[FeedbackCopyFormatter]
    Formatter --> TXT[Plain text]
    Formatter --> MD[Markdown]
    Formatter --> HTML[Escaped colored HTML markup]
    TXT --> Clipboard[Text clipboard]
    MD --> Clipboard
    HTML --> Clipboard
    Draft --> Export[Text-file export]
    Tools[MainWindow.ToolsPanel] --> Console[Bounded runtime terminal]
    Tools --> Violations[Violation list]
    Close[Close last file / empty editor] --> Clear[Clear inline comment overlay]
    Late[Late comment rendering] --> Guard[Require originating file and review context]
    Guard --> Overlay[Inline comment overlay]
```

Draft edits stay in memory; the arrow from SQLite imports original section records and does not save edited drafts back. Import tracking avoids repeated appends within a session. Tool-tab collapse frees the bottom content row without clearing capture buffers. See [feedback workspace documentation](FEEDBACK_WORKSPACE.md) for limitations and review findings.

## Decimal scores and rubric validation

```mermaid
flowchart LR
    Import[Rubric text import] --> Finite[Finite positive maximum points]
    Setup[Assignment setup save] --> Finite
    Finite --> Rubric[RubricItem double point values]
    Rubric --> Save[AssignmentPersistenceService validation]
    Save --> Assignments[(assignments.db / rubric JSON)]
    Response[Model SCORE text] --> Parse[SectionGradingService invariant decimal parsing]
    Parse --> Score[SectionFeedback.SuggestedScore : double]
    Score --> Comments[CommentPersistenceService]
    Comments --> DB[(section-comments.db)]
    DB --> Read[GetDouble: historical integers and fractional scores]
```

New section-score columns use REAL affinity. Existing SQLite INTEGER-affinity columns can retain fractional values; no destructive table migration is required. Setup and persistence validation reject nonfinite or nonpositive rubric maxima. DecimalFeedbackTests cover decimal parsing under a non-English culture and compatibility with an existing INTEGER-affinity column.

### Current behavioral boundaries
- Batch and queued grading capture the destination draft before awaiting work. Results for another selection remain in that draft rather than opening the currently selected student's panel.
- Approval persists status without republishing feedback. Generated sections and restored approved sections share import tracking and SavedFeedbackText.
- Empty-editor transitions clear inline overlays. RenderCommentsForFile ignores late results for a file that is no longer selected.
- Draft edits remain session-only; section persistence includes the originating student/lab/assignment context alongside the file and section. See FEEDBACK_WORKSPACE.md for remaining limitations.

### Maintenance
Update affected diagrams and component descriptions in the same change as dependency, workflow, persistence, or memory-ownership changes. Document implemented behavior separately from planned work. The date above records the latest source-checked update, not an automated synchronization guarantee.

### Provider failure handling (source reviewed September 16, 2026)

Ollama reads HTTP error bodies before throwing. A server error explicitly mentioning CUDA/GPU and out-of-memory triggers one retry of the same prompt with `num_gpu: 0`, within the current queue job. Structured-response schema and temperature are preserved; cancellation prevents further attempts. Other HTTP failures are not retried. `LlmHttpErrors` extracts Ollama/Azure error messages and caps displayed details at 4,000 characters while retaining HTTP status. A failed CPU retry advises checking the runner log. CPU execution can be slower and requires host RAM; no persistent settings are changed. A short live CPU probe succeeded with the configured `gemma4:e2b`; full assignment recovery has not been live-tested.

### Pinned workspace panels (September 17, 2026)

`MainWindow.Panels.cs` applies per-user Comments and Job queue visibility from `WorkspacePanelPreferences`, stored in `%APPDATA%/GuidedGrade/workspace-panels.json`. Both default to visible; missing or unreadable settings fall back to defaults. Settings saves visibility independently of folder violation configuration. The right rail stays visible when content collapses; Comments, Job queue, and Rubric select dedicated tab content. Workspace navigation no longer replaces `sidePanelContent.Child`. Hidden queue visibility also hides its status-bar shortcut, without cancelling jobs. Hidden comments still collect draft updates. Inline comments are clipped to the editor viewport.

`AppDataPaths` owns the renamed roaming and local product directories. On access it checks `LabFeedbackWPF` first, then `Lab Feedback WPF` (the former COM-cleanup diagnostics root). Each legacy directory is moved to `GuidedGrade` when the destination is absent; otherwise missing files are moved recursively while conflicts remain in their original location and are reported through `Trace`. Existing GuidedGrade files take precedence, followed by files from `LabFeedbackWPF`; repeated access preserves retained conflicts. `LLMSettings` rewrites saved guest credential and worker paths rooted under the previous `LabFeedbackWPF` local directory when their migrated destinations exist. SQLite databases, LLM settings, workspace panel preferences, test reports, runner workspaces and cleanup diagnostics all resolve through this shared owner.

### File actions and execution preference (source reviewed September 20, 2026)

The file-tree context menu always exposes Open in File Explorer and Refresh. Explorer selects files and opens directories; solution-only Programming tools contains Build, Build and Run, Run, and AI testing. Settings saves Local/VM and disables repeated local prompts in `LLMSettings`; LLM Settings exposes the same environment and an optional per-operation prompt checkbox. Existing settings retain their prior prompting behavior until saved. The explicit Test with AI locally command still requests one-time consent.

Run resolves existing output without invoking a compiler. The guest worker supports a `resolve` command in addition to `build`; both prepare the subsequent `start` command. VM runs use output already present in the uploaded submission. Disposable VM builds are not copied back to the host or retained for subsequent VM actions. Manual VM runs retain the existing bounded output-capture behavior and do not provide an interactive desktop. Local manual builds write to the selected submission, with dotnet solution fallback output under `bin/GuidedGrade`; AI tests still stage copies.

### Explicit feedback scope (source reviewed September 20, 2026)

File-tree overall and section actions have separate handlers; assignment selection no longer silently switches overall analysis into section grading. Overall results use the captured context for both persistent overall file cards and PresentGeneratedFeedback; they do not auto-apply grades. The explicit section path requires a rubric and reports files with no detected sections instead of grading whole files. Runtime grading retains its existing fallback; unused current-file helpers were removed. Function detection is still a line-oriented heuristic requiring the opening brace on the signature line. See FEEDBACK_WORKSPACE.md for the complete scope and persistence behavior.

### Native build failure handling (source reviewed September 20, 2026)

`SubmissionBuilder.BuildMsBuildArguments` preserves Windows backslashes in native output/intermediate paths and escapes the trailing separator for Windows argument parsing, avoiding the forward-slash destination ambiguity shown by xcopy post-build steps. Native solution failures return the MSBuild result instead of falling through to dotnet clean/build. Timed-out MSBuild attempts likewise do not retry. Builds remain bounded to 90 seconds, never treat a failed build's leftover executable as successful, and prepend timeout/copy-prompt explanations to retained tool output. Manual results flow through MainWindow.ShowBuildRunResult to the existing bounded Console presenter rather than an unbounded-size MessageBox. A real MSBuild/xcopy regression fixture verifies paths containing spaces; arbitrary student build scripts can still fail or require input and are not silently rewritten.

### Clear review ownership (source reviewed September 20, 2026)

The file-tree Clear review handler calls `CommentPersistenceService.DeleteComments` (case-insensitive file match), advances `ReviewGeneration`, removes `_fileComments[filePath]`, and clears the selected editor's overlay. Storage failure leaves cache/overlay intact. `ReviewGeneration` is a UI-thread-owned per-file counter map; active section grading and regeneration compare captured versions before publishing, saving, or rendering results. Whole-submission draft feedback remains separate and is not deleted. Import markers remain because the combined draft is preserved. Future or not-yet-started queued file reviews can create new records.

### UI framework migration (source reviewed September 20, 2026)

`MainWindow.Framework.cs` owns the native File/Settings menu through WpfUI.Native, declarative workflow toolbar/pickers, keyed file tabs, status and navigation hosts. Its focused native layout adapter supplies constrained vertical fill, splitter sizing and horizontal tab scrolling, absent from the pinned framework. `MainWindow.NativeControls.cs` owns stable source/terminal documents, tree/list selection, context menus and the independent panel TabControl. Settings, provider/assignment dialogs, review cards and panels use `View`, `State`, `StateList` and `ViewHost`. MainWindow disposes shell, panel, root and grading hosts on close; replaced feedback/rubric hosts and removed cards are also disposed. Draft bindings target the captured student/assignment. A framework toggle exposes the retained declarative GradingView through native interop.

The WPF-UI dependency and its resource dictionaries/chrome were removed. Five obsolete form/extraction XAML files and unused private grading helpers were removed; declarative form classes now use ordinary .cs filenames. `NuGet.Config` restores pinned local prerelease packages from `vendor/ui-framework`; source provenance/license accompanies the feed. Native editor/tree/menus/splitters/console and programming-results controls retain existing platform behavior inside the root adapter. Extraction now renders framework cards with keyed native progress bars. Closing cancels active operations; `ZipFileHandler` disposes each linked source in its worker finally, and cached token values remain safe after disposal. Late updates cannot revive cancelled or closed UI. See UI_FRAMEWORK_MIGRATION.md for exact coverage, limitations, and framework-agent reports. App rendering/behavior tests do not establish performance improvement or live keyboard/IME coverage.

### Extraction ownership after migration (September 20, 2026)

```mermaid
flowchart LR
    Worker[ZipFileHandler] -->|registers| Dialog[ExtractionProgressDialog / ViewHost]
    Dialog -->|creates linked source| Operation[ExtractionOperation / cached token]
    Dialog -->|keyed native island| Bar[WPF ProgressBar]
    Close[Close / Cancel all] -->|cancels active operations| Operation
    Worker -->|finally disposes source| Operation
    Close -->|disposes host and releases bars| Dialog
```

Current local.3 framework packages fix rich Button content upstream and pass functional validation.
Their themed update allocations fail the unchanged gate (+7.81% versus 6%; update time +9.87%).
They remain local integration dependencies. Current evidence lives in vendor/ui-framework;
local.2 history, including its initial failure and successful repeat, lives under history/local.2.

The current tree navigation has no active caller of `ExtractZipFilesInFolderWithProgressAsync`; its old selection-handler callers were commented out before this migration. Extraction service/dialog behavior is tested and available, but automatic ZIP extraction on tree selection is not implemented. Obsolete commented handlers and their unused dialog helper were removed.

### Sample submission fixture (source reviewed September 20, 2026)

`Demo/Submissions` contains two fictitious student folders with .NET 10 console solutions.
`FrameworkMigrationTests.SampleDirectoryRendersRealFilesAndMenus` parses those folders with
`Student.GetStudentsFromFolders`, selects native tree containers, verifies editor text against
the file on disk, and renders the actual workspace/context menus. Seeded comments use a temporary
SQLite database and explicitly say DEMO; no AI request or student execution is performed.
These are offscreen app-control captures, not live desktop screenshots.

File tabs now use keyed framework filename and close buttons with accessible names. Both the
FileTabButtonStyle and CompactCloseButton workarounds are removed. The framework's compact
padding gap remains reported upstream; normal-size close targets avoid it. Tests assert visible
labels/glyphs, independent close actions, and dense feedback/expanded reviews.

### Workspace visual refinement (source reviewed September 23, 2026)

`MainWindow.Framework.cs` restores File/Settings menus beside the saved course/assignment pickers, followed by the Assignment review heading and numbered Assignment, Open submissions, Review with rubric and Feedback workflow actions. The menu retains its native instance across reactive updates; its existing handlers open the same settings and assignment dialogs.
`ReviewTheme` applies code-built resources from `Presentation/NativeTheme.cs` to native windows, framework hosts, and the
detached file context menu, supplying dark menu, scrollbar, and tool-tab presentation while retaining
WPF controls and commands. File tree width starts at 260 pixels and is resizable. InlineCommentAdorner starts collapsed,
expands on its header action, and collapses after approval; all review actions remain inside expanded
details. Review cards cap at 480 pixels wide. The editable feedback draft remains separate.

```mermaid
flowchart LR
    Theme[ReviewTheme] -->|applies code-built styles| NativeTheme[NativeTheme.cs]
    NativeTheme --> Menus[Native menus / scrollbars / tool tabs]
    Header[Framework header] -->|WpfUI.Native| AppMenu[Stable File / Settings menu]
    Header --> Workflow[Numbered workflow buttons and saved pickers]
    Feedback[Section feedback] --> Chip[Collapsed review header]
    Chip -->|toggle header| Details[Scrollable review details / existing actions]
    Details -->|approve| Chip
```

App UI regression captures verify populated file navigation and native menus plus default collapsed
review state. They do not establish a live desktop keyboard/IME audit or app performance improvement.

### Overall feedback attaches to files (source reviewed September 20, 2026)

```mermaid
flowchart LR
    Start[Overall feedback menu] --> Capture[Checked paths / ReviewGeneration versions / draft key]
    Capture --> Provider[Queued provider review]
    Provider --> Complete[MainWindow.FileReviews CompleteOverallFileReview]
    Complete -->|skip files cleared since start| Guard[ReviewGeneration]
    Complete -->|preserve sections; replace prior overall| Store[CommentPersistenceService]
    Store --> DB[(SectionComments / IsOverallReview)]
    Complete -->|original selected file only| Inline[Collapsed overall review at line 1]
    Complete -->|once for surviving results| Draft[Original submission draft]
```

Overall completion previously only appended the session draft. It now persists a typed overall
review for each captured checked file, including files never opened, without overwriting section
reviews or source text. Multi-file reports are explicitly labeled combined feedback, not individual
scores. The current inline overlay updates only for its matching file; switching selection does not
retarget the result. Clear review invalidates captured generations for overall as well as section jobs.
SQLite adds IsOverallReview INTEGER NOT NULL DEFAULT 0; existing comments remain section reviews.
Overall cards omit the unused numeric score; section-only regeneration is disabled for these cards
(use the Overall feedback menu to rerun). Approval/rejection and Clear review retain their workflows.

### Remaining XAML conversion handoff (source reviewed September 20, 2026)

The XAML-removal handoff is complete: App.cs, MainWindow.cs/Framework.cs/NativeControls.cs, Views/GradingView.cs and Presentation/NativeTheme.cs replace the former application XAML. Specialized native controls and sizing adapters remain deliberately; there are no generated application fields, InitializeComponent calls or XAML pack loads. Editor .xshd highlighting resources remain active. ShellMigration build and 187 tests pass; actual-control captures use demo data.

### Grading view duplicate cleanup (source reviewed September 20, 2026)

The declarative framework Views/GradingView.cs is now the sole GradingView type. Obsolete Views/GradeView.xaml and Views/GradeView.xaml.cs were removed; duplicate generated/manual members no longer compile together. Its reactive points binding uses a fallback while switching result models so teardown cannot read a cleared dictionary. GradingViewTests cover score/deduction binding, native editor identity, switching models and host disposal.

## File-tree recycling (September 24, 2026)

`MainWindow.NativeControls.ConfigureFileTree` enables recycling virtualization with virtualizing panels at the root and nested item levels. FileSystemItem owns expansion, selection and analysis-checkbox state through two-way bindings, so recycled containers restore state for the correct file. `MainWindow.FileTreeView_SelectedItemChanged` skips reopening the already-active file, preserving its AvalonEdit document, caret and review state when a selected container is recreated.

`FileTreeVirtualizationTests` and `FileTreeChecks` cover bounded realization with 1,000 files, nested expansion/collapse, offscreen selection, checked-state isolation, automation patterns, actual source-file opening, document/caret retention and host disposal. The full consumer suite passed 189 tests. Performance evidence is described in FILE_TREE_PERFORMANCE.md; the app pins `0.1.0-alpha.3-local.2`.
## Application-owned COM cleanup

App creates the package-provided `WpfComCleanupPolicy` before `MainWindow`.
MainWindow's existing Closed handlers dispose native/framework owners before
App.OnExit drains cleanup. The thread-wide CLR setting lasts until process exit;
the policy is not installed by ViewHost. Failures are logged and counted across
recovery. Real typing, IME, and screen-reader validation remain pending; see
[the acceptance checklist](COM_CLEANUP_POLICY.md). The exact framework package
pin is `[0.1.0-alpha.3-local.2]`.

### Runner setup credential location (source reviewed September 29, 2026)

`Setup-RunnerTemplate.ps1` defaults guest credentials to the executing user's local app-data directory under `GuidedGrade\runner-guest.xml`, accepts an explicit `CredentialPath`, and reports the full path in `setup-status.json`. `New-RunnerAnswerMedia.ps1` exports the credentials for the executing Windows account; setup and runtime should use that same account.

## Queued review isolation and payload boundary (October 5, 2026)

ReviewContext identifies the student folder, course/title and submitted lab folder (the first directory below the student folder, with nested source directories grouped into that lab). MainWindow captures this key before asynchronous work. Section batches also snapshot the rubric, settings, file list, identifier-redaction inputs and search root before yielding. Related-file lookup excludes candidates outside that root. Mixed-lab checked-file reviews are rejected with guidance to select one lab; solution tests restrict checked paths to the selected solution's lab. Student changes detach old editor tabs; file/assignment changes refresh inline cards and drafts.

SQLite migration adds ReviewContext with an empty legacy default and extends the unique index. Existing records are preserved without guessing their assignment: legacy records display only with no assignment selected. New overall replacement, section replacement, rendering and approved-feedback import respect context. Clear review remains explicitly file-wide across contexts. The cache retains all contexts for a file; drafts and import markers still have no eviction or durable edited-draft storage.

Grading payloads contain requirements/rubric criteria and sanitized source text, generic numbered file labels and fixed grading instructions. Student names/IDs, original filenames, course/title, local routing keys, extracted type indexes and runtime reports are not added as metadata. Known identifiers and email/personal-path patterns are redacted in criteria and source; this is not a proof that arbitrary personal information embedded in code can always be recognized. HTTP contract tests inspect both Ollama and Azure request bodies. Interactive testing is an explicit exception: the console driver sends redacted output/history and testing instructions to request one next input, validates it, and writes it to the program input stream. Source remains available for local input validation but is omitted from console-choice requests. Free-form tasks send the text the user deliberately enters, without automatically attaching student files or metadata. Settings connection tests use synthetic criteria/source.

### Request preferences (October 5, 2026)

LLMSettings persists ConsoleModelWaitSeconds (default 30), ConfirmLocalExecution (default true) and ConfirmGrading (default true). AI Provider settings validates the model wait as an integer from 1 through 90 seconds. Invalid saved values use 30 seconds at runtime. ConsoleDriverAgent passes the captured timeout to its per-decision cancellation source; queue waiting and a model correction each remain within the overall 90-second session deadline. This setting does not alter grading HTTP timeouts or memory bounds.

Both settings windows expose separate local-execution and grading confirmation toggles. Saving Programming Checks no longer unconditionally disables local confirmation, and Test with AI locally no longer unconditionally enables it. Manual build/run and queued tests consult the saved local preference. GradingConfirmation gates overall reviews, section batches, selected-section grading, regeneration and post-test grading once per user request; an enabled prompt can be declined before queuing work. Setup, validation, error and completion messages remain independent.

Student-grade persistence (October 6): MainWindow.Grades owns the loaded grade dictionary, saves via GradePersistenceService, and refreshes reactive row/footer views. StudentGradeRow adapts the existing native ListBox selection and recycling to framework-rendered rows, disposing hosts on unload/replacement. Final grades use ReviewContext student/course/assignment/lab keys; they are entered by the instructor and remain separate from AI feedback and programming log scores. GradeTotals sums all recorded earned/possible points for the selected student's current course; ungraded submissions and other courses are excluded. The assignment badge is for the active lab, identified in the submissions header. Editing requires a selected assignment and a file inside the selected student's folder. Grade keys and scores stay local and are not model input. Rename/move migration, category weights, and automatic final-grade extraction from feedback are not implemented.

October 6 approval-transfer source review: CommentLayer_ApproveRequested now persists approval and calls RestoreApprovedFeedback immediately. RestoreApprovedFeedback updates both the scoped draft dictionary and its existing editor state, including when another file shares the lab context. Pending/rejected records are not restored into drafts; existing generated-draft behavior is unchanged. ReviewContext.Matches compares Windows folder/root components without case sensitivity while retaining exact course/assignment separation. SQLite comment reads and replacement deletes also ignore file-path case. PersistCommentsForFile leaves unloaded records untouched; explicit Clear review still deletes them.

October 6 batch source review: MainWindow.BatchReview owns the declarative designer/preview and disposes its host on close. BatchReviewPlan validates relative file lists and same-lab test entry points, snapshots targets, and iterates students serially within one AiTestQueue job. MainWindow captures rubric/settings, identifiers and ReviewContext routing independently of current selection; execution uses saved mode/confirmation policy and writes local reports. GradeFileWithRuntimeReportAsync captures clear-review versions, saves each successful section immediately, optionally approves it and imports the captured student's draft, and refreshes active inline cards. Failure continues to the next student; whole-batch cancellation stops further work and preserves saved comments. Model payload and console privacy rules remain unchanged. Batch configuration/progress are session-only; named recipes and durable resume are not implemented.

October 6 nested batch-path source review: BatchReviewPlan.ValidatePattern allows a leading **/ with a literal relative suffix. Resolve searches only under the current student's folder, skips reparse points, scans all matching paths and selects the newest LastWriteTimeUtc, breaking ties by alphabetical path order, and reports missing/search failures per student. Preview lists resolved paths; queued execution receives exact paths, not patterns. Existing physical ReviewContext routing and same-lab validation remain in place. General glob syntax is not implemented.

October 6 batch replacement source review (supersedes the earlier section-based batch description): ProcessStudentAsync clears the specified files, optionally executes the project once, then reviews files serially. MainWindow.BatchReview uses OverallFeedbackPrompt with one sanitized file per request and CompleteOverallFileReview with captured approval/draft options. ClearBatchFileReview deletes all contexts for only those files, advances generation counters, removes intact generated draft blocks through ReviewDraftCleanup, and resets file import markers; instructor-edited draft text remains. Build/run defaults off and disabled entry paths are ignored during preview. Generation failures after clearing do not restore old reviews. Batch no longer calls GradeFileWithRuntimeReportAsync; individual solution-test grading retains that section-based path.

October 6 batch-display source review: CommentPersistenceService.GetReviewedFiles reads non-rejected file/context locations in saved-time order. MainWindow.Workspace filters them to the selected student's folder and exact assignment/lab context, skips missing files, and opens the most recent matching file on student selection or a Comments request without an active file. Saved-file links refresh after overall results; Job queue exposes the same navigation. Batch completion refreshes the selected review without changing assignments. InlineCommentLayer makes restored cards visible before measuring; overall-file cards use the first visible source line when their line-one anchor is off-screen. Section cards remain line-anchored. Read-only inspection confirmed recent real batch records were present and approved; no user review database writes were performed during diagnosis.

October 6 latest-submission source review: BatchReviewPlan.Resolve now chooses the greatest file modification timestamp (UTC) across all recursive matches; equal timestamps use ordinal case-insensitive path order. Preview displays each chosen path and its UTC modification time. Recursive entry-point resolution stays within the selected review lab. Exact paths are unchanged; multiple review patterns still must resolve within one lab. The captured preview paths are not reselected when execution starts.

October 7 class-folder source review: AssignmentSetupWindow saves the class-wide folder-name toggle through AssignmentPersistenceService into CourseFolderSettings in assignments.db (case-insensitive course key). Missing settings, including PG1 and DSA, default to strict student naming. MainWindow reloads an open folder list on class selection and saved setup, retaining selection by path where possible. No change to review/grade keys or lab-root semantics.

```mermaid
flowchart LR
    Setup[Assignment setup class toggle] --> Store[CourseFolderSettings in assignments.db]
    Store --> Reload[MainWindow.ReloadSubmissionFolders]
    Reload --> Discover[Student.GetStudentsFromFolders]
    Discover --> Strict[Default Last_First-ID parsing]
    Discover --> Names[Optional immediate folder names]
```

October 7 native PDB collision review: SubmissionBuilder no longer passes a solution-wide IntDir override. Native solution builds retain each project's intermediate paths and use /m:1; direct C++ project builds also use /m:1. Existing OutDir handling remains for unattended post-build copies. C1041 diagnostics explain file locking, separate intermediates, /FS and retrying outside synchronized folders. External locks and project-defined conflicting directories can still fail; no student project files are rewritten.

```mermaid
flowchart LR
    NativeBuild[SubmissionBuilder native MSBuild] --> Serial[One MSBuild worker /m:1]
    Serial --> Intermediate[Project-defined intermediate directories]
    NativeBuild --> Failure[C1041 actionable diagnostic]
```

October 7 native launch-directory fix: ResolveWorkingDirectory first maps the launched executable name to a matching .sln project display name and its existing .vcxproj path before the filename heuristic. This handles Lab1.exe from CaveMatchingGame.vcxproj even when the solution output directory points under Practice. Direct project launches still use their project folder. Manual Run reports the full executable and working-directory paths. This fixes relative source/asset lookup for mapped projects; it does not establish that student initialization or graphics loops are correct.

```mermaid
flowchart LR
    Exe[Executable basename] --> Alias[Matching solution project display name]
    Alias --> Project[Project path from solution]
    Project --> Working[Native process working directory]
```

October 7 build diagnostic review: SubmissionBuilder.Combine adds actionable prior-process/file-lock guidance for executable LNK1104 errors; it preserves failure status and never terminates external programs.
