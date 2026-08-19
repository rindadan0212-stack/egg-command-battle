#nullable enable
using System;
using System.Collections.Generic;

namespace EggCommand.Core
{
    /// <summary>特性が働く場面。
    ///
    /// ⭐ **語彙をここで固定する。** 特性は「戦闘のあちこちに割り込むもの」なので、
    /// 条件を自由に書けるようにすると <see cref="Battle"/> が特性だらけになる。
    /// 割り込む場所をこの数に限れば、フックも同じ数で足りる。
    ///
    /// ⚠️ 増やす前に、既にある条件で書けないか必ず疑う（効果のプリミティブと同じ約束）。</summary>
    public enum TraitWhen
    {
        /// <summary>常に効いている。</summary>
        Always,
        /// <summary>戦闘が始まったとき1回。</summary>
        BattleStart,
        /// <summary>攻撃を当てたとき。</summary>
        OnHit,
        /// <summary>攻撃を受けたとき。</summary>
        OnHurt,
        /// <summary>盾が1枚剥がれたとき。</summary>
        OnShieldBreak,
        /// <summary>倒れる一撃を受けたとき。</summary>
        OnDown,
        /// <summary>弱化を**通した**とき。⚠️ 撃ったときではない ── 外れたら働かない。
        /// ⭐ 「通すこと」自体が報酬の条件になるので、通りやすさ（弱化命中・狙い澄まし・
        /// 属性有利）に投資する理由がここで生まれる。</summary>
        OnLand,

        // ── ここから下は「盤面」を見る場面（2026-08-19）──────────
        // ⭐ 上の6つは全部「自分に起きたこと」だった。自分の中で完結する条件は
        //    **待つことしかできない**ので、プレイヤーの計画に入らない。
        //    ⚠️ 盤面を見る条件だけが「その状態を作りに行く」動機になる
        //    （まもダンの進化スキルはほぼ全部この形・2026-08-19 調査）。

        /// <summary>**相手**が手番を飛ばしたとき（スタン・睡眠）。
        /// ⭐ 「止めてから動く」という手順そのものが条件になる。</summary>
        FoeSkipped,

        /// <summary>**味方**が倒れたとき。⚠️ 自分が倒れたときは <see cref="OnDown"/>。
        /// ⭐ 崩れ始めてから効くので、守りきる編成とは別の勝ち筋になる。</summary>
        AllyDown,
    }

    /// <summary>個体が1つ持つ特性。⭐ **技の3枠とは別枠**（枠を奪わない）。
    ///
    /// ⭐ これがある理由: 技を選ぶ側（個体）に個性が無いと、
    /// どの個体にどの技を付けても同じように働いてしまい、**判断が生まれない**。
    /// 特性は「特定の技・特定の動きだけを強くする」ので、
    /// 「この個体には低確率の大技を持たせる」という組み合わせの判断ができる。
    ///
    /// ⚠️ 特性は**技そのものを強くしない**。強くするのは「動き」のほう。
    /// 技を直に強くすると、結局その技を持つのが正解、で終わる。</summary>
    public sealed class Trait
    {
        public readonly string Id;
        public readonly string Name;
        public readonly TraitWhen When;
        /// <summary>画面に出す短い説明。⚠️ 凝った言い回しにしない。</summary>
        public readonly string Gist;
        /// <summary>何と噛み合うか。⭐ **図鑑に出す。**
        /// 特性の値打ちは単体では読めず、組み合わせでしか読めないため。</summary>
        public readonly string Pairs;

        public Trait(string id, string name, TraitWhen when, string gist, string pairs)
        {
            Id = id;
            Name = name;
            When = when;
            Gist = gist;
            Pairs = pairs;
        }
    }

