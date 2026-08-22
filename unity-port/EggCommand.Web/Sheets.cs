using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>画面1枚ぶんの「どの値をどの差し口へ流すか」。
///
/// ⭐ **確かめ用の頁も、遊ぶ頁も、ここを通る。**⚠️ 分けて書くと、片方だけ直した日に
/// 「確かめている画面」と「遊ぶ画面」が別物になる。
///
/// ⚠️ 座標は1つも無い。位置は `Assets/Resources/Layouts/*.txt` が持つ。</summary>
public static class Sheets
{
    /// <summary>畳んだ帯に出す1行。⚠️ どの数で並べているかまで出す
    /// （「素質合計 順」だけだと、育成を含む数なのか読めなかった）。</summary>
    private static string SortLine(Shell s) =>
        $"{Filters.LabelOf(s.Filter)}　／　{Storages.LabelOf(s.Sort)} 順（{Storages.LabelOf(s.Basis)}）";

    /// <summary>並べ替えの帯と札に共通の差し込み。⚠️ 3画面が同じものを出す。</summary>
    private static string SortText(Shell s, string key, int chip) => key switch
    {
        "bar-now" => SortLine(s),
        "bar-arrow" => s.SortOpen ? "▲" : "▼",
        "chips-fchip" => Filters.LabelOf(Filters.Keys[chip]),
        "chips-schip" => Storages.LabelOf(Storages.SortKeys[chip]),
        "chips-bchip" => Storages.LabelOf(Storages.Bases[chip]),
        _ => "",
    };

    private static int SortCount(string key) => key switch
    {
        "chips-filters" => Filters.Keys.Length,
        "chips-sorts" => Storages.SortKeys.Length,
        "chips-bases" => Storages.Bases.Length,
        _ => 0,
    };

    // ── BOX ─────────────────────────────────────────

    public static string Box(Shell s)
    {
        var sorted = s.Sorted();
        var picked = s.PickedOne();
        if (picked == null) return "<!-- 手持ちが無い -->";
        var face = new Face(picked);
        var cells = new Face[sorted.Count];
        int one = 0, chip = 0;

        return LayoutDom.Render(LayoutStore.Of("box"), new DomFill
        {
            Count = key => key == "box" ? sorted.Count
                : key == "detail-stats" ? Stats.Keys.Length
                : key == "detail-lines" ? Stats.Keys.Length - 1
                : SortCount(key),

            At = (key, i) =>
            {
                if (key == "box") one = i;
                else if (key == "detail-stats") face.Row = i;
                else chip = i;
            },

            Text = key => key switch
            {
                // ⭐ 押しどころは3つ。⚠️ 主役（塗る）は「Lv ＋1」だけ
                "grow" => $"Lv ＋1　EXP {Face.Digits(Levels.ExpToNext(picked))}",
                "cellA-star" or "cellB-star" => Face.Star(sorted[one]),
                _ => key.StartsWith("detail-") ? face.Text(key[7..], face.Row)
                    : SortText(s, key, chip),
            },

            Sprite = key => key == "detail-art" ? face.Sprite
                : key is "cellA-art" or "cellB-art" ? Cell(one).Sprite : null,
            Palette = key => key == "detail-art" ? face.Palette
                : key is "cellA-art" or "cellB-art" ? Cell(one).Palette : null,

            Tint = key => key is "cellA-elem" or "cellB-elem"
                ? Face.ElementCss(sorted[one].Element)
                : key.StartsWith("detail-") ? face.Tint(key[7..]) : null,

            When = key => key switch
            {
                "open" => s.SortOpen,
                // ⭐ 印を付けるのは「いま見ている個体」だけ
                "cellA-picked" or "cellB-picked" => sorted[one].Id == picked.Id,
                // ⚠️ BOX の升に一言は出さない（合成の「＋14」・編成の「Lv 44」だけ）
                "cellA-note" or "cellB-note" => false,
                _ => key.StartsWith("detail-") && face.Shows(key[7..]),
            },

            Tappable = key => key != "grow" || s.Game.Idle.Exp >= Levels.ExpToNext(picked),
        });

        Face Cell(int at) => cells[at] ??= new Face(sorted[at]);
    }

