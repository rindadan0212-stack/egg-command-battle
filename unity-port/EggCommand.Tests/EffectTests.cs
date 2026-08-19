using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>あとから足した効果が**実際に動く**か。
///
/// ⭐ 技に当てはめる前の段階なので、ここでは効果を直に打ち込んで確かめる。
/// ⚠️ 技表に乗せるかどうかは別の判断（釣り合いを測ってから決める）。
/// </summary>
public class EffectTests
{
    private static Creature Make(string id, int hp, int atk, int def, int spd) =>
        new Creature(id, "tamaru", new StatBlock(hp, atk, def, spd),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1);

    /// <summary>味方3体 対 敵3体の素の盤。⚠️ 乱数は引かせない（確率100の効果だけ使う）。</summary>
    private static BattleState Field()
    {
        var allies = new List<Creature>
        {
            Make("a0", 30, 30, 30, 30), Make("a1", 30, 30, 30, 30), Make("a2", 30, 30, 30, 30),
        };
        var foes = new List<Creature>
        {
            Make("e0", 30, 30, 30, 30), Make("e1", 30, 30, 30, 30), Make("e2", 30, 30, 30, 30),
        };
        return Battle.CreateBattle(allies, foes);
    }

    private static Unit Ally(BattleState s, int i) => s.Units.Find(u => u.Key == $"ally-{i}")!;
    private static Unit Foe(BattleState s, int i) => s.Units.Find(u => u.Key == $"enemy-{i}")!;

    /// <summary>効果を1つだけ打ち込む。⚠️ 技を作らずに効果そのものを試すための入口。</summary>
    private static void Hit(BattleState s, Unit from, Unit to, Effect e) =>
        Battle.ApplyOne(s, from, to, e);

    /// <summary>特性を持たせた盤。⚠️ 個体は作り直す（欄は書き換えない）。</summary>
    private static BattleState FieldWithTrait(string traitId)
    {
        var s = Field();
        var foe = Foe(s, 0);
        var made = new Creature(foe.Creature.Id, foe.Creature.SpeciesId, foe.Creature.Wild,
            foe.Creature.Trained, foe.Creature.Earned, foe.Creature.MutationCounter,
            foe.Creature.Skill2, foe.Creature.Skill3, foe.Creature.PaletteIndex,
            foe.Creature.ParentA, foe.Creature.ParentB, foe.Creature.Generation,
            foe.Creature.Strong, foe.Creature.Weak, foe.Creature.Element, traitId);
        var allies = new List<Creature>();
        var foes = new List<Creature>();
        foreach (var u in s.Units)
        {
            if (u.Side == Side.Ally) allies.Add(u.Creature);
            else foes.Add(u.Key == foe.Key ? made : u.Creature);
        }
        return Battle.CreateBattle(allies, foes);
    }

    // ── 眠りは特性を止める ──────────────────────

    /// <summary>⭐ **眠っている間は特性が働かない。**
    /// ⚠️ これが無いと眠りは「手番を飛ばす」だけで、スタンと役割が丸かぶりになる。</summary>
    [Fact]
    public void 眠っている間は意地が働かない()
    {
        // 意地 = 弱化を受ける率が下がる特性
        var awake = FieldWithTrait(Traits.Stubborn);
        var actor = Ally(awake, 0);
        int guarded = Battle.LandChanceOf(Effect.Buff(StatKey.Atk, -1, 3, chance: 80),
            actor, Foe(awake, 0));

        var asleep = FieldWithTrait(Traits.Stubborn);
        Hit(asleep, Ally(asleep, 0), Foe(asleep, 0), Effect.Sleep(2));
        int open = Battle.LandChanceOf(Effect.Buff(StatKey.Atk, -1, 3, chance: 80),
            Ally(asleep, 0), Foe(asleep, 0));

        Assert.True(open > guarded,
            $"眠っても意地が効いたまま（起きている {guarded}% / 眠っている {open}%）");
    }

