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
        /// <summary>名乗りを読ませる時間。⭐ 技名が読める長さが下限。</summary>
        private const float Announce = 0.55f;
        /// <summary>着弾のあとの間。⭐ 数字が飛び切るまで次を始めない。</summary>
        private const float Settle = 0.55f;

        private enum Phase { Idle, Announcing, Settling }

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
        public void Queue(Unit actor, int slot, Unit target)
        {
            if (Busy || actor == null) return;
            _pending = actor;
            _pendingSlot = slot;
            _pendingTarget = target;
            Actor = null;                 // 手番は終わり。札を消す（二度押しの防止でもある）
            Cast(actor, slot);
            _phase = Phase.Announcing;
            _wait = Announce;
            _app.Refresh();
        }

        private void Update()
        {
            if (_state == null) return;
            if (_wait > 0f) { _wait -= Time.deltaTime; return; }

            switch (_phase)
            {
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

            if (_state.Result != null || Actor != null) return;

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
            Queue(next, slot, null);
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
            var tint = ElementMark.ColorOf(Creatures.SpeciesOf(actor.Creature).Element);

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

            for (int i = from; i < state.Log.Count; i++)
            {
                var e = state.Log[i];
                var rect = view.StandOf(e.Unit);
                if (rect == null) continue;
                var head = HeadOf(UnitOf(state, e.Unit));

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
                }
            }
        }
    }
}
