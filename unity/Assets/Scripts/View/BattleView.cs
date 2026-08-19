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
        /// <summary>スキルレベル。⭐ 鍛えたぶんがここに出る（[スキルレベル]）。
        /// ⚠️ 効果量そのものは出さない ── 札に載せると名前が読めなくなる。長押しで開く。</summary>
        public Text Level;
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

        /// <summary>相手が1体のときの HP の帯。⭐ **画面の上に据える。**
        ///
        /// ⚠️ 体の足元の帯は、相手が大きいほど視線から外れる（親・ボスは 1.6 倍）。
        /// ⭐ 位置が動かない場所に置けば、殴っている最中でも同じ場所で読める。
        /// ⚠️ 雑魚の3対3では出さない（3体それぞれの足元に帯がある）。</summary>
        [SerializeField] private GameObject _foeBand;
        /// <summary>相手の名前。⭐ **帯の上**に置く（作者の指示 2026-08-19）。
        /// ⚠️ 帯に重ねていた頃は、HP が減ると字の下から赤が抜けて読みにくかった。</summary>
        [SerializeField] private Text _foeBandName;
        [SerializeField] private Image _foeBandFill;
        [SerializeField] private Image _foeBandMark;
        /// <summary>行動ゲージ。⭐ 相手の足元から**まるごと**ここへ移す。</summary>
        [SerializeField] private Image _foeBandGauge;

        /// <summary>帯の伸びる元の幅。⚠️ 実行時に縮めるので、最初に控えておく。</summary>
        private float _bandFullWidth = -1f;
        private float _bandGaugeFullWidth = -1f;
        /// <summary>いま出しているゲージの値。⚠️ Core は満ちる瞬間まで飛ぶので、
        /// そのまま描くと目で追えない（UnitStand と同じ理屈）。</summary>
        private float _bandGaugeShown = -1f;
        /// <summary>上の帯に出している相手。⚠️ 覚えないと <see cref="Retick"/> が誰の HP か分からない。</summary>
        private Unit _banded;

        /// <summary>いま出ている戦闘画面。⚠️ 演出が「どの体か」を引くための唯一の窓口。
        /// GameObject.Find で名前を探すのはやめた。Prefab で名前を変えた瞬間に
        /// 黙って何も出なくなる（実際それでダメージの数字が消えていた）。</summary>
        public static BattleView Live { get; private set; }

        private readonly System.Collections.Generic.Dictionary<string, UnitStand> _byKey =
            new System.Collections.Generic.Dictionary<string, UnitStand>();

        private void OnEnable() => Live = this;
        private void OnDisable() { if (Live == this) Live = null; }

        /// <summary>⚠️ <see cref="Retick"/> は「まる1目盛り進んだとき」しか来ない。
        /// ⭐ なめらかに詰めるのは毎フレームここで。</summary>
        private void Update()
        {
            if (_banded == null || _foeBandGauge == null) return;
            if (_bandGaugeFullWidth < 0f) return;
            float target = Mathf.Clamp01((float)_banded.Gauge / Core.Battle.GaugeMax);
            if (Mathf.Approximately(_bandGaugeShown, target)) return;
            // ⚠️ 打った直後は本当の値が下がる。そこは追いかけずすぐ合わせる
            _bandGaugeShown = target < _bandGaugeShown
                ? target
                : Mathf.MoveTowards(_bandGaugeShown, target, 6f * Time.deltaTime);
            Stretch(_foeBandGauge, _bandGaugeShown, _bandGaugeFullWidth);
        }

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
            // ⭐ 上の帯も一緒に。⚠️ ここを通さないと、殴っても帯が減らない
            PaintBand();
        }

        /// <summary>上の帯を描き直す。⭐ <see cref="Retick"/> からも <see cref="Bind"/> からも通る。
        /// ⚠️ 幅ではなく <c>sizeDelta</c> を縮める（UnitStand の帯と同じやり方に揃える）。</summary>
        private void PaintBand()
        {
            if (_banded == null) return;
            if (_foeBandFill != null)
            {
                if (_bandFullWidth < 0f) _bandFullWidth = _foeBandFill.rectTransform.sizeDelta.x;
                float ratio = _banded.MaxHp > 0 ? (float)_banded.Hp / _banded.MaxHp : 0f;
                Stretch(_foeBandFill, ratio, _bandFullWidth);
            }
            if (_foeBandGauge != null)
            {
                if (_bandGaugeFullWidth < 0f)
                {
                    _bandGaugeFullWidth = _foeBandGauge.rectTransform.sizeDelta.x;
                }
                float target = Mathf.Clamp01((float)_banded.Gauge / Core.Battle.GaugeMax);
                // ⚠️ 組み直しのたびに 0 から出し直さない（帯が飛んで見える）
                if (_bandGaugeShown < 0f) _bandGaugeShown = target;
                Stretch(_foeBandGauge, _bandGaugeShown, _bandGaugeFullWidth);
            }
        }

        private static void Stretch(Image image, float ratio, float fullWidth)
        {
            var size = image.rectTransform.sizeDelta;
            size.x = Mathf.Max(0f, fullWidth) * Mathf.Clamp01(ratio);
            image.rectTransform.sizeDelta = size;
        }

        /// <summary>体の四角を引く。⚠️ 居なければ null（黙って画面の隅に出さない）。</summary>
        public RectTransform StandOf(string key)
        {
            UnitStand stand;
            if (!_byKey.TryGetValue(key, out stand) || stand == null) return null;
            return (RectTransform)stand.transform;
        }

        /// <param name="targetFoe">狙っている敵。⚠️ 味方に掛ける技には使わない。</param>
        /// <param name="targetAlly">狙っている味方。⭐ 敵と別に覚える
        /// （1つで兼ねると、敵を選んだまま強化を押したときに黙って別の相手へ飛ぶ）。</param>
        /// <param name="onTap">体を押した。⭐ 押した側に応じて狙い先が入れ替わる。</param>
        /// <param name="onSkill">技を撃つ。</param>
        /// <param name="onDetail">技を**長押し**した。⭐ 効果の全文を読ませる。
        /// ⚠️ 使えない技（CT 中・自分の手番でない）でも開ける ── 待っている間こそ読みたい。</param>
        public void Bind(BattleState state, Unit actor, Unit targetFoe, Unit targetAlly,
            Action<int> onSkill, Action onFinish, Action<Unit> onTap,
            Action<Skill, int> onDetail = null)
        {
            _byKey.Clear();
            bool done = state.Result != null;

            // ── 味方 ────────────────────────────────────
            int i = 0;
            foreach (var unit in state.Units)
            {
                if (unit.Side != Side.Ally) continue;
                if (i < _allies.Length && _allies[i] != null)
                {
                    _allies[i].gameObject.SetActive(true);
                    var ally = unit;
                    _allies[i].Bind(ally, actor != null && ReferenceEquals(actor, ally), false,
                        targetAlly != null && ReferenceEquals(targetAlly, ally),
                        // ⚠️ 倒れている味方は狙えない（蘇生は狙い先を選ばせない）
                        done || !Core.Battle.IsAlive(ally) || onTap == null
                            ? null : () => onTap(ally));
                    _byKey[ally.Key] = _allies[i];
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

            bool banded = lone && foes.Count == 1;
            // ⚠️ 相手が入れ替わったらゲージを覚え直す（前の相手の値から続けない）
            if (!banded || _banded == null || !ReferenceEquals(_banded, foes[0]))
            {
                _bandGaugeShown = -1f;
            }
            _banded = banded ? foes[0] : null;
            if (_foe != null)
            {
                _foe.gameObject.SetActive(banded);
                if (banded)
                {
                    // ⚠️ 1体のときは選ぶ余地が無いので押させない
                    // ⭐ HP は**上の帯**に出すので、足元の帯は消す（同じ数を2か所に出さない）
                    _foe.Bind(foes[0], actor != null && ReferenceEquals(actor, foes[0]), true,
                        false, null, showHp: false);
                    _byKey[foes[0].Key] = _foe;
                }
            }
            // ⭐ **相手が1体のときだけ上の帯を出す。**
            // ⚠️ 雑魚の3対3では出さない（3体それぞれの足元に帯がある）
            if (_foeBand != null) _foeBand.SetActive(banded);
            if (banded)
            {
                if (_foeBandName != null) _foeBandName.text = foes[0].Name;
                if (_foeBandMark != null)
                {
                    _foeBandMark.color = ElementMark.ColorOf(foes[0].Creature.Element);
                }
                PaintBand();
            }
            if (_foes != null)
            {
                for (int k = 0; k < _foes.Length; k++)
                {
                    if (_foes[k] == null) continue;
                    bool show = !lone && k < foes.Count;
                    _foes[k].gameObject.SetActive(show);
                    if (!show) continue;
                    var foe = foes[k];
                    _foes[k].Bind(foe, actor != null && ReferenceEquals(actor, foe), true,
                        targetFoe != null && ReferenceEquals(targetFoe, foe),
                        done || !Core.Battle.IsAlive(foe) || onTap == null
                            ? null : () => onTap(foe));
                    _byKey[foe.Key] = _foes[k];
                }
            }

            // ⚠️ 「戻る」は選択肢ではない。決着したら戻るしかないので押させない。
            //    代わりに WIN / LOSE を挟んでから切り替える（BattleDriver）
            bool over = state.Result != null;
            if (_finish != null) _finish.gameObject.SetActive(false);

            // ⚠️ 「選ぶ」の札は外した（2026-08-18）。⭐ **体そのものを押して選ぶ**ので、
            //    別の押しどころは要らない。札は先頭の生存者を返すだけで、選べていなかった。
            if (_pick != null) _pick.gameObject.SetActive(false);

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
                // ⭐ **味方が動くときだけ出す**（作者の指示 2026-08-19）。
                // ⚠️ 相手の手番でも並べていた頃は、押せないのに札があって
                //    「押しても何も起きない」を毎回試させていた。
                view.Button.gameObject.SetActive(skill != null && !over && myTurn);
                if (skill == null || over || !myTurn) continue;

                bool usable = myTurn && Core.Battle.IsUsable(hand, slot);
                int cooldown = hand.Cooldowns[slot];
                int level = Creatures.SkillLevelOf(hand.Creature, slot);

                if (view.Name != null)
                {
                    view.Name.text = skill.Name;
                    // ⚠️ 属性の色の上でも読める濃紺で通す（読み方を色で変えない）
                    view.Name.color = usable ? Ui.OnLead : Ui.InkFaint;
                }
                if (view.Ct != null)
                {
                    // ⚠️ **素の CT を出さない。**卵で縮めたぶんが反映されず、
                    //    BOX の札（CreaturePanel は EffectiveCt を通している）と食い違っていた
                    //    ── 同じ技なのに画面によって数が違う（2026-08-19 の監査）。
                    int ct = Skills.EffectiveCt(slot, skill,
                        Creatures.SkillBoostOf(hand.Creature, slot));
                    view.Ct.text = slot == 0 ? "CT 0"
                        : cooldown > 0 ? $"あと {cooldown}" : $"CT {ct}";
                }
                // ⭐ **鍛えたぶんは札に出す。**⚠️ 出さないと、注ぎ込んだ卵が
                //    どこに効いたのか戦闘中に確かめようがない
                if (view.Level != null)
                {
                    view.Level.text = $"Lv{level}";
                    view.Level.color = usable ? Ui.OnLead : Ui.InkFaint;
                }
                if (view.Plate != null)
                {
                    // ⭐ **その個体の属性の色**（作者の指示 2026-08-19）。
                    // ⚠️ 絵は白い札のままにして、色は掛ける ── 属性は3色あるので
                    //    札の絵を3枚用意するより、同じ札に色を乗せるほうが揃う。
                    view.Plate.sprite = Ui.SkinSprite(usable ? "panel" : "button-off");
                    view.Plate.color = usable
                        ? ElementMark.ColorOf(hand.Creature.Element)
                        : Color.white;
                }
                view.Button.interactable = usable;

                int captured = slot;
                var chosen = skill;
                // ⚠️ **onClick は使わない。**長押しで詳細を開いたあと、指を離した拍子に
                //    その技を撃ってしまう（Button は必ず離した瞬間に反応する）。
                //    ⭐ 触る／押し続ける の分岐は LongPress が1か所で持つ。
                view.Button.onClick.RemoveAllListeners();
                var hold = view.Button.GetComponent<LongPress>();
                if (hold == null) hold = view.Button.gameObject.AddComponent<LongPress>();
                hold.OnTap = usable && onSkill != null ? () => onSkill(captured) : (Action)null;
                hold.OnHold = onDetail == null ? null : () => onDetail(chosen, level);
            }
        }
    }
}
