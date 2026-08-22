using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using EggCommand.View;

namespace EggCommand.EditorTools
{
    /// <summary>既にある Prefab に**足りない部品だけ**を足す。
    ///
    /// ⭐ **役割の境目はここ1本。**
    /// <code>
    ///   コード … 部品を「作る」と「値を流す」
    ///   人   … 「置く」と「飾る」（位置・大きさ・色・絵）
    /// </code>
    ///
    /// ⚠️ **既にある部品の位置・大きさには一切触らない。**
    /// 触っていた頃は、Unity で並べ直しても次にこれを走らせた瞬間に元へ戻っていた。
    /// ⭐ 新しい部品は初期位置に出るだけ。動かしたらその位置がずっと残る。
    ///
    /// ⚠️ 初期位置に戻したいときだけ「画面を作り直す」を選ぶ（**手で置いたものが消える**）。
    /// </summary>
    public static class PatchScreenPrefabs
    {
        private const string Dir = "Assets/Resources/Prefabs";

        /// <summary>初期位置へ戻してよいか。⚠️ 既定は false（人が置いた位置を守る）。</summary>
        private static bool _rebuild;

        /// <summary>この回に**新しく作った**部品。⭐ ここに居るものだけ位置を書いてよい。</summary>
        private static readonly HashSet<RectTransform> Fresh = new HashSet<RectTransform>();

        [MenuItem("Egg Command/画面に足りない部品を足す")]
        public static void PatchAll() => Run(false);

        /// <summary>⚠️ **手で置いたものが消える。**寸法を作り直したいときだけ。</summary>
        [MenuItem("Egg Command/画面を作り直す（⚠️ 手で置いたものが消えます）")]
        public static void RebuildAll()
        {
            if (!EditorUtility.DisplayDialog("画面を作り直す",
                "Unity で動かした位置・大きさが、すべて初期値に戻ります。\n\n"
                + "飾りを直したいだけなら「画面に足りない部品を足す」を選んでください。",
                "作り直す", "やめる")) return;
            Run(true);
        }

        private static void Run(bool rebuild)
        {
            _rebuild = rebuild;
            Fresh.Clear();
            int touched = 0;
            touched += Patch("EncounterCard", PatchEncounterCard);
            touched += Patch("CreatureCell", PatchCreatureCell);
            touched += Patch("BoxScreen", PatchBox);
            touched += Patch("BreedScreen", PatchBreed);
            touched += Patch("UnitStand", PatchUnitStand);
            touched += Patch("BattleScreen", PatchBattle);
            touched += Patch("HomeScreen", PatchHome);
            touched += Patch("AppFrame", PatchFrame);
            AssetDatabase.Refresh();
            _rebuild = false;
            Debug.Log(touched == 0
                ? "足すものは無かった（手で置いた位置はそのまま）"
                : $"{touched} 個の画面に足した（手で置いた位置はそのまま）");
        }

