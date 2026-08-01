using System.Security.Cryptography;

namespace Hato.Modules.People.Domain;

/// <summary>
/// PBKDF2 password hashing (Art. 4: credentials are never stored reversibly).
/// Format: "{iterations}.{saltBase64}.{keyBase64}" so the work factor can change
/// without invalidating previously-issued hashes.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string hashedPassword)
    {
        var parts = hashedPassword.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
            return false;

        byte[] salt, expectedKey;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedKey = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
