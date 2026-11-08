#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace XanaduYonder.Runtime
{
    internal static class IOSHaptics
    {
        [DllImport("__Internal", EntryPoint = "XanaduHaptics_Pulse")]
        private static extern void NativePulse();

        // Callers own event selection and the shared haptics preference.
        internal static void Pulse() { NativePulse(); }
    }
}
#endif
