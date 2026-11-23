using System.Security.Cryptography;
using System.Text;
using Xunit;
namespace TimeLock.Tests;

public class UpdateTests
{
    static readonly string Sha = new('a', 64);
    static byte[] Manifest(string version, string url) =>
        Encoding.UTF8.GetBytes($"{{\"version\":\"{version}\",\"sha256\":\"{Sha}\",\"url\":\"{url}\"}}");
    const string GoodUrl = UpdateService.UrlPrefix + "v1.0.5/TimeLock-win-x64.zip";

    [Fact]
    public void ValidManifestIsAccepted()
    {
        using var rsa = RSA.Create(2048);
        var data = Manifest("1.0.5", GoodUrl);
        var m = UpdateService.ParseAndVerify(data, UpdateService.Sign(rsa, data), rsa);
        Assert.NotNull(m);
        Assert.Equal("1.0.5", m!.Version);
    }

    [Fact]
    public void TamperedOrWrongKeyIsRejected()
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        var data = Manifest("1.0.5", GoodUrl);
        var sig = UpdateService.Sign(rsa, data);
        Assert.Null(UpdateService.ParseAndVerify(Manifest("1.0.6", GoodUrl), sig, rsa));
        Assert.Null(UpdateService.ParseAndVerify(data, sig, other));
    }

    [Theory]
    [InlineData("https://evil.example.com/TimeLock-win-x64.zip")]
    [InlineData(UpdateService.UrlPrefix + "v1/x.exe")]
    public void ForeignUrlIsRejectedEvenIfSigned(string url)
    {
        using var rsa = RSA.Create(2048);
        var data = Manifest("1.0.5", url);
        Assert.Null(UpdateService.ParseAndVerify(data, UpdateService.Sign(rsa, data), rsa));
    }

    [Theory]
    [InlineData("1.0.6", "1.0.5", true)]
    [InlineData("1.0.5", "1.0.5", false)]
    [InlineData("1.0.4", "1.0.5", false)]
    [InlineData("1.1.0", "1.0.99", true)]
    [InlineData("garbage", "1.0.0", false)]
    public void VersionComparison(string offered, string current, bool expected) =>
        Assert.Equal(expected, UpdateService.IsNewer(offered, current));

    [Fact]
    public void HashCheck()
    {
        var f = Path.GetTempFileName();
        File.WriteAllText(f, "hello");
        var hex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("hello")));
        Assert.True(UpdateService.Sha256Matches(f, hex.ToLowerInvariant()));
        Assert.False(UpdateService.Sha256Matches(f, new string('0', 64)));
        File.Delete(f);
    }
}
