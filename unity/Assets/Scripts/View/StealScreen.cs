using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>卵強奪。⭐ **この画面だけワールド空間の 2D**（他は uGUI）。
    ///
    /// 縦長の盤。一番上に卵。その手前に親が左右どちらかへ寄って立ちはだかる。
    /// 一番下の自分のモンスターを**引っ張って離す**と、飛んで壁で跳ね返る。
    /// 卵に届けば盗み、親に当たるか失速したら戦闘。
    ///
    /// ⭐ 飛距離は編成のスピード合計。ここが設計の芯:
    /// 強奪を狙ってスピードに寄せるほど、失敗したときの戦闘で編成が偏って苦しくなる。
    /// 同じ資源（編成）が2つの軸に引っ張られる。
    ///
    /// ⚠️ 当たり判定も跳ね返りも <see cref="Core.Steal"/>。この画面は角度を渡して結果を描くだけ。
    /// </summary>
    public static class StealScreen
    {
        private static StealRun _result;
        private static StealStage _stage;

        /// <summary>巣を選んで発射へ。⚠️ 親がどちらへ寄るかだけは巣ごとの乱数で決まる。</summary>
        public static void Enter(App app, Nest nest)
        {
            app.CurrentNest = nest;
            app.CurrentIsBoss = false;
            var side = app.Game.RngSteal.Chance(0.5) ? FieldSide.Left : FieldSide.Right;
            app.Field = Core.Steal.MakeField(nest.Tier, side);
            _result = null;
            app.Notice = "";
            app.Show(Screen.Steal);
        }

        /// <summary>画面を離れるときに盤を片付ける。⚠️ 残すとカメラの寸法が戻らない。</summary>
        public static void Leave()
        {
            if (_stage != null)
            {
                _stage.Dismiss();
                _stage = null;
            }
        }

        public static void Build(App app, RectTransform body, float height)
        {
            var field = app.Field;
            if (field == null) { app.Show(Screen.Nests); return; }

            var party = Games.PartyOf(app.Game);
            double budget = Core.Steal.DistanceFor(party);

            // ── 盤（ワールド空間） ──────────────────────
            // ⚠️ まだ飛ばしていないときだけ作る。結果を見せている間は残しておく
            if (_stage == null && _result == null && party.Count > 0)
            {
                _stage = StealStage.Create(field, budget, party[0], app.CurrentNest.SpeciesId,
                    run =>
                    {
                        _result = run;
                        app.Refresh();
                    });
            }

            // ── 上に重ねる案内 ──────────────────────────
            Ui.Label(body, "Info", $"飛距離 {budget:F0} / 奥行き {field.Height:F0}", 28,
                budget >= field.Height ? Ui.Good : Ui.Danger,
                TextAnchor.UpperLeft, Ui.Margin, 16f, Ui.W - Ui.Margin * 2f, 38f);

            if (_result == null)
            {
                Ui.Label(body, "Hint", "引っ張って離す", 30, Ui.Ink,
                    TextAnchor.LowerCenter, 0f, height - 120f, Ui.W, 44f);
                Ui.Label(body, "Hint2", "卵に届けば盗める。届かなければ戦闘になる。", 24, Ui.InkDim,
                    TextAnchor.LowerCenter, 0f, height - 76f, Ui.W, 36f);
                return;
            }

            // ── 結果 ────────────────────────────────────
            string message;
            switch (_result.Outcome)
            {
                case StealOutcome.Success: message = "卵に届いた。盗んで逃げた。"; break;
                case StealOutcome.Blocked: message = "親に見つかった。戦うしかない。"; break;
                default: message = "届かなかった。親に気づかれた。"; break;
            }

            float panelTop = height - 260f;
            Ui.Block(body, "ResultBg", new Color32(0x16, 0x12, 0x10, 0xee), 0f, panelTop, Ui.W, 260f);
            Ui.Label(body, "Result", message, 32,
                _result.Outcome == StealOutcome.Success ? Ui.Good : Ui.Danger,
                TextAnchor.UpperLeft, Ui.Margin, panelTop + 20f, Ui.W - Ui.Margin * 2f, 44f);
            Ui.Label(body, "Traveled", $"飛んだ距離 {_result.Traveled:F0} / {budget:F0}", 24, Ui.InkDim,
                TextAnchor.UpperLeft, Ui.Margin, panelTop + 66f, Ui.W - Ui.Margin * 2f, 34f);

            if (_result.Outcome == StealOutcome.Success)
            {
                Ui.Tappable(body, "Take", "卵を持ち帰る", () =>
                {
                    // ⚠️ 盗んだ卵は素質が落ちる（倒したほうが良い卵）
                    var egg = Games.GainEgg(app.Game, app.CurrentNest, EggOrigin.Stolen);
                    Games.AwardParty(Games.PartyOf(app.Game));
                    app.Notice = $"{app.CurrentNest.Name} の卵（{egg.Id}）を盗んだ。";
                    _result = null;
                    Leave();
                    app.Show(Screen.Nests);
                }, Ui.Margin, panelTop + 112f, Ui.W - Ui.Margin * 2f, Ui.Tap, true);
            }
            else
            {
                Ui.Tappable(body, "Fight", "戦闘へ", () =>
                {
                    var nest = app.CurrentNest;
                    _result = null;
                    Leave();
                    // ⭐ 立ちはだかるのも親1体なので、そのまま戦闘へ繋がる
                    app.EnterBattle(nest, false);
                }, Ui.Margin, panelTop + 112f, Ui.W - Ui.Margin * 2f, Ui.Tap, true);
            }
        }
    }
}
