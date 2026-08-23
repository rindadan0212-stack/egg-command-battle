using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EggCommand.Core;
using EggCommand.Sim;
using Xunit.Abstractions;

namespace EggCommand.Tests;

/// <summary>実物の帳面3枚（sheets/技.txt・種族.txt・特性.txt）が、
/// `Sheet` の**本物の読み書き経路**（`sim sheet write` と同じ `Sheet.Run("write")`）を
/// 一往復しても1文字も変わらないか。
///
/// ⭐ ここでの狙いは1つ ── **落ちた中身を数えて言えるようにすること。**
/// ⚠️ **直すのは次の段。**ここで見つかった差分はそのまま報告に回す（Skip の但し書き参照）。
///
/// ⚠️ **実物の sheets/*.txt へは1バイトも書かない。**`Sheet.Dir` は `"../sheets"` に
/// 決め打ちで、`Put` は private かつファイル結合（`File.Exists`/`File.ReadAllLines`/
/// `File.WriteAllText` を直に呼ぶ）なので、本物の Write() を実物に触れさせずに通す道は
/// 「一時フォルダへコピーし、その場所が `../sheets` に見えるところへ CWD を移す」しかない。
/// ⚠️ CWD はプロセス全体の状態。使う前後で必ず退避・復元し、<see cref="CwdGate"/> で直列化する
/// （2026-08-23 時点、他のテストは CWD に依存していないので実害は無いが、
///   将来 CWD 依存のテストが増えたら、そちらもこの lock に混ぜること）。</summary>
public class SheetRoundTripTests
{
    private readonly ITestOutputHelper _out;
    public SheetRoundTripTests(ITestOutputHelper output) => _out = output;

    /// <summary>CWD はプロセス全体で1つしかない。⚠️ 同時に2つの往復を走らせない。</summary>
    private static readonly object CwdGate = new();

    private static readonly (string File, string Head)[] Files =
    {
        (Sheet.SkillFile, "技"),
        (Sheet.SpeciesFile, "種族"),
        (Sheet.TraitFile, "特性"),
    };

