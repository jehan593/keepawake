using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Keepawake.Data;
using Keepawake.Native;

namespace Keepawake.Ui
{
    internal sealed class TrayIcon
    {
        private const string WindowClassName = "KeepawakeTrayWindow";
        private const int FirstCommandId = 1001;

        private static TrayIcon _instance;

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
        private List<MenuItemDescriptor> _items = new List<MenuItemDescriptor>();
        private bool _iconAdded;

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private static readonly WndProcDelegate _wndProcDelegate = WndProc;

        public TrayIcon(AppSettings settings, SettingsStore settingsStore)
        {
            _settings = settings;
            _settingsStore = settingsStore;
            _instance = this;

            var hInstance = Win32.GetModuleHandleW(null);

            var windowClass = new Win32.WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf(typeof(Win32.WNDCLASSEXW)),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
                hInstance = hInstance,
                lpszClassName = WindowClassName,
            };
            Win32.RegisterClassExW(ref windowClass);

            _hwnd = Win32.CreateWindowExW(0, WindowClassName, "keepawake", 0, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

            var iconSize = Win32.GetSystemMetrics(Win32.SM_CXSMICON);
            _onIcon = Win32.LoadImageW(IntPtr.Zero, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app-on.ico"),
                Win32.IMAGE_ICON, iconSize, iconSize, Win32.LR_LOADFROMFILE);
            _offIcon = Win32.LoadImageW(IntPtr.Zero, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app-off.ico"),
                Win32.IMAGE_ICON, iconSize, iconSize, Win32.LR_LOADFROMFILE);

            _fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "martian_mono_regular.ttf");
            _font = MenuTheme.LoadFont(_fontPath);

            _taskbarCreatedMessage = Win32.RegisterWindowMessageW("TaskbarCreated");

            Rebuild();
        }

        private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            var instance = _instance;
            if (instance != null)
            {
                var result = instance.HandleMessage(hWnd, msg, wParam, lParam);
                if (result.HasValue) return result.Value;
            }
            return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
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
                var evt = (uint)(long)lParam;
                if (evt == Win32.WM_LBUTTONUP) ToggleEnabled();
                else if (evt == Win32.WM_RBUTTONUP) ShowMenu();
                return IntPtr.Zero;
            }

            if (msg == Win32.WM_MEASUREITEM)
            {
                var measureItem = (Win32.MEASUREITEMSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32.MEASUREITEMSTRUCT));
                MeasureItem(ref measureItem);
                Marshal.StructureToPtr(measureItem, lParam, false);
                return (IntPtr)1;
            }

            if (msg == Win32.WM_DRAWITEM)
            {
                var drawItem = (Win32.DRAWITEMSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32.DRAWITEMSTRUCT));
                DrawItem(ref drawItem);
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
                cbSize = (uint)Marshal.SizeOf(typeof(Win32.NOTIFYICONDATAW)),
                hWnd = _hwnd,
                uID = 1,
                uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
                uCallbackMessage = Win32.WM_TRAYICON,
                hIcon = _settings.Enabled ? _onIcon : _offIcon,
                szTip = _settings.Enabled ? "keepawake \u2014 Keeping screen on" : "keepawake \u2014 Screen will turn off",
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

            var backBrush = Win32.CreateSolidBrush(MenuTheme.Background);
            var menuInfo = new Win32.MENUINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(Win32.MENUINFO)),
                fMask = Win32.MIM_BACKGROUND,
                hbrBack = backBrush,
            };
            Win32.SetMenuInfo(_hMenu, ref menuInfo);
            if (_menuBackBrush != IntPtr.Zero) Win32.DeleteObject(_menuBackBrush);
            _menuBackBrush = backBrush;

            var statusText = _settings.Enabled ? "Keeping screen on" : "Screen will turn off";

            _items = new List<MenuItemDescriptor>
            {
                new MenuItemDescriptor { Text = statusText, IsLabel = true },
                new MenuItemDescriptor { IsSeparator = true },
                new MenuItemDescriptor { Text = "Keep screen on", IsChecked = _settings.Enabled, OnClick = ToggleEnabled },
                new MenuItemDescriptor { IsSeparator = true },
                new MenuItemDescriptor { Text = "Start with Windows", IsChecked = StartupRegistration.IsEnabled(), OnClick = ToggleStartWithWindows },
                new MenuItemDescriptor { IsSeparator = true },
                new MenuItemDescriptor { Text = "Exit", OnClick = Exit },
            };

            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var flags = Win32.MF_OWNERDRAW;
                if (item.IsSeparator) flags |= Win32.MF_SEPARATOR;
                if (!item.IsEnabled) flags |= Win32.MF_DISABLED | Win32.MF_GRAYED;
                if (item.IsChecked) flags |= Win32.MF_CHECKED;

                item.CommandId = FirstCommandId + i;
                Win32.AppendMenuW(_hMenu, flags, (UIntPtr)(uint)item.CommandId, (IntPtr)i);
            }
        }

        private void ShowMenu()
        {
            Win32.GetCursorPos(out var cursor);

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
            if (item == null) return;

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

        private void DrawItem(ref Win32.DRAWITEMSTRUCT drawItem)
        {
            if (drawItem.CtlType != Win32.ODT_MENU) return;
            var item = ItemAt(drawItem.itemData);
            if (item == null) return;

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
                Win32.SetTextColor(hdc, item.IsLabel ? MenuTheme.DimText : item.IsEnabled ? MenuTheme.Text : MenuTheme.DisabledText);

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

        private MenuItemDescriptor ItemAt(IntPtr itemData)
        {
            var index = (int)itemData.ToInt64();
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

        private void Exit()
        {
            PowerManager.Apply(false);

            var data = new Win32.NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf(typeof(Win32.NOTIFYICONDATAW)),
                hWnd = _hwnd,
                uID = 1,
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
            public bool IsLabel;
            public bool IsEnabled = true;
            public bool IsChecked;
            public int CommandId;
            public Action OnClick;
        }
    }
}
