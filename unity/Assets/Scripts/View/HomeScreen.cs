using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>ホーム。⭐ 上半分が放置（素材が溜まる）、下半分が孵化器。
    ///
    /// ⚠️ 孵化は独立した画面をやめてここへ集めた。輪の待ち時間が2つ（放置と孵化）
    /// あるので、同じ画面で両方が進んでいるのが見えるほうが素直。
    /// </summary>
    public static class HomeScreen
    {
        public static void Build(App app, RectTransform body)
        {
            var view = app.Put<HomeView>(body, "HomeScreen");
            if (view == null) return;

            view.Bind(app,
                onBegin: (slot, egg) =>
                {
                    Hatchery.Begin(app.Game, egg.Id, app.Now(), app.HatchSpeed, slot);
                    app.Refresh();
                },
                onCollect: slot =>
                {
                    // ⚠️ 保管庫が満杯なら孵さない（黙って捨てない）
                    if (Storages.IsFull(app.Game.Storage)) { app.Show(Screen.Box); return; }
                    var born = Hatchery.Collect(app.Game, slot.Egg.Id, app.Now());
                    if (born == null) { app.Refresh(); return; }
                    // ⚠️ 枠が空いていれば入れる。埋まっていれば触らない
                    Games.LockParty(app.Game);
                    Fanfare.Born(app.Overlay, born, () => app.Show(Screen.Home));
                });
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
            // ⭐ 居座る時間が切れた巣を先に片付けてから補充する
            //    ⚠️ 順が逆だと、消えるはずの巣が枠を埋めたまま残る
            long now = app.Now();
            // ⚠️ 期限を持たない巣（時刻を渡さずに始めた古い保存）に、いまから期限を与える。
            //    ⭐ 消さずに数え直す ── 起動しただけで探索が作り替わらないように
            Encounters.Stamp(app.Game, now);
            Encounters.Expire(app.Game, now);
            Encounters.Refill(app.Game, now);

            var view = app.Put<NestsView>(body, "NestsScreen");
            if (view == null) return;
            view.Bind(app,
                encounter => StealScreen.Enter(app, encounter.Nest),
                () => app.EnterBattle(null, true));
        }
    }
}
