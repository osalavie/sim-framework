using System.Runtime.InteropServices;

namespace Sim.DeviceAgent.App.Tray;

internal sealed class TrayIcon : IDisposable
{
    private const int WM_NULL = 0;
    private const int WM_CONTEXTMENU = 0x007B;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_APP = 0x8000;
    private const int WM_TRAYICON = WM_APP + 1;

    private const int NIM_ADD = 0;
    private const int NIM_MODIFY = 1;
    private const int NIM_DELETE = 2;
    private const int NIM_SETVERSION = 4;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_INFO = 0x00000010;
    private const int NIF_SHOWTIP = 0x00000080;

    private const int NOTIFYICON_VERSION_4 = 4;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_GRAYED = 0x00000001;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;

    private const int IDI_APPLICATION = 32512;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;

    private const uint CMD_SHOW = 1;
    private const uint CMD_OPEN_CONFIG = 2;
    private const uint CMD_RELOAD = 3;
    private const uint CMD_EXIT = 4;

    private static readonly string WindowClassName = "SimDeviceAgentTrayWindow";

    private readonly string _agentId;
    private readonly Action _onShow;
    private readonly Action _onOpenConfig;
    private readonly Action _onReload;
    private readonly Action _onExit;
    private readonly WndProcDelegate _wndProc;

    private IntPtr _hwnd;
    private IntPtr _icon;
    private uint _taskbarCreatedMessage;
    private bool _disposed;

    public TrayIcon(string agentId, Action onShow, Action onOpenConfig, Action onReload, Action onExit)
    {
        _agentId = string.IsNullOrWhiteSpace(agentId) ? "Device Agent" : agentId;
        _onShow = onShow;
        _onOpenConfig = onOpenConfig;
        _onReload = onReload;
        _onExit = onExit;
        _wndProc = WindowProc;
    }

    public void Initialize()
    {
        var hInstance = GetModuleHandleW(null);

        var windowClass = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = WindowClassName
        };

        RegisterClassExW(ref windowClass);

        _hwnd = CreateWindowExW(
            WS_EX_TOOLWINDOW,
            WindowClassName,
            string.Empty,
            WS_POPUP,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not create the tray message window.");
        }

        _icon = LoadIconW(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");

        AddIcon();
    }

    public void ShowBalloon(string title, string message)
    {
        if (_disposed || _hwnd == IntPtr.Zero)
        {
            return;
        }

        var data = CreateData();
        data.uFlags = NIF_INFO;
        data.szInfo = message.Length > 255 ? message[..255] : message;
        data.szInfoTitle = title.Length > 63 ? title[..63] : title;
        data.dwInfoFlags = 1;
        data.uTimeoutOrVersion = 5000;

        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hwnd != IntPtr.Zero)
        {
            var data = CreateData();
            Shell_NotifyIconW(NIM_DELETE, ref data);
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    private void AddIcon()
    {
        var data = CreateData();
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP;
        Shell_NotifyIconW(NIM_ADD, ref data);

        data = CreateData();
        data.uTimeoutOrVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, ref data);
    }

    private NOTIFYICONDATAW CreateData()
    {
        var tooltip = $"Device Agent · {_agentId}";
        return new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _icon,
            szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };
    }

    private IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (message == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
            {
                AddIcon();
                return IntPtr.Zero;
            }

            if (message == WM_TRAYICON)
            {
                var mouseMessage = (int)(lParam.ToInt64() & 0xFFFF);
                switch (mouseMessage)
                {
                    case WM_LBUTTONDBLCLK:
                        _onShow();
                        return IntPtr.Zero;
                    case WM_RBUTTONUP:
                    case WM_CONTEXTMENU:
                        ShowMenu();
                        return IntPtr.Zero;
                }
            }
        }
        catch
        {
            // Never let a tray callback take down the UI thread.
        }

        return DefWindowProcW(hwnd, message, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AppendMenuW(menu, MF_STRING | MF_GRAYED, 0, $"Device Agent · {_agentId}");
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            AppendMenuW(menu, MF_STRING, CMD_SHOW, "打开主界面");
            AppendMenuW(menu, MF_STRING, CMD_OPEN_CONFIG, "打开配置文件");
            AppendMenuW(menu, MF_STRING, CMD_RELOAD, "重新加载配置");
            AppendMenuW(menu, MF_SEPARATOR, 0, null);
            AppendMenuW(menu, MF_STRING, CMD_EXIT, "退出");

            GetCursorPos(out var point);

            // Required so the menu closes when the user clicks elsewhere.
            SetForegroundWindow(_hwnd);
            var command = TrackPopupMenuEx(
                menu,
                TPM_RETURNCMD | TPM_RIGHTBUTTON,
                point.X,
                point.Y,
                _hwnd,
                IntPtr.Zero);
            PostMessageW(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            switch (command)
            {
                case CMD_SHOW:
                    _onShow();
                    break;
                case CMD_OPEN_CONFIG:
                    _onOpenConfig();
                    break;
                case CMD_RELOAD:
                    _onReload();
                    break;
                case CMD_EXIT:
                    _onExit();
                    break;
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessageW(string message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadIconW(IntPtr instance, IntPtr iconName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenuW(IntPtr menu, uint flags, uint itemId, string? text);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        IntPtr hwnd,
        IntPtr parameters);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessageW(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATAW data);
}