    /// <summary>特性表。⭐ <see cref="Wired"/> 件すべてが <see cref="Battle"/> に繋がっている。
    ///
    /// ⚠️ **件数をここに書かない。**11 と書いたまま 14 になっていた（2026-08-19 の監査）。
    ///
    /// 割り込み先:
    /// | 常時 | <see cref="Battle.LandChanceOf"/>（弱化の通る率） |
    /// | 戦闘開始時 | <see cref="Battle.CreateBattle"/>（開幕のゲージ） |
    /// | 当てた / 受けた / 盾が剥がれた / 倒れた | <c>Battle.DealDamage</c> |
    /// | 当てた（発数） | <see cref="Battle.PerformAction"/>（技の待ち） |
    /// | 当てた（弱化した相手 / 自分が半分以下） | <c>Battle.ApplyEffect</c>（ダメージの計算） |
    ///
    /// ⭐ 2026-08-19 に**条件付きの層**を足した（開幕 / 倒れ際 / 相手の弱化 / 自分のHP）。
    /// 参考にしたのは放置RPGの「個体に固定で1つ付くパッシブ」の設計 ──
    /// **条件 × 効果**の形で書き、条件が重い（満たしにくい）ものほど効き目を大きく取れる。
    /// ⚠️ ただし向こうの「確率で発動」「発動に内部CT」の形は採らない。
    /// 戦闘の乱数は「弱化が通るか」の1本だけ、という約束を崩さない。
    /// ⚠️ 繋いでいないものを増やさない（繋いだ数と表の数は <see cref="Audit"/> が突き合わせる）。</summary>
    public static class Traits
    {
        /// <summary>いま戦闘に繋がっている特性の数。⚠️ 足したらここと <see cref="WiredIds"/> を両方上げる。</summary>
        public const int Wired = 14;

        // ⭐ 戦闘が割り込み先を探すための id。⚠️ <see cref="Battle"/> に文字を直接書かない。
        //    書くと綴り違いが「何も起きない」として通ってしまい、繋いだつもりで繋がっていない
        //    状態に気づけない（<see cref="Audit"/> がここを表と突き合わせる）。
        public const string Aim = "aim";
        public const string Stubborn = "stubborn";
        public const string Spite = "spite";
        public const string Grit = "grit";
        public const string Flurry = "flurry";
        public const string Leech = "leech";
        public const string Opener = "opener";
        public const string Parting = "parting";
        public const string Pursuit = "pursuit";
        public const string Desperation = "desperation";
        public const string Tenacity = "tenacity";
        public const string Surge = "surge";
        public const string Ambush = "ambush";
        public const string Legacy = "legacy";

        /// <summary>戦闘が参照する id の一覧。⚠️ 繋いだものだけを並べる。</summary>
        private static readonly string[] WiredIds =
        {
            Aim, Stubborn, Spite, Grit, Flurry, Leech,
            Opener, Parting, Pursuit, Desperation, Tenacity, Surge, Ambush, Legacy,
        };

        public static string LabelOf(TraitWhen when)
        {
            switch (when)
            {
                case TraitWhen.Always: return "常時";
                case TraitWhen.BattleStart: return "戦闘開始時";
                case TraitWhen.OnHit: return "攻撃を当てたとき";
                case TraitWhen.OnHurt: return "攻撃を受けたとき";
                case TraitWhen.OnShieldBreak: return "シールドが剥がれたとき";
                case TraitWhen.OnDown: return "倒れる一撃を受けたとき";
                case TraitWhen.OnLand: return "弱化を通したとき";
                case TraitWhen.FoeSkipped: return "相手が手番を飛ばしたとき";
                case TraitWhen.AllyDown: return "味方が倒れたとき";
                default: throw new ArgumentOutOfRangeException(nameof(when));
            }
        }

