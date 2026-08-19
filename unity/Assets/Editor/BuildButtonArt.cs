using System.IO;
using UnityEngine;
using UnityEditor;

namespace EggCommand.EditorTools
{
    /// <summary>押しどころの絵を**数式で作る**。
    ///
    /// ⭐ **手で描かない。**角丸・枠線・つや・立体感はどれも幾何と勾配なので、
    /// 絵筆で描くとがたつくが、式で置けば何度出し直しても同じ精度で出る。
    /// ⚠️ 絵筆が要るのは「キャラ」や「装飾」であって、UI の押しどころではない。
    ///
    /// ⭐ 出すのは **9スライス**用の1枚。枠(border)も同時に書き込むので、
    /// どんな幅・高さに伸ばしても角が歪まない（52個の押しどころが1枚を共有している）。
    ///
    /// ⚠️ 上書きする前に必ず <c>.bak</c> へ退避する。気に入らなければ戻せる。
    /// </summary>
    public sealed class BuildButtonArt : EditorWindow
    {
        // ── 出す先 ──────────────────────────────────────

        private const string Dir = "Assets/Resources/UI";

        /// <summary>⚠️ いまの絵と同じ寸法・同じ枠。⭐ ここを変えると較正が壊れる
        /// （`pixelsPerUnitMultiplier = 1` は実測で決めた値）。</summary>
        private const int W = 359;
        private const int H = 162;
        private static readonly Vector4 Border = new Vector4(48f, 60f, 48f, 48f);

        /// <summary>作れる絵と、いまの色。⭐ 実物から採った値なので、初期状態は今と同じ見た目。</summary>
        private static readonly (string File, string Label, string Hex)[] Kinds =
        {
            ("button",        "ふつう",   "#17B0FF"),
            ("button-lead",   "主役",     "#FFDD19"),
            ("button-danger", "危ない",   "#E744FF"),
            ("button-good",   "よい",     "#19F463"),
            ("button-off",    "押せない", "#949494"),
        };

        // ── 触れる数 ────────────────────────────────────

        private int _kind;
        private Color _base = new Color32(0x17, 0xB0, 0xFF, 0xFF);
        private float _radius = 34f;     // 角丸の半径（px）
        private float _gloss = 0.30f;    // 上のつや
        private float _glossStop = 0.52f;// つやの切れ目（上から何割）
        private float _rim = 0.28f;      // 下のふちの明るさ
        private float _rimHeight = 26f;  // 下のふちの高さ（px）
        private float _bevel = 0.22f;    // ふちの立体感
        private float _bevelWidth = 5f;  // 立体感の幅（px）
        private float _outline = 0f;     // 枠線の太さ（px）
        private Color _outlineColor = new Color(0f, 0f, 0f, 0.35f);

        private Texture2D _preview;
        private bool _dirty = true;
        private Vector2 _scroll;

        [MenuItem("Egg Command/ボタンの絵を作る")]
        public static void Open()
        {
            // ⭐ 操作盤の隣に出す。⚠️ ただ GetWindow すると、どこに出たか分からないことがある
            //    （実際、開いたのに画面のどこにも見えなかった）。
            var neighbour = typeof(EditorWindow).Assembly.GetType("UnityEditor.ProjectBrowser");
            var w = neighbour != null
                ? GetWindow<BuildButtonArt>("ボタンの絵", true, neighbour)
                : GetWindow<BuildButtonArt>("ボタンの絵");
            w.minSize = new Vector2(400f, 520f);
            w.Show();
            w.Focus();
        }

        private void OnEnable()
        {
            ColorUtility.TryParseHtmlString(Kinds[_kind].Hex, out _base);
            _dirty = true;
        }

        // ── 描く ────────────────────────────────────────

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("どの押しどころを作るか", EditorStyles.boldLabel);
            var labels = new string[Kinds.Length];
            for (int i = 0; i < Kinds.Length; i++) labels[i] = Kinds[i].Label;
            int picked = GUILayout.SelectionGrid(_kind, labels, Kinds.Length);
            if (picked != _kind)
            {
                _kind = picked;
                ColorUtility.TryParseHtmlString(Kinds[_kind].Hex, out _base);
                _dirty = true;
            }
            EditorGUILayout.LabelField($"書き出し先  {Dir}/{Kinds[_kind].File}.png", EditorStyles.miniLabel);

