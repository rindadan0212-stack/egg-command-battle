using System;
using UnityEngine;
using UnityEngine.UI;

namespace EggCommand.View
{
    /// <summary>強奪の結果に出す押しどころ。
    /// ⚠️ 結果を文章で言わない。⭐ 盤の上に残った軌跡が既に語っている。</summary>
    public sealed class StealResultView : MonoBehaviour
    {
        [SerializeField] private Button _take;
        [SerializeField] private Button _fight;

        public void Bind(bool success, Action onTake, Action onFight)
        {
            if (_take != null)
            {
                _take.gameObject.SetActive(success);
                _take.onClick.RemoveAllListeners();
                if (success && onTake != null) _take.onClick.AddListener(() => onTake());
            }
            if (_fight != null)
            {
                _fight.gameObject.SetActive(!success);
                _fight.onClick.RemoveAllListeners();
                if (!success && onFight != null) _fight.onClick.AddListener(() => onFight());
            }
        }
    }
}
