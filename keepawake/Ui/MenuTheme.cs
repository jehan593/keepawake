using System;
using Keepawake.Native;

namespace Keepawake.Ui
{
    internal static class MenuTheme
    {
        public static readonly uint Background = Win32.Rgb(0x2E, 0x34, 0x40);
        public static readonly uint RowHover = Win32.Rgb(0x43, 0x4C, 0x5E);
        public static readonly uint Text = Win32.Rgb(0xEC, 0xEF, 0xF4);
        public static readonly uint DimText = Win32.Rgb(0xA0, 0xA9, 0xBA);
        public static readonly uint DisabledText = Win32.Rgb(0x4C, 0x56, 0x6A);
        public static readonly uint Separator = Win32.Rgb(0x4C, 0x56, 0x6A);
        public static readonly uint Accent = Win32.Rgb(0x81, 0xA1, 0xC1);

        public const string FontFamily = "MartianMono NF";
        public const int RowHeight = 26;
        public const int SeparatorHeight = 7;
        public const int CheckColumnWidth = 26;
        public const int HorizontalPadding = 12;

        public static IntPtr LoadFont(string ttfPath)
        {
            return CreateFont(ttfPath, Win32.FW_NORMAL);
        }

        private static IntPtr CreateFont(string ttfPath, int weight)
        {
            Win32.AddFontResourceExW(ttfPath, Win32.FR_PRIVATE, IntPtr.Zero);

            var logFont = new Win32.LOGFONTW
            {
                lfHeight = -14,
                lfWeight = weight,
                lfCharSet = Win32.DEFAULT_CHARSET,
                lfOutPrecision = Win32.OUT_TT_PRECIS,
                lfClipPrecision = Win32.CLIP_DEFAULT_PRECIS,
                lfQuality = Win32.CLEARTYPE_QUALITY,
                lfPitchAndFamily = Win32.DEFAULT_PITCH,
                lfFaceName = FontFamily,
            };
            return Win32.CreateFontIndirectW(ref logFont);
        }

        public static void UnloadFont(string ttfPath)
        {
            Win32.RemoveFontResourceExW(ttfPath, Win32.FR_PRIVATE, IntPtr.Zero);
        }
    }
}
