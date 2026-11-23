using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TimeLock;

public sealed record UpdateManifest(string Version, string Sha256, string Url);

public static class AppInfo
{
    public static string Version =>
        (Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0)).ToString(3);
}

public static class UpdateService
{
    /// <summary>רק קבצים מהמאגר הזה מתקבלים כעדכון.</summary>
    public const string UrlPrefix = "https://github.com/MOTEL-hue/time-lock/releases/download/";
    public const string LatestBase = "https://github.com/MOTEL-hue/time-lock/releases/latest/download/";

    /// <summary>המפתח הציבורי לבדיקת חתימת עדכונים. ריק = עדכון אוטומטי כבוי.</summary>
    public static string PublicKeyPem = "";

    public static byte[] Sign(RSA privateKey, byte[] data) =>
        privateKey.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    /// <summary>מחזיר את פרטי העדכון רק אם החתימה תקינה והכתובת מהמאגר שלנו.</summary>
    public static UpdateManifest? ParseAndVerify(byte[] manifest, byte[] signature, RSA publicKey)
    {
        try
        {
            if (!publicKey.VerifyData(manifest, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return null;
            var m = JsonSerializer.Deserialize<UpdateManifest>(manifest,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (m == null || !System.Version.TryParse(m.Version, out _)) return null;
            if (!Regex.IsMatch(m.Sha256 ?? "", "^[0-9a-fA-F]{64}$")) return null;
            if (m.Url == null || !m.Url.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase) ||
                !m.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return null;
            return m;
        }
        catch (Exception) { return null; }
    }

    public static bool IsNewer(string offered, string current) =>
        System.Version.TryParse(offered, out var o) && System.Version.TryParse(current, out var c) && o > c;

    public static bool Sha256Matches(string file, string hex)
    {
        using var fs = File.OpenRead(file);
        var actual = Convert.ToHexString(SHA256.HashData(fs));
        return actual.Equals(hex, StringComparison.OrdinalIgnoreCase);
    }
}
