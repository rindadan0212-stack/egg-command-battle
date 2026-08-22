using System.Text;
using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>すごろくの盤を、`host` の枠へ描く。
///
/// ⭐ **これが2つ目の `host`。**⚠️ マスの位置は（段, 車線）から出すので、
/// 骨組みに座標を書き出せない。線と駒も盤の中身なので、ここがまとめて持つ。
///
/// ⚠️ Unity 版（`TrailScreen.Layout`）と**同じ数**で置く:
/// 縦は段ごとに <see cref="RowStep"/>、横は車線を <see cref="LaneStep"/> で割る。
/// ⭐ 揺らぎは持たない（2026-08-21 に外した ── **段が「1段＝1歩」を運んでいる**ので、
/// 段の揃いを崩すと歩数が目で数えられなくなる）。</summary>
public static class Board
{
    public const float CellW = 248f;
    public const float CellH = 192f;
    public const float RowStep = 218f;
    public const float LaneStep = 396f;
    public const float GoalHeight = 176f;
    /// <summary>盤の幅。⚠️ 画面いっぱい（`Ui.W`）。</summary>
    public const float Wide = 1080f;

    public readonly record struct Spot(float X, float Y);

    /// <summary>マスの置き場。<paramref name="tall"/> は盤ぜんぶの高さ。</summary>
    public static Spot[] Lay(Trail trail, out float tall)
    {
        int deep = trail.Depth;
        // ⚠️ **一番下のマスのうしろまで数える。**⭐ `(段+1)×隔たり` だと
        //    入口のマスが 178 だけはみ出す（実測 2026-08-22）。
        tall = GoalHeight + 28f + deep * RowStep + CellH + 28f;
        float mid = Wide / 2f - CellW / 2f;

        var spots = new Spot[trail.Count];
        for (int i = 0; i < trail.Count; i++)
        {
            var sq = trail.Squares[i];
            spots[i] = new Spot(
                // ⭐ 車線は -3 〜 +3。⚠️ 一番外が画面の端に来るよう割る
                mid + sq.Lane * (LaneStep / Trail.LaneEdge),
                GoalHeight + 28f + (deep - sq.Row) * RowStep);
        }
        return spots;
    }