    /// <summary>⚠️ 弱化では起きない。⭐ だから「眠らせてから弱化を積む」が成立する。</summary>
    [Fact]
    public void 弱化を掛けても目を覚まさない()
    {
        var s = Field();
        var foe = Foe(s, 0);
        Hit(s, Ally(s, 0), foe, Effect.Sleep(2));
        Assert.True(foe.Status.Sleep > 0);

        Hit(s, Ally(s, 0), foe, Effect.Buff(StatKey.Def, -1, 3));
        Assert.True(foe.Status.Sleep > 0, "弱化で目を覚ましてしまった");
    }

    // ── ゲージ ──────────────────────────────────

    [Fact]
    public void ゲージ上昇は満タンに対する割合で増える()
    {
        var s = Field();
        var target = Ally(s, 0);
        target.Gauge = 0;

        Hit(s, target, target, Effect.Gauge(30));

        Assert.Equal(Battle.GaugeMax * 30 / 100, target.Gauge);
    }

    /// <summary>⚠️ 減らす側は**超過ぶんごと**削る。⭐ 貯めた先行が没収される。</summary>
    [Fact]
    public void ゲージ減少は超過ぶんも削る()
    {
        var s = Field();
        var target = Foe(s, 0);
        target.Gauge = Battle.GaugeMax * 2;                 // 満タンの2倍まで溜めている

        Hit(s, Ally(s, 0), target, Effect.Gauge(-50));

        Assert.Equal(Battle.GaugeMax * 2 - Battle.GaugeMax / 2, target.Gauge);
        Assert.True(Skills.IsHarmful(Effect.Gauge(-50)), "減らす側は弱化");
        Assert.False(Skills.IsHarmful(Effect.Gauge(30)), "増やす側は弱化でない");
    }

    [Fact]
    public void ゲージは0より下がらない()
    {
        var s = Field();
        var target = Foe(s, 0);
        target.Gauge = 100;
        Hit(s, Ally(s, 0), target, Effect.Gauge(-90));
        Assert.Equal(0, target.Gauge);
    }

    // ── 睡眠 ────────────────────────────────────

    [Fact]
    public void 睡眠は手番を飛ばす()
    {
        var s = Field();
        var target = Foe(s, 0);
        Hit(s, Ally(s, 0), target, Effect.Sleep(2));
        Assert.Equal(2, target.Status.Sleep);

        // 満タンにして手番を回す
        foreach (var u in s.Units) u.Gauge = 0;
        target.Gauge = Battle.GaugeMax;
        var next = Battle.NextActor(s);

        Assert.NotEqual(target, next);                      // ⭐ 眠っている者は動けない
    }

    /// <summary>⭐ **殴ると起きる。**⚠️ ここがスタンとの唯一の違い。</summary>
    [Fact]
    public void 睡眠は殴られると解ける()
    {
        var s = Field();
        var target = Foe(s, 0);
        Hit(s, Ally(s, 0), target, Effect.Sleep(3));
        Assert.Equal(3, target.Status.Sleep);

        Hit(s, Ally(s, 0), target, Effect.Damage(PowerTier.Small, DamageScale.Atk));

        Assert.Equal(0, target.Status.Sleep);
    }

    // ── ブロック ────────────────────────────────

    /// <summary>⭐ 外から受け取る回復と強化を弾く。</summary>
    [Fact]
    public void ブロックは回復と強化を弾く()
    {
        var s = Field();
        var target = Foe(s, 0);
        target.Hp = 10;
        Hit(s, Ally(s, 0), target, Effect.Block(3));

        Hit(s, target, target, Effect.HealRatio(50));
        Assert.Equal(10, target.Hp);                        // 回復しない

        Hit(s, target, target, Effect.Buff(StatKey.Atk, 1, 3));
        Assert.Equal(0, target.Status.Atk.Turns);           // 強化も乗らない
    }

