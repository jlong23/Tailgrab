using BuildSoft.VRChat.Osc;
using BuildSoft.VRChat.Osc.Avatar;
using NLog;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;


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

            try
            {
                var procs = Process.GetProcessesByName(WindowTitle);
                if (procs == null || procs.Length == 0)
                {
                    logger.Warn($"KeystrokesAction: Process: '{WindowTitle}' not found.");
                    return;
                }

                var proc = procs[0];
                IntPtr hWnd = proc.MainWindowHandle;
                if (hWnd == IntPtr.Zero)
                {
                    hWnd = FindMainWindowHandleForProcess(proc.Id);
                }

                if (hWnd == IntPtr.Zero)
                {
                    logger.Warn($"KeystrokesAction: Process: '{WindowTitle}' Could not find a main window for the process.");
                    return;
                }

                if (!BringWindowToForeground(hWnd))
                {
                    logger.Warn($"KeystrokesAction: Process: '{WindowTitle}' Could not bring the window to the foreground.");
                    return;
                }

                // Give the system a moment to focus the window
                Thread.Sleep(500);


                // Use SendInput with unicode characters for reliable keystroke delivery
                System.Windows.Forms.SendKeys.SendWait(Keys);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "KeystrokesAction: Error while attempting to find target process/window");
            }
        }

        private static IntPtr FindMainWindowHandleForProcess(int pid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                    return true;

                GetWindowThreadProcessId(hWnd, out uint windowPid);
                if ((int)windowPid != pid)
                    return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0)
                    return true;

                // first visible window with text for the process
                found = hWnd;
                return false; // stop enumeration
            }, IntPtr.Zero);

            return found;
        }

        private static bool BringWindowToForeground(IntPtr hWnd)
        {
            const int SW_RESTORE = 9;

            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            IntPtr foreground = GetForegroundWindow();
            uint foregroundThread = GetWindowThreadProcessId(foreground, out _);
            uint targetThread = GetWindowThreadProcessId(hWnd, out _);
            uint currentThread = GetCurrentThreadId();

            // Attach input threads so SetForegroundWindow works reliably
            bool attached = false;
            if (currentThread != targetThread)
            {
                attached = AttachThreadInput(currentThread, targetThread, true);
            }

            bool result = SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);
            SetFocus(hWnd);

            if (attached)
            {
                AttachThreadInput(currentThread, targetThread, false);
            }

            return result;
        }

        #region PInvoke

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            // we only need keyboard input for this use
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion
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

    public class TTSAction : IAction
    {
        public Logger logger = LogManager.GetCurrentClassLogger();

        public int Volume{ get; set; } = 100;

        public int Rate { get; set; } = 0;

        public string Text { get; set; }

        public TTSAction(string text, int volume, int rate)
        {
            Text = text;
            Volume = volume;
            Rate = rate;

            logger.Warn($"Added TTSAction: Parameter: '{Text}'; Volume: {Volume}; Rate: {Rate}.");

        }

        public void PerformAction()
        {
            if (string.IsNullOrEmpty(Text))
            {
                return;
            }
            // Create an instance of the synthesizer
            //SpeechSynthesizer synthesizer = new SpeechSynthesizer();

            // Configure the synthesizer (optional)
            //synthesizer.Volume = Volume;
            //synthesizer.Rate = Rate;

            // Convert text to speech
            //synthesizer.Speak(Text);
            logger.Info($"TTSAction: (Simulated) Speaking Text: '{Text}' with Volume: {Volume} and Rate: {Rate}.");

        }
    }
}
