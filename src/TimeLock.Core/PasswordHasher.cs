using System.Security.Cryptography;

namespace TimeLock;

public static class PasswordHasher
{
    const int Iterations = 200_000;

    public static string Hash(string secret)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(secret, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string secret, string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        var p = stored.Split('.');
        if (p.Length != 4 || p[0] != "v1" || !int.TryParse(p[1], out var it) || it < 1000) return false;
        try
        {
            var salt = Convert.FromBase64String(p[2]);
            var expected = Convert.FromBase64String(p[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(secret, salt, it, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}
