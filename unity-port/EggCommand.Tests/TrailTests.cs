using System;
using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>すごろく式の潜入（<see cref="Trail"/>）。
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

    /// <summary>盤の中身に関わらず最後まで回す。⭐ 止まらないことの確認も兼ねる。</summary>
    private static void Play(Rng rng, Raid raid, Func<Raid, bool>? take = null)
    {
        int guard = 0;
        while (raid.Result == null)
        {
            if (++guard > 10_000) throw new InvalidOperationException("潜入が終わらない");
            switch (raid.Step)
            {
                case RaidStep.AtFork:
                    if (Trails.CanBreak(raid) && (take == null || take(raid))) Trails.Break(raid);
                    else Trails.Walk(raid);
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
        var slow = Party(0);
        var fast = Party(30);
        Assert.True(Trails.RollsFor(fast) > Trails.RollsFor(slow));

        // ⚠️ 誰が速いかは効かない（3体で1つの駒なので）
        var lopsided = new List<Creature>
        {
            Make("x", 20, 20, 20, 45), Make("y", 20, 20, 20, 15), Make("z", 20, 20, 20, 0),
        };
        var even = new List<Creature>
        {
            Make("x", 20, 20, 20, 20), Make("y", 20, 20, 20, 20), Make("z", 20, 20, 20, 20),
        };
        Assert.Equal(Trails.RollsFor(even), Trails.RollsFor(lopsided));
    }

    /// <summary>⚠️ 速度 0 でも 1回は振れる（何もできない潜入を作らない）。</summary>
    [Fact]
    public void 速度がどれだけ低くても一度は振れる()
    {
        var still = new List<Creature> { Make("a", 0, 0, 0, 0) };
        Assert.True(Trails.RollsFor(still) >= 1);
    }

    /// <summary>⚠️ 道の長さを編成の速さで変えてはいけない（変えると速さが打ち消される）。</summary>
    [Fact]
    public void 道の長さは段だけで決まる()
    {
        for (int tier = 1; tier <= 5; tier++)
        {
            var a = Trails.Make(new Rng(1), tier);
            var b = Trails.Make(new Rng(99), tier);
            Assert.Equal(Trail.LengthFor(tier), a.Length);
            Assert.Equal(a.Length, b.Length);
        }
    }

    /// <summary>⭐ 巣の寿命。**盗まれた巣ほど親が早く帰ってくる。**
    ///
    /// ⚠️ 載せ替えのときここが落ちていて、4回で封鎖という巣の寿命が
    /// 丸ごと働いていなかった（2026-08-20 に気づいて足した）。</summary>
    [Fact]
    public void 盗んだ回数だけ振れる回数が減る()
    {
        var party = Party(30);
        int fresh = Trails.RollsFor(party);
        for (int raids = 1; raids < Steal.RaidsToSeal; raids++)
            Assert.Equal(fresh - raids * Trail.RollsLostPerRaid, Trails.RollsFor(party, raids));

        // ⚠️ どれだけ盗まれても 1回は振れる（何もできない潜入を作らない）
        Assert.True(Trails.RollsFor(Party(0), 99) >= 1);

        // ⭐ 盤の形は変えない（下見して編成を選べることが芯）
        var a = Trails.OfNest(Nests.All[0]);
        var b = Trails.OfNest(Nests.All[0]);
        Assert.Equal(a.Length, b.Length);
        Assert.Equal(Trails.Begin(a, party, 0).Trail.Length,
                     Trails.Begin(b, party, 3).Trail.Length);
    }

    // ── 分かれ道 ──────────────────────────────────

    /// <summary>⭐ 分かれ道は**踏まなくても、跨ごうとすると止まる**。
    /// ⚠️ 踏んだときだけ効く物にしたら、判断が1回の潜入で 0.65 回しか起きなかった。</summary>
    [Fact]
    public void 分かれ道は跨ごうとすると止まる()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 12; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[1] = new Square(SquareKind.Fork, GimmickKind.Wall, requires: 999, saves: 5);
        var trail = new Trail(spaces, 1);

        var raid = new Raid(trail, Party(), rolls: 5, pool: new StatBlock(0, 0, 0, 0));
        // ⭐ 6 を振っても、1マス目の分かれ道で止まる
        Assert.Equal(RaidStep.AtFork, Advance(raid, 6));
        Assert.Equal(1, raid.At);
        Assert.Equal(5, raid.Pending);   // ⚠️ 使い残した目は消えていない
    }

    /// <summary>⭐ 歩けば残った目のぶん進む（目を捨てない）。</summary>
    [Fact]
    public void 歩けば残った目のぶん進む()
    {
        var raid = ForkAt(1, requires: 999, saves: 5);
        Advance(raid, 4);
        Assert.Equal(1, raid.At);
        Trails.Walk(raid);
        Assert.Equal(4, raid.At);        // 1 + 残り3
    }

    /// <summary>⭐ 壊せば飛ぶ。⚠️ **残った目は捨てる** ── だから出目が小さいほど得。</summary>
    [Fact]
    public void 壊せば飛ぶが残った目は捨てる()
    {
        var raid = ForkAt(1, requires: 100, saves: 5);
        Advance(raid, 4);
        Assert.True(Trails.CanBreak(raid));
        Trails.Break(raid);
        Assert.Equal(6, raid.At);        // 1 + 5。⚠️ 残り3は乗らない
        Assert.Equal(0, raid.Pending);
    }

    /// <summary>⚠️ 一度歩いて通り過ぎた分かれ道では、二度は止まらない。</summary>
    [Fact]
    public void 通り過ぎた分かれ道では二度止まらない()
    {
        var raid = ForkAt(1, requires: 999, saves: 5);
        Advance(raid, 1);
        Trails.Walk(raid);
        Assert.Equal(1, raid.At);
        Assert.Equal(RaidStep.Moved, raid.Step);
        // ⭐ 同じマスから進み直しても止まらない
        Assert.NotEqual(RaidStep.AtFork, Advance(raid, 2));
    }

    /// <summary>⭐ 払った量は財布から減り、**戻らない**。</summary>
    [Fact]
    public void 壊すと財布が減る()
    {
        var raid = ForkAt(1, requires: 300, saves: 5);
        int before = raid.Power;
        Advance(raid, 1);
        Trails.Break(raid);
        Assert.True(raid.Power < before);
        Assert.Equal(before - Trails.CostOf(raid, raid.Trail.Squares[1]), raid.Power);
    }

    /// <summary>⚠️ 払えないのに壊そうとしたら投げる（黙って通さない）。</summary>
    [Fact]
    public void 払えない分かれ道は壊せない()
    {
        var raid = ForkAt(1, requires: 999_999, saves: 5);
        Advance(raid, 1);
        Assert.False(Trails.CanBreak(raid));
        Assert.Throws<InvalidOperationException>(() => Trails.Break(raid));
    }

    // ── 寄せた編成 ────────────────────────────────

    /// <summary>⭐ 遊びの芯。**寄せたステの関門は安く、寄せていない関門は高い。**
    ///
    /// ⚠️ 素質は1体3ステまでしか上限に届かないので、1本を厚くすると別の1本が薄くなる。
    /// ここが噛み合わないと「寄せる意味が無い」ことになる（実際そうなっていた）。</summary>
    [Fact]
    public void 寄せたステの関門は安く済む()
    {
        var raid = ForkAt(1, requires: 1000, saves: 5);
        int flat = Trails.CostOf(raid, raid.Trail.Squares[1]);

        // ⭐ 攻撃だけ2倍にする（＝壁に寄せた編成）
        raid.Pool = raid.Pool.With(StatKey.Atk, raid.Pool.Atk * 2);
        int slanted = Trails.CostOf(raid, raid.Trail.Squares[1]);
        Assert.True(slanted < flat);

        // ⚠️ 逆に薄いと高くつく
        raid.Pool = raid.Pool.With(StatKey.Atk, raid.Pool.Atk / 4);
        Assert.True(Trails.CostOf(raid, raid.Trail.Squares[1]) > flat);
    }

    /// <summary>⭐ 一時的な増減は**値段に効く**（「いまなら壊せる」を作る）。</summary>
    [Fact]
    public void 一時的な増減は値段を動かす()
    {
        var raid = ForkAt(1, requires: 1000, saves: 5);
        int plain = Trails.CostOf(raid, raid.Trail.Squares[1]);

        raid.Temp = raid.Temp.With(StatKey.Atk, 50);
        raid.TempLeft = raid.TempLeft.With(StatKey.Atk, 2);
        Assert.True(Trails.CostOf(raid, raid.Trail.Squares[1]) < plain);

        raid.Temp = raid.Temp.With(StatKey.Atk, -50);
        Assert.True(Trails.CostOf(raid, raid.Trail.Squares[1]) > plain);
    }

    /// <summary>⚠️ 増減は振るたびに1つ減り、切れたら元に戻る。</summary>
    [Fact]
    public void 増減は振った回数で切れる()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 40; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[1] = new Square(SquareKind.Boon, stat: StatKey.Atk, percent: 50, rolls: 2);
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 20, pool: Pool());

        Advance(raid, 1);
        Assert.Equal(50, raid.Temp.Atk);
        Assert.Equal(2, raid.TempLeft.Atk);

        Trails.Roll(new Rng(1), raid);
        Assert.Equal(1, raid.TempLeft.Atk);
        Trails.Roll(new Rng(2), raid);
        Assert.Equal(0, raid.TempLeft.Atk);
        Assert.Equal(0, raid.Temp.Atk);       // ⚠️ 札そのものも消す（残ると桁が読めない）
    }

    // ── 雑魚 ─────────────────────────────────────

    /// <summary>⭐ 雑魚は Core では決着しない。⚠️ 呼び側が戦闘を回して <see cref="Trails.Beat"/>。</summary>
    [Fact]
    public void 雑魚は呼び側が決着させる()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 20; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[1] = new Square(SquareKind.Mob);
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 3, pool: Pool());

        Assert.Equal(RaidStep.Met, Advance(raid, 1));
        Assert.Null(raid.Result);

        int had = raid.Rolls;
        Trails.Beat(raid);
        Assert.Equal(had + Trail.MobRefund, raid.Rolls);   // ⭐ 振れる回数が戻る
        Assert.Equal(RaidStep.Moved, raid.Step);
    }

    /// <summary>⚠️ 雑魚に負けたらそこで見つかる。</summary>
    [Fact]
    public void 雑魚に負けたら見つかる()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 20; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[1] = new Square(SquareKind.Mob);
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 3, pool: Pool());
        Advance(raid, 1);
        Trails.Lost(raid);
        Assert.Equal(StealOutcome.Blocked, raid.Result);
    }

    /// <summary>⚠️ 一度倒した雑魚とは、戻ってきても戦わない。</summary>
    [Fact]
    public void 倒した雑魚とは二度戦わない()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 20; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[3] = new Square(SquareKind.Mob);
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 9, pool: Pool());
        Advance(raid, 3);
        Trails.Beat(raid);
        raid.At = 2;                                  // ⚠️ 検証のため戻す
        Assert.NotEqual(RaidStep.Met, Advance(raid, 1));
    }

    // ── 決着 ─────────────────────────────────────

    /// <summary>⭐ 振り切って届かなければ親が帰ってくる（作者の決定）。</summary>
    [Fact]
    public void 振り切って届かなければ見つかる()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 40; i++) spaces.Add(new Square(SquareKind.Plain));
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 1, pool: Pool());
        Trails.Roll(new Rng(7), raid);
        Assert.Equal(StealOutcome.Stalled, raid.Result);
    }

    /// <summary>⭐ 卵まで届いたら成功。⚠️ 行き過ぎても届いた扱い（戻される遊びにしない）。</summary>
    [Fact]
    public void 届けば成功で行き過ぎても戻されない()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 3; i++) spaces.Add(new Square(SquareKind.Plain));
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 5, pool: Pool());
        Advance(raid, 6);
        Assert.Equal(StealOutcome.Success, raid.Result);
        Assert.Equal(3, raid.At);
    }

    /// <summary>⚠️ 決着した潜入をさらに動かそうとしたら投げる。</summary>
    [Fact]
    public void 決着した潜入は動かせない()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 3; i++) spaces.Add(new Square(SquareKind.Plain));
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 5, pool: Pool());
        Advance(raid, 6);
        Assert.Throws<InvalidOperationException>(() => Trails.Roll(new Rng(1), raid));
    }

    // ── 盤の作り ──────────────────────────────────

    /// <summary>⭐ 巣ごとに道が固定される（＝下見できる）。
    /// ⚠️ 毎回引き直すと、画面を出入りするだけで道を選び直せてしまう。</summary>
    [Fact]
    public void 巣の道は何度作っても同じ()
    {
        foreach (var nest in Nests.All)
        {
            var a = Trails.OfNest(nest);
            var b = Trails.OfNest(nest);
            Assert.Equal(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.Equal(a.Squares[i].Kind, b.Squares[i].Kind);
                Assert.Equal(a.Squares[i].Gate, b.Squares[i].Gate);
                Assert.Equal(a.Squares[i].Requires, b.Squares[i].Requires);
                Assert.Equal(a.Squares[i].Saves, b.Squares[i].Saves);
            }
        }
        // ⚠️ 巣ごとに違う道であること（全部同じでは下見が意味を持たない）
        var seen = new HashSet<string>();
        foreach (var nest in Nests.All)
        {
            var t = Trails.OfNest(nest);
            var key = "";
            foreach (var sp in t.Squares) key += $"{(int)sp.Kind}:{sp.Requires},";
            seen.Add(key);
        }
        Assert.True(seen.Count > 1);
    }

    /// <summary>⭐ 盤の不変条件。⚠️ ここが崩れると較正した確率が全部ずれる。</summary>
    [Fact]
    public void 盤の不変条件()
    {
        var rng = new Rng(4242);
        for (int tier = 1; tier <= 5; tier++)
            for (int n = 0; n < 300; n++)
            {
                var trail = Trails.Make(rng, tier);
                int forks = 0, last = -99;
                for (int i = 0; i < trail.Length; i++)
                {
                    var sp = trail.Squares[i];
                    if (sp.Kind != SquareKind.Fork) continue;
                    forks++;

                    // ⚠️ 入口と卵の直前には置かない（選ぶ余地が要る）
                    Assert.True(i >= 2, $"分かれ道が入口に近すぎる: {i}");
                    Assert.True(i < trail.Length - 2, $"分かれ道が卵に近すぎる: {i}");
                    // ⚠️ 隣り合うと、片方を壊した先がもう片方になって判断が潰れる
                    Assert.True(i - last >= Trail.ForkGap, $"分かれ道が隣り合っている: {last}→{i}");
                    last = i;

                    Assert.InRange(sp.Saves, Trail.SavesMin, Trail.SavesMax);
                    // ⭐ 値段は相場の 70〜130%（飛べる数とは別に振る）
                    int fair = Trail.FairPrice(tier, sp.Saves);
                    Assert.InRange(sp.Requires,
                        fair * Trail.PriceLow / 100, fair * Trail.PriceHigh / 100);
                }
                Assert.True(forks >= 2, $"段{tier} の分かれ道が {forks} 本しかない");
            }
    }

    /// <summary>⭐ 卵の直前に何も挟まない（決着に余計な物を置かない）。</summary>
    [Fact]
    public void 入口と卵の直前は素通り()
    {
        var rng = new Rng(77);
        for (int tier = 1; tier <= 5; tier++)
            for (int n = 0; n < 200; n++)
            {
                var trail = Trails.Make(rng, tier);
                Assert.Equal(SquareKind.Plain, trail.Squares[0].Kind);
                Assert.Equal(SquareKind.Plain, trail.Squares[1].Kind);
                Assert.Equal(SquareKind.Plain, trail.Squares[trail.Length - 1].Kind);
            }
    }

    /// <summary>⚠️ どんな盤・どんな指し手でも必ず終わる（無限に回らない）。</summary>
    [Fact]
    public void どんな盤でも必ず決着する()
    {
        var rng = new Rng(31337);
        for (int tier = 1; tier <= 5; tier++)
            for (int n = 0; n < 200; n++)
            {
                foreach (var greedy in new[] { true, false })
                {
                    var raid = Trails.Begin(Trails.Make(rng, tier), Party(30));
                    Play(rng, raid, _ => greedy);
                    Assert.NotNull(raid.Result);
                }
            }
    }

    // ── 見込みの出し方 ────────────────────────────

    /// <summary>⭐ 画面に出す「届く見込み」。⚠️ 端が合っていないと嘘の札になる。</summary>
    [Fact]
    public void 届く見込みの端()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 30; i++) spaces.Add(new Square(SquareKind.Plain));
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 1, pool: Pool());

        raid.At = 30;
        Assert.Equal(100, Trails.Odds(raid));            // もう届いている

        raid.At = 24;                                     // 残り6・1回振る → 6分の1
        Assert.Equal(17, Trails.Odds(raid));
        raid.At = 29;                                     // 残り1 → 必ず届く
        Assert.Equal(100, Trails.Odds(raid));
        raid.At = 23;                                     // 残り7・1回では届かない
        Assert.Equal(0, Trails.Odds(raid));

        // ⭐ 分かれ道を壊した先の見込みも出せる（壊すか歩くかの材料）
        raid.At = 18;
        Assert.True(Trails.Odds(raid, extraSteps: 6) > Trails.Odds(raid));
    }

    /// <summary>⚠️ 振れる回数が増えるほど見込みは上がる（単調）。</summary>
    [Fact]
    public void 見込みは振れる回数について単調()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 30; i++) spaces.Add(new Square(SquareKind.Plain));
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 0, pool: Pool());
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

    /// <summary>⭐ 画面に出す「壊せばここまで行ける」の目安。
    ///
    /// ⚠️ **出した数より実際が悪くなってはいけない**（＝払えない額を数えない）。
    /// 少なめに出るのは許す（最適解ではなく安い順に買うだけの見積りなので）。</summary>
    [Fact]
    public void 壊せば稼げるマス数は財布の中に収まる()
    {
        var rng = new Rng(555);
        for (int tier = 1; tier <= 5; tier++)
            for (int n = 0; n < 200; n++)
            {
                var raid = Trails.Begin(Trails.Make(rng, tier), Party(30));
                int spare = Trails.Sparable(raid);
                Assert.True(spare >= 0);

                // ⭐ 数えた本を実際に買えるか。⚠️ 安い順に足して財布を超えないこと
                var costs = new List<int>();
                var saves = new List<int>();
                for (int i = 0; i < raid.Trail.Length; i++)
                {
                    var sq = raid.Trail.Squares[i];
                    if (sq.Kind != SquareKind.Fork) continue;
                    costs.Add(Trails.CostOf(raid, sq));
                    saves.Add(sq.Saves);
                }
                // ⚠️ 全部買える上限より多く数えていないこと
                int all = 0;
                for (int i = 0; i < saves.Count; i++) all += saves[i];
                Assert.True(spare <= all, $"稼げる数 {spare} が全部の合計 {all} を超えた");

                // ⚠️ 一番安い1本すら買えないなら 0 のはず
                int cheapest = int.MaxValue;
                foreach (var c in costs) if (c < cheapest) cheapest = c;
                if (costs.Count > 0 && cheapest > raid.Power) Assert.Equal(0, spare);
            }
    }

    /// <summary>⚠️ 壊した／通り過ぎた分かれ道は、もう数えない。</summary>
    [Fact]
    public void 済んだ分かれ道は見積りに数えない()
    {
        var raid = ForkAt(1, requires: 100, saves: 5);
        Assert.Equal(5, Trails.Sparable(raid));
        Advance(raid, 1);
        Trails.Walk(raid);
        Assert.Equal(0, Trails.Sparable(raid));
    }

    // ── レビューで見つかった穴（2026-08-20） ──────

    /// <summary>⚠️ **最後の1振りが分かれ道で止まったとき、見込みが嘘をついていた。**
    ///
    /// `Odds` の中で <see cref="Raid.Pending"/> を足しつつ、呼び側も足していたので
    /// 同じ目を二重に数え、「壊しても歩いても 100%」になっていた
    /// （実際はどちらも届かず親と戦闘）。⭐ 足すのは**呼び側だけ**。</summary>
    [Fact]
    public void 振り切ったあとの見込みは使い残した目を二重に数えない()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 10; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[6] = new Square(SquareKind.Fork, GimmickKind.Wall, requires: 1, saves: 3);
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 1, pool: Pool());
        raid.At = 2;

        Advance(raid, 6);                          // 2 → 6 で分かれ道。残り2、振る回数 0
        Assert.Equal(RaidStep.AtFork, raid.Step);
        Assert.Equal(0, raid.Rolls);
        Assert.Equal(2, raid.Pending);

        // ⭐ 呼び側が渡した数だけを足す。⚠️ どちらも卵（10）には届かない
        Assert.Equal(0, Trails.Odds(raid, raid.Pending));
        Assert.Equal(0, Trails.Odds(raid, raid.Trail.Squares[raid.At].Saves));
        Assert.Equal(0, Trails.Odds(raid));

        // ⚠️ 実際に歩いても壊しても届かないこと（画面の数字と同じ結末になる）
        Trails.Walk(raid);
        Assert.Equal(StealOutcome.Stalled, raid.Result);
    }

    /// <summary>⚠️ 分かれ道は跨ぐのが普通なので、**壊した先に別の分かれ道がある**。
    /// ⭐ 画面が指す印は `At + Saves` ではなく <see cref="Trails.LandingOf"/>。</summary>
    [Fact]
    public void 壊した先に別の分かれ道があればそこで止まる()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 20; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[2] = new Square(SquareKind.Fork, GimmickKind.Wall, requires: 1, saves: 8);
        spaces[4] = new Square(SquareKind.Fork, GimmickKind.Damage, requires: 1, saves: 3);
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 9, pool: Pool());

        Advance(raid, 2);
        Assert.Equal(RaidStep.AtFork, raid.Step);
        // ⭐ +8 と書いてあっても、実際に着くのは4マス目
        Assert.Equal(4, Trails.LandingOf(raid));
        Trails.Break(raid);
        Assert.Equal(4, raid.At);
        Assert.Equal(RaidStep.AtFork, raid.Step);

        // ⭐ **飛びかけて途中で止まったぶんは残る**（8 のうち 2 進んで残り 6）。
        // ⚠️ 「壊すと残った目は捨てる」のは**さいころの目**のほうで、
        //    飛べる数そのものではない。⭐ ここで捨てると、跨いだ先の分かれ道が
        //    「壊すしかない」場所になり、選ぶ余地が消える。
        Assert.Equal(6, raid.Pending);
        // ⚠️ つまり次の分かれ道では「歩けば +6」── 壊す(+3)より得
        Assert.Equal(4 + 3, Trails.LandingOf(raid));
        Assert.Equal(4 + 6, Trails.WalkingTo(raid));
    }

    /// <summary>⭐ 歩くほうも同じ ── 途中に分かれ道があればそこで止まる。</summary>
    [Fact]
    public void 歩いた先に分かれ道があればそこで止まる()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 20; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[2] = new Square(SquareKind.Fork, GimmickKind.Wall, requires: 999_999, saves: 5);
        spaces[4] = new Square(SquareKind.Fork, GimmickKind.Damage, requires: 999_999, saves: 5);
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 9, pool: Pool());

        Advance(raid, 2);
        raid.Pending = 5;                          // ⚠️ 試験のため大きい目を残す
        Assert.Equal(4, Trails.WalkingTo(raid));
        Trails.Walk(raid);
        Assert.Equal(4, raid.At);
        Assert.Equal(RaidStep.AtFork, raid.Step);
        Assert.Equal(3, raid.Pending);             // 5 のうち 2 使って残り3
    }

    /// <summary>⚠️ 分かれ道を**踏んで**止まったとき（残りの目が 0）。
    /// ⭐ 歩いても進まないが、通り過ぎた扱いになって次から止まらない。</summary>
    [Fact]
    public void 踏んで止まった分かれ道を歩くと進まないが通過扱いになる()
    {
        var raid = ForkAt(3, requires: 999_999, saves: 5);
        Advance(raid, 3);
        Assert.Equal(RaidStep.AtFork, raid.Step);
        Assert.Equal(0, raid.Pending);

        Trails.Walk(raid);
        Assert.Equal(3, raid.At);                  // ⚠️ 1マスも進まない
        Assert.Equal(RaidStep.Moved, raid.Step);
        Assert.Contains(3, raid.Passed);
    }

    /// <summary>⚠️ 分かれ道の本数が、意図（<see cref="Trail.ForksFor"/>）どおり出ているか。
    ///
    /// ⚠️ 引いてから隣接分を間引くだけだと、実際の本数が下振れする
    /// （段3で「4本」のはずが 2本 2.3% / 3本 43% だった。レビューで実測 2026-08-20）。
    /// ⭐ 較正（`PriceShare`）が「1回の潜入で 4.3 回跨ぐ」を前提にしているので、
    /// ここが下振れすると値段の意味が変わる。</summary>
    [Fact]
    public void 分かれ道は意図した本数だけ置かれる()
    {
        var rng = new Rng(20260820);
        for (int tier = 1; tier <= 5; tier++)
        {
            int want = Trail.ForksFor(Trail.LengthFor(tier));
            for (int n = 0; n < 500; n++)
            {
                var trail = Trails.Make(rng, tier);
                int forks = 0;
                foreach (var sq in trail.Squares) if (sq.Kind == SquareKind.Fork) forks++;
                Assert.True(forks == want,
                    $"段{tier}: 分かれ道が {forks} 本（{want} 本のはず）");
            }
        }
    }

    /// <summary>⚠️ 最後の1振りが雑魚に当たったら、そこで終わらせない。
    /// ⭐ 倒せば回数が戻るので、まだ続けられる。</summary>
    [Fact]
    public void 振り切ったあとの雑魚は倒せば続けられる()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 20; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[3] = new Square(SquareKind.Mob);
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 1, pool: Pool());

        Assert.Equal(RaidStep.Met, Advance(raid, 3));
        Assert.Null(raid.Result);                  // ⚠️ 回数 0 でも決着させない
        Assert.Equal(0, raid.Rolls);

        Trails.Beat(raid);
        Assert.Equal(1, raid.Rolls);               // ⭐ 戻った
        Assert.Equal(RaidStep.Moved, raid.Step);
        Assert.Null(raid.Result);
    }

    /// <summary>⚠️ 間違った場面で呼んだら黙って通さない。</summary>
    [Fact]
    public void 場面ちがいの操作は投げる()
    {
        var raid = ForkAt(1, requires: 999_999, saves: 5);
        // まだ振っていない（Moved）ので、分かれ道と雑魚の操作は通らない
        Assert.Throws<InvalidOperationException>(() => Trails.Walk(raid));
        Assert.Throws<InvalidOperationException>(() => Trails.Beat(raid));
        Assert.Throws<InvalidOperationException>(() => Trails.Lost(raid));
        Assert.False(Trails.CanBreak(raid));

        Advance(raid, 1);                          // AtFork
        Assert.Throws<InvalidOperationException>(() => Trails.Roll(new Rng(1), raid));
        Assert.Throws<InvalidOperationException>(() => Trails.Beat(raid));
        Assert.Throws<InvalidOperationException>(() => Trails.Lost(raid));
    }

    /// <summary>⭐ 壊して出た先のマスも、踏んだマスとして効く。</summary>
    [Fact]
    public void 壊して出た先の効き目も乗る()
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 20; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[1] = new Square(SquareKind.Fork, GimmickKind.Wall, requires: 1, saves: 5);
        spaces[6] = new Square(SquareKind.Boon, stat: StatKey.Atk, percent: 50, rolls: 3);
        var raid = new Raid(new Trail(spaces, 1), Party(), rolls: 9, pool: Pool());

        Advance(raid, 1);
        Trails.Break(raid);
        Assert.Equal(6, raid.At);
        Assert.Equal(50, raid.Temp.Atk);            // ⭐ 出た先の ▲ が効いている

        // ⚠️ 出た先が雑魚なら、そこで戦闘になる
        spaces[6] = new Square(SquareKind.Mob);
        var second = new Raid(new Trail(spaces, 1), Party(), rolls: 9, pool: Pool());
        Advance(second, 1);
        Assert.Equal(RaidStep.Met, Trails.Break(second));
    }

    /// <summary>⚠️ 持ち分が 0 でも値段が壊れない（0除算・青天井にしない）。</summary>
    [Fact]
    public void 持ち分が空でも値段が壊れない()
    {
        var raid = ForkAt(1, requires: 1000, saves: 5);
        raid.Pool = new StatBlock(0, 0, 0, 0);
        raid.Power = 0;

        foreach (var gate in new[] { GimmickKind.Wall, GimmickKind.Damage, GimmickKind.Pressure })
        {
            int slant = Trails.SlantOf(raid, gate);
            Assert.True(slant >= 10, $"値引き率が {slant} まで落ちた（下限 10 のはず）");
        }
        int cost = Trails.CostOf(raid, raid.Trail.Squares[1]);
        Assert.True(cost > 0 && cost <= 1000 * 10);
        Advance(raid, 1);
        Assert.False(Trails.CanBreak(raid));
    }

    // ── 道具 ─────────────────────────────────────

    private static StatBlock Pool() => new StatBlock(900, 800, 1100, 0);

    /// <summary>1マス目に分かれ道を置いた盤。</summary>
    private static Raid ForkAt(int at, int requires, int saves)
    {
        var spaces = new List<Square>();
        for (int i = 0; i < 30; i++) spaces.Add(new Square(SquareKind.Plain));
        spaces[at] = new Square(SquareKind.Fork, GimmickKind.Wall, requires: requires, saves: saves);
        return new Raid(new Trail(spaces, 1), Party(), rolls: 9, pool: Pool());
    }

    /// <summary>その出目だけを返す <see cref="Rng"/>。
    ///
    /// ⚠️ 潜入を巻き戻して振り直す作りにしてはいけない ── <see cref="Raid.Passed"/> のような
    /// 「進んだ跡」まで消えて、跡を見る規則が検査できなくなる（実際そうなっていた）。
    /// ⭐ 種のほうを選べば、潜入には一切触らずに出目を決められる。</summary>
    private static Rng RngFor(int pips)
    {
        for (int seed = 0; seed < 100_000; seed++)
            if (new Rng(seed).Int(0, Trail.Pips) == pips - 1) return new Rng(seed);
        throw new InvalidOperationException($"出目 {pips} を出す種が見つからない");
    }

    /// <summary>出目を決め打ちして進める。</summary>
    private static RaidStep Advance(Raid raid, int pips)
    {
        var step = Trails.Roll(RngFor(pips), raid);
        Assert.Equal(pips, raid.LastRoll);
        return step;
    }
}
