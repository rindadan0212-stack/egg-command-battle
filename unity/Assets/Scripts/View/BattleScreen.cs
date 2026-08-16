using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>戦闘。3体同時・スピードゲージ制・スキルごとの CT。
    ///
    /// ⭐ 配置はモック（タマゴハンター）に合わせた:
    /// 上＝相手を大きく / 中＝味方3体を薄く / 下＝スキルシート（1を幅広、2・3を並列）。
    ///
    /// ⚠️ モックにあって実装に無いものは置かない — WAVE / TURN / Lv / SPゲージ / 威力%。
    /// 消したぶんは空けずに詰める。
    ///
    /// ⚠️ 判定は <see cref="Core.Battle"/> が全部持つ。この画面は描いて枠を渡すだけ。
    /// ⭐ 言葉で説明しない。何が起きたかは飛ぶ数字で見せる。
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

            float y = 16f;
            foreach (var unit in SideOf(state, Side.Enemy))
            {
                EnemyRow(app, body, unit, y, Core.Battle.LivingOf(state, Side.Enemy).Count > 1);
                y += 236f;
            }

            y += 16f;
            foreach (var unit in SideOf(state, Side.Ally))
            {
                AllyRow(body, unit, y, _driver.Actor);
                y += 124f;
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

        /// <summary>相手。⭐ 1体しか居ないので大きく構える（モックの BOSS 枠）。</summary>
        private static void EnemyRow(App app, RectTransform body, Unit unit, float top, bool selectable)
        {
            bool alive = Core.Battle.IsAlive(unit);
            float width = Ui.W - Ui.Margin * 2f;
            var panel = Ui.Card(body, $"Unit {unit.Key}", Ui.Margin, top, width, 220f);

            var image = Ui.PixelOf(panel, "Art", unit.Creature, 24f, 24f, 172f);
            if (!alive) image.color = new Color(1f, 1f, 1f, 0.22f);

            ElementMark.Put(panel, Creatures.SpeciesOf(unit.Creature).Element, 212f, 30f);
            Ui.Label(panel, "Name", unit.Name, 38, alive ? Ui.Ink : Ui.InkFaint,
                TextAnchor.UpperLeft, 248f, 24f, width - 440f, 46f);

            // ⭐ 残りは割合で大きく（モックの「68%」）。実数も添えて数を隠さない
            int percent = unit.MaxHp > 0 ? Mathf.RoundToInt(100f * unit.Hp / unit.MaxHp) : 0;
            Ui.Label(panel, "Percent", percent + "%", 40, Ui.Danger,
                TextAnchor.UpperRight, 248f, 22f, width - 272f, 50f);
            Ui.Bar(panel, "HpBar", unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f,
                alive ? Ui.Danger : Ui.InkFaint, 212f, 92f, width - 236f, 22f);
            Ui.Label(panel, "Hp", $"{unit.Hp}/{unit.MaxHp}", 24, Ui.InkDim,
                TextAnchor.UpperRight, 212f, 120f, width - 236f, 32f);
            Ui.Bar(panel, "Gauge", Mathf.Clamp01((float)unit.Gauge / Core.Battle.GaugeMax),
                Ui.Accent, 212f, 158f, width - 236f, 8f);

            var statuses = Core.Battle.ActiveStatuses(unit);
            if (statuses.Count > 0)
            {
                Ui.Label(panel, "Status", string.Join("  ", statuses), 22, Ui.InkDim,
                    TextAnchor.UpperLeft, 212f, 174f, width - 236f, 34f);
            }

            if (selectable && alive)
            {
                bool chosen = ReferenceEquals(_target, unit);
                Ui.Tappable(panel, "Pick", chosen ? "狙う" : "選ぶ",
                    () => { _target = unit; app.Refresh(); },
                    width - 200f, 220f - Ui.Tap - 12f, 180f, Ui.Tap, chosen);
            }
        }

        /// <summary>味方。⭐ 3体を薄く並べる。⚠️ Lv も SP も実装に無いので置かない。</summary>
        private static void AllyRow(RectTransform body, Unit unit, float top, Unit actor)
        {
            bool alive = Core.Battle.IsAlive(unit);
            bool isActor = actor != null && ReferenceEquals(actor, unit);
            float width = Ui.W - Ui.Margin * 2f;
            var panel = Ui.Card(body, $"Unit {unit.Key}", Ui.Margin, top, width, 112f);

            if (isActor) Ui.Block(panel, "Now", Ui.Accent, 0f, 0f, 8f, 112f);

            var image = Ui.PixelOf(panel, "Art", unit.Creature, 16f, 12f, 88f);
            if (!alive) image.color = new Color(1f, 1f, 1f, 0.22f);

            ElementMark.Put(panel, Creatures.SpeciesOf(unit.Creature).Element, 116f, 14f);
            Ui.Label(panel, "Name", unit.Name, 28, alive ? Ui.Ink : Ui.InkFaint,
                TextAnchor.UpperLeft, 152f, 10f, 300f, 36f);
            Ui.Label(panel, "Hp", $"{unit.Hp}/{unit.MaxHp}", 24, Ui.InkDim,
                TextAnchor.UpperRight, 152f, 10f, width - 176f, 34f);
            Ui.Bar(panel, "HpBar", unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f,
                alive ? Ui.Good : Ui.InkFaint, 116f, 52f, width - 140f, 14f);
            Ui.Bar(panel, "Gauge", Mathf.Clamp01((float)unit.Gauge / Core.Battle.GaugeMax),
                Ui.Accent, 116f, 72f, width - 140f, 6f);

            var statuses = Core.Battle.ActiveStatuses(unit);
            if (statuses.Count > 0)
            {
                Ui.Label(panel, "Status", string.Join(" ", statuses), 20, Ui.InkDim,
                    TextAnchor.UpperLeft, 116f, 82f, width - 140f, 26f);
            }
        }

        /// <summary>スキルシート。⭐ モックどおり 1 を幅広1行、2・3 を並列。
        /// 枠1は CT が無く必ず押せるので、幅がそのまま「いつでも打てる札」を表す。</summary>
        private static void BuildHand(App app, RectTransform body, BattleState state, Unit actor, float height)
        {
            float full = Ui.W - Ui.Margin * 2f;
            float half = (full - 20f) / 2f;
            float top = height - 300f;

            SkillCard(app, body, state, actor, 0, Ui.Margin, top, full, 128f);
            SkillCard(app, body, state, actor, 1, Ui.Margin, top + 144f, half, 128f);
            SkillCard(app, body, state, actor, 2, Ui.Margin + half + 20f, top + 144f, half, 128f);
        }

        private static void SkillCard(App app, RectTransform body, BattleState state, Unit actor,
            int slot, float left, float top, float width, float height)
        {
            var skill = Core.Battle.SkillAt(actor, slot);
            if (skill == null)
            {
                Ui.Block(body, $"Empty {slot}", new Color32(0x1e, 0x1b, 0x17, 0xff), left, top, width, height);
                return;
            }

            int cooldown = actor.Cooldowns[slot];
            bool usable = Core.Battle.IsUsable(actor, slot);
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
                usable ? (slot == 0 ? Ui.OnLead : Ui.Ink) : Ui.InkFaint,
                TextAnchor.MiddleCenter, 4f, 0f, width - 8f, height);
            // ⚠️ 威力%（モックの「全体 220%」）は実装に無い（段位で持っている）。待ち数だけ出す
            Ui.Label(button.transform, "Ct", cooldown > 0 ? cooldown.ToString() : "",
                26, Ui.Danger, TextAnchor.LowerRight, 0f, height - 46f, width - 16f, 36f);
        }
    }
}
