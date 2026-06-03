using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using WixToolset.Dtf.WindowsInstaller;

namespace UninstallGuard
{
    public class CustomActions
    {
        // Master parola: "Admin123!"  (SHA-256, BÜYÜK harf hex)
        // ÜRETİM ÖNERİSİ: Burada offline hash yerine bir API'ye doğrulama yapın.
        // Örn: ValidateViaApi(password) -> HttpClient ile sunucudan true/false alın.
        private const string MasterPasswordHash =
            "3EB3FE66B31E3B4D10FA70B5CAD49C7112294AF6AE4E476A1C405155D45AA121";

        private const int MaxAttempts = 3;

        /// <summary>
        /// Uninstall sırasında çalışır. Doğru parola girilmezse kurulumu iptal eder.
        /// </summary>
        [CustomAction]
        public static ActionResult CheckUninstallPassword(Session session)
        {
            session.Log("UninstallGuard: CheckUninstallPassword başladı.");

            // Sessiz mod (/qn) tespiti: UI yoksa parola soramayız.
            // Güvenlik gereği: UI yoksa uninstall'a İZİN VERMİYORUZ (engelle).
            // İsterseniz burada davranışı tersine çevirip izin verebilirsiniz.
            if (session["UILevel"] == "2" /* INSTALLUILEVEL_NONE */)
            {
                session.Log("UninstallGuard: Sessiz mod algılandı. Uninstall engellendi.");
                return ActionResult.Failure;
            }

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                string entered = PasswordPrompt.Show(attempt, MaxAttempts);

                if (entered == null)
                {
                    // Kullanıcı İptal'e bastı -> uninstall'u durdur
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

            MessageBox.Show(
                "Çok fazla hatalı deneme. Kaldırma işlemi iptal edildi.",
                "Yetkisiz İşlem",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            session.Log("UninstallGuard: Maksimum deneme aşıldı. Uninstall engellendi.");
            return ActionResult.Failure;
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