    /// <summary>⚠️ **弱化は止めない。**止まるのは「外から買った分」だけ。</summary>
    [Fact]
    public void ブロックは弱化を止めない()
    {
        var s = Field();
        var target = Foe(s, 0);
        Hit(s, Ally(s, 0), target, Effect.Block(3));

        Hit(s, Ally(s, 0), target, Effect.Buff(StatKey.Def, -1, 3));

        Assert.True(target.Status.Def.Turns > 0, "弱化は通るべき");
    }

    // ── 防御無視 ────────────────────────────────

    /// <summary>⭐ 硬い相手ほど差が出る。⚠️ 効果の種類ではなくダメージの性質。</summary>
    [Fact]
    public void 防御無視は防御の高い相手に強い()
    {
        var soft = Make("soft", 30, 30, 0, 30);
        var hard = Make("hard", 30, 30, 40, 30);
        var attacker = Make("atk", 30, 40, 0, 30);

        int normalVsHard = Battle.DamageOf(Skills.DamagePowerOf(PowerTier.Medium),
            Creatures.StatsOf(attacker).Atk, Creatures.StatsOf(hard).Def, 1.0);
        int pierceVsHard = Battle.DamageOf(Skills.DamagePowerOf(PowerTier.Medium),
            Creatures.StatsOf(attacker).Atk, 0, 1.0);
        int normalVsSoft = Battle.DamageOf(Skills.DamagePowerOf(PowerTier.Medium),
            Creatures.StatsOf(attacker).Atk, Creatures.StatsOf(soft).Def, 1.0);

        Assert.True(pierceVsHard > normalVsHard, "貫通のほうが硬い相手に通る");
        Assert.True(pierceVsHard - normalVsHard > pierceVsHard - normalVsSoft,
            "硬い相手ほど貫通の利得が大きい");
    }

    [Fact]
    public void 防御無視は盾を抜かない()
    {
        var s = Field();
        var target = Foe(s, 0);
        Hit(s, target, target, Effect.Shield(1));
        int before = target.Hp;

        Hit(s, Ally(s, 0), target, Effect.Damage(PowerTier.Large, DamageScale.Atk, pierce: true));

        Assert.Equal(before, target.Hp);                    // ⭐ 盾を抜くのは手数の仕事
        Assert.Equal(0, target.Status.Shield);
    }

    // ── 強化解除・強奪 ──────────────────────────

    [Fact]
    public void 強化解除は乗っている強化を消す()
    {
        var s = Field();
        var target = Foe(s, 0);
        Hit(s, target, target, Effect.Buff(StatKey.Atk, 1, 5));
        Hit(s, target, target, Effect.Buff(StatKey.Def, 1, 5));
        Hit(s, target, target, Effect.Shield(2));

        Hit(s, Ally(s, 0), target, Effect.Dispel(2));

        // ⚠️ 剥がす順は固定（攻→防→速→盾→…）。乱数で選ばない
        Assert.Equal(0, target.Status.Atk.Turns);
        Assert.Equal(0, target.Status.Def.Turns);
        Assert.Equal(2, target.Status.Shield);              // 3つ目は残る
    }

    [Fact]
    public void 強化解除は弱化には触らない()
    {
        var s = Field();
        var target = Foe(s, 0);
        Hit(s, Ally(s, 0), target, Effect.Buff(StatKey.Atk, -1, 5));

        Hit(s, Ally(s, 0), target, Effect.Dispel(3));

        Assert.True(target.Status.Atk.Turns > 0, "弱化は消えない");
    }

