using System;
using System.Security.Cryptography;
using System.Text;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>
    /// Helper class for password hashing and verification using SHA256 with salt
    /// </summary>
    public static class PasswordHelper
    {
        /// <summary>
        /// Generate a new random salt
        /// </summary>
        public static byte[] GenerateSalt()
        {
            byte[] salt = new byte[128 / 8]; // 128-bit salt
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return salt;
        }

        /// <summary>
        /// Hash a password with the provided salt
        /// </summary>
        public static byte[] HashPassword(string password, byte[] salt)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty", nameof(password));
            if (salt == null || salt.Length == 0)
                throw new ArgumentException("Salt cannot be empty", nameof(salt));

            using (var sha256 = SHA256.Create())
            {
                // Combine password and salt
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] combinedBytes = new byte[passwordBytes.Length + salt.Length];
                Buffer.BlockCopy(passwordBytes, 0, combinedBytes, 0, passwordBytes.Length);
                Buffer.BlockCopy(salt, 0, combinedBytes, passwordBytes.Length, salt.Length);

                // Hash the combined bytes
                return sha256.ComputeHash(combinedBytes);
            }
        }

        /// <summary>
        /// Verify a password against a stored hash and salt
        /// </summary>
        public static bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
        {
            if (string.IsNullOrWhiteSpace(password))
                return false;
            if (storedHash == null || storedHash.Length == 0)
                return false;
            if (storedSalt == null || storedSalt.Length == 0)
                return false;

            // Hash the provided password with the stored salt
            byte[] computedHash = HashPassword(password, storedSalt);

            // Compare hashes
            if (computedHash.Length != storedHash.Length)
                return false;

            for (int i = 0; i < computedHash.Length; i++)
            {
                if (computedHash[i] != storedHash[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Generate a temporary password (8 characters: letters and numbers)
        /// </summary>
        public static string GenerateTemporaryPassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            var random = new Random();
            var result = new char[8];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }

            return new string(result);
        }
    }
}
