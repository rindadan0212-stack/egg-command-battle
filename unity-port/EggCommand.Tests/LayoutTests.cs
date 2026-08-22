using System;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組み（レイアウトのアセット）。
///
/// ⭐ **これが在る理由**: 座標をコードから追い出すため（2026-08-22・作者の指示）。
/// ⚠️ そして**検査がエンジン無しで回る**ようにするため ── Unity の往復は
/// 無変更でも 19秒、ここは1秒未満。
///
/// ⚠️ **道具はわざと壊して効きを確かめる。**「不備 0 件」は、
/// 見つけられないだけかもしれない。</summary>
public class LayoutTests
{
    private const string Good = @"
# 図鑑
head    label   48 16 984 44     size=28 anchor=left
grid    scroll  48 76 984 1400
  cell  card    0 0 317 304
    art pixel   78 24 160 160    bind=art
    name label  8 196 301 40     size=30 bind=name
";

    [Fact]
    public void 読めて木になる()
    {
        var layout = Layouts.Parse("book", Good);
        Assert.Equal(2, layout.Roots.Count);
        Assert.Equal("head", layout.Roots[0].Name);
        Assert.Equal("label", layout.Roots[0].Kind);
        Assert.Equal(48f, layout.Roots[0].Left);
        Assert.Equal("28", layout.Roots[0].Option("size"));

        var grid = layout.Roots[1];
        Assert.Single(grid.Children);
        var cell = grid.Children[0];
        Assert.Equal(2, cell.Children.Count);
        Assert.Equal("art", cell.Children[0].Name);
        Assert.Equal("name", cell.Children[1].Name);
    }

    [Fact]
    public void 正しい骨組みは不備なし()
    {
        Assert.Equal(new System.Collections.Generic.List<string>(),
            Layouts.Faults(Layouts.Parse("book", Good)));
    }

    // ── ⚠️ ここから「わざと壊す」──────────────────────

    [Fact]
    public void 字の重なりを見つける()
    {
        var bad = Layouts.Parse("t", @"
a label 0 0 100 40
b label 0 20 100 40
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("字の重なり"));
    }

    /// <summary>⭐ 札（面）と字が重なるのは当たり前 ── そこは落とさない。</summary>
    [Fact]
    public void 面と字の重なりは落とさない()
    {
        var fine = Layouts.Parse("t", @"
a card  0 0 100 40
b label 0 0 100 40
");
        Assert.Equal(new System.Collections.Generic.List<string>(), Layouts.Faults(fine));
    }

    [Fact]
    public void 親から横へのはみ出しを見つける()
    {
        var bad = Layouts.Parse("t", @"
box  box   0 0 100 100
  in label 0 0 120 40
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("横へはみ出し"));
    }

    /// <summary>⚠️ これが今日 BOX で起きた形 ── 一覧が下の帯へ潜った。
    /// ⭐ 骨組みにすると**テストで落ちます**（実機を見る前に）。</summary>
    [Fact]
    public void 親から縦へのはみ出しを見つける()
    {
        var bad = Layouts.Parse("t", @"
box  box   0 0 100 100
  in label 0 80 100 40
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("縦へはみ出し"));
    }

    [Fact]
    public void 画面の外を見つける()
    {
        var bad = Layouts.Parse("t", "a label 1000 0 200 40");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("画面の外"));
    }

