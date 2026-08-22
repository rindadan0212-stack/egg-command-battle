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
            // ⭐ 3つ目の `host`。⚠️ 何体並ぶかは編成しだい
            Inside = key => key == "idle" ? EggCommand.Web.Idle.Draw(s.Game) : "",
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

    // ── すごろく ───────────────────────────────────

    /// <summary>⭐ **2つ目の `host`。**⚠️ マスの位置は（段, 車線）から出すので、
    /// 骨組みが持つのは枠だけ（`Board` が中を埋める）。</summary>
    public static string Raid(Shell s)
    {
        var raid = s.Raid_;
        if (raid == null) return "<!-- 潜っていない -->";

        // ⚠️ 雑魚を倒すと回数が戻るので、最初の数より増えることがある
        int had = Math.Max(raid.Rolls, raid.Given);
        int show = Math.Min(had, 12);
        // ⭐ **見せかけの居場所で数える**（本当の居場所は歩き始めに終点へ動く）
        int left = Trails.LeftFrom(raid.Trail, raid.At);
        var keys = new[] { StatKey.Atk, StatKey.Hp, StatKey.Def };
        int die = 0, purse = 0;

        return LayoutDom.Render(LayoutStore.Of("trail"), new DomFill
        {
            Count = key => key switch { "dice" => show, "purse" => keys.Length, _ => 0 },
            At = (key, i) => { if (key == "dice") die = i; else purse = i; },

            Inside = key => key == "ground" ? Board.Draw(raid) : "",

            Text = key => key switch
            {
                "num" => left < 0 ? "—" : left.ToString(),
                "more" => $"+{had - show}",
                // ⚠️ **`Usable` で出す。**⭐ 払ったぶんを引き、一時増減を掛けた
                //    「いま実際に出せる額」でないと、関門の数と見比べられない。
                "pursen" => Face.Digits(Shown(keys[purse], Trails.Usable(raid, keys[purse]))),
                _ => "",
            },

            // ⭐ さいころの絵の数 ＝ あと何回振れるか。使ったぶんは空のさいころに変わる
            Pic = key => key switch
            {
                "die" => die < raid.Rolls ? "die" : "die-spent",
                "purse" => Board.IconOf(keys[purse]),
                _ => null,
            },
            Tint = key => key switch
            {
                // ⭐ 帯は暗い板（実物の `TrailScreen.Board`）
                "board" => "rgba(10,15,26,.55)",
                "die" => die < raid.Rolls ? "#ffffff" : "rgba(255,255,255,.26)",
                "num" => left < 0 ? "rgba(255,255,255,.55)" : "#ffffff",
                // ⭐ 一時増減が効いている間は色が変わる（▲▼ のマスと同じ色）
                "purse" or "pursen" => Temp(raid, keys[purse]),
                _ => null,
            },

            When = key => key switch
            {
                "more" => had > show,
                // ⚠️ **振れるのは Moved のときだけ。**⭐ 他の段で釦を出すと、
                //    押した瞬間に `Trails.Roll` が撥ねて進行不能に見える。
                "canroll" => raid.Result == null && raid.Step == RaidStep.Moved,
                _ => false,
            },

            Tappable = key => key != "roll" || raid.Rolls > 0,
        });

        static string Temp(Raid raid, StatKey key)
        {
            int pct = raid.TempLeft[key] > 0 ? raid.Temp[key] : 0;
            return pct > 0 ? "#1e7a38" : pct < 0 ? "#c0303f" : "#ffffff";
        }

        // ⚠️ HP だけ桁が違う（画面に出る HP は ×105）
        static int Shown(StatKey key, int value) =>
            key == StatKey.Hp ? value * EggCommand.Core.Battle.HpScale : value;
    }

    // ── 戦闘 ──────────────────────────────────────────────

    /// <summary>⭐ **`host` の初実戦。**⚠️ 立ち位置は体数から逆算するので、
    /// 骨組みが持つのは枠だけ（`Stands` が中を埋める）。</summary>
    public static string Fight(Shell s)
    {
        var state = s.Fight_;
        if (state == null) return "<!-- 戦っていない -->";

        var allies = new List<Unit>();
        var foes = new List<Unit>();
        foreach (var u in state.Units) (u.Side == Side.Ally ? allies : foes).Add(u);
        var actor = EggCommand.Core.Battle.NextActor(state);
        var skills = actor != null ? Creatures.SkillsOf(actor.Creature) : new Skill?[3];
        bool done = state.Result != null;

        return LayoutDom.Render(LayoutStore.Of("battle"), new DomFill
        {
            Inside = key => key switch
            {
                "allies" => Column(allies, 540, 1278, false, actor),
                "foes" => Column(foes, 540, 1278, true, actor),
                _ => "",
            },

            Text = key => key switch
            {
                "pick" => "オート",
                "finish" => state.Result switch
                {
                    Outcome.Ally => "勝った",
                    Outcome.Enemy => "負けた",
                    _ => "引き分け",
                },
                _ => Slot(key) is (int n, string what)
                    ? SkillWord(skills, actor, n, what) : "",
            },

            // ⭐ CT の丸薬は濃紺・字は白。⚠️ 同じ色を2か所に書かない
            Tint = key => key.EndsWith("pill") ? "#2b3350"
                : key.EndsWith("ct") ? "#ffffff" : null,

            When = key => key switch
            {
                "done" => done,
                "s0" => skills[0] != null,
                "s1" => skills.Length > 1 && skills[1] != null,
                "s2" => skills.Length > 2 && skills[2] != null,
                _ => false,
            },

            // ⚠️ CT が残っている技は押せない
            Tappable = key => Slot(key) is (int n, "") ? Ready(actor, n) : true,
        });

        static (int, string)? Slot(string? key)
        {
            if (key == null || key.Length < 2 || key[0] != 's' || key[1] < '0' || key[1] > '2')
                return null;
            return (key[1] - '0', key.Substring(2));
        }

        static bool Ready(Unit? actor, int slot) =>
            actor != null && slot < actor.Cooldowns.Length && actor.Cooldowns[slot] <= 0;

        static string SkillWord(Skill?[] skills, Unit? actor, int slot, string what)
        {
            if (slot >= skills.Length || skills[slot] is not Skill skill) return "";
            return what switch
            {
                "name" => skill.Name,
                // ⭐ 残りの CT を出す。⚠️ 0 なら「使える」と読めるように空にしない
                "ct" => actor != null && actor.Cooldowns[slot] > 0
                    ? $"あと {actor.Cooldowns[slot]}"
                    : $"CT{Skills.EffectiveCt(slot, skill)}",
                "lv" => actor != null ? $"Lv{Creatures.SkillLevelOf(actor.Creature, slot)}" : "",
                _ => "",
            };
        }
    }

    /// <summary>片側の列。⭐ 1体ずつ `unit.txt` で描いて、計算した場所へ置く。</summary>
    private static string Column(List<Unit> units, float wide, float room, bool foe, Unit? actor)
    {
        var spots = Stands.Lay(units.Count, wide, room);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            bool alive = EggCommand.Core.Battle.IsAlive(u);
            var says = EggCommand.Core.Battle.ActiveStatuses(u);
            // ⚠️ **側も名前に入れる。**⭐ 番号だけだと味方の1体目と敵の1体目が同じ id になる
            sb.Append(Stands.One(spots[i], (foe ? "f" : "a") + i, new DomFill
            {
                Text = key => key == "status" ? string.Join(" ", says) : "",
                Sprite = key => key == "art" ? Creatures.SpeciesOf(u.Creature).Sprite : null,
                Palette = key => key == "art" ? Creatures.PaletteOf(u.Creature) : null,
                Ratio = key => key switch
                {
                    "hp" => u.MaxHp > 0 ? Math.Clamp(u.Hp / (double)u.MaxHp, 0, 1) : 0,
                    "gauge" => Math.Clamp(u.Gauge / (double)EggCommand.Core.Battle.GaugeMax, 0, 1),
                    _ => 0,
                },
                Tint = key => key switch
                {
                    // ⭐ 生きていれば味方は緑・敵は赤。⚠️ 倒れたら沈める
                    "hp" => !alive ? "#636980" : foe ? "#e04f5f" : "#2fa84a",
                    "elem" => Face.ElementCss(u.Creature.Element),
                    "beats" => Face.ElementCss(SpeciesTable.Beats(u.Creature.Element)),
                    "glow" => "rgba(255,217,77,.55)",
                    _ => null,
                },
                When = key => key switch
                {
                    "foe" => foe,
                    // ⭐ いま手番が回っている体を光らせる
                    "actor" => actor != null && actor.Key == u.Key,
                    _ => false,
                },
            }));
        }
        return sb.ToString();
    }

    // ── パーティ編成 ─────────────────────────────────────────

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
