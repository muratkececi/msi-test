using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WixToolset.Dtf.WindowsInstaller;

namespace UninstallGuard
{
    public class CustomActions
    {
        // Master password: "Admin123!"  (SHA-256, UPPERCASE hex)
        // PRODUCTION TIP: validate against an API instead of this offline hash.
        private const string MasterPasswordHash =
            "3EB3FE66B31E3B4D10FA70B5CAD49C7112294AF6AE4E476A1C405155D45AA121";

        private const int MaxAttempts = 3;

        private static Session _session;
        private static string _logFile;

        /// <summary>
        /// Runs during uninstall. Cancels the operation unless the correct password is entered.
        /// </summary>
        [CustomAction]
        public static ActionResult CheckUninstallPassword(Session session)
        {
            _session = session;

            // Prepare to write the log file into the install folder (Program Files\MyApp).
            // INSTALLFOLDER is resolved during uninstall too.
            try
            {
                string installDir = session["INSTALLFOLDER"];
                if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                    _logFile = Path.Combine(installDir, "uninstall-guard.log");
            }
            catch { /* if the log file can't be set, we still fall back to the MSI log */ }

            try
            {
                Log("CheckUninstallPassword started.");

                string uiLevel = session["UILevel"];
                Log("UILevel = " + uiLevel);

                // Silent mode (/qn) detection: with no UI we can't prompt for a password.
                // INSTALLUILEVEL_NONE = 2. For safety we block when there is no UI.
                if (uiLevel == "2")
                {
                    Log("Silent mode detected. Uninstall blocked.");
                    return ActionResult.Failure;
                }

                for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    Log($"Showing password prompt (attempt {attempt}).");

                    // Open the WinForms window on a SEPARATE STA thread. The MSI
                    // custom action thread may not be STA; this prevents the window
                    // from crashing without ever appearing.
                    string entered = ShowPromptOnStaThread(attempt);

                    if (entered == null)
                    {
                        Log("User cancelled the password prompt.");
                        return ActionResult.Failure;
                    }

                    if (Verify(entered))
                    {
                        Log("Correct password. Uninstall allowed.");
                        return ActionResult.Success;
                    }

                    Log($"Wrong password attempt {attempt}/{MaxAttempts}.");
                }

                ShowMessageOnStaThread(
                    "Too many failed attempts. Uninstall has been cancelled.",
                    "Unauthorized");

                Log("Maximum attempts exceeded. Uninstall blocked.");
                return ActionResult.Failure;
            }
            catch (Exception ex)
            {
                Log("ERROR -> " + ex);
                // For safety: if the password check crashes, BLOCK the uninstall.
                return ActionResult.Failure;
            }
        }

        // Writes to both the MSI log and Program Files\MyApp\uninstall-guard.log.
        private static void Log(string message)
        {
            try { _session?.Log("UninstallGuard: " + message); } catch { }

            if (string.IsNullOrEmpty(_logFile))
                return;

            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}";
                File.AppendAllText(_logFile, line, Encoding.UTF8);
            }
            catch { /* if the file can't be written, fail silently */ }
        }

        private static string ShowPromptOnStaThread(int attempt)
        {
            string result = null;
            Exception threadEx = null;

            var t = new Thread(() =>
            {
                try
                {
                    result = PasswordPrompt.Show(attempt, MaxAttempts);
                }
                catch (Exception ex)
                {
                    threadEx = ex;
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();

            if (threadEx != null)
                throw threadEx;
            return result;
        }

        private static void ShowMessageOnStaThread(string text, string caption)
        {
            var t = new Thread(() =>
                MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error));
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
        }

        private static bool Verify(string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                    sb.Append(b.ToString("X2"));
                return string.Equals(sb.ToString(), MasterPasswordHash,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
