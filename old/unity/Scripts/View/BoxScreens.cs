using System.Collections.Generic;
using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>配合。ARK 準拠。⭐ 並びは Prefabs/BreedScreen.prefab が持つ。</summary>
    public static class BreedScreen
    {
        private static string _a;
        private static string _b;
        private static FilterKey _filter = FilterKey.All;
        private static SortKey _sort = SortKey.WildTotal;
        /// <summary>何の数で並べるか。⭐ 既定は**素質だけ**
        /// （厳選のための画面なので、生まれつきの良し悪しが先に見えるほうがよい）。</summary>
        private static SortBasis _basis = SortBasis.Born;

        public static void Build(App app, RectTransform body)
        {
            // ⭐ **絞ってから並べる**（BOX と同じ順）。
            var pool = Filters.Apply(app.Game, app.Game.Storage.Creatures, _filter);
            var creatures = Storages.Sorted(
                new Storage(app.Game.Storage.Slots, pool), _sort);
            // ⚠️ 選んでいる親は、絞って消えても札には残す
            //    （絞った瞬間に選び直しになるのを避ける）。
            var a = Find(app.Game.Storage.Creatures, _a);
            var b = Find(app.Game.Storage.Creatures, _b);

            var view = app.Put<BreedView>(body, "BreedScreen");
            if (view == null) return;

            view.Bind(creatures, a, b,
                onBreed: () =>
                {
                    // ⭐ 2体が卵に還る。両親は失われる
                    var outcome = Games.FusePair(app.Game, _a, _b);
                    _a = null;
                    _b = null;
                    Fanfare.EggGot(app.Overlay, outcome.Egg, () => app.Show(Screen.Home));
                },
                filter: _filter, sort: _sort,
                onFilter: key => { _filter = key; app.Refresh(); },
                onSort: key => { _sort = key; app.Refresh(); },
                repaint: () => app.Refresh(),
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
        /// <summary>何の数で並べるか。⭐ 既定は**素質だけ**
        /// （厳選のための画面なので、生まれつきの良し悪しが先に見えるほうがよい）。</summary>
        private static SortBasis _basis = SortBasis.Born;
        /// <summary>一覧を絞る軸。⭐ 並べ替えだけでは候補の数が減らない。</summary>
        private static FilterKey _filter = FilterKey.All;
        private static string _picked;
        /// <summary>合成で食わせる相手。⚠️ 選んでいる個体とは別に持つ。</summary>
        private static string _food;

        public static void Build(App app, RectTransform body)
        {
            var game = app.Game;
            // ⭐ **絞ってから並べる。**逆にすると、絞ったあとの順が崩れる。
            var kept = Filters.Apply(game, game.Storage.Creatures, _filter);
            var sorted = Storages.Sorted(new Storage(game.Storage.Slots, kept), _sort, _basis);

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
                // ⭐ 個体もたまごも、育てる道はここ1つ
                onFilter: key => { _filter = key; app.Refresh(); },
                repaint: () => app.Refresh(),
                filter: _filter,
                onFuse: () => FusePanel.Show(app, creature.Id),
                onGrow: () => { Core.Idle.Spend(game.Idle, creature); app.Refresh(); },
                basis: _basis,
                onBasis: b => { _basis = b; app.Refresh(); },
                // ⭐ 技の札を**長押し**すると詳細（2026-08-21・作者の指示）。
                // ⚠️ 短く触っても開かない ── この札は押しどころではないので、
                //    触っただけで開くと一覧を選ぶ指が誤爆する。
                onSkillHeld: (skill, level, slot) => SkillInfoPanel.Show(app, skill, level, slot),
                // ⭐ たまごで技を鍛える。⚠️ 分解とは**別の入口**（2026-08-22）
                onTrain: () => SkillEggPanel.Show(app, creature.Id));
        }
    }
}
