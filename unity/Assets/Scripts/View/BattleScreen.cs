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
        private static Unit _target;
        private static BattleDriver _driver;
        private static GameObject _prefab;

        public static void Leave()
        {
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
            view.Bind(state, _driver.Actor, _target,
                onSkill: slot =>
                {
                    var actor = _driver.Actor;
                    if (actor == null) return;
                    var skill = Core.Battle.SkillAt(actor, slot);
                    var chosen = skill != null && Core.Battle.NeedsTarget(skill) ? _target : null;
                    _target = null;
                    // ⚠️ ここで計算しない。名乗り → 着弾 → 間 の3拍は Driver が持つ
                    _driver.Queue(actor, slot, chosen);
                },
                onFinish: () => { Leave(); app.FinishBattle(); },
                onPick: () =>
                {
                    foreach (var unit in state.Units)
                    {
                        if (unit.Side == Side.Enemy && Core.Battle.IsAlive(unit)) { _target = unit; break; }
                    }
                    app.Refresh();
                });
        }
    }
}
