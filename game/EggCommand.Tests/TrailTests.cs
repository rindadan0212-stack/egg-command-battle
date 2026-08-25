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

    /// <summary>検査用の編成。⚠️ **体数は決め打ちしない**
    /// （2026-08-20 に 3 → 4。<see cref="Games.PartySize"/> が唯一の出所）。</summary>
    private static List<Creature> Party(int spd = 20)
    {
        var party = new List<Creature>();
        for (int i = 0; i < Games.PartySize; i++)
        {
            party.Add(Make($"p{i}", 20, 20, 20, spd));
        }
        return party;
    }

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

    /// <summary>その出目で振って、**行ける先の1つ目**へ進める。
    /// ⚠️ 振るのと進むのが分かれた（2026-08-20）ので、検査からはまとめて呼ぶ。</summary>
    private static RaidStep Advance(Raid raid, int pips)
    {
        var step = Trails.Roll(RngFor(pips), raid);
        Assert.Equal(pips, raid.LastRoll);
        if (step != RaidStep.Choosing) return step;
        var all = Trails.Reach(raid, raid.Pending);
        if (all.Count == 0) return Trails.Stuck(raid);
        return Trails.Go(raid, all[0]);
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
                case RaidStep.Choosing:
                {
                    // ⭐ 出目で行ける先から選ぶ（2026-08-20 の作り替え）
                    var all = Trails.Reach(raid, raid.Pending);
                    if (all.Count == 0) { Trails.Stuck(raid); break; }
                    int pick = 0;
                    for (int i = 1; i < all.Count; i++)
                    {
                        int a = raid.Trail.Squares[all[i][all[i].Count - 1]].Row;
                        int b = raid.Trail.Squares[all[pick][all[pick].Count - 1]].Row;
                        if (near ? a > b : a < b) pick = i;
                    }
                    Trails.Go(raid, all[pick]);
                    break;
                }
                case RaidStep.Met: Trails.Beat(raid); break;
                // ⭐ 払えるなら払う（2026-08-21）。⚠️ ここを handle しないと Roll が撥ねる
                case RaidStep.Offered: Trails.Pay(raid); break;
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

        // ⚠️ 誰が速いかは効かない（編成ぜんぶで1つの駒なので）。
        // ⭐ 合計だけを揃える ── ⚠️ 体数を決め打ちすると 4体化で落ちる
        var lopsided = Party(20);
        int sum = 0;
        foreach (var c in lopsided) sum += Creatures.StatsOf(c).Spd;
        var skewed = new List<Creature> { Make("x", 20, 20, 20, 0) };
        for (int i = 1; i < lopsided.Count; i++) skewed.Add(Make($"y{i}", 20, 20, 20, 0));
        // ⚠️ 先頭1体に合計を全部背負わせる（素質の上限を超えないよう、素の速度で積む）
        skewed[0] = Make("x", 20, 20, 20, 20 * lopsided.Count);
        int skew = 0;
        foreach (var c in skewed) skew += Creatures.StatsOf(c).Spd;
        Assert.True(skew >= sum, $"合計がそろっていない（{skew} 対 {sum}）");
        Assert.Equal(Trails.RollsFor(lopsided), Trails.RollsFor(skewed));

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

                // ⚠️ 分かれ道の数は**成り行き**（2026-08-20・道をランダムにした）。
                //    ⭐ 見るのは「在ること」だけ
                Assert.True(trail.Junctions.Count > 0, "分かれ道が1つも無い");

                for (int i = 0; i < trail.Count; i++)
                {
                    var sq = trail.Squares[i];
                    // ⚠️ **道は必ず前へ進む。**添字が増える向きでないと、
                    //    最短歩数の計算（後ろから1回なめる）が成り立たない
                    foreach (var way in sq.Ways)
                        Assert.True(way.To > i, $"道が後ろへ向いている: {i} → {way.To}");

                    if (!sq.IsJunction) continue;
                    // ⭐ 分かれ道の本数は成り行き（2026-08-20・作者の指示
                    //    「もはや道は完全にランダムでもいいかもよ」）。
                    // ⚠️ 上限は**列の数**。入口は真ん中からどの列へも開くので、
                    //    そこだけ列の数いっぱいまで出る。
                    Assert.InRange(sq.Ways.Count, 2, Trail.LanesMax);
                }

                // ⭐ **盤の形は、マスの種類を一切知らない**（2026-08-21）。
                //    ⚠️ 以前はここに「関門を通らない道が残っているか」「関門で行き止まらないか」
                //    という守りが3つ在った。⭐ 関門が**只で入れる**ようになったので、
                //    形と中身が切れて、その3つがまとめて要らなくなった。
                //    ⚠️ あの3つを守らせていたことが、詰みの不具合3件の出どころだった。

                // ⭐ 関門は**マス**。⚠️ 入口と卵には置かない
                Assert.False(trail.Squares[0].IsGate, "入口が関門");
                Assert.False(trail.Squares[trail.Goal].IsGate, "卵が関門");
                foreach (var sq in trail.Squares)
                {
                    var toll = sq.Toll;
                    if (toll == null) continue;
                    Assert.InRange(toll.Grade, 1, Trail.GateGrades);
                    Assert.Equal(Trail.PriceOfGrade(toll.Kind, tier, toll.Grade), toll.Price);
                    Assert.True(toll.Price % Trail.PriceRound == 0,
                        $"払う量が {Trail.PriceRound} の倍数でない: {toll.Price}");
                    // ⭐ **払えば必ず何かもらえる。**⚠️ 只働きの関門を作らない
                    Assert.NotEmpty(sq.OnPay);
                }

                // ⭐ **線が×型に交わらない。**⚠️ 交わると、どのマスへ行けるのかを
                //    目で追えなくなる（2026-08-21・`Untangle` で解いている）。
                for (int i = 0; i < trail.Count; i++)
                {
                    var left = trail.Squares[i];
                    for (int j = 0; j < trail.Count; j++)
                    {
                        var right = trail.Squares[j];
                        if (right.Row != left.Row || right.Lane <= left.Lane) continue;
                        foreach (var a in left.Ways)
                        {
                            var toA = trail.Squares[a.To];
                            if (toA.Row - left.Row != 1) continue;
                            foreach (var b in right.Ways)
                            {
                                var toB = trail.Squares[b.To];
                                if (toB.Row - right.Row != 1) continue;
                                Assert.False(toA.Lane > toB.Lane,
                                    $"線が交わっている: {i}→{a.To} と {j}→{b.To}");
                            }
                        }
                    }
                }

                // ⭐ **どの繋ぎもちょうど1段だけ進む。**
                //    ⚠️ ここが崩れると「1マス＝1歩」が崩れ、出目のぶん進んでいないように見える。
                //    ⚠️ 段飛ばしの近道は 2026-08-21 に捨てた（関門が只になり、近道も只になったため）。
                //    ⭐ 距離の伸び縮みは、いまはマスがくれる Hop が担う。
                for (int i = 0; i < trail.Count; i++)
                {
                    var from = trail.Squares[i];
                    foreach (var way in from.Ways)
                    {
                        int gap = trail.Squares[way.To].Row - from.Row;
                        Assert.True(gap == 1, $"{i} → {way.To} が {gap} 段ぶん動いている");
                    }
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
    public void 関門を通らずに卵まで行ける()
    {
        var rng = new Rng(909);
        for (int tier = 1; tier <= 5; tier++)
        {
            // ⭐ 1本も払えない、すかすかの編成
            var broke = new StatBlock(0, 0, 0, 0);

            for (int n = 0; n < 200; n++)
            {
                var trail = Trails.Make(rng, tier);
                // ⭐ **一文無しでも卵まで歩ける。**関門は道を塞がない（作者の指示 2026-08-21
                //    「払わなくても入れる」）。⚠️ ここが崩れると潜入が途中で打ち切られる。
                var raid = new Raid(trail, Party(), rolls: 999, pool: broke);
                int guard = 0;
                while (raid.At != trail.Goal)
                {
                    Assert.True(guard++ < trail.Count, $"段{tier}: 歩き続けても卵に着かない");
                    raid.Step = RaidStep.Choosing;
                    raid.Pending = 1;
                    var open = Trails.Reach(raid, 1);
                    Assert.NotEmpty(open);
                    Trails.Go(raid, open[0]);
                    // ⚠️ 払えないので、払うか訊かれることは無い
                    Assert.NotEqual(RaidStep.Offered, raid.Step);
                    if (raid.Step == RaidStep.Met) Trails.Beat(raid);
                }
                Assert.Equal(StealOutcome.Success, raid.Result);
            }
        }
    }

    /// <summary>⭐ **どの段でも、必ず次の一手がある。**
    ///
    /// ⚠️ これが崩れると**進行不能**になる。2026-08-21 に実際に起きた:
    /// 関門で「N マス進む」を買うと <see cref="RaidStep.Choosing"/> に戻るのに、
    /// 画面は「振ったあと」しか行ける先を並べていなかったので、
    /// **光るマスが0・さいころの釦は例外**で、そこから何も押せなくなった。
    ///
    /// ⭐ ここで押さえるのは Core 側の約束:
    /// <list type="bullet">
    ///   <item><see cref="RaidStep.Moved"/> なら**必ず振れる**（回数が残っている）</item>
    ///   <item><see cref="RaidStep.Choosing"/> なら**必ず行ける先がある**</item>
    ///   <item><see cref="RaidStep.Offered"/> なら**必ず払える**</item>
    /// </list>
    /// ⚠️ 呼び側（画面）は、この3つの段すべてに操作を出さなければならない。</summary>
    [Fact]
    public void どの段でも次の一手がある()
    {
        var rng = new Rng(9182);
        for (int tier = 1; tier <= 5; tier++)
            for (int n = 0; n < 60; n++)
            {
                var raid = Trails.Begin(Trails.Make(rng, tier), Party(spd: 60));
                int guard = 0;
                while (raid.Result == null)
                {
                    Assert.True(guard++ < 400, "潜入が終わらない");
                    switch (raid.Step)
                    {
                        case RaidStep.Moved:
                            Assert.True(raid.Rolls > 0,
                                $"振れないのに Moved（段{tier}・マス {raid.At}）");
                            Trails.Roll(rng, raid);
                            break;

                        case RaidStep.Choosing:
                        {
                            var open = Trails.Reach(raid, raid.Pending);
                            Assert.True(open.Count > 0,
                                $"行ける先が無いのに Choosing（段{tier}・マス {raid.At}"
                                + $"・出目 {raid.Pending}）");
                            Trails.Go(raid, open[rng.Int(0, open.Count)]);
                            break;
                        }

                        case RaidStep.Offered:
                            Assert.True(Trails.CanPay(raid, raid.At),
                                $"払えないのに Offered（マス {raid.At}）");
                            // ⭐ 払う／払わない を交互に試す
                            if (guard % 2 == 0) Trails.Pay(raid); else Trails.Pass(raid);
                            break;

                        case RaidStep.Met:
                            if (guard % 7 == 0) Trails.Lost(raid); else Trails.Beat(raid);
                            break;

                        default:
                            throw new InvalidOperationException($"知らない段 {raid.Step}");
                    }
                }
            }
    }

    // ── 授かり物の順（2026-08-21 の監査で出た穴）───────────────

    /// <summary>⚠️ **戦闘の前に報酬を配らない。**
    ///
    /// ⚠️ 監査で実測: `OnLand = [Fight, Rolls+5]` にすると、**戦う前に +5 が入り、
    /// 負けたあとも残って**いた。⭐ 倒してから配る物は <c>OnWin</c> に置く。</summary>
    [Fact]
    public void 戦闘に負けたら報酬はもらえない()
    {
        var trail = Line(20, new Dictionary<int, Square>
        {
            [1] = Square.Mob(new Gift(GiftKind.Rolls, 5)),
        });
        var raid = new Raid(trail, Party(), rolls: 9, pool: Rich());
        raid.Step = RaidStep.Choosing;
        raid.Pending = 1;

        Assert.Equal(RaidStep.Met, Trails.Go(raid, new[] { 0, 1 }));
        Assert.Equal(9, raid.Rolls);          // ⚠️ 戦う前に増えていないこと

        Trails.Lost(raid);
        Assert.Equal(StealOutcome.Blocked, raid.Result);
        Assert.Equal(9, raid.Rolls);          // ⚠️ 負けたのに増えていないこと
    }

    /// <summary>⭐ 倒せば <c>OnWin</c> がもらえる。</summary>
    [Fact]
    public void 戦闘に勝てば報酬がもらえる()
    {
        var trail = Line(20, new Dictionary<int, Square>
        {
            [1] = Square.Mob(new Gift(GiftKind.Rolls, 5)),
        });
        var raid = new Raid(trail, Party(), rolls: 9, pool: Rich());
        raid.Step = RaidStep.Choosing;
        raid.Pending = 1;
        Trails.Go(raid, new[] { 0, 1 });

        Trails.Beat(raid);
        // ⭐ 雑魚の払い戻し（+1）と OnWin（+5）の両方
        Assert.Equal(9 + Trail.MobRefund + 5, raid.Rolls);
    }

    /// <summary>⚠️ **払いに戦闘は混ぜられない。**
    /// ⭐ 混ぜられると、<c>Pay</c> が段を上書きして**戦闘が黙って起きない**。</summary>
    [Fact]
    public void 払いに戦闘は混ぜられない()
    {
        Assert.Throws<ArgumentException>(() =>
            Square.Gate(new Toll(GimmickKind.Wall, 100, 1), new Gift(GiftKind.Fight, 0)));
    }

    /// <summary>⚠️ **払っても何ももらえない関門は作れない。**</summary>
    [Fact]
    public void 只働きの関門は作れない()
    {
        Assert.Throws<ArgumentException>(() =>
            Square.Gate(new Toll(GimmickKind.Wall, 100, 1)));
    }

    /// <summary>⚠️ **どのマスにも入ってくる道がある。**
    /// ⭐ 出ていく道しか見ていなかったので、<c>Untangle</c> を触ったときに
    /// 孤立マスが出ても気づけなかった（2026-08-21 の監査）。</summary>
    [Fact]
    public void 孤立したマスができない()
    {
        var rng = new Rng(555);
        for (int tier = 1; tier <= 5; tier++)
            for (int n = 0; n < 100; n++)
            {
                var trail = Trails.Make(rng, tier);
                var comes = new bool[trail.Count];
                comes[0] = true;
                foreach (var sq in trail.Squares)
                    foreach (var way in sq.Ways) comes[way.To] = true;
                for (int i = 0; i < trail.Count; i++)
                {
                    Assert.True(comes[i], $"段{tier}: マス {i} に入ってくる道が無い");
                    Assert.True(trail.Squares[i].IsGoal || trail.Squares[i].Ways.Count > 0,
                        $"段{tier}: マス {i} から出ていく道が無い");
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
                Assert.Equal(a.Squares[i].Toll?.Price, b.Squares[i].Toll?.Price);
                Assert.Equal(a.Squares[i].Ways.Count, b.Squares[i].Ways.Count);
                for (int w = 0; w < a.Squares[i].Ways.Count; w++)
                {
                    Assert.Equal(a.Squares[i].Ways[w].To, b.Squares[i].Ways[w].To);
                    key += $"{a.Squares[i].Ways[w].To},";
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

        // ⭐ 分かれ道でも止まらない（2026-08-20）。⚠️ 出目のぶんきっちり進む
        Advance(raid, 5);
        Assert.Equal(0, raid.Pending);
        Assert.True(raid.At > 3, $"分かれ道（3）で止まっている: {raid.At}");
    }

    /// <summary>⭐ 出目のぶんで行ける先が、道ごとに並ぶ。</summary>
    [Fact]
    public void 行ける先が道ごとに並ぶ()
    {
        var trail = Ladder(gap: 0, shortLen: 2, longLen: 4, nearReq: 1, farReq: 1);
        var raid = AtHub(trail, Rich());
        raid.Step = RaidStep.Choosing;

        var all = Trails.Reach(raid, 3);
        Assert.True(all.Count >= 2, $"行ける先が {all.Count} 通りしかない");
        foreach (var path in all)
        {
            Assert.Equal(raid.At, path[0]);
            Assert.Equal(4, path.Count);      // ⭐ いま居るマス ＋ 3マス
        }
    }

    /// <summary>⭐ **払えなくても入れる。**（作者の指示 2026-08-21）
    ///
    /// ⚠️ 2026-08-20 まではステが足りない関門は行ける先に出てこなかった。
    /// ⭐ いまは只で入れて、払うかどうかだけがプレイヤーの判断になる。</summary>
    [Fact]
    public void 払えない関門にも入れる()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 999_999, farReq: 1);
        var raid = AtHub(trail, new StatBlock(1, 1, 1, 0));

        int steep = trail.Squares[0].Ways[0].To;
        bool found = false;
        foreach (var path in Trails.Reach(raid, 1)) if (path[1] == steep) found = true;
        Assert.True(found, "払えない関門が行ける先から外れている");

        // ⭐ 入っても、払うかは訊かれない（払えないので）
        Trails.Go(raid, new[] { raid.At, steep });
        Assert.NotEqual(RaidStep.Offered, raid.Step);
        Assert.Null(raid.Result);
    }

    /// <summary>⭐ **払えるときだけ訊かれる。**</summary>
    [Fact]
    public void 払えるときだけ訊かれる()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 500, farReq: 1);
        var raid = AtHub(trail, Rich());
        int gate = trail.Squares[0].Ways[0].To;

        Assert.Equal(RaidStep.Offered, Trails.Go(raid, new[] { raid.At, gate }));
        Assert.True(Trails.CanPay(raid, gate));
    }

    /// <summary>⭐ **払うと減り、対価がもらえる。**⚠️ ここが 2026-08-21 の芯。</summary>
    [Fact]
    public void 払うとステが減って回数がもらえる()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 500, farReq: 1);
        var raid = AtHub(trail, Rich());
        int gate = trail.Squares[0].Ways[0].To;
        int rolls = raid.Rolls;
        int had = Trails.Usable(raid, StatKey.Atk);

        Trails.Go(raid, new[] { raid.At, gate });
        Trails.Pay(raid);

        Assert.Equal(rolls + 1, raid.Rolls);
        Assert.Equal(had - 500, Trails.Usable(raid, StatKey.Atk));
        // ⚠️ 財布そのものは動かさない（使ったぶんは別に持つ）
        Assert.Equal(9999, raid.Pool.Atk);
        Assert.Equal(500, raid.Spent.Atk);
    }

    /// <summary>⭐ **払わなければ、何も起きずに進む。**</summary>
    [Fact]
    public void 払わなければ何も起きない()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 500, farReq: 1);
        var raid = AtHub(trail, Rich());
        int rolls = raid.Rolls;

        Trails.Go(raid, new[] { raid.At, trail.Squares[0].Ways[0].To });
        Assert.Equal(RaidStep.Moved, Trails.Pass(raid));
        Assert.Equal(rolls, raid.Rolls);
        Assert.Equal(0, raid.Spent.Atk);
    }

    /// <summary>⚠️ **同じ関門で二度は払えない。**</summary>
    [Fact]
    public void 同じ関門で二度は払えない()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 500, farReq: 1);
        var raid = AtHub(trail, Rich());
        int gate = trail.Squares[0].Ways[0].To;
        Trails.Go(raid, new[] { raid.At, gate });
        Trails.Pay(raid);
        Assert.False(Trails.CanPay(raid, gate));
    }

    /// <summary>⭐ **距離をもらうと、振らずにもう一度選べる。**
    /// ⚠️ 画面に新しい仕掛けが要らないよう、行ける先を並べる段（Choosing）へ戻す。</summary>
    [Fact]
    public void 距離をもらうと振らずにもう一度進める()
    {
        var squares = new List<Square>();
        for (int i = 0; i < 12; i++) squares.Add(new Square());
        squares[1] = Hopper(GimmickKind.Wall, 100, hop: 3);
        for (int i = 0; i + 1 < 12; i++) squares[i].Ways.Add(new Way(i + 1));
        var trail = new Trail(squares, 1, new List<int>());

        var raid = AtHub(trail, Rich());
        int rolls = raid.Rolls;
        Trails.Go(raid, new[] { 0, 1 });
        Assert.Equal(RaidStep.Choosing, Trails.Pay(raid));
        Assert.Equal(3, raid.Pending);
        Assert.Equal(rolls, raid.Rolls);          // ⚠️ 回数は使っていない

        Trails.Go(raid, Trails.Reach(raid, raid.Pending)[0]);
        Assert.Equal(4, raid.At);                 // ⭐ 1 → 4 へ3マス
    }

    // ── 一時的な増減 ──────────────────────────────

    /// <summary>⭐ ▲ は**払えなかった関門を払えるようにする**。
    /// ⚠️ ここが「▲ に止まりたい」の中身（2026-08-21）。</summary>
    [Fact]
    public void 増減で払える関門が変わる()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 1200, farReq: 100);
        var raid = AtHub(trail, new StatBlock(999, 1000, 999, 0));
        int gate = trail.Squares[0].Ways[0].To;
        Assert.False(Trails.CanPay(raid, gate));

        raid.Temp = raid.Temp.With(StatKey.Atk, 30);
        raid.TempLeft = raid.TempLeft.With(StatKey.Atk, 3);
        Assert.True(Trails.CanPay(raid, gate));      // 1000 × 1.3 = 1300

        // ⚠️ ▼ なら逆に届かなくなる
        raid.Temp = raid.Temp.With(StatKey.Atk, -30);
        Assert.False(Trails.CanPay(raid, gate));
    }

    /// <summary>⚠️ 増減は振った回数で切れる。</summary>
    [Fact]
    public void 増減は振った回数で切れる()
    {
        var trail = Line(20, new Dictionary<int, Square>
        {
            [1] = Swing(StatKey.Atk, 50, 2),
        });
        var raid = new Raid(trail, Party(), rolls: 20, pool: Rich());

        Advance(raid, 1);
        Assert.Equal(50, raid.Temp.Atk);
        Assert.Equal(2, raid.TempLeft.Atk);

        Advance(raid, 1);
        Assert.Equal(1, raid.TempLeft.Atk);
        Advance(raid, 1);
        Assert.Equal(0, raid.TempLeft.Atk);
        Assert.Equal(0, raid.Temp.Atk);       // ⚠️ 札そのものも消す
    }

    /// <summary>⚠️ **通り抜けただけのマスは効かない**（止まったときだけ）。</summary>
    [Fact]
    public void 通り抜けたマスは効かない()
    {
        var trail = Line(20, new Dictionary<int, Square>
        {
            [1] = Swing(StatKey.Atk, 50, 5),
            [2] = Swing(StatKey.Def, 50, 5),
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
        var trail = Line(20, new Dictionary<int, Square> { [1] = Square.Mob() });
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
        var trail = Line(20, new Dictionary<int, Square> { [3] = Square.Mob() });
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
        var trail = Line(20, new Dictionary<int, Square> { [1] = Square.Mob() });
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
        Advance(raid, 6);
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

        Assert.Throws<InvalidOperationException>(() =>
            Trails.Go(raid, new[] { raid.At, trail.Squares[raid.At].Ways[0].To }));
        Assert.Throws<InvalidOperationException>(() => Trails.Beat(raid));
        Assert.Throws<InvalidOperationException>(() => Trails.Lost(raid));

        // ⭐ 振ったあとは「行ける先を選ぶ」段。⚠️ そこで続けて振れない
        Trails.Roll(new Rng(1), raid);
        Assert.Equal(RaidStep.Choosing, raid.Step);
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
    public void 残りマス数は関門を数に入れない()
    {
        // 近い道 2マス（払い 1200）／遠い道 4マス（払い 100）
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 1200, farReq: 100,
            nearGate: GimmickKind.Wall, farGate: GimmickKind.Pressure);

        // ⭐ **払えても払えなくても同じ数。**関門は道を塞がないので、
        //    残りマス数は「盤の形」だけで決まる（2026-08-21）。
        var strong = AtHub(trail, new StatBlock(999, 1500, 999, 0));
        var weak = AtHub(trail, new StatBlock(999, 100, 999, 0));
        Assert.Equal(2, Trails.Left(strong));
        Assert.Equal(2, Trails.Left(weak));

        Assert.Equal(1, Trails.LeftFrom(trail, trail.Squares[0].Ways[0].To));
        Assert.Equal(3, Trails.LeftFrom(trail, trail.Squares[0].Ways[1].To));
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

    /// <summary>⭐ 卵までの最短マス数が出る。⚠️ 画面はこれを出す（%ではなく）。</summary>
    [Fact]
    public void 卵までの最短マス数が出る()
    {
        var trail = Ladder(gap: 0, shortLen: 1, longLen: 3, nearReq: 1, farReq: 1);
        // ⭐ 入口から: 近い道（1マス）→ 合流 ＝ 2
        Assert.Equal(2, Trails.LeftFrom(trail, 0));
        Assert.Equal(0, Trails.LeftFrom(trail, trail.Goal));
    }

    // ── 道具 ─────────────────────────────────────

    private static StatBlock Rich() => new StatBlock(9999, 9999, 9999, 0);

    /// <summary>入口がいきなり分かれ道の盤で始める。
    /// ⚠️ <see cref="Raid"/> を素で作ると <see cref="RaidStep.Moved"/> のままなので、
    /// <see cref="Trails.Begin"/> と同じ形に揃える。</summary>
    private static Raid AtHub(Trail trail, StatBlock pool, int rolls = 9)
    {
        var raid = new Raid(trail, Party(), rolls, pool);
        // ⚠️ 分かれ道でも止まらなくなったので、「行ける先を選ぶ」段に揃える（2026-08-20）
        raid.Step = RaidStep.Choosing;
        raid.Pending = 1;
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

        // ⭐ 関門は**マス**（2026-08-20）。⚠️ 道の先頭マスを関門にする
        if (nearReq > 0) squares[nearHead] = Gated(nearGate, nearReq);
        if (farReq > 0) squares[farHead] = Gated(farGate, farReq);
        squares[hub].Ways.Add(new Way(nearHead));
        squares[hub].Ways.Add(new Way(farHead));
        for (int i = 0; i < shortLen; i++)
            squares[nearHead + i].Ways.Add(new Way(i + 1 < shortLen ? nearHead + i + 1 : join));
        for (int i = 0; i < longLen; i++)
            squares[farHead + i].Ways.Add(new Way(i + 1 < longLen ? farHead + i + 1 : join));

        var made = new Trail(squares, 1, new List<int> { hub });
        // ⚠️ 段は検査が見るので、素直に振っておく
        for (int i = 0; i < squares.Count; i++) squares[i].Row = Depth(made, i);
        return made;
    }

    /// <summary>関門のマス。⭐ 払うと**振れる回数 +1**。</summary>
    private static Square Gated(GimmickKind gate, int price, int grade = 1) =>
        Square.Gate(new Toll(gate, price, grade), new Gift(GiftKind.Rolls, 1));

    /// <summary>関門のマス。⭐ 払うと**その場で N マス進める**。</summary>
    private static Square Hopper(GimmickKind gate, int price, int hop) =>
        Square.Gate(new Toll(gate, price, 1), new Gift(GiftKind.Hop, hop));

    /// <summary>▲ / ▼ のマス。</summary>
    private static Square Swing(StatKey key, int percent, int turns) =>
        Square.Swing(key, percent, turns);

    /// <summary>入口からの段（一番短い辿り方）。</summary>
    private static int Depth(Trail trail, int at)
    {
        var deep = new int[trail.Count];
        for (int i = 0; i < trail.Count; i++) deep[i] = -1;
        deep[0] = 0;
        for (int i = 0; i < trail.Count; i++)
        {
            if (deep[i] < 0) continue;
            foreach (var way in trail.Squares[i].Ways)
                if (deep[way.To] < 0 || deep[i] + 1 < deep[way.To]) deep[way.To] = deep[i] + 1;
        }
        return deep[at] < 0 ? 0 : deep[at];
    }
}
