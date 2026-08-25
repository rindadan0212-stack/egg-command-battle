using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>画面を確かめるための、決まった中身のゲーム。
///
/// ⚠️ **本番の入口ではありません。**⭐ 骨組みが実物の数で描けることと、
/// 検査（`scripts/audit.mjs`）が通ることを見るための土台です。
///
/// ⚠️ 種と時刻を固定する ── 毎回違う画面を撮ると、比べたときの差が
/// 「直したから」なのか「引きが違うから」なのか分からなくなる。</summary>
public static class Demo
{
    public const int Seed = 20260822;
    public const long Now = 1_700_000_000;

    /// <summary>いつもの場面。⭐ 14体。一覧が4列で3段以上になる数（巻物が効くことを見るため）。
    /// <param name="full">🔴 **あふれの場面**（`?full=true` の受け口）。⚠️ 遊びの規則が許す
    /// 「実際に起こりうる最悪」を1つの盤へ詰めて返す（<see cref="Overflow"/>）。
    /// 既定は false ＝ 今までどおりの14体。</param></summary>
    public static Game Game(bool full = false) =>
        full ? Overflow() : Games.NewGame(Seed, Now, startWith: 14);

    /// <summary>🔴 **本命 ── あふれの場面。**（`wiki/開発/web移行計画.md` §11）
    ///
    /// 詰めたもの（⚠️ 数はすべて Core の規則から引く。適当に長い字は1つも入れない）:
    /// | 何 | 出所 |
    /// |---|---|
    /// | 保管庫 **50体** | <see cref="Storages.StorageSlots"/>。満杯にする |
    /// | 4体の素質 | <see cref="Stats.WildStatMaxFor"/>(21) ＝ 60（<see cref="Stats.GenerationCapSteps"/>
    /// ＝20 を使い切る世代21が、素質の天井が動く最後の世代） |
    /// | 育成 | <see cref="Creatures.TrainMax"/> ＝ 20（満タン） |
    /// | 技レベル | <see cref="SkillCosts.TotalFor"/>(<see cref="Skills.MaxLevel"/>) ＝ 120pt（Lv5） |
    /// | 試練 | <see cref="Core.Trials"/>.All.Count ＝ 5（全段クリア済み） |
    /// | 溜まった EXP | ⚠️ **上限が規則から読めなかった**（<see cref="IdleRun.Exp"/> は放置と
    /// 分解で際限なく増える）。目安として「満タン個体50体を分解した額」
    /// （<see cref="Levels.DissolveExpOf"/> × 50）を採用した</param>
    ///
    /// ⚠️ 種は固定（<see cref="Seed"/>）。⭐ 42体は乱数で「ふつう」を作り、
    /// そこへ4体の「極まった個体」を足して50に詰める ── 全員が極まった箱より、
    /// 少数の主力＋厚い箱のほうが**実際に起こりうる**形に近い。</summary>
    public static Game Overflow()
    {
        const int heroCount = 4;
        var game = Games.NewGame(Seed, Now, startWith: Storages.StorageSlots - heroCount);

        // ⭐ 4体の「極まった個体」── 編成に出る主力。それぞれ種族の基礎値が
        //    一番高いステを中心に、素質3本を天井まで積む（他の3本は 0）。
        var h1 = Extreme(game, "iwao", StatKey.Hp, StatKey.Atk, StatKey.Spd);     // Hp基礎140が最大
        var h2 = Extreme(game, "tsunoga", StatKey.Atk, StatKey.Def, StatKey.Spd); // Atk基礎120が最大
        var h3 = Extreme(game, "haneru", StatKey.Spd, StatKey.Atk, StatKey.Hp);  // Spd基礎130が最大
        var h4 = Extreme(game, "hirabe", StatKey.Def, StatKey.Spd, StatKey.Hp);  // Def基礎130が最大
        var heroes = new[] { h1, h2, h3, h4 };
        foreach (var hero in heroes) Games.Keep(game, hero);

        // ⭐ 編成へ明示的に登録する。⚠️ 素質合計が最大（180）なので、選んでいなくても
        //    Games.PartyOf の自動選出（素質の高い順）で同じ4体が出るが、
        //    「たまたま」ではなく確実にするためここで書く。
        var ids = new List<string> { h1.Id, h2.Id, h3.Id, h4.Id };
        game.NestParties[0].Clear();
        game.NestParties[0].AddRange(ids);
        game.Party.Clear();
        game.Party.AddRange(ids);

        // ⭐ 試練は全段クリア済み。「勝った」の出方も詰める。
        Beat(game, Trials.All.Count);

        // ⭐ 溜まった EXP。IdleRun.Exp に規則上の上限は無いので、
        //    「満タン個体（h1）を分解したときの額」× 50体ぶんを目安に採用した
        //    （h1 は世代・素質・育成のすべてが天井なので、この式でいちばん大きな値になる）。
        game.Idle.Exp = Levels.DissolveExpOf(h1) * Storages.StorageSlots;

        return game;
    }

