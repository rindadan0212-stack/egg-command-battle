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
        /// <summary>告知を出して戦闘へ渡している最中。
        /// ⚠️ 画面が組み直されるたびに告知を作らないための札。</summary>
        private static bool _handing;

        /// <summary>巣を選んで潜入へ。
        ///
        /// ⚠️ **盤は必ず <see cref="Core.Steal.MakeValidatedField"/> を通す。**
        /// 素の MakeField は検査を通らない盤も返す（関門の車線の出目しだいで、
        /// 通る角度が 1度しか無い盤ができる）。
        ///
        /// ⚠️ 種は**巣と盗んだ回数だけ**から作る。挑むたびに引くと、
        /// 画面を出入りするだけで盤を選び直せてしまう。
        /// ⭐ 親がどちらへ寄るかも同じ理由で巣の乱数に載せない。</summary>
        public static void Enter(App app, Nest nest)
        {
            app.CurrentNest = nest;
            app.CurrentIsBoss = false;

            int raids = Games.RaidsOn(app.Game, nest);
            var rng = Core.Steal.RngFor(nest, raids);
            var side = rng.Chance(0.5) ? FieldSide.Left : FieldSide.Right;
            var field = Core.Steal.MakeValidatedField(nest.Tier, side, raids, rng);
            // ⭐ 進み具合は App が持つ。⚠️ 盤は雑魚と戦うたびに畳まれるので持たせない
            app.Infiltration = new Core.Steal.Infiltration(field, Games.PartyOf(app.Game));
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

        public static void Build(App app, RectTransform body)
        {
            var infil = app.Infiltration;
            if (infil == null) { app.Show(Screen.Nests); return; }

            // ── 盤（ワールド空間） ──────────────────────
            // ⚠️ まだ飛ばしていないときだけ作る。結果を見せている間は残しておく
            if (_stage == null && _result == null && infil.Party.Count > 0)
            {
                // ⭐ 進み具合ごと渡す。⭐ 雑魚と戦って戻ってきたら、
                //    着地した個体・壊した壁・倒した雑魚がそのまま描き直される
                _stage = StealStage.Create(infil, app.CurrentNest,
                    Games.RaidsOn(app.Game, app.CurrentNest),
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
            // ⚠️ ここに押しどころを置かない。届いたなら持ち帰るしかないし、
            //    見つかったなら戦うしかない。選択肢でないものを押させない。
            if (_handing) return;
            _handing = true;

            // ⭐ 雑魚に当たった。⚠️ **潜入の決着ではない** ── 勝てば続きへ戻る
            if (_result.Outcome == StealOutcome.Fought)
            {
                int mob = _result.Mob;
                var here = app.CurrentNest;
                BannerView.Show(app.Overlay, "雑魚に囲まれた！", () =>
                {
                    _result = null;
                    _handing = false;
                    Leave();
                    app.EnterMobBattle(here, mob);
                });
                return;
            }

            bool won = _result.Outcome == StealOutcome.Success;
            var nest = app.CurrentNest;
            BannerView.Show(app.Overlay, won ? "GET!" : "親に見つかった！", () =>
            {
                _result = null;
                _handing = false;
                Leave();
                // ⭐ ここで潜入は終わり。⚠️ 残しておくと次の巣に前の進み具合が付いてくる
                app.Infiltration = null;
                if (won)
                {
                    Games.GrowParty(Games.PartyOf(app.Game));
                    // ⚠️ 盗んだ卵は素質が落ちる（倒したほうが良い卵）
                    // ⭐ 盗んだ巣は**残る**。次はもっと固くなっているだけ
                    app.GainEgg(nest, EggOrigin.Stolen, closeNest: false);
                    // ⭐ 盗まれた巣は次から守りを固める（関門が増え、隙間が狭まる）
                    Games.RecordRaid(app.Game, nest);
                }
                else
                {
                    // ⭐ 立ちはだかるのも親1体なので、そのまま戦闘へ繋がる
                    // ⚠️ 潜入で負った傷と CT を持ち込む（雑魚と戦うほどここが苦しくなる）
                    app.EnterBattle(nest, false, infil);
                }
            });
        }
    }
}
