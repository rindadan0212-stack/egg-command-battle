using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>分岐するすごろく式の潜入（<see cref="Trail"/>）。
///
/// ⚠️ 飛ばす遊び（<see cref="Steal"/>）の <see cref="InfiltrationTests"/> は**別に残す**。
/// あちらは移植元の規則で、較正済みの照合が踏んでいる。</summary>
public class TrailTests
{
    private static Creature Make(string id, int hp, int atk, int def, int spd) =>
        new Creature(id, "tamaru", new StatBlock(hp, atk, def, spd),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1);

    private static List<Creature> Party(int spd = 20) => new()
    {
        Make("a", 20, 20, 20, spd), Make("b", 20, 20, 20, spd), Make("c", 20, 20, 20, spd),
    };

    /// <summary>その出目だけを返す <see cref="Rng"/>。
    ///
    /// ⚠️ 潜入を巻き戻して振り直す作りにしてはいけない ── 進んだ跡まで消えて、
    /// 跡を見る規則が検査できなくなる。⭐ 種のほうを選べば、潜入には触らずに出目を決められる。</summary>
    private static Rng RngFor(int pips)
    {
        for (int seed = 0; seed < 100_000; seed++)
            if (new Rng(seed).Int(0, Trail.Pips) == pips - 1) return new Rng(seed);
        throw new InvalidOperationException($"出目 {pips} を出す種が見つからない");
    }

    private static RaidStep Advance(Raid raid, int pips)
    {
        var step = Trails.Roll(RngFor(pips), raid);
        Assert.Equal(pips, raid.LastRoll);
        return step;
    }

    /// <summary>最後まで回す。⭐ 止まらないことの確認も兼ねる。</summary>
    private static void Play(Rng rng, Raid raid, bool near = true)
    {
        int guard = 0;
        while (raid.Result == null)
        {
            if (++guard > 5000) throw new InvalidOperationException("潜入が終わらない");
            switch (raid.Step)
            {
                case RaidStep.AtJunction:
                    var ways = raid.Trail.Squares[raid.At].Ways;
                    int pick = -1;
                    for (int i = 0; i < ways.Count; i++)
                    {
                        if (!Trails.CanPass(raid, ways[i])) continue;
                        if (pick < 0) { pick = i; continue; }
                        bool better = near ? ways[i].Length < ways[pick].Length
                                           : ways[i].Length > ways[pick].Length;
                        if (better) pick = i;
                    }
                    Assert.True(pick >= 0, "通れる道が無いのに詰みになっていない");
                    Trails.Take(raid, pick);
                    break;
                case RaidStep.Met: Trails.Beat(raid); break;
                default: Trails.Roll(rng, raid); break;
            }
        }
    }

    // ── 移動力 ────────────────────────────────────

    /// <summary>⭐ 作者の決定「合計速度によってさいころを振れる数が変わる」。</summary>
    [Fact]
    public void 振れる回数は速度の合計だけで決まる()
    {
        Assert.True(Trails.RollsFor(Party(30)) > Trails.RollsFor(Party(0)));

        // ⚠️ 誰が速いかは効かない（3体で1つの駒なので）
        var lopsided = new List<Creature>
        {
            Make("x", 20, 20, 20, 45), Make("y", 20, 20, 20, 15), Make("z", 20, 20, 20, 0),
        };
        Assert.Equal(Trails.RollsFor(Party(20)), Trails.RollsFor(lopsided));

        // ⚠️ 速度 0 でも 1回は振れる（何もできない潜入を作らない）
        Assert.True(Trails.RollsFor(new List<Creature> { Make("a", 0, 0, 0, 0) }) >= 1);
    }

    /// <summary>⭐ 巣の寿命。**盗まれた巣ほど親が早く帰ってくる。**</summary>
    [Fact]
    public void 盗んだ回数だけ振れる回数が減る()
    {
        var party = Party(30);
        int fresh = Trails.RollsFor(party);
        for (int raids = 1; raids < Steal.RaidsToSeal; raids++)
            Assert.Equal(fresh - raids * Trail.RollsLostPerRaid, Trails.RollsFor(party, raids));
        Assert.True(Trails.RollsFor(Party(0), 99) >= 1);

        // ⭐ 道の形は変えない（下見して編成を替えられることが芯）
        var a = Trails.OfNest(Nests.All[0]);
        Assert.Equal(a.Count, Trails.Begin(a, party, 3).Trail.Count);
    }