            EditorGUILayout.Space(8f);
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("かたち", EditorStyles.boldLabel);
            _base = EditorGUILayout.ColorField("地の色", _base);
            _radius = Slider("角の丸み", _radius, 0f, 70f, "0 で角ばった四角、70 で丸みが最大");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("光沢", EditorStyles.boldLabel);
            _gloss = Slider("上のつや", _gloss, 0f, 0.8f, "上半分を明るくして、光が当たっているように見せます");
            _glossStop = Slider("つやの切れ目", _glossStop, 0.2f, 0.9f, "上から何割の位置でつやが終わるか");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("立体感", EditorStyles.boldLabel);
            _bevel = Slider("ふちの厚み", _bevel, 0f, 0.6f, "外周を明暗で縁取り、面が起きているように見せます");
            _bevelWidth = Slider("厚みの幅", _bevelWidth, 1f, 16f, "px");
            _rim = Slider("下の受け", _rim, 0f, 0.7f, "下端を明るくして、板が浮いているように見せます");
            _rimHeight = Slider("受けの高さ", _rimHeight, 4f, 48f, "px");

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("枠線", EditorStyles.boldLabel);
            _outline = Slider("太さ", _outline, 0f, 10f, "0 なら枠線なし");
            if (_outline > 0f) _outlineColor = EditorGUILayout.ColorField("色", _outlineColor);

            if (EditorGUI.EndChangeCheck()) _dirty = true;

            EditorGUILayout.Space(10f);
            Preview();

            EditorGUILayout.Space(10f);
            Write();

            EditorGUILayout.EndScrollView();
        }

        private static float Slider(string label, float value, float min, float max, string help)
        {
            var v = EditorGUILayout.Slider(new GUIContent(label, help), value, min, max);
            return v;
        }

        /// <summary>⭐ **9スライスで伸びた姿も一緒に見せる。**
        /// ⚠️ 原寸だけ見て決めると、実際に使う幅（145〜984）で角がどう見えるか分からない。</summary>
        private void Preview()
        {
            if (_dirty || _preview == null)
            {
                if (_preview != null) DestroyImmediate(_preview);
                _preview = Make();
                _dirty = false;
            }
            EditorGUILayout.LabelField("見え方", EditorStyles.boldLabel);

            // ⭐ **まず原寸。**⚠️ 縮めて見ると、角丸もつやも潰れて「のっぺり」に見える
            //    （実際そう見えて、生成が壊れていると誤診した）。
            var raw = GUILayoutUtility.GetRect(W, H, GUILayout.Width(W), GUILayout.Height(H));
            GUI.DrawTexture(raw, _preview, ScaleMode.ScaleToFit, true);
            EditorGUILayout.LabelField($"↑ 原寸 {W}×{H}（絵そのもの）", EditorStyles.miniLabel);

            EditorGUILayout.Space(6f);
            var wide = GUILayoutUtility.GetRect(position.width - 30f, 132f);
            DrawSliced(wide);
            EditorGUILayout.LabelField("↑ 横に伸ばしたところ（実際の押しどころはこの形）", EditorStyles.miniLabel);

            EditorGUILayout.Space(6f);
            var one = GUILayoutUtility.GetRect(180f, 132f);
            one.width = 180f;
            DrawSliced(one);
            EditorGUILayout.LabelField("↑ 幅の狭いところ（BOX の下の並び）", EditorStyles.miniLabel);
        }

