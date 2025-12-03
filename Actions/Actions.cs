using System.Runtime.InteropServices;
using BuildSoft.VRChat.Osc;
using BuildSoft.VRChat.Osc.Avatar;
using NLog;

namespace Tailgrab.Actions
{
    public interface IAction
    {
        void PerformAction();
    }   


    public class DelayAction : IAction
    {
        public int DelayMilliseconds { get; set; }
        public Logger logger = LogManager.GetCurrentClassLogger();

        public DelayAction(int delayMilliseconds)
        {
            DelayMilliseconds = delayMilliseconds;
            logger.Warn($"Added DelayAction: Will delay for : '{DelayMilliseconds}' milliseconds."); 

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

        // Additional native methods for reliably setting foreground focus
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const int SW_RESTORE = 9;

        public Logger logger = LogManager.GetCurrentClassLogger();

        public string WindowTitle { get; set; }
        public string Keys { get; set; }

        public KeystrokesAction(string windowTitle, string keys)
        {
            WindowTitle = windowTitle;
            Keys = keys;

            logger.Warn($"Added KeystrokesAction: Window Title: '{WindowTitle}' with Keys: {Keys}."); 
        }

        public void PerformAction()
        {
            if( WindowTitle == null || Keys == null )
            {
                logger.Warn($"KeystrokesAction: Window Title: '{WindowTitle}' or Keys: {Keys} not supplied."); 
                return;
            }

            System.Diagnostics.Process? targetProcess = null;

            try
            {
                var all = System.Diagnostics.Process.GetProcesses();
                foreach (var p in all)
                {
                    try
                    {
                        if (p.MainWindowHandle == System.IntPtr.Zero) continue;
                        var title = p.MainWindowTitle;
                        if (string.IsNullOrEmpty(title)) continue;
                        if (title.IndexOf(WindowTitle, StringComparison.CurrentCultureIgnoreCase) >= 0)
                        {
                            targetProcess = p;
                            break;
                        }
                    }
                    catch
                    {
                        // Access denied or process exited -- ignore and continue
                    }
                }

                // Fallback: try by process name if no title match
                if (targetProcess == null)
                {
                    var byName = System.Diagnostics.Process.GetProcessesByName(WindowTitle);
                    if (byName.Length > 0) targetProcess = byName[0];
                }

                if (targetProcess != null)
                {
                    IntPtr handle = targetProcess.MainWindowHandle;

                    logger.Debug($"KeystrokesAction: Sending to process '{targetProcess.ProcessName}' (PID {targetProcess.Id}) Title: '{targetProcess.MainWindowTitle}' Keys: {Keys}."); 

                    // Attempt to reliably bring the target window to the foreground
                    uint targetThread = GetWindowThreadProcessId(handle, out _);
                    uint currentThread = GetCurrentThreadId();
                    bool attached = false;

                    try
                    {
                        // Attach input threads so SetForegroundWindow works reliably
                        attached = AttachThreadInput(currentThread, targetThread, true);

                        ShowWindow(handle, SW_RESTORE);
                        BringWindowToTop(handle);
                        SetForegroundWindow(handle);
                        SetFocus(handle);

                        // Use SendInput with unicode characters for reliable keystroke delivery
                        SendUnicodeString(Keys);
                    }
                    finally
                    {
                        if (attached)
                        {
                            AttachThreadInput(currentThread, targetThread, false);
                        }
                    }
                }
                else
                {
                    logger.Warn($"KeystrokesAction: Window with title containing '{WindowTitle}' not found."); 
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "KeystrokesAction: Error while attempting to find target process/window");
            }
        }

        private void SendUnicodeString(string s)
        {
            foreach (var ch in s)
            {
                SendUnicodeChar(ch);
            }
        }

        // New: parse SendKeys-style notation and send via SendInput.
        // Supports modifiers '^' (Ctrl), '%' (Alt), '+' (Shift), grouping with '()' and braced keys like '{ENTER}', '{F1}', etc.
        private void SendKeysNotation(string keys)
        {
            if (string.IsNullOrEmpty(keys)) return;

            for (int i = 0; i < keys.Length; i++)
            {
                char c = keys[i];

                // Handle modifiers prefixing a token
                if (c == '^' || c == '%' || c == '+')
                {
                    var mods = new List<ushort>();
                    // collect consecutive modifier symbols
                    while (i < keys.Length && (keys[i] == '^' || keys[i] == '%' || keys[i] == '+'))
                    {
                        if (keys[i] == '^') mods.Add(0x11); // VK_CONTROL
                        if (keys[i] == '%') mods.Add(0x12); // VK_MENU (Alt)
                        if (keys[i] == '+') mods.Add(0x10); // VK_SHIFT
                        i++;
                    }

                    if (i >= keys.Length) break;

                    // Determine the token: group '(... )', braced '{...}', or single char
                    if (keys[i] == '(')
                    {
                        int start = i + 1;
                        int end = keys.IndexOf(')', start);
                        if (end == -1) end = keys.Length - 1;
                        string group = keys.Substring(start, end - start);
                        foreach (var ch in group)
                        {
                            SendCharWithModifiers(ch, mods);
                        }
                        i = end;
                    }
                    else if (keys[i] == '{')
                    {
                        int start = i + 1;
                        int end = keys.IndexOf('}', start);
                        if (end == -1) end = keys.Length - 1;
                        string token = keys.Substring(start, end - start);
                        SendTokenWithModifiers(token, mods);
                        i = end;
                    }
                    else
                    {
                        SendCharWithModifiers(keys[i], mods);
                    }

                    continue;
                }

                // Braced token or single character
                if (c == '{')
                {
                    int start = i + 1;
                    int end = keys.IndexOf('}', start);
                    if (end == -1) end = keys.Length - 1;
                    string token = keys.Substring(start, end - start);
                    SendTokenWithModifiers(token, new List<ushort>());
                    i = end;
                }
                else
                {
                    // Regular char
                    SendUnicodeChar(c);
                }
            }
        }

        private void SendCharWithModifiers(char ch, List<ushort> mods)
        {
            // press mods
            foreach (var m in mods)
            {
                SendVirtualKeyDown(m);
            }

            // send character
            SendUnicodeChar(ch);

            // release mods in reverse order
            for (int j = mods.Count - 1; j >= 0; j--)
            {
                SendVirtualKeyUp(mods[j]);
            }
        }

        private void SendTokenWithModifiers(string token, List<ushort> mods)
        {
            // Normalize token
            var t = token.ToUpperInvariant();

            // Mapping of common SendKeys tokens to virtual-key codes
            Dictionary<string, ushort> map = new Dictionary<string, ushort>
            {
                {"ENTER", 0x0D},
                {"TAB", 0x09},
                {"BACKSPACE", 0x08},
                {"BS", 0x08},
                {"BKSP", 0x08},
                {"ESC", 0x1B},
                {"LEFT", 0x25},
                {"UP", 0x26},
                {"RIGHT", 0x27},
                {"DOWN", 0x28},
                {"HOME", 0x24},
                {"END", 0x23},
                {"PGUP", 0x21},
                {"PRIOR", 0x21},
                {"PGDN", 0x22},
                {"NEXT", 0x22},
                {"INSERT", 0x2D},
                {"DELETE", 0x2E},
                {"DEL", 0x2E},
                {"SPACE", 0x20},
                {"F1", 0x70}, {"F2", 0x71}, {"F3", 0x72}, {"F4", 0x73}, {"F5", 0x74},
                {"F6", 0x75}, {"F7", 0x76}, {"F8", 0x77}, {"F9", 0x78}, {"F10", 0x79},
                {"F11", 0x7A}, {"F12", 0x7B},
                {"LWIN", 0x5B}, {"RWIN", 0x5C}, {"APPS", 0x5D}
            };

            if (map.TryGetValue(t, out ushort vk))
            {
                // press mods
                foreach (var m in mods) SendVirtualKeyDown(m);

                // send vk
                SendVirtualKey(vk);

                // release mods
                for (int j = mods.Count - 1; j >= 0; j--) SendVirtualKeyUp(mods[j]);
            }
            else
            {
                // If token length == 1, send that character
                if (token.Length == 1)
                {
                    SendCharWithModifiers(token[0], mods);
                }
                else
                {
                    // For unknown tokens, attempt to send the text literally
                    foreach (var ch in token)
                    {
                        SendCharWithModifiers(ch, mods);
                    }
                }
            }
        }

        private void SendVirtualKeyDown(ushort vk)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = vk;
            inputs[0].U.ki.wScan = 0;
            inputs[0].U.ki.dwFlags = 0;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = System.IntPtr.Zero;
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private void SendVirtualKeyUp(ushort vk)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = vk;
            inputs[0].U.ki.wScan = 0;
            inputs[0].U.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[0].U.ki.time = 0;
            inputs[0].U.ki.dwExtraInfo = System.IntPtr.Zero;
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private void SendVirtualKey(ushort vk)
        {
            SendVirtualKeyDown(vk);
            SendVirtualKeyUp(vk);
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
        public Logger logger = LogManager.GetCurrentClassLogger();

        private OscAvatarConfig? oscAvatarConfig = OscAvatarConfig.CreateAtCurrent();

        public string ParameterName { get; set; }

        public OscType OscTypeValue { get; set; }

        public string Value { get; set; }

        public OSCAction(string parameterName, OscType type, string value)
        {
            ParameterName = parameterName;
            OscTypeValue = type;
            Value = value;

            logger.Warn($"Added OSCAction: Parameter: '{ParameterName}'; Type: {OscTypeValue}; Value: {Value}."); 

        }  

        public void PerformAction()
        {
            var parameterName = ParameterName;
            var value = Value;
            if (string.IsNullOrEmpty(parameterName) || string.IsNullOrEmpty(value))
            {
                return;
            }

            switch (OscTypeValue)
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
