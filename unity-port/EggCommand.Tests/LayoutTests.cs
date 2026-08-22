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

    // ── `when=`（2026-08-22 に足した1語）──────────────

    [Fact]
    public void 条件の名前と向きが読める()
    {
        var l = Layouts.Parse("t", @"
a label 0 0 100 40 when=有る
b label 0 60 100 40 when=!有る
c label 0 120 100 40
");
        Assert.Equal("有る", Layouts.WhenOf(l.Roots[0]));
        Assert.False(Layouts.WhenNot(l.Roots[0]));

        Assert.Equal("有る", Layouts.WhenOf(l.Roots[1]));
        Assert.True(Layouts.WhenNot(l.Roots[1]));

        Assert.Null(Layouts.WhenOf(l.Roots[2]));   // ⭐ 無ければ常に出す
    }

    /// <summary>⭐ **条件で入れ替わる2つは、同時には出ない。**
    /// ⚠️ 見ないと「重なっている」と誤って落とす。</summary>
    [Fact]
    public void 排他な2つの重なりは落とさない()
    {
        var fine = Layouts.Parse("t", @"
box box    0 0 400 200
  a label  0 0 400 40 when=空
  b label  0 0 400 40 when=!空
");
        Assert.Equal(new System.Collections.Generic.List<string>(), Layouts.Faults(fine));
    }

    /// <summary>⚠️ **同じ向きなら、重なりは落とす。**⭐ 見逃しを広げすぎていないことの裏取り。</summary>
    [Fact]
    public void 同じ条件どうしの重なりは落とす()
    {
        var bad = Layouts.Parse("t", @"
box box    0 0 400 200
  a label  0 0 400 40 when=空
  b label  0 0 400 40 when=空
");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("字の重なり"));
    }

    [Fact]
    public void 条件の名前が空なら落とす()
    {
        var bad = Layouts.Parse("t", "a label 0 0 100 40 when=!");
        Assert.Contains(Layouts.Faults(bad), p => p.Contains("when= の名前が空"));
    }

    // ── `use=`（別の骨組みを部品として差す）────────────

    private static Layout Find(string name, params (string id, string text)[] parts)
    {
        foreach (var p in parts) if (p.id == name) return Layouts.Parse(p.id, p.text);
        return null;
    }

    [Fact]
    public void 部品が差し込まれる()
    {
        var main = Layouts.Parse("main", "slot box 100 200 400 300 use=part");
        var got = Layouts.Resolve(main, n => Find(n, ("part", @"a label 0 0 400 40
b label 0 60 400 40")));

        Assert.Single(got.Roots);
        Assert.Equal("slot", got.Roots[0].Name);
        // ⭐ 部品の中身が、差した枠の子になる
        Assert.Equal(2, got.Roots[0].Children.Count);
        // ⭐ 差した枠の名前を冠す（id が重ならないように）
        Assert.Equal("slot-a", got.Roots[0].Children[0].Name);
    }

    /// <summary>⭐ 差した枠の子は、部品の**後ろ**に並ぶ（順番で言える）。</summary>
    [Fact]
    public void 差した枠の子は部品の後ろに来る()
    {
        var main = Layouts.Parse("main", @"slot box 0 0 400 300 use=part
  own label 0 200 400 40");
        var got = Layouts.Resolve(main, n => Find(n, ("part", "inner label 0 0 400 40")));

        var kids = got.Roots[0].Children;
        Assert.Equal(2, kids.Count);
        Assert.Equal("slot-inner", kids[0].Name);
        Assert.Equal("own", kids[1].Name);
    }

    /// <summary>⚠️ **輪を作らせない。**⭐ 止まらなくなるので、名前ごと叱る。</summary>
    [Fact]
    public void 輪になっていたら落とす()
    {
        var a = Layouts.Parse("a", "x box 0 0 100 100 use=b");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Layouts.Resolve(a, n => Find(n, ("b", "y box 0 0 100 100 use=a"))));
        Assert.Contains("輪", ex.Message);
    }

    [Fact]
    public void 無い部品を差したら落とす()
    {
        var a = Layouts.Parse("a", "x box 0 0 100 100 use=どこにも無い");
        var ex = Assert.Throws<InvalidOperationException>(() => Layouts.Resolve(a, n => null));
        Assert.Contains("見つからない", ex.Message);
    }

    /// <summary>⚠️ **同じ部品を1画面で2度差すと、名前がそのまま重なる。**
    /// ⭐ web では名前が id になるので、重なった時点でどちらも指し示せない。
    /// 差した枠の名前を冠して避ける（配合は親札を左右2つ差す）。</summary>
    [Fact]
    public void 同じ部品を二度差しても名前が重ならない()
    {
        var main = Layouts.Parse("main", @"pa box 0 0 400 300 use=part
pb box 500 0 400 300 use=part");
        var got = Layouts.Resolve(main, n => Find(n, ("part", @"art pixel 0 0 100 100 bind=art
  in label 0 0 100 40")));

        Assert.Equal("pa-art", got.Roots[0].Children[0].Name);
        Assert.Equal("pb-art", got.Roots[1].Children[0].Name);
        // ⚠️ 冠は**中身すべて**に付く ── 根だけだと孫が重なる
        Assert.Equal("pa-in", got.Roots[0].Children[0].Children[0].Name);
        Assert.Equal("pb-in", got.Roots[1].Children[0].Children[0].Name);
    }

    /// <summary>⚠️ **差し込み口にも冠が要る。**⭐ 付けないと、配合の左右2枚が
    /// 同じ `bind=art` を持ち、どちらの親の絵か言えなくなる。</summary>
    [Fact]
    public void 差し込み口にも冠が付く()
    {
        var main = Layouts.Parse("main", "pa box 0 0 400 300 use=part");
        var got = Layouts.Resolve(main, n => Find(n, ("part",
            @"art pixel 0 0 100 100 bind=art tap=open when=有る
row card 0 120 100 40 repeat=stats max=6")));

        var art = got.Roots[0].Children[0];
        Assert.Equal("pa-art", art.Option("bind"));
        Assert.Equal("pa-open", art.Option("tap"));
        Assert.Equal("pa-有る", art.Option("when"));
        Assert.Equal("pa-stats", got.Roots[0].Children[1].Option("repeat"));
    }

    /// <summary>⚠️ 条件の `!` は先頭のまま。⭐ 冠は名前のほうに付く。</summary>
    [Fact]
    public void 冠は否定の印を壊さない()
    {
        var main = Layouts.Parse("main", "pa box 0 0 400 300 use=part");
        var got = Layouts.Resolve(main, n => Find(n, ("part", "a label 0 0 100 40 when=!開")));
        Assert.Equal("!pa-開", got.Roots[0].Children[0].Option("when"));
        Assert.Equal("pa-開", Layouts.WhenOf(got.Roots[0].Children[0]));
        Assert.True(Layouts.WhenNot(got.Roots[0].Children[0]));
    }

    // ── flow=down ── ⭐ 兄弟を上から詰める ──────────────

    /// <summary>⭐ **`上` は「その上に空ける隙間」になる。**</summary>
    [Fact]
    public void 詰めると上から順に並ぶ()
    {
        var got = Layouts.Parse("t", @"body box 0 0 400 600 flow=down
  a label 0 0 400 40
  b label 0 10 400 40
  c label 0 10 400 40");
        var tops = Layouts.TopsOf(got.Roots[0], null, null);
        Assert.Equal(new[] { 0f, 50f, 100f }, tops);
    }

    /// <summary>⚠️ **これが `flow=down` の目的そのもの。**⭐ 出さない子の高さぶんの
    /// 空白がそのまま残っていた（編成のレビュー指摘 2026-08-19）。</summary>
    [Fact]
    public void 出さない子は場所を取らない()
    {
        var got = Layouts.Parse("t", @"body box 0 0 400 600 flow=down
  a label 0 0 400 40 when=巣
  b label 0 10 400 40");
        var tops = Layouts.TopsOf(got.Roots[0], child => Layouts.WhenOf(child) == null, null);
        Assert.Equal(10f, tops[1]);   // ⭐ a が消えたぶん上へ詰まる
    }

    /// <summary>⭐ 繰り返しは**段数ぶん**場所を取る。</summary>
    [Fact]
    public void 詰めるとき繰り返しは段数ぶん場所を取る()
    {
        var got = Layouts.Parse("t", @"body box 0 0 400 900 flow=down
  a card 0 0 100 50 repeat=x cols=2 rows=60 max=6
  b label 0 10 400 40");
        // 6個 ÷ 2列 = 3段 → (3-1)×60 + 50 = 170
        Assert.Equal(180f, Layouts.TopsOf(got.Roots[0], null, null)[1]);
    }

    /// <summary>⚠️ 🔴 **入れ替わる2つを詰める中に置かせない。**
    /// ⭐ 同時には出ないので、検査が数えすぎて嘘の位置になる。</summary>
    [Fact]
    public void 詰める中の入れ替わりを落とす()
    {
        var bad = Layouts.Parse("t", @"body box 0 0 400 600 flow=down
  a label 0 0 400 40 when=開
  b label 0 0 400 40 when=!開");
        Assert.Contains(Layouts.Faults(bad), f => f.Contains("入れ替わる2つ"));
    }

    /// <summary>⚠️ 綴り違いが黙って「詰めない」に落ちると、重なった画面が出る。</summary>
    [Fact]
    public void 知らないflowを落とす()
    {
        var bad = Layouts.Parse("t", "a box 0 0 400 600 flow=up");
        Assert.Contains(Layouts.Faults(bad), f => f.Contains("flow="));
    }

    /// <summary>⚠️ **詰めた位置で重なりを見る。**⭐ 骨組みの `上` で比べると、
    /// 詰める中は全部が同じ位置に見えて偽の重なりが出る。</summary>
    [Fact]
    public void 詰めた中は偽の重なりを出さない()
    {
        var fine = Layouts.Parse("t", @"body box 0 0 400 600 flow=down
  a label 0 0 400 40
  b label 0 0 400 40
  c label 0 0 400 40");
        Assert.Equal(new System.Collections.Generic.List<string>(), Layouts.Faults(fine));
    }

    // ── text= ── ⭐ 動かない字は骨組みに置く ────────────

    /// <summary>⭐ **行末まで全部が字。**⚠️ 引用符もエスケープも要らない。</summary>
    [Fact]
    public void 動かない字が行末まで読める()
    {
        var got = Layouts.Parse("t", "head label 0 0 400 40 size=28 text=技を鍛える　＝　たまごを使う");
        Assert.Equal("技を鍛える　＝　たまごを使う", got.Roots[0].Option("text"));
        Assert.Equal("28", got.Roots[0].Option("size"));
    }

    /// <summary>⚠️ **空白で切ってから繋ぎ直さない。**⭐ 二重空白が失われる。</summary>
    [Fact]
    public void 字の中の空白がそのまま残る()
    {
        var got = Layouts.Parse("t", "a label 0 0 400 40 text=Lv 1  /  Lv 40");
        Assert.Equal("Lv 1  /  Lv 40", got.Roots[0].Option("text"));
    }

    /// <summary>⚠️ `text=` より後ろは全部字なので、`=` が入っていても壊れない。</summary>
    [Fact]
    public void 字の中の等号が付け足しに化けない()
    {
        var got = Layouts.Parse("t", "a label 0 0 400 40 text=素質 ＋ 強化 = 実値");
        Assert.Equal("素質 ＋ 強化 = 実値", got.Roots[0].Option("text"));
        Assert.Single(got.Roots[0].Options);
    }

    /// <summary>⭐ **`\n` だけは行替えとして読む。**⚠️ 骨組みは1部品1行なので、
    /// これが無いと2行の字が書けない（編成の「空き／（自動で埋まる）」）。</summary>
    [Fact]
    public void 字の中の行替えが読める()
    {
        var got = Layouts.Parse("t", @"a label 0 0 400 80 text=空き\n（自動で埋まる）");
        Assert.Equal("空き\n（自動で埋まる）", got.Roots[0].Option("text"));
    }

    [Fact]
    public void 空のtextは落とす()
    {
        var ex = Assert.Throws<ArgumentException>(() => Layouts.Parse("t", "a label 0 0 400 40 text="));
        Assert.Contains("text= が空", ex.Message);
    }

    /// <summary>⚠️ **字の出所は1つ。**⭐ 2つあると、勝つほうを描く側が決めることになる。</summary>
    [Fact]
    public void 字の出所が2つあったら落とす()
    {
        var got = Layouts.Parse("t", "a label 0 0 400 40 bind=name text=固定の字");
        Assert.Contains(Layouts.Faults(got), f => f.Contains("字の出所は1つ"));
    }

    /// <summary>⚠️ 字を出さない種類に書いても**どこにも出ない**。⭐ 黙って捨てない。</summary>
    [Fact]
    public void 字を出さない種類のtextを落とす()
    {
        var got = Layouts.Parse("t", "a pixel 0 0 100 100 text=出ない");
        Assert.Contains(Layouts.Faults(got), f => f.Contains("字を出さない"));
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
