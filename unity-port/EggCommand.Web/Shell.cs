using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>いま出ている画面。⚠️ Unity 版 `App.Screen` と同じ並び。</summary>
public enum Sheet { Home, Nests, Breed, Box, Book, Fight, Raid, Trial }

/// <summary>覆いで前に出る札。⚠️ 画面とは別に数える（後ろの画面は残る）。</summary>
public enum Panel { None, Party, Species, Skill, Eggs, Fuse, Train, Ask, Keep }

/// <summary>アプリ1つぶんの状態と、そこから出る画面。
///
/// ⭐ **画面ごとの「どの値をどの差し口へ流すか」は、ここが唯一の出所。**
/// ⚠️ 確かめ用の頁（`/box` など）も、遊ぶ頁（`/app`）も、同じここを通る
/// ── 分けて書くと、片方だけ直した日に**確かめている画面と遊ぶ画面が違うもの**になる。
///
/// ⚠️ ここに座標は1つも無い。位置は `Assets/Resources/Layouts/*.txt` が持つ。</summary>
public sealed class Shell
{
    public Game Game;
    public long Now;

    public Sheet Now_Sheet = Sheet.Home;
    public Panel Open = Panel.None;
    /// <summary>その札の**下に居た**札。⭐ 技の詳細は図鑑の種族の上にも出るので、
    /// 閉じたときに戻る先が要る。⚠️ **1枚だけ**覚える
    /// ── 積み重ねを作ると「どこまで戻るのか」が読めなくなる。</summary>
    public Panel Under = Panel.None;

    // ── 一覧の並べ替え（BOX・配合・編成で共通）──────────
    public FilterKey Filter = FilterKey.All;
    public SortKey Sort = SortKey.WildTotal;
    public SortBasis Basis = SortBasis.Born;
    /// <summary>⚠️ 開いた状態を**覚えない**のが Unity 版の決めごとだが、
    /// ここは1つの器で持ち回るので、画面を離れるときに畳む。</summary>
    public bool SortOpen;

    /// <summary>BOX でいま見ている個体。</summary>
    public string? Picked;
    /// <summary>配合の親2体。</summary>
    public string? ParentA, ParentB;

    /// <summary>分解でえらんだ個体。⭐ **押した順に入る**（`Games.PickAtOnce` 体まで）。
    /// ⚠️ 押した瞬間には減らさない ── 最後に「分解する」を押すまで戻せる。</summary>
    public readonly List<string> Melts = new();
    /// <summary>技を鍛えるでえらんだ卵。
    /// ⚠️ **個体の側（<see cref="Melts"/>）と混ぜない** ── 別のものを数えている。</summary>
    public readonly List<string> Feeds = new();
    /// <summary>技を鍛えるで注ぐ先の枠。</summary>
    public int Slot_;
    /// <summary>編成の札が「放置」か。⚠️ **どちらかは開いた場所で決まる**
    /// （2026-08-21・作者の指示）── 札の中に切り替えは無い。</summary>
    public bool IdleParty;

    /// <summary>図鑑で開いている種族（`SpeciesTable.All` の番号）。</summary>
    public int SpeciesAt;
    /// <summary>長押しで開いている技。⚠️ 無ければ null。</summary>
    public string? SkillId;
    /// <summary>その技が入っている枠。⚠️ **枠1（0）は CT を 0 で出す。**</summary>
    public int SkillSlot;
    /// <summary>その技のいまのレベル。</summary>
    public int SkillLevel = 1;

    /// <summary>いまの保存の大きさ（字数）。⚠️ 0 なら**まだ1度も書かれていない**。
    /// ⭐ 画面（`save.txt`）に出すためだけに持つ ── 遊びには関わらない。</summary>
    public int SaveSize;
    /// <summary>残っている控えの古さ（秒・新しい順）。</summary>
    public int[] SavePast = Array.Empty<int>();

    /// <summary>いまの戦い。⚠️ 無ければ戦っていない。
    /// ⭐ 名前に `_` が付いているのは、Core の `Battle` と見分けるため。</summary>
    public BattleState? Fight_;

    /// <summary>いまの潜入。⚠️ 無ければ潜っていない。</summary>
    public Raid? Raid_;

