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
        Steal,
        Battle,
        Breed,
        Box,
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
        public int Seed = 20260816;

        /// <summary>試遊のための短縮。⭐ 孵化の待ち時間をこの数で割る。
        /// ⚠️ 1 が本番の速さ。出荷前に 1 へ戻すこと。</summary>
        public int HatchSpeed = 120;

        /// <summary>保存を無視して最初から始める。⚠️ 試遊用。出荷前に false へ。</summary>
        public bool FreshStart;

        public Game Game { get; set; }

        // 画面をまたいで持ち回るもの
        public Nest CurrentNest;
        public bool CurrentIsBoss;
        public StealField Field;
        public BattleState Battle;

        /// <summary>強奪に成功したか（戦闘を挟まずに卵が手に入ったか）。</summary>
        public EggOrigin PendingOrigin = EggOrigin.Defeated;

        private Screen _screen = Screen.Home;
        private RectTransform _root;
        private Image _sky;
        private FrameView _frame;

        /// <summary>いまの時刻（Unix 秒）。⚠️ Core は時計を持たない。ここが唯一の出所。</summary>
        public long Now() => Hatchery.Now(DateTime.UtcNow);

        /// <summary>演出を載せる場所。⚠️ App 本体ではなく Canvas。
        /// 本体に載せると RectTransform の親が無く、画面のどこにも出ない。</summary>
        public RectTransform Overlay => _root;

        private void Start()
        {
            // ⭐ 保存があれば続きから。無ければ新しく始める
            Game = FreshStart ? null : SaveFile.Read();
            if (Game == null) Game = Games.NewGame(Seed);
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
            _sinceSave = 0f;
            SaveFile.Write(Game);
        }

        // ⚠️ 閉じる/隠れるときに必ず書く。Android は OnApplicationQuit が来ないことがある
        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Save(); }
        private void OnApplicationQuit() => Save();

        /// <summary>いま書く。⭐ 状態が変わる操作のあとに呼ぶ。</summary>
        public void Save()
        {
            if (Game == null) return;
            _sinceSave = 0f;
            SaveFile.Write(Game);
        }

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
                Game = Games.NewGame(Seed);
                Battle = null;
                Field = null;
            }
            if (_frame == null) return;

            // ⚠️ 強奪の盤はワールド空間に居るので、画面を離れるときに自分で片付ける。
            //    残すとカメラの寸法が戻らず、次の画面が拡大されたままになる。
            if (_screen == Screen.Steal && screen != Screen.Steal) StealScreen.Leave();
            if (_screen == Screen.Battle && screen != Screen.Battle) BattleScreen.Leave();

            _screen = screen;
            bool home = screen == Screen.Home;

            // ⚠️ 強奪だけは盤がワールド空間に居る。地を塗ると UI が世界を隠してしまう
            var sky = SkyOf(screen);
            _sky.sprite = screen == Screen.Steal ? null : Ui.SkySpriteOf(sky);
            _sky.color = screen == Screen.Steal ? new Color(0f, 0f, 0f, 0f)
                : _sky.sprite != null ? Color.white : Ui.SkyOf(sky);
            _sky.raycastTarget = screen != Screen.Steal;

            _frame.Bind(home, TitleOf(screen), BadgeOf(screen), () => Show(Screen.Home));
            // ⚠️ 孵化はホームへ移したのでドックから外した。札は3枚
            _frame.BindPanel(0, "探索", $"{Game.Encounters.Count}", () => Show(Screen.Nests));
            _frame.BindPanel(1, "配合", $"{Game.Storage.Creatures.Count}体", () => Show(Screen.Breed));
            _frame.BindPanel(2, "BOX", $"{Game.Storage.Creatures.Count}/{Game.Storage.Slots}",
                () => Show(Screen.Box));
            _frame.HidePanelsFrom(3);

            var body = _frame.Body;
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
                case Screen.Steal: StealScreen.Build(this, body); break;
                case Screen.Battle: BattleScreen.Build(this, body); break;
                case Screen.Breed: BreedScreen.Build(this, body); break;
                case Screen.Box: BoxScreen.Build(this, body); break;
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
                case Screen.Steal: return Sky.Nest;
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
                case Screen.Steal: return CurrentNest != null ? CurrentNest.Name : "強奪";
                case Screen.Battle: return CurrentIsBoss ? Nests.BossName : "戦闘";
                case Screen.Breed: return "配合";
                default: return "BOX";
            }
        }

        /// <summary>右肩の状態。⚠️ 数えられる事実だけを置く。</summary>
        private string BadgeOf(Screen screen)
        {
            switch (screen)
            {
                case Screen.Box:
                case Screen.Breed: return $"{Game.Storage.Creatures.Count}/{Game.Storage.Slots}";
                case Screen.Battle: return Battle == null ? "" : $"行動 {Battle.Actions}";
                default: return "";
            }
        }

        // ── 進行 ────────────────────────────────────────

        /// <summary>巣へ挑む。⚠️ 守り手は挑むたびに作り直す（同じ巣でも顔ぶれが変わる）。</summary>
        public void EnterBattle(Nest nest, bool boss)
        {
            CurrentNest = nest;
            CurrentIsBoss = boss;
            PendingOrigin = EggOrigin.Defeated;
            var enemies = boss ? Nests.MakeBossParty() : Games.DefendersOf(Game, nest);
            Battle = Core.Battle.CreateBattle(Games.PartyOf(Game), enemies);
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
            Battle = null;

            if (won)
            {
                Games.GrowParty(Games.PartyOf(Game));
                if (!CurrentIsBoss && nest != null)
                {
                    GainEgg(nest, PendingOrigin);
                    return;
                }
            }
            // ⚠️ 負けた巣も引き直す。同じ相手を叩き続ける形にしない
            if (!CurrentIsBoss && nest != null) Encounters.Replace(Game, nest);
            Show(Screen.Nests);
        }

        /// <summary>卵を1個手に入れる。⭐ 手に入れた瞬間だけは演出を出す。</summary>
        public void GainEgg(Nest nest, EggOrigin how)
        {
            var egg = Games.GainEgg(Game, nest, how);
            Encounters.Replace(Game, nest);
            Fanfare.EggGot(_root, egg, () => Show(Screen.Nests));
        }
    }
}
