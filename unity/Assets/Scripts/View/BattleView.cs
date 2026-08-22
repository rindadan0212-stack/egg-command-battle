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

        /// <summary>器を体数ぶん並べ直す。⭐ **足りなければ写して増やし、収まるだけ大きく置く。**
        ///
        /// ⚠️ prefab には3つしか置いていない（体数を変えるたびに手で触らないため）。
        /// ⚠️ **元の並びをそのまま詰めない。**最初そうしたら、使える縦 1290 に対して
        /// 元の 1080 の中へ押し込み、必要以上に小さくなった（2026-08-20 に実測して直した）。
        /// ⭐ 使える高さから、詰まらない最大の大きさを**逆算**する。
        ///
        /// ⚠️ **横は自分で中央に置く。**prefab の x（60 と 600）は左右で非対称で、
        /// 縮めると左上に張り付いたまま残るので、**全体が左へ寄る**（実測で中心 437／画面は 540）。
        /// ⭐ 味方は幅の 1/4、相手は 3/4 を**中心**にする。</summary>
        /// <param name="at">列の中心を、親の幅のどこに置くか（0〜1）。</param>
        private static UnitStand[] Lay(UnitStand[] slots, int want, float at)
        {
            if (slots == null || slots.Length < 2 || want <= 0) return slots;
            if (slots[0] == null || slots[slots.Length - 1] == null) return slots;

            var first = (RectTransform)slots[0].transform;
            var last = (RectTransform)slots[slots.Length - 1].transform;
            var parent = first.parent as RectTransform;
            if (parent == null) return slots;

            float was = Mathf.Abs(last.anchoredPosition.y - first.anchoredPosition.y)
                / (slots.Length - 1);
            float high = first.rect.height;
            if (was <= 0.01f || high <= 0.01f) return slots;

            // ⭐ 器の詰まり具合（高さ ÷ 間隔）は元のまま保つ
            float density = high / was;
            // ⚠️ **1つ目の上端から、床までの長さ。**足し算にしていた頃は
            //    使える長さを2倍近く見積もり、技の札の下まではみ出していた（2026-08-20）
            float room = Room(parent) - Mathf.Abs(first.anchoredPosition.y);
            if (room <= 0f) return slots;
            // ⭐ N 個が収まる最大の間隔。⚠️ 元より大きくはしない
            float step = Mathf.Min(was, room / ((want - 1) + density));
            float shrink = step / was;
            float top = first.anchoredPosition.y;

            var grown = new UnitStand[want];
            for (int i = 0; i < want && i < slots.Length; i++) grown[i] = slots[i];
            for (int i = slots.Length; i < want; i++)
            {
                var made = Instantiate(slots[slots.Length - 1], last.parent);
                made.name = $"{slots[0].name} +{i}";
                grown[i] = made;
            }
            // ⚠️ 余った器は隠す（体数が減ったとき）
            for (int i = want; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].gameObject.SetActive(false);
            }

            float wide = first.rect.width * shrink;
            float x = parent.rect.width * at - wide / 2f;
            for (int i = 0; i < want; i++)
            {
                var rect = (RectTransform)grown[i].transform;
                rect.anchoredPosition = new Vector2(x, top - step * i);
                rect.localScale = Vector3.one * shrink;
            }
            return grown;
        }

        /// <summary>立ち位置に使ってよい縦の長さ。⚠️ 技の札の上まで。</summary>
        private static float Room(RectTransform parent)
        {
            float floor = parent.rect.height;
            // ⚠️ **隠れている札も数える。**自分の手番でないと技の札は出ないが、
            //    場所は変わらない。false（表示中だけ）にしていた頃は、
            //    相手の手番に組むと札の下まで並びが伸びていた（2026-08-20）
            foreach (var rect in parent.GetComponentsInChildren<RectTransform>(true))
            {
                if (!rect.name.StartsWith("Skill ")) continue;
                float top = Mathf.Abs(rect.anchoredPosition.y);
                if (top < floor) floor = top;
            }
            // ⚠️ 札にぴったり付けない。⭐ 一息ぶん空ける
            return Mathf.Max(0f, floor - Breath);
        }

        /// <summary>並びと技の札のあいだ。</summary>
        private const float Breath = 24f;

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
            // ⚠️ 引数は (技, Lv, **枠の番号**)。⭐ 枠1 の CT は常に 0 なので、
            //    枠を渡さないと詳細だけ技の表の数（0 でない CT）を出してしまう。
            Action<Skill, int, int> onDetail = null)
        {
            _byKey.Clear();
            bool done = state.Result != null;

            // ⭐ **器を体数に合わせる。**⚠️ prefab には3つしか置いていない ──
            //    体数を変えるたびに prefab を手で触るのをやめるため、足りなければ写して増やす
            //    （2026-08-20 の4体化）。⚠️ 増えたぶんは**元の占有範囲の中**に詰める。
            int allies = 0, enemies = 0;
            foreach (var unit in state.Units)
            {
                if (unit.Side == Side.Ally) allies++; else enemies++;
            }
            _allies = Lay(_allies, allies, 0.25f);
            _foes = Lay(_foes, enemies, 0.75f);

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
                hold.OnHold = onDetail == null ? null : () => onDetail(chosen, level, captured);
            }
        }
    }
}