    // ── 配合 ────────────────────────────────────────

    public static string Breed(Shell s)
    {
        var sorted = s.Sorted();
        var a = Of(s.ParentA);
        var b = Of(s.ParentB);
        var fa = a == null ? null : new Face(a);
        var fb = b == null ? null : new Face(b);
        var cells = new Face[sorted.Count];
        int one = 0, chip = 0;

        return LayoutDom.Render(LayoutStore.Of("breed"), new DomFill
        {
            Count = key => key == "box" ? sorted.Count
                : key is "pfill-stats" or "qfill-stats" ? Stats.Keys.Length
                : key is "pfill-lines" or "qfill-lines" ? Stats.Keys.Length - 1
                : SortCount(key),

            At = (key, i) =>
            {
                if (key == "box") one = i;
                else if (key == "pfill-stats") { if (fa != null) fa.Row = i; }
                else if (key == "qfill-stats") { if (fb != null) fb.Row = i; }
                else chip = i;
            },

            Text = key => Which(key) is (Face f, string what) ? f.Text(what, f.Row)
                : key is "cellA-star" or "cellB-star" ? Face.Star(sorted[one])
                : SortText(s, key, chip),

            Sprite = key => key is "cellA-art" or "cellB-art" ? Cell(one).Sprite
                : Which(key) is (Face f, "art") ? f.Sprite : null,
            Palette = key => key is "cellA-art" or "cellB-art" ? Cell(one).Palette
                : Which(key) is (Face f, "art") ? f.Palette : null,

            Tint = key => key is "cellA-elem" or "cellB-elem"
                ? Face.ElementCss(sorted[one].Element)
                : Which(key) is (Face f, string what) ? f.Tint(what) : null,

            When = key => key switch
            {
                "open" => s.SortOpen,
                "pa" => a != null,
                "pb" => b != null,
                "cellA-picked" or "cellB-picked" =>
                    sorted[one].Id == a?.Id || sorted[one].Id == b?.Id,
                "cellA-note" or "cellB-note" => false,
                _ => Which(key) is (Face f, string what) && f.Shows(what),
            },

            // ⚠️ **2体そろうまで押せない。**⭐ 押せないのに主導線の色のままだった頃は、
            //    「配合する」が押せるように見えていた
            Tappable = key => key != "breed" || (a != null && b != null && Fusion.CanFuse(a, b)),
        });

        Creature? Of(string? id)
        {
            if (id == null) return null;
            foreach (var c in s.Game.Storage.Creatures) if (c.Id == id) return c;
            return null;
        }
        Face Cell(int at) => cells[at] ??= new Face(sorted[at]);
        // ⭐ 冠から「どちらの親か」を読む。⚠️ ここが `use=` を2度差せる理由
        (Face, string)? Which(string? key) =>
            key == null ? null
            : key.StartsWith("pfill-") && fa != null ? (fa, key[6..])
            : key.StartsWith("qfill-") && fb != null ? (fb, key[6..])
            : null;
    }

    // ── ホーム ───────────────────────────────────────

