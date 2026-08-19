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
        /// ⚠️ **0.22 → 0.35**（2026-08-19）。効いてはいたが短く、
        /// 「ゆっくり止まる」と読めなかった（作者の指摘）。
        private const float SlowFrom = 0.35f;
        /// <summary>失速しきったときの速さの割合。⚠️ 0 にすると永久に着かない。</summary>
        /// ⚠️ **0.18 → 0.08**（2026-08-19）。最後をもっと粘らせる。
        private const float SlowTo = 0.08f;

        /// <summary>目盛りの間隔（盤の単位＝メートル）。</summary>
        private const float MeterStep = 10f;

        /// <summary>数字を書く間隔。⚠️ 10 ごとに数字を書くと、字が縦に連なって帯になる。
        /// ⭐ 刻みは細かく、数字は粗く ── 定規と同じ。</summary>
        private const float MeterLabelStep = 50f;

        /// <summary>画面に映る世界の幅。⭐ **道より広く取る**。
        /// ⚠️ 道と同じにすると目盛りを置く場所が道の上しか無くなり、線が盤を横切る。</summary>
        private const float ViewWidth = 160f;

        /// <summary>縦だけを引き伸ばす倍率。⭐ **1画面に 100m** が収まる（作者の指示 2026-08-19）。
        ///
        /// ⚠️ 画面は 1080×1920 なので、見える縦は「見える横 ÷ 0.5625」＝ 284 で固定。
        /// 道を細くすれば 100 にできるが、それだと**道そのものが細くなる**。
        /// ⭐ 道の幅はそのままに、**縦の位置だけ** 2.844 倍して伸ばす。
        /// 284 ÷ 2.844 ＝ 100 ── 1画面に 100m。
        ///
        /// ⚠️ **絵は伸ばさない。**位置だけを伸ばし、ドット絵の大きさは元のまま
        /// （非等方に伸ばすとドット絵が潰れる。画面の作法）。
        /// ⚠️ 縦に厚みを持つもの（地・親の帯・関門）だけ、厚みにも掛ける。</summary>
        private const float Stretch = 2.844f;

        /// <summary>カメラが追いつく速さ。⚠️ 速すぎると画面が跳ねて酔う。</summary>
        /// <summary>見回しているときのカメラの追従速度。
        /// ⚠️ **飛んでいる最中には使わない。**走者は <see cref="FlightSpeed"/>（260）で進むので、
        /// 90 では**3倍近く置いていかれて画面から消えていた**（作者の指摘 2026-08-19）。
        /// ⭐ 飛行中は下の <c>StepFlight</c> で走者に直に貼り付ける。</summary>
        private const float CameraCatchUp = 90f;

        private StealField _field;
        private Nest _nest;
        /// <summary>その巣から盗んだ回数。⚠️ 雑魚の顔ぶれを引くのに要る。</summary>
        private int _raids;
        private Action<StealRun> _onDone;

        /// <summary>潜入そのもの。⭐ **判定も発射台も Core が持つ。**画面は選ばせて描くだけ。</summary>
        private Steal.Infiltration _infil;
        /// <summary>いま投げようとしている個体（<see cref="Steal.Infiltration.Party"/> の添字）。</summary>
        private int _member = -1;
        /// <summary>どこから投げるか。⚠️ **-1 は初期位置**。それ以外は Pads の添字。</summary>
        private int _pad = -1;

        /// <summary>選んだ個体が変わった／1投終わった。⭐ 盤の外の帯を描き直させる。
        /// ⚠️ 盤は帯を知らない（知らせると、盤が uGUI に依存してしまう）。</summary>
        private Action _onChanged;
        /// <summary>着地した個体の絵（＝発射台）。</summary>
        private readonly List<Transform> _pads = new List<Transform>();
        /// <summary>関門の絵。⭐ 壁を壊したら消すので持っておく。</summary>
        private readonly List<GameObject> _gates = new List<GameObject>();
        /// <summary>雑魚の絵。⭐ 倒したら消すので持っておく。</summary>
        private readonly List<GameObject> _mobs = new List<GameObject>();
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

        /// <summary>いま投げようとしている個体。⚠️ まだ誰も選べないときは -1。</summary>
        public int Chosen { get { return _member; } }

        /// <summary>⭐ **盤の外の帯から選ぶ。**
        /// ⚠️ 飛んでいる最中と、決着したあとは受け付けない。</summary>
        public void Choose(int member)
        {
            if (_run != null || _infil == null || _infil.Result != null) return;
            if (member == _member) return;
            if (!_infil.Left.Contains(member)) return;
            Select(member);
            if (_onChanged != null) _onChanged();
        }

        /// <summary>盤の外の帯が変わったら教えてもらう。</summary>
        public void Watch(Action onChanged) { _onChanged = onChanged; }

        /// <param name="infil">⭐ **進み具合ごと**渡す。誰をいつ投げるかは盤で選ぶ。
        /// ⚠️ 盤はこれを持たない（<see cref="App.Infiltration"/> が持つ）。
        /// 雑魚と戦うと盤は一度畳まれるので、持たせると進み具合が消える。
        /// ⭐ 途中から渡されたら、着地した個体・壊した壁・倒した雑魚をそのまま描き直す。</param>
        /// <param name="nest">どの巣か。⭐ 親の絵と、雑魚の顔ぶれを引くのに要る。</param>
        /// <param name="raids">その巣から盗んだ回数。⚠️ 雑魚の種を決める一部。</param>
        public static StealStage Create(Steal.Infiltration infil, Nest nest, int raids,
            Action<StealRun> onDone)
        {
            var go = new GameObject("Steal Stage");
            var stage = go.AddComponent<StealStage>();
            stage.Build(infil, nest, raids, onDone);
            return stage;
        }

        // ── 盤を組む ────────────────────────────────────

        private void Build(Steal.Infiltration infil, Nest nest, int raids, Action<StealRun> onDone)
        {
            _field = infil.Field;
            var field = _field;
            _onDone = onDone;
            _infil = infil;
            _nest = nest;
            _raids = raids;

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
            // ⚠️ 縦に厚みを持つものは、厚みにも Stretch を掛ける
            Skinned("Board", "tile", new Color32(0x6f, 0x8a, 0x5e, 0xff),
                new Vector2(0f, 0f),
                new Vector2((float)Steal.FieldWidth, (float)field.Height * Stretch), 5f);

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
            var species = SpeciesTable.ById(nest.SpeciesId);
            foreach (var span in Steal.ParentSpans(field))
            {
                float centerX = (float)(span.From + span.To) / 2f;
                float width = (float)(span.To - span.From);

                // ⭐ **絵だけで塞ぐ。**判定の箱は描かない（2026-08-18・作者判断）。
                //
                // ⚠️ 以前は半透明の箱を敷いて、その中に小さい絵を立たせていた。
                //    塞ぐ幅は 56 あるのに絵は 30 しか無く（帯の厚み 30 に合わせていた）、
                //    **箱が当たり判定で絵は飾り**にしか見えなかった。
                // ⭐ 絵を塞ぐ幅そのものまで大きくすれば、道が塞がっていることは
                //    絵だけで読める。⚠️ 縦横比は保つ（ドット絵を非等方に伸ばさない）。
                //
                // ⚠️ 縦は帯（厚み 30）より絵のほうが高くなる。判定は帯のままなので、
                //    **絵の上下は当たらない**。⭐ それでよい ── 隙間は左右に空いていて、
                //    そこを抜ける軌跡は絵の横を通るので、嘘にはならない。
                // ⚠️ **絵は伸ばさない**（ドット絵を非等方に伸ばさない・画面の作法）。
                //    ⭐ 位置だけ伸びるので、塞ぐ縦の範囲は帯（判定）が持つ。
                PixelObject("Parent", species.Sprite, species.Palettes[0],
                    ToWorld(centerX, bandMid), width, 3f);
            }

            // ⭐ 関門。⚠️ 以前は1枚も描いていなかった（盤に在るのに見えなかった）
            BuildGates();
            RefreshGates();

            // ⭐ 道中の雑魚。⚠️ 関門と違って要求は出さない（ステでは越えられない）
            BuildMobs();

            // 卵
            PixelObject("Egg", EggArt.Sprite, EggArt.Shell,
                ToWorld((float)field.Egg.X, (float)field.Egg.Y),
                (float)Steal.EggRadius * 2.4f, 2f);

            // ⭐ もう着地している個体は発射台として置き直す（雑魚と戦って戻ってきたとき）
            BuildPads();
            // ⭐ **選ぶのは盤の外。**盤に立つのは「いま投げる1体」だけ（StealScreen の帯）。
            // ⚠️ 3体を出発点に並べていた頃は、選んだ1体が Start へ移されて
            //    ちょうど別の1体と重なり、どれを触ったのか分からなかった。
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
            // ⭐ **縦だけ Stretch 倍。**横はそのまま（道の幅は変えない）。
            return new Vector2(fx - (float)Steal.FieldWidth / 2f,
                ((float)_field.Height / 2f - fy) * Stretch);
        }

        /// <summary>伸ばしたあとの盤の高さ。⚠️ カメラの端も目盛りもこれで測る。</summary>
        private float TallWorld { get { return (float)_field.Height * Stretch; } }

        /// <summary>絵に属するずらし（影・印）。⚠️ **これは伸ばさない。**
        ///
        /// ⭐ 「体からどれだけ離すか」は絵の大きさの話であって、盤の距離ではない。
        /// ⚠️ 伸ばしていた頃は、雑魚の影だけが本体から 2.8 倍離れて
        /// 画面の上に取り残された（実測 2026-08-19）。</summary>
        private Vector2 Beside(float fx, float fy, float dx, float dy)
        {
            var at = ToWorld(fx, fy);
            return new Vector2(at.x + dx, at.y - dy);
        }

        /// <summary>目盛り。⭐ **道の外（右の余白）**に短い線と数字を置く。
        /// ⚠️ 盤を横切る線を引かない。道の上に線があると通り道の一部に見える。
        ///
        /// ⭐ **刻みは 10、数字は 50 ごと**（定規と同じ）。
        /// ⚠️ 50 刻みだけだったときは、飛んだ先が「50 と 100 のあいだ」までしか読めなかった。
        /// 飛距離は速度 × 3 の整数なので、10 の目があれば数えて足せる。</summary>
        private void BuildMeters()
        {
            float roadEdge = (float)Steal.FieldWidth / 2f;
            float tickFrom = roadEdge + 4f;
            // ⭐ 数字を書く目盛りだけ長くする。⚠️ 全部同じ長さだと数字がどれに付くか読めない
            float longTo = roadEdge + 14f;
            float shortTo = roadEdge + 9f;
            // ⚠️ 左寄せにしたら「250」が画面の右端で切れた。
            //    画面の縁から右寄せで置く（何桁でも必ず収まる）
            float textRight = ViewWidth / 2f - 3f;

            for (float d = MeterStep; d <= (float)_field.Height; d += MeterStep)
            {
                float y = (float)_field.Start.Y - d;
                if (y < 0f) break;
                float worldY = ToWorld(0f, y).y;
                // ⚠️ 浮動小数で割った余りを 0 と比べない。丸めてから整数で判定する
                bool numbered = Mathf.RoundToInt(d) % Mathf.RoundToInt(MeterLabelStep) == 0;
                float tickTo = numbered ? longTo : shortTo;

                Skinned($"Tick {d}", "pill",
                    new Color(1f, 1f, 1f, numbered ? 0.65f : 0.35f),
                    new Vector2((tickFrom + tickTo) / 2f, worldY),
                    new Vector2(tickTo - tickFrom, numbered ? 1.6f : 1.1f), 4.8f);

                if (!numbered) continue;

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

                // ⭐ **白い札に濃紺の字。**画面のほかの札と同じ作りにする。
                // ⚠️ 濃い色べた塗り＋白い字にしていた頃は、この四角だけ角がシャープで
                //    盤のトーンから浮いていた（レビュー指摘 2026-08-19）。
                var body = Skinned($"Gate {i}", "pill", Color.white,
                    ToWorld((float)(gate.From + gate.To) / 2f, mid),
                    new Vector2(width, height * Stretch), 4.2f);
                // ⭐ 種類は**下の細い帯**で示す（塗り分けは残すが、字の読みやすさを取らない）
                // ⚠️ 帯の位置は「札の下端から少し内側」＝絵の話。伸ばさない
                Skinned($"Gate {i} 種類", "pill", GateColor(gate.Kind),
                    Beside((float)(gate.From + gate.To) / 2f, mid,
                        0f, height * Stretch / 2f - 5f),
                    new Vector2(width, 8f), 4.15f);

                var label = new GameObject($"Gate {i} 要求");
                label.transform.SetParent(body.transform, false);
                label.transform.position = new Vector3(
                    body.transform.position.x, body.transform.position.y, 4.1f);
                var text = label.AddComponent<TextMesh>();
                // ⚠️ **HP の関門だけ単位が違う。**判定は素の StatBlock.Hp で行うが、
            //    画面に出ている HP は ×Battle.HpScale（2026-08-19 の桁上げ）。
            //    そのまま出すと「関門 HP 213」対「この個体 HP 22,365」になり、比べようがなかった。
            // ⭐ 攻撃力・防御力・スピードの関門は素のままで、既にステ表と揃っている。
            var need = Steal.StatOf(gate.Kind);
            int shown = need == StatKey.Hp ? gate.Requires * Battle.HpScale : gate.Requires;
            text.text = $"{Stats.LabelOf(need)} {Ui.Digits(shown)}";
                text.font = Ui.TheFont;
                text.fontSize = 64;
                text.characterSize = 0.44f;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = Ui.Ink;
                label.GetComponent<MeshRenderer>().sharedMaterial = Ui.TheFont.material;

                _gates.Add(body);
            }
        }

        /// <summary>関門の色。⚠️ 種類が読めればよい（下の細い帯だけに使う）。
        /// ⭐ 札そのものは白 ── 要求の数を読ませるのが主で、種類は補助。</summary>
        private static Color GateColor(GimmickKind kind)
        {
            switch (kind)
            {
                case GimmickKind.Wall: return new Color32(0x8a, 0x6f, 0x4e, 0xff);
                case GimmickKind.Damage: return new Color32(0xb0, 0x53, 0x4a, 0xff);
                default: return new Color32(0x4a, 0x63, 0xa8, 0xff);
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

        /// <summary>道中の雑魚を描く。⭐ **絵は実際に出てくる相手**（先頭の1体）。
        ///
        /// ⚠️ 適当な印を置かない。当たると戦闘が始まるので、
        /// 「何と戦うことになるか」が見えないまま踏ませることになる。
        /// ⭐ 編成は巣と番号で決まっている（<see cref="Steal.MobPartyOf"/>）ので、
        /// ここで引いた絵と戦闘に出る相手は必ず一致する。</summary>
        private void BuildMobs()
        {
            for (int i = 0; i < _field.Mobs.Count; i++)
            {
                var mob = _field.Mobs[i];
                var species = SpeciesTable.ById(MobFace(i));
                var go = PixelObject($"Mob {i}", species.Sprite, species.Palettes[0],
                    ToWorld((float)mob.At.X, (float)mob.At.Y),
                    (float)mob.Radius * 2.2f, 2.6f);

                // ⭐ 足元の影。⚠️ pill を薄く敷いていたが、この寸法では**四角い箱**に見えた。
                //    円の絵を潰して楕円にする（縁が丸いので影として読める）
                Ellipse($"Mob {i} 影", new Color32(0x2a, 0x1e, 0x18, 0x55),
                    Beside((float)mob.At.X, (float)mob.At.Y, 0f, (float)mob.Radius * 0.9f),
                    new Vector2((float)mob.Radius * 2.0f, (float)mob.Radius * 0.6f), 2.8f);

                // ⭐ **親と見分けるための印。**⚠️ 巣の種族と同じ絵が出ることがあるので、
                //    絵だけでは「当たったら終わり（親）」か「当たると3対3（雑魚）」か読めない。
                //    ⚠️ 離して置くと何の数字か分からない。**体の右肩に貼る**
                float badgeX = (float)mob.Radius * 0.95f;
                float badgeY = -(float)mob.Radius * 0.85f;
                // ⚠️ 濃紺のままだと暗い輪郭線の上で沈んで読めない（レビュー指摘 2026-08-19）。
                //    ⭐ 白い縁を1枚下に敷いて、体から浮かせる。
                Ellipse($"Mob {i} 印縁", Color.white,
                    Beside((float)mob.At.X, (float)mob.At.Y, badgeX, badgeY),
                    new Vector2((float)mob.Radius * 1.28f, (float)mob.Radius * 1.28f), 2.5f)
                    .transform.SetParent(go.transform, true);
                var disc = Ellipse($"Mob {i} 印地", new Color32(0x2b, 0x33, 0x50, 0xff),
                    Beside((float)mob.At.X, (float)mob.At.Y, badgeX, badgeY),
                    new Vector2((float)mob.Radius * 1.0f, (float)mob.Radius * 1.0f), 2.45f);
                var badge = new GameObject($"Mob {i} 印");
                badge.transform.SetParent(transform, false);
                var at = Beside((float)mob.At.X, (float)mob.At.Y, badgeX, badgeY);
                badge.transform.position = new Vector3(at.x, at.y, 2.4f);
                var mark = badge.AddComponent<TextMesh>();
                mark.text = "3";
                mark.font = Ui.TheFont;
                mark.fontSize = 64;
                mark.characterSize = 0.28f;
                mark.anchor = TextAnchor.MiddleCenter;
                mark.color = Color.white;
                badge.GetComponent<MeshRenderer>().sharedMaterial = Ui.TheFont.material;
                badge.transform.SetParent(go.transform, true);
                disc.transform.SetParent(go.transform, true);

                _mobs.Add(go);
            }
            RefreshMobs();
        }

        /// <summary>その雑魚の顔。⚠️ 決まっている編成の先頭を引く。</summary>
        private string MobFace(int mob)
        {
            var party = Steal.MobPartyOf(_nest, _raids, mob);
            return party.Count > 0 ? party[0].SpeciesId : _nest.SpeciesId;
        }

        /// <summary>倒した雑魚を盤から消す。⭐ もう居ないことが目で分かる。</summary>
        private void RefreshMobs()
        {
            for (int i = 0; i < _mobs.Count; i++)
            {
                if (_mobs[i] != null && _infil.Cleared.Contains(i)) _mobs[i].SetActive(false);
            }
        }

        /// <summary>もう着地している個体を発射台として置き直す。
        /// ⚠️ 雑魚と戦って戻ってきたとき、ここを通らないと前線が消える。</summary>
        private void BuildPads()
        {
            for (int i = 0; i < _infil.Pads.Count; i++)
            {
                int owner = i < _infil.PadOwner.Count ? _infil.PadOwner[i] : -1;
                if (owner < 0 || owner >= _infil.Party.Count) continue;
                var creature = _infil.Party[owner];
                var at = _infil.Pads[i];
                var go = PixelObject($"発射台 {i}",
                    Creatures.SpeciesOf(creature).Sprite, Creatures.PaletteOf(creature),
                    ToWorld((float)at.X, (float)at.Y),
                    (float)Steal.RunnerRadius * 2.2f, 1.2f);
                Fade(go.transform, PadAlpha);
                _pads.Add(go.transform);
            }
        }

        /// <summary>発射台の濃さ。⭐ **投げられる個体と見分けるため**に薄くする。
        ///
        /// ⚠️ 同じ濃さで描くと、雑魚を倒して回数が戻ったあと
        /// 「同じ個体が出発点と盤の上の2か所に居る」ようにしか見えない。
        /// ⭐ 薄いほうは足場（次はここから投げられる）、濃いほうが投げる本体。</summary>
        private const float PadAlpha = 0.5f;

        /// <summary>絵を薄くする。⚠️ 見つからなければ何もしない。</summary>
        private static void Fade(Transform mark, float alpha)
        {
            if (mark == null) return;
            var renderer = mark.GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            var color = renderer.color;
            renderer.color = new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>いま投げる1体の絵を作り直す。
        /// ⚠️ 前の絵は必ず消す。⭐ 着地した絵は <see cref="Land"/> で
        /// <see cref="_pads"/> へ渡してから <see cref="_runner"/> を null にしてあるので、
        /// ここで消えるのは「まだ投げていない絵」だけ。</summary>
        private void MakeRunner()
        {
            if (_runner != null) Destroy(_runner.gameObject);
            _runner = null;
            if (_member < 0) return;

            var creature = _infil.Party[_member];
            var go = PixelObject($"投げる {_member}",
                Creatures.SpeciesOf(creature).Sprite, Creatures.PaletteOf(creature),
                ToWorld((float)_field.Start.X, (float)_field.Start.Y),
                (float)Steal.RunnerRadius * 2.2f, 1.2f);
            _runner = go.transform;
        }

        /// <summary>投げる個体を選ぶ。⚠️ 発射台は初期位置へ戻す（前線は選び直せる）。</summary>
        private void Select(int member)
        {
            _member = member;
            _pad = -1;
            MakeRunner();
            PlaceRunner();
            DrawReach();
        }

        /// <summary>選んだ個体を、選んだ発射台の上に立たせる。</summary>
        private void PlaceRunner()
        {
            if (_member < 0 || _runner == null) return;
            var at = _pad < 0 ? _field.Start : _infil.Pads[_pad];
            _runner.position = new Vector3(
                ToWorld((float)at.X, (float)at.Y).x, ToWorld((float)at.X, (float)at.Y).y, 1.2f);
        }

        /// <summary>⚠️ **飛距離の目安は描かない**（2026-08-18・作者判断）。
        ///
        /// ⭐ 盤の右に 10m 刻みの目盛りが立つので、どこまで届くかはそちらで読む。
        /// ⚠️ 線を引くと「そこまでは必ず届く」と読めてしまうが、実際は跳ね返りと
        /// 関門で変わるので、線のほうが嘘に近い。
        ///
        /// ⚠️ 残してある理由: 選び直しのたびに呼ばれるので、消し込みだけは要る。</summary>
        private void DrawReach()
        {
            if (_reach != null) Destroy(_reach);
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
            // ⚠️ **誰を投げるかは盤では選ばない。**盤の外の帯（StealScreen）が持つ。
            return false;
        }

        /// <summary>盤の下を隠している帯の高さ（画面の設計単位＝<see cref="Ui.H"/> と同じ物差し）。
        /// ⭐ 3体を選ぶ帯は uGUI なので、盤はその高さだけ上へ逃がす。</summary>
        private float _dock;

        /// <summary>帯に隠される高さを伝える。⚠️ 伝えないと、出発点が帯の下に潜る。</summary>
        public void HideBehind(float designPixels)
        {
            _dock = designPixels;
            _cameraY = ClampCamera(_cameraY);
            ApplyCamera();
        }

        /// <summary>帯の高さを盤の単位へ。
        /// ⭐ 画面の横幅に <see cref="ViewWidth"/> を映しているので、
        /// 設計単位1つは必ず <c>ViewWidth / Ui.W</c> ぶん。
        /// ⚠️ Screen.height を使わない ── 覆いの倍率は**横幅**で合わせてある
        /// （CanvasScaler.matchWidthOrHeight = 0）ので、高さから出すと機種で狂う。</summary>
        private float DockWorld { get { return _dock * ViewWidth / Ui.W; } }

        /// <summary>盤の外を見ないように挟む。
        /// ⚠️ 始まりと終わりだけは端に張り付く（ここが「その限りでない」ところ）。</summary>
        private float ClampCamera(float y)
        {
            // ⚠️ 見えているのは帯の**上**だけ。全画面ぶんで挟むと、端で盤が帯に潜る
            float half = _camera.orthographicSize - DockWorld / 2f;
            float top = TallWorld / 2f;
            if (top <= half) return 0f;
            return Mathf.Clamp(y, -top + half, top - half);
        }

        private void ApplyCamera()
        {
            var at = _camera.transform.position;
            // ⭐ 帯の半分だけカメラを下げる → 盤は「帯より上」の真ん中に来る
            _camera.transform.position = new Vector3(0f, _cameraY - DockWorld / 2f, at.z);
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
                // ⭐ **最初に触った所へ戻せば取り消し**（作者の指示 2026-08-19）。
                //    ⚠️ 前の下限（MinPull ＝ 短辺の4%）は狭すぎて、戻したつもりが飛んでいた。
                if (pull.magnitude >= CancelPixels()) Fire(pull);
            }
        }

        /// <summary>飛ぶ先を点線で見せる。⭐ **本番と同じ式で下見する**（<see cref="Steal.Preview"/>）。
        ///
        /// ⚠️ 前は引っ張った向きへ**まっすぐな線**を描いていた。実際には壁で跳ね返るので、
        /// 予告と実際が食い違い「狙った角度に飛ばない」ように見えていた（作者の指摘 2026-08-19）。
        /// ⭐ 同じ関数を通せば、予告と実際は**必ず一致する**。</summary>
        private void DrawGuide()
        {
            Vector2 pull = _dragFrom - _dragTo;
            // ⭐ 引き戻したら取り消し。⚠️ 線を消して「離しても飛ばない」と分かるようにする
            if (pull.magnitude < CancelPixels()) { _guide.positionCount = 0; return; }

            Vector2 direction = pull.normalized;
            double angle = Mathf.Atan2(direction.x, direction.y);
            var look = Steal.Preview(_infil, _member, _pad, angle);
            var path = look.Path;
            if (path.Count < 2) { _guide.positionCount = 0; return; }

            // ⚠️ 点を全部置くと数百になる。⭐ 間引いても跳ね返りの形は残る
            int step = Mathf.Max(1, path.Count / GuidePoints);
            var points = new List<Vector3>();
            for (int i = 0; i < path.Count; i += step)
            {
                points.Add(ToWorld((float)path[i].X, (float)path[i].Y));
            }
            points.Add(ToWorld((float)path[path.Count - 1].X, (float)path[path.Count - 1].Y));

            _guide.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) _guide.SetPosition(i, points[i]);
        }

        /// <summary>予告線に置く点の数。⚠️ 多すぎると線が重くなる。</summary>
        private const int GuidePoints = 40;

        /// <summary>ここまで引き戻したら取り消し（画面の短辺に対する割合）。
        /// ⭐ **最初に触った所へ指を戻せば、離しても飛ばない**（作者の指示 2026-08-19）。
        /// ⚠️ <see cref="MinPull"/> と同じ値だと「取り消せる範囲」が狭すぎて、
        /// 戻したつもりが飛んでいた。⭐ 広くとって、予告線が消えることで見せる。</summary>
        private const float CancelPull = 0.10f;

        private float CancelPixels() =>
            Mathf.Min(UnityEngine.Screen.width, UnityEngine.Screen.height) * CancelPull;

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

            // ⭐ **走者に直に貼り付ける。**⚠️ 盤の端では張り付く（始まりと終わり）
            // ⚠️ MoveTowards で追わせていた頃は、走者(260)に対しカメラ(90)が遅く、
            //    飛んだ瞬間に画面の外へ消えていた。⭐ 位置は毎フレーム決まるので、
            //    そのまま合わせても揺れない（走者自身が滑らかに動いている）。
            _cameraY = ClampCamera(_runner.position.y);
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

            // ⭐ 雑魚に当たった場所も着地点。そこも発射台になる
            if (finished.Outcome == StealOutcome.Landed || finished.Outcome == StealOutcome.Fought)
            {
                // ⭐ 着地した個体はその場に残り、次の発射台になる
                _pads.Add(_runner);
                _runner.localScale = new Vector3(
                    (float)Steal.RunnerRadius * 2.2f, (float)Steal.RunnerRadius * 2.2f, 1f);
                Fade(_runner, PadAlpha);
                _runner = null;
            }

            // ⭐ 雑魚に当たった。⚠️ **決着ではない** ── 続きは戦闘のあと。
            //    ⚠️ ここで Select へ進めない。戦闘の結果を待たずに次を投げられてしまう
            if (finished.Outcome == StealOutcome.Fought)
            {
                if (_onDone != null) _onDone(finished);
                return;
            }

            if (_infil.Result != null)
            {
                if (_onDone != null) _onDone(finished);
                return;
            }

            // ⭐ 次の個体へ。⚠️ 発射台は初期位置に戻す（前線は選び直せる）
            Select(_infil.Left.Count > 0 ? _infil.Left[0] : -1);
            // ⭐ 盤の外の帯も描き直す（投げ終わった1体を「投げた」にする）
            if (_onChanged != null) _onChanged();
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
                // ⚠️ **FullRect にする。**既定は Tight で、9スライス（SpriteDrawMode.Sliced）に
                //    渡すと Unity が絵1枚ごとに警告を出す。盤は絵を何十個も置くので大量に溜まる。
                _white = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f),
                    1f, 0, SpriteMeshType.FullRect);
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

        /// <summary>楕円。⭐ 影に使う。⚠️ 円の絵を潰すだけなので縁が丸いまま。
        /// ⚠️ ドット絵ではないので潰してよい（作法が禁じているのは**キャラクターの絵**）。
        ///
        /// ⚠️ <see cref="Solid"/> は「1単位＝1マス」の白い絵を前提に localScale へ寸法を渡す。
        /// 意匠の絵は pixelsPerUnit が違うので、**そのまま渡すと何倍にもなる**
        /// （実測で頼んだ 22 が 57 になった）。絵の実寸で割ってから渡す。</summary>
        private GameObject Ellipse(string name, Color color, Vector2 center, Vector2 size, float depth)
        {
            var go = Solid(name, color, center, size, depth);
            var sprite = Ui.SkinSprite("circle");
            if (sprite == null) return go;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            var native = sprite.bounds.size;
            go.transform.localScale = new Vector3(
                size.x / Mathf.Max(0.0001f, native.x),
                size.y / Mathf.Max(0.0001f, native.y), 1f);
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
