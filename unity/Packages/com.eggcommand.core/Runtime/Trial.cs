#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>試練の相手1体ぶんの仕様。⭐ **手で書く。引かない。**
    ///
    /// ⚠️ 巣の顔ぶれ（<see cref="Nests.MakeDefenders"/>）は挑むたびに引き直すが、
    /// 試練は**毎回まったく同じ**にする。⭐ そうしないと「何が足りなかったか考えて、
    /// 組み直して、挑み直す」という輪が回らない ── 相手が変わるなら対策の立てようがない。</summary>
    public sealed class TrialFoe
    {
        public readonly string SpeciesId;
        /// <summary>枠2・枠3。⚠️ 枠1 は種族が持つ。⭐ **袋の外の技も渡してよい**
        /// （試練は手で組んだ相手なので、卵ガチャの取り決めに縛られない）。</summary>
        public readonly string Skill2;
        public readonly string Skill3;
        public readonly Element Element;
        /// <summary>野生レベル。⚠️ <see cref="Generation"/> ぶんの上限に収まること
        /// （<see cref="Trials.Faults"/> が数える）。</summary>
        public readonly StatBlock Wild;
        /// <summary>何代目か。⭐ **ここが上限を押し上げる**
        /// （1代進むごとに1ステ上限 +1・合計 +3／2026-08-21 に変異から渡した）。
        /// ⚠️ 1 は野生と同じ ＝ 押し上げ無し。</summary>
        public readonly int Generation;
        /// <summary>偏り4本。⚠️ 4つとも別のステにすること。</summary>
        public readonly StatKey Best, Strong, Weak, Worst;

        public TrialFoe(string speciesId, string skill2, string skill3, Element element,
            StatBlock wild, int generation,
            StatKey best, StatKey strong, StatKey weak, StatKey worst)
        {
            SpeciesId = speciesId;
            Skill2 = skill2;
            Skill3 = skill3;
            Element = element;
            Wild = wild;
            Generation = generation;
            Best = best;
            Strong = strong;
            Weak = weak;
            Worst = worst;
        }
    }

    /// <summary>試練の1段。</summary>
    public sealed class Trial
    {
        public readonly string Id;
        public readonly string Name;
        /// <summary>⭐ **何が来るかを1行で言う。**⚠️ 数は書かない
        /// （数を出すと「勝てる相手だけ選ぶ」に戻る）。書くのは**筋**だけ。</summary>
        public readonly string Gist;
        public readonly IReadOnlyList<TrialFoe> Foes;

        public Trial(string id, string name, string gist, params TrialFoe[] foes)
        {
            Id = id;
            Name = name;
            Gist = gist;
            Foes = foes;
        }
    }

    /// <summary>試練 ── ⭐ **手で組んだ敵編成と戦う場所**（2026-08-21・作者の指示）。
    ///
    /// ⭐ これは 2026-08-21 に決めた方針の実装:
    /// 「ヌシを倒すことを目標とするのではなく、いくつもの敵構成を用意して戦える場所を設ける」。
    ///
    /// ⚠️ **巣と違って卵は出ない。**出すと「試練で卵を稼ぐ」が最短経路になり、
    /// 潜入も配合も回らなくなる。⭐ 試練が返すのは**勝った記録**だけ
    /// （出撃していた個体の育成 +1 は、他の戦闘と同じように付く）。
    ///
    /// ⭐ **どの段も「1つの噛み合わせ」でできている。**
    /// | 段 | 噛み合わせ |
    /// |---|---|
    /// | 1 | 弱化を**置いて**、置いた数で殴る（追い打ち・追い崩し・総崩し）|
    /// | 2 | 手番を**奪って**、奪ったことを手数に変える（不意打ち・畳み掛け・寝込み討ち）|
    /// | 3 | **倒れない**（意地・執念・蘇生・ブロック）|
    /// | 4 | **倒すほど速くなる**（遺志・置き土産・挑発）|
    /// | 5 | 上の4つ全部 |
    ///
    /// ⚠️ **属性は段の中で散らしてある。**1色に寄せると、有利属性を4体揃えるだけで
    /// 崩れてしまう（巣の守り手を単一属性にしていた頃と同じ穴）。</summary>
    public static class Trials
    {
        /// <summary>1段の体数。⭐ プレイヤーと同数（<see cref="Games.PartySize"/>）。
        /// ⚠️ 揃えないと <see cref="Battle.LoneScale"/> が働いて、
        /// 「手で組んだ通りの強さ」ではなくなる。</summary>
        public static int Size => Games.PartySize;

        /// <summary>試練の相手は育て切っている。⚠️ 生の数を書かない。</summary>
        public static int Trained => Creatures.TrainMax;

        // ⚠️ 野生レベルの上限は**世代**で決まる（1ステ 40+(代-1) / 合計 その3倍）。
        //    ⭐ 下の表は**どれも上限ちょうど**にしてある ── 段が上がると世代が深くなり、
        //    それがそのまま「素質の天井が上がる」になる。
        //    ⚠️ 2026-08-21 まで変異の回数で書いていた（役が世代へ移った）。
        private static readonly Trial[] List =
        {
            // ══ 段1 ══════════════════════════════════════
            // ⭐ **置いてから殴る。**弱化を撒く2体と、撒いた数で伸びる2体。
            // ⚠️ ここで「弱化を先に落とす」か「撒く役を先に倒す」かの判断が生まれる。
            new Trial("bane", "毒の園", "弱化を撒き、撒いた数だけ深く入ってくる",
                // トゲル＝畳み掛け（弱化を通すともう一度動ける）。命中を最大に振ってある
                new TrialFoe("togeru", "curse", "venom-heavy", Element.Wood,
                    new StatBlock(12, 52, 12, 28, 52, 0), 13,
                    StatKey.Acc, StatKey.Atk, StatKey.Def, StatKey.Res),
                // ツノガ＝追い打ち（弱化持ちへの与ダメ +）。追い崩しで種類ぶん伸びる
                new TrialFoe("tsunoga", "chase-down", "attack-heavy", Element.Fire,
                    new StatBlock(24, 52, 12, 28, 40, 0), 13,
                    StatKey.Atk, StatKey.Acc, StatKey.Def, StatKey.Res),
                // ハネル＝狙い澄まし（通る率 +）。全体の弱化を担当する
                new TrialFoe("haneru", "slow-all", "poison-all", Element.Water,
                    new StatBlock(12, 12, 12, 52, 52, 16), 13,
                    StatKey.Acc, StatKey.Spd, StatKey.Atk, StatKey.Def),
                // マルミ＝先駆け（開幕の1手は外れない）。⭐ 挑発で殴る先を縛り、鬨の声で押し上げる。
                // ⚠️ **受ける役が要る。**初版は撒くだけの4体で、天井の編成に素通りされた
                //    （実測 100%）── 撒いている間に落とされては、撒いた意味が出ない
                new TrialFoe("marumi", "taunt-long", "warcry", Element.Wood,
                    new StatBlock(52, 0, 52, 0, 52, 0), 13,
                    StatKey.Def, StatKey.Hp, StatKey.Atk, StatKey.Spd)),

            // ══ 段2 ══════════════════════════════════════
            // ⭐ **止めてから殴る。**止まった相手に深く入る技と、止めるたびに速くなる特性。
            // ⚠️ 免疫か弱化耐性を用意しないと、手番がほとんど回ってこない。
            new Trial("halt", "動けぬ盤", "手番を奪い、止まった相手を狙ってくる",
                // ⚠️ **止めるだけの編成は弱い。**初版は制圧だけで殴り手が無く、
                //    参照編成に **100%** 負けた（段1 の 19% より軽い＝順番が逆転していた）。
                //    ⭐ 「止める」に**止まった相手を刈る技**（寝込み討ち）を2枚差して直した。
                // キバネ＝不意打ち（相手が飛ばすたびゲージ +）。寝込み討ちが回収先
                new TrialFoe("kibane", "stun-heavy", "ambush-strike", Element.Fire,
                    new StatBlock(28, 54, 0, 40, 40, 0), 15,
                    StatKey.Atk, StatKey.Acc, StatKey.Def, StatKey.Res),
                // トゲル＝畳み掛け。眠らせて、寝込みを刈る。⚠️ 睡眠は殴ると起きるので順番が要る
                new TrialFoe("togeru", "sleep", "ambush-strike", Element.Wood,
                    new StatBlock(28, 54, 0, 40, 40, 0), 15,
                    StatKey.Atk, StatKey.Acc, StatKey.Def, StatKey.Res),
                // ハネル＝狙い澄まし。スタンと CT延長で「動ける番」を削る
                new TrialFoe("haneru", "stun", "ct-lock", Element.Water,
                    new StatBlock(28, 26, 14, 54, 40, 0), 15,
                    StatKey.Spd, StatKey.Acc, StatKey.Atk, StatKey.Hp),
                // マルミ＝先駆け。⭐ ブロックで回復と強化を止め、挑発で殴る先を縛る
                new TrialFoe("marumi", "block", "taunt-long", Element.Water,
                    new StatBlock(54, 0, 54, 0, 54, 0), 15,
                    StatKey.Def, StatKey.Hp, StatKey.Atk, StatKey.Spd)),

            // ══ 段3 ══════════════════════════════════════
            // ⭐ **倒れない。**盾・免疫・蘇生と、それを守るブロック。
            // ⚠️ 火力の総量ではなく「1手でどれだけ削れるか」が問われる
            //    （細く長く削ると、削ったぶんが戻る）。
            new Trial("wall", "崩れぬ壁", "固めて、癒して、こちらの手を止めてくる",
                // ヒラベ＝意地（弱化を受ける率 −）。盾と免疫を配る本体
                new TrialFoe("hirabe", "shield-wall", "immune-long", Element.Water,
                    new StatBlock(56, 0, 56, 0, 0, 56), 17,
                    StatKey.Def, StatKey.Res, StatKey.Atk, StatKey.Spd),
                // タマル＝執念（盾が剥がれるたびゲージ +）。⭐ 盾を剥がすほど手数が増える
                new TrialFoe("tamaru", "harden", "taunt-long", Element.Wood,
                    new StatBlock(56, 24, 56, 0, 0, 32), 17,
                    StatKey.Def, StatKey.Hp, StatKey.Acc, StatKey.Spd),
                // ホムラ＝置き土産（倒れたら味方のゲージが進む）。蘇生とリジェネの担当
                new TrialFoe("homura", "revive-heavy", "regen-heavy", Element.Fire,
                    new StatBlock(44, 24, 20, 44, 0, 36), 17,
                    StatKey.Spd, StatKey.Hp, StatKey.Acc, StatKey.Def),
                // マルミ＝先駆け。ブロックでこちらの回復を止め、弱化解除で味方を洗う
                new TrialFoe("marumi", "block", "cleanse-all", Element.Water,
                    new StatBlock(44, 0, 24, 44, 56, 0), 17,
                    StatKey.Acc, StatKey.Spd, StatKey.Atk, StatKey.Res)),

            // ══ 段4 ══════════════════════════════════════
            // ⭐ **倒すほど速くなる。**遺志（味方が倒れると待ちが全部消える）を軸に、
            //    わざと倒れる役（挑発・置き土産）を添えてある。
            // ⚠️ 削る順を間違えると、こちらが有利になった瞬間に一番重い技が飛ぶ。
            new Trial("wake", "倒すほど昂ぶる", "1体倒すごとに、残りが速くなる",
                // イワオ＝遺志（味方が倒れると自分の待ちが全部 0）。重い技を2枚持つ
                new TrialFoe("iwao", "pierce-strike-heavy", "attack-all-twice", Element.Fire,
                    new StatBlock(58, 58, 58, 0, 0, 0), 19,
                    StatKey.Atk, StatKey.Def, StatKey.Acc, StatKey.Res),
                // ホムラ＝置き土産（倒れたら味方のゲージが進む）。⭐ 倒されること自体が仕事
                new TrialFoe("homura", "tailwind", "gauge-boost-heavy", Element.Wood,
                    new StatBlock(44, 44, 20, 58, 0, 8), 19,
                    StatKey.Spd, StatKey.Atk, StatKey.Def, StatKey.Res),
                // タマル＝執念。挑発で殴られに行き、剥がれた盾がゲージになる
                new TrialFoe("tamaru", "taunt-long", "guts-deep", Element.Water,
                    new StatBlock(58, 24, 58, 0, 0, 34), 19,
                    StatKey.Def, StatKey.Hp, StatKey.Acc, StatKey.Spd),
                // ヒラベ＝意地。立て直しで弱化を洗い、免疫で次を防ぐ
                // ⚠️ **全快（HP割合回復・特大）は外した。**確率を廃したぶん必ず戻るようになり、
                //    挑発で守られた壁が**永久に落ちない**盤になっていた（実測で段5より重かった）。
                new TrialFoe("hirabe", "rally", "immune-long", Element.Wood,
                    new StatBlock(58, 0, 44, 14, 0, 58), 19,
                    StatKey.Res, StatKey.Hp, StatKey.Atk, StatKey.Acc)),

            // ══ 段5 ══════════════════════════════════════
            // ⭐ **上の4つ全部。**弱化を置き、止め、倒れず、倒れたら速くなる。
            // ⚠️ 世代は天井（<see cref="Stats.GenerationCapSteps"/>）まで積んである。
            //    ⭐ **これ以上は素質では強くできない** ── 上を作るなら噛み合わせで作ること。
            new Trial("depth", "試練の主", "置き、止め、耐え、倒れてなお速くなる",
                // ヌシ＝背水（HP半分以下で待ちが速く減る）。⭐ 削るほど手数が増える
                // ⚠️ **速さを 0 にしてはいけない。**初版は4体とも速度に振らず、
                //    素質だけ天井まで積んだ編成に**100%**押し切られた（段4 より軽かった）。
                new TrialFoe("nushi", "attack-all-heavy", "finisher", Element.Fire,
                    new StatBlock(60, 60, 30, 30, 0, 0), 21,
                    StatKey.Atk, StatKey.Hp, StatKey.Acc, StatKey.Res),
                // トゲル＝畳み掛け。⭐ ブロックで**回復と強化を止め**、命削りで硬さを無視して割る。
                // ⚠️ どちらも弱化なので、通すたびに畳み掛けでもう一度動く
                new TrialFoe("togeru", "block", "life-cut", Element.Water,
                    new StatBlock(0, 60, 0, 60, 60, 0), 21,
                    StatKey.Acc, StatKey.Spd, StatKey.Def, StatKey.Res),
                // ヒラベ＝意地。⭐ 挑発で**殴る先を自分に縛り**、蘇生で倒した端から戻す。
                // ⚠️ 防御が最も厚い体へ攻撃を集めさせるので、防御を抜けない編成は手が止まる
                new TrialFoe("hirabe", "revive-heavy", "taunt-long", Element.Wood,
                    new StatBlock(60, 0, 60, 0, 0, 60), 21,
                    StatKey.Res, StatKey.Def, StatKey.Atk, StatKey.Acc),
                // イワオ＝遺志。誰かが倒れた瞬間、重い技が2枚とも撃てる
                new TrialFoe("iwao", "pierce-strike-heavy", "sweep-down", Element.Fire,
                    new StatBlock(30, 60, 30, 60, 0, 0), 21,
                    StatKey.Atk, StatKey.Spd, StatKey.Def, StatKey.Res)),
        };

        public static IReadOnlyList<Trial> All => List;

        public static bool Has(string id)
        {
            foreach (var trial in List) if (trial.Id == id) return true;
            return false;
        }

        public static Trial ById(string id)
        {
            foreach (var trial in List) if (trial.Id == id) return trial;
            throw new ArgumentException($"試練の表に {id} が無い");
        }

        /// <summary>何段目か（1 始まり）。⚠️ 表に無ければ 0。</summary>
        public static int StepOf(string id)
        {
            for (int i = 0; i < List.Length; i++) if (List[i].Id == id) return i + 1;
            return 0;
        }

        /// <summary>その段の顔ぶれを作る。⭐ **毎回まったく同じ**（乱数を1度も引かない）。
        ///
        /// ⚠️ id は段ごとに固定。⭐ 保管庫へは入らないので、他の個体と衝突しない。</summary>
        public static List<Creature> PartyOf(Trial trial)
        {
            var party = new List<Creature>(trial.Foes.Count);
            for (int i = 0; i < trial.Foes.Count; i++)
            {
                var foe = trial.Foes[i];
                var creature = new Creature(
                    $"trial-{trial.Id}-{i}", foe.SpeciesId,
                    Stats.ApplyTotalCap(foe.Wild, foe.Generation), new StatBlock(0, 0, 0, 0), 0,
                    0, foe.Skill2, foe.Skill3, 0, null, null, foe.Generation,
                    foe.Strong, foe.Weak, foe.Element,
                    Creatures.TraitIdFor(foe.SpeciesId), foe.Best, foe.Worst);
                // ⭐ 育て切った状態で来る。⚠️ ここを飛ばすと、素質だけの案山子になる
                Creatures.Grow(creature, Trained);
                party.Add(creature);
            }
            return party;
        }

        /// <summary>表の不備。⭐ 起動時の <see cref="Audit"/> が使う。</summary>
        public static List<string> Faults()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            foreach (var trial in List)
            {
                if (!seen.Add(trial.Id)) problems.Add($"試練 id が重複している: {trial.Id}");
                if (trial.Name.Length == 0) problems.Add($"{trial.Id}: 名前が空");
                if (trial.Gist.Length == 0) problems.Add($"{trial.Id}: 一言が空");
                // ⚠️ **体数を揃える。**揃わないと LoneScale が働いて、
                //    書いたとおりの強さで来なくなる（手で組んだ意味が薄れる）
                if (trial.Foes.Count != Size)
                    problems.Add($"{trial.Id}: {trial.Foes.Count} 体（{Size} 体に揃える）");

                for (int i = 0; i < trial.Foes.Count; i++)
                {
                    var foe = trial.Foes[i];
                    string at = $"{trial.Id}[{i}]";
                    if (!SpeciesTable.Has(foe.SpeciesId))
                    { problems.Add($"{at}: 種族 {foe.SpeciesId} が表に無い"); continue; }
                    var species = SpeciesTable.ById(foe.SpeciesId);

                    if (!Skills.Has(foe.Skill2)) problems.Add($"{at}: 技 {foe.Skill2} が表に無い");
                    if (!Skills.Has(foe.Skill3)) problems.Add($"{at}: 技 {foe.Skill3} が表に無い");
                    // ⚠️ **同じ技を2枠に置かない。**置くと片方が丸ごと死に枠になる
                    if (foe.Skill2 == foe.Skill3) problems.Add($"{at}: 枠2と枠3が同じ（{foe.Skill2}）");
                    if (foe.Skill2 == species.Skill1 || foe.Skill3 == species.Skill1)
                        problems.Add($"{at}: 枠1（{species.Skill1}）と同じ技を持っている");

                    // ⚠️ **偏りは4つとも別のステ。**重なると Slanted が両方とも捨てる
                    var keys = new HashSet<StatKey> { foe.Best, foe.Strong, foe.Weak, foe.Worst };
                    if (keys.Count != 4) problems.Add($"{at}: 偏り4本が重なっている");

                    // ⚠️ **上限を超えた素質を書かない。**⭐ 黙って削られると、
                    //    表に書いた数と実際に戦う相手が食い違う（一番気づけない食い違い）
                    int statMax = Stats.WildStatMaxFor(foe.Generation);
                    int totalMax = Stats.WildTotalMaxFor(foe.Generation);
                    foreach (var key in Stats.Keys)
                    {
                        if (foe.Wild[key] > statMax)
                            problems.Add($"{at}: {Stats.LabelOf(key)} が {foe.Wild[key]}（上限 {statMax}）");
                    }
                    int total = Stats.TotalOf(foe.Wild);
                    if (total > totalMax)
                        problems.Add($"{at}: 素質の合計が {total}（上限 {totalMax}）");
                    if (foe.Generation < 1)
                        problems.Add($"{at}: 世代 {foe.Generation}（1 以上）");
                }
            }
            return problems;
        }

        public static void Audit()
        {
            var problems = Faults();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "試練の表の不備:" + Environment.NewLine + "  " +
                    string.Join(Environment.NewLine + "  ", problems));
            }
        }
    }
}
