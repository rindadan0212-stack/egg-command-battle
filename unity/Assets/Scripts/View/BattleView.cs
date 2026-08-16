using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>スキルの札1枚。⭐ 配置は Prefab が持つ。コードは値を流し込むだけ。</summary>
    [Serializable]
    public sealed class SkillSlot
    {
        public Button Button;
        public Image Plate;
        public Text Name;
        public Text Ct;
        public Image CtPill;
    }

    /// <summary>戦闘の画面まるごと。
    ///
    /// ⭐ **並び・大きさ・色はすべて Assets/Resources/Prefabs/BattleScreen.prefab が持つ。**
    /// ここに座標は1つも書かない。直したいときは Unity Editor で Prefab を開く。
    /// ⚠️ 新しい見た目を足したくなったら、まず「座標を書こうとしていないか」を疑う。
    /// </summary>
    public sealed class BattleView : MonoBehaviour
    {
        [SerializeField] private UnitStand[] _allies;
        [SerializeField] private UnitStand _foe;
        [SerializeField] private SkillSlot[] _skills;
        [SerializeField] private Button _finish;
        [SerializeField] private Button _pick;

        public void Bind(BattleState state, Unit actor, Unit target,
            Action<int> onSkill, Action onFinish, Action onPick)
        {
            // ── 味方 ────────────────────────────────────
            int i = 0;
            foreach (var unit in state.Units)
            {
                if (unit.Side != Side.Ally) continue;
                if (i < _allies.Length && _allies[i] != null)
                {
                    _allies[i].gameObject.SetActive(true);
                    _allies[i].Bind(unit, actor != null && ReferenceEquals(actor, unit), false);
                }
                i++;
            }
            // ⚠️ 余った枠は隠す。空の器が残ると「誰か居る」に見える
            for (int k = i; k < _allies.Length; k++)
            {
                if (_allies[k] != null) _allies[k].gameObject.SetActive(false);
            }

            // ── 相手 ────────────────────────────────────
            Unit foe = null;
            foreach (var unit in state.Units)
            {
                if (unit.Side == Side.Enemy) { foe = unit; break; }
            }
            if (_foe != null)
            {
                _foe.gameObject.SetActive(foe != null);
                if (foe != null) _foe.Bind(foe, actor != null && ReferenceEquals(actor, foe), true);
            }

            bool over = state.Result != null;
            if (_finish != null)
            {
                _finish.gameObject.SetActive(over);
                _finish.onClick.RemoveAllListeners();
                if (over && onFinish != null) _finish.onClick.AddListener(() => onFinish());
            }

            // 狙い先を選ばせるのは、相手が2体以上いるときだけ
            if (_pick != null)
            {
                bool many = Core.Battle.LivingOf(state, Side.Enemy).Count > 1;
                _pick.gameObject.SetActive(many && !over);
                _pick.onClick.RemoveAllListeners();
                if (many && onPick != null) _pick.onClick.AddListener(() => onPick());
            }

            // ── 手札 ────────────────────────────────────
            var hand = actor;
            if (hand == null)
            {
                foreach (var unit in state.Units)
                {
                    if (unit.Side == Side.Ally && Core.Battle.IsAlive(unit)) { hand = unit; break; }
                }
            }
            bool myTurn = hand != null && ReferenceEquals(hand, actor);

            for (int slot = 0; slot < _skills.Length; slot++)
            {
                var view = _skills[slot];
                if (view == null || view.Button == null) continue;

                var skill = hand == null ? null : Core.Battle.SkillAt(hand, slot);
                view.Button.gameObject.SetActive(skill != null && !over);
                if (skill == null || over) continue;

                bool usable = myTurn && Core.Battle.IsUsable(hand, slot);
                int cooldown = hand.Cooldowns[slot];

                if (view.Name != null)
                {
                    view.Name.text = skill.Name;
                    view.Name.color = usable ? Ui.OnLead : Ui.InkFaint;
                }
                if (view.Ct != null)
                {
                    view.Ct.text = slot == 0 ? "CT 0"
                        : cooldown > 0 ? $"あと {cooldown}" : $"CT {skill.Ct}";
                }
                if (view.Plate != null)
                {
                    // ⭐ 主導線は枠1（CT が無いので必ず打てる）。⚠️ 色を掛けず絵を差し替える
                    view.Plate.sprite = Ui.SkinSprite(!usable ? "button-off" : slot == 0 ? "button-lead" : "button");
                }
                view.Button.interactable = usable;

                int captured = slot;
                view.Button.onClick.RemoveAllListeners();
                if (onSkill != null) view.Button.onClick.AddListener(() => onSkill(captured));
            }
        }
    }
}
