using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>骨組みの不備検査 ── 形（<see cref="Fault"/> / <see cref="Box"/>）まで持たせた出口。
///
/// ⭐ **`LayoutTests` の「わざと壊す」が土台。**⚠️ ここが薄い包み
/// （<see cref="Layouts.Faults"/>）でなく本体（<see cref="Layouts.Inspect"/>）を直に見るのは、
/// `Fault` の `Kind` / `Boxes` / `Focus` はそちらにしか出ないため。</summary>
public class LayoutFaultTests
{
    /// <summary>種類ごとに1枚。⭐ **唯一の出所** ── 「字が変わっていないか」と
    /// 「全種類作れているか」の両方がこの1つの並びだけを読む（2箇所に書くと片方が古くなる）。
    /// ⚠️ 字は `git show HEAD:...Layout.cs`（この仕事の直前）の `problems.Add(...)` を
    /// そのまま写した ── 記憶で書くと空似のミスに気づけない。</summary>
    private static readonly (FaultKind Kind, string Id, string Src, string Text)[] Cases =
    {
        (FaultKind.UnknownKind, "t", "a wobble 0 0 100 40",
            "t/a: 知らない種類「wobble」"),
        (FaultKind.ZeroSize, "t", "a label 0 0 0 40",
            "t/a: 大きさが 0 以下（0x40）"),
        (FaultKind.EmptyWhenName, "t", "a label 0 0 100 40 when=!",
            "t/a: when= の名前が空"),
        (FaultKind.TextAndBind, "t", "a label 0 0 400 40 bind=name text=固定の字",
            "t/a: text= と bind= の両方がある（字の出所は1つ）"),
        (FaultKind.TextOnNonText, "t", "a pixel 0 0 100 100 text=出ない",
            "t/a: 「pixel」は字を出さないのに text= がある"),
        (FaultKind.HostWithChildren, "t",
            "board host 0 0 400 300\n  a label 0 0 400 40",
            "t/board: host の中に子がある（1個）── 書けるなら box にする"),
        (FaultKind.IconMissingSource, "t", "a icon 0 0 100 100",
            "t/a: icon に pic= も bind= も無い（何の絵か言えていない）"),
        (FaultKind.UnknownFlow, "t", "a box 0 0 400 600 flow=up",
            "t/a: flow=「up」は知らない（down だけ）"),
        (FaultKind.UnknownOption, "t", "a label 0 0 100 40 anchr=left",
            "t/a: 知らない付け足し「anchr=」"),
        (FaultKind.VeilNotFullScreen, "t", "v veil 0 0 500 500",
            "t/v: 覆いが画面いっぱいでない（0,0 500x500）── 隙間から後ろが押せる"),
        (FaultKind.NotSquare, "t", "a pixel 0 0 984 40 bind=art",
            "t/a: pixel は正方形で描かれる（984x40 と書いても 40 角になる）"),
        (FaultKind.OverflowParentX, "t",
            "box  box   0 0 100 100\n  in label 0 0 120 40",
            "t/in: 親の枠から横へはみ出し（子 左0 幅120 / 親 幅100）"),
        (FaultKind.OverflowParentY, "t",
            "box  box   0 0 100 100\n  in label 0 80 100 40",
            "t/in: 親の枠から縦へはみ出し（子 上80 高40 / 親 高100）"),
        (FaultKind.OffScreen, "t", "a label 1000 0 200 40",
            "t/a: 画面の外（1000,0 200x40）"),
        (FaultKind.TapTooShort, "t", "go button 0 0 400 84",
            "t/go: 押しどころの高さが 84。112 以上にする（指で押せない）"),
        (FaultKind.InvalidCols, "t", "cell card 0 0 100 40 repeat=x cols=0",
            "t/cell: cols= が 0（1以上）"),
        (FaultKind.ColsOverflow, "t",
            "s box    0 0 984 400\n  cell card 0 0 340 300 repeat=species cols=3 gap=16",
            "t/cell: 3列が親の幅に収まらない（左0 + 要る 1052 = 1052 / 親 984）"),
        (FaultKind.RepeatMissingMax, "t",
            "s box     0 0 984 400\n  cell card 0 0 317 100 repeat=species cols=3 gap=16",
            "t/cell: 巻物の外の繰り返しには max=（上限の個数）が要る"),
        (FaultKind.RepeatMaxOverflow, "t",
            "s box     0 0 984 400\n  cell card 0 0 317 100 repeat=species cols=3 gap=16 max=30",
            "t/cell: max=30 だと親の枠から縦へはみ出す（要る 1144 / 親 高400）"),
        (FaultKind.ExclusivePairInFlow, "t",
            "body box 0 0 400 600 flow=down\n  a label 0 0 400 40 when=開\n  b label 0 0 400 40 when=!開",
            "t/body: 詰める中に入れ替わる2つ「a」×「b」── 決め打ちの位置か、別の骨組みに置く"),
        (FaultKind.DuplicateName, "t",
            "box box    0 0 200 200\n  a label  0 0 100 40\n  a label  0 60 100 40",
            "t/box: 「a」が2つある"),
        (FaultKind.LabelOverlap, "t",
            "a label 0 0 100 40\nb label 0 20 100 40",
            "t/t: 字の重なり「a」×「b」"),
        (FaultKind.TapOverlap, "t",
            "a button 0 0 400 112\nb button 0 60 400 112",
            "t/t: 押しどころの重なり「a」×「b」── 片方に指が届かない"),
    };

