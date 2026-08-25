using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests;

/// <summary>未配布の技。⭐ 作者指示（2026-08-19）「実装だけ・キャラへのあてはめはまだ」の形を守る見張り。
///
/// ⚠️ ここが守るのは2つ:
/// ・未配布の技が**どの種族からも出ない**こと（配ったら Undistributed から印を外す運用）
/// ・未配布でも**中身は完成している**こと（成長表・AI の採点 ── 配った日に壊れているのが一番困る）</summary>
public class UndistributedTests
{
    [Fact]
    public void 未配布の技は表にあるがどの種族からも出ない()
    {
        // ⚠️ **空であることを咎めない。**ここが見張るのは「印が付いているものは出ない」で、
        //    印の付いた技が居ることではない（2026-08-19 に10件すべてを配って空になった）。
        //    ⭐ 逆向き（印が無いのに出ない）は Skills.Audit が数えている。
        foreach (var id in Skills.Undistributed)
        {
            Assert.True(Skills.Has(id), $"{id} が技表に無い");
            foreach (var species in SpeciesTable.All)
            {
                Assert.NotEqual(id, species.Skill1);
                Assert.DoesNotContain(id, species.Slot2.Pool);
                Assert.DoesNotContain(id, species.Slot3.Pool);
            }
        }
    }

    /// <summary>⚠️ Audit も同じことを見るが、落ちたときにどの技のどこかが読める形でここでも数える。</summary>
    [Fact]
    public void 未配布でも成長表と採点は揃っている()
    {
        foreach (var id in Skills.Undistributed)
        {
            var skill = Skills.ById(id);
            Assert.Equal(Skills.MaxLevel - 1, Skills.GrowthOf(skill).Count);
            foreach (var effect in skill.Effects)
            {
                Assert.True(Ai.Knows(effect.Kind), $"{id}: {effect.Kind} を AI が採点しない");
            }
        }
    }
}
