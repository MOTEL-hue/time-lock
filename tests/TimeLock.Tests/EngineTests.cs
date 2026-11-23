using Xunit;
namespace TimeLock.Tests;

public class EngineTests
{
    static BlockRule R(bool local, params Target[] t) => new() { AllowLocalNetwork = local, Targets = t.ToList() };
    static Target T(TargetKind k, params string[] v) => new() { Kind = k, Values = v.ToList() };

    [Fact]
    public void InternetKeepsLocalOnlyIfEveryRuleAllows()
    {
        var d = Engine.Compute(new[] { R(true, T(TargetKind.Internet)), R(false, T(TargetKind.Internet)) });
        Assert.True(d.BlockInternet);
        Assert.False(d.AllowLocalNetwork);
        Assert.True(Engine.Compute(new[] { R(true, T(TargetKind.Internet)) }).AllowLocalNetwork);
    }

    [Fact]
    public void BrowsersAndPlayersExpand()
    {
        var d = Engine.Compute(new[] { R(true, T(TargetKind.Browsers), T(TargetKind.MediaPlayers, "Foo.exe")) });
        Assert.Contains("chrome", d.Processes);
        Assert.Contains("vlc", d.Processes);
        Assert.Contains("foo", d.Processes);
    }

    [Fact]
    public void AllExceptIntersects()
    {
        var d = Engine.Compute(new[]
        {
            R(true, T(TargetKind.AllExcept, "@Word", "@Excel")),
            R(true, T(TargetKind.AllExcept, "@Word", "@Pdf"))
        });
        Assert.NotNull(d.AllowedOnly);
        Assert.Equal(new[] { "winword" }, d.AllowedOnly!.ToArray());
    }

    [Fact]
    public void NoRulesMeansNothing()
    {
        var d = Engine.Compute(Array.Empty<BlockRule>());
        Assert.False(d.BlockAllNetwork || d.BlockWifi || d.BlockInternet);
        Assert.Null(d.AllowedOnly);
        Assert.Empty(d.Processes);
    }

    [Theory]
    [InlineData("https://www.Facebook.com/home?x=1", "facebook.com")]
    [InlineData("youtube.com:443", "youtube.com")]
    [InlineData("news.walla.co.il", "news.walla.co.il")]
    [InlineData("notasite", null)]
    [InlineData("bad domain.com", null)]
    public void SiteNormalization(string input, string? expected) =>
        Assert.Equal(expected, Catalog.NormalizeSite(input));

    [Fact]
    public void ExtensionsExpand()
    {
        var e = Catalog.ExpandExtensions(new[] { "@video", ".PDF", "*.x y" }).ToList();
        Assert.Contains("mp4", e);
        Assert.Contains("pdf", e);
        Assert.DoesNotContain("x y", e);
    }
}
