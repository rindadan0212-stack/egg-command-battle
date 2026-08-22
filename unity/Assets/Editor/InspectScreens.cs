using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace EggCommand.EditorTools
{
    /// <summary>飾ったあとに崩れていないかを**数で**調べる。
    ///
    /// ⭐ **コードを読まずに押すだけ**で分かるようにするための道具。
    /// 位置は人が決めるので、コードにできるのは「壊れていないか」を測ることだけ。
    ///
    /// ⚠️ **見た目で判断しない。**スクリーンショットは縮んで届くので、
    /// 字の被りや切れは目では分からない（実際それで何度も見落とした）。
    /// ⭐ 重なりも はみ出しも、四隅の座標を引き算して数で出す。
    /// </summary>
    public static class InspectScreens
    {
        /// <summary>**画面の1点ぶんの世界の長さ。**⚠️ <see cref="Sweep"/> の頭で入れ直す。
        ///
        /// ⚠️ **誤差の許容を生の数で書かない。**⭐ 四隅は**世界の単位**で返ってくるので、
        /// 画面まるごとが縦 10.0 しかない。そこへ「0.5 なら誤差」と書くと、
        /// 実質 **96 点ぶん**を見逃す ── 検査が甘いのではなく、**目盛りが違っていた**。
        /// ⭐ 実測（2026-08-21）: BOX 画面で、縦に 0.23 も離れた別の札に
        /// 「覆われて見えない」と 3 件の嘘が出た。⚠️ 嘘を出す道具は読まれなくなる。</summary>
        private static float _dot = 0.005f;

        /// <summary>接している（隣り合う表の行など）を重なりと数えない余裕。⭐ 4点ぶん。</summary>
        private static float Slack { get { return _dot * 4f; } }

        /// <summary>ここまでのずれは測り誤差とみなす。⭐ 2点ぶん。</summary>
        private static float Nudge { get { return _dot * 2f; } }

        /// <summary>これ以上薄いものは「描かれていない」とみなす。</summary>
        private const float Faint = 0.02f;

        /// <summary>見つかったもの。⭐ 種類ごとに分ける ── 直す先が違う。</summary>
        private sealed class Findings
        {
            public readonly List<string> Overlaps = new List<string>();
            public readonly List<string> Wide = new List<string>();
            public readonly List<string> Outside = new List<string>();
            public readonly List<string> OffScreen = new List<string>();
            public readonly List<string> Buried = new List<string>();

            public int Total => Overlaps.Count + Wide.Count + Outside.Count
                + OffScreen.Count + Buried.Count;
        }

        [MenuItem("Egg Command/画面を検査する")]
        public static void Inspect()
        {
            // ⚠️ **ダイアログを出さない。**出すと人が押すまで Unity 全体が止まり、
            //    道具（MCP の execute_menu_item 等）から叩いたときに応答不能になる。
            //    ⭐ 実際に固まって、作業が丸ごと止まった（2026-08-18）。
            //    結果は Console に出す ── 読む場所が1つ増えるより、止まらないほうが良い。
            if (!Application.isPlaying)
            {
                Debug.LogWarning("画面を検査する: ▶ を押してゲームを動かしてから、"
                    + "もう一度この項目を選んでください。"
                    + "⚠️ 字が入った状態でないと、被りや はみ出しは測れません");
                return;
            }

            string report = Report();
            if (report.Contains("⚠️")) Debug.LogWarning("■ 崩れています\n" + report);
            else Debug.Log("■ 崩れていません\n" + report);
        }

        /// <summary>**画面を順に回して全部見る。**
        ///
        /// ⚠️ **1画面ずつ手で回さない。**⭐ この道具は今まで「いま出ている画面」しか
        /// 見られず、実際には**触っている画面にしか掛けていなかった**。
        /// そのせいで配合画面の重なり 15 件が、検査を持っているのに 3 日見つからなかった
        /// （2026-08-21 の討論 ── 「測る道具が見ていない」）。
        ///
        /// ⚠️ 画面を渡り歩くので、遊んでいる途中に押すと**その潜入は捨てられる**。</summary>
        [MenuItem("Egg Command/画面を全部検査する")]
        public static void InspectAll()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("画面を全部検査する: ▶ を押してから、もう一度どうぞ。");
                return;
            }
            Debug.Log(AllScreens());
        }

        /// <summary>全画面ぶんの結果を1つの文字にして返す。⭐ 道具から呼べる。</summary>
        public static string AllScreens()
        {
            var app = Object.FindAnyObjectByType<View.App>();
            if (app == null) return "画面の親（App）が見つかりませんでした。";

            var sb = new StringBuilder("■ 画面を全部検査する\n");
            var order = new[]
            {
                View.Screen.Home, View.Screen.Nests, View.Screen.Breed, View.Screen.Box,
            };
            foreach (var screen in order)
            {
                app.Show(screen);
                app.Refresh();
                sb.Append(screen).Append(": ").Append(OneLine()).Append('\n');
            }

            // ⭐ 潜入だけは巣が要る。⚠️ 出会いが1つも無い盤面では飛ばす
            if (app.Game != null && app.Game.Encounters.Count > 0)
            {
                View.TrailScreen.Enter(app, app.Game.Encounters[0].Nest);
                app.Refresh();
                sb.Append(View.Screen.Trail).Append(": ").Append(OneLine()).Append('\n');
            }
            else sb.Append("Trail: 出会いが無いので飛ばしました\n");

            return sb.ToString();
        }

        /// <summary>結果を文字で返す。⭐ ダイアログを出さないので、道具から呼べる。
        /// ⚠️ ダイアログを出すと Unity が操作待ちで止まり、外から続きを流せない。</summary>
        public static string Report()
        {
            var canvas = Screenful();
            if (canvas == null) return "画面が見つかりませんでした。";

            var found = new Findings();
            int looked = Sweep(canvas.transform, found);

            var report = new StringBuilder();
            report.Append("調べた部品: ").Append(looked).Append("\n\n");
            Line(report, "字の重なり", found.Overlaps);
            Line(report, "字が枠より広い", found.Wide);
            Line(report, "字が枠からはみ出し", found.Outside);
            Line(report, "画面の外", found.OffScreen);
            Line(report, "覆われて見えない", found.Buried);

            if (found.Total > 0)
            {
                report.Append("\n詳しい場所は Console に出しました。");
                Debug.LogWarning(Detail(found));
            }
            return report.ToString();
        }

        /// <summary>1行にまとめる。⭐ 道具から読むとき用。
        ///
        /// ⚠️ **呼ぶ側で while で空白を詰めない。**実際に
        /// <c>while (r.Contains("  ")) r = r.Replace("  ", "  ")</c> と書いて
        /// Unity を無限ループで固め、強制終了する羽目になった（2026-08-18）。
        /// ⭐ 詰めるならここで1回だけやる。</summary>
        public static string OneLine()
        {
            var parts = Report().Split(
                new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();
            foreach (var part in parts) sb.Append(part.Trim()).Append("  ");
            return sb.ToString().Trim();
        }

        /// <summary>中身の載っている覆いを選ぶ。
        ///
        /// ⚠️ **FindAnyObjectByType&lt;Canvas&gt;() を使わない。**戦闘中は演出用の
        /// 「Fx」も Canvas なので、そちらを掴むと**部品0個・重なり0件**と報告する。
        /// ⭐ 何も無いことを「異常なし」と読み違える ── 道具として最悪の壊れ方（実測 2026-08-18）。
        /// 子を一番多く抱えているものを本体とみなす。</summary>
        private static Canvas Screenful()
        {
            Canvas best = null;
            int most = -1;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                int count = canvas.GetComponentsInChildren<Transform>(false).Length;
                if (count <= most) continue;
                most = count;
                best = canvas;
            }
            return best;
        }

        private static void Line(StringBuilder report, string what, List<string> found)
        {
            report.Append(found.Count == 0 ? "OK  " : "⚠️  ").Append(what).Append("  ")
                .Append(found.Count).Append(" 件\n");
        }

        private static string Detail(Findings f)
        {
            var sb = new StringBuilder("■ 画面の検査\n");
            foreach (var line in f.Overlaps) sb.Append("  字の重なり: ").Append(line).Append('\n');
            foreach (var line in f.Wide) sb.Append("  字が枠より広い: ").Append(line).Append('\n');
            foreach (var line in f.Outside) sb.Append("  枠からはみ出し: ").Append(line).Append('\n');
            foreach (var line in f.OffScreen) sb.Append("  画面の外: ").Append(line).Append('\n');
            foreach (var line in f.Buried) sb.Append("  覆われて見えない: ").Append(line).Append('\n');
            return sb.ToString();
        }

        /// <summary>いま出ているものを全部見る。⚠️ 隠れているものは数えない。
        ///
        /// ⚠️ **絵も測る。**⭐ 2026-08-21 の討論まで、ここは <see cref="Text"/> と
        /// <see cref="Button"/> しか集めていなかった。つまり「はみ出し 0 件」は
        /// **字だけを数えた 0** で、絵は1枚も見ていなかった。
        /// ⭐ 実際にその隙間から2つ抜けた ── 行き先の印を一番下へ送って
        /// 地の**下**に沈めた件と、卵のマスが盤ぜんぶを押しどころにした件。</summary>
        private static int Sweep(Transform root, Findings f)
        {
            Canvas.ForceUpdateCanvases();

            // ⭐ **目盛りを合わせる。**画面の高さ（世界）÷ 画面の高さ（点）
            {
                var frame = (RectTransform)root;
                var edge = new Vector3[4];
                frame.GetWorldCorners(edge);
                _dot = frame.rect.height > 1f ? (edge[2].y - edge[0].y) / frame.rect.height : 0.005f;
            }

            var rects = new List<RectTransform>();
            var names = new List<string>();
            var isText = new List<bool>();
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(false))
            {
                var text = rect.GetComponent<Text>();
                var button = rect.GetComponent<Button>();
                if (text == null && button == null) continue;
                if (text != null && string.IsNullOrEmpty(text.text)) continue;

                rects.Add(rect);
                names.Add(Path(rect) + (text == null ? "（押しどころ）" : $"「{text.text}」"));
                isText.Add(text != null);

                // ⚠️ 字が入れ物より広いと、端が切れるか隣へはみ出す
                if (text != null && text.preferredWidth > rect.rect.width + 1f)
                {
                    f.Wide.Add($"{Path(rect)}「{text.text}」"
                        + $" 要る {text.preferredWidth:0} / 幅 {rect.rect.width:0}");
                }
            }

            // ⭐ **描かれている物を全部集める**（字・絵の両方）。
            //    ⚠️ 重なりの判定には使わない ── 面と面は重なって当たり前。
            //    使うのは「画面の外」と「覆われて見えない」だけ。
            var art = new List<RectTransform>();
            var artNames = new List<string>();
            var artOpaque = new List<bool>();
            var order = new Dictionary<Transform, int>();
            {
                int at = 0;
                foreach (var rect in root.GetComponentsInChildren<RectTransform>(false))
                {
                    order[rect] = at++;
                    if (Faded(rect, root)) continue;
                    var text = rect.GetComponent<Text>();
                    var image = rect.GetComponent<Image>();
                    bool inked = text != null && !string.IsNullOrEmpty(text.text)
                        && text.color.a > Faint;
                    bool painted = image != null && image.enabled && image.color.a > Faint;
                    if (!inked && !painted) continue;
                    art.Add(rect);
                    artNames.Add(Path(rect) + (inked ? $"「{text.text}」" : "（絵）"));
                    // ⭐ 覆い隠せるのは「透けない一色の面」。
                    //    ⚠️ 絵柄つきは中が抜けていることがあるので覆いに数えない。
                    artOpaque.Add(!inked && painted
                        && image.sprite == null && image.color.a > 0.99f);
                }
            }

            var boxes = new Rect[rects.Count];
            // ⚠️ **覆いの中と外を比べない。**合成・ステ・技の札は、後ろを押させないために
            //    画面いっぱいの Button を敷き、その上に札を載せる。
            //    比べてしまうと、札を1枚開くだけで画面中の部品と重なったと報告する
            //    （実測: ステの札 9件、技の札 4件）。
            //    ⭐ **層が違うものは重ならない。**どの覆いの中に居るかで分ける。
            var whole = new Vector3[4];
            ((RectTransform)root).GetWorldCorners(whole);
            float screenArea = (whole[2].x - whole[0].x) * (whole[2].y - whole[0].y);
            var layer = new Transform[rects.Count];
            var hidden = new bool[rects.Count];
            for (int i = 0; i < rects.Count; i++)
            {
                boxes[i] = InkOf(rects[i]);
                layer[i] = LayerOf(rects[i], root, screenArea);
                // ⚠️ **巻物の外へ出たぶんは描かれない。**数えると嘘の重なりになる
                hidden[i] = !Clip(rects[i], ref boxes[i]);
            }

            for (int i = 0; i < boxes.Length; i++)
            {
                for (int j = i + 1; j < boxes.Length; j++)
                {
                    // 親子は重なっていて当たり前
                    if (rects[i].IsChildOf(rects[j]) || rects[j].IsChildOf(rects[i])) continue;
                    // ⚠️ 層が違えば重ならない（覆いの中と外）。数えない
                    if (layer[i] != layer[j]) continue;
                    // ⚠️ 切り取られて見えていないものは数えない
                    if (hidden[i] || hidden[j]) continue;
                    // ⚠️ **押しどころどうしも見る。**見ていなかった頃は、開いた札の下に
                    //    一覧の升が丸ごと潜り込んでも「0件」と報告していた（実測で発覚）。
                    //    ⭐ 触れる面が重なっていたら、下は押せないので必ず不具合。
                    if (boxes[i].Overlaps(boxes[j])) f.Overlaps.Add($"{names[i]} × {names[j]}");
                }
            }

            // ⭐ **画面の外へ出ていないか。**⚠️ 巻物で切り取られたぶんは数えない
            var screen = Rect.MinMaxRect(whole[0].x, whole[0].y, whole[2].x, whole[2].y);
            var artBox = new Rect[art.Count];
            var artLayer = new Transform[art.Count];
            var artGone = new bool[art.Count];
            for (int i = 0; i < art.Count; i++)
            {
                artBox[i] = InkOf(art[i]);
                artLayer[i] = LayerOf(art[i], root, screenArea);
                artGone[i] = !Clip(art[i], ref artBox[i]);
                if (artGone[i]) continue;
                // ⚠️ **巻物の中は数えない。**⭐ 一覧は画面の端で切れるのが当たり前で、
                //    数えると升が流れているだけで嘘の警告が出る
                //    （実測 12件・2026-08-21 の配合画面 ── 窓が画面の下端を
                //    0.66 はみ出しており、そこに並ぶ升が全部引っかかった）。
                if (Scrolled(art[i], root)) continue;
                if (artBox[i].xMin < screen.xMin - Nudge || artBox[i].xMax > screen.xMax + Nudge
                    || artBox[i].yMin < screen.yMin - Nudge || artBox[i].yMax > screen.yMax + Nudge)
                {
                    f.OffScreen.Add(artNames[i]);
                }
            }

            // ⭐ **覆われて見えないものが無いか。**
            //    ⚠️ 「あとから描かれた・透けない・一色の面」に**丸ごと**入っているものだけ。
            //    ⭐ 見えているのに数えると誰も読まなくなるので、条件はきつくしてある。
            for (int i = 0; i < art.Count; i++)
            {
                if (artGone[i] || artOpaque[i]) continue;
                for (int j = 0; j < art.Count; j++)
                {
                    if (i == j || !artOpaque[j] || artGone[j]) continue;
                    if (artLayer[i] != artLayer[j]) continue;
                    if (art[i].IsChildOf(art[j]) || art[j].IsChildOf(art[i])) continue;
                    // ⚠️ **あとに描かれたものが上。**先に描かれた面は覆えない
                    if (order[art[j]] <= order[art[i]]) continue;
                    if (!Swallows(artBox[j], artBox[i])) continue;
                    f.Buried.Add($"{artNames[i]} ← {artNames[j]}");
                    break;
                }
            }

            // ⭐ 器の外へ出ていないか。⚠️ 器に絵が付いているものだけを器とみなす
            var corners = new Vector3[4];
            foreach (var box in root.GetComponentsInChildren<Image>(false))
            {
                var holder = (RectTransform)box.transform;
                if (holder.childCount == 0) continue;
                // ⚠️ **巻物の窓を「器」と見なさない。**窓は中身より小さいのが当たり前で、
                //    まだ流れてきていない升を全部「はみ出し」と数えてしまう
                //    （実測 14件・2026-08-20 の潜入画面）。⭐ 器は札のほうであって窓ではない。
                if (holder.GetComponent<RectMask2D>() != null) continue;
                holder.GetWorldCorners(corners);
                var bounds = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                foreach (var child in holder.GetComponentsInChildren<RectTransform>(false))
                {
                    if (child == holder) continue;
                    var text = child.GetComponent<Text>();
                    // ⚠️ **ここは字だけ。絵を混ぜない。**⭐ 器が切り取らないなら、
                    //    枠から出た絵は「崩れ」ではなく**飾り**。混ぜて測ったら
                    //    出てきたのは 42 件すべて意図した飾りだった
                    //    （実測 2026-08-21 ── 草の房・いま居る印・レア度の角バッジ）。
                    //    ⭐ 絵は「画面の外」と「覆われて見えない」で見る。そちらは曖昧さが無い。
                    if (text == null || string.IsNullOrEmpty(text.text)
                        || text.color.a <= Faint) continue;
                    if (Faded(child, root)) continue;
                    // ⚠️ 巻物で切り取られて**描かれていない**ものは数えない
                    var ink = InkOf(child);
                    if (!Clip(child, ref ink)) continue;
                    child.GetWorldCorners(corners);
                    if (corners[0].x < bounds.xMin - Nudge || corners[2].x > bounds.xMax + Nudge
                        || corners[0].y < bounds.yMin - Nudge || corners[2].y > bounds.yMax + Nudge)
                    {
                        f.Outside.Add($"{Path(child)}「{text.text}」が {holder.name} の外");
                    }
                }
            }
            return rects.Count;
        }

        /// <summary>巻物の中に居るか。⭐ 中なら「画面の端で切れる」のは当たり前。</summary>
        private static bool Scrolled(Transform part, Transform root)
        {
            for (var at = part.parent; at != null && at != root; at = at.parent)
                if (at.GetComponent<RectMask2D>() != null) return true;
            return false;
        }

        /// <summary>a が b を**丸ごと**呑み込んでいるか。</summary>
        private static bool Swallows(Rect a, Rect b) =>
            a.xMin <= b.xMin + Nudge && a.yMin <= b.yMin + Nudge
            && a.xMax >= b.xMax - Nudge && a.yMax >= b.yMax - Nudge;

        /// <summary>親のどこかで薄められていないか。
        /// ⚠️ <see cref="CanvasGroup"/> で消してある札を「見えている」と数えない。</summary>
        private static bool Faded(Transform part, Transform root)
        {
            for (var at = part; at != null; at = at.parent)
            {
                var group = at.GetComponent<CanvasGroup>();
                if (group != null && group.alpha <= Faint) return true;
                if (at == root) break;
            }
            return false;
        }

        /// <summary>巻物（<see cref="RectMask2D"/>）で切り取られたぶんを落とす。
        ///
        /// ⚠️ 一覧は器より長い中身を持つので、下のほうの升は**描かれていない**。
        /// それを数えていたころは、スクロールしていないだけで
        /// 「升が『決定』の押しどころと重なっている」と出た（実測 3件・2026-08-19）。
        ///
        /// ⭐ 器と重なっている部分だけを残す。まったく見えないなら false。</summary>
        private static bool Clip(RectTransform part, ref Rect box)
        {
            var corners = new Vector3[4];
            for (var at = part.parent; at != null; at = at.parent as Transform)
            {
                var mask = at.GetComponent<RectMask2D>();
                if (mask == null) continue;
                ((RectTransform)at).GetWorldCorners(corners);
                var window = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                if (!box.Overlaps(window)) return false;
                box = Rect.MinMaxRect(
                    Mathf.Max(box.xMin, window.xMin), Mathf.Max(box.yMin, window.yMin),
                    Mathf.Min(box.xMax, window.xMax), Mathf.Min(box.yMax, window.yMax));
            }
            return box.width > 0f && box.height > 0f;
        }

        /// <summary>その部品が「どの覆いの上」に載っているか。⚠️ 覆いの外なら null。
        ///
        /// ⭐ 覆い＝画面をほぼ丸ごと覆う、字を持たない面。
        /// 一番近い覆いを返すので、覆いの中の札どうしは今までどおり比べられる。</summary>
        private static Transform LayerOf(Transform part, Transform root, float screenArea)
        {
            var corners = new Vector3[4];
            for (var at = part; at != null && at != root; at = at.parent)
            {
                var rect = at as RectTransform;
                if (rect == null) continue;
                if (rect.GetComponent<Text>() != null) continue;
                rect.GetWorldCorners(corners);
                float area = (corners[2].x - corners[0].x) * (corners[2].y - corners[0].y);
                if (area >= screenArea * 0.98f) return at;
            }
            return null;
        }

        /// <summary>**字が実際に乗っている範囲**を返す。⚠️ 入れ物の枠ではない。
        ///
        /// ⭐ 中央揃えの題名は、幅800の枠に「BOX」の107しか描かれない。
        /// 枠どうしで比べると、右肩の数字と**必ず**重なっていると報告されてしまう
        /// （実測では字の間に余白が 2.78 あった）。
        /// ⚠️ 嘘の警告を出す道具は、そのうち誰にも読まれなくなるので、ここは実寸で測る。
        ///
        /// ⚠️ 押しどころ（絵だけ）は枠がそのまま当たり判定なので、枠で測る。</summary>
        private static Rect InkOf(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var full = Rect.MinMaxRect(corners[0].x + Slack, corners[0].y + Slack,
                corners[2].x - Slack, corners[2].y - Slack);

            var text = rect.GetComponent<Text>();
            if (text == null || rect.rect.width <= 0f || rect.rect.height <= 0f) return full;

            // 世界の長さ ÷ 部品の長さ＝1px あたりの倍率（親の拡大縮小をまとめて拾う）
            float sx = full.width / rect.rect.width;
            float sy = full.height / rect.rect.height;
            float inkW = Mathf.Min(text.preferredWidth, rect.rect.width) * sx;
            float inkH = Mathf.Min(text.preferredHeight, rect.rect.height) * sy;

            float left = full.xMin + Slide(text.alignment, full.width - inkW, horizontal: true);
            float top = full.yMax - Slide(text.alignment, full.height - inkH, horizontal: false);
            return Rect.MinMaxRect(left, top - inkH, left + inkW, top);
        }

        /// <summary>揃え方から、余ったぶんをどれだけ寄せるか。</summary>
        private static float Slide(TextAnchor anchor, float slackSpace, bool horizontal)
        {
            if (slackSpace <= 0f) return 0f;
            if (horizontal)
            {
                switch (anchor)
                {
                    case TextAnchor.UpperLeft:
                    case TextAnchor.MiddleLeft:
                    case TextAnchor.LowerLeft: return 0f;
                    case TextAnchor.UpperRight:
                    case TextAnchor.MiddleRight:
                    case TextAnchor.LowerRight: return slackSpace;
                    default: return slackSpace / 2f;
                }
            }
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.UpperCenter:
                case TextAnchor.UpperRight: return 0f;
                case TextAnchor.LowerLeft:
                case TextAnchor.LowerCenter:
                case TextAnchor.LowerRight: return slackSpace;
                default: return slackSpace / 2f;
            }
        }

        /// <summary>どこにある部品かを「親/子」で示す。⚠️ 名前だけだと探せない。</summary>
        private static string Path(Transform t) =>
            t.parent == null ? t.name : t.parent.name + "/" + t.name;
    }
}
