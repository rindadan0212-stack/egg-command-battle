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
        /// <summary>相手が**1体だけ**のときの器。⭐ 大きく1体だけ立つ（親・ボス）。</summary>
        [SerializeField] private UnitStand _foe;
        /// <summary>相手が**複数**のときの器。⭐ 味方と同じ形で並べる（雑魚の3対3）。
        ///
        /// ⚠️ 以前は <see cref="_foe"/> しか無かったので、**3体居ても1体しか見えなかった**。
        /// 残り2体は盤の上に存在するのに、狙うことも生死を読むこともできなかった。</summary>
        [SerializeField] private UnitStand[] _foes;
        [SerializeField] private SkillSlot[] _skills;
        [SerializeField] private Button _finish;
        [SerializeField] private Button _pick;

        /// <summary>いま出ている戦闘画面。⚠️ 演出が「どの体か」を引くための唯一の窓口。
        /// GameObject.Find で名前を探すのはやめた。Prefab で名前を変えた瞬間に
        /// 黙って何も出なくなる（実際それでダメージの数字が消えていた）。</summary>
        public static BattleView Live { get; private set; }

        private readonly System.Collections.Generic.Dictionary<string, UnitStand> _byKey =
            new System.Collections.Generic.Dictionary<string, UnitStand>();

        private void OnEnable() => Live = this;
        private void OnDisable() { if (Live == this) Live = null; }

        /// <summary>ゲージだけ描き直す。⭐ 競り合いを見せるために毎フレーム呼ぶ。
        /// ⚠️ ここで画面を組み直さない。組み直すと押しどころが毎フレーム作り直され、
        /// 触れないうえに帯が飛んで見える（それが「パッパッ」の正体だった）。</summary>
        public void Retick(BattleState state)
        {
            foreach (var unit in state.Units)
            {
                UnitStand stand;
                if (_byKey.TryGetValue(unit.Key, out stand) && stand != null) stand.Retick(unit);
            }
        }

        /// <summary>体の四角を引く。⚠️ 居なければ null（黙って画面の隅に出さない）。</summary>
        public RectTransform StandOf(string key)
        {
            UnitStand stand;
            if (!_byKey.TryGetValue(key, out stand) || stand == null) return null;
            return (RectTransform)stand.transform;
        }

        public void Bind(BattleState state, Unit actor, Unit target,
            Action<int> onSkill, Action onFinish, Action onPick)
        {
            _byKey.Clear();

            // ── 味方 ────────────────────────────────────
            int i = 0;
            foreach (var unit in state.Units)
            {
                if (unit.Side != Side.Ally) continue;
                if (i < _allies.Length && _allies[i] != null)
                {
                    _allies[i].gameObject.SetActive(true);
                    _allies[i].Bind(unit, actor != null && ReferenceEquals(actor, unit), false);
                    _byKey[unit.Key] = _allies[i];
                }
                i++;
            }
            // ⚠️ 余った枠は隠す。空の器が残ると「誰か居る」に見える
            for (int k = i; k < _allies.Length; k++)
            {
                if (_allies[k] != null) _allies[k].gameObject.SetActive(false);
            }

            // ── 相手 ────────────────────────────────────
            // ⭐ 1体なら大きく1体、2体以上なら味方と同じ形で並べる
            var foes = new System.Collections.Generic.List<Unit>();
            foreach (var unit in state.Units)
            {
                if (unit.Side == Side.Enemy) foes.Add(unit);
            }
            bool lone = foes.Count <= 1;

            if (_foe != null)
            {
                _foe.gameObject.SetActive(lone && foes.Count == 1);
                if (lone && foes.Count == 1)
                {
                    _foe.Bind(foes[0], actor != null && ReferenceEquals(actor, foes[0]), true);
                    _byKey[foes[0].Key] = _foe;
                }
            }
            if (_foes != null)
            {
                for (int k = 0; k < _foes.Length; k++)
                {
                    if (_foes[k] == null) continue;
                    bool show = !lone && k < foes.Count;
                    _foes[k].gameObject.SetActive(show);
                    if (!show) continue;
                    _foes[k].Bind(foes[k], actor != null && ReferenceEquals(actor, foes[k]), true);
                    _byKey[foes[k].Key] = _foes[k];
                }
            }

            // ⚠️ 「戻る」は選択肢ではない。決着したら戻るしかないので押させない。
            //    代わりに WIN / LOSE を挟んでから切り替える（BattleDriver）
            bool over = state.Result != null;
            if (_finish != null) _finish.gameObject.SetActive(false);

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
