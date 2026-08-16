using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EggCommand.Core;

namespace EggCommand.View
{
    /// <summary>ホームの放置。⭐ 編成が横並びで右へ進み、敵が出たら自動で戦う。
    ///
    /// ⭐ 進んでいることは**背景が左へ流れる**ことで見せる。走者は歩幅だけ揺らす。
    /// ⚠️ ここは <see cref="Core.Idle"/> が決めた結果を描くだけ。
    /// 勝ち負けも素材もここでは決めない（決めた瞬間に第2の出所ができる）。
    /// </summary>
    public sealed class IdleStrip : MonoBehaviour
    {
        [SerializeField] private RectTransform _ground;   // 左へ流す地面（2枚を繋いで送る）
        [SerializeField] private Image[] _walkers;        // 編成3体
        [SerializeField] private Image _enemy;
        [SerializeField] private Image _enemyHp;
        [SerializeField] private RectTransform _enemySlot;

        /// <summary>地面が1秒に流れる幅。⭐ 進んでいる速さの見た目。</summary>
        private const float Scroll = 90f;
        /// <summary>歩幅の揺れ。⚠️ 大きいと跳ねて見えて「進んでいる」から離れる。</summary>
        private const float Bob = 6f;

        private Game _game;
        private System.Func<long> _clock;
        private System.Action _onGain;

        private readonly List<Vector2> _home = new List<Vector2>();
        private float _shownHp = 1f;
        private float _groundWidth;

        public void Bind(Game game, System.Func<long> clock, System.Action onGain)
        {
            _game = game;
            _clock = clock;
            _onGain = onGain;

            if (_ground != null && _groundWidth <= 0f) _groundWidth = _ground.rect.width * 0.5f;

            _home.Clear();
            var party = Games.PartyOf(game);
            for (int i = 0; i < _walkers.Length; i++)
            {
                if (_walkers[i] == null) continue;
                _home.Add(_walkers[i].rectTransform.anchoredPosition);
                bool has = i < party.Count;
                _walkers[i].gameObject.SetActive(has);
                if (!has) continue;
                var species = Creatures.SpeciesOf(party[i]);
                _walkers[i].sprite = PixelSpriteTexture.ToSprite(species.Sprite, Creatures.PaletteOf(party[i]));
                _walkers[i].preserveAspect = true;
            }

            if (_enemy != null)
            {
                // ⚠️ 相手は編成から決めない。放置の敵は「立ちはだかるもの」の絵でよい
                var species = SpeciesTable.ById("nushi");
                _enemy.sprite = PixelSpriteTexture.ToSprite(species.Sprite, species.Palettes[0]);
                _enemy.preserveAspect = true;
            }
        }

        private void Update()
        {
            if (_game == null || _clock == null) return;

            var party = Games.PartyOf(_game);
            long now = _clock();
            if (Core.Idle.Advance(_game.Idle, party, now) > 0 && _onGain != null) _onGain();

            // ── 地面を流す。⭐ 進んでいるのは「誰かが立っている」ときだけ
            bool moving = Core.Idle.PowerOf(_game.Idle, party, now) > 0.0;
            if (_ground != null && moving && _groundWidth > 0f)
            {
                var pos = _ground.anchoredPosition;
                pos.x -= Scroll * Time.deltaTime;
                // ⚠️ 1枚ぶん流れたら折り返す。放っておくと座標が際限なく増える
                if (pos.x <= -_groundWidth) pos.x += _groundWidth;
                _ground.anchoredPosition = pos;
            }

            // ── 走者。倒れている者は伏せて薄くする
            for (int i = 0; i < _walkers.Length && i < _home.Count; i++)
            {
                if (_walkers[i] == null || !_walkers[i].gameObject.activeSelf) continue;
                bool down = i < party.Count && Core.Idle.IsDown(_game.Idle, party[i], now);
                var rect = _walkers[i].rectTransform;
                rect.localEulerAngles = new Vector3(0f, 0f, down ? -75f : 0f);
                _walkers[i].color = down ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                float bob = down ? 0f : Mathf.Sin((Time.time + i * 0.4f) * 7f) * Bob;
                rect.anchoredPosition = _home[i] + new Vector2(0f, bob);
            }

            // ── 敵の帯。⚠️ Core は1秒刻みなので、そのまま描くと段になる。寄せて滑らかにする
            float target = Mathf.Clamp01((float)(_game.Idle.EnemyHp / Core.Idle.EnemyHp));
            _shownHp = Mathf.MoveTowards(_shownHp, target, 2.5f * Time.deltaTime);
            if (target > _shownHp) _shownHp = target;   // 次の敵が出たら即座に満タンへ
            if (_enemyHp != null) _enemyHp.fillAmount = _shownHp;

            // 倒れる瞬間だけ縮める。⭐ 「倒した」が目に見える
            if (_enemySlot != null)
            {
                float scale = 0.85f + 0.15f * _shownHp;
                _enemySlot.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
