using System.Security.Cryptography;
using Xunit;
namespace TimeLock.Tests;

public class SecurityTests
{
    [Fact]
    public void PasswordRoundTrip()
    {
        var h = PasswordHasher.Hash("שלום123");
        Assert.True(PasswordHasher.Verify("שלום123", h));
        Assert.False(PasswordHasher.Verify("שלום124", h));
        Assert.False(PasswordHasher.Verify("x", null));
        Assert.False(PasswordHasher.Verify("x", "garbage"));
    }

    [Fact]
    public void ConfigRoundTripAndBadFileKeepsLast()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var store = new ConfigStore(path);
        var cfg = new AppConfig();
        cfg.Rules.Add(new BlockRule { Name = "עבודה", Strictness = Strictness.Hard,
            Days = { DayOfWeek.Monday }, Targets = { new Target { Kind = TargetKind.Wifi } } });
        store.Save(cfg);
        var back = store.LoadSafe();
        Assert.Equal("עבודה", back.Rules[0].Name);
        Assert.Equal(Strictness.Hard, back.Rules[0].Strictness);
        File.WriteAllText(path, "{ broken");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        Assert.Single(store.LoadSafe().Rules);
        File.Delete(path);
    }

    [Fact]
    public void LicenseSignVerifyAndTamper()
    {
        using var rsa = RSA.Create(2048);
        using var pub = RSA.Create();
        pub.ImportRSAPublicKey(rsa.ExportRSAPublicKey(), out _);
        var key = LicenseService.Sign(rsa, new LicensePayload("דוד", null));
        var cfg = new AppConfig { LicenseKey = key, FirstRunUtc = DateTime.UtcNow.AddDays(-100) };
        Assert.Equal(LicenseStatus.Licensed, LicenseService.Evaluate(cfg, DateTime.UtcNow, pub).Status);
        cfg.LicenseKey = "A" + key;
        Assert.Equal(LicenseStatus.Expired, LicenseService.Evaluate(cfg, DateTime.UtcNow, pub).Status);
    }

    [Fact]
    public void ExpiredLicenseFallsBackToTrialRules()
    {
        using var rsa = RSA.Create(2048);
        var key = LicenseService.Sign(rsa, new LicensePayload("x", DateTime.UtcNow.AddDays(-1)));
        var cfg = new AppConfig { LicenseKey = key, FirstRunUtc = DateTime.UtcNow };
        var info = LicenseService.Evaluate(cfg, DateTime.UtcNow, rsa);
        Assert.Equal(LicenseStatus.Trial, info.Status);
        Assert.True(info.AllowEnforcement);
    }

    [Fact]
    public void TrialEndsAfterFourteenDays()
    {
        var cfg = new AppConfig { FirstRunUtc = DateTime.UtcNow.AddDays(-15) };
        Assert.False(LicenseService.Evaluate(cfg, DateTime.UtcNow).AllowEnforcement);
        cfg.FirstRunUtc = DateTime.UtcNow.AddDays(-1);
        Assert.Equal(LicenseStatus.Trial, LicenseService.Evaluate(cfg, DateTime.UtcNow).Status);
    }
}
