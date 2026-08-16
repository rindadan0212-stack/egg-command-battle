using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>ホーム。⭐ 並びは Assets/Resources/Prefabs/HomeScreen.prefab が持つ。
    ///
    /// ⚠️ 以前は「入る幅から倍率を逆算する」計算がここに 30 行あった。
    /// Prefab へ移したので、大きさも間隔も Editor でドラッグして決める。
    /// </summary>
    public sealed class HomeView : MonoBehaviour
    {
        [SerializeField] private PartyStand[] _stands;   // 0=リーダー 1,2=脇
        [SerializeField] private GameObject _emptyStage; // 誰も居ないときの空の台座
        [SerializeField] private Text _goal;
        [SerializeField] private Text _partyValue;
        [SerializeField] private Text _speedValue;
        [SerializeField] private Text _reachValue;

        public void Bind(Game game)
        {
            var party = Games.PartyOf(game);

            for (int i = 0; i < _stands.Length; i++)
            {
                if (_stands[i] == null) continue;
                bool has = i < party.Count;
                _stands[i].gameObject.SetActive(has);
                if (has) _stands[i].Bind(party[i]);
            }
            if (_emptyStage != null) _emptyStage.SetActive(party.Count == 0);

            if (_goal != null) { _goal.text = $"{Nests.BossName} を倒す"; Ui.Knockout(_goal); }

            int speed = 0;
            foreach (var creature in party) speed += Creatures.StatsOf(creature).Spd;

            Put(_partyValue, $"{party.Count}/{Games.PartySize}");
            Put(_speedValue, speed.ToString());
            Put(_reachValue, party.Count > 0 ? Steal.DistanceFor(party).ToString("F0") : "—");
        }

        private static void Put(Text text, string value)
        {
            if (text == null) return;
            text.text = value;
            Ui.Knockout(text);
        }
    }
}
