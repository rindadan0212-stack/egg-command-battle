using UnityEngine;

namespace EggCommand.View
{
    /// <summary>回る立体のさいころ。⭐ **UI へ貼るための絵を1枚焼く。**
    ///
    /// ⭐ **なぜ RenderTexture 越しか**（2026-08-20・作者の指示「B で実装」）:
    /// 画面（Canvas）は操作のたびに丸ごと組み直されるので、さいころは
    /// **画面の外の層**に置いてある（<see cref="TrailDice"/> の但し書き）。
    /// ⚠️ 立体をそのまま Canvas の手前へ置くと、その「組み直しから守る」仕組みの外へ出る。
    /// ⭐ 焼いた絵を <c>RawImage</c> に貼れば、いまの作りを1つも壊さずに済む。
    ///
    /// ⚠️ **出目はここで決めない。**<see cref="Core.Trails.Roll"/> が先に決めたものを
    /// 「その面が正面に来る向き」へ回すだけ（決めると出所が2つになる）。</summary>
    public sealed class DieCube : MonoBehaviour
    {
        /// <summary>焼く絵の一辺。⚠️ ドットが溶けないよう、面の絵（128）の倍数にする。
        /// ⭐ **画面に出す大きさ（<see cref="TrailDice"/>）の整数分の1**にすること。
        /// ⚠️ 半端な倍率で引き伸ばすと、Point で拡大してもドットの大きさが揃わない
        /// （256 を 660 に伸ばして 2.58倍になっていた・2026-08-20）。</summary>
        public const int Pixels = 384;

        /// <summary>撮る箱の大きさ。⭐ 立方体の対角（√3）が入るだけの余白を取る。</summary>
        private const float Framing = 1.15f;

        /// <summary>⚠️ **他の物と重ならない場所で撮る。**
        /// ⭐ 層（Layer）を足すと ProjectSettings を触ることになるので、
        /// 遠くへ置いて専用のカメラだけが見えるようにする。</summary>
        private static readonly Vector3 FarAway = new Vector3(0f, -5000f, 0f);

        /// <summary>⭐ **一番明るくなる面の向き。**左上・手前。
        ///
        /// ⚠️ 実際に照らさない（Unlit）。面の向きから明るさを**自分で**決める
        /// ── ドット絵の面塗りと同じ作法で、ライティングの階調を出さないため。
        /// ⚠️ **手前（-Z ＝ カメラ側）を明るく**すること。⭐ こちらを向いた面が読む面なので、
        /// ここを暗くすると**一番読みたい面が一番暗い**という逆さまの絵になる
        /// （2026-08-20 に実際にそうなって、焼いた絵を見て気づいた）。</summary>
        private static readonly Vector3 Light = new Vector3(-0.35f, 0.55f, -0.75f);

        /// <summary>一番暗い面の明るさ。⚠️ 0 にすると裏面が黒く潰れて立体に見えない。</summary>
        private const float Darkest = 0.45f;

        /// <summary>⭐ 面の並び。**向かい合う面の和が 7**（本物のさいころと同じ）。
        /// 並びは <see cref="Faces"/> の向きと同じ順。</summary>
        private static readonly int[] Pips = { 1, 6, 2, 5, 3, 4 };

        /// <summary>面の正面の向き。⚠️ <see cref="Pips"/> と同じ並び。</summary>
        private static readonly Vector3[] Faces =
        {
            Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down,
        };

        private Camera _camera;
        private MeshRenderer _skin;
        private Transform _die;
        private RenderTexture _shot;

        /// <summary>焼き上がった絵。⚠️ 使い終わったら <see cref="Dismiss"/> を呼ぶこと。</summary>
        public RenderTexture Shot { get { return _shot; } }