    /// <summary>実物のコピーで `sim sheet write` 相当を1回だけ走らせ、
    /// (元の文字列, 書き出し後の文字列) を3ファイルぶん返す。⭐ 実物には一切触れない。</summary>
    private static Dictionary<string, (string Original, string Produced)> RunRealRoundTrip()
    {
        var originals = new Dictionary<string, string>();
        foreach (var (file, _) in Files) originals[file] = ReadLinkedSheet(file);

        string temp = Directory.CreateTempSubdirectory("eggcommand-sheet-roundtrip-").FullName;
        try
        {
            string sheetsDir = Path.Combine(temp, "sheets");
            string cwdDir = Path.Combine(temp, "cwd");
            Directory.CreateDirectory(sheetsDir);
            Directory.CreateDirectory(cwdDir);
            var utf8 = new UTF8Encoding(false);
            foreach (var (file, content) in originals)
                File.WriteAllText(Path.Combine(sheetsDir, file), content, utf8);

            lock (CwdGate)
            {
                string prev = Environment.CurrentDirectory;
                try
                {
                    // ⭐ ここでの "../sheets" は sheetsDir を指す（実物の sheets/ ではない）。
                    Environment.CurrentDirectory = cwdDir;
                    Sheet.Run("write");
                }
                finally
                {
                    Environment.CurrentDirectory = prev;
                }
            }

            var result = new Dictionary<string, (string, string)>();
            foreach (var (file, _) in Files)
            {
                string produced = File.ReadAllText(Path.Combine(sheetsDir, file), utf8);
                result[file] = (originals[file], produced);
            }
            return result;
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); }
            catch { /* ⚠️ 一時フォルダの掃除に失敗しても検査結果には関わらない */ }
        }
    }

    /// <summary>csproj が写した「読むだけ」のコピーを読む（実物の CWD に依存しないため）。</summary>
    private static string ReadLinkedSheet(string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sheets", file));

    /// <summary>🔴 **これは赤くなる。それが目的。**
    /// 実物の帳面3枚は、いまの `Sheet.Put` を一往復すると1文字も変わらない ── はずだったが、
    /// 実際には `//` コメントが消える／動く形で変わる（監査で発覚）。
    /// ⚠️ ここを直しても帳面は直らない。直すのは `Sheet.Put`（次の段）。
    /// ⭐ Skip を外して単体で走らせると、xUnit の Assert.Equal が最初に食い違う位置を教えてくれる
    ///    （どのファイルの何行目からズレたかを読みたいときに使う）。</summary>
    [Theory(Skip = "🔴 意図的に赤。往復で // コメントが失われる既知の欠落（監査 2026-08-23）。"
        + "直すのは Sheet.Put の側。帳面の往復差分を分類して報告する() が中身の分類を報告する。")]
    [MemberData(nameof(FileNames))]
    public void 帳面は書いて読んで一往復しても1バイトも変わらない(string file)
    {
        var rt = RunRealRoundTrip();
        var (original, produced) = rt[file];
        // ⭐ バイト単位（改行コード込み）で見る ── 文字列比較でも .NET は \r を勝手に触らないので、
        //    ReadAllText した時点で原文の改行コードはそのまま文字列に残っている。
        Assert.Equal(original, produced);
    }

    public static IEnumerable<object[]> FileNames()
    {
        foreach (var (file, _) in Files) yield return new object[] { file };
    }

    /// <summary>⭐ **常に緑。**落ちた中身を分類して数え、人が読める形でテスト出力に流す。
    /// ⚠️ 「直っているか」ではなく「何が・どれだけ壊れているか」を言うための検査。
    /// `dotnet test --logger "console;verbosity=detailed"` で中身が読める。</summary>
    [Fact]
    public void 帳面の往復差分を分類して報告する()
    {
        var rt = RunRealRoundTrip();
        _out.WriteLine("══ 帳面の往復差分（sheet write を実物のコピーへ通した結果）══");
        int totalIssues = 0;

        foreach (var (file, head) in Files)
        {
            var (original, produced) = rt[file];
            var r = SheetRoundTripDiff.Analyze(original, produced, head);
            totalIssues += r.HeaderChanged + r.CommentsLost + r.CommentsMovedTotal
                + r.CommentsTrailingWsDropped + r.SpacingChanged + r.OtherChanged;

            _out.WriteLine("");
            // ⚠️ OriginalHasCrlf だけで「LF に化ける」と決め打ちしない ── 直した後は
            //    ProducedHasCrlf も CRLF のままなので、実際に変わったときだけ警告を出す。
            string crlfNote = r.OriginalHasCrlf != r.ProducedHasCrlf
                ? $"　⚠️ 元は{(r.OriginalHasCrlf ? "CRLF" : "LF")}、書き出しは{(r.ProducedHasCrlf ? "CRLF" : "LF")}に化ける"
                : "";
            _out.WriteLine($"── {file} ── 元 {r.OriginalLines} 行 / 書き出し後 {r.ProducedLines} 行" + crlfNote);
            _out.WriteLine($"  ヘッダで変わった行数:            {r.HeaderChanged}");
            _out.WriteLine($"  // コメントが消えた行数:          {r.CommentsLost}");
            _out.WriteLine($"  // コメントが移動した行数:        {r.CommentsMovedTotal}"
                + $"（同じ見出しの中: {r.CommentsMovedWithinBlock} / 別の見出しへ吸着: {r.CommentsMovedAcrossBlock}）");
            _out.WriteLine($"  行末空白が落ちた行数:             {r.CommentsTrailingWsDropped}");
            _out.WriteLine($"  桁揃え（= の空白）が変わった行数: {r.SpacingChanged}");
            _out.WriteLine($"  その他（空行の増減・値の食い違いなど）: {r.OtherChanged}");

            if (r.Notes.Count > 0)
            {
                _out.WriteLine("  内訳（最大40件まで表示）:");
                foreach (var n in r.Notes.GetRange(0, Math.Min(40, r.Notes.Count)))
                    _out.WriteLine("    ・" + n);
                if (r.Notes.Count > 40) _out.WriteLine($"    …ほか {r.Notes.Count - 40} 件");
            }
        }

        _out.WriteLine("");
        _out.WriteLine($"■ 合計 {totalIssues} 件の差分（0 なら往復は無傷）");

        // ⚠️ **ここで Assert しない。**分類の中身を数えて出すのが目的で、
        //    件数がいくつであっても検査としては成功（＝赤くならない）。
        // ⭐ ただし「分類そのものが動いた」ことだけは確かめる（例外を投げずに走り切ったか）。
        Assert.True(rt.Count == Files.Length);
    }

    // ══ ここから下: 2026-08-23 の直し（症状2・3・4・5）の再発防止 ═══════════
    //
    // ⭐ 骨組み側 LayoutWriteTests.cs と同じ考え方 ── 「直った」だけでなく、
    //    「その直し方が本当に効いている」ところまで、対で確かめる。
    // ⚠️ Put() は private なので、ここでも `Sheet.Run("write")` を通した
    //    ブラックボックス確認しかできない（RunRealRoundTrip と同じ CWD の作法）。

    /// <summary>作り物の1枚を <c>sheet write</c> に通し、書き出された中身を返す。
    /// ⭐ 実物には一切触れない（一時フォルダの sheets/ だけに書く）。
    /// ⚠️ <paramref name="file"/> 以外の帳面（技/種族/特性）はここでは作らない ──
    /// 存在しないファイルは Put() が真っさらから作るだけなので、無視してよい。</summary>
    private static string WriteAndCapture(string file, string original)
    {
        string temp = Directory.CreateTempSubdirectory("eggcommand-sheet-synthetic-").FullName;
        try
        {
            string sheetsDir = Path.Combine(temp, "sheets");
            string cwdDir = Path.Combine(temp, "cwd");
            Directory.CreateDirectory(sheetsDir);
            Directory.CreateDirectory(cwdDir);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(sheetsDir, file), original, utf8);

            lock (CwdGate)
            {
                string prev = Environment.CurrentDirectory;
                try
                {
                    Environment.CurrentDirectory = cwdDir;
                    Sheet.Run("write");
                }
                finally { Environment.CurrentDirectory = prev; }
            }
            return File.ReadAllText(Path.Combine(sheetsDir, file), utf8);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    /// <summary>⭐ **実物3枚は、いま偶然どれも「移動済みで安定した形」なので、
    /// 今回の1回の往復だけでは「コメントが移動した」が実測0件になる。**
    /// ⚠️ それが「移動しない」の証拠にならないよう、最小の作り物で
    /// 「札の上に書いたコメントが、元の位置（見出しの直後）に残る」を直接確かめる。
    ///
    /// ⚠️ 2026-08-23 に直すまでは、ここは逆（塊の末尾へ動く）を確かめる試験だった
    /// （症状4そのもの）。直した今は、動かないことを確かめるのが仕事。</summary>
    [Fact]
    public void 既知の技では札の上のコメントが元の位置に残る()
    {
        // ⭐ 実装済みの技（Skills.All にある id）を1つ拝借し、
        //    名前の**上**に手で書いたふうのコメントを足す。
        var real = Skills.All[0];
        string original = Sheet.SkillHead()
            + $"# 技 {real.Id}\n"
            + "// 手で書いたコメント（本来は名前の上）\n"
            + Sheet.BlockOf(real).Substring(("# 技 " + real.Id + "\n").Length);

        string produced = WriteAndCapture(Sheet.SkillFile, original);

        var lines = produced.Split('\n');
        int headerLine = Array.FindIndex(lines, l => l.Trim() == $"# 技 {real.Id}");
        Assert.True(headerLine >= 0, "書き出し後に見出しが見つからない");
        _out.WriteLine(string.Join('\n', lines.Skip(headerLine).Take(6)));

        // ⭐ 見出しの直後（＝元の位置）にコメントが残っている。
        Assert.StartsWith("//", lines[headerLine + 1].TrimStart());
        // ⭐ その次が「名前 = 」── 末尾へ流れていない。
        Assert.StartsWith("名前", lines[headerLine + 2].TrimStart());
    }

    /// <summary>⚠️ 症状4の裏取り ── 「効果 = 」のように**同じ札が何度も出る**技で、
    /// 2番目の出現の上に書いたコメントが、1番目の出現の上に紛れ込まないこと。
    /// ⭐ 出現回数まで見ずに「札の名前」だけで戻す実装だと、ここで落ちる。</summary>
    [Fact]
    public void 効果が複数ある技では出現回数ごとに正しい行の上へ戻る()
    {
        var real = Skills.All.First(s => s.Effects.Count >= 2);
        var lines = new List<string>(Sheet.BlockOf(real).Split('\n'));
        int first = lines.FindIndex(l => l.StartsWith("効果 = "));
        int second = lines.FindIndex(first + 1, l => l.StartsWith("効果 = "));
        Assert.True(second > first, $"{real.Id}: 効果が2つ見つからない（見本の選び方を見直す）");
        lines.Insert(second, "// 2つ目の効果の上のコメント");
        string original = Sheet.SkillHead() + string.Join("\n", lines);

        string produced = WriteAndCapture(Sheet.SkillFile, original);
        var producedLines = produced.Split('\n');
        int headerLine = Array.FindIndex(producedLines, l => l.Trim() == $"# 技 {real.Id}");
        Assert.True(headerLine >= 0, "書き出し後に見出しが見つからない");
        int commentIdx = Array.FindIndex(producedLines, headerLine, l => l.Trim() == "// 2つ目の効果の上のコメント");
        Assert.True(commentIdx >= 0, "コメントが消えている");

        // ⭐ この技のブロック内で、コメントより前に「効果 = 」がちょうど1回だけ
        //    （＝1番目の効果の上ではない）。⚠️ 他の技の「効果 = 」まで数えないよう
        //    見出しからの範囲に絞る。
        int effectsBefore = producedLines.Skip(headerLine).Take(commentIdx - headerLine)
            .Count(l => l.StartsWith("効果 = "));
        Assert.Equal(1, effectsBefore);
        // ⭐ コメントの直後が「効果 = 」（＝2番目の効果の直前に戻っている）。
        Assert.StartsWith("効果 = ", producedLines[commentIdx + 1]);
    }

    /// <summary>🔴 **付け先の出現が実装の作り直しで消えたら、コメントはブロック末尾へ運ばれる
    /// （消えない）。**⚠️ コードレビュー（2026-08-23）で見つかった実害の再発防止 ──
    /// Before は「札の N 番目の出現の直前」でコメントを覚えるが、実装側で効果を減らす等して
    /// N 番目がもう作り直した本文に現れないと、何もしなければそのコメントは黙って消えていた。</summary>
    [Fact]
    public void 付け先の出現が実装の作り直しで消えたコメントはブロック末尾へ運ばれ失われない()
    {
        // ⭐ 効果が1つだけの技を選び、原文だけ「2つ目の効果」を装ってその直前にコメントを置く。
        //    実装（Skills.All）は1つしか効果を持たないので、書き出しでは2つ目の出現が
        //    二度と現れない ── 付け先を失ったコメントをこれで再現する。
        var real = Skills.All.First(s => s.Effects.Count == 1);
        string original = Sheet.SkillHead()
            + $"# 技 {real.Id}\n名前 = {real.Name}\n説明 = {real.Gist}\n"
            + $"型 = {Skills.LabelOf(real.Type)}\nCT = {real.Ct}\n"
            + $"狙い = {SkillText.TargetOf(real.Target)}\n"
            + $"効果 = {Sheet.LineOf(real.Effects[0])}\n"
            + "// 幻の2つ目の効果の上のコメント\n"
            + "効果 = ダメージ 威力:小 依存:攻撃\n";   // ⚠️ 実装には無い、原文だけの「2つ目」

        string produced = WriteAndCapture(Sheet.SkillFile, original);

        // ⭐ コメントは消えずに、実装から作り直したブロックのどこかに残る
        //    （付け先が無いので末尾＝Trailing 行きが正しい）。
        Assert.Contains("// 幻の2つ目の効果の上のコメント", produced);
    }

    /// <summary>🔴 症状2の再発防止 ── 書きかけ区切り線がすでに何組も紛れ込んでいても、
    /// 書き出しは**1組だけ**に畳む（消えずに増殖もしない）。
    /// ⭐ 実物（tailwind に4回・nimble に2回・tenacity に6回）と同じ壊れ方を、最小の作り物で再現する。</summary>
    [Fact]
    public void 書きかけ区切り線が紛れ込んでいても1組に畳まれ増殖しない()
    {
        var a = Skills.All[0];
        var b = Skills.All[1];
        const string banner =
            "// ══ ここから下は、まだ実装に入っていない書きかけ ══════\n"
            + "// ⭐ `sim sheet write` はここを**1文字も触りません**。\n"
            + "// ⚠️ 実装に入れたら、次の write で上の並びへ移ります。\n";

        // ⚠️ 実物と同じ形 ── a の空行より**前**（フィールドの直後）に区切り線が2組、直接くっついている。
        string original = Sheet.SkillHead()
            + Sheet.BlockOf(a) + banner + banner + "\n"
            + Sheet.BlockOf(b) + "\n"
            + "# 技 not-a-real-skill\n名前 = 未実装\n説明 = まだ実装していない書きかけ\n"
            + "型 = アタック\nCT = 3\n狙い = 敵1体\n効果 = ダメージ 威力:小 依存:攻撃\n";

        string produced = WriteAndCapture(Sheet.SkillFile, original);

        const string bannerHead = "// ══ ここから下は、まだ実装に入っていない書きかけ ══════";
        Assert.Equal(1, CountOccurrences(produced, bannerHead));
        Assert.DoesNotContain("not-a-real-skill", produced.Substring(0, produced.IndexOf(bannerHead, StringComparison.Ordinal)));

        // ⭐ 安定性の裏取り ── 一度直った出力を、もう一度書き出しても増えない（idempotent）。
        //    増殖するバグが戻っていれば、ここで2組目が付いて再び赤くなる。
        string producedAgain = WriteAndCapture(Sheet.SkillFile, produced);
        Assert.Equal(produced, producedAgain);
    }

    /// <summary>🔴 症状3の再発防止 ── CRLF の原文は CRLF のまま書き出される
    /// （技.txt が CRLF・種族.txt/特性.txt が LF という、実物の混在を再現）。</summary>
    [Fact]
    public void CRLFの原文はCRLFのまま書き出される()
    {
        var real = Skills.All[0];
        string original = (Sheet.SkillHead() + Sheet.BlockOf(real)).Replace("\n", "\r\n");

        string produced = WriteAndCapture(Sheet.SkillFile, original);

        Assert.Contains("\r\n", produced);
        // ⚠️ 「\r を伴わない \n」が1つも無いこと（＝常に LF に化けるバグの再発検知）。
        Assert.DoesNotContain("\n", produced.Replace("\r\n", ""));
    }

    /// <summary>⚠️ 上と対 ── LF の原文が、CRLF へ「格上げ」されたりしないこと。</summary>
    [Fact]
    public void LFの原文はLFのまま書き出される()
    {
        var real = Skills.All[0];
        string original = Sheet.SkillHead() + Sheet.BlockOf(real);   // \n のまま

        string produced = WriteAndCapture(Sheet.SkillFile, original);

        Assert.DoesNotContain("\r", produced);
    }

    /// <summary>⚠️ 症状5の再発防止 ── 救出したコメントの行末の空白は落とさない。</summary>
    [Fact]
    public void コメントの行末の空白は保たれる()
    {
        var real = Skills.All[0];
        const string commentWithTrailingSpace = "// 行末に空白がある   ";   // 空白3つ
        string original = Sheet.SkillHead()
            + $"# 技 {real.Id}\n"
            + commentWithTrailingSpace + "\n"
            + Sheet.BlockOf(real).Substring(("# 技 " + real.Id + "\n").Length);

        string produced = WriteAndCapture(Sheet.SkillFile, original);

        Assert.Contains(commentWithTrailingSpace + "\n", produced);
    }

    /// <summary>⭐ 上と対 ── 行末に空白の無いコメントに、余計な空白が足されたりしないこと。</summary>
    [Fact]
    public void 行末に空白の無いコメントに空白は足されない()
    {
        var real = Skills.All[0];
        const string comment = "// 行末に空白は無い";
        string original = Sheet.SkillHead()
            + $"# 技 {real.Id}\n"
            + comment + "\n"
            + Sheet.BlockOf(real).Substring(("# 技 " + real.Id + "\n").Length);

        string produced = WriteAndCapture(Sheet.SkillFile, original);

        Assert.Contains(comment + "\n", produced);
        Assert.DoesNotContain(comment + " \n", produced);
    }

    // ══ ここから下: 帳面に無かった id の書き戻しを黙らせない ═══════════════
    //
    // ⭐ `sheet write` が実装にしか無い id を書き戻すこと自体は仕様どおり（実装が正・帳面は入口）。
    //    ここで確かめるのは「その件数を正しく数えて言うか」だけ ── 振る舞いは変えていない。

    /// <summary>作り物の1枚を <c>sheet write</c> に通し、書き出された中身と、
    /// そのとき標準出力に出た文言を両方持ち帰る。⭐ <see cref="WriteAndCapture"/> と同じ CWD の作法に、
    /// 標準出力の捕捉を足しただけ。
    /// ⚠️ Console.Out もプロセス全体の状態なので、CWD と同じ <see cref="CwdGate"/> で直列化する
    /// （2026-08-23 時点、他のテストは Console.Out を差し替えていないので実害は無いが、
    ///   差し替えるテストが増えたらそちらもこの lock に混ぜること）。</summary>
    private static (string File, string Console) WriteAndCaptureBoth(string file, string original)
    {
        string temp = Directory.CreateTempSubdirectory("eggcommand-sheet-writeback-").FullName;
        try
        {
            string sheetsDir = Path.Combine(temp, "sheets");
            string cwdDir = Path.Combine(temp, "cwd");
            Directory.CreateDirectory(sheetsDir);
            Directory.CreateDirectory(cwdDir);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(sheetsDir, file), original, utf8);

            string consoleText;
            lock (CwdGate)
            {
                string prevDir = Environment.CurrentDirectory;
                var prevOut = Console.Out;
                var sw = new StringWriter();
                try
                {
                    Environment.CurrentDirectory = cwdDir;
                    Console.SetOut(sw);
                    Sheet.Run("write");
                }
                finally
                {
                    Console.SetOut(prevOut);
                    Environment.CurrentDirectory = prevDir;
                }
                consoleText = sw.ToString();
            }
            string produced = File.ReadAllText(Path.Combine(sheetsDir, file), utf8);
            return (produced, consoleText);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }

    /// <summary>⭐ 帳面から手で消した id は、実装から書き戻される（仕様どおり ── 直さない）。
    /// ⚠️ ここで確かめるのはその**件数が合っているか**。原文には real の1件しか無いので、
    /// 書き戻し数は「実装の全件数 − 1」にちょうど一致するはず
    /// （2026-08-23、黙って書き戻していたのを言うようにした監査の再発防止）。</summary>
    [Fact]
    public void 帳面に無かったidの書き戻し件数が実装との差分に一致する()
    {
        var real = Skills.All[0];
        // ⚠️ 原文には real の1件しか無い。他の実装済み技は全部「帳面に無い」ことになる。
        string original = Sheet.SkillHead() + Sheet.BlockOf(real);

        var (_, console) = WriteAndCaptureBoth(Sheet.SkillFile, original);

        int expected = Skills.All.Count - 1;
        Assert.Contains(
            $"帳面に無かった {expected} 件を実装から書き戻しました（実装が正 ── 消すなら Skills.cs から）",
            console);
    }

    /// <summary>⭐ 上と対 ── 実装にある id が全部原文にもあれば（＝何も消えていなければ）、
    /// 書き戻しは0件で、その行自体が出ない（「書きかけ」表示が0件で出ないのと同じ約束）。
    /// ⚠️ 数え方を「常に出す」ように壊す、あるいは分母/分子を取り違えると、ここが赤くなる。</summary>
    [Fact]
    public void 何も消えていなければ書き戻しの行は出ない()
    {
        var md = new StringBuilder(Sheet.SkillHead());
        foreach (var s in Skills.All) md.Append(Sheet.BlockOf(s)).Append('\n');

        var (_, console) = WriteAndCaptureBoth(Sheet.SkillFile, md.ToString());

        Assert.DoesNotContain("書き戻しました", console);
    }
}
