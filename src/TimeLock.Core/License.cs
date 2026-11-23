using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TimeLock;

public enum LicenseStatus { Trial, Licensed, Expired }

public sealed record LicenseInfo(LicenseStatus Status, int TrialDaysLeft, string? Holder, DateTime? Expires)
{
    /// <summary>האם החסימות פועלות. כשהרישיון פג ההחלטה היא כאן, במקום אחד.</summary>
    public bool AllowEnforcement => Status != LicenseStatus.Expired;
}

public sealed record LicensePayload(string Holder, DateTime? Expires);

public static class LicenseService
{
    public const int TrialDays = 14;

    /// <summary>המפתח הציבורי לבדיקת רישיונות. ריק = אפשר רק תקופת ניסיון, עד שתחליט על מכירה.</summary>
    public static string PublicKeyPem = "";

    public static string Sign(RSA privateKey, LicensePayload payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var sig = privateKey.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return B64(bytes) + "." + B64(sig);
    }

    public static LicensePayload? Verify(string? key, RSA publicKey)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var parts = key.Trim().Split('.');
        if (parts.Length != 2) return null;
        try
        {
            var data = FromB64(parts[0]);
            var sig = FromB64(parts[1]);
            if (!publicKey.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) return null;
            return JsonSerializer.Deserialize<LicensePayload>(Encoding.UTF8.GetString(data));
        }
        catch (Exception) { return null; }
    }

    public static LicenseInfo Evaluate(AppConfig cfg, DateTime nowUtc, RSA? publicKey = null)
    {
        if (publicKey == null && !string.IsNullOrWhiteSpace(PublicKeyPem))
        {
            publicKey = RSA.Create();
            publicKey.ImportFromPem(PublicKeyPem);
        }
        if (publicKey != null)
        {
            var p = Verify(cfg.LicenseKey, publicKey);
            if (p != null && (p.Expires == null || p.Expires.Value.Date >= nowUtc.Date))
                return new LicenseInfo(LicenseStatus.Licensed, 0, p.Holder, p.Expires);
        }
        var first = cfg.FirstRunUtc ?? nowUtc;
        int left = TrialDays - (int)Math.Floor((nowUtc - first).TotalDays);
        return left > 0
            ? new LicenseInfo(LicenseStatus.Trial, left, null, null)
            : new LicenseInfo(LicenseStatus.Expired, 0, null, null);
    }

    static string B64(byte[] b) => Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    static byte[] FromB64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