    /// <summary>極まった1体を直接組む。
    ///
    /// ⚠️ **`Nests.Hatch` を通さない。**野生では絶対に出ない組み合わせ
    /// （世代21・素質天井・育成満タン・技レベル満タン）を作るための唯一の場所。
    /// ⭐ どの値も「規則が許す天井」であって、規則を超えた値は1つも入れない。</summary>
    /// <param name="a">素質を天井まで積む1本目。⭐ 大得意（+30%）にもする。</param>
    /// <param name="b">素質を天井まで積む2本目。⭐ 得意（+15%）にもする。</param>
    /// <param name="c">素質を天井まで積む3本目。</param>
    private static Creature Extreme(Game game, string speciesId, StatKey a, StatKey b, StatKey c)
    {
        // ⭐ 世代21 ＝ Stats.GenerationCapSteps(20) を使い切る最初の世代。
        //    これより先へ進めても素質の天井は動かない（Stats.WildStatMaxFor が clamp する）。
        int generation = Stats.GenerationCapSteps + 1;
        int cap = Stats.WildStatMaxFor(generation); // 60
        var wild = new StatBlock(0, 0, 0, 0).With(a, cap).With(b, cap).With(c, cap);
        // ⚠️ 合計はちょうど cap*3 ＝ Stats.WildTotalMaxFor(generation)。削りは要らない。

        // ⭐ 偏り4本は必ず別ステ（Creatures.Slanted の約束）。
        //    a を大得意・b を得意にし、余った3本（wild を積んでいない側）から
        //    不得意・大不得意を選ぶ ── 伸ばしていないステを削るほうが実態に近い。
        var rest = new List<StatKey>();
        foreach (var key in Stats.Keys) if (key != a && key != b && key != c) rest.Add(key);
        StatKey best = a, strong = b, worst = rest[0], weak = rest[1];

        var species = SpeciesTable.ById(speciesId);
        string id = $"c{(++game.Serial).ToString().PadLeft(3, '0')}";
        var creature = new Creature(id, speciesId, wild, new StatBlock(0, 0, 0, 0), 0,
            // ⭐ 変異カウンタは、変異が止まる境目（Breeding.MutationCounterLimit）に置く
            //    ── 「これ以上配合しても変異が増えなくなる」という規則上の意味がある値。
            mutationCounter: Breeding.MutationCounterLimit,
            skill2: species.Slot2.Pool[^1], skill3: species.Slot3.Pool[^1],
            paletteIndex: 0, parentA: null, parentB: null, generation: generation,
            strong: strong, weak: weak, element: SpeciesTable.Roll(game.RngElement),
            traitId: Creatures.TraitIdFor(speciesId), best: best, worst: worst);

        // ⭐ 育成を満タンまで（Creatures.TrainMax）。
        Creatures.Grow(creature, Creatures.TrainMax);

        // ⭐ 技レベル。⚠️ 枠0はわざと Lv4 止まり（SkillCosts.TotalFor(4)）にして、
        //    「まだ鍛えられる」場面（/train の伸びしろ表示）も残す。枠1・2は Lv5満タン。
        creature.SkillPoints[0] = SkillCosts.TotalFor(Skills.MaxLevel - 1);
        creature.SkillPoints[1] = SkillCosts.TotalFor(Skills.MaxLevel);
        creature.SkillPoints[2] = SkillCosts.TotalFor(Skills.MaxLevel);

        return creature;
    }