    /// <summary>いま挑んでいる巣。⚠️ ヌシのときは null。</summary>
    public Nest? Nest_;
    /// <summary>いま挑んでいる試練。⚠️ 試練でないときは null
    /// ── ⭐ 空にしておかないと、決着のときに巣の後始末（卵・引き直し）が動く。</summary>
    public Trial? Trial_;
    /// <summary>ヌシとの戦いか。</summary>
    public bool Boss;
    /// <summary>雑魚戦のマス。⚠️ -1 なら雑魚戦ではない
    /// （⭐ これで「卵を出すか」が分かれる）。</summary>
    public int Space = -1;
    /// <summary>行ける先。⚠️ null なら選んでいない。</summary>
    public List<List<int>>? Open_;
    /// <summary>孵化器のどの枠へ入れるか。</summary>
    public int Aim;
    /// <summary>狙っている相手と味方。⭐ **別々に覚える**
    /// ── 1つで兼ねると、敵を選んだまま強化を押したときに黙って別の相手へ飛ぶ。</summary>
    public string? AimFoe, AimAlly;
    /// <summary>オートで戦うか。⚠️ 戦闘をまたいで覚えておく。</summary>
    public bool Auto;
    /// <summary>画面に出す一言。⚠️ 黙って変わらないことを防ぐ。</summary>
    public string? Say;

    public Shell(Game game, long now) { Game = game; Now = now; }

    // ── 一覧 ────────────────────────────────────────

    /// <summary>⭐ **絞ってから並べる**（BOX・配合・編成で同じ順）。</summary>
    public IReadOnlyList<Creature> Sorted()
    {
        var pool = Filters.Apply(Game, Game.Storage.Creatures, Filter);
        return Storages.Sorted(new Storage(Game.Storage.Slots, pool), Sort, Basis);
    }

    public Creature? PickedOne()
    {
        foreach (var c in Game.Storage.Creatures) if (c.Id == Picked) return c;
        var list = Sorted();
        return list.Count > 0 ? list[0] : null;
    }

    // ── 押された ────────────────────────────────────

    /// <param name="at">繰り返しの番号（入れ子なら `2#1`）。⚠️ 無ければ空。</param>
    public void Tap(string what, string at)
    {
        int i = Index(at);
        switch (what)
        {
            case "tab":
                // ⚠️ 画面を移るときに並べ替えを畳む（Unity 版と同じ ── 一覧へ戻るたびに
                //    開いていると、見たいのは一覧なのに毎回畳む操作が要る）
                SortOpen = false;
                Open = Under = Panel.None;
                Now_Sheet = i switch
                {
                    1 => Sheet.Nests, 2 => Sheet.Breed, 3 => Sheet.Box, _ => Sheet.Home,
                };
                break;

            case "back": Open = Under = Panel.None; Now_Sheet = Sheet.Home; break;
            // ⚠️ **閉じたら選びかけも捨てる。**⭐ 残すと、次に開いたとき
            //    身に覚えのない個体が選ばれていて、そのまま分解できてしまう
            case "close": Open = Under; Under = Panel.None; Melts.Clear(); Feeds.Clear(); break;

            // ⭐ **右肩は画面ごとに中身が変わる**（Unity 版 `App.ShowExtra`）
            case "extra":
                if (Now_Sheet == Sheet.Home) { Now_Sheet = Sheet.Book; SortOpen = false; }
                else if (Now_Sheet == Sheet.Nests) { IdleParty = false; Open = Panel.Party; }
                break;

            case "bar-toggle": SortOpen = !SortOpen; break;
            case "chips-filter": Filter = Filters.Keys[i]; break;
            case "chips-sort": Sort = Storages.SortKeys[i]; break;
            case "chips-basis": Basis = Storages.Bases[i]; break;

            case "one": Choose(i); break;

            // ── 遊びを動かす ────────────────────────
            case "nest": Deeds.Dive(this, i); break;
            case "boss": Deeds.Boss(this); break;
            case "roll": Deeds.Roll(this); break;
            case "square": Deeds.Step(this, i); break;
            case "pay": Deeds.Pay(this); break;
            case "skip": Deeds.Pass(this); break;
            case "s0": Deeds.Strike(this, 0); break;
            case "s1": Deeds.Strike(this, 1); break;
            case "s2": Deeds.Strike(this, 2); break;
            case "pick": Auto = !Auto; break;
            case "finish": Now_Sheet = Sheet.Nests; break;
            // ⭐ **取り返しがつかないので一度だけ確かめる**（押し間違いで負けにしない）
            case "give": if (Fight_ != null) Open = Panel.Ask; break;
            case "stop": Open = Panel.None; break;
            case "go": Deeds.Concede(this); break;

            // ⭐ 空き枠を押したら、そのとき初めて卵の在庫が開く（棚を常に出しておかない）
            case "slot": Deeds.Slot(this, i); break;
            case "egg": Deeds.Warm(this, i); break;

            // ── 育てる ──────────────────────────────
            // ⚠️ 分解は**開くだけ**。⭐ 減るのは札の中の「分解する」を押したとき
            case "fuse": Melts.Clear(); Open = Panel.Fuse; break;
            case "melt": Deeds.Melt(this); break;
            case "train": Feeds.Clear(); Slot_ = 0; Open = Panel.Train; break;
            case "row": Slot_ = i; Feeds.Clear(); break;
            case "chip": Deeds.Feed_(this, i); break;
            case "feed": Deeds.Feed(this); break;
            case "grow": Deeds.Grow(this); break;

            // ── 配合 ────────────────────────────────
            case "pa": ParentA = null; break;
            case "pb": ParentB = null; break;
            case "breed": Deeds.Breed(this); break;

            // ── 編成 ────────────────────────────────
            // ⭐ ホームからは放置の編成・探索の右肩からは巣の編成
            case "party": IdleParty = true; Open = Panel.Party; break;
            case "set": Deeds.Team(this, i); break;
            case "seat": Deeds.Drop(this, i); break;
            case "done": Open = Under = Panel.None; break;

            // ── 保存の控え ──────────────────────────
            // ⚠️ **出し入れそのものは画面の外**（ブラウザに聞く）ので、
            //    ここは開くだけ。⭐ 実際の読み書きは `AppPage` が持つ。
            case "keep": Open = Panel.Keep; break;

            // ── 図鑑・試練 ──────────────────────────
            case "trials": Now_Sheet = Sheet.Trial; SortOpen = false; break;
            case "trial": Deeds.Trial(this, i); break;
            case "species": SpeciesAt = i; Open = Panel.Species; break;
        }
    }

