using System.Security.Cryptography;
using System.Text;

namespace Yakult.ITCM.Server.Data;

public static class SecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Yakult.Inventory.App|SecretProtector|v1");

    public static byte[]? ProtectString(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return null;

        var data = Encoding.UTF8.GetBytes(plainText);
        return ProtectedData.Protect(data, Entropy, DataProtectionScope.LocalMachine);
    }

    public static string? UnprotectToString(byte[]? protectedBytes)
    {
        if (protectedBytes is null || protectedBytes.Length == 0)
            return null;

        try
        {
            var data = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(data);
        }
        catch (CryptographicException)
        {
            try
            {
                var data = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch (CryptographicException)
            {
                return null;
            }
        }
    }
}
