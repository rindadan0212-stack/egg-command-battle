using System;
using System.IO;
using System.Linq;
using System.Text;
using EggCommand.Core;
using EggCommand.Sim;
using Xunit;

namespace EggCommand.Tests;

/// <summary>`sim import-screen`（pixelizer で起こした画面 → 絵と骨組み）の検査。
///
/// 🔴 **一番大事なのは「出した骨組みが読めること」。**⚠️ 雛形が `Layouts.Parse` を
/// 通らないと、作者は「絵は出たのに画面が開けない」状態で放り出される。
/// ⭐ しかも往復（`Write(Parse(x)) == x`）まで閉じていないと、エディタで開いた瞬間に
/// 保存が封じられる（この作品の土台）。
///
/// ⚠️ 実物の `assets/` へは1バイトも書かない ── 使い捨ての作業場でだけ動かす。</summary>
public class ImportScreenTests
{
    /// <summary>pixelizer が書くのと同じ形の `.pixelizer.json` を組む。
    /// ⭐ 各レイヤーは**キャンバス全面**の等倍 PNG（pixelizer の `celToDataURL` と同じ）。</summary>
    private static string MakeJson(params (string Name, int X, int Y, int W, int H)[] layers)
    {
        var sb = new StringBuilder();
        sb.Append("{\"v\":1,\"w\":").Append(ImportScreen.ScreenW)
          .Append(",\"h\":").Append(ImportScreen.ScreenH)
          .Append(",\"currentLayer\":0,\"currentFrame\":0,\"color\":\"#000000\",\"palette\":[],\"layers\":[");
        for (int i = 0; i < layers.Length; i++)
        {
            var l = layers[i];
            var rgba = new byte[ImportScreen.ScreenW * ImportScreen.ScreenH * 4];
            for (int y = l.Y; y < l.Y + l.H; y++)
                for (int x = l.X; x < l.X + l.W; x++)
                {
                    int at = (y * ImportScreen.ScreenW + x) * 4;
                    rgba[at] = 200; rgba[at + 1] = 100; rgba[at + 2] = 50; rgba[at + 3] = 255;
                }
            string png = Convert.ToBase64String(
                SpritePng.EncodeRgba(ImportScreen.ScreenW, ImportScreen.ScreenH, rgba));
            if (i > 0) sb.Append(',');
            sb.Append("{\"name\":\"").Append(l.Name)
              .Append("\",\"opacity\":1,\"visible\":true,\"frames\":[\"data:image/png;base64,")
              .Append(png).Append("\"]}");
        }
        return sb.Append("]}").ToString();
    }

