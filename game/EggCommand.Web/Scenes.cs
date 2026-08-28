using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>骨組み33枚ぶんの「場面」の対応表。
///
/// ⭐ **この頁（`/edit`）が触れる骨組みは、ここが唯一の出所。**⚠️ どの実データで・
/// どの画面の中に描くかを、あちこちに書き散らさない ── `EditPage.razor` はここを
/// 引くだけにする（前は `EditPage` 自身が持っていた4枚だけの `Dispatch` を、
/// 32枚ぶんに広げてここへ集めた）。
///
/// ⚠️ **`Sheets.cs` は1文字も変えない。**ここは「確かめ用の頁」（`Pages/*Page.razor`）
/// と同じ組み方（`Demo.Game()` の固定 seed）で `Sheets.*` を呼ぶだけ ──
/// 実データは各頁の `OnParametersSet`（既定値・クエリ無し相当）から集めてきた。</summary>
public static class Scenes
{
    /// <summary>骨組み1枚ぶんの「どこから来て・どこに描くか」。</summary>
    public readonly struct Scene
    {
        /// <summary>骨組みの id（`Assets/Resources/Layouts/&lt;Id&gt;.txt`）。</summary>
        public readonly string Id;
        /// <summary>描くときの土台。⚠️ 単独で描けるなら <see cref="Id"/> と同じ。
        /// ⭐ 部品（`use=` で差し込まれる・コードから描かれる）は、差されている側の画面。</summary>
        public readonly string HostId;
        /// <summary>一覧に出す短い日本語。</summary>
        public readonly string Why;
        /// <summary>⭐ **「部品」（土台の中でしか描けない13枚）か。**
        ///
        /// ⚠️ **`HostId != Id` とは別の軸。**`frame` は `HostId == Id`（`Shell` が直に描く・
        /// 差し込まれる土台が無い）のに、`Sheets.*` を経由しないので「画面」ではなく
        /// こちら側 ── `EditPage` の `<optgroup>` はこのフラグだけで分ける。</summary>
        public readonly bool IsPart;
        /// <summary>⭐ **選ぶときの探し方が `data-part` か。**
        ///
        /// `true` は `use=` で差し込まれた9部品だけ（`panel`/`panelmini`/`cell`/`sortbar`/
        /// `sortchips`/`eggcard`/`eggchip`/`encounter`/`slot`）── `Layouts.Rename` を通るので
        /// 差し込まれた側の節点は `LineNumber` を失い、代わりに `PartId`/`PartLine` を持つ
        /// （`LayoutDom.cs` が `data-part`/`data-part-line` を出す）。
        ///
        /// `false` は単独の20枚と、**コードから描かれる4枚**（`unit`/`square`/`walker`/`frame`）。
        /// ⚠️ この4つは `use=` を一度も通らない ── `Stands`/`Board`/`Idle`/`Shell` が
        /// `LayoutStore.Of("unit"|"square"|"walker"|"frame")` を**独立した骨組みとして**
        /// 直接 `LayoutDom.Render` するので、差し込まれた側でも自分の行番号（`data-line`）
        /// をそのまま持つ ── 今までどおりの探し方で選べる。</summary>
        public readonly bool ByPart;

        public Scene(string id, string hostId, string why, bool isPart, bool byPart)
        {
            Id = id;
            HostId = hostId;
            Why = why;
            IsPart = isPart;
            ByPart = byPart;
        }
    }

    private static Scene S(string id, string hostId, string why, bool isPart = false, bool byPart = false) =>
        new(id, hostId, why, isPart, byPart);

