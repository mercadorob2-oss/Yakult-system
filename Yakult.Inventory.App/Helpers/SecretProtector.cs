using System;
using System.Security.Cryptography;
using System.Text;

namespace Yakult.Inventory.App.Helpers
{
    public static class SecretProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Yakult.Inventory.App|SecretProtector|v1");

        public static byte[] ProtectString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return null;

            byte[] data = Encoding.UTF8.GetBytes(plainText);
            return ProtectedData.Protect(data, Entropy, DataProtectionScope.LocalMachine);
        }

        public static string UnprotectToString(byte[] protectedBytes)
        {
            if (protectedBytes == null || protectedBytes.Length == 0)
                return null;

            // Try LocalMachine first (new format), then fall back to CurrentUser (old format)
            try
            {
                byte[] data = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(data);
            }
            catch (CryptographicException)
            {
                // Fall back to CurrentUser for passwords saved before the LocalMachine change
                try
                {
                    byte[] data = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(data);
                }
                catch (CryptographicException)
                {
                    return null;
                }
            }
        }
    }
}
