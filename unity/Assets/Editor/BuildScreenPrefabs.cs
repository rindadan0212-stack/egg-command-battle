using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using EggCommand.View;
using EggCommand.Core;

namespace EggCommand.EditorTools
{
    /// <summary>画面と部品を Prefab に**一度だけ**書き出す足場。
    ///
    /// ⭐ ここで書く寸法は「初期値」でしかない。以後は Unity Editor で
    /// Prefab を開いてドラッグして決める。**コードへ戻さない。**
    ///
    /// ⚠️ 既にある Prefab は上書きしない（手で直した位置が消えるため）。
    /// 作り直したいときは、その Prefab を消してからもう一度走らせる。
    /// </summary>
    public static class BuildScreenPrefabs
    {
        private const string Dir = "Assets/Resources/Prefabs";

        [MenuItem("Egg Command/画面を Prefab に書き出す")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");

            int made = 0;
            made += One("Fanfare", BuildFanfare);
            made += One("Banner", BuildBanner);
            made += One("AppFrame", BuildFrame);
            // ⚠️ **HomeScreen より先に作る。**ホームは孵化枠と卵の札を Prefab として
            //    読み込むので、順が逆だと全部消して作り直した日に null で落ちる
            made += One("IncubatorSlot", BuildIncubatorSlot);
            made += One("EggCard", BuildEggCard);
            made += One("HomeScreen", BuildHome);
            made += One("EncounterCard", BuildEncounterCard);
            made += One("NestsScreen", BuildNests);
            made += One("CreatureCell", BuildCreatureCell);
            made += One("BoxScreen", BuildBox);
            made += One("BreedScreen", BuildBreed);
            made += One("CreaturePanel", BuildCreaturePanelPrefab);

            AssetDatabase.Refresh();
            Debug.Log(made == 0 ? "すべて既にある（何も書き出していない）" : $"{made} 個を書き出した: {Dir}");
        }

        private static int One(string name, System.Func<GameObject> build)
        {
            string path = $"{Dir}/{name}.prefab";
            if (System.IO.File.Exists(path)) return 0;
            var root = build();
            root.name = name;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return 1;
        }

        // ── 演出 ────────────────────────────────────────

        private static GameObject BuildFanfare()
        {
            var root = Screen("Fanfare");
            // 覆い。⚠️ ここでクリックを吸わないと、後ろの画面が押せてしまう
            var dim = Full(root, "Dim");
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0.04f, 0.06f, 0.12f, 0.62f);
            var close = dim.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;

            var pop = Add(root, "Pop", 140f, 620f, 800f, 800f);
            pop.pivot = new Vector2(0.5f, 0.5f);
            pop.anchorMin = pop.anchorMax = new Vector2(0.5f, 0.5f);
            pop.anchoredPosition = new Vector2(0f, 140f);

            var burst = Add(pop.gameObject, "Burst", 0f, 0f, 800f, 800f);
            burst.pivot = new Vector2(0.5f, 0.5f);
            burst.anchorMin = burst.anchorMax = new Vector2(0.5f, 0.5f);
            burst.anchoredPosition = Vector2.zero;
            Skin(burst, "circle", new Color(1f, 1f, 1f, 0.3f));

            var art = Add(pop.gameObject, "Art", 160f, 120f, 480f, 480f);
            art.gameObject.AddComponent<Image>().preserveAspect = true;

            var stars = Add(pop.gameObject, "Stars", 0f, 620f, 800f, 80f);
            Text(stars, "★★★", 56, Ui.Accent, TextAnchor.MiddleCenter);

            var line = Add(root, "Line", 40f, 1300f, 1000f, 200f);
            var lineText = Text(line, "", 54, Ui.Ink, TextAnchor.UpperCenter);
            lineText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Ui.Knockout(lineText, 5);

