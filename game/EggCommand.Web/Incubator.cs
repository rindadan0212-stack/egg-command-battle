using System.Text;
using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>孵化器の巣を、`host` の枠へ描く。
///
/// ⭐ **これが4つ目の `host`。**（`Board`＝すごろく／`Idle`＝放置の帯／`Sheets` の器に続く）
/// ⚠️ 巣5つは **2-1-2 の菱形**に置いてあるので、`repeat=` の「桁と段」では書けない
/// ── 格子でない置き場所は骨組みに書き出せない、という `Board` と同じ理由でここが持つ。
///
/// ⭐ **数は作者のドット絵「ホーム画面」の実測**（2026-08-27）。1ドット＝4px で写す。
/// ⚠️ 巣1つぶんの中身（巣・たまご・時計）は `slot.txt` が持つ ── ここは
/// **置き場所と、星の数**だけ。⚠️ 中身の座標をここに書かない（出所が2つになる）。
///
/// ⚠️ **名前を `Nests` にしない。**⭐ `Core.Nests`（巣の表）が隠れる
/// （`Layout` フォルダ・`Trial` 頁・`Deeds.Rove` で踏んだのと同じ形）。</summary>
public static class Incubator
{
    /// <summary>巣1つぶんの器。⚠️ `slot.txt` の器と同じ数
    /// （ずれると絵が枠からはみ出す ── あちらが本体、ここは置く側）。</summary>
    public const float CellW = 304f, CellH = 376f;

    /// <summary>5つの巣の左上（`home.txt` の `nests` 枠の中での位置）。
    ///
    /// ⭐ **並びは読む順**（左上 → 右上 → 真ん中 → 左下 → 右下）。
    /// ⚠️ 作者のモックのレイヤー名（巣全体＝左下, ２＝右下, ３＝真ん中, ４＝左上, ５＝右上）は
    /// **描いた順**であって枠の番号ではない ── 0番が左上になるよう読む順に並べ直してある。
    ///
    /// ⚠️ 右上だけモックでは1ドット高かった（他の対は揃っている）ので、左上と揃えた
    /// ── 対になる2つが4pxずれていると、画面では歪みに見える。</summary>
    private static readonly (float X, float Y)[] Spots =
    {
        (64f, 0f),      // 左上
        (700f, 0f),     // 右上
        (388f, 252f),   // 真ん中
        (64f, 504f),    // 左下
        (700f, 504f),   // 右下
    };

    /// <summary>星1つの大きさと隔たり（モックの実測: 20x19ドット・14ドット刻み）。
    /// ⚠️ 隔たり(56)が幅(80)より狭い ── ⭐ 星は少し重なって並ぶ（作者の絵のとおり）。</summary>
    private const float StarW = 80f, StarH = 76f, StarStep = 56f, StarY = 212f;

    /// <summary>巣5つぶん。⭐ 1つずつ `slot.txt` を描いて、置き場所の器で包む。</summary>
    public static string Draw(Shell s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Hatchery.Slots && i < Spots.Length; i++)
        {
            var egg = Hatchery.At(s.Game, i);
            var at = Spots[i];

            // ⭐ **押しどころは器のほう。**⚠️ 中の絵（巣・たまご）に付けると、
            //    絵の透けている所（巣の左右の余白）で指が抜ける。
            sb.Append("<div id=\"nest#").Append(i)
              .Append("\" class=\"n\" style=\"left:").Append(Px(at.X))
              .Append(";top:").Append(Px(at.Y))
              .Append(";width:").Append(Px(CellW)).Append(";height:").Append(Px(CellH))
              .Append("\" data-tap=\"slot\" data-at=\"").Append(i).Append("\">");

            sb.Append(LayoutDom.Render(LayoutStore.Of("slot"), new DomFill
            {
                Text = key => key switch
                {
                    // ⭐ 孵ったら「孵った」と出す。⚠️ 残り時間だけだと、
                    //    取り出せるようになったことに気づけない。
                    "clock" => egg == null ? ""
                        : Hatchery.IsReady(egg, s.Now) ? "孵った" : Rarities.Clock(Hatchery.LeftOf(egg, s.Now)),
                    _ => "",
                },
                // ⭐ **たまごの柄は種族ごと。**⚠️ ここが名前を返さないと `slot.txt` の
                //    `pic=nest-egg`（原画）へ落ちる ── 黙って「？」にはならない。
                Pic = key => key == "egg" && egg != null
                    ? EggSkins.NameOf(egg.Egg.SpeciesId) : null,
                Tint = key => key switch
                {
                    "clock" => egg != null && Hatchery.IsReady(egg, s.Now) ? "#8ef06a" : "#ffffff",
                    _ => null,
                },
                When = key => key switch
                {
                    "full" => egg != null,
                    _ => false,
                },
                Tappable = key => true,
            }, "#" + i));

            // ⭐ **星はレア度の数だけ。**⚠️ 点けたり消したりではなく**出す数を変える**
            //    （作者の指示 2026-08-27）── 中央揃えなので、奇数と偶数で左端が変わる。
            if (egg != null) Stars(sb, Rarities.Clamp(egg.Egg.Rarity), i);

            sb.Append("</div>");
        }
        return sb.ToString();
    }

    /// <summary>★を <paramref name="many"/> 個、器の真ん中へ並べる。
    ///
    /// ⭐ **中央揃えなので左端が数で動く**（★1は112、★5は0）。⚠️ 位置が実行時に決まるので
    /// 骨組み（`slot.txt`）には書けない ── だからここが描く。
    /// ⚠️ どの数でも左端は整数（隔たり56の半分が28で割り切れる）── 半端な位置に来ない。</summary>
    private static void Stars(StringBuilder sb, int many, int slot)
    {
        float wide = StarW + (many - 1) * StarStep;
        float left = (CellW - wide) / 2f;
        for (int i = 0; i < many; i++)
        {
            sb.Append("<img id=\"star#").Append(slot).Append('_').Append(i)
              .Append("\" class=\"n paint\" src=\"paint/nest-star.png\" alt=\"\" style=\"left:")
              .Append(Px(left + i * StarStep)).Append(";top:").Append(Px(StarY))
              .Append(";width:").Append(Px(StarW)).Append(";height:").Append(Px(StarH))
              .Append("\" />");
        }
    }

    private static string Px(float v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "px";
}
