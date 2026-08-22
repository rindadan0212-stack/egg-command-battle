using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>孵さない卵で技を鍛える画面。⭐ **卵の唯一の出口**。
    ///
    /// ⭐ ★＝強さ を成立させている支え。★5 は「2時間待って強い個体」と
    /// 「いま技を1段上げる」の二択になり、どちらも正解でありうる。
    ///
    /// ⭐ **一度に <see cref="MaxPick"/> 個まで選んでから、最後に「強化する」を押す**
    /// （2026-08-21・作者の指示）。⚠️ 直す前は**押した瞬間に入っていた**ので、
    /// 10個入れるには10回押すことになり、そのたびに裏でレベルが上がっていた。
    ///
    /// ⚠️ 入れた卵は戻らない。⭐ だから「選ぶ」と「入れる」を分ける ──
    /// 取り返しがつかないのは変えないが、**押し間違いで起きない**ようにする。
    ///
    /// ⚠️ 器はここでは Prefab にしていない。並ぶ卵の数が変わるので、
    /// 置き場所を固定した Prefab にしても中身は結局コードが作ることになる。
    /// </summary>
    public static class SkillEggPanel
    {
        /// <summary>一度に選べる卵の数。
        /// ⚠️ **分解と「揃える」のではなく、同じ定数を見る**（Core が唯一の出所）。</summary>
        public const int MaxPick = Games.PickAtOnce;

        private const float PanelLeft = 48f;
        private const float PanelTop = 200f;
        private const float PanelWidth = 984f;
        private const float PanelHeight = 1520f;
        private const float Pad = 24f;
        private const float Inner = PanelWidth - Pad * 2f;

        private const float RowTop = 148f;
        private const float RowStep = 124f;

        private const float EggCellW = 228f;
        private const float EggCellH = 168f;
        private const int EggPerRow = 4;

        private static GameObject _open;
        /// <summary>いま注ぐ先の枠。⚠️ 画面を開き直しても覚えておく（続けて入れるため）。</summary>
        private static int _slot;

        /// <summary>いま選んでいる卵。⭐ **押した順に入る**（入る順がそのまま並び順）。</summary>
        private static readonly System.Collections.Generic.List<string> Picked =
            new System.Collections.Generic.List<string>();

        /// <summary>開く。⭐ 最初に鍛えられる枠を選んでおく。</summary>
        public static void Show(App app, string creatureId)
        {
            Picked.Clear();
            _slot = FirstOpen(app, creatureId);
            Rebuild(app, creatureId);
        }

        /// <summary>中身を描き直す。⚠️ <see cref="Show"/> と分ける ──
        /// 選んだ枠を覚えたまま組み直したいので、ここでは <c>_slot</c> に触らない
        /// （まとめると、卵を1個入れるたびに枠が先頭へ戻る）。</summary>
        private static void Rebuild(App app, string creatureId)
        {
            Close();
            Build(app, creatureId);
        }

        public static void Close()
        {
            if (_open == null) return;
            // ⚠️ Destroy はフレームの終わりまで効かない。残すと覆いが指を吸う
            _open.SetActive(false);
            _open.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(_open);
            _open = null;
        }

        private static void Build(App app, string creatureId)
        {
            var creature = Find(app, creatureId);
            if (creature == null) return;

            var root = Ui.Rect("SkillEggPanel", app.Overlay);
            Ui.Stretch(root);
            _open = root.gameObject;

            // ⭐ 地を暗くして、後ろの画面を押せないようにする。⚠️ ここを押したら閉じる
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimButton = root.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.onClick.AddListener(Close);

            var panel = Ui.Card(root, "Panel", PanelLeft, PanelTop, PanelWidth, PanelHeight);

            Ui.Label(panel, "Title", "技を鍛える", 40, Ui.Ink, TextAnchor.UpperLeft,
                Pad, Pad, Inner, 56f);
            Ui.Label(panel, "Who", Creatures.SpeciesOf(creature).Name, 26, Ui.InkDim,
                TextAnchor.UpperLeft, Pad, 86f, Inner, 40f);

            BuildSlots(app, panel, creature, creatureId);
            BuildEggs(app, panel, creature, creatureId);

            Ui.Tappable(panel, "Close", "閉じる", Close,
                Pad, PanelHeight - Ui.Tap - Pad, Inner, Ui.Tap);
        }

        /// <summary>注ぐ先の3枠。⭐ **いまの進み具合を数で出す。**
        /// ⚠️ 「あと少し」と書かない。要る数を出せば足りる。</summary>
        private static void BuildSlots(App app, RectTransform panel, Creature creature,
            string creatureId)
        {
            var skills = Creatures.SkillsOf(creature);
            for (int i = 0; i < skills.Length; i++)
            {
                int slot = i;
                var skill = skills[i];
                float top = RowTop + RowStep * i;
                bool usable = skill != null && !SkillCosts.IsMaxed(creature.SkillPoints[i]);
                bool picked = slot == _slot;

                Ui.Tappable(panel, $"Slot {i}", "",
                    // ⚠️ **枠を変えたら選び直し**（FusePanel と同じ理由）
                    usable
                        ? new Action(() => { _slot = slot; Picked.Clear(); Rebuild(app, creatureId); })
                        : null,
                    Pad, top, Inner, Ui.Tap, lead: picked && usable, enabled: usable);

                var row = (RectTransform)panel.Find($"Slot {i}");
                var name = row.Find("Label");
                if (name != null) UnityEngine.Object.Destroy(name.gameObject);

                var ink = !usable ? Ui.InkFaint : picked ? Ui.OnLead : Ui.Ink;
                Ui.Label(row, "Name", skill == null ? "—" : skill.Name, 30, ink,
                    TextAnchor.MiddleLeft, 24f, 0f, 420f, Ui.Tap);

                int points = creature.SkillPoints[i];
                Ui.Label(row, "Lv", $"Lv{SkillCosts.LevelOf(points)}", 30, ink,
                    TextAnchor.MiddleCenter, 460f, 0f, 160f, Ui.Tap);

                // ⭐ あと何ポイントで次かを出す。⚠️ 上限は「上限」と書く（0 と出さない）
                string need = skill == null ? ""
                    : SkillCosts.IsMaxed(points) ? "上限"
                    : $"あと {SkillCosts.ToNext(points)}";
                Ui.Label(row, "Need", need, 26, ink, TextAnchor.MiddleRight,
                    Inner - 24f - 260f, 0f, 260f, Ui.Tap);
            }
        }

        /// <summary>棚の卵。⭐ **押すのは「選ぶ／外す」だけ。**入るのは「強化する」を押したとき。
        ///
        /// ⚠️ 上限を超える卵は選ばせない。受け取ると超えた分が黙って消える
        /// （2時間待った★5が蒸発する）。⭐ 入らないことは灰色で示す。</summary>
        private static void BuildEggs(App app, RectTransform panel, Creature creature,
            string creatureId)
        {
            var eggs = app.Game.Eggs;
            var skills = Creatures.SkillsOf(creature);
            bool slotUsable = _slot >= 0 && _slot < skills.Length && skills[_slot] != null
                && !SkillCosts.IsMaxed(creature.SkillPoints[_slot]);
            int points = slotUsable ? creature.SkillPoints[_slot] : 0;
            int room = slotUsable
                ? SkillCosts.TotalFor(Skills.MaxLevel) - creature.SkillPoints[_slot]
                : 0;

            int gain = 0;
            foreach (string id in Picked)
            {
                var one = EggById(app, id);
                if (one != null) gain += Rarities.PointsOf(one.Rarity);
            }

            float listTop = RowTop + RowStep * 3f + 12f;
            // ⭐ **入れたあとのレベルまで出す。**⚠️ ポイントだけだと人が計算することになる
            string say = !slotUsable ? "この枠はもう鍛えられません"
                : gain > 0
                    ? $"選んだ {Picked.Count}/{MaxPick} 個で ＋{gain}　"
                        + $"Lv{SkillCosts.LevelOf(points)} → Lv{SkillCosts.LevelOf(points + gain)}"
                    : $"棚の卵 {eggs.Count}　（{MaxPick} 個まで選べます）";
            Ui.Label(panel, "EggsTitle", say, 26, gain > 0 ? Ui.Ink : Ui.InkDim,
                TextAnchor.UpperLeft, Pad, listTop, Inner, 36f);

            float areaTop = listTop + 48f;
            float areaHeight = PanelHeight - Ui.Tap * 2f - Pad * 2f - 12f - areaTop;
            int rows = (eggs.Count + EggPerRow - 1) / EggPerRow;
            var content = Ui.Scroller(panel, "Eggs", Pad, areaTop, Inner, areaHeight,
                rows * EggCellH);

            for (int i = 0; i < eggs.Count; i++)
            {
                var egg = eggs[i];
                string eggId = egg.Id;
                int worth = Rarities.PointsOf(egg.Rarity);
                bool picked = Picked.Contains(eggId);
                bool fits = picked
                    || (slotUsable && Picked.Count < MaxPick && worth <= room - gain);
                float left = (i % EggPerRow) * EggCellW;
                float top = (i / EggPerRow) * EggCellH;

                // ⭐ **どの画面でも同じ卵の升**（絵・★・一言）
                var box = Ui.EggCell(content, $"Egg {i}", egg, "＋" + worth, Ui.Ink,
                    left + 6f, top + 6f, EggCellW - 12f, EggCellH - 12f,
                    dim: !fits, picked: picked);
                var tap = box.gameObject.AddComponent<Button>();
                tap.targetGraphic = box.GetComponent<Image>();
                tap.interactable = fits;
                if (!fits) continue;
                tap.onClick.AddListener(() =>
                {
                    if (!Picked.Remove(eggId)) Picked.Add(eggId);
                    Rebuild(app, creatureId);
                });
            }

            bool ready = Picked.Count > 0;
            var go = Ui.Tappable(panel, "Feed",
                ready ? $"強化する（＋{gain}）" : "強化する",
                () => Feed(app, creatureId),
                Pad, PanelHeight - Ui.Tap * 2f - Pad - 12f, Inner, Ui.Tap);
            go.interactable = ready;
            var face = go.GetComponent<Image>();
            if (face != null) face.sprite = Ui.SkinSprite(ready ? "button-lead" : "button-off");
        }

        /// <summary>選んだぶんをまとめて注ぐ。
        /// ⭐ **入れる順も削除も Core が1回で持つ**（<see cref="Games.FeedEggsToSkill"/>）。</summary>
        private static void Feed(App app, string creatureId)
        {
            int count = Picked.Count;
            int total = Games.FeedEggsToSkill(app.Game, creatureId, _slot,
                new System.Collections.Generic.List<string>(Picked));
            Picked.Clear();
            if (total > 0)
            {
                BannerView.Show(app.Overlay, $"たまご{count}個で技が鍛わった  ＋{total}", null);
            }
            app.Refresh();                  // 後ろの BOX も新しいレベルにする
            Rebuild(app, creatureId);       // ⭐ 続けて選べるよう開いたまま
        }

        private static Egg EggById(App app, string id)
        {
            foreach (var egg in app.Game.Eggs) if (egg.Id == id) return egg;
            return null;
        }

        /// <summary>保管庫から引く。⚠️ <see cref="Games.CreatureById"/> は
        /// 居ないと投げるので使わない（逃がした直後でも画面が落ちないように）。</summary>
        private static Creature Find(App app, string id)
        {
            foreach (var creature in app.Game.Storage.Creatures)
            {
                if (creature.Id == id) return creature;
            }
            return null;
        }

        /// <summary>最初に鍛えられる枠。⚠️ どれも無理なら 0（画面は開くが全部灰色）。</summary>
        private static int FirstOpen(App app, string creatureId)
        {
            var creature = Find(app, creatureId);
            if (creature == null) return 0;
            var skills = Creatures.SkillsOf(creature);
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] != null && !SkillCosts.IsMaxed(creature.SkillPoints[i])) return i;
            }
            return 0;
        }
    }
}
