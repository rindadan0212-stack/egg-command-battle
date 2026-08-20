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

                // ⚠️ 遊びは**設計座標の1**。ワールドの 1f だと、この Canvas では
                //    設計座標 192 ぶんになり、検査が事実上効かない（2026-08-20 に踏んだ）
                float slack = Slack(rect);
                // ⚠️ **スクロールする層の中身は画面外に出てよい**（切り取るのが仕事）。
                //    ⭐ 数えると、盤をスクロールにした日に 93件の偽の指摘が出る（2026-08-20）
                if (rect.GetComponentInParent<RectMask2D>() != null) continue;
                if (self[0].x < frame[0].x - slack || self[2].x > frame[2].x + slack
                    || self[0].y < frame[0].y - slack || self[2].y > frame[2].y + slack)
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
                    if (self[0].y < box[0].y - slack || self[2].y > box[2].y + slack
                        || self[0].x < box[0].x - slack || self[2].x > box[2].x + slack)
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

            // ⭐ 同じ札の中で、字どうし・字と絵が重なっていないか。
            // ⚠️ これを見ていなかったので、モックへ組み替えたとき詳細カードの中で
            //    絵とステの行が重なったまま通してしまった。親の枠内なので「枠外」では出ない。
            int textOverlaps = 0;
            var byParent = new Dictionary<Transform, List<RectTransform>>();
            foreach (var text in canvas.GetComponentsInChildren<Text>(false))
            {
                if (string.IsNullOrWhiteSpace(text.text)) continue;
                var rect = (RectTransform)text.transform;
                if (!byParent.ContainsKey(rect.parent)) byParent[rect.parent] = new List<RectTransform>();
                byParent[rect.parent].Add(rect);
            }
            foreach (var pair in byParent)
            {
                var group = pair.Value;
                for (int i = 0; i < group.Count; i++)
                {
                    for (int j = i + 1; j < group.Count; j++)
                    {
                        // ⚠️ **箱ではなく「字が乗っている範囲」で比べる。**
                        //    箱で比べていた頃、66pt の数字の箱がその下の小さい行を丸ごと含み、
                        //    「含むときは数えない」の除外に当たって 0件 と報告した
                        //    ── 画面では完全に被っていた（2026-08-20）。
                        var a = Ink(group[i]);
                        var b = Ink(group[j]);
                        float gap = Mathf.Max(Slack(group[i]), Slack(group[j]));
                        bool hit = !(a.xMax <= b.xMin + gap || b.xMax <= a.xMin + gap
                                  || a.yMax <= b.yMin + gap || b.yMax <= a.yMin + gap);
                        if (hit)
                        {
                            textOverlaps++;
                            if (textOverlaps <= 4)
                            {
                                sb.Append("  字が重なる: ").Append(Where(group[i]))
                                  .Append(" × ").Append(group[j].name).Append('\n');
                            }
                        }
                    }
                }
            }

            // ⭐ 並べたドット絵どうしが重なっていないか。
            //    倍率を変えた日に静かに重なる（実際、フォントを替えたら編成の3体が重なった）。
            var art = new List<RectTransform>();
            foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(false))
            {
                if (rect.name.StartsWith("Art ")) art.Add(rect);
            }
            int overlaps = 0;
            for (int i = 0; i < art.Count; i++)
            {
                for (int j = i + 1; j < art.Count; j++)
                {
                    if (art[i].parent != art[j].parent) continue;
                    var a = new Vector3[4]; art[i].GetWorldCorners(a);
                    var b = new Vector3[4]; art[j].GetWorldCorners(b);
                    bool hit = !(a[2].x <= b[0].x || b[2].x <= a[0].x || a[2].y <= b[0].y || b[2].y <= a[0].y);
                    if (hit)
                    {
                        overlaps++;
                        if (overlaps <= 3)
                            sb.Append("  絵が重なる: ").Append(art[i].name).Append(" × ").Append(art[j].name).Append('\n');
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
              .Append(" 字の重なり=").Append(textOverlaps)
              .Append(" 絵の重なり=").Append(overlaps)
              .Append(" 小さい押しどころ=").Append(tooSmall)
              .Append(" / Button=").Append(canvas.GetComponentsInChildren<Button>(false).Length)
              .Append(" Text=").Append(canvas.GetComponentsInChildren<Text>(false).Length);
            return sb.ToString();
        }

        /// <summary>面で区切ったパネルか。⚠️ 名前でしか見分けられないので、
        /// 画面側がパネルに付ける名前の付け方をここが知っている（増えたら足す）。</summary>
        /// <summary>判定の遊び。⭐ **設計座標の 1 ぶん**をワールド単位で返す。
        ///
        /// ⚠️ 判定はワールド座標で書くのに、遊びだけ生の `1f` を書いてはいけない。
        /// この Canvas は ScreenSpaceCamera で倍率が 0.0052 なので、
        /// `1f` は設計座標の **約192** ── 検査が丸ごと効かなくなる（2026-08-20 に踏んだ）。</summary>
        private static float Slack(RectTransform rect)
        {
            float scale = Mathf.Max(Mathf.Abs(rect.lossyScale.x), Mathf.Abs(rect.lossyScale.y));
            return scale <= 0f ? 1f : scale;
        }

        /// <summary>字が実際に乗っている範囲（ワールド座標）。
        ///
        /// ⚠️ <see cref="Text"/> の矩形は器の大きさで、字の大きさではない。
        /// 中央寄せの見出しは器いっぱいの矩形を持つので、矩形で重なりを見ると
        /// **同じ器に入っている物すべてと重なって見える** ── だから昔は
        /// 「丸ごと含むときは数えない」で逃げていた。⚠️ その除外が本物の被りも隠した。
        /// ⭐ 寄せ（<see cref="Text.alignment"/>）と preferred 寸法から、
        /// 器の中のどこに字が乗るかを出す。</summary>
        private static Rect Ink(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float left = corners[0].x, bottom = corners[0].y;
            float width = corners[2].x - left, height = corners[2].y - bottom;

            var text = rect.GetComponent<Text>();
            if (text == null) return new Rect(left, bottom, width, height);

            // ⚠️ **先にワールド単位へ直してから器に収める。**
            //    preferred は器のローカル単位、width/height はワールド単位。
            //    順を逆にすると Min がローカルとワールドを比べてしまい、
            //    字の範囲が器いっぱいに膨らんで**重なりを見逃す**（2026-08-20 に踏んだ）。
            float sx = rect.lossyScale.x <= 0f ? 1f : rect.lossyScale.x;
            float sy = rect.lossyScale.y <= 0f ? 1f : rect.lossyScale.y;
            // ⚠️ 横は器で切ってよい（折り返す設定なので、器より広くは描かれない）。
            // ⚠️ **縦は切らない。**Ui.Label は verticalOverflow = Overflow なので、
            //    器より高い字は**器の外に描かれる**。切ると、はみ出したぶんが
            //    検査から消えて被りを見逃す（2026-08-20 に踏んだ）。
            float w = Mathf.Min(text.preferredWidth * sx, width);
            float h = text.preferredHeight * sy;

            float x = left, y = bottom;
            switch (text.alignment)
            {
                case TextAnchor.UpperLeft: case TextAnchor.MiddleLeft: case TextAnchor.LowerLeft:
                    break;
                case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight:
                    x = left + width - w; break;
                default:
                    x = left + (width - w) / 2f; break;
            }
            switch (text.alignment)
            {
                case TextAnchor.UpperLeft: case TextAnchor.UpperCenter: case TextAnchor.UpperRight:
                    y = bottom + height - h; break;
                case TextAnchor.LowerLeft: case TextAnchor.LowerCenter: case TextAnchor.LowerRight:
                    break;
                default:
                    y = bottom + (height - h) / 2f; break;
            }
            return new Rect(x, y, w, h);
        }

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
