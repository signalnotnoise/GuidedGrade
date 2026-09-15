# Output memory limits

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
- `Lab Feedback Runner.csproj` compiles the same buffer and limit sources into the guest worker. The JSON protocol preserves truncation notices. **Republish the runner and point execution settings at the updated publish folder** to apply the changes inside Hyper-V; an older published worker does not gain the changes from rebuilding the desktop app alone.

## Verification

The regression tests exercise a generated 20-million-character stream with an allocation ceiling, simultaneous flooded build pipes, redirected/ConPTY floods followed by interactive input, continuous activity after buffer saturation, ring-buffer cursor rollover, mailbox chunk limits, and real WPF document/undo behavior on an STA thread. Runner-protocol tests build and interact with a temporary console application in normal and flooded-output modes without LLM credentials.

```powershell
dotnet test "Lab Feedback WPF.Tests/Lab Feedback WPF.Tests.csproj" --filter "FullyQualifiedName~OutputMemoryTests|FullyQualifiedName~RuntimeTerminalPresenterTests|FullyQualifiedName~InteractiveProcessSessionTests|FullyQualifiedName~ProcessRunnerTests|FullyQualifiedName~ConsoleDriverAgentTests|FullyQualifiedName~AiTestQueueTests|FullyQualifiedName~RunnerWorkerTests"
```

The worker-protocol tests run the worker locally; they do not validate a real Hyper-V guest deployment or live model calls.

The agent also maintains a fixed 120-by-30-character `ConsoleScreen` projection for each output stream. See [console interaction](CONSOLE_INTERACTION.md) for prompt selection, terminal-control handling, input validation and cancellation behavior.

## Remaining memory work

The feedback cache has no eviction, directory-tree loading is eager, and source/log reads still need size budgets or streaming. Local models and student programs can consume RAM independently of captured output. Those remain separate improvements; see the [knowledge graph](KNOWLEDGE_GRAPH.md) for their relationships to the rest of the app.