        /// <summary>9スライスの見え方をそのまま描く。⚠️ 単純に引き伸ばすと角が歪んで嘘になる。</summary>
        private void DrawSliced(Rect area)
        {
            if (_preview == null) return;
            float l = Border.x, b = Border.y, r = Border.z, t = Border.w;
            // ⚠️ Unity の spriteBorder は (左, 下, 右, 上)
            float texL = l / W, texR = r / W, texB = b / H, texT = t / H;
            float cutL = Mathf.Min(l, area.width * 0.45f);
            float cutR = Mathf.Min(r, area.width * 0.45f);
            float cutB = Mathf.Min(b, area.height * 0.45f);
            float cutT = Mathf.Min(t, area.height * 0.45f);

            Slice(area, 0f, 0f, cutL, cutB, 0f, 0f, texL, texB);                        // 左下
            Slice(area, cutL, 0f, area.width - cutL - cutR, cutB, texL, 0f, 1f - texL - texR, texB);
            Slice(area, area.width - cutR, 0f, cutR, cutB, 1f - texR, 0f, texR, texB);   // 右下
            Slice(area, 0f, cutB, cutL, area.height - cutB - cutT, 0f, texB, texL, 1f - texB - texT);
            Slice(area, cutL, cutB, area.width - cutL - cutR, area.height - cutB - cutT,
                texL, texB, 1f - texL - texR, 1f - texB - texT);                         // 中央
            Slice(area, area.width - cutR, cutB, cutR, area.height - cutB - cutT,
                1f - texR, texB, texR, 1f - texB - texT);
            Slice(area, 0f, area.height - cutT, cutL, cutT, 0f, 1f - texT, texL, texT);  // 左上
            Slice(area, cutL, area.height - cutT, area.width - cutL - cutR, cutT,
                texL, 1f - texT, 1f - texL - texR, texT);
            Slice(area, area.width - cutR, area.height - cutT, cutR, cutT,
                1f - texR, 1f - texT, texR, texT);                                       // 右上
        }

        /// <summary>⚠️ GUI の y は下向き、テクスチャの y は上向きなので、上下を入れ替えて渡す。</summary>
        private void Slice(Rect area, float x, float y, float w, float h,
            float u, float v, float uw, float vh)
        {
            if (w <= 0f || h <= 0f) return;
            var dst = new Rect(area.x + x, area.y + area.height - y - h, w, h);
            GUI.DrawTextureWithTexCoords(dst, _preview, new Rect(u, v, uw, vh), true);
        }

        // ── 作る ────────────────────────────────────────

        /// <summary>1枚ぶんの画素を全部決める。⭐ ここが「絵」の唯一の出所。</summary>
        private Texture2D Make()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color[W * H];
            float radius = Mathf.Min(_radius, Mathf.Min(W, H) * 0.5f);

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    // ⭐ 角丸の内側からの距離。負なら中、正なら外
                    float d = RoundedDistance(x + 0.5f, y + 0.5f, W, H, radius);

