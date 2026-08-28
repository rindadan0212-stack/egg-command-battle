#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>⭐ **技を組み合わせで作り直す**（`sim brew`・2026-08-27・作者の指示）。
    ///
    /// ⭐ 考え方: **効果の語彙はもう固まっている**（毒・スタン・シールド…）。
    /// 残る伸びしろは「**どれだけ多くの組を作れるか**」なので、
    /// 部品を決めて、簡単な決まりの下で機械的に組む。
    /// ⚠️ 確率・CT・効き目の細かい釣り合いは**後で見る** ── まず種類を出す。
    ///
    /// ⚠️ **これは候補を数える道具であって、技表を書き換える道具ではない。**
    /// 出てくるのは「作りうる技の一覧と、その格の散らばり」。
    /// ⭐ ここから選んだものを `Core.Skills` へ書く。
    ///
    /// 決まり（いまのところ4つ）:
    ///   ① **単体のバフ・デバフは3ターン以上**（作者の指示）
    ///   ② 1つの技に効果は **1〜3つ**
    ///   ③ **ダメージ部品は1つまで**（多段は発数で表す）
    ///   ④ **狙う側は混ぜてよい**（2026-08-27・作者の指示）── 味方強化と敵弱化、
    ///      自分に掛けてから撃つ、自分に弱化を負う代わりに深く入る、まで作る</summary>
    public static class Brew
    {
        /// <summary>技の部品。⭐ **効果1つ ＋ 短い語**。名前はここから機械的に作る。</summary>
        private sealed class Part
        {
            public string Word = "";          // 画面に出す短い語
            public string Id = "";            // 機械の id
            public Effect Effect = null!;
            public Side To;                   // 誰に向く部品か
            public bool IsDamage;
        }

        private enum Side { Foe, Ally, Own }

        /// <summary>単体のバフ・デバフの最短の持続（作者の決まり①）。</summary>
        private const int MinTurns = 3;

        private static List<Part> Attacks()
        {
            var list = new List<Part>();
            void Add(string id, string word, PowerTier p, DamageScale s, int repeat, bool pierce)
                => list.Add(new Part
                {
                    Id = id, Word = word, To = Side.Foe, IsDamage = true,
                    Effect = Effect.Damage(p, s, repeat, pierce),
                });

            // ⭐ 素直な一撃（依存を変えると、育て方の違う個体の火力になる）
            Add("hit-s", "小撃", PowerTier.Small, DamageScale.Atk, 1, false);
            Add("hit-m", "中撃", PowerTier.Medium, DamageScale.Atk, 1, false);
            Add("hit-l", "大撃", PowerTier.Large, DamageScale.Atk, 1, false);
            Add("hit-def", "堅撃", PowerTier.Medium, DamageScale.Def, 1, false);
            Add("hit-spd", "疾撃", PowerTier.Small, DamageScale.Spd, 1, false);
            // ⭐ 多段（盾を剥がす・毎回判定できる）
            Add("hit-s2", "連撃", PowerTier.Small, DamageScale.Atk, 2, false);
            Add("hit-s3", "乱撃", PowerTier.Small, DamageScale.Atk, 3, false);
            Add("hit-m2", "重連撃", PowerTier.Medium, DamageScale.Atk, 2, false);
            // ⭐ 防御を踏み倒す（硬い相手への回答）
            Add("hit-pierce", "貫撃", PowerTier.Medium, DamageScale.Atk, 1, true);
            return list;
        }

        private static List<Part> Debuffs()
        {
            var list = new List<Part>();
            void Add(string id, string word, Effect e)
                => list.Add(new Part { Id = id, Word = word, To = Side.Foe, Effect = e });

            Add("atk-dn", "攻減", Effect.Buff(StatKey.Atk, -1, MinTurns));
            Add("def-dn", "防減", Effect.Buff(StatKey.Def, -1, MinTurns));
            Add("spd-dn", "速減", Effect.Buff(StatKey.Spd, -1, MinTurns));
            Add("poison1", "毒", Effect.Poison(1, 4));
            Add("poison2", "猛毒", Effect.Poison(2, 4));
            Add("stun", "麻痺", Effect.Stun(1));
            Add("sleep", "眠り", Effect.Sleep(2));
            Add("taunt", "挑発", Effect.Taunt(3));
            Add("block", "封", Effect.Block(MinTurns));
            Add("ct-up", "遅延", Effect.Ct(2));
            Add("gauge-dn", "後退", Effect.Gauge(-40));
            Add("dispel", "剥がし", Effect.Dispel(1));
            Add("steal", "奪取", Effect.Steal(1));
            Add("shave", "削り", Effect.HealRatio(-20));
            return list;
        }

        private static List<Part> Boons()
        {
            var list = new List<Part>();
            void Add(string id, string word, Effect e)
                => list.Add(new Part { Id = id, Word = word, To = Side.Ally, Effect = e });

            Add("atk-up", "攻増", Effect.Buff(StatKey.Atk, 1, MinTurns));
            Add("def-up", "防増", Effect.Buff(StatKey.Def, 1, MinTurns));
            Add("spd-up", "速増", Effect.Buff(StatKey.Spd, 1, MinTurns));
            Add("shield", "盾", Effect.Shield(2));
            Add("regen", "再生", Effect.Regen(1, 4));
            Add("guts", "根性", Effect.Guts(MinTurns));
            Add("immune", "護り", Effect.Immune(MinTurns));
            Add("ct-dn", "加速", Effect.Ct(-2));
            Add("gauge-up", "前進", Effect.Gauge(30));
            Add("heal", "回復", Effect.HealRatio(30));
            Add("heal-big", "大回復", Effect.HealRatio(55));
            Add("cleanse", "浄化", Effect.Cleanse(1));
            Add("revive", "蘇生", Effect.Revive(40));
            return list;
        }

        /// <summary>⭐ **自分が負う代償。**⚠️ 弱化なので `Skills.PriceOf` では値引きになる。
        /// 見返りに重い一撃を撃つ形（捨て身）を作るための部品。</summary>
        private static List<Part> SelfCosts()
        {
            var list = new List<Part>();
            void Add(string id, string word, Effect e)
                => list.Add(new Part { Id = id, Word = word, To = Side.Own, Effect = e.To(Target.Self) });
            Add("c-def", "無防備", Effect.Buff(StatKey.Def, -1, MinTurns));
            Add("c-spd", "鈍足", Effect.Buff(StatKey.Spd, -1, MinTurns));
            Add("c-atk", "消耗", Effect.Buff(StatKey.Atk, -1, MinTurns));
            Add("c-gauge", "反動", Effect.Gauge(-40));
            return list;
        }

        /// <summary>⭐ **代償と引き換えの重い一撃。**⚠️ ここだけ特大（3.0倍）を使う
        /// ── 表にありながら、どの技もまだ使っていなかった段位。</summary>
        private static List<Part> Heavy()
        {
            var list = new List<Part>();
            void Add(string id, string word, PowerTier p, DamageScale s, int repeat)
                => list.Add(new Part
                {
                    Id = id, Word = word, To = Side.Foe, IsDamage = true,
                    Effect = Effect.Damage(p, s, repeat, false),
                });
            Add("hit-xl", "特大撃", PowerTier.Huge, DamageScale.Atk, 1);
            Add("hit-l2", "大連撃", PowerTier.Large, DamageScale.Atk, 2);
            Add("hit-m3", "重乱撃", PowerTier.Medium, DamageScale.Atk, 3);
            return list;
        }

        /// <summary>⭐ 「殴って自分が得をする」型の相棒。</summary>
        private static List<Part> SelfGains()
        {
            var list = new List<Part>();
            void Add(string id, string word, Effect e)
                => list.Add(new Part { Id = id, Word = word, To = Side.Own, Effect = e.To(Target.Self) });
            Add("s-heal", "吸収", Effect.HealRatio(15));
            Add("s-atk", "気勢", Effect.Buff(StatKey.Atk, 1, MinTurns));
            Add("s-spd", "疾走", Effect.Buff(StatKey.Spd, 1, MinTurns));
            Add("s-shield", "身構え", Effect.Shield(1));
            Add("s-gauge", "踏込", Effect.Gauge(30));
            return list;
        }

        public static void Run(string[] args)
        {
            var attacks = Attacks();
            var debuffs = Debuffs();
            var boons = Boons();
            var gains = SelfGains();

            var made = new List<(Skill Skill, string Shape, string[] Parts)>();
            string[] parts = Array.Empty<string>();
            void Use(params string[] ids) => parts = ids;
            void Keep(string id, string name, SkillType type, Target at, string shape,
                params Effect[] effects) =>
                made.Add((new Skill(id, name, GistOf(shape), type, at, effects), shape,
                    parts.Length > 0 ? parts : new[] { id }));

            // ── ① 単品（部品そのまま）──────────────────────────
            foreach (var a in attacks) { Use(a.Id); Keep("b-" + a.Id, a.Word, SkillType.Attack,
                Target.EnemyOne, "単品", a.Effect); }
            foreach (var d in debuffs) { Use(d.Id); Keep("b-" + d.Id, d.Word, SkillType.Debuff,
                Target.EnemyOne, "単品", d.Effect); }
            foreach (var b in boons) { Use(b.Id); Keep("b-" + b.Id, b.Word, TypeOfBoon(b),
                Target.AllyOne, "単品", b.Effect); }

            // ── ② 全体版（効き目は1段下げる約束なので、威力だけ落とす）────────
            foreach (var a in attacks)
            {
                var e = Weaker(a.Effect);
                if (e == null) continue;
                { Use(a.Id); Keep("b-all-" + a.Id, a.Word + "・全", SkillType.Attack,
                    Target.EnemyAll, "全体", e); }
            }
            foreach (var d in debuffs)
            {
                var e = Weaker(d.Effect);
                if (e == null) continue;
                { Use(d.Id); Keep("b-all-" + d.Id, d.Word + "・全", SkillType.Debuff, Target.EnemyAll, "全体", e); }
            }
            foreach (var b in boons)
            {
                var e = Weaker(b.Effect);
                if (e == null) continue;
                { Use(b.Id); Keep("b-all-" + b.Id, b.Word + "・全", TypeOfBoon(b), Target.AllyAll, "全体", e); }
            }

            // ── ③ 殴りながら置く（攻撃 ＋ 弱化1つ）⭐ ここが一番数が出る ────
            foreach (var a in attacks)
                foreach (var d in debuffs)
                    { Use(a.Id, d.Id); Keep($"b-{a.Id}-{d.Id}", a.Word + d.Word, SkillType.Attack,
                        Target.EnemyOne, "攻＋弱", a.Effect, d.Effect); }

            // ── ④ 弱化2つ（狙いを1つに絞って重ねる）────────────────
            for (int i = 0; i < debuffs.Count; i++)
                for (int j = i + 1; j < debuffs.Count; j++)
                    { Use(debuffs[i].Id, debuffs[j].Id); Keep($"b-{debuffs[i].Id}-{debuffs[j].Id}", debuffs[i].Word + debuffs[j].Word,
                        SkillType.Debuff, Target.EnemyOne, "弱＋弱",
                        debuffs[i].Effect, debuffs[j].Effect); }

            // ── ⑤ 強化2つ（配る側の厚み）──────────────────────
            for (int i = 0; i < boons.Count; i++)
                for (int j = i + 1; j < boons.Count; j++)
                    { Use(boons[i].Id, boons[j].Id); Keep($"b-{boons[i].Id}-{boons[j].Id}", boons[i].Word + boons[j].Word,
                        TypeOfBoon(boons[i]), Target.AllyOne, "強＋強",
                        boons[i].Effect, boons[j].Effect); }

            // ── ⑥ 殴って自分が得をする（攻撃のあとに自分へ）────────────
            foreach (var a in attacks)
                foreach (var g in gains)
                    { Use(a.Id, g.Id); Keep($"b-{a.Id}-{g.Id}", a.Word + g.Word, SkillType.Attack,
                        Target.EnemyOne, "攻→自", a.Effect, g.Effect); }

            // ── ⑦ ⭐ **自分に掛けてから撃つ**（前置き。その一撃自体が伸びる）──────
            //    ⚠️ 効果を書く順が意味を持つ（`Battle` の前置き）。攻増→大撃 は
            //    その大撃が実際に 50% 増しで入り、さらに3ターン残る。
            foreach (var g in gains)
                foreach (var a in attacks.Concat(Heavy()))
                    { Use(g.Id, a.Id); Keep($"b-{g.Id}-{a.Id}", g.Word + a.Word, SkillType.Attack,
                        Target.EnemyOne, "自→攻", g.Effect, a.Effect); }

            // ── ⑧ ⭐ **代償を負って深く入る**（自分に弱化 ＋ 重い一撃）──────────
            foreach (var c in SelfCosts())
                foreach (var a in Heavy())
                    { Use(c.Id, a.Id); Keep($"b-{c.Id}-{a.Id}", c.Word + a.Word, SkillType.Attack,
                        Target.EnemyOne, "代償", a.Effect, c.Effect); }

            // ── ⑨ ⭐ **味方を上げながら敵を下げる**（1手で盤面を両方動かす）────────
            foreach (var b in boons)
                foreach (var d in debuffs)
                    { Use(b.Id, d.Id); Keep($"b-{b.Id}-{d.Id}", b.Word + d.Word, SkillType.Debuff,
                        Target.EnemyOne, "強＋弱", d.Effect, b.Effect.To(Target.AllyOne)); }

            // ── ⑩ ⭐ **全体を上げながら全体を下げる**（鬨の声の型）──────────────
            foreach (var b in boons)
                foreach (var d in debuffs)
                {
                    // ⭐ **両方1段下げる。**⚠️ 下げないとこの形だけで★5が埋まる（実測 98/142）
                    var weakD = Weaker(d.Effect);
                    var weakB = Weaker(b.Effect);
                    if (weakD == null || weakB == null) continue;
                    { Use(b.Id, d.Id); Keep($"b-all-{b.Id}-{d.Id}", b.Word + d.Word + "・全", SkillType.Debuff,
                        Target.EnemyAll, "全＋全", weakD, weakB.To(Target.AllyAll)); }
                }

            Report(made, args);
        }

        /// <summary>⭐ **全体に効くものは1段下げる。**（既存の約束・2026-08-27 に全効果へ広げた）
        ///
        /// ⚠️ 前はダメージだけ下げていた。そのせいで「全体強化＋全体弱化」が
        /// 只で最上位になり、★5 の 142通り中 **98通り**をこの形1つが占めていた。
        /// ⭐ 状態ものも下げると、全＋全 は自然に★3〜★4へ落ちる。
        ///
        /// ⚠️ 下げきれないもの（既に最小）は **null** を返す ＝ その全体版は作らない。</summary>
        private static Effect? Weaker(Effect e)
        {
            switch (e.Kind)
            {
                case EffectKind.Damage:
                    if (e.Power == PowerTier.Small) return e.Repeat > 1 ? e : null;
                    var down = e.Power == PowerTier.Huge ? PowerTier.Large
                        : e.Power == PowerTier.Large ? PowerTier.Medium : PowerTier.Small;
                    return Effect.Damage(down, e.Scale, e.Repeat, e.Pierce);
                // ⭐ 持続もの ── 1ターン短く。⚠️ 単体の下限3Tは全体には掛からない
                case EffectKind.Buff:
                    return e.Turns <= 2 ? null : Effect.Buff(e.Stat, e.Sign, e.Turns - 1, e.Chance);
                case EffectKind.Poison:
                    return e.Turns <= 2 ? null : Effect.Poison(e.Stacks, e.Turns - 1, e.Chance);
                case EffectKind.Regen:
                    return e.Turns <= 2 ? null : Effect.Regen(e.Stacks, e.Turns - 1, e.Chance);
                case EffectKind.Stun: return null;              // ⚠️ 1Tが最小。全体スタンは作らない
                case EffectKind.Sleep:
                    return e.Turns <= 1 ? null : Effect.Sleep(e.Turns - 1, e.Chance);
                case EffectKind.Block:
                    return e.Turns <= 1 ? null : Effect.Block(e.Turns - 1, e.Chance);
                case EffectKind.Immune:
                    return e.Turns <= 1 ? null : Effect.Immune(e.Turns - 1, e.Chance);
                case EffectKind.Guts:
                    return e.Turns <= 1 ? null : Effect.Guts(e.Turns - 1, e.Chance);
                case EffectKind.Shield:
                    return e.Count <= 1 ? null : Effect.Shield(e.Count - 1, e.Chance);
                case EffectKind.Taunt:
                    return e.Hits <= 1 ? null : Effect.Taunt(e.Hits - 1, e.Chance);
                case EffectKind.Ct:
                    return Math.Abs(e.Delta) <= 1 ? null : Effect.Ct(e.Delta - Math.Sign(e.Delta), e.Chance);
                // ⭐ 割合もの ── 6割にする
                case EffectKind.HealRatio:
                    return Step(e.Percent) == 0 ? null : Effect.HealRatio(Step(e.Percent), e.Chance);
                case EffectKind.Gauge:
                    return Step(e.Percent) == 0 ? null : Effect.Gauge(Step(e.Percent), e.Chance);
                case EffectKind.Revive:
                    return Step(e.Percent) == 0 ? null : Effect.Revive(Step(e.Percent), e.Chance);
                // ⚠️ 個数もの ── 1個が最小なので全体版を作らない
                case EffectKind.Dispel:
                case EffectKind.Steal:
                    return Math.Abs(e.Count) <= 1 ? null
                        : (e.Count < 0 ? Effect.Cleanse(Math.Abs(e.Count) - 1, e.Chance)
                                       : Effect.Dispel(e.Count - 1, e.Chance));
                default: return null;
            }
        }

        /// <summary>割合を1段下げる（6割）。⚠️ 5%未満になったら作らない。</summary>
        private static int Step(int percent)
        {
            int moved = percent * 6 / 10;
            return Math.Abs(moved) < 5 ? 0 : moved;
        }

        /// <summary>仮の説明文。⚠️ **選んだあとに手で書き直す前提**（機械では狙いが書けない）。</summary>
        private static string GistOf(string shape) => shape switch
        {
            "攻＋弱" => "殴りながら崩す",
            "弱＋弱" => "まとめて崩す",
            "強＋強" => "まとめて支える",
            "強＋弱" => "味方を上げ、敵を下げる",
            "全＋全" => "盤面を両方動かす",
            "自→攻" => "自分に掛けてから撃つ",
            "攻→自" => "殴って自分が得をする",
            "代償" => "代償を負って深く入る",
            "全体" => "全体に効く",
            _ => "",
        };

        /// <summary>⭐ **格ごとに何本ずつ選ぶか。**⚠️ ★1 の卵が一番よく出るので下も厚く取る。
        /// ⚠️ プールの必要数は 55本以上（11種族 × 2枠 × 5本 ÷ 相乗り上限2）。</summary>
        private static readonly int[] Want = { 0, 20, 25, 25, 20, 10 };

        /// <summary>⭐ **部品が偏らないように選ぶ。**⚠️ 手ぶんの高い順に採ると、
        /// 強い部品（乱撃・奪取）ばかりの一覧になる ── 種類を増やすのが目的なので、
        /// **まだ使っていない部品を持つ候補**を優先する。⚠️ 乱数は使わない（毎回同じ結果）。</summary>
        private static List<T> Pick<T>(List<T> pool, int want,
            Func<T, string[]> partsOf, Func<T, string> shapeOf, Func<T, string> idOf)
        {
            var taken = new List<T>();
            var usedPart = new Dictionary<string, int>(StringComparer.Ordinal);
            var usedShape = new Dictionary<string, int>(StringComparer.Ordinal);
            var left = new List<T>(pool);
            while (taken.Count < want && left.Count > 0)
            {
                T best = left[0];
                int bestScore = int.MaxValue;
                foreach (var cand in left)
                {
                    int score = 0;
                    foreach (var part in partsOf(cand))
                        score += usedPart.TryGetValue(part, out int n) ? n * 10 : 0;
                    score += usedShape.TryGetValue(shapeOf(cand), out int m) ? m : 0;
                    // ⚠️ 同点は id 順で決める（乱数を使わない＝毎回同じ一覧が出る）
                    if (score < bestScore
                        || (score == bestScore
                            && string.CompareOrdinal(idOf(cand), idOf(best)) < 0))
                    {
                        best = cand; bestScore = score;
                    }
                }
                taken.Add(best);
                left.Remove(best);
                foreach (var part in partsOf(best))
                    usedPart[part] = usedPart.TryGetValue(part, out int n) ? n + 1 : 1;
                usedShape[shapeOf(best)] = usedShape.TryGetValue(shapeOf(best), out int had) ? had + 1 : 1;
            }
            return taken;
        }

        private static SkillType TypeOfBoon(Part b) =>
            b.Effect.Kind == EffectKind.HealRatio || b.Effect.Kind == EffectKind.Revive
            || b.Effect.Kind == EffectKind.Regen || b.Effect.Kind == EffectKind.Dispel
                ? SkillType.Heal : SkillType.Support;

        private static void Report(List<(Skill Skill, string Shape, string[] Parts)> made, string[] args)
        {
            Console.WriteLine();
            Console.WriteLine($"■ 組み合わせで作れる技: **{made.Count} 通り**");
            Console.WriteLine("  ⚠️ 決まり: 単体のバフ・デバフは3T以上 / 効果は1〜3つ / ダメージ部品は1つまで");
            Console.WriteLine("  ⚠️ 確率・CT・効き目の釣り合いは見ていない（まず種類を数える）");
            Console.WriteLine();

            bool listAll = args.Length > 0 && args[0] == "all";
            bool pick = args.Length > 0 && args[0] == "pick";
            var rows = made.Select(m => (m.Shape, m.Skill, m.Parts,
                Value: SkillValues.Of(m.Skill, out _), Grade: SkillValues.GradeOf(m.Skill))).ToList();

            Console.WriteLine("  組の形ごと");
            Console.WriteLine($"  {"形",-8}{"通り",6}{"外",5}{"★1",5}{"★2",5}{"★3",5}{"★4",5}{"★5",5}");
            foreach (var g in rows.GroupBy(r => r.Shape).OrderByDescending(g => g.Count()))
            {
                var c = new int[6];
                foreach (var r in g) c[r.Grade]++;
                Console.WriteLine($"  {g.Key,-8}{g.Count(),6}{c[0],5}{c[1],5}{c[2],5}{c[3],5}{c[4],5}{c[5],5}");
            }

            Console.WriteLine();
            Console.WriteLine("  格ごとの総数（⭐ ★4・★5 が何通り作れるかがこの実験の答え）");
            for (int grade = 0; grade <= 5; grade++)
            {
                var ms = rows.Where(r => r.Grade == grade).ToList();
                string label = grade == 0 ? "外（1.0未満）" : "★" + grade;
                Console.WriteLine($"  {label,-12}{ms.Count,5} 通り"
                    + (ms.Count > 0 ? "   例: " + string.Join("・",
                        ms.OrderByDescending(m => m.Value).Take(5).Select(m => m.Skill.Name)) : ""));
            }

            if (pick)
            {
                Console.WriteLine();
                Console.WriteLine("■ 選抜（部品が偏らないように取る・乱数なし）");
                var chosen = new List<(string Shape, Skill Skill, string[] Parts, double Value, int Grade)>();
                for (int grade = 1; grade <= 5; grade++)
                {
                    var band = rows.Where(r => r.Grade == grade).OrderBy(r => r.Skill.Id).ToList();
                    var got = Pick(band, Want[grade], r => r.Parts, r => r.Shape, r => r.Skill.Id);
                    Console.WriteLine($"  ★{grade}: {got.Count} 本 / 候補 {band.Count} 通り");
                    chosen.AddRange(got);
                }
                Console.WriteLine();
                Console.WriteLine($"  ⭐ 合計 {chosen.Count} 本");
                Console.WriteLine();
                Console.WriteLine($"  {"格",-4}{"手ぶん",7}  {"形",-8}{"型",-8}名前");
                foreach (var r in chosen.OrderByDescending(r => r.Value))
                    Console.WriteLine($"  ★{r.Grade,-3}{r.Value,7:0.00}  {r.Shape,-8}"
                        + $"{Skills.LabelOf(r.Skill.Type),-8}{r.Skill.Name}");
                Console.WriteLine();
                Console.WriteLine("  ⚠️ 名前と説明は仮（機械で連結しただけ）── 採用時に手で書き直すこと");
                return;
            }
            if (!listAll)
            {
                Console.WriteLine();
                Console.WriteLine("  ⭐ 全件は `sim brew all` / 選抜は `sim brew pick`");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"  {"格",-4}{"手ぶん",7}  {"形",-8}名前");
            foreach (var r in rows.OrderByDescending(r => r.Value))
            {
                Console.WriteLine($"  {(r.Grade == 0 ? "外" : "★" + r.Grade),-4}{r.Value,7:0.00}  "
                    + $"{r.Shape,-8}{r.Skill.Name}");
            }
        }
    }
}
