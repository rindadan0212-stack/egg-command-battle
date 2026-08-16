using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>卵強奪の発射フェーズ。
    ///
    /// 縦長のフィールド。一番上に卵。その手前に親が左右どちらかへ寄って立ちはだかる。
    /// 一番下の自分のモンスターを1回だけ飛ばす。卵に届けば成功。親に当たるか失速したら戦闘へ。
    ///
    /// ⭐ 飛距離は編成のスピード合計。ここが設計の芯:
    /// 強奪を狙ってスピードに寄せるほど、失敗したときの戦闘で編成が偏って苦しくなる。
    ///
    /// ⚠️ 判定は <see cref="Core.Steal"/> が全部持つ。この画面は角度を渡して結果を描くだけ。
    /// </summary>
    public static class StealScreen
    {
        private static float _angleDeg;
        private static StealRun _run;

        /// <summary>巣を選んで発射画面へ。⚠️ 親がどちらへ寄るかだけは巣ごとの乱数で決まる。</summary>
        public static void Enter(App app, Nest nest)
        {
            app.CurrentNest = nest;
            app.CurrentIsBoss = false;
            var side = app.Game.RngSteal.Chance(0.5) ? FieldSide.Left : FieldSide.Right;
            app.Field = Core.Steal.MakeField(nest.Tier, side);
            _angleDeg = 0f;
            _run = null;
            app.Notice = "";
            app.Show(Screen.Steal);
        }

        public static void Build(App app, RectTransform body, float height)
        {
            var field = app.Field;
            if (field == null) { app.Show(Screen.Nests); return; }

            var party = Games.PartyOf(app.Game);
            double budget = Core.Steal.DistanceFor(party);

            // ── 情報 ────────────────────────────────────
            Ui.Label(body, "Info",
                $"飛距離 {budget:F0} / 奥行き {field.Height:F0}", 30,
                budget >= field.Height ? Ui.Good : Ui.Danger,
                TextAnchor.UpperLeft, Ui.Margin, 20f, Ui.W - Ui.Margin * 2f, 40f);

            // ── 盤 ──────────────────────────────────────
            float boardTop = 76f;
            float boardHeight = height - boardTop - 300f;
            float scale = Mathf.Min((Ui.W - Ui.Margin * 2f) / (float)Core.Steal.FieldWidth,
                                    boardHeight / (float)field.Height);
            float boardWidth = (float)Core.Steal.FieldWidth * scale;
            float boardLeft = (Ui.W - boardWidth) / 2f;
            float drawnHeight = (float)field.Height * scale;

            var board = Ui.Block(body, "Board", new Color32(0x14, 0x18, 0x12, 0xff),
                boardLeft, boardTop, boardWidth, drawnHeight);

            // 親が塞ぐ帯（隙間の左右2枚）
            float bandTop = (float)field.BandTop * scale;
            float bandHeight = (float)(field.BandBottom - field.BandTop) * scale;
            foreach (var span in Core.Steal.ParentSpans(field))
            {
                Ui.Block(board, "Parent", new Color32(0x6b, 0x4a, 0x3a, 0xff),
                    (float)span.From * scale, bandTop, (float)(span.To - span.From) * scale, bandHeight);
            }

            // 卵
            float eggSize = (float)Core.Steal.EggRadius * 2f * scale;
            Ui.Block(board, "Egg", new Color32(0xea, 0xe0, 0xc0, 0xff),
                (float)(field.Egg.X - Core.Steal.EggRadius) * scale,
                (float)(field.Egg.Y - Core.Steal.EggRadius) * scale, eggSize, eggSize);

            // 走る者
            float runnerSize = (float)Core.Steal.RunnerRadius * 2f * scale;
            Ui.Block(board, "Runner", Ui.Accent,
                (float)(field.Start.X - Core.Steal.RunnerRadius) * scale,
                (float)(field.Start.Y - Core.Steal.RunnerRadius) * scale, runnerSize, runnerSize);

            // 狙いの線（まだ飛ばしていないとき）
            if (_run == null)
            {
                double radians = _angleDeg * Mathf.Deg2Rad;
                for (int i = 1; i <= 14; i++)
                {
                    float t = i * 12f;
                    float x = (float)(field.Start.X + Mathf.Sin((float)radians) * t);
                    float y = (float)(field.Start.Y - Mathf.Cos((float)radians) * t);
                    Ui.Block(board, "Aim", new Color32(0xd8, 0xb4, 0x5c, 0x66),
                        x * scale - 3f, y * scale - 3f, 6f, 6f);
                }
            }
            else
            {
                // ⭐ 通った軌跡をそのままなぞる（判定と同じ点を描く）
                var path = _run.Path;
                int step = Mathf.Max(1, path.Count / 90);
                for (int i = 0; i < path.Count; i += step)
                {
                    Ui.Block(board, "Trail", new Color32(0xef, 0xe9, 0xdc, 0x99),
                        (float)path[i].X * scale - 3f, (float)path[i].Y * scale - 3f, 6f, 6f);
                }
            }

            // ── 操作 ────────────────────────────────────
            float panelTop = boardTop + drawnHeight + 24f;

            if (_run == null)
            {
                Ui.Label(body, "AimLabel", $"狙い {_angleDeg:F0}°", 30, Ui.Ink,
                    TextAnchor.UpperLeft, Ui.Margin, panelTop, 400f, 40f);

                // ⚠️ 狙いは寛容に作ってある。難しさは距離で分けている
                float sliderWidth = Ui.W - Ui.Margin * 2f;
                var track = Ui.Block(body, "Track", Ui.Panel, Ui.Margin, panelTop + 52f, sliderWidth, Ui.Tap);
                var slider = track.gameObject.AddComponent<Slider>();
                slider.minValue = -80f;
                slider.maxValue = 80f;
                slider.value = _angleDeg;
                slider.wholeNumbers = false;

                var fill = Ui.Block(track, "Knob", Ui.Accent, 0f, 0f, 24f, Ui.Tap);
                slider.handleRect = fill;
                slider.targetGraphic = fill.GetComponent<Image>();
                slider.direction = Slider.Direction.LeftToRight;
                slider.onValueChanged.AddListener(v => { _angleDeg = v; app.Refresh(); });

                Ui.Tappable(body, "Launch", "飛ばす", () =>
                {
                    _run = Core.Steal.Launch(field, _angleDeg * Mathf.Deg2Rad, budget);
                    app.Refresh();
                }, Ui.Margin, panelTop + 52f + Ui.Tap + 20f, Ui.W - Ui.Margin * 2f, Ui.Tap, true);
            }
            else
            {
                string message;
                switch (_run.Outcome)
                {
                    case StealOutcome.Success: message = "卵に届いた。盗んで逃げた。"; break;
                    case StealOutcome.Blocked: message = "親に見つかった。戦うしかない。"; break;
                    default: message = "届かなかった。親に気づかれた。"; break;
                }
                Ui.Label(body, "Result", message, 32,
                    _run.Outcome == StealOutcome.Success ? Ui.Good : Ui.Danger,
                    TextAnchor.UpperLeft, Ui.Margin, panelTop, Ui.W - Ui.Margin * 2f, 44f);
                Ui.Label(body, "Traveled", $"飛んだ距離 {_run.Traveled:F0} / {budget:F0}", 26, Ui.InkDim,
                    TextAnchor.UpperLeft, Ui.Margin, panelTop + 48f, Ui.W - Ui.Margin * 2f, 36f);

                if (_run.Outcome == StealOutcome.Success)
                {
                    Ui.Tappable(body, "Take", "卵を持ち帰る", () =>
                    {
                        // ⚠️ 盗んだ卵は素質が落ちる（倒したほうが良い卵）
                        var egg = Games.GainEgg(app.Game, app.CurrentNest, EggOrigin.Stolen);
                        Games.AwardParty(Games.PartyOf(app.Game));
                        app.Notice = $"{app.CurrentNest.Name} の卵（{egg.Id}）を盗んだ。";
                        _run = null;
                        app.Show(Screen.Nests);
                    }, Ui.Margin, panelTop + 96f, Ui.W - Ui.Margin * 2f, Ui.Tap, true);
                }
                else
                {
                    Ui.Tappable(body, "Fight", "戦闘へ", () =>
                    {
                        var nest = app.CurrentNest;
                        _run = null;
                        // ⭐ 発射で立ちはだかるのも親1体なので、そのまま戦闘へ繋がる
                        app.EnterBattle(nest, false);
                    }, Ui.Margin, panelTop + 96f, Ui.W - Ui.Margin * 2f, Ui.Tap, true);
                }
            }
        }
    }
}
