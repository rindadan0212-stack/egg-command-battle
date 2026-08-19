using System;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>探索に出ている巣1件の札。⭐ 配置は Prefab（EncounterCard）が持つ。
    ///
    /// ⚠️ 出すのは**絵とレベルと残り時間だけ**。名前も素質も届く距離も出さない。
    /// 中身が分かると「勝てる相手だけ選ぶ」になり、飛ばして確かめる意味が消える。
    ///
    /// ⭐ **残り時間は中身ではなく急かすもの**なので出す。
    /// ⚠️ ★が高い巣ほど短く（★5 は10分）、黙って消えると理不尽にしかならない。
    /// </summary>
    public sealed class EncounterCard : MonoBehaviour
    {
        [SerializeField] private Image _art;
        [SerializeField] private Text _level;
        [SerializeField] private Button _button;
        /// <summary>残り時間の字。⚠️ 無ければ何も出さない（古い Prefab でも落ちない）。</summary>
        [SerializeField] private Text _left;
        /// <summary>減っていく帯。⭐ 字を読まなくても「もう少し」が分かる。</summary>
        [SerializeField] private Image _drain;
        /// <summary>盗んだ回数。⭐ **守りがどれだけ固まったか**。
        /// ⚠️ 出していなかったので、あと何回盗めるか・もう塞がっているかが札から読めなかった。</summary>
        [SerializeField] private Text _raids;

        /// <summary>残りがこの割合を切ったら赤くする。⭐ 数字を読ませずに急かす。</summary>
        private const float Hurry = 0.25f;

        private Encounter _encounter;
        private Func<long> _now;
        private Action _onGone;
        /// <summary>消えたことを1度だけ伝えるための札。⚠️ 毎フレーム伝えない。</summary>
        private bool _told;

        /// <param name="now">いまの Unix 秒。⚠️ Core は時計を持たないので呼ぶ側が渡す。</param>
        /// <param name="onGone">居座る時間が切れた。⭐ 画面を組み直させる。</param>
        /// <param name="raids">その巣から盗んだ回数。⭐ 札に「守りの固さ」として出す。</param>
        public void Bind(Encounter encounter, Action onTap, Func<long> now = null,
            Action onGone = null, int raids = 0)
        {
            _encounter = encounter;
            _now = now;
            _onGone = onGone;
            _told = false;

            var species = SpeciesTable.ById(encounter.Nest.SpeciesId);
            if (_art != null)
            {
                _art.sprite = PixelSpriteTexture.ToSprite(species.Sprite, species.Palettes[0]);
                _art.preserveAspect = true;
            }
            if (_level != null) _level.text = encounter.Level.ToString();
            if (_raids != null)
            {
                // ⭐ 4回盗むと親が道を塞ぐ ＝ 入れば必ず戦闘（巣の寿命）
                bool sealed_ = Steal.IsSealed(raids);
                _raids.text = sealed_ ? "戦闘" : raids <= 0 ? "" : new string('●', raids);
                _raids.color = sealed_ ? Ui.DangerInk : Ui.AccentInk;
            }
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                if (onTap != null) _button.onClick.AddListener(() => onTap());
            }
            Retime();
        }

        // ⚠️ 組み直しは触ったときにしか走らないので、時計はここで進める。
        //    ⭐ 秒が動いているものを止めて見せると、画面が嘘をつく
        private void Update() => Retime(canTell: true);

        /// <param name="canTell">切れたことを呼び側へ伝えてよいか。
        /// ⚠️ <see cref="Bind"/> からは **false**。組み立ての最中に組み直しを頼むと、
        /// Build → Bind → Refresh → Build … と無限に回る（実際スタックが溢れた）。</param>
        private void Retime(bool canTell = false)
        {
            if (_encounter == null || _now == null) return;

            // ⚠️ **期限を持たない巣がある**（時刻を渡さずに始めた保存）。
            //    ⭐ 0 を「もう切れた」と読まない ── 読むと即座に消しにかかる
            if (_encounter.UntilUnix <= 0)
            {
                if (_left != null) _left.text = "";
                if (_drain != null) _drain.fillAmount = 0f;
                return;
            }

            int left = Encounters.LeftOf(_encounter, _now());
            int whole = Encounters.SecondsFor(_encounter.Nest.Tier);
            float ratio = whole <= 0 ? 0f : Mathf.Clamp01((float)left / whole);
            bool hurry = ratio <= Hurry;

            if (_left != null)
            {
                _left.text = Rarities.Clock(left);
                _left.color = hurry ? Ui.DangerInk : Ui.InkDim;
            }
            if (_drain != null)
            {
                _drain.fillAmount = ratio;
                _drain.color = hurry ? Ui.Danger : Ui.Good;
            }

            // ⭐ 切れたら画面を組み直させる（Expire → Refill が走って次の巣が出る）
            if (left > 0 || _told || !canTell) return;
            _told = true;
            if (_onGone != null) _onGone();
        }
    }
}
