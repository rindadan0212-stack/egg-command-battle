using System;
using System.IO;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>戦闘の狙い先（`tap=aim`・2026-08-29・作者の指示「ターゲットしていることが
/// わかるように（敵味方両方）」）。
///
/// ⭐ 中身の規則（選んだ相手に飛ぶ・倒れていたら自動へ戻す）は **Core が持っている**ので、
/// そこは本物の <see cref="Battle"/> を動かして確かめる。
/// ⚠️ 画面側（`Deeds`/`Sheets`）は `EggCommand.Tests` に**コンパイルされない**（csproj で
/// `&lt;None&gt;` として字だけ運ばれる）ので、`TapCatalogTests`/`HoldCatalogTests` と同じ流儀で
/// **ソースを字として読み直して**、目に見えない形で壊れると困る2点だけ杭を打つ。</summary>
public class AimTargetTests
{
    private static readonly string Dir = Path.Combine(AppContext.BaseDirectory, "websrc");

    // ── Core の規則（本物を動かす）─────────────────────────

    /// <summary>⭐ **選んだ相手に飛ぶ。**⚠️ 選ばないときの既定は「残 HP の低い相手」なので、
    /// わざと**満タンの相手**を選んで、既定と違う先が返ることを確かめる。</summary>
    [Fact]
    public void 選んだ敵に飛ぶ()
    {
        var state = Fight();
        var actor = First(state, Side.Ally);
        var chosen = Last(state, Side.Enemy);
        // ⚠️ 既定が chosen を選ばないように、別の敵を一番弱らせておく
        var weakest = First(state, Side.Enemy);
        weakest.Hp = 1;

        var hit = Battle.TargetsFor(state, actor, Target.EnemyOne, chosen);
        Assert.Single(hit);
        Assert.Equal(chosen.Key, hit[0].Key);

        // ⭐ 選ばなければ従来どおり（残 HP の低い相手）── ここが変わっていないことも一緒に固定
        var auto = Battle.TargetsFor(state, actor, Target.EnemyOne, null);
        Assert.Single(auto);
        Assert.Equal(weakest.Key, auto[0].Key);
    }

    /// <summary>⭐ 味方への技も同じ（`AllyOne`）。</summary>
    [Fact]
    public void 選んだ味方に飛ぶ()
    {
        var state = Fight();
        var actor = First(state, Side.Ally);
        var chosen = Last(state, Side.Ally);
        actor.Hp = 1;   // ⚠️ 既定（一番弱った味方）は chosen 以外になるように

        var hit = Battle.TargetsFor(state, actor, Target.AllyOne, chosen);
        Assert.Single(hit);
        Assert.Equal(chosen.Key, hit[0].Key);
    }

    /// <summary>🔴 **倒れた相手を狙っていても壊れない。**⭐ 黙って自動の狙いへ戻る
    /// （狙い先が消えた拍で技が不発になったり、死体を殴り続けたりしない）。</summary>
    [Fact]
    public void 倒れた狙い先は自動へ戻る()
    {
        var state = Fight();
        var actor = First(state, Side.Ally);
        var chosen = Last(state, Side.Enemy);
        chosen.Hp = 0;                          // 狙っていた相手が倒れた
        var alive = First(state, Side.Enemy);
        alive.Hp = 1;                           // いま一番弱っているのはこちら

        var hit = Battle.TargetsFor(state, actor, Target.EnemyOne, chosen);
        Assert.Single(hit);
        Assert.Equal(alive.Key, hit[0].Key);
    }

