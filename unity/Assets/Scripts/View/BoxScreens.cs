using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>孵化。⭐ 野生の卵はここでスキル2・3のガチャが引かれる。
    /// ⚠️ 保管庫が満杯なら孵さない（黙って捨てない）。</summary>
    public static class HatchScreen
    {
        private const float Row = 176f;

        public static void Build(App app, RectTransform body, float height)
        {
            var game = app.Game;
            float top = 0f;

            if (app.Notice.Length > 0)
            {
                Ui.Label(body, "Notice", app.Notice, 26, Ui.InkDim,
                    TextAnchor.UpperLeft, Ui.Margin, 16f, Ui.W - Ui.Margin * 2f, 40f);
                top = 62f;
            }

            if (Storages.IsFull(game.Storage))
            {
                Ui.Label(body, "Full", $"保管庫が満杯（{game.Storage.Slots}枠）。BOX でどれかを逃がす。",
                    28, Ui.Danger, TextAnchor.UpperLeft, Ui.Margin, top + 8f, Ui.W - Ui.Margin * 2f, 44f);
                top += 60f;
            }

            if (game.Eggs.Count == 0)
            {
                Ui.Label(body, "Empty", "卵がない。探索で巣から奪ってくる。", 30, Ui.InkDim,
                    TextAnchor.MiddleCenter, Ui.Margin, top, Ui.W - Ui.Margin * 2f, height - top - 100f);
                return;
            }

            float contentHeight = game.Eggs.Count * (Row + 16f) + 32f;
            var content = Ui.Scroller(body, "Eggs", 0f, top, Ui.W, height - top, contentHeight);

            float y = 8f;
            foreach (var egg in new List<Egg>(game.Eggs))
            {
                Egg captured = egg;
                var panel = Ui.Block(content, $"Egg {egg.Id}", Ui.Panel, Ui.Margin, y,
                    Ui.W - Ui.Margin * 2f, Row);
                float width = Ui.W - Ui.Margin * 2f;

                var species = SpeciesTable.ById(egg.SpeciesId);
                int paletteIndex = Mathf.Min(egg.PaletteIndex, species.Palettes.Count - 1);
                Ui.Pixel(panel, "Art", species.Sprite, species.Palettes[paletteIndex], 20f, 20f, 80f);

                Ui.Label(panel, "Id", $"{egg.Id}  {species.Name}", 30, Ui.Ink,
                    TextAnchor.UpperLeft, 116f, 16f, width - 300f, 40f);
                Ui.Label(panel, "How", HowLabel(egg.How), 22,
                    egg.How == EggOrigin.Stolen ? Ui.Danger : Ui.InkDim,
                    TextAnchor.UpperLeft, 116f, 58f, 300f, 32f);
                Ui.Label(panel, "Wild",
                    $"素質 {Stats.TotalOf(egg.Wild)}  HP{egg.Wild.Hp} 攻{egg.Wild.Atk} 防{egg.Wild.Def} 速{egg.Wild.Spd}",
                    22, Ui.InkDim, TextAnchor.UpperLeft, 116f, 92f, width - 140f, 32f);
                if (egg.MutationCounter > 0)
                {
                    Ui.Label(panel, "Mut", $"変異 {egg.MutationCounter}", 22, Ui.Accent,
                        TextAnchor.UpperRight, 116f, 58f, width - 140f, 32f);
                }

                Ui.Tappable(panel, "Hatch", "孵す", () =>
                {
                    var creature = Games.HatchEgg(app.Game, captured.Id);
                    app.Notice = $"{creature.Id} が生まれた。";
                    app.Refresh();
                }, width - 220f, Row - 128f, 200f, Ui.Tap, true, !Storages.IsFull(game.Storage));

                y += Row + 16f;
            }
        }

        private static string HowLabel(EggOrigin how)
        {
            switch (how)
            {
                case EggOrigin.Defeated: return "倒して奪った";
                case EggOrigin.Stolen: return "盗んだ（素質は落ちる）";
                default: return "配合";
            }
        }
    }

    /// <summary>配合。ARK 準拠。⭐ ステごとに独立ロールするのが厳選の中毒性の源。
    /// ⚠️ 卵は保管庫ではなく卵の棚に入る（孵すまでが1手間）。</summary>
    public static class BreedScreen
    {
        private static string _a;
        private static string _b;

        public static void Build(App app, RectTransform body, float height)
        {
            var creatures = app.Game.Storage.Creatures;

            if (app.Notice.Length > 0)
            {
                Ui.Label(body, "Notice", app.Notice, 26, Ui.InkDim,
                    TextAnchor.UpperLeft, Ui.Margin, 12f, Ui.W - Ui.Margin * 2f, 40f);
            }

            // ── 選んだ2体と、起こりうること ──────────────
            float panelTop = 56f;
            var a = Find(creatures, _a);
            var b = Find(creatures, _b);
            var preview = Ui.Block(body, "Preview", Ui.Panel, Ui.Margin, panelTop,
                Ui.W - Ui.Margin * 2f, 250f);
            float width = Ui.W - Ui.Margin * 2f;

            Slot(preview, "親A", a, 24f, 20f);
            Slot(preview, "親B", b, width / 2f + 12f, 20f);

            bool ready = a != null && b != null && Breeding.CanBreed(a, b);
            if (ready)
            {
                List<string> speciesNames, skillPool;
                bool mutable;
                Breeding.PreviewOf(a, b, out speciesNames, out skillPool, out mutable);
                Ui.Label(preview, "Pred",
                    $"種族: {string.Join(" / ", speciesNames)}\n技の候補: {string.Join("・", skillPool)}",
                    22, Ui.InkDim, TextAnchor.UpperLeft, 24f, 124f, width - 48f, 66f);
                // ⚠️ 無限強化のブレーキ。親のどちらかが 20 未満でなければ変異は出ない
                Ui.Label(preview, "Mut", mutable ? "変異あり（2.5%×3回）" : "変異は出ない（両親とも上限）",
                    22, mutable ? Ui.Accent : Ui.InkFaint,
                    TextAnchor.UpperLeft, 24f, 192f, width - 48f, 32f);
            }
            else
            {
                Ui.Label(preview, "Hint", "下から2体えらぶ（同じ個体どうしは配合できない）", 24, Ui.InkDim,
                    TextAnchor.UpperLeft, 24f, 124f, width - 48f, 40f);
            }

            Ui.Tappable(body, "Breed", "配合する", () =>
            {
                var outcome = Games.BreedPair(app.Game, _a, _b);
                app.Notice = outcome.Mutations > 0
                    ? $"卵 {outcome.Egg.Id}。⭐ 変異が {outcome.Mutations} 回出た。"
                    : $"卵 {outcome.Egg.Id} ができた。孵化で孵す。";
                _a = null;
                _b = null;
                app.Refresh();
            }, Ui.Margin, panelTop + 266f, Ui.W - Ui.Margin * 2f, Ui.Tap, true, ready);

            // ── 一覧 ────────────────────────────────────
            float listTop = panelTop + 266f + Ui.Tap + 20f;
            float rowHeight = 132f;
            var content = Ui.Scroller(body, "List", 0f, listTop, Ui.W, height - listTop,
                creatures.Count * (rowHeight + 12f) + 24f);

            float y = 8f;
            foreach (var creature in creatures)
            {
                Creature captured = creature;
                bool picked = creature.Id == _a || creature.Id == _b;
                var row = Ui.Block(content, $"C {creature.Id}",
                    picked ? Ui.PanelHi : Ui.Panel, Ui.Margin, y, Ui.W - Ui.Margin * 2f, rowHeight);
                if (picked) Ui.Block(row, "Mark", Ui.Accent, 0f, 0f, 6f, rowHeight);

                Ui.PixelOf(row, "Art", creature, 20f, 18f, 96f);
                Ui.Label(row, "Name",
                    $"{creature.Id}  {Creatures.SpeciesOf(creature).Name}", 28, Ui.Ink,
                    TextAnchor.UpperLeft, 132f, 16f, 420f, 38f);
                Ui.Label(row, "Wild",
                    $"素質 {Creatures.WildTotalOf(creature)} / 世代 {creature.Generation} / 変異 {creature.MutationCounter}",
                    22, Ui.InkDim, TextAnchor.UpperLeft, 132f, 58f, 560f, 32f);

                Ui.Tappable(row, "Pick", picked ? "はずす" : "えらぶ", () =>
                {
                    if (captured.Id == _a) _a = null;
                    else if (captured.Id == _b) _b = null;
                    else if (_a == null) _a = captured.Id;
                    else _b = captured.Id;
                    app.Refresh();
                }, Ui.W - Ui.Margin * 2f - 200f, 12f, 180f, Ui.Tap, picked);

                y += rowHeight + 12f;
            }
        }

        private static void Slot(RectTransform parent, string label, Creature creature, float left, float top)
        {
            Ui.Label(parent, $"L {label}", label, 22, Ui.InkDim,
                TextAnchor.UpperLeft, left, top, 200f, 30f);
            if (creature == null)
            {
                Ui.Label(parent, $"V {label}", "—", 30, Ui.InkFaint,
                    TextAnchor.UpperLeft, left, top + 32f, 300f, 44f);
                return;
            }
            Ui.PixelOf(parent, $"A {label}", creature, left, top + 30f, 64f);
            Ui.Label(parent, $"V {label}",
                $"{creature.Id}\n素質 {Creatures.WildTotalOf(creature)}", 24, Ui.Ink,
                TextAnchor.UpperLeft, left + 76f, top + 30f, 240f, 70f);
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

    /// <summary>保管庫。枠は有限。どれを逃がすかの整理が遊びになる。
    /// ⭐ 出撃の3体をここで決める。飛距離も戦闘もこの3体で決まる。</summary>
    public static class BoxScreen
    {
        private static SortKey _sort = SortKey.WildTotal;
        private static string _open;

        public static void Build(App app, RectTransform body, float height)
        {
            var game = app.Game;

            // ── 並べ替え ────────────────────────────────
            // ⚠️ 7つを1行に詰めると1つ 133px で語が入らない。4 + 3 の2段にする
            float y = 16f;
            float buttonWidth = (Ui.W - Ui.Margin * 2f - 12f * 3f) / 4f;
            for (int i = 0; i < Storages.SortKeys.Length; i++)
            {
                var key = Storages.SortKeys[i];
                int row = i / 4;
                int column = i % 4;
                Ui.Tappable(body, $"Sort {key}", Storages.LabelOf(key),
                    () => { _sort = key; app.Refresh(); },
                    Ui.Margin + (buttonWidth + 12f) * column, y + row * (Ui.Tap + 12f),
                    buttonWidth, Ui.Tap, _sort == key);
            }
            y += (Ui.Tap + 12f) * 2f + 12f;

            var sorted = Storages.Sorted(game.Storage, _sort);
            // ⚠️ 押しどころが 112 あるので、行はそれを収める高さが要る
            float rowHeight = 208f;
            // 育成を開いたときの追加ぶん。中身（説明34 + 振る112 + 技40 + 逃がす112）が入る高さ
            float openExtra = 348f;
            float contentHeight = 24f;
            foreach (var creature in sorted)
                contentHeight += rowHeight + 12f + (creature.Id == _open ? openExtra : 0f);

            var content = Ui.Scroller(body, "Box", 0f, y, Ui.W, height - y, contentHeight);

            float rowY = 8f;
            foreach (var creature in sorted)
            {
                Creature captured = creature;
                bool open = creature.Id == _open;
                bool inParty = Games.IsInParty(game, creature.Id);
                float thisHeight = rowHeight + (open ? openExtra : 0f);

                var row = Ui.Block(content, $"C {creature.Id}",
                    inParty ? Ui.PanelHi : Ui.Panel, Ui.Margin, rowY, Ui.W - Ui.Margin * 2f, thisHeight);
                float width = Ui.W - Ui.Margin * 2f;
                if (inParty) Ui.Block(row, "Mark", Ui.Accent, 0f, 0f, 6f, thisHeight);

                Ui.PixelOf(row, "Art", creature, 20f, 18f, 104f);

                var stats = Creatures.StatsOf(creature);
                Ui.Label(row, "Name",
                    $"{creature.Id}  {Creatures.SpeciesOf(creature).Name}"
                    + $"  {SpeciesTable.LabelOf(Creatures.SpeciesOf(creature).Element)}",
                    28, Ui.Ink, TextAnchor.UpperLeft, 140f, 14f, 520f, 38f);
                Ui.Label(row, "Wild",
                    $"素質 {Creatures.WildTotalOf(creature)}"
                    + $"（HP{creature.Wild.Hp} 攻{creature.Wild.Atk} 防{creature.Wild.Def} 速{creature.Wild.Spd}）",
                    22, Ui.InkDim, TextAnchor.UpperLeft, 140f, 54f, 620f, 32f);
                Ui.Label(row, "Actual",
                    $"実値 HP{stats.Hp} 攻{stats.Atk} 防{stats.Def} 速{stats.Spd}"
                    + $"  世代{creature.Generation} 変異{creature.MutationCounter}",
                    22, Ui.InkDim, TextAnchor.UpperLeft, 140f, 88f, 700f, 32f);

                int unspent = Creatures.UnspentOf(creature);
                if (unspent > 0)
                {
                    Ui.Label(row, "Point", $"育成 +{unspent}", 24, Ui.Accent,
                        TextAnchor.UpperRight, 140f, 14f, width - 164f, 34f);
                }

                Ui.Tappable(row, "Party", inParty ? "出撃中" : "出撃",
                    () => { Games.TogglePartyMember(game, captured.Id); app.Refresh(); },
                    width - 400f, rowHeight - Ui.Tap - 16f, 180f, Ui.Tap, inParty);
                Ui.Tappable(row, "Open", open ? "閉じる" : "育成",
                    () => { _open = open ? null : captured.Id; app.Refresh(); },
                    width - 204f, rowHeight - Ui.Tap - 16f, 184f, Ui.Tap);

                if (open) Detail(app, row, creature, rowHeight, width);

                rowY += thisHeight + 12f;
            }
        }

        /// <summary>育成。⚠️ 振ったら戻せない（取り返しがつかないほうが判断に重みが出る）。</summary>
        private static void Detail(App app, RectTransform row, Creature creature, float top, float width)
        {
            int unspent = Creatures.UnspentOf(creature);
            Ui.Label(row, "Train",
                unspent > 0 ? $"振れる点 {unspent}（戻せない）" : "振れる点がない。戦闘に勝つと増える。",
                24, unspent > 0 ? Ui.Ink : Ui.InkDim,
                TextAnchor.UpperLeft, 24f, top + 8f, width - 48f, 34f);

            float buttonWidth = (width - 48f - 12f * 3f) / 4f;
            for (int i = 0; i < Stats.Keys.Length; i++)
            {
                var key = Stats.Keys[i];
                Ui.Tappable(row, $"Spend {key}",
                    $"{Stats.LabelOf(key)} +1\n{creature.Trained[key]}",
                    () => { Creatures.SpendPoint(creature, key); app.Refresh(); },
                    24f + (buttonWidth + 12f) * i, top + 48f, buttonWidth, Ui.Tap, false, unspent > 0);
            }

            var skills = Creatures.SkillsOf(creature);
            var names = new List<string>();
            for (int i = 0; i < skills.Length; i++)
            {
                var skill = skills[i];
                names.Add(skill == null ? "空き"
                    : i == 0 ? $"{skill.Name}（枠1・いつでも）" : $"{skill.Name}（CT{skill.Ct}）");
            }
            Ui.Label(row, "Skills", string.Join(" / ", names), 22, Ui.InkDim,
                TextAnchor.UpperLeft, 24f, top + 48f + Ui.Tap + 10f, width - 48f, 40f);

            // ⚠️ 逃がすと編成からも外れる。技の行と横に並べず、下に置く（重なるため）
            Ui.Tappable(row, "Release", "逃がす", () =>
            {
                Games.ReleaseCreature(app.Game, creature.Id);
                _open = null;
                app.Refresh();
            }, width - 204f, top + 48f + Ui.Tap + 58f, 184f, Ui.Tap);
        }
    }
}