    /// <summary>使い捨ての作業場で走らせる。⚠️ 実物の assets/ は触らない。</summary>
    private static (int Code, string Out, string Root) RunIn(string json, string screenName)
    {
        string root = Path.Combine(Path.GetTempPath(), "ecb-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "art", "screens"));
        string jsonPath = Path.Combine(root, "art", "screens", screenName + ".pixelizer.json");
        File.WriteAllText(jsonPath, json);

        // ⚠️ **`Console.SetOut` を使わない。**xUnit は検査の組を並列で走らせるので、
        //    グローバルな出し先を差し替えると**他の検査の出力まで奪う**
        //    （2026-08-25 に実際に踏んだ ── `SheetRoundTripTests` が巻き添えで落ちた）。
        using var sw = new StringWriter();
        int code = ImportScreen.Run(root, jsonPath, sw);
        return (code, sw.ToString(), root);
    }

    [Fact]
    public void 出した骨組みは読めて往復も閉じている()
    {
        var json = MakeJson(
            ("home-mats", 0, 0, 270, 20),
            ("slot-frame", 18, 154, 75, 95),
            ("_exp", 200, 4, 60, 12),
            ("_tap-trial", 12, 355, 79, 28));
        var (code, _, root) = RunIn(json, "sample");
        Assert.Equal(0, code);

        string text = File.ReadAllText(Path.Combine(root, "assets", "layouts", "sample.txt"));
        var parsed = Layouts.Parse("sample", text);            // 🔴 読めること
        Assert.Equal(text, Layouts.Write(parsed));             // 🔴 往復が閉じること
        Assert.Empty(Layouts.Faults(parsed));                  // ⚠️ 不備ゼロで出すこと

        Directory.Delete(root, true);
    }

    [Fact]
    public void 座標はドット4倍で必ず4の倍数になる()
    {
        // ⚠️ ドット (18,154) 75×95 は、実物の home.txt の `slot`（72 616 300 380）と同じはず。
        var json = MakeJson(("slot-frame", 18, 154, 75, 95));
        var (code, _, root) = RunIn(json, "sample");
        Assert.Equal(0, code);

        string text = File.ReadAllText(Path.Combine(root, "assets", "layouts", "sample.txt"));
        var node = Layouts.Parse("sample", text).Roots[0];
        Assert.Equal(72f, node.Left);
        Assert.Equal(616f, node.Top);
        Assert.Equal(300f, node.Width);
        Assert.Equal(380f, node.Height);
        Assert.Equal(0, (int)node.Left % 4);
        Assert.Equal(0, (int)node.Top % 4);

        Directory.Delete(root, true);
    }

    [Fact]
    public void 絵は枠とぴったり同じ大きさで焼かれる()
    {
        // ⭐ 段取り4 で 44箇所出た「枠と絵が合わない」が、この経路では起きないことの証拠。
        var json = MakeJson(("home-mats", 0, 0, 270, 20));
        var (code, _, root) = RunIn(json, "sample");
        Assert.Equal(0, code);

        var png = File.ReadAllBytes(Path.Combine(root, "assets", "ui", "paint", "home-mats.png"));
        SpritePng.DecodeRgba(png, out int w, out int h, out _);
        Assert.Equal(270, w);
        Assert.Equal(20, h);

        var node = Layouts.Parse("sample",
            File.ReadAllText(Path.Combine(root, "assets", "layouts", "sample.txt"))).Roots[0];
        Assert.Equal(w * ImportScreen.Scale, (int)node.Width);
        Assert.Equal(h * ImportScreen.Scale, (int)node.Height);

        Directory.Delete(root, true);
    }

    [Fact]
    public void 目安と押しどころは絵にしない()
    {
        // 🔴 字・数は骨組みが描くので、絵にすると二重に出る。
        var json = MakeJson(("_exp", 200, 4, 60, 12), ("_tap-trial", 12, 355, 79, 28));
        var (code, _, root) = RunIn(json, "sample");
        Assert.Equal(0, code);

        string paint = Path.Combine(root, "assets", "ui", "paint");
        Assert.False(File.Exists(Path.Combine(paint, "_exp.png")));
        Assert.False(File.Exists(Path.Combine(paint, "exp.png")));
        Assert.False(File.Exists(Path.Combine(paint, "trial.png")));

        var roots = Layouts.Parse("sample",
            File.ReadAllText(Path.Combine(root, "assets", "layouts", "sample.txt"))).Roots;
        Assert.Equal("label", roots[0].Kind);
        Assert.Equal("exp", roots[0].Name);
        Assert.Equal("button", roots[1].Kind);
        Assert.Equal("trial", roots[1].Name);

        Directory.Delete(root, true);
    }

    [Fact]
    public void 既にある骨組みは書き換えない()
    {
        // 🔴 手で入れた when=・use=・注釈を消さない（往復のバイト忠実と同じ姿勢）。
        var json = MakeJson(("home-mats", 0, 0, 270, 20));
        string root = Path.Combine(Path.GetTempPath(), "ecb-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "art", "screens"));
        Directory.CreateDirectory(Path.Combine(root, "assets", "layouts"));
        string jsonPath = Path.Combine(root, "art", "screens", "sample.pixelizer.json");
        File.WriteAllText(jsonPath, json);

        string mine = "# 手で書いた\nkeep box 0 0 100 100\n";
        string layoutPath = Path.Combine(root, "assets", "layouts", "sample.txt");
        File.WriteAllText(layoutPath, mine);

        using var sw = new StringWriter();
        ImportScreen.Run(root, jsonPath, sw);

        Assert.Equal(mine, File.ReadAllText(layoutPath));       // 1バイトも変わっていない
        Assert.Contains("書き換えていません", sw.ToString());

        Directory.Delete(root, true);
    }

    [Fact]
    public void 画面の大きさが違うキャンバスは断る()
    {
        // ⚠️ 通すと座標が全部ずれた骨組みができ、後から原因が分からなくなる。
        string json = MakeJson(("home-mats", 0, 0, 10, 10)).Replace("\"w\":270", "\"w\":72")
                                                            .Replace("\"h\":480", "\"h\":128");
        var (code, output, root) = RunIn(json, "sample");
        Assert.Equal(1, code);
        Assert.Contains("270×480 で描いてください", output);
        Directory.Delete(root, true);
    }

    [Fact]
    public void 名前を付け忘れたレイヤーは書き出さずに言う()
    {
        // ⚠️ `Layer 3.png` を黙って作らない。
        var json = MakeJson(("home-mats", 0, 0, 270, 20), ("Layer 2", 10, 10, 20, 20));
        var (code, output, root) = RunIn(json, "sample");
        Assert.Equal(0, code);
        Assert.False(File.Exists(Path.Combine(root, "assets", "ui", "paint", "Layer 2.png")));
        Assert.Contains("名前を部品名に変えてください", output);
        Directory.Delete(root, true);
    }

    [Fact]
    public void 製図モードの図形レイヤーは静かに読み飛ばす()
    {
        // 🔴 `tools/draw` の図形レイヤー（wiki/開発/製図モード.md §3）は `frames` を
        //    持たず `kind:"shapes"` を名乗る。取り込まない・警告にはしない・
        //    他のレイヤーの取り込みは止めない、の3つを確かめる。
        string raster = MakeJson(("home-mats", 0, 0, 270, 20));
        // MakeJson が組んだ JSON の "layers":[...] の末尾に、図形レイヤーを1つ足す。
        string json = raster.Substring(0, raster.Length - 2)
            + ",{\"name\":\"製図\",\"opacity\":1,\"visible\":true,\"kind\":\"shapes\","
            + "\"shapes\":[{\"id\":1,\"kind\":\"rect\",\"x\":1,\"y\":1,\"w\":5,\"h\":5}]}]}";

        var (code, output, root) = RunIn(json, "sample");
        Assert.Equal(0, code);
        Assert.False(File.Exists(Path.Combine(root, "assets", "ui", "paint", "製図.png")));
        Assert.DoesNotContain("⚠️", output.Split('\n').FirstOrDefault(l => l.Contains("製図")) ?? "");
        Assert.Contains("製図モードの下敷きなので取り込みません", output);
        // 他のレイヤー（home-mats）は普通に取り込まれる。
        Assert.True(File.Exists(Path.Combine(root, "assets", "ui", "paint", "home-mats.png")));
        Directory.Delete(root, true);
    }
}