    /// <summary>盤ぜんぶ。⭐ 線 → マス → 駒 の順に重ねる。</summary>
    public static string Draw(Raid raid)
    {
        var trail = raid.Trail;
        var spots = Lay(trail, out float tall);
        var sb = new StringBuilder();
        // ⚠️ **盤の高さを名乗る器を敷く。**⭐ これが無いと、マスは
        //    枠（host）から縦に溢れて見え、検査が 120件 出す（実測 2026-08-22）。
        sb.Append("<div id=\"ground-in\" class=\"n\" style=\"left:0;top:0;width:")
          .Append(Px(Wide)).Append(";height:").Append(Px(tall)).Append("\">");

        // ⭐ **線が先。**⚠️ 後に描くとマスの上に乗る
        for (int i = 0; i < trail.Count; i++)
            foreach (var way in trail.Squares[i].Ways)
                sb.Append(Link(spots[i], spots[way.To], Behind(raid, i) ? .10 : .42));

        for (int i = 0; i < trail.Count; i++) sb.Append(Cell(raid, i, spots[i]));

        // ⭐ 駒は一番上（いま居る所）
        int at = raid.At;
        if (at >= 0 && at < spots.Length)
        {
            sb.Append("<div id=\"piece\" class=\"n round\" style=\"left:")
              .Append(Px(spots[at].X + CellW / 2f - 26f)).Append(";top:")
              .Append(Px(spots[at].Y - 20f))
              .Append(";width:52px;height:52px;background:#f59e0b\"></div>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>⚠️ **添字で「通り過ぎた」を決めない。**⭐ 添字は（段, 列）の順なので、
    /// 同じ段で自分より左にあるマスまで暗く落ちる（2026-08-21 監査）。</summary>
    private static bool Behind(Raid raid, int i) =>
        raid.Took.ContainsKey(i) || raid.Trail.Squares[i].Row < raid.Trail.Squares[raid.At].Row;

    private static string Cell(Raid raid, int index, Spot at)
    {
        var sq = raid.Trail.Squares[index];
        bool behind = Behind(raid, index);
        var gift = sq.Face;
        bool beaten = raid.Beaten.Contains(index);
        // ⭐ 贈り物は「上がる／下がる」で色が変わる（▲▼ のマスと同じ約束）
        bool up = gift != null && gift.Amount >= 0;
        string ink = behind ? "#636980" : up ? "#1e7a38" : "#c0303f";

        var sb = new StringBuilder();
        sb.Append("<div id=\"sq#").Append(index)
          .Append("\" class=\"n card").Append(behind ? " gone" : "")
          .Append(sq.Kind == SquareKind.Mob && !beaten ? " dark" : "")
          .Append("\" style=\"left:").Append(Px(at.X)).Append(";top:").Append(Px(at.Y))
          .Append(";width:").Append(Px(CellW)).Append(";height:").Append(Px(CellH))
          .Append("\" data-tap=\"square\" data-at=\"").Append(index).Append("\">");

        sb.Append(LayoutDom.Render(LayoutStore.Of("square"), new DomFill
        {
            Text = key => key switch
            {
                "num" => gift != null && gift.Kind == GiftKind.Stat
                    ? $"{(gift.Amount < 0 ? -gift.Amount : gift.Amount)}%" : "",
                // ⚠️ **HP は ×105 して出す。**⭐ 素のまま出すと、HP を要求する関門だけが
                //    手持ちの 1/105 の数に見え、「安い関門」だと誤解する。
                "gnum" => sq.Toll is Toll toll ? Face.Digits(Shown(toll.Kind, toll.Price)) : "",
                _ => "",
            },
            Pic = key => key switch
            {
                "arrow" => "arrow",
                "stat" => gift != null ? IconOf(gift.Stat) : "plain",
                "gstat" => sq.Toll is Toll t ? IconOf(Trails.StatOf(t.Kind)) : "plain",
                _ => null,
            },
            Tint = key => key switch
            {
                "arrow" or "stat" or "num" => ink,
                "gstat" or "gnum" => behind ? "#636980" : "#2b3350",
                "mob" => beaten ? "rgba(255,255,255,.30)" : "#ffffff",
                "plain" => behind ? "rgba(0,0,0,.12)" : "rgba(0,0,0,.26)",
                _ => null,
            },
            // ⭐ 矢印は上下で向きが変わる（▲は上、▼は下）
            When = key => key switch
            {
                "plain" => sq.Kind == SquareKind.Plain,
                "mob" => sq.Kind == SquareKind.Mob,
                "gate" => sq.IsGate,
                "arrow" or "stat" => !sq.IsGate && gift != null && gift.Kind == GiftKind.Stat,
                _ => false,
            },
        }, "#" + index));

        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>マスとマスを繋ぐ線。⚠️ 回すので、骨組みには書けない。</summary>
    private static string Link(Spot a, Spot b, double fade)
    {
        double ax = a.X + CellW / 2, ay = a.Y + CellH / 2;
        double bx = b.X + CellW / 2, by = b.Y + CellH / 2;
        double dx = bx - ax, dy = by - ay;
        double len = Math.Sqrt(dx * dx + dy * dy);
        double turn = Math.Atan2(dy, dx) * 180 / Math.PI;
        return "<div class=\"n road\" id=\"road" + Tag(a, b) + "\" style=\"left:" + Px((float)ax) + ";top:" + Px((float)(ay - 5))
            + ";width:" + Px((float)len) + ";height:10px;transform-origin:0 50%;transform:rotate("
            + turn.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
            + "deg);background:rgba(255,255,255,"
            + fade.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ")\"></div>";
    }

    /// <summary>線に付ける名前。⚠️ DOM の id は一意でなければ指し示せない。</summary>
    private static string Tag(Spot a, Spot b) =>
        $"#{(int)a.X}_{(int)a.Y}_{(int)b.X}_{(int)b.Y}";

    /// <summary>⚠️ 内側の HP は素の値、⭐ 画面に出る HP は ×<see cref="Battle.HpScale"/>。</summary>
    private static int Shown(GimmickKind kind, int price) =>
        Trails.StatOf(kind) == StatKey.Hp ? price * Battle.HpScale : price;

    /// <summary>⚠️ 関門にできるのはこの3つだけ（剣・心・盾）。
    /// ⭐ 他は絵が無い ── 黙って別の絵にしない。</summary>
    public static string IconOf(StatKey key) => key switch
    {
        StatKey.Atk => "stat-atk",
        StatKey.Def => "stat-def",
        StatKey.Hp => "stat-hp",
        _ => "plain",
    };

    private static string Px(float v) =>
        v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "px";
}
