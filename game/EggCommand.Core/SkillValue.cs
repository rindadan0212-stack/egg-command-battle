#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>⭐ **技1つが1手で生む値打ち（「手ぶん」）。**枠1 の一撃 ＝ 1.0。
    ///
    /// ⭐ **ここが技の格の出所**（<see cref="GradeOf"/>）。⚠️ 2026-08-27 に
    /// `EggCommand.Sim` から移してきた ── ★で技を引くには**ゲーム本体が格を知る**必要があり、
    /// Core から Sim は見えないため。⭐ Sim に残したのは**並べて印字する側**だけ。
    ///
    /// ⚠️ **構造（効果の数）で格を決めない。**「毒を10重ねる」のような単品が
    /// 効果1つのまま最上位の働きをする（実測 3.62手ぶん）── 構造は代理指標にすぎない
    /// （作者の指摘 2026-08-27）。⭐ 測るのは**期待値**。</summary>
    public static class SkillValues
    {
        // ══ 1手あたりの価値（算数）═══════════════════════════
        //
        // ⭐ **AI もサイコロも通さない。**（作者の判断 2026-08-19）
        // ⚠️ AI を通す測り方（sim skillvalue / traits / species）は、AI の腕を測ってしまう。
        //    実際 2026-08-19 に AI の採点定数が古く、**23技を一度も選ばなかった**ことが判明した。
        //    あのとき その23技は「弱い」と測れていた ── 順位付けには使えない。
        // ⭐ ここは式だけで出すので、AI が賢くなっても愚かでも同じ数が出る。
        //
        // 🔴 **実測の分母は「掛かった回数」**（撃った回数ではない）。⚠️ 式は最後に確率を掛けるので、
        //    撃った回数を分母にすると**確率が二重に掛かる**（2026-08-27 に踏んだ）。
        //
        // ⚠️ **勘で置いた見積もりには「見積」と印を付ける。**
        //    文脈で価値が変わるもの（挑発・ガッツ・蘇生）は算数にならない。

        /// <summary>⭐ **挑発が、与えた回数1つにつき実際に狙いをずらした回数**
        /// （`sim guess` 実測 2026-08-27・300戦）。
        /// ⚠️ 挑発(3回) が 0.19回、挑発・長(5回) が 0.31回 ── 1回あたり 0.063 と 0.062 で**一致する**
        /// （回数の違う2札が同じ値を出す＝式の形が正しい）。⚠️ 与えた回数の **6%** しか働かない
        /// ── 相手がもともと掛け手を狙っていれば、挑発は何も起こしていない。
        /// ⭐ 1回ずらしたときの値打ちは 1.0 手ぶん（**上限**。実際は差分なのでもっと小さい）。</summary>
        public const double TauntPullPerHit = 0.06;

        /// <summary>⭐ **ガッツが実際に致命傷を耐えた回数**（`sim guess` 実測・0.68回/回）。
        /// ⭐ 耐えた1回は一撃ぶん ＝ 1.0 手ぶんなので、そのまま手ぶん。</summary>
        public const double GutsSavesPerCast = 0.68;

        /// <summary>⭐ **免疫が実際に弱化を弾いた回数**（`sim guess` 実測・0.09回/回）。
        ///
        /// 🔴 **持続に比例させてはいけない。**⚠️ 実測では 3ターンの「免疫」が 0.09、
        /// 6ターンの「免疫・長」が **0.08** ── 倍にしても増えていない。
        /// ⭐ 効き目を縛っているのは持続ではなく「4体のうちその1体が狙われるか」。
        /// ⚠️ つまり**免疫・長は免疫の上位版になっていない**（持続だけ伸ばしても無意味）。</summary>
        public const double ImmuneBlocksPerCast = 0.09;

        /// <summary>⭐ **ブロックが実際に回復・強化を弾いた回数**（`sim guess` 実測・0.39回/回）。
        /// ⚠️ 免疫と同じく持続に比例しない。</summary>
        public const double BlockBluntsPerCast = 0.39;

        /// <summary>封印1回ぶんの見積り。⭐ 枠2・3 が押せないので、その手番は枠1 に落ちる。
        /// ⚠️ 枠2・3 が選ばれるのは全手番の 31%（`sim pace`）なので、そのぶんが消える。🚧 未測定。</summary>
        public const double GuessSealPerTurn = 0.31;

        /// <summary>無敵1回ぶん。⭐ 一撃（1.0）に加えて**毒も止める**（2026-08-27）。
        /// ⚠️ 毒が乗っているとは限らないので、上乗せは控えめ。🚧 未測定。</summary>
        public const double InvinciblePerTurn = 1.2;

        /// <summary>固着1回ぶんの見積り。⚠️ 相手が解除を持っていなければ 0。🚧 未測定。</summary>
        public const double GuessAnchorPerTurn = 0.2;

        /// <summary>弱化延長1回ぶんの見積り。⚠️ 乗っていなければ 0。🚧 未測定。</summary>
        public const double GuessExtendPerTurn = 0.3;

        /// <summary>CT を1縮める／延ばす見積り。
        /// ⭐ 枠2・3 が選ばれるのは全手番の 31%（`sim pace` 実測）なので、そのぶんだけ効く。</summary>
        public const double GuessCtPerStep = 0.31;

        /// <summary>⭐ **蘇生で戻った個体が、その後実際に動いた回数**（`sim guess` 実測）。
        /// ⚠️ 見積りは 3.0 だったが、実測は **0.20回**（HP40%）／**0.47回**（HP70%）。
        /// ⭐ 蘇生の値打ちのほとんどは「動くこと」ではなく**戻した HP を相手に削り直させること**
        /// なので、そちらは算数（回復と同じ式）で数える ── ここは上乗せぶんだけ。
        ///
        /// ⚠️ 🔴 **上の 0.20／0.47 は計測バグ下の値だった**（2026-08-27 発見）。
        /// `GuessProbe` の分岐が `else if` の連鎖で、**蘇生された個体がもう一度蘇生を
        /// 撃った Act**（両陣営とも枠2・3 に同じ蘇生を配る作りなので、戻った個体自身も
        /// 蘇生を持っている）が最初の分岐（`cast++`）に吸われ、「戻ってから動いた」の
        /// 集計に永久に届いていなかった ── 実際より低く出ていた。
        /// ⭐ 分岐を独立した `if` に直して**再実測（2026-08-27・300戦）**:
        /// 蘇生 **0.26回**（HP40%）／蘇生・大 **0.58回**（HP70%）── 平均 0.42。
        /// ⚠️ 2枚で1定数なので、平均よりやや低めに保守的に置く（0.3 を旧平均 0.335 より
        /// 低く置いていたのと同じ流儀）。</summary>
        public const double ReviveActs = 0.4;

        /// <summary>強化1つを消したときの見積もり。⭐ 相手が撒くのに使った1手ぶん。</summary>
        public const double GuessBuffWorth = 0.9;

        /// <summary>盤面を数える技が、実際に数えられる個数の見積り。
        /// ⚠️ 天井は <see cref="Skills.PerCap"/>（4）だが、満載は仕込みを積んだ後だけ。
        /// ⭐ 半分に置く ── 「素で撃つ」と「仕込んでから撃つ」の間を取る。🚧 未測定。</summary>
        private const int GuessTallyStacks = 2;

        /// <summary>⭐ **全体技が実際に効く「体数」**（作者の指示 2026-08-27）。
        ///
        /// ⚠️ 以前は <c>Games.PartySize</c>（4体）をそのまま掛けていた。⭐ そのせいで
        /// 全体技が手ぶんの上位を独占し（全体連撃 6.40・全体強攻撃 5.33）、
        /// **「全体である」ことだけが上位の格の証**になっていた。
        /// ⚠️ コマンドバトルで全体攻撃は**普通の札**であって、格の証ではない。
        ///
        /// ⭐ 2.0 の根拠: 相手は1体ずつ落ちていくので、生きている数は 4→3→2→1 と減る。
        /// ⚠️ さらに**溢れたぶんは捨てられる**（瀕死の相手に全体で撃っても、余った威力は消える）。
        /// 一列に落としていく間の平均は 2.5 だが、溢れぶんを見て 2.0 に置いてある。
        /// 🚧 **未測定**（`sim delivered` に全体の実入りを数える口はまだ無い）。</summary>
        public const double AreaTargets = 2.0;

        /// <summary>⭐ **1体が1戦闘で動ける回数**（`sim pace` 実測）。
        ///
        /// ⭐ **切れない強化（<see cref="Skills.Lasting"/>）の持続はこれで数える。**
        /// ⚠️ `Lasting` は **−1**（「切れない」の印であって回数ではない）。
        /// そのまま掛けると**価値が負になる** ── 実際 パッシブ3件（生命力・頑丈・身軽）が
        /// −0.02〜−0.21手ぶんで並んでいた（2026-08-27 に発見）。
        /// ⚠️ このファイルは同じ罠を既に3度踏んでいる（ゲージの負・挑発の Hits・弱化解除の負）。
        /// ⭐ **負の値を「向きの印」に使っている欄は、大きさとして掛ける前に必ず直す。**
        /// 検査 `どの技も手ぶんが負にならない` がこの一族を止める。</summary>
        public const double PaceTurns = 5.6;

        /// <summary>**後で効くものの割引**（毒・リジェネ・強化・弱化）。
        ///
        /// ⚠️ 表に書いてある持続を、そのまま足してはいけない。`sim delivered` の実測（2026-08-19）:
        /// ・毒の持続は**4ターン**だが、実際に削れたのは**平均 2.1回**
        ///   （残り 1,389ターンぶんが捨てられ、うち 580体は毒が乗ったまま倒れた）
        /// ・量では一撃の **1.13倍** 入っているのに、勝率では **±0**
        ///
        /// ⭐ 理由は2つ:
        /// 1. **使い切る前に決着する**（相手が先に倒れる／戦闘が終わる）
        /// 2. **直接ダメージは相手の手番を奪うが、後から効くものは奪わない** ──
        ///    同じ総量でも、遅れて入るぶん相手が動く回数が増える
        ///
        /// 🚧 **毒1件からの見積もり。**強化・弱化にも同じ係数を当てているが、測っていない。</summary>
        public const double LateDiscount = 0.7;

        /// <summary>防御ステを <paramref name="pct"/> だけ増やしたとき、被ダメが何割減るか。
        ///
        /// ⚠️ **式は `Battle.DamageOf` と同じ二乗**（2026-08-26 に1乗から変わった）。
        /// ⭐ ここが1乗のまま取り残されていて、防御+30% を **−3%**（正しくは −6%）と
        /// 報告していた（2026-08-27 に発見）── 物差しは本体と一緒に直すこと。
        /// ⚠️ 使うのは**生まれつき（パッシブ）だけ**。札の防御はステを通らない。</summary>
        private static double SoftenGap(int def, double pct)
        {
            double now = (double)Battle.DefSoften / (Battle.DefSoften + def);
            double moved = (double)Battle.DefSoften / (Battle.DefSoften + def * (1 + pct));
            return Math.Abs(1 - moved * moved / (now * now));
        }

        public static StatBlock Middle()
        {
            var baseSum = new int[Stats.Keys.Length];
            int count = 0;
            foreach (var sp in SpeciesTable.All)
            {
                if (sp.Id == Encounters.BossSpeciesId) continue;
                for (int i = 0; i < Stats.Keys.Length; i++) baseSum[i] += sp.Base[Stats.Keys[i]];
                count++;
            }
            int wildEach = Stats.WildTotalMax / Stats.Keys.Length;
            var mid = new StatBlock(0, 0, 0, 0);
            for (int i = 0; i < Stats.Keys.Length; i++)
                mid = mid.With(Stats.Keys[i], baseSum[i] / count + wildEach * Stats.Scale);
            return mid;
        }

        /// <summary>技1つの手ぶん。⭐ **帳面の検査から呼ぶ入口。**
        /// ⚠️ 表に載っていない技（まだ実装していないもの）も測れる。</summary>
        public static double Of(Skill skill, out string why)
        {
            var mid = Middle();
            int atk = mid.Atk, def = mid.Def;
            int maxHp = mid.Hp * Battle.HpScale;
            int one = Battle.DamageOf(Skills.DamagePowerOf(PowerTier.Medium), atk, def, 1.0);

            double total = 0;
            bool guessed = false;
            var reasons = new List<string>();
            // ⭐ **前置きの効きを数える**（2026-08-27）。⚠️ 自分に掛ける強化を
            //    ダメージ**より前**に書くと、`Battle` はその一撃にも乗せる ──
            //    数えないと「攻増→大撃」が「大撃→攻増」と同じ値に見え、
            //    書く順で強さが変わるという設計そのものが測れない。
            var ahead = new Dictionary<StatKey, double>();
            foreach (var e in skill.Effects)
            {
                double got = ValueOf(e, skill, mid, maxHp, one, ref guessed, reasons)
                    * e.Chance / 100.0;
                if (e.Kind == EffectKind.Damage && ahead.Count > 0)
                {
                    var lifts = Battle.AttackStatOf(mid, new UnitStatus(), e.Scale) == 0
                        ? 0.0 : LiftFor(ahead, e.Scale);
                    if (lifts > 0)
                    {
                        got *= 1 + lifts;
                        reasons.Add($"前置きで威力+{lifts * 100:0}%");
                    }
                }
                // ⚠️ **全体は対象数ぶん。**⭐ 技の Target でなく、**その効果自身の飛び先**
                //    （`e.Own ?? skill.Target`）で見る（2026-08-27・唯一の出所は <see cref="IsArea"/>）。
                //    ⚠️ 以前はここを技全体の話にしていた（`!HasDamage(skill) && 技のTarget が全体`）
                //    ので、**ダメージを持つ技は丸ごとこのゲートから外れていた**。ダメージ効果自身は
                //    下の Damage 分岐で既に掛けているのに、**同居する弱化・強化には掛からない**ままだった
                //    ── `collapse`（崩落＝全体攻撃＋防御DOWN＋速度DOWN）が 2.61 に沈み、
                //    実際は約 3.62（★4→★5相当）だった。
                // ⚠️ **ダメージは二重に掛けない。**Damage 分岐が自分の飛び先を見て
                //    既に `AreaTargets` を掛けている。
                if (e.Kind != EffectKind.Damage && IsArea(e.Own ?? skill.Target))
                    got *= AreaTargets;
                // ⚠️ **自分に掛ける強化だけ。**味方1体への配りは、その一撃を撃つ本人に
                //    乗るとは限らない（誰に配るかはプレイヤーが決める）
                if (e.Kind == EffectKind.Buff && e.Own == Target.Self && e.Sign > 0 && !e.Innate)
                    ahead[e.Stat] = Skills.BuffPercentOf(e.Stat) / 100.0;
                total += got;
            }

            why = (guessed ? "見積 " : "") + string.Join(" ＋ ", reasons);
            return total;
        }

        /// <summary>その一撃が乗るステに、前置きの強化が掛かっているか。
        /// ⚠️ 依存ステと同じ軸の強化だけが効く（速度依存の一撃に攻撃力UPは乗らない）。</summary>
        private static double LiftFor(Dictionary<StatKey, double> ahead, DamageScale scale)
        {
            var key = scale == DamageScale.Def ? StatKey.Def
                : scale == DamageScale.Spd ? StatKey.Spd : StatKey.Atk;
            return ahead.TryGetValue(key, out double lift) ? lift : 0.0;
        }

        /// <summary>その飛び先が「全体」か。⭐ **唯一の出所**（ダメージ内部の実効体数も、
        /// 同居する弱化・強化の実効体数も、ここを通す）。</summary>
        private static bool IsArea(Target target) =>
            target == Target.EnemyAll || target == Target.AllyAll;

        /// <summary>効果1つの手ぶん。⚠️ 確率は呼び側で掛ける。</summary>
        private static double ValueOf(Effect e, Skill skill, StatBlock mid, int maxHp, int one,
            ref bool guessed, List<string> why)
        {
            int atk = mid.Atk, def = mid.Def;
            switch (e.Kind)
            {
                case EffectKind.Damage:
                {
                    // ⚠️ **`== Atk ? atk : def` と書かない。**スピード依存を足した日に、
                    //    Spd が黙って**防御**で測られていた（2026-08-19 の監査）。
                    //    ⭐ 選び方は Core の1か所（Battle.AttackStatOf）に寄せる。
                    int stat = Battle.AttackStatOf(mid, new UnitStatus(), e.Scale);
                    int hit = Battle.DamageOf(Skills.DamagePowerOf(e.Power), stat,
                        e.Pierce ? 0 : def, 1.0);
                    // ⚠️ ランダムな1体は「1体」。全体と同じに数えない
                    // ⭐ 全体は **実効の体数**（`AreaTargets`）── 4体そのままは高く見すぎる
                    // ⚠️ **判断は `IsArea` 1つに寄せる**（この効果自身の飛び先で見る）。
                    double targets = IsArea(e.Own ?? skill.Target) ? AreaTargets : 1;
                    double v = (double)hit * e.Repeat * targets / one;
                    why.Add($"ダメージ {hit:N0}×{e.Repeat}×{targets:0.#}体");
                    // ⚠️ **数えるぶんを足していなかった**（2026-08-27 に発見）。
                    //    ⭐ 盤面を数える技（`Tally`）は満載で威力が約2.2倍になるのに、
                    //    見積りは素の一撃と同じ値を出していた ── 追い崩し・驕り討ちが
                    //    0.80手ぶんで「殴るより弱い」と並んでいたのはこれが原因。
                    if (e.Per != Tally.None)
                    {
                        guessed = true;
                        v *= 1 + Skills.PerBonusPercent * GuessTallyStacks / 100.0;
                        why.Add($"数え{GuessTallyStacks}つぶん（威力+{Skills.PerBonusPercent * GuessTallyStacks}%）");
                    }
                    return v;
                }
                case EffectKind.HealRatio:
                    // ⚠️ **負は「最大HPを削る」の印**（命削り）。回復として掛けると
                    //    価値が負になる ── 実際 命削りが −0.72手ぶんで並んでいた（2026-08-27）。
                    if (e.Percent < 0)
                    {
                        // ⭐ 相手の使える HP がそのぶん消える＝防御も属性も通さない削り
                        why.Add($"最大HPの{-e.Percent}%を削る（防御を通さない）");
                        return (double)maxHp * -e.Percent / 100.0 / one;
                    }
                    // ⚠️ **満タンに近い相手へ撃つと、はみ出したぶんは捨てられる。**
                    //    ここは「削られた相手に撃った」ときの上限値なので、実戦では下がる
                    why.Add($"回復 最大HPの{e.Percent}%（削られた相手に撃ったとき）");
                    return (double)maxHp * e.Percent / 100.0 / one;

                case EffectKind.Poison:
                case EffectKind.Regen:
                {
                    double amount = (double)maxHp * Skills.TickPercent / 100.0 * e.Stacks * e.Turns;
                    why.Add($"{(e.Kind == EffectKind.Poison ? "毒" : "回復")} "
                        + $"最大HPの{Skills.TickPercent * e.Stacks * e.Turns}%（後で効くので割引）");
                    return amount / one * LateDiscount;
                }
                case EffectKind.Shield:
                    why.Add($"盾{e.Count}枚＝相手の{e.Count}発を消す");
                    return e.Count;

                case EffectKind.Stun:
                    why.Add($"相手の{e.Turns}手を消す");
                    return e.Turns;

                case EffectKind.Sleep:
                    why.Add($"相手の{e.Turns}手を消す（殴ると解ける→半分）");
                    return e.Turns * 0.5;

                case EffectKind.Buff:
                {
                    // ⚠️ **生まれつき（パッシブ）は効き目が別の定数。**⭐ 手番を1回も払わない
                    //    ぶん小さい（`Skills.InnatePercent` 対 `BuffPercent`）── 同じ 30% で
                    //    数えると、パッシブが強化と同じ価値に見える
                    double pct = (e.Innate ? Skills.InnatePercent : Skills.BuffPercentOf(e.Stat))
                        / 100.0;
                    // ⚠️ **`Lasting`（−1）は「切れない」の印であって回数ではない。**
                    //    ⭐ 切れないなら戦闘のあいだ効き続けるので、動ける回数ぶん数える
                    double turns = e.Turns == Skills.Lasting ? PaceTurns : e.Turns;
                    string span = e.Turns == Skills.Lasting ? "戦闘のあいだずっと"
                        : $"{e.Turns}ターン";
                    // 🔴 **自分への代償（`reckless` の防御DOWN 等）だけが値引かれていなかった**
                    //    （2026-08-27 監査で発覚）。⚠️ `Skills.LoadOf`（CTの値段）・
                    //    `Ai.ScoreOfSkill`（AIの採点）は `Skills.IsSelfCost` で正しく値引いて
                    //    いたのに、ここだけ判定が無く常に加点していた ── `reckless` が
                    //    「素の一撃」より値打ちが高く見えていた（1.33→2.38）。
                    //    ⭐ 大きさの計算はそのまま、最後に符号だけ `Skills.SignedByCost` へ通す
                    //    （判断の出所は増やさない）。
                    bool selfCost = Skills.IsSelfCost(e);
                    // ⭐ **防御は被ダメそのものに掛かる**（2026-08-27・`Battle.Guarded`）。
                    // ⚠️ ここには軽減式（`DefSoften/(DefSoften+防御)`）を再現した見積りが在り、
                    //    ①**式が1乗のまま**だった（本体は 2026-08-26 に二乗へ）
                    //    ②そもそもステ経由なので +30% が −3% にしかならなかった
                    //    ⭐ いまは掛ける先が被ダメなので、言った割合がそのまま出る。
                    if (Skills.GuardsDamage(e.Stat))
                    {
                        // ⚠️ **生まれつきだけは別経路。**パッシブはステそのものに焼かれるので
                        //    （`Battle.WithPassives`）、軽減式を通って薄まる ── 札とは効き方が違う
                        double gap = e.Innate ? SoftenGap(def, pct) : pct;
                        // ⚠️ 「被ダメ／与ダメ」の向きは、乗る先が自分か相手かで変わる。
                        //    ⭐ 相手の防御DOWN は相手の被ダメが増える＝こちらの与ダメが増える（得）。
                        //    自分の防御DOWN（代償）は自分の被ダメが増える（損）。
                        why.Add(e.Sign > 0 ? $"被ダメ −{gap * 100:0.#}% × {span}（後で効くので割引）"
                            : selfCost ? $"自分の被ダメ +{gap * 100:0.#}% × {span}（代償・後で効くので割引）"
                            : $"与ダメ +{gap * 100:0.#}% × {span}（後で効くので割引）");
                        return Skills.SignedByCost(e, gap * turns * LateDiscount);
                    }
                    why.Add($"{Stats.LabelOf(e.Stat)} {(e.Sign > 0 ? "+" : "−")}{pct * 100:0}%"
                        + $" × {span}（後で効くので割引）" + (selfCost ? "（代償）" : ""));
                    return Skills.SignedByCost(e, pct * turns * LateDiscount);
                }
                case EffectKind.Gauge:
                    // ⚠️ 減らす側は Percent が負。そのまま返して **−0.26 で並んでいた**（道具の不備）。
                    //    ⭐ 相手のゲージを減らすのも「相手の手番を削る」ぶんの価値がある
                    why.Add($"ゲージ {e.Percent:+0;-0}%");
                    return Math.Abs(e.Percent) / 100.0;

                case EffectKind.Ct:
                    guessed = true;
                    why.Add($"CT {e.Delta:+0;-0}");
                    return Math.Abs(e.Delta) * GuessCtPerStep;

                case EffectKind.Taunt:
                    // ⭐ 実測: 与えた回数の 5% しか狙いをずらしていない（`sim guess`）
                    guessed = true;
                    why.Add($"狙いを{e.Hits}回ずらす（実際にずれるのは {e.Hits * TauntPullPerHit:0.00}回）");
                    return e.Hits * TauntPullPerHit;

                case EffectKind.Guts:
                    why.Add($"致命傷を耐える（実測 {GutsSavesPerCast:0.00}回）");
                    return GutsSavesPerCast;

                // 🔴 **持続を掛けない。**⚠️ 実測で 3ターンと6ターンの効き目が同じだった
                //    （縛っているのは持続ではなく「その1体が狙われるか」）。
                //    ⭐ 掛けていた頃は 免疫・長 が 1.80手ぶん＝★2 に化けていた。
                case EffectKind.Immune:
                    why.Add($"弱化を弾く（実測 {ImmuneBlocksPerCast:0.00}回・持続では増えない）");
                    return ImmuneBlocksPerCast;

                case EffectKind.Block:
                    why.Add($"回復・強化を弾く（実測 {BlockBluntsPerCast:0.00}回）");
                    return BlockBluntsPerCast;

                // ── 2026-08-27 に足した5つ。🚧 どれも未測定（`sim guess` で潰せる形）──
                case EffectKind.Seal:
                    guessed = true;
                    why.Add($"枠2・3 を{e.Turns}回ぶん封じる");
                    return e.Turns * GuessSealPerTurn;
                case EffectKind.Anchor:
                    guessed = true;
                    why.Add($"弱化を{e.Turns}回ぶん落とせなくする");
                    return e.Turns * GuessAnchorPerTurn;
                // ⭐ 無敵は「受ける一撃をまるごと消す」── 盾1枚と同じ数え方
                // ⭐ **毒も止める**（2026-08-27）ので、1回ぶんが一撃より少し重い
                case EffectKind.Invincible:
                    why.Add($"{e.Turns}回ぶん一撃と毒を消す");
                    return e.Turns * InvinciblePerTurn;
                // ⚠️ **単体では 0。**乗っている弱化が無ければ何も起きない札なので、
                //    「1つは乗っている」を見込んだ数にしてある
                case EffectKind.Extend:
                    guessed = true;
                    why.Add($"乗っている弱化を{e.Turns}回ぶん伸ばす");
                    return e.Turns * GuessExtendPerTurn;
                // ⭐ 返す割合ぶん。⚠️ 殴られないと働かないので割り引く
                // ⭐ **枠1 の一撃を返す**（2026-08-27）。⚠️ 枠1 ＝ 1.0手ぶんが物差しの基準なので、
                //    返す1回はそのまま 1.0。⚠️ 殴られないと働かないので割り引く。
                case EffectKind.Counter:
                    guessed = true;
                    why.Add($"殴られたら枠1 で返す（{e.Turns}回ぶん）");
                    return e.Turns * LateDiscount;

                // ⚠️ **負の Count は「味方の弱化を落とす」の印**（`Effect.Cleanse`）。
                //    そのまま掛けると価値が負になる ── 実際 弱化解除が −1.80、
                //    弱化解除・全体が −3.60 手ぶんで並んでいた（2026-08-27）。
                case EffectKind.Dispel:
                {
                    guessed = true;
                    int many = Math.Abs(e.Count);
                    why.Add(e.Count < 0 ? $"弱化を{many}つ落とす" : $"強化を{many}つ消す");
                    return many * GuessBuffWorth;
                }
                case EffectKind.Steal:
                    guessed = true;
                    why.Add($"強化を{Math.Abs(e.Count)}つ奪う（消す＋得る）");
                    return Math.Abs(e.Count) * GuessBuffWorth * 2;

                case EffectKind.Revive:
                    // ⭐ **勘をやめて算数にした**（2026-08-27）。値打ちの本体は
                    //    「戻した HP を相手に削り直させること」── 回復とまったく同じ式。
                    //    ⚠️ 実測で、戻った個体が動いた回数は 0.20〜0.47回しかない
                    //    （見積りは 3.0 だった ── 6〜15倍の過大評価）。
                    why.Add($"HP{e.Percent}%で復帰（動き直すのは {ReviveActs:0.0}回）");
                    return (double)maxHp * e.Percent / 100.0 / one + ReviveActs;

                default:
                    guessed = true;
                    why.Add(e.Kind.ToString());
                    return 0;
            }
        }
        /// <summary>⭐ **技の格（1〜5）。**⚠️ ★と同じ段数（★N の卵は 格N まで引ける）。
        ///
        /// ⭐ **期待値（手ぶん）で切る。**⚠️ 効果の数で切ると、
        /// 「毒10重ね」のような単品が格1に落ちる（実測では格5相当の 3.62手ぶん）。
        ///
        /// ⚠️ <see cref="Floor"/> を下回る技は**枠1 で殴るより損**なので、格を持たない
        /// （0 を返す）── 押す理由の無い技は、どの★にも配ってはいけない。</summary>
        public static int GradeOf(Skill skill)
        {
            double value = Of(skill, out _);
            if (value < Floor) return 0;
            for (int grade = 1; grade < Bands.Length + 1; grade++)
            {
                if (grade - 1 < Bands.Length && value < Bands[grade - 1]) return grade;
            }
            return Bands.Length + 1;
        }

        /// <summary>⚠️ **どの技もこれ以上でなければならない。**枠1 の一撃（CT なし・ただ）が
        /// 1.0 なので、下回る技は「押すと手番を損する」── 存在しても選ばれない。</summary>
        public const double Floor = 1.0;

        /// <summary>格の境目。⭐ 格1 は <see cref="Floor"/>〜Bands[0]、格5 は Bands[3] 以上。
        /// ⚠️ 実測の分布（2026-08-27・76本）から取った ── 勘で等間隔に切らない。</summary>
        public static readonly double[] Bands = { 1.3, 1.8, 2.4, 3.2 };
    }
}
