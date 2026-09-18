using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Yakult.Inventory.App.Helpers
{
    internal static class AesEncryptionHelper
    {
        // 256-bit AES key — lives in the compiled binary, not in the DB or app.config.
        // A DB breach alone cannot decrypt PlainPassword without this key.
        private static readonly byte[] Key =
        {
            0x59, 0x61, 0x6B, 0x75, 0x6C, 0x74, 0x49, 0x6E,
            0x76, 0x32, 0x30, 0x32, 0x35, 0x21, 0x40, 0x23,
            0x44, 0x65, 0x70, 0x74, 0x41, 0x63, 0x63, 0x50,
            0x77, 0x64, 0x4B, 0x65, 0x79, 0x21, 0x76, 0x31
        };

        // Encrypts plainText to a Base64 string: Base64(random-IV + AES-ciphertext).
        // Returns null/empty unchanged.
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            using (var aes = Aes.Create())
            {
                aes.Key = Key;
                aes.GenerateIV();

                using (var ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs, Encoding.UTF8))
                        sw.Write(plainText);

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        // Decrypts a value produced by Encrypt. If the value is not valid ciphertext
        // (e.g. a legacy plaintext record), it is returned as-is.
        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            try
            {
                byte[] buffer = Convert.FromBase64String(cipherText);

                using (var aes = Aes.Create())
                {
                    aes.Key = Key;
                    int ivLen = aes.BlockSize / 8;
                    byte[] iv = new byte[ivLen];
                    Array.Copy(buffer, 0, iv, 0, ivLen);
                    aes.IV = iv;

                    using (var ms = new MemoryStream(buffer, ivLen, buffer.Length - ivLen))
                    using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs, Encoding.UTF8))
                        return sr.ReadToEnd();
                }
            }
            catch
            {
                return cipherText;
            }
        }
    }
}
