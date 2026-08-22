using System;
using System.Collections.Generic;
using System.Text;

namespace EggCommand.Core
{
    /// <summary>画面の骨組み1つ。⭐ **座標はここにしか無い。**
    ///
    /// ⚠️ コードに座標を書かせないための型です（2026-08-22・作者の指示
    /// 「コードでボタンを作るからいけないのでは？すべてアセットを使用することを
    /// 厳格に守れば」）。⭐ コードがするのは **bind に値を差すこと**と
    /// **button に手を繋ぐこと**だけ。
    ///
    /// ⚠️ **Core に置く理由**は検査です。座標がデータなら、重なりもはみ出しも
    /// **エンジンを起動せずに**数えられます（実測 2026-08-22: Unity の往復は
    /// 無変更でも19秒、コンパイル確認だけなら1.2秒）。</summary>
    public sealed class LayoutNode
    {
        /// <summary>部品の名前。⚠️ 同じ親の中で重ねない（<see cref="Layouts.Faults"/> が落とす）。</summary>
        public readonly string Name;
        /// <summary>何を出すか。⚠️ 知らない種類は読み込みで落とす（黙って素通りさせない）。</summary>
        public readonly string Kind;
        /// <summary>親の左上からのずれと大きさ。</summary>
        public readonly float Left, Top, Width, Height;
        /// <summary>`key=value` の付け足し。⚠️ 中身の解釈は描く側の仕事。</summary>
        public readonly IReadOnlyDictionary<string, string> Options;
        public readonly IReadOnlyList<LayoutNode> Children;

        public LayoutNode(string name, string kind, float left, float top, float width, float height,
            IReadOnlyDictionary<string, string> options, IReadOnlyList<LayoutNode> children)
        {
            Name = name;
            Kind = kind;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
            Options = options ?? new Dictionary<string, string>();
            Children = children ?? new List<LayoutNode>();
        }

        public string Option(string key)
        {
            string found;
            return Options.TryGetValue(key, out found) ? found : null;
        }

        public bool Flag(string key) => Option(key) != null;

        public int Number(string key, int fallback)
        {
            var text = Option(key);
            int value;
            return text != null && int.TryParse(text, out value) ? value : fallback;
        }

        public override string ToString() =>
            $"{Name}({Kind}) {Left},{Top} {Width}x{Height}";
    }

    /// <summary>1画面ぶんの骨組み。</summary>
    public sealed class Layout
    {
        public readonly string Id;
        public readonly IReadOnlyList<LayoutNode> Roots;

        public Layout(string id, IReadOnlyList<LayoutNode> roots)
        {
            Id = id;
            Roots = roots;
        }
    }

    /// <summary>骨組みの読み込みと検査。
    ///
    /// ⭐ **形式は行ベース。**⚠️ JSON にしません ── Unity と .NET で読む道具が違い、
    /// Core に依存を足すことになるためです。⭐ 行ベースなら
    /// <see cref="PixelSprite.Parse"/> と同じく `Split` だけで読めて、
    /// **差分が読める**（エディタが吐いたものを目で確かめられる）。
    ///
    /// 書き方:
    /// <code>
    /// head   label  48 16 984 44   size=28 ink=dim anchor=left text=手に入れた種族
    /// grid   scroll 48 76 984 1432 content=1280
    ///   cell card   0 0 317 304    repeat=species
    ///     art pixel 78 24 160 160  bind=art
    /// </code>
    /// ⚠️ 字下げが親子。⭐ **空白2つで1段**（タブは混ぜない ── 落とします）。</summary>
    public static class Layouts
    {
        /// <summary>画面の大きさ。⚠️ <c>Ui.W</c> / <c>Ui.H</c> と同じ数。
        /// ⭐ ここが検査の基準なので、View 側と食い違わせない。</summary>
        public const float ScreenWidth = 1080f;
        public const float ScreenHeight = 1920f;

        /// <summary>指で押せる最小の高さ。⚠️ View の <c>Ui.Tap</c> と同じ数。</summary>
        public const float TapHeight = 112f;

        /// <summary>知っている種類。⚠️ **ここに無いものは落とす。**
        /// ⭐ 黙って素通りさせると、綴り違いが「何も出ない」として通ります。</summary>
        public static readonly string[] Kinds =
        {
            "box",      // 何も描かない入れ物（まとめるだけ）
            "card",     // 面（明度差）で区切る札
            "label",    // 字
            "pixel",    // ドット絵
            "button",   // 押しどころ
            "scroll",   // 巻物（content= で中身の高さ）
            "round",    // 丸
            "veil",     // ⭐ 覆い（地を暗くし、後ろを押させない）
        };