    /// <summary>図鑑の種族カードで「あふれ」を見るための番号。
    /// ⭐ 特性の一言（`tgist`）と名乗り（`tname` ＝ 特性名＋いつ効くか）を、
    /// 実際に `Sheets.Species` が組み立てるのと同じ形で作り、いちばん長い種族を選ぶ。
    /// ⚠️ 決め打ちの番号を書かない ── 表が変わっても計算し直される。</summary>
    public static int OverflowSpeciesIndex
    {
        get
        {
            var all = SpeciesTable.All;
            int best = 0, bestLen = -1;
            for (int i = 0; i < all.Count; i++)
            {
                var trait = Traits.ById(all[i].TraitId);
                string tname = $"{trait.Name}　― {Traits.LabelOf(trait.When)}";
                int len = tname.Length + trait.Gist.Length;
                if (len > bestLen) { bestLen = len; best = i; }
            }
            return best;
        }
    }

    /// <summary>技の詳細札で「あふれ」を見るための番号。
    /// ⭐ 名前（`name`）と、実際に画面へ出る効果文（`SkillText.Describe`）を足した長さで、
    /// いちばん長い技を選ぶ。⚠️ `Skill.Gist`（一覧の一言）ではなく**実際に描く文**で測る
    /// ── 複合技は Describe のほうが長くなることがある。</summary>
    public static int OverflowSkillIndex
    {
        get
        {
            var all = Skills.All;
            int best = 0, bestLen = -1;
            for (int i = 0; i < all.Count; i++)
            {
                int len = all[i].Name.Length + SkillText.Describe(all[i]).Length;
                if (len > bestLen) { bestLen = len; best = i; }
            }
            return best;
        }
    }

    /// <summary>絞って並べた一覧。⭐ **絞ってから並べる**（BOX・配合・編成で同じ順）。</summary>
    public static IReadOnlyList<Creature> Sorted(Game game,
        FilterKey filter = FilterKey.All,
        SortKey sort = SortKey.WildTotal,
        SortBasis basis = SortBasis.Born)
    {
        var pool = Filters.Apply(game, game.Storage.Creatures, filter);
        return Storages.Sorted(new Storage(game.Storage.Slots, pool), sort, basis);
    }

    /// <summary>卵をいくつか温めている状態を作る。
    /// ⚠️ 入れる前に**棚へ載せる** ── `Hatchery.Begin` は棚から取る作り。
    /// ⭐ 始めた時刻をずらして、進み具合の違う枠を並べる（帯を見るため）。</summary>
    public static void Incubate(Game game, long now, int howMany)
    {
        var nest = Nests.ById("shallow-scale");
        int want = Math.Clamp(howMany, 0, Hatchery.Slots);
        for (int i = 0; i < want; i++)
        {
            var egg = Nests.MakeEgg(game.RngEgg, nest, EggOrigin.Defeated, ++game.Serial,
                element: SpeciesTable.Roll(game.RngElement));
            game.Eggs.Add(egg);
            Hatchery.Begin(game, egg.Id, now - i * 1200);
        }
    }

    /// <summary>決まった戦いを1つ組む。⚠️ 遊びの入口ではない ── 画面を見るための土台。
    /// ⭐ 巣の守り手を相手にする（本番と同じ作り方）。
    /// <param name="full">🔴 **あふれの場面。**味方1体に支援を、敵1体に妨害を、
    /// 実在する技が配る量のぶんだけ同時に載せる（<see cref="Pile"/>）。</param></summary>
    public static BattleState Fight(Game game, bool full = false)
    {
        var nest = Nests.ById("shallow-scale");
        var mine = new List<Creature>();
        foreach (var id in Games.RosterOf(game, PartyKind.Nest))
            foreach (var c in game.Storage.Creatures) if (c.Id == id) mine.Add(c);
        if (mine.Count == 0)
            for (int i = 0; i < Games.PartySize && i < game.Storage.Creatures.Count; i++)
                mine.Add(game.Storage.Creatures[i]);

        var state = EggCommand.Core.Battle.CreateBattle(mine, Games.DefendersOf(game, nest));
        // ⭐ ゲージを少し進めておく（帯が動いていることが見える形にする）
        for (int i = 0; i < state.Units.Count; i++)
            state.Units[i].Gauge = EggCommand.Core.Battle.GaugeMax / (i + 2);

        if (full) Pile(state);
        return state;
    }

