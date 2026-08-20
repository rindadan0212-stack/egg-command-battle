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
        /// <summary>盤の外に置く「誰を投げるか」の帯の高さ。
        /// ⚠️ 盤にもこの数を渡す（<see cref="StealStage.HideBehind"/>）。渡さないと
        /// 出発点が帯の下に潜って、走者を掴めなくなる。</summary>
        public const float StripHeight = 268f;

        private const float StripPad = 12f;

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

        /// <summary>盤の外の帯。⭐ **編成ぶんを並べて、そこで選ぶ。**
        /// ⚠️ 体数は `infil.Party.Count` から引く（決め打ちしない ── 2026-08-20 に 3 → 4）。
        ///
        /// ⭐ 短く触れば選ぶ／押し続ければステの札が開く（<see cref="LongPress"/>）。
        /// ⚠️ 「詳細」の押しどころを別に並べない ── 小さい札の上に押しどころが2つ乗ると、
        /// どちらを押したのか分からなくなる。
        ///
        /// ⚠️ **投げ終わった個体も並べたままにする。**消すと札の位置がずれて、
        /// 「さっき触った場所」が別の個体になる。</summary>
        private static void Strip(App app, RectTransform body, Core.Steal.Infiltration infil)
        {
            var strip = Ui.Rect("Strip", body);
            Ui.Place(strip, 0f, Ui.H - Ui.TopBarHeight - StripHeight, Ui.W, StripHeight);
            var face = strip.gameObject.AddComponent<Image>();
            // ⭐ 線で囲わず、明度を一段落として面で分ける（盤と帯の境目はこれで足りる）
            face.color = new Color(0.04f, 0.06f, 0.10f, 0.72f);
            face.raycastTarget = true;

            int count = infil.Party.Count;
            if (count <= 0) return;
            float cell = (Ui.W - Ui.Margin * 2f - StripPad * (count - 1)) / count;

            for (int i = 0; i < count; i++)
            {
                int member = i;
                var creature = infil.Party[member];
                bool left = infil.Left.Contains(member);
                bool chosen = _stage != null && _stage.Chosen == member;

                var box = Ui.Rect($"Member {member}", strip);
                Ui.Place(box, Ui.Margin + (cell + StripPad) * i, StripPad,
                    cell, StripHeight - StripPad * 2f);
                var plate = box.gameObject.AddComponent<Image>();
                plate.sprite = Ui.SkinSprite("panel");
                plate.type = Image.Type.Sliced;
                plate.color = left ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                // ⭐ **選択は「角丸の黄色い輪」に揃える**（一覧の升と同じ約束）。
                if (chosen)
                {
                    var ring = Ui.Ring(box, "Ring", 0f, 0f, cell, StripHeight - StripPad * 2f);
                    ring.SetAsFirstSibling();
                }

                Ui.PixelOf(box, "Art", creature, (cell - 120f) / 2f, 14f, 120f);
                Ui.Label(box, "Speed", $"スピード {Creatures.StatsOf(creature).Spd}",
                    24, Ui.Ink, TextAnchor.MiddleCenter, 0f, 142f, cell, 32f);
                // ⚠️ 「飛距離」と書き換えない。⭐ 盤の目盛りは m、こちらはステの名前のまま
                //    （同じ数が2つの名前で出ると、どちらが元か読めなくなる）
                Ui.Label(box, "State", left ? (chosen ? "つぎ投げる" : "まだ") : "投げた",
                    22, left ? (chosen ? Ui.Ink : Ui.InkDim) : Ui.InkFaint,
                    TextAnchor.MiddleCenter, 0f, 178f, cell, 30f);

                var hold = box.gameObject.AddComponent<LongPress>();
                hold.OnHold = () => StatusPanel.Show(app, creature);
                if (left) hold.OnTap = () => { if (_stage != null) _stage.Choose(member); };
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
            //    ⭐ どこまで届くかは、盤の右の目盛り（10刻み）が見せる。
            // ⚠️ 「引っ張って離す」と書かない。⭐ 走者が脈打っていれば触る。
            //    触れば線が伸びて、離せば飛ぶ。1回やれば分かることを字にしない。

            // ⭐ **誰を投げるかは盤の外で選ぶ。**
            // ⚠️ 盤の上に3体を並べていた頃は、選んだ1体が出発点へ移されて
            //    ちょうど別の1体と重なり、どれを触ったのか分からなかった。
            if (_result == null && _stage != null)
            {
                _stage.HideBehind(StripHeight);
                _stage.Watch(() => app.Refresh());
                Strip(app, body, infil);
                return;
            }
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