            var view = root.AddComponent<Fanfare>();
            var so = new SerializedObject(view);
            so.FindProperty("_pop").objectReferenceValue = pop;
            so.FindProperty("_art").objectReferenceValue = art.GetComponent<Image>();
            so.FindProperty("_burst").objectReferenceValue = burst.GetComponent<Image>();
            so.FindProperty("_stars").objectReferenceValue = stars.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_line").objectReferenceValue = lineText;
            so.FindProperty("_close").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject BuildBanner()
        {
            var root = Screen("Banner");
            // ⚠️ 全面は覆わない。後ろで何が起きたか（盤に残った軌跡）を隠さない
            var strip = Add(root, "Strip", 0f, 780f, Ui.W, 240f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.anchoredPosition = new Vector2(Ui.W / 2f, -780f);
            var image = strip.gameObject.AddComponent<Image>();
            image.color = new Color(0.04f, 0.06f, 0.12f, 0.72f);
            image.raycastTarget = true;   // ⭐ この間は下を押させない

            var line = Add(strip.gameObject, "Line", -Ui.W / 2f, 0f, Ui.W, 240f);
            Text(line, "", 58, Ui.Ink, TextAnchor.MiddleCenter);

            var view = root.AddComponent<BannerView>();
            var so = new SerializedObject(view);
            so.FindProperty("_strip").objectReferenceValue = strip;
            so.FindProperty("_line").objectReferenceValue = line.GetComponent<UnityEngine.UI.Text>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ── 器 ──────────────────────────────────────────

        private static GameObject BuildFrame()
        {
            var root = Screen("AppFrame");

            var body = Full(root, "Body");
            body.offsetMax = new Vector2(0f, -Ui.TopBarHeight);

            var bar = Add(root, "TopBar", 0f, 0f, Ui.W, Ui.TopBarHeight);
            var back = Tap(bar.gameObject, "Back", 12f, 10f, 112f, 112f, "button", "‹", 44);
            var title = Add(bar.gameObject, "Title", 140f, 0f, Ui.W - 280f, Ui.TopBarHeight);
            Text(title, "", 40, Ui.Ink, TextAnchor.MiddleCenter);
            var badge = Add(bar.gameObject, "Badge", Ui.W - 300f, 0f, 300f - Ui.Margin, Ui.TopBarHeight);
            Text(badge, "", 28, Ui.InkDim, TextAnchor.MiddleRight);

            var dock = Add(root, "Dock", 0f, Ui.H - Ui.DockHeight, Ui.W, Ui.DockHeight);
            float gap = 16f;
            float width = (Ui.W - Ui.Margin * 2f - gap * 3f) / 4f;
            float top = (Ui.DockHeight - 148f) / 2f;
            var panels = new DockPanel[4];
            for (int i = 0; i < 4; i++)
            {
                // ⭐ 主導線（探索）だけ塗る
                var button = Tap(dock.gameObject, $"Panel {i}",
                    Ui.Margin + (width + gap) * i, top, width, 148f, i == 0 ? "button-lead" : "button", "", 0);
                var name = Add(button.gameObject, "Name", 0f, 28f, width, 44f);
                Text(name, "", 32, Ui.Ink, TextAnchor.UpperCenter);
                var count = Add(button.gameObject, "Count", 0f, 84f, width, 36f);
                Text(count, "", 24, Ui.Ink, TextAnchor.UpperCenter);
                panels[i] = new DockPanel
                {
                    Button = button, Name = name.GetComponent<UnityEngine.UI.Text>(),
                    Count = count.GetComponent<UnityEngine.UI.Text>(),
                };
            }

            var view = root.AddComponent<FrameView>();
            var so = new SerializedObject(view);
            so.FindProperty("_back").objectReferenceValue = back;
            so.FindProperty("_title").objectReferenceValue = title.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_badge").objectReferenceValue = badge.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_dock").objectReferenceValue = dock.gameObject;
            so.FindProperty("_body").objectReferenceValue = body;
            var array = so.FindProperty("_panels");
            array.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var e = array.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Button").objectReferenceValue = panels[i].Button;
                e.FindPropertyRelative("Name").objectReferenceValue = panels[i].Name;
                e.FindPropertyRelative("Count").objectReferenceValue = panels[i].Count;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ── ホーム ──────────────────────────────────────

        private static GameObject BuildHome()
        {
            var root = Screen("HomeScreen");
            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/IncubatorSlot.prefab");
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/EggCard.prefab");
            float h = Ui.H - Ui.TopBarHeight;   // 器の下に入るぶん短い

            // ── 一番上: 溜まっている素材 ────────────────
            var top = Add(root, "Materials", 0f, 0f, Ui.W, 78f);
            Skin(top, "pill", new Color(1f, 1f, 1f, 0.75f), sliced: true);
            var matIcon = Add(top.gameObject, "Icon", Ui.Margin, 19f, 40f, 40f);
            Skin(matIcon, "circle", Ui.Accent);
            var matText = Add(top.gameObject, "Count", Ui.Margin + 56f, 0f, 400f, 78f);
            var materials = Text(matText, "0", 40, Ui.Ink, TextAnchor.MiddleLeft);

            // ── 上半分: 放置（横スクロール） ──────────────
            var strip = Add(root, "Idle", 0f, 90f, Ui.W, 470f);
            // ⚠️ 地面は画面幅の2倍ある。RectMask2D で切らないと画面外へはみ出す
            //    （検査が「画面外」と言うのはこの帯のこと。意図どおり）
            strip.gameObject.AddComponent<RectMask2D>();

            // ⭐ 地面は2枚ぶんの幅。左へ流して、1枚ぶん流れたら折り返す
            var ground = Add(strip.gameObject, "Ground", 0f, 396f, Ui.W * 2f, 40f);
            ground.gameObject.AddComponent<Image>().color = new Color32(0xf2, 0xb3, 0x4b, 0xff);
            for (int i = 0; i < 8; i++)
            {
                var tuft = Add(ground.gameObject, $"Tuft {i}", 90f + 260f * i, -26f, 46f, 26f);
                tuft.gameObject.AddComponent<Image>().color = new Color32(0x9a, 0xc9, 0x5e, 0xff);
            }

            var walkers = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var w = Add(strip.gameObject, $"Walker {i}", 0f, 0f, 160f, 160f);
                w.pivot = new Vector2(0.5f, 0f);
                w.anchoredPosition = new Vector2(120f + 130f * i, -396f);
                walkers[i] = w.gameObject.AddComponent<Image>();
                walkers[i].preserveAspect = true;
            }

            var slot = Add(strip.gameObject, "Enemy", 0f, 0f, 200f, 200f);
            slot.pivot = new Vector2(0.5f, 0f);
            slot.anchoredPosition = new Vector2(880f, -396f);
            var enemyImage = slot.gameObject.AddComponent<Image>();
            enemyImage.preserveAspect = true;

            var track = Add(strip.gameObject, "EnemyTrack", 740f, 176f, 280f, 18f);
            track.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
            var fill = Add(track.gameObject, "Fill", 0f, 0f, 280f, 18f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = Ui.Danger;
            fillImage.sprite = Ui.SkinSprite("pill");
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;

            var idle = strip.gameObject.AddComponent<IdleStrip>();
            var iso = new SerializedObject(idle);
            iso.FindProperty("_ground").objectReferenceValue = ground;
            Fill(iso, "_walkers", walkers);
            iso.FindProperty("_enemy").objectReferenceValue = enemyImage;
            iso.FindProperty("_enemyHp").objectReferenceValue = fillImage;
            iso.FindProperty("_enemySlot").objectReferenceValue = slot;
            iso.ApplyModifiedPropertiesWithoutUndo();

            // ── 下半分: 孵化器5枠を X に置く ──────────────
            var slots = new IncubatorSlot[Hatchery.Slots];
            var at = new Vector2[]
            {
                new Vector2( 70f, 620f), new Vector2(710f, 620f),
                new Vector2(390f, 840f),
                new Vector2( 70f, 1060f), new Vector2(710f, 1060f),
            };
            for (int i = 0; i < slots.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, root.transform);
                go.name = $"Slot {i}";
                var r = (RectTransform)go.transform;
                r.sizeDelta = new Vector2(300f, 400f);
                r.anchoredPosition = new Vector2(at[i].x, -at[i].y);
                r.localScale = new Vector3(0.8f, 0.8f, 1f);
                slots[i] = go.GetComponent<IncubatorSlot>();
            }

            // ── 在庫。⚠️ 常設しない。空き枠を押したときだけ開く ─────
            var picker = Full(root, "Picker");
            var dim = picker.gameObject.AddComponent<Image>();
            dim.color = new Color(0.04f, 0.06f, 0.12f, 0.72f);
            var close = picker.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            var shelf = Scroll(picker.gameObject, "Shelf", 0f, 180f, Ui.W, h - 400f,
                new Vector2(228f, 268f), 4);
            picker.gameObject.SetActive(false);

            var template = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, root.transform);
            template.name = "EggCard (型)";
            template.SetActive(false);

            var view = root.AddComponent<HomeView>();
            var so = new SerializedObject(view);
            so.FindProperty("_materials").objectReferenceValue = materials;
            so.FindProperty("_idle").objectReferenceValue = idle;
            Fill(so, "_slots", slots);
            so.FindProperty("_picker").objectReferenceValue = picker.gameObject;
            so.FindProperty("_shelf").objectReferenceValue = shelf;
            so.FindProperty("_eggCard").objectReferenceValue = template.GetComponent<EggCard>();
            so.FindProperty("_pickerClose").objectReferenceValue = close;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ── 探索 ────────────────────────────────────────

        private static GameObject BuildEncounterCard()
        {
            var root = new GameObject("EncounterCard", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            Anchor(rect, 984f, 300f);
            Skin(rect, "panel", Color.white, sliced: true);
            var button = root.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();

            var art = Add(root, "Art", 36f, 36f, 228f, 228f);
            art.gameObject.AddComponent<Image>().preserveAspect = true;

            // ⚠️ 名前も素質も出さない。⭐ 手掛かりはこの数だけ
            var tag = Add(root, "LvTag", 312f, 92f, 90f, 40f);
            Text(tag, "Lv", 28, Ui.InkDim, TextAnchor.UpperLeft);
            var level = Add(root, "Level", 400f, 62f, 300f, 100f);
            Text(level, "", 76, Ui.Ink, TextAnchor.UpperLeft);

            var view = root.AddComponent<EncounterCard>();
            var so = new SerializedObject(view);
            so.FindProperty("_art").objectReferenceValue = art.GetComponent<Image>();
            so.FindProperty("_level").objectReferenceValue = level.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_button").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject BuildNests()
        {
            var root = Screen("NestsScreen");
            var card = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/EncounterCard.prefab");

            var cards = new EncounterCard[Encounters.Shown];
            for (int i = 0; i < cards.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(card, root.transform);
                go.name = $"Card {i}";
                ((RectTransform)go.transform).anchoredPosition = new Vector2(Ui.Margin, -(24f + 324f * i));
                cards[i] = go.GetComponent<EncounterCard>();
            }

            // ⭐ この画面で塗るのはここだけ。輪の目的地は1つしかない
            var boss = Tap(root, "Boss", Ui.Margin, 24f + 324f * cards.Length, 984f, 300f,
                "button-lead", "", 0);
            var bossArt = Add(boss.gameObject, "Art", 36f, 36f, 228f, 228f);
            bossArt.gameObject.AddComponent<Image>().preserveAspect = true;
            var bossName = Add(boss.gameObject, "Name", 312f, 108f, 600f, 90f);
            Text(bossName, Nests.BossName, 56, Ui.Ink, TextAnchor.UpperLeft);

            var view = root.AddComponent<NestsView>();
            var so = new SerializedObject(view);
            Fill(so, "_cards", cards);
            so.FindProperty("_bossArt").objectReferenceValue = bossArt.GetComponent<Image>();
            so.FindProperty("_boss").objectReferenceValue = boss;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ── 孵化 ────────────────────────────────────────

        private static GameObject BuildIncubatorSlot()
        {
            var root = new GameObject("IncubatorSlot", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            Anchor(rect, 187f, 272f);
            Skin(rect, "tile", Color.white, sliced: true);
            var button = root.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();

            var empty = Add(root, "Empty", 0f, 0f, 187f, 272f);
            var stand = Add(empty.gameObject, "Stand", 37f, 200f, 112f, 14f);
            stand.gameObject.AddComponent<Image>().color = new Color32(0xc9, 0xa4, 0x6a, 0xff);

            var filled = Add(root, "Filled", 0f, 0f, 187f, 272f);
            var art = Add(filled.gameObject, "Art", 45f, 16f, 96f, 96f);
            art.gameObject.AddComponent<Image>().preserveAspect = true;
            var ready = Add(filled.gameObject, "Ready", 33f, 4f, 120f, 120f);
            Skin(ready, "circle", new Color(0.18f, 0.66f, 0.29f, 0.35f));
            ready.SetSiblingIndex(0);   // ⚠️ 絵の後ろへ。前に出すと卵が隠れる
            var stars = Add(filled.gameObject, "Stars", 0f, 118f, 187f, 34f);
            Text(stars, "", 22, Ui.AccentInk, TextAnchor.UpperCenter);
            var track = Add(filled.gameObject, "Track", 16f, 160f, 155f, 16f);
            track.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);
            var fill = Add(track.gameObject, "Fill", 0f, 0f, 155f, 16f);
            fill.gameObject.AddComponent<Image>().color = Ui.Accent;
            var clock = Add(filled.gameObject, "Clock", 0f, 186f, 187f, 40f);
            Text(clock, "", 24, Ui.Ink, TextAnchor.UpperCenter);

            var view = root.AddComponent<IncubatorSlot>();
            var so = new SerializedObject(view);
            so.FindProperty("_filled").objectReferenceValue = filled.gameObject;
            so.FindProperty("_empty").objectReferenceValue = empty.gameObject;
            so.FindProperty("_art").objectReferenceValue = art.GetComponent<Image>();
            so.FindProperty("_stars").objectReferenceValue = stars.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_fill").objectReferenceValue = fill.GetComponent<Image>();
            so.FindProperty("_clock").objectReferenceValue = clock.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_ready").objectReferenceValue = ready.gameObject;
            so.FindProperty("_button").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject BuildEggCard()
        {
            var root = new GameObject("EggCard", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            Anchor(rect, 228f, 268f);
            Skin(rect, "panel", Color.white, sliced: true);
            var button = root.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();

            var art = Add(root, "Art", 66f, 16f, 96f, 96f);
            art.gameObject.AddComponent<Image>().preserveAspect = true;
            var element = Add(root, "Element", 190f, 14f, 24f, 24f);
            Skin(element, "circle", Color.white);
            var stars = Add(root, "Stars", 0f, 116f, 228f, 34f);
            Text(stars, "", 22, Ui.AccentInk, TextAnchor.UpperCenter);
            var wild = Add(root, "Wild", 0f, 150f, 228f, 48f);
            Text(wild, "", 34, Ui.Ink, TextAnchor.UpperCenter);
            var wait = Add(root, "Wait", 0f, 202f, 228f, 34f);
            Text(wait, "", 22, Ui.InkDim, TextAnchor.UpperCenter);

            var view = root.AddComponent<EggCard>();
            var so = new SerializedObject(view);
            so.FindProperty("_art").objectReferenceValue = art.GetComponent<Image>();
            so.FindProperty("_stars").objectReferenceValue = stars.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_wild").objectReferenceValue = wild.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_element").objectReferenceValue = element.GetComponent<Image>();
            so.FindProperty("_wait").objectReferenceValue = wait.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_button").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ── BOX / 配合 ──────────────────────────────────

        private static GameObject BuildCreatureCell()
        {
            var root = new GameObject("CreatureCell", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            Anchor(rect, 228f, 200f);
            Skin(rect, "panel", Color.white, sliced: true);
            var button = root.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();

            // ⭐ **選んでいる升は4辺で囲む。**（作者の指示「黄色く囲む」）
            // ⚠️ 上辺の帯1本だと、隣との境目にしか見えず「囲む」になっていない。
            // ⚠️ 四隅のカギ括弧にはしない ── 戦闘の「狙い先」がその形なので意味が混ざる。
            var mark = Add(root, "Mark", 0f, 0f, 228f, 200f);
            Frame(mark, 228f, 200f);
            var art = Add(root, "Art", 70f, 30f, 88f, 88f);
            art.gameObject.AddComponent<Image>().preserveAspect = true;
            var element = Add(root, "Element", 10f, 10f, 22f, 22f);
            Skin(element, "circle", Color.white);
            var wild = Add(root, "Wild", 6f, 130f, 216f, 44f);
            Text(wild, "", 30, Ui.Ink, TextAnchor.UpperCenter);

            var view = root.AddComponent<CreatureCell>();
            var so = new SerializedObject(view);
            so.FindProperty("_art").objectReferenceValue = art.GetComponent<Image>();
            so.FindProperty("_element").objectReferenceValue = element.GetComponent<Image>();
            so.FindProperty("_wild").objectReferenceValue = wild.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_mark").objectReferenceValue = mark.gameObject;
            so.FindProperty("_button").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>BOX の押しどころ。⚠️ 並び順が <see cref="BoxView"/> の Repurpose の番号と対応する。</summary>
        private static readonly string[] ActionNames =
            { "Party", "Spend 0", "Spend 1", "Spend 2", "Spend 3", "Release" };

        private static GameObject BuildBox()
        {
            var root = Screen("BoxScreen");
            var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/CreatureCell.prefab");
            float full = Ui.W - Ui.Margin * 2f;

            // ⚠️ 札(706)＋隙間(16)＋押しどころ(112)＋余白(26)。PatchScreenPrefabs の BoxDetailHeight と揃える
            const float DetailHeight = 860f;
            var detail = Add(root, "Detail", Ui.Margin, 12f, full, DetailHeight);
            Skin(detail, "panel", Color.white, sliced: true);

            // ⭐ 中身は配合の親札と同じ部品。並びは BuildCreaturePanel が持つ
            var panel = BuildCreaturePanel.Build(detail, "Panel", BuildCreaturePanel.Wide(), true);

            // ⭐ 押しどころは札の下に1列。⚠️ 札の中に混ぜない（見る場所と押す場所を分ける）
            var actions = new Button[6];
            float actionW = (full - 52f - 12f * 5f) / 6f;
            float actionTop = DetailHeight - Ui.Tap - 26f;
            for (int i = 0; i < actions.Length; i++)
            {
                actions[i] = Tap(detail.gameObject, ActionNames[i], 26f + (actionW + 12f) * i,
                    actionTop, actionW, Ui.Tap, i == 5 ? "button-danger" : "button", "", 0);
                var label = Add(actions[i].gameObject, "Label", 0f, 0f, actionW, Ui.Tap);
                Text(label, i == 5 ? "逃がす" : "", 20, Ui.OnLead, TextAnchor.MiddleCenter);
            }
            var party = actions[0];
            var partyLabel = party.transform.Find("Label").gameObject;
            var release = actions[5];
            var spend = new Button[] { actions[1], actions[2], actions[3], actions[4] };

            // ⚠️ 詳細の下端(12+DetailHeight)より下。⭐ 詳細を伸ばしたら札も下がる
            float TabTop = 12f + DetailHeight + 24f;
            var tabs = new Button[Storages.SortKeys.Length];
            float tabW = (full - 12f * (tabs.Length - 1)) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i] = Tap(root, $"Sort {i}", Ui.Margin + (tabW + 12f) * i, TabTop, tabW, Ui.Tap,
                    "button", "", 0);
                var label = Add(tabs[i].gameObject, "Label", 0f, 0f, tabW, Ui.Tap);
                Text(label, "", 20, Ui.OnLead, TextAnchor.MiddleCenter);
            }

            float gridTop = TabTop + Ui.Tap + 12f;
            var grid = Scroll(root, "Grid", 0f, gridTop, Ui.W, Ui.H - Ui.TopBarHeight - gridTop,
                new Vector2(228f, 200f), 4);
            var template = (GameObject)PrefabUtility.InstantiatePrefab(cellPrefab, root.transform);
            template.name = "CreatureCell (型)";
            template.SetActive(false);

            var view = root.AddComponent<BoxView>();
            var so = new SerializedObject(view);
            so.FindProperty("_detail").objectReferenceValue = detail.gameObject;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_party").objectReferenceValue = party;
            so.FindProperty("_partyLabel").objectReferenceValue = partyLabel.GetComponent<UnityEngine.UI.Text>();
            so.FindProperty("_release").objectReferenceValue = release;
            Fill(so, "_spend", spend);
            Fill(so, "_sortTabs", tabs);
            so.FindProperty("_grid").objectReferenceValue = grid;
            so.FindProperty("_cell").objectReferenceValue = template.GetComponent<CreatureCell>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject BuildBreed()
        {
            var root = Screen("BreedScreen");
            var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/CreatureCell.prefab");
            float full = Ui.W - Ui.Margin * 2f;
            float half = (full - 64f) / 2f;

            var parents = new ParentSlot[2];
            for (int i = 0; i < 2; i++)
            {
                var card = Add(root, i == 0 ? "ParentA" : "ParentB",
                    Ui.Margin + (half + 64f) * i, 12f, half, 200f);
                Skin(card, "panel", Color.white, sliced: true);

                // ⭐ 空き枠は**札いっぱい**に取って、真ん中で行き先を言う。
                // ⚠️ 茶色い帯1本だった頃は、540 の白い箱に線が1本あるだけで
                //    何をすればいいのか読めなかった（実測 2026-08-19）。
                var empty = Add(card.gameObject, "Empty", 0f, 0f, half, 200f);
                var say = Add(empty.gameObject, "Say", 0f, 70f, half, 60f);
                Text(say, "下から選ぶ", 30, Ui.InkFaint, TextAnchor.MiddleCenter);

                var filled = Add(card.gameObject, "Filled", 0f, 0f, half, 200f);
                // ⭐ BOX の詳細と同じ部品。⚠️ 幅が違うので Narrow の寸法で組む
                var panel = BuildCreaturePanel.Build(filled, "Panel", BuildCreaturePanel.Narrow(), true);

                parents[i] = new ParentSlot
                {
                    Filled = filled.gameObject, Empty = empty.gameObject, Panel = panel,
                };
            }
            var plus = Add(root, "Plus", Ui.Margin + half, 12f, 64f, 200f);
            Text(plus, "＋", 48, Ui.Accent, TextAnchor.MiddleCenter);

            var result = Add(root, "Result", Ui.Margin, 224f, full, 128f);
            Skin(result, "panel", Color.white, sliced: true);
            var egg = Add(result.gameObject, "Egg", 20f, 16f, 96f, 96f);
            egg.gameObject.AddComponent<Image>().preserveAspect = true;
            var species = Add(result.gameObject, "Species", 132f, 20f, full - 220f, 36f);
            var poolText = Add(result.gameObject, "Skills", 132f, 60f, full - 160f, 50f);
            var mutable = Add(result.gameObject, "Mut", full - 64f, 20f, 44f, 16f);
            mutable.gameObject.AddComponent<Image>().color = Ui.Accent;

            var breed = Tap(root, "Breed", Ui.Margin, 364f, full, Ui.Tap, "button-lead", "配合する", 32);

            float gridTop = 364f + Ui.Tap + 16f;
            var grid = Scroll(root, "Grid", 0f, gridTop, Ui.W, Ui.H - Ui.TopBarHeight - gridTop,
                new Vector2(228f, 200f), 4);
            var template = (GameObject)PrefabUtility.InstantiatePrefab(cellPrefab, root.transform);
            template.name = "CreatureCell (型)";
            template.SetActive(false);

            var view = root.AddComponent<BreedView>();
            var so = new SerializedObject(view);
            var slots = so.FindProperty("_parents");
            slots.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                var e = slots.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Filled").objectReferenceValue = parents[i].Filled;
                e.FindPropertyRelative("Empty").objectReferenceValue = parents[i].Empty;
                e.FindPropertyRelative("Panel").objectReferenceValue = parents[i].Panel;
            }
            so.FindProperty("_result").objectReferenceValue = result.gameObject;
            so.FindProperty("_resultEgg").objectReferenceValue = egg.GetComponent<Image>();
            so.FindProperty("_resultSpecies").objectReferenceValue =
                Text(species, "", 28, Ui.Ink, TextAnchor.UpperLeft);
            so.FindProperty("_resultSkills").objectReferenceValue =
                Text(poolText, "", 22, Ui.InkDim, TextAnchor.UpperLeft);
            so.FindProperty("_resultMutable").objectReferenceValue = mutable.gameObject;
            so.FindProperty("_breed").objectReferenceValue = breed;
            so.FindProperty("_grid").objectReferenceValue = grid;
            so.FindProperty("_cell").objectReferenceValue = template.GetComponent<CreatureCell>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>ステの札だけを単体で書き出す。⭐ **長押しの覆いが読む。**
        ///
        /// ⚠️ 覆いの側で並びを書き直さないため。画面ごとに組み立てを書くと、
        /// 同じ個体が画面によって違う顔になる（配合で実際に起きた）。
        /// ⭐ 中身は BOX の詳細とまったく同じ <see cref="BuildCreaturePanel.Wide"/>。</summary>
        private static GameObject BuildCreaturePanelPrefab()
        {
            var root = new GameObject("CreaturePanel", typeof(RectTransform));
            var panel = BuildCreaturePanel.Build(root.transform, "Panel",
                BuildCreaturePanel.Wide(), true);
            // ⚠️ 入れ子にしない。⭐ 読む側が Instantiate してすぐ Bind できる形にする
            var inner = (RectTransform)panel.transform;
            inner.SetParent(null, false);
            Object.DestroyImmediate(root);
            inner.gameObject.name = "CreaturePanel";
            return inner.gameObject;
        }

        private static GameObject BuildStealResult()
        {
            var root = Screen("StealResult");
            float full = Ui.W - Ui.Margin * 2f;
            float top = Ui.H - Ui.TopBarHeight - 168f;
            var take = Tap(root, "Take", Ui.Margin, top, full, Ui.Tap, "button-lead", "卵を持ち帰る", 32);
            var fight = Tap(root, "Fight", Ui.Margin, top, full, Ui.Tap, "button-danger", "戦闘へ", 32);

            return root;
        }

        // ── 道具 ────────────────────────────────────────

        private static GameObject Screen(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            return root;
        }

        private static RectTransform Full(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            return r;
        }

        /// <summary>スクロールする入れ物。⭐ 中身は GridLayoutGroup が並べる。
        /// ⚠️ Mask を使わない（透明な絵だと中身が消える）。RectMask2D にする。</summary>
        /// <summary>中を空けた枠を4本の帯で描く。
        /// ⚠️ 四角い輪郭の絵が素材に無い（`Resources/UI/NOTICE.md`）ので、帯で組む。
        /// ⭐ 太さは指で触れる升に対して 8 ── 細すぎると選択が読めない。</summary>
        private static void Frame(RectTransform box, float width, float height)
        {
            const float Thick = 8f;
            Bar(box, "上", 0f, 0f, width, Thick);
            Bar(box, "下", 0f, height - Thick, width, Thick);
            Bar(box, "左", 0f, Thick, Thick, height - Thick * 2f);
            Bar(box, "右", width - Thick, Thick, Thick, height - Thick * 2f);
        }

        private static void Bar(RectTransform parent, string name,
            float left, float top, float width, float height)
        {
            var rect = Add(parent.gameObject, name, left, top, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Ui.Accent;
            image.raycastTarget = false;
        }

        private static RectTransform Scroll(GameObject parent, string name,
            float left, float top, float width, float height, Vector2 cell, int columns)
        {
            var viewport = Add(parent, name, left, top, width, height);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            var content = Add(viewport.gameObject, "Content", 0f, 0f, width, height);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset((int)Ui.Margin, (int)Ui.Margin, 8, 24);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        private static void Fill(SerializedObject so, string field, System.Array items)
        {
            var array = so.FindProperty(field);
            array.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = (Object)items.GetValue(i);
            }
        }

        private static Button Tap(GameObject parent, string name, float left, float top,
            float width, float height, string skin, string label, int size)
        {
            var rect = Add(parent, name, left, top, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/" + skin);
            image.type = Image.Type.Sliced;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (label.Length > 0 && size > 0)
            {
                var t = Add(rect.gameObject, "Label", 0f, 0f, width, height);
                Text(t, label, size, Ui.OnLead, TextAnchor.MiddleCenter);
            }
            return button;
        }

        private static Button Tap(RectTransform parent, string name, float left, float top,
            float width, float height, string skin, string label, int size) =>
            Tap(parent.gameObject, name, left, top, width, height, skin, label, size);

        private static Button Tap(GameObject parent, string name, float left, float top,
            float width, float height, string skin, string label) =>
            Tap(parent, name, left, top, width, height, skin, label, 32);

        /// <summary>左上を原点に置く（Ui.Place と同じ約束）。</summary>
        private static RectTransform Add(GameObject parent, string name,
            float left, float top, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var r = (RectTransform)go.transform;
            Anchor(r, width, height);
            r.anchoredPosition = new Vector2(left, -top);
            return r;
        }

        private static void Anchor(RectTransform r, float width, float height)
        {
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(width, height);
        }

        private static void Skin(RectTransform rect, string path, Color color, bool sliced = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/" + path);
            image.color = color;
            if (sliced) image.type = Image.Type.Sliced;
        }

        private static UnityEngine.UI.Text Text(RectTransform rect, string content, int size,
            Color color, TextAnchor anchor)
        {
            var text = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
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
