using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>分解。⭐ **個体を EXP に還す入口。**
    ///
    /// ⚠️ 2026-08-22 まで「たまごで技を鍛える」も同じ札の中のタブに入っていた。
    /// ⭐ 別のことなので分けた（作者の指示）── たまごは BOX の「技を鍛える」から。
    ///
    ///
    /// ⚠️ 2026-08-19 に「合成」（1体に食わせて直接 Lv を上げる）から置き換えた（作者の指示）。
    /// ⭐ EXP を**溜める側**に一本化したので、「誰に食わせるか」を先に決めなくてよくなった
    /// ── 還した EXP はどの個体にも後から使える。
    ///
    /// ⭐ **一度に <see cref="MaxPick"/> 体まで選べる。**1体ずつだと保管庫を空けるのに
    /// 何十回も往復することになるため。
    ///
    /// ⚠️ 分解した個体は**失われる**（＝「逃がす」の代わりでもある）。
    /// </summary>
    public static class FusePanel
    {
        /// <summary>一度に分解できる数。
        /// ⚠️ **ここで数を持たない** ── 遵びの規則なので Core が唯一の出所。</summary>
        public const int MaxPick = Games.PickAtOnce;

        private const float PanelLeft = 48f;
        private const float PanelTop = 180f;
        private const float PanelWidth = 984f;
        private const float PanelHeight = 1560f;
        private const float Pad = 24f;
        private const float Inner = PanelWidth - Pad * 2f;

        private const float TabTop = 150f;
        // ⚠️ **タブの高さは Ui.Tap（112）になる。**Ui.Tappable が指で押せる下限まで
        //    勝手に引き上げるので、72 を渡しても 112 で置かれる（実測で 262 まで伸びていた）。
        //    ⭐ 実際に置かれる高さで次の位置を決めること。
        private const float TabH = 112f;          // = Ui.Tap
        private const float NoteTop = TabTop + TabH + 12f;   // 274
        private const float ListTop = NoteTop + 52f;         // 326
        private const float CellW = 228f;
        private const float CellH = 200f;
        private const int PerRow = 4;

        private static GameObject _open;

        /// <summary>いま選んでいる餌。⭐ 画面を組み直しても覚えておく。</summary>
        private static readonly List<string> Picked = new List<string>();

        /// <summary>いま選んでいる卵。⭐ **押した順に入る**（入る順がそのまま並び順）。
        /// ⚠️ 個体の側（<see cref="Picked"/>）と混ぜない ── 別のものを数えている。</summary>
        private static readonly List<string> PickedEggs = new List<string>();

        /// <summary>たまごの側を見ているか。⚠️ 組み直しで先頭へ戻さない。</summary>
        private static bool _eggs;

        /// <summary>たまごを注ぐ先の枠。</summary>
        private static int _slot;

        public static void Show(App app, string creatureId)
        {
            Picked.Clear();
            PickedEggs.Clear();
            _eggs = false;
            _slot = FirstOpen(app, creatureId);
            Rebuild(app, creatureId);
        }

        /// <summary>⚠️ <see cref="Show"/> と分ける ── 選んだ餌とタブを覚えたまま描き直すため。</summary>
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
            Object.Destroy(_open);
            _open = null;
        }

        // ── 組み立て ────────────────────────────────────

        private static void Build(App app, string creatureId)
        {
            var eater = Find(app, creatureId);
            if (eater == null) return;

            var root = Ui.Rect("FusePanel", app.Overlay);
            Ui.Stretch(root);
            _open = root.gameObject;

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimButton = root.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.onClick.AddListener(Close);

            var panel = Ui.Card(root, "Panel", PanelLeft, PanelTop, PanelWidth, PanelHeight);

            Ui.Label(panel, "Title", "分解", 40, Ui.Ink, TextAnchor.UpperLeft,
                Pad, Pad, Inner, 56f);
            // ⭐ 溜まっている EXP を出す。⚠️ 分解の行き先がここだと分かる形にする
            Ui.Label(panel, "Who",
                $"{Creatures.SpeciesOf(eater).Name}  Lv {Levels.Of(eater)}/{Levels.MaxOf(eater)}"
                + $"　　持っている EXP {Ui.Digits(app.Game.Idle.Exp)}",
                26, Ui.InkDim, TextAnchor.UpperLeft, Pad, 86f, Inner, 40f);

            // ⚠️ **たまごのタブは 2026-08-22 に外した**（作者の指示
            //    「技を鍛えるが分解の中に入っているので表に出して分離」）。
            //    ⭐ たまごは BOX の「技を鍛える」から開く（`SkillEggPanel`）。
            //    ⚠️ 分解は「個体を捨てる」画面なので、たまごを使う道と混ぜない。
            BuildCreatures(app, panel, eater, creatureId);

            Ui.Tappable(panel, "Close", "閉じる", Close,
                Pad, PanelHeight - Ui.Tap - Pad, Inner, Ui.Tap);
        }

        /// <summary>⭐ **トグル。**「個体を分解」と「たまごで技を鍛える」を
        /// 1枚の札の中で切り替える。
        /// ⚠️ 別画面に分けると、どちらをするか決める前に画面を選ばされる。</summary>
        private static void Tabs(App app, RectTransform panel, string creatureId)
        {
            float half = (Inner - 12f) / 2f;
            Tab(app, panel, "TabUnits", "個体を分解", false, creatureId, Pad, half);
            Tab(app, panel, "TabEggs", "たまごで技を鍛える", true, creatureId, Pad + half + 12f, half);
        }

        private static void Tab(App app, RectTransform panel, string name, string label,
            bool eggs, string creatureId, float left, float width)
        {
            var button = Ui.Tappable(panel, name, label,
                () => { _eggs = eggs; Rebuild(app, creatureId); },
                left, TabTop, width, TabH);
            // ⚠️ 色を掛けず絵を差し替える（掛けると「押せない」と見分けが付かない）
            var plate = button.GetComponent<Image>();
            if (plate != null) plate.sprite = Ui.SkinSprite(_eggs == eggs ? "button-lead" : "button");
        }

        // ── 個体を食わせる ──────────────────────────────

        private static void BuildCreatures(App app, RectTransform panel, Creature eater,
            string creatureId)
        {
            // ⭐ **分解で入るのは EXP だけ。**⚠️ 誰に使うかはここで決めない
            //    （溜めてから、どの個体のレベルアップにも使える）。
            int exp = 0;
            foreach (string id in Picked)
            {
                var one = Find(app, id);
                if (one != null) exp += Levels.DissolveExpOf(one);
            }

            Ui.Label(panel, "Gain",
                $"選んだ {Picked.Count}/{MaxPick} 体で  EXP ＋{Ui.Digits(exp)}",
                28, exp > 0 ? Ui.Ink : Ui.InkDim, TextAnchor.UpperLeft,
                Pad, NoteTop, Inner, 40f);

            var pool = new List<Creature>();
            foreach (var c in app.Game.Storage.Creatures)
            {
                // ⚠️ 出さないのは**いま見ている本人だけ**（見ている札が消えるのを防ぐ）。
                //    ⭐ 編成中の個体も候補に出す ── 外していた頃は、手持ちが3体で
                //    全員が出撃中という序盤に、1体も選べず何もできなかった。
                //    出撃中であることは升に印で出し、選ぶかどうかは人が決める。
                if (c.Id == creatureId) continue;
                pool.Add(c);
            }

            float top = ListTop;
            // ⚠️ **候補が0のとき、理由を出す。**空の枠だけ出すと「壊れている」に見える。
            //    ⭐ 編成に入っている個体を候補から外しているので、
            //    手持ちが少ないうちは全員が対象外になりうる（実際そうなった）。
            if (pool.Count == 0)
            {
                Ui.Label(panel, "Empty", "分解できる個体がいません", 28, Ui.InkDim,
                    TextAnchor.UpperCenter, Pad, top + 40f, Inner, 100f);
                return;
            }

            // ⭐ **BOX・配合と同じ升**（作者の指示「すべて揃えたい」）。
            // ⚠️ 手書きで並べていた頃は、同じ「個体を選ぶ升」が画面ごとに
            //    228×200（丸なし・べた塗りで選択）と 224×200（丸あり・枠で選択）に割れていた。
            CellGrid.Scroll(panel, "Pool", Pad, top, Inner,
                PanelHeight - top - Ui.Tap * 2f - Pad * 2f,
                CellGrid.Template(), pool,
                id => Picked.Contains(id),
                // ⭐ **升そのものがトグル。**押すたびに選ぶ／外すが入れ替わる
                id =>
                {
                    if (Picked.Contains(id)) Picked.Remove(id);
                    else if (Picked.Count < MaxPick) Picked.Add(id);
                    Rebuild(app, creatureId);
                },
                // ⭐ 一言は「分解したら何 EXP になるか」。
                // ⚠️ **出撃中は分解する前に分かるようにする**（分解すると失われる）
                c => Games.IsInParty(app.Game, c.Id)
                    ? $"出撃中  EXP {Ui.Digits(Levels.DissolveExpOf(c))}"
                    : $"EXP {Ui.Digits(Levels.DissolveExpOf(c))}",
                c => Games.IsInParty(app.Game, c.Id) ? Ui.DangerInk : Ui.InkDim);
            bool canFeed = Picked.Count > 0;
            var go = Ui.Tappable(panel, "Fuse",
                canFeed ? $"分解する（EXP ＋{Ui.Digits(exp)}）" : "分解する",
                () => Fuse(app, creatureId),
                Pad, PanelHeight - Ui.Tap * 2f - Pad - 12f, Inner, Ui.Tap);
            go.interactable = canFeed;
            var face = go.GetComponent<Image>();
            if (face != null) face.sprite = Ui.SkinSprite(canFeed ? "button-lead" : "button-off");
        }

        /// <summary>選んだぶんをまとめて分解する。
        /// ⭐ **数え方も削除も Core が1回で持つ**（<see cref="Games.Dissolve"/>）。
        /// ⚠️ 画面側で1体ずつ数えると、途中で失敗したときに帳尻が合わなくなる。</summary>
        private static void Fuse(App app, string creatureId)
        {
            int count = Picked.Count;
            int total = Games.Dissolve(app.Game, new List<string>(Picked));
            Picked.Clear();
            if (total > 0)
            {
                BannerView.Show(app.Overlay, $"{count}体を分解した  EXP ＋{Ui.Digits(total)}", null);
            }
            app.Refresh();
            Rebuild(app, creatureId);
        }

        // ── たまごで技を鍛える ──────────────────────────

        private static void BuildEggs(App app, RectTransform panel, Creature eater,
            string creatureId)
        {
            // 注ぐ先の3枠
            var skills = Creatures.SkillsOf(eater);
            float slotW = (Inner - 24f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                int slot = i;
                var skill = i < skills.Length ? skills[i] : null;
                int points = eater.SkillPoints[i];
                bool room = skill != null && !SkillCosts.IsMaxed(points);
                var b = Ui.Tappable(panel, $"Slot {i}",
                    skill == null ? "—" : $"{skill.Name}  Lv{SkillCosts.LevelOf(points)}",
                    // ⚠️ **枠を変えたら選び直し。**⭐ 残すと、別の枠の空きで選んだ卵が
                    //    新しい枠には入らず、「＋いくつ」と出した数と実際が食い違う
                    () => { _slot = slot; PickedEggs.Clear(); Rebuild(app, creatureId); },
                    Pad + (slotW + 12f) * i, NoteTop, slotW, Ui.Tap);
                b.interactable = room;
                var plate = b.GetComponent<Image>();
                if (plate != null)
                    plate.sprite = Ui.SkinSprite(!room ? "button-off" : _slot == slot ? "button-lead" : "button");
            }

            EggShelf(app, panel, eater, creatureId, skills);
        }

        /// <summary>棚の卵。⭐ **選んでから、最後に一度だけ注ぐ**（2026-08-21・作者の指示）。
        ///
        /// ⚠️ 直す前は**押した瞬間に入っていた**ので、10個入れるには10回押すことになり、
        /// そのたびに裏でレベルが上がっていた ── ⭐ 取り消せない操作を、
        /// 押した回数だけ黙って重ねる形だった。
        ///
        /// ⚠️ **入らない卵は選ばせない。**⭐ 選んだぶんが必ず入るので、
        /// 「＋いくつ」と出した数と実際が食い違わない。</summary>
        private static void EggShelf(App app, RectTransform panel, Creature eater,
            string creatureId, Skill[] skills)
        {
            var eggs = app.Game.Eggs;
            bool usable = _slot >= 0 && _slot < skills.Length && skills[_slot] != null
                && !SkillCosts.IsMaxed(eater.SkillPoints[_slot]);
            int points = eater.SkillPoints[_slot];
            int room = usable ? SkillCosts.TotalFor(Skills.MaxLevel) - points : 0;

            // ⭐ 選んだぶんの合計。⚠️ 押した順に足す（Core が入れる順と同じにする）
            int gain = 0;
            foreach (string id in PickedEggs)
            {
                var one = EggById(app, id);
                if (one != null) gain += Rarities.PointsOf(one.Rarity);
            }

            float noteTop = NoteTop + Ui.Tap + 8f;
            // ⭐ **入れたあとのレベルまで出す。**⚠️ ポイントだけだと、
            //    「これで上がるのか」を人が計算することになる
            string say = !usable ? "この枠はもう鍛えられません"
                : gain > 0
                    ? $"選んだ {PickedEggs.Count}/{MaxPick} 個で ＋{gain}　"
                        + $"Lv{SkillCosts.LevelOf(points)} → Lv{SkillCosts.LevelOf(points + gain)}"
                    : $"たまごを選ぶ（{MaxPick} 個まで）　あと {SkillCosts.ToNext(points)} で次の Lv";
            Ui.Label(panel, "EggGain", say, 28, gain > 0 ? Ui.Ink : Ui.InkDim,
                TextAnchor.UpperLeft, Pad, noteTop, Inner, 40f);

            float eggTop = noteTop + 48f;
            float gridH = PanelHeight - eggTop - Ui.Tap * 2f - Pad * 2f - 12f;
            float eggRows = Mathf.Max(Mathf.Ceil(eggs.Count / (float)PerRow), 1f);
            var grid = Ui.Scroller(panel, "Eggs", Pad, eggTop, Inner, gridH, eggRows * CellH);

            for (int i = 0; i < eggs.Count; i++)
            {
                var egg = eggs[i];
                string eggId = egg.Id;
                int worth = Rarities.PointsOf(egg.Rarity);
                bool picked = PickedEggs.Contains(eggId);
                // ⚠️ 上限を越える卵は押させない。⭐ 越えた分は黙って消える（★5 なら 81pt が蒸発する）
                bool fits = picked
                    || (usable && PickedEggs.Count < MaxPick && worth <= room - gain);

                // ⭐ **どの画面でも同じ卵の升**（絵・★・一言）
                var cell = Ui.EggCell(grid, $"Egg {i}", egg, $"＋{worth}", Ui.Ink,
                    (i % PerRow) * CellW, (i / PerRow) * CellH, CellW - 8f, CellH - 8f,
                    dim: !fits, picked: picked);

                var tap = cell.gameObject.AddComponent<Button>();
                tap.targetGraphic = cell.GetComponent<Image>();
                tap.interactable = fits;
                if (!fits) continue;
                tap.onClick.AddListener(() =>
                {
                    if (!PickedEggs.Remove(eggId)) PickedEggs.Add(eggId);
                    Rebuild(app, creatureId);
                });
            }
            if (eggs.Count == 0)
            {
                Ui.Label(panel, "None", "たまごがありません", 26, Ui.InkDim,
                    TextAnchor.MiddleCenter, Pad, eggTop + 60f, Inner, 40f);
            }

            bool ready = PickedEggs.Count > 0;
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
            int count = PickedEggs.Count;
            int total = Games.FeedEggsToSkill(app.Game, creatureId, _slot, new List<string>(PickedEggs));
            PickedEggs.Clear();
            if (total > 0)
            {
                BannerView.Show(app.Overlay, $"たまご{count}個で技が鍛わった  ＋{total}", null);
            }
            app.Refresh();
            Rebuild(app, creatureId);
        }

        private static Egg EggById(App app, string id)
        {
            foreach (var egg in app.Game.Eggs) if (egg.Id == id) return egg;
            return null;
        }

        // ── 小物 ────────────────────────────────────────

        private static Creature Find(App app, string id)
        {
            foreach (var c in app.Game.Storage.Creatures) if (c.Id == id) return c;
            return null;
        }

        /// <summary>まだ鍛えられる最初の枠。⚠️ 全部上限なら 0（押しても何も起きない）。</summary>
        private static int FirstOpen(App app, string creatureId)
        {
            var c = Find(app, creatureId);
            if (c == null) return 0;
            for (int i = 0; i < 3; i++) if (!SkillCosts.IsMaxed(c.SkillPoints[i])) return i;
            return 0;
        }
    }
}