        /// <summary>押しどころとして指が触れる種類。⚠️ 高さの検査はこれだけに掛ける。</summary>
        private static bool IsTappable(string kind) => kind == "button";

        /// <summary>知っている付け足し。⚠️ **ここに無い名前は落とす。**
        /// ⭐ `anchr=left` のような綴り違いが黙って無視されると、
        /// 「直したのに効かない」を延々と追うことになる。</summary>
        public static readonly string[] Options =
        {
            "size",     // 字の大きさ
            "ink",      // 字の色（名前で指す。色そのものは書かない）
            "anchor",   // 字の寄せ
            "bind",     // 値の差し込み口
            "tap",      // 押したときの手の名前
            "lead",     // 主導線の見た目にするか
            "repeat",   // 繰り返す元（データの名前）
            "cols",     // 繰り返しの列数
            "gap",      // 繰り返しの隙間
            "rows",     // 繰り返しの1段ぶんの高さ
            "max",      // 繰り返しの上限（⚠️ 巻物の外で繰り返すときは必須）
            "when",     // ⭐ 条件で出す／出さない（`when=有る` / `when=!有る`）
            "foe",      // ⭐ 左右反転して出す（敵はすべて反転・2026-08-21 の指示）
        };

        /// <summary>その部品を出す条件の名前。⚠️ null なら常に出す。
        ///
        /// ⭐ **式は書けない。名前だけ。**⚠️ 骨組みに `and` や比較を入れ始めると、
        /// そこが第二のプログラムになり、検査も編集も追えなくなる。
        /// 真偽を決めるのは呼ぶ側（`When(key)`）。</summary>
        public static string WhenOf(LayoutNode node)
        {
            var text = node.Option("when");
            if (text == null) return null;
            return text.Length > 0 && text[0] == '!' ? text.Substring(1) : text;
        }

        /// <summary>その条件が「偽のとき出す」ものか（`when=!有る`）。</summary>
        public static bool WhenNot(LayoutNode node)
        {
            var text = node.Option("when");
            return text != null && text.Length > 0 && text[0] == '!';
        }

        /// <summary>⭐ **2つが同時には出ないか。**`when=x` と `when=!x` は排他。
        ///
        /// ⚠️ これが無いと、条件で入れ替わる2つを**重なっている**と誤って落とす
        /// （どちらか片方しか出ないのに）。</summary>
        public static bool Exclusive(LayoutNode a, LayoutNode b)
        {
            var ka = WhenOf(a);
            var kb = WhenOf(b);
            if (ka == null || kb == null || ka != kb) return false;
            return WhenNot(a) != WhenNot(b);
        }

        /// <summary>繰り返しの1段ぶんの高さ。⭐ **ここが唯一の出所。**
        ///
        /// ⚠️ 2026-08-22 の初版は、置く側が `高さ+隙間`・数える側が `高さ` と
        /// **別々に決めていた**ので、`rows=` を書かない画面で
        /// **巻物の中身が1段あたり隙間のぶん足りなくなる**穴があった。</summary>
        public static float StepOf(LayoutNode node) =>
            node.Number("rows", (int)(node.Height + node.Number("gap", 0)));

        /// <summary>字を出す種類。⚠️ 重なりの検査はこれだけに掛ける
        /// （札の上に字が乗るのは当たり前なので、面どうしは見ない）。</summary>
        private static bool IsText(string kind) => kind == "label";

        // ── 読み込み ────────────────────────────────────

        public static Layout Parse(string id, string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            var roots = new List<LayoutNode>();
            var pending = new List<object[]>();   // [depth, name, kind, l, t, w, h, options]

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                if (raw.IndexOf('\t') >= 0)
                    throw new ArgumentException($"{id}: {i + 1}行目にタブがある（空白2つで1段）");
                string body = raw.Trim();
                if (body.Length == 0 || body[0] == '#') continue;

                int spaces = 0;
                while (spaces < raw.Length && raw[spaces] == ' ') spaces++;
                if (spaces % 2 != 0)
                    throw new ArgumentException($"{id}: {i + 1}行目の字下げが奇数（空白2つで1段）");
                int depth = spaces / 2;

                var parts = body.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 6)
                    throw new ArgumentException(
                        $"{id}: {i + 1}行目「{body}」── 名前 種類 左 上 幅 高 が要る");