        /// <summary>網と材質は**1度だけ**作って使い回す。
        ///
        /// ⚠️ **振るたびに作ると、そのぶん丸ごと残る。**Unity は実行時に <c>new</c> した
        /// <see cref="Mesh"/> / <see cref="Material"/> / <see cref="Texture2D"/> を、
        /// GameObject を壊しても回収しない（`Resources.UnloadUnusedAssets` か場面の入れ替えまで）。
        /// ⚠️ 本作は場面が1つなので、**永久に残る**。
        /// ⭐ 面の絵は 128×128 の RGBA が6枚 ＝ **1回振るごとに約 384 KB**。
        /// 段5 の潜入で 9〜11 回振るので、1回の潜入で 4 MB 近く積み上がっていた
        /// （2026-08-21 の監査で発覚）。
        /// ⭐ 中身は毎回まったく同じなので、作り直す理由が最初から無い。</summary>
        private static Mesh _mesh;
        private static Material[] _materials;

        /// <summary>立体のさいころを1つ用意する。⚠️ 作れなければ null（呼び側は平面へ落とす）。</summary>
        public static DieCube Make()
        {
            var mesh = _mesh != null ? _mesh : (_mesh = BuildMesh());
            if (mesh == null) return null;

            var materials = _materials != null ? _materials : (_materials = BuildMaterials());
            if (materials == null) return null;

            var root = new GameObject("DieCube");
            root.transform.position = FarAway;
            var cube = root.AddComponent<DieCube>();

            var body = new GameObject("Die", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(root.transform, false);
            body.GetComponent<MeshFilter>().sharedMesh = mesh;
            var skin = body.GetComponent<MeshRenderer>();
            skin.sharedMaterials = materials;
            // ⚠️ 影も光も要らない（Unlit なので受けないが、投げるほうは別に切る）
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            skin.receiveShadows = false;

            var shot = new RenderTexture(Pixels, Pixels, 16, RenderTextureFormat.ARGB32);
            shot.filterMode = FilterMode.Point;   // ⭐ ドットを溶かさない
            shot.Create();

            var eye = new GameObject("Eye", typeof(Camera));
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0f, -4f);
            var camera = eye.GetComponent<Camera>();
            // ⚠️ 本編のカメラと同じ orthographic にそろえる（パースだけ別、は目に付く）
            camera.orthographic = true;
            camera.orthographicSize = Framing;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 12f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            // ⭐ 透けさせる。⚠️ 不透明だと四角い板が乗って見える
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.targetTexture = shot;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            // ⚠️ **音を二重に拾わせない。**主カメラ側のリスナーだけが要る
            camera.enabled = true;

            cube._camera = camera;
            cube._skin = skin;
            cube._die = body.transform;
            cube._shot = shot;
            cube.Face(1, 0f);
            return cube;
        }

        /// <summary>その目が正面を向く姿勢。⭐ **出目 → 向き の唯一の出所。**</summary>
        public static Quaternion PoseOf(int pips)
        {
            for (int i = 0; i < Pips.Length; i++)
            {
                if (Pips[i] != pips) continue;
                // ⭐ その面を**カメラのほう**へ向ける。⚠️ カメラは -Z 側に居る
                return Quaternion.FromToRotation(Faces[i], Vector3.back);
            }
            return Quaternion.identity;
        }

        /// <summary>その目を正面にして、<paramref name="tilt"/> だけ捻る。
        /// ⭐ 捻りを入れるのは、真正面だと**立体に見えない**から。</summary>
        public void Face(int pips, float tilt)
        {
            if (_die == null) return;
            // ⚠️ X は**負**。⭐ 正にすると上面が奥へ倒れて**底面**が見え、
            //    机に転がったさいころの見え方から外れる（2026-08-20 に焼いた絵で気づいた）
            _die.localRotation = Quaternion.Euler(-tilt * 0.6f, tilt, tilt * 0.3f) * PoseOf(pips);
            Shade();
        }

        /// <summary>好きな向きに置く。⭐ 回している最中はこちら。</summary>
        public void Turn(Quaternion rotation)
        {
            if (_die == null) return;
            _die.localRotation = rotation;
            Shade();
        }

