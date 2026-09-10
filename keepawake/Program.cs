using System;
using System.Threading;
using Keepawake.Data;
using Keepawake.Native;
using Keepawake.Ui;

namespace Keepawake
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = "Local\\keepawake-tray-single-instance";

        [STAThread]
        public static int Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, SingleInstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    return 0;
                }

                var settingsStore = new SettingsStore();
                var settings = settingsStore.Load();

                PowerManager.Apply(settings.Enabled);

                _ = new TrayIcon(settings, settingsStore);

                Win32.MSG msg;
                while (Win32.GetMessageW(out msg, IntPtr.Zero, 0, 0) > 0)
                {
                    Win32.TranslateMessage(ref msg);
                    Win32.DispatchMessageW(ref msg);
                }
            }

            return 0;
        }
    }
}
