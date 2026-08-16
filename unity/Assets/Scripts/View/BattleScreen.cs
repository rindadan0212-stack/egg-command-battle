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
    /// ⭐ 敵の手番はここで自動で進める。プレイヤーの手番まで一気に送るので、
    /// 画面に出るのは常に「自分が選ぶ場面」だけになる。
    /// </summary>
    public static class BattleScreen
    {
        private static Unit _target;

        public static void Build(App app, RectTransform body, float height)
        {
            var state = app.Battle;
            if (state == null) { app.Show(Screen.Nests); return; }

            // ⭐ プレイヤーが選ぶ場面まで進める
            var actor = Advance(state);

            float y = 16f;

            // ── 敵 ──────────────────────────────────────
            var enemies = Core.Battle.LivingOf(state, Side.Enemy);
            Ui.Label(body, "EnemyLabel", "敵", 24, Ui.InkDim,
                TextAnchor.UpperLeft, Ui.Margin, y, 200f, 30f);
            y += 34f;
            foreach (var unit in AllOf(state, Side.Enemy))
            {
                UnitRow(app, body, unit, y, true, enemies.Count > 1);
                y += 188f;
            }

            y += 12f;

            // ── 味方 ────────────────────────────────────
            Ui.Label(body, "AllyLabel", "編成", 24, Ui.InkDim,
                TextAnchor.UpperLeft, Ui.Margin, y, 200f, 30f);
            y += 34f;
            foreach (var unit in AllOf(state, Side.Ally))
            {
                UnitRow(app, body, unit, y, false, false, actor);
                y += 188f;
            }

            // ── 出来事 ──────────────────────────────────
            // ⚠️ 残りを全部ログ帯にすると、暗い面が画面の半分を占めて「何も無い」ように見える。
            //    高さを決め打ちして手札の真上に置き、余りは地の色のままにする。
            const float LogHeight = 240f;
            float handTop = height - (state.Result != null ? 160f : 296f);
            float logTop = handTop - LogHeight - 12f;
            if (logTop > y) BuildLog(state, body, logTop, LogHeight);

            // ── 手 ──────────────────────────────────────
            if (state.Result != null)
            {
                string message = state.Result == Outcome.Ally ? "勝った"
                    : state.Result == Outcome.Enemy ? "負けた" : "決着つかず";
                Ui.Label(body, "Result", message, 44,
                    state.Result == Outcome.Ally ? Ui.Good : Ui.Danger,
                    TextAnchor.MiddleCenter, 0f, height - 148f, Ui.W, 52f);
                Ui.Tappable(body, "Finish", "戻る", () => app.FinishBattle(),
                    Ui.Margin, height - 92f, Ui.W - Ui.Margin * 2f, Ui.Tap, true);
            }
            else if (actor != null)
            {
                BuildHand(app, body, state, actor, height);
            }
        }

        /// <summary>敵の手番を自動で進め、味方が選ぶところで止める。</summary>
        private static Unit Advance(BattleState state)
        {
            int guard = 0;
            while (state.Result == null && guard++ < Core.Battle.MaxActions * 3)
            {
                var actor = Core.Battle.NextActor(state);
                if (actor == null) return null;
                if (actor.Side == Side.Ally) return actor;

                // ⚠️ AI は乱数を使わない。同じ状況からは必ず同じ手を選ぶ
                int slot = Ai.ChooseAction(state, actor);
                Core.Battle.PerformAction(state, actor, slot);
            }
            return null;
        }

        private static List<Unit> AllOf(BattleState state, Side side)
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

            var panel = Ui.Card(body, $"Unit {unit.Key}", Ui.Margin, top, width, 176f, isActor);

            // ⭐ 「今動く者」だけを差し色の一辺で示す（面と線を二重に使わない）
            if (isActor) Ui.Block(panel, "Now", Ui.Accent, 0f, 0f, 6f, 176f);

            var image = Ui.PixelOf(panel, "Art", unit.Creature, 20f, 20f, 88f);
            if (!alive) image.color = new Color(1f, 1f, 1f, 0.25f);

            Ui.Label(panel, "Name", unit.Name, 30, alive ? Ui.Ink : Ui.InkFaint,
                TextAnchor.UpperLeft, 124f, 16f, width - 300f, 40f);
            Ui.Label(panel, "Hp", $"{unit.Hp}/{unit.MaxHp}", 26, Ui.InkDim,
                TextAnchor.UpperRight, 124f, 16f, width - 148f, 36f);

            Ui.Bar(panel, "HpBar", unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f,
                alive ? (isEnemy ? Ui.Danger : Ui.Good) : Ui.InkFaint,
                124f, 60f, width - 148f, 14f);

            // ゲージ。⭐ 満ちた者が動く
            Ui.Bar(panel, "Gauge", Mathf.Clamp01((float)unit.Gauge / Core.Battle.GaugeMax),
                Ui.InkFaint, 124f, 82f, width - 148f, 6f);

            var statuses = Core.Battle.ActiveStatuses(unit);
            string line = statuses.Count > 0 ? string.Join(" ", statuses) : "";
            Ui.Label(panel, "Status", line, 22, Ui.InkDim,
                TextAnchor.UpperLeft, 124f, 100f, width - 148f, 60f);

            // 単体攻撃の狙い先。⚠️ 敵が1体しかいないときは選ばせない
            if (selectable && alive && isEnemy)
            {
                bool chosen = ReferenceEquals(_target, unit);
                Ui.Tappable(panel, "Pick", chosen ? "狙う" : "選ぶ",
                    () => { _target = unit; app.Refresh(); },
                    width - 200f, 176f - Ui.Tap - 8f, 180f, Ui.Tap, chosen);
            }
        }

        private static void BuildLog(BattleState state, RectTransform body, float top, float height)
        {
            Ui.Block(body, "LogBg", new Color32(0x16, 0x12, 0x10, 0xff), 0f, top, Ui.W, height);

            var lines = new List<string>();
            int from = Mathf.Max(0, state.Log.Count - 6);
            for (int i = from; i < state.Log.Count; i++) lines.Add(Describe(state, state.Log[i]));

            Ui.Label(body, "Log", string.Join("\n", lines), 24, Ui.InkDim,
                TextAnchor.LowerLeft, Ui.Margin, top + 8f, Ui.W - Ui.Margin * 2f, height - 16f);
        }

        /// <summary>出来事を1行の日本語にする。⚠️ ここが唯一の言い換え。</summary>
        private static string Describe(BattleState state, BattleEvent e)
        {
            string who = NameOf(state, e.Unit);
            switch (e.Kind)
            {
                case BattleEventKind.Act: return $"{who} の {e.Label}";
                case BattleEventKind.Damage:
                    return e.Absorbed > 0 ? $"  {who} は盾で防いだ" : $"  {who} に {e.Amount}";
                case BattleEventKind.Heal: return $"  {who} が {e.Amount} 回復";
                case BattleEventKind.Buff:
                    return $"  {who} の{Stats.LabelOf(e.Stat)} {(e.Percent > 0 ? "+" : "")}{e.Percent}%";
                case BattleEventKind.Poison: return $"  {who} は毒で {e.Amount}";
                case BattleEventKind.Regen: return $"  {who} が {e.Amount} 回復";
                case BattleEventKind.Applied: return $"  {who} に {e.Label}";
                case BattleEventKind.Shield: return $"  {who} に盾 {e.Amount}枚";
                case BattleEventKind.Stun: return $"  {who} の手番を飛ばす";
                case BattleEventKind.Skipped: return $"{who} は動けない";
                case BattleEventKind.Ct: return $"  {who} の待ちが {(e.Delta > 0 ? "+" : "")}{e.Delta}";
                case BattleEventKind.Taunt: return $"  {who} が引き受ける（{e.Hits}回）";
                case BattleEventKind.Guts: return $"  {who} が踏みとどまる構え";
                case BattleEventKind.GutsSaved: return $"  {who} は HP1 で耐えた";
                case BattleEventKind.Immune: return $"  {who} は弱化を受けない";
                case BattleEventKind.Blocked: return $"  {who} には効かない";
                case BattleEventKind.Down: return $"  {who} は倒れた";
                default: return "";
            }
        }

        private static string NameOf(BattleState state, string key)
        {
            foreach (var unit in state.Units)
            {
                if (unit.Key == key) return unit.Side == Side.Ally ? unit.Name : $"敵{unit.Name}";
            }
            return key;
        }

        /// <summary>手札。⭐ 枠1は CT が無いので必ず押せる（「たたかう」の代わり）。</summary>
        private static void BuildHand(App app, RectTransform body, BattleState state, Unit actor, float height)
        {
            float top = height - 296f;
            Ui.Block(body, "HandBg", new Color32(0x16, 0x12, 0x10, 0xff), 0f, top, Ui.W, 296f);
            Ui.Label(body, "Turn", $"{actor.Name} の番", 28, Ui.Accent,
                TextAnchor.UpperLeft, Ui.Margin, top + 12f, Ui.W - Ui.Margin * 2f, 36f);

            float width = (Ui.W - Ui.Margin * 2f - 24f * 2f) / 3f;
            for (int slot = 0; slot < 3; slot++)
            {
                var skill = Core.Battle.SkillAt(actor, slot);
                float left = Ui.Margin + (width + 24f) * slot;
                if (skill == null)
                {
                    Ui.Block(body, $"Empty {slot}", new Color32(0x1a, 0x17, 0x14, 0xff),
                        left, top + 56f, width, 200f);
                    Ui.Label(body, $"EmptyLabel {slot}", "空き", 24, Ui.InkFaint,
                        TextAnchor.MiddleCenter, left, top + 56f, width, 200f);
                    continue;
                }

                int cooldown = actor.Cooldowns[slot];
                bool usable = Core.Battle.IsUsable(actor, slot);
                int capturedSlot = slot;

                var button = Ui.Tappable(body, $"Skill {slot}", "", () =>
                {
                    var chosen = Core.Battle.NeedsTarget(skill) ? _target : null;
                    Core.Battle.PerformAction(state, actor, capturedSlot, chosen);
                    _target = null;
                    app.Refresh();
                }, left, top + 56f, width, 200f, slot == 0 && usable, usable);

                Ui.Label(button.transform, "Name", skill.Name, 28,
                    usable ? (slot == 0 ? new Color32(0x1a, 0x16, 0x12, 0xff) : Ui.Ink) : Ui.InkFaint,
                    TextAnchor.UpperCenter, 0f, 16f, width, 40f);
                Ui.Label(button.transform, "Gist", skill.Gist, 20,
                    usable ? (slot == 0 ? new Color32(0x4a, 0x3c, 0x22, 0xff) : Ui.InkDim) : Ui.InkFaint,
                    TextAnchor.UpperCenter, 8f, 60f, width - 16f, 90f);
                // ⭐ CT は技ではなく枠の性質。枠1は常に 0
                Ui.Label(button.transform, "Ct",
                    slot == 0 ? "いつでも" : cooldown > 0 ? $"あと {cooldown}" : $"CT {skill.Ct}",
                    22, cooldown > 0 ? Ui.Danger : Ui.InkDim,
                    TextAnchor.LowerCenter, 0f, 156f, width, 36f);
            }
        }
    }
}
