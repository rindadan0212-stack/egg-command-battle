using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EggCommand.Core;
using EggCommand.View;
using UnityEditor;
using UnityEngine;

namespace EggCommand.EditorTools
{
    /// <summary>画面を**決まった順に全部撮る**。
    ///
    /// ⭐ **撮る箇所の唯一の出所は <see cref="Plan"/>。**画面や札や演出を足したら、
    /// あそこに1行足す。⚠️ 「今回はこれも撮って」と口頭で足していくと、
    /// 次に撮ったとき何が抜けたか分からなくなる。
    ///
    /// ⚠️ <see cref="ScreenCapture.CaptureScreenshot"/> は使わない ── フレームの終わりに
    /// 書き出す作りなので、道具から呼ぶと**呼んだ直後にファイルが無い**
    /// （実際に空振りした 2026-08-21）。⭐ 覆いは `ScreenSpaceCamera` なので、
    /// カメラを <see cref="RenderTexture"/> へ描けばその場で絵が取れる。
    ///
    /// ⚠️ **見た目の判定には使わない。**縮んで届くので被りは分からない
    /// （被りと はみ出しは <see cref="InspectScreens"/> の数で見る）。
    /// ⭐ これは「並べて眺める」ための道具。
    ///
    /// 使い方:
    /// <list type="bullet">
    ///   <item>メニュー **Egg Command / 画面を撮る**（▶ を押した状態で）</item>
    ///   <item>道具から: <c>Shots.RunAll()</c> ── 撮り終えると Console に一覧が出る</item>
    ///   <item>中身が寂しいときは先に <c>Shots.Fill()</c>（個体と卵を積む）</item>
    /// </list></summary>
    public static class Shots
    {
        /// <summary>置き場所。⭐ 既定はリポジトリの `shots/`。</summary>
        public static string Dir =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "shots"));

        /// <summary>1枚ぶんの段取り。</summary>
        private sealed class Scene
        {
            public readonly string Name;
            public readonly Action Set;
            /// <summary>撮る前に待つフレーム数。⭐ 演出は動き始めを何フレームか進める。</summary>
            public readonly int Wait;

            public Scene(string name, Action set, int wait)
            {
                Name = name; Set = set; Wait = wait;
            }
        }

        private static List<Scene> _plan;
        private static int _at;
        private static int _waited;
        private static int _keepOverlay;
        private static readonly List<string> _made = new List<string>();

        // ── 入口 ────────────────────────────────────

        [MenuItem("Egg Command/画面を撮る")]
        public static void RunAll()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("画面を撮る: ▶ を押してから、もう一度どうぞ。");
                return;
            }
            var app = UnityEngine.Object.FindAnyObjectByType<App>();
            if (app == null) { Debug.LogWarning("画面を撮る: App が見つかりません。"); return; }

            Directory.CreateDirectory(Dir);
            _made.Clear();
            _at = 0;
            _waited = 0;
            // ⚠️ **App 自身の部品を数えておく。**⭐ 札を片付けるとき、ここより後ろだけ消す。
            //    数えずに覆いの子を全部消して、空の絵まで壊したことがある（2026-08-21）。
            _keepOverlay = app.Overlay.childCount;
            _plan = Build(app);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log($"画面を撮る: {_plan.Count} 枚 → {Dir}");
        }

        /// <summary>撮影用に中身を積む。⚠️ **保存を書き換える**ので、撮るときだけ。</summary>
        public static void Fill()
        {
            var app = UnityEngine.Object.FindAnyObjectByType<App>();
            if (app == null || app.Game == null) { Debug.LogWarning("App が見つかりません。"); return; }
            var game = app.Game;
            var rng = new Rng(4242);

            // ⚠️ **前に積んだぶんを先に捨てる。**⭐ 何度でも回せるようにするため
            //    ── 捨てないと 4回目で「保管庫が満杯（50枠）」で落ちた（2026-08-21）。
            //    ⚠️ 捨てるのは撮影用（shot〜）だけ。⭐ 手で作った個体には触らない。
            var mine = new List<string>();
            foreach (var one in game.Storage.Creatures)
                if (one.Id.StartsWith("shot")) mine.Add(one.Id);
            foreach (var id in mine) game.Storage = Storages.Release(game.Storage, id);
            game.Eggs.Clear();

            for (int i = 0; i < 14; i++)
            {
                var nest = Nests.All[i % Nests.All.Length];
                var egg = Nests.MakeEggOfRarity(rng, nest, EggOrigin.Stolen, ++game.Serial,
                    1 + (i % 5), element: SpeciesTable.Roll(rng));
                StatKey best, strong, weak, worst;
                Nests.RollSlant(rng, out best, out strong, out weak, out worst);
                // ⚠️ **id を作り直すたびに新しくする。**⭐ "shot0" のような決め打ちだと、
                //    2回目の Fill が「shot0 は既に保管庫にいる」で落ちる（実際に落ちた）。
                var born = Nests.Hatch(rng, egg, "shot" + (++game.Serial), strong, weak, best, worst);
                Creatures.Grow(born, Creatures.TrainMax * (i % 4) / 3);
                // ⚠️ **`Storages.Accept` を直に呼ばない。**⭐ 撮影用の個体も
                //    本番と同じ口（図鑑に載る道）を通す ── 通さないと
                //    「検証経路 ≠ 本番経路」になる。
                Games.Keep(game, born);
            }
            for (int i = 0; i < 4; i++)
                game.Eggs.Add(Nests.MakeEggOfRarity(rng, Nests.All[i % Nests.All.Length],
                    EggOrigin.Stolen, ++game.Serial, 1 + i, element: SpeciesTable.Roll(rng)));
            Games.LockParty(game);
            app.Refresh();
            Debug.Log($"撮影用に積んだ: 個体 {game.Storage.Creatures.Count} / 卵 {game.Eggs.Count}");
        }

        // ── 撮る箇所（⭐ ここが唯一の出所）──────────

        /// <summary>⭐ **撮る箇所の一覧。**画面・札・演出を足したら、ここへ1行。
        ///
        /// ⚠️ 並び順がそのまま番号になる。⭐ 番号は「見る順」でつける
        /// （画面 → 潜入の途中 → 札 → 演出）。</summary>
        private static List<Scene> Build(App app)
        {
            var plan = new List<Scene>();
            Action<string, Action> shot = (name, set) => plan.Add(new Scene(name, set, 0));
            Action<string, Action, int> moving = (name, set, wait) =>
                plan.Add(new Scene(name, set, wait));

            // ── 画面 ─────────────────────────────
            shot("01-ホーム", () => Screenful(app, View.Screen.Home));
            shot("02-探索", () => Screenful(app, View.Screen.Nests));
            shot("03-配合", () => Screenful(app, View.Screen.Breed));
            shot("04-BOX", () => Screenful(app, View.Screen.Box));

            // ── 潜入（すごろく）──────────────────
            shot("05-潜入_振る前", () => Raid(app));
            shot("06-潜入_行き先が光る", () => { Raid(app); Lit(app, 2); });
            shot("07-潜入_行き先が遠い", () => { Raid(app); Lit(app, 6); });
            shot("08-潜入_歩いている", () => { Raid(app); Walking(app, 4); });
            shot("09-潜入_関門の払い札", () => Until(app, RaidStep.Offered, true));
            shot("10-潜入_払えない関門", () => Until(app, RaidStep.Offered, false));

            // ── 戦闘 ─────────────────────────────
            shot("11-戦闘_はじめ", () => Fight(app, 0));
            shot("12-戦闘_数手すすめた", () => Fight(app, 6));

            // ── 試練（2026-08-21）─────────────────
            shot("12b-試練_一覧", () => Screenful(app, View.Screen.Trial));
            shot("12c-試練_段5と戦う", () =>
            {
                Sweep(app);
                app.EnterTrial(Trials.All[Trials.All.Count - 1]);
                app.Refresh();
            });

            // ── 図鑑（2026-08-22）─────────────────
            shot("12d-図鑑_一覧", () =>
            {
                // ⭐ **半分だけ載せた状態で撮る。**⚠️ 全部載せると「伏せ」の見え方が
                //    写らず、埋まっていない図鑑がどう見えるか分からない
                Sweep(app);
                Screenful(app, View.Screen.Book);
            });
            shot("12e-図鑑_種族の中身", () =>
            {
                Sweep(app);
                var species = SpeciesTable.All[0];
                Games.See(app.Game, species.Id);
                app.Show(View.Screen.Book);
                app.Refresh();
                View.SpeciesPanel.Show(app, species);
            });

            // ── 札（ポップアップ）────────────────
            shot("13-札_パーティ編成", () =>
            {
                Screenful(app, View.Screen.Home);
                PartyPanel.Show(app, PartyKind.Idle);
            });
            shot("15-札_技の詳細", () =>
            {
                Screenful(app, View.Screen.Box);
                SkillInfoPanel.Show(app, Skills.All[4], 3);
            });
            shot("16-札_分解", () =>
            {
                Screenful(app, View.Screen.Box);
                var who = Someone(app);
                if (who != null) FusePanel.Show(app, who.Id);
            });
            shot("17-札_技の卵", () =>
            {
                Screenful(app, View.Screen.Box);
                var who = Someone(app);
                if (who != null) SkillEggPanel.Show(app, who.Id);
            });

            // ── 演出 ─────────────────────────────
            // ⚠️ **動いている物は数フレーム進めてから撮る。**0 フレームだと
            //    どれも「まだ始まっていない絵」になり、確かめたい所が写らない。
            moving("18-演出_さいころ_回っている", () =>
            {
                Screenful(app, View.Screen.Home);
                TrailDice.Show(app.Overlay, 5, () => { });
            }, 8);
            moving("19-演出_さいころ_止まった", () =>
            {
                Screenful(app, View.Screen.Home);
                TrailDice.Show(app.Overlay, 5, () => { });
            }, 90);
            moving("20-演出_▲の数字とリング", () => Burst(app, true), 6);
            moving("21-演出_▼の数字とリング", () => Burst(app, false), 6);
            moving("22-演出_衝撃と画面揺れ", () =>
            {
                Screenful(app, View.Screen.Home);
                var fx = Fx.Get(app.transform);
                var at = Middle();
                fx.Impact(at, Ui.Accent);
                Shake.Play(app.Stage, 34f);
            }, 4);
            moving("23-演出_告知の帯", () =>
            {
                Screenful(app, View.Screen.Home);
                BannerView.Show(app.Overlay, "親に見つかった！", () => { });
            }, 20);
            moving("24-演出_生まれた", () =>
            {
                Screenful(app, View.Screen.Home);
                var who = Someone(app);
                if (who != null) Fanfare.Born(app.Overlay, who, () => { });
            }, 24);

            return plan;
        }

        // ── 段取りの部品 ────────────────────────────

        /// <summary>札を片付けて、画面を出す。</summary>
        private static void Screenful(App app, View.Screen screen)
        {
            Sweep(app);
            app.Show(screen);
            app.Refresh();
        }

        /// <summary>⚠️ **後から足した物だけ**を消す。⭐ App 自身の部品は残す。</summary>
        private static void Sweep(App app)
        {
            for (int i = app.Overlay.childCount - 1; i >= _keepOverlay; i--)
                UnityEngine.Object.DestroyImmediate(app.Overlay.GetChild(i).gameObject);
        }

        private static Creature Someone(App app)
        {
            var all = app.Game.Storage.Creatures;
            return all.Count == 0 ? null : all[all.Count > 3 ? 3 : 0];
        }

        /// <summary>画面のまんなか。
        /// ⚠️ **`Screen` は必ず修飾する。**`UnityEngine.Screen` と
        /// <see cref="View.Screen"/>（画面の種類）が同じ名前で、素で書くと通らない。</summary>
        private static Vector2 Middle() =>
            new Vector2(UnityEngine.Screen.width * 0.5f, UnityEngine.Screen.height * 0.5f);

        private static void Burst(App app, bool up)
        {
            Screenful(app, View.Screen.Home);
            var fx = Fx.Get(app.transform);
            var at = Middle();
            var ink = up ? Ui.GoodInk : Ui.DangerInk;
            fx.Number(at, up ? "+60%" : "-35%", ink, 58f);
            fx.Ring(at, ink, 120f, 320f, 0.9f);
        }

        /// <summary>潜入を頭から始める。</summary>
        private static void Raid(App app)
        {
            Sweep(app);
            if (app.Game.Encounters.Count == 0) { app.Show(View.Screen.Nests); app.Refresh(); return; }
            TrailScreen.Enter(app, app.Game.Encounters[0].Nest);
            app.Refresh();
        }

        /// <summary>出目ぶんの行き先を光らせる。⚠️ 画面の裏の札を直に触る。</summary>
        private static void Lit(App app, int pips)
        {
            var raid = app.Raid;
            if (raid == null) return;
            raid.Pending = pips;
            raid.Step = RaidStep.Choosing;
            Poke("_open", Trails.Reach(raid, pips));
            Poke("_flagged", raid);
            app.Refresh();
        }

        /// <summary>歩いている最中の絵（駒に残り歩数の札が付く）。</summary>
        private static void Walking(App app, int left)
        {
            var raid = app.Raid;
            if (raid == null) return;
            raid.Step = RaidStep.Choosing;
            var reach = Trails.Reach(raid, raid.Pending > 0 ? raid.Pending : 3);
            if (reach.Count == 0) return;
            Poke("_open", null);
            Poke("_walking", true);
            Poke("_walkLeft", left);
            Poke("_shownAt", reach[0][0]);
            Poke("_flagged", raid);
            app.Refresh();
        }

        /// <summary>その段まで進める。⭐ <paramref name="payable"/> で払える関門／払えない関門を選ぶ。</summary>
        private static void Until(App app, RaidStep step, bool payable)
        {
            Raid(app);
            var raid = app.Raid;
            if (raid == null) return;
            if (!payable)
            {
                // ⭐ 財布を空にすると、どの関門も払えなくなる
                raid.Spent = raid.Pool;
            }
            int guard = 0;
            while (raid.Step != step && raid.Result == null && guard++ < 400)
            {
                if (raid.Step == RaidStep.Met) { Trails.Beat(raid); continue; }
                if (raid.Step == RaidStep.Choosing)
                {
                    var reach = Trails.Reach(raid, raid.Pending);
                    if (reach.Count == 0) break;
                    int pick = 0;
                    for (int i = 0; i < reach.Count; i++)
                        if (Trails.CanPay(raid, reach[i][reach[i].Count - 1])) { pick = i; break; }
                    Trails.Go(raid, reach[pick]);
                    continue;
                }
                Trails.Roll(new Rng(guard * 31).Stream("shot"), raid);
            }
            // ⚠️ 払えない側は Offered に入らない。⭐ 関門の上に立った絵になればよい
            Poke("_open", null);
            Poke("_flagged", raid);
            app.Refresh();
        }

        private static void Fight(App app, int steps)
        {
            Sweep(app);
            if (app.Game.Encounters.Count == 0) return;
            app.EnterBattle(app.Game.Encounters[0].Nest, false);
            var state = app.Battle;
            for (int i = 0; i < steps && state != null && state.Result == null; i++)
            {
                var actor = Battle.NextActor(state);
                if (actor == null) break;
                Battle.PerformAction(state, actor, Ai.ChooseAction(state, actor));
            }
            app.Refresh();
        }

        /// <summary>画面の裏の札を直に入れる。
        /// ⚠️ **見つからなければ黙って諦めない。**⭐ 名前を変えたときに気づけるよう声を上げる。</summary>
        private static void Poke(string field, object value)
        {
            var f = typeof(TrailScreen).GetField(field,
                BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null)
            {
                Debug.LogError($"画面を撮る: TrailScreen に {field} が無い（名前が変わった？）");
                return;
            }
            f.SetValue(null, value);
        }

        // ── 進行 ────────────────────────────────────

        private static void Tick()
        {
            if (!Application.isPlaying || _plan == null) { Stop(); return; }
            if (_at >= _plan.Count) { Stop(); return; }

            var scene = _plan[_at];
            if (_waited == 0)
            {
                try { scene.Set(); }
                catch (Exception e)
                {
                    Debug.LogError($"画面を撮る: 「{scene.Name}」の段取りで転んだ ── {e.Message}");
                    _at++;
                    return;
                }
            }
            // ⭐ 待つ ── 動いている物は始まりだけ写しても意味が無い
            if (_waited < scene.Wait) { _waited++; return; }

            _made.Add(Take(scene.Name));
            _at++;
            _waited = 0;
        }

        private static void Stop()
        {
            EditorApplication.update -= Tick;
            if (_made.Count == 0) return;
            Debug.Log($"画面を撮った: {_made.Count} 枚\n  " + string.Join("\n  ", _made));
            _made.Clear();
            _plan = null;
        }

        // ── 1枚撮る ─────────────────────────────────

        /// <summary>撮って、書いた場所を返す。⚠️ 失敗したら理由を返す。</summary>
        public static string Take(string name)
        {
            var cam = Camera.main;
            if (cam == null) return name + ": カメラが無い";
            if (string.IsNullOrEmpty(Dir)) return name + ": 置き場所が決まっていない";
            Directory.CreateDirectory(Dir);

            int w = UnityEngine.Screen.width > 0 ? UnityEngine.Screen.width : 1080;
            int h = UnityEngine.Screen.height > 0 ? UnityEngine.Screen.height : 1920;

            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var wasTarget = cam.targetTexture;
            var wasActive = RenderTexture.active;
            var shot = new Texture2D(w, h, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
                shot.Apply();
            }
            finally
            {
                cam.targetTexture = wasTarget;
                RenderTexture.active = wasActive;
            }

            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), shot.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(shot);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            return $"{name}.png ({w}x{h})";
        }
    }
}
