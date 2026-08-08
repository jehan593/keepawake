using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Keepawake.Data;
using Keepawake.Native;

namespace Keepawake.Ui;

/// <summary>
/// The whole UI: a hidden message-only-ish window that owns a Shell_NotifyIcon tray icon and an
/// owner-drawn native popup menu (see MenuTheme for the Nord/Martian-Mono rendering). No Avalonia/
/// Skia here — Windows never supplies themed chrome for a tray context menu on its own (the old
/// Theme/Styles.axaml comment on this, now removed, cited exactly why: "if you want to style native
/// tray menu on windows, just style [it] yourself"), so getting the Nord look either way means owning
/// the drawing — this just does it directly with GDI instead of through Avalonia's Skia backend.
///
/// Left-clicking the icon directly toggles "keep screen on"; right-click shows the full menu (keep
/// screen on, start-with-Windows, exit) — same behavior as before this rewrite.
/// </summary>
internal sealed class TrayIcon
{
    private const string WindowClassName = "KeepawakeTrayWindow";
    private const int FirstCommandId = 1001;

    private static TrayIcon? _instance;

    private readonly AppSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly IntPtr _hwnd;
    private readonly IntPtr _onIcon;
    private readonly IntPtr _offIcon;
    private readonly IntPtr _font;
    private readonly string _fontPath;
    private readonly uint _taskbarCreatedMessage;

    private IntPtr _hMenu;
    private IntPtr _menuBackBrush;
    private List<MenuItemDescriptor> _items = new();
    private bool _iconAdded;

    public TrayIcon(AppSettings settings, SettingsStore settingsStore)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _instance = this;

        var hInstance = Win32.GetModuleHandleW(null);

