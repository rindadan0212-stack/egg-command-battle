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
            touched += Patch("BreedScreen", PatchBreed);
            touched += Patch("BattleScreen", PatchBattle);
            touched += Patch("HomeScreen", PatchHome);
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

            // ⭐ 盗んだ回数（守りの固さ）。時計の下、同じ右端に揃える
            if (Find(root, "Raids") == null)
            {
                var made5 = Add(root.transform, "Raids", 0f, 0f, 248f, 48f);
                Label(made5, "", 32, Ui.Accent, TextAnchor.MiddleRight);
            }
            var raids = (RectTransform)Find(root, "Raids");
            raids.anchorMin = new Vector2(1f, 1f);
            raids.anchorMax = new Vector2(1f, 1f);
            raids.pivot = new Vector2(1f, 1f);
            raids.sizeDelta = new Vector2(248f, 48f);
            raids.anchoredPosition = new Vector2(-36f, -186f);

            var bar = (RectTransform)Find(root, "Drain");
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = Vector2.one;
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;

            var view = root.GetComponent<EncounterCard>();
            var so = new SerializedObject(view);
            so.FindProperty("_left").objectReferenceValue = clock.GetComponent<Text>();
            so.FindProperty("_drain").objectReferenceValue = bar.GetComponent<Image>();
            so.FindProperty("_raids").objectReferenceValue = raids.GetComponent<Text>();
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

            // ⚠️ 世代・変異（Point）は「逃がす」ボタンの真下に置かれていて、
            //    ボタンの面に隠れて読めなかった（字どうしは被っていないので、
            //    字だけを比べる検査では見つからない）。
            //    ⭐ ステの最終行(452)と押しどころ(560)の間の空きへ動かす
            var point = Find(detail.gameObject, "Point");
            if (point != null)
            {
                var pr = (RectTransform)point;
                pr.anchorMin = new Vector2(0f, 1f);
                pr.anchorMax = new Vector2(0f, 1f);
                pr.pivot = new Vector2(0f, 1f);
                pr.sizeDelta = new Vector2(420f, 40f);
                pr.anchoredPosition = new Vector2(26f, -472f);
                var pt = point.GetComponent<Text>();
                if (pt != null) pt.alignment = TextAnchor.MiddleLeft;
            }

            if (Find(detail.gameObject, "Trait") != null) return true;

            // ⚠️ Slant（y 152〜190）の下、ステ1行目（y 250）の上に置く
            var trait = Add(detail, "Trait", 268f, 192f, 700f, 40f);
            var text = Label(trait, "", 24, Ui.Accent, TextAnchor.UpperLeft);

            var view = root.GetComponent<BoxView>();
            var so = new SerializedObject(view);
            so.FindProperty("_trait").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── 配合: 親の特性と得意・不得意 ────────────────

        /// <summary>⚠️ 特性も得意・不得意も**遺伝する**のに、親の札に出ていなかった。</summary>
        private static bool PatchBreed(GameObject root)
        {
            var view = root.GetComponent<BreedView>();
            var so = new SerializedObject(view);
            var slots = so.FindProperty("_parents");
            bool changed = false;

            // ⚠️ 親の札は 460×537。技の欄が下端まで使っているので、行を足す場所が無い。
            //    ⭐ 札を伸ばし、下の段（結果・配合する・一覧）をそのぶん押し下げる。
            //    ⚠️ 伸ばさずに下へ書くと、足した2行が**札からはみ出して**
            //    「配合する」の上に重なった（実測）。
            const float CardHeight = 620f;
            const float StackTop = 650f;
            foreach (var name in new[] { "ParentA", "ParentB", "Plus" })
            {
                var card = Find(root, name);
                if (card == null) continue;
                var cr = (RectTransform)card;
                if (name == "Plus") continue;
                cr.sizeDelta = new Vector2(cr.sizeDelta.x, CardHeight);
                // ⚠️ 中身の器（Filled）が札より小さいままだと、あとでマスクを付けた瞬間に
                //    下の行が切れる。⭐ 札と同じ高さに揃えておく
                var filled = Find(card.gameObject, "Filled");
                if (filled != null)
                    ((RectTransform)filled).sizeDelta =
                        new Vector2(((RectTransform)filled).sizeDelta.x, CardHeight);
            }
            var stack = Find(root, "Stack");
            if (stack != null)
            {
                var sr = (RectTransform)stack;
                sr.anchoredPosition = new Vector2(sr.anchoredPosition.x, -StackTop);
            }

            for (int i = 0; i < slots.arraySize; i++)
            {
                var element = slots.GetArrayElementAtIndex(i);
                var skills = element.FindPropertyRelative("Skills").objectReferenceValue as Text;
                if (skills == null) continue;
                var box = (RectTransform)skills.transform.parent;
                if (Find(box.gameObject, $"Slant {i}") != null) { changed = true; continue; }

                // ⚠️ 技の1行（Skills）のすぐ下に2行足す。位置は技の欄から測る
                var sr = (RectTransform)skills.transform;
                float left = sr.anchoredPosition.x;
                float top = -sr.anchoredPosition.y + sr.sizeDelta.y;
                float width = sr.sizeDelta.x;

                var slant = Add(box, $"Slant {i}", left, top + 4f, width, 34f);
                var slantText = Label(slant, "", 22, Ui.InkDim, TextAnchor.UpperLeft);
                var trait = Add(box, $"Trait {i}", left, top + 40f, width, 34f);
                var traitText = Label(trait, "", 22, Ui.Accent, TextAnchor.UpperLeft);

                element.FindPropertyRelative("Slant").objectReferenceValue = slantText;
                element.FindPropertyRelative("Trait").objectReferenceValue = traitText;
                changed = true;
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        // ── 戦闘: 相手が複数のときの器 ──────────────────

        /// <summary>⚠️ 相手の器が1つしか無く、雑魚の3対3で**1体しか見えなかった**。</summary>
        private static bool PatchBattle(GameObject root)
        {
            var view = root.GetComponent<BattleView>();
            var so = new SerializedObject(view);

            // ⚠️ 「選ぶ」は相手の3体目と**同じ場所**に置かれていた（1体しか出ない前提の位置）。
            //    ⭐ 相手の列より上、体のどれとも重ならない右肩へ逃がす
            var pick = Find(root, "Pick");
            if (pick != null)
            {
                var pr = (RectTransform)pick;
                pr.anchorMin = new Vector2(0f, 1f);
                pr.anchorMax = new Vector2(0f, 1f);
                pr.pivot = new Vector2(0f, 1f);
                pr.sizeDelta = new Vector2(180f, 112f);
                pr.anchoredPosition = new Vector2(880f, -16f);
                foreach (Transform child in pick)
                {
                    var cr = (RectTransform)child;
                    cr.sizeDelta = pr.sizeDelta;
                    cr.anchoredPosition = Vector2.zero;
                }
            }

            var foes = so.FindProperty("_foes");
            if (foes.arraySize == 3 && foes.GetArrayElementAtIndex(0).objectReferenceValue != null)
                return true;

            // ⭐ 味方の並びを鏡にする。味方は x 60・y 150 + 300i なので、相手は右側の同じ高さ
            var allies = so.FindProperty("_allies");
            var first = allies.GetArrayElementAtIndex(0).objectReferenceValue as UnitStand;
            var lone = so.FindProperty("_foe").objectReferenceValue as UnitStand;
            if (first == null || lone == null) { Debug.LogError("味方/相手の器が無い"); return false; }

            var ar = (RectTransform)first.transform;
            var lr = (RectTransform)lone.transform;
            float step = 300f;
            var second = allies.GetArrayElementAtIndex(1).objectReferenceValue as UnitStand;
            if (second != null)
                step = Mathf.Abs(((RectTransform)second.transform).anchoredPosition.y - ar.anchoredPosition.y);

            foes.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var made = Object.Instantiate(lone.gameObject, root.transform);
                made.name = $"Foe {i}";
                var r = (RectTransform)made.transform;
                r.anchorMin = lr.anchorMin; r.anchorMax = lr.anchorMax; r.pivot = lr.pivot;
                r.sizeDelta = lr.sizeDelta;
                // ⚠️ 1体のときの器は大きい（1.6倍）。並べるときは味方と同じ大きさに戻す
                r.localScale = ar.localScale;
                r.anchoredPosition = new Vector2(lr.anchoredPosition.x,
                    ar.anchoredPosition.y - step * i);
                foes.GetArrayElementAtIndex(i).objectReferenceValue = made.GetComponent<UnitStand>();
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── ホーム: 孵化器の並び ────────────────────────

        /// <summary>⚠️ 5枠が互い違いの3段で、どれが何番目か読めなかった。
        /// ⭐ 卵と孵化は「入れた場所に留まる」と決めているので、**場所が読めることが要る**。
        /// 3枠 + 2枠の格子に整える（1列5枠だと1枠 200px を切って絵が潰れる）。</summary>
        private static bool PatchHome(GameObject root)
        {
            var view = root.GetComponent<HomeView>();
            var so = new SerializedObject(view);
            var slots = so.FindProperty("_slots");
            if (slots.arraySize < 5) return false;

            var head = slots.GetArrayElementAtIndex(0).objectReferenceValue as IncubatorSlot;
            if (head == null) return false;
            var hr = (RectTransform)head.transform;
            if (Mathf.Approximately(hr.anchoredPosition.x, 42f)) return false;   // 既に整えてある

            float w = hr.sizeDelta.x, h = hr.sizeDelta.y;
            float gap = 18f;
            float top = -hr.anchoredPosition.y;
            float rowTop3 = (1080f - (w * 3f + gap * 2f)) / 2f;
            float rowTop2 = (1080f - (w * 2f + gap)) / 2f;

            for (int i = 0; i < 5; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as IncubatorSlot;
                if (slot == null) continue;
                var r = (RectTransform)slot.transform;
                float x = i < 3 ? rowTop3 + (w + gap) * i : rowTop2 + (w + gap) * (i - 3);
                float y = top + (i < 3 ? 0f : h + gap);
                r.anchoredPosition = new Vector2(x, -y);
            }
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
