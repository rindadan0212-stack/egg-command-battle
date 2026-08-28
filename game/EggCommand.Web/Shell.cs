using EggCommand.Core;

namespace EggCommand.Web;

/// <summary>いま出ている画面。⚠️ Unity 版 `App.Screen` と同じ並び。</summary>
public enum Sheet { Home, Nests, Breed, Box, Book, Fight, Raid, Trial }

/// <summary>覆いで前に出る札。⚠️ 画面とは別に数える（後ろの画面は残る）。</summary>
public enum Panel { None, Party, Species, Skill, Eggs, Fuse, Train, Ask, Keep, Grow }

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
/// ⭐ 演出の秒（`Core.Beats.CheerPop`/`CheerSpin`）と語を合わせて `Cheer` にした。</summary>
public readonly record struct Cheer(PixelSprite Art, Palette Palette, string Stars, string Line, string Burst)
{
    /// <summary>卵を手に入れた。⭐ Unity 版 `Fanfare.EggGot` と同じ中身
    /// （絵は `EggArt` ── 卵はまだ PNG に焼いていないので SVG のまま出る）。</summary>
    public static Cheer EggGot(Egg egg)
    {
        var species = SpeciesTable.ById(egg.SpeciesId);
        return new Cheer(EggArt.Sprite, EggArt.Shell, Rarities.StarsOf(egg.Rarity),
            $"{species.Name}のたまごをゲットした！！", BurstOf(egg.Element));
    }

    /// <summary>卵が孵った。⭐ Unity 版 `Fanfare.Born` と同じ中身。
    /// ⚠️ **★は出さない**（卵のときだけの印 ── `Stars` を空にすると `Sheets.Fanfare` が隠す）。</summary>
    public static Cheer Born(Creature creature)
    {
        var species = Creatures.SpeciesOf(creature);
        return new Cheer(species.Sprite, Creatures.PaletteOf(creature), "",
            $"{species.Name}がうまれた！！", BurstOf(creature.Element));
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
    public long Now => _live ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : _frozenAt;

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

    /// <summary>いま出ている告知（「WIN」など）。⚠️ 無ければ null。
    /// ⭐ ボタンは置かない ── 結果であって、選択ではない。</summary>
    public string? Banner;

    /// <summary>いま出ている祝い（卵を得た・生まれた）。⚠️ 無ければ null（出さない）。
    /// ⭐ Banner と違って**自分では消えない** ── 覆いを押す（`case "cheer"`）まで出しっぱなし。</summary>
    public Cheer? Cheer_;

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

            // ⭐ 祝い（Fanfare）を閉じる。⚠️ Unity 版も覆い全体が close ボタン
            //    （`Fanfare.prefab` の `Dim` が Image と Button を兼ねている）── 同じ形。
            case "cheer": Cheer_ = null; break;

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
            // 🔴 **「育てる」は札を開くだけ**（2026-08-26・ARK式の自由配分）。
            //    ⚠️ 以前はここで直に Lv＋1 していたが、振り先を選ぶ場所が要る。
            //    ⭐ EXP→点（`levelup`）も、点を振る（`spend`）も、その札の中でやる。
            case "grow": Open = Panel.Grow; break;
            case "levelup": Deeds.Grow(this); break;
            case "spend": Deeds.SpendPoint(this, i); break;

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
            // ⚠️ 上限は技ごと（Skills.MaxLevelOf）。skills[slot] は直前の null チェック済み
            SkillLevel = SkillCosts.LevelOf(one.SkillPoints[slot], Skills.MaxLevelOf(skills[slot]!));
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
