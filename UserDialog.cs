using System.Security.Cryptography;

namespace CIEL.Reconciliation.Security;

public static class PasswordHasher
{
    private const int Iterations = 150_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Verify(string password, string expectedHash, string salt)
    {
        try
        {
            var saltBytes = Convert.FromBase64String(salt);
            var expected = Convert.FromBase64String(expectedHash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }

    public static string? ValidatePassword(string password)
    {
        if (password.Length < 8) return "Password must contain at least 8 characters.";
        if (!password.Any(char.IsUpper)) return "Password must contain an uppercase letter.";
        if (!password.Any(char.IsLower)) return "Password must contain a lowercase letter.";
        if (!password.Any(char.IsDigit)) return "Password must contain a number.";
        return null;
    }
}
