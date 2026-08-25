using System;
using System.Collections.Generic;
using UnityEngine;

namespace EggCommand.View
{
    /// <summary>駒が**1マスずつ踏みながら**進む間。
    ///
    /// ⭐ **これがある理由**（2026-08-20・作者の指示
    /// 「移動は瞬間移動ではなくひとマスずつ踏みながら進むように」）:
    /// ⚠️ 前は振った瞬間に駒が飛んでいたので、
    /// **何マス進んだのかが目で追えなかった**（出目と進みが合っていないように見える）。
    ///
    /// ⚠️ 進む先は <see cref="Core.Trails.Roll"/> が**もう決めている**。
    /// ここは決まった道を辿って見せるだけで、行き先を選び直さない。
    ///
    /// ⚠️ 画面の外（Overlay）に置く。画面は1マスごとに組み直されるので、
    /// 中に置くと歩いている最中に自分が消える。
    ///
    /// ⭐ **一歩ごとに間を変えられる**（2026-08-21）。⚠️ 全部が同じ間だと、
    /// 関門を通った所も素通りの所も同じ重さに見える ── 一番「払った甲斐」を
    /// 感じるべき所が、何も起きずに過ぎていた。</summary>
    public sealed class TrailWalk : MonoBehaviour
    {
        /// <summary>1マスぶんの間。⭐ **短く。**⚠️ 長いと、出目のぶんだけ待たされる。</summary>
        private const float StepTime = (float)Core.Beats.WalkStep;

        private List<int> _path;
        private int _at;
        private float _age;
        private float _wait;
        private Action<int> _onStep;
        private Func<int, float> _holdOf;
        private Action _onDone;

        /// <summary>その道を1マスずつ辿る。⚠️ <paramref name="path"/> の先頭は**いま居るマス**。</summary>
        /// <param name="holdOf">そのマスを踏んだあと、余分に置く間（秒）。
        /// ⭐ 関門のような「重い」マスで一拍おくために使う。null なら一定。</param>
        public static void Show(RectTransform parent, List<int> path,
            Action<int> onStep, Action onDone, Func<int, float> holdOf = null)
        {
            if (path == null || path.Count <= 1)
            {
                // ⚠️ 動かないなら何も見せない（1フレーム待たせる意味が無い）
                onDone?.Invoke();
                return;
            }

            var go = new GameObject("TrailWalk", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var walk = go.AddComponent<TrailWalk>();
            walk._path = path;
            walk._at = 0;
            walk._wait = StepTime;
            walk._onStep = onStep;
            walk._holdOf = holdOf;
            walk._onDone = onDone;
        }

        /// <summary>いま居るマスから行き先まで、実際に通る道を並べる。
        ///
        /// ⭐ **分かれ道では「選んだ道」をたどる。**⚠️ 素直に先頭の道をたどると、
        /// 選んでいない枝へ入って行き先に着かない。</summary>
        public static List<int> PathOf(Core.Raid raid, int from, int to)
        {
            var path = new List<int> { from };
            int cursor = from;
            // ⚠️ 万一たどり着けなくても止まる（盤の作りが変わったときに固まらせない）
            for (int guard = 0; guard < 200 && cursor != to; guard++)
            {
                var ways = raid.Trail.Squares[cursor].Ways;
                if (ways.Count == 0) break;
                int pick = 0;
                if (ways.Count > 1 && !raid.Took.TryGetValue(cursor, out pick)) break;
                cursor = ways[pick].To;
                path.Add(cursor);
            }
            // ⚠️ 着けなかったら歩かせない（途中で止まった駒を残さない）
            return cursor == to ? path : new List<int> { to };
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age < _wait) return;
            _age = 0f;

            _at++;
            if (_at < _path.Count)
            {
                int here = _path[_at];
                // ⭐ 次の一歩までの間は、踏んだマスが決める
                _wait = StepTime + (_holdOf == null ? 0f : Mathf.Max(0f, _holdOf(here)));
                _onStep?.Invoke(here);
                return;
            }

            var done = _onDone;
            _onDone = null;
            _onStep = null;
            _holdOf = null;
            gameObject.SetActive(false);
            transform.SetParent(null, false);
            Destroy(gameObject);
            done?.Invoke();
        }
    }
}