    // ── 盤の形 ────────────────────────────────────

    /// <summary>⭐ 盤の不変条件。⚠️ ここが崩れると較正した確率が全部ずれる。</summary>
    [Fact]
    public void 盤の不変条件()
    {
        var rng = new Rng(4242);
        for (int tier = 1; tier <= 5; tier++)
            for (int n = 0; n < 200; n++)
            {
                var trail = Trails.Make(rng, tier);

                // ⚠️ 分かれ道の数は段で決まる
                Assert.Equal(Trail.JunctionsFor(tier), trail.Junctions.Count);

                for (int i = 0; i < trail.Count; i++)
                {
                    var sq = trail.Squares[i];
                    // ⚠️ **道は必ず前へ進む。**添字が増える向きでないと、
                    //    最短歩数の計算（後ろから1回なめる）が成り立たない
                    foreach (var way in sq.Ways)
                        Assert.True(way.To > i, $"道が後ろへ向いている: {i} → {way.To}");

                    if (!sq.IsJunction) continue;
                    Assert.Equal(2, sq.Ways.Count);
                    var near = sq.Ways[0];
                    var far = sq.Ways[1];
                    // ⭐ 近い道は必ず短い
                    Assert.True(near.Length < far.Length, "近い道が遠い道より長い");
                    // ⭐ 2本は必ず違うステを要求する（種類を比べる場面を作るため）
                    Assert.NotEqual(near.Gate, far.Gate);
                    Assert.True(near.IsGated && far.IsGated, "関門の無い道がある");
                    Assert.InRange(near.Length - 1, Trail.ShortMin, Trail.ShortMax);
                    Assert.InRange(far.Length - 1, Trail.LongMin, Trail.LongMax);

                    int nearFair = Trail.PriceFor(near.Gate, tier, Trail.ShortShare);
                    int farFair = Trail.PriceFor(far.Gate, tier, Trail.LongShare);
                    Assert.InRange(near.Requires,
                        nearFair * Trail.PriceLow / 100, nearFair * Trail.PriceHigh / 100);
                    Assert.InRange(far.Requires,
                        farFair * Trail.PriceLow / 100, farFair * Trail.PriceHigh / 100);
                }

                // ⚠️ 卵は最後の1マスだけ
                for (int i = 0; i < trail.Count - 1; i++)
                    Assert.False(trail.Squares[i].IsGoal, $"{i} が行き止まり");
                Assert.True(trail.Squares[trail.Goal].IsGoal);
            }
    }

    /// <summary>⚠️ **遠い道の関門は、寄せた編成の薄いほうでも通れる。**
    /// ⭐ ここが崩れると、どの道も通れない行き止まりが生まれる
    /// （払う形にしていたときは詰みが 63% 出た。2026-08-20 の実測）。</summary>
    [Fact]
    public void 遠い道は薄いステでも通れる()
    {
        var rng = new Rng(909);
        for (int tier = 1; tier <= 5; tier++)
        {
            // ⭐ 1本に全振りした編成の、薄いほう（参照の 0.5倍）
            var thin = new StatBlock(
                Trail.RefStat(GimmickKind.Damage, tier) / 2,
                Trail.RefStat(GimmickKind.Wall, tier) / 2,
                Trail.RefStat(GimmickKind.Pressure, tier) / 2, 0);

            for (int n = 0; n < 200; n++)
            {
                var trail = Trails.Make(rng, tier);
                var raid = new Raid(trail, Party(), rolls: 99, pool: thin);
                foreach (var j in trail.Junctions)
                {
                    raid.At = j;
                    Assert.True(Trails.OpenWays(raid) > 0,
                        $"段{tier} のマス {j} で、どの道も通れない");
                }
            }
        }
    }

