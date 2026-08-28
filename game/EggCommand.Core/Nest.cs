#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>どうやって手に入れたか。盗んだ卵はやや劣る。</summary>
    public enum EggOrigin
    {
        Defeated,
        Stolen,
        Bred,
    }

    public sealed class Nest
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string SpeciesId;
        /// <summary>段階。高いほど親が強く、落とす卵も良い。</summary>
        public readonly int Tier;

        public Nest(string id, string name, string speciesId, int tier)
        {
            Id = id;
            Name = name;
            SpeciesId = speciesId;
            Tier = tier;
        }
    }

    public sealed class Egg
    {
        public readonly string Id;
        public readonly string SpeciesId;
        public readonly StatBlock Wild;
        public readonly int MutationCounter;
        // ⚠️ **色の欄は 2026-08-21 に外した。**⭐ 色は「孵るとき」に引くので、
        //    卵が運ぶ必要が無い（運ばせると、引く場所が2つになる）。
        public readonly string? ParentA;
        public readonly string? ParentB;
        public readonly int Generation;
        public readonly EggOrigin How;

        /// <summary>⭐ null なら孵すときにガチャで決まる（野生の卵）。
        /// 値が入っていれば配合で既に決まっている（両親の4枠から抽選済み）。
        /// ⚠️ ここを区別しないと、配合で狙って引いた技を孵化時に引き直してしまう。</summary>
        public readonly bool HasSkills;
        public readonly string? Skill2;
        public readonly string? Skill3;

        /// <summary>希少さ 1〜5。⭐ 孵るまでの時間はここだけで決まる。
        /// ⚠️ 素質（<see cref="Wild"/>）とは別の軸にしてある。混ぜると
        /// 「時間をかけた＝強い」が確定してしまい、待つ以外の選択が消える。</summary>
        public readonly int Rarity;

        /// <summary>生まれつきの得意・不得意。⭐ null なら孵すときに引く（野生の卵）。
        /// ⚠️ <see cref="HasSkills"/> と同じ約束。配合で決まっているものを引き直さない。</summary>
        public readonly StatKey? Strong;
        public readonly StatKey? Weak;

        /// <summary>生まれつきの大得意・大不得意。⭐ <see cref="Strong"/> と同じ約束。</summary>
        public readonly StatKey? Best;
        public readonly StatKey? Worst;

        /// <summary>孵ったときの属性。⭐ 卵の時点で決まっている（孵るまでの楽しみは希少さと素質）。</summary>
        public readonly Element Element;

        // ⚠️ **特性の欄は 2026-08-21 に外した。**⭐ 種族から決まるので、卵が運ぶ必要が無い
        //    （<see cref="Creatures.TraitIdFor"/>）。運ばせていた頃は、配合で継ぐ／孵化で引く
        //    という2つの経路があり、片方だけ直すと黙って食い違った。

        public Egg(string id, string speciesId, StatBlock wild, int mutationCounter,
            string? parentA, string? parentB, int generation, EggOrigin how,
            bool hasSkills, string? skill2, string? skill3, int rarity = 1,
            StatKey? strong = null, StatKey? weak = null, Element? element = null,
            StatKey? best = null, StatKey? worst = null)
        {
            Element = element ?? Migrations.ElementOf(speciesId);
            Rarity = rarity < 1 ? 1 : rarity > Rarities.Max ? Rarities.Max : rarity;
            Strong = strong;
            Weak = weak;
            Best = best;
            Worst = worst;
            Id = id;
            SpeciesId = speciesId;
            Wild = wild;
            MutationCounter = mutationCounter;

            ParentA = parentA;
            ParentB = parentB;
            Generation = generation;
            How = how;
            HasSkills = hasSkills;
            Skill2 = skill2;
            Skill3 = skill3;
        }
    }

    /// <summary>巣と卵。
    ///
    /// ⭐ 強い親ほど良い卵。これが難易度と報酬を自動で結ぶので、
    /// 報酬テーブルを別に設計しなくてよい。
    ///
    /// ⭐ 巣では二択:
    /// | 親を倒す | 確実に奪える。良い卵。ただし勝てる相手に限る |
    /// | 盗んで逃げる | 格上の巣でも狙えるが、失敗のリスクがある |
    ///
    /// これで「まだ勝てない巣に挑む」動機が生まれ、輪の駆動力になる。
    /// </summary>
    public static class Nests
    {
        /// <summary>段階ごとの、親が持つ野生レベルの合計。
        /// ⚠️ 上限（<see cref="Creatures.WildMax"/> × 3ステ）に届くのは最上位だけ。
        /// ⭐ 生の数を書かない ── 「上限 80」と書いたまま 120 になっていた（2026-08-19 の監査）。</summary>
        public static int WildTotalForTier(int tier)
        {
            // ⭐ 上限からの割合で書く。⚠️ 生の数を並べると、上限を動かした日に
            //    最終段だけが跳ね上がる（80→120 の日に実際そうなった）。
            var ratio = new[] { 0.30, 0.475, 0.65, 0.825, 1.0 };
            var table = new int[ratio.Length];
            for (int i = 0; i < ratio.Length; i++) table[i] = JsRound(Stats.WildTotalMax * ratio[i]);
            int index = tier - 1;
            if (index < 0) index = 0;
            if (index > table.Length - 1) index = table.Length - 1;
            return table[index];
        }

        public static readonly Nest[] All =
        {
            new Nest("shallow-scale", "浅瀬の巣", "tamaru", 1),
            new Nest("thicket-fang", "藪の巣", "tsunoga", 2),
            new Nest("cliff-plume", "崖の巣", "haneru", 3),
            new Nest("deep-scale", "深みの巣", "tamaru", 4),
            new Nest("peak-fang", "嶺の巣", "tsunoga", 5),
        };

        public static Nest ById(string id)
        {
            foreach (var nest in All)
            {
                if (nest.Id == id) return nest;
            }
            throw new ArgumentException($"巣の表に {id} が無い");
        }

        /// <summary>⚠️ JS の <c>Math.round</c> は「0.5 は上へ」。
        /// C# の <c>Math.Round</c> は既定が銀行丸めなので、そのまま使うと系列がずれる。</summary>
        private static int JsRound(double value) => (int)Math.Floor(value + 0.5);

        /// <summary>合計 total を6ステへ配る。偏らせたいので上位3箇所に寄せる。</summary>
        private static StatBlock SpreadWild(Rng rng, int total)
        {
            var keys = new List<StatKey>(Stats.Keys);
            rng.Shuffle(keys);

            // 上位3つに多く配り、残りを下位へ。⭐ 野生も「得意3つ」の形にする
            // ⚠️ 長さは Stats.Keys と揃える。短いと配りの途中で落ちる
            var shares = new[] { 0.34, 0.26, 0.19, 0.11, 0.06, 0.04 };
            var raw = new StatBlock(0, 0, 0, 0);
            int left = total;
            for (int i = 0; i < keys.Count; i++)
            {
                int want = i == keys.Count - 1 ? left : JsRound(total * shares[i]);
                int give = want;
                if (give > left) give = left;
                if (give > Stats.WildStatMax) give = Stats.WildStatMax;
                if (give < 0) give = 0;
                raw = raw.With(keys[i], give);
                left -= give;
            }
            return Stats.ApplyTotalCap(raw);
        }

        /// <summary>枠2・枠3 を引く。⭐ **枠ごとに別の型のプールから1つずつ。**
        ///
        /// ⚠️ 同じプールから2つ取っていた頃は、狙った組み合わせが 2.8〜4.8% でしか出ず、
        /// 「この巣からは何が来るか」も読めなかった。
        /// ⭐ 型を分けると、巣を選ぶ理由が「どの型が欲しいか」になる。
        ///
        /// ⭐ **★→技の格を繋ぐ唯一の場所**（2026-08-27）。<paramref name="maxGrade"/> を超える技は
        /// 引かない（<see cref="SkillValues.GradeOf"/>）。⚠️ 呼び側ごとに意味が違う ──
        /// 卵なら**その卵の★**、雑魚・親なら**巣の段階**（<see cref="Nest.Tier"/>）を渡す
        /// （どちらも「★の代わり」）。
        /// ⭐ 絞った結果が0本のときの落とし先は <see cref="CappedPool"/> を見る。</summary>
        private static void RollSkills23(Rng rng, string speciesId, string skill1, int maxGrade,
            out string? skill2, out string? skill3)
        {
            var pool2 = CappedPool(Skills.SlotPoolOf(speciesId, 1, skill1), maxGrade);
            skill2 = pool2.Count > 0 ? rng.Pick(pool2) : null;

            // ⚠️ 型が違えば重ならないが、同じ技が2枠を占めないことはここで担保する
            var pool3raw = new List<string>();
            foreach (var id in Skills.SlotPoolOf(speciesId, 2, skill1))
            {
                if (id != skill2) pool3raw.Add(id);
            }
            var pool3 = CappedPool(pool3raw, maxGrade);
            skill3 = pool3.Count > 0 ? rng.Pick(pool3) : null;
        }

        /// <summary>そのプールを格 <paramref name="maxGrade"/> 以下へ絞る。
        ///
        /// 🔴 **絞った結果が0本でも、黙って空き枠にしない。**
        /// ⭐ そのプールの中で**一番格が低い技**（同格が複数あれば全部）へ落とす
        /// ── 「★1の卵でも枠2・3が必ず埋まる」を保証する唯一の出所。
        /// ⚠️ 落とし先も「そのプールの最低格」なので、上限（<paramref name="maxGrade"/>）を
        /// 超える技が出ることは無い ── ★の低い卵が格上の技を引く事故は起きない。
        /// ⚠️ 空のプールを渡されたら空のまま返す（呼び側が null に落とす）。</summary>
        private static List<string> CappedPool(IReadOnlyList<string> pool, int maxGrade)
        {
            var fit = new List<string>();
            foreach (var id in pool)
            {
                if (SkillValues.GradeOf(Skills.ById(id)) <= maxGrade) fit.Add(id);
            }
            if (fit.Count > 0 || pool.Count == 0) return fit;

            int lowest = int.MaxValue;
            foreach (var id in pool)
            {
                int grade = SkillValues.GradeOf(Skills.ById(id));
                if (grade < lowest) lowest = grade;
            }
            var fallback = new List<string>();
            foreach (var id in pool)
            {
                if (SkillValues.GradeOf(Skills.ById(id)) == lowest) fallback.Add(id);
            }
            return fallback;
        }

        /// <summary>巣を守るのは親1体だけ。
        ///
        /// ⭐ 発射フェーズで立ちはだかるのも親1体なので、話が繋がる。
        /// ⚠️ 以前は見張り2体を足して3体にしていたが、同じ種族が3体並ぶだけで、
        /// 画面でも戦術でも区別が付かなかった。1体にすると「この親をどう崩すか」に話が絞れる。
        /// HP の埋め合わせは loneScale（体数の比）が持つので、ここでは何もしない。</summary>
        /// <param name="element">⚠️ 既定は種族が昔持っていた属性。
        /// 遊びの中では呼び側（<see cref="Games.DefendersOf"/>）が個体ごとに引いて渡す。</param>
        public static List<Creature> MakeDefenders(Rng rng, Nest nest, Element? element = null)
        {
            var wild = SpreadWild(rng, WildTotalForTier(nest.Tier));
            // ⭐ **親と卵は同じ技を持つ。**「その親の卵」なのに技が無関係、という状態を直した
            //    （2026-08-19）。⚠️ 巣ごとに固定なので、挑み直しても顔ぶれが変わらない。
            // ⚠️ 🔴 **親には★が無い。**代わりに巣の段階（nest.Tier）を格の上限に使う。
            //    卵の実際の★は段階から ±1 ブレる（Rarities.Roll）ので、ブレた回（3回に2回）は
            //    親が見せた技と、実際に手に入る卵の技が食い違いうる ── ★2の卵まで
            //    段階3の技を持たせると「★N は格N以下」が破れるので、卵側は
            //    MakeEggOfRarity で**その卵自身の★**を渡し直している（下記）。
            string? skill2, skill3;
            SkillsOfNest(nest, nest.Tier, out skill2, out skill3);

            return new List<Creature>
            {
                // ⭐ **敵も種族の特性を持つ**（2026-08-21）。⚠️ 持たせていなかった頃は、
                //    特性が味方だけの一方通行で、顔ぶれを見ても何をしてくる相手か読めなかった。
                new Creature($"{nest.Id}-0", nest.SpeciesId, wild, new StatBlock(0, 0, 0, 0), 0,
                    0, skill2, skill3, 0, null, null, 1, null, null, element,
                    Creatures.TraitIdFor(nest.SpeciesId)),
            };
        }

        /// <summary>道中の雑魚の素質。⭐ 親の6割。
        ///
        /// ⚠️ 親と同じにすると **3体ぶんで親より重くなる**。
        /// 親は1体なので体数の比ぶん HP と手数が割増されるが、それでも
        /// 「同じ素質が3つ」には届かない（手数の増分は半分に割り引かれるため）。
        /// ⭐ 雑魚は「取れば楽になる」もの。⚠️ 親より重い関所にしない。</summary>
        public const double MobWildShare = 0.6;

        /// <summary>道中の雑魚3体。⭐ **その深さに居る顔ぶれ**から引く。
        ///
        /// ⭐ 親と同じ種族に固定しない。同じ顔が3体並ぶと画面でも戦術でも区別が付かず、
        /// 「巣ごとに違う戦い」にならない（親を1体にしたときと同じ理由）。
        /// ⚠️ 引く先は <see cref="Encounters.PoolFor"/> ＝ 巣に立ちうる種族。
        /// 深い巣ほど顔ぶれが増えるので、雑魚もそれに従う。
        ///
        /// ⚠️ 乱数は雑魚ごとに分ける（呼び側が <c>mob</c> を渡す）。
        /// 同じ巣の雑魚1と雑魚2が同じ編成になると、2戦目が1戦目の繰り返しになる。</summary>
        /// <param name="mob">何番目の雑魚か。⚠️ 種を分けるためだけに使う。</param>
        public static List<Creature> MakeMobParty(Rng rng, Nest nest, int mob, Element? element = null)
        {
            var pool = Encounters.PoolFor(nest.Tier);
            int total = JsRound(WildTotalForTier(nest.Tier) * MobWildShare);
            // ⚠️ 雑魚もプレイヤーと同じ体数にする（片側だけ多いと LoneScale が働く）
            var party = new List<Creature>(Games.PartySize);

            for (int i = 0; i < Games.PartySize; i++)
            {
                string speciesId = rng.Pick(pool);
                var species = SpeciesTable.ById(speciesId);
                var wild = SpreadWild(rng, total);
                // ⚠️ 雑魚には★が無いので、親と同じく巣の段階を格の上限にする
                //    （雑魚が親より格上の技を持つのは変）。
                string? skill2, skill3;
                RollSkills23(rng, speciesId, species.Skill1, nest.Tier, out skill2, out skill3);
                party.Add(new Creature($"{nest.Id}-m{mob}-{i}", speciesId, wild,
                    new StatBlock(0, 0, 0, 0), 0, 0, skill2, skill3, 0, null, null, 1,
                    null, null, element, Creatures.TraitIdFor(speciesId)));
            }
            return party;
        }

        /// <summary>親から卵を作る。
        /// ⚠️ 盗んだ卵は素質が落ちる。倒したほうが良い卵、という企画どおりにするため。</summary>
        /// <param name="element">⚠️ ここで引かない。呼び側が別の系統（RngElement）で引いて渡す。
        /// 引くと卵の系統がずれて、較正済みの検査が無効になる。</param>
        public static Egg MakeEgg(Rng rng, Nest nest, EggOrigin how, int serial, int rarity = 1,
            Element? element = null)
        {
            int baseTotal = WildTotalForTier(nest.Tier);
            double quality = how == EggOrigin.Defeated ? 1.0 : 0.78;
            int jitter = rng.Int(-3, 4);
            int total = JsRound(baseTotal * quality) + jitter;
            if (total < 4) total = 4;
            if (total > Stats.WildTotalMax) total = Stats.WildTotalMax;

            return new Egg(
                $"e{serial.ToString().PadLeft(3, '0')}",
                nest.SpeciesId,
                SpreadWild(rng, total),
                0, null, null, 1, how,
                hasSkills: false, skill2: null, skill3: null, // 野生の卵。孵すときにガチャ
                rarity: rarity, element: element);
        }

        /// <summary>★が約束する素質の合計。⭐ **★が唯一の見える予告。**
        ///
        /// ⭐ 「★が高い＝強い個体が出る」。孵るまでの時間も★で決まるので、
        /// **見る数字が1つになる**（段階・希少さ・レベルを別々に読まなくてよい）。
        ///
        /// ⚠️ 正典はもともと「希少さは強さを決めない」と決めていた。理由は
        /// 「長く待った卵が必ず強いなら、どれを孵化器に入れるかの選択が消える」。
        /// ⭐ **孵さない使い道（強化素材）ができたので、この懸念は解ける** ──
        /// ★5は「2時間待って強い個体」と「いま強化に使う」の二択になる。
        /// ⚠️ **素材の出口が入るまでは、この選択は成立していない。**先に消すと元の問題が戻る。</summary>
        public static int WildTotalForRarity(int rarity) => WildTotalForTier(Rarities.Clamp(rarity));

        /// <summary>素質の合計から★を逆に引く。⭐ **升の枠を色分けするため。**
        ///
        /// ⭐ 卵は ★ごとの目標値 ±3 で作られる（<see cref="MakeEggOfRarity"/>）ので、
        /// 目標値をそのまま閾値に使えば元の★に戻る。
        /// ⚠️ 配合で生まれた個体は目標値の上に乗らないので、**下の★へ丸める**
        /// （★4の目標に3足りない個体を★4と呼ぶと、枠が実力より良く見える）。</summary>
        public static int RarityOfWildTotal(int total)
        {
            for (int rarity = Rarities.Max; rarity > 1; rarity--)
                if (total >= WildTotalForRarity(rarity) - EggWildJitter) return rarity;
            return 1;
        }

        /// <summary>卵の素質が目標値からぶれる幅。⚠️ <see cref="MakeEggOfRarity"/> と対で動かす。</summary>
        public const int EggWildJitter = 3;

        /// <summary>親から卵を作る（**遊びで使うほう**）。⭐ 素質は★だけで決まる。
        ///
        /// ⚠️ <see cref="MakeEgg"/> は移植元の規則で、較正済みの照合が踏んでいるので残してある。
        /// 両方を混ぜないこと（<see cref="Breeding"/> と <see cref="Fusion"/> と同じ関係）。
        ///
        /// ⚠️ **盗んだ卵の割引をここでは掛けない。**★を引くときに1段下げてあるので、
        /// ここでも掛けると二重に罰することになる（`Rarities.Roll` が唯一の出所）。</summary>
        public static Egg MakeEggOfRarity(Rng rng, Nest nest, EggOrigin how, int serial, int rarity,
            Element? element = null)
        {
            int total = WildTotalForRarity(rarity) + rng.Int(-EggWildJitter, EggWildJitter + 1);
            if (total < 4) total = 4;
            if (total > Stats.WildTotalMax) total = Stats.WildTotalMax;

            // ⭐ **その巣の親が持っている技を、そのまま卵に載せる**（2026-08-19・作者の指示）。
            //
            // ⚠️ 前は `hasSkills: false` で作り、**孵すまで技が分からなかった**。
            //    1つの巣から出る5個（盗み4＋撃破1）が全部バラバラの技だったので、
            //    「この技が欲しいからこの巣を攻略する」という動機が成立しなかった
            //    ── 卵1個の実時間の 84% が「狙った種族の巣を探す」コストなのに、
            //    掘り当てても中身が読めない、という形だった。
            //
            // ⭐ これで巣が**中身の読める箱**になる。⚠️ 確率は1ミリも動かしていない
            //    （同じ袋から同じように引いている）。動いたのは「いつ分かるか」だけ。
            //
            // ⭐ 🔴 **ここで渡す rarity が「この卵自身の★」**（2026-08-27・★→技の格）。
            //    ⚠️ 巣の段階（nest.Tier）ではない ── 段階を使うと、段階からブレて
            //    低い★になった卵（Rarities.Roll の ±1）が、段階なりの格の技を引いてしまい
            //    「★N は格N以下」が破れる。★を渡すことで、その卵の格上限は必ずその卵の★になる。
            SkillsOfNest(nest, rarity, out string? skill2, out string? skill3);

            return new Egg(
                $"e{serial.ToString().PadLeft(3, '0')}",
                nest.SpeciesId,
                SpreadWild(rng, total),
                0, null, null, 1, how,
                hasSkills: true, skill2: skill2, skill3: skill3,
                rarity: rarity, element: element);
        }

        /// <summary>その巣が抱えている技。⭐ **巣ごとに固定**（何度見ても同じ）。
        ///
        /// ⭐ 雑魚の編成が既にこの形（<see cref="Steal.RngFor"/>）なので、それに揃えた。
        /// ⚠️ 揃える前は、雑魚だけ固定で**親と卵は挑むたびに変わる**という非対称だった。
        ///
        /// ⚠️ 巣の id と、その巣を何回盗んだかは**混ぜない**。盗んでも中身は変わらない
        /// （変わると「この巣を掘り切る」という判断が成り立たない）。
        ///
        /// <param name="rarity">⭐ 技の格の上限（★→格・2026-08-27）。「巣ごとに固定」なのは
        /// 変わらない ── 同じ (巣, rarity) を渡せば何度呼んでも同じ技が返る。
        /// ⚠️ 呼び側が変える値なのは <paramref name="rarity"/> だけ（<see cref="MakeDefenders"/>
        /// は巣の段階、<see cref="MakeEggOfRarity"/> は卵自身の★）。</param></summary>
        public static void SkillsOfNest(Nest nest, int rarity, out string? skill2, out string? skill3)
        {
            var species = SpeciesTable.ById(nest.SpeciesId);
            var rng = new Rng(0).Stream($"nest-skills:{nest.Id}");
            RollSkills23(rng, nest.SpeciesId, species.Skill1, rarity, out skill2, out skill3);
        }

        /// <summary>孵す。⭐ 野生の卵はここでスキル2・3のガチャを引く。
        /// 配合の卵は既に決まっているのでそのまま使う。</summary>
        /// <summary>偏り4本は、卵が持っていないときの引き直し結果。
        /// ⚠️ ここで乱数を引かない — 引くと既にある hatch の系統がずれて、
        /// 較正済みの検査が無効になる。呼び側が別の系統で引いて渡す。
        /// ⚠️ **特性は受け取らない。**⭐ 種族から決まる（2026-08-21・作者の指示）。</summary>
        /// <param name="paletteIndex">⭐ **色**（2026-08-21）。0 は通常色。
        /// ⚠️ ここで引かない ── 呼び側が専用の系統（<c>RngPalette</c>）で引いて渡す
        /// （<see cref="SpeciesTable.RollPalette"/>）。</param>
        public static Creature Hatch(Rng rng, Egg egg, string id,
            StatKey? strong = null, StatKey? weak = null,
            StatKey? best = null, StatKey? worst = null, int paletteIndex = 0)
        {
            var species = SpeciesTable.ById(egg.SpeciesId);
            string? skill2 = egg.Skill2;
            string? skill3 = egg.Skill3;
            if (!egg.HasSkills)
            {
                // ⭐ 野生の卵（技が未確定）はここで初めて★→格が働く（egg.Rarity が上限）。
                RollSkills23(rng, egg.SpeciesId, species.Skill1, egg.Rarity, out skill2, out skill3);
            }

            return new Creature(id, egg.SpeciesId, egg.Wild, new StatBlock(0, 0, 0, 0), 0,
                egg.MutationCounter, skill2, skill3, paletteIndex,
                egg.ParentA, egg.ParentB, egg.Generation,
                egg.Strong ?? strong, egg.Weak ?? weak, egg.Element,
                Creatures.TraitIdFor(egg.SpeciesId), egg.Best ?? best, egg.Worst ?? worst);
        }

        /// <summary>偏りを4本引く。⚠️ 6ステから**別々に**4つ取る（重ならない）。
        ///
        /// ⭐ **引き方は今までどおり1回の切り直し**（<c>Shuffle</c>）。
        /// 前は先頭2枚だけ見ていたのを4枚見るようにしただけなので、
        /// ⚠️ 乱数を引く回数は変わらない ── 較正済みの列がずれない。</summary>
        public static void RollSlant(Rng rng, out StatKey best, out StatKey strong,
            out StatKey weak, out StatKey worst)
        {
            var keys = new List<StatKey>(Stats.Keys);
            rng.Shuffle(keys);
            best = keys[0];
            strong = keys[1];
            weak = keys[2];
            worst = keys[3];
        }

        // ── ボス ─────────────────────────────────────────

        /// <summary>最後の壁。⭐ 手で書いた固定の相手にしてある。
        ///
        /// 巣の守り手は挑むたびに顔ぶれが変わるが、ボスは毎回同じ。
        /// ⭐ そうしないと「何が足りないか考えて、配合で作って、挑み直す」という
        /// 輪の駆動力が働かない（相手が毎回変わるなら対策の立てようがない）。</summary>
        public const string BossName = "淵のヌシ";

        /// <summary>⭐ ヌシ1体だけ。眷属は置かない。
        ///
        /// ⚠️ 以前は眷属2体（壁と撹乱）を付けていたが、同じ画面に3体並ぶと
        /// 「どれを狙うか」が作業になり、ヌシ本体に一度も触れないまま負けることがあった。
        /// 1体にすると、難しさがその1体の技の噛み合いだけで決まる。
        ///
        /// ⭐ 変異を4回重ねた個体という扱い。上限が 44/132 に上がるので、
        /// ボス専用の例外ルールを足さずに強くできる。
        /// ⭐ 震撼（全体強攻撃）は枠2へ。枠1は CT が無いので、大技はここに置いて CT を効かせる。</summary>
        public static List<Creature> MakeBossParty()
        {
            // ⚠️ 2026-08-21 に**上限を押し上げる役が世代へ移った**。
            //    ⭐ 素質は 1ステ上限 44（40+4段）で移植元とまったく同じ数になる。
            //    ⚠️ 変異カウンタ 4 はそのまま持たせる ── 移植元が記録している値で、
            //    いまは**色と見た目の由来**でしかないが、動かすと照合が壊れる。
            const int generation = 5;
            const int mutationCounter = 4;
            // ⭐ 抵抗を厚く持たせる。⚠️ ここが 0 だと、弱化を積むだけで
            //    ヌシが一度も動かないまま終わる（速度3の個体は元々そうなりやすい）。
            //    ⚠️ 命中は低め ── ヌシの弱化まで通ると、事故で一方的になる。
            var wild = Stats.ApplyTotalCap(new StatBlock(16, 22, 21, 3, 8, 24), generation);
            return new List<Creature>
            {
                new Creature("boss-0", "nushi", wild, new StatBlock(0, 0, 0, 0), 0,
                    mutationCounter, "attack-all-heavy", "spd-down", 0, null, null, generation,
                    null, null, null, Creatures.TraitIdFor("nushi")),
            };
        }

        /// <summary>巣の表に抜けが無いか数える検査。</summary>
        public static void Audit()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            foreach (var nest in All) ids.Add(nest.Id);
            if (ids.Count != All.Length) problems.Add("巣の id が重複している");

            foreach (var nest in All)
            {
                // 存在しない種族を指していないか（指していると孵した瞬間に落ちる）
                var species = SpeciesTable.ById(nest.SpeciesId);
                if (Skills.GachaPoolOf(nest.SpeciesId, species.Skill1).Count == 0)
                {
                    problems.Add($"{nest.Id}: 卵ガチャのプールが空");
                }
                if (nest.Tier < 1) problems.Add($"{nest.Id}: 段階が {nest.Tier}");
            }

            if (problems.Count > 0)
                throw new InvalidOperationException("巣の表の不備:\n  " + string.Join("\n  ", problems));
        }
    }
}
