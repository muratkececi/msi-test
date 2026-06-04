using System;
using System.Drawing;
using System.Windows.Forms;

namespace UninstallGuard
{
    /// <summary>
    /// A simple, code-built password entry window.
    /// Show() returns the ENTERED text (not a pass/fail), or null on cancel.
    /// </summary>
    internal static class PasswordPrompt
    {
        public static string Show(int attempt, int maxAttempts)
        {
            using (var form = new Form())
            using (var label = new Label())
            using (var textBox = new TextBox())
            using (var okButton = new Button())
            using (var cancelButton = new Button())
            {
                form.Text = "Administrator Password Required to Uninstall";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(380, 140);
                form.TopMost = true;

                label.Text = attempt == 1
                    ? "Enter the master password to uninstall this application:"
                    : $"Wrong password. Try again ({attempt}/{maxAttempts}):";
                label.SetBounds(15, 15, 350, 30);
                label.AutoSize = false;

                textBox.UseSystemPasswordChar = true;
                textBox.SetBounds(15, 50, 350, 25);

                okButton.Text = "OK";
                okButton.DialogResult = DialogResult.OK;
                okButton.SetBounds(190, 95, 80, 30);

                cancelButton.Text = "Cancel";
                cancelButton.DialogResult = DialogResult.Cancel;
                cancelButton.SetBounds(285, 95, 80, 30);

                form.Controls.Add(label);
                form.Controls.Add(textBox);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                // Make sure the window opens on top and focused
                // (otherwise it can hide behind the MSI window and be missed).
                form.ShowInTaskbar = true;
                form.Shown += (s, e) =>
                {
                    form.Activate();
                    form.BringToFront();
                    textBox.Focus();
                };

                DialogResult result = form.ShowDialog();
                if (result != DialogResult.OK)
                    return null;

                return textBox.Text ?? string.Empty;
            }
        }
    }
}