    /// <summary>⭐ 巣ごとに道が固定される（＝下見できる）。</summary>
    [Fact]
    public void 巣の道は何度作っても同じ()
    {
        var seen = new HashSet<string>();
        foreach (var nest in Nests.All)
        {
            var a = Trails.OfNest(nest);
            var b = Trails.OfNest(nest);
            Assert.Equal(a.Count, b.Count);
            var key = "";
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a.Squares[i].Kind, b.Squares[i].Kind);
                Assert.Equal(a.Squares[i].Ways.Count, b.Squares[i].Ways.Count);
                for (int w = 0; w < a.Squares[i].Ways.Count; w++)
                {
                    Assert.Equal(a.Squares[i].Ways[w].To, b.Squares[i].Ways[w].To);
                    Assert.Equal(a.Squares[i].Ways[w].Requires, b.Squares[i].Ways[w].Requires);
                    key += $"{a.Squares[i].Ways[w].To}:{a.Squares[i].Ways[w].Requires},";
                }
            }
            seen.Add(key);
        }
        // ⚠️ 巣ごとに違う道であること（全部同じでは下見が意味を持たない）
        Assert.True(seen.Count > 1);
    }

    // ── 分かれ道 ──────────────────────────────────

    /// <summary>⭐ 分かれ道に着いたら止まる。⚠️ 使い残した目は消えない。</summary>
    [Fact]
    public void 分かれ道で止まり残った目は消えない()
    {
        var trail = Ladder(gap: 3, shortLen: 1, longLen: 3, nearReq: 1, farReq: 1);
        var raid = new Raid(trail, Party(), rolls: 9, pool: Rich());

        // 0〜2 は素通りの一本道。3マス進むと分かれ道
        Assert.Equal(RaidStep.AtJunction, Advance(raid, 5));
        Assert.Equal(3, raid.At);
        Assert.Equal(2, raid.Pending);      // 5 のうち 3 使って残り2
    }

    /// <summary>⭐ 道を選ぶと、そこへ1マス入って残りの目を歩く。</summary>
    [Fact]
    public void 道を選ぶと残った目のぶん歩く()
    {
        var trail = Ladder(gap: 0, shortLen: 2, longLen: 4, nearReq: 1, farReq: 1);
        var raid = AtHub(trail, Rich());
        raid.Pending = 3;

        int head = trail.Squares[0].Ways[1].To;
        Trails.Take(raid, 1);               // 遠い道（4マス）
        // ⭐ 入って1マス + 残り2 ＝ 頭から2マス先
        Assert.Equal(head + 2, raid.At);
    }

    /// <summary>⚠️ 通れない道は選べない（黙って通さない）。</summary>
    [Fact]
    public void 通れない道は選べない()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 999_999, farReq: 1);
        var raid = AtHub(trail, Rich());

        Assert.False(Trails.CanPass(raid, trail.Squares[0].Ways[0]));
        Assert.True(Trails.CanPass(raid, trail.Squares[0].Ways[1]));
        Assert.Equal(1, Trails.OpenWays(raid));
        Assert.Throws<InvalidOperationException>(() => Trails.Take(raid, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Trails.Take(raid, 7));
    }

    /// <summary>⭐ 遊びの芯。**片方は攻撃が足りないが、もう片方は防御で通れる。**</summary>
    [Fact]
    public void 片方が通れなくてももう片方が通れる()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3,
            nearReq: 1000, farReq: 300, nearGate: GimmickKind.Wall, farGate: GimmickKind.Pressure);

        // ⭐ 攻撃が薄く、防御が厚い編成
        var raid = AtHub(trail, new StatBlock(999, 400, 1500, 0));
        Assert.False(Trails.CanPass(raid, trail.Squares[0].Ways[0]));   // 壁は無理
        Assert.True(Trails.CanPass(raid, trail.Squares[0].Ways[1]));    // 重圧なら通れる

        // ⚠️ 逆に寄せると、通れる道が入れ替わる
        var other = AtHub(trail, new StatBlock(999, 1500, 200, 0));
        Assert.True(Trails.CanPass(other, trail.Squares[0].Ways[0]));
        Assert.False(Trails.CanPass(other, trail.Squares[0].Ways[1]));
    }

    /// <summary>⚠️ 関門を通っても**ステは減らない**。
    ///
    /// ⚠️ 払って減らす形にしていたら、2本とも関門付きなので払い切って行き止まりになり、
    /// 詰みが 63% 出た。⭐ 分岐では**道の長さそのものが代価**（2026-08-20 の実測）。</summary>
    [Fact]
    public void 関門を通ってもステは減らない()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 500, farReq: 100);
        var raid = AtHub(trail, Rich());
        var before = raid.Pool;
        Trails.Take(raid, 0);
        Assert.Equal(before.Atk, raid.Pool.Atk);
        Assert.Equal(before.Hp, raid.Pool.Hp);
        Assert.Equal(before.Def, raid.Pool.Def);
    }

    /// <summary>⚠️ どの道も通れなければ、そこで見つかる。</summary>
    [Fact]
    public void どの道も通れなければ見つかる()
    {
        var trail = Ladder(gap: 2, shortLen: 1, longLen: 3,
            nearReq: 999_999, farReq: 999_999);
        var raid = new Raid(trail, Party(), rolls: 9, pool: new StatBlock(1, 1, 1, 0));
        Advance(raid, 2);
        Assert.Equal(StealOutcome.Blocked, raid.Result);
        Assert.Equal(RaidStep.Caught, raid.Step);
    }

    // ── 一時的な増減 ──────────────────────────────

    /// <summary>⭐ ▲ は**閉じていた道を開ける**。⚠️ ここが遠回りの取り柄。</summary>
    [Fact]
    public void 増減で通れる道が変わる()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 1200, farReq: 100);
        var raid = AtHub(trail, new StatBlock(999, 1000, 999, 0));
        Assert.False(Trails.CanPass(raid, trail.Squares[0].Ways[0]));

        raid.Temp = raid.Temp.With(StatKey.Atk, 30);
        raid.TempLeft = raid.TempLeft.With(StatKey.Atk, 3);
        Assert.True(Trails.CanPass(raid, trail.Squares[0].Ways[0]));    // 1000 × 1.3 = 1300

        // ⚠️ ▼ なら逆に閉じる
        raid.Temp = raid.Temp.With(StatKey.Atk, -30);
        Assert.False(Trails.CanPass(raid, trail.Squares[0].Ways[0]));
    }

    /// <summary>⚠️ 増減は振った回数で切れる。</summary>
    [Fact]
    public void 増減は振った回数で切れる()
    {
        var trail = Line(20, new Dictionary<int, Square>
        {
            [1] = new Square(SquareKind.Boon, StatKey.Atk, 50, 2),
        });
        var raid = new Raid(trail, Party(), rolls: 20, pool: Rich());

        Advance(raid, 1);
        Assert.Equal(50, raid.Temp.Atk);
        Assert.Equal(2, raid.TempLeft.Atk);

        Trails.Roll(new Rng(1), raid);
        Assert.Equal(1, raid.TempLeft.Atk);
        Trails.Roll(new Rng(2), raid);
        Assert.Equal(0, raid.TempLeft.Atk);
        Assert.Equal(0, raid.Temp.Atk);       // ⚠️ 札そのものも消す
    }

    /// <summary>⚠️ **通り抜けただけのマスは効かない**（止まったときだけ）。</summary>
    [Fact]
    public void 通り抜けたマスは効かない()
    {
        var trail = Line(20, new Dictionary<int, Square>
        {
            [1] = new Square(SquareKind.Boon, StatKey.Atk, 50, 5),
            [2] = new Square(SquareKind.Boon, StatKey.Def, 50, 5),
        });
        var raid = new Raid(trail, Party(), rolls: 20, pool: Rich());
        Advance(raid, 2);                      // 1 を通り抜けて 2 に止まる
        Assert.Equal(0, raid.Temp.Atk);        // ⚠️ 通っただけの 1 は効いていない
        Assert.Equal(50, raid.Temp.Def);
    }

    // ── 雑魚 ─────────────────────────────────────

    /// <summary>⭐ 雑魚は Core では決着しない。⚠️ 呼び側が戦闘を回す。</summary>
    [Fact]
    public void 雑魚は呼び側が決着させる()
    {
        var trail = Line(20, new Dictionary<int, Square> { [1] = new Square(SquareKind.Mob) });
        var raid = new Raid(trail, Party(), rolls: 3, pool: Rich());

        Assert.Equal(RaidStep.Met, Advance(raid, 1));
        Assert.Null(raid.Result);
        int had = raid.Rolls;
        Trails.Beat(raid);
        Assert.Equal(had + Trail.MobRefund, raid.Rolls);
        Assert.Equal(RaidStep.Moved, raid.Step);

        // ⚠️ 一度倒した雑魚とは、戻ってきても戦わない
        raid.At = 0;
        raid.Step = RaidStep.Moved;
        Assert.NotEqual(RaidStep.Met, Advance(raid, 1));
    }

    /// <summary>⚠️ 最後の1振りで雑魚に当たっても、そこで終わらせない。</summary>
    [Fact]
    public void 振り切ったあとの雑魚は倒せば続けられる()
    {
        var trail = Line(20, new Dictionary<int, Square> { [3] = new Square(SquareKind.Mob) });
        var raid = new Raid(trail, Party(), rolls: 1, pool: Rich());

        Assert.Equal(RaidStep.Met, Advance(raid, 3));
        Assert.Null(raid.Result);
        Assert.Equal(0, raid.Rolls);
        Trails.Beat(raid);
        Assert.Equal(1, raid.Rolls);
        Assert.Null(raid.Result);
    }

    /// <summary>⚠️ 雑魚に負けたらそこで見つかる。</summary>
    [Fact]
    public void 雑魚に負けたら見つかる()
    {
        var trail = Line(20, new Dictionary<int, Square> { [1] = new Square(SquareKind.Mob) });
        var raid = new Raid(trail, Party(), rolls: 3, pool: Rich());
        Advance(raid, 1);
        Trails.Lost(raid);
        Assert.Equal(StealOutcome.Blocked, raid.Result);
    }

    // ── 決着 ─────────────────────────────────────

    /// <summary>⭐ 振り切って届かなければ親が帰ってくる。</summary>
    [Fact]
    public void 振り切って届かなければ見つかる()
    {
        var raid = new Raid(Line(40), Party(), rolls: 1, pool: Rich());
        Trails.Roll(new Rng(7), raid);
        Assert.Equal(StealOutcome.Stalled, raid.Result);
    }

    /// <summary>⭐ 卵まで届いたら成功。⚠️ 行き過ぎても戻されない。</summary>
    [Fact]
    public void 届けば成功で行き過ぎても戻されない()
    {
        var raid = new Raid(Line(3), Party(), rolls: 5, pool: Rich());
        Advance(raid, 6);
        Assert.Equal(StealOutcome.Success, raid.Result);
        Assert.Equal(2, raid.At);                   // ⚠️ 卵は最後のマス
    }

    /// <summary>⚠️ 場面ちがいの操作は黙って通さない。</summary>
    [Fact]
    public void 場面ちがいの操作は投げる()
    {
        var trail = Ladder(gap: 2, shortLen: 1, longLen: 3, nearReq: 1, farReq: 1);
        var raid = new Raid(trail, Party(), rolls: 9, pool: Rich());

        Assert.Throws<InvalidOperationException>(() => Trails.Take(raid, 0));
        Assert.Throws<InvalidOperationException>(() => Trails.Beat(raid));
        Assert.Throws<InvalidOperationException>(() => Trails.Lost(raid));

        Advance(raid, 2);                            // AtJunction
        Assert.Throws<InvalidOperationException>(() => Trails.Roll(new Rng(1), raid));
        Assert.Throws<InvalidOperationException>(() => Trails.Beat(raid));
    }

    /// <summary>⚠️ どんな盤・どんな指し手でも必ず終わる（無限に回らない）。</summary>
    [Fact]
    public void どんな盤でも必ず決着する()
    {
        var rng = new Rng(31337);
        for (int tier = 1; tier <= 5; tier++)
            for (int n = 0; n < 120; n++)
                foreach (var near in new[] { true, false })
                {
                    var raid = Trails.Begin(Trails.Make(rng, tier), Party(30));
                    if (raid.Result == null) Play(rng, raid, near);
                    Assert.NotNull(raid.Result);
                }
    }

    // ── 見通し ────────────────────────────────────

    /// <summary>⭐ 画面に出す「あと何マス」。⚠️ **通れる道だけ**を数える。</summary>
    [Fact]
    public void 残りマス数は通れる道だけで数える()
    {
        // 近い道 2マス（攻1200 が要る）／遠い道 4マス（防100）
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 1200, farReq: 100,
            nearGate: GimmickKind.Wall, farGate: GimmickKind.Pressure);

        var strong = AtHub(trail, new StatBlock(999, 1500, 999, 0));
        Assert.Equal(2, Trails.Left(strong));       // ⭐ 近い道が通れるので 2

        var weak = AtHub(trail, new StatBlock(999, 100, 999, 0));
        Assert.Equal(4, Trails.Left(weak));         // ⚠️ 遠い道しか無いので 4

        // ⭐ 道ごとの残りも出る
        Assert.Equal(2, Trails.LeftIfTake(strong, 0));
        Assert.Equal(4, Trails.LeftIfTake(strong, 1));
    }

    /// <summary>⭐ 届く見込みの端。⚠️ 端が合っていないと嘘の札になる。</summary>
    [Fact]
    public void 届く見込みの端()
    {
        var raid = new Raid(Line(31), Party(), rolls: 1, pool: Rich());
        raid.At = 30;
        Assert.Equal(100, Trails.Odds(raid));       // もう届いている
        raid.At = 24;                                // 残り6・1回振る → 6分の1
        Assert.Equal(17, Trails.Odds(raid));
        raid.At = 29;                                // 残り1 → 必ず届く
        Assert.Equal(100, Trails.Odds(raid));
        raid.At = 23;                                // 残り7・1回では届かない
        Assert.Equal(0, Trails.Odds(raid));
    }

    /// <summary>⚠️ 振れる回数が増えるほど見込みは上がる（単調）。</summary>
    [Fact]
    public void 見込みは振れる回数について単調()
    {
        var raid = new Raid(Line(31), Party(), rolls: 0, pool: Rich());
        int last = -1;
        for (int rolls = 1; rolls <= 40; rolls++)
        {
            raid.Rolls = rolls;
            int now = Trails.Odds(raid);
            Assert.True(now >= last, $"{rolls}回で見込みが下がった: {last} → {now}");
            last = now;
        }
        Assert.Equal(100, last);
    }

    /// <summary>⭐ 道ごとの見込みは、近いほうが高い。</summary>
    [Fact]
    public void 道ごとの見込みは近いほうが高い()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 1, farReq: 1);
        var raid = AtHub(trail, Rich(), rolls: 3);
        raid.Pending = 0;

        int near = Trails.OddsIfTake(raid, 0);
        int far = Trails.OddsIfTake(raid, 1);
        Assert.True(near >= far, $"近い道のほうが低い: 近{near}% 遠{far}%");
        Assert.InRange(near, 0, 100);
        Assert.InRange(far, 0, 100);
    }

    // ── 道具 ─────────────────────────────────────

    private static StatBlock Rich() => new StatBlock(9999, 9999, 9999, 0);

    /// <summary>入口がいきなり分かれ道の盤で始める。
    /// ⚠️ <see cref="Raid"/> を素で作ると <see cref="RaidStep.Moved"/> のままなので、
    /// <see cref="Trails.Begin"/> と同じ形に揃える。</summary>
    private static Raid AtHub(Trail trail, StatBlock pool, int rolls = 9)
    {
        var raid = new Raid(trail, Party(), rolls, pool);
        raid.Step = RaidStep.AtJunction;
        return raid;
    }

    /// <summary>一本道。⭐ <paramref name="marks"/> で好きなマスを差し替える。</summary>
    private static Trail Line(int count, Dictionary<int, Square>? marks = null)
    {
        var squares = new List<Square>();
        for (int i = 0; i < count; i++)
            squares.Add(marks != null && marks.ContainsKey(i) ? marks[i] : new Square());
        for (int i = 0; i + 1 < count; i++) squares[i].Ways.Add(new Way(i + 1));
        return new Trail(squares, 1, new List<int>());
    }

    /// <summary>分かれ道1つの盤。⭐ <paramref name="gap"/> マス歩いてから分かれる。</summary>
    private static Trail Ladder(int gap, int shortLen, int longLen, int nearReq, int farReq,
        GimmickKind nearGate = GimmickKind.Wall, GimmickKind farGate = GimmickKind.Pressure)
    {
        var squares = new List<Square>();
        for (int i = 0; i <= gap; i++) squares.Add(new Square());
        for (int i = 0; i < gap; i++) squares[i].Ways.Add(new Way(i + 1));

        int hub = gap;
        int nearHead = squares.Count;
        for (int i = 0; i < shortLen; i++) squares.Add(new Square());
        int farHead = squares.Count;
        for (int i = 0; i < longLen; i++) squares.Add(new Square());
        int join = squares.Count;
        squares.Add(new Square());

        squares[hub].Ways.Add(new Way(nearHead, nearGate, nearReq, shortLen + 1));
        squares[hub].Ways.Add(new Way(farHead, farGate, farReq, longLen + 1));
        for (int i = 0; i < shortLen; i++)
            squares[nearHead + i].Ways.Add(new Way(i + 1 < shortLen ? nearHead + i + 1 : join));
        for (int i = 0; i < longLen; i++)
            squares[farHead + i].Ways.Add(new Way(i + 1 < longLen ? farHead + i + 1 : join));

        return new Trail(squares, 1, new List<int> { hub });
    }
}
