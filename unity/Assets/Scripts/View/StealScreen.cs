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

            // ⚠️ 「飛距離 204 / 奥行き 290」と字で出さない。
            //    ⭐ どこまで届くかは、盤の上に引いた線（StealStage）が見せる。
            // ⚠️ 「引っ張って離す」と書かない。⭐ 走者が脈打っていれば触る。
            //    触れば線が伸びて、離せば飛ぶ。1回やれば分かることを字にしない。
            if (_result == null) return;

            // ── 結果 ────────────────────────────────────
            // ⚠️ 結果を文章で言わない。⭐ 盤の上に残った軌跡が既に語っている。
            //    ここに残すのは「次にどうするか」の押しどころだけ。
            float panelTop = height - 168f;

            if (_result.Outcome == StealOutcome.Success)
            {
                Ui.Tappable(body, "Take", "卵を持ち帰る", () =>
                {
                    // ⚠️ 盗んだ卵は素質が落ちる（倒したほうが良い卵）
                    var egg = Games.GainEgg(app.Game, app.CurrentNest, EggOrigin.Stolen);
                    Games.AwardParty(Games.PartyOf(app.Game));
                    _result = null;
                    Leave();
                    app.Show(Screen.Nests);
                }, Ui.Margin, panelTop, Ui.W - Ui.Margin * 2f, Ui.Tap, true);
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
                }, Ui.Margin, panelTop, Ui.W - Ui.Margin * 2f, Ui.Tap, true);
            }
        }
    }
}
