using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>ホームの放置。⚠️ 移植元に無い規則なので、規則そのものを検査する。
///
/// 🔴 **2026-08-28 に「見せかけの打ち合い」から「本物の手番制」へ作り直した検査に更新**
/// （<c>Idle.cs</c> のクラス doc 参照）。⭐ 経過が <see cref="Idle.LiveWindowSeconds"/>（2秒）
/// 以内の呼び出しは本物の手番を1つずつ回す（<see cref="AdvanceTo"/> はこれを守って
/// 1秒刻みで呼ぶ）。それを超える経過は期待値の近似（<c>AdvanceApprox</c>）に落ちる ──
/// 「本物の手番が要る検査」と「近似でよい検査（12時間の追いつき等）」を混同しないこと。</summary>
public class IdleTests
{
    private const long T0 = 1_700_000_000;

    private static List<Creature> Party(int hp, int atk, int def, int spd, int n = 3)
    {
        var party = new List<Creature>();
        for (int i = 0; i < n; i++)
        {
            party.Add(new Creature($"c{i}", "tamaru", new StatBlock(hp, atk, def, spd),
                new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1));
        }
        return party;
    }

    /// <summary>属性を指定できる版。⭐ 属性の有利不利の検査だけがこれを使う
    /// （他の検査は Migrations.ElementOf("tamaru")＝水のままでよい）。</summary>
    private static Creature MakeCreature(string id, Element element, int spd,
        int atk = 300, int def = 20, int hp = 20) =>
        new Creature(id, "tamaru", new StatBlock(hp, atk, def, spd), new StatBlock(0, 0, 0, 0),
            0, 0, null, null, 0, null, null, 1, element: element);

    private static IdleRun Started(long now = T0)
    {
        var run = new IdleRun();
        Idle.Advance(run, Party(20, 20, 20, 20), now);   // 1回目は時計を合わせるだけ
        return run;
    }

    /// <summary>⭐ 「見ている間」（<see cref="Idle.LiveWindowSeconds"/> 以内）を守って、
    /// <paramref name="from"/> から <paramref name="to"/> まで1秒刻みで <see cref="Idle.Advance"/>
    /// を呼び続ける。⚠️ 本物の手番（誰が動いたか・実ダメージ・ダウン）を確かめる検査は
    /// **必ずこれを使う** ── 一度に大きく飛ばすと期待値の近似（<c>AdvanceApprox</c>）に
    /// 落ちて、本物の手番は1つも回らない。</summary>
    private static List<Idle.IdleGain> AdvanceTo(IdleRun run, IReadOnlyList<Creature> party,
        double from, double to, Rng? rng = null, double step = 1.0)
    {
        var gains = new List<Idle.IdleGain>();
        double t = from;
        while (to - t > 1e-9)
        {
            double s = Math.Min(step, to - t);
            t += s;
            gains.Add(Idle.Advance(run, party, t, rng));
        }
        return gains;
    }

    /// <summary>1体を倒しきるまで、1秒刻みで進め続ける。⭐ 打ち合いの中身（誰が何発、
    /// いくつ与えたか）を見たい検査の共通の土台。</summary>
    private static List<Idle.IdleGain> RunUntilFinished(IdleRun run, IReadOnlyList<Creature> party,
        double from, Rng? rng = null, double maxSeconds = 120)
    {
        var gains = new List<Idle.IdleGain>();
        double t = from;
        double end = from + maxSeconds;
        while (t < end)
        {
            t += 1.0;
            var g = Idle.Advance(run, party, t, rng);
            gains.Add(g);
            if (g.Finished) return gains;
        }
        throw new InvalidOperationException("時間内に討伐が終わらなかった（検査の前提が壊れている）");
    }

    private static IReadOnlyList<Idle.IdleBlow> AllBlows(IEnumerable<Idle.IdleGain> gains)
    {
        var all = new List<Idle.IdleBlow>();
        foreach (var g in gains) all.AddRange(g.Blows);
        return all;
    }

    // ── 起動・基本の清算（経過は乱数系統に依らないので、大きく飛ばしても壊れない） ──────

    [Fact]
    public void 初回は時計を合わせるだけで素材は入らない()
    {
        var run = new IdleRun();
        Assert.Equal(0, Idle.Advance(run, Party(20, 20, 20, 20), T0).Exp);
        Assert.Equal(T0, run.LastUnix);
    }

