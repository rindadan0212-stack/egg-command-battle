using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>種族1つの中身。⭐ **特性と、技枠の抽選内容**（2026-08-22・作者の指示）。
    ///
    /// ⚠️ **属性はここに出さない。**⭐ 属性は卵ごとに引く（`SpeciesTable.Roll`）ので、
    /// 種族の性質ではない ── 書くと嘘になる。
    ///
    /// ⚠️ **★もここに出さない。**★は卵の側の話。
    ///
    /// ⭐ 技は**長押しで効果**（作者の指示）。⚠️ 押すだけでは何も起きない ──
    /// ここは読む場所で、選ぶ場所ではない。</summary>
    public static class SpeciesPanel
    {
        private const float PanelLeft = 48f;
        private const float PanelTop = 150f;
        private const float PanelWidth = 984f;
        private const float PanelHeight = 1500f;
        private const float Pad = 32f;
        private static float Inner => PanelWidth - Pad * 2f;

        private const float Gap = 12f;
        private const int PerRow = 3;
        /// <summary>技の札。⭐ **長押しの的なので、指で押せる高さを下回らせない。**</summary>
        private const float ChipHeight = 112f;
        private const float HeadHeight = 38f;

        private static GameObject _open;

        public static void Close()
        {
            if (_open == null) return;
            // ⚠️ Destroy はフレームの終わりまで効かない。残すと覆いが指を吸う
            _open.SetActive(false);
            _open.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(_open);
            _open = null;
        }

        public static void Show(App app, Species species)
        {
            Close();
            if (species == null) return;

            var root = Ui.Rect("SpeciesPanel", app.Overlay);
            Ui.Stretch(root);
            _open = root.gameObject;

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.58f);
            var block = root.gameObject.AddComponent<Button>();
            block.transition = Selectable.Transition.None;
            block.targetGraphic = dim;
            block.onClick.AddListener(Close);

            var panel = Ui.Card(root, "Panel", PanelLeft, PanelTop, PanelWidth, PanelHeight);

            Ui.Pixel(panel, "Art", species.Sprite, species.Palettes[0], Pad, Pad, 128f);
            Ui.Label(panel, "Name", species.Name, 46, Ui.Ink, TextAnchor.UpperLeft,
                Pad + 152f, Pad + 8f, Inner - 152f, 60f);
            // ⭐ **読む場所だと最初に言う。**⚠️ 長押しは見えない操作なので、
            //    書かないと誰も試さない
            Ui.Label(panel, "Hint", "技を長押しすると効果が出ます", 24, Ui.InkFaint,
                TextAnchor.UpperLeft, Pad + 152f, Pad + 72f, Inner - 152f, 34f);

            float top = Pad + 152f;
            var body = Ui.Scroller(panel, "Body", Pad, top, Inner,
                PanelHeight - top - Ui.Tap - Pad * 2f, Need(species));

            float y = 0f;
            y += Trait(panel: body, species: species, top: y);
            y += Gap * 2f;
            // ⚠️ 枠1 に「1種」と付けない ── ⭐ **抽選ではない**（必ずこれ）ので、
            //    数を出すと引くものに見える
            // ⚠️ 枠 0 を渡す ── ⭐ **枠1 の CT は常に 0**。渡さないと詳細だけ
            //    技の表の数を出して、戦闘で見る数と食い違う。
            y += Slot(app, body, "枠1（この種族が必ず持つ）", new[] { species.Skill1 }, y,
                count: false, slot: 0);
            y += Gap * 2f;
            y += Slot(app, body, "枠2の抽選", species.Slot2.Pool, y);
            y += Gap * 2f;
            Slot(app, body, "枠3の抽選", species.Slot3.Pool, y);

            Ui.Tappable(panel, "Close", "閉じる", Close,
                Pad, PanelHeight - Ui.Tap - Pad, Inner, Ui.Tap);
        }

        /// <summary>⚠️ **中身を組む前に高さを数える。**⭐ 巻物は中身の高さを先に要る。</summary>
        private static float Need(Species species)
        {
            float need = TraitHeight;
            need += Gap * 2f + Deep(1);
            need += Gap * 2f + Deep(species.Slot2.Pool.Count);
            need += Gap * 2f + Deep(species.Slot3.Pool.Count);
            return need;
        }

        private const float TraitHeight = HeadHeight + 44f + 62f;

        private static float Deep(int count)
        {
            int rows = Mathf.CeilToInt(count / (float)PerRow);
            return HeadHeight + rows * (ChipHeight + Gap);
        }

        /// <summary>特性。⭐ **いつ効くか**を名前の隣に置く ── 「常時」と
        /// 「倒れる一撃を受けたとき」では、編成に入れる理由がまるで違う。</summary>
        private static float Trait(RectTransform panel, Species species, float top)
        {
            var trait = Traits.Has(species.TraitId) ? Traits.ById(species.TraitId) : null;
            Ui.Label(panel, "TraitHead", "特性", 26, Ui.InkFaint, TextAnchor.MiddleLeft,
                0f, top, Inner, HeadHeight);
            if (trait == null)
            {
                // ⚠️ **黙って空にしない。**⭐ 特性は全種族が持つ決まりなので、
                //    無いなら仕様の穴（`Species.Faults` が落とすはずのもの）
                Ui.Label(panel, "TraitName", "（特性が繋がっていない）", 30, Ui.DangerInk,
                    TextAnchor.MiddleLeft, 0f, top + HeadHeight, Inner, 44f);
                return TraitHeight;
            }
            Ui.Label(panel, "TraitName", $"{trait.Name}　― {Traits.LabelOf(trait.When)}", 32,
                Ui.AccentInk, TextAnchor.MiddleLeft, 0f, top + HeadHeight, Inner, 44f);
            Ui.Label(panel, "TraitGist", trait.Gist, 26, Ui.InkDim,
                TextAnchor.UpperLeft, 0f, top + HeadHeight + 46f, Inner, 62f);
            return TraitHeight;
        }

        /// <summary>技の袋を1つ並べる。⭐ 押しても何も起きず、**長押しで効果**。</summary>
        /// <param name="count">見出しに「N種」を付けるか。⚠️ 抽選でない枠には付けない。</param>
        /// <param name="slot">その袋が入る枠。⚠️ **枠1（0）だけ CT が 0 になる。**
        /// ⭐ 枠2・枠3 は技の表のまま（-1 と同じ）。</param>
        private static float Slot(App app, RectTransform panel, string head,
            System.Collections.Generic.IReadOnlyList<string> pool, float top,
            bool count = true, int slot = -1)
        {
            Ui.Label(panel, head + "Head", count ? $"{head}　{pool.Count}種" : head,
                26, Ui.InkFaint, TextAnchor.MiddleLeft, 0f, top, Inner, HeadHeight);

            float width = (Inner - Gap * (PerRow - 1)) / PerRow;
            for (int i = 0; i < pool.Count; i++)
            {
                var skill = Skills.Has(pool[i]) ? Skills.ById(pool[i]) : null;
                float left = (i % PerRow) * (width + Gap);
                float y = top + HeadHeight + (i / PerRow) * (ChipHeight + Gap);
                var chip = Ui.Card(panel, head + i, left, y, width, ChipHeight);

                if (skill == null)
                {
                    // ⚠️ **知らない id を黙って飛ばさない。**⭐ 袋に綴り違いが入ったら
                    //    「その技は一生出ない」なので、目に見える形で落とす
                    Ui.Label(chip, "Name", pool[i], 22, Ui.DangerInk,
                        TextAnchor.MiddleCenter, 6f, 0f, width - 12f, ChipHeight);
                    continue;
                }

                Ui.Label(chip, "Name", skill.Name, 26, Ui.Ink, TextAnchor.MiddleCenter,
                    6f, 12f, width - 12f, 44f);
                Ui.Label(chip, "Kind", Skills.LabelOf(skill.Type), 22, Ui.InkFaint,
                    TextAnchor.MiddleCenter, 6f, 58f, width - 12f, 34f);

                var hold = chip.gameObject.AddComponent<LongPress>();
                var chosen = skill;
                hold.OnTap = null;
                // ⭐ Lv は 1 で出す。⚠️ 個体が居ないので「育てたあと」の数は言えない
                int which = slot;
                hold.OnHold = () => SkillInfoPanel.Show(app, chosen, 1, which);
            }
            return Deep(pool.Count);
        }
    }
}
