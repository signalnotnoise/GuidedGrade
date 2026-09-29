using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace GuidedGrade.Services
{
    public sealed class ConsoleSlice
    {
        public string StandardOutput { get; init; } = string.Empty;
        public string StandardError { get; init; } = string.Empty;
        public bool Exited { get; init; }
        public bool OutputStillFlowing { get; init; }
    }

    public sealed class ConsoleProgress
    {
        public string Text { get; init; } = string.Empty;
        public bool IsStderr { get; init; }
        public bool IsInput { get; init; }
        public bool IsDiagnostic { get; init; }
    }

    /// <summary>
    /// Runs a child program interactively. For native console .exes it hosts the
    /// program in a Windows Pseudo Console (ConPTY) so output from programs that
    /// write directly to the console API is captured. For other programs it uses
    /// classic redirected streams.
    /// </summary>
    public sealed class InteractiveProcessSession : IInteractiveConsoleSession
    {
        private readonly Process? _process;
        private readonly BoundedTextBuffer _stdout = new(OutputLimits.ConsoleCharacters);
        private readonly BoundedTextBuffer _stderr = new(OutputLimits.ConsoleCharacters);
        private readonly object _gate = new();
        private readonly Task _stdoutPump;
        private readonly Task _stderrPump;
        private readonly IProgress<ConsoleProgress>? _progress;
        private readonly PseudoConsoleHandle? _pty;
        private readonly StreamReader? _ptyOutputReader;
        private readonly StreamWriter? _ptyInputWriter;
        private readonly SafeHandle? _processHandle;
        private readonly BoundedTextBuffer _ptyScreen = new(OutputLimits.ConsoleCharacters);
        private readonly CancellationTokenSource _ptyCts = new();
        private readonly Task _ptyOutputPump;
        private long _stdoutConsumed;
        private long _stderrConsumed;
        private long _ptyConsumed;
        private bool _inputClosed;
        private bool _disposed;

        private InteractiveProcessSession(
            Process? process,
            PseudoConsoleHandle? pty,
            StreamReader? ptyOutputReader,
            StreamWriter? ptyInputWriter,
            SafeHandle? processHandle,
            bool started,
            string? error,
            IProgress<ConsoleProgress>? progress)
        {
            _process = process;
            _pty = pty;
            _ptyOutputReader = ptyOutputReader;
            _ptyInputWriter = ptyInputWriter;
            _processHandle = processHandle;
            Started = started;
            Error = error;
            _progress = progress;

            if (process == null)
            {
                _stdoutPump = Task.CompletedTask;
                _stderrPump = Task.CompletedTask;
                _ptyOutputPump = Task.CompletedTask;
                _processHandle?.Dispose();
                return;
            }

            if (pty != null)
            {
                _stdoutPump = Task.CompletedTask;
                _stderrPump = Task.CompletedTask;
                _ptyOutputPump = started && ptyOutputReader != null
                    ? PumpPtyAsync(ptyOutputReader, _ptyScreen, _gate, progress, _ptyCts.Token)
                    : Task.CompletedTask;
            }
            else
            {
                _stdoutPump = PumpAsync(process.StandardOutput, _stdout, _gate, false, progress);
                _stderrPump = PumpAsync(process.StandardError, _stderr, _gate, true, progress);
                _ptyOutputPump = Task.CompletedTask;
            }
        }

        public bool Started { get; }
        public string? Error { get; }
        public bool UsesConPty => _pty != null;

        public bool HasExited
        {
            get
            {
                if (_processHandle != null && !_processHandle.IsInvalid)
                {
                    if (!GetExitCodeProcess(_processHandle, out var code))
                        return false;
                    return code != STILL_ACTIVE;
                }

                try
                {
                    return _process == null || _process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
                catch
                {
                    return false;
                }
            }
        }

        public int? ExitCode
        {
            get
            {
                if (_processHandle != null && !_processHandle.IsInvalid)
                {
                    if (!GetExitCodeProcess(_processHandle, out var code) || code == STILL_ACTIVE)
                        return null;
                    return unchecked((int)code);
                }

                try
                {
                    return _process is { HasExited: true } ? _process.ExitCode : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public string StandardOutput
        {
            get
            {
                lock (_gate)
                {
                    if (_pty != null)
                        return _ptyScreen.ToString();
                    return _stdout.ToString();
                }
            }
        }

        public string StandardError
        {
            get
            {
                lock (_gate)
                    return _stderr.ToString();
            }
        }

        public static InteractiveProcessSession Start(
            string fileName,
            string arguments,
            string? workingDirectory,
            IEnumerable<string>? extraPathDirectories = null,
            IProgress<ConsoleProgress>? progress = null,
            bool useConPty = false)
        {
            if (useConPty)
            {
                var ptySession = StartConPty(fileName, arguments, workingDirectory, extraPathDirectories, progress);
                if (ptySession.Started)
                    return ptySession;

                progress?.Report(new ConsoleProgress
                {
                    Text = $"[ConPTY failed: {ptySession.Error}; falling back to stream redirection]{Environment.NewLine}",
                    IsDiagnostic = true
                });
            }

            return StartStream(fileName, arguments, workingDirectory, extraPathDirectories, progress);
        }

        public static bool IsConsoleSubsystemExecutable(string fileName)
        {
            if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                return PEHeaderReader.IsConsoleSubsystem(fileName);
            }
            catch
            {
                return false;
            }
        }

        private static InteractiveProcessSession StartStream(
            string fileName,
            string arguments,
            string? workingDirectory,
            IEnumerable<string>? extraPathDirectories,
            IProgress<ConsoleProgress>? progress)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Environment.CurrentDirectory
                    : workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = new UTF8Encoding(false),
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false)
            };
            PrependPath(startInfo, extraPathDirectories);

            try
            {
                var process = Process.Start(startInfo);
                if (process == null)
                    return new InteractiveProcessSession(null, null, null, null, null, false, $"Failed to start '{fileName}'.", progress);

                return new InteractiveProcessSession(process, null, null, null, null, true, null, progress);
            }
            catch (Exception ex)
            {
                return new InteractiveProcessSession(null, null, null, null, null, false, ex.Message, progress);
            }
        }

        private static InteractiveProcessSession StartConPty(
            string fileName,
            string arguments,
            string? workingDirectory,
            IEnumerable<string>? extraPathDirectories,
            IProgress<ConsoleProgress>? progress)
        {
            var width = (short)120;
            var height = (short)30;

            try
            {
                if (!CreatePipe(out var inputRead, out var inputWrite, IntPtr.Zero, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!CreatePipe(out var outputRead, out var outputWrite, IntPtr.Zero, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                if (!SetHandleInformation(inputWrite, HANDLE_FLAG_INHERIT, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (!SetHandleInformation(outputRead, HANDLE_FLAG_INHERIT, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var hr = CreatePseudoConsole(
                    new COORD { X = width, Y = height },
                    inputRead,
                    outputWrite,
                    0,
                    out var hPty);

                if (hr != 0)
                    throw new Win32Exception(hr);

                // CreatePseudoConsole duplicates these; close our copies so the
                // only remaining PTY-side refs are inside the HPCON.
                CloseHandle(inputRead);
                CloseHandle(outputWrite);

                var pty = new PseudoConsoleHandle(hPty);

                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                        ? Environment.CurrentDirectory
                        : workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                PrependPath(startInfo, extraPathDirectories);

                var created = CreateProcessWithPty(startInfo, hPty);
                if (created == null)
                {
                    pty.Dispose();
                    CloseHandle(inputWrite);
                    CloseHandle(outputRead);
                    return new InteractiveProcessSession(null, null, null, null, null, false, $"Failed to start '{fileName}'.", progress);
                }

                var outputStream = new FileStream(new SafeFileHandle(outputRead, ownsHandle: true), FileAccess.Read, 4096, isAsync: false);
                var inputStream = new FileStream(new SafeFileHandle(inputWrite, ownsHandle: true), FileAccess.Write, 4096, isAsync: false);
                var reader = new StreamReader(outputStream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, 4096, leaveOpen: false);
                var writer = new StreamWriter(inputStream, new UTF8Encoding(false)) { AutoFlush = true };

                return new InteractiveProcessSession(created.Value.Process, pty, reader, writer, created.Value.Handle, true, null, progress);
            }
            catch (Exception ex)
            {
                return new InteractiveProcessSession(null, null, null, null, null, false, ex.Message, progress);
            }
        }

        internal static void PrependPath(ProcessStartInfo startInfo, IEnumerable<string>? extraPathDirectories)
        {
            var dirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(startInfo.WorkingDirectory))
                dirs.Add(startInfo.WorkingDirectory);
            if (extraPathDirectories != null)
                dirs.AddRange(extraPathDirectories.Where(dir => !string.IsNullOrWhiteSpace(dir)));

            if (dirs.Count == 0)
                return;

            var existing = startInfo.Environment.TryGetValue("PATH", out var path) ? path : Environment.GetEnvironmentVariable("PATH");
            var combined = dirs
                .Concat(new[] { existing ?? string.Empty })
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            startInfo.Environment["PATH"] = string.Join(Path.PathSeparator, combined);
        }

        public async Task<ConsoleSlice> WaitForIdleAsync(
            TimeSpan idle,
            TimeSpan maxWait,
            CancellationToken cancellationToken = default)
        {
            if (_pty != null)
                return await WaitForPtyIdleAsync(idle, maxWait, cancellationToken).ConfigureAwait(false);

            return await WaitForStreamIdleAsync(idle, maxWait, cancellationToken).ConfigureAwait(false);
        }

        private async Task<ConsoleSlice> WaitForStreamIdleAsync(
            TimeSpan idle,
            TimeSpan maxWait,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + maxWait;
            var lastChange = DateTime.UtcNow;
            var lastLength = SnapshotLength();
            long stdoutFrom;
            long stderrFrom;
            lock (_gate)
            {
                stdoutFrom = _stdoutConsumed;
                stderrFrom = _stderrConsumed;
            }

            var hasOutput = false;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var length = SnapshotLength();
                if (length != lastLength)
                {
                    lastLength = length;
                    lastChange = DateTime.UtcNow;
                    hasOutput = true;
                }
                else if (HasExited || (hasOutput && DateTime.UtcNow - lastChange >= idle))
                {
                    break;
                }

                var remaining = deadline - DateTime.UtcNow;
                var delay = remaining < TimeSpan.FromMilliseconds(40) ? remaining : TimeSpan.FromMilliseconds(40);
                if (delay <= TimeSpan.Zero)
                    break;

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            if (HasExited)
            {
                try
                {
                    await Task.WhenAny(Task.WhenAll(_stdoutPump, _stderrPump), Task.Delay(200, cancellationToken))
                        .ConfigureAwait(false);
                }
                catch
                {
                }
            }

            lock (_gate)
            {
                var stdout = Slice(_stdout, stdoutFrom, out _stdoutConsumed);
                var stderr = Slice(_stderr, stderrFrom, out _stderrConsumed);
                var flowing = !HasExited && DateTime.UtcNow >= deadline && DateTime.UtcNow - lastChange < idle;

                Debug.WriteLine($"[WaitForIdleAsync] stdout={stdout.Length}, stderr={stderr.Length}");

                return new ConsoleSlice
                {
                    StandardOutput = stdout,
                    StandardError = stderr,
                    Exited = HasExited,
                    OutputStillFlowing = flowing
                };
            }
        }

        private async Task<ConsoleSlice> WaitForPtyIdleAsync(
            TimeSpan idle,
            TimeSpan maxWait,
            CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _ptyCts.Token);
            var combined = linked.Token;
            var deadline = DateTime.UtcNow + maxWait;
            var lastChange = DateTime.UtcNow;
            var lastLength = PtyLength();

            var hasOutput = HasPrintablePtyText();
            while (DateTime.UtcNow < deadline)
            {
                combined.ThrowIfCancellationRequested();

                var length = PtyLength();
                if (length != lastLength)
                {
                    lastLength = length;
                    lastChange = DateTime.UtcNow;
                    hasOutput = HasPrintablePtyText();
                }
                else if (HasExited || (hasOutput && DateTime.UtcNow - lastChange >= idle))
                {
                    break;
                }

                var remaining = deadline - DateTime.UtcNow;
                var delay = remaining < TimeSpan.FromMilliseconds(40) ? remaining : TimeSpan.FromMilliseconds(40);
                if (delay <= TimeSpan.Zero)
                    break;

                await Task.Delay(delay, combined).ConfigureAwait(false);
            }

            if (HasExited)
                await Task.WhenAny(_ptyOutputPump, Task.Delay(500, combined)).ConfigureAwait(false);

            lock (_gate)
            {
                var text = Slice(_ptyScreen, _ptyConsumed, out _ptyConsumed);
                var flowing = !HasExited && DateTime.UtcNow >= deadline && DateTime.UtcNow - lastChange < idle;

                Debug.WriteLine($"[WaitForPtyIdleAsync] length={text.Length}");

                return new ConsoleSlice
                {
                    StandardOutput = text,
                    StandardError = string.Empty,
                    Exited = HasExited,
                    OutputStillFlowing = flowing
                };
            }
        }

        public async Task WriteInputAsync(string text, CancellationToken cancellationToken = default)
        {
            if (_ptyInputWriter != null)
            {
                await WritePtyInputAsync(text, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (_process == null || _inputClosed)
                return;

            var payload = text ?? string.Empty;
            if (!payload.EndsWith('\n'))
                payload += Environment.NewLine;

            _progress?.Report(new ConsoleProgress
            {
                Text = $"> {payload}",
                IsInput = true
            });

            await _process.StandardInput.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task WritePtyInputAsync(string text, CancellationToken cancellationToken)
        {
            if (_ptyInputWriter == null)
                return;

            var payload = NormalizePtyInput(text);
            _progress?.Report(new ConsoleProgress { Text = $"> {payload}", IsInput = true });
            await _ptyInputWriter.WriteAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _ptyInputWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        internal static string NormalizePtyInput(string? text)
        {
            var payload = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Replace('\n', '\r');
            return payload.EndsWith('\r') ? payload : payload + "\r";
        }

        public void CloseInput()
        {
            if (_process == null || _inputClosed)
                return;

            _inputClosed = true;

            // In ConPTY mode there is no redirected StandardInput stream.
            if (_pty != null)
                return;

            try
            {
                _process.StandardInput.Close();
            }
            catch
            {
            }
        }

        public void Kill()
        {
            try
            {
                if (_process is { HasExited: false })
                    _process.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }
            catch
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;
            _disposed = true;

            _ptyCts.Cancel();

            // Close pipes only for the stream-mode path.
            if (_pty == null)
            {
                CloseInput();
                _ptyInputWriter?.Close();
            }

            Kill();

            try
            {
                await Task.WhenAny(Task.WhenAll(_stdoutPump, _stderrPump, _ptyOutputPump), Task.Delay(500))
                    .ConfigureAwait(false);
            }
            catch
            {
            }

            _ptyOutputReader?.Dispose();
            _ptyInputWriter?.Dispose();
            _pty?.Dispose();
            _processHandle?.Dispose();
            _process?.Dispose();
            _ptyCts.Dispose();
        }

        private long SnapshotLength()
        {
            lock (_gate)
                return _stdout.Written + _stderr.Written;
        }

        private long PtyLength()
        {
            lock (_gate)
                return _ptyScreen.Written;
        }

        private bool HasPrintablePtyText()
        {
            lock (_gate)
            {
                var text = _ptyScreen.ReadFrom(_ptyConsumed);
                return HasVisibleConsoleText(text);
            }
        }

        internal static bool HasVisibleConsoleText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\u001b')
                {
                    i++;
                    if (i >= text.Length)
                        break;
                    if (text[i] == '[')
                    {
                        i++;
                        while (i < text.Length && (text[i] < '@' || text[i] > '~'))
                            i++;
                        continue;
                    }

                    if (text[i] == ']')
                    {
                        i++;
                        while (i < text.Length && text[i] != '\u0007' && text[i] != '\u001b')
                            i++;
                        if (i < text.Length && text[i] == '\u001b')
                            i++;
                        continue;
                    }

                    continue;
                }

                if (!char.IsControl(c) && !char.IsWhiteSpace(c))
                    return true;
            }

            return false;
        }

        private static string Slice(BoundedTextBuffer source, long from, out long consumed)
        {
            var text = source.ReadFrom(from);
            consumed = source.Written;
            return text;
        }

        private static async Task PumpAsync(
            StreamReader reader,
            BoundedTextBuffer destination,
            object gate,
            bool isStderr,
            IProgress<ConsoleProgress>? progress)
        {
            var buffer = new char[1024];
            try
            {
                while (true)
                {
                    var read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    if (read <= 0)
                        break;

                    string chunk;
                    lock (gate)
                    {
                        destination.Append(buffer.AsSpan(0, read));
                        chunk = new string(buffer, 0, read);
                    }

                    progress?.Report(new ConsoleProgress
                    {
                        Text = chunk,
                        IsStderr = isStderr
                    });
                }
            }
            catch
            {
            }
        }

        private static Task PumpPtyAsync(
            StreamReader reader,
            BoundedTextBuffer destination,
            object gate,
            IProgress<ConsoleProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(() =>
            {
                var buffer = new byte[4096];
                var characters = new char[4096];
                var decoder = Encoding.UTF8.GetDecoder();
                var handle = ((FileStream)reader.BaseStream).SafeFileHandle;
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (!ReadFile(handle, buffer, (uint)buffer.Length, out var read, IntPtr.Zero) || read == 0)
                            break;

                        var count = decoder.GetChars(buffer, 0, (int)read, characters, 0, flush: false);
                        var chunk = new string(characters, 0, count);
                        lock (gate)
                            destination.Append(chunk);

                        progress?.Report(new ConsoleProgress { Text = chunk });
                    }
                }
                catch
                {
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        #region ConPTY helpers

        private static (Process Process, SafeHandle Handle)? CreateProcessWithPty(ProcessStartInfo startInfo, IntPtr hPty)
        {
            var attributeSize = IntPtr.Zero;
            InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeSize);
            var attributeList = Marshal.AllocHGlobal(attributeSize);

            var startupInfo = new STARTUPINFOEX();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
            // Prevent Windows from copying the host's redirected standard handles.
            // Null handles with STARTF_USESTDHANDLES let ConPTY supply all three;
            // otherwise output can bypass our pipe and input waits on the host.
            startupInfo.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
            startupInfo.lpAttributeList = attributeList;

            var commandLine = new StringBuilder(
                string.IsNullOrWhiteSpace(startInfo.Arguments)
                    ? $"\"{startInfo.FileName}\""
                    : $"\"{startInfo.FileName}\" {startInfo.Arguments}");

            try
            {
                if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeSize))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                // Microsoft sample passes the HPCON handle as lpValue, not a
                // pointer to it. A pointer is treated as the console handle and
                // the child never attaches to the pseudoconsole.
                if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    hPty,
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // Inherit the parent environment. A custom Unicode block that
                // omits SystemRoot causes STATUS_DLL_INIT_FAILED (0xC0000142).
                if (!CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    EXTENDED_STARTUPINFO_PRESENT,
                    IntPtr.Zero,
                    startInfo.WorkingDirectory,
                    ref startupInfo,
                    out var processInfo))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                CloseHandle(processInfo.hThread);
                var handle = new PipeHandle(processInfo.hProcess);
                try
                {
                    var process = Process.GetProcessById(processInfo.dwProcessId);
                    return (process, handle);
                }
                catch
                {
                    handle.Dispose();
                    return null;
                }
            }
            finally
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
        }

        #endregion

        #region P/Invoke

        private const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        private const int STARTF_USESTDHANDLES = 0x00000100;
        private const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        private const int CREATE_SUSPENDED = 0x00000004;
        private const int CREATE_NO_WINDOW = 0x08000000;
        private const int HANDLE_FLAG_INHERIT = 0x00000001;
        private const uint STILL_ACTIVE = 259;
        private static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = new IntPtr(0x00020016);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, uint nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr hObject, int dwMask, int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(SafeHandle hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
            string? lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            int dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr Attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll")]
        private static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

        [DllImport("kernel32.dll")]
        private static extern void ClosePseudoConsole(IntPtr hPC);

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        private sealed class PseudoConsoleHandle : SafeHandle
        {
            public PseudoConsoleHandle(IntPtr handle) : base(IntPtr.Zero, true)
            {
                SetHandle(handle);
            }

            public override bool IsInvalid => handle == IntPtr.Zero;

            protected override bool ReleaseHandle()
            {
                if (!IsInvalid)
                    ClosePseudoConsole(handle);
                return true;
            }
        }

        private sealed class PipeHandle : SafeHandle
        {
            public PipeHandle(IntPtr handle) : base(IntPtr.Zero, true)
            {
                SetHandle(handle);
            }

            public override bool IsInvalid => handle == IntPtr.Zero;

            protected override bool ReleaseHandle()
            {
                if (!IsInvalid)
                    CloseHandle(handle);
                return true;
            }
        }

        #endregion
    }
}
