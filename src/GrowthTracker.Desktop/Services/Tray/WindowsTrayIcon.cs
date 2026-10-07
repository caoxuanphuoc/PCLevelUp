using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;

namespace GrowthTracker.Desktop.Services.Tray;

/// <summary>
/// Windows tray icon built directly on Shell_NotifyIcon, because Avalonia's TrayIcon cannot show notifications.
/// A balloon from a notify icon is displayed as a regular toast on Windows 10/11 and needs no AppUserModelID or
/// Start-menu shortcut, so it also works in development and portable runs. Clicking the toast opens Today.
/// It must be created and used on the UI thread, whose message loop dispatches to its hidden window.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsTrayIcon : ITrayService
{
    private const int WM_APP_TRAY = 0x8000 + 1; // WM_APP + 1
    private const int WM_CONTEXTMENU = 0x007B;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_NULL = 0x0000;
    private const int NIN_BALLOONUSERCLICK = 0x0405;
    private const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const int NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04, NIF_INFO = 0x10, NIF_SHOWTIP = 0x80;
    private const int NIIF_INFO = 0x01;
    private const int NOTIFYICON_VERSION_4 = 4;
    private const int TPM_RETURNCMD = 0x0100, TPM_RIGHTBUTTON = 0x0002;
    private const int MF_STRING = 0x0000, MF_SEPARATOR = 0x0800;
    private const int CmdOpen = 1, CmdToday = 2, CmdExit = 3;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private readonly WndProc _wndProc; // kept alive: the native window calls back into it
    private readonly uint _taskbarCreatedMessage;
    private IntPtr _hwnd;
    private IntPtr _icon;
    private bool _added;

    public WindowsTrayIcon()
    {
        _wndProc = WindowProc;
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? OpenTodayRequested;
    public event EventHandler? ExitRequested;

    public void Show()
    {
        if (_added)
            return;
        if (_hwnd == IntPtr.Zero)
            CreateMessageWindow();
        _icon = LoadAppIcon();
        AddIcon();
    }

    /// <summary>Shows a notification (rendered as a toast on Windows 10/11).</summary>
    public void ShowBalloon(string title, string message)
    {
        if (!_added)
            Show();
        var data = NewData(NIF_INFO);
        data.szInfoTitle = Truncate(title, 63);
        data.szInfo = Truncate(message, 255);
        data.dwInfoFlags = NIIF_INFO;
        if (!Shell_NotifyIcon(NIM_MODIFY, ref data))
            Log.Warning("Shell_NotifyIcon could not show the notification");
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = NewData(0);
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }
        if (_icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    private void CreateMessageWindow()
    {
        string className = "GrowthTrackerTray_" + Environment.ProcessId;
        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = className,
        };
        if (RegisterClassEx(ref wc) == 0)
            throw new InvalidOperationException("RegisterClassEx failed: " + Marshal.GetLastWin32Error());
        _hwnd = CreateWindowEx(0, className, "Growth Tracker tray", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException("CreateWindowEx failed: " + Marshal.GetLastWin32Error());
    }

    private void AddIcon()
    {
        var data = NewData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        data.uCallbackMessage = WM_APP_TRAY;
        data.hIcon = _icon;
        data.szTip = "Growth Tracker";
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
        if (!_added)
        {
            Log.Warning("Shell_NotifyIcon could not add the tray icon");
            return;
        }
        data.uTimeoutOrVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
    }

    private IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_APP_TRAY)
        {
            // NOTIFYICON_VERSION_4: the event is in LOWORD(lParam).
            switch ((int)(lParam.ToInt64() & 0xFFFF))
            {
                case WM_LBUTTONUP:
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case NIN_BALLOONUSERCLICK:
                    OpenTodayRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case WM_CONTEXTMENU:
                    ShowMenu();
                    break;
            }
            return IntPtr.Zero;
        }
        if (msg == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            // Explorer restarted: the icon is gone and must be added again.
            _added = false;
            AddIcon();
            return IntPtr.Zero;
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        try
        {
            AppendMenu(menu, MF_STRING, CmdOpen, "Mở Growth Tracker");
            AppendMenu(menu, MF_STRING, CmdToday, "Hôm nay");
            AppendMenu(menu, MF_SEPARATOR, 0, null);
            AppendMenu(menu, MF_STRING, CmdExit, "Thoát");
            GetCursorPos(out var point);
            SetForegroundWindow(_hwnd); // required so the menu closes when clicking elsewhere
            int command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, point.X, point.Y, _hwnd, IntPtr.Zero);
            PostMessage(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            switch (command)
            {
                case CmdOpen: OpenRequested?.Invoke(this, EventArgs.Empty); break;
                case CmdToday: OpenTodayRequested?.Invoke(this, EventArgs.Empty); break;
                case CmdExit: ExitRequested?.Invoke(this, EventArgs.Empty); break;
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private NOTIFYICONDATA NewData(int flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = flags,
        szTip = "",
        szInfo = "",
        szInfoTitle = "",
    };

    /// <summary>The exe's own icon (ApplicationIcon in the csproj), small size for the tray.</summary>
    private static IntPtr LoadAppIcon()
    {
        var path = Environment.ProcessPath;
        if (path is not null && ExtractIconEx(path, 0, out var large, out var small, 1) > 0 && small != IntPtr.Zero)
        {
            if (large != IntPtr.Zero)
                DestroyIcon(large);
            return small;
        }
        return LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";

    // ── Win32 interop ──
    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    private static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
    private static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint icons);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "RegisterClassExW")]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateWindowExW")]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName, int style, int x, int y,
                                                int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterWindowMessageW")]
    private static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
    private static extern bool AppendMenu(IntPtr menu, int flags, int id, string? text);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr menu, int flags, int x, int y, IntPtr hwnd, IntPtr tpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "LoadIconW")]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr name);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
    private static extern IntPtr GetModuleHandle(string? module);
}
