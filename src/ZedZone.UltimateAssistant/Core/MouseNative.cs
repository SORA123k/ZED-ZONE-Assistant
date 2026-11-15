using System;
using System.Runtime.InteropServices;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>Win32 鼠标注入（连点器 / 自动开火共用）。</summary>
    internal static class MouseNative
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        private const uint LEFTDOWN = 0x0002;
        private const uint LEFTUP = 0x0004;

        internal static void LeftDown() => mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        internal static void LeftUp() => mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
        internal static void LeftClick()
        {
            LeftDown();
            LeftUp();
        }
    }
}
