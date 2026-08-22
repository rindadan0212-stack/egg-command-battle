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
                // ⭐ 16進なら8桁に収まり、口で伝えられる長さになる
                "world" => "#" + s.Game.Seed.ToString("X8"),
                "trials" => $"試練　{Games.TrialsCleared(s.Game)}/{Core.Trials.All.Count}",
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

            // ⭐ 駒は**見せている所**に立つ（歩いている最中は道の途中）
            Inside = key => key == "ground"
                ? Board.Draw(raid, s.Open_, s.Path != null ? s.Path[s.Step_] : -1) : "",

            Text = key => key switch
            {
                "num" => left < 0 ? "—" : left.ToString(),
                "more" => $"+{had - show}",
                // ⚠️ **`Usable` で出す。**⭐ 払ったぶんを引き、一時増減を掛けた
                //    「いま実際に出せる額」でないと、関門の数と見比べられない。
                "pursen" => Face.Digits(Shown(keys[purse], Trails.Usable(raid, keys[purse]))),
                // ⭐ 関門で払う量と、もらえる物
                "payn" => Toll_(raid) is Toll t
                    ? Face.Digits(Shown(Trails.StatOf(t.Kind), t.Price)) : "",
                "paygot" => Gift_(raid) is Gift g ? Word(g) : "",
                _ => "",
            },

            // ⭐ さいころの絵の数 ＝ あと何回振れるか。使ったぶんは空のさいころに変わる
            Pic = key => key switch
            {
                "die" => die < raid.Rolls ? "die" : "die-spent",
                "purse" => Board.IconOf(keys[purse]),
                "paypic" => Toll_(raid) is Toll t ? Board.IconOf(Trails.StatOf(t.Kind)) : "plain",
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
                // ⭐ 関門で選んでいるあいだは、振る釦を引っ込める
                "offer" => raid.Step == RaidStep.Offered,
                _ => false,
            },

            // ⚠️ **振れるのは Moved のときだけ**（他の段で押せると
            //    `Trails.Roll` が樰ねて進行不能に見える）。⭐ 出すが、押せない。
            Tappable = key => key != "roll"
                || (raid.Rolls > 0 && raid.Result == null && raid.Step == RaidStep.Moved),
        });

        static string Temp(Raid raid, StatKey key)
        {
            int pct = raid.TempLeft[key] > 0 ? raid.Temp[key] : 0;
            return pct > 0 ? "#1e7a38" : pct < 0 ? "#c0303f" : "#ffffff";
        }

        // ⚠️ HP だけ桁が違う（画面に出る HP は ×105）
        static int Shown(StatKey key, int value) =>
            key == StatKey.Hp ? value * EggCommand.Core.Battle.HpScale : value;

        static Toll? Toll_(Raid r) =>
            r.Step == RaidStep.Offered ? r.Trail.Squares[r.At].Toll : null;
        static Gift? Gift_(Raid r) =>
            r.Step == RaidStep.Offered ? r.Trail.Squares[r.At].Face : null;

        // ⭐ もらえる物を短く。⚠️ 字で説明しない
        static string Word(Gift g) => g.Kind switch
        {
            GiftKind.Rolls => $"＋{g.Amount}",
            GiftKind.Hop => $"＋{g.Amount}マス",
            GiftKind.Stat => $"{(g.Amount < 0 ? "" : "＋")}{g.Amount}%",
            _ => "",
        };
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
                // ⭐ **オートは入切の札。**⚠️ 「オート」とだけ書いていた頃は、
                //    押しても**いまどちらなのか画面のどこにも出ていなかった**
                //    （Unity 版は字と色の両方で出している）。
                "pick" => s.Auto ? "オート  ON" : "オート  OFF",
                _ => Slot(key) is (int n, string what)
                    ? SkillWord(skills, actor, n, what) : "",
            },

            // ⭐ CT の丸薬は濃紺・字は白。⚠️ 同じ色を2か所に書かない
            Tint = key => key.EndsWith("pill") ? "#2b3350"
                : key.EndsWith("ct") ? "#ffffff" : null,

            // ⭐ 入っているあいだは主役に立てる（字だけだと遠目に読めない）
            Lead = key => key == "pick" && s.Auto,

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
    public static string Party(Shell s, bool idle, string crown = "")
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
        }, crown: crown);

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

    // ── 種族の札 ────────────────────────────────────

    /// <summary>種族の中身。⭐ **`flow=down` の初実戦**
    /// ── 技の袋の長さが変わるので、下の塊の位置を骨組みに書けない。</summary>
    public static string Species(Shell s, string crown = "")
    {
        var all = SpeciesTable.All;
        var species = all[Math.Clamp(s.SpeciesAt, 0, all.Count - 1)];
        var trait = Traits.Has(species.TraitId) ? Traits.ById(species.TraitId) : (Trait?)null;

        var pools = new[]
        {
            new[] { species.Skill1 },
            species.Slot2.Pool.ToArray(),
            species.Slot3.Pool.ToArray(),
        };
        var at = new[] { 0, 0, 0 };

        return LayoutDom.Render(LayoutStore.Of("species"), new DomFill
        {
            Count = key => key switch
            {
                "slot1" => pools[0].Length,
                "slot2" => pools[1].Length,
                "slot3" => pools[2].Length,
                _ => 0,
            },
            At = (key, i) =>
            {
                if (key == "slot1") at[0] = i;
                else if (key == "slot2") at[1] = i;
                else if (key == "slot3") at[2] = i;
            },

            Text = key => key switch
            {
                "name" => species.Name,
                // ⭐ **いつ効くか**を名前の隣に置く ── 「常時」と「倒れる一撃を受けたとき」では
                //    編成に入れる理由がまるで違う。
                "tname" => trait is Trait t ? $"{t.Name}　― {Traits.LabelOf(t.When)}"
                    : "（特性が繋がっていない）",
                "tgist" => trait is Trait g ? g.Gist : "",
                // ⚠️ 枠1 に「N種」と付けない ── ⭐ 抽選ではない（必ずこれ）ので、
                //    数を出すと引くものに見える
                "s2head" => $"枠2の抽選　{pools[1].Length}種",
                "s3head" => $"枠3の抽選　{pools[2].Length}種",
                "s1name" => NameOf(0), "s2name" => NameOf(1), "s3name" => NameOf(2),
                "s1kind" => KindOf(0), "s2kind" => KindOf(1), "s3kind" => KindOf(2),
                _ => "",
            },

            Sprite = key => key == "art" ? species.Sprite : null,
            Palette = key => key == "art" ? species.Palettes[0] : null,

            // ⚠️ **知らない id を黙って飛ばさない。**⭐ 袋に綴り違いが入ったら
            //    「その技は一生出ない」なので、目に見える形で出す
            Tint = key => key switch
            {
                "s1name" => Skills.Has(pools[0][at[0]]) ? null : "#c0303f",
                "s2name" => Skills.Has(pools[1][at[1]]) ? null : "#c0303f",
                "s3name" => Skills.Has(pools[2][at[2]]) ? null : "#c0303f",
                "tname" => trait == null ? "#c0303f" : null,
                _ => null,
            },

            Tappable = key => true,
        }, crown: crown);

        string NameOf(int slot)
        {
            var id = pools[slot][at[slot]];
            return Skills.Has(id) ? Skills.ById(id).Name : id;
        }

        string KindOf(int slot)
        {
            var id = pools[slot][at[slot]];
            return Skills.Has(id) ? Skills.LabelOf(Skills.ById(id).Type) : "";
        }
    }

    /// <summary>その種族の技の袋（枠1・枠2・枠3）。⭐ **長押しの行き先を出すのに使う。**
    /// ⚠️ 出す側と読む側が別々に作ると、押した札と開く技がずれる。</summary>
    public static IReadOnlyList<string> PoolOf(Species species, int slot) => slot switch
    {
        0 => new[] { species.Skill1 },
        1 => species.Slot2.Pool.ToArray(),
        _ => species.Slot3.Pool.ToArray(),
    };

    // ── 技の詳細 ────────────────────────────────────

    /// <summary>技1つの中身。⚠️ **枠1（0）は CT を 0 で出す**
    /// ── 技の表の数をそのまま出すと画面が嘘をつく（実測 2026-08-22:
    /// BOX の札は「CT0」、長押しの詳細は「CT 3」と出ていた）。</summary>
    public static string SkillCard(Shell s, string crown = "")
    {
        if (!Skills.Has(s.SkillId)) return "";
        var skill = Skills.ById(s.SkillId!);
        var power = SkillText.PowerOf(skill);
        int slot = s.SkillSlot;
        int level = Math.Max(1, s.SkillLevel);

        return LayoutDom.Render(LayoutStore.Of("skillinfo"), new DomFill
        {
            Text = key => key switch
            {
                "name" => skill.Name,
                // ⭐ Lv・CT・威力を1行に。⚠️ 3行に割ると札より縦に長い覆いになる
                "meta" => $"Lv {level} / {Skills.MaxLevel}"
                    + $"　CT {(slot == 0 ? 0 : skill.Ct)}"
                    + (power.Length > 0 ? $"　威力 {power}" : ""),
                "body" => SkillText.Describe(skill),
                // ⚠️ 「上げると強くなる」と書かない。⭐ Lv2→Lv5 の実数を並べる
                "steps" => SkillText.StepsOf(skill, slot),
                _ => "",
            },
            Tappable = key => true,
        }, crown: crown);
    }

    // ── 保存の控え ──────────────────────────────────

    /// <summary>⭐ **ブラウザの外へ出す唯一の口。**
    /// ⚠️ 中の棚（localStorage）は容量が足りなくなると**まとめて**消えるので、
    /// 世代を残しても一緒に消える ── 別の消え方をする場所は外にしかない。</summary>
    public static string Keep(Shell s, string crown = "") =>
        LayoutDom.Render(LayoutStore.Of("save"), new DomFill
        {
            Text = key => key switch
            {
                // ⚠️ **0 を「0字」と出さない。**⭐ まだ書かれていないのと、
                //    空っぽなのは別のこと（読む人には同じに見える）
                "where" => s.SaveSize > 0
                    ? $"いまの保存　{Face.Digits(s.SaveSize)} 字"
                    : "まだ書かれていません",
                // ⚠️ **全部の古さを並べない。**⭐ 5本あると枠に入らない
                //    （実測 926 対 824）。読む人が知りたいのは「何本・どこまで遡れるか」。
                "gens" => s.SavePast.Length == 0 ? "この端末の控えはまだ在りません"
                    : $"この端末の控え　{s.SavePast.Length}本　"
                        + $"いちばん古いのは {Age(Oldest(s.SavePast))}",
                _ => "",
            },
            // ⚠️ **読み込みは押し間違いが怖い。**⭐ 何も無いときは押させない
            Tappable = key => key != "in" || s.SaveSize > 0 || s.SavePast.Length > 0,
        }, crown: crown);

    private static int Oldest(int[] seconds)
    {
        int most = 0;
        foreach (int one in seconds) if (one > most) most = one;
        return most;
    }

    /// <summary>控えの古さを読める字に。⭐ `Rarities.Clock` と同じ言い回しに揃える。</summary>
    private static string Age(int seconds) =>
        seconds < 60 ? "たった今" : Rarities.Clock(seconds) + "前";

    // ── さいころ ────────────────────────────────────

    /// <summary>回っているさいころ。⭐ **目が決まる瞬間だけを見せる。**
    /// ⚠️ 出目は `Trails.Roll` が先に決めている ── ここは見せるだけ。
    /// ⚠️ 回っている間の面は**乱数を引かない**（`fx.js` が順に送る）。</summary>
    public static string Dice(Shell s, string crown = "") =>
        LayoutDom.Render(LayoutStore.Of("dice"), new DomFill
        {
            Pic = key => "die-" + Math.Clamp(s.Dice, 1, Trail.Pips),
            Tint = key => key == "face" ? "var(--ink)" : null,
            Tappable = key => false,
        }, crown: crown);

    // ── 告知 ────────────────────────────────────────

    /// <summary>短い告知。⭐ 出て、読ませて、自分で消えて、次へ渡す。
    /// ⚠️ **ボタンを置かない** ── 勝ち負けは選択ではなく結果。</summary>
    public static string Banner(Shell s, string crown = "") =>
        LayoutDom.Render(LayoutStore.Of("banner"), new DomFill
        {
            Text = key => key == "line" ? s.Banner ?? "" : "",
            Tappable = key => false,
        }, crown: crown);

    // ── 確かめる ────────────────────────────────────

    /// <summary>「本当にやりますか」を一度だけ聞く札。
    /// ⚠️ **取り返しがつかない操作にだけ挟む** ── 何にでも挟むと、
    /// 読まずに押す癖が付いて、肝心なときに効かなくなる。
    ///
    /// ⚠️ **札の字に印付けを混ぜない**（`**` や ⚠️ はコードの注釈の書き方であって、
    /// 遊ぶ人の画面にそのまま出る）。</summary>
    public static string Ask(Shell s, string crown = "") =>
        LayoutDom.Render(LayoutStore.Of("ask"), new DomFill
        {
            Text = key => key switch
            {
                "title" => "あきらめますか",
                "body" => "この戦いは負けになります。戻すことはできません。",
                "stop" => "やめる",
                "go" => "あきらめる",
                _ => "",
            },
            Tappable = key => true,
        }, crown: crown);

    // ── 卵を選ぶ ────────────────────────────────────

    /// <summary>孵化器の空き枠に入れる卵を選ぶ覆い。
    /// ⚠️ **画面いっぱいに出す** ── 本体の中に置くと、上の見出しと下の帯だけ
    /// 明るいまま押せてしまう。</summary>
    public static string Eggs(Shell s, string crown = "")
    {
        var eggs = s.Game.Eggs;
        int at = 0;

        return LayoutDom.Render(LayoutStore.Of("eggpicker"), new DomFill
        {
            Count = key => key == "eggs" ? eggs.Count : 0,
            At = (key, i) => at = i,

            Text = key => key switch
            {
                // ⚠️ 「卵がありません」と書かない。⭐ 数を言えば足りる
                "count" => $"棚の卵 {eggs.Count}",
                "egg-stars" => Rarities.StarsOf(eggs[at].Rarity),
                // ⭐ 素質は伏せない。手元にある卵なので、どれを先に温めるかの材料になる
                "egg-wild" => Stats.TotalOf(eggs[at].Wild).ToString(),
                "egg-wait" => Rarities.Clock(Math.Max(1, Rarities.SecondsOf(eggs[at].Rarity))),
                "egg-who" => SpeciesTable.ById(eggs[at].SpeciesId).Name,
                _ => "",
            },

            Sprite = key => key == "egg-art" ? EggArt.Sprite : null,
            Palette = key => key == "egg-art" ? EggArt.Shell : null,
            Tint = key => key == "egg-elem" ? Face.ElementCss(eggs[at].Element) : null,

            Tappable = key => true,
        }, crown: crown);
    }

    // ── 分解 ────────────────────────────────────────

    /// <summary>⭐ **個体を EXP に還す札。**⚠️ 分解した個体は失われる。</summary>
    public static string Fuse(Shell s, string crown = "")
    {
        var game = s.Game;
        var eater = s.PickedOne();
        var pool = Deeds.Food(s);
        var cells = new Face[pool.Count];
        int at = 0;

        // ⭐ 押した順ではなく**並びの順**で数える（画面に出ている順と合わせる）
        int exp = 0;
        foreach (var c in pool) if (s.Melts.Contains(c.Id)) exp += Levels.DissolveExpOf(c);

        return LayoutDom.Render(LayoutStore.Of("fuse"), new DomFill
        {
            Count = key => key == "box" ? pool.Count : 0,
            At = (key, i) => at = i,

            Text = key => key switch
            {
                "who" => eater == null ? ""
                    : $"{Creatures.SpeciesOf(eater).Name}  "
                        + $"Lv {Levels.Of(eater)}/{Levels.MaxOf(eater)}"
                        + $"　　持っている EXP {Face.Digits(game.Idle.Exp)}",
                "gain" => $"選んだ {s.Melts.Count}/{Games.PickAtOnce} 体で  EXP ＋{Face.Digits(exp)}",
                "go" => s.Melts.Count > 0 ? $"分解する（EXP ＋{Face.Digits(exp)}）" : "分解する",
                "cell-star" => Face.Star(pool[at]),
                // ⭐ 一言は「分解したら何 EXP になるか」。
                // ⚠️ **出撃中は分解する前に分かるようにする**（分解すると失われる）
                "cell-note" => (Games.IsInParty(game, pool[at].Id) ? "出撃中  " : "")
                    + $"EXP {Face.Digits(Levels.DissolveExpOf(pool[at]))}",
                _ => "",
            },

            Sprite = key => key == "cell-art" ? Cell(at).Sprite : null,
            Palette = key => key == "cell-art" ? Cell(at).Palette : null,

            Tint = key => key switch
            {
                "cell-elem" => Face.ElementCss(pool[at].Element),
                "cell-note" => Games.IsInParty(game, pool[at].Id) ? "#c0303f" : null,
                _ => null,
            },

            When = key => key switch
            {
                "any" => pool.Count > 0,
                "cell-picked" => s.Melts.Contains(pool[at].Id),
                "cell-note" => true,
                _ => false,
            },

            Tappable = key => key != "melt" || s.Melts.Count > 0,
        }, crown: crown);

        Face Cell(int i) => cells[i] ??= new Face(pool[i]);
    }

    // ── 技を鍛える ──────────────────────────────────

    /// <summary>⭐ **孵さない卵の唯一の出口。**
    /// ⚠️ 選んでから、最後に「強化する」を押す ── 1個ずつ入ると取り消せない。</summary>
    public static string Train(Shell s, string crown = "")
    {
        var game = s.Game;
        var one = s.PickedOne();
        var skills = one == null ? new Skill?[3] : Creatures.SkillsOf(one);
        var eggs = game.Eggs;
        int slot = Math.Clamp(s.Slot_, 0, skills.Length - 1);
        bool usable = one != null && skills[slot] != null
            && !SkillCosts.IsMaxed(one.SkillPoints[slot]);
        int points = usable ? one!.SkillPoints[slot] : 0;
        int gain = 0;
        foreach (var e in eggs) if (s.Feeds.Contains(e.Id)) gain += Rarities.PointsOf(e.Rarity);
        int row = 0, at = 0;

        return LayoutDom.Render(LayoutStore.Of("skillegg"), new DomFill
        {
            Count = key => key switch
            {
                "slots" => skills.Length,
                "eggs" => eggs.Count,
                _ => 0,
            },
            At = (key, i) => { if (key == "slots") row = i; else at = i; },

            Text = key => key switch
            {
                "who" => one == null ? "" : Creatures.SpeciesOf(one).Name,
                "rname" => skills[row]?.Name ?? "—",
                "rlv" => one == null ? "" : $"Lv{SkillCosts.LevelOf(one.SkillPoints[row])}",
                // ⭐ あと何ポイントで次かを出す。⚠️ 上限は「上限」と書く（0 と出さない）
                "rneed" => one == null || skills[row] == null ? ""
                    : SkillCosts.IsMaxed(one.SkillPoints[row]) ? "上限"
                    : $"あと {SkillCosts.ToNext(one.SkillPoints[row])}",
                // ⭐ **入れたあとのレベルまで出す。**⚠️ ポイントだけだと人が計算することになる
                "head" => !usable ? "この枠はもう鍛えられません"
                    : gain > 0
                        ? $"選んだ {s.Feeds.Count}/{Games.PickAtOnce} 個で ＋{gain}　"
                            + $"Lv{SkillCosts.LevelOf(points)} → Lv{SkillCosts.LevelOf(points + gain)}"
                        : $"棚の卵 {eggs.Count}　（{Games.PickAtOnce} 個まで選べます）",
                "chip-who" => SpeciesTable.ById(eggs[at].SpeciesId).Name,
                "chip-stars" => Rarities.StarsOf(eggs[at].Rarity),
                "chip-note" => "＋" + Rarities.PointsOf(eggs[at].Rarity),
                "go" => s.Feeds.Count > 0 ? $"強化する（＋{gain}）" : "強化する",
                _ => "",
            },

            Sprite = key => key == "chip-art" ? EggArt.Sprite : null,
            Palette = key => key == "chip-art" ? EggArt.Shell : null,

            // ⭐ 選んでいる枠だけ塗る。⚠️ 鍛えられない枠は沈める
            Tint = key => key == "row" && row == slot && usable ? "#f59e0b" : null,

            When = key => key == "chip-picked" && s.Feeds.Contains(eggs[at].Id),

            // ⚠️ **上限を超える卵は選ばせない。**受け取ると超えた分が黙って消える
            Tappable = key => key switch
            {
                "row" => one != null && skills[row] != null
                    && !SkillCosts.IsMaxed(one.SkillPoints[row]),
                "feed" => s.Feeds.Count > 0,
                _ => true,
            },
        }, crown: crown);
    }

    // ── 図鑑 ────────────────────────────────────────

    /// <summary>⭐ **見たことのある種族だけ名前が出る。**
    /// ⚠️ 伏せた種族も枠は残す ── 何種類いるかは隠さない（集める的が見える）。</summary>
    public static string Book(Shell s)
    {
        var all = SpeciesTable.All;
        int at = 0;
        bool Seen(int i) => Games.HasSeen(s.Game, all[i].Id);

        return LayoutDom.Render(LayoutStore.Of("book"), new DomFill
        {
            Count = key => key == "species" ? all.Count : 0,
            At = (key, i) => at = i,

            Text = key => key switch
            {
                "count" => $"手に入れた種族　{Games.SeenCount(s.Game)} / {all.Count}",
                "name" => Seen(at) ? all[at].Name : "？？？",
                "trait" => Traits.Has(all[at].TraitId) ? Traits.ById(all[at].TraitId).Name : "—",
                "hide" => "—",
                _ => "",
            },

            Sprite = key => key == "art" ? all[at].Sprite : null,
            Palette = key => key == "art" ? all[at].Palettes[0] : null,

            Fade = key => key == "art" && !Seen(at) ? 0.28 : (double?)null,
            Tint = key => key != "art" && !Seen(at) ? "var(--ink-faint)" : null,

            Tappable = key => key == "species" && Seen(at),
            When = key => key == "known" && Seen(at),
        });
    }

    // ── 試練 ────────────────────────────────────────

    /// <summary>⭐ **中身を先に見せる場所。**巣は隠すが、試練は逆 ──
    /// 何が来るか分かったうえで**組み直して挑む**ので、顔ぶれも一言も出す。</summary>
    public static string Trials_(Shell s)
    {
        var trials = Core.Trials.All;
        int step = 0, who = 0;

        Trial Now() => trials[step];
        IReadOnlyList<Creature> Party() => Core.Trials.PartyOf(Now());
        bool Beaten() => Games.BeatTrial(s.Game, Now().Id);

        return LayoutDom.Render(LayoutStore.Of("trial"), new DomFill
        {
            Count = key => key switch
            {
                "trials" => trials.Count,
                "party" => Party().Count,
                _ => 0,
            },
            At = (key, i) => { if (key == "trials") step = i; else if (key == "party") who = i; },

            Text = key => key switch
            {
                "note" => $"勝った段　{Games.TrialsCleared(s.Game)} / {trials.Count}",
                "step" => Core.Trials.StepOf(Now().Id).ToString(),
                "name" => Now().Name,
                "gist" => Now().Gist,
                "won" => "勝った",
                "go" => Beaten() ? "もう一度" : "挑む",
                _ => "",
            },

            // ⚠️ **敵は左右反転で出す**（作者の指示 2026-08-21）
            Sprite = key => key == "face"
                ? SpeciesTable.ById(Party()[who].SpeciesId).Sprite : null,
            Palette = key => key == "face"
                ? SpeciesTable.ById(Party()[who].SpeciesId).Palettes[0] : null,

            When = key => key == "beaten" && Beaten(),
            Tappable = key => key == "trial",
        });
    }
}