    public static string Home(Shell s)
    {
        int at = 0;
        return LayoutDom.Render(LayoutStore.Of("home"), new DomFill
        {
            Count = key => key == "slots" ? Hatchery.Slots : 0,
            At = (key, i) => at = i,

            Text = key => key switch
            {
                // ⭐ **EXP と書く。**⚠️ 数だけ出していた頃は、丸い印の隣の数が
                //    何の数なのか画面のどこにも書いていなかった。
                "count" => $"EXP {Face.Digits(s.Game.Idle.Exp)}",
                "slot-stars" => Slot(at) is Incubation e ? Rarities.StarsOf(e.Egg.Rarity) : "",
                // ⭐ 孵ったら「孵った」と出す。⚠️ 帯の色（橙→緑）だけでは、
                //    取り出せるようになったことに気づけなかった
                "slot-clock" => Slot(at) is Incubation c
                    ? (Hatchery.IsReady(c, s.Now) ? "孵った" : Rarities.Clock(Hatchery.LeftOf(c, s.Now)))
                    : "",
                "slot-who" => Slot(at) is Incubation w
                    ? SpeciesTable.ById(w.Egg.SpeciesId).Name : "",
                _ => "",
            },

            Sprite = key => key == "slot-art" && Slot(at) != null ? EggArt.Sprite : null,
            Palette = key => key == "slot-art" && Slot(at) != null ? EggArt.Shell : null,

            Ratio = key => key == "slot-track" && Slot(at) is Incubation e
                ? Hatchery.ProgressOf(e, s.Now) : 0,

            Tint = key => key switch
            {
                "icon" => "#f59e0b",
                // ⭐ 孵る合図はうっすらした緑の丸（Prefab の実測 `#2ea84a` α.35）
                "slot-ready" => "rgba(46,168,74,.35)",
                "slot-track" => Slot(at) is Incubation e && Hatchery.IsReady(e, s.Now)
                    ? "#2fa84a" : "#f59e0b",
                "slot-clock" => Slot(at) is Incubation c && Hatchery.IsReady(c, s.Now)
                    ? "#1e7a38" : null,
                _ => null,
            },

            When = key => key switch
            {
                "slot-full" => Slot(at) != null,
                "slot-ready" => Slot(at) is Incubation e && Hatchery.IsReady(e, s.Now),
                _ => false,
            },

            Tappable = key => true,
        });

        Incubation? Slot(int i) => Hatchery.At(s.Game, i);
    }

    // ── パーティ編成 ─────────────────────────────────

    /// <summary>⭐ 巣は `party.txt`・放置は `partyidle.txt`。
    /// ⚠️ **差し込み口の名前は同じ**にしてあるので、ここは1つで足りる。</summary>
    public static string Party(Shell s, bool idle)
    {
        var game = s.Game;
        var kind = idle ? PartyKind.Idle : PartyKind.Nest;
        var roster = Games.RosterOf(game, kind);
        var other = idle ? PartyKind.Nest : PartyKind.Idle;
        var sorted = s.Sorted();
        var cells = new Face[sorted.Count];
        int one = 0, chip = 0, pick = 0, set = 0;

        return LayoutDom.Render(LayoutStore.Of(idle ? "partyidle" : "party"), new DomFill
        {
            Count = key => key switch
            {
                "box" => sorted.Count,
                "picks" => Games.PartySize,
                "sets" => Games.NestPartySlots,
                _ => SortCount(key),
            },
            At = (key, i) =>
            {
                if (key == "box") one = i;
                else if (key == "picks") pick = i;
                else if (key == "sets") set = i;
                else chip = i;
            },

            Text = key => key switch
            {
                "note" => idle
                    ? $"放置で戦い続ける{Games.PartySize}体です。巣へ潜る編成とは別です。"
                    : $"巣へ潜る{Games.PartySize}体です。3つまで登録できます。",
                "head" => $"選んでいる {roster.Count}/{Games.PartySize} 体",
                "slab" => $"編成{set + 1}  {game.NestParties[set].Count}体",
                "plv" => Member(pick) is Creature c ? $"Lv {Levels.Of(c)}" : "",
                "cellA-star" or "cellB-star" => Face.Star(sorted[one]),
                // ⭐ 一覧の一言は **Lv を優先**（2026-08-21・作者の指示）
                "cellA-note" or "cellB-note" => $"Lv {Levels.Of(sorted[one])}",
                _ => SortText(s, key, chip),
            },

            Sprite = key => key == "part" ? (Member(pick) is Creature c
                    ? Creatures.SpeciesOf(c).Sprite : null)
                : key is "cellA-art" or "cellB-art" ? Cell(one).Sprite : null,
            Palette = key => key == "part" ? (Member(pick) is Creature c
                    ? Creatures.PaletteOf(c) : null)
                : key is "cellA-art" or "cellB-art" ? Cell(one).Palette : null,

            Tint = key => key switch
            {
                "cellA-elem" or "cellB-elem" => Face.ElementCss(sorted[one].Element),
                // ⭐ もう一方の編成に入っていることは**色**で示す（Lv の場所を奪わない）
                "cellA-note" or "cellB-note" =>
                    Games.IsInParty(game, sorted[one].Id, other) ? "#b45309" : null,
                _ => null,
            },

            When = key => key switch
            {
                "open" => s.SortOpen,
                "full" => pick < roster.Count,
                "cellA-picked" or "cellB-picked" => roster.Contains(sorted[one].Id),
                "cellA-note" or "cellB-note" => true,
                _ => false,
            },

            Tappable = key => true,
        });

        Creature? Member(int at)
        {
            if (at >= roster.Count) return null;
            foreach (var c in game.Storage.Creatures) if (c.Id == roster[at]) return c;
            return null;
        }
        Face Cell(int at) => cells[at] ??= new Face(sorted[at]);
    }

