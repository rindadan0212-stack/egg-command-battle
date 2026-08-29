using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>保存と復元。⚠️ ここが崩れると、遊んだ結果が黙って消える。</summary>
public class SnapshotTests
{
    private const long T0 = 1_700_000_000;

    /// <summary>⭐ **古い保存（育てた分が得意1本だけ）を読み直すと、全ステに揃う。**
    ///
    /// ⚠️ 2026-08-19 に育成を全ステへ変えた。そのまま読むと、同じ Lv なのに
    /// 新しく育てた個体より弱い個体が保存に残り続ける。
    /// ⭐ 育てた分は Earned から一意に決まるので、読むときに作り直せる。</summary>
    [Fact]
    public void 古い保存は未使用ポイントとして戻る()
    {
        var game = Games.NewGame(1, T0);
        // ⚠️ 「得意1本にだけ乗った」古い形を手で作る
        var old = new Creature("old", "tamaru",
            new StatBlock(20, 20, 20, 20, 20, 20),
            new StatBlock(0, 0, 0, 9, 0, 0), 9, 0, null, null, 0, null, null, 1,
            StatKey.Spd, StatKey.Res);
        game.Storage = Storages.Accept(game.Storage, old);

        var back = Snapshots.Load(Snapshots.Save(game));
        Creature found = null;
        foreach (var c in back.Storage.Creatures) if (c.Id == "old") found = c;
        Assert.NotNull(found);
        // 🔴 **古い保存は「未使用ポイント」として戻る**（2026-08-26・ARK式の自由配分）。
        //    ⚠️ 古い個体は誰も振り先を選んでいないので、育てた分は復元できない。
        //    ⭐ そのぶん **Earned がまるごと未使用**で返るので、開いた側が振り直せる
        //    （`振り直し不可` の唯一の例外 ── 移行のときだけ）。
        Assert.Equal(found.Earned, Creatures.UnspentOf(found));
        var want = Creatures.TrainedFor(found.SpeciesId, found.Wild, found.Points);
        foreach (var key in Stats.Keys)
        {
            // ⚠️ まだ1点も振っていないので、育てた分は 0 で揃う（伸びていないのが正）
            Assert.Equal(want[key], found.Trained[key]);
            Assert.Equal(0, found.Trained[key]);
        }

        // ⭐ 振れば伸びる ── 移行した個体がちゃんと使えることまで見る
        Creatures.Spend(found, StatKey.Atk, found.Earned);
        Assert.Equal(0, Creatures.UnspentOf(found));
        Assert.True(found.Trained[StatKey.Atk] > 0, "振ったのに攻撃力が伸びていない");
    }

    /// <summary>ひととおり触った状態を作る。⭐ 空の状態だけ通しても検査にならない。</summary>
    private static Game Played()
    {
        var game = Games.NewGame(2026_08_16);
        var ids = new List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);

