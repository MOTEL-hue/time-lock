using System.Security.Cryptography;
using System.Text;
using Xunit;
namespace TimeLock.Tests;

public class UpdateTests
{
    static readonly string Sha = new('a', 64);
    static byte[] Manifest(string version, string url, string? sha = null) =>
        Encoding.UTF8.GetBytes($"{{\"version\":\"{version}\",\"sha256\":\"{sha ?? Sha}\",\"url\":\"{url}\"}}");
    const string GoodUrl = UpdateService.UrlPrefix + "v1.0.5/TimeLock-win-x64.zip";

    [Fact]
    public void ValidManifestIsAccepted()
    {
        var m = UpdateService.Parse(Manifest("1.0.5", GoodUrl));
        Assert.NotNull(m);
        Assert.Equal("1.0.5", m!.Version);
    }

    [Theory]
    [InlineData("https://evil.example.com/TimeLock-win-x64.zip")]
    [InlineData(UpdateService.UrlPrefix + "v1/x.exe")]
    public void ForeignUrlIsRejected(string url) =>
        Assert.Null(UpdateService.Parse(Manifest("1.0.5", url)));

    [Fact]
    public void BadHashOrVersionOrGarbageIsRejected()
    {
        Assert.Null(UpdateService.Parse(Manifest("1.0.5", GoodUrl, "short")));
        Assert.Null(UpdateService.Parse(Manifest("not-a-version", GoodUrl)));
        Assert.Null(UpdateService.Parse(Encoding.UTF8.GetBytes("<html>not json</html>")));
        Assert.Null(UpdateService.Parse(new byte[5000]));
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
