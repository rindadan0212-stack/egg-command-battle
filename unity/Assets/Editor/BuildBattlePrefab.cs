using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using EggCommand.View;

namespace EggCommand.EditorTools
{
    /// <summary>戦闘の部品を Prefab に**一度だけ**書き出す足場。
    ///
    /// ⭐ ここで作った寸法は「初期値」でしかない。以後は Unity Editor で
    /// Prefab を開いてドラッグして決める。**コードへ戻さない。**
    ///
    /// ⚠️ 作り直したくなったら、Prefab を消してからもう一度これを走らせる
    /// （上書きすると手で直した位置が消える）。
    /// </summary>
    public static class BuildBattlePrefab
    {
        // ⚠️ Resources の下に置く。実行時に Resources.Load で拾うため
        private const string Dir = "Assets/Resources/Prefabs";
        private const string Path = Dir + "/UnitStand.prefab";

        [MenuItem("Egg Command/戦闘の部品を Prefab に書き出す")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");
            if (System.IO.File.Exists(Path))
            {
                Debug.LogWarning($"{Path} が既にある。手で直した位置を消さないよう、消してから実行する");
                return;
            }

            const float Size = 200f;
            var root = new GameObject("UnitStand", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(Size + 120f, Size + 140f);

            // 足元の光（今動く者）
            var glow = Add(root, "Glow", -10f, Size - 60f, Size + 20f, Size + 20f);
            Sprite(glow, "UI/circle", new Color(1f, 0.85f, 0.3f, 0.55f));

            var art = Add(root, "Art", 0f, 0f, Size, Size);
            art.gameObject.AddComponent<Image>().preserveAspect = true;

            // ゲージのピル
            var pill = Add(root, "Hp", 0f, Size + 6f, Size + 60f, 46f);
            Sprite(pill, "UI/pill", Color.white, sliced: true);
            var fill = Add(pill.gameObject, "Fill", 52f, 9f, Size + 60f - 62f, 28f);
            fill.gameObject.AddComponent<Image>().color = Ui.Good;
            var badge = Add(pill.gameObject, "Badge", 0f, 0f, 46f, 46f);
            Sprite(badge, "UI/circle", Ui.Good);
            var num = Add(pill.gameObject, "Num", 0f, 0f, 46f, 46f);
            Text(num, "0", 23, Ui.Ink, TextAnchor.MiddleCenter);

            // 行動ゲージ
            var track = Add(root, "GaugeTrack", 52f, Size + 58f, Size + 8f, 12f);
            track.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.8f);
            var gauge = Add(track.gameObject, "GaugeFill", 0f, 0f, Size + 8f, 12f);
            gauge.gameObject.AddComponent<Image>().color = new Color32(0x2f, 0xa8, 0xff, 0xff);

            // 属性の印
            var mark = Add(root, "Element", Size + 24f, Size + 12f, 22f, 22f);
            mark.gameObject.AddComponent<Image>();
            var beats = Add(root, "ElementBeats", Size + 24f, Size + 38f, 22f, 6f);
            beats.gameObject.AddComponent<Image>();

            var status = Add(root, "Status", 0f, Size + 78f, Size + 120f, 30f);
            Text(status, "", 20, Ui.Ink, TextAnchor.UpperLeft);

            var stand = root.AddComponent<UnitStand>();
            var so = new SerializedObject(stand);
            so.FindProperty("_art").objectReferenceValue = art.GetComponent<Image>();
            so.FindProperty("_hpFill").objectReferenceValue = fill.GetComponent<Image>();
            so.FindProperty("_hpBadge").objectReferenceValue = badge.GetComponent<Image>();
            so.FindProperty("_hpNumber").objectReferenceValue = num.GetComponent<Text>();
            so.FindProperty("_gaugeFill").objectReferenceValue = gauge.GetComponent<Image>();
            so.FindProperty("_glow").objectReferenceValue = glow.gameObject;
            so.FindProperty("_elementMark").objectReferenceValue = mark.GetComponent<Image>();
            so.FindProperty("_elementBeats").objectReferenceValue = beats.GetComponent<Image>();
            so.FindProperty("_status").objectReferenceValue = status.GetComponent<Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, Path);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log($"書き出した: {Path}。以後はここを Editor で直す");
        }

        private const string ScreenPath = Dir + "/BattleScreen.prefab";

