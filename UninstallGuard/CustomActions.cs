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
        // Master parola: "Admin123!"  (SHA-256, BÜYÜK harf hex)
        // ÜRETİM ÖNERİSİ: Burada offline hash yerine bir API'ye doğrulama yapın.
        private const string MasterPasswordHash =
            "3EB3FE66B31E3B4D10FA70B5CAD49C7112294AF6AE4E476A1C405155D45AA121";

        private const int MaxAttempts = 3;

        private static Session _session;
        private static string _logFile;

        /// <summary>
        /// Uninstall sırasında çalışır. Doğru parola girilmezse kurulumu iptal eder.
        /// </summary>
        [CustomAction]
        public static ActionResult CheckUninstallPassword(Session session)
        {
            _session = session;

            // Log dosyasını kurulum klasörüne (Program Files\MyApp) yazmaya hazırlan.
            // INSTALLFOLDER kaldırma sırasında da çözümlenir.
            try
            {
                string installDir = session["INSTALLFOLDER"];
                if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                    _logFile = Path.Combine(installDir, "uninstall-guard.log");
            }
            catch { /* log dosyası ayarlanamazsa MSI log'una düşmeye devam ederiz */ }

            try
            {
                Log("CheckUninstallPassword başladı.");

                string uiLevel = session["UILevel"];
                Log("UILevel = " + uiLevel);

                // Sessiz mod (/qn) tespiti: UI yoksa parola soramayız.
                // INSTALLUILEVEL_NONE = 2. Güvenlik gereği UI yoksa engelliyoruz.
                if (uiLevel == "2")
                {
                    Log("Sessiz mod algılandı. Uninstall engellendi.");
                    return ActionResult.Failure;
                }

                for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    Log($"Parola ekranı gösteriliyor (deneme {attempt}).");

                    // WinForms penceresini AYRI bir STA thread'inde aç.
                    // MSI custom action thread'i STA olmayabilir; bu, pencerenin
                    // görünmeden çökmesini engeller.
                    string entered = ShowPromptOnStaThread(attempt);

                    if (entered == null)
                    {
                        Log("Kullanıcı parola ekranını iptal etti.");
                        return ActionResult.Failure;
                    }

                    if (Verify(entered))
                    {
                        Log("Parola doğru. Uninstall'a izin verildi.");
                        return ActionResult.Success;
                    }

                    Log($"Yanlış parola denemesi {attempt}/{MaxAttempts}.");
                }

                ShowMessageOnStaThread(
                    "Çok fazla hatalı deneme. Kaldırma işlemi iptal edildi.",
                    "Yetkisiz İşlem");

                Log("Maksimum deneme aşıldı. Uninstall engellendi.");
                return ActionResult.Failure;
            }
            catch (Exception ex)
            {
                Log("HATA -> " + ex);
                // Güvenlik gereği: parola kontrolü çökerse kaldırmayı ENGELLE.
                return ActionResult.Failure;
            }
        }

        // Hem MSI log'una hem de Program Files\MyApp\uninstall-guard.log'a yazar.
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
            catch { /* dosyaya yazılamazsa sessizce geç */ }
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
