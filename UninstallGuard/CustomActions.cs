using System;
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

        /// <summary>
        /// Uninstall sırasında çalışır. Doğru parola girilmezse kurulumu iptal eder.
        /// </summary>
        [CustomAction]
        public static ActionResult CheckUninstallPassword(Session session)
        {
            try
            {
                session.Log("UninstallGuard: CheckUninstallPassword başladı.");

                string uiLevel = session["UILevel"];
                session.Log("UninstallGuard: UILevel = " + uiLevel);

                // Sessiz mod (/qn) tespiti: UI yoksa parola soramayız.
                // INSTALLUILEVEL_NONE = 2. Güvenlik gereği UI yoksa engelliyoruz.
                if (uiLevel == "2")
                {
                    session.Log("UninstallGuard: Sessiz mod algılandı. Uninstall engellendi.");
                    return ActionResult.Failure;
                }

                for (int attempt = 1; attempt <= MaxAttempts; attempt++)
                {
                    session.Log($"UninstallGuard: Parola ekranı gösteriliyor (deneme {attempt}).");

                    // WinForms penceresini AYRI bir STA thread'inde aç.
                    // MSI custom action thread'i STA olmayabilir; bu, pencerenin
                    // görünmeden çökmesini (senin gördüğün "açılıp kapanma") engeller.
                    string entered = ShowPromptOnStaThread(attempt);

                    if (entered == null)
                    {
                        session.Log("UninstallGuard: Kullanıcı parola ekranını iptal etti.");
                        return ActionResult.Failure;
                    }

                    if (Verify(entered))
                    {
                        session.Log("UninstallGuard: Parola doğru. Uninstall'a izin verildi.");
                        return ActionResult.Success;
                    }

                    session.Log($"UninstallGuard: Yanlış parola denemesi {attempt}/{MaxAttempts}.");
                }

                ShowMessageOnStaThread(
                    "Çok fazla hatalı deneme. Kaldırma işlemi iptal edildi.",
                    "Yetkisiz İşlem");

                session.Log("UninstallGuard: Maksimum deneme aşıldı. Uninstall engellendi.");
                return ActionResult.Failure;
            }
            catch (Exception ex)
            {
                // Hiçbir exception sessizce yutulmasın; loga tam dökülsün.
                session.Log("UninstallGuard: HATA -> " + ex);
                // Güvenlik gereği: parola kontrolü çökerse kaldırmayı ENGELLE.
                return ActionResult.Failure;
            }
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
