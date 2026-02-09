using System.Security.Cryptography;
using Transaction.Application.Users;

namespace Transaction.Infrastructure.Authentication;

/// <summary>
/// BCrypt-style Password Hasher using PBKDF2
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32; // 256 bit
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return $"{Convert.ToHexString(salt)}.{Convert.ToHexString(hash)}";
    }

    public bool VerifyPassword(string password, string hash)
    {
        var parts = hash.Split('.');
        if (parts.Length != 2)
            return false;

        var salt = Convert.FromHexString(parts[0]);
        var key = Convert.FromHexString(parts[1]);

        var keyToCheck = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return CryptographicOperations.FixedTimeEquals(keyToCheck, key);
    }
}
