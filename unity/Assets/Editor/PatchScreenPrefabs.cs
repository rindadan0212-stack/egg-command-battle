using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using EggCommand.View;

namespace EggCommand.EditorTools
{
    /// <summary>既にある Prefab に**足りない部品だけ**を足す。
    ///
    /// ⚠️ <see cref="BuildScreenPrefabs"/> は「無ければ丸ごと作る」道具なので、
    /// 手で位置を直したあとの Prefab には使えない（消しては作り直せない）。
    /// ⭐ こちらは**同じ名前の子が既にあれば何もしない**。何度走らせても同じ形になる。
    ///
    /// ⚠️ ここで書く寸法も「初期値」でしかない。以後は Unity Editor で直す。
    /// </summary>
    public static class PatchScreenPrefabs
    {
        private const string Dir = "Assets/Resources/Prefabs";

        [MenuItem("Egg Command/画面に足りない部品を足す")]
        public static void PatchAll()
        {
            int touched = 0;
            touched += Patch("EncounterCard", PatchEncounterCard);
            touched += Patch("CreatureCell", PatchCreatureCell);
            touched += Patch("BoxScreen", PatchBox);
            AssetDatabase.Refresh();
            Debug.Log(touched == 0 ? "足すものは無かった" : $"{touched} 個の Prefab に足した");
        }

        private static int Patch(string name, System.Func<GameObject, bool> patch)
        {
            string path = $"{Dir}/{name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) { Debug.LogError($"{path} が読めない"); return 0; }
            bool changed = patch(root);
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return changed ? 1 : 0;
        }

        // ── 巣の札: 残り時間 ────────────────────────────

        /// <summary>⭐ ★5 の巣は10分で消える。⚠️ 予告が無いと理不尽にしかならない。</summary>
        private static bool PatchEncounterCard(GameObject root)
        {
            bool made = Find(root, "Left") == null;
            if (made)
            {
                var made1 = Add(root.transform, "Left", 0f, 0f, 248f, 56f);
                Label(made1, "", 44, Ui.InkDim, TextAnchor.MiddleRight);

                var made2 = Add(root.transform, "Drain Track", 0f, 0f, 100f, 14f);
                made2.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);
                var made3 = Add(made2, "Drain", 0f, 0f, 100f, 14f);
                var made4 = made3.gameObject.AddComponent<Image>();
                made4.color = Ui.Good;
                // ⚠️ 幅ではなく fillAmount で減らす（Prefab で幅を変えても比が壊れない）
                made4.sprite = Ui.SkinSprite("pill");
                made4.type = Image.Type.Filled;
                made4.fillMethod = Image.FillMethod.Horizontal;
            }

            // ⚠️ **札の高さは手で変えてある**（984×300 で書き出したが実物は 984×380）。
            //    ⭐ 左上からの座標で置くと、伸ばしたぶんだけ下が空く。
            //    端に留めておけば、あとで何度伸ばしても同じ見え方になる。
            var clock = (RectTransform)Find(root, "Left");
            clock.anchorMin = new Vector2(1f, 1f);
            clock.anchorMax = new Vector2(1f, 1f);
            clock.pivot = new Vector2(1f, 1f);
            clock.sizeDelta = new Vector2(248f, 56f);
            clock.anchoredPosition = new Vector2(-36f, -122f);   // Lv の数字と高さを揃える

            var track = (RectTransform)Find(root, "Drain Track");
            track.anchorMin = new Vector2(0f, 0f);
            track.anchorMax = new Vector2(1f, 0f);
            track.pivot = new Vector2(0.5f, 0f);
            track.offsetMin = new Vector2(36f, 24f);             // 下端から 24
            track.offsetMax = new Vector2(-36f, 38f);            // 厚み 14

            var bar = (RectTransform)Find(root, "Drain");
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = Vector2.one;
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;

            var view = root.GetComponent<EncounterCard>();
            var so = new SerializedObject(view);
            so.FindProperty("_left").objectReferenceValue = clock.GetComponent<Text>();
            so.FindProperty("_drain").objectReferenceValue = bar.GetComponent<Image>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── 一覧の升: 特性の印 ──────────────────────────

        /// <summary>⭐ 一覧で「持っている個体」を見つけられるようにする。
        /// ⚠️ 名前は出さない（升は小さい）。詳細で読ませる。</summary>
        private static bool PatchCreatureCell(GameObject root)
        {
            if (Find(root, "Trait") != null) return false;

            // 属性の丸（左上）と対になる位置＝右上
            var mark = Add(root.transform, "Trait", 284f, 14f, 30f, 30f);
            var image = mark.gameObject.AddComponent<Image>();
            image.sprite = Ui.SkinSprite("circle-outline");
            image.color = Ui.Accent;
            image.raycastTarget = false;

            var view = root.GetComponent<CreatureCell>();
            var so = new SerializedObject(view);
            so.FindProperty("_trait").objectReferenceValue = mark.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── BOX の詳細: 特性 ────────────────────────────

        private static bool PatchBox(GameObject root)
        {
            var detail = Find(root, "Detail");
            if (detail == null) { Debug.LogError("BoxScreen に Detail が無い"); return false; }
            if (Find(detail.gameObject, "Trait") != null) return false;

            // ⚠️ Slant（y 152〜190）の下、ステ1行目（y 250）の上に置く
            var trait = Add(detail, "Trait", 268f, 192f, 700f, 40f);
            var text = Label(trait, "", 24, Ui.Accent, TextAnchor.UpperLeft);

            var view = root.GetComponent<BoxView>();
            var so = new SerializedObject(view);
            so.FindProperty("_trait").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── 道具 ────────────────────────────────────────

        private static Transform Find(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name && t != root.transform) return t;
            }
            return null;
        }

        /// <summary>左上を原点に置く。⚠️ <see cref="Ui.Place"/> と同じ約束。</summary>
        private static RectTransform Add(Transform parent, string name,
            float left, float top, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
            return rect;
        }

        private static Text Label(RectTransform rect, string content, int size, Color color,
            TextAnchor anchor)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = Ui.TheFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
