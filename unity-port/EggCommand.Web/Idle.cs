using System.Text;
using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>ホームの放置の帯を、`host` の枠へ描く。
///
/// ⭐ **3つ目の `host`。**⚠️ 何体並ぶかは編成しだいなので、間隔と大きさを逆算する。
///
/// ⭐ 進んでいることは**地面が左へ流れる**ことで見せる（実物）。
/// 🔴 **その動きはまだ web に無い。**⚠️ いまは止まった絵。
/// ⭐ 動きを付けるときは `requestAnimationFrame` 側の話になるので、
/// この枠の中だけで済む ── 骨組みも他の画面も触らない。
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

        // ⭐ 地面。⚠️ 画面幅の2倍あるのは、左へ流して折り返すため（動きは未実装）
        sb.Append(Box("ground", 0, GroundTop, Wide * 2, GroundHigh, "#f2b34b"));
        for (int i = 0; i < 8; i++)
            sb.Append(Box($"tuft#{i}", 90 + 260 * i, GroundTop - 26, 46, 26, "#9ac95e"));

        // ⭐ 編成ぶん並べる。⚠️ **占有する幅は変えない** ── 間隔を詰め、そのぶん縮める
        var party = Games.PartyOf(game);
        int want = Math.Max(1, party.Count);
        const float Span = 130f, First = 120f, Size = 160f;
        float step = Span * 3f / Math.Max(1, want - 1);   // ⚠️ 元は3体ぶんの幅
        float shrink = Math.Min(1f, step / Span);
        for (int i = 0; i < want; i++)
        {
            var c = party[Math.Min(i, party.Count - 1)];
            sb.Append("<div class=\"n\" style=\"left:")
              .Append(Px(First + step * i)).Append(";top:")
              .Append(Px(GroundTop - Size * shrink))
              .Append(";width:160px;height:160px;transform-origin:0 0;transform:scale(")
              .Append(shrink.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
              .Append(")\">")
              .Append(LayoutDom.Render(LayoutStore.Of("walker"), new DomFill
              {
                  Sprite = key => Creatures.SpeciesOf(c).Sprite,
                  Palette = key => Creatures.PaletteOf(c),
              }, "#w" + i))
              .Append("</div>");
        }

        // ⭐ 相手。⚠️ 居ないときは出さない（`Core.Idle` が決める）
        if (game.Idle.EnemyHp > 0)
        {
            var foe = SpeciesTable.All[0];
            sb.Append("<div class=\"n\" style=\"left:880px;top:196px;width:200px;height:200px\">")
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

    private static string Box(string id, float x, float y, float w, float h, string paint) =>
        $"<div id=\"{id}\" class=\"n\" style=\"left:{Px(x)};top:{Px(y)};width:{Px(w)};"
        + $"height:{Px(h)};background:{paint}\"></div>";

    private static string Px(float v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "px";
}
