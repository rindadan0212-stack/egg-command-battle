using System.Collections.Generic;
using EggCommand.Core;
using Xunit;

namespace EggCommand.Tests
{
    /// <summary>
    /// ⭐ **移植が正しいかは、TypeScript 版の実際の出力と突き合わせて決める。**
    ///
    /// ⚠️ ここに書いてある数値は目視で写したものではなく、
    /// TS 側を実際に走らせて吐かせた値（`node -e ... rng.ts`）。
    /// 較正済みの数値（変異 2.5%×3回、掃引で決めた倍率など）は
    /// **この系列が1ビットも違わないこと**に依存しているので、
    /// ここが落ちたら移植は失敗であって、テストを緩めてはいけない。
    /// </summary>
    public class RngTests
    {
        [Theory]
        [InlineData("breeding", 7242095u)]
        [InlineData("battle", 2820971575u)]
        [InlineData("nest", 1590744203u)]
        [InlineData("egg", 1501368774u)]
        [InlineData("hatch", 1592057985u)]
        [InlineData("steal", 218799968u)]
        public void 系統名のハッシュがTSと一致する(string name, uint expected)
        {
            Assert.Equal(expected, Rng.HashString(name));
        }

        public static IEnumerable<object[]> U32Cases => new List<object[]>
        {
            new object[] { 1L, new uint[] { 1130556604, 2591592147, 3014952990, 960850752, 2734082507, 3058966613 } },
            new object[] { 20260815L, new uint[] { 3917307804, 2144515334, 3911081408, 3792461301, 3032504455, 3181676420 } },
            new object[] { 4294967295L, new uint[] { 2246433216, 2156899173, 2040853526, 3823104220, 1358629326, 3404600419 } },
        };

        [Theory]
        [MemberData(nameof(U32Cases))]
        public void 同じ種から同じ系列が出る(long seed, uint[] expected)
        {
            var rng = new Rng(seed);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], rng.U32Value());
            }
        }

        [Fact]
        public void 系統を分けた先の整数列がTSと一致する()
        {
            var rng = new Rng(20260815).Stream("breeding");
            var expected = new[] { 84, 14, 30, 57, 20, 45, 60, 52, 90, 77 };
            foreach (var want in expected)
            {
                Assert.Equal(want, rng.Int(0, 100));
            }
        }

        [Fact]
        public void 小さい確率の当たり回数がTSと一致する()
        {
            // ⭐ 変異の 2.5% がここに乗っている。10万回で 2525 回。
            var rng = new Rng(20260815).Stream("battle");
            int hits = 0;
            for (int i = 0; i < 100000; i++)
            {
                if (rng.Chance(0.025)) hits++;
            }
            Assert.Equal(2525, hits);
        }

        [Fact]
        public void 抽選の並びがTSと一致する()
        {
            // 配合の「4枠から2つ抽選」
            var rng = new Rng(7);
            var got = rng.Sample(new[] { "a", "b", "c", "d" }, 2);
            Assert.Equal(new[] { "a", "c" }, got);
        }
    }
}
