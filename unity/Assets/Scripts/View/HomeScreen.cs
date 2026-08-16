using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>ホーム。⭐ 輪のハブ。編成が画面の主役。
    ///
    /// ⭐ 並びは Assets/Resources/Prefabs/HomeScreen.prefab が持つ。ここに座標は無い。
    /// </summary>
    public static class HomeScreen
    {
        public static void Build(App app, RectTransform body)
        {
            var view = app.Put<HomeView>(body, "HomeScreen");
            if (view != null) view.Bind(app.Game);
        }
    }

    /// <summary>探索。⭐ ランダムな巣が3つだけ出る。
    ///
    /// ⚠️ 見せるのは絵とレベルだけ。中身が分かると「勝てる相手だけ選ぶ」になり、
    /// 飛ばして確かめるという芯が消える。
    /// </summary>
    public static class NestsScreen
    {
        public static void Build(App app, RectTransform body)
        {
            // ⚠️ 減っていたら補う。空欄のまま置かない
            Encounters.Refill(app.Game);

            var view = app.Put<NestsView>(body, "NestsScreen");
            if (view == null) return;
            view.Bind(app.Game,
                encounter => StealScreen.Enter(app, encounter.Nest),
                () => app.EnterBattle(null, true));
        }
    }
}
