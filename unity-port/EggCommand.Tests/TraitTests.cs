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

    // ── 常時: 狙い澄まし・意地 ────────────────────────

    /// <summary>⭐ 筆頭の約束。特性を持たない者どうしなら、率は素の式のまま。</summary>
    [Fact]
    public void 特性を持たない者どうしなら通る率は動かない()
    {
        var state = Fight(Make("a", null), Make("b", null));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);

        // 速度が同じなので、素の率がそのまま出る
        Assert.Equal(60, Battle.LandChanceOf(Effect.Poison(1, 3, 60), actor, target));
        // ⚠️ 率 100 の弱化は乱数を1度も引かない（移植した試合が1手も変わらない条件）
        Assert.Equal(100, Battle.LandChanceOf(Effect.Poison(1, 3), actor, target));
    }

    [Fact]
    public void 狙い澄ましは弱化の通る率を上げる()
    {
        var state = Fight(Make("a", Traits.Aim), Make("b", null));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);

        Assert.Equal(60 + Battle.TraitAim, Battle.LandChanceOf(Effect.Poison(1, 3, 60), actor, target));
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

        Assert.Equal(60 - Battle.TraitStubborn, Battle.LandChanceOf(Effect.Poison(1, 3, 60), actor, target));
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
        Assert.Equal(100 - Battle.TraitStubborn, land);
    }

    [Fact]
    public void 狙い澄ましと意地はぶつかると打ち消し合う()
    {
        var state = Fight(Make("a", Traits.Aim), Make("b", Traits.Stubborn));
        var actor = UnitOf(state, Side.Ally);
        var target = UnitOf(state, Side.Enemy);

        Assert.Equal(60, Battle.LandChanceOf(Effect.Poison(1, 3, 60), actor, target));
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
        actor.Hp = actor.MaxHp - 30;

        Battle.PerformAction(state, actor, 1);

        int dealt = target.MaxHp - target.Hp;
        Assert.Equal(actor.MaxHp - 30 + dealt * Battle.TraitLeechPercent / 100, actor.Hp);
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
