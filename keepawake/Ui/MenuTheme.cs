using Keepawake.Native;

namespace Keepawake.Ui;

/// <summary>
/// Same Nord "Dark" palette values the old Theme/Colors.axaml used, translated to GDI COLORREFs, plus
/// the Martian Mono font (regular weight only — the only one Styles.axaml ever actually applied to the
/// menu) loaded as a process-private font resource so it doesn't need installing system-wide.
/// </summary>
internal static class MenuTheme
{
    public static readonly uint Background = Win32.Rgb(0x2E, 0x34, 0x40); // Nord0 — BackgroundBrush
    public static readonly uint RowHover = Win32.Rgb(0x43, 0x4C, 0x5E); // Nord2 — RowHoverBrush
    public static readonly uint Text = Win32.Rgb(0xEC, 0xEF, 0xF4); // Nord6 — OnBackgroundBrush
    public static readonly uint DisabledText = Win32.Rgb(0x4C, 0x56, 0x6A); // Nord3 — OutlineBrush
    public static readonly uint Separator = Win32.Rgb(0x4C, 0x56, 0x6A); // Nord3 — OutlineBrush
    public static readonly uint Accent = Win32.Rgb(0x88, 0xC0, 0xD0); // Nord8 — same as app-on.ico's glyph

    public const string FontFamily = "MartianMono NF";
    public const int RowHeight = 26;
    public const int SeparatorHeight = 7;
    public const int CheckColumnWidth = 26;
    public const int HorizontalPadding = 12;

    /// <summary>Registers the ttf as FR_PRIVATE (visible to this process only, auto-cleaned up by
    /// Windows if UnloadFont is never reached) and returns an HFONT built against it.</summary>
    public static IntPtr LoadFont(string ttfPath)
    {
        Win32.AddFontResourceExW(ttfPath, Win32.FR_PRIVATE, IntPtr.Zero);

        var logFont = new Win32.LOGFONTW
        {
            lfHeight = -14,
            lfWeight = Win32.FW_NORMAL,
            lfCharSet = Win32.DEFAULT_CHARSET,
            lfOutPrecision = Win32.OUT_TT_PRECIS,
            lfClipPrecision = Win32.CLIP_DEFAULT_PRECIS,
            lfQuality = Win32.CLEARTYPE_QUALITY,
            lfPitchAndFamily = Win32.DEFAULT_PITCH,
            lfFaceName = FontFamily,
        };
        return Win32.CreateFontIndirectW(ref logFont);
    }

    public static void UnloadFont(string ttfPath) =>
        Win32.RemoveFontResourceExW(ttfPath, Win32.FR_PRIVATE, IntPtr.Zero);
}
