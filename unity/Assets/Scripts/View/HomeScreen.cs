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

            // ⭐ **パーティ編成の入口。**放置の編成をここで決める。
            // ⚠️ BOX から「出撃」を消したので、ここが唯一の入口になる。
            Ui.Tappable(body, "Party", "パーティ編成",
                () => PartyPanel.Show(app, PartyKind.Idle),
                // ⚠️ **下の帯（探索・配合・BOX）より上位に見せない。**
                //    全幅の札にしていた頃は、ナビより大きくて階層が逆転していた
                //    （レビュー指摘 2026-08-19）。⭐ 幅を半分にして右へ寄せる。
                // ⚠️ 縦は孵化枠の下（616+780=1396〜1556）が空いている。
                Ui.W / 2f, 1420f, Ui.W / 2f - Ui.Margin, Ui.Tap);

            // ⭐ **試練の入口。**⚠️ 下の帯（探索・配合・BOX）には足さない ──
            //    帯は4つで埋まっていて、5つ目を足すと1つあたりが指で押しにくくなる。
            // ⚠️ パーティ編成と同じ幅・同じ列に置く（並びが揃っていないと、
            //    どちらが上位の入口か読めない）。⭐ 1段上に重ねる。
            // ⚠️ **置ける行は 1420 の1行しか無い。**⭐ 実測（`画面を全部検査する`）:
            //    素材 0〜78 / 放置 96〜592 / 孵化枠 616〜1396 / **1420〜1532** /
            //    帯 1556〜1788。⚠️ **本体は 1920 ではなく 1788**（帯が下 232 を覆う）。
            //    上へ重ねたら孵化枠と3件、下へ置いたら帯と6件重なった。
            // ⭐ そこで「パーティ編成」と**同じ行の左半分**に置き、
            //    世界の番号は素材の行の右側（空いている）へ移した。
            Ui.Tappable(body, "Trial",
                $"試練　{Games.TrialsCleared(app.Game)}/{Trials.All.Count}",
                () => app.Show(Screen.Trial),
                Ui.Margin, 1420f, Ui.W / 2f - Ui.Margin * 2f, Ui.Tap);

            // ⭐ **この世界の番号。**⚠️ 端末ごとに違う世界を引くようにした（2026-08-21）ので、
            //    「この盤面で起きた」と伝える手立てがこれしか無い。
            // ⚠️ **説明の文は付けない。**数だけ置く ── 要る人だけが読む。
            //    ⭐ 16進で出すと8桁に収まり、口で伝えられる長さになる。
            //    ⚠️ 置き場所は素材の行の右側（素材は左 48〜504 しか使っていない）。
            Ui.Label(body, "World", "#" + app.WorldSeed.ToString("X8"), 24,
                new Color(1f, 1f, 1f, 0.34f), TextAnchor.MiddleRight,
                Ui.W / 2f, 0f, Ui.W / 2f - Ui.Margin, 78f);

            // ⭐ **図鑑の入口は右肩**（2026-08-22・作者の指示「右上の方にボタンを設置」）。
            // ⚠️ 本体には置けない ── 実測で空いているのは 1420 の1行だけで、
            //    そこは試練とパーティ編成で埋まっている。
            // ⭐ 右肩は `ShowExtra` が既に「本体に置けないものを出す口」として在る
            //    （探索が同じ理由で使っている）。⚠️ ホームでは空だったので取り合わない。
            app.ShowExtra("図鑑", () => app.Show(Screen.Book));

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

            // ⭐ **巣を選ぶ前に編成を決める。**⚠️ 潜ってから「違った」と気づいても戻れない。
            //    ⚠️ 画面の中には置けない ── 巣の札4枚が 24〜1604 を埋めていて、
            //    どこに置いても重なる（実測）。⭐ 画面の外（上の帯）へ出す。
            app.ShowExtra("パーティ編成", () => PartyPanel.Show(app, PartyKind.Nest));
            view.Bind(app,
                // ⭐ 潜入は**すごろく**（2026-08-20）。⚠️ 弾いて飛ばす StealScreen は残してあるが繋がない
                encounter => TrailScreen.Enter(app, encounter.Nest),
                () => app.EnterBattle(null, true));
        }
    }
}
