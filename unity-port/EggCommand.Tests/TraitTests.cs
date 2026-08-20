using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>特性。⚠️ **goldens では守れない。**
/// golden は TS を実走させて作るので、TS に存在しない特性は照合の対象外になる。
/// 特性が壊れても 112件は緑のままなので、ここが唯一の見張り。
///
/// ⭐ 筆頭の約束は「特性を持たない個体が1ビットも変わらないこと」。
/// 得意・不得意のときと同じ約束で、これが守れているかぎり移植の照合は生き続ける。</summary>
public class TraitTests
{
    /// <summary>特性と技だけを指定した個体。⚠️ 属性は全員 Fire に揃える
    /// （倍率が 1.0 になるので、測っているのが特性だけになる）。</summary>
    private static Creature Make(string id, string? traitId,
        string? skill2 = null, string? skill3 = null,
        int hp = 20, int atk = 20, int def = 20, int spd = 20)
    {
        return new Creature(id, "tamaru", new StatBlock(hp, atk, def, spd),
            new StatBlock(0, 0, 0, 0), 0, 0, skill2, skill3, 0, null, null, 1,
            null, null, Element.Fire, traitId);
    }

    private static BattleState Fight(Creature ally, Creature enemy) =>
        Battle.CreateBattle(new List<Creature> { ally }, new List<Creature> { enemy });

    private static Unit UnitOf(BattleState state, Side side)
    {
        foreach (var unit in state.Units)
        {
            if (unit.Side == side) return unit;
        }
        throw new System.InvalidOperationException($"{side} が居ない");
    }

    // ── 表そのもの ──────────────────────────────────

    [Fact]
    public void 表と戦闘が繋がっている()
    {
        Traits.Audit();
        Assert.Equal(Traits.All.Count, Traits.Wired);
    }

    // ── 畳み掛け（手番を報酬にする唯一の特性）────────────

    /// <summary>⭐ **弱化を通したら、そのまま続けてもう一度動ける。**
    ///
    /// ⚠️ **見張るのはゲージの値ではなく「次に動くのが誰か」。**
    /// 最初この特性は `actor.Gauge = GaugeMax`（代入）で書いてあった。
    /// ゲージの値だけを見る検査ならそれで通るが、実際には技の処理のあとに
    /// `PerformAction` が必ず `Gauge -= GaugeMax` するので、満タンがそっくり引かれて
    /// **繋がっているのに効果ゼロ**だった（`sim traits` で −1.5pt ＝ 何も起きていない）。
    /// ⭐ だからここは <see cref="Battle.NextActor"/> が同じ個体を返すことまで見る。</summary>
    [Fact]
    public void 畳み掛けは弱化を通すと続けて動ける()
    {
        // ⚠️ 素で必ず通る弱化（呪詛）を選ぶ。外れると条件を満たさず、検査が揺れる
        // ⚠️ **相手をはるかに速くする。**同じ速度だと、特性が無くても次に自分が回ってきて
        //    検査が通ってしまう（最初それで書いて、素の側の検査が落ちて気づいた）。
        var state = Fight(Make("a", Traits.Surge, "curse", spd: 1),
            Make("b", null, spd: 40));
        var actor = UnitOf(state, Side.Ally);
        actor.Gauge = Battle.GaugeMax;

        Battle.PerformAction(state, actor, 1);

        Assert.True(actor.TraitSpent, "使った印が立っていない");
        Assert.Same(actor, Battle.NextActor(state));
    }

    /// <summary>⚠️ **1戦闘1回。**縛らないと、弱化を通すたびに動けて手番が返ってこない。</summary>
    [Fact]
    public void 畳み掛けは一戦闘に一度だけ()
    {
        var state = Fight(Make("a", Traits.Surge, "curse", spd: 1),
            Make("b", null, spd: 40));
        var actor = UnitOf(state, Side.Ally);
        actor.Gauge = Battle.GaugeMax;
        Battle.PerformAction(state, actor, 1);

        // ⭐ 2回目は素の消費だけ ── 続けて動けない
        actor.Cooldowns[1] = 0;
        int before = actor.Gauge;
        Battle.PerformAction(state, actor, 1);
        Assert.True(actor.Gauge < before, "2回目も満タンに戻っている（1戦闘1回になっていない）");
    }

