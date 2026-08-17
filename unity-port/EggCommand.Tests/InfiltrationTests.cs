using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>潜入（リレー方式の発射フェーズ）。
///
/// ⭐ 芯は「速さは強さではなく、**どこで消費するか**の資源」。
/// 速い個体を先に使えば前線基地ができるが、最終区間の飛距離を失う。
///
/// ⚠️ 移植元（TS）にリレーも関門も無いので、**goldens では守れない**。ここが唯一の見張り。</summary>
public class InfiltrationTests
{
    /// <summary>ステだけ指定した個体。⚠️ 素質（Wild）で作る — 実値は種族基礎ぶん上に出る。</summary>
    private static Creature Make(string id, int hp, int atk, int def, int spd) =>
        new Creature(id, "tamaru", new StatBlock(hp, atk, def, spd),
            new StatBlock(0, 0, 0, 0), 0, 0, null, null, 0, null, null, 1);

    private static int SpdOf(Creature c) => Creatures.StatsOf(c).Spd;

    // ── 飛距離が個体ごとになった ─────────────────────

    /// <summary>⭐ 課題「3体ぶんの速さで1体が飛ぶ理屈が画面から読めない」への答え。</summary>
    [Fact]
    public void 飛距離は飛ぶ個体の速度だけで決まる()
    {
        var slow = Make("slow", 20, 20, 20, 0);
        var fast = Make("fast", 20, 20, 20, 30);

        Assert.Equal(SpdOf(slow) * Steal.SpeedToDistance, Steal.DistanceFor(slow));
        Assert.Equal(SpdOf(fast) * Steal.SpeedToDistance, Steal.DistanceFor(fast));
        Assert.True(Steal.DistanceFor(fast) > Steal.DistanceFor(slow));
    }

    /// <summary>⭐ 3回に分けても合計は変わらない。
    /// ⚠️ ここが崩れると <see cref="Steal.DepthForTier"/> の較正が無効になる。</summary>
    [Fact]
    public void 三体ぶんの合計は編成合計と同じ()
    {
        var party = new List<Creature>
        {
            Make("a", 20, 20, 20, 10),
            Make("b", 20, 20, 20, 20),
            Make("c", 20, 20, 20, 30),
        };
        double each = 0;
        foreach (var c in party) each += Steal.DistanceFor(c);
        Assert.Equal(Steal.DistanceFor(party), each);
    }

    // ── リレー ──────────────────────────────────────

    /// <summary>関門を外した盤。⚠️ リレーの仕組みだけを見たいときに使う
    /// （関門で止まると「着地した理由」が混ざって、何を測っているか分からなくなる）。</summary>
    private static StealField Plain(int tier)
    {
        var f = Steal.MakeField(tier, FieldSide.Right);
        return new StealField(f.Height, f.Side, f.GapFrom, f.GapTo,
            f.BandTop, f.BandBottom, f.Egg, f.Start);
    }

    /// <summary>⚠️ 深い盤にする。浅い盤だと1投目が親に届いて決着してしまい、
    /// リレーそのものを検査できない。</summary>
    private static Steal.Infiltration Fresh()
    {
        var party = new List<Creature>
        {
            Make("a", 30, 30, 30, 20),
            Make("b", 30, 30, 30, 20),
            Make("c", 30, 30, 30, 20),
        };
        return new Steal.Infiltration(Plain(5), party);
    }

    /// <summary>⭐ 着地した個体は盤に残り、次の発射台になる。</summary>
    [Fact]
    public void 着地した個体が発射台になる()
    {
        var run = Fresh();
        Assert.Empty(run.Pads);

        var first = Steal.Hop(run, 0, -1, 0);
        Assert.Equal(StealOutcome.Landed, first.Outcome);
        Assert.Single(run.Pads);
        Assert.Equal(0, run.PadOwner[0]);
        Assert.Equal(first.Landing.X, run.Pads[0].X);
        Assert.Equal(first.Landing.Y, run.Pads[0].Y);

        // 2体目は着地点から飛べる
        var second = Steal.Hop(run, 1, 0, 0);
        Assert.Equal(first.Landing.X, second.Path[0].X);
        Assert.Equal(first.Landing.Y, second.Path[0].Y);
    }

    /// <summary>⚠️ 初期位置からの発射は最後まで選べる（前線が伸びても退路が消えない）。</summary>
    [Fact]
    public void 初期位置からはいつでも投げられる()
    {
        var run = Fresh();
        Steal.Hop(run, 0, -1, 0);
        var again = Steal.Hop(run, 1, -1, 0);
        Assert.Equal(run.Field.Start.X, again.Path[0].X);
        Assert.Equal(run.Field.Start.Y, again.Path[0].Y);
    }

    [Fact]
    public void 同じ個体は二度投げられない()
    {
        var run = Fresh();
        Steal.Hop(run, 0, -1, 0);
        Assert.Throws<System.ArgumentException>(() => Steal.Hop(run, 0, -1, 0));
    }

    [Fact]
    public void 無い発射台は選べない()
    {
        var run = Fresh();
        Assert.Throws<System.ArgumentException>(() => Steal.Hop(run, 0, 0, 0));
    }

