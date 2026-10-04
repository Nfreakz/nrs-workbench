using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace NRS.Workbench.App.Services;

public static class MachineIdentityService
{
    public static string GetFingerprint()
    {
        string material;
        try
        {
            material = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
                "MachineGuid",
                null) as string ?? string.Empty;
        }
        catch
        {
            material = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(material))
            material = $"{Environment.MachineName}|{Environment.OSVersion.VersionString}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("NRSWorkbench|" + material));
        return Convert.ToHexString(hash)[..24];
    }
}