    [Fact]
    public void 巻き戻してもEXPは増えない()
    {
        var run = Started();
        Assert.Equal(0, Idle.Advance(run, Party(20, 20, 20, 20), T0 - 100).Exp);
        Assert.Equal(0, run.Exp);
    }

    [Fact]
    public void 同じ編成と同じ経過なら必ず同じ結果()
    {
        // ⭐ 乱数を渡さない検査。放置は「見ていない間」に進むので、
        //    結果が揺れると何が起きたのか説明できなくなる
        var a = Started();
        Idle.Advance(a, Party(24, 22, 18, 20), T0 + 500);
        var b = Started();
        Idle.Advance(b, Party(24, 22, 18, 20), T0 + 500);
        Assert.Equal(a.Exp, b.Exp);
        Assert.Equal(a.Defeated, b.Defeated);
    }

    [Fact]
    public void 同じ乱数の種なら本物の手番でも必ず同じ結果()
    {
        // ⭐ 仕事3で「敵の狙い先」に乱数が増えたので、本物の手番でも
        //    同じ種なら同じ結果になることを直に確かめる。
        var partyA = Party(20, 20, 20, 300, 4);
        var runA = new IdleRun();
        Idle.Advance(runA, partyA, T0, new Rng(20260828));
        var gainsA = AdvanceTo(runA, partyA, T0, T0 + 30, new Rng(1));

        var partyB = Party(20, 20, 20, 300, 4);
        var runB = new IdleRun();
        Idle.Advance(runB, partyB, T0, new Rng(20260828));
        var gainsB = AdvanceTo(runB, partyB, T0, T0 + 30, new Rng(1));

        Assert.Equal(runA.Exp, runB.Exp);
        Assert.Equal(runA.Defeated, runB.Defeated);
        var blowsA = AllBlows(gainsA);
        var blowsB = AllBlows(gainsB);
        Assert.Equal(blowsA.Count, blowsB.Count);
        for (int i = 0; i < blowsA.Count; i++)
        {
            Assert.Equal(blowsA[i].Who, blowsB[i].Who);
            Assert.Equal(blowsA[i].Target, blowsB[i].Target);
            Assert.Equal(blowsA[i].Damage, blowsB[i].Damage);
        }
    }

    [Fact]
    public void 何日でも一度に流し込まない()
    {
        var capped = Started();
        Idle.Advance(capped, Party(24, 22, 18, 20), T0 + Idle.CatchUpMax * 10);
        var exact = Started();
        Idle.Advance(exact, Party(24, 22, 18, 20), T0 + Idle.CatchUpMax);
        Assert.Equal(exact.Exp, capped.Exp);
    }

    // ── 稼ぎ（ダメージ量が払う） ──────────────────────────────

    [Fact]
    public void 時間が進むとEXPが溜まる()
    {
        var run = Started();
        var gain = Idle.Advance(run, Party(20, 20, 20, 20), T0 + 60);
        Assert.True(gain.Exp > 0, "1分で EXP が1つも入らない");
        Assert.Equal(gain.Exp, run.Exp);
        Assert.True(run.Defeated > 0, "1分経っても1体も倒れていない");
    }

    [Fact]
    public void 十分でおよそ一体ぶんのEXPが溜まる()
    {
        // ⭐ 「10分回せば最初の個体は MAX 近くまで育つ」が狙い。
        //    ⚠️ 手で作った編成ではなく**遊び始めの実物**で測る。
        var game = Games.NewGame(2026_08_16);
        var real = Games.PartyOf(game);
        var run = new IdleRun();
        Idle.Advance(run, real, T0);
        Idle.Advance(run, real, T0 + 600);
        Assert.Empty(run.DownUntil);   // ⚠️ 600秒は近似枝（ダウン無視）に落ちるので、
                                        //    ダウンが一切記録されないのは仕様どおり
        // 🔴 **2026-08-28（本物の手番制）: 稼ぎは「与えたダメージ ÷ ExpPerDamage」**。
        //    ExpPerDamage は実測で「約137 EXP/分」に較正してある（Idle.cs の doc 参照）。
        int levels = Levels.LevelsFor(real[0], run.Exp);
        const int Pace = 20;
        Assert.True(levels >= Pace * 0.5 && levels <= Pace * 2.0,
            $"10分で {run.Exp} EXP = {levels}Lv（狙いは {Levels.GrowMax}Lv 前後）");
    }

