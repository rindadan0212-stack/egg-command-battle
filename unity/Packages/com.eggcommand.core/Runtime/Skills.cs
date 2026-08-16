#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>誰に効くか。</summary>
    public enum Target
    {
        /// <summary>敵1体</summary>
        EnemyOne,
        /// <summary>敵全体</summary>
        EnemyAll,
        /// <summary>残 HP 割合が最も低い味方（自分を含む）</summary>
        AllyLowest,
        Self,
    }

    /// <summary>効き目の段位。
    ///
    /// ⭐ 技ごとに数値を置かない。段位を選ぶだけにする。
    /// 独立した数値が「技の数」から効果の種類ごとに4つまで減り、
    /// 較正は表を動かすだけで済む（勘で置いた数値が散らばらない）。
    ///
    /// ⚠️ 全体に効くものは1段下げて選ぶ。
    /// 全体の「中」は単体の「中」よりずっと強いので、同じ段位にすると段位が意味を失う。</summary>
    public enum PowerTier
    {
        Small,
        Medium,
        Large,
        Huge,
    }

    /// <summary>ダメージが何のステで伸びるか。</summary>
    public enum DamageScale
    {
        Atk,
        Def,
    }

    public enum EffectKind
    {
        Damage,
        Buff,
        Poison,
        Regen,
        HealRatio,
        Shield,
        Stun,
        Ct,
        Taunt,
        Guts,
        Immune,
    }

    /// <summary>効果のプリミティブ。
    ///
    /// ⚠️ ここを増やすときは、本当に組み合わせで表せないか先に疑う。
    /// ⚠️ 持続するものの単位は「その個体の行動回数」。CT と同じ数え方に揃えてある。
    ///
    /// 種類ごとに型を分けず1つの型に畳んであるのは、TS 側が判別共用体の**データ表**として
    /// 持っているのと同じ形にするため。作り方は下の静的メソッドに寄せて、
    /// 意味の無い組み合わせを外から作れないようにしている。</summary>
    public sealed class Effect
    {
        public readonly EffectKind Kind;
        /// <summary>damage</summary>
        public readonly PowerTier Power;
        /// <summary>damage</summary>
        public readonly DamageScale Scale;
        /// <summary>buff</summary>
        public readonly StatKey Stat;
        /// <summary>buff: +1 で UP、-1 で DOWN</summary>
        public readonly int Sign;
        /// <summary>buff / poison / regen / stun / guts / immune</summary>
        public readonly int Turns;
        /// <summary>poison / regen。⭐ スタックする</summary>
        public readonly int Stacks;
        /// <summary>healRatio。⚠️ 技ごとに割合が違う（段位を使わない）</summary>
        public readonly int Percent;
        /// <summary>shield。⭐ 点数ではなく枚数</summary>
        public readonly int Count;
        /// <summary>ct。負で短縮・正で延長</summary>
        public readonly int Delta;
        /// <summary>taunt</summary>
        public readonly int Hits;

        private Effect(EffectKind kind, PowerTier power, DamageScale scale, StatKey stat, int sign,
            int turns, int stacks, int percent, int count, int delta, int hits)
        {
            Kind = kind;
            Power = power;
            Scale = scale;
            Stat = stat;
            Sign = sign;
            Turns = turns;
            Stacks = stacks;
            Percent = percent;
            Count = count;
            Delta = delta;
            Hits = hits;
        }

        /// <summary>scale が Def のものは「防御が高いほど強い一撃」になる。</summary>
        public static Effect Damage(PowerTier power, DamageScale scale) =>
            new Effect(EffectKind.Damage, power, scale, default, 0, 0, 0, 0, 0, 0, 0);

        /// <summary>攻撃力/防御力/スピードの UP・DOWN。⚠️ 効き目は一律 <see cref="Skills.BuffPercent"/>。段位は使わない。</summary>
        public static Effect Buff(StatKey stat, int sign, int turns)
        {
            if (stat != StatKey.Atk && stat != StatKey.Def && stat != StatKey.Spd)
                throw new ArgumentException($"buff は atk/def/spd のみ（{stat} が渡された）");
            if (sign != 1 && sign != -1)
                throw new ArgumentException($"buff の sign は ±1（{sign} が渡された）");
            return new Effect(EffectKind.Buff, default, default, stat, sign, turns, 0, 0, 0, 0, 0);
        }

        /// <summary>毒。1行動ごとに最大HPの TickPercent × スタック数 ぶん減る。</summary>
        public static Effect Poison(int stacks, int turns) =>
            new Effect(EffectKind.Poison, default, default, default, 0, turns, stacks, 0, 0, 0, 0);

        /// <summary>リジェネ。1行動ごとに回復。</summary>
        public static Effect Regen(int stacks, int turns) =>
            new Effect(EffectKind.Regen, default, default, default, 0, turns, stacks, 0, 0, 0, 0);

        /// <summary>HP割合回復。即時。</summary>
        public static Effect HealRatio(int percent) =>
            new Effect(EffectKind.HealRatio, default, default, default, 0, 0, 0, percent, 0, 0, 0);

        /// <summary>シールド。1回の攻撃につき1枚消費し、その攻撃を威力に関係なく完全に無効化する。
        /// ⭐ つまり「大きな一撃」に強く、「手数」に弱い。</summary>
        public static Effect Shield(int count) =>
            new Effect(EffectKind.Shield, default, default, default, 0, 0, 0, 0, count, 0, 0);

        /// <summary>スタン。その回数ぶん手番を飛ばす。</summary>
        public static Effect Stun(int turns) =>
            new Effect(EffectKind.Stun, default, default, default, 0, turns, 0, 0, 0, 0, 0);

        /// <summary>CT短縮（負）/ CT延長（正）。⚠️ 枠1には効かない。</summary>
        public static Effect Ct(int delta) =>
            new Effect(EffectKind.Ct, default, default, default, 0, 0, 0, 0, 0, delta, 0);

        /// <summary>挑発。味方への単体攻撃を、あと hits 回ぶん自分が引き受ける。</summary>
        public static Effect Taunt(int hits) =>
            new Effect(EffectKind.Taunt, default, default, default, 0, 0, 0, 0, 0, 0, hits);

        /// <summary>ガッツ。致死のダメージを HP1 で耐える。</summary>
        public static Effect Guts(int turns) =>
            new Effect(EffectKind.Guts, default, default, default, 0, turns, 0, 0, 0, 0, 0);

        /// <summary>免疫。DOWN・毒・スタンを受けない。</summary>
        public static Effect Immune(int turns) =>
            new Effect(EffectKind.Immune, default, default, default, 0, turns, 0, 0, 0, 0, 0);
    }

    public sealed class Skill
    {
        public readonly string Id;
        public readonly string Name;
        /// <summary>何をするスキルなのかの短い説明。</summary>
        public readonly string Gist;
        /// <summary>使ったあと、自分が何回行動するまで使えないか。⚠️ 枠1では常に 0 扱い。</summary>
        public readonly int Ct;
        public readonly Target Target;
        public readonly IReadOnlyList<Effect> Effects;

        public Skill(string id, string name, string gist, int ct, Target target, params Effect[] effects)
        {
            Id = id;
            Name = name;
            Gist = gist;
            Ct = ct;
            Target = target;
            Effects = effects;
        }
    }

    /// <summary>スキル表。
    ///
    /// ⚠️ 「たたかう」は無い。枠1が CT 0 なので、全スキルが CT 中でも必ず打てる札が残る。
    /// ⚠️ スキルを個別にコードで書かない。効果のプリミティブの組み合わせをデータで表す。
    /// ⚠️ 効果の名前は画面にそのまま出す語。凝った名前を付けない。</summary>
    public static class Skills
    {
        /// <summary>攻撃の威力。</summary>
        public static int DamagePowerOf(PowerTier tier)
        {
            switch (tier)
            {
                case PowerTier.Small: return 12;
                case PowerTier.Medium: return 20;
                case PowerTier.Large: return 30;
                case PowerTier.Huge: return 42;
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        /// <summary>画面に出す段位の語。⚠️ TS 側は段位そのものがこの文字列。</summary>
        public static string LabelOf(PowerTier tier)
        {
            switch (tier)
            {
                case PowerTier.Small: return "小";
                case PowerTier.Medium: return "中";
                case PowerTier.Large: return "大";
                case PowerTier.Huge: return "特大";
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        /// <summary>ステータス系が動かす割合（%）。⭐ ステータスの数値そのものに掛かる。
        /// ⚠️ 段位を使わない。威力とは別の軸なので揃えない。UP も DOWN も一律この値。</summary>
        public const int BuffPercent = 30;

        /// <summary>毒・リジェネの1スタックが、1行動ごとに動かす最大HP の割合（%）。
        /// ⭐ スタックする。2重なら 10%、3重なら 15%。
        /// ⚠️ 上限を置いていない。掛け続けられると青天井になる形なので、実測で見張る。</summary>
        public const int TickPercent = 5;

        private static readonly Skill[] List =
        {
            // ── 攻撃 ──────────────────────────────
            new Skill("attack", "攻撃", "敵1体にダメージ", 3, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Atk)),
            new Skill("attack-heavy", "強攻撃", "敵1体に大きなダメージ。次が遠い", 6, Target.EnemyOne,
                Effect.Damage(PowerTier.Large, DamageScale.Atk)),
            // ⚠️ 全体なので1段下げて「小」
            new Skill("attack-all", "全体攻撃", "敵全体にダメージ", 5, Target.EnemyAll,
                Effect.Damage(PowerTier.Small, DamageScale.Atk)),
            new Skill("attack-all-heavy", "全体強攻撃", "敵全体に大きなダメージ。次がとても遠い", 7, Target.EnemyAll,
                Effect.Damage(PowerTier.Large, DamageScale.Atk)),
            new Skill("attack-def", "防御依存攻撃", "防御力が高いほど強い一撃", 3, Target.EnemyOne,
                Effect.Damage(PowerTier.Medium, DamageScale.Def)),

            // ── ステータス系 ──────────────────────
            new Skill("atk-up", "攻撃力UP", "自分の攻撃力を上げる", 4, Target.Self,
                Effect.Buff(StatKey.Atk, 1, 3)),
            new Skill("atk-down", "攻撃力DOWN", "敵1体の攻撃力を下げる", 4, Target.EnemyOne,
                Effect.Buff(StatKey.Atk, -1, 3)),
            new Skill("def-up", "防御力UP", "自分の防御力を上げる", 4, Target.Self,
                Effect.Buff(StatKey.Def, 1, 3)),
            new Skill("def-down", "防御力DOWN", "敵1体の防御力を下げる", 4, Target.EnemyOne,
                Effect.Buff(StatKey.Def, -1, 3)),
            new Skill("spd-up", "スピードUP", "自分のスピードを上げる", 4, Target.Self,
                Effect.Buff(StatKey.Spd, 1, 3)),
            new Skill("spd-down", "スピードDOWN", "敵1体のスピードを下げる", 4, Target.EnemyOne,
                Effect.Buff(StatKey.Spd, -1, 3)),

            // ── HP系 ──────────────────────────────
            new Skill("poison", "毒", "敵1体が行動するたびに削れる", 5, Target.EnemyOne,
                Effect.Poison(1, 4)),
            new Skill("regen", "リジェネ", "味方1体が行動するたびに回復する", 5, Target.AllyLowest,
                Effect.Regen(1, 4)),
            new Skill("heal-ratio", "HP割合回復", "味方1体の HP を最大値の割合ぶん回復", 4, Target.AllyLowest,
                Effect.HealRatio(30)),
            new Skill("shield", "シールド", "味方1体に、HP より先に減る盾を張る", 4, Target.AllyLowest,
                Effect.Shield(2)),

            // ── 行動系 ────────────────────────────
            new Skill("stun", "スタン", "敵1体の手番を飛ばす", 6, Target.EnemyOne,
                Effect.Stun(1)),
            new Skill("ct-short", "CT短縮", "自分の技の待ちを縮める", 4, Target.Self,
                Effect.Ct(-2)),
            new Skill("ct-long", "CT延長", "敵1体の技の待ちを延ばす", 5, Target.EnemyOne,
                Effect.Ct(2)),
            new Skill("taunt", "挑発", "味方への攻撃を自分が引き受ける", 3, Target.Self,
                Effect.Taunt(3)),

            // ── 特殊 ──────────────────────────────
            new Skill("guts", "ガッツ", "致命傷を HP1 で耐える", 6, Target.Self,
                Effect.Guts(3)),
            new Skill("immune", "免疫", "DOWN・毒・スタンを受けなくなる", 5, Target.Self,
                Effect.Immune(3)),
        };

        public static IReadOnlyList<Skill> All => List;

        private static readonly Dictionary<string, Skill> Index = BuildIndex();

        private static Dictionary<string, Skill> BuildIndex()
        {
            var map = new Dictionary<string, Skill>(List.Length);
            foreach (var skill in List) map.Add(skill.Id, skill);
            return map;
        }

        /// <summary>知らない id を黙って握りつぶさない。表に無いものは「効かないだけ」で気づけないため。</summary>
        public static Skill ById(string id)
        {
            Skill? skill;
            if (!Index.TryGetValue(id, out skill)) throw new ArgumentException($"スキル表に {id} が無い");
            return skill!;
        }

        /// <summary>⭐ 枠1（種族固定）の CT は常に 0。
        ///
        /// ⚠️ CT は技ではなく枠の性質として扱う。
        /// 同じ技が、ある種族では枠1（CTなし）に、別の種族では枠2・3（CTあり）に入りうるため。</summary>
        public static int EffectiveCt(int slot, Skill skill) => slot == 0 ? 0 : skill.Ct;

        /// <summary>弱い側の効果か（免疫が防ぐ対象）。</summary>
        public static bool IsHarmful(Effect effect)
        {
            if (effect.Kind == EffectKind.Buff) return effect.Sign < 0;
            return effect.Kind == EffectKind.Poison || effect.Kind == EffectKind.Stun;
        }

        /// <summary>卵ガチャ（枠2・3）で出うるスキル。
        ///
        /// ⭐ 種族ごとにプールを分ける。全体プールにすると、どこで卵を奪っても同じ技が出るので
        /// 「必要な技を持つ親の巣へ行く」という輪の駆動力が消える。</summary>
        private static readonly Dictionary<string, string[]> GachaPools = new Dictionary<string, string[]>
        {
            // 鱗・守りの系統
            { "tamaru", new[] { "def-up", "taunt", "shield", "heal-ratio", "guts", "attack", "ct-long" } },
            // 牙・攻めの系統
            { "tsunoga", new[] { "atk-up", "def-down", "attack-heavy", "ct-short", "poison", "attack-def", "stun" } },
            // 羽・撹乱の系統
            { "haneru", new[] { "spd-up", "spd-down", "atk-down", "stun", "regen", "ct-long", "immune" } },
            // ヌシ。⚠️ 卵は落とさないが、表に無いと数える検査が落ちる
            { "nushi", new[] { "def-up", "spd-down", "taunt", "guts", "immune", "attack-all-heavy" } },
        };

        /// <summary>技表とガチャプールの整合を数える。
        ///
        /// ⭐ **件数を数えない。** 数えると技を足すたびに落ちるので、
        /// 検査を緩める圧力になる（そして緩めたら二度と戻らない）。
        /// 見るのは「規則を守っているか」と「繋がっているか」だけ。
        ///
        /// ⚠️ ここが catch するのは、足した日には気づけない類のものばかり:
        /// AI が採点しない効果 / どの巣からも出ない技 / 実在しない id を指すプール。
        /// どれも**コンパイルは通り、遊べてしまう**。</summary>
        public static void Audit()
        {
            var problems = new List<string>();

            var seen = new HashSet<string>();
            foreach (var skill in List)
            {
                if (!seen.Add(skill.Id)) problems.Add($"技 id が重複している: {skill.Id}");
                if (skill.Effects.Count == 0) problems.Add($"{skill.Id}: 効果が1つも無い");
                if (skill.Name.Length == 0) problems.Add($"{skill.Id}: 名前が空");
                if (skill.Gist.Length == 0) problems.Add($"{skill.Id}: 画面に出す短い説明が空");
                if (skill.Ct < 0) problems.Add($"{skill.Id}: CT が {skill.Ct}");

                foreach (var effect in skill.Effects)
                {
                    if (!Ai.Knows(effect.Kind))
                    {
                        problems.Add(
                            $"{skill.Id}: {effect.Kind} を AI が採点しない。" +
                            "スコア0になって**永久に選ばれない技**になる（Ai.ScoreOf に case を足す）");
                    }
                }
            }

            // ── 卵ガチャ。⭐ ここが「技を手に入れる唯一の経路」なので、切れていると入手不能になる
            var reachable = new HashSet<string>();
            foreach (var species in SpeciesTable.All)
            {
                reachable.Add(species.Skill1);

                string[]? pool;
                if (!GachaPools.TryGetValue(species.Id, out pool))
                {
                    problems.Add($"{species.Id}: 卵ガチャのプールが無い（種族を足したら必ず要る）");
                    continue;
                }

                var inPool = new HashSet<string>();
                foreach (var id in pool!)
                {
                    if (!Index.ContainsKey(id)) problems.Add($"{species.Id} のプールが実在しない技 {id} を指している");
                    if (!inPool.Add(id)) problems.Add($"{species.Id} のプールで {id} が重複している");
                    reachable.Add(id);
                }

                // ⭐ 枠2・3 を別々に引くので、枠1を除いて2件は要る
                int usable = GachaPoolOf(species.Id, species.Skill1).Count;
                if (usable < 2)
                {
                    problems.Add($"{species.Id}: 枠1を除いたプールが {usable} 件。枠2・3 を別々に引けない");
                }
            }

            foreach (var skill in List)
            {
                if (!reachable.Contains(skill.Id))
                {
                    problems.Add($"{skill.Id}: どの種族の枠1にもプールにも無い。**手に入らない技**になっている");
                }
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException("技表の不備:\n  " + string.Join("\n  ", problems));
            }
        }

        /// <summary>その種族の卵から出うる技。⚠️ 表に無い種族は黙って空にせず投げる。
        /// 枠1（種族固定）と同じ技はここで外す。</summary>
        public static List<string> GachaPoolOf(string speciesId, string skill1)
        {
            string[]? pool;
            if (!GachaPools.TryGetValue(speciesId, out pool))
                throw new ArgumentException($"卵ガチャの表に {speciesId} が無い");

            var result = new List<string>(pool!.Length);
            foreach (var id in pool!)
            {
                if (id != skill1) result.Add(id);
            }
            return result;
        }
    }
}
