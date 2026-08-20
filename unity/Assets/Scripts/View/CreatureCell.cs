using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>一覧の升1つ。⭐ BOX と配合で同じ型を使う（読み方が画面ごとに変わらない）。</summary>
    public sealed class CreatureCell : MonoBehaviour
    {
        [SerializeField] private Image _art;
        [SerializeField] private Image _element;
        [SerializeField] private Text _wild;
        [SerializeField] private GameObject _mark;   // 選ばれている印
        /// <summary>特性を持っている印。⭐ 一覧で持ち主を見つけるためだけの丸。
        /// ⚠️ 名前は出さない（升が小さい）。何を持っているかは詳細で読ませる。</summary>
        [SerializeField] private GameObject _trait;
        [SerializeField] private Button _button;

        /// <summary>★の枠。⭐ **Prefab には無い。**要るときに1度だけ作って使い回す。
        ///
        /// ⚠️ Prefab を作り直すと手で置いた物が消えるので、後から足す物はここで作る。
        /// ⚠️ Bind のたびに作らない（升は毎フレーム敷き直されるので、積み上がる）。</summary>
        private RectTransform _rarity;

        public void Bind(Creature creature, bool picked, Action onTap) =>
            Bind(creature, picked, onTap, null, Ui.InkDim);

        /// <param name="note">升の下段に出す一言。⭐ 画面ごとに違うのはここだけ
        /// （合成なら「＋14」、編成なら「Lv 44」）。⚠️ null なら出さない。</param>
        /// <param name="noteInk">その字の色。⚠️ 白い札の上なので …Ink 系を渡すこと。</param>
        public void Bind(Creature creature, bool picked, Action onTap, string note, Color noteInk)
        {
            var species = Creatures.SpeciesOf(creature);
            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(species.Sprite, Creatures.PaletteOf(creature));
                _art.preserveAspect = true;
            }
            if (_element != null) _element.color = ElementMark.ColorOf(creature.Element);
            // ⚠️ **升の下に素質の合計を書かない**（2026-08-18・作者判断）。
            //    ⭐ 並べ替えれば順に並ぶので、1つずつ数を読ませる必要がない。
            //    数が並ぶと一覧が「表」になり、絵で選ぶ画面でなくなる。
            // ⭐ ただし**画面ごとの一言**はここに出す（合成の「＋14」など）。
            if (_wild != null)
            {
                bool say = !string.IsNullOrEmpty(note);
                _wild.gameObject.SetActive(say);
                if (say) { _wild.text = note; _wild.color = noteInk; }
            }
            // ⭐ ★の枠。素質の合計から引く（生まれつきの良し悪しがそのまま縁に出る）
            ShowRarity(Nests.RarityOfWildTotal(Stats.TotalOf(creature.Wild)));
            if (_mark != null) _mark.SetActive(picked);
            if (_trait != null) _trait.SetActive(creature.TraitId != null);
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                if (onTap != null) _button.onClick.AddListener(() => onTap());
            }
        }

        /// <summary>★の枠を出す。⭐ 升より一回り大きく、**升の後ろ**に敷いて縁だけ見せる。</summary>
        private void ShowRarity(int rarity)
        {
            var self = (RectTransform)transform;
            float w = self.rect.width, h = self.rect.height;
            // ⚠️ 格子が並べ終わる前は寸法が 0。そのときは出さない（次の Bind で付く）
            if (w <= 1f || h <= 1f) { if (_rarity != null) _rarity.gameObject.SetActive(false); return; }

            if (_rarity == null) _rarity = Ui.RarityFrame(transform, "Rarity", rarity, w, h);
            _rarity.gameObject.SetActive(true);
            float edge = Ui.RarityEdge(rarity);
            Ui.Place(_rarity, -edge, -edge, w + edge * 2f, h + edge * 2f);
            var image = _rarity.GetComponent<Image>();
            if (image != null) image.color = Ui.RarityInk(rarity);
            _rarity.SetAsFirstSibling();
        }
    }

    /// <summary>配合の親1枠。
    /// ⭐ 中身は <see cref="CreaturePanel"/> に丸ごと預ける（BOX と同じ札になる）。
    /// ⚠️ ここに欄を1つずつ持っていた頃は、BOX に出るのに配合に出ない欄が生まれていた。</summary>
    [Serializable]
    public sealed class ParentSlot
    {
        public GameObject Filled;
        public GameObject Empty;
        public CreaturePanel Panel;
    }

    /// <summary>格子に札を敷き直す共通処理。
    /// ⚠️ 位置を計算しない。並べるのは Prefab に付けた GridLayoutGroup の仕事。</summary>
    public static class CellGrid
    {
        /// <summary>升の型。⭐ **BOX・配合と同じ Prefab。**
        /// ⚠️ 見つからなければ null（呼ぶ側が気づけるように黙って代用しない）。</summary>
        public static CreatureCell Template()
        {
            if (_template == null) _template = Resources.Load<CreatureCell>("Prefabs/CreatureCell");
            if (_template == null)
            {
                Debug.LogWarning("Prefabs/CreatureCell が無い。"
                    + "「画面を Prefab に書き出す」を1度走らせること");
            }
            return _template;
        }

        private static CreatureCell _template;

        /// <summary>覆いの中に**同じ升の一覧**を作る。
        ///
        /// ⭐ 合成・パーティ編成・BOX・配合が、これ1つを通る。
        /// ⚠️ 手書きで並べていた頃は、同じ「個体を選ぶ升」が画面ごとに
        /// 228×200（丸なし・べた塗りで選択）と 224×200（丸あり・枠で選択）に割れていた。
        ///
        /// ⚠️ 高さは中身から決めるので、呼ぶ側は**器の高さ**だけ渡す。</summary>
        public static RectTransform Scroll(Transform panel, string name,
            float left, float top, float width, float height,
            CreatureCell template, IReadOnlyList<Creature> list,
            Func<string, bool> isPicked, Action<string> onPick,
            Func<Creature, string> noteOf = null, Func<Creature, Color> inkOf = null)
        {
            var size = ((RectTransform)template.transform).sizeDelta;
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + Gap) / (size.x + Gap)));
            float rows = Mathf.Max(1f, Mathf.Ceil(list.Count / (float)columns));
            var content = Ui.Scroller(panel, name, left, top, width, height,
                rows * (size.y + Gap));
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = size;
            grid.spacing = new Vector2(Gap, Gap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            // ⚠️ 列が変われば行数も変わる。⭐ 高さは中身に追わせる
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Fill(content, template, list, isPicked, onPick, noteOf, inkOf);
            return content;
        }

        /// <summary>升と升のあいだの隙間。⭐ **固定する。**
        /// ⚠️ 余りを隙間へ配る形にしたら、升 328 が2列しか入らない幅で
        ///    隙間が 328 まで開いた（実測）。余りは左右の余白へ回す。</summary>
        private const float Gap = 12f;

        /// ⚠️ **左右の余白を先に引かない。**器の幅がそのまま並べられる幅。
        ///    引いていた頃は、覆いの中（器 936）だけ3列になって BOX（4列）と揃わなかった。
        ///    ⭐ 余りは下で左右へ等分するので、端に張り付くことはない。

        public static void Fill(RectTransform parent, CreatureCell template,
            IReadOnlyList<Creature> list, Func<string, bool> isPicked, Action<string> onPick) =>
            Fill(parent, template, list, isPicked, onPick, null, null);

        /// <param name="noteOf">升の下段に出す一言。⚠️ null なら出さない。</param>
        /// <param name="inkOf">その字の色。⚠️ null なら <see cref="Ui.InkDim"/>。</param>
        public static void Fill(RectTransform parent, CreatureCell template,
            IReadOnlyList<Creature> list, Func<string, bool> isPicked, Action<string> onPick,
            Func<Creature, string> noteOf, Func<Creature, Color> inkOf)
        {
            if (parent == null || template == null) return;
            // ⭐ 見張り役を付ける。⚠️ ここで測るだけでは足りない ── 敷き直す時点では
            //    まだレイアウトが走っておらず、配合の器は このあと 1080 → 984 に縮む。
            var fit = parent.GetComponent<CellGridFit>();
            if (fit == null) fit = parent.gameObject.AddComponent<CellGridFit>();
            fit.Watch(template);

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(child);
            }
            foreach (var creature in list)
            {
                string id = creature.Id;
                var cell = UnityEngine.Object.Instantiate(template, parent);
                cell.gameObject.SetActive(true);
                cell.Bind(creature, isPicked != null && isPicked(id), () => onPick(id),
                    noteOf == null ? null : noteOf(creature),
                    inkOf == null ? Ui.InkDim : inkOf(creature));
            }
        }

        /// <summary>⭐ **升の大きさは型そのものから取る。**
        ///
        /// ⚠️ 格子の側にも寸法を書いていたので、型を大きくした日に食い違った。
        /// 実測（2026-08-19）: 型は 328×300、格子は 228×200 を配っていて、
        /// 絵は中心から 50 右へずれ、選んだ印は**隣の升へ 100 はみ出し**、
        /// 特性の丸は升の外に落ちていた。
        /// ⭐ 数が2か所にあると、いつか必ず食い違う。出所を型1つにする。
        ///
        /// ⚠️ 列の数も余白も**幅から出す**。⭐ 余りは隙間へ配るので、
        /// 左右の余白は必ず同じになる（今までは 左48 / 右84 と傾いていた）。</summary>
        private static void Measure(RectTransform parent, CreatureCell template)
        {
            var grid = parent.GetComponent<GridLayoutGroup>();
            if (grid == null) return;
            var size = ((RectTransform)template.transform).sizeDelta;
            if (size.x <= 0f || size.y <= 0f) return;
            grid.cellSize = size;

            // ⚠️ **中身ではなく器（viewport）の幅で数える。**
            //    中身の幅で数えていたとき、器を 984 に縮めた配合の画面が
            //    1080 のまま4列を並べ、右端の升が切れていた（実測）。
            var holder = parent.parent as RectTransform;
            float width = holder != null && holder.rect.width > 0f
                ? holder.rect.width : parent.rect.width;
            if (width <= 0f) return;
            // ⭐ 中身も器に合わせる（横へはみ出す余地を残さない）。
            // ⚠️ 左右に張り付けてある中身（覆いの巻物）は触らない ── 触ると幅が壊れる
            bool stretched = !Mathf.Approximately(parent.anchorMin.x, parent.anchorMax.x);
            if (!stretched && !Mathf.Approximately(parent.rect.width, width))
            {
                parent.sizeDelta = new Vector2(width, parent.sizeDelta.y);
            }
            int columns = Mathf.FloorToInt((width + Gap) / (size.x + Gap));
            if (columns < 1) columns = 1;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.spacing = new Vector2(Gap, grid.spacing.y);

            // ⭐ **余りは左右へ等分する。**⚠️ 片側へ寄せると格子が傾く
            //    （実測では 左48 / 右84 で、1列目だけ左へずれて見えた）。
            float row = size.x * columns + Gap * (columns - 1);
            int side = Mathf.RoundToInt((width - row) / 2f);
            if (side < 0) side = 0;
            grid.padding.left = side;
            grid.padding.right = side;
        }

        /// <summary>器の幅を見張って、変わったら格子を測り直す。
        ///
        /// ⚠️ **敷き直した瞬間に測るだけでは足りない。**そのときはまだレイアウトが
        /// 走っておらず、配合の器は そのあと 1080 → 984 に縮む。
        /// 実測（2026-08-19）では、1080 の前提で 3列（1008 幅）を並べたまま
        /// 器が 984 に縮み、右端の升が切れていた。
        ///
        /// ⭐ 毎フレーム数えない ── 幅が変わったときだけ測り直す。</summary>
        private sealed class CellGridFit : MonoBehaviour
        {
            private CreatureCell _template;
            private float _was = -1f;

            public void Watch(CreatureCell template)
            {
                _template = template;
                _was = -1f;        // ⚠️ 型が変わったかもしれないので測り直させる
                Apply();
            }

            private void LateUpdate() => Apply();

            private void Apply()
            {
                if (_template == null) return;
                var parent = (RectTransform)transform;
                Settle(parent);
                var holder = parent.parent as RectTransform;
                float width = holder != null && holder.rect.width > 0f
                    ? holder.rect.width : parent.rect.width;
                if (width <= 0f || Mathf.Approximately(width, _was)) return;
                _was = width;
                Measure(parent, _template);
            }

            /// <summary>器の寸法を**いま**確定させる。
            /// ⚠️ 敷き直した直後はレイアウトが走っておらず、器はまだ古い幅のまま。
            /// ⭐ 待たずに測ると、1フレームだけ古い列数で描かれて画面がちらつく
            ///    （実測: 配合で 3列が 984 の器からはみ出して見えた）。</summary>
            private static void Settle(RectTransform at)
            {
                RectTransform top = null;
                for (var t = at.parent as RectTransform; t != null; t = t.parent as RectTransform)
                {
                    if (t.GetComponent<LayoutGroup>() != null) top = t;
                }
                if (top != null) LayoutRebuilder.ForceRebuildLayoutImmediate(top);
            }
        }
    }
}
