using System;
using System.Collections.Generic;
using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>卵強奪の盤。⭐ ここだけは UI ではなく**ワールド空間の 2D**で作る。
    ///
    /// 引っ張って離すと飛んでいき、壁で跳ね返る。卵に届けば盗み、届かなければ戦闘。
    ///
    /// ⚠️ 当たり判定も跳ね返りも <see cref="Core.Steal"/> が全部決める。
    /// ここは Core が返した軌跡を**なぞって見せるだけ**。
    /// 見た目の側で当たりを取り直すと、遊びの結果が2つの出所を持つことになる。
    ///
    /// 座標: 盤は x 0〜160・y は下向き。世界は中央原点・y は上向きなので、
    /// <see cref="ToWorld"/> が1箇所で変換する。
    /// </summary>
    public sealed class StealStage : MonoBehaviour
    {
        /// <summary>1秒に進む距離（盤の単位）。⭐ 手応えの速さはここだけで決まる。</summary>
        private const float FlightSpeed = 260f;

        /// <summary>引っ張りとみなす最小の長さ（画面の割合）。⚠️ これ未満は誤タップ。</summary>
        private const float MinPull = 0.04f;

        private StealField _field;
        private double _budget;
        private Action<StealRun> _onDone;

        private Transform _runner;
        private LineRenderer _guide;
        private Camera _camera;
        private float _cameraSizeBefore;
        private Vector3 _cameraPosBefore;

        private bool _dragging;
        private Vector2 _dragFrom;
        private Vector2 _dragTo;

        private StealRun _run;
        private float _travelled;
        private readonly List<Transform> _trail = new List<Transform>();

        public bool Flying { get { return _run != null; } }

        public static StealStage Create(StealField field, double budget, Creature leader,
            string nestSpeciesId, Action<StealRun> onDone)
        {
            var go = new GameObject("Steal Stage");
            var stage = go.AddComponent<StealStage>();
            stage.Build(field, budget, leader, nestSpeciesId, onDone);
            return stage;
        }

        // ── 盤を組む ────────────────────────────────────

        private void Build(StealField field, double budget, Creature leader,
            string nestSpeciesId, Action<StealRun> onDone)
        {
            _field = field;
            _budget = budget;
            _onDone = onDone;

            _camera = Camera.main;
            _cameraSizeBefore = _camera.orthographicSize;
            _cameraPosBefore = _camera.transform.position;
            // 盤の全体が入る大きさに合わせる。
            // ⚠️ 高さだけで決めない。縦持ち（横が狭い）なので、浅い巣では**横が先に足りなくなり**、
            //    盤の左右が画面の外へ出る（段1で実際に切れた）。縦と横の両方を満たす。
            float halfHeight = (float)field.Height / 2f + 16f;
            float halfWidthNeeded = ((float)Steal.FieldWidth / 2f + 8f) / _camera.aspect;
            _camera.orthographicSize = Mathf.Max(halfHeight, halfWidthNeeded);
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            // 地。⚠️ 盤の外と中を色で分ける（線を引かずに面で）
            Solid("Board", new Color32(0x14, 0x18, 0x12, 0xff),
                new Vector2(0f, 0f), new Vector2((float)Steal.FieldWidth, (float)field.Height), 5f);

            // 親が塞ぐ帯。隙間の左右2枚
            float bandMid = (float)(field.BandTop + field.BandBottom) / 2f;
            float bandHeight = (float)(field.BandBottom - field.BandTop);
            foreach (var span in Steal.ParentSpans(field))
            {
                float width = (float)(span.To - span.From);
                float centerX = (float)(span.From + span.To) / 2f;
                Solid("Wall", new Color32(0x6b, 0x4a, 0x3a, 0xff),
                    ToWorld(centerX, bandMid), new Vector2(width, bandHeight), 3f);
            }

            // 親。⭐ 塞いでいる側の真ん中に立たせる（誰が邪魔しているのか分かるように）
            var species = SpeciesTable.ById(nestSpeciesId);
            var spans = Steal.ParentSpans(field);
            if (spans.Count > 0)
            {
                var widest = spans[0];
                foreach (var s in spans) if (s.To - s.From > widest.To - widest.From) widest = s;
                float centerX = (float)(widest.From + widest.To) / 2f;
                // ⚠️ 帯より大きく描かない。はみ出すと「どこが塞がっているか」が読めなくなる
                PixelObject("Parent", species.Sprite, species.Palettes[0],
                    ToWorld(centerX, bandMid), bandHeight * 0.9f, 2f);
            }

            // 卵
            PixelObject("Egg", EggArt.Sprite, EggArt.Shell,
                ToWorld((float)field.Egg.X, (float)field.Egg.Y),
                (float)Steal.EggRadius * 2.4f, 2f);

            // 走る者。⭐ 出撃の先頭をそのまま飛ばす
            var runner = PixelObject("Runner",
                Creatures.SpeciesOf(leader).Sprite, Creatures.PaletteOf(leader),
                ToWorld((float)field.Start.X, (float)field.Start.Y),
                (float)Steal.RunnerRadius * 2.6f, 1f);
            _runner = runner.transform;

            // ⭐ どこまで届くかを線で見せる（字で「飛距離 204」と書かない）。
            //    真上へ撃ったときに止まる高さ。届かない巣では卵の下に線が残る。
            float reachY = (float)field.Start.Y - (float)budget;
            if (reachY > 0f && reachY < (float)field.Height)
            {
                Solid("Reach", new Color32(0xff, 0xd9, 0x77, 0x55),
                    ToWorld((float)Steal.FieldWidth / 2f, reachY),
                    new Vector2((float)Steal.FieldWidth, 1.5f), 4.5f);
            }

            // 狙いの線
            var guideGo = new GameObject("Guide");
            guideGo.transform.SetParent(transform, false);
            _guide = guideGo.AddComponent<LineRenderer>();
            _guide.useWorldSpace = true;
            _guide.positionCount = 0;
            _guide.startWidth = 2.2f;
            _guide.endWidth = 0.6f;
            _guide.material = new Material(Shader.Find("Sprites/Default"));
            _guide.startColor = new Color32(0xd8, 0xb4, 0x5c, 0xdd);
            _guide.endColor = new Color32(0xd8, 0xb4, 0x5c, 0x22);
            _guide.sortingOrder = 6;
        }

        /// <summary>盤を畳む。⚠️ カメラを戻すのは <see cref="OnDestroy"/> ではなくここ。
        ///
        /// Destroy はフレームの終わりまで効かないので、OnDestroy で戻すと
        /// **次の盤が設定したカメラを、古い盤が後から上書きする**（実際そうなった）。
        /// 畳むのは「畳むと決めた瞬間」でなければならない。
        /// 名前も変える。変えないと GameObject.Find が死んだ盤を拾い続ける。</summary>
        public void Dismiss()
        {
            if (_camera != null)
            {
                _camera.orthographicSize = _cameraSizeBefore;
                _camera.transform.position = _cameraPosBefore;
                _camera = null;
            }
            gameObject.name = "Steal Stage (畳んだ)";
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        /// <summary>盤の座標を世界の座標へ。⚠️ 上下の反転をここ1箇所に閉じ込める。</summary>
        private Vector2 ToWorld(float fx, float fy)
        {
            return new Vector2(fx - (float)Steal.FieldWidth / 2f, (float)_field.Height / 2f - fy);
        }

        // ── 引っ張る ────────────────────────────────────

        private void Update()
        {
            if (_run != null) { StepFlight(); return; }

            // ⭐ 触ってほしいものを脈打たせる。⚠️「引っ張って離す」と書く代わり。
            //    引っ張っている間は止める（もう触れていると分かっているので）
            if (_runner != null)
            {
                float pulse = _dragging ? 1f : 1f + Mathf.Sin(Time.time * 4.5f) * 0.09f;
                float size = (float)Steal.RunnerRadius * 2.6f * pulse;
                _runner.localScale = new Vector3(size, size, 1f);
            }

            // ⚠️ マウスも指も同じ扱いにする（Editor と実機で操作が変わらないように）
            if (Input.GetMouseButtonDown(0))
            {
                _dragging = true;
                _dragFrom = Input.mousePosition;
                _dragTo = _dragFrom;
            }
            else if (_dragging && Input.GetMouseButton(0))
            {
                _dragTo = Input.mousePosition;
                DrawGuide();
            }
            else if (_dragging && Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                _guide.positionCount = 0;
                Vector2 pull = _dragFrom - _dragTo;
                // ⭐ 引っ張った向きの**逆**へ飛ぶ（パチンコと同じ）
                // ⚠️ Screen は自前の画面 enum と名前がぶつかる。UnityEngine のほうを明示する
                float shortSide = Mathf.Min(UnityEngine.Screen.width, UnityEngine.Screen.height);
                if (pull.magnitude >= shortSide * MinPull) Fire(pull);
            }
        }

        /// <summary>引っ張っている向きに、飛ぶ先を点線で見せる。</summary>
        private void DrawGuide()
        {
            Vector2 pull = _dragFrom - _dragTo;
            if (pull.sqrMagnitude < 1f) { _guide.positionCount = 0; return; }
            Vector2 direction = pull.normalized;

            Vector3 origin = _runner.position;
            const int Points = 12;
            _guide.positionCount = Points;
            for (int i = 0; i < Points; i++)
            {
                float t = i * 9f;
                _guide.SetPosition(i, origin + new Vector3(direction.x * t, direction.y * t, 0f));
            }
        }

        /// <summary>離した。⚠️ 角度以外に入力は無い。飛距離は編成のスピード合計。</summary>
        private void Fire(Vector2 pull)
        {
            Vector2 direction = pull.normalized;
            // Core の角度は「上向きが 0、時計回り」。世界は y が上なのでこの式になる
            double angle = Mathf.Atan2(direction.x, direction.y);
            _run = Steal.Launch(_field, angle, _budget);
            _travelled = 0f;
        }

        // ── 飛ぶ ────────────────────────────────────────

        private void StepFlight()
        {
            var path = _run.Path;
            _travelled += Time.deltaTime * FlightSpeed;
            int index = Mathf.FloorToInt(_travelled);

            if (index >= path.Count - 1)
            {
                // 着地。⭐ 判定は既に Core が出しているので、ここでやり直さない
                var last = path[path.Count - 1];
                _runner.position = ToWorld((float)last.X, (float)last.Y);
                var finished = _run;
                _run = null;
                if (_onDone != null) _onDone(finished);
                return;
            }

            var point = path[index];
            _runner.position = ToWorld((float)point.X, (float)point.Y);

            // 通った跡を残す。⚠️ 1点ずつ置くと数千個になるので間引く
            if (index % 7 == 0 && _trail.Count < 140)
            {
                var dot = Solid("Trail", new Color32(0xef, 0xe9, 0xdc, 0x55),
                    ToWorld((float)point.X, (float)point.Y), new Vector2(2.4f, 2.4f), 4f);
                _trail.Add(dot.transform);
            }
        }

        // ── 部品 ────────────────────────────────────────

        private static Sprite _white;

        private static Sprite White()
        {
            if (_white == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                texture.filterMode = FilterMode.Point;
                _white = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            }
            return _white;
        }

        private GameObject Solid(string name, Color color, Vector2 center, Vector2 size, float depth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(center.x, center.y, depth);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = White();
            renderer.color = color;
            renderer.sortingOrder = Mathf.RoundToInt(-depth * 10f);
            return go;
        }

        private GameObject PixelObject(string name, PixelSprite sprite, Palette palette,
            Vector2 center, float size, float depth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(center.x, center.y, depth);
            var renderer = go.AddComponent<SpriteRenderer>();
            // pixelsPerUnit = 幅 なので、1体ぶんが 1 単位になる。そこから実寸へ伸ばす
            renderer.sprite = PixelSpriteTexture.ToSprite(sprite, palette, sprite.Width);
            renderer.sortingOrder = Mathf.RoundToInt(-depth * 10f) + 1;
            go.transform.localScale = new Vector3(size, size, 1f);
            return go;
        }
    }

    /// <summary>卵の意匠。⚠️ Core には置かない（遊びの規則ではなく見た目）。</summary>
    public static class EggArt
    {
        public static readonly PixelSprite Sprite = PixelSprite.Parse(new[]
        {
            "................",
            "................",
            ".....111111.....",
            "....11322211....",
            "...1132222211...",
            "..113222222211..",
            "..122222222221..",
            ".11222222222211.",
            ".12222222222221.",
            ".12222222222221.",
            ".11222222222211.",
            "..112222222211..",
            "...1122222211...",
            "....11111111....",
            "................",
            "................",
        });

        public static readonly Palette Shell = new Palette("#6b5a3e", "#eae0c0", "#fdf6e0");
    }
}
