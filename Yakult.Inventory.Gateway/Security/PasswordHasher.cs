using System.Security.Cryptography;
using System.Text;

namespace Yakult.Inventory.Gateway.Security;

/// <summary>
/// Byte-for-byte the same SHA256(password + salt) scheme as the desktop
/// Helpers\PasswordHelper, so hashes written by either side verify on both.
/// </summary>
public static class PasswordHasher
{
    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(128 / 8);

    public static byte[] Hash(string password, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var combined = new byte[passwordBytes.Length + salt.Length];
        Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
        Buffer.BlockCopy(salt, 0, combined, passwordBytes.Length, salt.Length);
        return SHA256.HashData(combined);
    }

    public static bool Verify(string password, byte[]? storedHash, byte[]? storedSalt)
    {
        if (string.IsNullOrWhiteSpace(password)) return false;
        if (storedHash is null || storedHash.Length == 0) return false;
        if (storedSalt is null || storedSalt.Length == 0) return false;

        return CryptographicOperations.FixedTimeEquals(Hash(password, storedSalt), storedHash);
    }
}