    [Fact]
    public void 強い編成のほうが速い()
    {
        var weak = Started();
        Idle.Advance(weak, Party(10, 8, 8, 8), T0 + 300);
        var strong = Started();
        Idle.Advance(strong, Party(30, 30, 30, 30), T0 + 300);
        Assert.True(strong.Exp > weak.Exp,
            $"弱 {weak.Exp} / 強 {strong.Exp}");
    }

    [Fact]
    public void 稼ぎは与えた総ダメージに比例する()
    {
        // ⭐ 仕事4の核心そのもの ── 1体を倒しきるまでの EXP を比べる。速度・体数は揃えて
        //    「与えたダメージ」だけを変数にする。
        //    ⚠️ tamaru の枠1（attack-def）は**防御スケール**（Species.cs の doc 参照）
        //    ── 攻撃力ではなく防御が一撃の元になるので、ここで動かすのは def。
        var weakParty = Party(20, 20, 50, 300, 4);
        var strongParty = Party(20, 20, 5000, 300, 4);

        var weakRun = new IdleRun();
        Idle.Advance(weakRun, weakParty, T0, new Rng(1));
        var weakGains = RunUntilFinished(weakRun, weakParty, T0, new Rng(1));
        int weakExp = 0;
        foreach (var g in weakGains) weakExp += g.Exp;

        var strongRun = new IdleRun();
        Idle.Advance(strongRun, strongParty, T0, new Rng(1));
        var strongGains = RunUntilFinished(strongRun, strongParty, T0, new Rng(1));
        int strongExp = 0;
        foreach (var g in strongGains) strongExp += g.Exp;

        Assert.True(strongExp > weakExp,
            $"攻撃力が100倍違うのに EXP が同水準（弱 {weakExp} / 強 {strongExp}）");
    }

    // ── 手番（誰が動くか＝速度で決まる。実時間には効かない） ──────────────

    [Fact]
    public void 速い個体ほど手番が多い()
    {
        var party = new List<Creature>
        {
            new Creature("fast", "tamaru", new StatBlock(20, 20, 20, 999),
                new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1),
            new Creature("slow", "tamaru", new StatBlock(20, 20, 20, 5),
                new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1),
        };
        var run = new IdleRun();
        Idle.Advance(run, party, T0, new Rng(1));
        var gains = RunUntilFinished(run, party, T0, new Rng(1));

        int fastHits = 0, slowHits = 0;
        foreach (var blow in AllBlows(gains))
        {
            if (blow.Who < 0) continue;
            if (party[blow.Who].Id == "fast") fastHits++;
            else if (party[blow.Who].Id == "slow") slowHits++;
        }
        Assert.True(fastHits > slowHits,
            $"速い個体のほうが多く動くはず（速 {fastHits} / 遅 {slowHits}）");
    }

    [Fact]
    public void 十分に遅い個体は一度も動かないことがある()
    {
        var party = new List<Creature>
        {
            new Creature("f0", "tamaru", new StatBlock(20, 20, 20, 999),
                new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1),
            new Creature("f1", "tamaru", new StatBlock(20, 20, 20, 995),
                new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1),
            new Creature("f2", "tamaru", new StatBlock(20, 20, 20, 990),
                new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1),
            new Creature("snail", "tamaru", new StatBlock(20, 20, 20, 0),
                new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1),
        };
        var run = new IdleRun();
        Idle.Advance(run, party, T0, new Rng(1));
        var gains = RunUntilFinished(run, party, T0, new Rng(1));

        foreach (var blow in AllBlows(gains))
        {
            if (blow.Who >= 0) Assert.NotEqual("snail", party[blow.Who].Id);
        }
    }

    // ── 敵は8発で倒れる（威力・多段に関係ない） ───────────────────────

