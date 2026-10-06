# AI job and solution-test queues

Source reviewed October 6, 2026.

## Batch student review

Paths may start with `**/` (or `**\`) to allow zero or more extra folders inside each student's submission. For example, `**\Lab_2_Conversions\Lab 2\StudentWork.h` matches both a direct lab and one inside an extra extraction folder. The remaining path is literal, matched case-insensitively; other wildcards and parent-folder traversal are rejected. This also works for the optional test entry point. When a pattern matches multiple files, the file with the newest LastWriteTimeUtc (last-modified time) is selected; equal timestamps use alphabetical path order. Preview shows the selected relative paths and UTC modification times. Missing matches skip the student. A recursive test-entry pattern searches within the selected review lab so an older submission is not executed. Recursive search skips reparse points (linked files/folders) and reports access failures instead of selecting from a partially searched tree.

Use **Batch review** in the main toolbar (or **Design batch review** in Job queue). Choose an assignment with a rubric and open the student submissions folder first. Enter one or more paths relative to each student's folder, one per line; **Use checked files from selected student** fills these paths from the tree. An optional relative test entry point selects a solution, project, or runnable file; otherwise detection starts from the first review file. All specified files and the entry point must be in the same lab.

Preview lists every loaded student as ready or missing files. Editing paths, approval mode or build/run mode requires a new preview. Queue batch captures the students, assignment/rubric, paths, settings, review-generation versions and options. It occupies one solution-queue slot and processes students serially. For each ready student it clears all persisted/cache/inline reviews on the specified files (including older assignment contexts), invalidates older in-flight responses, optionally builds/runs the project once, then requests and saves one overall review per file in list order. Build/run defaults off; when off the entry-point field is disabled and ignored, and no execution service is called. Automatic approval defaults on. Grades are not automatically changed.

Replacement removes intact generated blocks from the matching in-memory feedback drafts and resets import markers for the cleared files. Instructor-edited draft text is preserved. Students with missing files are skipped before clearing; cancellation leaves unstarted students untouched. Clearing happens before generation: a failed or cancelled replacement does not restore the old reviews. Completed file reviews remain saved. The model receives only the current file's sanitized content plus criteria, never another selected file or an execution report.

Missing files skip that student; per-student exceptions are reported and the next student continues. Execution reports (including compile/run failures) remain local under TestReports; source review can still proceed after an unsuccessful build. Declining local execution skips grading for that student. Saved execution mode and confirmation settings apply; turn off local confirmations in settings if you want an unattended local run. Grading confirmation occurs once for the batch when enabled. Console/model limits are unchanged. The model receives the existing sanitized grading payload, not student identity, routing keys, or test reports.

Job queue's Batch progress box lists running/completed/failed/skipped students. Cancel the **Batch student review** job to stop remaining work; already saved overall comments remain. Each successfully returned file review is persisted immediately. Queue history, batch progress and remembered design fields are session-only; there is no durable batch resume or named recipe library. Student tests share the existing serial execution queue; model requests still use the separate shared model scheduler.

## Job queue panel

After a batch, use **Open selected student's saved review** to open the saved file and Comments panel for the selected student and assignment. Selecting a student also restores their most recent reviewed file for the current assignment. Saved-review links in Comments allow switching among that student's reviewed files. Reviews are not lost merely because another file/assignment is active; only matching review contexts are displayed.

Click the status-bar **Queue** indicator to open the **Job queue** tab in the right-side panel. It shows separate counts for solution tests and LLM requests (a running solution may own an LLM request, so these are distinct levels of work). Each row shows its priority and Waiting, Running, Cancelling, Finished, Failed or Cancelled status. Select a row to read its result; use **Cancel job** for individual cancellation and **Clear finished** to remove completed history. The comment sections are unchanged.

Grading requests contain criteria and anonymized file contents. Interactive testing separately sends redacted console output and testing instructions to ask for the next input. Free-form tasks send only the prompt the user deliberately enters via Queue task; no student context is automatically attached.

All Ollama and Azure completion calls use the process-wide `LlmJobQueue.Shared`. Assignment analysis, section grading and console-input requests have assignment priority. User-authored free-form tasks have general priority. Between requests, assignments are selected first, with FIFO ordering within each priority. An active request is never preempted by a newly queued assignment. A continuous assignment workload can delay general tasks. Scheduling is per model request, not an entire multi-section grading batch; cancelling a request cancels that request, while the solution-test row cancels the whole test and its subsequent grading.

When analyzing checked code files with an assignment rubric, the UI detects function/method sections and submits one grading request per detected section. Control statements such as `if`, `switch`, `for`, `while` and `catch` are excluded from section detection, so they do not create spurious grading requests. Files with no detected functions are graded as one whole-file section.

Waiting cancellation removes work immediately and frees capacity. Running cancellation signals the provider/execution token and waits for the delegate to return (including cleanup) before the next job starts. It does not guarantee the remote server stops generation instantly. Cancelling a console-input request stops that interaction; use the solution row to cancel the full test.

Each scheduler allows at most 50 active and waiting jobs, and retains at most 50 finished snapshots. Displayed results are capped at 32,000 characters plus a truncation notice; completed snapshots release request delegates and completion tasks. Pending model requests retain their prompts, so this is a count limit rather than a total memory limit. The UI refreshes snapshots every 300 ms and stops its timer on window close. Queues and history are in memory only.


## Using the queue

Choose **Test with AI** for each solution you want to test. Requests run in arrival order, one at a time. The window title shows the total number running or waiting. Selecting another student does not change previously queued requests.

The same full solution path cannot be added again while it is running or waiting, including through the other execution mode. You can retry after it finishes. The queue accepts at most 50 active and waiting jobs combined.

Each request captures its solution path, student identifiers for local redaction, lab search folder, checked file paths within that lab, student/lab/assignment draft key, assignment requirements, rubric and LLM/execution settings. Waiting requests do not preload source content or start a VM. Files are read when the request runs: keep the submission files in place and avoid editing them while queued.

The terminal shows the current test. Reports are saved as uniquely named text files under `%LOCALAPPDATA%\GuidedGrade\TestReports`; the terminal prints the saved path. The first local-data access after the product rename moves the previous product directory to `%LOCALAPPDATA%\GuidedGrade` when the new directory does not already exist. Completion does not open a modal report dialog. These reports contain submission paths and may contain source or console output; manage them like other local grading records. Old reports are not automatically deleted.

After execution, checked files are graded with the captured assignment and settings; runtime reports remain local and are excluded from model prompts. Execution disposal and this grading finish before the next request starts. A failed request is reported in the terminal/debug output and does not stop the queue. Local execution still requires the existing confirmation when its turn starts; declining skips that request.

The queue is in memory and applies to this window's **Test with AI** commands. It is not shared between app instances and does not serialize separate **Build** or **Run** commands. Closing the app discards waiting requests. The right-side queue tab provides individual cancellation; there is no reordering or restart recovery UI.

## Implementation and memory behavior

`AiTestQueue` is owned and called by the WPF UI thread. It wraps a separate `LlmJobQueue` instance, putting all solution jobs at assignment priority, and retains case-insensitive active/pending keys. Each work item is awaited, including asynchronous disposal inside the execution service, before advancing. Exceptions complete the individual task as failed while draining continues. Do not call the solution wrapper concurrently from background threads. The shared provider scheduler is thread-safe; the separate solution scheduler preserves UI context and does not hold the shared model queue while awaiting model requests.

The Hyper-V execution service normally disposes the bridge/VM before returning. Its existing emergency cleanup can fall back to an independent watchdog after a bridge failure; queue serialization does not prove that emergency VM cleanup has finished. Inspect runner diagnostics if a guest remains after a failure.

Serial execution avoids overlapping AI-test VMs (the default VM allocation is 4096 MB per run). It does not impose a total RAM limit. Console capture, terminal display and build-output capture now enforce [output memory limits](MEMORY_LIMITS.md). Feedback-cache eviction and source/log size budgets remain separate improvements. See the [dependency knowledge graph](KNOWLEDGE_GRAPH.md) for the connected components.

## Verification

Run `dotnet test "GuidedGrade.Tests/GuidedGrade.Tests.csproj" --filter FullyQualifiedName~AiTestQueueTests|FullyQualifiedName~LlmJobQueueTests`.

Tests cover FIFO execution, waiting for asynchronous cleanup, case-insensitive duplicate rejection, capacity, recovery after failure and retrying a completed key. For UI verification, queue two different solutions, navigate to another student, and verify the original submissions and captured rubric are used. Double-click one solution and check that it runs once. Confirm reports appear in the report directory and the next request proceeds without dismissing a completion dialog.

### Ollama GPU-memory recovery (September 16, 2026)

An explicit CUDA/GPU out-of-memory HTTP server error triggers one CPU retry within the same queued request. Cancellation still applies, and no subsequent job starts during this retry. The same prompt and response schema are preserved. CPU fallback can be slower and consume host RAM; it does not change saved provider settings or impose a RAM budget. Failed jobs retain the server explanation (up to 4,000 characters); unrelated HTTP errors are not retried.

## Pinned tabs (September 17, 2026)

Use the right-side **Comments**, **Job queue**, and **Rubric** tabs to switch panels. Collapsing the panel leaves the tabs available. Each panel owns separate content, so opening feedback or the rubric cannot remove the queue. **Settings → Pinned workspace tabs** saves Comments and Job queue visibility for the current user across restarts. Both are shown by default. Hiding a tab preserves drafts and running jobs; hiding Job queue also hides its status-bar shortcut. Inline feedback stays within the editor viewport.

The queue panel now uses a custom-framework ViewHost with keyed virtualized rows, explicit Details/Cancel buttons and a read-only result editor. Its 300ms UI snapshot timer and scheduler ordering/cancellation limits are unchanged. Closing the main window stops the timer and disposes the host; collapsing a panel keeps jobs and their state.

Console decisions capture the saved model-wait timeout (1–90 seconds; default 30), capped by the overall 90-second test deadline. Ask before grading is evaluated once before queuing a test that will grade checked files. Local build/run confirmation uses the captured saved preference, including the explicit local-test command.
