using System.Text;
using System.Text.Json;
using Lab_Feedback_WPF.Services;

// This executable runs inside the guest. It never receives LLM settings or keys.
Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);
if (args.Length != 1 || !Directory.Exists(args[0]))
    return 2;
var root = Path.GetFullPath(args[0]);
using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(10));
InteractiveProcessSession? session = null;
SubmissionBuilder.BuildResult? build = null;
try
{
    while (await Console.In.ReadLineAsync(lifetime.Token) is { } line)
    {
        try
        {
            if (line.Length > 65536) throw new InvalidDataException("Request exceeds the worker limit.");
            using var document = JsonDocument.Parse(line);
            var request = document.RootElement;
            object result;
            switch (request.GetProperty("command").GetString())
            {
                case "build":
                    if (build != null) throw new InvalidOperationException("Use a fresh worker for each build.");
                    var entry = Path.GetFullPath(Path.Combine(root, request.GetProperty("entry").GetString()!));
                    if (!entry.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Entry must be inside the submission.");
                    var submission = RunnableSubmissionDetector.Detect(entry)
                        ?? throw new InvalidDataException("No runnable solution was found.");
                    build = await new SubmissionBuilder().BuildAsync(submission, lifetime.Token);
                    result = build;
                    break;
                case "start":
                    if (session != null || build is not { Succeeded: true })
                        throw new InvalidOperationException("A successful build is required before starting.");
                    NativeDependencyStager.CopyDependencies(build.CommandFileName, root);
                    // Child ConPTY I/O uses dedicated pipes. This process keeps JSON
                    // on its own redirected stdin/stdout for the worker protocol.
                    session = InteractiveProcessSession.Start(build.CommandFileName, build.Arguments,
                        build.WorkingDirectory, NativeDependencyStager.GetDllDirectories(root, build.CommandFileName),
                        useConPty: InteractiveProcessSession.IsConsoleSubsystemExecutable(build.CommandFileName));
                    result = Status(session);
                    break;
                case "poll":
                    if (session == null) throw new InvalidOperationException("No process is running.");
                    var slice = await session.WaitForIdleAsync(
                        TimeSpan.FromMilliseconds(Math.Clamp(request.GetProperty("idleMs").GetInt32(), 1, 2000)),
                        TimeSpan.FromMilliseconds(Math.Clamp(request.GetProperty("windowMs").GetInt32(), 1, 15000)), lifetime.Token);
                    result = new { slice = new ConsoleSlice
                    {
                        StandardOutput = Cap(slice.StandardOutput), StandardError = Cap(slice.StandardError),
                        Exited = slice.Exited, OutputStillFlowing = slice.OutputStillFlowing
                    }, status = Status(session) };
                    break;
                case "input":
                    if (session == null) throw new InvalidOperationException("No process is running.");
                    await session.WriteInputAsync(request.GetProperty("text").GetString() ?? "", lifetime.Token);
                    result = Status(session);
                    break;
                case "close":
                    session?.CloseInput();
                    result = Status(session);
                    break;
                case "kill":
                    session?.Kill();
                    result = Status(session);
                    break;
                default:
                    throw new InvalidDataException("Unknown worker command.");
            }
            Console.WriteLine(JsonSerializer.Serialize(new { ok = true, result }));
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = ex.Message }));
        }
    }
}
finally
{
    if (session != null) await session.DisposeAsync();
}
return 0;

static string Cap(string value) => value.Length <= OutputLimits.ConsoleCharacters
    ? value : OutputLimits.TruncationMarker + value[^OutputLimits.ConsoleCharacters..];
static object Status(InteractiveProcessSession? session) => new
{
    started = session?.Started ?? false, error = session?.Error,
    hasExited = session?.HasExited ?? true, exitCode = session?.ExitCode,
    standardOutput = Cap(session?.StandardOutput ?? ""), standardError = Cap(session?.StandardError ?? "")
};