        private static int Patch(string name, System.Func<GameObject, bool> patch)
        {
            string path = $"{Dir}/{name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) { Debug.LogError($"{path} が読めない"); return 0; }
            // ⚠️ **必ず閉じる。**patch が投げたまま開きっぱなしにすると、
            //    その Prefab が Unity で開けなくなり、以降の画面も全部止まる
            try
            {
                bool changed = patch(root);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                return changed ? 1 : 0;
            }
            catch (System.Exception error)
            {
                Debug.LogError($"{name} の途中で止まった: {error.Message}");
                return 0;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
            if (MayPlace(clock))
            {
                clock.anchorMin = new Vector2(1f, 1f);
                clock.anchorMax = new Vector2(1f, 1f);
                clock.pivot = new Vector2(1f, 1f);
                clock.sizeDelta = new Vector2(248f, 56f);
                clock.anchoredPosition = new Vector2(-36f, -122f);   // Lv の数字と高さを揃える
            }

            var track = (RectTransform)Find(root, "Drain Track");
            if (MayPlace(track))
            {
                track.anchorMin = new Vector2(0f, 0f);
                track.anchorMax = new Vector2(1f, 0f);
                track.pivot = new Vector2(0.5f, 0f);
                track.offsetMin = new Vector2(36f, 24f);             // 下端から 24
                track.offsetMax = new Vector2(-36f, 38f);            // 厚み 14
            }

            // ⭐ 盗んだ回数（守りの固さ）。時計の下、同じ右端に揃える
            if (Find(root, "Raids") == null)
            {
                var made5 = Add(root.transform, "Raids", 0f, 0f, 248f, 48f);
                Label(made5, "", 32, Ui.AccentInk, TextAnchor.MiddleRight);
            }
            var raids = (RectTransform)Find(root, "Raids");
            if (MayPlace(raids))
            {
                raids.anchorMin = new Vector2(1f, 1f);
                raids.anchorMax = new Vector2(1f, 1f);
                raids.pivot = new Vector2(1f, 1f);
                raids.sizeDelta = new Vector2(248f, 48f);
                raids.anchoredPosition = new Vector2(-36f, -186f);
            }

            // ⚠️ 帯の中身は器いっぱいに広げる。これは飾りではなく仕組み（fillAmount で減らす）
            var bar = (RectTransform)Find(root, "Drain");
            // ⚠️ 「Left」だけ残して「Drain」を消すと、上の再生成が走らないのでここが null になる
            if (bar == null)
            {
                Debug.LogError("巣の札に Drain（残り時間の帯）が無い。"
                    + "「画面を作り直す」で戻せる");
                return false;
            }
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = Vector2.one;
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;

            var view = root.GetComponent<EncounterCard>();
            if (view == null)
            {
                Debug.LogError("巣の札 の根から EncounterCard が外れている");
                return false;
            }
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
            if (view == null)
            {
                Debug.LogError("個体の升 の根から CreatureCell が外れている");
                return false;
            }
            var so = new SerializedObject(view);
            so.FindProperty("_trait").objectReferenceValue = mark.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── BOX の詳細: 特性 ────────────────────────────

        /// <summary>BOX の詳細を、作者のラフ図（2026-08-18）の並びに載せ替える。
        ///
        /// ⭐ 中身は <see cref="BuildCreaturePanel"/> が持つ札1つに置き換える。
        /// ⚠️ 古い並び（絵の右に名前、左にステ、右に技）の部品は**消してから**作る。
        /// 残すと、見えない字が押しどころの下に溜まる。
        /// ⭐ 押しどころ（出撃・餌・合成・そだてる・技を鍛える・逃がす）は札の外、下の1列へ。</summary>
        /// ⚠️ 札（706）＋隙間 16 ＋押しどころ 112 ＋余白 26。
        /// ⭐ 札の高さは BuildCreaturePanel.Wide が持つ。ここはそこから足した数。
        private const float BoxDetailHeight = 860f;

        private static bool PatchBox(GameObject root)
        {
            var detail = Find(root, "Detail") as RectTransform;
            if (detail == null) { Debug.LogError("BoxScreen に Detail が無い"); return false; }
            if (MayPlace(detail)) detail.sizeDelta = new Vector2(detail.sizeDelta.x, BoxDetailHeight);

            // ⚠️ 古い並びの部品。⭐ 名前で消す（押しどころは消さない）
            Drop(detail, "Art", "Element", "Name", "Id", "Level", "Slant", "Trait", "Point");
            for (int i = 0; i < 8; i++) Drop(detail, $"K {i}", $"V {i}", $"T {i}", $"S {i}", $"SC {i}");

            var panel = BuildCreaturePanel.Build(detail, "Panel", BuildCreaturePanel.Wide(), _rebuild);

            // ⭐ 押しどころは1列6枚。⚠️ 並び順が BoxView の Repurpose の番号と対応する
            var names = new[] { "Party", "Spend 0", "Spend 1", "Spend 2", "Spend 3", "Release" };
            float width = detail.sizeDelta.x;
            float slot = (width - 52f - 12f * 5f) / 6f;
            for (int i = 0; i < names.Length; i++)
            {
                var button = Find(detail.gameObject, names[i]) as RectTransform;
                if (button == null) continue;
                // ⚠️ 器の枠のぶん、下に余白を残す（枠の線が札を切って見える）
                Place(button, 26f + (slot + 12f) * i, BoxDetailHeight - Ui.Tap - 26f, slot, Ui.Tap);
                foreach (var text in button.GetComponentsInChildren<Text>(true))
                {
                    var label = (RectTransform)text.transform;
                    // ⚠️ **字の大きさは人の担当。**門の外に置いていた頃は、Unity で
                    //    大きくしても押すたびに 20 へ戻っていた
                    if (MayPlace(label)) text.fontSize = 20;
                    if (label != button) Place(label, 0f, 0f, slot, Ui.Tap);
                }
            }

            // ⭐ 札が伸びたぶん、下の並べ替えと一覧を押し下げる
            float tabTop = 12f + BoxDetailHeight + 24f;
            for (int i = 0; i < 8; i++)
            {
                var tab = Find(root, $"Sort {i}") as RectTransform;
                if (tab != null) Place(tab, tab.anchoredPosition.x, tabTop, tab.sizeDelta.x, tab.sizeDelta.y);
            }
            var grid = Find(root, "Grid") as RectTransform;
            if (grid != null)
            {
                float gridTop = tabTop + Ui.Tap + 18f;
                // ⚠️ 下端は動かさない（一覧が画面外へ伸びる）
                float bottom = -grid.anchoredPosition.y + grid.sizeDelta.y;
                Place(grid, grid.anchoredPosition.x, gridTop, grid.sizeDelta.x, bottom - gridTop);
            }

            var view = root.GetComponent<BoxView>();
            if (view == null)
            {
                Debug.LogError("BOX の根から BoxView が外れている");
                return false;
            }
            var so = new SerializedObject(view);
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── 配合: 親の札 ────────────────────────────────

        /// <summary>親の札も BOX と同じ部品にする（2026-08-18）。
        /// ⚠️ 幅が 460 しかないので、絵と表を横に並べず縦に積む（Narrow の寸法）。</summary>
        private const float BreedCardHeight = 524f;
        private const float BreedStackTop = 554f;

        private static bool PatchBreed(GameObject root)
        {
            var view = root.GetComponent<BreedView>();
            if (view == null)
            {
                Debug.LogError("配合 の根から BreedView が外れている");
                return false;
            }
            var so = new SerializedObject(view);
            var slots = so.FindProperty("_parents");

            foreach (var name in new[] { "ParentA", "ParentB" })
            {
                var card = Find(root, name) as RectTransform;
                if (card == null) continue;
                if (MayPlace(card)) card.sizeDelta = new Vector2(card.sizeDelta.x, BreedCardHeight);
                var filled = Find(card.gameObject, "Filled") as RectTransform;
                if (filled == null) continue;
                if (MayPlace(filled))
                    filled.sizeDelta = new Vector2(filled.sizeDelta.x, BreedCardHeight);
            }

            for (int i = 0; i < slots.arraySize; i++)
            {
                var element = slots.GetArrayElementAtIndex(i);
                var card = Find(root, i == 0 ? "ParentA" : "ParentB");
                if (card == null) continue;
                var filled = Find(card.gameObject, "Filled") as RectTransform;
                if (filled == null) continue;

                // ⚠️ 古い並びの部品を消す
                Drop(filled, "Art", "Element", "Name", "Wild", "Skills", $"Slant {i}", $"Trait {i}");
                for (int k = 0; k < 8; k++) Drop(filled, $"Stat {k}");

                var panel = BuildCreaturePanel.Build(filled, "Panel", BuildCreaturePanel.Narrow(), _rebuild);
                element.FindPropertyRelative("Panel").objectReferenceValue = panel;
            }

            var plus = Find(root, "Plus") as RectTransform;
            if (plus != null) Place(plus, plus.anchoredPosition.x, 12f, plus.sizeDelta.x, BreedCardHeight);
            var stack = Find(root, "Stack") as RectTransform;
            if (stack != null)
            {
                if (MayPlace(stack))
                    stack.anchoredPosition = new Vector2(stack.anchoredPosition.x, -BreedStackTop);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── 戦闘に立つ体: 押して狙う ────────────────────

        /// <summary>体そのものを押して狙い先にできるようにする（2026-08-18）。
        ///
        /// ⚠️ それまでは「選ぶ」という札が1枚あるだけで、押しても**先頭の生存者**を返していた。
        /// ⭐ 3体並ぶ雑魚戦でも味方への強化でも、**押した体が狙い先**になる形に変える。</summary>
        private static bool PatchUnitStand(GameObject root)
        {
            var view = root.GetComponent<UnitStand>();
            if (view == null) { Debug.LogError("UnitStand に部品が無い"); return false; }

            // ⭐ 狙われている印は**四隅のカギ括弧**。
            // ⚠️ 円で囲うと体を覆ってしまう（circle-outline は中まで塗りつぶしの絵）。
            // ⚠️ 行動中の光（Glow＝背後の丸）と形を変える。同じ形だと意味が混ざる。
            // ⚠️ **毎回作り直す。**寸法を変えた版が古いまま残ると、
            //    「直したのに直らない」に見える（実際そうなった）。飾りなので作り直して安全
            var stale = Find(root, "Target Mark");
            if (stale != null && MayDrop()) Object.DestroyImmediate(stale.gameObject);
            if (Find(root, "Target Mark") == null)
            {
                // ⚠️ 絵の 200×200 より少しだけ外。⭐ 下は HP の帯（y 206）に掛からない高さで止める
                const float BoxW = 216f, BoxH = 204f, Arm = 34f, Thick = 6f;
                var mark = Add(root.transform, "Target Mark", -8f, -6f, BoxW, BoxH);
                for (int i = 0; i < 4; i++)
                {
                    bool right = i == 1 || i == 3;
                    bool bottom = i >= 2;
                    float x = right ? BoxW - Arm : 0f;
                    float y = bottom ? BoxH - Thick : 0f;
                    Bar(mark, $"横 {i}", x, y, Arm, Thick);
                    Bar(mark, $"縦 {i}", right ? BoxW - Thick : 0f, bottom ? BoxH - Arm : 0f, Thick, Arm);
                }
            }
            // ⚠️ 印は絵より後ろに置かない（体の下に隠れる）
            var target = (RectTransform)Find(root, "Target Mark");
            if (MayPlace(target)) target.SetAsLastSibling();

            // ⭐ 押しどころは体の絵ぶん。⚠️ 一番後ろに置いて、他の押しどころを塞がない
            if (Find(root, "Tap") == null)
            {
                var tap = Add(root.transform, "Tap", 0f, 0f, 200f, 200f);
                var plate = tap.gameObject.AddComponent<Image>();
                // ⚠️ 透明でも raycastTarget が要る（見えない押しどころ）
                plate.color = new Color(0f, 0f, 0f, 0f);
                var button = tap.gameObject.AddComponent<Button>();
                button.targetGraphic = plate;
                button.transition = Selectable.Transition.None;
            }
            var tapRect = (RectTransform)Find(root, "Tap");
            // ⚠️ 押しどころは一番手前でないと、他の絵に隠れて押せない（飾りではなく仕組み）
            tapRect.SetAsLastSibling();

            var so = new SerializedObject(view);
            so.FindProperty("_targetMark").objectReferenceValue = target.gameObject;
            so.FindProperty("_tap").objectReferenceValue = tapRect.GetComponent<Button>();
            // ⭐ HP の帯まるごと。⚠️ 相手が1体のときは上の帯に出すので、こちらを消す
            var hpBar = Find(root, "Hp");
            if (hpBar != null) so.FindProperty("_hpBar").objectReferenceValue = hpBar.gameObject;
            var gaugeBar = Find(root, "GaugeTrack");
            if (gaugeBar != null)
            {
                so.FindProperty("_gaugeBar").objectReferenceValue = gaugeBar.gameObject;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        // ── 戦闘: 相手が複数のときの器 ──────────────────

        /// <summary>⚠️ 相手の器が1つしか無く、雑魚の3対3で**1体しか見えなかった**。</summary>
        private static bool PatchBattle(GameObject root)
        {
            var view = root.GetComponent<BattleView>();
            if (view == null)
            {
                Debug.LogError("戦闘 の根から BattleView が外れている");
                return false;
            }
            var so = new SerializedObject(view);

            // ── 上の帯（相手が1体のときの HP） ────────────
            // ⭐ **位置が動かない場所に据える。**⚠️ 体の足元の帯は、相手が大きいほど
            //    視線から外れる（親・ボスは 1.6 倍）。
            if (Find(root, "Foe Band") == null)
            {
                // ⚠️ HP（太い帯）とゲージ（細い線）を**段で分ける**。
                //    重ねていたときは、ゲージが HP の下に潜って見えなかった（実測）。
                // ⭐ 名前は **HP バーの上**の行（作者の指示 2026-08-19）。
                const float BandW = 984f, BandH = 128f, MarkW = 56f;
                const float NameTop = 6f, NameH = 38f;
                const float HpTop = 48f, HpH = 44f, GaugeTop = 100f, GaugeH = 14f;
                var band = Add(root.transform, "Foe Band", 48f, 16f, BandW, BandH);
                var plate = band.gameObject.AddComponent<Image>();
                plate.sprite = Ui.SkinSprite("pill");
                plate.type = Image.Type.Sliced;
                plate.color = Color.white;
                plate.raycastTarget = false;

                // ⚠️ 帯は**器の中**に置く（器を伸ばしても、比が壊れない）
                var fill = Add(band, "Fill", MarkW + 10f, HpTop, BandW - MarkW - 24f, HpH);
                var fillImage = fill.gameObject.AddComponent<Image>();
                fillImage.sprite = Ui.SkinSprite("pill");
                fillImage.type = Image.Type.Sliced;
                fillImage.color = Ui.Danger;
                fillImage.raycastTarget = false;

                var mark = Add(band, "Mark", 0f, (BandH - MarkW) / 2f, MarkW, MarkW);
                var markImage = mark.gameObject.AddComponent<Image>();
                markImage.sprite = Ui.SkinSprite("circle");
                markImage.raycastTarget = false;

                // ⭐ 行動ゲージも同じ帯へ。⚠️ 体の下に残すと、線1本だけが浮いて破片に見える
                var gauge = Add(band, "Gauge", MarkW + 10f, GaugeTop, BandW - MarkW - 24f, GaugeH);
                var gaugeImage = gauge.gameObject.AddComponent<Image>();
                gaugeImage.sprite = Ui.SkinSprite("pill");
                gaugeImage.type = Image.Type.Sliced;
                gaugeImage.color = new Color32(0x2f, 0xa8, 0xff, 0xff);
                gaugeImage.raycastTarget = false;

                // ⚠️ 名前は帯の**上**に重ねる。⭐ 減っても字の位置が動かない
                var name = Add(band, "Name", MarkW + 20f, NameTop, BandW - MarkW - 40f, NameH);
                Label(name, "", 32, Ui.Ink, TextAnchor.MiddleLeft);
            }
            var bandRoot = (RectTransform)Find(root, "Foe Band");
            so.FindProperty("_foeBand").objectReferenceValue = bandRoot.gameObject;
            so.FindProperty("_foeBandFill").objectReferenceValue =
                Child(bandRoot, "Fill").GetComponent<Image>();
            so.FindProperty("_foeBandMark").objectReferenceValue =
                Child(bandRoot, "Mark").GetComponent<Image>();
            so.FindProperty("_foeBandName").objectReferenceValue =
                Child(bandRoot, "Name").GetComponent<Text>();
            so.FindProperty("_foeBandGauge").objectReferenceValue =
                Child(bandRoot, "Gauge").GetComponent<Image>();

            // ── 札の Lv ────────────────────────────────
            // ⭐ **鍛えたぶんを札に出す。**⚠️ CT の丸と同じ行に置くと、
            //    数が2つ並んでどちらが CT か読めない。左肩へ逃がす。
            var skills = so.FindProperty("_skills");
            for (int i = 0; i < skills.arraySize; i++)
            {
                var slot = skills.GetArrayElementAtIndex(i);
                var button = slot.FindPropertyRelative("Button").objectReferenceValue as Button;
                if (button == null) continue;
                var found = Child(button.transform, "Lv");
                var made = found == null ? null : (RectTransform)found;
                if (made == null)
                {
                    made = Add(button.transform, "Lv", 12f, 70f, 120f, 40f);
                    Label(made, "", 24, Ui.OnLead, TextAnchor.MiddleLeft);
                }
                slot.FindPropertyRelative("Level").objectReferenceValue = made.GetComponent<Text>();
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // ⚠️ 「選ぶ」は相手の3体目と**同じ場所**に置かれていた（1体しか出ない前提の位置）。
            //    ⭐ 相手の列より上、体のどれとも重ならない右肩へ逃がす
            var pick = Find(root, "Pick");
            if (pick != null)
            {
                var pr = (RectTransform)pick;
                if (MayPlace(pr))
                {
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
            }

            // ⚠️ **相手の器は Instantiate で複製したので、UnitStand.prefab の子ではない。**
            //    元を直しても伝わらないので、ここに立っている体を1つずつ直す。
            //    ⭐ PatchUnitStand は何度走らせても同じ形になる
            foreach (var stand in root.GetComponentsInChildren<UnitStand>(true))
            {
                PatchUnitStand(stand.gameObject);
            }

            var foes = so.FindProperty("_foes");
            // ⚠️ **3つとも見る。**0番だけ見ていた頃は、1体だけ消すと3体を新しく作り、
            //    古い「Foe 1」「Foe 2」が残って同じ名前の体が2つずつ並んだ
            bool foesReady = foes.arraySize == 3;
            for (int i = 0; foesReady && i < 3; i++)
            {
                if (foes.GetArrayElementAtIndex(i).objectReferenceValue == null) foesReady = false;
            }
            if (foesReady) return true;

            // ⚠️ 作り直す前に、前回の作りかけを片付ける（同名の幽霊を残さない）
            for (int i = 0; i < 3; i++)
            {
                var ghost = Child(root.transform, $"Foe {i}");
                if (ghost != null) Object.DestroyImmediate(ghost.gameObject);
            }

            // ⭐ 味方の並びを鏡にする。味方は x 60・y 150 + 300i なので、相手は右側の同じ高さ
            var allies = so.FindProperty("_allies");
            if (allies.arraySize < 1) { Debug.LogError("味方の器が1つも無い"); return false; }
            var first = allies.GetArrayElementAtIndex(0).objectReferenceValue as UnitStand;
            var lone = so.FindProperty("_foe").objectReferenceValue as UnitStand;
            if (first == null || lone == null) { Debug.LogError("味方/相手の器が無い"); return false; }

            var ar = (RectTransform)first.transform;
            var lr = (RectTransform)lone.transform;
            float step = 300f;
            var second = allies.arraySize > 1
                ? allies.GetArrayElementAtIndex(1).objectReferenceValue as UnitStand
                : null;
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
            if (view == null)
            {
                Debug.LogError("ホーム の根から HomeView が外れている");
                return false;
            }
            var so = new SerializedObject(view);
            var slots = so.FindProperty("_slots");
            if (slots.arraySize < 5) return false;

            var head = slots.GetArrayElementAtIndex(0).objectReferenceValue as IncubatorSlot;
            if (head == null) return false;
            var hr = (RectTransform)head.transform;

            // ⚠️ **見えている大きさで並べる。**枠は localScale 0.8 で縮めてあるので、
            //    sizeDelta（300）のまま中央寄せを計算すると行が左へ寄る
            //    （実測で 左余白72 / 右余白132 とずれていた）。
            float w = hr.sizeDelta.x * hr.localScale.x, h = hr.sizeDelta.y * hr.localScale.y;
            float gap = 18f;
            float top = -hr.anchoredPosition.y;
            float rowTop3 = (1080f - (w * 3f + gap * 2f)) / 2f;
            float rowTop2 = (1080f - (w * 2f + gap)) / 2f;

            for (int i = 0; i < 5; i++)
            {
                var slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as IncubatorSlot;
                if (slot == null) continue;
                var r = (RectTransform)slot.transform;
                // ⚠️ **人が動かした枠は動かさない。**ここには
                //    「x が 42 なら整えてある」という判定が置いてあったが、枠の実寸は 300 で
                //    rowTop3 は 72 なので**一度も一致せず**、毎回5枠とも上書きしていた。
                if (!MayPlace(r)) continue;
                float x = i < 3 ? rowTop3 + (w + gap) * i : rowTop2 + (w + gap) * (i - 3);
                float y = top + (i < 3 ? 0f : h + gap);
                r.anchoredPosition = new Vector2(x, -y);
            }
            return true;
        }

        // ── 道具 ────────────────────────────────────────

        /// <summary>名前で子を消す。⚠️ 並びを作り替えた日に、古い部品が
        /// 押しどころの下へ潜って残るのを防ぐ（字は見えないが当たり判定は残る）。
        ///
        /// ⚠️ **直接の子だけを見る。**<see cref="Find"/> は子孫まで潜るので、これに使うと
        /// 後から作った札の**中身**に届いてしまう。実際そうなっていた ──
        /// "Art"/"Element"/"Name"/"Level"/"K 0".."K 5" が
        /// <see cref="BuildCreaturePanel"/> の作る子と名前で衝突し、
        /// 「足りない部品を足す」を押すたびに BOX と配合の札が丸ごと作り直されていた。
        ///
        /// ⚠️ **消すのは作り直しのときだけ。**古い並びの残骸より、人が置いたものを
        /// 壊さないほうが大事（残骸は「画面を作り直す」で消える）。</summary>
        // ── 器: タブ帯の地 ─────────────────────────────

        /// <summary>⭐ **タブ帯に地を敷く**（2026-08-21・作者の指示）。
        ///
        /// ⚠️ 帯はボタン4つだけで、**地が無かった**。だから一覧が帯の隙間から
        /// 透けて見え、「貫通している」ように読めた
        /// （`画面を全部検査する` も字の重なりとして数え続けていた）。
        /// ⭐ 地を敷けば、下に潜った行が見えなくなる。
        ///
        /// ⚠️ **ボタンより後ろへ置く**（子の並びの先頭）。後ろにしないとボタンを覆う。</summary>
        private static bool PatchFrame(GameObject root)
        {
            var dock = Find(root, "Dock");
            if (dock == null) { Debug.LogError("AppFrame に Dock が無い"); return false; }
            if (Find(root, "Plate") != null) return false;

            var plate = Add(dock, "Plate", 0f, 0f, Ui.W, Ui.DockHeight);
            // ⭐ 面で分ける（線は引かない・画面の作法）。⚠️ 透けないこと
            var image = plate.gameObject.AddComponent<Image>();
            image.sprite = Ui.SkinSprite("panel");
            image.type = Image.Type.Sliced;
            image.color = Ui.Paper;
            // ⚠️ 帯の下を押しても画面へ抜けないようにする
            image.raycastTarget = true;
            plate.SetAsFirstSibling();
            return true;
        }

        private static void Drop(Component parent, params string[] names)
        {
            foreach (var name in names)
            {
                var found = Child(parent.transform, name);
                if (found == null) continue;
                if (!MayDrop())
                {
                    Debug.LogWarning($"{parent.name}/{name} は古い並びの部品。"
                        + "⚠️ 触らずに残した（消すなら「画面を作り直す」）");
                    continue;
                }
                Object.DestroyImmediate(found.gameObject);
            }
        }

        /// <summary>直接の子を名前で1つ。⚠️ 孫は見ない（<see cref="Find"/> との違い）。</summary>
        private static Transform Child(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            }
            return null;
        }

        /// <summary>細い棒。⭐ 狙い先のカギ括弧を作る部品。</summary>
        private static void Bar(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = Add(parent, name, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Ui.Danger;
            image.raycastTarget = false;
        }

        /// <summary>左上を原点に置き直す。
        /// ⚠️ **人が動かした部品には効かない。**この回に作ったものと、作り直しのときだけ。</summary>
        private static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            if (!MayPlace(rect)) return;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
        }

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
            // ⭐ この回に作ったものだけ、あとから位置を書いてよい
            Fresh.Add(rect);
            return rect;
        }

        /// <summary>その部品の位置を書いてよいか。
        /// ⭐ **この回に作ったものだけ。**⚠️ 人が動かしたものは触らない。</summary>
        /// ⚠️ **null を先に弾く。**_rebuild を先に見ていた頃は、部品が消されている画面で
        /// `MayPlace(null)` が true を返し、直後の代入で道具ごと止まっていた。
        private static bool MayPlace(Component part) =>
            part != null && (_rebuild || Fresh.Contains(part as RectTransform));

        /// <summary>消してよいか。⚠️ 作り直しのときだけ。</summary>
        private static bool MayDrop() => _rebuild;

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
