# AI test queue

## Using the queue

Choose **Test with AI** for each solution you want to test. Requests run in arrival order, one at a time. The window title shows the total number running or waiting. Selecting another student does not change previously queued requests.

The same full solution path cannot be added again while it is running or waiting, including through the other execution mode. You can retry after it finishes. The queue accepts at most 50 active and waiting jobs combined.

Each request captures its solution path, student identifiers, search folder, checked file paths, assignment requirements, rubric and LLM/execution settings. Waiting requests do not preload source content or start a VM. Files are read when the request runs: keep the submission files in place and avoid editing them while queued.

The terminal shows the current test. Reports are saved as uniquely named text files under `%LOCALAPPDATA%\LabFeedbackWPF\TestReports`; the terminal prints the saved path. Completion does not open a modal report dialog. These reports contain submission paths and may contain source or console output; manage them like other local grading records. Old reports are not automatically deleted.

After execution, checked files are graded with the captured assignment and settings. Execution disposal and this grading finish before the next request starts. A failed request is reported in the terminal/debug output and does not stop the queue. Local execution still requires the existing confirmation when its turn starts; declining skips that request.

The queue is in memory and applies to this window's **Test with AI** commands. It is not shared between app instances and does not serialize separate **Build** or **Run** commands. Closing the app discards waiting requests. There is currently no queue editing, cancellation or restart recovery UI.

## Implementation and memory behavior

`AiTestQueue` is owned and called by the WPF UI thread. It holds FIFO work items and case-insensitive active/pending keys. Each work item is awaited, including asynchronous disposal inside the execution service, before advancing. Exceptions complete the individual task as failed while draining continues. Do not call this class concurrently from background threads.

The Hyper-V execution service normally disposes the bridge/VM before returning. Its existing emergency cleanup can fall back to an independent watchdog after a bridge failure; queue serialization does not prove that emergency VM cleanup has finished. Inspect runner diagnostics if a guest remains after a failure.

Serial execution avoids overlapping AI-test VMs (the default VM allocation is 4096 MB per run). It does not impose a total RAM limit. Console capture, terminal display and build-output capture now enforce [output memory limits](MEMORY_LIMITS.md). Feedback-cache eviction and source/log size budgets remain separate improvements. See the [dependency knowledge graph](KNOWLEDGE_GRAPH.md) for the connected components.

## Verification

Run `dotnet test "Lab Feedback WPF.Tests/Lab Feedback WPF.Tests.csproj" --filter FullyQualifiedName~AiTestQueueTests`.

Tests cover FIFO execution, waiting for asynchronous cleanup, case-insensitive duplicate rejection, capacity, recovery after failure and retrying a completed key. For UI verification, queue two different solutions, navigate to another student, and verify the original submissions and captured rubric are used. Double-click one solution and check that it runs once. Confirm reports appear in the report directory and the next request proceeds without dismissing a completion dialog.
