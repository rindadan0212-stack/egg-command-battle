using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace EggCommand.EditorTools
{
    /// <summary>画面を数値で調べる。
    ///
    /// ⚠️ スクショだけで被り・はみ出しを判定しない。
    /// 「キレイに見える」画像は、親が中身を切り取っているだけかもしれない。
    /// 実体は矩形の数値で確かめる。
    ///
    /// ⭐ ここが見るのは3つ:
    ///   1. 画面の外へ出ていないか
    ///   2. **親の枠から出ていないか**（切り取られて見た目には出ない）
    ///   3. 押しどころが指で押せる大きさか
    /// </summary>
    public static class Inspect
    {
        /// <summary>押しどころの下限（設計座標）。1080 幅の設計で 112 ≒ 実機 44pt。</summary>
        private const float MinTap = 112f;

        public static string Screen()
        {
            var canvas = GameObject.Find("App Canvas");
            if (canvas == null) return "App Canvas が無い";
            var sb = new StringBuilder();

            // 画面が二重に積まれていないか（Destroy はフレーム末尾まで効かない）
            int layers = 0;
            foreach (Transform child in canvas.transform)
            {
                if (child.name == "Sky") layers++;
            }

            var canvasRect = canvas.GetComponent<RectTransform>();
            var frame = new Vector3[4];
            canvasRect.GetWorldCorners(frame);

            int offScreen = 0, offParent = 0, tooSmall = 0;

            foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(false))
            {
                if (rect == canvasRect) continue;
                var self = new Vector3[4];
                rect.GetWorldCorners(self);

                if (self[0].x < frame[0].x - 1f || self[2].x > frame[2].x + 1f
                    || self[0].y < frame[0].y - 1f || self[2].y > frame[2].y + 1f)
                {
                    offScreen++;
                    if (offScreen <= 5) sb.Append("  画面外: ").Append(Where(rect)).Append('\n');
                }

                // ⭐ 面で区切ったパネルの中身が、そのパネルから出ていないか。
                //    スクロール層の中身は出てよい（切り取るのが仕事）ので除く。
                var parent = rect.parent as RectTransform;
                if (parent != null && IsPanel(parent) && rect.parent.GetComponent<RectMask2D>() == null)
                {
                    var box = new Vector3[4];
                    parent.GetWorldCorners(box);
                    if (self[0].y < box[0].y - 1f || self[2].y > box[2].y + 1f
                        || self[0].x < box[0].x - 1f || self[2].x > box[2].x + 1f)
                    {
                        offParent++;
                        if (offParent <= 5)
                        {
                            sb.Append("  枠外: ").Append(Where(rect))
                              .Append(" h=").Append(rect.rect.height.ToString("F0"))
                              .Append(" 親h=").Append(parent.rect.height.ToString("F0")).Append('\n');
                        }
                    }
                }
            }

            foreach (var button in canvas.GetComponentsInChildren<Button>(false))
            {
                var rect = button.GetComponent<RectTransform>();
                if (rect.rect.height < MinTap - 1f || rect.rect.width < 60f)
                {
                    tooSmall++;
                    if (tooSmall <= 5)
                    {
                        sb.Append("  小さい: ").Append(Where(rect))
                          .Append(' ').Append(rect.rect.width.ToString("F0"))
                          .Append('x').Append(rect.rect.height.ToString("F0")).Append('\n');
                    }
                }
            }

            sb.Append("層=").Append(layers)
              .Append(" 画面外=").Append(offScreen)
              .Append(" 枠外=").Append(offParent)
              .Append(" 小さい押しどころ=").Append(tooSmall)
              .Append(" / Button=").Append(canvas.GetComponentsInChildren<Button>(false).Length)
              .Append(" Text=").Append(canvas.GetComponentsInChildren<Text>(false).Length);
            return sb.ToString();
        }

        /// <summary>面で区切ったパネルか。⚠️ 名前でしか見分けられないので、
        /// 画面側がパネルに付ける名前の付け方をここが知っている（増えたら足す）。</summary>
        private static bool IsPanel(RectTransform rect)
        {
            string name = rect.name;
            return name.StartsWith("Nest ") || name.StartsWith("C ") || name.StartsWith("Unit ")
                || name.StartsWith("Egg ") || name == "Boss" || name == "Preview" || name == "Board";
        }

        private static string Where(RectTransform rect)
        {
            string chain = rect.name;
            var parent = rect.parent;
            int depth = 0;
            while (parent != null && depth++ < 2)
            {
                chain = parent.name + "/" + chain;
                parent = parent.parent;
            }
            return chain;
        }
    }
}
