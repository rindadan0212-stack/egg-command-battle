using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace EggCommand.EditorTools
{
    /// <summary>飾りを直すための**操作盤**。⭐ ここだけ見れば作業が始められる。
    ///
    /// ⚠️ **コードを書かない人が使う前提。**Project フォルダを掘らせない・
    /// 数値の欄を細くしない・押したら何が起きるかを日本語で書く、の3つを守る。
    ///
    /// ⭐ 置き場所は「Egg Command / 操作盤をひらく」。ドッキングできる普通の窓なので、
    /// Inspector の隣に置いたまま作業できる。
    /// </summary>
    public sealed class EggCommandWindow : EditorWindow
    {
        [MenuItem("Egg Command/操作盤をひらく %#e")]
        public static void Open()
        {
            // ⭐ Inspector の隣に**貼り付けて**開く。⚠️ 浮いた窓のままだと
            //    Inspector を覆ってしまい、数値を触るのに毎回どかすことになる。
            var window = Dock<EggCommandWindow>("Egg Command", "UnityEditor.InspectorWindow");
            window.minSize = new Vector2(280f, 400f);
            window.Show();
        }

        /// <summary>指定の窓の隣に貼り付けて開く。⚠️ 相手が見つからなければ普通に開く
        /// （Unity の内部の型名なので、版が変わると消えることがある）。</summary>
        private static T Dock<T>(string title, params string[] neighbours) where T : EditorWindow
        {
            var types = new List<System.Type>();
            foreach (string name in neighbours)
            {
                var type = typeof(EditorWindow).Assembly.GetType(name);
                if (type != null) types.Add(type);
            }
            return types.Count == 0
                ? GetWindow<T>(title)
                : GetWindow<T>(title, true, types.ToArray());
        }

        /// <summary>飾りを直すための配置にする。⚠️ 押すと窓の並びが変わる。
        ///
        /// ⭐ 出すのは4つだけ ── **どこに何があるか**／**大きく見る**／
        /// **数を触る**／**操作盤**。⚠️ Project も Game も使わないので出さない
        /// （画面は操作盤から開き、Prefab はファイル名で探さない）。</summary>
        [MenuItem("Egg Command/配置をととのえる")]
        public static void Arrange()
        {
            var scene = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneView");
            var hierarchy = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            var inspector = typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");
            var console = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");

            if (scene != null) GetWindow(scene, false, null, true);
            if (hierarchy != null) GetWindow(hierarchy, false, null, false);
            if (inspector != null) GetWindow(inspector, false, null, false);
            if (console != null) GetWindow(console, false, null, false);
            Open();

            // ⭐ Scene ビューを 2D にして、いま編集している画面へ寄せる
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.in2DMode = true;
                FrameCurrent(view);
            }
            Debug.Log("配置をととのえた。⭐ この並びを残すなら "
                + "Window > Layouts > Save Layout... で名前を付けて保存してください");
        }

        /// <summary>いま開いている画面に Scene ビューを寄せる。</summary>
        [MenuItem("Egg Command/いまの画面に寄せる _F2")]
        public static void FrameNow()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            view.in2DMode = true;
            FrameCurrent(view);
        }

        private static void FrameCurrent(SceneView view)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var root = stage != null ? stage.prefabContentsRoot : Selection.activeGameObject;
            if (root == null) return;
            var rect = root.GetComponent<RectTransform>();
            if (rect == null) return;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bounds = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++) bounds.Encapsulate(corners[i]);
            view.Frame(bounds, false);
            view.Repaint();
        }

        // ── 画面の一覧 ──────────────────────────────────
        //
        // ⚠️ 名前は Assets/Resources/Prefabs/<Prefab>.prefab に対応する。
        //    App.Put が "Prefabs/" + name で読むのと同じ綴り。

        private struct Page
        {
            public string Label;    // 人が読む名前
            public string Prefab;   // ファイル名
            public string Note;     // 何の画面か
        }

        private static readonly Page[] Screens =
        {
            new Page { Label = "ホーム",   Prefab = "HomeScreen",   Note = "孵化器・放置・下のタブ" },
            new Page { Label = "探索",     Prefab = "NestsScreen",  Note = "巣の札が並ぶ画面" },
            new Page { Label = "BOX",      Prefab = "BoxScreen",    Note = "個体の一覧と詳細の札" },
            new Page { Label = "配合",     Prefab = "BreedScreen",  Note = "親2体を選ぶ画面" },
            new Page { Label = "戦闘",     Prefab = "BattleScreen", Note = "体・手札・狙いの印" },
            new Page { Label = "外枠",     Prefab = "AppFrame",     Note = "題名・戻る・下のタブ（全画面共通）" },
        };

        private static readonly Page[] Parts =
        {
            new Page { Label = "巣の札",   Prefab = "EncounterCard", Note = "探索に並ぶ1枚" },
            new Page { Label = "個体の升", Prefab = "CreatureCell",  Note = "BOX の一覧の1マス" },
            new Page { Label = "卵の札",   Prefab = "EggCard",       Note = "在庫の卵1枚" },
            new Page { Label = "孵化枠",   Prefab = "IncubatorSlot", Note = "ホームの5枠" },
            new Page { Label = "体",       Prefab = "UnitStand",     Note = "戦闘に立つ1体" },
            new Page { Label = "報せ",     Prefab = "Banner",        Note = "画面上に出る帯" },
            new Page { Label = "祝い",     Prefab = "Fanfare",       Note = "孵ったときの演出" },
            new Page { Label = "強奪の結果", Prefab = "StealResult", Note = "潜入のあとに出る札" },
        };

        // ── 描く ────────────────────────────────────────

        private Vector2 _scroll;
        private float _step = 10f;
        private static GUIStyle _big;
        private static GUIStyle _head;
        private static GUIStyle _note;

        private static void MakeStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.button) { fontSize = 15, fixedHeight = 34f };
            _head = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _note = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }

        private void OnGUI()
        {
            MakeStyles();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            Section("画面をひらく", "押すと、その画面の Prefab が編集の形で開きます。");
            Grid(Screens);

            EditorGUILayout.Space(6f);
            Section("部品をひらく", "画面の中で何度も使う小さな部品です。");
            Grid(Parts);

            EditorGUILayout.Space(10f);
            PartList();

            EditorGUILayout.Space(10f);
            Selected();

            EditorGUILayout.Space(10f);
            Tools();

            EditorGUILayout.EndScrollView();
        }

        private static void Section(string title, string note)
        {
            EditorGUILayout.LabelField(title, _head);
            if (!string.IsNullOrEmpty(note)) EditorGUILayout.LabelField(note, _note);
        }

        /// <summary>2列に並べる。⚠️ 窓が細いときは1列に落ちる。</summary>
        private void Grid(Page[] pages)
        {
            int columns = position.width < 360f ? 1 : 2;
            for (int i = 0; i < pages.Length; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < columns && i + c < pages.Length; c++)
                {
                    var page = pages[i + c];
                    if (GUILayout.Button(new GUIContent(page.Label, page.Note), _big))
                    {
                        OpenPrefab(page.Prefab);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>Prefab を編集の形で開く。⚠️ Project を掘らせない。</summary>
        private static void OpenPrefab(string name)
        {
            string path = $"Assets/Resources/Prefabs/{name}.prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("開けない",
                    $"{name}.prefab が見つかりません。\n\n"
                    + "「画面を Prefab に書き出す」を先に走らせてください。", "わかった");
                return;
            }
            AssetDatabase.OpenAsset(asset);
            // ⭐ 開いた直後に寄せる。⚠️ 寄せないと 1080x1920 の器が画面外に居て、
            //    「開いたのに何も出ない」ように見える（実際そうなっていた）。
            EditorApplication.delayCall += () =>
            {
                var view = SceneView.lastActiveSceneView;
                if (view == null) return;
                view.in2DMode = true;
                FrameCurrent(view);
            };
        }

        // ── 選んでいる部品を、大きな欄で触る ────────────

        private string _find = "";
        private bool _onlyVisible = true;

        /// <summary>いま開いている画面の部品を並べ、押したら選べるようにする。
        ///
        /// ⚠️ **シーンを直接クリックしても選べない**（実測: 67個中0個しか拾えない。
        /// ただの四角は拾えるので UI だけの問題。原因は未特定）。
        /// ⭐ 選ぶ手段が無いと何も始まらないので、確実に動く道を1本用意する。</summary>
        private void PartList()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            Section("この画面の部品", stage == null
                ? "画面を開くと、中の部品がここに並びます。"
                : "押すと選べます。⚠️ シーンを直接クリックしても選べないので、ここから。");
            if (stage == null) return;

            EditorGUILayout.BeginHorizontal();
            _find = EditorGUILayout.TextField("さがす", _find, GUILayout.Height(20f));
            if (GUILayout.Button("×", GUILayout.Width(24f))) _find = "";
            EditorGUILayout.EndHorizontal();
            _onlyVisible = EditorGUILayout.ToggleLeft("出ているものだけ", _onlyVisible);

            var root = stage.prefabContentsRoot.transform;
            int shown = 0;
            foreach (var t in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (t == root) continue;
                if (_onlyVisible && !t.gameObject.activeInHierarchy) continue;
                if (_find.Length > 0 && t.name.IndexOf(_find, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (++shown > 120) break;   // ⚠️ 際限なく並べない（BOX は 84 個ある）

                int depth = 0;
                for (var p = t.parent; p != null && p != root; p = p.parent) depth++;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(depth * 12f);
                bool here = Selection.activeGameObject == t.gameObject;
                string what = t.GetComponent<Text>() != null ? "文字"
                    : t.GetComponent<Button>() != null ? "押しどころ"
                    : t.GetComponent<Image>() != null ? "絵" : "入れ物";
                if (GUILayout.Button($"{(here ? "▶ " : "")}{t.name}",
                    here ? EditorStyles.whiteLabel : EditorStyles.label))
                {
                    Selection.activeGameObject = t.gameObject;
                    EditorGUIUtility.PingObject(t.gameObject);
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(what, _note, GUILayout.Width(64f));
                EditorGUILayout.EndHorizontal();
            }
            if (shown == 0) EditorGUILayout.LabelField("見つかりません", _note);
        }

        /// <summary>⚠️ **Inspector の欄が細くて押しにくい**という声への答え。
        /// ⭐ よく触る数だけを大きく出し、矢印で少しずつ動かせるようにする。
        /// ⚠️ すべて Undo を通すので、Ctrl+Z で戻せる。</summary>
        private void Selected()
        {
            Section("いま選んでいるもの", "");

            var go = Selection.activeGameObject;
            var rect = go == null ? null : go.GetComponent<RectTransform>();
            if (rect == null)
            {
                EditorGUILayout.HelpBox(
                    "Hierarchy か Scene で、動かしたい部品を1つ選んでください。\n"
                    + "（RectTransform を持つもの＝画面の部品だけがここに出ます）",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(Where(rect), EditorStyles.boldLabel);

            // ⚠️ **伸縮する部品では数字の意味が変わる。**
            //    親いっぱいに広がる設定だと、はば・たかさは「大きさ」ではなく
            //    **親の端からの余白**になり、0 と出る。0 を見て「壊れている」と
            //    読まれるので、実際の大きさを必ず併記する。
            bool stretch = rect.anchorMin.x != rect.anchorMax.x || rect.anchorMin.y != rect.anchorMax.y;
            EditorGUILayout.LabelField(
                $"いまの大きさ  {rect.rect.width:0} × {rect.rect.height:0}", EditorStyles.miniLabel);
            if (stretch)
            {
                EditorGUILayout.HelpBox(
                    "この部品は親いっぱいに広がる設定です。\n"
                    + "下の「はば・たかさ」は大きさではなく、親の端からの余白になります。",
                    MessageType.None);
            }

            EditorGUIUtility.labelWidth = 64f;
            EditorGUI.BeginChangeCheck();

            var pos = rect.anchoredPosition;
            var size = rect.sizeDelta;
            EditorGUILayout.BeginHorizontal();
            pos.x = EditorGUILayout.FloatField("よこ", pos.x, GUILayout.Height(22f));
            pos.y = EditorGUILayout.FloatField("たて", pos.y, GUILayout.Height(22f));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            size.x = EditorGUILayout.FloatField(stretch ? "よこ余白" : "はば", size.x, GUILayout.Height(22f));
            size.y = EditorGUILayout.FloatField(stretch ? "たて余白" : "たかさ", size.y, GUILayout.Height(22f));
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rect, "部品を動かす");
                rect.anchoredPosition = pos;
                rect.sizeDelta = size;
                Mark(rect);
            }

            // ⭐ 数を打たずに少しずつ動かす。⚠️ たては上が＋（Unity は下が＋なので符号を反す）
            EditorGUILayout.Space(2f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("うごかす", GUILayout.Width(64f));
            if (GUILayout.Button("←", _big)) Nudge(rect, -_step, 0f);
            if (GUILayout.Button("→", _big)) Nudge(rect, _step, 0f);
            if (GUILayout.Button("↑", _big)) Nudge(rect, 0f, _step);
            if (GUILayout.Button("↓", _big)) Nudge(rect, 0f, -_step);
            _step = EditorGUILayout.FloatField(_step, GUILayout.Width(48f), GUILayout.Height(22f));
            EditorGUILayout.EndHorizontal();

            var text = rect.GetComponent<Text>();
            if (text != null)
            {
                EditorGUILayout.Space(2f);
                EditorGUI.BeginChangeCheck();
                int fontSize = EditorGUILayout.IntField("字の大きさ", text.fontSize, GUILayout.Height(22f));
                var color = EditorGUILayout.ColorField("字の色", text.color, GUILayout.Height(22f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(text, "字を変える");
                    text.fontSize = fontSize;
                    text.color = color;
                    Mark(text);
                }
                // ⚠️ 枠より字が広いと端が切れる。数で出す（見た目では分からない）
                if (text.preferredWidth > rect.rect.width + 1f)
                {
                    EditorGUILayout.HelpBox(
                        $"字が枠より広いです（要る {text.preferredWidth:0} / 枠 {rect.rect.width:0}）。"
                        + "端が切れるか、隣にはみ出します。", MessageType.Warning);
                }
            }

            var image = rect.GetComponent<Image>();
            if (image != null)
            {
                EditorGUI.BeginChangeCheck();
                var tint = EditorGUILayout.ColorField("絵の色", image.color, GUILayout.Height(22f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(image, "色を変える");
                    image.color = tint;
                    Mark(image);
                }
            }
            EditorGUIUtility.labelWidth = 0f;
            Jump(rect, image, text);
        }

        /// <summary>選んでいる部品から、**その元になっているファイル**へ跳ぶ。
        ///
        /// ⚠️ Project フォルダを掘らせないための入口。
        /// 「この見た目を直したい」と思った瞬間に、絵か大元の Prefab を開けるようにする。</summary>
        private static void Jump(RectTransform rect, Image image, Text text)
        {
            var master = PrefabUtility.GetCorrespondingObjectFromSource(rect.gameObject);
            var sprite = image == null ? null : image.sprite;
            if (master == null && sprite == null) return;

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();

            if (master != null)
            {
                string path = AssetDatabase.GetAssetPath(master);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (GUILayout.Button(new GUIContent($"大元「{name}」をひらく",
                    "この部品の元になっている Prefab を開きます。直すと、これを使っている全部に伝わります"), _big))
                {
                    AssetDatabase.OpenAsset(master);
                }
            }

            if (sprite != null)
            {
                if (GUILayout.Button(new GUIContent($"絵「{sprite.name}」を選ぶ",
                    "使っている画像ファイルを Project で選びます（差し替えや取り込み設定の変更用）"), _big))
                {
                    // ⭐ Ping で場所を光らせる。⚠️ 選ぶだけだとどこにあるか分からない
                    Selection.activeObject = sprite;
                    EditorGUIUtility.PingObject(sprite);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (sprite != null)
            {
                string p = AssetDatabase.GetAssetPath(sprite);
                EditorGUILayout.LabelField(p, _note);
            }
        }

        private void Nudge(RectTransform rect, float dx, float dy)
        {
            Undo.RecordObject(rect, "部品を動かす");
            rect.anchoredPosition += new Vector2(dx, dy);
            Mark(rect);
        }

        /// <summary>変えたことを Unity に伝える。⚠️ これが無いと保存されない。</summary>
        private static void Mark(Object target)
        {
            EditorUtility.SetDirty(target);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) EditorSceneManager.MarkSceneDirty(stage.scene);
        }

        /// <summary>どこの部品かを「親/子」で示す。⚠️ 名前だけだと同名が多くて分からない。</summary>
        private static string Where(Transform t) =>
            t.parent == null ? t.name : t.parent.name + " / " + t.name;

        // ── 道具 ────────────────────────────────────────

        private void Tools()
        {
            Section("道具", "");

            if (GUILayout.Button(new GUIContent("足りない部品を足す",
                "決めたのに画面に無いものだけを作ります。手で置いた位置は動きません"), _big))
            {
                PatchScreenPrefabs.PatchAll();
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button(new GUIContent(
                    Application.isPlaying ? "崩れていないか調べる" : "崩れていないか調べる（▶ を押してから）",
                    "字の重なり・はみ出しを数で調べます"), _big))
                {
                    Debug.Log("■ 画面の検査\n" + InspectScreens.Report());
                }
            }

            if (GUILayout.Button(new GUIContent("いまの画面を1枚撮る",
                "Game ビューをそのまま画像に保存します"), _big))
            {
                EditorApplication.ExecuteMenuItem("Egg Command/画面を1枚撮る");
            }

            // ⚠️ **Unity の側の出力で埋まる。**UI Text を持つ Prefab を開くたびに、
            //    Unity が「使い終わっていない領域がある」という中身の写し（16進の羅列）を
            //    10行ほど吐く。⭐ 自前のコードとは無関係で、出荷したアプリには出ない。
            //    ⚠️ 消す設定は無いので、溜まったら流す（実測: 1画面あたり約10行）。
            if (GUILayout.Button(new GUIContent("Console をきれいにする",
                "Unity が出す 16進の羅列で埋まったら押します。自前のコードとは無関係です"), _big))
            {
                ClearConsole();
            }

            EditorGUILayout.Space(8f);
            var warn = new GUIStyle(_big) { normal = { textColor = new Color(0.8f, 0.25f, 0.2f) } };
            if (GUILayout.Button(new GUIContent("⚠️ 画面を作り直す",
                "手で置いた位置・大きさが、すべて初期値に戻ります"), warn))
            {
                PatchScreenPrefabs.RebuildAll();
            }
        }

        /// <summary>Console を空にする。⚠️ 公開されている入口が無いので、
        /// Unity の中の <c>LogEntries.Clear</c> を名前で呼ぶ（版が変わると消えることがある）。</summary>
        private static void ClearConsole()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
            var clear = type == null ? null : type.GetMethod("Clear");
            if (clear == null)
            {
                Debug.LogWarning("Console を流す入口が見つからない（Console の Clear を押してください）");
                return;
            }
            clear.Invoke(null, null);
        }

        // ⭐ 選び直したら描き直す（選択に追従する窓なので）
        private void OnSelectionChange() => Repaint();
        private void OnInspectorUpdate() => Repaint();
    }
}
