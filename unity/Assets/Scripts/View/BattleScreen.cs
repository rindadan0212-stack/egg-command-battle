using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>戦闘。3体同時・スピードゲージ制・スキルごとの CT。
    ///
    /// ⚠️ 判定は <see cref="Core.Battle"/> が全部持つ。この画面は
    /// 「今の状態を描く」「枠を選んで渡す」しかしない。
    ///
    /// ⭐ **言葉で説明しない。**
    /// 何が起きたかは飛ぶ数字と点滅で見せる。以前は下に戦闘ログを流していたが、
    /// 読ませている間は画面を見ていない。数字が当たった体の上に出るほうが早い。
    /// </summary>
    public static class BattleScreen
    {
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

            float y = 24f;
            foreach (var unit in SideOf(state, Side.Enemy))
            {
                UnitRow(app, body, unit, y, true, Core.Battle.LivingOf(state, Side.Enemy).Count > 1);
                y += 184f;
            }

            // ⭐ 敵と味方は「上と下」で分ける。見出しを置かない
            y += 40f;

            foreach (var unit in SideOf(state, Side.Ally))
            {
                UnitRow(app, body, unit, y, false, false, _driver.Actor);
                y += 184f;
            }

            if (state.Result != null)
            {
                Ui.Tappable(body, "Finish", "戻る", () => { Leave(); app.FinishBattle(); },
                    Ui.Margin, height - 132f, Ui.W - Ui.Margin * 2f, Ui.Tap, true);
            }
            else if (_driver.Actor != null)
            {
                BuildHand(app, body, state, _driver.Actor, height);
            }
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

        private static void UnitRow(App app, RectTransform body, Unit unit, float top,
            bool isEnemy, bool selectable, Unit actor = null)
        {
            bool alive = Core.Battle.IsAlive(unit);
            bool isActor = actor != null && ReferenceEquals(actor, unit);
            float width = Ui.W - Ui.Margin * 2f;

            var panel = Ui.Card(body, $"Unit {unit.Key}", Ui.Margin, top, width, 168f);

            // ⭐ 今動く者は差し色の一辺だけで示す（「〜の番」と書かない）
            if (isActor) Ui.Block(panel, "Now", Ui.Accent, 0f, 0f, 8f, 168f);

            var image = Ui.PixelOf(panel, "Art", unit.Creature, 20f, 20f, 88f);
            if (!alive) image.color = new Color(1f, 1f, 1f, 0.22f);


            // ⭐ 属性は色の印。名前の隣に置く（字で「鱗」と書かない）
            ElementMark.Put(panel, Creatures.SpeciesOf(unit.Creature).Element, 124f, 22f);
            Ui.Label(panel, "Name", unit.Name, 30, alive ? Ui.Ink : Ui.InkFaint,
                TextAnchor.UpperLeft, 160f, 18f, width - 360f, 40f);
            Ui.Label(panel, "Hp", $"{unit.Hp}/{unit.MaxHp}", 26, Ui.InkDim,
                TextAnchor.UpperRight, 124f, 18f, width - 148f, 36f);

            Ui.Bar(panel, "HpBar", unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f,
                alive ? (isEnemy ? Ui.Danger : Ui.Good) : Ui.InkFaint,
                124f, 66f, width - 148f, 16f);
            Ui.Bar(panel, "Gauge", Mathf.Clamp01((float)unit.Gauge / Core.Battle.GaugeMax),
                Ui.Accent, 124f, 90f, width - 148f, 6f);

            // かかっている状態は短い札のまま（名前と数だけ。説明ではない）
            var statuses = Core.Battle.ActiveStatuses(unit);
            if (statuses.Count > 0)
            {
                Ui.Label(panel, "Status", string.Join("  ", statuses), 22, Ui.InkDim,
                    TextAnchor.UpperLeft, 124f, 108f, width - 148f, 50f);
            }

            if (selectable && alive && isEnemy)
            {
                bool chosen = ReferenceEquals(_target, unit);
                Ui.Tappable(panel, "Pick", chosen ? "狙う" : "選ぶ",
                    () => { _target = unit; app.Refresh(); },
                    width - 200f, 168f - Ui.Tap - 8f, 180f, Ui.Tap, chosen);
            }
        }

        /// <summary>手札。⭐ 枠1は CT が無いので必ず押せる。
        /// ⚠️ 技の説明文を載せない。名前と「あと何回待つか」だけで足りる。</summary>
        private static void BuildHand(App app, RectTransform body, BattleState state, Unit actor, float height)
        {
            float top = height - 236f;
            float width = (Ui.W - Ui.Margin * 2f - 20f * 2f) / 3f;

            for (int slot = 0; slot < 3; slot++)
            {
                var skill = Core.Battle.SkillAt(actor, slot);
                float left = Ui.Margin + (width + 20f) * slot;
                if (skill == null) continue;

                int cooldown = actor.Cooldowns[slot];
                bool usable = Core.Battle.IsUsable(actor, slot);
                int capturedSlot = slot;

                var button = Ui.Tappable(body, $"Skill {slot}", "", () =>
                {
                    var chosen = Core.Battle.NeedsTarget(skill) ? _target : null;
                    int before = state.Log.Count;
                    Core.Battle.PerformAction(state, actor, capturedSlot, chosen);
                    _driver.ShowSince(state, before);
                    _target = null;
                    _driver.HandOff();
                    app.Refresh();
                }, left, top, width, 196f, slot == 0 && usable, usable);

                Ui.Label(button.transform, "Name", skill.Name, 28,
                    usable ? (slot == 0 ? Ui.OnLead : Ui.Ink) : Ui.InkFaint,
                    TextAnchor.MiddleCenter, 4f, 40f, width - 8f, 80f);
                // ⭐ 待ちは数だけ。⚠️「防御力が高いほど強い一撃」のような説明は置かない
                Ui.Label(button.transform, "Ct", cooldown > 0 ? cooldown.ToString() : "",
                    30, Ui.Danger, TextAnchor.LowerCenter, 0f, 140f, width, 44f);
            }
        }
    }
}
