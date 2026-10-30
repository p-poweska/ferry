using System.Runtime.InteropServices;

namespace Ferry.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MONITORINFO
{
    public uint Size;
    public RECT Monitor;
    public RECT Work;
    public uint Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public nint Window;
    public uint Message;
    public nint WParam;
    public nint LParam;
    public uint Time;
    public POINT Point;
    public uint Private;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSLLHOOKSTRUCT
{
    public POINT Point;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public nuint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WNDCLASSEXW
{
    public uint Size;
    public uint Style;
    public delegate* unmanaged<nint, uint, nint, nint, nint> WindowProcedure;
    public int ClassExtra;
    public int WindowExtra;
    public nint Instance;
    public nint Icon;
    public nint Cursor;
    public nint Background;
    public char* MenuName;
    public char* ClassName;
    public nint SmallIcon;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NOTIFYICONDATAW
{
    public uint Size;
    public nint Window;
    public uint Id;
    public uint Flags;
    public uint CallbackMessage;
    public nint Icon;
    public fixed char Tip[128];
    public uint State;
    public uint StateMask;
    public fixed char Info[256];
    public uint Version;
    public fixed char InfoTitle[64];
    public uint InfoFlags;
    public Guid Item;
    public nint BalloonIcon;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct SHFILEINFOW
{
    public nint Icon;
    public int IconIndex;
    public uint Attributes;
    public fixed char DisplayName[260];
    public fixed char TypeName[80];
}

/// <summary>The Win32 functions Ferry uses, from user32, shell32, ole32 and friends.</summary>
internal static unsafe partial class NativeMethods
{
    // Window messages.
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint WM_DISPLAYCHANGE = 0x007E;
    public const uint WM_NCLBUTTONDOWN = 0x00A1;
    public const uint WM_HOTKEY = 0x0312;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_QUIT = 0x0012;
    public const uint WM_NULL = 0x0000;
    public const uint WM_USER = 0x0400;
    public const uint WM_APP = 0x8000;

    // Window styles.
    public const uint WS_POPUP = 0x80000000;
    public const int GWL_EXSTYLE = -20;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_LAYERED = 0x00080000;
    public const uint WS_EX_NOACTIVATE = 0x08000000;
    public const uint LWA_ALPHA = 0x2;
    public const int HTCAPTION = 2;

    // SetWindowPos and ShowWindow.
    public static readonly nint HWND_TOPMOST = -1;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;

    // Monitors and DPI.
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int MDT_EFFECTIVE_DPI = 0;
    public const int SM_CXDRAG = 68;
    public const int SM_CYDRAG = 69;
    public const int SM_CXSMICON = 49;
    public const int SM_CYSMICON = 50;

    // DWM.
    public const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_ROUND = 2;

    // Hot keys and hooks.
    public const uint MOD_NOREPEAT = 0x4000;
    public const int WH_MOUSE_LL = 14;

    // Tray icons.
    public const uint NIM_ADD = 0;
    public const uint NIM_MODIFY = 1;
    public const uint NIM_DELETE = 2;
    public const uint NIM_SETVERSION = 4;
    public const uint NIF_MESSAGE = 0x01;
    public const uint NIF_ICON = 0x02;
    public const uint NIF_TIP = 0x04;
    public const uint NIF_SHOWTIP = 0x80;
    public const uint NOTIFYICON_VERSION_4 = 4;
    public const uint NIN_SELECT = WM_USER;
    public const uint NIN_KEYSELECT = WM_USER + 1;
    public const uint IMAGE_ICON = 1;
    public const uint LR_LOADFROMFILE = 0x10;

    // Menus.
    public const uint MF_STRING = 0x0000;
    public const uint MF_GRAYED = 0x0001;
    public const uint MF_SEPARATOR = 0x0800;
    public const uint TPM_RIGHTBUTTON = 0x0002;
    public const uint TPM_NONOTIFY = 0x0080;
    public const uint TPM_RETURNCMD = 0x0100;

    // OLE drag and drop.
    public const ushort CF_HDROP = 15;
    public const uint DVASPECT_CONTENT = 1;
    public const uint TYMED_HGLOBAL = 1;
    public const uint TYMED_ISTREAM = 4;
    public const uint TYMED_ISTORAGE = 8;
    public const uint DROPEFFECT_NONE = 0;
    public const uint DROPEFFECT_COPY = 1;
    public const uint DROPEFFECT_MOVE = 2;
    public const uint DROPEFFECT_LINK = 4;
    public const uint STGM_READWRITE = 0x2;
    public const uint STGM_SHARE_EXCLUSIVE = 0x10;
    public const uint STGM_CREATE = 0x1000;
    public const uint CLSCTX_INPROC_SERVER = 0x1;
    public const uint CLSCTX_ALL = 0x17;

    // Shell.
    public const uint SHGFI_TYPENAME = 0x400;
    public const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    // Keys.
    public const uint MAPVK_VK_TO_VSC = 0;

    public static int LowWord(nint value) => (short)((long)value & 0xFFFF);
    public static int HighWord(nint value) => (short)(((long)value >> 16) & 0xFFFF);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandle(string? moduleName);

    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll")]
    public static partial uint GetACP();

    [LibraryImport("kernel32.dll")]
    public static partial void* GlobalLock(nint memory);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalUnlock(nint memory);

    [LibraryImport("kernel32.dll")]
    public static partial nuint GlobalSize(nint memory);

    // Windows.

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    public static partial ushort RegisterClassEx(in WNDCLASSEXW windowClass);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowEx(
        uint exStyle, string className, string? windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint window);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static partial nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessage(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    public static partial nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessage(string name);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint window, int index, nint value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShowWindow(nint window, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(nint window, out RECT rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetLayeredWindowAttributes(nint window, uint colorKey, byte alpha, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumChildWindows(nint parent, delegate* unmanaged<nint, nint, int> callback, nint parameter);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReleaseCapture();

    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint window);

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetricsForDpi(int index, uint dpi);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint window, uint attribute, in int value, int size);

    // Monitors and the pointer.

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT point);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(POINT point, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint monitor, ref MONITORINFO info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplayMonitors(nint dc, nint clip, delegate* unmanaged<nint, nint, RECT*, nint, int> callback, nint parameter);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    // Hot keys, hooks and message loops.

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint key);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint window, int id);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static partial nint SetWindowsHookEx(int hook, delegate* unmanaged<int, nint, nint, nint> procedure, nint module, uint threadId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    public static partial nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    public static partial int GetMessage(out MSG message, nint window, uint filterMin, uint filterMax);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);

    // Keys.

    [LibraryImport("user32.dll")]
    public static partial short GetKeyState(int key);

    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    public static partial uint MapVirtualKey(uint code, uint mapType);

    [LibraryImport("user32.dll", EntryPoint = "GetKeyNameTextW")]
    public static partial int GetKeyNameText(int lParam, char* buffer, int size);

    // Tray icons and menus.

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool Shell_NotifyIcon(uint message, in NOTIFYICONDATAW data);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint LoadImage(nint instance, string name, uint type, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll")]
    public static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AppendMenu(nint menu, uint flags, nuint id, string? text);

    [LibraryImport("user32.dll")]
    public static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint window, nint parameters);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyMenu(nint menu);

    /// <summary>Lets native menus follow dark mode (uxtheme's SetPreferredAppMode).</summary>
    [LibraryImport("uxtheme.dll", EntryPoint = "#135")]
    public static partial int SetPreferredAppMode(int mode);

    // OLE.

    [LibraryImport("ole32.dll")]
    public static partial int OleInitialize(nint reserved);

    [LibraryImport("ole32.dll")]
    public static partial int RegisterDragDrop(nint window, nint dropTarget);

    [LibraryImport("ole32.dll")]
    public static partial int RevokeDragDrop(nint window);

    [LibraryImport("ole32.dll")]
    public static partial void ReleaseStgMedium(ref STGMEDIUM medium);

    [LibraryImport("ole32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int StgCreateDocfile(string name, uint mode, uint reserved, out nint storage);

    [LibraryImport("ole32.dll")]
    public static partial int CoCreateInstance(in Guid classId, nint outer, uint context, in Guid interfaceId, out nint instance);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial ushort RegisterClipboardFormat(string name);

    [LibraryImport("shell32.dll", EntryPoint = "DragQueryFileW")]
    public static partial uint DragQueryFile(nint drop, uint index, char* buffer, uint size);

    // Shell.

    [LibraryImport("shell32.dll", EntryPoint = "SHGetFileInfoW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint SHGetFileInfo(string path, uint attributes, ref SHFILEINFOW info, uint size, uint flags);

    [LibraryImport("shell32.dll", EntryPoint = "ILCreateFromPathW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint ILCreateFromPath(string path);

    [LibraryImport("shell32.dll")]
    public static partial void ILFree(nint itemList);

    [LibraryImport("shell32.dll")]
    public static partial int SHCreateDataObject(nint folder, uint count, nint* items, nint inner, in Guid interfaceId, out nint dataObject);

    [LibraryImport("shell32.dll")]
    public static partial int SHDoDragDrop(nint window, nint dataObject, nint dropSource, uint allowedEffects, out uint effect);

    [LibraryImport("shell32.dll")]
    public static partial int SHOpenFolderAndSelectItems(nint folder, uint count, nint* items, uint flags);
}
