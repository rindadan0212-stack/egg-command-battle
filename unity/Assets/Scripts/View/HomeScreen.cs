using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>ホーム。⭐ 輪のハブ。
    ///
    /// 編成3体を三角に置き、リーダーを手前に大きく。
    /// ⭐ 編成が画面の主役。強奪の飛距離も戦闘も編成で決まるのに、
    /// BOX の「出撃中」でしか見えないと、選んだ結果が実感できない。
    /// </summary>
    public static class HomeScreen
    {
        public static void Build(App app, RectTransform body, float height)
        {
            var party = Games.PartyOf(app.Game);

            // ⭐ 輪の目的地をここに置く。企画の駆動力は「この壁を越えるには何が要るか」なので、
            //    ホームで常に壁の名前が見えているのが素直。
            Ui.Knockout(Ui.Label(body, "GoalLabel", "GOAL", 24, Ui.Accent,
                TextAnchor.UpperLeft, Ui.Margin, 32f, 300f, 32f), 3);
            Ui.Knockout(Ui.Label(body, "GoalName", $"{Nests.BossName} を倒す", 42, Ui.Ink,
                TextAnchor.UpperLeft, Ui.Margin, 68f, Ui.W - Ui.Margin * 2f, 60f));

            // ⚠️ 「勝った」「卵を手に入れた」といった事後報告を置かない。
            //    孵化の数が増えていることが、そのまま報告になっている。

            // ── 舞台 ────────────────────────────────────
            float stageTop = 190f;
            float stageHeight = height - stageTop - 200f;

            if (party.Count == 0)
            {
                // ⚠️ 遊び方を字で書かない。⭐ 空の台座を3つ置いて「ここに入る」を見せる
                float slot = 132f;
                for (int i = 0; i < Games.PartySize; i++)
                {
                    Ui.Block(body, $"Empty {i}", new Color32(0x24, 0x28, 0x22, 0xff),
                        Ui.W / 2f - slot * 1.6f + i * slot * 1.2f,
                        stageTop + stageHeight * 0.5f, slot, 26f);
                }
            }
            else
            {
                // ⭐ 整数倍だけ。ドット絵は小数倍で拡大するとボケる。
                // ⚠️ 高さだけで選ぶと**横が足りず3体が重なる**（実際に重なった）。
                //    3体が並ぶのに要る幅から上限を出して、そこで頭打ちにする。
                //    要る幅 = lead*16 + 2*(lead-10)*16 - 重ね幅*2  ≦ 画面幅 - 余白
                int byHeight = stageHeight >= 900f ? 30 : stageHeight >= 700f ? 24 : stageHeight >= 520f ? 18 : 13;
                // ⚠️ 重ねない。平面のドット絵は重ねても奥行きに見えず、ただ潰れて見える
                const float Gap = 24f;
                float usable = Ui.W - Ui.Margin * 2f;
                // 3体ぶん = lead*16 + 2*(lead-10)*16 + 2*Gap ≦ usable
                int byWidth = Mathf.FloorToInt((((usable - Gap * 2f) / 16f) + 20f) / 3f);
                int lead = Mathf.Clamp(Mathf.Min(byHeight, byWidth), 8, 30);
                int side = Mathf.Max(6, lead - 10);

                float leadSize = lead * 16f;
                float sideSize = side * 16f;
                float centerX = Ui.W / 2f;

                // 役割 30 + 名札 40 + 絵 が1体ぶんの高さ
                const float Plate = 78f;
                float blockHeight = Plate + leadSize + 40f;
                // ⚠️ 下に寄せると上が間延びする。中身の高さを測って舞台の中で釣り合わせる
                float groupTop = stageTop + Mathf.Max(0f, (stageHeight - blockHeight) * 0.52f);
                float baseline = groupTop + Plate + leadSize;

                // 台座。⭐ 足元だけを一段明るくする（線を引かず面で示す）
                float standWidth = leadSize + 300f;
                Ui.Block(body, "Ground", new Color32(0xf2, 0xb3, 0x4b, 0xff),
                    centerX - standWidth / 2f, baseline + 6f, standWidth, 26f);

                // ⭐ 三角配置。手前のリーダーを一番大きく。脇は奥に見えるよう少し上へ。
                // ⚠️ 離す幅は絵の大きさから出す。決め打ちにすると倍率が変わった日に重なる
                float apart = leadSize / 2f + sideSize / 2f + Gap;
                float sideBaseline = baseline - 96f;
                if (party.Count > 1)
                    Stand(body, party[1], sideSize, "02",
                        centerX - apart - sideSize / 2f, sideBaseline - sideSize);
                if (party.Count > 2)
                    Stand(body, party[2], sideSize, "03",
                        centerX + apart - sideSize / 2f, sideBaseline - sideSize);
                Stand(body, party[0], leadSize, "LEADER", centerX - leadSize / 2f, baseline - leadSize);
            }

            // ── 数えられる事実 ──────────────────────────
            // ⭐ 編成の総スピードは飛距離そのもの。ホームで見えることに意味がある
            int speed = 0;
            foreach (var creature in party) speed += Creatures.StatsOf(creature).Spd;

            float factTop = height - 176f;
            float factWidth = (Ui.W - Ui.Margin * 2f) / 3f;
            Fact(body, "編成", $"{party.Count}/{Games.PartySize}", Ui.Margin, factTop, factWidth);
            Fact(body, "スピード合計", speed.ToString(), Ui.Margin + factWidth, factTop, factWidth);
            Fact(body, "飛距離", party.Count > 0 ? Steal.DistanceFor(party).ToString("F0") : "—",
                Ui.Margin + factWidth * 2f, factTop, factWidth);
        }

        private static void Stand(RectTransform body, Creature creature, float size, string role,
            float left, float top)
        {
            // ⚠️ 役割は名札の側へ寄せる。絵の下に置くと台座の帯と重なって読めなくなった
            Ui.Knockout(Ui.Label(body, $"Tag {creature.Id}", Creatures.SpeciesOf(creature).Name, 30, Ui.Ink,
                TextAnchor.LowerCenter, left - 60f, top - 46f, size + 120f, 40f));
            Ui.Knockout(Ui.Label(body, $"Role {creature.Id}", role, 20, Ui.InkFaint,
                TextAnchor.UpperCenter, left - 60f, top - 78f, size + 120f, 30f), 3);
            Ui.PixelOf(body, $"Art {creature.Id}", creature, left, top, size);
        }

        private static void Fact(RectTransform body, string label, string value, float left, float top, float width)
        {
            Ui.Knockout(Ui.Label(body, $"K {label}", label, 22, Ui.InkDim,
                TextAnchor.UpperLeft, left, top, width, 32f), 3);
            Ui.Knockout(Ui.Label(body, $"V {label}", value, 42, Ui.Ink,
                TextAnchor.UpperLeft, left, top + 34f, width, 54f));
        }
    }

    /// <summary>巣の一覧。⭐ 巣ごとに二択（倒す / 盗む）。
    ///
    /// | 親を倒す | 確実に奪える。良い卵。ただし勝てる相手に限る |
    /// | 盗んで逃げる | 格上の巣でも狙えるが、失敗すると戦闘になる |
    ///
    /// これで「まだ勝てない巣に挑む」動機が生まれ、輪の駆動力になる。
    /// </summary>
    public static class NestsScreen
    {
        // ⚠️ 232 だとメタ行の下端（108）とボタンの上端（104）が重なる。測って広げた
        private const float Row = 264f;

        public static void Build(App app, RectTransform body, float height)
        {
            // ⚠️ 直前に何が起きたかを字で流さない
            const float top = 0f;

            float contentHeight = (Nests.All.Length + 1) * (Row + 16f) + 32f;
            var content = Ui.Scroller(body, "Nests", 0f, top, Ui.W, height - top, contentHeight);

            float y = 8f;
            foreach (var nest in Nests.All)
            {
                NestRow(app, content, nest, y);
                y += Row + 16f;
            }
            BossRow(app, content, y);
        }

        private static void NestRow(App app, RectTransform content, Nest nest, float top)
        {
            var panel = Ui.Card(content, $"Nest {nest.Id}", Ui.Margin, top, Ui.W - Ui.Margin * 2f, Row);
            float width = Ui.W - Ui.Margin * 2f;

            var species = SpeciesTable.ById(nest.SpeciesId);
            Ui.Pixel(panel, "Art", species.Sprite, species.Palettes[0], 24f, 24f, 96f);

            Ui.Label(panel, "Name", nest.Name, 36, Ui.Ink,
                TextAnchor.UpperLeft, 140f, 22f, width - 300f, 46f);
            // 名前と数だけ。⚠️ 説明の語（「〜なので」「〜すると」）を混ぜない
            Ui.Label(panel, "Meta", $"{species.Name}  {Nests.WildTotalForTier(nest.Tier)}",
                24, Ui.InkDim, TextAnchor.UpperLeft, 140f, 74f, 300f, 36f);
            ElementMark.Put(panel, species.Element, 140f, 116f);

            // ⭐ 届くかどうかを字で言わない。帯の伸び方と色で見せる。
            //    ⚠️ 「奥行き 290 / 飛距離 204」と書いても、引き算はプレイヤーの仕事になる
            double need = Steal.DepthForTier(nest.Tier);
            double have = Steal.DistanceFor(Games.PartyOf(app.Game));
            float reach = need <= 0 ? 1f : Mathf.Clamp01((float)(have / need));
            Ui.Bar(panel, "Reach", reach, reach >= 1f ? Ui.Good : Ui.Danger,
                width - 320f, 34f, 280f, 18f);

            // ⭐ 二択は置かない。**引っ張って卵に届けば盗み、届かなければ戦闘**。
            // ⚠️ 以前は「親を倒す / 盗んで逃げる」を選ばせていたが、
            //    どちらを選ぶかを先に決めさせると、飛ばした結果で決まるという芯が消える。
            //    選ぶのは「どの巣へ行くか」と「どう飛ばすか」だけでよい。
            // ⚠️ ここを塗らない。5行すべてを塗ると「主役は1つ」が崩れる
            Ui.Tappable(panel, "Go", "卵をねらう", () => StealScreen.Enter(app, nest),
                24f, Row - 132f, width - 48f, Ui.Tap);
        }

        private static void BossRow(App app, RectTransform content, float top)
        {
            // ⭐ ゴールだけ明るい札にする（この画面の主役）
            var panel = Ui.Card(content, "Boss", Ui.Margin, top, Ui.W - Ui.Margin * 2f, Row, true);
            float width = Ui.W - Ui.Margin * 2f;

            var species = SpeciesTable.ById("nushi");
            Ui.Pixel(panel, "Art", species.Sprite, species.Palettes[0], 24f, 24f, 96f);

            Ui.Label(panel, "Name", Nests.BossName, 36, Ui.Ink,
                TextAnchor.UpperLeft, 140f, 22f, width - 180f, 46f);
            // ⚠️ 「鱗に有利を取るのは羽」と書かない。⭐ 属性の印を出せば、
            //    同じ色の並びが戦闘でも出るので、負けた経験のほうが早く教える
            ElementMark.Put(panel, species.Element, 140f, 78f);

            // ⭐ この画面で塗るのはここだけ。輪の目的地は1つしかない
            Ui.Tappable(panel, "Fight", "挑む", () => app.EnterBattle(null, true),
                24f, Row - 132f, width - 48f, Ui.Tap, true);
        }
    }
}
