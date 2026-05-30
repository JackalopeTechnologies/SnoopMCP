// TrayIcon.cs
// Copyright (c) 2026 Jackalope Technologies

namespace SnoopMCP.Host;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

/// <summary>
/// Notification-area icon built directly on Win32 <c>Shell_NotifyIcon</c>, hosted by a hidden
/// top-level <see cref="HwndSource"/> window. The window is top-level (not message-only) so it
/// receives the <c>TaskbarCreated</c> broadcast and can re-add the icon after an Explorer restart.
/// Right-click shows a WPF context menu with Start / Stop / Status / Exit.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const uint NimAdd = 0x0;
    private const uint NimModify = 0x1;
    private const uint NimDelete = 0x2;
    private const uint NifMessage = 0x1;
    private const uint NifIcon = 0x2;
    private const uint NifTip = 0x4;
    private const uint NifInfo = 0x10;
    private const uint WmTrayIcon = 0x8001;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmNull = 0x0000;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x10;
    private const uint LrDefaultSize = 0x40;
    private const uint TrayIconId = 1;
    private const long LoWordMask = 0xFFFF;
    private const int TipCapacity = 128;
    private const int InfoCapacity = 256;
    private const int InfoTitleCapacity = 64;
    private const string IconRelativePath = "Images\\icon.ico";
    private const string TaskbarCreatedMessage = "TaskbarCreated";
    private const string WindowName = "SnoopMcpTrayWindow";
    private const string StartHeader = "Start MCP";
    private const string StopHeader = "Stop MCP";
    private const string StatusHeader = "Status";
    private const string ExitHeader = "Exit";
    private const string BalloonTitle = "SnoopMCP";
    private const string StatusFormat = "{0}\nAttached: {1}";
    private const string AttachedYes = "yes";
    private const string AttachedNo = "no";

    private static readonly CompositeFormat smStatusFormat = CompositeFormat.Parse(StatusFormat);

    private readonly ServerController mController;
    private readonly Action mExit;
    private readonly HwndSource mSource;
    private readonly ContextMenu mMenu;
    private readonly MenuItem mStartItem;
    private readonly MenuItem mStopItem;
    private readonly uint mTaskbarCreated;
    private IntPtr mIcon;
    private bool mAdded;

    /// <summary>Creates the tray icon and adds it to the notification area.</summary>
    /// <param name="controller">The server controller the menu drives.</param>
    /// <param name="exit">Callback that shuts the whole application down.</param>
    public TrayIcon(ServerController controller, Action exit)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(exit);
        mController = controller;
        mExit = exit;

        var parameters = new HwndSourceParameters(WindowName)
        {
            Width = 0,
            Height = 0
        };
        mSource = new HwndSource(parameters);
        mSource.AddHook(WndProc);
        mTaskbarCreated = RegisterWindowMessageW(TaskbarCreatedMessage);
        mIcon = LoadTrayIcon();

        mStartItem = new MenuItem { Header = StartHeader };
        mStartItem.Click += (_, _) => OnStart();
        mStopItem = new MenuItem { Header = StopHeader };
        mStopItem.Click += (_, _) => OnStop();
        var statusItem = new MenuItem { Header = StatusHeader };
        statusItem.Click += (_, _) => OnStatus();
        var exitItem = new MenuItem { Header = ExitHeader };
        exitItem.Click += (_, _) => mExit();

        mMenu = new ContextMenu();
        mMenu.Items.Add(mStartItem);
        mMenu.Items.Add(mStopItem);
        mMenu.Items.Add(new Separator());
        mMenu.Items.Add(statusItem);
        mMenu.Items.Add(new Separator());
        mMenu.Items.Add(exitItem);

        mController.StateChanged += (_, _) => OnStateChanged();
        AddOrModify(NimAdd);
        OnStateChanged();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (mAdded)
        {
            NOTIFYICONDATAW data = CreateData(NifMessage);
            Shell_NotifyIconW(NimDelete, ref data);
            mAdded = false;
        }
        if (mIcon != IntPtr.Zero)
        {
            DestroyIcon(mIcon);
            mIcon = IntPtr.Zero;
        }
        mSource.RemoveHook(WndProc);
        mSource.Dispose();
    }

    private static IntPtr LoadTrayIcon()
    {
        string path = Path.Combine(AppContext.BaseDirectory, IconRelativePath);
        return LoadImageW(IntPtr.Zero, path, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
    }

    private void OnStart()
    {
        _ = mController.StartAsync();
    }

    private void OnStop()
    {
        _ = mController.StopAsync();
    }

    private void OnStatus()
    {
        string attached = mController.IsAttached ? AttachedYes : AttachedNo;
        string text = string.Format(
            CultureInfo.InvariantCulture, smStatusFormat, ServerStateInfo.Tooltip(mController.State), attached);
        ShowBalloon(text);
    }

    private void OnStateChanged()
    {
        mStartItem.IsEnabled = ServerStateInfo.CanStart(mController.State);
        mStopItem.IsEnabled = ServerStateInfo.CanStop(mController.State);
        if (mAdded)
        {
            AddOrModify(NimModify);
        }
    }

    private void AddOrModify(uint message)
    {
        NOTIFYICONDATAW data = CreateData(NifMessage | NifIcon | NifTip);
        data.hIcon = mIcon;
        data.szTip = ServerStateInfo.Tooltip(mController.State);
        bool ok = Shell_NotifyIconW(message, ref data);
        if (ok && message == NimAdd)
        {
            mAdded = true;
        }
    }

    private void ShowBalloon(string text)
    {
        NOTIFYICONDATAW data = CreateData(NifInfo);
        data.szInfo = text;
        data.szInfoTitle = BalloonTitle;
        Shell_NotifyIconW(NimModify, ref data);
    }

    private NOTIFYICONDATAW CreateData(uint flags)
    {
        return new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = mSource.Handle,
            uID = TrayIconId,
            uFlags = flags,
            uCallbackMessage = WmTrayIcon,
            szTip = string.Empty,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        uint message = (uint)msg;
        if (message == WmTrayIcon)
        {
            handled = HandleTrayMessage(lParam);
        }
        if (message == mTaskbarCreated)
        {
            AddOrModify(NimAdd);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private bool HandleTrayMessage(IntPtr lParam)
    {
        uint mouse = (uint)(lParam.ToInt64() & LoWordMask);
        bool handled = mouse is WmRButtonUp or WmLButtonUp;
        if (handled)
        {
            ShowMenu();
        }
        return handled;
    }

    private void ShowMenu()
    {
        SetForegroundWindow(mSource.Handle);
        mMenu.Placement = PlacementMode.MousePoint;
        mMenu.IsOpen = true;
        PostMessageW(mSource.Handle, WmNull, IntPtr.Zero, IntPtr.Zero);
    }

    // STY0004 suppression: this is the Win32 NOTIFYICONDATAW interop layout. The field names mirror
    // the documented shell32 struct contract one-for-one; renaming them to the pm-prefixed house style
    // would sever that mapping and make the marshalling impossible to verify against the Win32 headers.
#pragma warning disable STY0004
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = TipCapacity)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = InfoCapacity)]
        public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = InfoTitleCapacity)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }
#pragma warning restore STY0004

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImageW(IntPtr hinst, string lpszName, uint uType, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessageW(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
