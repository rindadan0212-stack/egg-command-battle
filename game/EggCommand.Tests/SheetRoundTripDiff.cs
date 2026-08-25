using System;
using System.Collections.Generic;
using System.Linq;

namespace EggCommand.Tests;

/// <summary>「読んで書いて元に戻るか」の差分を**分類して数える**ための、この検査だけの道具。
///
/// ⭐ 狙いは <see cref="Sheet"/> を直すことではない。**何が変わったかを人が読める形で言う**こと。
/// ⚠️ だから `Sheet.Put` の中身を書き写さない ── ここは Sheet.cs の実装を信じず、
///    「元の文字列」と「実際に書き出された文字列」を**外から**突き合わせるだけ。
///    実装が変わっても、この検査自体は壊れない（分類の中身が変わるだけ）。</summary>
internal static class SheetRoundTripDiff
{
    /// <summary>1つの帳面ファイルについて、往復でどう変わったかをまとめたもの。</summary>
    public sealed class Report
    {
        public bool OriginalHasCrlf;
        public bool ProducedHasCrlf;
        public int OriginalLines;
        public int ProducedLines;

        public int HeaderChanged;
        public int CommentsLost;
        public int CommentsTrailingWsDropped;   // ⚠️ CommentsMoved と重なってよい（両方に該当する行がある）
        public int CommentsMovedWithinBlock;    // 同じ見出しの中で位置が変わった（例: 塊の下へ）
        public int CommentsMovedAcrossBlock;    // 別の見出しの持ち物として出てきた（一番まずい）
        public int SpacingChanged;              // 「札 = 中身」の空白（桁揃え）だけが変わった
        public int OtherChanged;                // 上のどれにも当てはまらない差分（空行の増減も含む）

        public readonly List<string> Notes = new();

        public int CommentsMovedTotal => CommentsMovedWithinBlock + CommentsMovedAcrossBlock;

        public void Note(string s)
        {
            if (Notes.Count < 400) Notes.Add(s);   // ⚠️ 無限に溜めない（読めなくなる）
        }
    }

    private static string Trunc(string s, int n = 70) => s.Length <= n ? s : s.Substring(0, n) + "…";

    /// <summary>差分の本体。⭐ <paramref name="original"/> は実物の帳面、
    /// <paramref name="produced"/> は「実物のコピーに `Sheet.Run("write")` を通した結果」。
    /// どちらも**1文字も実ファイルへは書いていない**（呼び出し側の責任）。</summary>
    public static Report Analyze(string original, string produced, string head)
    {
        var report = new Report
        {
            OriginalHasCrlf = original.Contains("\r\n"),
            // ⭐ Put() の直し（2026-08-23）で、書き出しはファイル自身の流儀（CRLF/LF）に
            //    合わせるようになった。⚠️ ここを見ずに OriginalHasCrlf だけで
            //    「書き出しは LF に化ける」と決め打ちすると、直った後も嘘の警告を出し続ける。
            ProducedHasCrlf = produced.Contains("\r\n"),
        };

        // ⚠️ 改行コードそのものの差は別枠で数える（CRLF→LF は1行ごとの「内容の変化」ではなく
        //    ファイル全体の性質なので、ここで畳んでおかないと本文の差分が丸ごと汚染される）。
        string[] o = original.Replace("\r\n", "\n").Split('\n');
        string[] p = produced.Replace("\r\n", "\n").Split('\n');
        report.OriginalLines = o.Length;
        report.ProducedLines = p.Length;

        string marker = "# " + head + " ";
        int headEndO = HeaderEnd(o, marker);
        int headEndP = HeaderEnd(p, marker);

        // ① ヘッダ（最初の「# 技 」より前）
        DiffHeader(o, p, headEndO, headEndP, report);

        // ② 各行が「どの見出し（id）の持ち物か」を、見出し行を跨がず素直に読む
        int[] ownerO = OwnerOf(o, marker, headEndO);
        int[] ownerP = OwnerOf(p, marker, headEndP);

        // ③ // コメント行を対応付けて、消えた／動いた／行末空白が落ちたを数える
        MatchComments(o, p, headEndO, headEndP, ownerO, ownerP, report);

        // ④ コメントを除いた本文を突き合わせて、桁揃え／その他を数える
        DiffBody(o, p, headEndO, headEndP, report);

        return report;
    }

