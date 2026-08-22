using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>戦闘。編成ぶん同時・スピードゲージ制・スキルごとの CT。
    ///
    /// ⭐ **この画面に座標は1つも無い。**
    /// 並び・大きさ・色は Assets/Resources/Prefabs/BattleScreen.prefab が持つ。
    /// 見た目を直したいときは Unity Editor で Prefab を開いてドラッグする。
    /// ⚠️ 見た目を足したくなったら、まず「座標を書こうとしていないか」を疑う。
    ///
    /// ここがやるのは3つだけ:
    ///   1. Prefab を置く
    ///   2. 今の状態を <see cref="BattleView"/> に渡す
    ///   3. 押されたら <see cref="Core.Battle"/> へ流す
    /// </summary>
    public static class BattleScreen
    {
        /// <summary>狙っている敵と味方。⭐ **別々に覚える。**
        /// ⚠️ 1つで兼ねると、敵を選んだまま強化を押したときに黙って別の相手へ飛ぶ。</summary>
        private static Unit _targetFoe;
        private static Unit _targetAlly;
        private static BattleDriver _driver;
        private static GameObject _prefab;

        /// <summary>⭐ オートで戦うか。⚠️ 戦闘をまたいで覚えておく
        /// （毎回押し直すのでは「自動」の意味が薄い）。</summary>
        private static bool _auto;

        public static void Leave()
        {
            // ⚠️ 戦闘をまたいで狙い先を持ち越さない（別の体を指したままになる）
            _targetFoe = null;
            _targetAlly = null;
            if (_driver != null)
            {
                Object.Destroy(_driver.gameObject);
                _driver = null;
            }
        }

        public static void Build(App app, RectTransform body)
        {
            var state = app.Battle;
            if (state == null) { app.Show(Screen.Nests); return; }

            if (_driver == null) _driver = BattleDriver.Create(app);
            _driver.Bind(app, state);

            // ⭐ **オートの状態は毎回渡し直す**（2026-08-22）。⚠️ Driver は戦闘をまたいで
            //    使い回されるので、作ったときに1回渡すだけでは切り替えが効かない。
            _driver.Auto = _auto;
            // ⭐ 狙い先は**画面が持っているものをそのまま**返す。⚠️ Driver 側に覚えさせない
            //    ── 人が指し直した瞬間に古いほうを撃つ。
            _driver.TargetOf = skill =>
                Core.Battle.TargetsAlly(skill) ? _targetAlly : _targetFoe;
            // ⭐ **入れた瞬間に始める。**⚠️ `Auto` を渡すだけでは、いま待っている
            //    手番は人のまま ── 1回だけ手で選ばされる（作者の指摘 2026-08-22）。
            _driver.Nudge();

            if (_prefab == null) _prefab = Resources.Load<GameObject>("Prefabs/BattleScreen");
            if (_prefab == null)
            {
                // ⚠️ 黙って何も出さない、をしない。無いことに気づけないほうが困る
                Debug.LogError("BattleScreen.prefab が読めない（Resources/Prefabs にあるか）");
                return;
            }

            var view = Object.Instantiate(_prefab, body).GetComponent<BattleView>();
            view.Bind(state, _driver.Actor, _targetFoe, _targetAlly,
                onSkill: slot =>
                {
                    var actor = _driver.Actor;
                    if (actor == null) return;
                    var skill = Core.Battle.SkillAt(actor, slot);
                    // ⭐ 技が味方に掛かるものなら味方の狙い先、敵ならの敵の狙い先を渡す
                    Unit chosen = null;
                    if (skill != null && Core.Battle.NeedsTarget(skill))
                    {
                        chosen = Core.Battle.TargetsAlly(skill) ? _targetAlly : _targetFoe;
                    }
                    // ⚠️ ここで計算しない。名乗り → 着弾 → 間 の3拍は Driver が持つ
                    _driver.Queue(actor, slot, chosen);
                },
                onFinish: () => { Leave(); app.FinishBattle(); },
                // ⭐ 長押しで効果の全文。⚠️ 札には名前・Lv・CT しか載らない
                onDetail: (skill, level, slot) => SkillInfoPanel.Show(app, skill, level, slot),
                onTap: unit =>
                {
                    // ⭐ もう一度押したら外す（選び直しに戻るための道を1つ残す）
                    if (unit.Side == Side.Enemy)
                    {
                        _targetFoe = ReferenceEquals(_targetFoe, unit) ? null : unit;
                    }
                    else
                    {
                        _targetAlly = ReferenceEquals(_targetAlly, unit) ? null : unit;
                    }
                    app.Refresh();
                });

            Controls(app, body);
        }

        /// <summary>⭐ **戦い方の2つ**（2026-08-22・作者の指示）。
        ///
        /// ⚠️ 器（Prefab）に足さずにここで作る ── 置き場所が画面の中身の量で変わるので、
        /// 固定の枠に入れると技の札とぶつかる。
        /// ⚠️ **上に置く。**⭐ 下は技の札と下の帯で埋まっている（実測 2026-08-22:
        /// 戦闘中も下の帯は出ている ── 「下端まで使ってよい」は誤り）。</summary>
        private static void Controls(App app, RectTransform body)
        {
            // ⚠️ **高さは `Ui.Tap` を下回らせない。**⭐ 84 にしていたら
            //    「指で押せない」と検査に叱られた（実測 2026-08-22）。
            const float Gap = 12f;
            float High = Ui.Tap;
            float wide = (Ui.W - Ui.Margin * 2f - Gap) / 2f;
            float top = 8f;

            // ⭐ **オートは入切の札。**⚠️ 押すたびに色が変わる（字だけだと今どちらか読めない）
            var auto = Ui.Tappable(body, "Auto", _auto ? "オート  ON" : "オート  OFF",
                () => { _auto = !_auto; app.Refresh(); },
                Ui.Margin, top, wide, High, lead: _auto);

            // ⭐ **あきらめるは負けと同じ扱い。**⚠️ 只で抜けられると、
            //    不利な戦いをいつでも無かったことにできてしまう。
            // ⚠️ 取り返しがつかないので、一度だけ確かめる。
            Ui.Tappable(body, "Give", "あきらめる",
                () => Confirm(app),
                Ui.Margin + wide + Gap, top, wide, High);
        }

        /// <summary>⚠️ **一度だけ確かめる。**⭐ 押し間違いで負けにしない。</summary>
        private static void Confirm(App app)
        {
            var state = app.Battle;
            if (state == null || state.Result != null) return;
            AskPanel.Show(app, "あきらめますか",
                // ⚠️ **札の字に印付けを混ぜない。**⭐ `**` や ⚠️ はコードの注釈の書き方であって、
                //    遊ぶ人の画面にそのまま出る（実測 2026-08-22: 生のまま出ていた）。
                "この戦いは負けになります。戻すことはできません。",
                "あきらめる", () =>
                {
                    var now = app.Battle;
                    if (now == null || now.Result != null) return;
                    // ⭐ **負けとして畳む。**⚠️ 画面側で勝敗を作らない ── Core が持つ
                    Core.Battle.Concede(now);
                    app.Refresh();
                });
        }
    }
}
