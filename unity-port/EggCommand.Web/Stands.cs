using System.Text;
using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>戦闘に立つ体を、`host` の枠へ並べる。
///
/// ⭐ **これが `host` の中身。**⚠️ 骨組みが持つのは枠だけで、
/// **何体を、どれだけの間隔で、どれだけの大きさで置くか**は体数から逆算する
/// ── だから座標を書き出せない（計画が `Fit` の式は骨組みに書かないと決めた所）。
///
/// ⚠️ Unity 版（`BattleView.Lay`）と**同じ形**で数える:
/// 器の詰まり具合（高さ ÷ 間隔）を保ったまま、収まる最大の間隔を選び、
/// ⭐ **元より大きくはしない**。</summary>
public static class Stands
{
    /// <summary>1体ぶんの器（`unit.txt` の大きさ）。</summary>
    public const float High = 350f;
    /// <summary>詰めないときの間隔。⚠️ 高さより少し広い（触れる隙間を残す）。</summary>
    public const float Step = 380f;

    /// <summary>並べたときの、i 番目の上端と縮め率。</summary>
    public readonly record struct Spot(float Left, float Top, float Shrink);

    /// <summary>⚠️ **元の並びをそのまま詰めない。**⭐ 使える高さから、
    /// 詰まらない最大の大きさを逆算する（Unity 版で実測して直した形）。</summary>
    public static Spot[] Lay(int want, float wide, float room)
    {
        var spots = new Spot[want <= 0 ? 0 : want];
        if (spots.Length == 0) return spots;

        // ⭐ 器の詰まり具合は元のまま保つ
        double density = High / Step;
        double step = Math.Min(Step, room / ((want - 1) + density));
        double shrink = step / Step;
        double drawn = High * shrink;
        // ⭐ 横は器の真ん中（⚠️ 縮めると左上に張り付いたまま残るので、自分で寄せる）
        double left = wide / 2 - (340 * shrink) / 2;

        for (int i = 0; i < want; i++)
            spots[i] = new Spot((float)left, (float)(step * i), (float)shrink);
        return spots;
    }

    /// <summary>1体を `unit.txt` で描いて、計算した場所へ置く。
    ///
    /// ⚠️ **縮めるのは `transform` で。**⭐ 中の数（`unit.txt`）は設計のまま置いておく
    /// ── 縮めた数を書き込むと、骨組みと実物がずれる。</summary>
    /// <param name="at">この体を指す名前（`a0` `f2` など）。⚠️ **id を分けるのに要る。**
    /// ⭐ **側も入れる** ── 番号だけだと、味方の1体目と敵の1体目が同じ名前になる
    /// （実測 2026-08-22: 番号を足しても 6件 残った）。</param>
    public static string One(Spot spot, string at, DomFill fill)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"n\" style=\"left:").Append(Px(spot.Left))
          .Append(";top:").Append(Px(spot.Top))
          .Append(";width:340px;height:350px;transform-origin:0 0;transform:scale(")
          .Append(spot.Shrink.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture))
          .Append(")\">")
          .Append(LayoutDom.Render(LayoutStore.Of("unit"), fill, "#" + at))
          .Append("</div>");
        return sb.ToString();
    }

    private static string Px(float v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "px";
}
