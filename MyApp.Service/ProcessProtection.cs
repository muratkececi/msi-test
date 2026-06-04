using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MyApp.Service;

/// <summary>
/// Geçerli süreci Task Manager'dan "End task" ile sonlandırmaya karşı korur.
/// Sürecin DACL'ine "Interactive" kullanıcılar için PROCESS_TERMINATE'i REDDEDEN
/// bir ACE ekler.
///
/// - "Interactive" (S-1-5-4) hedeflenir: oturum açmış kullanıcı/admin'in Task
///   Manager'dan öldürmesi engellenir.
/// - SYSTEM "interactive" olmadığı için etkilenmez; SCM service'i yine yönetir.
/// - SCM düzgün durdurma SERVICE_CONTROL_STOP ile yapılır ve PROCESS_TERMINATE
///   gerektirmez; bu yüzden temiz durdurma çalışmaya devam eder.
///
/// NOT: Bu bir CAYDIRICI korumadır, kernel düzeyi değildir. Kararlı bir admin
/// DACL'i geri değiştirip yine de sonlandırabilir.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ProcessProtection
{
    private const uint PROCESS_TERMINATE = 0x0001;

    // SE_KERNEL_OBJECT
    private const int SE_KERNEL_OBJECT = 6;
    // DACL_SECURITY_INFORMATION
    private const uint DACL_SECURITY_INFORMATION = 0x00000004;

    private const uint DENY_ACCESS = 3;        // ACCESS_MODE.DENY_ACCESS
    private const uint NO_INHERITANCE = 0x0;
    private const uint TRUSTEE_IS_SID = 0;     // TRUSTEE_FORM.TRUSTEE_IS_SID
    private const uint TRUSTEE_IS_WELL_KNOWN_GROUP = 5;

    // WELL_KNOWN_SID_TYPE.WinInteractiveSid = 8
    private const int WinInteractiveSid = 8;

    public static void DenyTerminate()
    {
        IntPtr handle = Process.GetCurrentProcess().Handle;

        // 1) "Interactive" SID'ini oluştur (S-1-5-4).
        IntPtr sid = CreateInteractiveSid();
        try
        {
            // 2) Mevcut DACL'i al (sürecin diğer izinlerini korumak için).
            uint err = GetSecurityInfo(handle, SE_KERNEL_OBJECT, DACL_SECURITY_INFORMATION,
                IntPtr.Zero, IntPtr.Zero, out IntPtr pOldDacl, IntPtr.Zero, out IntPtr pSD);
            if (err != 0)
                throw new Win32Exception((int)err, "GetSecurityInfo başarısız");

            try
            {
                // 3) PROCESS_TERMINATE'i reddeden tek bir ACE tanımla.
                var ea = new EXPLICIT_ACCESS
                {
                    grfAccessPermissions = PROCESS_TERMINATE,
                    grfAccessMode = DENY_ACCESS,
                    grfInheritance = NO_INHERITANCE,
                    Trustee = new TRUSTEE
                    {
                        pMultipleTrustee = IntPtr.Zero,
                        MultipleTrusteeOperation = 0,
                        TrusteeForm = TRUSTEE_IS_SID,
                        TrusteeType = TRUSTEE_IS_WELL_KNOWN_GROUP,
                        ptstrName = sid
                    }
                };

                // 4) Mevcut DACL'e bu ACE'i ekleyerek yeni DACL üret.
                err = SetEntriesInAcl(1, ref ea, pOldDacl, out IntPtr pNewDacl);
                if (err != 0)
                    throw new Win32Exception((int)err, "SetEntriesInAcl başarısız");

                try
                {
                    // 5) Yeni DACL'i sürece geri yaz.
                    err = SetSecurityInfo(handle, SE_KERNEL_OBJECT, DACL_SECURITY_INFORMATION,
                        IntPtr.Zero, IntPtr.Zero, pNewDacl, IntPtr.Zero);
                    if (err != 0)
                        throw new Win32Exception((int)err, "SetSecurityInfo başarısız");
                }
                finally
                {
                    if (pNewDacl != IntPtr.Zero) LocalFree(pNewDacl);
                }
            }
            finally
            {
                if (pSD != IntPtr.Zero) LocalFree(pSD);
            }
        }
        finally
        {
            if (sid != IntPtr.Zero) FreeSid(sid);
        }
    }

    private static IntPtr CreateInteractiveSid()
    {
        uint cb = 0;
        // İlk çağrı boyutu öğrenmek için (false döner, ERROR_INSUFFICIENT_BUFFER).
        CreateWellKnownSid(WinInteractiveSid, IntPtr.Zero, IntPtr.Zero, ref cb);
        IntPtr sid = Marshal.AllocHGlobal((int)cb);
        if (!CreateWellKnownSid(WinInteractiveSid, IntPtr.Zero, sid, ref cb))
        {
            Marshal.FreeHGlobal(sid);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWellKnownSid başarısız");
        }
        return sid;
    }

    // ----- P/Invoke -----

    [StructLayout(LayoutKind.Sequential)]
    private struct TRUSTEE
    {
        public IntPtr pMultipleTrustee;
        public uint MultipleTrusteeOperation;
        public uint TrusteeForm;
        public uint TrusteeType;
        public IntPtr ptstrName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EXPLICIT_ACCESS
    {
        public uint grfAccessPermissions;
        public uint grfAccessMode;
        public uint grfInheritance;
        public TRUSTEE Trustee;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint GetSecurityInfo(
        IntPtr handle, int objectType, uint securityInfo,
        IntPtr ppsidOwner, IntPtr ppsidGroup,
        out IntPtr ppDacl, IntPtr ppSacl, out IntPtr ppSecurityDescriptor);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint SetSecurityInfo(
        IntPtr handle, int objectType, uint securityInfo,
        IntPtr psidOwner, IntPtr psidGroup, IntPtr pDacl, IntPtr pSacl);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint SetEntriesInAcl(
        uint cCountOfExplicitEntries, ref EXPLICIT_ACCESS pListOfExplicitEntries,
        IntPtr OldAcl, out IntPtr NewAcl);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateWellKnownSid(
        int wellKnownSidType, IntPtr domainSid, IntPtr pSid, ref uint cbSid);

    [DllImport("advapi32.dll")]
    private static extern IntPtr FreeSid(IntPtr pSid);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
