using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    public enum Screen
    {
        Home,
        Nests,
        Trail,
        Battle,
        Breed,
        Box,
        /// <summary>試練。⭐ ホームから開く（下の帯には出さない）。
        /// ⚠️ 帯は4つで埋まっていて、5つ目を足すと1つあたりが指で押しにくくなる。</summary>
        Trial,
        /// <summary>図鑑。⭐ ホームの**右肩**から開く（2026-08-22・作者の指示）。
        /// ⚠️ 帯にも本体にも空きが無い ── 本体は 1420 の1行しか空いておらず、
        /// そこは試練とパーティ編成で埋まっている。</summary>
        Book,
    }

    /// <summary>画面の器と行き来。⭐ ホームがハブ。各画面は ‹ でホームへ戻る。
    ///
    /// ⭐ 状態の唯一の出所は <see cref="Core.Game"/>。この層は**それを描くだけ**で、
    /// 勝敗も遺伝も飛距離もここでは決めない（決めた瞬間に第2の出所ができる）。
    ///
    /// ⭐ **配置はすべて Assets/Resources/Prefabs/*.prefab が持つ。**
    /// この層に座標は無い。見た目を直したいときは Unity Editor で Prefab を開く。
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        /// <summary>⚠️ 種を固定しておくと、同じ話が何度でも再現できる。</summary>
        /// <summary>⭐ **世界の種を固定するか**（不具合を追うときだけ）。
        /// ⚠️ 既定は false ＝ **端末ごとに引く**。2026-08-21 まで下の定数を
        /// そのまま使っていたので、**全端末がまったく同じ世界**を引いていた
        /// （同じ初期編成・同じ卵・同じ巣）。集める遊びなので、そこが同じでは売りが立たない。</summary>
        public bool PinWorld = false;

        /// <summary><see cref="PinWorld"/> のときに使う種。⚠️ 固定しないなら使わない。</summary>
        public int Seed = 20260816;

        /// <summary>いまの世界の種。⭐ 引いた種はここに残る（保存にも入る）。
        /// ⚠️ 不具合の報告にはこの数が要る。</summary>
        public int WorldSeed { get; private set; }

        /// <summary>試遊のための短縮。⭐ 孵化の待ち時間をこの数で割る。
        /// ⚠️ 1 が本番の速さ。出荷前に 1 へ戻すこと。</summary>
        public int HatchSpeed = 120;

        /// <summary>保存を無視して最初から始める。⚠️ 試遊用。出荷前に false へ。</summary>
        public bool FreshStart;

        public Game Game { get; set; }

        // 画面をまたいで持ち回るもの
        public Nest CurrentNest;
        public bool CurrentIsBoss;
        public BattleState Battle;

        /// <summary>いま潜っている巣の進み具合。⭐ **画面ではなくここが持つ。**
        ///
        /// ⚠️ 盤（StealStage）に持たせない。雑魚と戦うと戦闘画面へ移り、
        /// そのとき盤は畳まれる（カメラを戻すため）。盤が持っていると
        /// **戦って戻った瞬間に、着地した個体も壊した壁も消える**。</summary>
        public Steal.Infiltration Infiltration;

        /// <summary>進行中のすごろく潜入。⚠️ <see cref="Infiltration"/> とは別物。
        /// ⭐ 遊びの経路はこちら（<see cref="TrailScreen"/>）。あちらは移植の証拠として残してある。</summary>
        public Raid Raid;

        /// <summary>雑魚と戦っているマス。⚠️ -1 は「戦っていない」。</summary>
        public int CurrentSpace = -1;


        /// <summary>強奪に成功したか（戦闘を挟まずに卵が手に入ったか）。</summary>
        public EggOrigin PendingOrigin = EggOrigin.Defeated;

        private Screen _screen = Screen.Home;
        private RectTransform _root;
        private Image _sky;
        private FrameView _frame;

        /// <summary>いまの時刻（Unix 秒）。⚠️ Core は時計を持たない。ここが唯一の出所。</summary>
        public long Now() => Hatchery.Now(DateTime.UtcNow);

        /// <summary>新しい世界の種を出す。⭐ **端末ごとに違う**（2026-08-21）。
        ///
        /// ⚠️ <see cref="PinWorld"/> のときだけ <see cref="Seed"/> を使う。
        /// ⭐ 引いた種は保存に入る（`GameSave.Seed`）ので、同じ端末では変わらない。
        /// ⚠️ **`Rng` は Core の物なのでここでは使わない** ── Core は時計を知らない決まり。
        /// 種を作るのは画面の仕事。</summary>
        private int NewSeed()
        {
            if (PinWorld) return Seed;
            // ⭐ 時計と GUID を混ぜる。⚠️ 時計だけだと、同じ秒に始めた端末が揃う
            long mixed = DateTime.UtcNow.Ticks ^ ((long)Guid.NewGuid().GetHashCode() << 16);
            int drawn = unchecked((int)(mixed ^ (mixed >> 32)));
            return drawn == 0 ? 1 : drawn;
        }

        /// <summary>演出を載せる場所。⚠️ App 本体ではなく Canvas。
        /// 本体に載せると RectTransform の親が無く、画面のどこにも出ない。</summary>
        public RectTransform Overlay => _root;

        /// <summary>⭐ **揺らす相手。**画面ごと震わせたいときはここへ。
        ///
        /// ⚠️ <see cref="Overlay"/>（＝ Canvas そのもの）は揺らせない。
        /// Screen Space の Canvas は自分の RectTransform を**毎フレーム画面の大きさへ戻す**ので、
        /// ずらしても次のフレームで消える（＝何も起きないのに動いたつもりになる）。
        /// ⭐ 1つ内側の枠を揺らす ── 空（背景）は動かず、UI だけが震える。</summary>
        public RectTransform Stage =>
            _frame == null ? _root : (RectTransform)_frame.transform;

        /// <summary>いま出ている画面。⚠️ 告知の後始末が
        /// 「まだその画面に居るか」を確かめるために要る（レビュー指摘 2026-08-20）。</summary>
        public Screen Showing => _screen;

        private void Start()
        {
            // ⭐ 保存があれば続きから。無ければ新しく始める
            // ⚠️ **「保存が無い」と「読めなかった」を分ける。**
            //    分けないと、読めなかった日に新しいゲームを作り、20秒後にそれを
            //    元のファイルへ書き戻して、遊んだ結果が復旧不能に消える。
            Game = FreshStart ? null : SaveFile.Read(out _readFailed);
            // ⚠️ 時刻を渡す。渡さないと最初の3つの巣が**期限を持たない**まま作られ、
            //    「巣ごとに居座る時間がある」という規則がその巣にだけ効かない
            if (Game == null) Game = Games.NewGame(NewSeed(), Now());
            WorldSeed = Game.Seed;
            // ⭐ 編成をここで確定させる。⚠️ 通さないと、良い個体を手に入れた瞬間に
            //    「素質の高い順」で埋め直されて、選んだ3体が黙って入れ替わる
            Games.LockParty(Game);
            BuildCanvas();
            Show(Screen.Home);
        }

        // ⚠️ 保存は「画面を組み直したとき」に書く。操作のたびに Refresh が走るので、
        //    そこに乗せておけば取りこぼさない。毎フレームは書かない（放置で焼き付く）
        private float _sinceSave;
        /// <summary>保存を書く間隔（秒）。⭐ 放置で溜まる素材はこの間隔で落ちる。</summary>
        private const float SaveEvery = 20f;

        private void Update()
        {
            if (Game == null) return;
            _sinceSave += Time.unscaledDeltaTime;
            if (_sinceSave < SaveEvery) return;
            // ⚠️ **必ず Save() を通す。**ここで SaveFile.Write を直に呼んでいた頃は、
            //    「読めなかったら書かない」のような約束をこの経路だけがすり抜けた
            Save();
        }

        // ⚠️ 閉じる/隠れるときに必ず書く。Android は OnApplicationQuit が来ないことがある
        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Save(); }
        private void OnApplicationQuit() => Save();

        /// <summary>右肩を押せる入口にする。⚠️ 画面を組んだあとに呼ぶ
        /// （Show の中で Bind が走ったあとでないと消される）。</summary>
        public void ShowExtra(string label, System.Action onTap)
        {
            if (_frame != null) _frame.ShowExtra(label, onTap);
        }

        /// <summary>いま書く。⭐ 状態が変わる操作のあとに呼ぶ。
        ///
        /// ⚠️ **中身が変わっていなければ書かない。**
        /// <see cref="Refresh"/> は演出の拍ごとに呼ばれる（戦闘は1手に3〜4回）ので、
        /// 素通しにすると1戦で 60〜200回のフル書き込みになる。
        /// ⭐ 書き出した文字を憶えておいて、同じなら捨てる。
        /// ⚠️ 「保存する場所を減らす」方向で直さない ── 呼び忘れた瞬間に遊んだ結果が消える。</summary>
        public void Save()
        {
            if (Game == null) return;
            // ⚠️ **読めなかった保存の上には書かない。**このまま書くと、直せたはずの
            //    ファイル（版が新しすぎるだけ、等）が作り直した中身で潰れる。
            //    ⭐ 遊べはする。次に正しく読める版で開けば続きから戻る
            if (_readFailed) return;
            _sinceSave = 0f;
            _lastSaved = SaveFile.Write(Game, _lastSaved);
        }

        /// <summary>最後に書き出した中身。⚠️ 比べるためだけに持つ。</summary>
        private string _lastSaved;

        /// <summary>保存が在るのに読めなかった。⚠️ 立っている間は一切書かない。</summary>
        private bool _readFailed;

        // ── 器 ──────────────────────────────────────────

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("App Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            // ⚠️ Overlay にしない。カメラの描画に入らないと撮影に写らない
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 10f;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Ui.W, Ui.H);
            // 縦持ちなので幅に合わせる
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            _root = canvasGo.GetComponent<RectTransform>();

            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var events = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }

            var sky = Ui.Rect("Sky", _root);
            Ui.Stretch(sky);
            _sky = sky.gameObject.AddComponent<Image>();

            var frame = Resources.Load<GameObject>("Prefabs/AppFrame");
            if (frame == null)
            {
                // ⚠️ 黙って何も出さない、をしない。無いことに気づけないほうが困る
                Debug.LogError("AppFrame.prefab が読めない（Egg Command/画面を Prefab に書き出す を走らせる）");
                return;
            }
            _frame = Instantiate(frame, _root).GetComponent<FrameView>();
        }

        /// <summary>画面を差し替える。⚠️ 毎回すべて組み直す。
        /// 差分で描くと、状態と見た目が食い違ったときに追えなくなる。</summary>
        public void Show(Screen screen)
        {
            // ⚠️ Play 中にスクリプトを直すと Unity はドメインを作り直す。
            //    Game はただの C# オブジェクトなので、そこで null に戻る（Start は再実行されない）。
            //    握りつぶすと「押しても何も起きない」になって原因が見えないので、作り直して続ける。
            if (Game == null)
            {
                Debug.LogWarning("Game が失われていた（Play 中の再コンパイル）。作り直して続ける");
                Game = Games.NewGame(NewSeed(), Now());
                WorldSeed = Game.Seed;
                Battle = null;
                Infiltration = null;
                Raid = null;
                CurrentSpace = -1;
            }
            if (_frame == null) return;

            // ⚠️ 強奪の盤はワールド空間に居るので、画面を離れるときに自分で片付ける。
            //    残すとカメラの寸法が戻らず、次の画面が拡大されたままになる。
            if (_screen == Screen.Battle && screen != Screen.Battle) BattleScreen.Leave();

            _screen = screen;
            bool home = screen == Screen.Home;

            // ⚠️ 強奪だけは盤がワールド空間に居る。地を塗ると UI が世界を隠してしまう
            var sky = SkyOf(screen);
            _sky.sprite = Ui.SkySpriteOf(sky);
            _sky.color = _sky.sprite != null ? Color.white : Ui.SkyOf(sky);
            _sky.raycastTarget = true;

            // ⚠️ **戦闘中と潜入中は戻れない。**戻れると、
            //    不利な盤面をいつでも無かったことにできてしまう。
            // ⚠️ 潜入も同じ ── ‹ で抜けられると、親と戦う羽目になる前に何度でも
            //    やり直せてしまう（出目は状態から決まるので、選び方を変えて総当たりできた。
            //    レビューで発覚 2026-08-20）。
            bool canBack = screen != Screen.Battle && screen != Screen.Trail;
            // ⚠️ タブのある画面に ‹ を出さない。⭐ タブが行き先を全部持っているので、
            //    ‹ は「ホームへ戻る」だけの重複した道になる（2026-08-20）
            bool showBack = false;
            // ⭐ タブバーは**常設**（作者の指示 2026-08-20）。
            // ⚠️ 戻れない画面では出さない ── タブも「戻る道」なので、
            //    ‹ を隠しておいてタブから抜けられては意味がない
            _frame.Bind(home, TitleOf(screen), BadgeOf(screen),
                showBack ? (System.Action)(() => Show(Screen.Home)) : null, showBack, canBack);
            // ⚠️ 孵化はホームへ移したのでドックから外した。⭐ 札は4枚（ホームもタブ）
            // ⚠️ 0 を出さない。⭐ 数が無いことは**書かない**ことで伝わる
            _frame.BindPanel(0, "ホーム",
                Game.Incubating.Count > 0 ? $"{Game.Incubating.Count}" : "",
                () => Show(Screen.Home), screen == Screen.Home);
            _frame.BindPanel(1, "探索", $"{Game.Encounters.Count}",
                () => Show(Screen.Nests), screen == Screen.Nests);
            _frame.BindPanel(2, "配合", $"{Game.Storage.Creatures.Count}体",
                () => Show(Screen.Breed), screen == Screen.Breed);
            _frame.BindPanel(3, "BOX", $"{Game.Storage.Creatures.Count}/{Game.Storage.Slots}",
                () => Show(Screen.Box), screen == Screen.Box);
            _frame.HidePanelsFrom(4);

            var body = _frame.Body;
            // ⚠️ View で唯一ここだけ素通しだった。AppFrame から Body を消すと
            //    毎フレーム落ちて、真っ黒のまま何も動かなくなる
            if (body == null)
            {
                Debug.LogError("AppFrame に Body が無い（各画面を入れる場所）。"
                    + "「画面に足りない部品を足す」で戻せる");
                return;
            }
            // ⚠️ Destroy はフレームの終わりまで効かない。
            //    そのまま組み直すと、同じフレームのあいだ古い画面が生きていて、
            //    見えない古いボタンがクリックを受け取る（実測で3枚積み重なった）。
            for (int i = body.childCount - 1; i >= 0; i--)
            {
                var child = body.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                Destroy(child);
            }

            switch (screen)
            {
                case Screen.Home: HomeScreen.Build(this, body); break;
                case Screen.Nests: NestsScreen.Build(this, body); break;
                case Screen.Trail: TrailScreen.Build(this, body); break;
                case Screen.Battle: BattleScreen.Build(this, body); break;
                case Screen.Breed: BreedScreen.Build(this, body); break;
                case Screen.Box: BoxScreen.Build(this, body); break;
                case Screen.Trial: TrialScreen.Build(this, body); break;
                case Screen.Book: BookScreen.Build(this, body); break;
            }

            // ⚠️ **タブバーは本体の下 232px を覆う。**Body は縮まないので、
            //    下端まで中身がある画面だけが隠れる。
            // ⭐ 実測（2026-08-20）で食い込むのは**スクロールする層だけ**だったので、
            //    そこだけ縮める。⚠️ 固定配置の部品には触らない（動かすと配置の出所が2つになる）
            if (canBack) ReserveDock(body);
        }

        /// <summary>タブバーのぶん、スクロールする層を縮める。
        ///
        /// ⚠️ 中身を動かさない ── **窓を小さくするだけ**。
        /// ⭐ こうすると、下端の1行がタブバーの下へ潜らず、最後まで送れる。</summary>
        private static void ReserveDock(RectTransform body)
        {
            // ⚠️ **測る前に組み終わらせる。**組み上がる前の rect は 0 なので、
            //    そのまま測ると「食い込んでいない」と誤判定する
            Canvas.ForceUpdateCanvases();

            var frame = new Vector3[4];
            body.GetWorldCorners(frame);
            float span = frame[1].y - frame[0].y;
            if (span <= 0f || body.rect.height <= 0f) return;
            // ⭐ 設計の1px が世界でいくつか
            float unit = span / body.rect.height;
            // ⭐ 帯の上端（世界）。ここより下へ出た窓を詰める
            float limit = frame[0].y + Ui.DockHeight * unit;

            foreach (var scroll in body.GetComponentsInChildren<ScrollRect>(true))
            {
                var view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
                // ⚠️ **直の親の座標で測らない。**⭐ 窓は入れ子になりうる
                //    （Body → Stack → Grid）ので、途中の親のぶんが丸ごと抜ける。
                //    実測 2026-08-21: 配合の一覧が本体を **126px** はみ出して
                //    タブ帯を貫通していたのに、この関数は素通りしていた。
                var window = new Vector3[4];
                view.GetWorldCorners(window);
                float over = (limit - window[0].y) / unit;
                if (over <= 0.5f) continue;
                view.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(120f, view.rect.height - over));
            }
        }

        /// <summary>今の画面をそのまま組み直す（操作のあと）。
        /// ⭐ 状態が変わったあとに必ず通るので、保存もここに乗せる。</summary>
        public void Refresh()
        {
            Save();
            Show(_screen);
        }

        /// <summary>Prefab を1枚置く。⚠️ 読めなければ黙って飛ばさず、はっきり残す。</summary>
        public T Put<T>(RectTransform body, string name) where T : Component
        {
            var prefab = Resources.Load<GameObject>("Prefabs/" + name);
            if (prefab == null)
            {
                Debug.LogError($"{name}.prefab が読めない（Egg Command/画面を Prefab に書き出す を走らせる）");
                return null;
            }
            return Instantiate(prefab, body).GetComponent<T>();
        }

        private static Sky SkyOf(Screen screen)
        {
            switch (screen)
            {
                case Screen.Home: return Sky.Home;
                case Screen.Nests:
                case Screen.Trail:
                case Screen.Trial: return Sky.Nest;
                case Screen.Book: return Sky.Nest;
                case Screen.Battle: return Sky.Battle;
                case Screen.Breed: return Sky.Breed;
                default: return Sky.Box;
            }
        }

        private string TitleOf(Screen screen)
        {
            switch (screen)
            {
                case Screen.Home: return "EGG COMMAND";
                case Screen.Nests: return "探索";
                case Screen.Trail: return CurrentNest != null ? CurrentNest.Name : "強奪";
                case Screen.Battle: return CurrentIsBoss ? Nests.BossName : "戦闘";
                case Screen.Breed: return "配合";
                case Screen.Trial: return "試練";
                case Screen.Book: return "図鑑";
                default: return "BOX";
            }
        }

        /// <summary>右肩の状態。⚠️ 数えられる事実だけを置く。</summary>
        private string BadgeOf(Screen screen)
        {
            switch (screen)
            {
                // ⭐ **溜まっている EXP をここにも出す**（2026-08-21・作者の指示）。
                // ⚠️ ホームにしか出ていなかったので、BOX で「レベルアップ +1 EXP 122」を
                //    見ても、足りているのかが分からなかった。
                // ⚠️ **体数はここに出さない。**⭐ 下のタブが既に出している
                //    （「BOX 44/50」「配合 44体」）。両方出したら枠に入らなかった
                //    ── 実測「EXP 19,475　44/50」は 315 要るのに枠は 252（2026-08-21）。
                case Screen.Box:
                case Screen.Breed:
                    return $"EXP {Ui.Digits(Game.Idle.Exp)}";
                // ⚠️ 行動回数は出さない。⭐ 数えて楽しむものではなく、
                //    出すと「減らすべき数」に見えてしまう
                case Screen.Battle: return "";
                default: return "";
            }
        }

        // ── 進行 ────────────────────────────────────────

        /// <summary>巣へ挑む。⚠️ 守り手は挑むたびに作り直す（同じ巣でも顔ぶれが変わる）。</summary>
        /// <param name="carry">潜入から続けて戦うなら、その潜入。
        /// ⭐ **負った傷と CT を持ち込む**（雑魚と戦うほど親戦が苦しくなる）。</param>
        public void EnterBattle(Nest nest, bool boss, Steal.Infiltration carry = null)
        {
            CurrentNest = nest;
            CurrentIsBoss = boss;
            CurrentSpace = -1;
            PendingOrigin = EggOrigin.Defeated;
            var enemies = boss ? Nests.MakeBossParty() : Games.DefendersOf(Game, nest);
            StartBattle(enemies, carry?.Hp, carry?.Cooldowns);
        }

        /// <summary>いま挑んでいる試練。⚠️ 試練でないときは null。</summary>
        public Trial CurrentTrial { get; private set; }

        /// <summary>試練へ挑む。⭐ **顔ぶれは毎回まったく同じ**（<see cref="Trials.PartyOf"/>）。
        ///
        /// ⚠️ 巣の欄（<c>CurrentNest</c>）は空にする。⭐ 空にしないと、決着のときに
        /// 「巣を引き直す」「卵を出す」といった巣の後始末が動いてしまう。</summary>
        public void EnterTrial(Trial trial)
        {
            CurrentNest = null;
            CurrentIsBoss = false;
            CurrentSpace = -1;
            CurrentTrial = trial;
            StartBattle(Trials.PartyOf(trial), null, null);
        }

        /// <summary>すごろく潜入から親戦へ。⭐ 負った傷と CT をそのまま持ち込む。</summary>
        public void EnterBattle(Nest nest, bool boss,
            System.Collections.Generic.List<int> hp,
            System.Collections.Generic.List<int[]> cooldowns)
        {
            CurrentNest = nest;
            CurrentIsBoss = boss;
            CurrentSpace = -1;
            PendingOrigin = EggOrigin.Defeated;
            var enemies = boss ? Nests.MakeBossParty() : Games.DefendersOf(Game, nest);
            StartBattle(enemies, hp, cooldowns);
        }

        /// <summary>すごろくの道中の雑魚と戦う。⭐ **3対3**。
        ///
        /// ⚠️ 相手は巣とマスの番号だけで決まる。その場で引くと、
        /// 画面を出入りするだけで顔ぶれを選び直せてしまう。</summary>
        public void EnterTrailMobBattle(Nest nest, int space)
        {
            CurrentNest = nest;
            CurrentIsBoss = false;
            CurrentSpace = space;
            PendingOrigin = EggOrigin.Defeated;
            var enemies = Steal.MobPartyOf(nest, Games.RaidsOn(Game, nest), space);
            StartBattle(enemies, Raid?.Hp, Raid?.Cooldowns);
        }


        private void StartBattle(System.Collections.Generic.List<Creature> enemies,
            System.Collections.Generic.List<int> hp,
            System.Collections.Generic.List<int[]> cooldowns)
        {
            // ⭐ **戦闘ごとに引く。**⚠️ 渡さないと `CreateBattle` の既定（固定の種）が使われ、
            //    弱化が通るかが**毎回同じテープ**になる ── 同じ編成なら必ず同じ試合。
            //    ⭐ `Game.RngBattle` は保存されるので、**落として引き直すことはできない**。
            Battle = Core.Battle.CreateBattle(Games.PartyOf(Game), enemies, Game.RngBattle);
            // ⭐ 潜入で負った傷と CT をそのまま持ち込む
            if (hp != null && cooldowns != null) Core.Battle.CarryIn(Battle, hp, cooldowns);
            // ⚠️ 前の戦闘の帯を忘れる。残ると初手から満タンに見える
            UnitStand.ForgetGauges();
            Show(Screen.Battle);
        }

        /// <summary>戦闘の決着を状態へ反映する。
        /// ⭐ 報酬は出撃していた個体だけがもらう（連れ出すことが育成に直結する）。</summary>
        public void FinishBattle()
        {
            if (Battle == null || Battle.Result == null) return;
            var won = Battle.Result == Outcome.Ally;
            var nest = CurrentNest;
            var state = Battle;
            Battle = null;

            // ⭐ 雑魚戦は潜入の途中。⚠️ 卵も巣の差し替えもここでは起きない
            if (CurrentSpace >= 0) { FinishTrailMobBattle(state, nest, won); return; }
            if (CurrentTrial != null) { FinishTrial(won); return; }

            if (won)
            {
                Games.GrowParty(Games.PartyOf(Game));
                if (!CurrentIsBoss && nest != null)
                {
                    // ⭐ **戦って倒したら親は失われる。**その巣にはもう挑めない。
                    // ⚠️ 倒しても巣が残ると「迷ったら倒せばいい」で全部片付き、
                    //    潜入が「やってもやらなくてもいい前座」になる。
                    GainEgg(nest, PendingOrigin, closeNest: true);
                    return;
                }
            }
            // ⚠️ 負けた巣も引き直す。同じ相手を叩き続ける形にしない
            if (!CurrentIsBoss && nest != null) Encounters.Replace(Game, nest, Now());
            Show(Screen.Nests);
        }

        /// <summary>試練の決着。⭐ **返るのは勝った印だけ**（卵も EXP も出さない）。
        ///
        /// ⚠️ 出撃していた個体の育成 +1 は、他の戦闘とまったく同じ扱いで付く
        /// ── 試練だけ特別扱いにしない。
        /// ⚠️ 負けても何も失わない。⭐ 「組み直して挑み直す」のが遊びなので、
        /// 挑むこと自体に対価を付けない。</summary>
        private void FinishTrial(bool won)
        {
            var trial = CurrentTrial;
            CurrentTrial = null;
            if (won && trial != null)
            {
                Games.GrowParty(Games.PartyOf(Game));
                // ⭐ 初めて勝ったときだけ告げる。⚠️ 2度目以降に同じ帯を出すと、
                //    「何か新しく起きた」と読めてしまう
                if (Games.MarkTrial(Game, trial.Id))
                {
                    BannerView.Show(Overlay, $"試練 {Trials.StepOf(trial.Id)}「{trial.Name}」を越えた", null);
                }
            }
            Show(Screen.Trial);
        }

        /// <summary>すごろくの雑魚戦の決着。⭐ 勝てば**振れる回数が戻って**続きへ。
        ///
        /// ⚠️ 傷と CT を潜入へ書き戻してから <see cref="Trails.Beat"/> を呼ぶ。
        /// 書き戻しを飛ばすと、次の戦いが毎回満タンから始まり、
        /// 「戦うほど苦しくなる」という雑魚の対価が丸ごと消える。</summary>
        private void FinishTrailMobBattle(BattleState state, Nest nest, bool won)
        {
            var raid = Raid;
            CurrentSpace = -1;
            if (raid == null) { Show(Screen.Nests); return; }

            if (!won)
            {
                Trails.Lost(raid);
                Raid = null;
                // ⚠️ 負けた巣は引き直す（親に見つかって負けたときと同じ）
                if (nest != null) Encounters.Replace(Game, nest, Now());
                Show(Screen.Nests);
                return;
            }

            Core.Battle.CarryOut(state, raid.Hp, raid.Cooldowns);
            Trails.Beat(raid);
            Games.GrowParty(Games.PartyOf(Game), Steal.MobReward);
            Show(Screen.Trail);
        }


        /// <summary>卵を1個手に入れる。⭐ 手に入れた瞬間だけは演出を出す。</summary>
        /// <param name="closeNest">その巣を閉じるか。
        /// ⭐ **戦って倒したときだけ true**（親が失われるので、もう挑めない）。
        /// ⚠️ 盗んだときは **false**。閉じてしまうと同じ巣に二度と行けず、
        /// 「盗むたびに守りが固くなり4回で封鎖される」という巣の寿命が丸ごと働かない
        /// （実際そうなっていた）。</param>
        public void GainEgg(Nest nest, EggOrigin how, bool closeNest)
        {
            // ⚠️ 遊びの経路は TakeEgg（素質も孵化時間も★だけで決まる）。
            //    GainEgg は移植元の規則で、較正済みの照合が踏んでいる
            var egg = Games.TakeEgg(Game, nest, how);
            if (closeNest) Encounters.Replace(Game, nest, Now());
            Fanfare.EggGot(_root, egg, () => Show(Screen.Nests));
        }
    }
}
