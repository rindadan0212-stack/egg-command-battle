using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>いま出ている画面。⚠️ Unity 版 `App.Screen` と同じ並び。</summary>
public enum Sheet { Home, Nests, Breed, Box, Book, Fight, Raid }

/// <summary>覆いで前に出る札。⚠️ 画面とは別に数える（後ろの画面は残る）。</summary>
public enum Panel { None, Party, Species, Skill, Eggs, Fuse, Train }

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

    /// <summary>いまの戦い。⚠️ 無ければ戦っていない。
    /// ⭐ 名前に `_` が付いているのは、Core の `Battle` と見分けるため。</summary>
    public BattleState? Fight_;

    /// <summary>いまの潜入。⚠️ 無ければ潜っていない。</summary>
    public Raid? Raid_;

    /// <summary>いま挑んでいる巣。⚠️ ヌシのときは null。</summary>
    public Nest? Nest_;
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
                Open = Panel.None;
                Now_Sheet = i switch
                {
                    1 => Sheet.Nests, 2 => Sheet.Breed, 3 => Sheet.Box, _ => Sheet.Home,
                };
                break;

            case "back": Open = Panel.None; Now_Sheet = Sheet.Home; break;
            case "close": Open = Panel.None; break;

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

            // ⭐ 空き枠を押したら、そのとき初めて卵の在庫が開く（棚を常に出しておかない）
            case "slot": Deeds.Slot(this, i); break;
            case "egg": Deeds.Warm(this, i); break;
            case "train": Open = Panel.Train; break;
            case "fuse": Open = Panel.Fuse; break;
        }
    }

    /// <summary>一覧の升を押した。⭐ BOX は「見る」だけ・配合は親を出し入れする。</summary>
    private void Choose(int i)
    {
        var list = Sorted();
        if (i < 0 || i >= list.Count) return;
        string id = list[i].Id;

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
        Sheet.Fight => Boss ? Nests.BossName : "戦闘",
        Sheet.Raid => Nest_ != null ? Nest_.Name : "強奪",
        _ => "",
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
