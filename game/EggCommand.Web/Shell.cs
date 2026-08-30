using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>いま出ている画面。⚠️ Unity 版 `App.Screen` と同じ並び。</summary>
public enum Sheet { Home, Nests, Breed, Box, Book, Fight, Raid, Trial }

/// <summary>覆いで前に出る札。⚠️ 画面とは別に数える（後ろの画面は残る）。
///
/// ⭐ **`Tree`**（家系図）を追加（2026-08-29・作者の指示「BOXで2世代以降の
/// キャラクターの家系図を見られるように」）。⚠️ 末尾に足す ── 途中に挿すと
/// 既存の値の並びが1つずつずれる（値そのものを保存には書いていないが、
/// 並びに依存した比較を疑う手間を増やさないため）。</summary>
public enum Panel { None, Party, Species, Skill, Eggs, Fuse, Train, Ask, Keep, Grow, Tree, Menu }

/// <summary>すごろくの演出がどこまで進んだか。</summary>
public enum Roam
{
    /// <summary>止まっている（押しどころを待つ）。</summary>
    Still,
    /// <summary>さいころが回っている。⚠️ 出目は**もう決まっている**。</summary>
    Rolling,
    /// <summary>駒が1マスずつ踏んでいる。⚠️ 行き先は**もう決まっている**。</summary>
    Walking,
}

/// <summary>盤に1つ出す演出。⭐ **「何が起きたか」を字で説明しないための道具。**
///
/// ⚠️ 座標は持たない ── ⭐ 出す先は**体の名前**（`a0` `f2`）で言い、
/// 実際の場所はブラウザがその体の枠から測る（`fx.js`）。
/// ⚠️ ここで座標を計算すると、`Stands.Lay` の式と2か所になる。</summary>
/// <param name="At">どの体の上に出すか（`a0` `f2`）。</param>
/// <param name="Kind">出し方。`say` 字 / `shout` 名乗り / `ring` 輪 /
/// `hit` 光 / `shock` 跳ね / `step` 踏み込み（`stepf` は左へ）。</param>
/// <param name="Up">同じ体に重ねないための段（0 が頭の上）。</param>
/// <param name="Wait">出るまでの間（秒）。⭐ **1手で2つ以上起きたとき、順番に出すための数。**
/// ⚠️ 0 なら即座。⭐ 積む段（<paramref name="Up"/>）と役が違う ── あちらは**場所**、
/// こちらは**時間**。両方要る（順に出しても、前のが消える前に次が出るので重なる）。</param>
public readonly record struct Spark(
    string At, string Kind, string Text, string? Tint, int Size, int Up, double Wait = 0);

