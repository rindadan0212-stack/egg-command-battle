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
            Ui.Label(body, "GoalLabel", "GOAL", 24, Ui.Accent,
                TextAnchor.UpperLeft, Ui.Margin, 32f, 300f, 32f);
            Ui.Label(body, "GoalName", $"{Nests.BossName} を倒す", 40, Ui.Ink,
                TextAnchor.UpperLeft, Ui.Margin, 68f, Ui.W - Ui.Margin * 2f, 56f);

            if (app.Notice.Length > 0)
            {
                Ui.Label(body, "Notice", app.Notice, 26, Ui.InkDim,
                    TextAnchor.UpperLeft, Ui.Margin, 134f, Ui.W - Ui.Margin * 2f, 40f);
            }

            // ── 舞台 ────────────────────────────────────
            float stageTop = 190f;
            float stageHeight = height - stageTop - 200f;

            if (party.Count == 0)
            {
                Ui.Label(body, "Empty", "BOX で3体を「出撃」にすると、ここに並ぶ。", 30, Ui.InkDim,
                    TextAnchor.MiddleCenter, Ui.Margin, stageTop, Ui.W - Ui.Margin * 2f, stageHeight);
            }
            else
            {
                // ⭐ 整数倍だけ。ドット絵は小数倍で拡大するとボケる。
                // ⚠️ 舞台の高さに合わせて選ぶ。決め打ちすると狭い端末で潰れる
                int lead = stageHeight >= 900f ? 30 : stageHeight >= 700f ? 24 : stageHeight >= 520f ? 18 : 13;
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
                Ui.Block(body, "Ground", new Color32(0x1f, 0x24, 0x1c, 0xff),
                    centerX - standWidth / 2f, baseline + 6f, standWidth, 26f);

                // ⭐ 三角配置。手前のリーダーを一番大きく。脇は奥に見えるよう少し上へ
                float sideBaseline = baseline - 96f;
                if (party.Count > 1)
                    Stand(body, party[1], sideSize, "02",
                        centerX - 320f - sideSize / 2f, sideBaseline - sideSize);
                if (party.Count > 2)
                    Stand(body, party[2], sideSize, "03",
                        centerX + 320f - sideSize / 2f, sideBaseline - sideSize);
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
            Ui.Label(body, $"Tag {creature.Id}", Creatures.SpeciesOf(creature).Name, 28, Ui.Ink,
                TextAnchor.LowerCenter, left - 60f, top - 46f, size + 120f, 40f);
            Ui.Label(body, $"Role {creature.Id}", role, 20, Ui.InkFaint,
                TextAnchor.UpperCenter, left - 60f, top - 78f, size + 120f, 30f);
            Ui.PixelOf(body, $"Art {creature.Id}", creature, left, top, size);
        }

        private static void Fact(RectTransform body, string label, string value, float left, float top, float width)
        {
            Ui.Label(body, $"K {label}", label, 22, Ui.InkDim,
                TextAnchor.UpperLeft, left, top, width, 32f);
            Ui.Label(body, $"V {label}", value, 40, Ui.Ink,
                TextAnchor.UpperLeft, left, top + 34f, width, 52f);
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
            float top = 0f;
            if (app.Notice.Length > 0)
            {
                Ui.Block(body, "NoticeBg", Ui.Panel, 0f, 0f, Ui.W, 92f);
                Ui.Label(body, "Notice", app.Notice, 28, Ui.Ink,
                    TextAnchor.MiddleLeft, Ui.Margin, 0f, Ui.W - Ui.Margin * 2f, 92f);
                top = 112f;
            }

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
            var panel = Ui.Block(content, $"Nest {nest.Id}", Ui.Panel, Ui.Margin, top,
                Ui.W - Ui.Margin * 2f, Row);
            float width = Ui.W - Ui.Margin * 2f;

            var species = SpeciesTable.ById(nest.SpeciesId);
            Ui.Pixel(panel, "Art", species.Sprite, species.Palettes[0], 24f, 24f, 96f);

            Ui.Label(panel, "Name", nest.Name, 36, Ui.Ink,
                TextAnchor.UpperLeft, 140f, 22f, width - 300f, 46f);
            Ui.Label(panel, "Meta",
                $"段{nest.Tier} / {species.Name} / 素質 {Nests.WildTotalForTier(nest.Tier)}",
                24, Ui.InkDim, TextAnchor.UpperLeft, 140f, 72f, width - 180f, 36f);

            // ⭐ 奥行きが「速度を積む意味」。ここで必要な飛距離が見える
            double need = Steal.DepthForTier(nest.Tier);
            double have = Steal.DistanceFor(Games.PartyOf(app.Game));
            Ui.Label(panel, "Depth", $"奥行き {need:F0} / 飛距離 {have:F0}",
                24, have >= need ? Ui.Good : Ui.Danger,
                TextAnchor.UpperRight, 140f, 22f, width - 164f, 36f);

            // ⚠️ ここを塗らない。5行すべてを塗ると「主役は1つ」が崩れて、
            //    どれも同じ重さに見える。この画面の主役はゴール（ヌシ）だけ。
            float buttonWidth = (width - 24f * 3f) / 2f;
            Ui.Tappable(panel, "Fight", "親を倒す", () => app.EnterBattle(nest, false),
                24f, Row - 132f, buttonWidth, Ui.Tap);
            Ui.Tappable(panel, "Steal", "盗んで逃げる", () => StealScreen.Enter(app, nest),
                24f + buttonWidth + 24f, Row - 132f, buttonWidth, Ui.Tap);
        }

        private static void BossRow(App app, RectTransform content, float top)
        {
            var panel = Ui.Block(content, "Boss", new Color32(0x2c, 0x1c, 0x1a, 0xff),
                Ui.Margin, top, Ui.W - Ui.Margin * 2f, Row);
            float width = Ui.W - Ui.Margin * 2f;

            var species = SpeciesTable.ById("nushi");
            Ui.Pixel(panel, "Art", species.Sprite, species.Palettes[0], 24f, 24f, 96f);

            Ui.Label(panel, "Name", Nests.BossName, 36, Ui.Ink,
                TextAnchor.UpperLeft, 140f, 22f, width - 180f, 46f);
            // ⚠️ 毎回同じ相手。だから「何が足りないか考えて、配合で作って、挑み直す」が働く
            Ui.Label(panel, "Meta", "毎回同じ相手。鱗に有利を取るのは羽。", 24, Ui.InkDim,
                TextAnchor.UpperLeft, 140f, 72f, width - 180f, 36f);

            // ⭐ この画面で塗るのはここだけ。輪の目的地は1つしかない
            Ui.Tappable(panel, "Fight", "挑む", () => app.EnterBattle(null, true),
                24f, Row - 132f, width - 48f, Ui.Tap, true);
        }
    }
}