    /// <summary>⚠️ 持たない個体は1ビットも変わらない（筆頭の約束）。</summary>
    [Fact]
    public void 畳み掛けを持たなければ続けて動けない()
    {
        var state = Fight(Make("a", null, "curse", spd: 1), Make("b", null, spd: 40));
        var actor = UnitOf(state, Side.Ally);
        actor.Gauge = Battle.GaugeMax;

        Battle.PerformAction(state, actor, 1);

        Assert.False(actor.TraitSpent);
        Assert.NotSame(actor, Battle.NextActor(state));
    }

    // ── 常時: 狙い澄まし・意地 ────────────────────────

    /// <summary>⭐ 筆頭の約束。特性を持たない者どうしなら、率は素の式のまま。</summary>
    [Fact]
    public void 特性を持たない者どうしなら通る率は動かない()
    {
        var state = Fight(Make("a", null), Make("b", null));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);

        // ⭐ 同じ種族どうしなので、動くのは**基礎の命中と耐性の差**だけ。
        // ⚠️ 「60 のまま」と直書きしていた頃は、種族の基礎に弱化命中・弱化耐性を
        //    配った日（2026-08-19）に落ちた。⭐ 差から出す。
        int gap = StatAccuracyTests.GapOf(actor, target);
        Assert.Equal(60 + gap, Battle.LandChanceOf(Effect.Poison(1, 3, 60), actor, target));
        // ⚠️ 率 100 の弱化は乱数を1度も引かない（移植した試合が1手も変わらない条件）
        Assert.Equal(100, Battle.LandChanceOf(Effect.Poison(1, 3), actor, target));
    }

    [Fact]
    public void 狙い澄ましは弱化の通る率を上げる()
    {
        var state = Fight(Make("a", Traits.Aim), Make("b", null));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);

        // ⚠️ 期待値を直書きしない（種族の基礎に弱化命中・弱化耐性が入ったので差がある）
        int gap = StatAccuracyTests.GapOf(actor, target);
        Assert.Equal(60 + gap + Battle.TraitAim,
            Battle.LandChanceOf(Effect.Poison(1, 3, 60), actor, target));
    }

    /// <summary>⚠️ 自分・味方に掛けるものは速度でも特性でも動かない。
    /// 誰も抵抗していないのに通しやすくなるのは筋が通らない。</summary>
    [Fact]
    public void 狙い澄ましは味方に掛けるものには効かない()
    {
        var state = Fight(Make("a", Traits.Aim), Make("b", null));
        var actor = UnitOf(state, Side.Ally);

        Assert.Equal(50, Battle.LandChanceOf(Effect.Shield(2, 50), actor, actor));
        Assert.Equal(50, Battle.LandChanceOf(Effect.HealRatio(40, 50), actor, actor));
    }

    [Fact]
    public void 意地は弱化を受ける率を下げる()
    {
        var state = Fight(Make("a", null), Make("b", Traits.Stubborn));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);

        int gap = StatAccuracyTests.GapOf(actor, target);
        Assert.Equal(60 + gap - Battle.TraitStubborn,
            Battle.LandChanceOf(Effect.Poison(1, 3, 60), actor, target));
    }

    /// <summary>⭐ 「必ず通る弱化」にも効く。効かないと画面の説明と食い違う。</summary>
    [Fact]
    public void 意地は必ず通る弱化にも効く()
    {
        var state = Fight(Make("a", null), Make("b", Traits.Stubborn));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);

        int land = Battle.LandChanceOf(Effect.Poison(1, 3), actor, target);
        Assert.True(land < 100, $"率が {land} のままで意地が働いていない");
        int gap = StatAccuracyTests.GapOf(actor, target);
        Assert.Equal(100 + gap - Battle.TraitStubborn, land);
    }

    [Fact]
    public void 狙い澄ましと意地はぶつかると打ち消し合う()
    {
        var state = Fight(Make("a", Traits.Aim), Make("b", Traits.Stubborn));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);

        int gap = StatAccuracyTests.GapOf(actor, target);
        Assert.Equal(60 + gap, Battle.LandChanceOf(Effect.Poison(1, 3, 60), actor, target));
    }

    // ── 攻撃を受けたとき: 返し身 ──────────────────────

    [Fact]
    public void 返し身は受けたダメージの一部を返す()
    {
        var state = Fight(Make("a", null, "attack"), Make("b", Traits.Spite));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);

        Battle.PerformAction(state, actor, 1);

        int taken = target.MaxHp - target.Hp;
        Assert.True(taken > 0, "そもそも当たっていない");
        Assert.Equal(actor.MaxHp - taken * Battle.TraitSpitePercent / 100, actor.Hp);
    }

    /// <summary>⚠️ **この検査が無いと無限に往復して固まる。**
    /// 返した一撃では返さない、という止め木がただ1つの歯止め。</summary>
    [Fact]
    public void 返し身どうしでも往復しない()
    {
        var state = Fight(Make("a", Traits.Spite, "attack"), Make("b", Traits.Spite));
        var actor = UnitOf(state, Side.Ally);

        Battle.PerformAction(state, actor, 1);   // 固まらずに戻ってくれば通る

        // 返し身の返し身は起きない ＝ 殴った側が受けるのは1回だけ
        int reflections = 0;
        foreach (var log in state.Log)
        {
            if (log.Kind == BattleEventKind.Damage && log.Unit == actor.Key && log.Amount > 0)
                reflections++;
        }
        Assert.Equal(1, reflections);
    }

    [Fact]
    public void 返し身は倒れたら返さない()
    {
        // HP1 の相手を殴る。⭐ 働く場面は OnHurt であって OnDown ではない
        var state = Fight(Make("a", null, "attack"), Make("b", Traits.Spite));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);
        target.Hp = 1;

        Battle.PerformAction(state, actor, 1);

        Assert.Equal(0, target.Hp);
        Assert.Equal(actor.MaxHp, actor.Hp);
    }

    [Fact]
    public void 盾で無効化されたら返さないし吸わない()
    {
        var state = Fight(Make("a", Traits.Leech, "attack"), Make("b", Traits.Spite));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);
        actor.Hp = actor.MaxHp - 10;
        target.Status.Shield = 1;

        Battle.PerformAction(state, actor, 1);

        Assert.Equal(target.MaxHp, target.Hp);            // 無効化された
        Assert.Equal(actor.MaxHp - 10, actor.Hp);         // 吸ってもいないし返されてもいない
    }

    // ── 攻撃を当てたとき: 食らいつき・手数 ─────────────

    [Fact]
    public void 食らいつきは与えたダメージの一部を吸う()
    {
        var state = Fight(Make("a", Traits.Leech, "attack"), Make("b", null));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);
        // ⚠️ **傷は最大HPの割合で作る。**手で 30 と置いていた頃は、桁を上げた日に
        //    吸った量が最大HPを超えて頭打ちになり、別の検査（超えて吸わない）と重なった。
        int wound = actor.MaxHp / 2;
        actor.Hp = actor.MaxHp - wound;

        Battle.PerformAction(state, actor, 1);

        int dealt = target.MaxHp - target.Hp;
        Assert.Equal(actor.MaxHp - wound + dealt * Battle.TraitLeechPercent / 100, actor.Hp);
    }

    [Fact]
    public void 食らいつきは最大HPを超えて吸わない()
    {
        var state = Fight(Make("a", Traits.Leech, "attack"), Make("b", null));
        var actor = UnitOf(state, Side.Ally);

        Battle.PerformAction(state, actor, 1);

        Assert.Equal(actor.MaxHp, actor.Hp);
    }

    /// <summary>⭐ 特性は技を強くしない。**噛み合ったときだけ**働く。</summary>
    [Fact]
    public void 手数は多段のぶんだけ待ちを縮める()
    {
        var withTrait = Cooldown(Traits.Flurry, "attack-thrice");
        var without = Cooldown(null, "attack-thrice");

        // 乱打は3発。⭐ 縮むのは「増えたぶん」の2
        Assert.Equal(without - 2, withTrait);
    }

    [Fact]
    public void 手数は単発では何もしない()
    {
        Assert.Equal(Cooldown(null, "attack-heavy"), Cooldown(Traits.Flurry, "attack-heavy"));
    }

    /// <summary>⚠️ 手数が見るのは**1体に何発当てたか**。
    /// 対象ぶん足し込むと、多段でもない全体攻撃で待ちが縮む。
    /// ⭐ ハネルの枠1 は全体攻撃で CT 0 なので、毎行動 CT が3ずつ減る別ゲームになっていた。</summary>
    [Fact]
    public void 手数は全体攻撃の対象数では縮まない()
    {
        var withTrait = AllHitCooldown(Traits.Flurry);
        var without = AllHitCooldown(null);
        Assert.Equal(without, withTrait);
    }

    /// <summary>敵3体に全体攻撃を1回撃った直後の、枠2の CT。</summary>
    private static int AllHitCooldown(string? traitId)
    {
        var allies = new List<Creature> { Make("a", traitId, "attack-all") };
        var foes = new List<Creature>
        {
            Make("b0", null, hp: 400, def: 200),
            Make("b1", null, hp: 400, def: 200),
            Make("b2", null, hp: 400, def: 200),
        };
        var state = Battle.CreateBattle(allies, foes);
        var actor = UnitOf(state, Side.Ally);

        Battle.PerformAction(state, actor, 1);

        // ⚠️ 3体に当たっているのを確かめてから CT を見る（当たっていなければ検査にならない）
        int struck = 0;
        foreach (var unit in state.Units)
        {
            if (unit.Side == Side.Enemy && unit.Hp < unit.MaxHp) struck++;
        }
        Assert.Equal(3, struck);
        return actor.Cooldowns[1];
    }

    /// <summary>⚠️ 返し身が入るまで「行動者が自分の行動中に死ぬ」経路は無かった。
    /// 見ていないと、返し身で倒れた死体が2発目・3発目を打つ。</summary>
    [Fact]
    public void 返し身で倒れた者は残りの発を打たない()
    {
        var state = Fight(Make("a", null, "attack-thrice"), Make("b", Traits.Spite, hp: 60, def: 60));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);
        actor.Hp = 1;   // 1発目の返しで必ず倒れる

        Battle.PerformAction(state, actor, 1);

        Assert.Equal(0, actor.Hp);
        // 倒れたあとは1発も入っていない ＝ 相手が受けたのは1発だけ
        int struck = 0;
        foreach (var log in state.Log)
        {
            if (log.Kind == BattleEventKind.Damage && log.Unit == target.Key) struck++;
        }
        Assert.Equal(1, struck);
    }

    /// <summary>⚠️ 全体攻撃でも同じ。1体目の返しで倒れたら2体目へ進まない。</summary>
    [Fact]
    public void 返し身で倒れたら残りの対象へ進まない()
    {
        var allies = new List<Creature> { Make("a", null, "attack-all") };
        var foes = new List<Creature>
        {
            Make("b0", Traits.Spite, hp: 60, def: 60),
            Make("b1", null, hp: 60, def: 60),
        };
        var state = Battle.CreateBattle(allies, foes);
        var actor = UnitOf(state, Side.Ally);
        actor.Hp = 1;

        Battle.PerformAction(state, actor, 1);

        Assert.Equal(0, actor.Hp);
        foreach (var unit in state.Units)
        {
            if (unit.Side == Side.Enemy && unit.Slot == 1)
                Assert.Equal(unit.MaxHp, unit.Hp);   // 2体目は無傷
        }
    }

    /// <summary>その技を1回使った直後の、枠1の CT。</summary>
    private static int Cooldown(string? traitId, string skillId)
    {
        var state = Fight(Make("a", traitId, skillId), Make("b", null, hp: 400, def: 200));
        var actor = UnitOf(state, Side.Ally);
        Battle.PerformAction(state, actor, 1);
        return actor.Cooldowns[1];
    }

    // ── 盾が剥がれたとき: 執念 ────────────────────────

    [Fact]
    public void 執念は盾が剥がれるたびゲージが溜まる()
    {
        var state = Fight(Make("a", null, "attack-thrice"), Make("b", Traits.Grit));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);
        target.Status.Shield = 2;
        int before = target.Gauge;

        Battle.PerformAction(state, actor, 1);

        // 乱打3発のうち2発が盾を剥がす
        Assert.Equal(before + Battle.TraitGritGauge * 2, target.Gauge);
        Assert.Equal(0, target.Status.Shield);
    }

    [Fact]
    public void 執念を持たなければゲージは溜まらない()
    {
        var state = Fight(Make("a", null, "attack-thrice"), Make("b", null));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);
        target.Status.Shield = 2;
        int before = target.Gauge;

        Battle.PerformAction(state, actor, 1);

        Assert.Equal(before, target.Gauge);
    }

    // ── 戦闘開始時: 先駆け ────────────────────────────

    /// <summary>⭐ TraitWhen.BattleStart の繋ぎ先は CreateBattle だけ。
    /// ⚠️ 2026-08-20 に中身を替えた ── 開幕ゲージは実測で「まったく技を選ばない」特性だった
    /// （進んでも**どの技を選ぶかが1つも変わらない**）。
    /// ⭐ いまは「開幕の1手目の弱化が外れない」。</summary>
    [Fact]
    public void 先駆けは開幕の一手だけ弱化が外れない()
    {
        var state = Fight(Make("a", Traits.Opener), Make("b", null));
        var actor = UnitOf(state, Side.Ally);
        var foe = UnitOf(state, Side.Enemy);
        var risky = Effect.Buff(StatKey.Atk, -1, 3, chance: 40);

        Assert.Equal(100, Battle.LandChanceOf(risky, actor, foe));

        // ⚠️ 1手動いたら、もう開幕ではない
        Battle.PerformAction(state, actor, 0);
        Assert.True(Battle.LandChanceOf(risky, actor, foe) < 100, "1手動いても開幕のまま");
    }

    /// <summary>⚠️ 意地は普通に効く（外れないのは「率」の話で、弾く側は別）。</summary>
    [Fact]
    public void 先駆けでも意地の相手には外れる()
    {
        var state = Fight(Make("a", Traits.Opener), Make("b", Traits.Stubborn));
        var risky = Effect.Buff(StatKey.Atk, -1, 3, chance: 40);
        Assert.True(
            Battle.LandChanceOf(risky, UnitOf(state, Side.Ally), UnitOf(state, Side.Enemy)) < 100,
            "意地を無視している");
    }

    // ── 倒れる一撃を受けたとき: 置き土産 ────────────────

    [Fact]
    public void 置き土産は倒れたとき残った味方のゲージが進む()
    {
        var state = PartingBattle(Traits.Parting, out var actor, out var holder, out var friend);
        holder.Hp = 1;
        int before = friend.Gauge;

        Battle.PerformAction(state, actor, 1);

        Assert.Equal(0, holder.Hp);
        Assert.Equal(before + Battle.TraitPartingGauge, friend.Gauge);
    }

    /// <summary>⚠️ 毒は DealDamage を通らない ＝「一撃」ではないので働かない。
    /// ⭐ 場面の名（倒れる**一撃**を受けたとき）と実装を揃える見張り。</summary>
    [Fact]
    public void 置き土産は毒で倒れたときは働かない()
    {
        int with = FriendGaugeAfterPoisonDown(Traits.Parting);
        int without = FriendGaugeAfterPoisonDown(null);
        Assert.Equal(without, with);
    }

    private static int FriendGaugeAfterPoisonDown(string? traitId)
    {
        var state = PartingBattle(traitId, out _, out var holder, out var friend);
        holder.Hp = 1;
        holder.Status.Poison = new Stacking { Stacks = 1, Turns = 1 };
        holder.Gauge = Battle.GaugeMax;   // 次に動くのは毒で倒れる本人

        Battle.NextActor(state);

        Assert.Equal(0, holder.Hp);
        return friend.Gauge;
    }

    /// <summary>味方1体 vs（特性持ち＋相方）の2体。⭐ 単体攻撃は残 HP の低い側に落ちる。</summary>
    private static BattleState PartingBattle(string? traitId,
        out Unit actor, out Unit holder, out Unit friend)
    {
        var allies = new List<Creature> { Make("a", null, "attack") };
        var foes = new List<Creature> { Make("b0", traitId), Make("b1", null) };
        var state = Battle.CreateBattle(allies, foes);
        actor = UnitOf(state, Side.Ally);
        Unit? found = null, other = null;
        foreach (var unit in state.Units)
        {
            if (unit.Side != Side.Enemy) continue;
            if (unit.Slot == 0) found = unit; else other = unit;
        }
        holder = found!;
        friend = other!;
        return state;
    }

    // ── 攻撃を当てたとき: 追い打ち・背水 ────────────────

    /// <summary>⭐ しかけ（弱化）→ 回収（殴る）の2段。弱化が無ければ1ビットも変わらない。</summary>
    [Fact]
    public void 追い打ちは弱化が付いた相手にだけ増える()
    {
        int clean = PursuitDamage(poisoned: false);
        int marked = PursuitDamage(poisoned: true);
        Assert.True(clean > 0, "そもそも当たっていない");
        Assert.Equal(clean + clean * Battle.TraitPursuitPercent / 100, marked);
    }

    private static int PursuitDamage(bool poisoned)
    {
        var state = Fight(Make("a", Traits.Pursuit, "attack"), Make("b", null, hp: 400));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);
        if (poisoned) target.Status.Poison = new Stacking { Stacks = 1, Turns = 3 };

        Battle.PerformAction(state, actor, 1);

        return target.MaxHp - target.Hp;
    }

    /// <summary>⚠️ 2026-08-20 に中身を替えた ── 威力上昇は「技を選ばない」特性だった
    /// （どの技を撃っても同じだけ増えるので、選び方が変わらない）。
    /// ⭐ いまは「半分以下の間、待ちが速く減る」＝**重い技を持たせる理由**。</summary>
    [Fact]
    public void 背水は半分以下の間だけ待ちが速く減る()
    {
        Assert.Equal(1, DesperationStep(woundToHalf: false));
        Assert.Equal(Battle.TraitDesperationStep, DesperationStep(woundToHalf: true));
    }

    /// <summary>1回行動したときに、**撃っていない枠**の待ちがいくつ減ったか。</summary>
    private static int DesperationStep(bool woundToHalf)
    {
        var state = Fight(Make("a", Traits.Desperation, "attack", "attack-heavy"),
            Make("b", null, hp: 400));
        var actor = UnitOf(state, Side.Ally);
        if (woundToHalf) actor.Hp = actor.MaxHp / 2;

        actor.Cooldowns[2] = 5;
        Battle.PerformAction(state, actor, 1);

        return 5 - actor.Cooldowns[2];
    }

    // ── 常時: 粘り腰 ─────────────────────────────

    /// <summary>⚠️ 2026-08-20 に中身を替えた ── 被害減は「技を選ばない」特性だった
    /// （受け身に効くだけで、こちらの手が変わらない）。
    /// ⭐ いまは「受け取る回復が増える」＝**回復役を連れているか**が編成の判断になる。</summary>
    [Fact]
    public void 粘り腰は半分以下の間だけ受け取る回復が増える()
    {
        int high = TenacityHealed(woundToHalf: false);
        int low = TenacityHealed(woundToHalf: true);
        Assert.True(high > 0, "そもそも回復していない");
        Assert.Equal(high + high * Battle.TraitTenacityPercent / 100, low);
    }

    private static int TenacityHealed(bool woundToHalf)
    {
        var state = Fight(Make("a", null), Make("b", Traits.Tenacity, hp: 400));
        var healer = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);
        // ⚠️ 満タンだと戻る量が頭打ちになるので、どちらも十分に削っておく
        target.Hp = woundToHalf ? target.MaxHp / 2 : target.MaxHp * 3 / 4;
        int before = target.Hp;

        Battle.ApplyOne(state, healer, target, Effect.HealRatio(10));

        return target.Hp - before;
    }

    // ── 入手経路 ────────────────────────────────────

    /// <summary>⭐ **始めたばかりの3体は特性を持たない。**
    ///
    /// ⚠️ 理由は強さではなく**覚えることの量**。まだ何も分かっていない人に
    /// 種族・技3枠・属性・得意/不得意・素質に加えて特性まで出すと、読むものが多すぎる。
    /// ⭐ 浅い巣からは低い★しか出ないので、序盤は自然に特性なしになる。</summary>
    [Fact]
    public void 始めたばかりの3体は特性を持たない()
    {
        var game = Games.NewGame(2026_08_17);
        foreach (var creature in game.Storage.Creatures)
        {
            Assert.Null(creature.TraitId);
        }
    }

    /// <summary>★の低い卵からは出ず、★の高い卵からは出る。⭐ 境目は <see cref="Traits.MinRarity"/>。</summary>
    [Fact]
    public void 特性は星の高い卵からだけ出る()
    {
        Assert.False(Traits.AppearsAt(Traits.MinRarity - 1));
        Assert.True(Traits.AppearsAt(Traits.MinRarity));

        for (int rarity = 1; rarity <= Rarities.Max; rarity++)
        {
            var born = HatchOfRarity(rarity);
            if (rarity < Traits.MinRarity) Assert.Null(born.TraitId);
            else Assert.True(Traits.Has(born.TraitId!), $"★{rarity}: 特性が付いていない");
        }
    }

    /// <summary>その★の卵を1つ孵す。⚠️ 本番の経路（孵化器）を通す。</summary>
    private static Creature HatchOfRarity(int rarity)
    {
        var game = Games.NewGame(31 + rarity);
        var nest = Nests.ById("thicket-fang");
        var egg = Nests.MakeEggOfRarity(game.RngEgg, nest, EggOrigin.Defeated, ++game.Serial, rarity);
        game.Eggs.Add(egg);

        var started = Hatchery.Begin(game, egg.Id, 1000);
        Hatchery.Rush(started, 1000);
        var born = Hatchery.Collect(game, egg.Id, 1000);
        Assert.NotNull(born);
        return born!;
    }

    /// <summary>⭐ 配合は「持っているものを尖らせる」出口。
    /// ⚠️ 特性を減らす手段にしない（両親が持っていれば子も必ず持つ）。</summary>
    [Fact]
    public void 配合では親のどちらかの特性を継ぐ()
    {
        var game = WithTraitedParents(777, out string aId, out string bId);
        var parents = new HashSet<string?>
        {
            Games.CreatureById(game, aId).TraitId,
            Games.CreatureById(game, bId).TraitId,
        };

        var outcome = Games.FusePair(game, aId, bId);
        Assert.Contains(outcome.Egg.TraitId, parents);
    }

    /// <summary>⭐ **配合は★の下限を見ない。**
    /// ⚠️ 親が持っているのに子が失うほうが分かりにくいので、継承は無条件。
    /// ⭐ 序盤に特性を出さないのは「初めて手にする経路」を絞る話であって、
    /// 既に持っているものを取り上げる話ではない。</summary>
    [Fact]
    public void 配合の継承は星の下限を見ない()
    {
        var game = WithTraitedParents(101, out string aId, out string bId);
        var outcome = Games.FusePair(game, aId, bId);

        Assert.True(outcome.Egg.Rarity < Traits.MinRarity || outcome.Egg.TraitId != null);
        Assert.NotNull(outcome.Egg.TraitId);
    }

    /// <summary>⚠️ 配合で決まった特性を、孵すときに引き直さない。</summary>
    [Fact]
    public void 配合の卵は孵しても特性が変わらない()
    {
        var game = WithTraitedParents(43, out string aId, out string bId);
        var outcome = Games.FusePair(game, aId, bId);

        var born = Nests.Hatch(game.RngHatch, outcome.Egg, "child", null, null, Traits.Spite);
        Assert.Equal(outcome.Egg.TraitId, born.TraitId);
    }

    /// <summary>特性を持つ親を2体そろえた状態。⚠️ 序盤の個体は持たないので、明示的に持たせる。</summary>
    private static Game WithTraitedParents(int seed, out string aId, out string bId)
    {
        var game = Games.NewGame(seed);
        var kept = new List<Creature>();
        string[] traits = { Traits.Grit, Traits.Leech, Traits.Flurry };
        for (int i = 0; i < game.Storage.Creatures.Count; i++)
        {
            var c = game.Storage.Creatures[i];
            kept.Add(new Creature(c.Id, c.SpeciesId, c.Wild, c.Trained, c.Earned,
                c.MutationCounter, c.Skill2, c.Skill3, c.PaletteIndex,
                c.ParentA, c.ParentB, c.Generation, c.Strong, c.Weak, c.Element,
                traits[i % traits.Length]));
        }
        game.Storage = new Storage(game.Storage.Slots, kept);
        aId = kept[0].Id;
        bId = kept[1].Id;
        return game;
    }

    // ── 保存 ────────────────────────────────────────

    [Fact]
    public void 特性は保存して読み直しても消えない()
    {
        var game = Games.NewGame(5);
        var save = Snapshots.Save(game);
        var back = Snapshots.Load(save);
        Assert.NotNull(back);

        for (int i = 0; i < game.Storage.Creatures.Count; i++)
        {
            Assert.Equal(game.Storage.Creatures[i].TraitId, back!.Storage.Creatures[i].TraitId);
        }
    }

    /// <summary>⚠️ 表から消えた id で開かないセーブを作らない。空にして先へ進む。</summary>
    [Fact]
    public void 表に無い特性のidは読み込みで空になる()
    {
        var game = Games.NewGame(11);
        var save = Snapshots.Save(game);
        save.Creatures[0].Trait = "存在しない特性";

        var notes = new List<string>();
        var back = Snapshots.Load(save, notes);

        Assert.NotNull(back);
        Assert.Null(back!.Storage.Creatures[0].TraitId);
        Assert.NotEmpty(notes);
    }

    /// <summary>⚠️ 特性より前のセーブ（Trait が無い）も読めること。</summary>
    [Fact]
    public void 特性を知らない古いセーブも読める()
    {
        var game = Games.NewGame(13);
        var save = Snapshots.Save(game);
        foreach (var c in save.Creatures) c.Trait = null;

        var back = Snapshots.Load(save);
        Assert.NotNull(back);
        Assert.Null(back!.Storage.Creatures[0].TraitId);
    }
}
