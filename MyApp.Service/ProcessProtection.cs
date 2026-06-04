using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MyApp.Service;

/// <summary>
/// Protects the current process against being terminated from Task Manager's
/// "End task". Adds an ACE to the process DACL that DENIES PROCESS_TERMINATE to
/// "Interactive" users.
///
/// - Targets "Interactive" (S-1-5-4): a logged-on user/admin is blocked from
///   killing it via Task Manager.
/// - SYSTEM is not "interactive", so it is unaffected; SCM still manages the service.
/// - A clean SCM stop uses SERVICE_CONTROL_STOP and does not require
///   PROCESS_TERMINATE, so proper shutdown keeps working.
///
/// NOTE: This is a DETERRENT, not kernel-level protection. A determined admin can
/// revert the DACL and still terminate it.
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

        // 1) Build the "Interactive" SID (S-1-5-4).
        IntPtr sid = CreateInteractiveSid();
        try
        {
            // 2) Get the current DACL (to preserve the process's other permissions).
            uint err = GetSecurityInfo(handle, SE_KERNEL_OBJECT, DACL_SECURITY_INFORMATION,
                IntPtr.Zero, IntPtr.Zero, out IntPtr pOldDacl, IntPtr.Zero, out IntPtr pSD);
            if (err != 0)
                throw new Win32Exception((int)err, "GetSecurityInfo failed");

            try
            {
                // 3) Define a single ACE that denies PROCESS_TERMINATE.
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

                // 4) Build a new DACL by adding this ACE to the existing one.
                err = SetEntriesInAcl(1, ref ea, pOldDacl, out IntPtr pNewDacl);
                if (err != 0)
                    throw new Win32Exception((int)err, "SetEntriesInAcl failed");

                try
                {
                    // 5) Write the new DACL back to the process.
                    err = SetSecurityInfo(handle, SE_KERNEL_OBJECT, DACL_SECURITY_INFORMATION,
                        IntPtr.Zero, IntPtr.Zero, pNewDacl, IntPtr.Zero);
                    if (err != 0)
                        throw new Win32Exception((int)err, "SetSecurityInfo failed");
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
        // First call to learn the required size (returns false, ERROR_INSUFFICIENT_BUFFER).
        CreateWellKnownSid(WinInteractiveSid, IntPtr.Zero, IntPtr.Zero, ref cb);
        IntPtr sid = Marshal.AllocHGlobal((int)cb);
        if (!CreateWellKnownSid(WinInteractiveSid, IntPtr.Zero, sid, ref cb))
        {
            Marshal.FreeHGlobal(sid);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateWellKnownSid failed");
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
