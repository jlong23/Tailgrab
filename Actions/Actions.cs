using System.Threading;
using System.Runtime.InteropServices;
using BuildSoft.VRChat.Osc;
using BuildSoft.VRChat.Osc.Avatar;

namespace Tailgrab.Actions
{
    public interface IAction
    {
        void PerformAction();
    }   


    public class DelayAction : IAction
    {
        public int DelayMilliseconds { get; set; }

        public DelayAction(int delayMilliseconds)
        {
            DelayMilliseconds = delayMilliseconds;
        }

        public void PerformAction()
        {
            if( DelayMilliseconds <= 0 )
            {
                return;
            }

            Thread.Sleep(DelayMilliseconds);
        }
    }


    public class KeystrokesAction : IAction
    {
        [DllImport ("User32.dll")]
        public static extern IntPtr SetForegroundWindow (IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        public required string WindowTitle { get; set; }
        public required string Keys { get; set; }

        public void PerformAction()
        {
            if( WindowTitle == null || Keys == null )
            {
                return;
            }

            var processes = System.Diagnostics.Process.GetProcessesByName(WindowTitle);
            if (processes.Length > 0)
            {
                IntPtr handle = processes[0].MainWindowHandle;
                SetForegroundWindow(handle);
                SendUnicodeString(Keys);
            }
        }

        private void SendUnicodeString(string s)
        {
            foreach (var ch in s)
            {
                SendUnicodeChar(ch);
            }
        }

        private void SendUnicodeChar(char ch)
        {
            INPUT[] inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = 0;
            inputs[0].U.ki.wScan = (ushort)ch;
            inputs[0].U.ki.dwFlags = KEYEVENTF_UNICODE;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = System.IntPtr.Zero;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = 0;
            inputs[1].U.ki.wScan = (ushort)ch;
            inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            inputs[1].U.ki.time = 0;
            inputs[1].U.ki.dwExtraInfo = System.IntPtr.Zero;

            var result = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            // if result == 0 you can call Marshal.GetLastWin32Error() to inspect failure
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public System.IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public System.IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
    }



    public class OSCAction : IAction
    {

        private OscAvatarConfig? oscAvatarConfig = OscAvatarConfig.CreateAtCurrent();

        public string ParameterName { get; set; }

        public OscType Type { get; set; }

        public string Value { get; set; }

        public OSCAction(string parameterName, OscType type, string value)
        {
            ParameterName = parameterName;
            Type = type;
            Value = value;
        }  

        public void PerformAction()
        {
            var parameterName = ParameterName;
            var value = Value;
            if (string.IsNullOrEmpty(parameterName) || string.IsNullOrEmpty(value))
            {
                return;
            }

            switch (Type)
            {
                case OscType.Bool:
                    if (bool.TryParse(value, out bool boolValue))
                    {                        
                        OscParameter.SendValue(parameterName, boolValue);
                    }
                    break;
                case OscType.Int:
                    if (int.TryParse(value, out int intValue))
                    {
                        OscParameter.SendValue(parameterName, intValue);
                    }
                    break;
                case OscType.Float:
                    if (float.TryParse(value, out float floatValue))
                    {
                        OscParameter.SendValue(parameterName, floatValue);
                    }
                    break;
            }        
        }
    }
}
