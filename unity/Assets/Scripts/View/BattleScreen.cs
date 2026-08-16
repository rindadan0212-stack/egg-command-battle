using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>戦闘。3体同時・スピードゲージ制・スキルごとの CT。
    ///
    /// ⭐ 配置はモック（参考/モック_タマゴハンター/mockshot-Battle.png）を**見て**合わせた:
    ///   左＝味方3体を縦一列（円のアバター＋名前＋2本の帯）
    ///   右＝相手を大きな円で1体
    ///   下＝白いシートに、幅広の1と並列の2・3
    ///
    /// ⚠️ 以前はこれを「文字の並び順」から推測して、上下に札を積むリストにしていた。
    /// 別物だった。**モックは必ず描画して見る。**
    ///
    /// ⚠️ モックにあって実装に無いものは置かない — WAVE / TURN / Lv / SP / 威力%。
    /// ⚠️ 判定は <see cref="Core.Battle"/>。この画面は描いて枠を渡すだけ。
    /// ⭐ 言葉で説明しない。何が起きたかは飛ぶ数字で見せる。
    /// </summary>
    public static class BattleScreen
    {
        // 左の列（味方）
        private const float ColLeft = 48f;
        private const float ColWidth = 462f;
        private const float RowTop = 250f;
        private const float RowStep = 300f;
        private const float Avatar = 150f;

        // 右（相手）
        private const float FoeLeft = 558f;
        private const float FoeWidth = 474f;

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

            int i = 0;
            foreach (var unit in SideOf(state, Side.Ally))
            {
                Ally(body, unit, RowTop + RowStep * i, _driver.Actor);
                i++;
            }

            foreach (var unit in SideOf(state, Side.Enemy))
            {
                Foe(app, body, unit, Core.Battle.LivingOf(state, Side.Enemy).Count > 1);
            }

            Sheet(app, body, state, height);
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

        /// <summary>味方。⭐ 円のアバター＋名前、その下に帯2本（モックの並び）。
        /// ⚠️ 器（白い札）に入れない。モックは地の上に直接置いている。</summary>
        private static void Ally(RectTransform body, Unit unit, float top, Unit actor)
        {
            bool alive = Core.Battle.IsAlive(unit);
            bool isActor = actor != null && ReferenceEquals(actor, unit);

            var slot = Ui.Rect($"Unit {unit.Key}", body);
            Ui.Place(slot, ColLeft, top, ColWidth, RowStep - 20f);

            // 円の地。⭐ 今動く者だけ縁を出す（「〜の番」と書かない）
            Ui.Round(slot, "Disc", 0f, 0f, Avatar, isActor ? Ui.Accent : Color.white);
            if (isActor) Ui.Round(slot, "Ring", 0f, 0f, Avatar, Color.white, outline: true);

            var art = Ui.PixelOf(slot, "Art", unit.Creature, 22f, 22f, Avatar - 44f);
            if (!alive) art.color = new Color(1f, 1f, 1f, 0.25f);

            ElementMark.Put(slot, Creatures.SpeciesOf(unit.Creature).Element, Avatar + 20f, 46f);
            Ui.Knockout(Ui.Label(slot, "Name", unit.Name, 32, Ui.Ink,
                TextAnchor.UpperLeft, Avatar + 56f, 40f, ColWidth - Avatar - 56f, 42f));
            Ui.Knockout(Ui.Label(slot, "Hp", $"{unit.Hp}/{unit.MaxHp}", 24, Ui.Ink,
                TextAnchor.UpperLeft, Avatar + 20f, 92f, ColWidth - Avatar - 20f, 32f), 3);

            // ⭐ 帯2本。上＝HP、下＝ゲージ（モックの HP/SP の位置）
            Ui.Bar(slot, "HpBar", unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f,
                alive ? Ui.Good : Ui.InkFaint, 0f, Avatar + 12f, ColWidth, 26f);
            Ui.Bar(slot, "Gauge", Mathf.Clamp01((float)unit.Gauge / Core.Battle.GaugeMax),
                new Color32(0x2f, 0xa8, 0xff, 0xff), 0f, Avatar + 46f, ColWidth, 20f);

            var statuses = Core.Battle.ActiveStatuses(unit);
            if (statuses.Count > 0)
            {
                Ui.Knockout(Ui.Label(slot, "Status", string.Join(" ", statuses), 20, Ui.Ink,
                    TextAnchor.UpperLeft, 0f, Avatar + 74f, ColWidth, 30f), 3);
            }
        }

        /// <summary>相手。⭐ 1体しか居ないので大きな円で構える。帯は円の**上**（モックの並び）。</summary>
        private static void Foe(App app, RectTransform body, Unit unit, bool selectable)
        {
            bool alive = Core.Battle.IsAlive(unit);
            var slot = Ui.Rect($"Unit {unit.Key}", body);
            Ui.Place(slot, FoeLeft, RowTop, FoeWidth, 760f);

            // ⚠️ 印を名前と同じ位置に置かない（重なって字が読めなくなった）
            ElementMark.Put(slot, Creatures.SpeciesOf(unit.Creature).Element, 0f, 2f);
            Ui.Knockout(Ui.Label(slot, "Name", unit.Name, 30, Ui.Ink,
                TextAnchor.UpperLeft, 36f, 0f, FoeWidth - 170f, 40f));
            int percent = unit.MaxHp > 0 ? Mathf.RoundToInt(100f * unit.Hp / unit.MaxHp) : 0;
            Ui.Knockout(Ui.Label(slot, "Percent", percent + "%", 32, Ui.Ink,
                TextAnchor.UpperRight, 0f, 0f, FoeWidth, 40f));
            Ui.Bar(slot, "HpBar", unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f,
                alive ? Ui.Danger : Ui.InkFaint, 0f, 46f, FoeWidth, 30f);

            // 大きな円。⭐ 画面の主役はここ
            const float Disc = 430f;
            Ui.Round(slot, "Disc", (FoeWidth - Disc) / 2f, 110f, Disc, Color.white);
            var art = Ui.PixelOf(slot, "Art", unit.Creature,
                (FoeWidth - Disc) / 2f + 55f, 165f, Disc - 110f);
            if (!alive) art.color = new Color(1f, 1f, 1f, 0.25f);

            var statuses = Core.Battle.ActiveStatuses(unit);
            if (statuses.Count > 0)
            {
                Ui.Knockout(Ui.Label(slot, "Status", string.Join(" ", statuses), 20, Ui.Ink,
                    TextAnchor.UpperLeft, 0f, 556f, FoeWidth, 40f), 3);
            }

            if (selectable && alive)
            {
                bool chosen = ReferenceEquals(_target, unit);
                Ui.Tappable(slot, "Pick", chosen ? "狙う" : "選ぶ",
                    () => { _target = unit; app.Refresh(); },
                    FoeWidth - 200f, 600f, 200f, Ui.Tap, chosen);
            }
        }

        /// <summary>白いシート。⭐ モックどおり 1 を幅広1行、2・3 を並列。
        /// 枠1は CT が無く必ず押せるので、幅がそのまま「いつでも打てる札」を表す。</summary>
        private static void Sheet(App app, RectTransform body, BattleState state, float height)
        {
            const float SheetH = 396f;
            float full = Ui.W - Ui.Margin * 2f;
            var sheet = Ui.Card(body, "Sheet", Ui.Margin, height - SheetH - 16f, full, SheetH);

            if (state.Result != null)
            {
                Ui.Tappable(sheet, "Finish", "戻る", () => { Leave(); app.FinishBattle(); },
                    32f, (SheetH - Ui.Tap) / 2f, full - 64f, Ui.Tap, true);
                return;
            }

            // ⚠️ 相手の手番の間にシートを空にしない。白い箱だけが残って壊れて見える。
            //    ⭐ 先頭の味方の札を**押せない状態で**出しておけば、次に何が打てるか分かる。
            var actor = _driver.Actor;
            if (actor == null)
            {
                foreach (var unit in SideOf(state, Side.Ally))
                {
                    if (!Core.Battle.IsAlive(unit)) continue;
                    actor = unit;
                    break;
                }
                if (actor == null) return;
            }
            bool myTurn = ReferenceEquals(actor, _driver.Actor);

            float inner = full - 64f;
            float half = (inner - 24f) / 2f;
            SkillCard(app, sheet, state, actor, 0, 32f, 36f, inner, 148f, myTurn);
            SkillCard(app, sheet, state, actor, 1, 32f, 208f, half, 148f, myTurn);
            SkillCard(app, sheet, state, actor, 2, 32f + half + 24f, 208f, half, 148f, myTurn);
        }

        private static void SkillCard(App app, RectTransform sheet, BattleState state, Unit actor,
            int slot, float left, float top, float width, float height, bool myTurn)
        {
            var skill = Core.Battle.SkillAt(actor, slot);
            if (skill == null) return;

            int cooldown = actor.Cooldowns[slot];
            bool usable = myTurn && Core.Battle.IsUsable(actor, slot);
            int captured = slot;

            var button = Ui.Tappable(sheet, $"Skill {slot}", "", () =>
            {
                var chosen = Core.Battle.NeedsTarget(skill) ? _target : null;
                int before = state.Log.Count;
                Core.Battle.PerformAction(state, actor, captured, chosen);
                _driver.ShowSince(state, before);
                _target = null;
                _driver.HandOff();
                app.Refresh();
            }, left, top, width, height, slot == 0 && usable, usable);

            Ui.Label(button.transform, "Name", skill.Name, slot == 0 ? 36 : 30,
                usable ? Ui.OnLead : Ui.InkFaint,
                TextAnchor.MiddleCenter, 8f, 0f, width - 16f, height);
            // ⚠️ 威力%（モックの「全体 220%」）は実装に無い。待ち数だけ
            Ui.Label(button.transform, "Ct", cooldown > 0 ? cooldown.ToString() : "",
                26, Ui.Danger, TextAnchor.LowerRight, 0f, height - 48f, width - 20f, 36f);
        }
    }
}
