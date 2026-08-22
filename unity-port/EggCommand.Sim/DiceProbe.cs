using System;
using System.Collections.Generic;
using EggCommand.Core;

namespace EggCommand.Sim
{
    /// <summary>⚠️ **出目と、実際に進むマス数が合っているか**を総当たりで数える。
    ///
    /// ⭐ 作者の報告（2026-08-22）:
    /// 「出た目に関わらず1マスしか進めないときがある」。
    ///
    /// ⚠️ 前回は「`Reach` の道筋の長さは出目と一致する」で終わらせたが、
    /// それは <see cref="Trails.Reach"/> **単体**の話でしかない。
    /// ⭐ ここでは実際の遊びの順（振る → 選ぶ → 動く）をそのまま回して、
    /// **駒が何マス動いたか**を数える。</summary>
    public static class DiceProbe
    {
        public static void Run(int seed)
        {
            Console.WriteLine();
            Console.WriteLine("■ 出目と、実際に進んだマス数");

            // ── ① Reach 単体の総当たり ─────────────────────
            int boards = 0, cases = 0, shortPath = 0, empty = 0;
            var badBoards = new List<string>();
            for (int grade = 1; grade <= 6; grade++)
            {
                for (int n = 0; n < 60; n++)
                {
                    var rng = new Rng(seed).Stream($"dice:{grade}:{n}");
                    var trail = Trails.Make(rng, grade);
                    boards++;
                    var party = new List<Creature>();
                    var raid = new Raid(trail, party, 20, new StatBlock(0, 0, 0, 0));
                    for (int at = 0; at < trail.Count; at++)
                    {
                        for (int pips = 1; pips <= 6; pips++)
                        {
                            raid.At = at;
                            raid.Pending = pips;
                            var reach = Trails.Reach(raid, pips);
                            cases++;
                            if (reach.Count == 0) { empty++; continue; }
                            foreach (var path in reach)
                            {
                                int steps = path.Count - 1;
                                bool goal = trail.Squares[path[path.Count - 1]].IsGoal;
                                if (steps == pips || goal) continue;
                                shortPath++;
                                if (badBoards.Count < 8)
                                    badBoards.Add($"段{grade} 盤{n} マス{at} 出目{pips} → {steps}マス");
                            }
                        }
                    }
                }
            }
            Console.WriteLine($"  盤 {boards} 枚 / 場合 {cases:N0} 通り");
            Console.WriteLine($"  ⚠️ 出目と長さが違う道筋: {shortPath} 件"
                + (shortPath == 0 ? "  ⭐ 異常なし" : ""));
            Console.WriteLine($"  行ける先が0（詰み）: {empty} 件"
                + "  ⚠️ これは仕様（`Trails.Stuck`）");
            foreach (var line in badBoards) Console.WriteLine("    " + line);

            // ── ② 遊びの順どおりに回す ─────────────────────
            Console.WriteLine();
            Console.WriteLine("  ── 振る → 選ぶ → 動く、をそのまま回す");
            var moved = new int[7];      // 出目ごとの回数
            var oneOnly = new int[7];    // そのうち1マスしか進めなかった回数
            int hops = 0, hopOne = 0, throws = 0;
            var firstBad = "";
            for (int grade = 1; grade <= 6; grade++)
            {
                for (int n = 0; n < 120; n++)
                {
                    var rng = new Rng(seed).Stream($"play:{grade}:{n}");
                    var trail = Trails.Make(rng, grade);
                    var raid = new Raid(trail, new List<Creature>(), 40,
                        new StatBlock(99999, 99999, 99999, 99999));
                    int guard = 0;
                    while (raid.Result == null && raid.Rolls > 0 && guard++ < 200)
                    {
                        if (raid.Step == RaidStep.Moved)
                        {
                            Trails.Roll(rng, raid);
                        }
                        if (raid.Step == RaidStep.Met)
                        {
                            // ⭐ 雑魚は勝ったことにして先へ進める（ここで見たいのは移動だけ）
                            Trails.Beat(raid);
                            continue;
                        }
                        if (raid.Step == RaidStep.Offered)
                        {
                            // ⭐ 払える限り払う ── Hop（N マス進む）を必ず通す
                            int before = raid.Pending;
                            Trails.Pay(raid);
                            if (raid.Pending > before) hops++;
                            continue;
                        }
                        if (raid.Step != RaidStep.Choosing) break;

                        int want = raid.Pending;
                        int face = raid.LastRoll;
                        var open = Trails.Reach(raid, want);
                        if (open.Count == 0) { Trails.Stuck(raid); break; }
                        var pick = open[rng.Int(0, open.Count)];
                        int from = raid.At;
                        try { Trails.Go(raid, pick); }
                        catch (InvalidOperationException error)
                        {
                            throws++;
                            if (firstBad.Length == 0) firstBad = error.Message;
                            break;
                        }
                        int steps = pick.Count - 1;
                        if (face >= 1 && face <= 6 && want == face)
                        {
                            moved[face]++;
                            if (steps <= 1 && !trail.Squares[raid.At].IsGoal) oneOnly[face]++;
                        }
                        else if (want != face)
                        {
                            if (steps <= 1 && !trail.Squares[raid.At].IsGoal) hopOne++;
                        }
                        if (raid.At == from) break;
                    }
                }
            }
            Console.WriteLine($"  {"出目",5}{"回数",8}{"1マスで終わった",18}");
            for (int i = 1; i <= 6; i++)
                Console.WriteLine($"  {i,5}{moved[i],8}{oneOnly[i],18}");
            Console.WriteLine($"  関門で「Nマス進む」を買った: {hops} 回 / "
                + $"そのうち1マスで終わった: {hopOne} 回");
            Console.WriteLine($"  ⚠️ 出目と歩数の食い違いで撥ねた: {throws} 件"
                + (throws == 0 ? "  ⭐ 異常なし" : "  → " + firstBad));

            // ── ③ 出目と Pending がずれる道 ─────────────────
            Console.WriteLine();
            Console.WriteLine("  ── ⚠️ さいころの目と Pending がずれる場合");
            Console.WriteLine("     `GiftKind.Hop`（関門の「N マス進む」）は"
                + " `Pending` を足すが `LastRoll` を触らない。");
            Console.WriteLine("     ⭐ つまり **画面が LastRoll を出していると、"
                + "出目と進む数が食い違って見える**。");
        }
    }
}
