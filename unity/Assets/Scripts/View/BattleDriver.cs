using UnityEngine;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>戦闘を1手ずつ進める。⭐ 一気に流さず**間**を置く。
    ///
    /// ⚠️ 以前は敵の手番を一瞬で全部進めていた。結果しか残らないので、
    /// 何が起きたかを文章で説明する羽目になっていた。
    /// 1手ずつ止めれば、飛ぶ数字が説明の代わりになる。
    /// </summary>
    public sealed class BattleDriver : MonoBehaviour
    {
        /// <summary>1手ごとの間。⭐ 短すぎると読めず、長いと待たされる。</summary>
        private const float Beat = 0.5f;

        private App _app;
        private BattleState _state;
        private float _wait;

        /// <summary>プレイヤーが選ぶ番の者。null なら進行中。</summary>
        public Unit Actor { get; private set; }

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

        /// <summary>プレイヤーが打ったので、次の進行へ戻す。</summary>
        public void HandOff()
        {
            Actor = null;
            _wait = Beat;
        }

        private void Update()
        {
            if (_state == null || _state.Result != null || Actor != null) return;
            if (_wait > 0f) { _wait -= Time.deltaTime; return; }

            var next = Core.Battle.NextActor(_state);
            if (next == null) { _app.Refresh(); return; }

            if (next.Side == Side.Ally)
            {
                Actor = next;
                _app.Refresh();
                return;
            }

            int before = _state.Log.Count;
            Core.Battle.PerformAction(_state, next, Ai.ChooseAction(_state, next));
            ShowSince(_state, before);
            _wait = Beat;
            _app.Refresh();
        }

        /// <summary>直前の手で起きたことを、当たった体の上に数字で出す。
        /// ⚠️ ここが「説明文の代わり」。増やすときは字数でなく**見え方**を足す。</summary>
        public void ShowSince(BattleState state, int from)
        {
            var fx = Fx.Get(_app.transform);
            for (int i = from; i < state.Log.Count; i++)
            {
                var e = state.Log[i];
                var card = GameObject.Find($"Unit {e.Unit}");
                if (card == null) continue;
                var rect = card.GetComponent<RectTransform>();

                switch (e.Kind)
                {
                    case BattleEventKind.Damage:
                        if (e.Absorbed > 0)
                            fx.Number(fx.PointOf(rect, new Vector2(0f, 54f)), "◇", Ui.Ink, 54f);
                        else if (e.Amount > 0)
                            fx.Number(fx.PointOf(rect, new Vector2(0f, 54f)), e.Amount.ToString(), Ui.Danger, 52f);
                        break;
                    case BattleEventKind.Poison:
                        fx.Number(fx.PointOf(rect, new Vector2(0f, 54f)), e.Amount.ToString(),
                            new Color32(0xb9, 0x8c, 0xd8, 0xff), 44f);
                        break;
                    case BattleEventKind.Heal:
                    case BattleEventKind.Regen:
                        if (e.Amount > 0)
                            fx.Number(fx.PointOf(rect, new Vector2(0f, 54f)), "+" + e.Amount, Ui.Good, 46f);
                        break;
                    case BattleEventKind.Buff:
                        fx.Number(fx.PointOf(rect, new Vector2(0f, 74f)),
                            (e.Percent > 0 ? "▲" : "▼") + Stats.LabelOf(e.Stat),
                            e.Percent > 0 ? Ui.Good : Ui.Danger, 34f);
                        break;
                    case BattleEventKind.Stun:
                    case BattleEventKind.Skipped:
                        fx.Number(fx.PointOf(rect, new Vector2(0f, 54f)), "✖", Ui.Accent, 50f);
                        break;
                    case BattleEventKind.GutsSaved:
                        fx.Number(fx.PointOf(rect, new Vector2(0f, 54f)), "1", Ui.Accent, 56f);
                        break;
                    case BattleEventKind.Blocked:
                        fx.Number(fx.PointOf(rect, new Vector2(0f, 54f)), "◇", Ui.InkDim, 44f);
                        break;
                    case BattleEventKind.Down:
                        fx.Number(fx.PointOf(rect, new Vector2(0f, 30f)), "…", Ui.InkFaint, 48f);
                        break;
                }
            }
        }
    }
}