    /// <summary>🔴 味方1体・敵1体へ、状態異常を**同時に載せられるだけ**載せる。
    /// wiki §11「状態異常を全部背負っている」をこの1戦闘で作る、唯一の場所。
    ///
    /// ⚠️ **免疫と、免疫が防ぐはずの弱化を同居させない。**⭐ だから支援は味方だけ・
    /// 妨害は敵だけに分けてある ── 免疫（弱化を受けない）を持つ個体に毒やスタンが
    /// 乗っているのは、実際には起こらない組み合わせになるため。
    ///
    /// ⚠️ **数値は全部、対応する技の効果からそのまま持ってくる**（欄の下にどの技かを書いた）。</summary>
    private static void Pile(BattleState state)
    {
        Unit? ally = null, foe = null;
        foreach (var u in state.Units)
        {
            if (ally == null && u.Side == Side.Ally) ally = u;
            if (foe == null && u.Side == Side.Enemy) foe = u;
        }

        if (ally != null)
        {
            // atk-up / def-up / spd-up ── 3本とも別ステなので同時に乗る
            ally.Status.Atk = new Modifier { Percent = Skills.BuffPercent, Turns = 3 };
            ally.Status.Def = new Modifier { Percent = Skills.BuffPercent, Turns = 3 };
            ally.Status.Spd = new Modifier { Percent = Skills.BuffPercent, Turns = 3 };
            ally.Status.Shield = 4;                                   // shield-wall
            ally.Status.Regen = new Stacking { Stacks = 2, Turns = 4 }; // regen-heavy
            ally.Status.Guts = 6;                                     // guts-deep
            ally.Status.Immune = 6;                                   // immune-long
        }
        if (foe != null)
        {
            // atk-down 系 / crush / slow-all ── これも3本とも別ステ
            foe.Status.Atk = new Modifier { Percent = -Skills.BuffPercent, Turns = 3 };
            foe.Status.Def = new Modifier { Percent = -Skills.BuffPercent, Turns = 3 };
            foe.Status.Spd = new Modifier { Percent = -Skills.BuffPercent, Turns = 3 };
            foe.Status.Poison = new Stacking { Stacks = 2, Turns = 4 }; // venom-heavy
            foe.Status.Stun = 2;                                      // stun-heavy
            foe.Status.Taunt = 5;                                     // taunt-long
            foe.Status.Block = 2;                                     // block
        }
    }

    /// <summary>決まった潜入を1つ組む。⚠️ 遊びの入口ではない ── 盤を見るための土台。</summary>
    public static Raid Raid(Game game, int raids = 0)
    {
        var nest = Nests.ById("shallow-scale");
        return Trails.Begin(Trails.OfNest(nest), Games.PartyOf(game), raids);
    }

    /// <summary>いくつかの試練に勝った状態にする。
    /// ⚠️ **勝った段が1つも無いと `when=beaten` の枝を一度も描かない**
    /// ── 検査が通っても「勝った」の出方は誰も見ていないことになる。</summary>
    public static void Beat(Game game, int howMany)
    {
        var all = EggCommand.Core.Trials.All;
        for (int i = 0; i < Math.Min(Math.Max(0, howMany), all.Count); i++)
            Games.MarkTrial(game, all[i].Id);
    }

    /// <summary>棚に卵を積む（孵化器には入れない）。
    /// <param name="rarity">⭐ 既定は★1。あふれの場面では★5（<see cref="Rarities.Max"/>）を渡し、
    /// 技を鍛える札の「1個の価値」も最大にする。</param></summary>
    public static void Shelve(Game game, int howMany, int rarity = 1)
    {
        var nest = Nests.ById("shallow-scale");
        for (int i = 0; i < Math.Max(0, howMany); i++)
        {
            game.Eggs.Add(Nests.MakeEgg(game.RngEgg, nest, EggOrigin.Defeated, ++game.Serial,
                rarity: rarity, element: SpeciesTable.Roll(game.RngElement)));
        }
    }
}
