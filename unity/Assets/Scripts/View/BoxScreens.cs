using System.Collections.Generic;
using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>孵化。⭐ 上の5枠が孵化器、下の棚が手持ちの卵。
    ///
    /// ⭐ 実時間で孵る。枠が有限なので「どれを先に温めるか」が選択になる。
    /// ⭐ 並びは Assets/Resources/Prefabs/HatchScreen.prefab が持つ。ここに座標は無い。
    /// </summary>
    public static class HatchScreen
    {
        public static void Build(App app, RectTransform body)
        {
            var view = app.Put<HatchView>(body, "HatchScreen");
            if (view == null) return;

            view.Bind(app.Game, app.Now, app.HatchSpeed,
                onBegin: egg =>
                {
                    Hatchery.Begin(app.Game, egg.Id, app.Now(), app.HatchSpeed);
                    app.Refresh();
                },
                onCollect: slot =>
                {
                    // ⚠️ 保管庫が満杯なら孵さない（黙って捨てない）
                    if (Storages.IsFull(app.Game.Storage)) { app.Show(Screen.Box); return; }
                    var born = Hatchery.Collect(app.Game, slot.Egg.Id, app.Now());
                    if (born == null) { app.Refresh(); return; }
                    Fanfare.Born(app.Overlay, born, () => app.Show(Screen.Hatch));
                });
        }
    }

    /// <summary>配合。ARK 準拠。⭐ 並びは Prefabs/BreedScreen.prefab が持つ。</summary>
    public static class BreedScreen
    {
        private static string _a;
        private static string _b;

        public static void Build(App app, RectTransform body)
        {
            var creatures = app.Game.Storage.Creatures;
            var a = Find(creatures, _a);
            var b = Find(creatures, _b);

            var view = app.Put<BreedView>(body, "BreedScreen");
            if (view == null) return;

            view.Bind(creatures, a, b,
                onBreed: () =>
                {
                    // ⭐ 2体が卵に還る。両親は失われる
                    var outcome = Games.FusePair(app.Game, _a, _b);
                    _a = null;
                    _b = null;
                    Fanfare.EggGot(app.Overlay, outcome.Egg, () => app.Show(Screen.Hatch));
                },
                onPick: id =>
                {
                    if (id == _a) _a = null;
                    else if (id == _b) _b = null;
                    else if (_a == null) _a = id;
                    else _b = id;
                    app.Refresh();
                });
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
    /// ⭐ 並びは Prefabs/BoxScreen.prefab が持つ。</summary>
    public static class BoxScreen
    {
        private static SortKey _sort = SortKey.WildTotal;
        private static string _picked;
        /// <summary>合成で食わせる相手。⚠️ 選んでいる個体とは別に持つ。</summary>
        private static string _food;

        public static void Build(App app, RectTransform body)
        {
            var game = app.Game;
            var sorted = Storages.Sorted(game.Storage, _sort);

            Creature creature = null, food = null;
            foreach (var c in sorted)
            {
                if (c.Id == _picked) creature = c;
                if (c.Id == _food) food = c;
            }
            if (creature == null && sorted.Count > 0) { creature = sorted[0]; _picked = creature.Id; }
            if (food != null && creature != null && food.Id == creature.Id) { food = null; _food = null; }

            var view = app.Put<BoxView>(body, "BoxScreen");
            if (view == null) return;

            view.Bind(game, creature, _sort, sorted,
                onSort: key => { _sort = key; app.Refresh(); },
                // ⭐ 一覧を押すのは「見る」だけ。押すたびに意味が変わる画面にしない
                onPick: id => { _picked = id; app.Refresh(); },
                onParty: () => { Games.TogglePartyMember(game, creature.Id); app.Refresh(); },
                onRelease: () => { Games.ReleaseCreature(game, creature.Id); _picked = null; app.Refresh(); },
                food: food,
                onMarkFood: () =>
                {
                    _food = _food == creature.Id ? null : creature.Id;
                    app.Refresh();
                },
                onFeed: () =>
                {
                    Games.FeedCreature(game, creature.Id, food.Id);
                    _food = null;
                    app.Refresh();
                });
        }
    }
}