/// <summary>いま祝っている物（卵を得た・生まれた）。⚠️ Unity 版 `View.Fanfare` が
/// `EggGot`/`Born` で組み立てる中身と同じ4つ（絵・パレット・★・告知の字）に、
/// 後ろの光の色を足しただけ。
///
/// ⚠️ 型の名前を「Fanfare」にしなかった理由: `Sheets.Fanfare` という**同名のメソッド**が
/// 同じ名前空間に在る（Unity 版に合わせた）── 型とメソッドが同名だと、
/// このファイルの中で `Fanfare` と書いた瞬間にどちらを指すか揺れる。
/// ⭐ 演出の秒（`Core.Beats.CheerPop`/`CheerSpin`）と語を合わせて `Cheer` にした。
///
/// ⭐ **`IsCreature`/`CreatureId`**（2026-08-29 追加・作者の指示「生まれたその場で
/// 分解とステータス詳細」）── 卵か個体かの手掛かりが無かったので足した。
/// ⚠️ `Stars` の空/非空を「個体か卵か」に流用しない ── 星の出し分けと釦の出し分けは
/// 別の判断で、たまたま今は同じ条件でも、片方だけ変えたら黙ってずれる。</summary>
public readonly record struct Cheer(PixelSprite Art, Palette Palette, string Stars, string Line, string Burst,
    bool IsCreature, string? CreatureId)
{
    /// <summary>卵を手に入れた。⭐ Unity 版 `Fanfare.EggGot` と同じ中身
    /// （絵は `EggArt` ── 卵はまだ PNG に焼いていないので SVG のまま出る）。</summary>
    public static Cheer EggGot(Egg egg)
    {
        var species = SpeciesTable.ById(egg.SpeciesId);
        return new Cheer(EggArt.Sprite, EggArt.Shell, Rarities.StarsOf(egg.Rarity),
            $"{species.Name}のたまごをゲットした！！", BurstOf(egg.Element),
            IsCreature: false, CreatureId: null);
    }

    /// <summary>卵が孵った。⭐ Unity 版 `Fanfare.Born` と同じ中身。
    /// ⚠️ **★は出さない**（卵のときだけの印 ── `Stars` を空にすると `Sheets.Fanfare` が隠す）。</summary>
    public static Cheer Born(Creature creature)
    {
        var species = Creatures.SpeciesOf(creature);
        return new Cheer(species.Sprite, Creatures.PaletteOf(creature), "",
            $"{species.Name}がうまれた！！", BurstOf(creature.Element),
            IsCreature: true, CreatureId: creature.Id);
    }

    /// <summary>属性の色を、後ろの光ぶん薄める（Unity 実測 alpha .35）。
    /// ⚠️ `Face.ElementCss` と同じ3色を rgba へ手で変換した値
    /// （3色だけなので、ここのためだけに 16進→rgb の変換器を持ち込まない）。</summary>
    private static string BurstOf(Element element) => element switch
    {
        Element.Fire => "rgba(232,122,92,.35)",
        Element.Wood => "rgba(168,216,110,.35)",
        _ => "rgba(110,168,216,.35)",
    };
}

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

    /// <summary>いまの時刻（Unix秒）。
    ///
    /// 🔴 **前は生成時の1度きりの値をそのまま持つだけの `long` フィールドで、
    /// どこからも更新されていなかった**（作者の報告 2026-08-27）。⚠️ 孵化器の残り時間
    /// （<see cref="Hatchery.LeftOf"/>）や巣の居座り時間（<see cref="Encounters.LeftOf"/>）は
    /// この値で出すので、**画面を開いた瞬間の時刻に固定され、待っても減らなかった**。
    /// ⭐ いまは既定で「いま」を都度返す ── **時計の出所を1つにする**（前任者は
    /// `_shell.Now` を諦めて `DateTimeOffset.UtcNow` を都度取り直す形で放置報酬のバグを
    /// 避けたが、それは根を直さず穴を回避しただけだった。`AppPage.razor` の `BeatIdle` 参照）。
    ///
    /// ⚠️ **確認画面（`Demo.Game()` 系）だけは凍結する。**⭐ 確認画面は「いつ見ても
    /// 同じ絵」でなければならない（毎回時刻が動くとスクショが比べられなくなる）── そちらは
    /// コンストラクタへ `live: false`（既定）で渡した `now` を動かさず返す。
    /// ⚠️ **本番（`/app` の実プレイ）だけ `live: true` を渡す。**Core からは1つも
    /// 読まれない（`Shell` は `EggCommand.Web` だけの型）ので、ここを動く時計に
    /// 変えても Core の計算には影響しない。</summary>
    public long Now => (long)NowFine;

    /// <summary>🔴 **秒より細かい「いま」**（2026-08-28）。⭐ <see cref="Now"/> はこれを切り捨てた物
    /// ── **時計の出所は1つのまま**（2026-08-27 に「出所を1つに戻す」と決めた約束を守る）。
    ///
    /// ⚠️ 放置の拍は 0.4秒・0.5秒まで細かい（`Core.Idle`）のに、`Now` は**整数秒**なので、
    /// 1秒に4回覗いても**進むのは1秒に1回・1.0秒ぶんずつ**だった。⭐ 結果、
    /// 0.5秒ごとのはずの打撃が**同じ瞬間に2発ずつ**出ていた（実測 2026-08-28: 間隔 0.00秒 → 1.08秒 → 0.00秒…）。
    /// ⚠️ 直すのに `Now` を実数へ変えると、孵化器・巣の期限・保存など**整数秒を前提にした
    /// 全部**に波及する ── ⭐ だから細かいほうを**足す**（読む側が要るほうを選ぶ）。</summary>
    public double NowFine => _live
        ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0
        : _frozenAt;

    private readonly long _frozenAt;
    private readonly bool _live;

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

    // ── 演出の拍（⭐ 数は `Core.Beats`）────────────────
    /// <summary>1手をどこまで進めたか。</summary>
    public Deeds.Stage Stage;
    /// <summary>次の拍までの残り（秒）。⚠️ 0 より大きい間は**何も進めない**。</summary>
    public double Wait;
    /// <summary>時の進み方の倍率。⭐ **1 が遊ぶ速さ**（0.5 なら2倍速）。
    ///
    /// ⚠️ **検査を速く回すためだけに在る。**⭐ 起きることも順も1つも変わらない
    /// ── **時計そのものを早送りする**ので、溜めもゲージも同じ比で縮む。
    /// ⚠️ 拍ごとの数（`Core.Beats`）は触らない ── 触ると比が崩れる。
    /// ⚠️ 遊ぶ道からは触れない（`/app?pace=` は検査の入口）。
    /// ⚠️ **速さそのものは別に見張る** ── これで縮めた検査は「着くか」しか見ていない。</summary>
    public double Pace = 1;
    /// <summary>まだ進めていない端数の刻み。⚠️ 切り捨てると遅い者が永久に進まない。</summary>
    public double Ticks;
    /// <summary>🔴 **もう人へ渡してある手番。**⚠️ 無ければ null。
    ///
    /// ⭐ 人の手番が来た拍で**一度だけ**画面を組み直すために要る。
    /// ⚠️ 毎回組み直すと、押した直後に部品が入れ替わって触れなくなる
    /// （2026-08-22 の実測 ── `AppPage.Beat` の `Tick.Stopped` の注記）。
    /// ⚠️ 逆に一度も組み直さないと、**札が押せないまま止まる**
    /// （2026-08-28 に実測。それまでは `Sheets.Fight` が描くたびに `NextActor` を
    /// 呼んで戦いを進めていたので、たまたま最新の画面になっていた ── 描く側の
    /// 副作用に頼っていた形。`Battle.Standing` の注記を参照）。</summary>
    public Unit? Handed;

    /// <summary>名乗り済みで、まだ打っていない手。</summary>
    public Unit? Cast;
    public int CastSlot;
    public Unit? CastAim;
    /// <summary>盤へ出す演出。⭐ **描いた側が空にする**（一度出したら消える）。</summary>
    public readonly List<Spark> Sparks = new();
    /// <summary>倒れた拍だけ伏せておく墓。砂煙の終わりで JS が見せ、次の Draw では外す。
    /// ⚠️ 生死の出所ではない ── 生死は Core の HP だけで判じる。</summary>
    public readonly HashSet<string> PendingGraves = new();

    /// <summary>いま出ている告知（「WIN」など）。⚠️ 無ければ null。
    /// ⭐ ボタンは置かない ── 結果であって、選択ではない。</summary>
    public string? Banner;

    /// <summary>いま出ている祝い（卵を得た・生まれた）。⚠️ 無ければ null（出さない）。
    /// ⭐ Banner と違って**自分では消えない** ── 覆いを押す（`case "cheer"`）まで出しっぱなし。</summary>
    public Cheer? Cheer_;

    /// <summary>⭐ 祝いの上に**詳細を重ねて見せているか**（2026-08-30・作者の指示
    /// 「BOXに飛ぶのではなくBOXで表示する詳細と同じものを見れるだけに」）。
    /// ⚠️ 祝いを閉じる（`case "cheer"`）ときに必ず下ろす ── 上げたまま残すと、
    /// 次に生まれた子の祝いが**いきなり詳細付きで開く**。</summary>
    public bool BornLook;

    // ── すごろくの拍 ──────────────────────────────────
    /// <summary>さいころ・駒がどこまで進んだか。</summary>
    public Roam Roam_;
    /// <summary>次の拍までの残り（秒）。</summary>
    public double RoamWait;
    /// <summary>いま転がしている目。⚠️ **ここで引き直さない**（`Trails.Roll` が決めた数）。</summary>
    public int Dice;
    /// <summary>いま辿っている道。⚠️ 先頭は**いま居るマス**。</summary>
    public List<int>? Path;
    /// <summary>道の何歩目まで見せたか。⭐ 駒はここに立つ（盤の中の数ではない）。</summary>
    public int Step_;
    /// <summary>画面に出す一言。⚠️ 黙って変わらないことを防ぐ。</summary>
    public string? Say;

    /// <param name="game">この器が動かすゲーム。</param>
    /// <param name="now">凍結するとき（<paramref name="live"/> が false）の固定時刻。
    /// ⚠️ <paramref name="live"/> が true のときも、初期状態（生成した瞬間）を
    /// 揃えるために使う場所がある（例: `Games.NewGame` の作成時刻）── `Shell.Now` 自体は
    /// 以後 `live` に従う。</param>
    /// <param name="live">⭐ **本番だけ true。**⚠️ 既定 false ── 呼び側を増やさずに
    /// 済むよう、これまでどおりの「固定」を既定の振る舞いにしてある
    /// （確認画面が20か所以上あり、すべてが凍結を期待しているため）。</param>
    public Shell(Game game, long now, bool live = false) { Game = game; _frozenAt = now; _live = live; }

    // ── 一覧 ────────────────────────────────────────

    /// <summary>⭐ **絞ってから並べる**（BOX・配合・編成で同じ順）。</summary>
    public IReadOnlyList<Creature> Sorted()
    {
        var pool = Filters.Apply(Game, Game.Storage.Creatures, Filter);
        return Storages.Sorted(new Storage(Game.Storage.Slots, pool), Sort, Basis);
    }

    /// <summary>卵の棚の並び順。⭐ `"star"`（★の多い順）か `"new"`（入手順）
    /// （2026-08-29・作者の指示「並び替え機能追加 ── 星、入手順」）。
    /// ⚠️ 2つしか無いので、BOX の絞り込み／並べ替え／何の数で（`sortchips`）は持ち込まない
    /// ── あれは3段の仕掛けで、2択のためには重すぎる。</summary>
    public string EggSort = "star";

    /// <summary>棚の卵を <see cref="EggSort"/> の順に並べたもの。
    ///
    /// 🔴 **画面も押しどころも必ずこれを通す。**⚠️ 絵だけ並べ替えて
    /// `Game.Eggs` の添字で温めると、**押した卵と違う卵が孵る**
    /// （<see cref="Sorted"/> と `Choose` が creature 側で守っているのと同じ約束）。
    /// ⭐ 入手順は `Game.Eggs` の並びそのもの ── 足すときは末尾に足されるため。</summary>
    public IReadOnlyList<Egg> SortedEggs()
    {
        var list = new List<Egg>(Game.Eggs);
        if (EggSort != "star") return list;
        // ⚠️ **同じ★の中は入手順のまま**にしたいので、添字を第2の鍵にした安定な並べ替え。
        //    ⭐ `List.Sort` は安定でないので、元の位置を覚えてから比べる。
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < list.Count; i++) order[list[i].Id] = i;
        list.Sort((x, y) =>
        {
            int d = y.Rarity.CompareTo(x.Rarity);
            return d != 0 ? d : order[x.Id].CompareTo(order[y.Id]);
        });
        return list;
    }

    public Creature? PickedOne()
    {
        foreach (var c in Game.Storage.Creatures) if (c.Id == Picked) return c;
        var list = Sorted();
        if (list.Count > 0) return list[0];
        // 🔴 **絞り込みが0件でも、手持ちが在れば倒す。**⚠️ ここが null だと `Sheets.Box`
        //    が丸ごと「手持ちが無い」扱いにして真っ白を返し、絞り込みを戻す帯
        //    （`sortbar`）自体もその HTML の中にあるので**戻す手段が無くなる**
        //    （実例: 新規セーブで編成が空のまま BOX→「出撃中」で行き止まる。
        //    2026-08-25 監査で発覚）。⭐ 絞り込み後の空と「本当に手持ちが無い」を分ける。
        var unfiltered = Storages.Sorted(Game.Storage, Sort, Basis);
        return unfiltered.Count > 0 ? unfiltered[0] : null;
    }

    // ── 押された ────────────────────────────────────

    /// <param name="at">繰り返しの番号（入れ子なら `2#1`）。⚠️ 無ければ空。</param>
    public void Tap(string what, string at)
    {
        if (UiCommands.TryParseTap(what, at, out var command)) Tap(command);
    }

    /// <summary>解析済みの短押しを実行する。外部文字列はここより前で検証済み。</summary>
    public void Tap(UiCommand command)
    {
        if (!UiCommands.IsValidTap(command) || !HasValidTapIndex(command)) return;
        int i = command.Index;
        string at = command.At;
        switch (command.Kind)
        {
            case UiActionKind.Tab:
                // ⚠️ 画面を移るときに並べ替えを畳む（Unity 版と同じ ── 一覧へ戻るたびに
                //    開いていると、見たいのは一覧なのに毎回畳む操作が要る）
                SortOpen = false;
                Open = Under = Panel.None;
                Now_Sheet = i switch
                {
                    1 => Sheet.Nests, 2 => Sheet.Breed, 3 => Sheet.Box,
                    // ⭐ 5つ目（2026-08-30）。⚠️ 旧 `trials` の受けはここへ畳んだ
                    //    ── 同じ行き先へ2つの道を残すと、片方だけ直す事故が起きる。
                    4 => Sheet.Trial, _ => Sheet.Home,
                };
                break;

            // 🔴 **`back`（‹）と `extra`（右肩）は消した**（2026-08-29・上のバーを外した）。
            //    ⚠️ `back` を出していたのは図鑑と試練の2画面だけで、どちらも下の帯の
            //    「ホーム」タブが同じ行き先を持っていた＝重複した道。
            //    ⭐ `extra` の行き先はここに来た（ホームの図鑑 → `menu`／探索の編成 → `party`）。
            // ⚠️ **閉じたら選びかけも捨てる。**⭐ 残すと、次に開いたとき
            //    身に覚えのない個体が選ばれていて、そのまま分解できてしまう
            case UiActionKind.Close: Open = Under; Under = Panel.None; Melts.Clear(); Feeds.Clear(); break;

            // ⭐ 祝い（Fanfare）を閉じる。⚠️ Unity 版も覆い全体が close ボタン
            //    （`Fanfare.prefab` の `Dim` が Image と Button を兼ねている）── 同じ形。
            case UiActionKind.Cheer: Cheer_ = null; BornLook = false; break;

            // ⭐ **輪の外のものを1つにまとめた入口**（2026-08-29・作者の指示
            //    「図鑑や保存などのボタンにしか使わないものを一か所にまとめ、右上に」）。
            //    ⚠️ ホームの右上にしか置いていない ── 遊びの輪（探索→潜入→戦闘→帰還）は
            //    下の帯が持つので、そこへ混ぜない。
            case UiActionKind.Menu: Open = Panel.Menu; break;
            // ⭐ メニューの4つ。⚠️ どれも**先にメニューを閉じてから**行き先を決める
            //    （閉じないと、行った先の上にメニューが残って被る）。
            case UiActionKind.Book: Open = Under = Panel.None; Now_Sheet = Sheet.Book; SortOpen = false; break;

            case UiActionKind.BarToggle: SortOpen = !SortOpen; break;
            case UiActionKind.ChipsFilter: Filter = Filters.Keys[i]; break;
            case UiActionKind.ChipsSort: Sort = Storages.SortKeys[i]; break;
            case UiActionKind.ChipsBasis: Basis = Storages.Bases[i]; break;

            case UiActionKind.One: Choose(i); break;

            // ── 遊びを動かす ────────────────────────
            case UiActionKind.Nest: Deeds.Dive(this, i); break;
            case UiActionKind.Boss: Deeds.Boss(this); break;
            case UiActionKind.Roll: Deeds.Roll(this); break;
            case UiActionKind.Square: Deeds.Step(this, i); break;
            case UiActionKind.Pay: Deeds.Pay(this); break;
            case UiActionKind.Skip: Deeds.Pass(this); break;
            case UiActionKind.S0: Deeds.Strike(this, 0); break;
            case UiActionKind.S1: Deeds.Strike(this, 1); break;
            case UiActionKind.S2: Deeds.Strike(this, 2); break;
            // ⭐ 体を押して狙い先にする（敵味方とも）。もう一度押すと外れる。
            //    ⚠️ 番号ではなく `a0`/`f2` の形で来るので `at` をそのまま渡す。
            case UiActionKind.Aim: Deeds.Aim(this, at); break;
            case UiActionKind.Pick: Auto = !Auto; break;
            // ⭐ **取り返しがつかないので一度だけ確かめる**（押し間違いで負けにしない）
            case UiActionKind.Give: if (Fight_ != null) Open = Panel.Ask; break;
            // ⭐ **いま手番の体の特性を読む**（2026-08-30・作者の指示「特性を確認できる
            //    ボタン追加」）。⚠️ 特性は勝ち負けを分けるのに、戦闘の盤には名前すら
            //    出ていなかった（BOX の札を開く道も戦闘中は無い）。
            // ⭐ 覆いでなく一言（`Say`）で出す ── 覆いを開くと `Deeds.Beat` が
            //    `Open != None` で時を止め、戦いの流れが切れる。
            // ⚠️ 読む相手は**手札の主**（`StandingAlly`）── `Standing` は敵の番なら
            //    敵を返すので、押した人の意図（自分の特性を確かめる）とずれる。
            case UiActionKind.Feat:
                if (Fight_ is BattleState fight
                    && EggCommand.Core.Battle.StandingAlly(fight) is Unit who)
                    Say = Creatures.TraitOf(who.Creature) is Trait trait
                        ? $"特性　{trait.Name} — {trait.Gist}"
                        : "この子に特性は無い";
                break;
            case UiActionKind.Stop: Open = Panel.None; break;
            case UiActionKind.Go: Deeds.Concede(this); break;

            // ⭐ 空き枠を押したら、そのとき初めて卵の在庫が開く（棚を常に出しておかない）
            case UiActionKind.Slot: Deeds.Slot(this, i); break;
            case UiActionKind.Egg: Deeds.Warm(this, i); break;
            // ⭐ 棚の並べ替え（2026-08-29・作者の指示「星、入手順」）。
            //    ⚠️ 押しどころは2つに分けてある ── 繰り返しでない節点は添字を持たない。
            case UiActionKind.EggStar: EggSort = "star"; break;
            case UiActionKind.EggNew: EggSort = "new"; break;

            // ── 育てる ──────────────────────────────
            // ⚠️ 分解は**開くだけ**。⭐ 減るのは札の中の「分解する」を押したとき
            // 🔴 **`ClaimBorn()` を先に呼ぶ**（2026-08-29・作者の指示「生まれたその場で
            //    分解とステータス詳細」）。⚠️ 祝い（`Cheer_`）が生きたままだと開いた札の
            //    裏で覆いが残る ── 普段（BOX から押したとき）は `Cheer_` が null なので
            //    ここは何もしない（無害）。
            // 🔴 祝い経由のときは生まれた本人を分解候補へ**事前選択**する（2026-08-29）。
            //    ⚠️ `ClaimBorn` が `Picked`=本人 にするため、素の `Deeds.Food` からは
            //    外れてしまう ──「`Melts` に居る個体は `Food` が外さない」（`Deeds.cs`）と
            //    対で成り立つ。
            case UiActionKind.Fuse:
            {
                string? born = ClaimBorn();
                Melts.Clear();
                if (born != null) Melts.Add(born);
                Open = Panel.Fuse;
                break;
            }
            case UiActionKind.Melt: Deeds.Melt(this); break;
            case UiActionKind.Train: Feeds.Clear(); Slot_ = 0; Open = Panel.Train; break;
            case UiActionKind.Row:
                if (i >= 0 && i < (PickedOne()?.SkillPoints.Length ?? 0)) { Slot_ = i; Feeds.Clear(); }
                break;
            case UiActionKind.Chip: Deeds.Feed_(this, i); break;
            case UiActionKind.Feed: Deeds.Feed(this); break;
            // 🔴 **「育てる」は札を開くだけ**（2026-08-26・ARK式の自由配分）。
            //    ⚠️ 以前はここで直に Lv＋1 していたが、振り先を選ぶ場所が要る。
            //    ⭐ EXP→点（`levelup`）も、点を振る（`spend`）も、その札の中でやる。
            // ⚠️ 祝いの「くわしく見る」はもうここでない ── detail（下）へ繋ぎ直した
            //    （2026-08-29・grow は全行が不可逆の EXP 消費で「読む」着地として誤り）。
            case UiActionKind.Grow: Open = Panel.Grow; break;
            // ⭐ 祝いの「くわしく見る」。🔴 **BOX へ飛ばさず、その場で重ねて見せる**
            //    （2026-08-30・作者の指示「BOXに飛ぶのではなくBOXで表示する詳細と
            //    同じものを見れるだけに」）。⚠️ 前は画面ごと BOX へ移していたので、
            //    **祝いが終わってしまい**、続けて「分解する」を選べなかった。
            // ⭐ 同じ `detail` が開くと閉じるを兼ねる（札の「閉じる」も `tap=detail`）
            //    ── 名前を増やさないため。⚠️ `TapCatalog` は増えない。
            // 🔴 **`ClaimBorn()` を呼ばない。**あれは `Cheer_` を null にする＝祝いを
            //    閉じてしまう ── 重ねて見せるには祝いが生きていなければならない。
            //    ⭐ 見せる相手は `Cheer_.CreatureId` から引く（`Sheets.Fanfare`）。
            case UiActionKind.Detail: BornLook = !BornLook; break;
            // ⭐ 家系図（2026-08-29・作者の指示）。⚠️ 押しどころ自体が
            //    `Generation < 2` では出ない（`Sheets.Box` の `Tappable` ── 2世代未満は
            //    墓標を辿っても何も出ないので、そもそも押せなくしてある）。
            //    ⭐ `Cheer_` からは開けない（`fuse`/`grow` と違い `ClaimBorn()` は呼ばない）。
            case UiActionKind.Tree: Open = Panel.Tree; break;
            // 🔴 二度手間解消（2026-08-29・作者の指示「点を振る前に点を獲得するのが
            //    二度手間」）。⚠️ 以前は `levelup`（EXP→点）と `spend`（点→ステ）が
            //    別の押しどころだった。⭐ 判断はここ1か所だけに置く（`Deeds.cs` は
            //    担当外）── 既存の Core の口 `Core.Idle.Spend`／`Creatures.Spend` を
            //    そのまま呼ぶだけで、中身（上限・値段の規則）は1つも書き写さない。
            //    ⚠️ **既に振れる点が残っている**（`spend` 追加前の古い保存の名残）ときは
            //    そちらを先に使う ── でないと残りを素通りして EXP だけ余計に減る。
            // ⭐ EXP→点→ステを1回で。⚠️ 中身は `Deeds.SpendPoint` が唯一の出所
            //    （ここへ書き写さない ── 2026-08-29 に一度書き写されていた）
            case UiActionKind.Spend: Deeds.SpendPoint(this, i); break;

            // ── 配合 ────────────────────────────────
            case UiActionKind.Pa: ParentA = null; break;
            case UiActionKind.Pb: ParentB = null; break;
            case UiActionKind.Breed: Deeds.Breed(this); break;

            // ── 編成 ────────────────────────────────
            // ⭐ **どちらの編成かは、押した画面が決める。**探索から押したときだけ巣の編成、
            //    それ以外（ホームのメニュー）は放置の編成。
            //    ⚠️ 2026-08-29 まで探索は右肩の `extra` から入っていたが、上のバーを
            //    外したので `nests.txt` の下端の釦から同じ `party` に入る ── 行き先が
            //    2つに割れないよう、`IdleParty` の決め方をここ1か所に集めた。
            case UiActionKind.Party: IdleParty = Now_Sheet != Sheet.Nests; Open = Panel.Party; break;
            case UiActionKind.Set: Deeds.Team(this, i); break;
            case UiActionKind.Seat: Deeds.Drop(this, i); break;
            case UiActionKind.Done: Open = Under = Panel.None; break;

            // ── 保存の控え ──────────────────────────
            // ⚠️ **出し入れそのものは画面の外**（ブラウザに聞く）ので、
            //    ここは開くだけ。⭐ 実際の読み書きは `AppPage` が持つ。
            case UiActionKind.Keep: Open = Panel.Keep; break;

            // ── 図鑑・試練 ──────────────────────────
            // 🔴 **`trials`（試練の画面へ行く）は消した**（2026-08-30・作者の指示で
            //    試練が下の帯の5つ目の釦になったため）。⚠️ 行き先は `case "tab"` の
            //    `4 => Sheet.Trial` ── 同じ場所へ2つの道を残すと、片方だけ直す事故が起きる
            //    （`TapEntranceTests` も「骨組みに入口の無い tap」として落とす）。
            case UiActionKind.Trial: Deeds.Trial(this, i); break;
            case UiActionKind.Species: SpeciesAt = i; Open = Panel.Species; break;
        }
    }

    /// <summary>**長押し**された。
    ///
    /// ⭐ 押しどころとは別の道（`hold=`）。⚠️ 短く触っても開かない
    /// ── 技の札は押しどころではないので、触っただけで開くと選ぶ指が誤爆する。</summary>
    public void Hold(string what, string at)
    {
        if (UiCommands.TryParseHold(what, at, out var command)) Hold(command);
    }

    /// <summary>解析済みの長押しを実行する。外部文字列はここより前で検証済み。</summary>
    public void Hold(UiCommand command)
    {
        if (!UiCommands.IsValidHold(command) || !HasValidHoldIndex(command)) return;
        int i = command.Index;
        switch (command.Kind)
        {
            // ⭐ BOX の札の技（枠0〜2）。⚠️ **いま見ている個体の**技とレベルを出す。
            //    ⚠️ 名前に `detail-` が冠されている（`use=panel` で差した部品なので）
            case UiActionKind.DetailS0: Show(PickedOne(), 0); break;
            case UiActionKind.DetailS1: Show(PickedOne(), 1); break;
            case UiActionKind.DetailS2: Show(PickedOne(), 2); break;

            // ⭐ 戦闘の手札（battle.txt の s0〜s2 ── 2026-08-29 配線）。
            //    ⚠️ 主は Sheets.Fight と同じ StandingAlly ── 描いている札と同じ技の出所。
            //    札そのものが when=sN（自分の手番に技がある枠だけ）でしか出ないので、
            //    敵の番には来ない。⭐ CT 冷却中でも長押しは効く（使えない技ほど読みたい）。
            case UiActionKind.S0: Show(Hand(), 0); break;
            case UiActionKind.S1: Show(Hand(), 1); break;
            case UiActionKind.S2: Show(Hand(), 2); break;

            // ⭐ 配合の親札（panelmini ── pfill=親A・qfill=親B。2026-08-29 配線）。
            //    ⚠️ 短押し（tap=pa/pb・親を外す）とは tap.js が押下時間で分ける
            case UiActionKind.PfillS0: Show(One(ParentA), 0); break;
            case UiActionKind.PfillS1: Show(One(ParentA), 1); break;
            case UiActionKind.PfillS2: Show(One(ParentA), 2); break;
            case UiActionKind.QfillS0: Show(One(ParentB), 0); break;
            case UiActionKind.QfillS1: Show(One(ParentB), 1); break;
            case UiActionKind.QfillS2: Show(One(ParentB), 2); break;

            // ⭐ 祝いに重ねた詳細の技（`fanfare.txt` の `sheetp` ── 2026-08-30）。
            //    ⚠️ **`PickedOne()` では引けない** ── くわしく見るは `ClaimBorn()` を
            //    呼ばない（祝いを閉じてしまうため）ので、`Picked` は前に見ていた個体のまま。
            //    ⭐ 生まれた本人は `Cheer_.CreatureId` からしか辿れない。
            case UiActionKind.SheetpS0: Show(Born(), 0); break;
            case UiActionKind.SheetpS1: Show(Born(), 1); break;
            case UiActionKind.SheetpS2: Show(Born(), 2); break;

            // ⭐ 種族の札の抽選（枠1〜3）。⚠️ こちらは個体ではないので Lv は 1
            case UiActionKind.Skill1: Pool(0, i); break;
            case UiActionKind.Skill2: Pool(1, i); break;
            case UiActionKind.Skill3: Pool(2, i); break;
        }

        // ⭐ 戦闘の手札の主。⚠️ 敵の番でも手札は「次に動かす味方」を出したまま
        //    （Sheets.Fight と同じ読み方）── 長押しの主も同じにする
        Creature? Hand() => Fight_ == null ? null
            : EggCommand.Core.Battle.StandingAlly(Fight_)?.Creature;

        Creature? One(string? id)
        {
            if (id == null) return null;
            foreach (var c in Game.Storage.Creatures) if (c.Id == id) return c;
            return null;
        }

        // ⭐ 祝いで生まれた本人（重ねた詳細の主）。⚠️ 卵の祝いなら居ない。
        Creature? Born() => Cheer_ is Cheer c && c.IsCreature ? One(c.CreatureId) : null;

        // ⭐ 旧 Peek の一般化（2026-08-29）── レベルの式は1つ。呼び手は個体を選ぶだけ
        void Show(Creature? one, int slot)
        {
            if (one == null) return;
            var skills = Creatures.SkillsOf(one);
            if (slot >= skills.Length || skills[slot] == null) return;
            SkillId = skills[slot]!.Id;
            SkillSlot = slot;
            // ⭐ レベルの式は Core が唯一の出所（2026-08-29 ── ここに写しを置かない）。
            //    ⚠️ 上限が技ごとであること（Skills.MaxLevelOf）も向こうが持っている。
            SkillLevel = Creatures.SkillLevelOf(one, slot);
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

    /// <summary>registry が非負までを保証する動的一覧の範囲。値が古い／改ざんされた場合も無操作にする。</summary>
    private bool HasValidTapIndex(UiCommand command) => command.Kind switch
    {
          UiActionKind.One => UiCommands.IsWithinRange(command, Open == Panel.Fuse ? Deeds.Food(this).Count : Sorted().Count),
          UiActionKind.Nest => UiCommands.IsWithinRange(command, Game.Encounters.Count),
          UiActionKind.Square => HasOpenSquare(command.Index),
          UiActionKind.Egg => UiCommands.IsWithinRange(command, SortedEggs().Count),
          UiActionKind.Row => UiCommands.IsWithinRange(command, PickedOne()?.SkillPoints.Length ?? 0),
          UiActionKind.Chip => UiCommands.IsWithinRange(command, Game.Eggs.Count),
          UiActionKind.Seat => UiCommands.IsWithinRange(command, Games.RosterOf(Game, IdleParty ? PartyKind.Idle : PartyKind.Nest).Count),
          UiActionKind.Trial => UiCommands.IsWithinRange(command, Trials.All.Count),
        _ => true,
    };

    private bool HasOpenSquare(int goal)
    {
        if (Open_ == null) return false;
        foreach (var path in Open_) if (path.Count > 0 && path[path.Count - 1] == goal) return true;
        return false;
    }

    private bool HasValidHoldIndex(UiCommand command)
    {
        int slot = command.Kind switch
        {
            UiActionKind.Skill1 => 0,
            UiActionKind.Skill2 => 1,
            UiActionKind.Skill3 => 2,
            _ => -1,
        };
        if (slot < 0) return true;
        var all = SpeciesTable.All;
        var pool = Sheets.PoolOf(all[Math.Clamp(SpeciesAt, 0, all.Count - 1)], slot);
          return UiCommands.IsWithinRange(command, pool.Count);
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

    /// <summary>祝いの覆いに足した「分解する」「くわしく見る」から呼ばれる
    /// （2026-08-29・作者の指示「生まれたその場で分解とステータス詳細」）。
    ///
    /// ⭐ **生まれた個体を「いま選んでいる個体」にしてから、祝いを閉じる。**
    /// ⚠️ 順番が逆（先に閉じてから選ぶ）だと、閉じた拍で描き直しが走り、
    /// 選ぶ前の古い `Picked` のまま札が開く（実際に踏む前に気づいた）。
    /// ⚠️ 卵を得たとき（`IsCreature` が false）は何もしない ── その釦自体が
    /// `when=creature` で出ていないので普段は起きないが、呼ばれても
    /// 個体を差し替えない（万一に備える）。
    /// ⭐ 戻り値＝生まれた本人の Id（居なければ null）── 分解の事前選択が使う
    /// （2026-08-29）。</summary>
    private string? ClaimBorn()
    {
        if (Cheer_ is Cheer c && c.IsCreature) { Picked = c.CreatureId; Cheer_ = null; return c.CreatureId; }
        return null;
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
            // ⭐ 試練は「どこまで勝ったか」。⚠️ 0/5 は出す ── ここだけは 0 に意味がある
            //    （まだ1段も勝っていない、が「段が5つある」と対で読める）。
            $"{Games.TrialsCleared(Game)}/{Core.Trials.All.Count}",
        };
        // 🔴 **5つ目に試練**（2026-08-30・作者の指示）。⚠️ 右上のメニューからは外した ──
        //    入口が2つあると、どちらが本道か言えなくなる。
        var names = new[] { "ホーム", "探索", "配合", "BOX", "試練" };
        int here = Now_Sheet switch
        {
            Sheet.Nests => 1, Sheet.Breed => 2, Sheet.Box => 3, Sheet.Trial => 4,
            Sheet.Home => 0, _ => -1,
        };

        return LayoutDom.Render(LayoutStore.Of("frame"), new DomFill
        {
            Count = key => key == "tabs" ? names.Length : 0,
            At = (key, i) => tab = i,
            Text = key => key switch
            {
                "tname" => names[tab],
                "tcount" => counts[tab],
                _ => "",
            },
            // ⭐ いま居るタブだけ塗る
            Tint = key => key == "tab" && tab == here ? "#f59e0b" : null,
            // 絵は不透明なので背景色だけでは見えない。級でも現在地を渡し、
            // stage.css が位置と下線で確実に区別する。
            Lead = key => key == "tab" && tab == here,
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

    // 🔴 **`Title()` / `Extra()` / `Badge()` は消した**（2026-08-29・上のバーを外したため）。
    //    ⚠️ 3つとも `frame.txt` の `top` の中の節点にしか繋がっていなかったので、
    //    残すと**誰も読まない枝**になる。行き先は `frame.txt` の頭に書いてある:
    //    題名とボス名は捨て・‹ はタブが持ち・EXP は BOX と配合の画面の中へ・
    //    右肩の入口はホームのメニューと探索の下端へ移した。
}