    /// <summary>⭐ 3体使い切って届かなければ、そこで戦闘へ。</summary>
    [Fact]
    public void 三体使い切ったら決着する()
    {
        var run = Fresh();
        Assert.Null(run.Result);
        Steal.Hop(run, 0, -1, 0);
        Assert.Null(run.Result);
        Steal.Hop(run, 1, -1, 0);
        Assert.Null(run.Result);
        Steal.Hop(run, 2, -1, 0);
        Assert.NotNull(run.Result);
        Assert.Throws<System.InvalidOperationException>(() => Steal.Hop(run, 0, -1, 0));
    }

    /// <summary>⭐ **親に当たった時点で戦闘。** 残りの個体は投げられない。
    ///
    /// ⚠️ 3体使い切ってから戦闘、ではない。触れた瞬間に見つかっている。
    /// ⚠️ 残りを投げられてしまうと「1体を捨てて偵察する」が最適手になり、
    /// 親が障害ではなく情報源になる。</summary>
    [Fact]
    public void 親に当たった時点で戦闘になる()
    {
        // 浅い盤（段1）なら1投目で親に届く
        var party = new List<Creature>
        {
            Make("a", 30, 30, 30, 30),
            Make("b", 30, 30, 30, 30),
            Make("c", 30, 30, 30, 30),
        };
        var run = new Steal.Infiltration(Plain(1), party);

        var flight = Steal.Hop(run, 0, -1, 0);

        Assert.Equal(StealOutcome.Blocked, flight.Outcome);
        Assert.Equal(StealOutcome.Blocked, run.Result);
        // ⭐ まだ2体残っているのに、もう投げられない
        Assert.Equal(2, run.Left.Count);
        Assert.Throws<System.InvalidOperationException>(() => Steal.Hop(run, 1, -1, 0));
    }

    /// <summary>⚠️ 卵に届いた時点でも決着する（触れてから更に飛ばせない）。</summary>
    [Fact]
    public void 卵に届いた時点で決着する()
    {
        var field = Plain(1);
        var party = new List<Creature> { Make("a", 30, 30, 30, 30), Make("b", 30, 30, 30, 30) };
        var run = new Steal.Infiltration(field, party);

        // 隙間側へ寄せて撃つ角度を機械で探す
        List<Steal.Shot> plan;
        Assert.True(Steal.FindRelaySolution(field, party, 61, out plan), "段1が解けない");

        var fresh = new Steal.Infiltration(field, party);
        StealRun? last = null;
        foreach (var shot in plan) last = Steal.Hop(fresh, shot.Member, shot.Pad, shot.Angle);

        Assert.Equal(StealOutcome.Success, last!.Outcome);
        Assert.Equal(StealOutcome.Success, fresh.Result);
    }

    // ── 巣の寿命 ────────────────────────────────────

    /// <summary>⭐ 盗まれるたびに隙間が狭まる。⚠️ 数値強化ではなく守りが厚くなる。</summary>
    [Fact]
    public void 盗まれるたびに隙間が狭まる()
    {
        double last = double.MaxValue;
        for (int raids = 0; raids <= Steal.RaidsToSeal; raids++)
        {
            double gap = Steal.GapWidthFor(raids);
            Assert.True(gap < last, $"raids={raids}: {last} → {gap} と狭まっていない");
            last = gap;
        }
    }

    /// <summary>⭐ **巣には寿命がある。** 最後は親が完全にふさぐ。
    /// ⚠️ 無限に盗めると、良い巣を1つ見つけた時点で探索が要らなくなる。</summary>
    [Fact]
    public void 盗み尽くした巣は親がふさぎ切る()
    {
        Assert.False(Steal.IsSealed(0));
        Assert.True(Steal.IsSealed(Steal.RaidsToSeal));
        Assert.Equal(0, Steal.GapWidthFor(Steal.RaidsToSeal));

        // 塞がった盤は、どんな編成でも解けない
        var field = Steal.MakeField(1, FieldSide.Right, Steal.RaidsToSeal);
        var party = new List<Creature>
        {
            Make("a", 0, 40, 0, 40), Make("b", 0, 40, 0, 40), Make("c", 0, 40, 0, 40),
        };
        List<Steal.Shot> plan;
        Assert.False(Steal.FindRelaySolution(field, party, 31, out plan));
    }

    // ── 生成の検査 ──────────────────────────────────

    /// <summary>⭐ **この検査が今回いちばん大事。**
    /// 出荷する経路（<see cref="Steal.MakeValidatedField"/>）が作る盤は、
    /// 塞がっていない限り**想定編成で通る角度に幅がある**こと。
    ///
    /// ⚠️ 「解が在るか」だけ見ていたときは、幅 1度の針の穴を「解けます」と報告していた。
    /// プレイヤーには「運が悪い」としか見えない盤を、検査が通してしまう。</summary>
    [Fact]
    public void 出荷する盤はどれも通る角度に幅がある()
    {
        for (int tier = 1; tier <= 5; tier++)
        {
            for (int raids = 0; raids < Steal.RaidsToSeal; raids++)
            {
                foreach (var side in new[] { FieldSide.Left, FieldSide.Right })
                {
                    var nest = new Nest($"check-t{tier}-{side}", "検査", "tamaru", tier);
                    int window;
                    Steal.MakeValidatedField(tier, side, raids, Steal.RngFor(nest, raids),
                        out window);
                    Assert.True(window >= Steal.MinWindowDegrees,
                        $"段{tier} raids{raids} {side}: 通る角度が {window}度しかない");
                }
            }
        }
    }

