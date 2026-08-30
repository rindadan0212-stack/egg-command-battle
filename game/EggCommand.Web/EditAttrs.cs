using System;
using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Web
{
    /// <summary>骨組みエディタの「属性表」── 付け足し（<see cref="Layouts.Options"/>）を
    /// どう編集させるかの唯一の出所（`wiki/開発/web移行計画.md` §11-3・作者承認の設計）。
    ///
    /// ⭐ `Core.Art` / `Web.Scenes` / `Core.Beats` と同じ作法 ── あちこちに書き散らさず
    /// 1つの表に閉じ込める。編集できる属性を増やすときは、この <see cref="EditAttrs.All"/>
    /// に1行足すだけでよい（`EditPage.razor` の Inspector はこの表を舐めて自動で欄を生む。
    /// `ApplyLeft`/`ApplyTop` のような専用メソッドを増やさない）。
    ///
    /// ⚠️ **Core に置かない**（作者の判断）── 座標（<see cref="LayoutNode.Left"/> 等）は
    /// 「遊びの規則」なので Core が持つが、「日本語ラベル」「欄の型」「効く種類」は
    /// **編集の都合**でしかない。Core に混ぜると、遊びの規則を変えるつもりが無いのに
    /// エディタの都合で Core を触ることになる。
    ///
    /// 🔴 **段階1（2026-08-24・作者の判断）は「飾りだけ」開けた。**その後、専用の欄を持つ
    /// ものが増えた（`pic`・`tap`・そして 2026-08-29 の P4 で `when`・`hold`）ので、
    /// <see cref="Excluded"/> は「編集できない一覧」ではなく
    /// **「この静的な表には収まらない一覧」**になった ── 三分類（A: 専用の欄がある／
    /// B: コードとの契約なので見せるだけ／C: 周りの意味まで書き換えるので字で直す）は
    /// <see cref="Excluded"/> と <see cref="Chips"/> の註にある。
    ///
    /// ⚠️ どれも「意図して外した」を「書き忘れた」と区別するために理由つきで記録する
    /// （`EditAttrsTests` が、理由の空欄と、表・除外一覧の食い違いを落とす）。</summary>
    public enum AttrKind
    {
        /// <summary>数。⭐ 「きざみ」ぶんの ± 釦付き（<c>EditPage.Field</c> が描く）。</summary>
        Number,
        /// <summary>切替。⚠️ OFF は付け足しごと消す（`ink=` のように値だけ空にして残さない
        /// ── `Layouts.Write` は「Options に無い」で欄そのものを畳む設計）。</summary>
        Toggle,
        /// <summary>決まった値からしか選べない。⚠️ 値は実物（骨組み／`stage.css`）から導く
        /// ── 決め打ちで書かない（各 <see cref="Attr"/> の宣言コメント参照）。</summary>
        Choice,
        /// <summary>動かない字そのもの。⚠️ `\n` は改行として打てる（<c>Layouts.TextMark</c>
        /// の規約どおり）。</summary>
        Text,
    }

    /// <summary>属性表の1行。</summary>
    public sealed class Attr
    {
        /// <summary>骨組みの `key=`（<see cref="Layouts.Options"/> と同じ綴り）。</summary>
        public readonly string Key;
        /// <summary>⭐ 日本語ラベル（作者の Unity 版 `Assets/Editor/EggCommandWindow.cs`
        /// に倣う ── 「字の大きさ」「字の色」と同じ言葉づかい）。</summary>
        public readonly string Label;
        public readonly AttrKind Kind;
        /// <summary><see cref="AttrKind.Choice"/> のときの値。⚠️ <see cref="AttrKind.Choice"/>
        /// 以外では null。</summary>
        public readonly IReadOnlyList<string>? Choices;
        /// <summary>⭐ **効く種類だけ欄を出す**（`LayoutDom.cs` が実際にその key を読んでいる
        /// 条件と1対1 ── 当てずっぽうで書かない。個々の <see cref="Attr"/> 宣言のコメント
        /// に、対応する `LayoutDom.cs` の場所を書いてある）。</summary>
        public readonly Func<LayoutNode, bool> AppliesTo;
        /// <summary><see cref="AttrKind.Number"/> の安全な範囲。⚠️ 他の型では使わない
        /// （既定は無制限）。</summary>
        public readonly float Min, Max;

        public Attr(string key, string label, AttrKind kind, Func<LayoutNode, bool> appliesTo,
            IReadOnlyList<string>? choices = null,
            float min = float.NegativeInfinity, float max = float.PositiveInfinity)
        {
            Key = key;
            Label = label;
            Kind = kind;
            AppliesTo = appliesTo;
            Choices = choices;
            Min = min;
            Max = max;
        }
    }

    public static class EditAttrs
    {
        // ── 「効く種類」の判定 ──────────────────────────
        // ⚠️ すべて `LayoutDom.cs`（`game/EggCommand.Web/LayoutDom.cs`）が実際に
        //    その key を読んでいる分岐そのまま。憶測で書かない。

        /// <summary>`size`/`text`: LayoutDom.cs の font-size 分岐・字を出す分岐は、
        /// どちらも `node.Kind == "label" || node.Kind == "button"` の1行そのもの。</summary>
        private static bool LabelOrButton(LayoutNode n) => n.Kind == "label" || n.Kind == "button";

        /// <summary>`anchor`/`wrap`: `if (node.Kind == "label") { ... a-寄せ ... wrapped ... }`
        /// の中だけで読まれる（label 以外は素通り）。</summary>
        private static bool LabelOnly(LayoutNode n) => n.Kind == "label";

        /// <summary>`ink`/`lead`: LayoutDom.cs は `ink=` を if（label）／else（それ以外）の
        /// **両方の分岐**で読む。`lead=` はその if/else の外（分岐に関係なく毎回）で読む。
        /// ⚠️ だから種類を問わず全部に効く（実装がそうなっている・使い方の是非は問わない）。</summary>
        private static bool AnyKind(LayoutNode n) => true;

        /// <summary>`foe`: `LayoutDom.Dots` の中でしか読まれず、`Dots` は
        /// `node.Kind == "pixel"` のときしか呼ばれない。</summary>
        private static bool PixelOnly(LayoutNode n) => n.Kind == "pixel";

        /// <summary>`crisp`: `if (node.Kind == "icon" && node.Option("crisp") == "yes")`
        /// の1行そのもの。</summary>
        private static bool IconOnly(LayoutNode n) => n.Kind == "icon";
        private static bool PaintOnly(LayoutNode n) => n.Kind == "paint";

        /// <summary>`turn`: `ink`/`lead` と同じ else 分岐（label 以外）で
        /// `transform:rotate(...)` に使われる。⚠️ 元は矢印（icon）の ±90 用に足された
        /// 付け足しだが、実装は label 以外なら種類を問わず読む。</summary>
        private static bool NotLabel(LayoutNode n) => n.Kind != "label";

        /// <summary>`gap`: 種類でなく「`repeat=` を持つか」で出す（段階1より前からの
        /// 既存の条件・`EditPage.razor` の `sel.Option("repeat") != null` をそのまま踏襲）。</summary>
        private static bool HasRepeat(LayoutNode n) => n.Option("repeat") != null;

        /// <summary>⭐ 段階1で開ける9つ＋既存の `gap`（挙動を変えず表に載せるだけ）。
        /// ⚠️ 並び順がそのまま Inspector の表示順 ── 作者の Unity 版
        /// 「字の大きさ・字の色」の並びに寄せた（字 → 見た目 → 切替 → 数、の順）。</summary>
        public static readonly IReadOnlyList<Attr> All = new[]
        {
            // ⭐ E1-3（2026-08-25・ドット絵化計画 §6）: 自由入力の数からやめて、段だけ選ばせる。
            //    PixelMplus10 は10ドット角 ── 1ドット=4pxに揃えるには、フォントも「1文字
            //    ドット=4px」で出す必要があり、40px が唯一「絵と同じドットの太さ」になる
            //    大きさ。80/120 はドットが太くなる例外（演出・大きい数字だけで使う）。
            new Attr("size", "字の大きさ", AttrKind.Choice, LabelOrButton,
                choices: new[] { "40", "80", "120" }),
            new Attr("text", "字そのもの", AttrKind.Text, LabelOrButton),
            new Attr("anchor", "寄せ", AttrKind.Choice, LabelOnly,
                // ⭐ 実物の骨組み（assets/layouts/*.txt）を grep して導いた6値
                //    （2026-08-24 実測）。決め打ちで書いていない。
                choices: new[] { "left", "center", "right", "upper-left", "upper-center", "upper-right" }),
            new Attr("ink", "色", AttrKind.Choice, AnyKind,
                // ⭐ `stage.css` の `.ink-*` から導いた6値（2026-08-24 実測）。
                //    ⚠️ Unity 側（`Ui.cs` の InkDim/InkFaint/AccentInk/DangerInk/GoodInk/OnLead）
                //    と6つとも名前が対応していることを確認済み（食い違いなし・報告参照）。
                choices: new[] { "dim", "faint", "accent", "danger", "good", "on-lead" }),
            new Attr("wrap", "折り返す", AttrKind.Toggle, LabelOnly),
            new Attr("lead", "主役にする", AttrKind.Toggle, AnyKind),
            new Attr("foe", "左右反転", AttrKind.Toggle, PixelOnly),
            new Attr("crisp", "縁をにじませない", AttrKind.Toggle, IconOnly),
            new Attr("natural", "絵の色をそのまま使う", AttrKind.Toggle, IconOnly),
            new Attr("fit", "枠に収める", AttrKind.Toggle, PaintOnly),
            new Attr("turn", "回す角度", AttrKind.Number, NotLabel, min: -360f, max: 360f),
            // ⚠️ ①②③（2026-08-24・§11-2）で既に実装済み。挙動は変えない ──
            //    表に載せて Inspector の自動生成に乗せ替えるだけ（専用の Field 呼び出しを畳む）。
            new Attr("gap", "隙間", AttrKind.Number, HasRepeat, min: 0f, max: 500f),
            // ⭐ P4（2026-08-29・案7の三分類 A）: **繰り返しの「形」だけ**を開ける。
            //    ⚠️ `repeat=`（どのデータを繰り返すか＝コードとの契約）は開けない
            //    （<see cref="Chips"/> の B ── 見せるだけ）。ここで開けるのは列数・段の高さ・
            //    上限という**純粋な幾何**で、綴りを変えてもコード側とは食い違わない。
            //    ⭐ 開ける値打ち: この3つは盤の上で不備（`InvalidCols`/`ColsOverflow`/
            //    `RepeatMaxOverflow`）が即座に効くので、「直した結果」がその場で見える。
            //    ⚠️ 最小1（`cols`/`max`）は `InvalidCols` を**そもそも作れなくする**ため。
            new Attr("cols", "繰り返しの列数", AttrKind.Number, HasRepeat, min: 1f, max: 64f),
            new Attr("rows", "繰り返しの段の高さ", AttrKind.Number, HasRepeat, min: 0f, max: 1920f),
            // ⚠️ 最小は **0**（2026-08-29 監査 B-3）。⭐ `max=` は「書いていない」が正しい状態でもあり
            //    （巻物の中なら上限は要らない ── 外なら `RepeatMissingMax` の不備が別に言う）、
            //    読む側の既定も 0（`Layouts.DeepOf` の `Number("max", 0)`）。最小を 1 にすると
            //    欄が 0 を見せているのに「−」で 1 に**増え**、無かった `max=` が書かれてしまう。
            //    🔴 0 を打つと付け足しごと消える（`EditPage.AttrField` がそう書いている）。
            new Attr("max", "繰り返しの上限（0で消す）", AttrKind.Number, HasRepeat, min: 0f, max: 999f),
            // ⭐ 2026-08-29: **絵を枠より大きく描いて枠で切る**（BOX一覧の升・作者の指示
            //    「イラストの一部だけを表示し意図的に見切れさせる」）。⚠️ `pixel` だけ。
            //    ⭐ 開ける値打ち: 「どれくらい寄るか」は目で決める数なので、盤を見ながら
            //    上げ下げできるのがいちばん早い。🔴 0 を打つと付け足しごと消える（＝切らない）。
            //    ⚠️ どこを見せるかは種族ごとに違うので骨組みでは持てない（`SpeciesArt`）。
            new Attr("crop", "枠で切る（絵を描く大きさ・0で切らない）", AttrKind.Number, PixelOnly,
                min: 0f, max: 1920f),
        };

        /// <summary>⭐ P4（2026-08-29・案7の三分類 B・C）: **編集させないが、付いていることは
        /// 見せる**付け足しと、その一言。⚠️ Inspector に灰色の札で常時出る ──
        /// 「付いていること」が見えないと**盤の挙動が謎に見える**（なぜ複製が並ぶのか・
        /// なぜ「上」を変えても動かないのか、が骨組みの字を読まないと分からない）。
        ///
        /// 🔴 **表示だけ。**ここに足しても編集の道は生えない（編集の唯一の出所は
        /// <see cref="All"/>）。
        ///
        /// 🔴 **B（`bind`/`repeat`/`use`）を編集させない理由**: いずれも**コードが読む名前**
        /// で、綴りを変えても骨組みの検査は何も言わない ── 遊びだけが黙って壊れる
        /// （`bind=art` を `bind=arts` にしても不備は0件のまま、絵が出なくなるだけ）。
        ///
        /// 🔴 **C（`flow`/`dock`/`roll`/`grow`）を編集させない理由**: 1つの付け足しが
        /// **周りの意味まで書き換える**（`flow` は兄弟全部の「上」の意味を変える／
        /// `roll` は `stage.css` の級と結線していて値を変えると動きが止まる／
        /// `dock`・`grow` は帯と `host` 専用の脱出弁で、外すと盤が見えなくなる方向に壊れる）。
        /// 頻度も低いので、字で直すほうが安全 ── 理由は <see cref="Excluded"/> に詳しい。</summary>
        public static readonly IReadOnlyDictionary<string, string> Chips = new Dictionary<string, string>
        {
            // ── B: コードとの契約（見せるだけ）──────────────
            ["bind"] = "コードが値を差す口",
            ["repeat"] = "繰り返す元のデータ",
            ["use"] = "差している部品",
            // ── C: 周りの意味まで書き換える（字で直す）────────
            ["flow"] = "兄弟を上から詰める（「上」＝上の隙間の意味になる）",
            ["dock"] = "下の帯を跨いでよい",
            ["roll"] = "背景を流す級（速さの出所は stage.css）",
            ["grow"] = "器の高さで中身を切らない",
        };

        /// <summary>⭐ **自動生成の表（<see cref="All"/>）に載せなかった付け足し**と、その理由。
        ///
        /// 🔴 **「編集できない」ではない**（2026-08-29・P4 で意味が変わった）── `tap`/`pic`/
        /// `when`/`hold` は Inspector に**専用の欄**を持つ。ここに居るのは、値の候補が
        /// 実物の骨組みや埋め込みリソースから決まり、**静的な表に収まらない**から。
        ///
        /// ⚠️ `EditAttrsTests` の「ずれない検査」は、`Layouts.Options` の22個すべてが
        /// <see cref="All"/> と、この一覧の**どちらか一方**に載っていることを固定する
        /// （両方・どちらでもない、は落とす）。
        ///
        /// ⭐ **三分類**（案7・2026-08-29）:
        /// **A**＝専用の欄で触れる（`tap`/`pic`/`when`/`hold`）／
        /// **B**＝コードとの契約なので見せるだけ（`bind`/`repeat`/`use`）／
        /// **C**＝周りの意味まで書き換えるので字で直す（`flow`/`dock`/`roll`/`grow`）。
        /// B・C は <see cref="Chips"/> に一言を持ち、Inspector に灰色の札で常時出る。</summary>
        public static readonly IReadOnlyDictionary<string, string> Excluded = new Dictionary<string, string>
        {
            // ── A: 専用の欄がある（表に収まらないだけで、編集はできる）────────
            ["tap"] = "A（専用の欄あり）: 押しどころの名前。候補は `TapCatalog.Names` と"
                + "「冠の逆算」（部品がどんな名前で差されているか）から決まるので、静的な"
                + "選択肢の表には収まらない ── Inspector の「機能を選ぶ」小窓が持つ（E2）",
            ["hold"] = "A（専用の欄あり）: 長押しで開く札の名前。`tap` と同じ理由で"
                + "小窓が持つ（候補は `HoldCatalog.Names` ＋冠の逆算・2026-08-29 P4）",
            ["when"] = "A（専用の欄あり）: 出す／出さないの条件。⭐ 候補は**実物の骨組みから**"
                + "集める（この骨組みとその土台で実際に使われている名前 ── 決め打ちの一覧を"
                + "持たない、の作法）ので静的な表に収まらない。`!`（偽のとき出す）を別の切替に"
                + "分ける必要もある ── Inspector の専用の欄が持つ（2026-08-29 P4）",
            ["pic"] = "A（専用の欄あり）: 絵の名前。一覧が IconManifest（`EggCommand.Web` 専用の"
                + "埋め込みリソース）に依存する。この属性表は EggCommand.Tests へ"
                + " ProjectReference を張らず直接コンパイル（dotnet test が Web を建てない約束を"
                + "守るため）しているので、IconManifest に依存すると Tests 側では一覧が空になり"
                + "表が嘘をつく ── Inspector の「絵を変える」小窓が持つ（E-4/E1-4）",

            // ── B: コードとの契約（見せるだけ ── Chips に一言）────────────
            ["bind"] = "B（見せるだけ）: 値の差し込み口。🔴 コードが読む名前なので、綴りを"
                + "変えても骨組みの検査は何も言わない ── 不備0件のまま遊びだけが黙って壊れる",
            ["repeat"] = "B（見せるだけ）: 繰り返す元データの名前。`bind` と同じくコードとの"
                + "契約。⭐ 繰り返しの**形**（`cols`/`rows`/`max`）は純粋な幾何なので"
                + " 2026-08-29 に表へ開けた ── 契約と形を分けた",
            ["use"] = "B（見せるだけ）: 部品を差す先。差し替えると中身が丸ごと入れ替わり、"
                + "冠（`Layouts.Rename`）も付け直しになる ── 綴りで壊す型",

            // ── C: 周りの意味まで書き換える（字で直す ── Chips に一言）────────
            ["flow"] = "C（字で直す）: 詰める並び方。🔴 1つ付けると**兄弟全部の「上」の意味**が"
                + "変わる（座標→上の隙間）ので、盤の上で1つだけ触ると他が総崩れに見える",
            ["dock"] = "C（字で直す）: 下の帯を跨いでよい。帯そのもの（`frame.txt`）と、"
                + "背景のように帯の裏まで敷くものだけの脱出弁 ── 外すと不備が出る側へ倒れる",
            ["roll"] = "C（字で直す）: 背景を流す級の名前。絵は「元・鏡・元・鏡」の4枚幅で"
                + "作ってあり、値を変えると `stage.css` の級と対応が切れて**動きが止まる**。"
                + "⭐ 速さの出所は `stage.css` ひとつ（2026-08-27）",
            ["grow"] = "C（字で直す）: 巻物の中の `host` を切らない。触ると盤が見えなくなる／"
                + "押せなくなる方向に壊れうる（2026-08-26・すごろくの盤で実際に起きた不具合の直し）",
        };

        public static Attr? For(string key)
        {
            foreach (var a in All) if (a.Key == key) return a;
            return null;
        }
    }
}