        unsafe
        {
            var wndProc = (delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr, IntPtr, IntPtr>)&WndProc;
            var windowClass = new Win32.WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
                lpfnWndProc = (IntPtr)wndProc,
                hInstance = hInstance,
                lpszClassName = WindowClassName,
            };
            Win32.RegisterClassExW(ref windowClass);
        }

        // Never shown (no WS_VISIBLE) — it exists purely to own the tray icon's callback messages and
        // the owner-draw menu's WM_MEASUREITEM/WM_DRAWITEM. Still a real top-level window (not
        // HWND_MESSAGE) because SetForegroundWindow, needed to make the popup menu dismiss correctly
        // on an outside click, doesn't work on message-only windows.
        _hwnd = Win32.CreateWindowExW(0, WindowClassName, "keepawake", 0, 0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        var iconSize = Win32.GetSystemMetrics(Win32.SM_CXSMICON);
        _onIcon = Win32.LoadImageW(IntPtr.Zero, Path.Combine(AppContext.BaseDirectory, "Assets", "app-on.ico"),
            Win32.IMAGE_ICON, iconSize, iconSize, Win32.LR_LOADFROMFILE);
        _offIcon = Win32.LoadImageW(IntPtr.Zero, Path.Combine(AppContext.BaseDirectory, "Assets", "app-off.ico"),
            Win32.IMAGE_ICON, iconSize, iconSize, Win32.LR_LOADFROMFILE);

        _fontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "martian_mono_regular.ttf");
        _font = MenuTheme.LoadFont(_fontPath);

        // Explorer restarting (crash, "Restart Explorer" from Task Manager) drops every tray icon
        // silently; re-adding ours when this broadcast message arrives is the standard fix.
        _taskbarCreatedMessage = Win32.RegisterWindowMessageW("TaskbarCreated");

        Rebuild();
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var result = _instance?.HandleMessage(hWnd, msg, wParam, lParam);
        return result ?? Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private IntPtr? HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _taskbarCreatedMessage)
        {
            _iconAdded = false;
            UpdateTrayIcon();
            return IntPtr.Zero;
        }

        if (msg == Win32.WM_TRAYICON)
        {
            // Default (version-0) NOTIFYICONDATA callback semantics: lParam IS the mouse message
            // itself, not word-packed the way NOTIFYICON_VERSION_4 packs it — we never call
            // Shell_NotifyIcon(NIM_SETVERSION, ...), so this comparison is correct as-is.
            var evt = (uint)lParam.ToInt64();
            if (evt == Win32.WM_LBUTTONUP) ToggleEnabled();
            else if (evt == Win32.WM_RBUTTONUP) ShowMenu();
            return IntPtr.Zero;
        }

        if (msg == Win32.WM_MEASUREITEM)
        {
            var measureItem = Marshal.PtrToStructure<Win32.MEASUREITEMSTRUCT>(lParam);
            MeasureItem(ref measureItem);
            Marshal.StructureToPtr(measureItem, lParam, false);
            return (IntPtr)1;
        }

        if (msg == Win32.WM_DRAWITEM)
        {
            var drawItem = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);
            DrawItem(in drawItem);
            return (IntPtr)1;
        }

        if (msg == Win32.WM_DESTROY)
        {
            Cleanup();
            Win32.PostQuitMessage(0);
            return IntPtr.Zero;
        }

        return null;
    }

    private void UpdateTrayIcon()
    {
        var data = new Win32.NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
            uCallbackMessage = Win32.WM_TRAYICON,
            hIcon = _settings.Enabled ? _onIcon : _offIcon,
            szTip = _settings.Enabled ? "keepawake — Screen kept on" : "keepawake — Off",
            szInfo = "",
            szInfoTitle = "",
        };
        Win32.Shell_NotifyIconW(_iconAdded ? Win32.NIM_MODIFY : Win32.NIM_ADD, ref data);
        _iconAdded = true;
    }

    private void Rebuild()
    {
        UpdateTrayIcon();

        if (_hMenu != IntPtr.Zero) Win32.DestroyMenu(_hMenu);
        _hMenu = Win32.CreatePopupMenu();

        // MIM_BACKGROUND covers the popup's own border/padding area, which owner-draw item rects
        // don't reach — without this it stays system COLOR_MENU (near-white), showing through as a
        // pale edge around an otherwise dark menu.
        var backBrush = Win32.CreateSolidBrush(MenuTheme.Background);
        var menuInfo = new Win32.MENUINFO
        {
            cbSize = (uint)Marshal.SizeOf<Win32.MENUINFO>(),
            fMask = Win32.MIM_BACKGROUND,
            hbrBack = backBrush,
        };
        Win32.SetMenuInfo(_hMenu, ref menuInfo);
        if (_menuBackBrush != IntPtr.Zero) Win32.DeleteObject(_menuBackBrush);
        _menuBackBrush = backBrush;

        var statusText = _settings.Enabled ? "Screen kept on" : "Off";

        _items =
        [
            new MenuItemDescriptor { Text = statusText, IsEnabled = false },
            new MenuItemDescriptor { IsSeparator = true },
            new MenuItemDescriptor { Text = "Keep screen on", IsChecked = _settings.Enabled, OnClick = ToggleEnabled },
            new MenuItemDescriptor { IsSeparator = true },
            new MenuItemDescriptor { Text = "Start with Windows", IsChecked = StartupRegistration.IsEnabled(), OnClick = ToggleStartWithWindows },
            new MenuItemDescriptor { IsSeparator = true },
            new MenuItemDescriptor { Text = "Exit", OnClick = Exit },
        ];

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var flags = Win32.MF_OWNERDRAW;
            if (item.IsSeparator) flags |= Win32.MF_SEPARATOR;
            if (!item.IsEnabled) flags |= Win32.MF_DISABLED | Win32.MF_GRAYED;
            if (item.IsChecked) flags |= Win32.MF_CHECKED;

            item.CommandId = FirstCommandId + i;
            // uIDNewItem (the command TrackPopupMenuEx's TPM_RETURNCMD hands back) and lpNewItem (the
            // owner-draw dwItemData that comes back through MEASUREITEMSTRUCT/DRAWITEMSTRUCT.itemData)
            // are separate parameters — the index goes in as item data purely so Measure/DrawItem can
            // look the row back up in _items.
            Win32.AppendMenuW(_hMenu, flags, (UIntPtr)(uint)item.CommandId, (IntPtr)i);
        }
    }

    private void ShowMenu()
    {
        Win32.GetCursorPos(out var cursor);

        // The classic tray-icon dance (documented by Raymond Chen and MSDN alike): the popup won't
        // dismiss itself on an outside click unless the owning window is briefly made foreground, and
        // a trailing no-op message avoids a second stuck click being needed afterward.
        Win32.SetForegroundWindow(_hwnd);
        var flags = Win32.TPM_RETURNCMD | Win32.TPM_NONOTIFY | Win32.TPM_LEFTALIGN | Win32.TPM_RIGHTBUTTON;
        var selectedId = Win32.TrackPopupMenuEx(_hMenu, flags, cursor.X, cursor.Y, _hwnd, IntPtr.Zero);
        Win32.PostMessageW(_hwnd, Win32.WM_NULL, IntPtr.Zero, IntPtr.Zero);

        if (selectedId == 0) return;
        foreach (var item in _items)
        {
            if (item.CommandId == selectedId)
            {
                item.OnClick?.Invoke();
                break;
            }
        }
    }

    private void MeasureItem(ref Win32.MEASUREITEMSTRUCT measureItem)
    {
        if (measureItem.CtlType != Win32.ODT_MENU) return;
        var item = ItemAt(measureItem.itemData);
        if (item is null) return;

        if (item.IsSeparator)
        {
            measureItem.itemHeight = MenuTheme.SeparatorHeight;
            measureItem.itemWidth = 120;
            return;
        }

        var hdc = Win32.GetDC(_hwnd);
        var previousFont = Win32.SelectObject(hdc, _font);
        Win32.GetTextExtentPoint32W(hdc, item.Text, item.Text.Length, out var size);
        Win32.SelectObject(hdc, previousFont);
        Win32.ReleaseDC(_hwnd, hdc);

        measureItem.itemHeight = MenuTheme.RowHeight;
        measureItem.itemWidth = (uint)(MenuTheme.CheckColumnWidth + size.cx + MenuTheme.HorizontalPadding);
    }

    private void DrawItem(in Win32.DRAWITEMSTRUCT drawItem)
    {
        if (drawItem.CtlType != Win32.ODT_MENU) return;
        var item = ItemAt(drawItem.itemData);
        if (item is null) return;

        var hdc = drawItem.hDC;
        var rect = drawItem.rcItem;
        var savedDc = Win32.SaveDC(hdc);
        try
        {
            var selected = !item.IsSeparator && item.IsEnabled && (drawItem.itemState & Win32.ODS_SELECTED) != 0;
            var backBrush = Win32.CreateSolidBrush(selected ? MenuTheme.RowHover : MenuTheme.Background);
            Win32.FillRect(hdc, ref rect, backBrush);
            Win32.DeleteObject(backBrush);

            if (item.IsSeparator)
            {
                DrawSeparatorLine(hdc, rect);
                return;
            }

            Win32.SelectObject(hdc, _font);
            Win32.SetBkMode(hdc, Win32.TRANSPARENT);
            Win32.SetTextColor(hdc, item.IsEnabled ? MenuTheme.Text : MenuTheme.DisabledText);

            if (item.IsChecked) DrawCheckmark(hdc, rect);

            var textRect = new Win32.RECT
            {
                Left = rect.Left + MenuTheme.CheckColumnWidth,
                Top = rect.Top,
                Right = rect.Right - MenuTheme.HorizontalPadding,
                Bottom = rect.Bottom,
            };
            Win32.DrawTextW(hdc, item.Text, item.Text.Length, ref textRect,
                Win32.DT_SINGLELINE | Win32.DT_VCENTER | Win32.DT_LEFT | Win32.DT_NOPREFIX);
        }
        finally
        {
            Win32.RestoreDC(hdc, savedDc);
        }
    }

    private static void DrawSeparatorLine(IntPtr hdc, Win32.RECT rect)
    {
        var pen = Win32.CreatePen(Win32.PS_SOLID, 1, MenuTheme.Separator);
        var previousPen = Win32.SelectObject(hdc, pen);
        var midY = (rect.Top + rect.Bottom) / 2;
        Win32.MoveToEx(hdc, rect.Left + MenuTheme.HorizontalPadding, midY, IntPtr.Zero);
        Win32.LineTo(hdc, rect.Right - MenuTheme.HorizontalPadding, midY);
        Win32.SelectObject(hdc, previousPen);
        Win32.DeleteObject(pen);
    }

    private static void DrawCheckmark(IntPtr hdc, Win32.RECT rect)
    {
        var pen = Win32.CreatePen(Win32.PS_SOLID, 2, MenuTheme.Accent);
        var previousPen = Win32.SelectObject(hdc, pen);
        var centerX = rect.Left + MenuTheme.CheckColumnWidth / 2;
        var centerY = (rect.Top + rect.Bottom) / 2;
        Win32.MoveToEx(hdc, centerX - 5, centerY, IntPtr.Zero);
        Win32.LineTo(hdc, centerX - 1, centerY + 4);
        Win32.LineTo(hdc, centerX + 6, centerY - 5);
        Win32.SelectObject(hdc, previousPen);
        Win32.DeleteObject(pen);
    }

    private MenuItemDescriptor? ItemAt(nuint itemData)
    {
        var index = (int)itemData;
        return index >= 0 && index < _items.Count ? _items[index] : null;
    }

    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _settingsStore.Save(_settings);
        PowerManager.Apply(_settings.Enabled);
        Rebuild();
    }

    private void ToggleStartWithWindows()
    {
        var enabled = !StartupRegistration.IsEnabled();
        StartupRegistration.SetEnabled(enabled);
        _settings.StartWithWindows = enabled;
        _settingsStore.Save(_settings);
        Rebuild();
    }

    /// <summary>Exit stops the effect immediately — SetThreadExecutionState(ES_CONTINUOUS) — so the
    /// machine is back to normal Windows sleep/screen behavior, but deliberately does not change
    /// AppSettings.Enabled: that's the user's last explicit on/off choice, restored on the next launch
    /// (Program.cs), exactly mirroring what was set when they quit.</summary>
    private void Exit()
    {
        PowerManager.Apply(enabled: false);

        var data = new Win32.NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            // ByValTStr-marshaled string fields can't be null even though NIM_DELETE ignores them.
            szTip = "",
            szInfo = "",
            szInfoTitle = "",
        };
        Win32.Shell_NotifyIconW(Win32.NIM_DELETE, ref data);

        Win32.DestroyWindow(_hwnd);
    }

    private void Cleanup()
    {
        if (_hMenu != IntPtr.Zero) Win32.DestroyMenu(_hMenu);
        if (_menuBackBrush != IntPtr.Zero) Win32.DeleteObject(_menuBackBrush);
        if (_font != IntPtr.Zero) Win32.DeleteObject(_font);
        MenuTheme.UnloadFont(_fontPath);
    }

    private sealed class MenuItemDescriptor
    {
        public string Text = "";
        public bool IsSeparator;
        public bool IsEnabled = true;
        public bool IsChecked;
        public int CommandId;
        public Action? OnClick;
    }
}