    /// <summary>⭐ 関門の要求は**想定編成から導く**。手で書いた表だと必ずいつかずれる。
    /// ⚠️ 実際にずれていた: 段1 の壁が攻撃力 28 を要求するのに、段1 の想定編成は最大 27。
    /// **誰にも通れない関門**が混じっていた。</summary>
    [Fact]
    public void 関門の要求は想定編成の誰かが必ず満たす()
    {
        for (int tier = 1; tier <= 5; tier++)
        {
            var party = Steal.ReferenceParty(tier);
            foreach (GimmickKind kind in System.Enum.GetValues(typeof(GimmickKind)))
            {
                int requires = Steal.RequirementFor(tier, kind);
                bool anyone = false;
                foreach (var creature in party)
                {
                    if (Creatures.StatsOf(creature)[Steal.StatOf(kind)] >= requires) anyone = true;
                }
                Assert.True(anyone, $"段{tier} の {kind}（要求 {requires}）は誰にも通れない");
            }
        }
    }

    /// <summary>⭐ 盤は**巣と盗んだ回数だけ**で決まる。
    /// ⚠️ 挑むたびに振り直すと、画面を出入りするだけで盤を選び直せる（粘れば良い盤が出る）。</summary>
    [Fact]
    public void 同じ巣と同じ回数からは同じ盤が出る()
    {
        var nest = Nests.ById("thicket-fang");
        var a = Steal.MakeValidatedField(nest.Tier, FieldSide.Right, 1, Steal.RngFor(nest, 1));
        var b = Steal.MakeValidatedField(nest.Tier, FieldSide.Right, 1, Steal.RngFor(nest, 1));

        Assert.Equal(a.Gimmicks.Count, b.Gimmicks.Count);
        for (int i = 0; i < a.Gimmicks.Count; i++)
        {
            Assert.Equal(a.Gimmicks[i].From, b.Gimmicks[i].From);
            Assert.Equal(a.Gimmicks[i].Top, b.Gimmicks[i].Top);
            Assert.Equal(a.Gimmicks[i].Kind, b.Gimmicks[i].Kind);
        }
        // ⭐ 盗めば別の盤になる
        var next = Steal.MakeValidatedField(nest.Tier, FieldSide.Right, 2, Steal.RngFor(nest, 2));
        Assert.NotEqual(a.GapTo - a.GapFrom, next.GapTo - next.GapFrom);
    }

    /// <summary>⭐ **ランダム化した生成は、1つの種で通っても安全の証拠にならない。**
    /// 本番で効くのは珍しい悪い出目のほうで、それは数を撃たないと出てこない。
    /// ⚠️ ここが落ちたら、振り直し（<see cref="Steal.MakeValidatedField"/>）が甘いということ。</summary>
    [Fact]
    public void どの巣の出目でも検査を通った盤しか出ない()
    {
        for (int seed = 0; seed < 12; seed++)
        {
            foreach (int tier in new[] { 1, 3, 5 })
            {
                foreach (int raids in new[] { 0, 2 })
                {
                    var nest = new Nest($"roll-{seed}", "検査", "tamaru", tier);
                    var side = seed % 2 == 0 ? FieldSide.Left : FieldSide.Right;
                    int window;
                    Steal.MakeValidatedField(tier, side, raids, Steal.RngFor(nest, raids),
                        out window);
                    Assert.True(window >= Steal.MinWindowDegrees,
                        $"seed{seed} 段{tier} raids{raids} {side}: {window}度しかない");
                }
            }
        }
    }