    /// <summary>⭐ 消すのではなく**自分へ移す**。</summary>
    [Fact]
    public void 強化強奪は自分に乗る()
    {
        var s = Field();
        var thief = Ally(s, 0);
        var target = Foe(s, 0);
        Hit(s, target, target, Effect.Buff(StatKey.Atk, 1, 4));

        Hit(s, thief, target, Effect.Steal(1));

        Assert.Equal(0, target.Status.Atk.Turns);           // 相手からは消え
        Assert.True(thief.Status.Atk.Turns > 0, "自分に乗る");
        Assert.True(thief.Status.Atk.Percent > 0);
    }

    // ── 挑発（相手に付ける弱化へ作り替え）────────

    /// <summary>⭐ 掛けた本人しか狙えなくなる。⚠️ 移植元は「味方が引き受ける」だった。</summary>
    [Fact]
    public void 挑発は掛けた本人しか狙えなくする()
    {
        var s = Field();
        var baiter = Ally(s, 2);
        var foe = Foe(s, 0);
        // ⚠️ 素の狙いは残HPの低い相手。そこを崩して確かめる
        Ally(s, 0).Hp = 1;

        Hit(s, baiter, foe, Effect.Taunt(2));
        Assert.Equal(baiter.Key, foe.Status.TauntBy);

        var picked = Battle.TargetsFor(s, foe, Target.EnemyOne, null);

        Assert.Single(picked);
        Assert.Equal(baiter.Key, picked[0].Key);            // 瀕死の ally-0 ではない
        Assert.True(Skills.IsHarmful(Effect.Taunt(1)), "挑発は弱化");
    }

    [Fact]
    public void 挑発は全体攻撃を縛らない()
    {
        var s = Field();
        Hit(s, Ally(s, 2), Foe(s, 0), Effect.Taunt(2));

        var picked = Battle.TargetsFor(s, Foe(s, 0), Target.EnemyAll, null);

        Assert.Equal(3, picked.Count);
    }

    [Fact]
    public void 挑発の掛け手が倒れたら縛りは解ける()
    {
        var s = Field();
        var baiter = Ally(s, 2);
        var foe = Foe(s, 0);
        Hit(s, baiter, foe, Effect.Taunt(3));
        baiter.Hp = 0;

        var picked = Battle.TargetsFor(s, foe, Target.EnemyOne, null);

        Assert.Single(picked);
        Assert.NotEqual(baiter.Key, picked[0].Key);
    }

    // ── 蘇生 ────────────────────────────────────

    [Fact]
    public void 蘇生は倒れた味方を戻す()
    {
        var s = Field();
        var down = Ally(s, 1);
        down.Hp = 0;

        Hit(s, Ally(s, 0), down, Effect.Revive(50));

        Assert.True(Battle.IsAlive(down));
        Assert.Equal(down.MaxHp / 2, down.Hp);
    }

    /// <summary>⭐ 立ち上がるときは強化も弱化も無い状態から。</summary>
    [Fact]
    public void 蘇生は強化も弱化も持ち越さない()
    {
        var s = Field();
        var down = Ally(s, 1);
        Hit(s, down, down, Effect.Buff(StatKey.Atk, 1, 5));
        Hit(s, Foe(s, 0), down, Effect.Buff(StatKey.Def, -1, 5));
        down.Hp = 0;

        Hit(s, Ally(s, 0), down, Effect.Revive(30));

        Assert.Equal(0, down.Status.Atk.Turns);
        Assert.Equal(0, down.Status.Def.Turns);
        Assert.Equal(0, down.Gauge);
    }

    [Fact]
    public void 蘇生は倒れていない相手には効かない()
    {
        var s = Field();
        var alive = Ally(s, 1);
        alive.Hp = 5;

        Hit(s, Ally(s, 0), alive, Effect.Revive(50));

        Assert.Equal(5, alive.Hp);
    }

    // ── AI が新しい効果を採点できるか ────────────

    /// <summary>⚠️ ここに漏れがあると「コンパイルは通り、AI が永久にその技を選ばない」になる。</summary>
    [Fact]
    public void AIは全ての効果を採点できる()
    {
        foreach (EffectKind kind in System.Enum.GetValues(typeof(EffectKind)))
        {
            Assert.True(Ai.Knows(kind), $"{kind} を AI が採点できない");
        }
    }

