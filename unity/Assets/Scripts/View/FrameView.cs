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
        [SerializeField] private GameObject _dock;
        [SerializeField] private DockPanel[] _panels;   // 探索 / 孵化 / 配合 / BOX
        [SerializeField] private RectTransform _body;   // 各画面を入れる場所

        public RectTransform Body => _body;

        public void Bind(bool home, string title, string badge, Action onBack)
        {
            if (_back != null)
            {
                // ⚠️ ホーム以外は必ず戻れる。戻れない画面を作らない
                _back.gameObject.SetActive(!home);
                _back.onClick.RemoveAllListeners();
                if (!home && onBack != null) _back.onClick.AddListener(() => onBack());
            }
            if (_title != null) { _title.text = title; Ui.Knockout(_title); }
            if (_badge != null)
            {
                _badge.text = badge;
                _badge.gameObject.SetActive(badge.Length > 0);
                Ui.Knockout(_badge, 3);
            }
            if (_dock != null) _dock.SetActive(home);
        }

        public void BindPanel(int index, string name, string count, Action onGo)
        {
            if (index < 0 || index >= _panels.Length) return;
            var panel = _panels[index];
            if (panel == null) return;
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
