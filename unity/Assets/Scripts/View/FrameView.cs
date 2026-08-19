using System;
using UnityEngine;
using UnityEngine.UI;

namespace EggCommand.View
{
    /// <summary>下段の札1枚。</summary>
    [Serializable]
    public sealed class DockPanel
    {
        public Button Button;
        public Text Name;
        public Text Count;
    }

    /// <summary>上段のバーと下段のドック。
    /// ⭐ 配置は Assets/Resources/Prefabs/AppFrame.prefab が持つ。ここに座標は無い。
    /// ⚠️ ドックはホームだけに出す（常時タブにしない）。</summary>
    public sealed class FrameView : MonoBehaviour
    {
        [SerializeField] private Button _back;
        [SerializeField] private Text _title;
        [SerializeField] private Text _badge;
        /// <summary>右肩を押しどころにするときだけ敷く札。
        /// ⚠️ 字と同じ部品には敷けない（Graphic は1つの GameObject に1つだけ）。
        /// ⭐ 数字（3/50 など）のときは出さない ── 押せないものを押せる形にしない。</summary>
        [SerializeField] private GameObject _badgePlate;
        [SerializeField] private GameObject _dock;
        [SerializeField] private DockPanel[] _panels;   // 探索 / 孵化 / 配合 / BOX
        [SerializeField] private RectTransform _body;   // 各画面を入れる場所

        public RectTransform Body => _body;

        /// <param name="canBack">戻れる画面か。⚠️ 戦闘中だけは false
        /// （途中で戻れると、不利な盤面を無かったことにできる）。</param>
        public void Bind(bool home, string title, string badge, Action onBack, bool canBack = true)
        {
            if (_back != null)
            {
                _back.gameObject.SetActive(!home && canBack);
                _back.onClick.RemoveAllListeners();
                if (!home && canBack && onBack != null) _back.onClick.AddListener(() => onBack());
            }
            if (_title != null) { _title.text = title; Ui.Knockout(_title); }
            if (_badge != null)
            {
                _badge.text = badge;
                _badge.gameObject.SetActive(badge.Length > 0);
                Ui.Knockout(_badge, 3);
            }
            if (_dock != null) _dock.SetActive(home);
            // ⚠️ **押せる状態を毎回消す。**残すと、別の画面へ移ったあとも
            //    右肩の数字が押せてしまい、前の画面の札が開く。
            if (_badge != null)
            {
                var was = _badge.GetComponent<Button>();
                if (was != null) { was.onClick.RemoveAllListeners(); was.enabled = false; }
                _badge.raycastTarget = false;
                if (_badgePlate != null) _badgePlate.SetActive(false);
                // ⚠️ 数字（3/50）は右端に寄せる。⭐ 押しどころのときだけ真ん中へ
                _badge.alignment = TextAnchor.MiddleRight;
            }
        }

        /// <summary>右肩を「押せる入口」に変える。⭐ 画面の中に置けないものをここへ出す。
        ///
        /// ⚠️ 使うのは、本体がびっしり埋まっている画面だけ（探索は札4枚で
        /// 24〜1604 が埋まり、どこに置いても重なった）。
        /// ⚠️ <see cref="Bind"/> のあとに呼ぶこと（Bind が毎回この状態を消す）。</summary>
        public void ShowExtra(string label, Action onTap)
        {
            if (_badge == null) return;
            _badge.text = label;
            _badge.gameObject.SetActive(true);
            _badge.raycastTarget = true;
            // ⭐ **押しどころなら札を敷く。**⚠️ 字だけだと題名と見分けが付かず、
            //    押せることが読めなかった（レビュー指摘 2026-08-19）。
            if (_badgePlate != null) _badgePlate.SetActive(true);
            // ⚠️ 右寄せのまま札を敷くと、字が札の右端に張り付く（実測で発覚）
            _badge.alignment = TextAnchor.MiddleCenter;
            _badge.color = Ui.Ink;

            var button = _badge.GetComponent<Button>();
            if (button == null) button = _badge.gameObject.AddComponent<Button>();
            button.enabled = true;
            button.targetGraphic = _badge;
            button.onClick.RemoveAllListeners();
            if (onTap != null) button.onClick.AddListener(() => onTap());
        }

        /// <summary>使わない札を隠す。⚠️ 空の札を残すと「押せるのに何も起きない」になる。</summary>
        public void HidePanelsFrom(int index)
        {
            for (int i = index; i < _panels.Length; i++)
            {
                if (_panels[i] != null && _panels[i].Button != null)
                {
                    _panels[i].Button.gameObject.SetActive(false);
                }
            }
        }

        public void BindPanel(int index, string name, string count, Action onGo)
        {
            if (index < 0 || index >= _panels.Length) return;
            var panel = _panels[index];
            if (panel == null) return;
            if (panel.Button != null) panel.Button.gameObject.SetActive(true);
            if (panel.Name != null) panel.Name.text = name;
            if (panel.Count != null) panel.Count.text = count;
            if (panel.Button != null)
            {
                panel.Button.onClick.RemoveAllListeners();
                if (onGo != null) panel.Button.onClick.AddListener(() => onGo());
            }
        }
    }
}
