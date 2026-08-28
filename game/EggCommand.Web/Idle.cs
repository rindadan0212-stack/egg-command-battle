using System.Text;
using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>ホームの放置の帯を、`host` の枠へ描く。
///
/// ⭐ **3つ目の `host`。**⚠️ 何体並ぶかは編成しだいなので、間隔と大きさを逆算する。
///
/// ⭐ 進んでいることは**地面が左へ流れる**ことで見せる。走者は歩幅だけ揺らす。
/// ⚠️ 動きは `stage.css` が持つ（時計を1本増やさずに済む）── ⭐ ここは級を付けるだけ。
/// ⚠️ 数（流れる速さ・揺れ幅）は `Core.Beats` が唯一の出所。
///
/// ⚠️ ここは `Core.Idle` が決めた結果を描くだけ。勝ち負けも素材もここでは決めない
/// （決めた瞬間に第2の出所ができる）。</summary>
public static class Idle
{
    /// <summary>枠の大きさ（`home.txt` の `idle`）。</summary>
    public const float Wide = 1080f, High = 470f;
    /// <summary>地面の高さと、その上端。⚠️ 実物（`BuildScreenPrefabs`）の数。</summary>
    public const float GroundTop = 396f, GroundHigh = 40f;

    public static string Draw(Game game)
    {
        var sb = new StringBuilder();

        // ⭐ 地面。⚠️ 画面幅の2倍あるのは、左へ流して**折り返す**ため
        //    （1枚ぶん流れたら元へ戻るので、繋ぎ目が見えない）
        // 🔴 **名前は `ground` にしない。**⚠️ すごろくの盤の器がその名前を使っている
        //    （`assets/layouts/trail.txt`）── 画面が違えば別物、ではない。
        //    id は画面をまたいでも唯一で、`stage.css` の `#ground` が**両方**に当たり、
        //    潜入の盤が左へ流れ続けていた（実測 2026-08-26）。
        //    ⭐ 動きは級（`idle-flow`）で掛ける ── 草（`idle-tuft`）と同じやり方。
        sb.Append(Box("idleground", 0, GroundTop, Wide * 2, GroundHigh, "#f2b34b",
            "idle-flow"));
        // ⚠️ 草も一緒に流す ── ⭐ 地面だけ動くと、生えている物が滑って見える
        for (int i = 0; i < 16; i++)
            sb.Append(Box($"tuft#{i}", 90 + 260 * i, GroundTop - 26, 46, 26, "#9ac95e",
                "idle-tuft"));

        // ⭐ 編成ぶん並べる。⚠️ **占有する幅は変えない** ── 間隔を詰め、そのぶん縮める
        var party = Games.PartyOf(game);
        int want = Math.Max(1, party.Count);
        const float Span = 130f, First = 120f, Size = 160f;
        // ⭐ 揺れ幅は `Core.Beats` が唯一の出所（動きは `stage.css` が同じ数で書く）
        const float Bob = (float)Beats.Bob;
        float step = Span * 3f / Math.Max(1, want - 1);   // ⚠️ 元は3体ぶんの幅
        float shrink = Math.Min(1f, step / Span);
        for (int i = 0; i < want; i++)
        {
            var c = party[Math.Min(i, party.Count - 1)];
            // ⚠️ **揺れは中の器に掛ける。**⭐ 外は縮めるための `scale` を持っているので、
            //    ここへ動きを足すと `transform` が丸ごと置き換わって縮みが消える。
            // ⚠️ 🔴 **揺れるぶんの天井を空けておく。**⭐ 器の高さを揺れ幅だけ足し、
            //    中の絵をそのぶん下げる ── ⚠️ 空けないと、上がった拍に
            //    絵が器の外へ出て、検査が「親の枠からはみ出し」と読む（実測 2026-08-23）。
            sb.Append("<div class=\"n\" style=\"left:")
              .Append(Px(First + step * i)).Append(";top:")
              .Append(Px(GroundTop - (Size + Bob) * shrink))
              .Append(";width:160px;height:").Append(Px(Size + Bob))
              .Append(";transform-origin:0 0;transform:scale(")
              .Append(shrink.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
              .Append(")\"><div class=\"n idle-walk\" style=\"left:0;top:").Append(Px(Bob))
              .Append(";width:160px;")
              // ⚠️ 一人ずつずらす ── ⭐ 揃うと行進になり、めいめいが歩いている感じが消える
              .Append("height:160px;animation-delay:")
              .Append((i * 0.21).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
              .Append("s\">")
              .Append(LayoutDom.Render(LayoutStore.Of("walker"), new DomFill
              {
                  Sprite = key => Creatures.SpeciesOf(c).Sprite,
                  Palette = key => Creatures.PaletteOf(c),
              }, "#w" + i))
              .Append("</div></div>");
        }

        // ⭐ 相手。⚠️ 居ないときは出さない（`Core.Idle` が決める）
        if (game.Idle.EnemyHp > 0)
        {
            var foe = SpeciesTable.All[0];
            // ⭐ **外から転がって来る**（⚠️ 定位置にぽんと現れると「回復した」に見える）。
            //    ⚠️ 一度きりの動きなので、満タンのときだけ掛ける。
            bool fresh = game.Idle.EnemyHp >= 1;
            sb.Append("<div class=\"n").Append(fresh ? " idle-come" : "")
              .Append("\" style=\"left:880px;top:196px;width:200px;height:200px\">")
              .Append(LayoutDom.Render(LayoutStore.Of("walker"), new DomFill
              {
                  Sprite = key => foe.Sprite,
                  Palette = key => foe.Palettes[0],
              }, "#foe"))
              .Append("</div>");
            // ⭐ 残りの体力。⚠️ 数は出さない（帯だけで足りる）
            sb.Append(Box("hptrack", 740, 176, 280, 18, "rgba(0,0,0,.18)"));
            double left = Math.Clamp(game.Idle.EnemyHp, 0, 1);
            sb.Append(Box("hpfill", 740, 176, (float)(280 * left), 18, "#e04f5f"));
        }
        return sb.ToString();
    }

    private static string Box(string id, float x, float y, float w, float h, string paint,
        string also = "") =>
        $"<div id=\"{id}\" class=\"n{(also.Length > 0 ? " " + also : "")}\""
        + $" style=\"left:{Px(x)};top:{Px(y)};width:{Px(w)};"
        + $"height:{Px(h)};background:{paint}\"></div>";

    private static string Px(float v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "px";
}
