using System.Runtime.InteropServices;

namespace Keepawake.Native
{
    public static class PowerManager
    {
        private const uint EsContinuous = 0x80000000;
        private const uint EsSystemRequired = 0x00000001;
        private const uint EsDisplayRequired = 0x00000002;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        public static void Apply(bool enabled)
        {
            var flags = EsContinuous;
            if (enabled)
            {
                flags |= EsSystemRequired | EsDisplayRequired;
            }

            SetThreadExecutionState(flags);
        }
    }
}
