using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace EggCommand.Tests;

/// <summary>⭐ **「座標はアセットにしか無い」を機械で守らせる。**
///
/// ⚠️ 作者の指示（2026-08-22）:
/// > コードでボタンを作るからいけないのでは？すべてアセットを使用することを
/// > 厳格に守れば自作UIエディタでも十分に機能すると思う。
///
/// ⚠️ **「気をつける」では守れません。**Unity へ移した理由の1つが
/// 「作者が Editor で置く」でしたが、`Ui.Place` がいつでも呼べたので、
/// 新しい画面はすべてコードで置かれました（Prefab 15枚は生成、新機能は Prefab 無し）。
///
/// ⭐ だから**呼べなくする**のではなく、**呼んだら落ちる**ようにします。
/// ⚠️ 直し終わった画面から <see cref="Converted"/> に足していきます
/// ── 一覧が伸びることが、そのまま移行の進み具合になります。</summary>
public class LayoutRuleTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "view");

    /// <summary>骨組みへ移し終わった画面。⚠️ **戻すときは理由を書くこと。**</summary>
    private static readonly string[] Converted =
    {
        "BookScreen",
    };

    /// <summary>座標をコードに書く呼び出し。⚠️ ここに無いものを増やさない。</summary>
    private static readonly string[] Banned =
    {
        "Ui.Place(", "Ui.Label(", "Ui.Card(", "Ui.Scroller(",
        "Ui.Tappable(", "Ui.Round(", "Ui.Pixel(", "Ui.Rect(",
    };

    public static IEnumerable<object[]> All()
    {
        foreach (var name in Converted) yield return new object[] { name };
    }

    [Fact]
    public void 画面の元が見つかる()
    {
        Assert.True(Directory.Exists(Dir), $"{Dir} が無い（csproj のコピー設定を見る）");
        foreach (var name in Converted)
            Assert.True(File.Exists(Path.Combine(Dir, name + ".cs")), $"{name}.cs が無い");
    }

    [Theory]
    [MemberData(nameof(All))]
    public void 座標をコードに書いていない(string name)
    {
        var lines = File.ReadAllLines(Path.Combine(Dir, name + ".cs"));
        var found = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            // ⚠️ 注釈の中の言及は数えない（「書き戻さないこと」と書けなくなる）
            string body = line.TrimStart();
            if (body.StartsWith("//") || body.StartsWith("///") || body.StartsWith("*")) continue;

            foreach (var call in Banned)
            {
                if (line.IndexOf(call, StringComparison.Ordinal) < 0) continue;
                found.Add($"{name}.cs:{i + 1} {call} ── 座標は骨組み（Layouts/*.txt）へ");
            }
        }
        Assert.Equal(new List<string>(), found);
    }

    /// <summary>⭐ **残りがどれだけか**を数える。⚠️ 0 になったら
    /// この検査は「全画面」に掛けられるので、そのとき <see cref="Converted"/> を消す。</summary>
    [Fact]
    public void 残りの画面を数える()
    {
        var rest = new List<string>();
        foreach (var path in Directory.GetFiles(Dir, "*.cs"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (Array.IndexOf(Converted, name) >= 0) continue;
            foreach (var call in Banned)
            {
                if (File.ReadAllText(path).IndexOf(call, StringComparison.Ordinal) < 0) continue;
                rest.Add(name);
                break;
            }
        }
        // ⚠️ **落とさない。**⭐ まだ移していないのは不備ではなく、途中だから。
        //    数だけ残して、進み具合が見えるようにする。
        Assert.True(rest.Count >= 0);
        Console.WriteLine($"骨組みへ未移行: {rest.Count} ファイル / 移行済み: {Converted.Length}");
        foreach (var name in rest) Console.WriteLine("  " + name);
    }
}