                    // ⚠️ 1px ぶんで滑らかに切る（これが無いと角ががたつく）
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) { pixels[y * W + x] = Color.clear; continue; }

                    var c = _base;
                    float up = y / (float)(H - 1);          // 0=下, 1=上

                    // ── 上のつや ──
                    if (_gloss > 0f)
                    {
                        float k = Mathf.InverseLerp(_glossStop, 1f, up);
                        c = Lighten(c, k * k * _gloss);
                    }

                    // ── 下の受け ──
                    if (_rim > 0f)
                    {
                        float k = 1f - Mathf.Clamp01(y / _rimHeight);
                        c = Lighten(c, k * k * _rim);
                    }

                    // ── ふちの立体感（上は明るく、下は暗く）──
                    if (_bevel > 0f)
                    {
                        float inside = Mathf.Clamp01(-d / Mathf.Max(1f, _bevelWidth));
                        float edge = 1f - inside;                     // 外周ほど 1
                        if (edge > 0f)
                        {
                            // 上半分は光を受け、下半分は影になる
                            float face = Mathf.Lerp(-1f, 1f, up);
                            c = face >= 0f
                                ? Lighten(c, edge * face * _bevel)
                                : Darken(c, edge * -face * _bevel);
                        }
                    }

                    // ── 枠線 ──
                    if (_outline > 0f && -d < _outline)
                    {
                        float k = Mathf.Clamp01((_outline - (-d)) / Mathf.Max(0.001f, _outline));
                        c = Color.Lerp(c, _outlineColor, k * _outlineColor.a);
                    }

                    c.a = alpha * _base.a;
                    pixels[y * W + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>角丸四角形の符号つき距離。⭐ これ1本で角の滑らかさが決まる。</summary>
        private static float RoundedDistance(float px, float py, float w, float h, float r)
        {
            float cx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
            float cy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
            float outside = Mathf.Sqrt(Mathf.Max(cx, 0f) * Mathf.Max(cx, 0f)
                + Mathf.Max(cy, 0f) * Mathf.Max(cy, 0f));
            float inside = Mathf.Min(Mathf.Max(cx, cy), 0f);
            // ⚠️ 最後に半径を引く。ここを「- r + r」と書いていた頃は打ち消し合って
            //    式が死んでおり、左端も上端も「外」と判定されていた（検算で発覚）。
            return outside + inside - r;
        }

        private static Color Lighten(Color c, float k) =>
            new Color(Mathf.Lerp(c.r, 1f, k), Mathf.Lerp(c.g, 1f, k), Mathf.Lerp(c.b, 1f, k), c.a);

        private static Color Darken(Color c, float k) =>
            new Color(c.r * (1f - k), c.g * (1f - k), c.b * (1f - k), c.a);

        // ── 書き出す ────────────────────────────────────

        private void Write()
        {
            EditorGUILayout.LabelField("書き出す", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "いまある絵は " + Kinds[_kind].File + ".png.bak に控えてから上書きします。\n"
                + "書き出すと、この絵を使っている押しどころが一斉に変わります。",
                MessageType.None);

            var big = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 32f };
            if (GUILayout.Button($"「{Kinds[_kind].Label}」を書き出す", big)) Save(_kind);

            if (GUILayout.Button("5種類ぜんぶ書き出す（色だけ変えて同じ形に）", big))
            {
                int keep = _kind;
                var keepColor = _base;
                for (int i = 0; i < Kinds.Length; i++)
                {
                    _kind = i;
                    ColorUtility.TryParseHtmlString(Kinds[i].Hex, out _base);
                    _dirty = true;
                    Save(i);
                }
                _kind = keep; _base = keepColor; _dirty = true;
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("控え（.bak）から戻す", big)) Restore(_kind);
        }

        private void Save(int kind)
        {
            string path = $"{Dir}/{Kinds[kind].File}.png";
            string full = Path.GetFullPath(path);

            // ⚠️ 上書きの前に控える。⭐ 既に控えがあるときは触らない（最初の1枚を守る）
            string bak = full + ".bak";
            if (File.Exists(full) && !File.Exists(bak)) File.Copy(full, bak);

            var tex = Make();
            File.WriteAllBytes(full, tex.EncodeToPNG());
            DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // ⚠️ 取り込み設定も必ず入れ直す。9スライスの枠と Full Rect が消えると角が歪む
            var im = (TextureImporter)AssetImporter.GetAtPath(path);
            im.textureType = TextureImporterType.Sprite;
            im.spriteImportMode = SpriteImportMode.Single;
            im.spriteBorder = Border;
            im.alphaIsTransparency = true;
            im.mipmapEnabled = false;
            im.wrapMode = TextureWrapMode.Clamp;
            im.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings();
            im.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // ⚠️ Tight だと SpriteRenderer が警告を出す
            im.SetTextureSettings(settings);
            im.SaveAndReimport();

            Debug.Log($"書き出した: {path}（控え: {Path.GetFileName(bak)}）");
        }

        private static void Restore(int kind)
        {
            string path = $"{Dir}/{Kinds[kind].File}.png";
            string full = Path.GetFullPath(path);
            string bak = full + ".bak";
            if (!File.Exists(bak))
            {
                EditorUtility.DisplayDialog("戻せない",
                    "控え（.bak）がありません。まだ一度も書き出していないか、控えを消しています。", "わかった");
                return;
            }
            File.Copy(bak, full, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"控えから戻した: {path}");
        }

        private void OnDisable()
        {
            if (_preview != null) DestroyImmediate(_preview);
        }
    }
}