        Games.GrowParty(Games.PartyOf(game), 3);
        Games.TogglePartyMember(game, ids[2]);
        Games.GainEgg(game, game.Encounters[0].Nest, EggOrigin.Stolen);
        Games.GainEgg(game, game.Encounters[1].Nest, EggOrigin.Defeated);
        Hatchery.Begin(game, game.Eggs[0].Id, T0);
        Core.Idle.Advance(game.Idle, Games.PartyOf(game), T0);
        Core.Idle.Advance(game.Idle, Games.PartyOf(game), T0 + 120);
        return game;
    }

    private static void Same(Game a, Game b)
    {
        Assert.Equal(a.Seed, b.Seed);
        Assert.Equal(a.Serial, b.Serial);
        Assert.Equal(a.EncounterSerial, b.EncounterSerial);
        Assert.Equal(a.Storage.Slots, b.Storage.Slots);
        Assert.Equal(a.Storage.Creatures.Count, b.Storage.Creatures.Count);
        for (int i = 0; i < a.Storage.Creatures.Count; i++)
        {
            var x = a.Storage.Creatures[i];
            var y = b.Storage.Creatures[i];
            Assert.Equal(x.Id, y.Id);
            Assert.Equal(x.SpeciesId, y.SpeciesId);
            Assert.True(x.Wild.Equals(y.Wild), $"{x.Id}: 素質が違う");
            Assert.True(x.Trained.Equals(y.Trained), $"{x.Id}: 育てた分が違う");
            Assert.Equal(x.Earned, y.Earned);
            Assert.Equal(x.Skill2, y.Skill2);
            Assert.Equal(x.Skill3, y.Skill3);
            Assert.Equal(x.Strong, y.Strong);
            Assert.Equal(x.Weak, y.Weak);
            Assert.Equal(x.Generation, y.Generation);
            // ⚠️ ここから下は比べていなかった欄。⭐ 落ちても誰も気づけない場所だった
            Assert.Equal(x.MutationCounter, y.MutationCounter);
            Assert.Equal(x.PaletteIndex, y.PaletteIndex);
            Assert.Equal(x.TraitId, y.TraitId);
            Assert.Equal(x.Element, y.Element);
            Assert.Equal(x.SkillPoints, y.SkillPoints);
        }
        Assert.Equal(a.Eggs.Count, b.Eggs.Count);
        for (int i = 0; i < a.Eggs.Count; i++)
        {
            Assert.Equal(a.Eggs[i].Id, b.Eggs[i].Id);
            Assert.Equal(a.Eggs[i].Rarity, b.Eggs[i].Rarity);
            Assert.True(a.Eggs[i].Wild.Equals(b.Eggs[i].Wild));
            Assert.Equal(a.Eggs[i].How, b.Eggs[i].How);
        }
        Assert.Equal(a.Incubating.Count, b.Incubating.Count);
        for (int i = 0; i < a.Incubating.Count; i++)
        {
            Assert.Equal(a.Incubating[i].Egg.Id, b.Incubating[i].Egg.Id);
            Assert.Equal(a.Incubating[i].ReadyUnix, b.Incubating[i].ReadyUnix);
        }
        Assert.Equal(a.Encounters.Count, b.Encounters.Count);
        for (int i = 0; i < a.Encounters.Count; i++)
        {
            Assert.Equal(a.Encounters[i].Nest.Id, b.Encounters[i].Nest.Id);
            Assert.Equal(a.Encounters[i].Nest.SpeciesId, b.Encounters[i].Nest.SpeciesId);
            Assert.Equal(a.Encounters[i].Nest.Tier, b.Encounters[i].Nest.Tier);
            Assert.Equal(a.Encounters[i].Level, b.Encounters[i].Level);
        }
        Assert.Equal(a.Party, b.Party);
        Assert.Equal(a.Idle.Exp, b.Idle.Exp);
        Assert.Equal(a.Idle.Defeated, b.Idle.Defeated);
        Assert.Equal(a.Idle.LastUnix, b.Idle.LastUnix);
        Assert.Equal(a.Idle.EnemyHp, b.Idle.EnemyHp);
        Assert.Equal(a.Idle.Charge, b.Idle.Charge);
        Assert.Equal(a.Idle.DownUntil.Count, b.Idle.DownUntil.Count);

        // ⚠️ **乱数は10系統ぜんぶ見る。**1本だけ見ていた頃は、StreamsOf から
        //    落ちた系統が読み込みで無音に巻き戻っても検査を通っていた
        var left = Snapshots.Save(a).Rng;
        var right = Snapshots.Save(b).Rng;
        Assert.Equal(left.Count, right.Count);
        for (int i = 0; i < left.Count; i++) Assert.Equal(left[i], right[i]);
    }

    [Fact]
    public void 遊んだ状態がそのまま戻る()
    {
        var game = Played();
        var back = Snapshots.Load(Snapshots.Save(game));
        Assert.NotNull(back);
        Same(game, back!);
    }

    [Fact]
    public void 二度通しても変わらない()
    {
        // ⭐ 保存 → 復元 → 保存 が同じ形になること。抜け落ちる欄があると崩れる
        var game = Played();
        var once = Snapshots.Load(Snapshots.Save(game))!;
        var twice = Snapshots.Load(Snapshots.Save(once))!;
        Same(once, twice);
    }

    [Fact]
    public void 乱数の続きから引ける()
    {
        // ⚠️ ここを保存しないと、遊び直すたびに同じ卵と同じ巣が出る
        var game = Played();
        var back = Snapshots.Load(Snapshots.Save(game))!;

        var a = new List<uint>();
        var b = new List<uint>();
        for (int i = 0; i < 8; i++)
        {
            a.Add(game.RngEgg.U32Value());
            b.Add(back.RngEgg.U32Value());
        }
        Assert.Equal(a, b);
    }

    /// <summary>⚠️ **乱数の系統を足しても、古い保存が読めるか。**
    ///
    /// ⭐ 系統は前から4語ずつ当てて、足りなくなったら止める作りなので、
    /// **末尾に足すかぎり**古い保存もそのまま読める（足した系統は初期状態のまま）。
    /// ⚠️ 途中へ挿すと、そこから後ろの系統が**全部ずれる** ── 卵の続きが巣の続きになる。
    ///
    /// ⭐ 2026-08-21 に `RngBattle` を足したときの決まりを、ここで押さえる。</summary>
    [Fact]
    public void 乱数の系統を足しても古い保存が読める()
    {
        var game = Played();
        var save = Snapshots.Save(game);
        // ⭐ 古い保存を真似る ── 末尾の1系統ぶん（4語）を削る
        Assert.True(save.Rng.Count >= 8);
        save.Rng.RemoveRange(save.Rng.Count - 4, 4);

        var back = Snapshots.Load(save);
        Assert.NotNull(back);
        Assert.Equal(game.Seed, back!.Seed);
        Assert.Equal(game.Storage.Creatures.Count, back.Storage.Creatures.Count);
        // ⚠️ 削らなかった系統は今までどおり続きから引ける
        Assert.Equal(game.RngEgg.U32Value(), back.RngEgg.U32Value());
    }

    /// <summary>⭐ **戦闘の運は、卵や巣と混ざらない。**
    /// ⚠️ 混ざると、戦っただけで次に出る卵が変わる。</summary>
    [Fact]
    public void 戦闘の乱数は他の系統と別々に進む()
    {
        var game = new Game(1234);
        uint eggBefore = game.RngEgg.U32Value();
        for (int i = 0; i < 50; i++) game.RngBattle.U32Value();

        var same = new Game(1234);
        same.RngEgg.U32Value();
        Assert.Equal(game.RngEgg.U32Value(), same.RngEgg.U32Value());
        Assert.NotEqual(eggBefore, game.RngBattle.U32Value());
    }

    [Fact]
    public void 保存しないと同じ卵が出てしまう()
    {
        // ⭐ 上の検査が本当に効いていることの裏取り（乱数を戻さなければ食い違う）
        var game = Played();
        var fresh = new Game(game.Seed);
        Assert.NotEqual(game.RngEgg.U32Value(), fresh.RngEgg.U32Value());
    }

    [Fact]
    public void 版が違う保存は読まない()
    {
        var save = Snapshots.Save(Played());
        save.Version = Snapshots.Version + 1;
        Assert.Null(Snapshots.Load(save));
        Assert.Null(Snapshots.Load(null));
    }

    [Fact]
    public void 倒れている者も戻る()
    {
        var game = Games.NewGame(5);
        game.Idle.DownUntil["c001"] = T0 + 20;
        var back = Snapshots.Load(Snapshots.Save(game))!;
        Assert.Equal(T0 + 20, back.Idle.DownUntil["c001"]);
    }

    [Fact]
    public void 復元したあと孵化の残り時間が続く()
    {
        var game = Played();
        var back = Snapshots.Load(Snapshots.Save(game))!;
        var slot = back.Incubating[0];
        Assert.Null(Hatchery.Collect(back, slot.Egg.Id, slot.ReadyUnix - 1));
        Assert.NotNull(Hatchery.Collect(back, slot.Egg.Id, slot.ReadyUnix));
    }

    // ── 中身が増減しても保存が死なないこと ────────────────────────
    // ⚠️ ここが落ちる形になっていると、種族や技を1つ消しただけで
    //    「それを持っている人のセーブだけが開かない」という壊れ方をする。
    //    手元の新規プレイでは再現しないので、検査でしか押さえられない。

    [Fact]
    public void 表から消えた種族を持つ保存でも読める()
    {
        var save = Snapshots.Save(Played());
        save.Creatures[0].SpeciesId = "もう無い種族";

        var notes = new List<string>();
        var back = Snapshots.Load(save, notes);

        Assert.NotNull(back);
        // ⭐ 個体は残る。素質も育てた分もそのまま
        Assert.Equal(save.Creatures[0].Id, back!.Storage.Creatures[0].Id);
        Assert.Equal(save.Creatures[0].Earned, back.Storage.Creatures[0].Earned);
        // 置き換え先は表から引ける
        Assert.True(SpeciesTable.Has(back.Storage.Creatures[0].SpeciesId));
        // ⚠️ 黙って別の種族にしない。何をしたかが残る
        Assert.Contains(notes, n => n.Contains("もう無い種族"));
    }

    [Fact]
    public void 表から消えた技は枠が空くだけで済む()
    {
        var save = Snapshots.Save(Played());
        save.Creatures[0].Skill2 = "もう無い技";

        var notes = new List<string>();
        var back = Snapshots.Load(save, notes)!;

        // ⚠️ 別の技で埋めない。持っていない技を持っている状態のほうが危ない
        Assert.Null(back.Storage.Creatures[0].Skill2);
        Assert.Contains(notes, n => n.Contains("もう無い技"));
        // 残りの枠は無事
        Assert.Equal(save.Creatures[0].Skill3, back.Storage.Creatures[0].Skill3);
    }

    [Fact]
    public void 卵と探索の中の種族も読み替わる()
    {
        var save = Snapshots.Save(Played());
        save.Eggs[0].SpeciesId = "もう無い種族";
        save.Encounters[0].SpeciesId = "もう無い種族";

        var back = Snapshots.Load(save)!;

        Assert.True(SpeciesTable.Has(back.Eggs[0].SpeciesId));
        Assert.True(SpeciesTable.Has(back.Encounters[0].Nest.SpeciesId));
    }

    /// <summary>⚠️ 古い版を捨てない。捨てるのは「直せない壊し方」の中で一番よくある。</summary>
    [Fact]
    public void 古い版の保存は読み_新しすぎる版だけ捨てる()
    {
        var save = Snapshots.Save(Played());

        save.Version = Snapshots.Version - 1;
        Assert.NotNull(Snapshots.Load(save));

        save.Version = Snapshots.Version + 1;
        Assert.Null(Snapshots.Load(save));
    }

    /// <summary>⭐ **墓標（家系図）も保存に載って戻る。**
    /// ⚠️ 配合で両親を消してから保存・復元しても、`Game.Tombs` の中身（家系図を
    /// 辿るための控え）が消えていないことを確かめる。</summary>
    [Fact]
    public void 墓標も保存から戻る()
    {
        var game = Games.NewGame(2026_08_29);
        var ids = new List<string>();
        foreach (var c in game.Storage.Creatures) ids.Add(c.Id);
        Games.FusePair(game, ids[0], ids[1]);
        Assert.Equal(2, game.Tombs.Count);

        var back = Snapshots.Load(Snapshots.Save(game))!;

        Assert.Equal(game.Tombs.Count, back.Tombs.Count);
        foreach (var tomb in game.Tombs)
        {
            var found = back.Tombs.Find(t => t.Id == tomb.Id);
            Assert.NotNull(found);
            Assert.Equal(tomb.SpeciesId, found!.SpeciesId);
            Assert.Equal(tomb.Element, found.Element);
            Assert.Equal(tomb.Generation, found.Generation);
            Assert.Equal(tomb.WildTotal, found.WildTotal);
            Assert.Equal(tomb.ParentA, found.ParentA);
            Assert.Equal(tomb.ParentB, found.ParentB);
        }

        // ⭐ 往復後も家系図を組み立てられる（値が壊れていないことの裏取り）
        var child = Games.CreatureById(back, back.Storage.Creatures[^1].Id);
        var nodes = Lineage.Of(back, child, 1);
        Assert.True(nodes[0].Known);
    }

    /// <summary>🔴 **`Tombs` の無い古い保存（この欄が無い頃のもの）でも読める。**
    /// ⚠️ 落ちない・消さない ── 空のリストとして読める。</summary>
    [Fact]
    public void Tombsの無い古い保存でも読める()
    {
        var save = Snapshots.Save(Played());
        // ⭐ 「古い保存」を模す ── この欄がまだ無かった頃と同じ、空のまま
        Assert.Empty(save.Tombs);

        var back = Snapshots.Load(save);

        Assert.NotNull(back);
        Assert.Empty(back!.Tombs);
    }

    /// <summary>引っ越し表そのもの。⭐ 仕組みだけ作って一度も通していない状態にしない。</summary>
    [Fact]
    public void 引っ越し表は多段を辿り輪で投げる()
    {
        var chain = new Dictionary<string, string> { { "a", "b" }, { "b", "c" } };
        Assert.Equal("c", Migrations.Apply(chain, "a"));
        Assert.Equal("c", Migrations.Apply(chain, "c"));
        // 表に無いものはそのまま
        Assert.Equal("z", Migrations.Apply(chain, "z"));

        var loop = new Dictionary<string, string> { { "a", "b" }, { "b", "a" } };
        Assert.Throws<System.InvalidOperationException>(() => Migrations.Apply(loop, "a"));
    }

    /// <summary>🔴 **孵化器の枠から溢れた卵は、棚へ戻る。**
    ///
    /// ⚠️ 枠の数（<see cref="Hatchery.Slots"/>）は減ることがある（2026-08-27: 6→5）。
    /// そのとき古い保存には**今は無い番号**（5番）に入った卵が残っている。
    /// ⭐ 画面は 0〜4 しか読まないので、置いたままだと**持っているのに一生取り出せない**
    /// ── 黙って消さず、棚（`Eggs`）へ戻す。
    ///
    /// ⚠️ この検査は「今の枠数」に追従する（6 に戻しても通る）── 数を直に書かない。</summary>
    [Fact]
    public void 枠から溢れた卵は棚へ戻る()
    {
        var game = Games.NewGame(7, T0);
        var save = Snapshots.Save(game);
        int eggsBefore = save.Eggs.Count;

        // ⚠️ 「今の枠数の外」に居る卵を手で作る（＝枠を減らす前に保存されたもの）
        var stray = new EggSave
        {
            Id = "stray", SpeciesId = "tamaru", Rarity = 3, Element = (int)Element.Fire,
        };
        save.Incubating.Add(new IncubationSave
        {
            Egg = stray, StartUnix = T0, ReadyUnix = T0 + 600, Slot = Hatchery.Slots,
        });

        var back = Snapshots.Load(save);

        // ⭐ 温めている側には入っていない（読めない番号なので）
        Assert.Null(Hatchery.At(back, Hatchery.Slots));
        Assert.Empty(back.Incubating);
        // ⭐ そのぶん棚に1つ増えている（消えていない）
        Assert.Equal(eggsBefore + 1, back.Eggs.Count);
        Assert.Contains(back.Eggs, e => e.Id == "stray");
    }

    /// <summary>いま生きている id は、引っ越し表を通しても自分のまま。
    /// ⚠️ 表に書き間違いがあると、遊んでいる最中の個体が別の種族に化ける。</summary>
    [Fact]
    public void 生きている_id_は引っ越し表で動かない()
    {
        foreach (var species in SpeciesTable.All)
        {
            Assert.Equal(species.Id, Migrations.SpeciesOf(species.Id));
        }
        foreach (var skill in Skills.All)
        {
            Assert.Equal(skill.Id, Migrations.SkillOf(skill.Id));
        }
    }
}