                // ⚠️ **名前に `#` を使わせない。**⭐ 繰り返しの複製が `名前#0` を作るので、
                //    元の名前に `#` があると「読む→書く→読む」が同じ木に戻らない
                //    ── エディタは往復が閉じている形式の上にしか載らない。
                if (parts[0].IndexOf('#') >= 0)
                    throw new ArgumentException($"{id}: {i + 1}行目 名前に # は使えない（繰り返しが使う）");

                var options = new Dictionary<string, string>();
                for (int p = 6; p < parts.Length; p++)
                {
                    int eq = parts[p].IndexOf('=');
                    if (eq <= 0)
                        throw new ArgumentException($"{id}: {i + 1}行目「{parts[p]}」は key=value でない");
                    // ⚠️ **後勝ちで黙って通さない。**⭐ 名前の重複は落とすのに
                    //    付け足しの重複を見逃すと、直したつもりの値が効かない
                    string key = parts[p].Substring(0, eq);
                    if (options.ContainsKey(key))
                        throw new ArgumentException($"{id}: {i + 1}行目「{key}=」が2つある");
                    options[key] = parts[p].Substring(eq + 1);
                }

                pending.Add(new object[]
                {
                    depth, parts[0], parts[1],
                    Num(id, i, parts[2]), Num(id, i, parts[3]),
                    Num(id, i, parts[4]), Num(id, i, parts[5]),
                    options,
                });
            }

