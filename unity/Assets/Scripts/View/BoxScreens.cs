using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>孵化。⭐ モックどおり**台座付きの枠を並べる**（空き枠も見せる）。
    /// ⚠️ モックの孵化タイマー（01:12:40）は実装に無いので置かない。
    /// 野生の卵はここでスキル2・3のガチャが引かれる。保管庫が満杯なら孵さない。</summary>
    public static class HatchScreen
    {
        private const int Columns = 3;
        private const float SlotH = 240f;

        public static void Build(App app, RectTransform body, float height)
        {
            var game = app.Game;
            bool full = Storages.IsFull(game.Storage);

            // ⭐ 空き枠も見せる。⚠️「卵がない。探索で奪ってくる」と字で書かない
            int slots = Mathf.Max(6, ((game.Eggs.Count + Columns - 1) / Columns) * Columns);
            float gap = 16f;
            float cell = (Ui.W - Ui.Margin * 2f - gap * (Columns - 1)) / Columns;
            int rows = (slots + Columns - 1) / Columns;

            var content = Ui.Scroller(body, "Eggs", 0f, 0f, Ui.W, height,
                rows * (SlotH + gap) + 24f);

            for (int i = 0; i < slots; i++)
            {
                float left = (cell + gap) * (i % Columns);
                float top = 8f + (SlotH + gap) * (i / Columns);
                if (i < game.Eggs.Count) Slot(app, content, game.Eggs[i], left, top, cell, full);
                else Empty(content, i, left, top, cell);
            }
        }

        private static void Empty(RectTransform content, int index, float left, float top, float width)
        {
            var card = Ui.Card(content, $"Slot {index}", Ui.Margin + left, top, width, SlotH);
            // 台座だけ置く。⭐ 何も無いことは、空の器が伝える
            Ui.Block(card, "Stand", new Color32(0x4a, 0x55, 0x60, 0xff),
                width / 2f - 56f, SlotH - 76f, 112f, 14f);
        }

        private static void Slot(App app, RectTransform content, Egg egg,
            float left, float top, float width, bool full)
        {
            var card = Ui.Card(content, $"Egg {egg.Id}", Ui.Margin + left, top, width, SlotH);
            var species = SpeciesTable.ById(egg.SpeciesId);
            int palette = Mathf.Min(egg.PaletteIndex, species.Palettes.Count - 1);

            Ui.Block(card, "Stand", new Color32(0x4a, 0x55, 0x60, 0xff),
                width / 2f - 56f, 122f, 112f, 14f);
            Ui.Pixel(card, "Art", EggArt.Sprite, EggArt.Shell, width / 2f - 48f, 26f, 96f);

            // ⚠️ レア度★（モック）は実装に無い。素質の数だけ出す
            Ui.Label(card, "Wild", Stats.TotalOf(egg.Wild).ToString(), 30, Ui.Ink,
                TextAnchor.UpperLeft, 14f, 12f, width - 28f, 36f);
            ElementMark.Put(card, species.Element, width - 40f, 14f);
            if (egg.MutationCounter > 0)
            {
                Ui.Label(card, "Mut", new string('★', Mathf.Min(3, egg.MutationCounter)), 22, Ui.Accent,
                    TextAnchor.LowerLeft, 14f, 96f, width - 28f, 30f);
            }

            Ui.Tappable(card, "Hatch", "孵す", () =>
            {
                Games.HatchEgg(app.Game, egg.Id);
                app.Refresh();
            }, 12f, SlotH - Ui.Tap - 12f, width - 24f, Ui.Tap, true, !full);
        }
    }

    /// <summary>配合。ARK 準拠。⭐ モックどおり**上を2体に割り、中央に＋**、最下部に大ボタン。
    /// ⚠️ モックの ◈コスト・レア度★・所要時間は実装に無いので置かない。</summary>
    public static class BreedScreen
    {
        private static string _a;
        private static string _b;

        public static void Build(App app, RectTransform body, float height)
        {
            var creatures = app.Game.Storage.Creatures;
            var a = Find(creatures, _a);
            var b = Find(creatures, _b);
            bool ready = a != null && b != null && Breeding.CanBreed(a, b);

            float full = Ui.W - Ui.Margin * 2f;
            float half = (full - 64f) / 2f;

            // ── 上: 親2体と ＋ ────────────────────────
            var left = Ui.Card(body, "ParentA", Ui.Margin, 12f, half, 200f);
            Parent(left, a, half);
            var right = Ui.Card(body, "ParentB", Ui.Margin + half + 64f, 12f, half, 200f);
            Parent(right, b, half);
            Ui.Label(body, "Plus", "＋", 48, Ui.Accent,
                TextAnchor.MiddleCenter, Ui.Margin + half, 12f, 64f, 200f);

            // ── 出る卵 ────────────────────────────────
            var result = Ui.Card(body, "Result", Ui.Margin, 224f, full, 128f);
            Ui.Pixel(result, "Egg", EggArt.Sprite, EggArt.Shell, 20f, 16f, 96f);
            if (ready)
            {
                List<string> speciesNames, skillPool;
                bool mutable;
                Breeding.PreviewOf(a, b, out speciesNames, out skillPool, out mutable);
                Ui.Label(result, "Species", string.Join(" / ", speciesNames), 28, Ui.Ink,
                    TextAnchor.UpperLeft, 132f, 20f, full - 220f, 36f);
                Ui.Label(result, "Skills", string.Join("・", skillPool), 22, Ui.InkDim,
                    TextAnchor.UpperLeft, 132f, 60f, full - 160f, 50f);
                // ⭐ 変異が出うるかは印1つ。⚠️ 確率を字で説明しない
                Ui.Block(result, "Mut", mutable ? Ui.Accent : new Color32(0x3a, 0x36, 0x30, 0xff),
                    full - 64f, 20f, 44f, 16f);
            }

            Ui.Tappable(body, "Breed", "配合する", () =>
            {
                var outcome = Games.BreedPair(app.Game, _a, _b);
                if (outcome.Mutations > 0)
                {
                    var fx = Fx.Get(app.transform);
                    fx.Number(fx.PointOf(result, Vector2.zero),
                        new string('★', outcome.Mutations), Ui.Accent, 64f);
                }
                _a = null;
                _b = null;
                app.Refresh();
            }, Ui.Margin, 364f, full, Ui.Tap, true, ready);

            // ── 下: 一覧（BOX と同じ4列グリッド） ──────
            float listTop = 364f + Ui.Tap + 16f;
            Grid.Build(body, creatures, listTop, height - listTop, id =>
            {
                if (id == _a) _a = null;
                else if (id == _b) _b = null;
                else if (_a == null) _a = id;
                else _b = id;
                app.Refresh();
            }, id => id == _a || id == _b);
        }

        private static void Parent(RectTransform card, Creature creature, float width)
        {
            if (creature == null)
            {
                Ui.Block(card, "Stand", new Color32(0x4a, 0x55, 0x60, 0xff),
                    width / 2f - 48f, 150f, 96f, 12f);
                return;
            }
            Ui.PixelOf(card, "Art", creature, width / 2f - 52f, 16f, 104f);
            Ui.Label(card, "Name", Creatures.SpeciesOf(creature).Name, 26, Ui.Ink,
                TextAnchor.UpperCenter, 8f, 126f, width - 16f, 34f);
            Ui.Label(card, "Wild", Creatures.WildTotalOf(creature).ToString(), 24, Ui.InkDim,
                TextAnchor.UpperCenter, 8f, 160f, width - 16f, 32f);
            ElementMark.Put(card, Creatures.SpeciesOf(creature).Element, 12f, 12f);
        }

        private static Creature Find(IReadOnlyList<Creature> list, string id)
        {
            if (id == null) return null;
            foreach (var creature in list)
            {
                if (creature.Id == id) return creature;
            }
            return null;
        }
    }

    /// <summary>保管庫。⭐ モックどおり**上に選んだ個体の詳細、下に4列グリッド**。
    /// 枠は有限。どれを逃がすかの整理が遊びになる。</summary>
    public static class BoxScreen
    {
        private static SortKey _sort = SortKey.WildTotal;
        private static string _picked;

        public static void Build(App app, RectTransform body, float height)
        {
            var game = app.Game;
            var sorted = Storages.Sorted(game.Storage, _sort);
            if (sorted.Count == 0) return;

            var creature = null as Creature;
            foreach (var c in sorted) if (c.Id == _picked) creature = c;
            if (creature == null) { creature = sorted[0]; _picked = creature.Id; }

            float full = Ui.W - Ui.Margin * 2f;

            // ── 上: 選んだ個体 ────────────────────────
            var card = Ui.Card(body, $"C {creature.Id}", Ui.Margin, 12f, full, 452f);
            Ui.PixelOf(card, "Art", creature, 20f, 20f, 132f);
            ElementMark.Put(card, Creatures.SpeciesOf(creature).Element, 168f, 24f);
            Ui.Label(card, "Name", Creatures.SpeciesOf(creature).Name, 34, Ui.Ink,
                TextAnchor.UpperLeft, 204f, 18f, 300f, 42f);
            Ui.Label(card, "Id", creature.Id, 22, Ui.InkDim,
                TextAnchor.UpperLeft, 204f, 60f, 300f, 30f);

            // 実値4本。⚠️ モックの Lv は実装に無い
            var stats = Creatures.StatsOf(creature);
            for (int i = 0; i < Stats.Keys.Length; i++)
            {
                var key = Stats.Keys[i];
                float rowTop = 184f + i * 36f;
                Ui.Label(card, $"K {key}", Stats.LabelOf(key), 22, Ui.InkDim,
                    TextAnchor.UpperLeft, 20f, rowTop, 96f, 32f);
                Ui.Label(card, $"V {key}", stats[key].ToString(), 24, Ui.Ink,
                    TextAnchor.UpperLeft, 120f, rowTop, 90f, 32f);
                Ui.Bar(card, $"B {key}", Mathf.Clamp01(creature.Wild[key] / 60f), Ui.Good,
                    216f, rowTop + 10f, 220f, 12f);
            }

            var skills = Creatures.SkillsOf(creature);
            for (int i = 0; i < skills.Length; i++)
            {
                var skill = skills[i];
                Ui.Label(card, $"S {i}", skill == null ? "—" : skill.Name, 24,
                    skill == null ? Ui.InkFaint : Ui.Ink,
                    TextAnchor.UpperLeft, 500f, 184f + i * 36f, 300f, 32f);
                if (skill != null && i > 0)
                {
                    Ui.Label(card, $"SC {i}", skill.Ct.ToString(), 22, Ui.InkDim,
                        TextAnchor.UpperRight, 500f, 184f + i * 36f, 420f, 32f);
                }
            }

            bool inParty = Games.IsInParty(game, creature.Id);
            Ui.Tappable(card, "Party", inParty ? "出撃中" : "出撃",
                () => { Games.TogglePartyMember(game, creature.Id); app.Refresh(); },
                full - 400f, 24f, 180f, Ui.Tap, inParty);
            Ui.Tappable(card, "Release", "逃がす",
                () => { Games.ReleaseCreature(game, creature.Id); _picked = null; app.Refresh(); },
                full - 208f, 24f, 188f, Ui.Tap);

            // 育成。⚠️「戻せない」「戦闘に勝つと増える」は書かない
            int unspent = Creatures.UnspentOf(creature);
            float spendW = (full - 40f - 12f * 3f) / 4f;
            for (int i = 0; i < Stats.Keys.Length; i++)
            {
                var key = Stats.Keys[i];
                Ui.Tappable(card, $"Spend {key}", $"{Stats.LabelOf(key)}＋",
                    () => { Creatures.SpendPoint(creature, key); app.Refresh(); },
                    20f + (spendW + 12f) * i, 452f - Ui.Tap - 16f, spendW, Ui.Tap, false, unspent > 0);
            }
            if (unspent > 0)
            {
                Ui.Label(card, "Point", "＋" + unspent, 26, Ui.Accent,
                    TextAnchor.UpperLeft, 204f, 60f, 300f, 30f);
            }

            // ── 並べ替え ──────────────────────────────
            float tabTop = 476f;
            float tabW = (full - 12f * 6f) / 7f;
            for (int i = 0; i < Storages.SortKeys.Length; i++)
            {
                var key = Storages.SortKeys[i];
                // WARN: the default size does not fit 128px; shrink before placing
                var tab = Ui.Tappable(body, $"Sort {key}", Storages.LabelOf(key),
                    () => { _sort = key; app.Refresh(); },
                    Ui.Margin + (tabW + 12f) * i, tabTop, tabW, Ui.Tap, _sort == key);
                Ui.Shrink(tab, 20);
            }

            // ── 下: 4列グリッド ───────────────────────
            float gridTop = tabTop + Ui.Tap + 12f;
            Grid.Build(body, sorted, gridTop, height - gridTop,
                id => { _picked = id; app.Refresh(); },
                id => id == _picked);
        }
    }

    /// <summary>4列のアイコングリッド。⭐ モックの ALL MONSTERS 部分。
    /// ⚠️ BOX と配合で同じ形にする。一覧の読み方が画面ごとに変わらないように。</summary>
    public static class Grid
    {
        private const int Columns = 4;
        private const float Cell = 200f;

        public static void Build(RectTransform body, IReadOnlyList<Creature> list,
            float top, float height, System.Action<string> onPick, System.Func<string, bool> isPicked)
        {
            float gap = 12f;
            float width = (Ui.W - Ui.Margin * 2f - gap * (Columns - 1)) / Columns;
            int rows = (list.Count + Columns - 1) / Columns;
            var content = Ui.Scroller(body, "Grid", 0f, top, Ui.W, height,
                rows * (Cell + gap) + 20f);

            for (int i = 0; i < list.Count; i++)
            {
                var creature = list[i];
                float left = Ui.Margin + (width + gap) * (i % Columns);
                float cellTop = 6f + (Cell + gap) * (i / Columns);
                bool picked = isPicked(creature.Id);

                var card = Ui.Card(content, $"G {creature.Id}", left, cellTop, width, Cell);
                if (picked) Ui.Block(card, "Mark", Ui.Accent, 0f, 0f, width, 8f);

                Ui.PixelOf(card, "Art", creature, width / 2f - 44f, 34f, 88f);
                ElementMark.Put(card, Creatures.SpeciesOf(creature).Element, 10f, 10f);
                // ⚠️ Lv は実装に無い。素質合計を出す
                Ui.Label(card, "Wild", Creatures.WildTotalOf(creature).ToString(), 28, Ui.Ink,
                    TextAnchor.UpperCenter, 6f, 134f, width - 12f, 36f);

                // WARN: do not draw the wooden plate here; make the card itself tappable
                string id = creature.Id;
                Ui.HitArea(card, "Pick", () => onPick(id), 0f, 0f, width, Cell);
            }
        }
    }
}