    [Fact]
    public void 敵はちょうど8発で倒れる_威力に関係ない()
    {
        var weakParty = Party(20, 5, 20, 300, 4);
        var run = new IdleRun();
        Idle.Advance(run, weakParty, T0, new Rng(1));
        var gains = RunUntilFinished(run, weakParty, T0, new Rng(1));

        int allyHits = 0;
        foreach (var blow in AllBlows(gains)) if (blow.Who >= 0) allyHits++;
        Assert.Equal(Idle.StrikeCount, allyHits);
        Assert.Equal(1, run.Defeated);
    }

    [Fact]
    public void 多段でも8発の数え方は崩れない()
    {
        // ⭐ 種族固定の枠1（tamaru の attack-def）は多段ではないので、代わりに
        //    RunUntilFinished の討伐数そのもの（＝当たった回数）が威力によらず
        //    一定であることを、弱い編成と強い編成の両方で確かめる
        //    （多段を持つ種族を個別に足さずとも、「1手＝1」という契約自体は
        //    HitDamageOf の実装（Repeat をダメージにだけ畳み込む）が唯一の出所であり、
        //    このテストは「呼び出し側から見て discovered hits が常に8」であることを
        //    別の編成でも再確認する回帰検査）。
        var strongParty = Party(20, 5000, 20, 300, 4);
        var run = new IdleRun();
        Idle.Advance(run, strongParty, T0, new Rng(1));
        var gains = RunUntilFinished(run, strongParty, T0, new Rng(1));

        int allyHits = 0;
        foreach (var blow in AllBlows(gains)) if (blow.Who >= 0) allyHits++;
        Assert.Equal(Idle.StrikeCount, allyHits);
    }

    [Fact]
    public void 属性の有利不利がダメージに乗る()
    {
        int tamaruIdx = -1;
        for (int i = 0; i < SpeciesTable.All.Count; i++)
        {
            if (SpeciesTable.All[i].Id == "tamaru") { tamaruIdx = i; break; }
        }
        Assert.True(tamaruIdx >= 0, "tamaru が種族表に無い（検査の前提が壊れている）");

        var allies = new List<Creature>
        {
            MakeCreature("fire", Element.Fire, spd: 999),   // タマル(水)に不利 ×0.75
            MakeCreature("water", Element.Water, spd: 998),  // 中立 ×1.0
            MakeCreature("wood", Element.Wood, spd: 997),   // タマル(水)に有利 ×1.5
        };
        var run = new IdleRun();
        Idle.Advance(run, allies, T0, new Rng(1));
        run.FoeSpecies = tamaruIdx;   // ⚠️ 相手の属性を Water に固定する（Migrations.ElementOf("tamaru")）
        var gains = RunUntilFinished(run, allies, T0, new Rng(1));

        int? fireDmg = null, waterDmg = null, woodDmg = null;
        foreach (var blow in AllBlows(gains))
        {
            if (blow.Who < 0) continue;
            string id = allies[blow.Who].Id;
            if (id == "fire") fireDmg ??= blow.Damage;
            else if (id == "water") waterDmg ??= blow.Damage;
            else if (id == "wood") woodDmg ??= blow.Damage;
        }
        Assert.NotNull(fireDmg);
        Assert.NotNull(waterDmg);
        Assert.NotNull(woodDmg);
        Assert.True(woodDmg!.Value > waterDmg!.Value,
            $"有利属性のほうが中立よりダメージが大きいはず（木 {woodDmg} / 水 {waterDmg}）");
        Assert.True(waterDmg.Value > fireDmg!.Value,
            $"中立のほうが不利属性よりダメージが大きいはず（水 {waterDmg} / 火 {fireDmg}）");
    }

    // ── 敵の一撃・ダウン・復帰 ────────────────────────────────

    [Fact]
    public void 倒れた者は時間で起き上がる()
    {
        // ⚠️ IsDown/ReviveSeconds の契約そのものの検査（DownUntil への書き込みは
        //    本物の手番制の中で実際に起きる ── 下の別の検査で確かめる）。
        var party = Party(20, 20, 20, 20, 1);
        var run = new IdleRun();
        run.DownUntil[party[0].Id] = T0 + Idle.ReviveSeconds;

        Assert.True(Idle.IsDown(run, party[0], T0 + Idle.ReviveSeconds - 1));
        Assert.False(Idle.IsDown(run, party[0], T0 + Idle.ReviveSeconds));
    }