        private static readonly Trait[] List =
        {
            new Trait("aim", "狙い澄まし", TraitWhen.Always,
                "弱化が通る率が上がる",
                "呪詛・スピードDOWN・全体・スタン・大。⭐ **通りにくい技ほど得**なので、博打側の技と組む"),

            new Trait("stubborn", "意地", TraitWhen.Always,
                "弱化を受ける率が下がる",
                "相手の弱化役を腐らせる。⚠️ 相手に弱化役がいないと何もしない"),

            new Trait("spite", "返し身", TraitWhen.OnHurt,
                "受けたダメージの一部を返す",
                "挑発・受けの構え。⭐ **わざと殴られる**動きが得になる"),

            new Trait("grit", "執念", TraitWhen.OnShieldBreak,
                "シールドが剥がれるたびゲージが溜まる",
                "シールド・大・硬化・シールド。⭐ シールドを「守り」から「手数の元」に変える"),

            new Trait("flurry", "手数", TraitWhen.OnHit,
                "多段の1発ごとに技の待ちが縮む",
                "連撃・乱打。⚠️ 単発の技しか持っていない個体では死ぬ"),

            new Trait("leech", "食らいつき", TraitWhen.OnHit,
                "与えたダメージの一部を吸う",
                "攻撃役の自己完結。⭐ 回復役を1枠空けられる"),

            // ── 条件付きの層（2026-08-19）─────────────────────
            // ⭐ ここから下は「条件 × 効果」の形。条件はどれも**画面で確かめられるもの**
            //    （開幕 / 倒れた / 相手に弱化が付いている / 自分のHPが半分以下）に限る。
            // ⚠️ 「確率で発動」「発動に内部CT」の形は作らない ── 戦闘の乱数を増やさない。

            new Trait("opener", "先駆け", TraitWhen.BattleStart,
                "ゲージが進んだ状態で戦闘を始める",
                "免疫・シールド・スピードDOWN・全体・ゲージ上昇。⭐ **先に配る札**が本当に先手になる"),

            new Trait("parting", "置き土産", TraitWhen.OnDown,
                "倒れたとき、残った味方のゲージが進む",
                "挑発・蘇生。⭐ 先に倒れる役が損で終わらなくなる。⚠️ 味方が残っていないと何もしない"),

            new Trait("pursuit", "追い打ち", TraitWhen.OnHit,
                "弱化が付いた相手に与えるダメージが増える",
                "毒・呪詛・スピードDOWN・全体。⭐ **弱化を置いてから殴る**という手順が火力になる"),

            new Trait("desperation", "背水", TraitWhen.OnHit,
                "自分のHPが半分以下の間、与えるダメージが増える",
                "挑発・ガッツ。⭐ わざと受けて低空で殴り続ける。⚠️ 回復役に戻されると条件が消える"),

            // ⭐ **手番そのものを報酬にする唯一の特性**（2026-08-19）。
            //    まもダン『台風の目』（スタンさせるとゲージ +100%）の縮小版。
            // ⚠️ 他の特性は「既にある数字を良くする」だけで、**盤面を動かさない**。
            //    これだけが「もう1手」を生むので、持たせた瞬間に戦い方が変わる
            //    ── 弱化を通しに行く編成そのものが正解になる。
            // ⚠️ 1戦闘1回。上限が無いと、弱化役が弱化を通すたびに動けて手番が止まらない。
            new Trait("surge", "畳み掛け", TraitWhen.OnLand,
                "弱化を通すと、その戦闘で一度だけすぐもう一度動ける",
                "呪詛・毒・スタン・スピードDOWN。⭐ **通す確率に投資するほど早く来る**ので、"
                + "弱化命中の高い個体・狙い澄まし・属性有利と重なる"),

            // ⭐ **盤面を見る2件**（2026-08-19）。条件が「相手の状態」「味方の生死」なので、
            //    持ち主が**その状態を作りに行く**理由になる。
            new Trait("ambush", "不意打ち", TraitWhen.FoeSkipped,
                "相手が手番を飛ばすたび、自分のゲージが進む",
                "スタン・スタン・大・眠り・痺れ打ち。⭐ **止めてから動く**という手順が、"
                + "そのまま自分の手数に変わる。⚠️ 止める技を持たない編成では何も起きない"),

            new Trait("legacy", "遺志", TraitWhen.AllyDown,
                "味方が倒れると、自分の技の待ちがすべて消える",
                "全体強攻撃・蘇生・挑発。⭐ **重い技をもう一度撃てる**ので、"
                + "崩れてからが本番になる。⚠️ 誰も倒れない編成では死に特性"),

            new Trait("tenacity", "粘り腰", TraitWhen.OnHurt,
                "自分のHPが半分以下の間、受けるダメージが減る",
                "HP割合回復・リジェネ。⭐ 半分より下が「粘る領域」になり、戻しながら受け切る"),
        };

