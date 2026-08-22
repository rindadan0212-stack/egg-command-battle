using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>試練。⭐ **手で組んだ敵編成と戦うだけの場所**（2026-08-21・作者の指示）。
    ///
    /// ⚠️ 巣と違って**卵は出ない。**出すと「試練で卵を稼ぐ」が最短経路になり、
    /// 潜入も配合も回らなくなる。⭐ 返ってくるのは勝った印だけ。
    ///
    /// ⭐ **中身を先に見せる。**巣は「絵とレベルだけ」を見せて中身を隠すが、
    /// 試練は逆 ── 何が来るか分かったうえで**組み直して挑む**場所なので、
    /// 顔ぶれも噛み合わせの一言も出す。⚠️ 隠すと「何が足りなかったか」を考えられない。
    ///
    /// ⚠️ 器は Prefab にしていない（段の数が変わるので、置き場所を固定しても
    /// 中身は結局コードが作ることになる）。</summary>
    public static class TrialScreen
    {
        private const float CardTop = 24f;
        private const float CardH = 300f;
        private const float Gap = 16f;
        private const float Pad = 24f;

        public static void Build(App app, RectTransform body)
        {
            var trials = Trials.All;
            float inner = Ui.W - Ui.Margin * 2f;

            // ⭐ **進み具合を1行で。**⚠️ 「あと何段」と書かない（数えれば分かる）
            Ui.Label(body, "Note",
                $"勝った試練　{Games.TrialsCleared(app.Game)} / {trials.Count}",
                28, Ui.InkDim, TextAnchor.UpperLeft,
                Ui.Margin, CardTop, inner, 40f);

            float top = CardTop + 52f;
            var scroll = Ui.Scroller(body, "Trials", Ui.Margin, top, inner,
                Ui.H - top - Ui.DockHeight - Ui.Margin,
                trials.Count * (CardH + Gap));

            for (int i = 0; i < trials.Count; i++)
            {
                Card(app, scroll, trials[i], i * (CardH + Gap), inner);
            }
        }

        private static void Card(App app, RectTransform parent, Trial trial, float top, float width)
        {
            var card = Ui.Card(parent, $"Trial {trial.Id}", 0f, top, width, CardH);
            bool beaten = Games.BeatTrial(app.Game, trial.Id);

            // ⭐ **段の番号を大きく。**⚠️ 名前より先に「何段目か」が読めるようにする
            Ui.Label(card, "Step", Trials.StepOf(trial.Id).ToString(), 64,
                beaten ? Ui.GoodInk : Ui.Ink, TextAnchor.UpperLeft, Pad, Pad - 6f, 90f, 80f);
            Ui.Label(card, "Name", trial.Name, 40, Ui.Ink,
                TextAnchor.UpperLeft, Pad + 96f, Pad, width - Pad * 2f - 96f - 160f, 56f);
            // ⭐ **何が来るかを1行で。**⚠️ 数は出さない（出すと勝てる段だけ選ぶ遊びになる）
            Ui.Label(card, "Gist", trial.Gist, 26, Ui.InkDim,
                TextAnchor.UpperLeft, Pad + 96f, Pad + 58f, width - Pad * 2f - 96f, 40f);

            // ⭐ 勝った印。⚠️ 字で出す（丸だけだと何の印か読めない）
            if (beaten)
            {
                Ui.Label(card, "Beaten", "勝った", 28, Ui.GoodInk,
                    TextAnchor.UpperRight, width - Pad - 160f, Pad + 6f, 160f, 44f);
            }

            // ⭐ **顔ぶれをそのまま出す。**組み直す材料になるので隠さない
            var party = Trials.PartyOf(trial);
            const float Face = 116f;
            for (int i = 0; i < party.Count; i++)
            {
                var one = party[i];
                float left = Pad + i * (Face + 12f);
                // ⚠️ **敵なので反転する**（画面の作法「敵は左右反転で描く」）。
                //    ⭐ ここだけ素の向きだと、同じ相手が戦闘では逆を向くことになる
                var face = Ui.PixelOf(card, $"Face {i}", one, left, 118f, Face);
                Ui.Face(face.rectTransform, true);
                // ⚠️ 属性は絵だけでは読めない。⭐ 一文字で添える
                Ui.Label(card, $"Elem {i}", SpeciesTable.LabelOf(one.Element), 24,
                    ElementMark.ColorOf(one.Element), TextAnchor.UpperCenter,
                    left, 118f + Face, Face, 32f);
            }

            Ui.Tappable(card, "Go", beaten ? "もう一度" : "挑む",
                () => app.EnterTrial(trial),
                width - Pad - 280f, CardH - Ui.Tap - Pad, 280f, Ui.Tap,
                lead: !beaten);
        }
    }
}