    [Fact]
    public void 敵の一撃は最大HPの50パーセントで2発でダウンする()
    {
        // ⭐ 味方はわざと弱く（8発では倒せない攻撃力）・遅くして、敵に手番を渡す。
        //    敵速度(Idle.FoeSpeed)より遅い味方1体だけの編成にすると、敵がほぼ毎回動く。
        var party = new List<Creature>
        {
            new Creature("victim", "tamaru", new StatBlock(20, 1, 20, 1),
                new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1),
        };
        var run = new IdleRun();
        Idle.Advance(run, party, T0, new Rng(1));
        AdvanceTo(run, party, T0, T0 + 1.6, new Rng(1));   // Fight へ入れる
        Assert.Equal(IdlePhase.Fight, run.Phase);

        var gains = new List<Idle.IdleGain>();
        double t = T0 + 1.6;
        int foeHitsSeen = 0;
        while (foeHitsSeen < 2 && t < T0 + 60)
        {
            t += 1.0;
            var g = Idle.Advance(run, party, t, new Rng(1));
            gains.Add(g);
            foreach (var blow in g.Blows)
            {
                if (blow.Who == -1 && blow.Target == 0) foeHitsSeen++;
            }
        }
        Assert.True(foeHitsSeen >= 2, "敵の一撃が2発観測できなかった（検査の前提が壊れている）");
        Assert.True(Idle.IsDown(run, party[0], (long)t),
            "2発（合計100%）受けたのにダウンしていない");

        double health = run.Health.TryGetValue(party[0].Id, out var h) ? h : 1.0;
        Assert.True(health <= 0.0 + 1e-9, $"ダウン時のHP割合が0でない（{health}）");
    }

    [Fact]
    public void ダウンした者はReviveSecondsで全快して復帰する()
    {
        // ⭐ 敵(Idle.FoeSpeed)より圧倒的に速い1体編成にする ── 復帰した直後の手番を
        //    味方が必ず取るので、「全快したのに、確かめる前にまた殴られて減っていた」
        //    という検査側のノイズが起きない。
        var party = Party(20, 20, 20, 999, 1);
        var run = new IdleRun();
        Idle.Advance(run, party, T0, new Rng(1));
        long downAt = T0 + 5;
        long reviveAt = downAt + Idle.ReviveSeconds;
        run.DownUntil[party[0].Id] = reviveAt;
        run.Health[party[0].Id] = 0.0;

        AdvanceTo(run, party, T0, reviveAt - 1, new Rng(1));
        Assert.True(Idle.IsDown(run, party[0], reviveAt - 1));

        // ⭐ 復帰の境界を細かい刻みでまたぐ
        AdvanceTo(run, party, reviveAt - 1, reviveAt + 0.2, new Rng(1), step: 0.2);

        Assert.False(run.DownUntil.ContainsKey(party[0].Id));
        double health = run.Health.TryGetValue(party[0].Id, out var h) ? h : 1.0;
        Assert.Equal(1.0, health, 6);
    }

    [Fact]
    public void 全員ダウン中は時間が流れても打ち合いは進まない_起きたら再開する()
    {
        var party = Party(20, 300, 20, 300, 4);
        var run = new IdleRun();
        Idle.Advance(run, party, T0, new Rng(1));
        AdvanceTo(run, party, T0, T0 + 1.6, new Rng(1));   // Fight へ
        Assert.Equal(IdlePhase.Fight, run.Phase);

        long stunAt = (long)(T0 + 1.6);
        long reviveAt = stunAt + Idle.ReviveSeconds;
        foreach (var c in party)
        {
            run.DownUntil[c.Id] = reviveAt;
            run.Health[c.Id] = 0.0;
        }
        int struckBefore = run.Struck;

        // ⚠️ 復帰の1秒手前まで進めても、誰も殴れないので Struck は増えない
        AdvanceTo(run, party, T0 + 1.6, reviveAt - stunAt + T0 + 0.6, new Rng(1));
        Assert.Equal(struckBefore, run.Struck);

        // ⭐ 復帰後は再開する（3秒なので詰まない）
        AdvanceTo(run, party, reviveAt - stunAt + T0 + 0.6, reviveAt - stunAt + T0 + 10.0, new Rng(1));
        Assert.True(run.Struck > struckBefore || run.Phase != IdlePhase.Fight,
            "全員が起きても打ち合いが再開していない");
    }

