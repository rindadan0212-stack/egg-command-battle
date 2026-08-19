using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>戦闘。3体同時・スピードゲージ制・スキルごとの CT。
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
                onDetail: (skill, level) => SkillInfoPanel.Show(app, skill, level),
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
        }
    }
}
