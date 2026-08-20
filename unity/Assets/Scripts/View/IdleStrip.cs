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
        [SerializeField] private Image[] _walkers;        // 編成ぶん（⚠️ 足りなければ写して増やす）
        [SerializeField] private Image _enemy;
        [SerializeField] private Image _enemyHp;
        [SerializeField] private RectTransform _enemySlot;

        /// <summary>器が足りなければ、最後の1つを写して増やす。
        /// ⭐ **占有する幅は変えない** ── 間隔を詰め、そのぶん縮める。
        /// ⚠️ 1つしか無いときは間隔が測れないので何もしない。</summary>
        private static Image[] Fit(Image[] slots, int want)
        {
            if (slots == null || slots.Length < 2 || want <= slots.Length) return slots;
            // ⚠️ 空きの入った配列（prefab で枠だけ残した形）では測れない
            if (slots[0] == null || slots[slots.Length - 1] == null) return slots;

            var first = slots[0].rectTransform;
            var last = slots[slots.Length - 1].rectTransform;
            Vector2 span = last.anchoredPosition - first.anchoredPosition;
            Vector2 was = span / (slots.Length - 1);
            Vector2 now = span / (want - 1);
            float shrink = was.sqrMagnitude > 0.01f
                ? Mathf.Sqrt(now.sqrMagnitude / was.sqrMagnitude) : 1f;

            var grown = new Image[want];
            for (int i = 0; i < slots.Length; i++) grown[i] = slots[i];
            for (int i = slots.Length; i < want; i++)
            {
                var made = Instantiate(slots[slots.Length - 1], last.parent);
                made.name = $"{slots[0].name} +{i}";
                grown[i] = made;
            }

            // ⚠️ **並びの中心を動かさない。**先頭の位置だけ合わせて縮めると、
            //    器が細くなったぶん全体が左（上）へ寄る（2026-08-20 に実測して 27px ずれていた）。
            // ⭐ 元の並びの中心と、詰めた並びの中心を合わせる。
            Vector2 size = first.rect.size;
            Vector2 wasMid = first.anchoredPosition + (span + size) * 0.5f;
            Vector2 nowSpan = now * (want - 1);
            Vector2 head = wasMid - (nowSpan + size * shrink) * 0.5f;

            for (int i = 0; i < want; i++)
            {
                grown[i].rectTransform.anchoredPosition = head + now * i;
                grown[i].rectTransform.localScale = Vector3.one * shrink;
            }
            return grown;
        }

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

        /// <summary>登場の残り（1 = 画面の外、0 = 定位置）。
        /// ⭐ 倒した瞬間に次が定位置へ現れると「回復した」ようにしか見えない。
        /// 右の外から転がってこさせて、別の個体だと分かるようにする。</summary>
        private float _entry;
        private int _shownDefeated = -1;
        private Vector2 _enemyHome;

        /// <summary>転がって来るのにかかる秒。⚠️ 短いと結局パッと入れ替わって見える。
        /// ⭐ 0.7 では次が来るのが早すぎたので倍にした（間が空くほど別個体だと分かる）。</summary>
        private const float EntrySeconds = 1.4f;
        /// <summary>画面の外へ置く距離。⚠️ 短いと画面内から湧いたように見える。</summary>
        private const float EntryFrom = 700f;

        public void Bind(Game game, System.Func<long> clock, System.Action onGain)
        {
            _game = game;
            _clock = clock;
            _onGain = onGain;

            if (_ground != null && _groundWidth <= 0f) _groundWidth = _ground.rect.width * 0.5f;

            _home.Clear();
            var party = Games.PartyOf(game, PartyKind.Idle);
            // ⭐ **器を体数に合わせる。**⚠️ prefab には3つしか置いていないので、
            //    4体目が黙って居ないことになる（2026-08-20 の4体化）。
            _walkers = Fit(_walkers, Games.PartySize);
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

            if (_enemySlot != null) _enemyHome = _enemySlot.anchoredPosition;
            _shownDefeated = game.Idle.Defeated;
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

            var party = Games.PartyOf(_game, PartyKind.Idle);
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

            // ── 倒したら、次を画面の外から転がしてくる
            if (_game.Idle.Defeated != _shownDefeated)
            {
                _shownDefeated = _game.Idle.Defeated;
                _entry = 1f;
                _shownHp = 1f;
            }
            if (_entry > 0f) _entry = Mathf.MoveTowards(_entry, 0f, Time.deltaTime / EntrySeconds);

            // ── 敵の帯。⚠️ Core は1秒刻みなので、そのまま描くと段になる。寄せて滑らかにする
            float target = Mathf.Clamp01((float)(_game.Idle.EnemyHp / Core.Idle.EnemyHp));
            _shownHp = Mathf.MoveTowards(_shownHp, target, 2.5f * Time.deltaTime);
            if (target > _shownHp) _shownHp = target;
            if (_enemyHp != null)
            {
                _enemyHp.fillAmount = _shownHp;
                // ⚠️ 来ている途中は帯を出さない。満タンの帯が動くと「回復した」に見える
                _enemyHp.transform.parent.gameObject.SetActive(_entry <= 0.05f);
            }

            if (_enemySlot != null)
            {
                // ⭐ 右の外から転がって来る。回るので「別の個体が来た」と分かる
                float ease = _entry * _entry;   // 近づくほど減速する
                _enemySlot.anchoredPosition = _enemyHome + new Vector2(EntryFrom * ease, 0f);
                // ⚠️ 向きは進む向きと合わせる。左へ転がるなら反時計回り
                _enemySlot.localEulerAngles = new Vector3(0f, 0f, -_entry * 720f);
                // 倒れる瞬間だけ縮める。⭐ 「倒した」が目に見える
                float scale = 0.85f + 0.15f * _shownHp;
                _enemySlot.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
