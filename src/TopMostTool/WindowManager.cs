using System.Text;
using System.Runtime.InteropServices;

namespace TopMostTool;

internal sealed record ManagedWindow(
    IntPtr Handle,
    uint ThreadId,
    uint ProcessId,
    string ClassName,
    string Title,
    DateTime PinnedAt);

internal sealed class WindowManager
{
    private readonly Dictionary<IntPtr, ManagedWindow> _managedWindows = new();

    internal int Count
    {
        get
        {
            CleanupInvalidWindows();
            return _managedWindows.Count;
        }
    }

    internal IReadOnlyList<ManagedWindow> GetManagedWindows()
    {
        CleanupInvalidWindows();
        return _managedWindows.Values
            .OrderBy(window => window.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal bool IsEligibleWindow(IntPtr hWnd, IntPtr ownWindow = default)
    {
        if (hWnd == IntPtr.Zero || hWnd == ownWindow || !NativeMethods.IsWindow(hWnd) ||
            !NativeMethods.IsWindowVisible(hWnd) || hWnd == NativeMethods.GetDesktopWindow() ||
            hWnd == NativeMethods.GetShellWindow())
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
        if (processId == Environment.ProcessId)
        {
            return false;
        }

        string className = GetClassName(hWnd);
        return className switch
        {
            "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW" => false,
            _ => IsTopLevelWindow(hWnd) && !IsCloaked(hWnd)
        };
    }

    internal bool IsTopMost(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd))
        {
            return false;
        }

        long style = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GwlExStyle).ToInt64();
        return (style & NativeMethods.WsExTopmost) != 0;
    }

    internal bool TryToggle(IntPtr hWnd, IntPtr ownWindow, out bool isNowTopMost, out string error)
    {
        isNowTopMost = false;
        error = string.Empty;
        if (!IsEligibleWindow(hWnd, ownWindow))
        {
            error = "无法置顶当前窗口。请先切换到一个普通应用窗口。";
            return false;
        }

        bool shouldPin = !IsTopMost(hWnd);
        if (!TrySetTopMost(hWnd, shouldPin, out error))
        {
            return false;
        }

        if (shouldPin)
        {
            AddManagedWindow(hWnd);
        }
        else
        {
            _managedWindows.Remove(hWnd);
        }

        isNowTopMost = shouldPin;
        return true;
    }

    internal bool TrySet(IntPtr hWnd, bool pin, IntPtr ownWindow, out string error)
    {
        error = string.Empty;
        if (!IsEligibleWindow(hWnd, ownWindow))
        {
            error = "无法置顶所选窗口。请选择普通应用窗口。";
            return false;
        }

        if (IsTopMost(hWnd) == pin)
        {
            if (!pin)
            {
                _managedWindows.Remove(hWnd);
            }

            return true;
        }

        if (!TrySetTopMost(hWnd, pin, out error))
        {
            return false;
        }

        if (pin)
        {
            AddManagedWindow(hWnd);
        }
        else
        {
            _managedWindows.Remove(hWnd);
        }

        return true;
    }

    internal bool TryUnpinManaged(IntPtr hWnd, out string error)
    {
        error = string.Empty;
        if (!_managedWindows.ContainsKey(hWnd))
        {
            error = "该窗口不在本工具的置顶列表中。";
            return false;
        }

        if (HasSameIdentity(_managedWindows[hWnd]) && IsTopMost(hWnd) && !TrySetTopMost(hWnd, false, out error))
        {
            return false;
        }

        _managedWindows.Remove(hWnd);
        return true;
    }

    internal int UnpinAllManaged(out string? error)
    {
        error = null;
        int count = 0;
        foreach (ManagedWindow window in _managedWindows.Values.ToArray())
        {
            if (TryUnpinManaged(window.Handle, out string currentError))
            {
                count++;
            }
            else if (error is null)
            {
                error = currentError;
            }
        }

        return count;
    }

    internal void RestoreManagedWindows()
    {
        foreach (ManagedWindow window in _managedWindows.Values.ToArray())
        {
            if (HasSameIdentity(window) && IsTopMost(window.Handle))
            {
                TrySetTopMost(window.Handle, false, out _);
            }
        }

        _managedWindows.Clear();
    }

    private void AddManagedWindow(IntPtr hWnd)
    {
        if (_managedWindows.ContainsKey(hWnd))
        {
            return;
        }

        uint threadId = NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
        _managedWindows[hWnd] = new ManagedWindow(
            hWnd,
            threadId,
            processId,
            GetClassName(hWnd),
            GetWindowTitle(hWnd),
            DateTime.Now);
    }

    private void CleanupInvalidWindows()
    {
        foreach (ManagedWindow window in _managedWindows.Values.ToArray())
        {
            if (!HasSameIdentity(window) || !IsTopMost(window.Handle))
            {
                _managedWindows.Remove(window.Handle);
            }
        }
    }

    private static bool HasSameIdentity(ManagedWindow window)
    {
        if (!NativeMethods.IsWindow(window.Handle))
        {
            return false;
        }

        uint threadId = NativeMethods.GetWindowThreadProcessId(window.Handle, out uint processId);
        return threadId == window.ThreadId &&
               processId == window.ProcessId &&
               string.Equals(GetClassName(window.Handle), window.ClassName, StringComparison.Ordinal);
    }

    private static bool IsTopLevelWindow(IntPtr hWnd)
    {
        bool found = false;
        NativeMethods.EnumWindows((candidate, _) =>
        {
            if (candidate != hWnd)
            {
                return true;
            }

            found = true;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static bool TrySetTopMost(IntPtr hWnd, bool pin, out string error)
    {
        bool success = NativeMethods.SetWindowPos(
            hWnd,
            pin ? NativeMethods.HwndTopmost : NativeMethods.HwndNoTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize |
            NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);

        if (success)
        {
            error = string.Empty;
            return true;
        }

        int win32Error = Marshal.GetLastWin32Error();
        error = $"窗口置顶操作未能完成（Windows 错误 {win32Error}）。";
        return false;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        int length = NativeMethods.GetWindowTextLength(hWnd);
        var buffer = new StringBuilder(Math.Max(length + 1, 256));
        NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
        string title = buffer.ToString().Trim();
        return string.IsNullOrWhiteSpace(title) ? "无标题窗口" : title;
    }

    private static string GetClassName(IntPtr hWnd)
    {
        var buffer = new StringBuilder(256);
        NativeMethods.GetClassName(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        return NativeMethods.DwmGetWindowAttribute(
            hWnd,
            NativeMethods.DwmwaCloaked,
            out int cloaked,
            sizeof(int)) == 0 && cloaked != 0;
    }
}