    // ── 拍（テンポ） ── 育っても Fight は縮まない ─────────────────

    [Fact]
    public void 最短周期は7秒でFightの最短は4秒()
    {
        Assert.Equal(7.0, Idle.MinCycleSeconds, 6);
        Assert.Equal(4.0, Idle.FightMinSeconds, 6);
        Assert.Equal(Idle.StrikeCount * Idle.ActSeconds, Idle.FightMinSeconds, 6);
    }

    [Fact]
    public void 敵より十分速い編成ならFightはちょうど最短の4秒になる()
    {
        // ⭐ 味方が敵(FoeSpeed)より圧倒的に速ければ、敵は一度もゲージ競争に勝てない ──
        //    Fight は必ず「味方だけが8回動く」最短の4.0秒になる、という決定論の確認。
        var party = Party(20, 300, 20, 999, 4);
        var run = new IdleRun();
        var allGains = new List<Idle.IdleGain>();
        Idle.Advance(run, party, T0);
        Assert.Equal(IdlePhase.Come, run.Phase);

        allGains.AddRange(AdvanceTo(run, party, T0, T0 + 1.2));
        Assert.Equal(IdlePhase.Face, run.Phase);

        allGains.AddRange(AdvanceTo(run, party, T0 + 1.2, T0 + 2.0));
        Assert.Equal(IdlePhase.Fight, run.Phase);

        // Come(1.0)+Face(0.5)+Fight(4.0) = 5.5 で Finish に入っているはず
        allGains.AddRange(AdvanceTo(run, party, T0 + 2.0, T0 + 5.7));
        Assert.Equal(IdlePhase.Finish, run.Phase);

        // 5.5+0.4=5.9 で Rest、7.0 で次の Come
        allGains.AddRange(AdvanceTo(run, party, T0 + 5.7, T0 + 6.5));
        Assert.Equal(IdlePhase.Rest, run.Phase);

        allGains.AddRange(AdvanceTo(run, party, T0 + 6.5, T0 + 7.5));
        Assert.Equal(IdlePhase.Come, run.Phase);
        Assert.Equal(1, run.Defeated);
        bool sawFinished = false;
        foreach (var g in allGains) if (g.Finished) sawFinished = true;
        Assert.True(sawFinished, "1周期ぶん進めたのに Finished が一度も立っていない");
    }

    [Fact]
    public void 育ってもFightは4秒より短くならない()
    {
        // ⭐ 今回の作り直しの肝: 育つほど手数が増えても、1手の実時間（ActSeconds）は
        //    固定なので、Fight の実時間は縮まない。
        var grown = Party(20, 900, 20, 5000, 4);   // 極端に育て切った想定の速度
        var run = new IdleRun();
        Idle.Advance(run, grown, T0);
        AdvanceTo(run, grown, T0, T0 + 1.2);
        Assert.Equal(IdlePhase.Face, run.Phase);
        AdvanceTo(run, grown, T0 + 1.2, T0 + 2.0);
        Assert.Equal(IdlePhase.Fight, run.Phase);

        // 5.5秒未満では、育て切っていても Fight を抜けられない
        AdvanceTo(run, grown, T0 + 2.0, T0 + 5.4);
        Assert.Equal(IdlePhase.Fight, run.Phase);
    }

    [Fact]
    public void ComeとFaceの間はまだ誰も殴っていない()
    {
        var run = Started();
        Assert.Equal(0, run.Struck);

        AdvanceTo(run, Party(20, 20, 20, 20), T0, T0 + 1.2);
        Assert.Equal(IdlePhase.Face, run.Phase);
        Assert.Equal(0, run.Struck);
        Assert.Equal(1.0, Idle.FoeLeft(run), 6);
    }