    /// <summary>⚠️ これは今日 Console 頼りで見つけた不備（戦闘の釦が高さ84）。
    /// ⭐ 骨組みにすると、**テストで落ちる**。</summary>
    [Fact]
    public void 押しどころが小さいのを見つける()
    {
        var bad = Layouts.Parse("t", "go button 0 0 400 84");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("指で押せない"));
        var fine = Layouts.Parse("t", "go button 0 0 400 112");
        Assert.Equal(new System.Collections.Generic.List<string>(), Layouts.Faults(fine));
    }

    [Fact]
    public void 知らない種類を見つける()
    {
        var bad = Layouts.Parse("t", "a wobble 0 0 100 40");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("知らない種類"));
    }

    [Fact]
    public void 同じ名前が2つあるのを見つける()
    {
        var bad = Layouts.Parse("t", @"
box box    0 0 200 200
  a label  0 0 100 40
  a label  0 60 100 40
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("が2つある"));
    }

    /// <summary>⭐ **巻物は縦に無限。**中身が下へ溢れるのが役目なので落とさない。</summary>
    [Fact]
    public void 巻物の中は縦に溢れてよい()
    {
        var fine = Layouts.Parse("t", @"
s scroll  0 0 400 200
  a label 0 700 400 40
");
        Assert.Equal(new System.Collections.Generic.List<string>(), Layouts.Faults(fine));
    }

    /// <summary>⚠️ **横は巻物でも見る。**巻物は縦にしか動かないので、
    /// 横へ溢れたものには指が届かない。</summary>
    [Fact]
    public void 巻物の中でも横は溢れてはいけない()
    {
        var bad = Layouts.Parse("t", @"
s scroll  0 0 400 200
  a label 0 10 500 40
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("横へはみ出し"));
    }

    /// <summary>⭐ **並びの検査。**⚠️ 「3列で置いたら右端が切れる」を実機まで持ち越さない。</summary>
    [Fact]
    public void 列が親の幅に収まらないのを見つける()
    {
        var bad = Layouts.Parse("t", @"
s box    0 0 984 400
  cell card 0 0 340 300 repeat=species cols=3 gap=16
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("収まらない"));

        var fine = Layouts.Parse("t", @"
s box    0 0 984 400
  cell card 0 0 317 300 repeat=species cols=3 gap=16 max=3
");
        Assert.Equal(new System.Collections.Generic.List<string>(), Layouts.Faults(fine));
    }

    // ── ⚠️ 2026-08-22 に塞いだ穴（事前検死とUI設計の指摘）──────

    /// <summary>⚠️ **巻物の外で繰り返すなら、何段までかを宣言させる。**
    /// ⭐ 繰り返しの数はデータ次第なので、検査は「何個来るか」を知らない。
    /// 宣言が無ければ、増えた日に黙って親からはみ出す。</summary>
    [Fact]
    public void 巻物の外の繰り返しに上限が無ければ落とす()
    {
        var bad = Layouts.Parse("t", @"
s box     0 0 984 400
  cell card 0 0 317 100 repeat=species cols=3 gap=16
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("max="));

        // ⭐ 巻物の中なら要らない（溢れることが巻物の役目）
        var fine = Layouts.Parse("t", @"
s scroll  0 0 984 400
  cell card 0 0 317 100 repeat=species cols=3 gap=16
");
        Assert.Equal(new System.Collections.Generic.List<string>(), Layouts.Faults(fine));
    }

    [Fact]
    public void 上限まで並べると親を超えるのを見つける()
    {
        var bad = Layouts.Parse("t", @"
s box     0 0 984 400
  cell card 0 0 317 100 repeat=species cols=3 gap=16 max=30
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("縦へはみ出す"));
    }

    /// <summary>⚠️ **押しどころどうしが重なると、片方に指が届かない。**
    /// ⭐ 初版は字しか見ておらず、釦が2枚重なっても素通りした。</summary>
    [Fact]
    public void 押しどころの重なりを見つける()
    {
        var bad = Layouts.Parse("t", @"
a button 0 0 400 112
b button 0 60 400 112
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("押しどころの重なり"));
    }

    /// <summary>⭐ 札の上に字が乗るのは当たり前。⚠️ そこは落とさない。</summary>
    [Fact]
    public void 釦の上の字は落とさない()
    {
        var fine = Layouts.Parse("t", @"
box box    0 0 400 200
  a button 0 0 400 112
  b label  0 0 400 112
");
        Assert.Equal(new System.Collections.Generic.List<string>(), Layouts.Faults(fine));
    }

    /// <summary>⚠️ **知らない付け足しを黙って無視しない。**
    /// ⭐ `anchr=left` が通ると「直したのに効かない」を延々と追うことになる。</summary>
    [Fact]
    public void 知らない付け足しを見つける()
    {
        var bad = Layouts.Parse("t", "a label 0 0 100 40 anchr=left");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("知らない付け足し"));
    }

    [Fact]
    public void 同じ付け足しを二度書いたら落とす()
    {
        Assert.Throws<ArgumentException>(() =>
            Layouts.Parse("t", "a label 0 0 100 40 size=20 size=30"));
    }

    /// <summary>⚠️ **検査する枠と、実際に描かれる枠を食い違わせない。**
    /// ⭐ 絵と丸は短いほうの辺で正方形に描かれる。</summary>
    [Fact]
    public void 絵や丸が正方形でなければ落とす()
    {
        var bad = Layouts.Parse("t", "a pixel 0 0 984 40 bind=art");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("正方形"));

        var fine = Layouts.Parse("t", "a pixel 0 0 160 160 bind=art");
        Assert.Equal(new System.Collections.Generic.List<string>(), Layouts.Faults(fine));
    }

    /// <summary>⚠️ **名前に `#` を使わせない。**⭐ 繰り返しの複製が `名前#0` を作るので、
    /// 元の名前に `#` があると往復が閉じない。</summary>
    [Fact]
    public void 名前に番号記号は使えない()
    {
        Assert.Throws<ArgumentException>(() => Layouts.Parse("t", "a#b label 0 0 100 40"));
    }

    /// <summary>⭐ **段の高さの出所は1つ。**⚠️ 置く側と数える側が別々に決めていた頃、
    /// `rows=` を書かない画面で巻物の中身が隙間のぶん足りなくなった。</summary>
    [Fact]
    public void 段の高さは隙間を含む()
    {
        var node = Layouts.Parse("t", "a card 0 0 100 300 repeat=x gap=16").Roots[0];
        Assert.Equal(316f, Layouts.StepOf(node));

        var told = Layouts.Parse("t", "a card 0 0 100 300 repeat=x gap=16 rows=320").Roots[0];
        Assert.Equal(320f, Layouts.StepOf(told));
    }

    // ── 書き方の間違いは読み込みで落とす ──────────────

    [Fact]
    public void 字下げが奇数なら落とす()
    {
        Assert.Throws<ArgumentException>(() => Layouts.Parse("t", "box box 0 0 10 10\n a label 0 0 5 5"));
    }

    [Fact]
    public void タブは落とす()
    {
        Assert.Throws<ArgumentException>(() => Layouts.Parse("t", "box box 0 0 10 10\n\ta label 0 0 5 5"));
    }

    [Fact]
    public void 欄が足りなければ落とす()
    {
        Assert.Throws<ArgumentException>(() => Layouts.Parse("t", "a label 0 0 100"));
    }

    [Fact]
    public void 数でなければ落とす()
    {
        Assert.Throws<ArgumentException>(() => Layouts.Parse("t", "a label 0 0 ひゃく 40"));
    }

    [Fact]
    public void 付け足しがkey_value形式でなければ落とす()
    {
        Assert.Throws<ArgumentException>(() => Layouts.Parse("t", "a label 0 0 100 40 size"));
    }

    /// <summary>⚠️ **巻物の中の「入れ子の箱」の中身**も、画面より下に在ってよい。
    ///
    /// ⭐ 巻物かどうかは「直近の親」ではなく「**祖先のどこかに巻物があるか**」で決まる。
    /// ⚠️ 2026-08-22 の初版は「自分が巻物か」しか渡しておらず、
    /// 巻物 → 箱 → 字 の3段になった瞬間に「画面の外」の嘘を出した。</summary>
    [Fact]
    public void 巻物の中の入れ子でも画面の外と言わない()
    {
        var fine = Layouts.Parse("t", @"
s scroll   0 0 400 200
  box box  0 1800 400 300
    a label 0 150 400 40
");
        Assert.Equal(new System.Collections.Generic.List<string>(), Layouts.Faults(fine));
    }

    /// <summary>⚠️ **巻物の外では、いままでどおり落とす。**
    /// ⭐ 見逃しを広げすぎていないことの裏取り。</summary>
    [Fact]
    public void 巻物の外なら画面の外を見つける()
    {
        var bad = Layouts.Parse("t", @"
box box    0 1800 400 300
  a label  0 10 400 40
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("画面の外"));
    }

    /// <summary>⚠️ 画面の大きさは View の `Ui` と同じ数でなければ、検査が嘘になる。</summary>
    [Fact]
    public void 画面の大きさがViewと揃っている()
    {
        Assert.Equal(1080f, Layouts.ScreenWidth);
        Assert.Equal(1920f, Layouts.ScreenHeight);
        Assert.Equal(112f, Layouts.TapHeight);
    }
}
