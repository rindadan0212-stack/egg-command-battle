using System;
using System.Collections.Generic;
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
        Hatch,
        Breed,
        Box,
    }

    /// <summary>画面の器と行き来。⭐ ホームがハブ。各画面は ‹ でホームへ戻る。
    ///
    /// ⚠️ 常時タブにしない（モックがそうなっている）。下段の4パネルはホームだけに出る。
    ///
    /// ⭐ 状態の唯一の出所は <see cref="Core.Game"/>。この層は**それを描くだけ**で、
    /// 勝敗も遺伝も飛距離もここでは決めない（決めた瞬間に第2の出所ができる）。
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        /// <summary>⚠️ 種を固定しておくと、同じ話が何度でも再現できる。</summary>
        public int Seed = 20260816;

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
        private RectTransform _body;
        private Text _title;
        private Text _badge;
        private RectTransform _backSlot;

        private void Start()
        {
            Game = Games.NewGame(Seed);
            BuildFrame();
            Show(Screen.Home);
        }

        // ── 器 ──────────────────────────────────────────

        private void BuildFrame()
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

            // ⚠️ 強奪の盤はワールド空間に居るので、画面を離れるときに自分で片付ける。
            //    残すとカメラの寸法が戻らず、次の画面が拡大されたままになる。
            if (_screen == Screen.Steal && screen != Screen.Steal) StealScreen.Leave();
            if (_screen == Screen.Battle && screen != Screen.Battle) BattleScreen.Leave();

            _screen = screen;

            // ⚠️ Destroy はフレームの終わりまで効かない。
            //    そのまま組み直すと、同じフレームのあいだ古い画面が生きていて、
            //    見えない古いボタンがクリックを受け取る（実測で3枚積み重なった）。
            //    親から外して無効にし、その場で居なくする。
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                var child = _root.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                Destroy(child);
            }

            var back = Ui.Rect("Sky", _root);
            Ui.Stretch(back);
            var backImage = back.gameObject.AddComponent<Image>();
            // ⚠️ 強奪だけは盤がワールド空間に居る。地を塗ると UI が世界を隠してしまうので、
            //    ここは透明にしてカメラの背景を見せる。
            if (screen == Screen.Steal)
            {
                backImage.color = new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                var sky = SkyOf(screen);
                // ⭐ 空→砂のグラデーション。地平線があると「立っている場所」が分かる
                backImage.sprite = Ui.SkySpriteOf(sky);
                // ⚠️ 絵が無いときは単色へ落ちる。黙って透明にしない
                backImage.color = backImage.sprite != null ? Color.white : Ui.SkyOf(sky);
            }
            backImage.raycastTarget = screen != Screen.Steal;

            bool home = screen == Screen.Home;
            float bodyTop = Ui.TopBarHeight;
            float bodyHeight = Ui.H - bodyTop - (home ? Ui.DockHeight : 0f);

            _body = Ui.Rect("Body", _root);
            Ui.Place(_body, 0f, bodyTop, Ui.W, bodyHeight);

            switch (screen)
            {
                case Screen.Home: HomeScreen.Build(this, _body, bodyHeight); break;
                case Screen.Nests: NestsScreen.Build(this, _body, bodyHeight); break;
                case Screen.Steal: StealScreen.Build(this, _body, bodyHeight); break;
                case Screen.Battle: BattleScreen.Build(this, _body, bodyHeight); break;
                case Screen.Hatch: HatchScreen.Build(this, _body, bodyHeight); break;
                case Screen.Breed: BreedScreen.Build(this, _body, bodyHeight); break;
                case Screen.Box: BoxScreen.Build(this, _body, bodyHeight); break;
            }

            // ⚠️ 上段は本体を組んだ**あと**に作る。
            //    先に作ると、戦闘画面が敵の手番を進める前の数（行動 0）を出してしまう。
            BuildTopBar(screen);

            if (home) BuildDock();
        }

        /// <summary>今の画面をそのまま組み直す（操作のあと）。</summary>
        public void Refresh() => Show(_screen);

        private static Sky SkyOf(Screen screen)
        {
            switch (screen)
            {
                case Screen.Home: return Sky.Home;
                case Screen.Nests:
                case Screen.Steal: return Sky.Nest;
                case Screen.Battle: return Sky.Battle;
                case Screen.Hatch: return Sky.Hatch;
                case Screen.Breed: return Sky.Breed;
                default: return Sky.Box;
            }
        }

        private void BuildTopBar(Screen screen)
        {
            // ⚠️ 帯を敷かない。字を濃紺にしたので、暗い帯の上では題字が消える。
            //    モックも上段に帯を持たず、地の上に直接置いている。
            var bar = Ui.Rect("TopBar", _root);
            Ui.Place(bar, 0f, 0f, Ui.W, Ui.TopBarHeight);

            // ⚠️ ホーム以外は必ず戻れる。戻れない画面を作らない
            if (screen != Screen.Home)
            {
                Ui.Tappable(bar, "Back", "‹", () => Show(Screen.Home),
                    12f, 10f, 112f, 112f);
            }

            // STAR: text placed straight on the sky is knocked out, so it reads on any colour
            _title = Ui.Knockout(Ui.Label(bar, "Title", TitleOf(screen), 40, Ui.Ink,
                TextAnchor.MiddleCenter, 140f, 0f, Ui.W - 280f, Ui.TopBarHeight));

            string badge = BadgeOf(screen);
            _badge = Ui.Knockout(Ui.Label(bar, "Badge", badge, 28, Ui.InkDim,
                TextAnchor.MiddleRight, Ui.W - 300f, 0f, 300f - Ui.Margin, Ui.TopBarHeight), 3);
        }

        private string TitleOf(Screen screen)
        {
            switch (screen)
            {
                case Screen.Home: return "EGG COMMAND";
                case Screen.Nests: return "探索";
                case Screen.Steal: return CurrentNest != null ? CurrentNest.Name : "強奪";
                case Screen.Battle: return CurrentIsBoss ? Nests.BossName : "戦闘";
                case Screen.Hatch: return "孵化";
                case Screen.Breed: return "配合";
                default: return "BOX";
            }
        }

        /// <summary>右肩の状態。⚠️ 数えられる事実だけを置く。</summary>
        private string BadgeOf(Screen screen)
        {
            switch (screen)
            {
                case Screen.Hatch: return $"卵 {Game.Eggs.Count}";
                case Screen.Box:
                case Screen.Breed: return $"{Game.Storage.Creatures.Count}/{Game.Storage.Slots}";
                case Screen.Battle: return Battle == null ? "" : $"行動 {Battle.Actions}";
                default: return "";
            }
        }

        private void BuildDock()
        {
            // WARN: no dark strip. Text turned navy, so a dark strip swallows the sub-labels.
            var dock = Ui.Rect("Dock", _root);
            Ui.Place(dock, 0f, Ui.H - Ui.DockHeight, Ui.W, Ui.DockHeight);

            float gap = 16f;
            float width = (Ui.W - Ui.Margin * 2f - gap * 3f) / 4f;
            float top = (Ui.DockHeight - 148f) / 2f;

            // ⭐ 主導線は1つだけ塗る
            Panel(dock, "探索", $"巣 {Nests.All.Length}", true, Ui.Margin, top, width,
                () => Show(Screen.Nests));
            Panel(dock, "孵化", $"{Game.Eggs.Count}", false, Ui.Margin + (width + gap), top, width,
                () => Show(Screen.Hatch));
            Panel(dock, "配合", $"{Game.Storage.Creatures.Count}体", false, Ui.Margin + (width + gap) * 2f, top, width,
                () => Show(Screen.Breed));
            Panel(dock, "BOX", $"{Game.Storage.Creatures.Count}/{Game.Storage.Slots}", false,
                Ui.Margin + (width + gap) * 3f, top, width, () => Show(Screen.Box));
        }

        private void Panel(Transform parent, string label, string count, bool lead,
            float left, float top, float width, Action onGo)
        {
            var button = Ui.Tappable(parent, $"Dock {label}", "", onGo, left, top, width, 148f, lead);
            Ui.Label(button.transform, "Name", label, 32,
                lead ? new Color32(0x1a, 0x16, 0x12, 0xff) : Ui.Ink,
                TextAnchor.UpperCenter, 0f, 28f, width, 44f);
            // ⚠️ 淡い字を色の札の上に置かない。数が読めなくなる（実測で消えていた）
            Ui.Label(button.transform, "Count", count, 24, Ui.Ink,
                TextAnchor.UpperCenter, 0f, 84f, width, 36f);
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
            Show(Screen.Battle);
        }

        /// <summary>戦闘の決着を状態へ反映する。
        /// ⭐ 報酬は出撃していた個体だけがもらう（連れ出すことが育成に直結する）。</summary>
        public void FinishBattle()
        {
            if (Battle == null || Battle.Result == null) return;
            // ⚠️ 何が起きたかを字で残さない。⭐ 勝てば孵化の数が増えている。それが報告。
            if (Battle.Result == Outcome.Ally)
            {
                Games.AwardParty(Games.PartyOf(Game));
                if (!CurrentIsBoss && CurrentNest != null)
                {
                    Games.GainEgg(Game, CurrentNest, PendingOrigin);
                }
            }
            Battle = null;
            Show(Screen.Nests);
        }
    }
}