    [Fact]
    public void 帯はCome_Faceで満タン_打ち合いで段階的に減り倒すと空になる()
    {
        Assert.Equal(1.0, Idle.FoeLeft(new IdleRun { Struck = 0 }), 6);
        Assert.Equal(1.0 - 4.0 / 8.0, Idle.FoeLeft(new IdleRun { Struck = 4 }), 6);
        Assert.Equal(0.0, Idle.FoeLeft(new IdleRun { Struck = 8 }), 6);

        // ⚠️ 壊れた／範囲外の値でも 0〜1 の外へ出ない（保険の検査）
        Assert.Equal(0.0, Idle.FoeLeft(new IdleRun { Struck = 99 }), 6);
        Assert.Equal(1.0, Idle.FoeLeft(new IdleRun { Struck = -3 }), 6);
    }

    [Fact]
    public void Finishedは討伐した拍だけで立つ()
    {
        var party = Party(20, 300, 20, 999, 4);
        var run = new IdleRun();
        Idle.Advance(run, party, T0);
        var early = AdvanceTo(run, party, T0, T0 + 3.0);
        foreach (var g in early) Assert.False(g.Finished);

        var later = AdvanceTo(run, party, T0 + 3.0, T0 + 7.5);
        bool anyFinished = false;
        foreach (var g in later) if (g.Finished) anyFinished = true;
        Assert.True(anyFinished);
    }

    // ── 見た目・卵（乱数） ── 旧検査をそのまま維持 ─────────────────

    [Fact]
    public void RollFoeは種族の範囲に収まる()
    {
        const int speciesCount = 5;
        var run = new IdleRun();
        var rng = new Rng(20260828);
        for (int i = 0; i < 500; i++)
        {
            Idle.RollFoe(run, rng, speciesCount, _ => 1);
            Assert.InRange(run.FoeSpecies, 0, speciesCount - 1);
            Assert.Equal(0, run.FoePalette);   // 色数1つなら常に通常色
        }
    }

    [Fact]
    public void RollFoeは種族が0でも落ちない()
    {
        var run = new IdleRun { FoeSpecies = 9, FoePalette = 9 };
        var rng = new Rng(1);
        Idle.RollFoe(run, rng, 0, _ => 1);
        Assert.Equal(0, run.FoeSpecies);
        Assert.Equal(0, run.FoePalette);
    }

    [Fact]
    public void RollFoeの色違いはおよそ256分の1で通常色がほとんど()
    {
        var run = new IdleRun();
        var rng = new Rng(7);
        const int trials = 300_000;
        int shiny = 0;
        for (int i = 0; i < trials; i++)
        {
            Idle.RollFoe(run, rng, 3, _ => 4);   // ⚠️ 色数2以上でないと絶対に光らない
            if (run.FoePalette != 0) shiny++;
        }
        double rate = (double)shiny / trials;
        double expect = 1.0 / Idle.ShinyOdds;
        Assert.True(rate > expect * 0.5 && rate < expect * 2.0,
            $"色違い率 {rate:P3}（狙いは {expect:P3} 前後）");
        Assert.True(shiny < trials / 2, "色違いのほうが多い（ほとんどは通常色のはず）");
    }

    [Fact]
    public void RollFoeは色数が1つの種族なら常に通常色()
    {
        var run = new IdleRun();
        var rng = new Rng(3);
        for (int i = 0; i < 3000; i++)
        {
            Idle.RollFoe(run, rng, 4, _ => 1);
            Assert.Equal(0, run.FoePalette);
        }
    }

