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

    /// <summary>一覧の升の下に出す「いま何順か」の数。⭐ 作者の指示（2026-08-30
    /// 「枠内下に並び替え中の数字か星を表示」）── 並べ替えの基準が数で語るものなら
    /// その数を出す。⚠️ **入手順だけは数を持たない**ので空を返す（★が升の上に在る）。
    /// ⭐ 数の出所は `Storages.ShownValue` ひとつ ── 並べ替えと同じ式で出す
    /// （ここで数え直すと、並び順と表示が食い違いうる）。</summary>
    private static string SortShown(Shell s, Creature creature)
    {
        int? value = Storages.ShownValue(creature, s.Sort, s.Basis);
        // ⚠️ **数だけ**を出す。⭐ 何の数かはすぐ上の並べ替えの帯が言っている
        //    （「素質合計 順（素質だけ）」）── 升ごとに繰り返すと、絵の上に長い字が乗って
        //    かえって読めなくなる（実測 2026-08-30: 「素質合計 726」は 208px の枠に対して
        //    ほぼ幅いっぱいで、絵に埋もれた）。
        return value is int v ? Face.Digits(v) : "";
    }

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
                // ⭐ **持っている EXP**（旧 上のバーの右肩 ── 2026-08-29 に画面の中へ）。
                //    ⚠️ すぐ上の "grow" は**要る量**、こちらは**持っている量**。両方要る。
                // 🔴 **「持っている」の字は消した**（2026-08-30・作者の指示）── 枠の絵
                //    （`home-frame1`）の中に数だけを出す。⚠️ 枠が「これは持ち物だ」と言う。
                "exp" => Face.Digits(s.Game.Idle.Exp),
                "cellA-star" or "cellB-star" => Face.Star(sorted[one]),
                // ⭐ 並び替え中の数（入手順なら数が無いので空 ── ★は上に出ている）
                "cellA-note" or "cellB-note" => SortShown(s, sorted[one]),
                _ => key.StartsWith("detail-") ? face.Text(key[7..], face.Row)
                    : SortText(s, key, chip),
            },

            Sprite = key => key == "detail-art" ? face.Sprite
                : key is "cellA-art" or "cellB-art" ? Cell(one).Sprite : null,
            Palette = key => key == "detail-art" ? face.Palette
                : key is "cellA-art" or "cellB-art" ? Cell(one).Palette : null,
            // ⭐ 升は絵を切って見せる（`cell.txt` の `crop=`）── その「見せどころ」。
            Focus = key => key is "cellA-art" or "cellB-art" ? Cell(one).Focus : null,

            Tint = key => key is "cellA-elem" or "cellB-elem"
                ? Face.ElementCss(sorted[one].Element)
                : key.StartsWith("detail-") ? face.Tint(key[7..]) : null,

            When = key => key switch
            {
                "open" => s.SortOpen,
                // ⭐ 印を付けるのは「いま見ている個体」だけ
                "cellA-picked" or "cellB-picked" => sorted[one].Id == picked.Id,
                // 🔴 **升の下に並び替え中の数を出す**（2026-08-30・作者の指示）。
                //    ⚠️ 入手順は数で並べていないので、そのときだけ出さない（★が上に在る）。
                "cellA-note" or "cellB-note" => SortShown(s, sorted[one]).Length > 0,
                _ => key.StartsWith("detail-") && face.Shows(key[7..]),
            },

            Tappable = key => key switch
            {
                "grow" => s.Game.Idle.Exp >= Levels.ExpToNext(picked),
                // ⭐ 2世代未満は墓標を辿っても親が居ない ── 押しても「不明」しか出ない
                //    ので、そもそも押させない（作者の指示「BOXで2世代以降の
                //    キャラクターの家系図を見られるように」）。
                "tree" => picked.Generation >= 2,
                _ => true,
            },
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
                // ⭐ 並び替え中の数（BOX と同じ ── 一覧の読み方を画面で変えない）
                : key is "cellA-note" or "cellB-note" ? SortShown(s, sorted[one])
                // 🔴 **持っている EXP は配合から外した**（2026-08-30・作者の指示
                //    「配合画面には不要」）── ここは2体を見比べる場所で、EXP は使わない。
                : SortText(s, key, chip),

            Sprite = key => key is "cellA-art" or "cellB-art" ? Cell(one).Sprite
                : Which(key) is (Face f, "art") ? f.Sprite : null,
            Palette = key => key is "cellA-art" or "cellB-art" ? Cell(one).Palette
                : Which(key) is (Face f, "art") ? f.Palette : null,
            // ⭐ 升は絵を切って見せる（`cell.txt` の `crop=`）── その「見せどころ」。
            Focus = key => key is "cellA-art" or "cellB-art" ? Cell(one).Focus : null,

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
                "cellA-note" or "cellB-note" => SortShown(s, sorted[one]).Length > 0,
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
        return LayoutDom.Render(LayoutStore.Of("home"), new DomFill
        {
            // ⭐ `host` は2つ。⚠️ どちらも「格子で書けない置き場所」を持つ側が描く
            //    ── 放置は何体並ぶかが編成しだい、巣は 2-1-2 の菱形。
            Inside = key => key switch
            {
                "idle" => EggCommand.Web.Idle.Draw(s.Game),
                "nests" => Incubator.Draw(s),
                _ => "",
            },

            Text = key => key switch
            {
                // ⭐ **EXP と書く。**⚠️ 数だけ出していた頃は、丸い印の隣の数が
                //    何の数なのか画面のどこにも書いていなかった。
                "count" => $"EXP {Face.Digits(s.Game.Idle.Exp)}",
                // ⚠️ 世界番号（bind=world）は保存の控えの札へ移した（2026-08-29 ──
                //    右端を「控え」釦に譲った。描く側は下の Keep）
                "trials" => $"試練　{Games.TrialsCleared(s.Game)}/{Core.Trials.All.Count}",
                _ => "",
            },

            Tint = key => key switch
            {
                "icon" => "#f59e0b",
                _ => null,
            },

            Tappable = key => true,
        });
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
        // 🔴 **`NextActor` を呼ばない**（2026-08-28）。⚠️ あれは名前に反して**進める**関数で、
        //    毒を入れ、強化の残りを減らし、スタンなら手番を捨てる。描くたびに呼んでいたので、
        //    1手のあいだに毒が3〜4回入っていた（`Battle.Standing` の注記）。
        var actor = EggCommand.Core.Battle.Standing(state);
        // 🔴 **手札は必ず味方の技。**⚠️ 前は「いま立っている者」の技をそのまま出していたので、
        //    敵の番になると**敵の技が手札に並んでいた**（2026-08-28・作者の報告）。
        //    ⭐ 敵の番でも札は消さず、次に動かす味方の技を出したまま押せなくする
        //    （消すと札の3枚が丸ごと消えて画面が跳ねる）。
        var hand = EggCommand.Core.Battle.StandingAlly(state);
        // ⭐ 押せるのは「味方が実際に立っていて、それが手札の主」のときだけ
        bool mine = actor != null && actor.Side == Side.Ally && ReferenceEquals(actor, hand);
        var skills = hand != null ? Creatures.SkillsOf(hand.Creature) : new Skill?[3];
        bool done = state.Result != null;

        var sheet = LayoutStore.Of("battle");
        // 🔴 **立ち位置の高さは骨組みから読む**（2026-08-30）。⚠️ 前はここに `1278` と
        //    **写しが埋め込んであった**ので、`battle.txt` の `allies`/`foes` を広げても
        //    実物は動かなかった（骨組みが座標を持つ、というこの作品の約束が片側だけ破れていた）。
        //    ⭐ `Stands.Lay` はこの高さから体の大きさを逆算するので、ここがずれると
        //    「骨組みでは届いているのに、実物は技の札に潜る」が起きる。
        float room = HostHigh(sheet, "allies");

        return LayoutDom.Render(sheet, new DomFill
        {
            Inside = key => key switch
            {
                "allies" => Column(s, allies, 540, room, false, actor),
                "foes" => Column(s, foes, 540, HostHigh(sheet, "foes"), true, actor),
                _ => "",
            },

            Text = key => key switch
            {
                // ⭐ **オートは入切の札。**⚠️ 「オート」とだけ書いていた頃は、
                //    押しても**いまどちらなのか画面のどこにも出ていなかった**
                //    （Unity 版は字と色の両方で出している）。
                "pick" => s.Auto ? "オート  ON" : "オート  OFF",
                _ => Slot(key) is (int n, string what)
                    ? SkillWord(skills, hand, n, what) : "",
            },

            // ⭐ CT の丸薬は濃紺・字は白。⚠️ 同じ色を2か所に書かない
            // 🔴 **技の札の地そのものを属性の色に**（2026-08-29・作者の指示「属性の色に」）。
            //    ⭐ BOX の札（`panel.txt` の s0/s1/s2）が既にそうしている ──
            //    出所は `Face.Tint` の "s0"/"s1"/"s2" と同じ `ElementCss` で、
            //    「属性の丸と技の札の地は同じ色」という約束の**戦闘側だけが抜けていた**。
            // ⭐ 技のラベルは手札の主（その個体）の属性の色（作者の指示 2026-08-29）。
            //    ⚠️ 地が薄い属性色で塗られたので、字まで同じ薄さだと沈む ──
            //    読める濃さの `Face.ElementInk` を使う（濃さの計算の出所は Face 側の1つだけ・
            //    BOX の札 `panel.txt` の s0name/s1name/s2name と同じ関数）。
            Tint = key => key.EndsWith("pill") ? "#2b3350"
                : key.EndsWith("ct") ? "#ffffff"
                : key.EndsWith("name") && hand != null ? Face.ElementInk(hand.Creature.Element)
                : Slot(key) is (int, "") && hand != null ? Face.ElementCss(hand.Creature.Element)
                : null,

            // ⭐ 入っているあいだは主役に立てる（字だけだと遠目に読めない）
            Lead = key => key == "pick" && s.Auto,

            When = key => key switch
            {
                "done" => done,
                // ⭐ 敵の番は札を出さない（押せない札を出しておくと、押せるのに反応しないように見える）
                "s0" => mine && skills[0] != null,
                "s1" => mine && skills.Length > 1 && skills[1] != null,
                "s2" => mine && skills.Length > 2 && skills[2] != null,
                _ => false,
            },

            // ⚠️ CT が残っている技は押せない。⭐ **敵の番も押せない**（`mine`）
            Tappable = key => Slot(key) is (int n, "") ? mine && Ready(hand, n) : true,
        });

        static (int, string)? Slot(string? key)
        {
            if (key == null || key.Length < 2 || key[0] != 's' || key[1] < '0' || key[1] > '2')
                return null;
            return (key[1] - '0', key.Substring(2));
        }

        static bool Ready(Unit? actor, int slot) =>
            actor != null && slot < actor.Cooldowns.Length && actor.Cooldowns[slot] <= 0;

        /// <summary>その `host` の丈。⚠️ 骨組みに無ければ、これまでの数（1278）へ落とす
        /// ── 名前を打ち間違えて**画面が消える**より、少し狭いほうがまだ直せる。</summary>
        static float HostHigh(Layout sheet, string name)
        {
            foreach (var node in sheet.Roots) if (node.Name == name) return node.Height;
            return 1278f;
        }

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
    private static string Column(Shell s, List<Unit> units, float wide, float room, bool foe,
        Unit? actor)
    {
        var spots = Stands.Lay(units.Count, wide, room);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            bool alive = EggCommand.Core.Battle.IsAlive(u);
            // ⭐ **狙い先の印**（2026-08-29）。⚠️ 倒れた体には出さない ── 鍵の後始末は
            //    `Deeds.ForgetDeadAims` が次の拍でやるが、印は倒れたその拍で消したい。
            bool aimed = alive && u.Key == (foe ? s.AimFoe : s.AimAlly);
            // ⭐ 絵の並び用（構造化）。⚠️ 字を返す `ActiveStatuses` は Unity の
            //    `UnitStand` がまだ読むので、そちらは触らない（Battle.cs 参照）。
            var badges = EggCommand.Core.Battle.ActiveStatusBadges(u);
            // ⚠️ 🔴 **unit.txt の `sicon`/`scount` の `cols=`/`max=` と同じ数にすること。**
            //    ⭐ 唯一の出所は骨組みだが、繰り返しの個数はここ（Fill 側）でしか
            //    決められない（`Count` が返す数がそのまま描かれる枚数になる）。
            // ⭐ 64px の4枠。5個以上は実絵を3枚にして最後の1枠を +N に譲る。
            //    ⚠️ ここは `unit.txt` の sicon/scount の cols=4、smore の4枠目と対になる。
            const int slots = 4;
            int shown = badges.Count > slots ? slots - 1 : slots;
            int cur = -1;   // ⭐ 「状態の何番目を描いているか」（At が置く）
            // ⚠️ **側も名前に入れる。**⭐ 番号だけだと味方の1体目と敵の1体目が同じ id になる
            sb.Append(Stands.One(spots[i], (foe ? "f" : "a") + i, new DomFill
            {
                Text = key => key switch
                {
                    // ⭐ 数は右上の小丸へ。％やスタック数（Text）は絵の下へは出さない。
                    "snum" => cur >= 0 && cur < badges.Count ? badges[cur].Turns : "",
                    "smoret" => badges.Count > slots ? "+" + (badges.Count - shown) : "",
                    // ⭐ **属性の玉の中にレベル**（2026-08-30・作者の指示）。⚠️ 「Lv」は書かない
                    //    ── 44px の玉に入る字は2桁が精一杯で、冠を付けると数が読めなくなる。
                    //    ⭐ 玉が属性、数がレベル ── 位置で意味が決まるので冠は要らない。
                    "elv" => Levels.Of(u.Creature).ToString(),
                    _ => "",
                },
                Sprite = key => key == "art" ? Creatures.SpeciesOf(u.Creature).Sprite : null,
                Palette = key => key == "art" ? Creatures.PaletteOf(u.Creature) : null,
                Pic = key => key == "sicon" && cur >= 0 && cur < badges.Count
                    ? EggCommand.Core.Art.StatusIcon(badges[cur].Kind, badges[cur].Good) : null,
                Ratio = key => key switch
                {
                    // ⭐ 影は「全部塗った帯」（`bar` を丸薬型の面として使う ── `unit.txt` 参照）
                    "shade" => 1,
                    "hp" => u.MaxHp > 0 ? Math.Clamp(u.Hp / (double)u.MaxHp, 0, 1) : 0,
                    // ⭐ 刻みの端数まで出す（`Deeds.Bars` と同じ式 ── 出所を2つにしない）
                    "gauge" => Deeds.GaugeAt(s, u),
                    _ => 0,
                },
                Tint = key => key switch
                {
                    // ⭐ 生きていれば味方は緑・敵は赤。⚠️ 倒れたら沈める
                    "hp" => !alive ? "#636980" : foe ? "#e04f5f" : "#2fa84a",
                    "elem" => Face.ElementCss(u.Creature.Element),
                    "beats" => Face.ElementCss(SpeciesTable.Beats(u.Creature.Element)),
                    "glow" => "rgba(255,217,77,.55)",
                    // ⭐ 絵の下の影（`unit.txt` の `shade`）── 濃さの出所は Face の1つだけ
                    "shade" => Face.ShadowCss,
                    // ⭐ 狙い先の棒は HP 帯と**同じ2色**（味方は緑・敵は赤）。
                    //    ⚠️ 新しい色を作らない ── 増やすと「この色は何の色か」が増える
                    "aim" => foe ? "#e04f5f" : "#2fa84a",
                    // ⭐ 既存の良い側／悪い側の色を小丸へ移す。字はどちらも白にして読ませる。
                    "sback" => cur >= 0 && cur < badges.Count
                        ? (badges[cur].Good ? "#1e7a38" : "#c0303f") : null,
                    "snum" => cur >= 0 && cur < badges.Count ? "#fff" : null,
                    "smoreback" => "#636980",
                    _ => null,
                },
                // ⭐ Down の拍だけ墓を伏せる。`fx.js` が砂煙の後に直接現し、次の Draw
                //    では Spill が札を外すので通常の墓へ戻る。
                Fade = key => key == "grave" && s.PendingGraves.Contains((foe ? "f" : "a") + i) ? 0 : null,
                Count = key => key == "status" ? Math.Min(badges.Count, shown) : 0,
                At = (key, idx) => { if (key == "status") cur = idx; },
                // 🔴 **これが無いと `tap=` は `data-tap` にならない**（`LayoutDom` の
                //    `live` は `Tappable` が **null でないこと**まで見る）。
                // ⭐ 狙えるのは生きている体だけ ── 倒れた体は押しどころごと消える
                //    （押せるのに何も起きない、を作らない）。
                Tappable = key => key != "aim" || alive,
                When = key => key switch
                {
                    "foe" => foe,
                    "allyalive" => !foe && alive,
                    "foealive" => foe && alive,
                    "allydying" => !foe && !alive && s.PendingGraves.Contains("a" + i),
                    "foedying" => foe && !alive && s.PendingGraves.Contains("f" + i),
                    "allydead" => !foe && !alive,
                    "foedead" => foe && !alive,
                    // ⭐ いま手番が回っている体を光らせる
                    "actor" => actor != null && actor.Key == u.Key,
                    // ⭐ 狙い先の印（棒8本）── 出す・出さないはここ1か所で決める
                    "aim" => aimed,
                    "smore" => badges.Count > slots,
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
            // ⭐ 升は絵を切って見せる（`cell.txt` の `crop=`）── その「見せどころ」。
            Focus = key => key is "cellA-art" or "cellB-art" ? Cell(one).Focus : null,

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
                // ⚠️ 🔴 **判断は `Clocks` に1本化**（1秒ごとの差し替え `AppPage.BeatIdle` と
                //    出所を分けない ── 分けると2か所目になる）。
                "card-left" => Clocks.NestText(s, at),
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
            // ⚠️ 🔴 **`Clocks.NestRatioOf`/`Clocks.NestLeftOf` を読む**（帯・字色・
            //    `card-left` の字が同じ「残り秒」を見るので、ここで計算し直さない）。
            Ratio = key => key == "card-track" ? Clocks.NestRatioOf(s, at) : 0,
            Tint = key => key switch
            {
                "card-track" => Clocks.NestRatioOf(s, at) <= 0.25 ? "#e04f5f" : "#2fa84a",
                "card-left" => Clocks.NestTint(s, at),
                "card-raids" => Steal.IsSealed(raids) ? "#c0303f" : null,
                _ => null,
            },

            Tappable = key => true,
        });
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
        // 🔴 **倍率は出さない**（2026-08-30・作者の指示「倍率非表示」）。⭐ 呼び名だけの
        //    `PowerLabelOf` を使う ── 図鑑と wiki の表は `PowerOf`（数つき）のまま。
        var power = SkillText.PowerLabelOf(skill);
        int slot = s.SkillSlot;
        int level = Math.Max(1, s.SkillLevel);

        return LayoutDom.Render(LayoutStore.Of("skillinfo"), new DomFill
        {
            Text = key => key switch
            {
                "name" => skill.Name,
                // ⭐ Lv・CT・威力を1行に。⚠️ 3行に割ると札より縦に長い覆いになる
                // ⚠️ 上限は技ごと（Skills.MaxLevelOf）。グローバルな Skills.MaxLevel は
                //    「どの技もこれを超えない」全体の天井であって、個々の技の上限ではない
                // ⭐ **区切りは全角2つ**（2026-08-30・作者の指示「間隔狭く見づらい」）。
                //    ⚠️ 1つだと「Lv 1 / 5　CT 0　威力（中）」が一続きの字に見え、
                //    どこまでが1項目なのか読み取れない。⭐ 威力の前に空白を入れない
                //    ── `PowerLabelOf` は「（中）」と括弧から始まるので、
                //    空けると括弧が宙に浮く。
                "meta" => $"Lv {level} / {Skills.MaxLevelOf(skill)}"
                    + $"　　CT {(slot == 0 ? 0 : skill.Ct)}"
                    + (power.Length > 0 ? $"　　威力{power}" : ""),
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
    /// <summary>輪の外のものをまとめた小窓（ホーム右上の入口から）。
    /// ⭐ 字も条件も持たない ── 4つの行き先は骨組み（`menu.txt`）に書いてあるだけで、
    /// ここで足す物が無い。⚠️ それでも `Sheets` を通す ── 描き方の出所を1つに保つため。</summary>
    public static string Menu(Shell s, string crown = "") =>
        LayoutDom.Render(LayoutStore.Of("menu"), new DomFill { Tappable = key => true }, crown: crown);

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
                // ⭐ この世界の番号（旧 ホーム上帯 ── 2026-08-29 にここへ移した）。
                //    ⚠️ 端末ごとに違う世界を引くので「この盤面で起きた」と伝える手立てが
                //    これしか無い。16進なら8桁で口に出せる。
                "world" => "世界 #" + s.Game.Seed.ToString("X8"),
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
    /// ⚠️ 回っている間の面は**乱数を引かない**（立体が転がるだけ）。</summary>
    public static string Dice(Shell s, string crown = "") =>
        LayoutDom.Render(LayoutStore.Of("dice"), new DomFill
        {
            Inside = key => key == "face" ? DieCube(Math.Clamp(s.Dice, 1, Trail.Pips)) : "",
            Tappable = key => false,
        }, crown: crown);

    /// <summary>立体のさいころ1個（2026-08-28・作者の指示「さいころを3D表示に」）。
    ///
    /// 🔴 **出た目は必ず正面**に置く。⭐ そうすると**止まる向きは常に「回転なし」**で済み、
    /// 意匠（`stage.css`）は出目を1つも知らなくてよい ── 目ごとに着地の角度を用意して
    /// 取り違える、という事故が起こりようがない。
    /// ⚠️ 逆に「面の並びを固定して、出目に合わせて止める角度を変える」形にすると、
    /// 6通りの角度が意匠側に散らばり、出所が2つ（C# と CSS）になる。
    ///
    /// ⭐ **向かい合う面の和は7**（本物のさいころと同じ）。⚠️ ここを守らないと、
    /// 転がっている最中に 1 と 6 が隣り合って見え、さいころに見えなくなる。</summary>
    private static string DieCube(int pips)
    {
        // ⭐ 3組（1-6・2-5・3-4）のうち、正面が使った組を除いた2組を側面と上下へ回す
        int used = Math.Min(pips, Trail.Pips + 1 - pips);
        var rest = new List<int>();
        for (int n = 1; n <= 3; n++) if (n != used) rest.Add(n);

        var sb = new System.Text.StringBuilder();
        sb.Append("<div class=\"die3d\">");
        Face("front", pips);
        Face("back", Trail.Pips + 1 - pips);
        Face("right", rest[0]);
        Face("left", Trail.Pips + 1 - rest[0]);
        Face("top", rest[1]);
        Face("bottom", Trail.Pips + 1 - rest[1]);
        sb.Append("</div>");
        return sb.ToString();

        // ⚠️ 絵は既存の `die-N.png`（白い角丸に、目のところが穴）。
        //    ⭐ 面の地を紙色にして絵を墨で塗ると、**穴から紙が覗いて目になる**
        //    ── 目のための絵を別に作らなくてよい（角丸の丸みも絵と地で揃えてある）。
        void Face(string where, int n) =>
            sb.Append("<div class=\"die3d-face die3d-").Append(where)
              .Append("\"><div class=\"n icon-art\" style=\"left:0;top:0;width:100%;height:100%")
              .Append(";--pic:url(icon/die-").Append(n).Append(".png)\"></div></div>");
    }

    // ── 告知 ────────────────────────────────────────

    /// <summary>短い告知。⭐ 出て、読ませて、自分で消えて、次へ渡す。
    /// ⚠️ **ボタンを置かない** ── 勝ち負けは選択ではなく結果。</summary>
    public static string Banner(Shell s, string crown = "") =>
        LayoutDom.Render(LayoutStore.Of("banner"), new DomFill
        {
            Text = key => key == "line" ? s.Banner ?? "" : "",
            Tappable = key => false,
        }, crown: crown);

    // ── 祝い ────────────────────────────────────────

    /// <summary>手に入れた瞬間・生まれた瞬間の全画面演出。⭐ Unity にあって web に無かった
    /// 最後の演出（`View/Fanfare.cs`）。⚠️ Banner と違って**閉じるまで出しっぱなし**
    /// ── 覆い（`veil`）が押しどころを兼ねる（`tap=cheer`）ので、どこを押しても閉じる。</summary>
    public static string Fanfare(Shell s, string crown = "")
    {
        // ⭐ **重ねて見せる詳細のための顔**（2026-08-30）。⚠️ `Cheer_` は絵と字しか
        //    持たないので、札に出す中身は個体そのものから作る（`CreatureId` で引く）。
        //    ⚠️ 卵の祝い（`IsCreature` が false）では null のまま ── 札を出さない。
        Face? born = null;
        int row = 0;
        if (s.BornLook && s.Cheer_ is Cheer look && look.IsCreature)
        {
            foreach (var one in s.Game.Storage.Creatures)
                if (one.Id == look.CreatureId) { born = new Face(one); break; }
        }

        return LayoutDom.Render(LayoutStore.Of("fanfare"), new DomFill
        {
            // ⚠️ 繰り返し（ステの表）は `sheetp-` の冠で来る ── `use=panel` の約束。
            Count = key => born == null ? 0 : key switch
            {
                "sheetp-stats" => Stats.Keys.Length,
                "sheetp-lines" => Stats.Keys.Length - 1,
                _ => 0,
            },
            At = (key, i) => { if (key == "sheetp-stats") row = i; },

            Text = key => key.StartsWith("sheetp-")
                ? (born == null ? "" : born.Text(key[7..], row))
                : s.Cheer_ is not Cheer c ? "" : key switch
                {
                    "line" => c.Line,
                    "stars" => c.Stars,
                    _ => "",
                },
            Sprite = key => key == "sheetp-art" ? born?.Sprite
                : key == "art" && s.Cheer_ is Cheer c ? c.Art : null,
            Palette = key => key == "sheetp-art" ? born?.Palette
                : key == "art" && s.Cheer_ is Cheer c ? c.Palette : null,
            Tint = key => key.StartsWith("sheetp-") ? born?.Tint(key[7..])
                : key == "burst" && s.Cheer_ is Cheer c ? c.Burst
                // ⭐ 絵の下の影（`fanfare.txt` の `shade`）── 濃さの出所は Face の1つだけ
                : key == "shade" ? Face.ShadowCss : null,
            // ⭐ 影は「全部塗った帯」（`bar` を丸薬型の面として使う ── `unit.txt` 参照）
            Ratio = key => key == "shade" ? 1 : 0,
            When = key => key switch
            {
                // ⭐ **★は卵のときだけ**（誕生では `Cheer.Born` が空文字を渡す）
                "stars" => s.Cheer_ is Cheer c && c.Stars.Length > 0,
                // ⭐ 生まれたその場の「分解」「くわしく見る」（作者の指示 2026-08-29）。
                //    ⚠️ 卵を得たとき（`Cheer.EggGot`）には出さない ── 分解も詳細も
                //    生まれた個体にしか意味が無い。`Cheer.IsCreature` が唯一の出所。
                // ⚠️ **詳細を重ねている間は引っ込める**（2026-08-30）── 出したままだと
                //    札の上に釦が重なる。
                "creature" => s.Cheer_ is Cheer c && c.IsCreature && !s.BornLook,
                // ⭐ 重ねて見せる詳細（`look`）── 個体が引けたときだけ出す
                "look" => born != null,
                _ => key.StartsWith("sheetp-") && born != null && born.Shows(key[7..]),
            },
            Tappable = key => true,
        }, crown: crown);
    }

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
        // 🔴 **並べ替えた順を使う**（2026-08-29）。⚠️ `Game.Eggs` をそのまま出して
        //    `Deeds.Warm` だけ並べ替えると、絵と押しどころの出所が割れる。
        var eggs = s.SortedEggs();
        int at = 0;

        return LayoutDom.Render(LayoutStore.Of("eggpicker"), new DomFill
        {
            Count = key => key == "eggs" ? eggs.Count : 0,
            At = (key, i) => at = i,

            Text = key => key switch
            {
                // ⚠️ 「卵がありません」と書かない。⭐ 数を言えば足りる
                "count" => $"棚の卵 {eggs.Count}",
                // ⭐ 並べ替えの2択（字は `bind` から ── 塗り分けが `bind` 持ちにしか掛からないため）
                "sstar" => "★の多い順",
                "snew" => "入手順",
                "egg-stars" => Rarities.StarsOf(eggs[at].Rarity),
                // 🔴 **素質合計（`egg-wild`）は消した**（2026-08-30・作者の指示
                //    「素質合計は書かない」）。⚠️ `eggcard.txt` の節点も同時に外した
                //    ── 片方だけ残すと誰も読まない枝になる。
                "egg-wait" => Rarities.Clock(Math.Max(1, Rarities.SecondsOf(eggs[at].Rarity))),
                "egg-who" => SpeciesTable.ById(eggs[at].SpeciesId).Name,
                _ => "",
            },

            // 🔴 **種族ごとの卵の絵**（2026-08-29・作者の指示「種族の卵の見た目で表示」）。
            //    ⚠️ 孵化器の巣（`Incubator.cs`）は前からこの絵を出していて、棚だけが
            //    どの卵も同じ汎用の絵（`EggArt.Sprite`）を出していた ── 出所を巣と揃えた。
            //    ⭐ `EggSkins.NameOf` が返すのは焼いた PNG の名前（`sim egg-art` が作る）。
            Pic = key => key == "egg-art" ? EggSkins.NameOf(eggs[at].SpeciesId) : null,
            // 🔴 **属性の色付けは無い**（2026-08-29・作者の指示「属性表示不要」で
            //    `eggcard.txt` の `elem` ごと外した）。⚠️ 節点が消えた側だけ直して
            //    ここに `egg-elem` を残すと、誰も読まない枝になる。

            // ⭐ いま効いている並び順だけ塗る（下の帯のタブと同じ約束・同じ色）。
            //    ⚠️ 字で「（選択中）」と書き足さない ── 色だけで足りる。
            Tint = key => (key == "sstar" && s.EggSort == "star")
                || (key == "snew" && s.EggSort != "star") ? "#f59e0b" : null,

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
            // ⭐ 升は絵を切って見せる（`cell.txt` の `crop=`）── その「見せどころ」。
            Focus = key => key == "cell-art" ? Cell(at).Focus : null,

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
    /// <summary>🔴 **育てる札**（2026-08-26・ARK式の自由配分）。
    ///
    /// ⭐ **点の入口と出口を1枚にまとめてある** ── EXP を点に替える（`levelup`）のと、
    /// 点をステへ振る（`spend`）のが同じ札にある。⚠️ 別々の場所に置くと
    /// 「点は増えたのに何も強くならない」で手が止まる。
    /// ⚠️ **振り直しはできない**ので、押す前に「いまの実値」と「振った点」を並べて出す。</summary>
    public static string Grow(Shell s, string crown = "")
    {
        var one = s.PickedOne();
        if (one == null) return "";
        var now = Creatures.StatsOf(one);
        int left = Creatures.UnspentOf(one);
        int row = 0;

        return LayoutDom.Render(LayoutStore.Of("grow"), new DomFill
        {
            Count = key => key == "stats" ? Stats.Keys.Length : 0,
            At = (key, i) => row = i,
            Text = key => key switch
            {
                "who" => $"{Creatures.SpeciesOf(one).Name}　Lv {Levels.Of(one)}/{Levels.MaxOf(one)}",
                // ⭐ 「強化」の中のタブ（2026-08-30）。⚠️ 字は `bind` から出す
                //    ── 塗り分けが `bind` 持ちにしか掛からないため（骨組みの註と対）。
                "tgrow" => "レベル上げ",
                "ttrain" => "技を鍛える",
                // 🔴 二度手間解消（2026-08-29・作者の指示「点を振る前に点を獲得するのが
                //    二度手間」）── `levelup` 釦を無くし、`spend`（ステを押す）だけで
                //    「EXP → 1点 → そのステへ」が完結する（判断は `Shell.Tap` の
                //    `case "spend"` に1か所だけ）。⭐ ここは「次の1点にいくら要るか」を
                //    1行にまとめて見せる案を選んだ（6本それぞれに同じ値を繰り返すより、
                //    値段は個体ごとに1つ（`Levels.ExpToNext`）なので1か所で言えば足りる）。
                // ⚠️ 振れる点が残っている（`spend` 追加前の古い保存の名残）ときは、
                //    それを優先して見せる ── 黙って EXP から2点目を作ろうとしない。
                "left" => Levels.IsMaxed(one) ? "これ以上は育たない"
                    : left > 0 ? $"振れる点 {left}　／　EXP {Face.Digits(s.Game.Idle.Exp)}"
                    : $"EXP {Face.Digits(s.Game.Idle.Exp)}　／　次の1点 {Face.Digits(Levels.ExpToNext(one))}",
                "gname" => Stats.LabelOf(Stats.Keys[row]),
                "gnow" => Face.Digits(now[Stats.Keys[row]]),
                // ⚠️ 0 を「0」と書かない（`panel.txt` の「強化」列と同じ約束）
                "gpts" => one.Points[Stats.Keys[row]] > 0
                    ? $"振った {one.Points[Stats.Keys[row]]}" : "−",
                "hint" => "⚠️ 振った点は戻せません。"
                    + "配合すると子は 0 から振り直せます。",
                _ => "",
            },
            // ⭐ 押しても成功しないなら押させない ── 振れる点が残っているか、
            //    上限未満で次の1点ぶんの EXP が足りるか、どちらかが要る
            //    （黙って何も起きない釦を出さない、はここでも守る）。
            // ⭐ 開いている側のタブだけ塗る（2026-08-30・「強化」の中のタブ分け）。
            //    ⚠️ 色は下の帯のタブと同じ ── 「いま居る所」の示し方を画面で変えない。
            Tint = key => key == "tgrow" ? "#f59e0b" : null,
            Tappable = key => key != "spend"
                || left > 0 || (!Levels.IsMaxed(one) && s.Game.Idle.Exp >= Levels.ExpToNext(one)),
            // ⚠️ 🔴 **`crown:` で渡す。**位置引数だと `suffix` に入り、
            //    押しどころの番号が `card#0` になって `Shell.Index` が読めなくなる（実際に踏んだ）。
        }, crown: crown);
    }

    public static string Train(Shell s, string crown = "")
    {
        var game = s.Game;
        var one = s.PickedOne();
        var skills = one == null ? new Skill?[3] : Creatures.SkillsOf(one);
        var eggs = game.Eggs;
        int slot = Math.Clamp(s.Slot_, 0, skills.Length - 1);
        bool usable = one != null && skills[slot] != null
            && !SkillCosts.IsMaxed(one.SkillPoints[slot], Skills.MaxLevelOf(skills[slot]!));
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
                // ⭐ 「強化」の中のタブ（2026-08-30・`Sheets.Grow` と同じ字・同じ場所）。
                "tgrow" => "レベル上げ",
                "ttrain" => "技を鍛える",
                "rname" => skills[row]?.Name ?? "—",
                // ⚠️ 上限は技ごと（Skills.MaxLevelOf）。空き枠（skills[row]==null）は
                //    今までどおり全体の天井のまま（points は常に 0 なので Lv1 のまま出る）
                "rlv" => one == null ? "" : skills[row] == null
                    ? $"Lv{SkillCosts.LevelOf(one.SkillPoints[row])}"
                    : $"Lv{SkillCosts.LevelOf(one.SkillPoints[row], Skills.MaxLevelOf(skills[row]!))}",
                // ⭐ あと何ポイントで次かを出す。⚠️ 上限は「上限」と書く（0 と出さない）
                "rneed" => one == null || skills[row] == null ? ""
                    : SkillCosts.IsMaxed(one.SkillPoints[row], Skills.MaxLevelOf(skills[row]!)) ? "上限"
                    : $"あと {SkillCosts.ToNext(one.SkillPoints[row], Skills.MaxLevelOf(skills[row]!))}",
                // ⭐ **入れたあとのレベルまで出す。**⚠️ ポイントだけだと人が計算することになる
                "head" => !usable ? "この枠はもう鍛えられません"
                    : gain > 0
                        ? $"選んだ {s.Feeds.Count}/{Games.PickAtOnce} 個で ＋{gain}　"
                            + $"Lv{SkillCosts.LevelOf(points, Skills.MaxLevelOf(skills[slot]!))}"
                            + $" → Lv{SkillCosts.LevelOf(points + gain, Skills.MaxLevelOf(skills[slot]!))}"
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
            // ⭐ 開いている側のタブ（`ttrain`）と、注ぐ先に選んでいる枠（`row`）を塗る
            //    （2026-08-30・「強化」の中のタブ分け ── `Sheets.Grow` と対）。
            Tint = key => key == "ttrain" || (key == "row" && row == slot && usable)
                ? "#f59e0b" : null,

            When = key => key == "chip-picked" && s.Feeds.Contains(eggs[at].Id),

            // ⚠️ **上限を超える卵は選ばせない。**受け取ると超えた分が黙って消える
            Tappable = key => key switch
            {
                "row" => one != null && skills[row] != null
                    && !SkillCosts.IsMaxed(one.SkillPoints[row], Skills.MaxLevelOf(skills[row]!)),
                "feed" => s.Feeds.Count > 0,
                _ => true,
            },
        }, crown: crown);
    }

    // ── 家系図 ──────────────────────────────────────

    /// <summary>⭐ **何代さかのぼるか。**⚠️ 2＝親と祖父母（3代ぶん）。
    /// 「BOXで2世代以降のキャラクターの家系図を見られるように」（作者の指示）に
    /// 足りる分だけ出す ── それより深く辿っても、墓標の上限（<see cref="Tombs.Limit"/>）に
    /// 先に当たって「不明」ばかりになりやすい。</summary>
    private const int TreeDepth = 2;

    /// <summary>配合で消えた祖先を、墓標から辿って見せる札。
    /// ⚠️ **押しどころは「閉じる」だけ**（読む場所であって選ぶ場所ではない ──
    /// `species.txt` の技の袋と同じ立ち位置）。</summary>
    public static string Tree(Shell s, string crown = "")
    {
        var picked = s.PickedOne();
        if (picked == null) return "<!-- 手持ちが無い -->";
        var nodes = Lineage.Of(s.Game, picked, TreeDepth);

        return LayoutDom.Render(LayoutStore.Of("tree"), new DomFill
        {
            Text = key => TreeText(nodes, key),
            Tappable = key => true,
        }, crown: crown);
    }

    /// <summary>⭐ 骨組みの節点名は「n0」〜「n6」（<see cref="Lineage.Of"/> と同じ並び）に、
    /// 「name」「sub」「gen」の3つの差し口を付けたもの。
    ///
    /// ⚠️ 7×3＝21個を書き並べるのは冗長に見えるが、この家系図は行ごとに幅が違う
    /// ピラミッド型（自分1枚・親2枚・祖父母4枚）── `panel.txt`/`cell.txt` のような
    /// 等間隔グリッド（`repeat=`）に載らないので、骨組み（`tree.txt`）側も
    /// 節点をそのまま7つ書いている。差し込み口をここで素直に7つぶん並べるほうが、
    /// 無理に `repeat=` へ押し込めるより読める。</summary>
    private static string TreeText(Lineage.Node[] nodes, string key)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            string prefix = "n" + i;
            if (key == prefix + "name") return TreeName(nodes[i]);
            if (key == prefix + "sub") return TreeSub(nodes[i]);
            if (key == prefix + "gen") return TreeGen(nodes[i]);
        }
        return "";
    }

    private static string TreeName(Lineage.Node node) =>
        node.Known ? SpeciesTable.ById(node.SpeciesId!).Name : "不明";

    private static string TreeSub(Lineage.Node node) => node.Known
        ? $"{SpeciesTable.LabelOf(node.Element!.Value)}　素質{Face.Digits(node.WildTotal)}"
        : "";

    private static string TreeGen(Lineage.Node node) => node.Known ? $"{node.Generation}代目" : "";

    // ── 図鑑 ────────────────────────────────────────

    /// <summary>⭐ **見たことのある種族だけ名前が出る。**
    /// ⚠️ 伏せた種族も枠は残す ── 何種類いるかは隠さない（集める的が見える）。</summary>
    /// <param name="part">⭐ **後ろに敷かれる側として描くときの出所**（`"book"`）。
    ///
    /// 🔴 `Scenes.Draw("ask")` だけが渡す（2026-08-29）。⚠️ 確かめ札（`ask`）は
    /// 「後ろが押せなくなっているか」を見るために図鑑を敷いて描くが、渡さないと
    /// book.txt の行番号が `data-line` として盤に出て **ask.txt の行番号と衝突**する
    /// ── 実測: 木で ask の `panel`（9行目）を選ぶと、輪は book の `cell#0` に付いていた。
    /// ⭐ 渡すと後ろの図鑑は `data-part="book"` になり、`data-line` は ask のものだけになる。
    /// ⚠️ 遊ぶ画面（`AppPage`/`AskPage`/`BookPage`）は渡さない ── 既定の空のままで、
    /// 出る HTML は1バイトも変わらない。</param>
    public static string Book(Shell s, string part = "")
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
        }, part: part);
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
            // ⭐ **BOX の升と同じ「見せどころ」**（`trial.txt` の `crop=256`・2026-08-30）。
            //    ⚠️ 出所は `SpeciesArt` の1つだけ ── ここで別に決めると、同じ種族が
            //    BOX と試練で違う寄り方をする。
            Focus = key => key == "face"
                ? SpeciesArt.FocusOf(
                    SpeciesTable.ById(Party()[who].SpeciesId).Id,
                    SpeciesTable.ById(Party()[who].SpeciesId).Sprite)
                : null,

            When = key => key == "beaten" && Beaten(),
            Tappable = key => key == "trial",
        });
    }
}