    private static int HeaderEnd(string[] lines, string marker)
    {
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Trim().StartsWith(marker, StringComparison.Ordinal)) return i;
        return lines.Length;
    }

    /// <summary>各行の「持ち主」= 直前に出てきた見出し行そのもの（テキストで持つ）。
    /// ⚠️ 見出しが出るまでは null。⭐ 空行では持ち主を切り替えない ──
    /// これは `Sheet.Put` の実際の挙動（空行でバッファを切らない）を外から観測するための決めごと。</summary>
    private static int[] OwnerOf(string[] lines, string marker, int headEnd)
    {
        var owner = new int[lines.Length];
        int cur = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (i >= headEnd && lines[i].Trim().StartsWith(marker, StringComparison.Ordinal)) cur = i;
            owner[i] = cur;
        }
        return owner;
    }

    private static void DiffHeader(string[] o, string[] p, int headEndO, int headEndP, Report report)
    {
        var ops = Lcs(o, 0, headEndO, p, 0, headEndP);
        foreach (var (oi, pj) in ops)
        {
            if (oi.HasValue && !pj.HasValue)
            {
                report.HeaderChanged++;
                report.Note($"ヘッダ消失: 元{oi + 1}行目「{Trunc(o[oi.Value])}」");
            }
            else if (!oi.HasValue && pj.HasValue)
            {
                report.HeaderChanged++;
                report.Note($"ヘッダ追加: 先{pj + 1}行目「{Trunc(p[pj.Value])}」");
            }
        }
    }

    private static bool IsComment(string line) => line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    private static void MatchComments(string[] o, string[] p, int headEndO, int headEndP,
        int[] ownerO, int[] ownerP, Report report)
    {
        var oIdx = new List<int>();
        for (int i = headEndO; i < o.Length; i++) if (IsComment(o[i])) oIdx.Add(i);
        var pIdx = new List<int>();
        for (int j = headEndP; j < p.Length; j++) if (IsComment(p[j])) pIdx.Add(j);

        // ⭐ まず一字一句そのままで対応付け（同じ文が何度も出る＝写しの自己増殖もこれで拾える）。
        var oLines = oIdx.Select(i => o[i]).ToArray();
        var pLines = pIdx.Select(j => p[j]).ToArray();
        var ops = Lcs(oLines, 0, oLines.Length, pLines, 0, pLines.Length);

        var matched = new List<(int oi, int pj)>();
        var unmatchedO = new List<int>();
        var unmatchedP = new List<int>();
        foreach (var (a, b) in ops)
        {
            if (a.HasValue && b.HasValue) matched.Add((oIdx[a.Value], pIdx[b.Value]));
            else if (a.HasValue) unmatchedO.Add(oIdx[a.Value]);
            else if (b.HasValue) unmatchedP.Add(pIdx[b.Value]);
        }

        // ⚠️ 残ったものは「行末空白だけ落ちて一致しなくなった」可能性がある。
        //    ⭐ TrimEnd で緩めてもう一度、出た順に対応付ける（Sheet.Put の TrimEnd() を狙い撃ち）。
        var usedP = new HashSet<int>();
        var stillUnmatchedO = new List<int>();
        foreach (int oi in unmatchedO)
        {
            int found = FindFirstUnused(unmatchedP, usedP, pj => p[pj] == o[oi].TrimEnd());
            if (found >= 0)
            {
                usedP.Add(found);
                matched.Add((oi, found));
                report.CommentsTrailingWsDropped++;
                report.Note($"行末空白が落ちた: 元{oi + 1}行目「{Trunc(o[oi])}」");
            }
            else stillUnmatchedO.Add(oi);
        }

        foreach (int oi in stillUnmatchedO)
        {
            report.CommentsLost++;
            report.Note($"コメントが消えた: 元{oi + 1}行目「{Trunc(o[oi])}」"
                + $"（{OwnerLabel(o, ownerO, oi)}）");
        }

        foreach (var (oi, pj) in matched)
        {
            bool sameOwner = OwnerText(o, ownerO, oi) == OwnerText(p, ownerP, pj);
            bool wasLastO = IsLastBeforeBoundary(o, oi);
            bool isLastP = IsLastBeforeBoundary(p, pj);
            if (!sameOwner)
            {
                report.CommentsMovedAcrossBlock++;
                report.Note($"別の見出しへ吸着: 元{oi + 1}行目「{Trunc(o[oi])}」 "
                    + $"── 元は{OwnerLabel(o, ownerO, oi)}、書き出し後は{OwnerLabel(p, ownerP, pj)}");
            }
            else if (wasLastO != isLastP)
            {
                report.CommentsMovedWithinBlock++;
                report.Note($"塊の中で位置が変わった: 元{oi + 1}行目「{Trunc(o[oi])}」"
                    + $"（{OwnerLabel(o, ownerO, oi)}） "
                    + (isLastP ? "── 塊の下へ移動" : "── 下から離れた場所へ移動"));
            }
        }
    }

    /// <summary>まだ使っていない候補の中から最初の1つ。⚠️ 名前を LINQ と被らせない
    /// （`FirstOrDefault(predicate, fallback)` は .NET 6+ の標準 LINQ と衝突し、
    ///   拡張メソッドの解決があいまいになる）。</summary>
    private static int FindFirstUnused(List<int> candidates, HashSet<int> used, Func<int, bool> pred)
    {
        foreach (var v in candidates) if (!used.Contains(v) && pred(v)) return v;
        return -1;
    }

    /// <summary>この行の持ち主（見出し行そのもの）を文字列で。⚠️ 見出しの前なら "(見出し無し)"。</summary>
    private static string OwnerText(string[] lines, int[] owner, int i) =>
        owner[i] < 0 ? "(見出し無し)" : lines[owner[i]].Trim();

    private static string OwnerLabel(string[] lines, int[] owner, int i) =>
        owner[i] < 0 ? "見出しの前" : OwnerText(lines, owner, i);

    /// <summary>この行より後、同じ見出しの範囲（空行・EOF・次の見出しまで）に、
    /// **コメント以外の中身**がもう無いか。⭐ 「塊の下に来た」の定義そのもの。</summary>
    private static bool IsLastBeforeBoundary(string[] lines, int i)
    {
        for (int k = i + 1; k < lines.Length; k++)
        {
            string t = lines[k].Trim();
            if (t.Length == 0) return true;                         // 空行＝ブロックの終わり
            if (t.StartsWith("#", StringComparison.Ordinal)) return true;  // 次の見出し
            if (IsComment(lines[k])) continue;                       // コメントはまたいでよい
            return false;                                            // 中身がまだ続く
        }
        return true;   // ファイルの終わり
    }

    private static void DiffBody(string[] o, string[] p, int headEndO, int headEndP, Report report)
    {
        // ⭐ コメントを除いた本文だけを比べる（コメントの移動をここでも二重に数えないため）。
        var oBody = new List<(int idx, string text)>();
        for (int i = headEndO; i < o.Length; i++) if (!IsComment(o[i])) oBody.Add((i, o[i]));
        var pBody = new List<(int idx, string text)>();
        for (int j = headEndP; j < p.Length; j++) if (!IsComment(p[j])) pBody.Add((j, p[j]));

        var oText = oBody.Select(t => t.text).ToArray();
        var pText = pBody.Select(t => t.text).ToArray();
        var ops = Lcs(oText, 0, oText.Length, pText, 0, pText.Length);

        // ⚠️ 連続する「消えた・増えた」を1つの塊（ハンク）にまとめ、
        //    その中で消えた行と増えた行を**出た順に1対1で対応付ける** ──
        //    そうして初めて「同じ札の空白だけが変わった」を見分けられる。
        var pending = new List<(int? o, int? p)>();
        void Flush()
        {
            if (pending.Count == 0) return;
            var dels = pending.Where(x => x.o.HasValue).Select(x => oBody[x.o!.Value]).ToList();
            var inss = pending.Where(x => x.p.HasValue).Select(x => pBody[x.p!.Value]).ToList();
            int n = Math.Min(dels.Count, inss.Count);
            for (int k = 0; k < n; k++) Classify(dels[k], inss[k], report);
            for (int k = n; k < dels.Count; k++)
            {
                report.OtherChanged++;
                report.Note($"本文の行が消えた: 元{dels[k].idx + 1}行目「{Trunc(dels[k].text)}」");
            }
            for (int k = n; k < inss.Count; k++)
            {
                report.OtherChanged++;
                report.Note($"本文に無い行が増えた: 先{inss[k].idx + 1}行目「{Trunc(inss[k].text)}」");
            }
            pending.Clear();
        }
        foreach (var (oi, pj) in ops)
        {
            if (oi.HasValue && pj.HasValue) Flush();
            else pending.Add((oi, pj));
        }
        Flush();
    }

    private static void Classify((int idx, string text) del, (int idx, string text) ins, Report report)
    {
        int eqO = del.text.IndexOf('=');
        int eqP = ins.text.IndexOf('=');
        if (eqO >= 0 && eqP >= 0)
        {
            string keyO = del.text.Substring(0, eqO).Trim(), valO = del.text.Substring(eqO + 1).Trim();
            string keyP = ins.text.Substring(0, eqP).Trim(), valP = ins.text.Substring(eqP + 1).Trim();
            if (keyO == keyP && valO == valP)
            {
                // 中身は同じで、= の周りの空白（桁揃え）だけが違う
                report.SpacingChanged++;
                report.Note($"桁揃えが変わった: 元{del.idx + 1}行目「{Trunc(del.text)}」"
                    + $" → 「{Trunc(ins.text)}」");
                return;
            }
        }
        report.OtherChanged++;
        report.Note($"内容が変わった: 元{del.idx + 1}行目「{Trunc(del.text)}」"
            + $" → 先{ins.idx + 1}行目「{Trunc(ins.text)}」");
    }

    /// <summary>行の並びを比べる、ふつうの最長共通部分列（LCS）。
    /// ⭐ ここでは意味を持たせない ── 「同じ文字列がどの順で対応するか」だけを返す。
    /// ⚠️ O(n*m) だが、帳面は最大でも1000行に満たないので十分に軽い。</summary>
    private static List<(int? OrigIdx, int? ProdIdx)> Lcs(
        string[] a, int aFrom, int aTo, string[] b, int bFrom, int bTo)
    {
        int n = aTo - aFrom, m = bTo - bFrom;
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                dp[i, j] = a[aFrom + i] == b[bFrom + j] ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var ops = new List<(int?, int?)>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[aFrom + x] == b[bFrom + y]) { ops.Add((aFrom + x, bFrom + y)); x++; y++; }
            else if (dp[x + 1, y] >= dp[x, y + 1]) { ops.Add((aFrom + x, null)); x++; }
            else { ops.Add((null, bFrom + y)); y++; }
        }
        while (x < n) { ops.Add((aFrom + x, null)); x++; }
        while (y < m) { ops.Add((null, bFrom + y)); y++; }
        return ops;
    }
}
