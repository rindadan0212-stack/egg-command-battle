using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace EggCommand.EditorTools
{
    /// <summary>シーンでクリックしたとき**実際に何が拾われるか**を1回だけ調べる。
    ///
    /// ⚠️ 拾い判定は Scene ビューの描画中でないと動かないので、
    /// 一度だけ差し込んで、測ったら自分で外す。
    /// ⭐ 「クリックできない」を目視や推測でなく、拾えた／拾えないの事実で切り分けるための道具。
    /// </summary>
    public static class PickProbe
    {
        [MenuItem("Egg Command/クリック判定を調べる")]
        public static void Probe()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) { Debug.LogWarning("Scene ビューが無い"); return; }
            SceneView.duringSceneGui -= Once;
            SceneView.duringSceneGui += Once;
            view.Focus();
            view.Repaint();
        }

        private static void Once(SceneView view)
        {
            SceneView.duringSceneGui -= Once;
            // ⚠️ 拾い判定は Layout のときに呼ぶ。Repaint で呼んでいた頃は
            //    1点も拾えず、Unity 側が壊れていると誤診しかけた（検査の側の間違い）。
            if (Event.current == null || Event.current.type != EventType.Layout)
            {
                SceneView.duringSceneGui += Once;
                view.Repaint();
                return;
            }

            var report = new System.Text.StringBuilder("■ クリック判定の実測\n");

            // ⭐ **狙った部品の画面座標を計算して突く。**
            // ⚠️ 適当な格子で突いていた頃は、そもそも部品の無い所を叩いていた可能性があった。
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null) { report.Append("Prefab Mode に入っていない\n"); Save(report); return; }

            int tried = 0, got = 0;
            foreach (var g in stage.prefabContentsRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (!g.gameObject.activeInHierarchy) continue;
                var r = (RectTransform)g.transform;
                if (r.rect.width < 20f || r.rect.height < 20f) continue;
                var world = r.TransformPoint(r.rect.center);
                var gui = HandleUtility.WorldToGUIPoint(world);
                tried++;
                var deep = HandleUtility.PickGameObject(gui, false);
                if (tried <= 8)
                {
                    report.Append("  " + r.name.PadRight(14)
                        + " 画面座標 " + gui.ToString("0")
                        + " → 拾えた: " + (deep == null ? "**なし**" : deep.name) + "\n");
                }
                if (deep != null) got++;
            }
            report.Append($"突いた部品 {tried} / 拾えた {got}\n");
            report.Append(got == 0
                ? "⚠️ **1つも拾えない。**クリックで選べない状態。\n"
                : "⭐ クリックで選べる状態。\n");
            report.Append("Scene ビューの大きさ: " + view.position.size + "\n");

            // ── 対照実験 ──────────────────────────────
            // ⚠️ UI が拾えないのか、検査そのものが動いていないのかを分ける。
            //    UI でない普通の物体を1つ置いて、同じ手順で突いてみる。
            var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
            probe.name = "__pick probe__";
            UnityEditor.SceneManagement.StageUtility.PlaceGameObjectInCurrentStage(probe);
            probe.transform.position = new Vector3(540f, 960f, -5f);   // 画面の真ん中・少し手前
            probe.transform.localScale = new Vector3(400f, 400f, 1f);
            var at = HandleUtility.WorldToGUIPoint(probe.transform.position);
            var hit = HandleUtility.PickGameObject(at, false);
            report.Append("対照（ただの四角）: 画面座標 " + at.ToString("0")
                + " → 拾えた: " + (hit == null ? "**なし**" : hit.name) + "\n");
            report.Append(hit == null
                ? "⚠️ **対照も拾えない → 検査の側が動いていない。**Unity の挙動は判定できない。\n"
                : "⭐ **対照は拾える → 検査は動いている。**拾えないのは UI だけ。\n");
            Object.DestroyImmediate(probe);
            Save(report);
        }

        /// <summary>⚠️ Console は1行目しか外から読めないので、ファイルにも落とす。</summary>
        private static void Save(System.Text.StringBuilder report)
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "egg-pick-probe.txt");
            System.IO.File.WriteAllText(path, report.ToString());
            Debug.Log(report.ToString() + "\n（控え: " + path + "）");
        }
    }
}
