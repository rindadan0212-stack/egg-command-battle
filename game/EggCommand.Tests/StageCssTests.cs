using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace EggCommand.Tests;

/// <summary>意匠（`stage.css`）が **id で** 物を指していないか見張る。
///
/// 🔴 **id は画面をまたいでも唯一。**⚠️ 骨組みは1枚ずつ書かれているので、
/// 別々の画面で同じ名前を付けてしまう ── そこへ `#名前 { … }` を書くと、
/// **書いた覚えのない画面にも効く**。
///
/// ⭐ 実際に起きたこと（2026-08-26）: 放置の地面（`Idle.cs`）とすごろくの盤の器
/// （`assets/layouts/trail.txt`）が両方 `ground` という名前で、
/// `#ground { animation: idle-roll 12s linear infinite }` が**盤にも掛かった**。
/// 盤は12秒かけて左へ流れ続け、マスが指の下から逃げて押せなくなった
/// （「すごろくがプレイできない。画面が動き続けて…」）。
///
/// ⚠️ **見た目の検査では捕まらない。**⭐ 絵は出ているし、押しどころも在る
/// ── ずれているのは**時間**なので、静止画をいくら見ても分からない。
/// だから名前の突き合わせで捕まえる。</summary>
public class StageCssTests
{
    private static readonly string Css =
        Path.Combine(AppContext.BaseDirectory, "websrc", "stage.css");
    private static readonly string LayoutDir =
        Path.Combine(AppContext.BaseDirectory, "layouts");

    /// <summary>意匠の中で**選び手**として出てくる `#名前`。
    ///
    /// ⚠️ 注記と `{ … }` の中は先に落とす ── ⭐ 落とさないと 16進の色（`#c0303f`）が
    /// 名前に見え、検査が色の数だけ嘘の警報を出す。</summary>
    private static HashSet<string> Selectors()
    {
        string text = File.ReadAllText(Css);
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        // ⚠️ 入れ子（`@media { … { … } }`）があるので、内側から繰り返し畳む
        string before;
        do { before = text; text = Regex.Replace(text, @"\{[^{}]*\}", " "); }
        while (text != before);
        return Regex.Matches(text, @"#([A-Za-z][\w-]*)")
            .Select(m => m.Groups[1].Value).ToHashSet();
    }

    /// <summary>骨組みが名乗る部品の名前（全ファイル）。</summary>
    private static HashSet<string> LayoutNames()
    {
        var names = new HashSet<string>();
        foreach (var path in Directory.GetFiles(LayoutDir, "*.txt"))
            foreach (var line in File.ReadAllLines(path))
            {
                string t = line.Trim();
                if (t.Length == 0 || t[0] == '#') continue;
                names.Add(t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0]);
            }
        return names;
    }

    /// <summary>描く側（`Idle`/`Board`）が直に書き出す id。
    /// ⚠️ 繰り返しの `tuft#{i}` などは `#` の手前までを名前とみなす。</summary>
    private static HashSet<string> DrawnIds()
    {
        var ids = new HashSet<string>();
        foreach (var file in new[] { "Idle.cs", "Board.cs" })
        {
            string text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "websrc", file));
            foreach (Match m in Regex.Matches(text, @"Box\(""([\w-]+)"))
                ids.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"id=\\""([\w-]+)"))
                ids.Add(m.Groups[1].Value);
        }
        return ids;
    }

    /// <summary>⚠️ 1つも読めなければ、**検査が空回りしている**。</summary>
    [Fact]
    public void 検査するものが在る()
    {
        Assert.True(File.Exists(Css), "stage.css が写されていない");
        Assert.NotEmpty(Selectors());
        Assert.NotEmpty(LayoutNames());
        Assert.NotEmpty(DrawnIds());
    }

    /// <summary>🔴 **意匠は、骨組みの部品を id で指してはいけない。**
    ///
    /// ⭐ 指したいなら級（`class`）にする ── そうすれば、付けた物にしか効かない。
    /// ⚠️ 冠付き（`#dim-roll` ＝ `dim` を `-roll` でずらしたもの）は素通しでよい
    /// ── 冠は「その場面のためにずらした名前」なので、他の画面とはぶつからない。</summary>
    [Fact]
    public void 意匠は骨組みの名前をidで指さない()
    {
        var hit = Selectors().Intersect(LayoutNames()).OrderBy(s => s).ToList();
        Assert.True(hit.Count == 0,
            "stage.css が骨組みの部品を id で指している（級に直す）: " + string.Join(", ", hit)
            + "\n── id は画面をまたいでも唯一。別の画面の同じ名前の部品にも効く。");
    }

    /// <summary>🔴 **描く側が出す id も、骨組みの名前とぶつけない。**
    /// ⚠️ ぶつかると `getElementById` が**先に見つかったほう**を返す
    /// （帯の差し替え `eggTap.bars` が別の画面の部品を掴む）。</summary>
    [Fact]
    public void 意匠は描く側のidも指さない()
    {
        var hit = Selectors().Intersect(DrawnIds()).OrderBy(s => s).ToList();
        Assert.True(hit.Count == 0,
            "stage.css が描く側の出す id を指している（級に直す）: " + string.Join(", ", hit));
    }

    /// <summary>🔴 **`ground` は二度と id で指さない**（2026-08-26 の不具合の釘）。
    /// ⚠️ 名前を戻しただけでは再発する ── ⭐ 「その名前を id で指さない」ことを直に留める。</summary>
    [Fact]
    public void 盤と地面の名前がぶつからない()
    {
        Assert.DoesNotContain("ground", Selectors());
        Assert.DoesNotContain("ground", DrawnIds());
    }
}