    /// <summary>**長押し**された。
    ///
    /// ⭐ 押しどころとは別の道（`hold=`）。⚠️ 短く触っても開かない
    /// ── 技の札は押しどころではないので、触っただけで開くと選ぶ指が誤爆する。</summary>
    public void Hold(string what, string at)
    {
        int i = Index(at);
        switch (what)
        {
            // ⭐ BOX の札の技（枠0〜2）。⚠️ **いま見ている個体の**技とレベルを出す。
            //    ⚠️ 名前に `detail-` が冠されている（`use=panel` で差した部品なので）
            case "detail-s0": Peek(0); break;
            case "detail-s1": Peek(1); break;
            case "detail-s2": Peek(2); break;

            // ⭐ 種族の札の抽選（枠1〜3）。⚠️ こちらは個体ではないので Lv は 1
            case "skill1": Pool(0, i); break;
            case "skill2": Pool(1, i); break;
            case "skill3": Pool(2, i); break;
        }

        void Peek(int slot)
        {
            var one = PickedOne();
            if (one == null) return;
            var skills = Creatures.SkillsOf(one);
            if (slot >= skills.Length || skills[slot] == null) return;
            SkillId = skills[slot]!.Id;
            SkillSlot = slot;
            SkillLevel = SkillCosts.LevelOf(one.SkillPoints[slot]);
            Under = Open;
            Open = Panel.Skill;
        }

        void Pool(int slot, int n)
        {
            var all = SpeciesTable.All;
            var pool = Sheets.PoolOf(all[Math.Clamp(SpeciesAt, 0, all.Count - 1)], slot);
            if (n < 0 || n >= pool.Count || !Skills.Has(pool[n])) return;
            SkillId = pool[n];
            SkillSlot = slot;
            SkillLevel = 1;
            Under = Open;
            Open = Panel.Skill;
        }
    }

    /// <summary>一覧の升を押した。
    ///
    /// ⚠️ **同じ升が、開いている札しだいで別のことをする。**
    /// ⭐ 前に出ている札が先（後ろの画面は隠れているので押せない）。</summary>
    private void Choose(int i)
    {
        // ⚠️ 分解の候補は**見ている本人を外した**並びなので、番号の意味が違う
        if (Open == Panel.Fuse) { Deeds.Mark(this, i); return; }

        var list = Sorted();
        if (i < 0 || i >= list.Count) return;
        string id = list[i].Id;

        if (Open == Panel.Party)
        {
            Games.TogglePartyMember(Game, id, IdleParty ? PartyKind.Idle : PartyKind.Nest);
            return;
        }
        if (Now_Sheet == Sheet.Breed)
        {
            if (id == ParentA) ParentA = null;
            else if (id == ParentB) ParentB = null;
            else if (ParentA == null) ParentA = id;
            else ParentB = id;
            return;
        }
        // ⭐ 一覧を押すのは「見る」だけ。押すたびに意味が変わる画面にしない
        Picked = id;
    }