        public static IReadOnlyList<Trait> All => List;

        /// <summary>1つ引く。⭐ **全員が必ず1つ持つ**（属性・得意/不得意と同じ扱い）。
        ///
        /// ⭐ 一部だけが持つ形にしなかったのは、持たない個体が「特性という軸が無い個体」になり、
        /// 厳選の目盛りが1本増えるどころか濁るため。全員が持つなら、どの個体も
        /// 「どの特性と技を噛み合わせるか」という同じ問いの上に乗る。
        ///
        /// ⚠️ 専用の系統（RngTrait）で引くこと。既にある系統に混ぜると列がずれて、
        /// 較正済みの検査が無効になる。</summary>
        public static string Roll(Rng rng) => rng.Pick(List).Id;

        /// <summary>これ未満の★の卵からは特性が出ない。
        ///
        /// ⭐ **理由は強さではなく、覚えることの量。**
        /// 始めたばかりの人に「種族・技3枠・属性・得意/不得意・素質」に加えて特性まで出すと、
        /// まだ何も分かっていないうちに読むものが増えて、そこで離れてしまう。
        /// ⭐ 浅い巣からは低い★しか出ないので、**序盤は自然に特性なし**になる。
        /// 深い巣へ行けるようになった頃 ── つまり他を覚えた頃 ── に初めて出てくる。
        ///
        /// ⚠️ 配合の継承はこの下限を見ない。⭐ 親が持っているのに子が失うほうが分かりにくい。
        /// ⚠️ 「弱いから出さない」ではない。効き目の釣り合いは別途取る話で、ここと混同しない。</summary>
        public const int MinRarity = 3;

        /// <summary>その★の卵に特性が付くか。</summary>
        public static bool AppearsAt(int rarity) => rarity >= MinRarity;

        /// <summary>★に応じて引く。⚠️ 低い★では null（＝持たない）。</summary>
        public static string? RollFor(Rng rng, int rarity) => AppearsAt(rarity) ? Roll(rng) : null;

        public static bool Has(string id)
        {
            foreach (var trait in List)
            {
                if (trait.Id == id) return true;
            }
            return false;
        }

        public static Trait ById(string id)
        {
            foreach (var trait in List)
            {
                if (trait.Id == id) return trait;
            }
            throw new ArgumentException($"特性表に {id} が無い");
        }

        public static void Audit()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            foreach (var trait in List)
            {
                if (!seen.Add(trait.Id)) problems.Add($"特性 id が重複している: {trait.Id}");
                if (trait.Name.Length == 0) problems.Add($"{trait.Id}: 名前が空");
                if (trait.Gist.Length == 0) problems.Add($"{trait.Id}: 説明が空");
                if (trait.Pairs.Length == 0) problems.Add($"{trait.Id}: 何と噛み合うかが空");
            }

            // ⚠️ 繋いでいない特性が増え続けるのを止める。
            //    表だけ長くなって戦闘では何も起きない、が一番気づきにくい
            // ⚠️ 以前ここに `&& List.Length > 8` が付いていた。7件目・8件目は素通りするので、
            //    守りたいと書いてあることを条件自体が打ち消していた
            if (Wired < List.Length)
            {
                problems.Add(
                    $"戦闘に繋がっているのは {Wired} 件だが表には {List.Length} 件ある。" +
                    "繋ぐ前に増やしすぎている");
            }

            // ⚠️ 綴り違いの id で繋ぐと、戦闘では**黙って何も起きない**。
            //    実際に効いているかを目で確かめる術が無いので、ここで突き合わせる
            foreach (var id in WiredIds)
            {
                if (!seen.Contains(id)) problems.Add($"戦闘が {id} を見ているが特性表に無い");
            }
            if (WiredIds.Length != Wired)
            {
                problems.Add($"Wired は {Wired} だが、戦闘が見ている id は {WiredIds.Length} 件");
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    "特性表の不備:" + Environment.NewLine + "  " +
                    string.Join(Environment.NewLine + "  ", problems));
            }
        }
    }
}