            // ⭐ 後ろから組む（子が先に要る）。⚠️ 前から作ると子を後付けすることになり、
            //    LayoutNode を書き換え可能にせざるを得なくなる。
            var built = new LayoutNode[pending.Count];
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                int depth = (int)pending[i][0];
                var kids = new List<LayoutNode>();
                for (int j = i + 1; j < pending.Count; j++)
                {
                    int at = (int)pending[j][0];
                    if (at <= depth) break;
                    if (at == depth + 1) kids.Add(built[j]);
                }
                built[i] = new LayoutNode(
                    (string)pending[i][1], (string)pending[i][2],
                    (float)pending[i][3], (float)pending[i][4],
                    (float)pending[i][5], (float)pending[i][6],
                    (Dictionary<string, string>)pending[i][7], kids);
            }
            for (int i = 0; i < pending.Count; i++)
                if ((int)pending[i][0] == 0) roots.Add(built[i]);

            return new Layout(id, roots);
        }

        private static float Num(string id, int line, string text)
        {
            float value;
            if (!float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out value))
                throw new ArgumentException($"{id}: {line + 1}行目「{text}」が数でない");
            return value;
        }

        // ── 検査（⭐ エンジン不要）────────────────────────

        /// <summary>骨組みの不備を数える。⭐ **`InspectScreens` の静的版。**
        ///
        /// ⚠️ 実物の字幅までは見られません（それは描いてからでないと分からない）。
        /// ⭐ ただし**枠どうしの関係**── 重なり・はみ出し・画面の外・押しどころの大きさ
        /// ── はここで全部落ちます。</summary>
        public static List<string> Faults(Layout layout)
        {
            var problems = new List<string>();
            if (layout == null) { problems.Add("骨組みが無い"); return problems; }

            // ⚠️ **根っこ同士も兄弟。**⭐ ここを見ていなくて、わざと重ねた2つの字が
            //    素通りした（2026-08-22・道具を壊して確かめたときに発覚）。
            Siblings(layout.Id, layout.Id, layout.Roots, problems);
            foreach (var root in layout.Roots)
                Walk(layout.Id, root, 0f, 0f, ScreenWidth, ScreenHeight, false, false, problems);

            return problems;
        }

        /// <param name="parentScrolls">**直近の親**が巻物か。⭐ 「親の枠から縦へはみ出し」を
        /// 見逃す唯一の場合。</param>
        /// <param name="insideScroll">**祖先のどこか**に巻物があるか。⭐ 「画面の外」を
        /// 見逃す唯一の場合。
        ///
        /// ⚠️ **この2つを1本の旗で兼ねてはいけない**（2026-08-22 の初版はそうしていた）。
        /// 巻物 → 箱 → 字 の3段になると、箱は巻物ではないので孫の旗が下りてしまい、
        /// **巻物の中なのに「画面の外」と嘘をつく**（実測: `t/a: 画面の外（0,1950 400x40）`）。</param>
        private static void Walk(string id, LayoutNode node,
            float parentX, float parentY, float parentW, float parentH,
            bool parentScrolls, bool insideScroll, List<string> problems)
        {
            bool known = false;
            for (int i = 0; i < Kinds.Length; i++) if (Kinds[i] == node.Kind) { known = true; break; }
            if (!known) problems.Add($"{id}/{node.Name}: 知らない種類「{node.Kind}」");

            float x = parentX + node.Left;
            float y = parentY + node.Top;

            if (node.Width <= 0f || node.Height <= 0f)
                problems.Add($"{id}/{node.Name}: 大きさが 0 以下（{node.Width}x{node.Height}）");

            // ⚠️ 条件の名前が空だと、何で出し分けるのか誰にも分からない
            if (node.Option("when") != null && string.IsNullOrEmpty(WhenOf(node)))
                problems.Add($"{id}/{node.Name}: when= の名前が空");

            // ⚠️ 知らない付け足しを黙って無視しない（#5）
            foreach (var pair in node.Options)
            {
                bool listed = false;
                for (int i = 0; i < Options.Length; i++) if (Options[i] == pair.Key) { listed = true; break; }
                if (!listed) problems.Add($"{id}/{node.Name}: 知らない付け足し「{pair.Key}=」");
            }

            // ⚠️ **覆いは画面いっぱいでなければ意味がない。**
            //    ⭐ 隙間があると、そこから後ろが押せる（覆いの目的が消える）。
            if (node.Kind == "veil"
                && (Math.Abs(node.Width - ScreenWidth) > 0.5f
                    || Math.Abs(node.Height - ScreenHeight) > 0.5f
                    || Math.Abs(node.Left) > 0.5f || Math.Abs(node.Top) > 0.5f))
            {
                problems.Add($"{id}/{node.Name}: 覆いが画面いっぱいでない"
                    + $"（{node.Left},{node.Top} {node.Width}x{node.Height}）── 隙間から後ろが押せる");
            }

            // ⚠️ **検査する枠と、実際に描かれる枠を食い違わせない**（#3）。
            //    ⭐ 絵と丸は短いほうの辺で正方形に描かれるので、
            //    「幅984・高40」と書くと 40x40 が描かれるのに検査は 984x40 を見てしまう。
            if ((node.Kind == "pixel" || node.Kind == "round")
                && Math.Abs(node.Width - node.Height) > 0.5f)
            {
                problems.Add($"{id}/{node.Name}: {node.Kind} は正方形で描かれる"
                    + $"（{node.Width}x{node.Height} と書いても {Math.Min(node.Width, node.Height)} 角になる）");
            }

            // ⚠️ 親の外へ出ていないか。
            // ⭐ **巻物の中は縦を見ない** ── 縦に溢れることが巻物の役目。
            //    ⚠️ 逆に**横は必ず見る**。横に溢れたものは指が届かない
            //    （巻物は縦にしか動かない）。
            if (parentW > 0f)
            {
                if (node.Left < -0.5f || node.Left + node.Width > parentW + 0.5f)
                {
                    problems.Add($"{id}/{node.Name}: 親の枠から横へはみ出し"
                        + $"（子 左{node.Left} 幅{node.Width} / 親 幅{parentW}）");
                }
            }
            if (parentH > 0f && !parentScrolls)
            {
                if (node.Top < -0.5f || node.Top + node.Height > parentH + 0.5f)
                {
                    problems.Add($"{id}/{node.Name}: 親の枠から縦へはみ出し"
                        + $"（子 上{node.Top} 高{node.Height} / 親 高{parentH}）");
                }
            }

            // ⚠️ 巻物の中は画面より下に在ってよい（動かせば見える）
            bool offScreen = x < -0.5f || x + node.Width > ScreenWidth + 0.5f;
            // ⚠️ ここは `insideScroll`。⭐ 巻物の中なら、何段目でも下に在ってよい
            if (!insideScroll && (y < -0.5f || y + node.Height > ScreenHeight + 0.5f)) offScreen = true;
            if (offScreen)
            {
                problems.Add($"{id}/{node.Name}: 画面の外（{x},{y} {node.Width}x{node.Height}）");
            }

            if (IsTappable(node.Kind) && node.Height < TapHeight - 0.5f)
            {
                problems.Add($"{id}/{node.Name}: 押しどころの高さが {node.Height}"
                    + $"。{TapHeight} 以上にする（指で押せない）");
            }

            Siblings(id, node.Name, node.Children, problems);

            // ⭐ **並びの検査。**`repeat=` を持つ札は、`cols=` 枚が親の幅に収まるか。
            // ⚠️ ここを見ないと「3列で置いたら右端が切れる」が実機まで分からない。
            if (node.Option("repeat") != null)
            {
                int cols = node.Number("cols", 1);
                float gap = node.Number("gap", 0);
                float need = cols * node.Width + (cols - 1) * gap;
                if (cols < 1)
                    problems.Add($"{id}/{node.Name}: cols= が {cols}（1以上）");
                else if (parentW > 0f && need > parentW + 0.5f)
                    problems.Add($"{id}/{node.Name}: {cols}列が親の幅に収まらない"
                        + $"（要る {need} / 親 {parentW}）");

                // ⚠️ **巻物の外で繰り返すなら、何段までかを宣言させる**（#7）。
                //    ⭐ 繰り返しの数はデータ次第なので、検査は「何個来るか」を知らない。
                //    宣言が無ければ、増えた日に黙って親からはみ出す。
                if (!parentScrolls)
                {
                    int max = node.Number("max", 0);
                    if (max <= 0)
                    {
                        problems.Add($"{id}/{node.Name}: 巻物の外の繰り返しには max=（上限の個数）が要る");
                    }
                    else if (parentH > 0f)
                    {
                        int rows = (max + cols - 1) / cols;
                        float deep = node.Top + rows * StepOf(node) - gap;
                        if (deep > parentH + 0.5f)
                            problems.Add($"{id}/{node.Name}: max={max} だと親の枠から縦へはみ出す"
                                + $"（要る {deep} / 親 高{parentH}）");
                    }
                }
            }

            bool scrolls = node.Kind == "scroll";
            foreach (var child in node.Children)
                Walk(id, child, x, y, node.Width, node.Height,
                    scrolls, insideScroll || scrolls, problems);
        }

        /// <summary>同じ親を持つ部品どうしの見張り。
        /// ⚠️ **根っこの一覧にも掛ける** ── 掛け忘れると、画面の一番外側だけ
        /// 検査が素通りする（2026-08-22 に実際そうなっていた）。</summary>
        private static void Siblings(string id, string owner,
            IReadOnlyList<LayoutNode> list, List<string> problems)
        {
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    if (list[i].Name == list[j].Name)
                        problems.Add($"{id}/{owner}: 「{list[i].Name}」が2つある");

                    if (!Overlaps(list[i], list[j])) continue;
                    // ⭐ 条件で入れ替わる2つは、同時には出ない
                    if (Exclusive(list[i], list[j])) continue;

                    // ⭐ 字どうし。⚠️ 面（card）と字が重なるのは当たり前なので見ない
                    if (IsText(list[i].Kind) && IsText(list[j].Kind))
                    {
                        problems.Add($"{id}/{owner}: 字の重なり"
                            + $"「{list[i].Name}」×「{list[j].Name}」");
                        continue;
                    }

                    // ⭐ **押しどころどうし**（#4）。⚠️ 重なると片方に指が届かない。
                    //    2026-08-22 の初版は字しか見ておらず、釦が2枚重なっても素通りした。
                    if (Tappable(list[i]) && Tappable(list[j]))
                    {
                        problems.Add($"{id}/{owner}: 押しどころの重なり"
                            + $"「{list[i].Name}」×「{list[j].Name}」── 片方に指が届かない");
                    }
                }
            }
        }

        /// <summary>指が触れる部品か。⭐ `button` と、`tap=` を持つ札。</summary>
        private static bool Tappable(LayoutNode node) =>
            IsTappable(node.Kind) || node.Option("tap") != null;

        private static bool Overlaps(LayoutNode a, LayoutNode b) =>
            !(a.Left + a.Width <= b.Left + 0.5f || b.Left + b.Width <= a.Left + 0.5f
              || a.Top + a.Height <= b.Top + 0.5f || b.Top + b.Height <= a.Top + 0.5f);

        /// <summary>不備があれば投げる。⚠️ 起動時に1度呼んで、**黙って壊れた画面を出さない**。</summary>
        public static void Audit(Layout layout)
        {
            var problems = Faults(layout);
            if (problems.Count == 0) return;
            var report = new StringBuilder("骨組みに不備:\n");
            foreach (var line in problems) report.Append("  ").Append(line).Append('\n');
            throw new InvalidOperationException(report.ToString());
        }
    }
}