    /// <summary>⭐ **同じ種類を2つ出さない。**
    /// 関門がある理由は「3体それぞれに別の役目を作る」ことなので、
    /// 壁を3枚並べると「攻撃力を持っているか」だけの検査に戻ってしまう。</summary>
    [Fact]
    public void 関門の種類は重ならない()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            for (int tier = 1; tier <= 5; tier++)
            {
                var nest = new Nest($"kind-{seed}", "検査", "tamaru", tier);
                var field = Steal.MakeField(tier, FieldSide.Right, 3, Steal.RngFor(nest, 3));
                var seen = new HashSet<GimmickKind>();
                foreach (var gate in field.Gimmicks)
                {
                    Assert.True(seen.Add(gate.Kind), $"seed{seed} 段{tier}: {gate.Kind} が重複");
                }
            }
        }
    }

    /// <summary>⚠️ **壁を一番奥にしない。** 壊すと後続が通れるのが壁の値打ちなので、
    /// 一番奥だと後続がもう通らず、その値打ちが丸ごと消える。</summary>
    [Fact]
    public void 壁は一番奥に置かない()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            for (int tier = 2; tier <= 5; tier++)
            {
                var nest = new Nest($"wall-{seed}", "検査", "tamaru", tier);
                var field = Steal.MakeField(tier, FieldSide.Right, 3, Steal.RngFor(nest, 3));
                if (field.Gimmicks.Count < 2) continue;
                Assert.NotEqual(GimmickKind.Wall, field.Gimmicks[field.Gimmicks.Count - 1].Kind);
            }
        }
    }

    /// <summary>⚠️ 揺らした縦位置が親の帯や出発点に食い込まないこと。
    /// ⭐ 食い込むと、出発した瞬間に関門で止まる盤ができる。</summary>
    [Fact]
    public void 揺らした関門が盤からはみ出さない()
    {
        for (int seed = 0; seed < 30; seed++)
        {
            for (int tier = 1; tier <= 5; tier++)
            {
                var nest = new Nest($"jit-{seed}", "検査", "tamaru", tier);
                var field = Steal.MakeField(tier, FieldSide.Right, 3, Steal.RngFor(nest, 3));
                foreach (var gate in field.Gimmicks)
                {
                    Assert.True(gate.Top > field.BandBottom,
                        $"seed{seed} 段{tier}: 関門が親の帯に食い込んでいる");
                    Assert.True(gate.Bottom < field.Start.Y,
                        $"seed{seed} 段{tier}: 関門が出発点に食い込んでいる");
                }
            }
        }
    }

    /// <summary>⭐ 盗んだ回数は保存に残る。⚠️ 消えると巣が若返り、寿命が無くなる。</summary>
    [Fact]
    public void 盗んだ回数は保存して読み直しても残る()
    {
        var game = Games.NewGame(2026_08_17);
        var nest = Nests.ById("thicket-fang");
        Assert.Equal(0, Games.RaidsOn(game, nest));

        Games.RecordRaid(game, nest);
        Games.RecordRaid(game, nest);
        Assert.Equal(2, Games.RaidsOn(game, nest));

        var back = Snapshots.Load(Snapshots.Save(game));
        Assert.NotNull(back);
        Assert.Equal(2, Games.RaidsOn(back!, nest));
        // ⚠️ 盗んでいない巣は 0 のまま
        Assert.Equal(0, Games.RaidsOn(back!, Nests.ById("shallow-scale")));
    }

    /// <summary>⭐ **盗んでも巣は残る。**次はもっと固くなっているだけ。
    ///
    /// ⚠️ 盗んだ時点で巣を片付けてしまうと、同じ巣に二度と行けず、
    /// 「盗むたびに守りが固くなり4回で封鎖される」という寿命が**丸ごと働かない**
    /// （実際に画面側がそうなっていた）。</summary>
    [Fact]
    public void 盗んだ巣は探索に残る()
    {
        var game = Games.NewGame(2026_08_17);
        var nest = game.Encounters[0].Nest;

        Games.RecordRaid(game, nest);
        Assert.Equal(1, Games.RaidsOn(game, nest));
        Assert.False(Games.IsNestSealed(game, nest));

        // ⭐ まだ並んでいる（盗んだだけでは消えない）
        Assert.Contains(game.Encounters, e => e.Nest.Id == nest.Id);
    }

    /// <summary>⭐ **塞がった巣も探索に残る。**
    ///
    /// ⚠️ 塞がりは壁ではなく、**戦闘へ向かわせる漏斗**。
    /// ここで巣を片付けてしまうと、最後の1個を取り上げることになる。</summary>
    [Fact]
    public void 塞がった巣も探索に残る()
    {
        var game = Games.NewGame(31);
        var nest = game.Encounters[0].Nest;

        for (int i = 0; i < Steal.RaidsToSeal; i++) Games.RecordRaid(game, nest);

        Assert.True(Games.IsNestSealed(game, nest));
        Assert.Contains(game.Encounters, e => e.Nest.Id == nest.Id);
    }

    /// <summary>⭐ **塞がった巣に潜入すると、必ず親との戦闘になる。**
    ///
    /// 守りが最大になると隙間が無くなるので、どこへ投げても親に当たります。
    /// ⚠️ ここが「実質盗めない」の実体 ── 入れないのではなく、**入ると戦闘になる**。</summary>
    [Fact]
    public void 塞がった巣に潜入すると必ず戦闘になる()
    {
        var nest = Nests.ById("thicket-fang");
        var party = new List<Creature>
        {
            Make("a", 30, 30, 30, 40), Make("b", 30, 30, 30, 40), Make("c", 30, 30, 30, 40),
        };

        foreach (var side in new[] { FieldSide.Left, FieldSide.Right })
        {
            var field = Steal.MakeValidatedField(nest.Tier, side, Steal.RaidsToSeal,
                Steal.RngFor(nest, Steal.RaidsToSeal));

            // ⭐ 隙間が無いので、親が盤の幅いっぱいを塞いでいる
            Assert.Equal(0, Steal.GapWidthFor(Steal.RaidsToSeal));

            // どの角度で投げても卵には届かない
            for (int deg = -80; deg <= 80; deg += 5)
            {
                var run = new Steal.Infiltration(field, party);
                var flight = Steal.Hop(run, 0, -1, deg * System.Math.PI / 180.0);
                Assert.NotEqual(StealOutcome.Success, flight.Outcome);
            }

            // ⚠️ 解も1つも無い（＝運が悪いのではなく、原理的に届かない）
            List<Steal.Shot> plan;
            Assert.False(Steal.FindRelaySolution(field, party, 33, out plan));
        }
    }

    /// <summary>⭐ 盗み尽くすと巣が死ぬ。⚠️ ここが無いと良い巣を1つ見つけた時点で探索が要らなくなる。</summary>
    [Fact]
    public void 盗み尽くすと巣は死ぬ()
    {
        var game = Games.NewGame(5);
        var nest = Nests.ById("shallow-scale");
        Assert.False(Games.IsNestSealed(game, nest));

        for (int i = 0; i < Steal.RaidsToSeal; i++) Games.RecordRaid(game, nest);
        Assert.True(Games.IsNestSealed(game, nest));
    }

    // ── 関門 ────────────────────────────────────────

    /// <summary>⚠️ 段階1・初回は関門なし ＝ 移植元の盤とまったく同じ。</summary>
    [Fact]
    public void 段階1の初回には関門が無い()
    {
        Assert.Empty(Steal.MakeField(1, FieldSide.Right).Gimmicks);
        Assert.Equal(0, Steal.GimmickCountFor(1, 0));
    }

    /// <summary>⭐ 盗まれるたびに増える。⚠️ 数値ではなく関門が増える。</summary>
    [Fact]
    public void 盗まれるたびに関門が増える()
    {
        int before = Steal.MakeField(2, FieldSide.Right).Gimmicks.Count;
        int after = Steal.MakeField(2, FieldSide.Right, raids: 2).Gimmicks.Count;
        Assert.True(after > before, $"{before} → {after} と増えていない");
        // ⚠️ 上限がある（際限なく増えると画面が埋まる）
        Assert.Equal(Steal.MakeField(5, FieldSide.Right, raids: 99).Gimmicks.Count,
            Steal.MakeField(5, FieldSide.Right, raids: 3).Gimmicks.Count);
    }

    /// <summary>⚠️ **塞ぎ切らない。** 全幅を塞ぐと「持っているかどうか」の検査になり、
    /// 「満たして直進する / 迂回して距離を払う」の二択が消える。</summary>
    [Fact]
    public void 関門は盤を塞ぎ切らない()
    {
        for (int tier = 1; tier <= 5; tier++)
        {
            var field = Steal.MakeField(tier, FieldSide.Right, raids: 3);
            foreach (var gate in field.Gimmicks)
            {
                double open = Steal.FieldWidth - (gate.To - gate.From);
                Assert.True(open > Steal.RunnerRadius * 2,
                    $"段{tier} {gate.Kind}: 空きが {open} しかない");
            }
        }
    }

    /// <summary>要求を満たさない個体は、その関門で止まる。⭐ **使用済みになるだけ**（台にはなる）。</summary>
    [Fact]
    public void 要求を満たさない個体は関門で止まる()
    {
        var field = OneGate(GimmickKind.Wall, requires: 200);
        var weak = Make("weak", 30, 0, 30, 30);
        var run = new Steal.Infiltration(field, new List<Creature> { weak });

        var flight = Steal.Hop(run, 0, -1, 0);

        Assert.Equal(StealOutcome.Landed, flight.Outcome);
        Assert.Equal(0, flight.StoppedBy);
        Assert.Single(run.Pads);              // ⭐ 台にはなる
        Assert.Empty(flight.Broke);

        // ⚠️ **関門の手前**で止まっている。中で止まると、この台から投げた次の個体が
        //    一歩目で同じ関門に捕まり、台が必ず詰む
        Assert.False(Steal.Inside(field.Gimmicks[0], flight.Landing),
            $"関門の中 ({flight.Landing.X:0}, {flight.Landing.Y:0}) に着地している");
    }

    /// <summary>関門で止まっても**潜入は続く**。⭐ 初期位置はいつでも選べる。
    ///
    /// ⚠️ **関門の真下にできた台は、上へは動けない**（一歩目でまた同じ関門に入る）。
    /// これは詰みではない — 初期位置も他の台も選べるので、潰れるのはその台1つだけ。
    /// ⭐ 「止まった場所が悪ければ台として使えない」は、どこで止まるかを考える理由になる。</summary>
    [Fact]
    public void 関門で止まっても潜入は続く()
    {
        var field = OneGate(GimmickKind.Wall, requires: 200, span: Steal.GimmickSpan);
        var run = new Steal.Infiltration(field, new List<Creature>
        {
            Make("a", 30, 0, 30, 30), Make("b", 30, 0, 30, 30), Make("c", 30, 0, 30, 30),
        });

        var first = Steal.Hop(run, 0, -1, 0);
        Assert.Equal(0, first.StoppedBy);
        Assert.Null(run.Result);                     // ⭐ まだ終わっていない

        // ⭐ 初期位置から関門の横を抜けられる角度が**ある**こと。
        // ⚠️ 角度を手で決め打ちしない — 通る窓が狭いと、検査が通ったかどうかが
        //    盤の寸法より先に手先の当てずっぽうで決まってしまう
        int windows = 0;
        for (int deg = 0; deg <= 80; deg += 2)
        {
            var probe = new Steal.Infiltration(field, run.Party);
            var shot = Steal.Hop(probe, 1, -1, deg * System.Math.PI / 180.0);
            if (shot.StoppedBy == -1 && shot.Traveled > 0) windows++;
        }
        // ⭐ 迂回できる角度が「ひとつだけ」では手先の勝負になる。幅が要る
        Assert.True(windows >= 3, $"関門を迂回できる角度が {windows} 通りしかない");
    }

    /// <summary>⭐ 攻撃力が足りれば壁を壊して**貫通**する。</summary>
    [Fact]
    public void 攻撃力が足りれば壁を壊して貫通する()
    {
        var field = OneGate(GimmickKind.Wall, requires: 20);
        var strong = Make("strong", 30, 40, 30, 30);
        var run = new Steal.Infiltration(field, new List<Creature> { strong });

        var flight = Steal.Hop(run, 0, -1, 0);

        Assert.Equal(-1, flight.StoppedBy);
        Assert.Contains(0, flight.Broke);
        Assert.Contains(0, run.Broken);
    }

    /// <summary>⭐ 壊した壁は開通したまま。⚠️ 後続は攻撃力が足りなくても通れる。</summary>
    [Fact]
    public void 壊した壁は後続も通れる()
    {
        var field = OneGate(GimmickKind.Wall, requires: 20);
        var strong = Make("strong", 30, 40, 30, 30);
        var weak = Make("weak", 30, 0, 30, 30);
        var run = new Steal.Infiltration(field, new List<Creature> { strong, weak });

        Steal.Hop(run, 0, -1, 0);
        var after = Steal.Hop(run, 1, -1, 0);

        Assert.Equal(-1, after.StoppedBy);
    }

    /// <summary>⚠️ 壊れるのは壁だけ。ダメージ床と重圧は通った本人にしか効かない。</summary>
    [Fact]
    public void ダメージ床は開通しない()
    {
        var field = OneGate(GimmickKind.Damage, requires: 200);
        var tough = Make("tough", 40, 30, 30, 30);
        var frail = Make("frail", 0, 30, 30, 30);
        var run = new Steal.Infiltration(field, new List<Creature> { tough, frail });

        Steal.Hop(run, 0, -1, 0);           // 通れずに止まる（要求200 は誰も満たさない）
        var second = Steal.Hop(run, 1, -1, 0);
        Assert.Equal(0, second.StoppedBy);  // ⭐ 開通していない
        Assert.Empty(run.Broken);
    }

    [Fact]
    public void 関門が要求するステは種類で決まる()
    {
        Assert.Equal(StatKey.Atk, Steal.StatOf(GimmickKind.Wall));
        Assert.Equal(StatKey.Hp, Steal.StatOf(GimmickKind.Damage));
        Assert.Equal(StatKey.Def, Steal.StatOf(GimmickKind.Pressure));
    }

    /// <summary>幅いっぱいの関門を1つだけ置いた盤。⚠️ 検査専用。
    /// ⚠️ 深い盤にする。浅いと投げた個体が関門を抜けたあと親まで届いて決着し、
    /// 「壊した壁を後続が通れるか」を検査できない。</summary>
    private static StealField OneGate(GimmickKind kind, int requires, double span = 1.0)
    {
        var plain = Plain(5);
        // ⭐ 出発点のすぐ先に置く。どの角度でも必ず通る位置
        double y = plain.Start.Y - 40;
        var gate = new Gimmick(kind, 0, Steal.FieldWidth * span, y - 9, y + 9, requires);
        return new StealField(plain.Height, plain.Side, plain.GapFrom, plain.GapTo,
            plain.BandTop, plain.BandBottom, plain.Egg, plain.Start, new[] { gate });
    }

    // ── 移植元との約束 ──────────────────────────────

    /// <summary>⭐ 筆頭の約束。関門の無い盤では、移植元の一投と1ビットも変わらない。</summary>
    [Fact]
    public void 関門が無ければ移植元の一投と同じ()
    {
        var field = Plain(3);
        Assert.Empty(field.Gimmicks);

        var runner = Make("x", 30, 30, 30, 30);
        var run = new Steal.Infiltration(field, new List<Creature> { runner });

        for (int deg = -80; deg <= 80; deg += 10)
        {
            double angle = deg * System.Math.PI / 180.0;
            var old = Steal.Launch(field, angle, Steal.DistanceFor(runner));
            var fresh = new Steal.Infiltration(field, new List<Creature> { runner });
            var hop = Steal.Hop(fresh, 0, -1, angle);

            Assert.Equal(old.Path.Count, hop.Path.Count);
            Assert.Equal(old.Traveled, hop.Traveled);
            // ⚠️ 失速だけは呼び名が違う（一投では Stalled / リレーでは着地）
            if (old.Outcome == StealOutcome.Stalled)
                Assert.Equal(StealOutcome.Landed, hop.Outcome);
            else
                Assert.Equal(old.Outcome, hop.Outcome);
        }
    }

    // ── 道中の雑魚 ──────────────────────────────────

    /// <summary>盤の真ん中に雑魚を1体だけ置いた盤。⚠️ 検査専用。</summary>
    private static StealField OneMob(out Point at)
    {
        var plain = Plain(5);
        at = new Point(Steal.FieldWidth / 2, plain.Start.Y - 60);
        return new StealField(plain.Height, plain.Side, plain.GapFrom, plain.GapTo,
            plain.BandTop, plain.BandBottom, plain.Egg, plain.Start,
            null, new[] { new Mob(at, Steal.MobRadius) });
    }

    private static List<Creature> Three() => new List<Creature>
    {
        Make("a", 30, 30, 30, 30), Make("b", 30, 30, 30, 30), Make("c", 30, 30, 30, 30),
    };

    /// <summary>⭐ 雑魚に当たると**その場が着地点**になって戦闘へ。
    /// ⚠️ 親に当たったとき（潜入の終わり）とは違い、決着させない。</summary>
    [Fact]
    public void 雑魚に当たるとその場で戦闘になる()
    {
        Point at;
        var run = new Steal.Infiltration(OneMob(out at), Three());

        var flight = Steal.Hop(run, 0, -1, 0);

        Assert.Equal(StealOutcome.Fought, flight.Outcome);
        Assert.Equal(0, flight.Mob);
        Assert.Null(run.Result);                       // ⭐ まだ終わっていない
        Assert.Single(run.Pads);                       // ⭐ そこが発射台になる
        Assert.True(flight.Landing.Y > at.Y, "雑魚より奥で止まっている");
    }

    /// <summary>⭐ 倒すと**投げる回数が戻り**、経験値が入る。</summary>
    [Fact]
    public void 雑魚を倒すと投げる回数が戻る()
    {
        Point at;
        var run = new Steal.Infiltration(OneMob(out at), Three());

        var flight = Steal.Hop(run, 0, -1, 0);
        Assert.Equal(2, run.Left.Count);               // 1体使った

        Steal.Beat(run, flight.Mob);

        Assert.Equal(3, run.Left.Count);               // ⭐ 全部戻る
        Assert.Equal(Steal.MobReward, run.Earned);
        Assert.Single(run.Pads);                       // ⚠️ 台は消えない
    }

    /// <summary>⚠️ 倒した雑魚はもう居ない。⭐ 同じ場所で何度も稼げない。</summary>
    [Fact]
    public void 倒した雑魚は通り抜けられる()
    {
        Point at;
        var run = new Steal.Infiltration(OneMob(out at), Three());

        var first = Steal.Hop(run, 0, -1, 0);
        Steal.Beat(run, first.Mob);

        var second = Steal.Hop(run, 1, -1, 0);
        Assert.NotEqual(StealOutcome.Fought, second.Outcome);
        Assert.Equal(-1, second.Mob);
    }

    /// <summary>⭐ **戦闘で負った傷と CT は潜入のあいだ残る。**</summary>
    [Fact]
    public void 傷とCTは次の戦闘へ引き継がれる()
    {
        Point at;
        var run = new Steal.Infiltration(OneMob(out at), Three());
        foreach (int hp in run.Hp) Assert.Equal(-1, hp);   // -1 ＝ 満タン

        var flight = Steal.Hop(run, 0, -1, 0);
        Steal.Beat(run, flight.Mob,
            new[] { 12, 34, 56 },
            new[] { new[] { 0, 2, 0 }, new[] { 0, 0, 3 }, new[] { 0, 0, 0 } });

        Assert.Equal(12, run.Hp[0]);
        Assert.Equal(34, run.Hp[1]);
        Assert.Equal(2, run.Cooldowns[0][1]);
        Assert.Equal(3, run.Cooldowns[1][2]);
    }

    [Fact]
    public void 雑魚戦に負けたら潜入は終わる()
    {
        Point at;
        var run = new Steal.Infiltration(OneMob(out at), Three());
        Steal.Hop(run, 0, -1, 0);

        Steal.LostTo(run);

        Assert.NotNull(run.Result);
        Assert.Throws<System.InvalidOperationException>(() => Steal.Hop(run, 1, -1, 0));
    }

    /// <summary>⚠️ 1つの巣に置ける数の上限を守ること。</summary>
    [Fact]
    public void 雑魚は三か所まで()
    {
        for (int tier = 1; tier <= 5; tier++)
        {
            Assert.True(Steal.MobCountFor(tier) <= Steal.MobsMax);
            for (int seed = 0; seed < 20; seed++)
            {
                var nest = new Nest($"mob-{seed}", "検査", "tamaru", tier);
                var field = Steal.MakeField(tier, FieldSide.Right, 0, Steal.RngFor(nest, 0));
                Assert.True(field.Mobs.Count <= Steal.MobsMax,
                    $"段{tier} seed{seed}: 雑魚が {field.Mobs.Count} 体");
            }
        }
    }

    /// <summary>⭐ **出荷する盤は、雑魚を1体も倒さずに解ける。**
    ///
    /// ⚠️ 検査が雑魚を無視するだけでは足りない。雑魚は飛行を止めるので、
    /// 無造作に置くと**通れる道そのものを食う**（実測: 段5 raids0 で 12度 → 解なし）。
    /// ⭐ だから置き方のほうで守る ── 素の盤で解いてから、
    /// **その道を塞がない場所にだけ**雑魚を置く。</summary>
    [Fact]
    public void 出荷する盤は雑魚を避けて解ける()
    {
        for (int tier = 3; tier <= 5; tier++)
        {
            for (int seed = 0; seed < 12; seed++)
            {
                var nest = new Nest($"clear-{tier}-{seed}", "検査", "tamaru", tier);
                for (int raids = 0; raids < Steal.RaidsToSeal; raids++)
                {
                    var field = Steal.MakeValidatedField(tier, FieldSide.Right, raids,
                        Steal.RngFor(nest, raids));
                    var party = Steal.ReferenceParty(tier);

                    // ⚠️ 走査の細かさは**出荷時と同じ 13**にすること。
                    //    細かくすると枝が増えて、探索の上限に先に当たって解を見失う
                    //    （33 にしたら 段3 seed3 raids3 が「解なし」になった）。
                    List<Steal.Shot> plan;
                    Assert.True(Steal.FindRelaySolution(field, party, 13, out plan),
                        $"段{tier} seed{seed} raids{raids}: 解が無い");

                    var run = new Steal.Infiltration(field, party);
                    foreach (var shot in plan)
                    {
                        if (run.Result != null) break;
                        var flight = Steal.Hop(run, shot.Member, shot.Pad, shot.Angle);
                        Assert.NotEqual(StealOutcome.Fought, flight.Outcome);
                    }
                    Assert.Equal(StealOutcome.Success, run.Result);
                }
            }
        }
    }

    /// <summary>⭐ **盤は雑魚に頼らずに解けること。**
    ///
    /// ⚠️ 雑魚は「取れば楽になる」ものであって「取らないと解けない」ものにしない。
    /// 雑魚に当てるのは半径18の的を狙う精密な行為なので、
    /// そこを通る手順を数えると**どの盤も通る角度が1度**になった（実測）。</summary>
    [Fact]
    public void 検査は雑魚を経由する手順を数えない()
    {
        Point at;
        var field = OneMob(out at);
        var party = Three();

        List<Steal.Shot> plan;
        Steal.FindRelaySolution(field, party, 33, out plan);

        // 見つけた手順のどの一投も、雑魚には当たらないこと
        var run = new Steal.Infiltration(field, party);
        foreach (var shot in plan)
        {
            if (run.Result != null) break;
            var flight = Steal.Hop(run, shot.Member, shot.Pad, shot.Angle);
            Assert.NotEqual(StealOutcome.Fought, flight.Outcome);
        }
    }

    // ── 雑魚の編成と、傷の持ち回り ────────────────────

    /// <summary>⭐ **巣と番号だけで決まる。**画面を出入りしても顔ぶれが変わらない。</summary>
    [Fact]
    public void 雑魚の編成は引き直せない()
    {
        var nest = new Nest("mob-fix", "検査", "tamaru", 3);

        var once = Steal.MobPartyOf(nest, 1, 0);
        var again = Steal.MobPartyOf(nest, 1, 0);

        Assert.Equal(3, once.Count);
        for (int i = 0; i < once.Count; i++)
        {
            Assert.Equal(once[i].SpeciesId, again[i].SpeciesId);
            Assert.Equal(Stats.TotalOf(once[i].Wild), Stats.TotalOf(again[i].Wild));
        }
    }

    /// <summary>⚠️ 雑魚1と雑魚2が同じ編成だと、2戦目が1戦目の繰り返しになる。</summary>
    [Fact]
    public void 雑魚ごとに顔ぶれが違う()
    {
        var nest = new Nest("mob-var", "検査", "tamaru", 5);
        var first = Steal.MobPartyOf(nest, 0, 0);
        var second = Steal.MobPartyOf(nest, 0, 1);

        bool same = true;
        for (int i = 0; i < first.Count; i++)
        {
            if (first[i].SpeciesId != second[i].SpeciesId
                || Stats.TotalOf(first[i].Wild) != Stats.TotalOf(second[i].Wild)) same = false;
        }
        Assert.False(same, "雑魚0と雑魚1が同じ編成");
    }

    /// <summary>⚠️ 雑魚は親より重い関所にしない（⭐「取れば楽になる」もの）。</summary>
    [Fact]
    public void 雑魚は親より弱い()
    {
        for (int tier = 1; tier <= 5; tier++)
        {
            var nest = new Nest($"mob-w{tier}", "検査", "tamaru", tier);
            var parent = Nests.MakeDefenders(new Rng(7), nest);
            var mobs = Steal.MobPartyOf(nest, 0, 0);

            int one = Stats.TotalOf(mobs[0].Wild);
            Assert.True(one < Stats.TotalOf(parent[0].Wild),
                $"段{tier}: 雑魚1体 {one} が親 {Stats.TotalOf(parent[0].Wild)} 以上");
        }
    }

    /// <summary>⭐ **潜入で負った傷と CT が、そのまま次の戦闘の味方に載る。**</summary>
    [Fact]
    public void 傷とCTは戦闘へ持ち込まれる()
    {
        var party = Three();
        var enemies = new List<Creature> { Make("e", 30, 30, 30, 30) };
        var state = Battle.CreateBattle(party, enemies);

        int full = state.Units[0].MaxHp;
        Battle.CarryIn(state,
            new[] { -1, 5, 0 },                       // -1 ＝ 満タン / 0 ＝ 倒れていた
            new[] { new[] { 0, 0, 0 }, new[] { 0, 4, 0 }, new[] { 0, 0, 0 } });

        Assert.Equal(full, state.Units[0].Hp);        // ⚠️ -1 は触らない
        Assert.Equal(5, state.Units[1].Hp);
        Assert.Equal(4, state.Units[1].Cooldowns[1]);
        // ⭐ 倒れた個体も 1 で立つ（投げられない個体を作らない）
        Assert.Equal(1, state.Units[2].Hp);
    }

    /// <summary>⚠️ 敵に持ち込まない（味方の傷なので）。</summary>
    [Fact]
    public void 持ち込む傷は味方だけ()
    {
        var enemies = new List<Creature> { Make("e", 30, 30, 30, 30) };
        var state = Battle.CreateBattle(Three(), enemies);
        var enemy = state.Units.First(u => u.Side == Side.Enemy);
        int before = enemy.Hp;

        Battle.CarryIn(state, new[] { 1, 1, 1 }, null);

        Assert.Equal(before, enemy.Hp);
    }

    /// <summary>⭐ 戦闘のあとの傷を潜入へ書き戻す。⚠️ 満タンに戻さない。</summary>
    [Fact]
    public void 戦闘のあとの傷を潜入へ書き戻す()
    {
        var party = Three();
        var enemies = new List<Creature> { Make("e", 30, 30, 30, 30) };
        var state = Battle.CreateBattle(party, enemies);
        state.Units[0].Hp = 9;
        state.Units[0].Cooldowns[2] = 3;

        Point at;
        var run = new Steal.Infiltration(OneMob(out at), party);
        Battle.CarryOut(state, run.Hp, run.Cooldowns);

        Assert.Equal(9, run.Hp[0]);
        Assert.Equal(3, run.Cooldowns[0][2]);
    }
}
