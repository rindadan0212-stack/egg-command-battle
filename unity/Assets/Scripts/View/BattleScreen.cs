using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>戦闘。3体同時・スピードゲージ制・スキルごとの CT。
    ///
    /// ⭐ 配置は実際の対戦ゲームの画面（ユーザー提供の参考スクショ）に合わせた:
    ///   左右の列で向かい合い、**枠に入れず地の上に直接立つ**
    ///   ゲージは**キャラの真下に短いピル**（左に丸い数字、右に帯）
    ///   縦にジグザグ（左右で高さをずらす）
    ///   スキルは地の上に浮く。上に幅広1つ、下に2つ並列。各ボタン内に CT の小さいピル
    ///
    /// ⚠️ 列幅いっぱいの帯にしない。**誰の量なのか**が離れると読めなくなる。
    /// ⚠️ モックにあって実装に無いものは置かない — WAVE / TURN / Lv / SP / 威力%。
    /// ⚠️ 判定は <see cref="Core.Battle"/>。この画面は描いて枠を渡すだけ。
    /// ⭐ 言葉で説明しない。何が起きたかは飛ぶ数字で見せる。
    /// </summary>
    public static class BattleScreen
    {
        private const float AllyX = 60f;
        private const float FoeX = 600f;
        private const float AllySize = 200f;
        private const float FoeSize = 320f;
        private const float RowStep = 300f;

        private static Unit _target;
        private static BattleDriver _driver;

        public static void Leave()
        {
            if (_driver != null)
            {
                Object.Destroy(_driver.gameObject);
                _driver = null;
            }
        }

        public static void Build(App app, RectTransform body, float height)
        {
            var state = app.Battle;
            if (state == null) { app.Show(Screen.Nests); return; }

            if (_driver == null) _driver = BattleDriver.Create(app);
            _driver.Bind(app, state);

            // ⭐ 左の列。⚠️ ジグザグにするため、右の列とは基準の高さをずらす
            int i = 0;
            foreach (var unit in SideOf(state, Side.Ally))
            {
                Stand(app, body, unit, AllyX, 150f + RowStep * i, AllySize, false, _driver.Actor);
                i++;
            }

            // ⭐ 右の列。1体しか居ないので大きく、列の真ん中あたりに置く
            var foes = SideOf(state, Side.Enemy);
            bool pickable = Core.Battle.LivingOf(state, Side.Enemy).Count > 1;
            for (int k = 0; k < foes.Count; k++)
            {
                Stand(app, body, foes[k], FoeX, 300f + RowStep * k, FoeSize, true, _driver.Actor, pickable);
            }

            Hand(app, body, state, height);
        }

        private static List<Unit> SideOf(BattleState state, Side side)
        {
            var list = new List<Unit>();
            foreach (var unit in state.Units)
            {
                if (unit.Side == side) list.Add(unit);
            }
            return list;
        }

        private static GameObject _standPrefab;

        /// <summary>1体を立たせる。
        ///
        /// ⭐ **中身の配置は Assets/Prefabs/UnitStand.prefab が持つ。**
        /// ここは Prefab を置いて値を流し込むだけ。⚠️ 座標を書き足さない。
        /// 見た目を直したいときは Unity Editor で Prefab を開く。それが移植の目的。
        /// </summary>
        private static void Stand(App app, RectTransform body, Unit unit,
            float left, float top, float size, bool isFoe, Unit actor, bool pickable = false)
        {
            if (_standPrefab == null) _standPrefab = Resources.Load<GameObject>("Prefabs/UnitStand");
            if (_standPrefab == null)
            {
                // ⚠️ 黙って何も出さない、をしない。無いことに気づけないほうが困る
                Debug.LogError("UnitStand.prefab が読めない（Resources/Prefabs にあるか）");
                return;
            }

            var go = Object.Instantiate(_standPrefab, body);
            go.name = $"Unit {unit.Key}";
            var slot = (RectTransform)go.transform;
            slot.anchorMin = new Vector2(0f, 1f);
            slot.anchorMax = new Vector2(0f, 1f);
            slot.pivot = new Vector2(0f, 1f);
            slot.anchoredPosition = new Vector2(left, -top);
            // ⭐ 相手は1体なので大きく見せる。Prefab の寸法に倍率を掛けるだけ
            float scale = size / 200f;
            slot.localScale = new Vector3(scale, scale, 1f);

            go.GetComponent<UnitStand>().Bind(unit, actor != null && ReferenceEquals(actor, unit), isFoe);

            if (pickable && Core.Battle.IsAlive(unit) && isFoe)
            {
                bool chosen = ReferenceEquals(_target, unit);
                Ui.Tappable(slot, "Pick", chosen ? "狙う" : "選ぶ",
                    () => { _target = unit; app.Refresh(); },
                    0f, 280f, 200f, Ui.Tap, chosen);
            }
        }

        /// <summary>手札。⭐ 参考画面どおり、地の上に浮かせる。
        /// 上に幅広1つ、下に2つ並列。⚠️ 白いシートに載せない。</summary>
        private static void Hand(App app, RectTransform body, BattleState state, float height)
        {
            float full = Ui.W - Ui.Margin * 2f;
            float wide = full * 0.66f;
            float half = (full - 24f) / 2f;
            float top = height - 320f;

            if (state.Result != null)
            {
                Ui.Tappable(body, "Finish", "戻る", () => { Leave(); app.FinishBattle(); },
                    Ui.Margin, height - 160f, full, Ui.Tap, true);
                return;
            }

            // ⚠️ 相手の手番の間も札を出す。空にすると壊れて見える
            var actor = _driver.Actor;
            if (actor == null)
            {
                foreach (var unit in SideOf(state, Side.Ally))
                {
                    if (Core.Battle.IsAlive(unit)) { actor = unit; break; }
                }
                if (actor == null) return;
            }
            bool myTurn = ReferenceEquals(actor, _driver.Actor);

            Skill(app, body, state, actor, 0, (Ui.W - wide) / 2f, top, wide, 130f, myTurn);
            Skill(app, body, state, actor, 1, Ui.Margin, top + 146f, half, 130f, myTurn);
            Skill(app, body, state, actor, 2, Ui.Margin + half + 24f, top + 146f, half, 130f, myTurn);
        }

        private static void Skill(App app, RectTransform body, BattleState state, Unit actor,
            int slot, float left, float top, float width, float height, bool myTurn)
        {
            var skill = Core.Battle.SkillAt(actor, slot);
            if (skill == null) return;

            int cooldown = actor.Cooldowns[slot];
            bool usable = myTurn && Core.Battle.IsUsable(actor, slot);
            int captured = slot;

            var button = Ui.Tappable(body, $"Skill {slot}", "", () =>
            {
                var chosen = Core.Battle.NeedsTarget(skill) ? _target : null;
                int before = state.Log.Count;
                Core.Battle.PerformAction(state, actor, captured, chosen);
                _driver.ShowSince(state, before);
                _target = null;
                _driver.HandOff();
                app.Refresh();
            }, left, top, width, height, slot == 0 && usable, usable);

            Ui.Label(button.transform, "Name", skill.Name, slot == 0 ? 34 : 28,
                usable ? Ui.OnLead : Ui.InkFaint,
                TextAnchor.UpperCenter, 8f, 16f, width - 16f, 44f);

            // ⭐ 参考画面の `CT 6` と同じ形の小さいピル。⚠️ Lv は実装に無いので置かない
            Ui.MiniPill(button.transform, "Ct",
                slot == 0 ? "CT 0" : cooldown > 0 ? $"あと {cooldown}" : $"CT {skill.Ct}",
                (width - 150f) / 2f, 70f, 150f);
        }
    }
}
