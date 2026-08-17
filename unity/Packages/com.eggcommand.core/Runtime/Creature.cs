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

        /// <summary>3すくみの属性。⭐ **種族ではなく個体が持つ**。
        /// 炎のタマルも水のタマルも生まれる。配合では親のどちらかから受け継ぐ。</summary>
        public readonly Element Element;

        /// <summary>1つだけ持つ特性。⭐ **技の3枠を奪わない**（表は <see cref="Traits"/>）。
        ///
        /// ⭐ 特性は技そのものを強くせず「動き」を強くするので、
        /// 「この個体には低確率の大技を持たせる」という**組み合わせの判断**が生まれる。
        /// ⚠️ null は「持たない」。移植元にはこの概念が無いので、
        /// 較正済みの検査が作る個体は null のまま＝従来と1ビットも変わらない。</summary>
        public readonly string? TraitId;

        /// <summary>枠ごとに注ぎ込んだスキルポイント。⭐ **レベルは導出する**（保存しない）。
        ///
        /// ⭐ 卵を孵さずに素材として食わせると溜まる（<see cref="Games.FeedEggToSkill"/>）。
        /// ⚠️ **配合すると個体ごと消える。**それを承知で強化するかどうかがプレイヤーの選択。
        /// ⚠️ 個体の中でここと <see cref="Trained"/>・<see cref="Earned"/> だけが書き換わる。</summary>
        public readonly int[] SkillPoints = new int[3];

        public Creature(string id, string speciesId, StatBlock wild, StatBlock trained, int earned,
            int mutationCounter, string? skill2, string? skill3, int paletteIndex,
            string? parentA, string? parentB, int generation,
            StatKey? strong = null, StatKey? weak = null, Element? element = null,
            string? traitId = null)
        {
            TraitId = traitId;
            Strong = strong;
            Weak = weak;
            // ⚠️ 指定が無ければ、その種族が昔持っていた属性にする。
            //    属性を個体へ移す前のセーブと、移植元との照合が、これで動かずに済む
            Element = element ?? Migrations.ElementOf(speciesId);
            Id = id;
            SpeciesId = speciesId;
            Wild = wild;
            Trained = trained;
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
        public const int TrainMax = 20;

        public static Species SpeciesOf(Creature creature) => SpeciesTable.ById(creature.SpeciesId);

        public static int SpentOf(Creature creature) => Stats.TotalOf(creature.Trained);

        /// <summary>まだ振っていない育成ポイント。</summary>
        public static int UnspentOf(Creature creature) => creature.Earned - SpentOf(creature);

        /// <summary>戦闘の報酬。⚠️ 上限を超えて溜めない。</summary>
        public static void Award(Creature creature, int amount)
        {
            int next = creature.Earned + amount;
            creature.Earned = next > TrainMax ? TrainMax : next;
        }

        /// <summary>育てる。⭐ 振り先は選ばせない — その個体の**得意**へ自動で乗る。
        ///
        /// ⭐ 選ばせないのは、選択になっていなかったから。上限も対価も無い ＋1 は
        /// 「多いほど良い」でしかなく、答えは画面が既に教えている。
        /// ⭐ 得意の方向へ乗るので、個体ごとに違う形に育つ（全部が同じ最適形へ収束しない）。
        /// ⚠️ 得意を持たない個体（移植元と同じ作り）は素質の高い順に乗せる。
        /// </summary>
        /// <returns>実際に伸びた点数。上限に達していれば 0。</returns>
        public static int Grow(Creature creature, int amount)
        {
            int before = creature.Earned;
            Award(creature, amount);
            int gained = creature.Earned - before;
            for (int i = 0; i < gained; i++)
            {
                var key = creature.Strong ?? Tallest(creature);
                creature.Trained = creature.Trained.With(key, creature.Trained[key] + 1);
            }
            return gained;
        }

        private static StatKey Tallest(Creature creature)
        {
            var best = Stats.Keys[0];
            foreach (var key in Stats.Keys)
            {
                if (creature.Wild[key] > creature.Wild[best]) best = key;
            }
            return best;
        }

        /// <summary>1点を振る。⚠️ 戻せない（取り返しがつかないほうが判断に重みが出る）。
        /// ⚠️ 遊びからは外した（<see cref="Grow"/> が自動で振る）。移植元との照合のために残す。</summary>
        public static void SpendPoint(Creature creature, StatKey key)
        {
            if (UnspentOf(creature) <= 0)
                throw new InvalidOperationException($"{creature.Id} に振れる育成ポイントが無い");
            creature.Trained = creature.Trained.With(key, creature.Trained[key] + 1);
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

        /// <summary>実値。唯一の出所は <see cref="Stats"/>。ここは種族基礎を渡すだけ。
        /// ⭐ 最後に得意・不得意を掛ける。⚠️ 持っていない個体（移植元と同じ作り）は素通り。</summary>
        public static StatBlock StatsOf(Creature creature)
        {
            var actual = Stats.ActualStats(SpeciesOf(creature).Base, creature.Wild, creature.Trained);
            return Slanted(actual, creature.Strong, creature.Weak);
        }

        /// <summary>得意を上げ、不得意を下げる。⚠️ 同じキーなら何もしない（打ち消し合う）。</summary>
        public static StatBlock Slanted(StatBlock stats, StatKey? strong, StatKey? weak)
        {
            if (strong == null || weak == null || strong.Value == weak.Value) return stats;
            var work = stats
                .With(strong.Value, Scale(stats[strong.Value], 1.0 + Slant))
                .With(weak.Value, Scale(stats[weak.Value], 1.0 - Slant));
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

        /// <summary>属性だけ差し替えた同じ個体。⚠️ 個体は作り直す（欄は書き換えない）。</summary>
        public static Creature WithElement(Creature c, Element element) => new Creature(
            c.Id, c.SpeciesId, c.Wild, c.Trained, c.Earned, c.MutationCounter,
            c.Skill2, c.Skill3, c.PaletteIndex, c.ParentA, c.ParentB, c.Generation,
            c.Strong, c.Weak, element, c.TraitId);

        /// <summary>その枠のスキルレベル。⭐ ポイントから**導出**する（第2の出所を作らない）。</summary>
        public static int SkillLevelOf(Creature creature, int slot) =>
            slot < 0 || slot >= creature.SkillPoints.Length
                ? 1
                : SkillCosts.LevelOf(creature.SkillPoints[slot]);

        /// <summary>その枠の技に、レベルぶんの上乗せを載せたもの。⚠️ Lv1 なら素のまま。</summary>
        public static SkillBoost SkillBoostOf(Creature creature, int slot)
        {
            var list = SkillsOf(creature);
            var skill = slot >= 0 && slot < list.Length ? list[slot] : null;
            if (skill == null) return new SkillBoost();
            return Skills.BoostOf(skill, SkillLevelOf(creature, slot), slot);
        }

        /// <summary>その個体の特性。⚠️ 持たなければ null（表を引かない）。
        /// ⚠️ **まだ誰も呼んでいない。**特性を出す画面が無いため
        /// （課題「特性が画面に一度も出ていない」）。画面ができたらここを引く。</summary>
        public static Trait? TraitOf(Creature creature) =>
            creature.TraitId == null ? null : Traits.ById(creature.TraitId);

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