    // ── 探索 ──────────────────────────────────────────────

    public static string Wilds(Shell s, int raids = 0)
    {
        var game = s.Game;
        int at = 0;

        return LayoutDom.Render(LayoutStore.Of("nests"), new DomFill
        {
            Count = key => key == "nests" ? game.Encounters.Count : 0,
            At = (key, i) => at = i,

            Text = key => key switch
            {
                "card-level" => game.Encounters[at].Level.ToString(),
                "card-left" => Left(at) is int n ? Rarities.Clock(n) : "",
                // ⭐ 4回盗むと親が道を塞ぐ ＝ 入れば必ず戦闘（巣の寿命）
                "card-raids" => Steal.IsSealed(raids) ? "戦闘"
                    : raids <= 0 ? "" : new string('●', raids),
                "bname" => EggCommand.Core.Nests.BossName,
                _ => "",
            },

            Sprite = key => key == "card-art"
                ? SpeciesTable.ById(game.Encounters[at].Nest.SpeciesId).Sprite
                : key == "bart" ? SpeciesTable.ById("nushi").Sprite : null,
            Palette = key => key == "card-art"
                ? SpeciesTable.ById(game.Encounters[at].Nest.SpeciesId).Palettes[0]
                : key == "bart" ? SpeciesTable.ById("nushi").Palettes[0] : null,

            // ⭐ 残りがこの割合を切ったら赤くする（数字を読ませずに急かす）
            Ratio = key => key == "card-track" ? RatioOf(at) : 0,
            Tint = key => key switch
            {
                "card-track" => RatioOf(at) <= 0.25 ? "#e04f5f" : "#2fa84a",
                "card-left" => RatioOf(at) <= 0.25 ? "#c0303f" : null,
                "card-raids" => Steal.IsSealed(raids) ? "#c0303f" : null,
                _ => null,
            },

            Tappable = key => true,
        });

        // ⚠️ **期限を持たない巣がある**（時刻を渡さずに始めた保存）。
        //    ⭐ 0 を「もう切れた」と読まない ── 読むと即座に消しにかかる。
        int? Left(int i)
        {
            var e = game.Encounters[i];
            return e.UntilUnix <= 0 ? null : Encounters.LeftOf(e, s.Now);
        }
        double RatioOf(int i)
        {
            if (Left(i) is not int left) return 0;
            int whole = Encounters.SecondsFor(game.Encounters[i].Nest.Tier);
            return whole <= 0 ? 0 : Math.Clamp(left / (double)whole, 0, 1);
        }
    }
}