    /// <summary>⭐ 33枚ぶん。⚠️ 実物のファイル名と1対1（増減したら
    /// <c>ScenesTests</c> の検査が落ちる）。
    ///
    /// ⭐ 前半20枚が「画面」（<c>HostId == Id</c>・`Sheets.*` を直に呼べる）、
    /// 後半13枚が「部品」（土台の中でしか描けない）── `EditPage` の一覧はこの並びのまま
    /// `optgroup` へ分ける。</summary>
    public static readonly IReadOnlyList<Scene> All = new[]
    {
        // ── 画面（20枚） ──────────────────────────────
        S("box", "box", "BOX"),
        S("home", "home", "ホーム"),
        S("nests", "nests", "探索"),
        S("breed", "breed", "配合"),
        S("fuse", "fuse", "分解"),
        S("skillegg", "skillegg", "技を鍛える"),
        S("grow", "grow", "育てる（点をステへ振る）"),
        S("skillinfo", "skillinfo", "技の詳細"),
        S("species", "species", "種族の札"),
        S("book", "book", "図鑑"),
        S("save", "save", "保存の控え"),
        S("ask", "ask", "確かめる（あきらめますか）"),
        S("eggpicker", "eggpicker", "卵を選ぶ"),
        // ⚠️ `battle` は `Sheets.Fight` の一択（曖昧さなし）。`Sheets.Raid` が描くのは
        //    「battle」ではなく「trail」骨組み（`LayoutStore.Of("trail")`）── 別の骨組み。
        S("battle", "battle", "戦闘"),
        S("party", "party", "編成（巣）"),
        S("partyidle", "partyidle", "編成（放置）"),
        S("trial", "trial", "試練"),
        S("trail", "trail", "すごろく（潜入）"),
        S("banner", "banner", "告知（WIN/LOSE）"),
        S("dice", "dice", "さいころ"),
        // 🔴 **足し忘れていた。**⚠️ `banner` と同じ「単独で描ける画面」（`Sheets.Fanfare` が
        //    `LayoutStore.Of("fanfare")` を直に呼ぶ）なのに、この表に無いせいで `/edit` から
        //    開けず、`?of=fanfare` は黙って `box` にフォールバックしていた
        //    （2026-08-25 監査で発覚。骨組みは33枚あるのに、ここは32件しか無かった）。
        S("fanfare", "fanfare", "祝い（手に入れた・生まれた瞬間の全画面演出）"),

        // ── 部品（13枚） ──────────────────────────────
        // ⭐ 複数の土台を持つ部品は1つを選ぶ（他の土台でも使われている旨を Why に残す）。
        S("panel", "box", "個体の詳細札", isPart: true, byPart: true),
        S("panelmini", "breed", "配合の親札", isPart: true, byPart: true),
        // ⭐ cell/sortbar/sortchips は box が一番数が並ぶ（4列×4段=16升）── はみ出しが見える。
        //    他に breed / fuse(cell のみ) / party / partyidle でも使われる。
        S("cell", "box", "一覧の升（他に breed/fuse/party/partyidle でも使用）", isPart: true, byPart: true),
        S("sortbar", "box", "並べ替え・畳んだ帯（他に breed/party/partyidle でも使用）", isPart: true, byPart: true),
        S("sortchips", "box", "並べ替え・開いた選択肢（他に breed/party/partyidle でも使用）", isPart: true, byPart: true),
        S("eggcard", "eggpicker", "卵を選ぶ札", isPart: true, byPart: true),
        S("eggchip", "skillegg", "強化に使う卵の札", isPart: true, byPart: true),
        S("encounter", "nests", "巣の札", isPart: true, byPart: true),
        S("slot", "home", "孵化器の枠", isPart: true, byPart: true),
        // ⭐ コードから描かれる4枚（`use=` を通らない・data-line のまま。ByPart は既定 false）。
        S("unit", "battle", "戦闘の立ち位置（Stands.cs）", isPart: true),
        S("square", "trail", "すごろくのマス（Board.cs）", isPart: true),
        S("walker", "home", "放置の歩く駒（Idle.cs）", isPart: true),
        S("frame", "frame", "外枠・タブと上のバー（Shell.cs が直に描く・土台になる画面が無い）", isPart: true),
    };

