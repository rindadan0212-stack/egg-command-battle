using System;
using System.Collections.Generic;
using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>潜入の盤。⭐ ここだけは UI ではなく**ワールド空間の 2D**で作る。
    ///
    /// ⭐ **3体を1体ずつ投げる。**着地した個体は盤に残り、次の発射台になる。
    /// 飛距離は**飛ぶ個体の速度**で決まる（編成の合計ではない）。
    /// 卵に届けば盗み、親に触れたら戦闘、3体使い切っても戦闘。
    ///
    /// ⚠️ **以前はここが旧 <see cref="Core.Steal.Launch"/>（一投・合計速度・関門なし）**
    /// を呼んでいた。盤の生成側は「3体リレー＋関門で解ける盤」だけを出荷していたので、
    /// **検査が保証している性質と、遊ばれる性質が無関係**だった。
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

        /// <summary>走者を掴めるとみなす半径（盤の単位）。
        /// ⚠️ ここより外を触ったら「上を見る」ほうの操作にする。</summary>
        private const float GrabRadius = 26f;

        /// <summary>失速し始める残りの割合。⭐ ここから先はだんだん遅くなる。
        /// ⚠️ 等速のまま終点で止めると「ビタ止まり」になって、
        ///    力尽きたのか壁に当たったのか区別が付かない。</summary>
        private const float SlowFrom = 0.22f;
        /// <summary>失速しきったときの速さの割合。⚠️ 0 にすると永久に着かない。</summary>
        private const float SlowTo = 0.18f;

        /// <summary>目盛りの間隔（盤の単位＝メートル）。</summary>
        private const float MeterStep = 50f;

        /// <summary>画面に映る世界の幅。⭐ **道より広く取る**。
        /// ⚠️ 道と同じにすると目盛りを置く場所が道の上しか無くなり、線が盤を横切る。</summary>
        private const float ViewWidth = 160f;

        /// <summary>カメラが追いつく速さ。⚠️ 速すぎると画面が跳ねて酔う。</summary>
        private const float CameraCatchUp = 90f;

        private StealField _field;
        private Action<StealRun> _onDone;

        /// <summary>潜入そのもの。⭐ **判定も発射台も Core が持つ。**画面は選ばせて描くだけ。</summary>
        private Steal.Infiltration _infil;
        /// <summary>いま投げようとしている個体（<see cref="Steal.Infiltration.Party"/> の添字）。</summary>
        private int _member = -1;
        /// <summary>どこから投げるか。⚠️ **-1 は初期位置**。それ以外は Pads の添字。</summary>
        private int _pad = -1;

        /// <summary>まだ投げていない個体の絵（出発点に並ぶ）。</summary>
        private readonly Dictionary<int, Transform> _waiting = new Dictionary<int, Transform>();
        /// <summary>着地した個体の絵（＝発射台）。</summary>
        private readonly List<Transform> _pads = new List<Transform>();
        /// <summary>関門の絵。⭐ 壁を壊したら消すので持っておく。</summary>
        private readonly List<GameObject> _gates = new List<GameObject>();
        /// <summary>いま届く距離を見せる線。⭐ 選んだ個体で変わるので作り直す。</summary>
        private GameObject _reach;

        private Transform _runner;
        private LineRenderer _guide;
        private Camera _camera;
        private float _cameraSizeBefore;
        private Vector3 _cameraPosBefore;

        private bool _dragging;
        private Vector2 _dragFrom;
        private Vector2 _dragTo;

        /// <summary>盤を上下に見回している最中（走者から離れたところを触った）。</summary>
        private bool _looking;
        private float _lookFrom;
        private float _cameraFrom;
        /// <summary>いま見ている高さ（世界座標）。⭐ 追従もここを動かすだけ。</summary>
        private float _cameraY;

        private StealRun _run;
        private float _travelled;
        private readonly List<Transform> _trail = new List<Transform>();

        public bool Flying { get { return _run != null; } }

        /// <param name="party">⭐ **3体そのまま**渡す。誰をいつ投げるかは盤で選ぶ。</param>
        public static StealStage Create(StealField field, IReadOnlyList<Creature> party,
            string nestSpeciesId, Action<StealRun> onDone)
        {
            var go = new GameObject("Steal Stage");
            var stage = go.AddComponent<StealStage>();
            stage.Build(field, party, nestSpeciesId, onDone);
            return stage;
        }

        // ── 盤を組む ────────────────────────────────────

        private void Build(StealField field, IReadOnlyList<Creature> party,
            string nestSpeciesId, Action<StealRun> onDone)
        {
            _field = field;
            _onDone = onDone;
            _infil = new Steal.Infiltration(field, party);

            _camera = Camera.main;
            _cameraSizeBefore = _camera.orthographicSize;
            _cameraPosBefore = _camera.transform.position;
            // ⭐ 盤を縮めて収めない。**倍率は横幅だけで決める**。
            // ⚠️ 全体が入るように縮めると、深い巣ほど絵が小さくなって距離感が消える。
            //    奥行きは上へ伸ばして、カメラで追う。
            _camera.orthographicSize = ViewWidth / 2f / _camera.aspect;
            _camera.transform.position = new Vector3(0f, 0f, -10f);

            // 地。⚠️ 盤の外と中を分ける。⭐ 素の四角ではなくタイルの絵を敷く
            //    （色だけの面は「まだ作っていない」ように見える）
            Skinned("Board", "tile", new Color32(0x6f, 0x8a, 0x5e, 0xff),
                new Vector2(0f, 0f), new Vector2((float)Steal.FieldWidth, (float)field.Height), 5f);

            // 親が塞ぐ帯。隙間の左右2枚
            float bandMid = (float)(field.BandTop + field.BandBottom) / 2f;
            float bandHeight = (float)(field.BandBottom - field.BandTop);

            // ⭐ 塞いでいる幅を**絵そのもの**で埋める。薄い箱は描かない。
            // ⚠️ 箱を描いて中に立たせると、「箱が当たり判定で絵は飾り」に見える。
            // ⚠️ 絵を並べて幅を埋めない（増殖して見える）。塞ぐ幅のほうを
            //    Steal.ParentWidth ＝ 絵1体ぶん に狭めてある。
            // ⚠️ **等方に縮めない。**以前は Mathf.Min(幅, 帯の厚み) で正方形にしていたので、
            //    塞ぐ幅が 56〜75 まで広がるのに絵は最大 30 しか無く、
            //    盤幅の 1/4 が「見えないのに当たる」状態だった。
            // ⭐ 判定（ParentSpans）の幅そのままに伸ばす。絵と当たりが必ず一致する。
            var species = SpeciesTable.ById(nestSpeciesId);
            foreach (var span in Steal.ParentSpans(field))
            {
                float centerX = (float)(span.From + span.To) / 2f;
                float width = (float)(span.To - span.From);
                var parent = PixelObject("Parent", species.Sprite, species.Palettes[0],
                    ToWorld(centerX, bandMid), 1f, 3f);
                parent.transform.localScale = new Vector3(width, bandHeight, 1f);
            }

            // ⭐ 関門。⚠️ 以前は1枚も描いていなかった（盤に在るのに見えなかった）
            BuildGates();

            // 卵
            PixelObject("Egg", EggArt.Sprite, EggArt.Shell,
                ToWorld((float)field.Egg.X, (float)field.Egg.Y),
                (float)Steal.EggRadius * 2.4f, 2f);

            // ⭐ まだ投げていない3体を出発点に並べる。触れば選べる
            BuildWaiting();
            Select(_infil.Left.Count > 0 ? _infil.Left[0] : -1);

            // ⭐ 画面の端に目盛り。距離が字と線の両方で分かる
            BuildMeters();

            // ⭐ 最初は走者のところを見る。上は自分で見に行く
            _cameraY = ClampCamera(ToWorld((float)field.Start.X, (float)field.Start.Y).y);
            ApplyCamera();

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

        /// <summary>目盛り。⭐ **道の外（右の余白）**に短い線と数字を置く。
        /// ⚠️ 盤を横切る線を引かない。道の上に線があると通り道の一部に見える。</summary>
        private void BuildMeters()
        {
            float roadEdge = (float)Steal.FieldWidth / 2f;
            float tickFrom = roadEdge + 4f;
            float tickTo = roadEdge + 14f;
            // ⚠️ 左寄せにしたら「250」が画面の右端で切れた。
            //    画面の縁から右寄せで置く（何桁でも必ず収まる）
            float textRight = ViewWidth / 2f - 3f;

            for (float d = MeterStep; d <= (float)_field.Height; d += MeterStep)
            {
                float y = (float)_field.Start.Y - d;
                if (y < 0f) break;
                float worldY = ToWorld(0f, y).y;

                Skinned($"Tick {d}", "pill", new Color(1f, 1f, 1f, 0.65f),
                    new Vector2((tickFrom + tickTo) / 2f, worldY),
                    new Vector2(tickTo - tickFrom, 1.6f), 4.8f);

                var label = new GameObject($"Meter {d}");
                label.transform.SetParent(transform, false);
                label.transform.position = new Vector3(textRight, worldY, 4.7f);
                var text = label.AddComponent<TextMesh>();
                text.text = $"{(int)d}";
                text.font = Ui.TheFont;
                text.fontSize = 64;
                text.characterSize = 0.62f;   // ⚠️ 0.34 では小さすぎて読めなかった
                text.anchor = TextAnchor.MiddleRight;
                text.color = new Color(1f, 1f, 1f, 0.85f);
                // ⚠️ TextMesh は自前の材質を持たないので、フォントのものを貼る
                label.GetComponent<MeshRenderer>().sharedMaterial = Ui.TheFont.material;
            }
        }


        // ── 選ぶ ────────────────────────────────────────

        /// <summary>関門を描く。⭐ **要求するステと値を絵の上に出す。**
        /// ⚠️ 「攻撃力が足りないと壊せません」と文で書かない。
        /// 要求は比べるための数なので、数のまま出す。</summary>
        private void BuildGates()
        {
            for (int i = 0; i < _field.Gimmicks.Count; i++)
            {
                var gate = _field.Gimmicks[i];
                float mid = (float)(gate.Top + gate.Bottom) / 2f;
                float width = (float)(gate.To - gate.From);
                float height = (float)(gate.Bottom - gate.Top);

                var body = Skinned($"Gate {i}", "pill", GateColor(gate.Kind),
                    ToWorld((float)(gate.From + gate.To) / 2f, mid),
                    new Vector2(width, height), 4.2f);

                var label = new GameObject($"Gate {i} 要求");
                label.transform.SetParent(body.transform, false);
                label.transform.position = new Vector3(
                    body.transform.position.x, body.transform.position.y, 4.1f);
                var text = label.AddComponent<TextMesh>();
                text.text = $"{Stats.LabelOf(Steal.StatOf(gate.Kind))} {gate.Requires}";
                text.font = Ui.TheFont;
                text.fontSize = 64;
                text.characterSize = 0.44f;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = new Color(1f, 1f, 1f, 0.95f);
                label.GetComponent<MeshRenderer>().sharedMaterial = Ui.TheFont.material;

                _gates.Add(body);
            }
        }

        /// <summary>関門の色。⚠️ 種類が読めればよい（塗り分けだけ）。</summary>
        private static Color GateColor(GimmickKind kind)
        {
            switch (kind)
            {
                case GimmickKind.Wall: return new Color32(0x8a, 0x6f, 0x4e, 0xdd);
                case GimmickKind.Damage: return new Color32(0xb0, 0x53, 0x4a, 0xcc);
                default: return new Color32(0x4a, 0x63, 0xa8, 0xcc);
            }
        }

        /// <summary>壊れた壁を盤から消す。⭐ 開通が目で分かる。</summary>
        private void RefreshGates()
        {
            for (int i = 0; i < _gates.Count && i < _field.Gimmicks.Count; i++)
            {
                if (_gates[i] == null) continue;
                bool broken = _field.Gimmicks[i].Kind == GimmickKind.Wall
                    && _infil.Broken.Contains(i);
                if (broken) _gates[i].SetActive(false);
            }
        }

        /// <summary>まだ投げていない個体を出発点に並べる。</summary>
        private void BuildWaiting()
        {
            var left = new List<int>(_infil.Left);
            for (int i = 0; i < left.Count; i++)
            {
                int member = left[i];
                var creature = _infil.Party[member];
                // ⚠️ 重ならないように少しずらす。⭐ 触り分けられる間隔にする
                float offset = (i - (left.Count - 1) / 2f) * 18f;
                var go = PixelObject($"待機 {member}",
                    Creatures.SpeciesOf(creature).Sprite, Creatures.PaletteOf(creature),
                    ToWorld((float)_field.Start.X + offset, (float)_field.Start.Y),
                    (float)Steal.RunnerRadius * 2.2f, 1.2f);
                _waiting[member] = go.transform;
            }
        }

        /// <summary>投げる個体を選ぶ。⭐ 届く距離の線も引き直す（個体ごとに違う）。</summary>
        private void Select(int member)
        {
            _member = member;
            if (_member < 0) return;
            _pad = -1;
            PlaceRunner();
            DrawReach();
        }

        /// <summary>選んだ個体を、選んだ発射台の上に立たせる。</summary>
        private void PlaceRunner()
        {
            if (_member < 0) return;
            Transform mark;
            if (!_waiting.TryGetValue(_member, out mark)) return;
            _runner = mark;

            var at = _pad < 0 ? _field.Start : _infil.Pads[_pad];
            _runner.position = new Vector3(
                ToWorld((float)at.X, (float)at.Y).x, ToWorld((float)at.X, (float)at.Y).y, 1.2f);
        }

        /// <summary>⭐ どこまで届くかを線で見せる（字で「飛距離 204」と書かない）。
        /// ⚠️ **選んだ個体の速度**で決まる。誰を選ぶかで線が動くのが、この遊びの芯。</summary>
        private void DrawReach()
        {
            if (_reach != null) Destroy(_reach);
            if (_member < 0) return;

            double budget = Steal.DistanceFor(_infil.Party[_member]);
            var from = _pad < 0 ? _field.Start : _infil.Pads[_pad];
            float reachY = (float)from.Y - (float)budget;
            if (reachY <= 0f || reachY >= (float)_field.Height) return;

            _reach = Skinned("Reach", "pill", new Color32(0xff, 0xd9, 0x77, 0x88),
                ToWorld((float)Steal.FieldWidth / 2f, reachY),
                new Vector2((float)Steal.FieldWidth, 2.2f), 4.5f);
        }

        /// <summary>触った先にある「選べるもの」を拾う。⚠️ 走者そのものは掴んで引っ張る。</summary>
        /// <returns>何かを選んだら true。</returns>
        private bool PickAt(Vector2 touch)
        {
            // ⭐ 発射台（着地した個体）を選ぶ
            for (int i = 0; i < _infil.Pads.Count; i++)
            {
                var at = ToWorld((float)_infil.Pads[i].X, (float)_infil.Pads[i].Y);
                if (Vector2.Distance(touch, at) <= GrabRadius)
                {
                    _pad = i;
                    PlaceRunner();
                    DrawReach();
                    return true;
                }
            }
            // ⭐ 待機している個体を選ぶ
            foreach (var pair in _waiting)
            {
                if (pair.Key == _member) continue;
                if (Vector2.Distance(touch, pair.Value.position) <= GrabRadius)
                {
                    Select(pair.Key);
                    return true;
                }
            }
            return false;
        }

        /// <summary>盤の外を見ないように挟む。
        /// ⚠️ 始まりと終わりだけは端に張り付く（ここが「その限りでない」ところ）。</summary>
        private float ClampCamera(float y)
        {
            float half = _camera.orthographicSize;
            float top = (float)_field.Height / 2f;
            if (top <= half) return 0f;
            return Mathf.Clamp(y, -top + half, top - half);
        }

        private void ApplyCamera()
        {
            var at = _camera.transform.position;
            _camera.transform.position = new Vector3(0f, _cameraY, at.z);
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
                float size = (float)Steal.RunnerRadius * 2.2f * pulse;
                _runner.localScale = new Vector3(size, size, 1f);
            }

            // ⚠️ マウスも指も同じ扱いにする（Editor と実機で操作が変わらないように）
            if (Input.GetMouseButtonDown(0))
            {
                // ⭐ 走者を掴んだら狙う。離れたところなら**上を見に行く**。
                //    飛ばす前に奥行きを確かめて、位置を決められるようにする
                var touch = _camera.ScreenToWorldPoint(Input.mousePosition);
                if (_runner != null && Vector2.Distance(touch, _runner.position) <= GrabRadius)
                {
                    _dragging = true;
                    _dragFrom = Input.mousePosition;
                    _dragTo = _dragFrom;
                }
                // ⭐ 走者の外を触ったら、まず「選ぶ」を試す（発射台 / 待機している個体）。
                //    何も無ければ盤を見回す
                else if (!PickAt(touch))
                {
                    _looking = true;
                    _lookFrom = Input.mousePosition.y;
                    _cameraFrom = _cameraY;
                }
            }
            else if (_looking)
            {
                if (Input.GetMouseButton(0))
                {
                    // ⚠️ 指の動きと同じだけ盤が動く（画面の割合ではなく世界の量で合わせる）
                    float perPixel = _camera.orthographicSize * 2f / UnityEngine.Screen.height;
                    _cameraY = ClampCamera(_cameraFrom + (_lookFrom - Input.mousePosition.y) * perPixel);
                    ApplyCamera();
                }
                else { _looking = false; }
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

        /// <summary>離した。⚠️ 入力は「誰を・どこから・どの角度で」の3つだけ。
        /// ⭐ 飛距離は**その個体の速度**（編成の合計ではない）。</summary>
        private void Fire(Vector2 pull)
        {
            if (_member < 0 || _infil.Result != null) return;

            Vector2 direction = pull.normalized;
            // Core の角度は「上向きが 0、時計回り」。世界は y が上なのでこの式になる
            double angle = Mathf.Atan2(direction.x, direction.y);
            // ⚠️ 判定は Core が全部持つ。ここでやり直さない
            _run = Steal.Hop(_infil, _member, _pad, angle);
            _travelled = 0f;
        }

        // ── 飛ぶ ────────────────────────────────────────

        private void StepFlight()
        {
            var path = _run.Path;
            // ⭐ 終わりに近づくほど遅くなる（失速）。
            // ⚠️ 等速のまま終点で止めると「ビタ止まり」になり、
            //    力尽きたのか壁に当たったのか区別が付かない
            float left = 1f - _travelled / Mathf.Max(1f, path.Count - 1);
            float ease = left >= SlowFrom ? 1f
                : Mathf.Lerp(SlowTo, 1f, left / SlowFrom);
            _travelled += Time.deltaTime * FlightSpeed * ease;
            int index = Mathf.FloorToInt(_travelled);

            if (index >= path.Count - 1)
            {
                // 着地。⭐ 判定は既に Core が出しているので、ここでやり直さない
                var last = path[path.Count - 1];
                _runner.position = ToWorld((float)last.X, (float)last.Y);
                var finished = _run;
                _run = null;
                Land(finished);
                return;
            }

            // ⭐ 走者を中心に画面が追う。⚠️ 盤の端では張り付く（始まりと終わり）
            _cameraY = Mathf.MoveTowards(_cameraY, ClampCamera(_runner.position.y),
                CameraCatchUp * Time.deltaTime);
            ApplyCamera();

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

        /// <summary>1投ぶんが終わった。⭐ **決着していなければ盤は続く。**
        ///
        /// ⚠️ 以前はここで必ず画面を閉じていた（一投しか無かったため）。
        /// リレーでは3体ぶん続くので、決着した時だけ呼び側へ返す。</summary>
        private void Land(StealRun finished)
        {
            // ⭐ 壊した壁を消す（開通が目で分かる）
            RefreshGates();

            if (finished.Outcome == StealOutcome.Landed)
            {
                // ⭐ 着地した個体はその場に残り、次の発射台になる
                _waiting.Remove(_member);
                _pads.Add(_runner);
                _runner.localScale = new Vector3(
                    (float)Steal.RunnerRadius * 2.2f, (float)Steal.RunnerRadius * 2.2f, 1f);
                _runner = null;
            }

            if (_infil.Result != null)
            {
                if (_onDone != null) _onDone(finished);
                return;
            }

            // ⭐ 次の個体へ。⚠️ 発射台は初期位置に戻す（前線は選び直せる）
            Select(_infil.Left.Count > 0 ? _infil.Left[0] : -1);
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

        /// <summary>意匠の絵を貼った板。⭐ 素の四角の代わり。
        /// ⚠️ 見つからなければ白い四角へ落ちる（黙って何も出さない、をしない）。</summary>
        private GameObject Skinned(string name, string skin, Color color,
            Vector2 center, Vector2 size, float depth)
        {
            var go = Solid(name, color, center, size, depth);
            var sprite = Ui.SkinSprite(skin);
            if (sprite != null)
            {
                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Sliced;
                // ⚠️ drawMode を変えたら localScale ではなく size で伸ばす
                go.transform.localScale = Vector3.one;
                renderer.size = size;
            }
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
