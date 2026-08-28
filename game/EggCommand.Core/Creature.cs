#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>個体。
    ///
    /// ⚠️ 導出できるものは保存しない。
    /// スキル枠1は種族固定なので個体に持たせない — 持たせると種族と食い違いうる
    /// 第2の出所になる。実値も同じ理由で保存せず、毎回 <see cref="Stats"/> で計算する。
    /// </summary>
    public sealed class Creature
    {
        public readonly string Id;
        public readonly string SpeciesId;

        /// <summary>遺伝で決まる素質。変えられない。合計上限は適用済みの値だけを入れる。</summary>
        public readonly StatBlock Wild;

        /// <summary>育成でプレイヤーが振った分。
        /// ⚠️ 個体の中でここと <see cref="Earned"/> だけが書き換わる。素質は変えられない。</summary>
        public StatBlock Trained;

        /// <summary>戦闘で得た育成ポイントの総数（振った分 + 未使用）。</summary>
        public int Earned;

        /// <summary>🔴 **どのステに何点振ったか**（2026-08-26・ARK式の自由配分）。
        ///
        /// ⭐ <see cref="Trained"/> は**ここから導出する**（第2の出所を作らない）。
        /// ⚠️ **振り直しはできない**（作者の決定）── 一度振った点は戻せない。
        /// ⭐ ただし**配合すると子は 0 から**なので、血統を進めれば振り直しの機会になる。
        /// ⚠️ 合計は <see cref="Earned"/> を超えない。余りが「未使用ポイント」
        /// （<see cref="Creatures.UnspentOf"/>）。</summary>
        public StatBlock Points;

        /// <summary>変異カウンタ。⚠️ 両親とも20以上だと子に変異が出ない（無限強化のブレーキ）。</summary>
        public readonly int MutationCounter;

        /// <summary>枠2・3 のみ。⚠️ 枠1は種族から導出する。null は「空き枠」。</summary>
        public readonly string? Skill2;
        public readonly string? Skill3;

        /// <summary>種族のパレット添字。変異は色変化として出る。</summary>
        public readonly int PaletteIndex;

        public readonly string? ParentA;
        public readonly string? ParentB;
        public readonly int Generation;

        /// <summary>生まれつきの得意・不得意。⭐ 遺伝するが**伸ばせない**。
        ///
        /// ⭐ これが「合計が高い＝良い個体」を崩す。同じ合計でも形が違う。
        /// ⭐ 育てた分はここ（得意）へ自動で乗るので、振り先を選ばせなくてよい。
        /// ⚠️ null は「持たない」。移植元にはこの概念が無いので、
        /// 較正済みの検査が作る個体は null のまま＝従来と1つも変わらない。</summary>
        public readonly StatKey? Strong;
        public readonly StatKey? Weak;

        /// <summary>⭐ **大得意・大不得意**（2026-08-21・作者の指示）。
        ///
        /// ⚠️ 足した理由は強さではなく**厳選の目盛り**。得意1・不得意1だけだと、
        /// 6ステのうち2本しか個体差が出ず、⭐ 引き直す面白さが薄かった。
        /// 4本（大得意・得意・不得意・大不得意）なら、同じ素質合計でも形が
        /// <c>6*5*4*3 = 360</c> 通りに割れる（2本のときは 30 通り）。
        ///
        /// ⚠️ **4つとも別のステ。**同じステに重なると打ち消し合って軸が消える。
        /// ⚠️ null は「持たない」（<see cref="Strong"/> と同じ約束）。
        /// 古い保存と、較正済みの検査が作る個体はここが null のまま。</summary>
        public readonly StatKey? Best;
        public readonly StatKey? Worst;

        /// <summary>3すくみの属性。⭐ **種族ではなく個体が持つ**。
        /// 炎のタマルも水のタマルも生まれる。配合では親のどちらかから受け継ぐ。</summary>
        public readonly Element Element;

        /// <summary>1つだけ持つ特性。⭐ **技の3枠を奪わない**（表は <see cref="Traits"/>）。
        ///
        /// ⚠️ **遊びの中では必ず種族のもの**（<see cref="Species.TraitId"/>）が入る。
        /// 2026-08-21 まで個体ごとに引いていたのをやめた（作者の指示）。
        /// ⭐ 欄として残してあるのは、戦闘が「特性を持たない個体」も扱えるようにするため
        /// ── 移植元の照合（goldens）と、特性ひとつぶんを測る検査がここに乗る。
        /// ⚠️ **生む側で null のまま通さない。**孵化・配合・巣の顔ぶれは
        /// <see cref="Creatures.TraitIdFor"/> を通す（種族から引く唯一の口）。</summary>
        public readonly string? TraitId;

        /// <summary>枠ごとに注ぎ込んだスキルポイント。⭐ **レベルは導出する**（保存しない）。
        ///
        /// ⭐ 卵を孵さずに素材として食わせると溜まる（<see cref="Games.FeedEggToSkill"/>）。
        /// ⚠️ **配合すると個体ごと消える。**それを承知で強化するかどうかがプレイヤーの選択。
        /// ⚠️ 個体の中でここと <see cref="Trained"/>・<see cref="Earned"/> だけが書き換わる。</summary>
        public readonly int[] SkillPoints = new int[3];

        /// <param name="points">⭐ 🔴 2026-08-27（監査で発覚）: 元は引数に無く、<see cref="Points"/> が
        /// 既定値 <c>(0,0,0,0)</c> のまま残っていた ── <see cref="Snapshot"/> が構築後に
        /// 手で補って回避していた（`made.Points = points;`）が、<see cref="Creatures.WithElement"/>
        /// はそれを忘れて地雷のまま残っていた。⭐ この型自身が「第2の出所を作らない」と
        /// 随所に書いているので、`Points` だけ例外にしない ── ここへ足すのが素直。
        /// ⚠️ **既定値 <c>default</c>（＝全ステ0）にしてあるのは、他の頁を同時に書き換えている
        /// 者がいて、その呼び出し（`new Creature(...)`）を編集できない制約があったため。**
        /// 省略した呼び出しは今までどおり `Trained` も `(0,0,0,0)` で作っている箇所ばかりで、
        /// その場合は既定値のままで挙動は変わらない。⚠️ **ただし「振った点を反映した
        /// `Trained` を渡すのに `points` を省略する」呼び出しを新しく足さないこと** ──
        /// それをやると `UnspentOf` が「まだ全部余っている」と嘘をつき、同じ点を
        /// 二度振れる（<see cref="Snapshot"/> が一度踏んだのと同じ事故）。</param>
        public Creature(string id, string speciesId, StatBlock wild, StatBlock trained, int earned,
            int mutationCounter, string? skill2, string? skill3, int paletteIndex,
            string? parentA, string? parentB, int generation,
            StatKey? strong = null, StatKey? weak = null, Element? element = null,
            string? traitId = null, StatKey? best = null, StatKey? worst = null,
            StatBlock points = default)
        {
            TraitId = traitId;
            Strong = strong;
            Weak = weak;
            Best = best;
            Worst = worst;
            // ⚠️ 指定が無ければ、その種族が昔持っていた属性にする。
            //    属性を個体へ移す前のセーブと、移植元との照合が、これで動かずに済む
            Element = element ?? Migrations.ElementOf(speciesId);
            Id = id;
            SpeciesId = speciesId;
            Wild = wild;
            Trained = trained;
            Points = points;
            Earned = earned;
            MutationCounter = mutationCounter;
            Skill2 = skill2;
            Skill3 = skill3;
            PaletteIndex = paletteIndex;
            ParentA = parentA;
            ParentB = parentB;
            Generation = generation;
        }
    }

    public static class Creatures
    {
        /// <summary>育成ポイントの上限。
        ///
        /// ⭐ 戦闘に勝つ（または盗みに成功する）と、出撃していた個体が +1 もらう。
        /// 「連れ出す」ことが育成に直結するので、強い個体を使うほど伸びる。
        /// ⚠️ 上限があるので「時間さえかければ素質差を埋められる」にはならない
        /// （素質＝厳選の成果が勝敗を決める、という軸を守るため）。</summary>
        /// 🔴 **2026-08-26 に 20 → 50**（作者の決定）。⭐ 6ステへ自由に配るので、
        /// 1ステに寄せれば旧来（全ステに20Lv）より遥かに尖り、均等に散らせば薄くなる。
        public const int TrainMax = 50;

        public static Species SpeciesOf(Creature creature) => SpeciesTable.ById(creature.SpeciesId);

        /// <summary>育てた分の合計。⭐ 並べ替えと画面の表示に使う。</summary>
        public static int SpentOf(Creature creature) => Stats.TotalOf(creature.Trained);

        /// <summary>1レベルで伸びる割合（素質に対する千分率）。0 なら
        /// <see cref="GrowthFlatOf"/> のほうで伸びる。
        ///
        /// ⭐ **平らな ＋1 ではなく「素質の何%」。**（作者の判断 2026-08-19）
        /// ⚠️ ＋1 を平らに配っていたとき、1点の価値がステで **22倍** ちがった
        /// （<c>sim statvalue</c> 実測: HP 2.24pt / 弱化命中 0.10pt）。
        /// 単位が違うものを 1点 ＝ 1点 で配っていたのが原因。
        ///
        /// ⭐ 割合にすると **素質の高い個体はその分野の伸びも大きくなる** ──
        /// 「素質が高い個体がその分野で有利になっていく」というこのゲーム共通の考え方が、
        /// 育成にもそのまま通る。
        ///
        /// ⚠️ **値は勘で置かない。**<c>sim statvalue</c> の「1レベルぶんの価値」が
        /// 揃うところを測って決める（1点の価値が低いステほど多く配る）。</summary>
        public static int GrowthPermilOf(StatKey key)
        {
            switch (key)
            {
                // 1点あたり 2.24pt（一番高い）→ 一番少なく配る
                // ⚠️ 50 では3つの種すべてで HP だけ 1割ほど高く出た（測って下げた）
                case StatKey.Hp: return 50;
                // 1点あたり 1.82pt
                // ⚠️ 60 では3つの種すべてで速度だけ 1割ほど高く出た（測って下げた）
                case StatKey.Spd: return 60;
                // 1点あたり 1.48pt
                case StatKey.Atk: return 70;
                // 1点あたり 1.39pt
                case StatKey.Def: return 90;
                // ⚠️ 弱化命中・弱化耐性は割合で伸ばさない。理由は GrowthFlatOf
                default: return 0;
            }
        }

        /// <summary>1レベルで伸びる**平らな**量。⭐ 割合で伸ばせないステのため。
        ///
        /// ⚠️ **弱化命中・弱化耐性は「差」で効く。**通る率は <c>(命中 − 抵抗) / 2</c> という
        /// **引き算**なので、両者を割合で伸ばすと**差まで倍になる**。
        /// 実際 8%/Lv だと、素質 14〜54 の幅が Lv20 で 36〜140 に開き、
        /// 通る率の差が 20pt → **52pt**（帯は床25%〜天井95% の 70pt しかない）。
        /// ほとんどの組み合わせが床か天井に貼り付いて、軸が死ぬ。
        ///
        /// ⭐ **比で効くステ（HP・攻撃・防御・速度）は割合でよい。**
        /// 攻撃と防御の式（<see cref="Battle.DamageOf"/>）・<c>GaugeBase+速度</c>・
        /// <c>HP×HpScale</c> はどれも比なので、
        /// 両者が同じ割合で伸びても釣り合いは動かない。
        ///
        /// ⭐ 平らに配れば、通る率の差は**素質の差のまま**保たれる。</summary>
        public static int GrowthFlatOf(StatKey key)
        {
            switch (key)
            {
                // ⚠️ 実値の単位。野生レベル1点ぶん（＝ Stats.Scale）に揃える
                // 🔴 **2026-08-26 に Stats.Scale(5) → 1。**⭐ 命中/耐性は `Stats.DebuffScale`
                //    の目盛り（0〜150）なので、育成も1点=+1 でないと桁が合わない
                //    （50点振って +50 ＝ 素質100 の個体が 150 に届く、という設計）。
                case StatKey.Acc:
                case StatKey.Res: return 1;
                default: return 0;
            }
        }

        /// <summary>育てる前の実値（＝素質）。⭐ 種族の基礎値 ＋ 野生レベル。
        /// ⚠️ 得意・不得意の ±15% は**掛けない** ── <see cref="Slanted"/> が
        /// 最後に実値へ掛けるので、ここで掛けると二重になる。</summary>
        public static StatBlock BornStatsOf(string speciesId, StatBlock wild) =>
            Stats.ActualStats(SpeciesTable.ById(speciesId).Base, wild, new StatBlock(0, 0, 0, 0));

        /// <summary>育てた分。⭐ **Lv から必ず一意に決まる**（素質 × 割合 × Lv）。
        ///
        /// ⚠️ 足し込みで持たない。1レベルぶんを毎回丸めて足すと誤差が積もり、
        /// 同じ素質・同じ Lv の個体でも数が食い違う。⭐ 毎回ゼロから作り直す。
        /// ⭐ 一意に決まるので、古い保存も読むときに作り直せる（<c>Snapshot</c>）。</summary>
        /// <summary>🔴 **振った点から育成ぶんを作る**（2026-08-26・ARK式）。
        /// ⭐ ステごとに**そのステへ振った点数**で伸びる。⚠️ 伸び方（割合／平ら）は
        /// <see cref="GrowthPermilOf"/>・<see cref="GrowthFlatOf"/> のまま動かしていない
        /// ── 変えるとステ間の1点の価値が揃わなくなる（2026-08-19 に実測で揃えた）。</summary>
        public static StatBlock TrainedFor(string speciesId, StatBlock wild, StatBlock points)
        {
            var born = BornStatsOf(speciesId, wild);
            var made = new StatBlock(0, 0, 0, 0);
            foreach (var key in Stats.Keys)
            {
                int n = points[key];
                if (n <= 0) continue;
                double grown = (double)born[key] * GrowthPermilOf(key) * n / 1000.0
                    + GrowthFlatOf(key) * n;
                made = made.With(key, (int)Math.Floor(grown + 0.5));
            }
            return made;
        }

        /// <summary>未使用の点。⚠️ 振った合計が <see cref="Creature.Earned"/> を超えることは無い。</summary>
        public static int UnspentOf(Creature creature) =>
            creature.Earned - Stats.TotalOf(creature.Points);

        /// <summary>🔴 **振る。**⭐ 振り直しはできない（作者の決定）。
        /// ⚠️ 未使用より多くは振れない。実際に振れた点数を返す。</summary>
        public static int Spend(Creature creature, StatKey key, int amount)
        {
            if (amount <= 0) return 0;
            int room = UnspentOf(creature);
            if (room <= 0) return 0;
            int n = amount > room ? room : amount;
            creature.Points = creature.Points.With(key, creature.Points[key] + n);
            creature.Trained = TrainedFor(creature.SpeciesId, creature.Wild, creature.Points);
            return n;
        }

        /// <summary>⭐ **自動で振る**（NPC 用 ── 巣の守り手・ヌシ・試練の相手）。
        /// ⚠️ 遊ぶ側の個体には使わない（振り先を選ぶのが遊びなので）。
        /// ⭐ 素質の高いステほど多く振る ＝「得意を伸ばす」── NPC の形が意図的に見える。</summary>
        public static void AutoSpend(Creature creature)
        {
            int room = UnspentOf(creature);
            if (room <= 0) return;
            int total = Stats.TotalOf(creature.Wild);
            var points = creature.Points;
            if (total <= 0)
            {
                // ⚠️ 素質が全部 0 の個体（検査が作る形）。⭐ 均等に配る
                int given = 0;
                foreach (var key in Stats.Keys)
                {
                    int n = room / Stats.Keys.Length;
                    points = points.With(key, points[key] + n);
                    given += n;
                }
                // ⚠️ 🔴 2026-08-27（監査で発覚）: 割り算の余り（最大 Stats.Keys.Length-1＝5点）が
                //    どこにも足されず捨てられていた ── 隣の分岐（下）は同じ余りを
                //    「一番素質が高いステへ寄せる（捨てない）」と決めているのに、ここだけ
                //    その規則を破っていた。⭐ ここは素質が全部同点（0）で「一番高い」が
                //    定まらないので、`top` の初期値と同じ規則（同点なら先頭のキーを勝たせる）
                //    で先頭へ寄せる。
                int rest = room - given;
                if (rest > 0) points = points.With(Stats.Keys[0], points[Stats.Keys[0]] + rest);
            }
            else
            {
                int given = 0;
                foreach (var key in Stats.Keys)
                {
                    int n = room * creature.Wild[key] / total;
                    points = points.With(key, points[key] + n);
                    given += n;
                }
                // ⚠️ 割り算の余りは、一番素質が高いステへ寄せる（捨てない）
                int rest = room - given;
                if (rest > 0)
                {
                    var top = Stats.Keys[0];
                    foreach (var key in Stats.Keys)
                        if (creature.Wild[key] > creature.Wild[top]) top = key;
                    points = points.With(top, points[top] + rest);
                }
            }
            creature.Points = points;
            creature.Trained = TrainedFor(creature.SpeciesId, creature.Wild, creature.Points);
        }

        /// <summary>⚠️ **N 点を全ステに振ったときの伸び。**⭐ 「1ステに N 点振ると
        /// そのステがどれだけ伸びるか」を <c>[key]</c> で取るために残してある（`sim` が使う）。
        /// ⚠️ 遊びの配分は <see cref="TrainedFor(string, StatBlock, StatBlock)"/> のほう。</summary>
        public static StatBlock TrainedFor(string speciesId, StatBlock wild, int earned)
        {
            if (earned <= 0) return new StatBlock(0, 0, 0, 0);
            var born = BornStatsOf(speciesId, wild);
            var made = new StatBlock(0, 0, 0, 0);
            foreach (var key in Stats.Keys)
            {
                double grown = (double)born[key] * GrowthPermilOf(key) * earned / 1000.0
                    + GrowthFlatOf(key) * earned;
                made = made.With(key, (int)Math.Floor(grown + 0.5));
            }
            return made;
        }

        /// <summary>戦闘の報酬。⚠️ 上限を超えて溜めない。</summary>
        public static void Award(Creature creature, int amount)
        {
            int next = creature.Earned + amount;
            creature.Earned = next > TrainMax ? TrainMax : next;
        }

        /// <summary>育てる。⭐ **1レベルにつき、6ステすべてが「素質の何%」ずつ伸びる。**
        ///
        /// ⚠️ 直す前は**得意1本**にだけ乗せ、そのあと**平らに ＋1**にした。
        /// どちらも作者の指摘で作り替えている（2026-08-19）:
        /// ・得意1本 → 「生まれた個体が弱かったら絶対に使いみちがない」
        /// ・平らな ＋1 → 「ステの種類によって ＋1 の価値が全然違う」（実測で 22倍 の開き）
        ///
        /// ⭐ 割合にすると、素質の高い個体はその分野の伸びも大きくなる。
        /// ⭐ **得意・不得意の補正は掛け直さない。**<see cref="Slanted"/> が
        /// 最後に実値へ ±15% を掛けるので、育てた分にも自動で乗る。
        ///
        /// ⚠️ 振り先は選ばせない（上限も対価も無い ＋1 は選択になっていない）。
        /// </summary>
        /// <returns>実際に上がったレベル。上限に達していれば 0。</returns>
        public static int Grow(Creature creature, int amount)
        {
            int before = creature.Earned;
            Award(creature, amount);
            int gained = creature.Earned - before;
            if (gained <= 0) return 0;
            // 🔴 **ここでは振らない**（2026-08-26）。⭐ 振り先を選ぶのが遊びなので、
            //    得た点は「未使用」のまま置く（`Creatures.Spend` で振る）。
            return gained;
        }

        /// <summary>NPC の育成。⭐ 点を配って**その場で自動で振る**。
        /// ⚠️ 巣の守り手・ヌシ・試練の相手など、**選ぶ人が居ない個体**にだけ使う。</summary>
        public static int GrowAuto(Creature creature, int amount)
        {
            int gained = Grow(creature, amount);
            AutoSpend(creature);
            return gained;
        }

        /// <summary>3枠ぶんのスキル。⭐ 枠1は必ず種族のもの。空き枠は null。</summary>
        public static Skill?[] SkillsOf(Creature creature)
        {
            var species = SpeciesOf(creature);
            return new Skill?[]
            {
                Skills.ById(species.Skill1),
                creature.Skill2 == null ? null : Skills.ById(creature.Skill2),
                creature.Skill3 == null ? null : Skills.ById(creature.Skill3),
            };
        }

        /// <summary>得意・不得意の増減。⭐ ±15%。
        /// ⚠️ 大きくすると「得意なステだけ見ればいい」になり、素質の意味が薄れる。</summary>
        public const double Slant = 0.15;

        /// <summary>大得意・大不得意の増減。⭐ ±30%（得意のちょうど2倍）。
        ///
        /// ⭐ 2倍にしたのは、**表の▲▲と▲が別物だと数で分かる**ようにするため。
        /// 1.5倍だと画面で並べても差が読めず、目盛りを増やした意味が出ない。
        /// ⚠️ 4本の増減を足すと <c>+30 +15 -15 -30 = 0</c> ── ⭐ **合計は動かない。**
        /// 増えるのは「どこに寄っているか」だけで、良い個体の総量は変えていない。</summary>
        public const double GreatSlant = 0.30;

        /// <summary>実値。唯一の出所は <see cref="Stats"/>。ここは種族基礎を渡すだけ。
        /// ⭐ 最後に得意・不得意を掛ける。⚠️ 持っていない個体（移植元と同じ作り）は素通り。</summary>
        public static StatBlock StatsOf(Creature creature)
        {
            var actual = Stats.ActualStats(SpeciesOf(creature).Base, creature.Wild, creature.Trained);
            return Slanted(actual, creature);
        }

        /// <summary>その個体の偏りを掛ける。⭐ **画面もここを通す**（掛け方を写さない）。</summary>
        public static StatBlock Slanted(StatBlock stats, Creature creature) =>
            Slanted(stats, creature.Strong, creature.Weak, creature.Best, creature.Worst);

        /// <summary>得意を上げ、不得意を下げる。
        ///
        /// 4本（大得意・大不得意 ±30% / 得意・不得意 ±15%）を**別々に掛ける**。
        /// 同じキーに重なった組は**両方とも捨てる**（掛けても打ち消し合うだけで、
        /// どちらが効いたのか画面と食い違う）。生む側が4本を別ステで配るので、
        /// ここに来るのは古い保存と、偏りを持たない個体だけ。</summary>
        public static StatBlock Slanted(StatBlock stats, StatKey? strong, StatKey? weak,
            StatKey? best = null, StatKey? worst = null)
        {
            var work = stats;
            if (best != null && worst != null && best.Value != worst.Value)
            {
                work = work
                    .With(best.Value, Scale(work[best.Value], 1.0 + GreatSlant))
                    .With(worst.Value, Scale(work[worst.Value], 1.0 - GreatSlant));
            }
            if (strong != null && weak != null && strong.Value != weak.Value)
            {
                work = work
                    .With(strong.Value, Scale(work[strong.Value], 1.0 + Slant))
                    .With(weak.Value, Scale(work[weak.Value], 1.0 - Slant));
            }
            return work;
        }

        /// <summary>⚠️ JS の Math.round は「0.5 は上へ」。C# の既定は銀行丸めなので合わせる。
        /// ⚠️ 1 未満にしない（0 にすると割り算のある式が壊れる）。</summary>
        private static int Scale(int value, double by)
        {
            int scaled = (int)Math.Floor(value * by + 0.5);
            return scaled < 1 ? 1 : scaled;
        }

        /// <summary>野生レベルの合計。厳選の目安として並べ替えに使う。</summary>
        public static int WildTotalOf(Creature creature) => Stats.TotalOf(creature.Wild);

        /// <summary>属性だけ差し替えた同じ個体。⚠️ 個体は作り直す（欄は書き換えない）。
        /// ⭐ 🔴 2026-08-27: **`Points` も引き継ぐ。**忘れると、振った点を反映した
        /// `Trained` はそのまま渡るのに `Points` だけ `(0,0,0,0)` に戻り、`UnspentOf` が
        /// 「まだ全部余っている」と嘘をついて同じ点を二度振れるようになる
        /// （`Snapshot` が一度踏んだのと同じ事故 ── 監査で地雷のまま残っているのが見つかった）。</summary>
        public static Creature WithElement(Creature c, Element element) => new Creature(
            c.Id, c.SpeciesId, c.Wild, c.Trained, c.Earned, c.MutationCounter,
            c.Skill2, c.Skill3, c.PaletteIndex, c.ParentA, c.ParentB, c.Generation,
            c.Strong, c.Weak, element, c.TraitId, c.Best, c.Worst, c.Points);

        /// <summary>その枠のスキルレベル。⭐ ポイントから**導出**する（第2の出所を作らない）。
        /// ⚠️ 上限は**技ごと**（<see cref="Skills.MaxLevelOf"/>）。空き枠は技が無いので
        /// 全体の天井（<see cref="Skills.MaxLevel"/>）のまま渡す（どのみち points は 0）。</summary>
        public static int SkillLevelOf(Creature creature, int slot)
        {
            if (slot < 0 || slot >= creature.SkillPoints.Length) return 1;
            var skill = SkillsOf(creature)[slot];
            int maxLevel = skill == null ? Skills.MaxLevel : Skills.MaxLevelOf(skill);
            return SkillCosts.LevelOf(creature.SkillPoints[slot], maxLevel);
        }

        /// <summary>その枠の技に、レベルぶんの上乗せを載せたもの。⚠️ Lv1 なら素のまま。</summary>
        public static SkillBoost SkillBoostOf(Creature creature, int slot)
        {
            var list = SkillsOf(creature);
            var skill = slot >= 0 && slot < list.Length ? list[slot] : null;
            if (skill == null) return new SkillBoost();
            return Skills.BoostOf(skill, SkillLevelOf(creature, slot), slot);
        }

        /// <summary>その個体の特性。⚠️ 持たなければ null（表を引かない）。</summary>
        public static Trait? TraitOf(Creature creature) =>
            creature.TraitId == null ? null : Traits.ById(creature.TraitId);

        /// <summary>⭐ **その種族が持つ特性。生む側はここだけを通す。**
        ///
        /// ⚠️ 産地ごとに <c>SpeciesTable.ById(id).TraitId</c> と書き写さない ──
        /// 写した数だけ「1か所だけ直し忘れる」余地ができる（巣の親は特性つき、
        /// 道中の雑魚は無し、のような食い違いは画面から読み取れない）。</summary>
        public static string TraitIdFor(string speciesId) => SpeciesTable.ById(speciesId).TraitId;

        /// <summary>その個体のパレット。添字が範囲外なら黙って通常色にせず投げる。</summary>
        public static Palette PaletteOf(Creature creature)
        {
            var species = SpeciesOf(creature);
            if (creature.PaletteIndex < 0 || creature.PaletteIndex >= species.Palettes.Count)
                throw new ArgumentException($"{species.Id} にパレット添字 {creature.PaletteIndex} が無い");
            return species.Palettes[creature.PaletteIndex];
        }
    }
}
