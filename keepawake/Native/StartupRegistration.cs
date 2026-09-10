using System;
using Microsoft.Win32;

namespace Keepawake.Native
{
    public static class StartupRegistration
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "keepawake";

        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
            {
                return key != null && key.GetValue(ValueName) != null;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null)
            {
                key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            }

            using (key)
            {
                if (enabled)
                {
                    var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    key.SetValue(ValueName, "\"" + exePath + "\"");
                }
                else
                {
                    try { key.DeleteValue(ValueName); }
                    catch (ArgumentException) { }
                }
            }
        }
    }
}