    // ── 1. 字が変わっていないこと（+ Kind も正しい組であること）───

    [Fact]
    public void 種類ごとに字が今までと同じ()
    {
        foreach (var c in Cases)
        {
            var layout = Layouts.Parse(c.Id, c.Src);
            // ⭐ 薄い包みの出口（呼び出し側43箇所が見るのはこちら）
            Assert.Contains(c.Text, Layouts.Faults(layout));

            // ⚠️ 字だけでなく、その字が「期待した Kind」から出ていることも確かめる
            //    ── 文字列がたまたま一致しても Kind が違えば作り忘れと同じ。
            var hit = false;
            foreach (var f in Layouts.Inspect(layout))
                if (f.Kind == c.Kind && f.Text == c.Text) hit = true;
            Assert.True(hit, $"{c.Kind}: 期待した Kind と Text の組が無い（「{c.Text}」）");
        }
    }

    [Fact]
    public void 骨組みが無いときの字も同じ()
    {
        var faults = Layouts.Inspect(null);
        Assert.Single(faults);
        Assert.Equal(FaultKind.NoLayout, faults[0].Kind);
        Assert.Equal("骨組みが無い", faults[0].Text);
        Assert.Equal("骨組みが無い", Layouts.Faults(null)[0]);
    }

    // ── 2. FaultKind が全部出せること ─────────────────

    /// <summary>⚠️ **作り忘れをここで落とす。**`Enum.GetValues` を舐めて、
    /// <see cref="Cases"/>（+ 骨組み無し）のどれからも出ていない種類があれば失敗する。</summary>
    [Fact]
    public void 全てのFaultKindを1回は作れる()
    {
        var produced = new HashSet<FaultKind>();
        foreach (var c in Cases)
            foreach (var f in Layouts.Inspect(Layouts.Parse(c.Id, c.Src)))
                produced.Add(f.Kind);
        produced.Add(Layouts.Inspect(null)[0].Kind);   // NoLayout は Cases の外（Parse を通らない）

        foreach (FaultKind kind in Enum.GetValues(typeof(FaultKind)))
            Assert.True(produced.Contains(kind), $"{kind} を出す骨組みが Cases に無い（作り忘れ）");
    }

    // ── 3. Focus の数の検証（目分量でなく計算で）───────

    [Fact]
    public void 字の重なりのFocusは交差した矩形()
    {
        var f = Only(FaultKind.LabelOverlap, "t", "a label 0 0 100 40\nb label 0 20 100 40");
        AssertBox(0, 20, 100, 20, f.Focus.Value);   // y[20,40) が重なる帯
    }

    [Fact]
    public void 押しどころの重なりのFocusは交差した矩形()
    {
        var f = Only(FaultKind.TapOverlap, "t", "a button 0 0 400 112\nb button 0 60 400 112");
        AssertBox(0, 60, 400, 52, f.Focus.Value);
    }

    [Fact]
    public void 横はみ出しのFocusは親の外に出た帯()
    {
        var f = Only(FaultKind.OverflowParentX, "t",
            "box  box   0 0 100 100\n  in label 0 0 120 40");
        AssertBox(100, 0, 20, 40, f.Focus.Value);   // 親の右端(100)から20px
    }

    [Fact]
    public void 縦はみ出しのFocusは親の外に出た帯()
    {
        var f = Only(FaultKind.OverflowParentY, "t",
            "box  box   0 0 100 100\n  in label 0 80 100 40");
        AssertBox(0, 100, 100, 20, f.Focus.Value);   // 親の下端(100)から20px
    }

    [Fact]
    public void 画面の外のFocusは出た部分()
    {
        var f = Only(FaultKind.OffScreen, "t", "a label 1000 0 200 40");
        AssertBox(1080, 0, 120, 40, f.Focus.Value);   // 画面の右端(1080)から120px
    }

    [Fact]
    public void 押しどころ不足のFocusは足りない高さの帯()
    {
        var f = Only(FaultKind.TapTooShort, "t", "go button 0 0 400 84");
        AssertBox(0, 84, 400, 28, f.Focus.Value);   // 枠の下端(84)から28px（112-84）
    }

    [Fact]
    public void 正方形のFocusは短い辺の正方形()
    {
        var f = Only(FaultKind.NotSquare, "t", "a pixel 0 0 984 40 bind=art");
        AssertBox(0, 0, 40, 40, f.Focus.Value);
    }

    [Fact]
    public void 覆いのFocusはいちばん大きい隙間()
    {
        var f = Only(FaultKind.VeilNotFullScreen, "t", "v veil 0 0 500 500");
        // 右の隙間 580x1920=1,113,600 より 下の隙間 1080x1420=1,533,600 が大きい
        AssertBox(0, 500, 1080, 1420, f.Focus.Value);
    }