    private static readonly Dictionary<string, Scene> ById = BuildIndex();

    private static Dictionary<string, Scene> BuildIndex()
    {
        var map = new Dictionary<string, Scene>();
        foreach (var s in All)
        {
            // ⚠️ 名前の重なりは黙って後勝ちにしない（骨組みの Parse と同じ姿勢）。
            if (map.ContainsKey(s.Id))
                throw new InvalidOperationException($"Scenes: id が重なっている「{s.Id}」");
            map[s.Id] = s;
        }
        return map;
    }

    public static Scene Of(string id)
    {
        if (ById.TryGetValue(id, out var s)) return s;
        throw new KeyNotFoundException(
            $"場面が無い: {id}（在るもの: {string.Join(", ", ById.Keys)}）");
    }

    /// <summary>⚠️ 例外を投げずに聞く版（クエリの値が33枚に無いかもしれないときに使う）。</summary>
    public static bool Has(string id) => ById.ContainsKey(id);

    // ── 描く ────────────────────────────────────────

    /// <summary>土台を実データで描く（HTML）。⚠️ 渡すのは **HostId**
    /// （部品の id をそのまま渡しても描けない ── 土台の中でしか描けないため）。
    ///
    /// ⭐ 「確かめ用の頁」（`Pages/*Page.razor`）の `OnParametersSet` と同じ組み方
    /// （クエリ無し＝既定値のときの姿）を1か所に集めた。
    /// ⚠️ **ここで `LayoutStore.SetOverride` を呼ばない**（差し込みはエディタの仕事）。</summary>
    public static string Draw(string hostId)
    {
        switch (hostId)
        {
            case "box":
                // ⭐ **開いた状態で描く。**⚠️ 閉じたまま（既定）だと `chips`（sortchips）が
                //    box.txt の `when=open` で一度も出ない ── 部品4つ（panel/sortbar/cell/
                //    sortchips）が**同時に**見える状態を選んだ（cell は cellB 側で見える）。
                return Sheets.Box(new Shell(Demo.Game(), Demo.Now) { SortOpen = true });

            case "home":
            {
                var shell = new Shell(Demo.Game(), Demo.Now);
                Demo.Incubate(shell.Game, shell.Now, 3);
                return Sheets.Home(shell);
            }

            case "nests":
                return Sheets.Wilds(new Shell(Demo.Game(), Demo.Now));

            case "breed":
            {
                var shell = new Shell(Demo.Game(), Demo.Now);
                var sorted = shell.Sorted();
                if (sorted.Count > 0) shell.ParentA = sorted[0].Id;
                if (sorted.Count > 1) shell.ParentB = sorted[1].Id;
                return Sheets.Breed(shell);
            }

            case "fuse":
            {
                var s = new Shell(Demo.Game(), Demo.Now);
                var pool = Deeds.Food(s);
                int want = Math.Clamp(3, 0, Math.Min(Games.PickAtOnce, pool.Count));
                for (int i = 0; i < want; i++) s.Melts.Add(pool[i].Id);
                return Sheets.Fuse(s);
            }

            case "grow":
            {
                // ⭐ 点が余っている状態で開く（`/edit` から振る前の形が見えるように）
                var s = new Shell(Demo.Game(), Demo.Now);
                var one = s.PickedOne();
                if (one != null) Creatures.Grow(one, 12);
                return Sheets.Grow(s);
            }

            case "skillegg":
            {
                var s = new Shell(Demo.Game(), Demo.Now);
                Demo.Shelve(s.Game, 8, 1);
                s.Slot_ = 0;
                int want = Math.Clamp(3, 0, Games.PickAtOnce);
                for (int i = 0; i < s.Game.Eggs.Count && s.Feeds.Count < want; i++) Deeds.Feed_(s, i);
                return Sheets.Train(s);
            }

            case "skillinfo":
            {
                var all = Skills.All;
                return Sheets.SkillCard(new Shell(Demo.Game(), Demo.Now)
                {
                    SkillId = all[0].Id,
                    SkillSlot = -1,
                    SkillLevel = 1,
                });
            }

            case "species":
                return Sheets.Species(new Shell(Demo.Game(), Demo.Now) { SpeciesAt = 0 });

            case "book":
                return Sheets.Book(new Shell(Demo.Game(), Demo.Now));

            case "save":
                return Sheets.Keep(new Shell(Demo.Game(), Demo.Now)
                {
                    SaveSize = 11024,
                    SavePast = new[] { 12, 300, 3600, 86400, 604800 },
                });

            case "ask":
            {
                // ⭐ AskPage と同じ組み方 ── 後ろに図鑑、前に確かめ札
                //    （覆いの目的そのもの「後ろが押せなくなっているか」を見る）。
                var s = new Shell(Demo.Game(), Demo.Now);
                return Sheets.Book(s) + Sheets.Ask(s);
            }

            case "eggpicker":
            {
                var s = new Shell(Demo.Game(), Demo.Now);
                Demo.Shelve(s.Game, 7, 1);
                return Sheets.Eggs(s);
            }

            case "battle":
            {
                var shell = new Shell(Demo.Game(), Demo.Now);
                shell.Fight_ = Demo.Fight(shell.Game);
                // ⭐ BattlePage と同じ組み方（下の帯を跨がない画面 ── 本体は top:132/1788）。
                return Wrap132(Sheets.Fight(shell));
            }

            case "party":
                return Sheets.Party(new Shell(Demo.Game(), Demo.Now), idle: false);

            case "partyidle":
                return Sheets.Party(new Shell(Demo.Game(), Demo.Now), idle: true);

            case "trial":
            {
                var s = new Shell(Demo.Game(), Demo.Now);
                Demo.Beat(s.Game, 2);
                return Sheets.Trials_(s);
            }

            case "trail":
            {
                var shell = new Shell(Demo.Game(), Demo.Now);
                shell.Raid_ = Demo.Raid(shell.Game, 0);
                // ⭐ RaidPage と同じ組み方（すごろくも top:132/1788）。
                return Wrap132(Sheets.Raid(shell));
            }

            case "banner":
                return Sheets.Banner(new Shell(Demo.Game(), Demo.Now) { Banner = "WIN" });

            case "fanfare":
            {
                var shell = new Shell(Demo.Game(), Demo.Now);
                var sorted = shell.Sorted();
                if (sorted.Count > 0) shell.Cheer_ = Cheer.Born(sorted[0]);
                return Sheets.Fanfare(shell);
            }

            case "dice":
                return Sheets.Dice(new Shell(Demo.Game(), Demo.Now) { Dice = 3 });

            case "frame":
                // ⚠️ 確かめ用の頁が無い（`Shell.Frame` は `AppPage` が直に呼ぶだけ）。
                //    ⭐ BOX タブを選ぶと、帯（タブ4つ）と右肩の EXP 札まで一度に見える。
                return new Shell(Demo.Game(), Demo.Now) { Now_Sheet = Sheet.Box }.Frame();

            default:
                throw new KeyNotFoundException($"土台が無い: {hostId}");
        }
    }

    /// <summary>戦闘・すごろくは下の帯を跨がない画面 ── `BattlePage`/`RaidPage` と同じ
    /// `top:132 / height:1788` の枠を掛けてから描く。⚠️ 見た目を実物に揃えるだけの飾り
    /// ── `Layouts.Faults` は骨組み自身の絶対座標（0起点）を見るので、この枠の有無に
    /// 影響されない（`battle.txt`/`trail.txt` 側も 1788 を前提に書かれている）。</summary>
    private static string Wrap132(string inner) =>
        "<div id=\"body\" class=\"n\" style=\"left:0;top:132px;width:1080px;height:1788px;overflow:hidden\">"
        + inner + "</div>";
}
