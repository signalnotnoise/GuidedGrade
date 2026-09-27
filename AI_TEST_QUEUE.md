# AI job and solution-test queues

Source reviewed September 18, 2026.

## Job queue panel

Click the status-bar **Queue** indicator to open the **Job queue** tab in the right-side panel. It shows separate counts for solution tests and LLM requests (a running solution may own an LLM request, so these are distinct levels of work). Each row shows its priority and Waiting, Running, Cancelling, Finished, Failed or Cancelled status. Select a row to read its result; use **Cancel job** for individual cancellation and **Clear finished** to remove completed history. The comment sections are unchanged.

Enter a free-form prompt and choose **Queue task** to send it to the configured Ollama or Azure provider. Results stay in the queue panel, not the feedback draft. Free-form jobs return text; they do not execute commands or modify files. The prompt limit is 32,000 characters.

All Ollama and Azure completion calls use the process-wide `LlmJobQueue.Shared`. Assignment analysis, section grading and console-input requests have assignment priority. General completion calls and free-form tasks have general priority. Between requests, assignments are selected first, with FIFO ordering within each priority. An active request is never preempted by a newly queued assignment. A continuous assignment workload can delay general tasks. Scheduling is per model request, not an entire multi-section grading batch; cancelling a request cancels that request, while the solution-test row cancels the whole test and its subsequent grading.

When analyzing checked code files with an assignment rubric, the UI detects function/method sections and submits one grading request per detected section. Control statements such as `if`, `switch`, `for`, `while` and `catch` are excluded from section detection, so they do not create spurious grading requests. Files with no detected functions are graded as one whole-file section.

Waiting cancellation removes work immediately and frees capacity. Running cancellation signals the provider/execution token and waits for the delegate to return (including cleanup) before the next job starts. It does not guarantee the remote server stops generation instantly. Cancelling a console-input request stops that interaction; use the solution row to cancel the full test.

Each scheduler allows at most 50 active and waiting jobs, and retains at most 50 finished snapshots. Displayed results are capped at 32,000 characters plus a truncation notice; completed snapshots release request delegates and completion tasks. Pending model requests retain their prompts, so this is a count limit rather than a total memory limit. The UI refreshes snapshots every 300 ms and stops its timer on window close. Queues and history are in memory only.


## Using the queue

Choose **Test with AI** for each solution you want to test. Requests run in arrival order, one at a time. The window title shows the total number running or waiting. Selecting another student does not change previously queued requests.

The same full solution path cannot be added again while it is running or waiting, including through the other execution mode. You can retry after it finishes. The queue accepts at most 50 active and waiting jobs combined.

Each request captures its solution path, student identifiers, search folder, checked file paths, assignment requirements, rubric and LLM/execution settings. Waiting requests do not preload source content or start a VM. Files are read when the request runs: keep the submission files in place and avoid editing them while queued.

The terminal shows the current test. Reports are saved as uniquely named text files under `%LOCALAPPDATA%\LabFeedbackWPF\TestReports`; the terminal prints the saved path. Completion does not open a modal report dialog. These reports contain submission paths and may contain source or console output; manage them like other local grading records. Old reports are not automatically deleted.

After execution, checked files are graded with the captured assignment and settings. Execution disposal and this grading finish before the next request starts. A failed request is reported in the terminal/debug output and does not stop the queue. Local execution still requires the existing confirmation when its turn starts; declining skips that request.

The queue is in memory and applies to this window's **Test with AI** commands. It is not shared between app instances and does not serialize separate **Build** or **Run** commands. Closing the app discards waiting requests. The right-side queue tab provides individual cancellation; there is no reordering or restart recovery UI.

## Implementation and memory behavior

`AiTestQueue` is owned and called by the WPF UI thread. It wraps a separate `LlmJobQueue` instance, putting all solution jobs at assignment priority, and retains case-insensitive active/pending keys. Each work item is awaited, including asynchronous disposal inside the execution service, before advancing. Exceptions complete the individual task as failed while draining continues. Do not call the solution wrapper concurrently from background threads. The shared provider scheduler is thread-safe; the separate solution scheduler preserves UI context and does not hold the shared model queue while awaiting model requests.

The Hyper-V execution service normally disposes the bridge/VM before returning. Its existing emergency cleanup can fall back to an independent watchdog after a bridge failure; queue serialization does not prove that emergency VM cleanup has finished. Inspect runner diagnostics if a guest remains after a failure.

Serial execution avoids overlapping AI-test VMs (the default VM allocation is 4096 MB per run). It does not impose a total RAM limit. Console capture, terminal display and build-output capture now enforce [output memory limits](MEMORY_LIMITS.md). Feedback-cache eviction and source/log size budgets remain separate improvements. See the [dependency knowledge graph](KNOWLEDGE_GRAPH.md) for the connected components.

## Verification

Run `dotnet test "Lab Feedback WPF.Tests/Lab Feedback WPF.Tests.csproj" --filter FullyQualifiedName~AiTestQueueTests|FullyQualifiedName~LlmJobQueueTests`.

Tests cover FIFO execution, waiting for asynchronous cleanup, case-insensitive duplicate rejection, capacity, recovery after failure and retrying a completed key. For UI verification, queue two different solutions, navigate to another student, and verify the original submissions and captured rubric are used. Double-click one solution and check that it runs once. Confirm reports appear in the report directory and the next request proceeds without dismissing a completion dialog.

### Ollama GPU-memory recovery (September 16, 2026)

An explicit CUDA/GPU out-of-memory HTTP server error triggers one CPU retry within the same queued request. Cancellation still applies, and no subsequent job starts during this retry. The same prompt and response schema are preserved. CPU fallback can be slower and consume host RAM; it does not change saved provider settings or impose a RAM budget. Failed jobs retain the server explanation (up to 4,000 characters); unrelated HTTP errors are not retried.

## Pinned tabs (September 17, 2026)

Use the right-side **Comments**, **Job queue**, and **Rubric** tabs to switch panels. Collapsing the panel leaves the tabs available. Each panel owns separate content, so opening feedback or the rubric cannot remove the queue. **Settings → Pinned workspace tabs** saves Comments and Job queue visibility for the current user across restarts. Both are shown by default. Hiding a tab preserves drafts and running jobs; hiding Job queue also hides its status-bar shortcut. Inline feedback stays within the editor viewport.

The queue panel now uses a custom-framework ViewHost with keyed virtualized rows, explicit Details/Cancel buttons and a read-only result editor. Its 300ms UI snapshot timer and scheduler ordering/cancellation limits are unchanged. Closing the main window stops the timer and disposes the host; collapsing a panel keeps jobs and their state.