    /// <summary>🔴 **技の向きと食い違う狙いは黙って読み替える**（禁じない・撃たせる）。
    /// ⚠️ 敵を選んだまま味方への技を押しても、敵に回復が乗らない。
    /// ⭐ 「選ぶ」と「押す」が別の操作である以上、食い違いは必ず起きる ── そこで止めると
    /// 「押しても何も起きない」になるので、**自動の狙いへ落として撃たせる**。</summary>
    [Fact]
    public void 側が食い違う狙いは無視される()
    {
        var state = Fight();
        var actor = First(state, Side.Ally);
        var hurt = Last(state, Side.Ally);
        hurt.Hp = 1;

        // 敵を選んだまま「味方1体」の技 → 味方（一番弱った者）へ落ちる
        var hit = Battle.TargetsFor(state, actor, Target.AllyOne, Last(state, Side.Enemy));
        Assert.Single(hit);
        Assert.Equal(Side.Ally, hit[0].Side);
        Assert.Equal(hurt.Key, hit[0].Key);
    }

    // ── 画面側（字として読む）───────────────────────────

    /// <summary>🔴 **戦いの出入りで狙いを捨てる。**
    ///
    /// ⚠️ <see cref="Unit.Key"/> は `ally-0`/`enemy-2` という**席の名前**なので、次の戦いにも
    /// 同じ鍵が居る ── 捨て忘れると、選び直していないのに前の戦いの席がそのまま狙われ、
    /// **別人が的になる**（画面には印が出るので気づけない類の事故）。</summary>
    [Fact]
    public void 戦いの出入りで狙いを捨てる()
    {
        string body = Between(File.ReadAllText(Path.Combine(Dir, "Deeds.cs")),
            "private static void Rewind(Shell s)", "あきらめる。");
        Assert.Contains("s.AimFoe = null", body);
        Assert.Contains("s.AimAlly = null", body);
    }

    /// <summary>⭐ **印は生きている体にだけ出す。**⚠️ 倒れた体に印が残ると、
    /// 「狙っているのに当たらない」に見える（実際は自動へ戻っている）。</summary>
    [Fact]
    public void 印は生きている体にだけ出す()
    {
        string src = File.ReadAllText(Path.Combine(Dir, "Sheets.cs"));
        Assert.Contains("bool aimed = alive &&", src);
    }

    // ── 道具 ───────────────────────────────────────────

    /// <summary>⚠️ 端（見つからない）は大声で落とす ── 黙って空文字を返すと、
    /// 上の `Contains` が「無いのに通った」ことになる。</summary>
    private static string Between(string src, string from, string to)
    {
        int start = src.IndexOf(from, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Deeds.cs: 「{from}」が見つからない（検査の前提が崩れた）");
        int end = src.IndexOf(to, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Deeds.cs: 「{to}」が見つからない（探索範囲の終端が決められない）");
        return src.Substring(start, end - start);
    }

    /// <summary>戦いを1つ作る。⚠️ 種は固定（この検査は乱数に依らない所だけを見る）。
    /// ⭐ 相手は**試練**の顔ぶれ ── 巣の護りは1体のこともあり、それでは
    /// 「選んだ先」と「自動の先」が同じになって、この検査が何も見なくなる。</summary>
    private static BattleState Fight()
    {
        var game = Games.NewGame(2026_08_29);
        return Battle.CreateBattle(Games.PartyOf(game), Trials.PartyOf(Trials.All[0]));
    }

    /// <summary>片側を枠順で並べる。⚠️ **枠番号を決め打ちしない** ── 巣の顔ぶれが変われば
    /// 体数も変わるので、「1体目」と「最後の1体」で指す。
    /// ⭐ 2体に満たなければ大声で落とす（1体だと「選んだ先」と「自動の先」が同じになり、
    /// この検査は何も見ていないことになる）。</summary>
    private static System.Collections.Generic.List<Unit> Line(BattleState s, Side side)
    {
        var list = new System.Collections.Generic.List<Unit>();
        foreach (var u in s.Units) if (u.Side == side) list.Add(u);
        list.Sort((a, b) => a.Slot - b.Slot);
        Assert.True(list.Count >= 2, $"{side} が {list.Count} 体しか居ない（検査の前提が崩れた）");
        return list;
    }

    private static Unit First(BattleState s, Side side) => Line(s, side)[0];

    private static Unit Last(BattleState s, Side side)
    {
        var line = Line(s, side);
        return line[line.Count - 1];
    }
}
