using System.Runtime.InteropServices;
namespace GuidedGrade.Services;
// Only the launched process's visible, unowned window can receive model events.
internal static class WindowInputDispatcher
{
    internal static void Send(uint processId, WindowInputAction action)
    {
        if (!OperatingSystem.IsWindows() || processId == 0) throw new InvalidOperationException("Window input is unavailable.");
        nint target = 0;
        EnumWindows((window, _) => {
            GetWindowThreadProcessId(window, out var owner);
            if (owner == processId && IsWindowVisible(window) && GetWindow(window, 4) == 0) { target = window; return false; }
            return true;
        }, 0);
        if (target == 0) throw new InvalidOperationException("The launched program has no visible window to receive events.");
        if (IsIconic(target)) ShowWindow(target, 9);
        if (GetForegroundWindow() != target && !SetForegroundWindow(target))
            throw new InvalidOperationException("Could not focus the launched program; no event was sent.");
        GetWindowThreadProcessId(GetForegroundWindow(), out var foregroundOwner);
        if (foregroundOwner != processId || GetForegroundWindow() != target)
            throw new InvalidOperationException("Program focus changed; no event was sent.");
        INPUT[] events;
        if (action.Kind == "key")
        {
            var scan = (ushort)MapVirtualKey(action.VirtualKey, 0);
            uint flags = 0x0008 | (action.VirtualKey is >= 0x25 and <= 0x28 ? 0x0001u : 0);
            events = [new() { type = 1, data = new() { keyboard = new() { scan = scan, flags = flags } } },
                      new() { type = 1, data = new() { keyboard = new() { scan = scan, flags = flags | 0x0002 } } }];
        }
        else if (action.Kind == "click")
        {
            if (!GetClientRect(target, out var rect) || rect.right <= 0 || rect.bottom <= 0)
                throw new InvalidOperationException("The program window has no usable client area.");
            var point = new POINT { x = (int)Math.Round(action.X * (rect.right - 1)), y = (int)Math.Round(action.Y * (rect.bottom - 1)) };
            if (!ClientToScreen(target, ref point) || GetAncestor(WindowFromPoint(point), 2) != target)
                throw new InvalidOperationException("The click location is covered by another window; no event was sent.");
            var left = GetSystemMetrics(76); var top = GetSystemMetrics(77);
            var width = GetSystemMetrics(78); var height = GetSystemMetrics(79);
            events = [new() { data = new() { mouse = new() { x = (point.x - left) * 65535 / Math.Max(1, width - 1), y = (point.y - top) * 65535 / Math.Max(1, height - 1), flags = 0x0001 | 0x8000 | 0x4000 } } },
                      new() { data = new() { mouse = new() { flags = action.RightButton ? 0x0008u : 0x0002u } } },
                      new() { data = new() { mouse = new() { flags = action.RightButton ? 0x0010u : 0x0004u } } }];
        }
        else throw new ArgumentException("Unsupported window event.", nameof(action));
        if (GetForegroundWindow() != target) throw new InvalidOperationException("Program focus changed; no event was sent.");
        var sent = SendInput((uint)events.Length, events, Marshal.SizeOf<INPUT>());
        if (sent != events.Length)
        {
            // If only the down event reached Windows, release it before stopping.
            if (sent > 0) SendInput(1, [events[^1]], Marshal.SizeOf<INPUT>());
            throw new InvalidOperationException("Windows did not deliver all input events (the target may run at a higher privilege level).");
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public UNION data; }
    [StructLayout(LayoutKind.Explicit)] private struct UNION { [FieldOffset(0)] public KEYBOARD keyboard; [FieldOffset(0)] public MOUSE mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBOARD { public ushort key, scan; public uint flags, time; public nuint extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSE { public int x, y; public uint data, flags, time; public nuint extra; }
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
    private delegate bool WindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint key, uint type);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out RECT rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint window, ref POINT point);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] events, int size);
}
