#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>Wiki のうち、**表が中身のページ**を書き出す。
    ///
    /// ⚠️ **数値を手で転記しない。** 種族・技・特性の表は実装が唯一の出所なので、
    /// 手で写すと必ずどこかでずれる。ずれた Wiki は、無いより悪い
    /// （読んだ人が嘘を信じて編成を組む）。
    ///
    /// ⭐ 説明文もここに置く。ページ全体を生成するので、
    /// 「表だけ生成して周りは手書き」より出所が1つに保たれる。
    /// ⚠️ 手で直したくなったら**このファイルを直す**。生成物を直しても次の生成で消える。
    ///
    /// ⚠️ 生成するのはこの3ページだけ。他のページは手で書く
    /// （遊びの手触りや判断の指針は、表から導けない）。
    /// </summary>
    public static class WikiPages
    {
        /// <summary>生成したページの名前を返す。</summary>
        public static List<string> Write(string dir)
        {
            Directory.CreateDirectory(dir);
            var written = new List<string>();
            written.Add(Put(dir, "種族一覧.md", SpeciesPage()));
            written.Add(Put(dir, "技一覧.md", SkillsPage()));
            written.Add(Put(dir, "特性.md", TraitsPage()));
            written.Add(Put(dir, "ダメージ計算.md", DamagePage()));
            return written;
        }

        private static string Put(string dir, string name, string body)
        {
            File.WriteAllText(Path.Combine(dir, name), body, new UTF8Encoding(false));
            return name;
        }

        /// <summary>⚠️ 生成物だと分かるようにする。手で直しても消えることを先に伝える。</summary>
        private static void Stamp(StringBuilder md)
        {
            md.Append("> ⚠️ **このページは実装から自動生成しています。**")
              .Append("直接編集しても次の生成で消えます。\n")
              .Append("> 数値を変えるときは実装側を変えて `sim wiki` を回してください。\n\n");
        }

        // ── 種族 ────────────────────────────────────────

        private static string SpeciesPage()
        {
            var md = new StringBuilder();
            md.Append("# 種族一覧\n\n");
            Stamp(md);

            md.Append("種族が決めるのは**見た目・基礎ステの配分・枠1の技・特性・卵ガチャの中身**です。\n");
            md.Append("⚠️ **基礎ステの合計は全種族で同じ**なので、種族に当たり外れはありません。");
            md.Append("違うのは配分だけです。\n\n");

            md.Append("## 一覧\n\n");
            md.Append("| 種族 | HP | 攻撃 | 防御 | 速度 | 弱化命中 | 弱化耐性 | 枠1（通常攻撃） | 特性 |\n");
            md.Append("|---|---|---|---|---|---|---|---|---|\n");
            foreach (var s in SpeciesTable.All)
            {
                var b = s.Base;
                // ⚠️ ボスは巣を持たないので卵から出ない。⭐ 表に出るのに手に入らない、
                //    という読み違いを起こすので印を付ける
                string boss = s.Id == Encounters.BossSpeciesId ? " ⚠️ボス専用" : "";
                md.Append($"| {s.Name}{boss} | {b.Hp} | {b.Atk} | {b.Def} | {b.Spd} | ")
                  .Append($"{b.Acc} | {b.Res} | {Skills.ById(s.Skill1).Name} | ")
                  .Append($"{Traits.ById(s.TraitId).Name} |\n");
            }
            md.Append($"\n基礎ステの合計はどの種族も **{SpeciesTable.BaseTotal}** です。\n");
            md.Append("⚠️ **ボス専用の種族は手に入りません。**巣を持たないので卵から出ません。\n");
            md.Append("⚠️ **弱化命中と弱化耐性にも基礎値があります**（2026-08-19 から）。\n");
            md.Append($"⭐ この2本だけの合計は全種族 {SpeciesTable.DebuffBaseTotal} で、ここも揃っています。");
            md.Append("攻める種族は命中寄り、守る種族は耐性寄りです。\n\n");

            md.Append("## 特性\n\n");
            md.Append("⭐ **特性は種族ごとに1つ、固定です。**同じ種族なら、"
                + "どこで手に入れても・何代目でも同じ特性を持ちます。\n");
            md.Append("⚠️ **引き直せません。**欲しい特性があるなら、"
                + "それを持つ種族の巣へ行ってください。\n");
            md.Append("⭐ 何をするかは[特性](特性.md)で読めます。\n\n");

            md.Append("## 枠1（通常攻撃）\n\n");
            md.Append("⭐ 枠1 は**その種族の通常攻撃**で、CT がありません（いつでも撃てます）。\n");
            md.Append("⚠️ CT が無いのは「行動できない手番を作らない」ためで、大技だからではありません。\n\n");

            md.Append("## 卵ガチャで出る技\n\n");
            md.Append("孵化のとき、枠2・3 はここから1つずつ引かれます。\n");
            md.Append("⭐ **枠ごとに引く型が決まっています。**");
            md.Append("だから「この巣からはこの形が出る」が先に分かります。\n");
            md.Append("⚠️ 型は アタック（殴る）/ サポート（味方を強くする）/ ");
            md.Append("デバフ（相手を弱くする）/ ヒール（HP を戻す）の4つです。\n\n");
            md.Append("| 種族 | 枠2 | 枠3 |\n|---|---|---|\n");
            foreach (var s in SpeciesTable.All)
            {
                // ⚠️ ボスの巣は探索に出ないので、この表に載せると「行けば取れる」と読める
                if (s.Id == Encounters.BossSpeciesId) continue;
                md.Append($"| **{s.Name}** | {SlotCell(s.Slot2)} | {SlotCell(s.Slot3)} |\n");
            }
            md.Append("\n⚠️ **枠1 と同じ技はそこから外れます**（同じ技が2枠を占めると片方が無駄になる）。\n");

            md.Append("\n## 関連\n\n- [技一覧](技一覧.md)\n- [ステータス](ステータス.md)\n- [探索](探索.md)\n");
            return md.ToString();
        }

        /// <summary>1枠ぶんの中身。⭐ 型を太字で先に出す（読む人が最初に見るのは型）。</summary>
        private static string SlotCell(SkillPool slot)
        {
            var names = new List<string>();
            foreach (var id in slot.Pool) names.Add(Skills.ById(id).Name);
            return $"**{Skills.FlavorOf(slot.Pool)}** … " + string.Join(" / ", names);
        }

        // ── 技 ──────────────────────────────────────────

        /// <summary>ダメージ計算・弱化計算・効果量。
        ///
        /// ⚠️ **手で書かない。**桁上げ（2026-08-19）で軟化定数が 20/110 → 100/550 になったのに、
        /// このページは手で転記してあったため古い数のまま残っていた。
        /// ⭐ 実装が唯一の出所なので、ここから書き出す。</summary>
        private static string DamagePage()
        {
            var md = new StringBuilder();
            md.Append("# ダメージ計算\n\n");
            Stamp(md);

            // ── ダメージ ──────────────────────────────
            md.Append("## ダメージ\n\n```\n");
            md.Append($"ダメージ ＝ 攻撃力 × 威力倍率 × {Battle.DamageBase}");
            md.Append($" × {Battle.DefSoften} ÷ ({Battle.DefSoften} ＋ 防御) × 属性倍率\n");
            md.Append("```\n\n");
            md.Append("⭐ **攻撃力にまっすぐ比例します。**威力は「攻撃力の何倍か」です。\n");
            md.Append("⚠️ ダメージは**最低でも 1**。0 にはなりません。\n\n");

            md.Append("### 威力の段位\n\n| 段位 | 攻撃力の |\n|---|---|\n");
            foreach (PowerTier tier in Enum.GetValues(typeof(PowerTier)))
            {
                md.Append($"| {Skills.LabelOf(tier)} | **×{(double)Skills.DamagePowerOf(tier) / Skills.PowerUnit:0.0}** |\n");
            }
            md.Append($"\n⭐ スキルレベルが1段上がるごとに **+{Skills.GainPowerPercent}%**。\n\n");

            md.Append("### 防御による軽減\n\n| 防御 | 通るダメージ |\n|---|---|\n");
            foreach (int def in new[] { 0, 150, 300, 600, 1000, 2000 })
            {
                md.Append($"| {def:N0} | {100.0 * Battle.DefSoften / (Battle.DefSoften + def):0}% |\n");
            }
            md.Append($"\n⚠️ **割り算にしていません。**`÷ 防御` にすると防御の低い相手に");
            md.Append("ダメージが爆発し、防御 0 では割れません。\n");
            md.Append($"⭐ `{Battle.DefSoften} ÷ ({Battle.DefSoften} ＋ 防御)` なら 0 でも割れて、");
            md.Append("積むほど効きが飽和します。\n\n");
            md.Append("⭐ 守備側の定数が大きいのは、3体が集中攻撃されるぶん");
            md.Append("防御が実質3倍働くからです。数字だけ見て「防御は効きが悪い」と読まないでください。\n\n");
            md.Append("⚠️ **防御無視**の技は、防御を 0 として扱います（軽減なし）。\n\n");

            md.Append("| | 値 |\n|---|---|\n");
            md.Append($"| 属性 有利 / 不利 | ×{Battle.ElementAdvantage} / ×{Battle.ElementWeakness} |\n");
            md.Append($"| 最大HP | ステの **HP × {Battle.HpScale}** |\n");
            md.Append($"| 基準（HP の桁に合わせる係数）| {Battle.DamageBase} |\n\n");
            md.Append($"⚠️ 基準 {Battle.DamageBase} は式の中で**唯一意味の無い数**です。");
            md.Append("最大HP と 攻撃力 は桁が2つ違うので、どこかで橋を渡す数が要ります。\n");
            md.Append("⭐ 技ごとに散らさず1箇所に置いてあります。\n\n");

            // ── 弱化が通る率 ──────────────────────────
            md.Append("## 弱化が通る率\n\n```\n");
            md.Append("通る率 ＝ 技の素の率");
            md.Append($" ＋ (弱化命中 − 弱化耐性) ÷ {Battle.LandStatDivisor}");
            md.Append(" ＋ 属性 ＋ 特性\n```\n\n");
            md.Append("| | 値 |\n|---|---|\n");
            md.Append($"| ステ差の割り算 | **÷ {Battle.LandStatDivisor}** |\n");
            md.Append($"| 属性 有利 / 不利 | **±{Battle.LandElementSwing}pt** |\n");
            md.Append($"| 特性「狙い澄まし」 | +{Battle.TraitAim}pt |\n");
            md.Append($"| 特性「意地」 | −{Battle.TraitStubborn}pt |\n");
            md.Append($"| 下限 / 上限 | **{Battle.LandFloor}% / {Battle.LandCeil}%** |\n");
            md.Append($"| スキルレベル1段につき | +{Skills.GainChancePoints}pt |\n\n");
            md.Append("⚠️ **素の率が 100% の効果は乱数を引きません**（必ず通ります）。\n");
            md.Append($"⚠️ 上限 {Battle.LandCeil}% は**スキルレベルのぶんを足したあとにも掛かります**。\n\n");
            md.Append("⭐ **免疫**が付いている相手には、弱化は率に関係なく通りません。\n\n");

            // ── 効果量 ────────────────────────────────
            md.Append("## 強化・弱化の効果量\n\n");
            md.Append("| 効果 | 量 |\n|---|---|\n");
            md.Append($"| 攻撃力・防御力・スピードの UP / DOWN | **±{Skills.BuffPercent}%** |\n");
            md.Append($"| 毒・リジェネ（1スタック・1ターンあたり）| 最大HP の **{Skills.TickPercent}%** |\n");
            md.Append($"| 執念（シールドが剥がれるたび）| ゲージ **+{100 * Battle.TraitGritGauge / Battle.GaugeMax}%** |\n");
            md.Append($"| 先駆け（開幕）| ゲージ **+{100 * Battle.TraitOpenerGauge / Battle.GaugeMax}%** |\n");
            md.Append($"| 置き土産（倒れたとき・味方1体につき）| ゲージ **+{100 * Battle.TraitPartingGauge / Battle.GaugeMax}%** |\n");
            md.Append($"| 不意打ち（相手が手番を飛ばすたび）| ゲージ **+{100 * Battle.TraitAmbushGauge / Battle.GaugeMax}%** |\n");
            md.Append($"| 畳み掛け（弱化を通したとき・1戦闘1回）| ゲージ **+{100 * Battle.TraitSurgeGauge / Battle.GaugeMax}%** |\n");
            md.Append($"| 追い打ち（弱化が付いた相手へ）| ダメージ **+{Battle.TraitPursuitPercent}%** |\n");
            md.Append($"| 背水（自分HP半分以下）| ダメージ **+{Battle.TraitDesperationPercent}%** |\n");
            md.Append($"| 粘り腰（自分HP半分以下）| 被ダメージ **−{Battle.TraitTenacityPercent}%** |\n");
            md.Append($"| 返し身（受けたダメージの）| **{Battle.TraitSpitePercent}%** を返す |\n");
            md.Append($"| 食らいつき（与えたダメージの）| **{Battle.TraitLeechPercent}%** を吸う |\n\n");

            md.Append("⚠️ **強化・弱化の持続は3ターン**（その個体の行動回数）。\n");
            md.Append($"⭐ つまり見返りは {Skills.BuffPercent}% × 3 ＝ **{Skills.BuffPercent * 3 / 100.0:0.0}手ぶん**、");
            md.Append("撒くのに使うのは **1手**です。\n\n");

            // ── 手番 ──────────────────────────────────
            md.Append("## 手番（ゲージ）\n\n```\n");
            md.Append($"1刻みで溜まる量 ＝ {Battle.GaugeBase} ＋ スピード\n");
            md.Append($"手番が回るのは  ゲージ {Battle.GaugeMax} に達したとき\n```\n\n");
            md.Append($"⭐ スピード 0 と {Battle.GaugeMax / 10} の差は、手番の速さで ");
            md.Append($"{100.0 * (Battle.GaugeBase + Battle.GaugeMax / 10) / Battle.GaugeBase - 100:0}% です。\n\n");

            md.Append("## 関連\n\n- [ステータス](ステータス.md)\n- [属性](属性.md)\n");
            md.Append("- [効果の種類](効果の種類.md)\n- [技一覧](技一覧.md)\n- [特性](特性.md)\n");
            return md.ToString();
        }

        private static string SkillsPage()
        {
            var md = new StringBuilder();
            md.Append("# 技一覧\n\n");
            Stamp(md);

            md.Append("個体は技を3枠持ちます。⭐ 枠1 は種族固定の通常攻撃（CT なし）、");
            md.Append("枠2・3 は卵ガチャか配合で決まります。\n\n");
            md.Append("| 項目 | 意味 |\n|---|---|\n");
            md.Append("| **型** | 卵の**どの枠から出るか**。⚠️ 戦闘の挙動には効きません |\n");
            md.Append("| **威力** | 1発ぶんの威力。⚠️ ダメージのある技だけ（他は空欄）|\n");
            md.Append("| **効果** | 何が起きるか。⭐ 狙い先・確率・持続をすべて書いています |\n");
            md.Append("| **T** | **その個体の行動回数**。⚠️ 実時間でも全体のターン数でもありません |\n");
            md.Append("| **上昇量** | レベルが1つ上がるたびに伸びる軸（Lv2→Lv5 の順）|\n");
            md.Append("| **CT** | 使ったあと、自分が何回行動するまで再使用できないか |\n");
            md.Append("| **⭐パッシブ** | ⚠️ **押せません。**枠は1つ使いますが、選ぶ対象に出てきません。");
            md.Append("戦闘が始まる前から効いています |\n\n");

            md.Append("## 一覧\n\n");
            md.Append("| スキル名 | 型 | 威力 | 効果 | レベルごとの上昇量 | CT |\n");
            md.Append("|---|---|---|---|---|---|\n");
            foreach (var skill in Skills.All)
            {
                // ⚠️ ボス専用の技は、どの種族のプールにも入っていない＝プレイヤーは持てない
                // ⚠️ 未配布の技も同じく持てない（実装済みだが、まだどの種族にも配っていない）
                string only = Skills.BossOnly.Contains(skill.Id) ? " ⚠️ボス専用"
                    : Skills.Undistributed.Contains(skill.Id) ? " 🚧未配布" : "";
                // ⚠️ **パッシブは CT 欄に 0 が並ぶ。**印が無いと「いつでも押せる技」と読まれる
                if (skill.Passive) only += " ⭐パッシブ";
                string ct = skill.Passive ? "─" : skill.Ct.ToString();
                md.Append($"| {skill.Name}{only} | {Skills.LabelOf(skill.Type)} | {SkillText.PowerOf(skill)} | ")
                  .Append($"{SkillText.Describe(skill)} | {SkillText.GrowthOf(skill)} | {ct} |\n");
            }

            md.Append("\n⚠️ **ボス専用の技は手に入りません。**どの種族の卵ガチャにも入っていません（相手が使うのを見るだけです）。\n");
            md.Append("\n⭐ **パッシブは押す技ではありません。**枠を1つ使ったまま、");
            md.Append("戦闘の間ずっと効いています（⚠️ 効き目は強化より小さい代わりに、");
            md.Append("**手番を1回も使わず、強化解除でも剥がれません**）。\n");
            md.Append("\n🚧 **未配布の技はまだ手に入りません。**実装は済んでいますが、");
            md.Append("どの種族の卵ガチャにもまだ入っていません（配る枠は今後決めます）。\n");
            md.Append("\n## 型\n\n");
            md.Append("⭐ **型は「卵のどの枠から出るか」だけを決めます。**");
            md.Append("戦闘での効き方には一切関係ありません。\n");
            md.Append("⚠️ 効果から自動で決めていません。**技ごとに手で決めています** ── ");
            md.Append("「攻撃しつつスタンを付ける」ような技を、");
            md.Append("殴る枠から出すか崩す枠から出すかは作り手が選ぶことだからです。\n\n");
            md.Append("| 型 | どんな技か |\n|---|---|\n");
            md.Append("| アタック | ダメージを与える |\n");
            md.Append("| サポート | 味方を強くする・守る |\n");
            md.Append("| デバフ | 相手を弱くする・止める |\n");
            md.Append("| ヒール | HP を戻す |\n\n");
            md.Append("⚠️ どの型がどの巣から出るかは[種族一覧](種族一覧.md)を見てください。\n");

            md.Append("\n## 威力の段位\n\n");
            md.Append("| 段位 | 威力 |\n|---|---|\n");
            foreach (PowerTier tier in Enum.GetValues(typeof(PowerTier)))
            {
                // ⚠️ 使っている技が1本も無い段位は、表に出すと「そういう技がある」と読める
                bool used = false;
                foreach (var skill in Skills.All)
                {
                    foreach (var effect in skill.Effects)
                    {
                        if (effect.Kind == EffectKind.Damage && effect.Power == tier) used = true;
                    }
                }
                string idle = used ? "" : " ⚠️ 使っている技はまだありません";
                md.Append($"| {Skills.LabelOf(tier)} | {Skills.DamagePowerOf(tier)}{idle} |\n");
            }
            md.Append("\n⚠️ **全体攻撃は1段下げて選ばれています。**");
            md.Append("全体の「中」は単体の「中」よりずっと強いためです。\n\n");

            md.Append("## 上昇量の伸び幅\n\n");
            md.Append("| 軸 | 1段ごとの伸び |\n|---|---|\n");
            md.Append($"| 威力 | +{Skills.GainPowerPercent}% |\n");
            md.Append("| CT | −1 |\n");
            md.Append($"| 確率 | +{Skills.GainChancePoints}pt |\n");
            md.Append("| 持続 | +1T |\n");
            md.Append("| 発数 | +1発 |\n");
            md.Append($"| 割合 | +{Skills.GainHealPoints}pt |\n");
            md.Append("| 個数 | +1 |\n");
            md.Append("| 量 | +1 |\n\n");
            md.Append("⚠️ **枠1 では CT の段が外れます**（枠1 の CT は常に 0 なので、縮めても何も起きない）。\n");

            md.Append("\n## 関連\n\n- [効果の種類](効果の種類.md)\n- [スキルレベル](スキルレベル.md)\n")
              .Append("- [種族一覧](種族一覧.md)\n");
            return md.ToString();
        }

        // ⚠️ 技と効果の言い回しは **Core の SkillText** に集約した（2026-08-18）。
        //    ここに第2の語彙を置かない ── 置いた結果、同じ効果が
        //    「盾2枚」「シールド 2枚」「免疫（0回）」と3通りに出ていた。

        /// <summary>「効き目」の表に手で書いてある行の数。
        /// ⚠️ 上の表に1行足したら**ここも1つ増やす**。⭐ <see cref="Traits.All"/> と
        /// 食い違うとページに 🚧 が出る。</summary>
        private const int EffectRows = 14;

        // ── 特性 ────────────────────────────────────────

        private static string TraitsPage()
        {
            var md = new StringBuilder();
            md.Append("# 特性\n\n");
            Stamp(md);

            md.Append("特性は**種族ごとに1つ**決まっています。⭐ 技の3枠とは別枠なので、技を圧迫しません。\n\n");
            md.Append("⭐ **手に入れた種族のぶんは[図鑑](図鑑.md)でも読めます**（ホームの右肩）。\n\n");
            md.Append("⚠️ **眠っている間は特性が働きません**（[効果の種類](効果の種類.md)）。\n\n");
            md.Append("⚠️ **特性は技そのものを強くしません。**強くするのは「動き」のほうです。\n");
            md.Append("だから**噛み合う技を持っていないと、持っていても何も起きません**。\n\n");

            md.Append("## 一覧\n\n");
            md.Append("| 特性 | 働く場面 | すること | 噛み合うもの |\n|---|---|---|---|\n");
            foreach (var trait in Traits.All)
            {
                md.Append($"| {trait.Name} | {Traits.LabelOf(trait.When)} | {trait.Gist} | ")
                  .Append($"{Flatten(trait.Pairs)} |\n");
            }

            md.Append("\n## 効き目\n\n");
            md.Append("| | |\n|---|---|\n");
            md.Append($"| 狙い澄まし | 弱化が通る率 +{Battle.TraitAim}pt |\n");
            md.Append($"| 意地 | 弱化を受ける率 −{Battle.TraitStubborn}pt |\n");
            md.Append($"| 返し身 | 受けたダメージの {Battle.TraitSpitePercent}% を返す |\n");
            md.Append($"| 執念 | シールドが1枚剥がれるごとにゲージ +{Battle.TraitGritGauge}"
                + $"（満タンは {Battle.GaugeMax}）|\n");
            md.Append("| 手数 | 1体に当てた発数−1 だけ技の待ちが縮む |\n");
            md.Append($"| 食らいつき | 与えたダメージの {Battle.TraitLeechPercent}% を吸う |\n");
            md.Append($"| 先駆け | 開幕にゲージ +{Battle.TraitOpenerGauge}（満タンは {Battle.GaugeMax}）|\n");
            md.Append($"| 置き土産 | 倒れたとき、残った味方1体ごとにゲージ +{Battle.TraitPartingGauge} |\n");
            md.Append($"| 追い打ち | 弱化が1つでも付いた相手への与ダメージ +{Battle.TraitPursuitPercent}% |\n");
            md.Append($"| 背水 | 自分のHPが半分以下の間、与ダメージ +{Battle.TraitDesperationPercent}% |\n");
            md.Append($"| 粘り腰 | 自分のHPが半分以下の間、受けるダメージ −{Battle.TraitTenacityPercent}% |\n");
            // ⚠️ **ここだけは手で書く。**効き目は特性ごとに単位が違うので
            //    （pt / % / ゲージ / 手番）、表から機械で引けない。
            //    ⚠️ だから**足し忘れる** ── 11行のまま 14件になっていた（2026-08-19 の監査）。
            //    ⭐ 下の突き合わせが、次に足し忘れたときページ自身に印を出す。
            md.Append($"| 畳み掛け | 弱化を通すとゲージ +{Battle.TraitSurgeGauge}"
                + $"（満タンは {Battle.GaugeMax} ＝ **すぐもう一度動ける**）|\n");
            md.Append($"| 不意打ち | 相手が手番を飛ばすたびにゲージ +{Battle.TraitAmbushGauge} |\n");
            md.Append("| 遺志 | 味方が倒れたとき、自分の技の待ちが全部 0 になる |\n");
            if (Traits.All.Count != EffectRows)
                md.Append($"\n🚧 ⚠️ 特性は {Traits.All.Count} 件あるのに、"
                    + $"この表は {EffectRows} 行しかありません（足りない行を書き足すこと）。\n");
            md.Append("\n⚠️ **置き土産は毒で倒れたときは働きません**（働くのは「一撃を受けて」倒れたときだけ）。\n");
            md.Append("⚠️ **追い打ちは弱化の有無だけを見ます。**重ねても増えません。\n");
            md.Append("⚠️ **畳み掛けと遺志は1戦闘に1回だけです。**\n");

            md.Append("\n## 誰が持つか\n\n");
            md.Append("⭐ **特性は種族ごとに決まっています。**同じ種族なら、"
                + "どこで手に入れても・何代目でも同じ特性です。\n");
            md.Append("⚠️ **引き直せません。**欲しい特性があるなら、"
                + "それを持つ種族の巣へ行ってください。\n\n");
            md.Append("| 種族 | 特性 |\n|---|---|\n");
            foreach (var sp in SpeciesTable.All)
            {
                md.Append($"| {sp.Name} | {Traits.ById(sp.TraitId).Name} |\n");
            }
            var parked = new List<string>();
            foreach (var id in Traits.Unassigned) parked.Add(Traits.ById(id).Name);
            if (parked.Count > 0)
            {
                md.Append($"\n🚧 ⚠️ **{string.Join("・", parked)}** は、"
                    + "まだどの種族も持っていません（表にはありますが手に入りません）。\n");
            }

            md.Append("\n## どこで見えるか\n\n");
            md.Append("**BOX** の詳細に、名前とすることが1行で出ます。\n\n");
            md.Append("⚠️ 一覧の升には出ません。⭐ 全員が持っているので、"
                + "升に印を付けても探す手がかりになりません。\n");
            md.Append("⭐ 姿が分かれば特性も分かります（同じ種族はいつも同じ特性です）。\n");

            md.Append("\n## 関連\n\n- [種族一覧](種族一覧.md)\n- [技一覧](技一覧.md)\n")
              .Append("- [配合](配合.md)\n");
            return md.ToString();
        }

        /// <summary>⚠️ 表の中に改行や強調が入ると崩れるので均す。</summary>
        private static string Flatten(string text) =>
            text.Replace("**", "").Replace("\n", " ").Replace("|", "／");
    }
}
