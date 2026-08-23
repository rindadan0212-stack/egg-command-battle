using System;
using System.Collections.Generic;
using System.IO;
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
            _out.WriteLine($"── {file} ── 元 {r.OriginalLines} 行 / 書き出し後 {r.ProducedLines} 行"
                + (r.OriginalHasCrlf ? "　⚠️ 元は CRLF、書き出しは LF に化ける" : ""));
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

    /// <summary>⭐ **実物3枚は、いま偶然どれも「移動済みで安定した形」なので、
    /// 今回の1回の往復だけでは「コメントが移動した」が実測0件になる。**
    /// ⚠️ それが「移動しない」の証拠にならないよう、最小の作り物で
    /// 「札の上に書いたコメントが、既知の技では塊の下へ動く」を直接確かめる
    /// （＝ <see cref="SheetRoundTripDiff"/> の「移動」判定そのものが正しく動く根拠にもなる）。</summary>
    [Fact]
    public void 既知の技では札の上のコメントが塊の下へ動く()
    {
        // ⭐ 実装済みの技（Skills.All にある id）を1つ拝借し、
        //    名前の**上**に手で書いたふうのコメントを足す。
        var real = Skills.All[0];
        string original = Sheet.SkillHead()
            + $"# 技 {real.Id}\n"
            + "// 手で書いたコメント（本来は名前の上）\n"
            + Sheet.BlockOf(real).Substring(("# 技 " + real.Id + "\n").Length);

        string temp = Directory.CreateTempSubdirectory("eggcommand-sheet-synthetic-").FullName;
        try
        {
            string sheetsDir = Path.Combine(temp, "sheets");
            string cwdDir = Path.Combine(temp, "cwd");
            Directory.CreateDirectory(sheetsDir);
            Directory.CreateDirectory(cwdDir);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(sheetsDir, Sheet.SkillFile), original, utf8);

            string produced;
            lock (CwdGate)
            {
                string prev = Environment.CurrentDirectory;
                try
                {
                    Environment.CurrentDirectory = cwdDir;
                    Sheet.Run("write");
                }
                finally { Environment.CurrentDirectory = prev; }
                produced = File.ReadAllText(Path.Combine(sheetsDir, Sheet.SkillFile), utf8);
            }

            var lines = produced.Split('\n');
            int headerLine = Array.FindIndex(lines, l => l.Trim() == $"# 技 {real.Id}");
            Assert.True(headerLine >= 0, "書き出し後に見出しが見つからない");
            int blockEnd = Array.FindIndex(lines, headerLine, l => l.Length == 0);
            Assert.True(blockEnd > headerLine, "ブロックの終わり（空行）が見つからない");
            _out.WriteLine(string.Join('\n', lines[headerLine..(blockEnd + 1)]));

            // ⭐ 見出し直後（＝札の上）にコメントは無いはず（動いた先＝塊の下にあるはず）。
            Assert.False(lines[headerLine + 1].TrimStart().StartsWith("//"),
                "コメントが札の上に残っている（想定と違う＝分類器を見直すこと）");
            // ⭐ ブロックの終わり（空行の直前）にコメントが来ているはず。
            Assert.StartsWith("//", lines[blockEnd - 1].TrimStart());
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }
}
