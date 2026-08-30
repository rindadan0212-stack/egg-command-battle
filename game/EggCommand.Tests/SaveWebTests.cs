using System;
using System.IO;
using Xunit;

namespace EggCommand.Tests;

public sealed class SaveWebTests
{
    private static readonly string WebSrc = Path.Combine(AppContext.BaseDirectory, "websrc");

    [Fact]
    public void 保存ロックは解放口を持つleaseである()
    {
        string save = File.ReadAllText(Path.Combine(WebSrc, "save.js"));
        string tap = File.ReadAllText(Path.Combine(WebSrc, "tap.js"));
        string app = File.ReadAllText(Path.Combine(WebSrc, "AppPage.razor"));

        Assert.Contains("_releaseLock", save);
        Assert.Contains("release()", save);
        Assert.Contains(" / scale + 392", tap);
        Assert.Contains("Number.isFinite(scale)", tap);
        Assert.Contains("scale <= 0", tap);
        Assert.Contains("Number.isFinite(hopTop)", tap);
        Assert.Contains("ReleaseAsync", app);
        Assert.Contains("IAsyncDisposable", app);
    }
}
