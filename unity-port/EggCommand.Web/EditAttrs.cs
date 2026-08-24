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
    /// 🔴 **段階1（2026-08-24・作者の判断）は「飾りだけ」開ける。**`when`/`bind`/`tap`/
    /// `hold`/`repeat` のような**遊びの意味が変わる**付け足しは開けない
    /// （<see cref="Excluded"/> に理由つきで記録 ── 「意図して外した」を「書き忘れた」と
    /// 区別するため）。</summary>
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
        // ⚠️ すべて `LayoutDom.cs`（`unity-port/EggCommand.Web/LayoutDom.cs`）が実際に
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
            new Attr("size", "字の大きさ", AttrKind.Number, LabelOrButton, min: 8f, max: 140f),
            new Attr("text", "字そのもの", AttrKind.Text, LabelOrButton),
            new Attr("anchor", "寄せ", AttrKind.Choice, LabelOnly,
                // ⭐ 実物の骨組み（unity/Assets/Resources/Layouts/*.txt）を grep して導いた6値
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
            new Attr("turn", "回す角度", AttrKind.Number, NotLabel, min: -360f, max: 360f),
            // ⚠️ ①②③（2026-08-24・§11-2）で既に実装済み。挙動は変えない ──
            //    表に載せて Inspector の自動生成に乗せ替えるだけ（専用の Field 呼び出しを畳む）。
            new Attr("gap", "隙間", AttrKind.Number, HasRepeat, min: 0f, max: 500f),
        };

        /// <summary>⭐ **意図して外した付け足し**（`Core.Layouts.Options` の残り12個）。
        /// ⚠️ `EditAttrsTests` の「ずれない検査」は、`Layouts.Options` の22個すべてが
        /// <see cref="All"/> と、この一覧の**どちらか一方**に載っていることを固定する
        /// （両方・どちらでもない、は落とす）。</summary>
        public static readonly IReadOnlyDictionary<string, string> Excluded = new Dictionary<string, string>
        {
            ["bind"] = "遊びの意味が変わる（値の差し込み口）── 段階1は飾りだけ（作者の判断）",
            ["tap"] = "遊びの意味が変わる（押しどころの名前）── 段階1は飾りだけ",
            ["hold"] = "遊びの意味が変わる（長押しで開く札の名前）── 段階1は飾りだけ",
            ["repeat"] = "遊びの意味が変わる（繰り返す元データの名前）── 段階1は飾りだけ",
            ["cols"] = "遊びの意味が変わる（繰り返しの列数）── 段階1は飾りだけ",
            ["rows"] = "遊びの意味が変わる（繰り返しの段の高さ）── 段階1は飾りだけ",
            ["max"] = "遊びの意味が変わる（繰り返しの上限）── 段階1は飾りだけ",
            ["when"] = "遊びの意味が変わる（出す／出さないの条件）── 段階1は飾りだけ",
            ["use"] = "遊びの意味が変わる（部品を差す先）── 段階1は飾りだけ",
            ["flow"] = "遊びの意味が変わる（詰める並び方）── 段階1は飾りだけ",
            ["dock"] = "遊びの意味が変わる（下の帯を跨ぐか）── 段階1は飾りだけ",
            // ⚠️ ここだけ「遊びの意味」でなく「実装の手間」で見送った ── 報告義務どおり明記する。
            ["pic"] = "載せなかった: IconManifest（`EggCommand.Web` 専用の埋め込みリソース）に"
                + "一覧が依存する。この属性表は EggCommand.Tests へ ProjectReference を張らず"
                + "直接コンパイル（dotnet test が Web を建てない約束を守るため）しているので、"
                + "IconManifest に依存すると Tests 側では一覧が空になり表が嘘をつく。"
                + "Core 語彙だけで組む単純さを優先して段階1では見送った"
                + "（絵の名前を打ち間違えても icon-missing の「？」印で気づける道は既にある）",
        };

        public static Attr? For(string key)
        {
            foreach (var a in All) if (a.Key == key) return a;
            return null;
        }
    }
}