    // ── 属性が弱化の通る率にも効く ────────────────

    private static Creature WithElement(Creature c, Element e) => Creatures.WithElement(c, e);

    /// <summary>⭐ 属性の有利・不利が通る率を動かす。⚠️ ダメージ倍率とは別枠。</summary>
    [Fact]
    public void 属性の有利は弱化を通しやすくする()
    {
        // ⚠️ 速度は揃える（速度差の効果と分けて測る）
        var attacker = Make("atk", 30, 30, 30, 30);
        var same = Make("same", 30, 30, 30, 30);

        var fire = WithElement(attacker, Element.Fire);
        var wood = WithElement(same, Element.Wood);   // 炎が有利
        var water = WithElement(same, Element.Water); // 炎が不利
        var fire2 = WithElement(same, Element.Fire);  // 互角

        var s = Battle.CreateBattle(
            new List<Creature> { fire },
            new List<Creature> { wood, water, fire2 });
        var me = s.Units.Find(u => u.Side == Side.Ally)!;
        var foes = s.Units.FindAll(u => u.Side == Side.Enemy);

        var weak = Effect.Buff(StatKey.Atk, -1, 3, chance: 60);
        int vsAdv = Battle.LandChanceOf(weak, me, foes[0]);
        int vsDis = Battle.LandChanceOf(weak, me, foes[1]);
        int vsEven = Battle.LandChanceOf(weak, me, foes[2]);

        // ⭐ 属性のぶんだけ動く。⚠️ 同じ種族なので命中と耐性の差はどれも同じ
        //    （種族の基礎に配ったので 0 とは限らない・2026-08-19）
        int gap = StatAccuracyTests.GapOf(me, foes[2]);
        Assert.Equal(60 + gap + Battle.LandElementSwing, vsAdv);
        Assert.Equal(60 + gap - Battle.LandElementSwing, vsDis);
        Assert.Equal(60 + gap, vsEven);
    }

    /// <summary>⚠️ 属性は**弱化にだけ**効く。回復や盾を属性で外させない。</summary>
    [Fact]
    public void 属性は自分に掛けるものには効かない()
    {
        var a = WithElement(Make("a", 30, 30, 30, 30), Element.Fire);
        var b = WithElement(Make("b", 30, 30, 30, 30), Element.Wood);
        var s = Battle.CreateBattle(new List<Creature> { a }, new List<Creature> { b });
        var me = s.Units.Find(u => u.Side == Side.Ally)!;
        var foe = s.Units.Find(u => u.Side == Side.Enemy)!;

        var boon = Effect.Shield(2, chance: 70);
        Assert.Equal(70, Battle.LandChanceOf(boon, me, foe));
    }

    /// <summary>⚠️ 速度差と足し算で重なる。⭐ 床と天井を越えないこと。</summary>
    [Fact]
    public void 属性と速度が重なっても床と天井を越えない()
    {
        var fast = WithElement(Make("f", 10, 10, 10, 40), Element.Fire);
        var slow = WithElement(Make("s", 10, 10, 10, 0), Element.Wood);
        var s = Battle.CreateBattle(new List<Creature> { fast }, new List<Creature> { slow });
        var me = s.Units.Find(u => u.Side == Side.Ally)!;
        var foe = s.Units.Find(u => u.Side == Side.Enemy)!;

        var weak = Effect.Buff(StatKey.Spd, -1, 3, chance: 90);
        int up = Battle.LandChanceOf(weak, me, foe);
        int down = Battle.LandChanceOf(weak, foe, me);

        Assert.True(up <= Battle.LandCeil, $"天井を越えた: {up}");
        Assert.True(down >= Battle.LandFloor, $"床を割った: {down}");
    }
}