        /// <summary>面ごとの明るさを、いまの向きから決める。
        ///
        /// ⭐ **照らさずに塗る。**⚠️ 本物のライティングを入れると、
        /// 面の中に階調が出てドット絵から浮く。面は**1面まるごと1色**で暗くする。</summary>
        private void Shade()
        {
            if (_skin == null) return;
            var block = new MaterialPropertyBlock();
            var light = Light.normalized;
            for (int i = 0; i < Faces.Length; i++)
            {
                Vector3 normal = _die.localRotation * Faces[i];
                // ⭐ 正面（1）から真後ろ（Darkest）へ。⚠️ 負にしない
                float lit = Mathf.Clamp01((Vector3.Dot(normal, light) + 1f) * 0.5f);
                float tone = Mathf.Lerp(Darkest, 1f, lit);
                block.Clear();
                block.SetColor(Tint, new Color(tone, tone, tone, 1f));
                _skin.SetPropertyBlock(block, i);
            }
        }

        private static readonly int Tint = Shader.PropertyToID("_Color");

        /// <summary>片づける。⚠️ RenderTexture は自分で解放しないと残る。
        ///
        /// ⚠️ 網と材質は**壊さない** ── <see cref="_mesh"/> / <see cref="_materials"/> で
        /// 使い回しているので、壊すと次に振ったときに空のさいころが出る。</summary>
        public void Dismiss()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_shot != null)
            {
                _shot.Release();
                Destroy(_shot);
                _shot = null;
            }
            Destroy(gameObject);
        }

        /// <summary>6面それぞれに別の絵を貼れる立方体。
        ///
        /// ⚠️ **Unity 標準の Cube は使えない。**6面の UV が同じなので、
        /// 面ごとに別の絵（die-1〜6）を貼り分けられない。
        /// ⭐ 頂点24・面ごとのサブメッシュ6で自分で組む。</summary>
        private static Mesh BuildMesh()
        {
            var mesh = new Mesh { name = "Die" };
            var points = new Vector3[24];
            var uv = new Vector2[24];
            var tris = new int[6][];

            for (int f = 0; f < 6; f++)
            {
                Vector3 normal = Faces[f];
                // ⭐ 面の中で「横」「縦」に当たる向き。⚠️ 上下面だけ別に取る（外積が潰れるため）
                Vector3 right = Mathf.Abs(normal.y) > 0.5f
                    ? Vector3.right : Vector3.Cross(Vector3.up, normal).normalized;
                Vector3 up = Vector3.Cross(normal, right).normalized;

                int at = f * 4;
                points[at + 0] = (normal - right - up) * 0.5f;
                points[at + 1] = (normal - right + up) * 0.5f;
                points[at + 2] = (normal + right + up) * 0.5f;
                points[at + 3] = (normal + right - up) * 0.5f;
                uv[at + 0] = new Vector2(0f, 0f);
                uv[at + 1] = new Vector2(0f, 1f);
                uv[at + 2] = new Vector2(1f, 1f);
                uv[at + 3] = new Vector2(1f, 0f);
                // ⚠️ **巻きは「面の外から見て表」。**逆にすると Cull Back で消える
                tris[f] = new[] { at, at + 2, at + 1, at, at + 3, at + 2 };
            }

            mesh.vertices = points;
            mesh.uv = uv;
            mesh.subMeshCount = 6;
            for (int f = 0; f < 6; f++) mesh.SetTriangles(tris[f], f);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>面ごとの材質。⚠️ 絵が1枚でも欠けたら作らない（黙って無地にしない）。</summary>
        private static Material[] BuildMaterials()
        {
            // ⭐ 裏を描かず・奥行きを書き・照らさないもの。
            // ⚠️ `Sprites/Default` では駄目（両面＋奥行き無しで**中が透ける**・2026-08-20）
            var shader = Shader.Find("EggCommand/DieFace");
            if (shader == null)
            {
                Debug.LogError("さいころの材質が作れない: EggCommand/DieFace が無い"
                    + "（Resources/Shaders/DieFace.shader）");
                return null;
            }

            var made = new Material[6];
            for (int f = 0; f < 6; f++)
            {
                var art = BuildFace(Pips[f]);
                if (art == null) return null;
                made[f] = new Material(shader) { mainTexture = art };
            }
            return made;
        }

        /// <summary>面の絵を作る。⭐ **面を塗って、目だけ抜く。**
        ///
        /// ⚠️ 元の絵（`die-N`）は**丸角の四角に目が穴として抜かれた白いシルエット**で、
        /// 平面の画面では色を乗せて使う前提になっている。
        /// ⚠️ そのまま立方体に貼ると、目が**素通しの穴**になって向こう側が見える
        /// （2026-08-20 に焼いた絵を見て気づいた）。
        ///
        /// ⭐ 立方体では**面そのものが四角い**ので、丸角の輪郭は要らない。
        /// 要るのは「目の位置」だけなので、**縁から届かない穴 ＝ 目**として取り出す。</summary>
        private static Texture2D BuildFace(int pips)
        {
            string path = "UI/icon/die-" + pips;
            var art = Resources.Load<Texture2D>(path);
            if (art == null)
            {
                Debug.LogError($"さいころの面が無い: Resources/{path}");
                return null;
            }

            // ⚠️ 読み取り可で取り込まれているとは限らないので、一度焼いてから読む
            var pad = RenderTexture.GetTemporary(art.width, art.height, 0,
                RenderTextureFormat.ARGB32);
            Graphics.Blit(art, pad);
            var was = RenderTexture.active;
            RenderTexture.active = pad;
            var read = new Texture2D(art.width, art.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0f, 0f, art.width, art.height), 0, 0);
            read.Apply();
            RenderTexture.active = was;
            RenderTexture.ReleaseTemporary(pad);

            int w = read.width, h = read.height;
            var source = read.GetPixels32();
            // ⭐ 縁から辿れる透明 ＝ 四角の**外**。辿れない透明 ＝ **目**
            var outside = Outside(source, w, h);

            var face = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,   // ⭐ ドットのまま拡大する
                wrapMode = TextureWrapMode.Clamp,
            };
            var paint = new Color32[w * h];
            Color32 body = Ui.Paper;
            Color32 pip = Ui.Ink;
            for (int i = 0; i < paint.Length; i++)
            {
                bool hole = source[i].a < 128;
                // ⚠️ 外側も塗る ── 立方体の面は端まで色が要る
                paint[i] = hole && !outside[i] ? pip : body;
            }
            face.SetPixels32(paint);
            face.Apply();
            Object.Destroy(read);
            return face;
        }

        /// <summary>縁から辿れる透明な点。⭐ **四角の外**（目の穴と見分けるため）。</summary>
        private static bool[] Outside(Color32[] pixels, int w, int h)
        {
            var outside = new bool[w * h];
            var stack = new System.Collections.Generic.Stack<int>();
            for (int x = 0; x < w; x++)
            {
                Push(stack, outside, pixels, x, 0, w, h);
                Push(stack, outside, pixels, x, h - 1, w, h);
            }
            for (int y = 0; y < h; y++)
            {
                Push(stack, outside, pixels, 0, y, w, h);
                Push(stack, outside, pixels, w - 1, y, w, h);
            }
            while (stack.Count > 0)
            {
                int at = stack.Pop();
                int x = at % w, y = at / w;
                Push(stack, outside, pixels, x - 1, y, w, h);
                Push(stack, outside, pixels, x + 1, y, w, h);
                Push(stack, outside, pixels, x, y - 1, w, h);
                Push(stack, outside, pixels, x, y + 1, w, h);
            }
            return outside;
        }

        private static void Push(System.Collections.Generic.Stack<int> stack, bool[] outside,
            Color32[] pixels, int x, int y, int w, int h)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int at = y * w + x;
            if (outside[at] || pixels[at].a >= 128) return;
            outside[at] = true;
            stack.Push(at);
        }
    }
}
