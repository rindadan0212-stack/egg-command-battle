using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>ステ1行。⭐ **素質と育成を別の列に置く。**
    /// ⚠️ 以前は「41 (17+8)」と1つの字に詰めていたので、
    /// 何体か見比べるときに桁が揃わず、どれが伸びているのか読み取れなかった。</summary>
    [Serializable]
    public sealed class PanelStatRow
    {
        /// <summary>ステの名前。⭐ 得意には ▲、不得意には ▼ を頭に付ける。</summary>
        public Text Label;
        /// <summary>素質。⭐ **種族の基礎値を含んだ「生まれつきの実値」**。
        /// ⚠️ 生の素質ロール（0〜40）ではない ── そのままでは戦闘の数と繋がらないので、
        /// **素質 ＋ 強化 ＝ 実値** になる分け方にしてある。</summary>
        public Text Wild;
        /// <summary>育てて伸びた分。⭐ プレイヤーが動かせるのはここだけ。
        /// ⚠️ 振ったポイントではなく**実値がいくつ増えたか**（得意の ×1.15 込み）。</summary>
        public Text Trained;
    }

    /// <summary>技1枠の札。⭐ 名前・レベル・CT を1つの箱にまとめる。</summary>
    [Serializable]
    public sealed class PanelSkillBox
    {
        public GameObject Root;
        public Text Name;
        public Text Level;
        public Text Ct;
    }

    /// <summary>1体を1枚で見せる札。⭐ **BOX と 配合 で同じ並びを使う。**
    ///
    /// ⚠️ 画面ごとに組み立てを書くと、同じ個体が画面によって違う顔になり、
    /// 「BOX では見えるのに配合では見えない」欄が生まれる（実際そうなっていた）。
    /// ⭐ 並びの寸法は Prefab が持つ。ここは**何をどこへ流すか**だけ。</summary>
    public sealed class CreaturePanel : MonoBehaviour
    {
        [SerializeField] private Image _art;
        [SerializeField] private Image _element;
        /// <summary>種族名。⭐ 見出しの主役。</summary>
        [SerializeField] private Text _name;
        /// <summary>「Lv 55/55」。</summary>
        [SerializeField] private Text _title;
        /// <summary>「c001　1代　変異0」。⚠️ どれも小さく、主役にしない。</summary>
        [SerializeField] private Text _sub;
        [SerializeField] private PanelStatRow[] _stats;
        /// <summary>特性。⭐ **名前だけでは何も伝わらない**ので働きも並べる。
        /// ⚠️ 無ければ空にする（「特性なし」と書かない ── 無いことは書かなくても分かる）。</summary>
        /// <summary>特性。⚠️ 無ければ**帯ごと**消す（空の灰色帯が残ると壊れて見える）。</summary>
        [SerializeField] private Text _trait;
        [SerializeField] private PanelSkillBox[] _skills;

        public void Bind(Creature creature)
        {
            if (creature == null) return;
            var species = Creatures.SpeciesOf(creature);

            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(species.Sprite, Creatures.PaletteOf(creature));
                _art.preserveAspect = true;
            }
            if (_element != null) _element.color = ElementMark.ColorOf(creature.Element);

            if (_name != null) _name.text = species.Name;
            // ⚠️ Lv を主役にしない。同じ Lv でも中身はまるで別物。見るべきは下の表
            if (_title != null) _title.text = $"Lv {Levels.Of(creature)}/{Levels.MaxOf(creature)}";
            // ⭐ 変異は「これ以上増えない」ことが判断に効く
            if (_sub != null)
            {
                _sub.text = $"{creature.Id}　{creature.Generation}代　変異{creature.MutationCounter}";
            }

            // ⭐ **素質 ＋ 強化 ＝ 実値** になるように割る。
            // ⚠️ 素質側に種族基礎を含めないと、足しても戦闘で使う数にならない。
            // ⚠️ 得意・不得意の ×1.15 / ×0.85 は最後に掛かるので、
            //    「育てる前の実値」を出してから引く（掛けたあとで引かないと1ずれる）。
            var full = Creatures.StatsOf(creature);
            var born = Creatures.Slanted(
                Stats.ActualStats(species.Base, creature.Wild, new StatBlock(0, 0, 0, 0)),
                creature.Strong, creature.Weak);

            for (int i = 0; i < _stats.Length && i < Stats.Keys.Length; i++)
            {
                var key = Stats.Keys[i];
                var row = _stats[i];
                if (row == null) continue;
                var tint = key == creature.Strong ? Ui.GoodInk
                    : key == creature.Weak ? Ui.DangerInk : Ui.Ink;

                if (row.Label != null)
                {
                    // ⭐ 得意・不得意は行そのものに書く。⚠️ 別の行に「▲速度」と書くと、
                    //    表のどの行のことか目で探すことになる
                    string mark = key == creature.Strong ? "▲" : key == creature.Weak ? "▼" : "";
                    row.Label.text = mark + Stats.LabelOf(key);
                    row.Label.color = tint == Ui.Ink ? Ui.InkDim : tint;
                }
                // ⚠️ **HP だけは戦闘で Battle.HpScale 倍される**（生の数を書かない
                //    ── 「3倍」と書いたまま 105 倍になっていた／2026-08-19 の監査）。
                //    ⭐ 表に素の数を出していた頃は、素質 37 と書いてある個体が
                //    戦闘では 111 で戦っていた ── 表が実値でなくポイントを出していた。
                //    ⭐ 作者の指示（2026-08-19）で、**生まれ持った実値**をそのまま出す。
                int scale = key == StatKey.Hp ? Battle.HpScale : 1;

                if (row.Wild != null)
                {
                    row.Wild.text = Ui.Digits(born[key] * scale);
                    row.Wild.color = tint;
                }
                if (row.Trained != null)
                {
                    // ⚠️ 0 を「0」と書かない。⭐ 伸びている行だけが目に入るようにする
                    int gained = (full[key] - born[key]) * scale;
                    row.Trained.text = gained > 0 ? $"+{Ui.Digits(gained)}" : "−";
                    row.Trained.color = gained > 0 ? Ui.AccentInk : Ui.InkFaint;
                }
            }

            if (_trait != null)
            {
                var trait = Creatures.TraitOf(creature);
                // ⭐ **無いときは「—」を入れる。**⚠️ 空の灰色帯のままだと壊れて見え、
                //    かといって帯ごと消すと札に穴が空く（どちらも実測で確認）。
                //    「—」は強化の列と同じ約束なので、画面の中で読み方が揃う。
                // ⚠️ 「特性なし」とは書かない（無いことは書かなくても分かる）。
                _trait.text = trait == null ? "—" : $"{trait.Name} — {trait.Gist}";
                _trait.color = trait == null ? Ui.InkFaint : Ui.AccentInk;
            }

            // ⭐ **技の札はその個体の属性の色**（戦闘と同じ約束・作者の指示 2026-08-19）。
            // ⚠️ 灰色のままだと「押せない」に見え、3つとも沈んで読めなかった。
            var tone = ElementMark.ColorOf(creature.Element);
            var skills = Creatures.SkillsOf(creature);
            for (int i = 0; i < _skills.Length; i++)
            {
                var box = _skills[i];
                if (box == null) continue;
                var skill = i < skills.Length ? skills[i] : null;
                // ⚠️ 空き枠は箱ごと消す。空の箱を置くと「何か入るはず」に見える
                if (box.Root != null) box.Root.SetActive(skill != null);
                if (skill == null) continue;

                // ⭐ 札の地を属性の色に。⚠️ 字は濃紺で通す（色で読み方を変えない）
                var face = box.Root == null ? null : box.Root.GetComponent<Image>();
                if (face != null) face.color = tone;
                if (box.Name != null) { box.Name.text = skill.Name; box.Name.color = Ui.OnLead; }
                if (box.Level != null)
                {
                    // ⭐ **レベルは常に出す。**出さないと「鍛えられる」ことに気づけない
                    int level = Creatures.SkillLevelOf(creature, i);
                    box.Level.text = $"Lv{level}";
                    box.Level.color = level > 1 ? Ui.AccentInk : Ui.OnLead;
                }
                if (box.Ct != null)
                {
                    var boost = Creatures.SkillBoostOf(creature, i);
                    box.Ct.text = $"CT{Skills.EffectiveCt(i, skill, boost)}";
                    box.Ct.color = Ui.OnLead;
                }
            }
        }
    }
}