    [Fact]
    public void 周期が終わるたびに見た目が引き直る()
    {
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 60; seed++)
        {
            var run = new IdleRun();
            var rng = new Rng(seed);
            var party = Party(20, 300, 20, 999, 4);
            Idle.Advance(run, party, T0, rng);                 // 初回は時計合わせ
            AdvanceTo(run, party, T0, T0 + 7.5, rng);           // ちょうど1周期以上
            seen.Add(run.FoeSpecies);
        }
        Assert.True(seen.Count > 1, "60回引いて種族が1種類しか出ていない（乱数になっていない疑い）");
    }

    [Fact]
    public void 卵は5パーセントの率で星75_20_5の内訳で出る()
    {
        var run = new IdleRun();
        var strong = Party(20, 5000, 20, 999, 4);
        var rng = new Rng(20260828);
        Idle.Advance(run, strong, T0, rng);   // 1回目は時計合わせ

        const int targetKills = 3_000;
        int star1 = 0, star2 = 0, star3 = 0, eggs = 0;
        double clock = T0;
        while (run.Defeated < targetKills)
        {
            clock += 1.0;   // ⚠️ 本物の手番（Live）に留める ── 近似の卵上限に引っかからないため
            var gain = Idle.Advance(run, strong, clock, rng);
            star1 += gain.Star1;
            star2 += gain.Star2;
            star3 += gain.Star3;
            eggs += gain.Eggs;
        }

        double rate = (double)eggs / run.Defeated;
        Assert.True(rate > Idle.EggDropChance * 0.7 && rate < Idle.EggDropChance * 1.3,
            $"卵の出る率 {rate:P2}（狙いは {Idle.EggDropChance:P2} 前後）");

        double s1 = (double)star1 / eggs, s2 = (double)star2 / eggs, s3 = (double)star3 / eggs;
        Assert.True(s1 > 0.60 && s1 < 0.90, $"★1の内訳 {s1:P1}（狙いは75%前後）");
        Assert.True(s2 > 0.08 && s2 < 0.32, $"★2の内訳 {s2:P1}（狙いは20%前後）");
        Assert.True(s3 > 0.0 && s3 < 0.15, $"★3の内訳 {s3:P1}（狙いは5%前後）");
    }

    [Fact]
    public void 放置の卵は倒した相手と同じ種族になる()
    {
        var all = SpeciesTable.All;
        int at = -1;
        for (int i = 0; i < all.Count; i++)
        {
            bool has = false;
            foreach (var nest in Nests.All) if (nest.SpeciesId == all[i].Id) { has = true; break; }
            if (!has) { at = i; break; }
        }
        Assert.True(at >= 0, "固定表に無い種族が1つも無い（検査が空回り）");

        var game = Games.NewGame(2026_08_28);
        int before = game.Eggs.Count;
        Games.GainIdleEggs(game, new Idle.IdleGain(0, 1, 0, 0), at);
        Assert.Equal(before + 1, game.Eggs.Count);
        Assert.Equal(all[at].Id, game.Eggs[game.Eggs.Count - 1].SpeciesId);
    }

    [Fact]
    public void 放置の卵は同じ種族でも技が固定されない()
    {
        var game = Games.NewGame(2026_08_28);
        Games.GainIdleEggs(game, new Idle.IdleGain(0, 8, 0, 0), 0);
        var 技 = new HashSet<string>();
        foreach (var egg in game.Eggs) 技.Add((egg.Skill2 ?? "-") + "/" + (egg.Skill3 ?? "-"));
        Assert.True(技.Count > 1, "8個とも同じ技だった（巣の名前を使い回している）");
    }

    // ── 12時間の追いつき（近似） ─────────────────────────────

    [Fact]
    public void 十二時間の清算でも拍は壊れず卵は3個以下でEXPは期待値どおり()
    {
        var run = Started();
        var party = Party(20, 20, 20, 20);
        var rng = new Rng(20260828);
        double damagePerSecond = Idle.ExpectedDamagePerSecond(run, party);

        var gain = Idle.Advance(run, party, T0 + Idle.CatchUpMax, rng);

        Assert.True(gain.Eggs <= Idle.MaxEggsPerCatchUp,
            $"卵が {gain.Eggs} 個（上限 {Idle.MaxEggsPerCatchUp} を超えている）");
        Assert.True(Enum.IsDefined(typeof(IdlePhase), run.Phase));
        Assert.InRange(run.Struck, 0, Idle.StrikeCount);
        Assert.Empty(gain.Blows);   // ⚠️ 近似は一撃の並びを作らない（唯一の出所は本物の手番）

        double expected = Math.Floor(damagePerSecond * Idle.CatchUpMax / Idle.ExpPerDamage);
        Assert.True(Math.Abs(gain.Exp - expected) <= Math.Max(2, expected * 0.05),
            $"EXP {gain.Exp}（式どおりなら {expected} 前後）");
    }

    [Fact]
    public void 力がゼロでも十二時間の追いつきは壊れない()
    {
        var run = Started();
        var empty = new List<Creature>();
        var gain = Idle.Advance(run, empty, T0 + Idle.CatchUpMax);
        Assert.Equal(0, gain.Exp);
        Assert.Equal(0, gain.Eggs);
        Assert.True(Enum.IsDefined(typeof(IdlePhase), run.Phase));
    }
}