    [Fact]
    public void 列はみ出しのFocusははみ出す列の位置()
    {
        var f = Only(FaultKind.ColsOverflow, "t",
            "s box    0 0 984 400\n  cell card 0 0 340 300 repeat=species cols=3 gap=16");
        AssertBox(984, 0, 68, 300, f.Focus.Value);   // 親の右端(984)から 1052-984=68
    }

    [Fact]
    public void maxはみ出しのFocusは越える帯()
    {
        var f = Only(FaultKind.RepeatMaxOverflow, "t",
            "s box     0 0 984 400\n  cell card 0 0 317 100 repeat=species cols=3 gap=16 max=30");
        AssertBox(0, 400, 317, 744, f.Focus.Value);   // 親の下端(400)から 1144-400=744
    }

    /// <summary>⚠️ **形で示せないものを無理に埋めない。**⭐ Focus は null のままでよい
    /// （エディタは木の行だけ赤くする）。</summary>
    [Fact]
    public void 形で示せない不備はFocusがnull()
    {
        Assert.Null(Only(FaultKind.UnknownKind, "t", "a wobble 0 0 100 40").Focus);
        Assert.Null(Only(FaultKind.DuplicateName, "t",
            "box box    0 0 200 200\n  a label  0 0 100 40\n  a label  0 60 100 40").Focus);
        Assert.Null(Only(FaultKind.ExclusivePairInFlow, "t",
            "body box 0 0 400 600 flow=down\n  a label 0 0 400 40 when=開\n  b label 0 0 400 40 when=!開").Focus);
    }

    // ── 4. Boxes が絶対座標であることの証明 ────────────

    [Fact]
    public void Boxesは入れ子でも絶対座標()
    {
        // ⭐ 親 outer(50,60) + 子 in の相対(10,20) = 絶対(60,80)。
        //    ⚠️ ここが相対のままだと (10,20,100,40) のまま返ってしまう。
        var f = Only(FaultKind.UnknownKind, "t2",
            "outer box 50 60 400 300\n  in wobble 10 20 100 40");
        AssertBox(60, 80, 100, 40, f.Boxes[0]);
    }

    [Fact]
    public void 差し込まれた側の行番号はマイナス1のまま()
    {
        // ⚠️ use= で差した部品の中身は、原文の行を持たない（別ファイルの行だから）。
        //    ⭐ Lines はここを 0 に丸めず、-1 のまま運ぶ ── エディタが
        //    「掴めない不備」と区別するための唯一の手がかり。
        var main = Layouts.Parse("main", "slot box 0 0 400 300 use=part");
        var resolved = Layouts.Resolve(main, n => n == "part"
            ? Layouts.Parse("part", "bad wobble 0 0 100 40")
            : null);

        var f = Find(Layouts.Inspect(resolved), FaultKind.UnknownKind);
        Assert.NotNull(f);
        Assert.Equal(-1, f.Lines[0]);
        Assert.Contains("slot-bad", f.Text);   // ⭐ 差した枠の名前を冠している
    }

    // ── 5. 巻物の中の扱いが変わっていないこと ──────────

    [Fact]
    public void 巻物の中の縦溢れはInspectでも不備なし()
    {
        var fine = Layouts.Parse("t", "s scroll  0 0 400 200\n  a label 0 700 400 40");
        Assert.Empty(Layouts.Inspect(fine));
    }

    [Fact]
    public void 巻物の中でも横溢れはInspectで見つかる()
    {
        var bad = Layouts.Parse("t", "s scroll  0 0 400 200\n  a label 0 10 500 40");
        Assert.NotNull(Find(Layouts.Inspect(bad), FaultKind.OverflowParentX));
    }

    [Fact]
    public void 巻物の中の入れ子はInspectでも画面の外と言わない()
    {
        var fine = Layouts.Parse("t",
            "s scroll   0 0 400 200\n  box box  0 1800 400 300\n    a label 0 150 400 40");
        Assert.Empty(Layouts.Inspect(fine));
    }

    [Fact]
    public void 巻物の外はInspectでも画面の外が見つかる()
    {
        var bad = Layouts.Parse("t", "box box    0 1800 400 300\n  a label  0 10 400 40");
        Assert.NotNull(Find(Layouts.Inspect(bad), FaultKind.OffScreen));
    }

    // ── 補助 ───────────────────────────────────────

    private static Fault Only(FaultKind kind, string id, string src)
    {
        var found = Find(Layouts.Inspect(Layouts.Parse(id, src)), kind);
        Assert.NotNull(found);
        return found;
    }

    private static Fault Find(List<Fault> faults, FaultKind kind)
    {
        foreach (var f in faults) if (f.Kind == kind) return f;
        return null;
    }

    private static void AssertBox(float x, float y, float w, float h, Box box)
    {
        Assert.Equal(x, box.X);
        Assert.Equal(y, box.Y);
        Assert.Equal(w, box.W);
        Assert.Equal(h, box.H);
    }
}
