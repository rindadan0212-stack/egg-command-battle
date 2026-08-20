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
    /// 中に置くと歩いている最中に自分が消える。</summary>
    public sealed class TrailWalk : MonoBehaviour
    {
        /// <summary>1マスぶんの間。⭐ **短く。**⚠️ 長いと、出目のぶんだけ待たされる。</summary>
        private const float StepTime = 0.13f;

        private List<int> _path;
        private int _at;
        private float _age;
        private Action<int> _onStep;
        private Action _onDone;

        /// <summary>その道を1マスずつ辿る。⚠️ <paramref name="path"/> の先頭は**いま居るマス**。</summary>
        public static void Show(RectTransform parent, List<int> path,
            Action<int> onStep, Action onDone)
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
            walk._onStep = onStep;
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
            if (_age < StepTime) return;
            _age = 0f;

            _at++;
            if (_at < _path.Count)
            {
                _onStep?.Invoke(_path[_at]);
                return;
            }

            var done = _onDone;
            _onDone = null;
            _onStep = null;
            gameObject.SetActive(false);
            transform.SetParent(null, false);
            Destroy(gameObject);
            done?.Invoke();
        }
    }
}