        /// <summary>戦闘の画面を丸ごと Prefab に書き出す。⚠️ 一度だけ。</summary>
        [MenuItem("Egg Command/戦闘の画面を Prefab に書き出す")]
        public static void BuildScreen()
        {
            if (System.IO.File.Exists(ScreenPath))
            {
                Debug.LogWarning($"{ScreenPath} が既にある。手で直した位置を消さないよう、消してから実行する");
                return;
            }
            var stand = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            if (stand == null) { Debug.LogError("先に UnitStand.prefab を書き出す"); return; }

            var root = new GameObject("BattleScreen", typeof(RectTransform));
            var rr = (RectTransform)root.transform;
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;

            // 味方3体。⭐ ここの数値は初期値。以後は Editor で動かす
            var allies = new UnitStand[3];
            for (int i = 0; i < 3; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(stand, root.transform);
                go.name = $"Ally {i}";
                Put((RectTransform)go.transform, 60f, 150f + 300f * i, 1f);
                allies[i] = go.GetComponent<UnitStand>();
            }
            var foeGo = (GameObject)PrefabUtility.InstantiatePrefab(stand, root.transform);
            foeGo.name = "Foe";
            Put((RectTransform)foeGo.transform, 600f, 300f, 1.6f);

            // 手札
            float full = 1080f - 96f;
            float wide = full * 0.66f;
            float half = (full - 24f) / 2f;
            var slots = new SkillSlot[3];
            slots[0] = Skill(root, "Skill 0", (1080f - wide) / 2f, 1130f, wide, 130f, "button-lead", 34);
            slots[1] = Skill(root, "Skill 1", 48f, 1276f, half, 130f, "button", 28);
            slots[2] = Skill(root, "Skill 2", 48f + half + 24f, 1276f, half, 130f, "button", 28);

            var finish = Tap(root, "Finish", 48f, 1290f, full, 112f, "button-lead", "戻る");
            var pick = Tap(root, "Pick", 600f, 620f, 200f, 112f, "button", "選ぶ");

            var view = root.AddComponent<BattleView>();
            var so = new SerializedObject(view);
            var a = so.FindProperty("_allies");
            a.arraySize = 3;
            for (int i = 0; i < 3; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = allies[i];
            so.FindProperty("_foe").objectReferenceValue = foeGo.GetComponent<UnitStand>();
            var s = so.FindProperty("_skills");
            s.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var e = s.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Button").objectReferenceValue = slots[i].Button;
                e.FindPropertyRelative("Plate").objectReferenceValue = slots[i].Plate;
                e.FindPropertyRelative("Name").objectReferenceValue = slots[i].Name;
                e.FindPropertyRelative("Ct").objectReferenceValue = slots[i].Ct;
                e.FindPropertyRelative("CtPill").objectReferenceValue = slots[i].CtPill;
            }
            so.FindProperty("_finish").objectReferenceValue = finish;
            so.FindProperty("_pick").objectReferenceValue = pick;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            Debug.Log($"書き出した: {ScreenPath}");
        }

        private static void Put(RectTransform r, float left, float top, float scale)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(left, -top);
            r.localScale = new Vector3(scale, scale, 1f);
        }

        private static SkillSlot Skill(GameObject parent, string name,
            float left, float top, float width, float height, string skin, int size)
        {
            var button = Tap(parent, name, left, top, width, height, skin, "");
            var t = Add(button.gameObject, "Name", 8f, 16f, width - 16f, 44f);
            Text(t, "", size, Ui.OnLead, TextAnchor.UpperCenter);
            var pill = Add(button.gameObject, "CtPill", (width - 150f) / 2f, 70f, 150f, 40f);
            var pillImage = pill.gameObject.AddComponent<Image>();
            pillImage.sprite = Resources.Load<Sprite>("UI/pill");
            pillImage.type = Image.Type.Sliced;
            pillImage.color = new Color32(0x2b, 0x33, 0x50, 0xff);
            pillImage.raycastTarget = false;
            var ct = Add(pill.gameObject, "Ct", 0f, 0f, 150f, 40f);
            Text(ct, "", 22, Color.white, TextAnchor.MiddleCenter);
            return new SkillSlot
            {
                Button = button, Plate = button.GetComponent<Image>(),
                Name = t.GetComponent<Text>(), Ct = ct.GetComponent<Text>(), CtPill = pillImage,
            };
        }

        private static Button Tap(GameObject parent, string name,
            float left, float top, float width, float height, string skin, string label)
        {
            var rect = Add(parent, name, left, top, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/" + skin);
            image.type = Image.Type.Sliced;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (label.Length > 0)
            {
                var t = Add(rect.gameObject, "Label", 0f, 0f, width, height);
                Text(t, label, 32, Ui.OnLead, TextAnchor.MiddleCenter);
            }
            return button;
        }

        /// <summary>左上を原点に置く（Ui.Place と同じ約束）。</summary>
        private static RectTransform Add(GameObject parent, string name,
            float left, float top, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(width, height);
            r.anchoredPosition = new Vector2(left, -top);
            return r;
        }

        private static void Sprite(RectTransform rect, string path, Color color, bool sliced = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>(path);
            image.color = color;
            if (sliced) image.type = Image.Type.Sliced;
            image.raycastTarget = false;
        }

        private static void Text(RectTransform rect, string content, int size, Color color, TextAnchor anchor)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = Ui.TheFont;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