    /// <summary>⚠️ 入れ子の番号は `2#1`。⭐ ここでは**いちばん外側**を読む。</summary>
    private static int Index(string at)
    {
        if (string.IsNullOrEmpty(at)) return -1;
        int cut = at.IndexOf('#');
        return int.TryParse(cut < 0 ? at : at.Substring(0, cut), out int n) ? n : -1;
    }

    // ── 外枠 ────────────────────────────────────────

    /// <summary>上のバーと下の帯。⚠️ 画面より**後**に描く（帯の下に潜らせない）。</summary>
    public string Frame()
    {
        int tab = 0;
        var counts = new[]
        {
            // ⚠️ 0 を出さない。⭐ 数が無いことは**書かない**ことで伝わる
            Game.Incubating.Count > 0 ? $"{Game.Incubating.Count}" : "",
            $"{Game.Encounters.Count}",
            $"{Game.Storage.Creatures.Count}体",
            $"{Game.Storage.Creatures.Count}/{Game.Storage.Slots}",
        };
        var names = new[] { "ホーム", "探索", "配合", "BOX" };
        int here = Now_Sheet switch
        {
            Sheet.Nests => 1, Sheet.Breed => 2, Sheet.Box => 3, Sheet.Home => 0, _ => -1,
        };

        return LayoutDom.Render(LayoutStore.Of("frame"), new DomFill
        {
            Count = key => key == "tabs" ? names.Length : 0,
            At = (key, i) => tab = i,
            Text = key => key switch
            {
                "title" => Title(),
                "badge" => Badge(),
                "extra" => Extra() ?? "",
                "tname" => names[tab],
                "tcount" => counts[tab],
                _ => "",
            },
            // ⭐ いま居るタブだけ塗る
            Tint = key => key == "tab" && tab == here ? "#f59e0b" : null,
            // ⚠️ タブのある画面に ‹ を出さない ── タブが行き先を全部持っている
            When = key => key switch
            {
                // ⚠️ **戦闘中と潜入中は戻れない** ── 抜けられると、
                //    不利な盤面をいつでも無かったことにできてしまう。
                "dock" => Now_Sheet is not (Sheet.Fight or Sheet.Raid),
                // ⭐ タブに乗っていない画面（図鑑・試練）だけ ‹ を出す
                "back" => Now_Sheet is Sheet.Book or Sheet.Trial,
                "extra" => Extra() != null,
                _ => false,
            },
            Tappable = key => true,
        });
    }

    private string Title() => Now_Sheet switch
    {
        Sheet.Home => "EGG COMMAND",
        Sheet.Nests => "探索",
        Sheet.Breed => "配合",
        Sheet.Box => "BOX",
        Sheet.Book => "図鑑",
        Sheet.Trial => "試練",
        Sheet.Fight => Boss ? Nests.BossName : "戦闘",
        Sheet.Raid => Nest_ != null ? Nest_.Name : "強奪",
        _ => "",
    };

    /// <summary>右肩に出す入口。⚠️ **本体に置けなかったものだけ**が来る
    /// （Unity 版 `App.ShowExtra` と同じ役目）。⭐ 無ければ右肩は字に戻る。</summary>
    private string? Extra() => Now_Sheet switch
    {
        Sheet.Home => "図鑑",
        // ⭐ **巣を選ぶ前に編成を決める。**⚠️ 潜ってから「違った」と気づいても戻れない。
        //    ⚠️ 本体には置けない ── 巣の札が縦を埋めていて、どこに置いても重なる。
        Sheet.Nests => "パーティ編成",
        _ => null,
    };

    /// <summary>右肩の字。
    ///
    /// ⭐ **溜まっている EXP を BOX と配合に出す**（2026-08-21・作者の指示）。
    /// ⚠️ ホームにしか出ていなかったので、BOX で「Lv ＋1 EXP 122」を見ても
    /// 足りているのかが分からなかった。
    ///
    /// ⚠️ **ホームには出さない。**⭐ 画面の中の帯が既に同じ数を出していて、
    /// 並べると同じ字が2つ見える（実測 2026-08-22）。
    /// ⚠️ **体数もここに出さない。**⭐ 下のタブが既に出している
    /// （「BOX 44/50」「配合 44体」）── 両方出すと枠に入らない
    /// （実測「EXP 19,475　44/50」は 315 要るのに枠は 252）。</summary>
    private string Badge() => Now_Sheet switch
    {
        Sheet.Box or Sheet.Breed => $"EXP {Face.Digits(Game.Idle.Exp)}",
        _ => "",
    };
}
