using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Tailgrab.Common
{
    public static class NativeOtpDialog
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const uint MB_OK = 0;
        private const uint MB_OKCANCEL = 1;
        private const uint MB_YESNO = 4;
        private const uint MB_ICONINFORMATION = 64;
        private const uint MB_ICONWARNING = 48;
        private const uint MB_TOPMOST = 0x00040000;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        public static string? PromptForOtpCode(string promptMessage)
        {
            var form = new System.Windows.Forms.Form
            {
                Text = "OTP Code Entry",
                Width = 350,
                Height = 180,
                StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen,
                TopMost = true,
                ControlBox = true,
                MinimizeBox = false,
                MaximizeBox = false,
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
                BackColor = System.Drawing.SystemColors.Control
            };

            var label = new System.Windows.Forms.Label
            {
                Text = promptMessage,
                Location = new System.Drawing.Point(15, 15),
                Width = 300,
                Height = 60,
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.TopLeft
            };

            var textBox = new System.Windows.Forms.TextBox
            {
                Location = new System.Drawing.Point(15, 75),
                Width = 300,
                Height = 30,
                MaxLength = 6,
                TextAlign = System.Windows.Forms.HorizontalAlignment.Center,
                Font = new System.Drawing.Font("Arial", 18)
            };

            textBox.KeyPress += (sender, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)System.Windows.Forms.Keys.Back)
                {
                    e.Handled = true;
                }
            };

            var okButton = new System.Windows.Forms.Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(150, 115),
                Width = 75,
                Height = 30,
                DialogResult = System.Windows.Forms.DialogResult.OK
            };

            var cancelButton = new System.Windows.Forms.Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(240, 115),
                Width = 75,
                Height = 30,
                DialogResult = System.Windows.Forms.DialogResult.Cancel
            };

            okButton.Click += (sender, e) =>
            {
                if (textBox.Text.Length == 6)
                {
                    form.DialogResult = System.Windows.Forms.DialogResult.OK;
                    form.Close();
                }
                else
                {
                    MessageBox(form.Handle, "Please enter exactly 6 digits.", "Invalid Input", MB_OK | MB_ICONWARNING | MB_TOPMOST);
                    textBox.Focus();
                }
            };

            form.Controls.Add(label);
            form.Controls.Add(textBox);
            form.Controls.Add(okButton);
            form.Controls.Add(cancelButton);
            form.AcceptButton = okButton;
            form.CancelButton = cancelButton;

            textBox.Focus();
            SetForegroundWindow(form.Handle);

            var result = form.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                return textBox.Text;
            }

            return null;
        }
    }
}
