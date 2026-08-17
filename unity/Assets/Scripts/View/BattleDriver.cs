using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>戦闘を1手ずつ進める。⭐ **押した瞬間に片付けない。**
    ///
    /// 1手を3拍に割る:
    ///   1. 名乗り — 打つ者の頭上に技名。足元に輪。体が前へ出る
    ///   2. 着弾   — ここで初めて <see cref="Core.Battle.PerformAction"/> を呼ぶ。
    ///               数字が飛び、当たった体が跳ね、帯が減る
    ///   3. 間     — 次の手までひと呼吸
    ///
    /// ⚠️ 以前は押した瞬間に計算して即座に組み直していた。
    /// 結果しか残らないので「何が起きたか」を字で説明する羽目になっていた。
    /// ⚠️ 状態を変えるのは 2 の一度だけ。拍ごとに触ると出所が2つになる。
    /// </summary>
    public sealed class BattleDriver : MonoBehaviour
    {
        /// <summary>ゲージが満ちてから名乗るまでの溜め。
        /// ⭐ 相手の番はここが無いと「満ちた瞬間に殴られた」になり、
        /// 帯が満タンになったことを目が確かめる前に次が始まる。
        /// ⚠️ 自分の番には要らない（札が出て考える時間がそのまま溜めになる）。</summary>
        private const float Ready = 0.40f;
        /// <summary>名乗りを読ませる時間。⭐ 技名が読める長さが下限。</summary>
        private const float Announce = 0.72f;
        /// <summary>着弾のあとの間。⭐ 数字が飛び切るまで次を始めない。</summary>
        private const float Settle = 0.72f;
        /// <summary>1秒に進める刻み。⭐ 速い者が先に満ちる様子が目で追える速さ。
        /// ⚠️ 上げすぎると結局パッと切り替わり、下げすぎると待たされる。</summary>
        private const float TicksPerSecond = 14f;

        private enum Phase { Idle, Ready, Announcing, Settling }

        /// <summary>体の中心・頭の上を、枠の中心から見たずれで持つ。
        /// ⚠️ 枠は絵より広い（帯や印のぶん）。枠の中心に出すと足元に寄る。</summary>
        private static readonly Vector2 Body = new Vector2(-60f, 70f);
        /// <summary>頭の上。⚠️ 味方は縦に積んであるので、真上に出すと1つ上の帯に乗る。
        /// 空いている真ん中へ寄せる（味方は右、相手は左）。</summary>
        private static Vector2 HeadOf(Unit unit) =>
            new Vector2(unit.Side == Side.Ally ? 130f : -160f, 170f);

        private App _app;
        private BattleState _state;
        private Phase _phase;
        private float _wait;
        /// <summary>まだ進めていない端数の刻み。⚠️ 切り捨てると遅い者が永久に進まない。</summary>
        private float _ticks;
        /// <summary>決着を渡し終えたか。⚠️ 毎フレーム告知を作らないための札。</summary>
        private bool _handed;

        // 名乗り済みで、まだ打っていない手
        private Unit _pending;
        private int _pendingSlot;
        private Unit _pendingTarget;

        /// <summary>プレイヤーが選ぶ番の者。null なら進行中。</summary>
        public Unit Actor { get; private set; }

        /// <summary>いま演出の最中か。⚠️ 画面はこの間、札を出さない。</summary>
        public bool Busy => _phase != Phase.Idle;

        public static BattleDriver Create(App app)
        {
            var go = new GameObject("Battle Driver");
            go.transform.SetParent(app.transform, false);
            return go.AddComponent<BattleDriver>();
        }

        public void Bind(App app, BattleState state)
        {
            _app = app;
            _state = state;
        }

        /// <summary>プレイヤーが技を選んだ。⚠️ ここでは**まだ計算しない**。
        /// 名乗りを出して、着弾は次の拍に回す。</summary>
        public void Queue(Unit actor, int slot, Unit target, bool ready = false)
        {
            if (Busy || actor == null) return;
            _pending = actor;
            _pendingSlot = slot;
            _pendingTarget = target;
            Actor = null;                 // 手番は終わり。札を消す（二度押しの防止でもある）

            if (ready)
            {
                // ⭐ 帯が満タンになったことを目で確かめさせてから名乗る
                _phase = Phase.Ready;
                _wait = Ready;
            }
            else
            {
                Cast(actor, slot);
                _phase = Phase.Announcing;
                _wait = Announce;
            }
            _app.Refresh();
        }

        private void Update()
        {
            if (_state == null) return;
            if (_wait > 0f) { _wait -= Time.deltaTime; return; }

            switch (_phase)
            {
                case Phase.Ready:
                    // 溜めが終わった。ここで初めて名乗る
                    Cast(_pending, _pendingSlot);
                    _phase = Phase.Announcing;
                    _wait = Announce;
                    return;
                case Phase.Announcing:
                {
                    // ⭐ 状態が変わるのはここだけ
                    int before = _state.Log.Count;
                    Core.Battle.PerformAction(_state, _pending, _pendingSlot, _pendingTarget);
                    _pending = null;
                    _pendingTarget = null;
                    _app.Refresh();       // 帯を減らしてから
                    ShowSince(_state, before);  // 数字と跳ねを載せる
                    _phase = Phase.Settling;
                    _wait = Settle;
                    return;
                }
                case Phase.Settling:
                    _phase = Phase.Idle;
                    _app.Refresh();
                    return;
            }

            if (_state.Result != null)
            {
                // ⭐ 決着。⚠️ ボタンを置かない。何が起きたかを一言だけ挟んで渡す
                if (_handed) return;
                _handed = true;
                BannerView.Show(_app.Overlay,
                    _state.Result == Outcome.Ally ? "WIN" : "LOSE",
                    () => _app.FinishBattle());
                return;
            }
            if (Actor != null) return;

            // ⭐ ここが「ゲージのレース」。誰も満ちていない間は少しずつ進めて、
            //    帯だけ描き直す。⚠️ 画面は組み直さない（組み直すと帯が飛ぶ）。
            _ticks += TicksPerSecond * Time.deltaTime;
            int whole = (int)_ticks;
            if (whole > 0)
            {
                _ticks -= whole;
                if (Core.Battle.AdvanceGauges(_state, whole) > 0)
                {
                    var live = BattleView.Live;
                    if (live != null) live.Retick(_state);
                    return;
                }
            }

            var next = Core.Battle.NextActor(_state);
            if (next == null) { _app.Refresh(); return; }

            if (next.Side == Side.Ally)
            {
                Actor = next;
                _app.Refresh();
                return;
            }

            // 敵も同じ3拍で打つ。⚠️ 敵だけ即座に済ませると、
            //    何をされたのか分からないまま HP だけ減る
            int slot = Ai.ChooseAction(_state, next);
            Queue(next, slot, null, ready: true);
        }

        /// <summary>名乗り。頭上に技名、足元に輪、体をひと突き。</summary>
        private void Cast(Unit actor, int slot)
        {
            var view = BattleView.Live;
            if (view == null) return;
            var stand = view.StandOf(actor.Key);
            if (stand == null) return;

            var skill = Core.Battle.SkillAt(actor, slot);
            var fx = Fx.Get(_app.transform);
            var tint = ElementMark.ColorOf(actor.Creature.Element);

            if (skill != null) fx.Shout(fx.PointOf(stand, HeadOf(actor)), skill.Name, Ui.Ink);
            fx.Ring(fx.PointOf(stand, Body), tint, 120f, 420f, 0.5f);
            // 味方は右へ、敵は左へ踏み込む
            Jolt.Play(stand, new Vector2(actor.Side == Side.Ally ? 46f : -46f, 0f), Announce * 0.7f);
        }

        /// <summary>Key から体を引く。⚠️ 見つからなければ味方扱い（左寄せ）で描く。</summary>
        private static Unit UnitOf(BattleState state, string key)
        {
            foreach (var unit in state.Units)
            {
                if (unit.Key == key) return unit;
            }
            return state.Units[0];
        }

        /// <summary>直前の手で起きたことを、当たった体の上に出す。
        /// ⚠️ ここが「説明文の代わり」。増やすときは字数でなく**見え方**を足す。</summary>
        public void ShowSince(BattleState state, int from)
        {
            var view = BattleView.Live;
            if (view == null) return;
            var fx = Fx.Get(_app.transform);

            // ⚠️ 同じ体に2つ以上出ることがある（殴って毒を盛って CT を伸ばす、など）。
            //    ⭐ 同じ場所に重ねると**下の字が読めない**ので、1つ出すごとに上へ積む。
            //    ⚠️ 「数を減らす」方向では直さない — 起きたことを隠すことになる。
            var stacked = new System.Collections.Generic.Dictionary<string, int>();

            for (int i = from; i < state.Log.Count; i++)
            {
                var e = state.Log[i];
                var rect = view.StandOf(e.Unit);
                if (rect == null) continue;
                var head = HeadOf(UnitOf(state, e.Unit));
                if (e.Kind != BattleEventKind.Act) head = Stack(stacked, e.Unit, head);

                switch (e.Kind)
                {
                    case BattleEventKind.Damage:
                        if (e.Absorbed > 0)
                        {
                            fx.Number(fx.PointOf(rect, head), "◇", Ui.Ink, 54f);
                        }
                        else if (e.Amount > 0)
                        {
                            // ⭐ 光る → 数字 → 体が跳ねる。3つ同時に出すから「当たった」に見える
                            fx.Impact(fx.PointOf(rect, Body), Ui.Danger);
                            fx.Number(fx.PointOf(rect, head), e.Amount.ToString(), Ui.Danger, 56f);
                            Jolt.Play(rect, new Vector2(0f, -22f), 0.22f);
                        }
                        break;
                    case BattleEventKind.Poison:
                        fx.Number(fx.PointOf(rect, head), e.Amount.ToString(),
                            new Color32(0xb9, 0x8c, 0xd8, 0xff), 44f);
                        break;
                    case BattleEventKind.Heal:
                    case BattleEventKind.Regen:
                        if (e.Amount > 0)
                        {
                            fx.Ring(fx.PointOf(rect, Body), Ui.Good, 100f, 300f, 0.4f);
                            fx.Number(fx.PointOf(rect, head), "+" + e.Amount, Ui.Good, 46f);
                        }
                        break;
                    case BattleEventKind.Buff:
                        fx.Number(fx.PointOf(rect, head),
                            (e.Percent > 0 ? "▲" : "▼") + Stats.LabelOf(e.Stat),
                            e.Percent > 0 ? Ui.Good : Ui.Danger, 34f);
                        break;
                    case BattleEventKind.Shield:
                        fx.Ring(fx.PointOf(rect, Body), Ui.Ink, 100f, 280f, 0.4f);
                        break;
                    case BattleEventKind.Stun:
                    case BattleEventKind.Skipped:
                        fx.Number(fx.PointOf(rect, head), "✖", Ui.Accent, 50f);
                        break;
                    case BattleEventKind.GutsSaved:
                        fx.Number(fx.PointOf(rect, head), "1", Ui.Accent, 56f);
                        break;
                    case BattleEventKind.Blocked:
                        fx.Number(fx.PointOf(rect, head), "◇", Ui.InkDim, 44f);
                        break;
                    case BattleEventKind.Down:
                        fx.Number(fx.PointOf(rect, Body), "…", Ui.InkFaint, 48f);
                        break;

                    // ⭐ 以下は**盤に一度も出ていなかった**もの。
                    //    ⚠️ 出ないと「効いたのか外れたのか」が読めず、
                    //    弱化を持つ技が「何も起きない技」に見える。

                    case BattleEventKind.Missed:
                        // ⭐ 弱化が外れた。⚠️ 免疫で弾いた（Blocked ＝ ◇）とは別物なので字を変える。
                        //    ⚠️ 語は Wiki（効果の種類）の「外れる」に合わせる。造語を作らない
                        fx.Number(fx.PointOf(rect, head), "外れ", Ui.InkDim, 40f);
                        break;
                    case BattleEventKind.Applied:
                        // ⭐ 毒・リジェネが乗った。何が何個乗ったかは Core が札にしている
                        fx.Number(fx.PointOf(rect, head), e.Label ?? "", Ui.Ink, 34f);
                        break;
                    case BattleEventKind.Ct:
                        // ⚠️ **増える方が悪い**（待たされる）。符号ではなく色で読ませる
                        fx.Number(fx.PointOf(rect, head),
                            e.Delta > 0 ? $"CT+{e.Delta}" : $"CT{e.Delta}",
                            e.Delta > 0 ? Ui.Danger : Ui.Good, 36f);
                        break;
                    case BattleEventKind.Taunt:
                        fx.Number(fx.PointOf(rect, head),
                            e.Hits > 0 ? $"挑発×{e.Hits}" : "挑発", Ui.Accent, 34f);
                        break;
                    case BattleEventKind.Guts:
                        fx.Number(fx.PointOf(rect, head), "ガッツ", Ui.Accent, 34f);
                        break;
                    case BattleEventKind.Immune:
                        fx.Number(fx.PointOf(rect, head), "免疫", Ui.Good, 34f);
                        break;
                }
            }
        }

        /// <summary>同じ体に重ねて出さないように、1つ出すごとに上へ積む。</summary>
        private static Vector2 Stack(System.Collections.Generic.Dictionary<string, int> seen,
            string key, Vector2 head)
        {
            int n;
            seen.TryGetValue(key, out n);
            seen[key] = n + 1;
            return head + new Vector2(0f, n * StackStep);
        }

        /// <summary>積むときの間隔。⭐ 字の高さより広く取る（縁が触れると読みにくい）。</summary>
        private const float StackStep = 46f;
    }
}
