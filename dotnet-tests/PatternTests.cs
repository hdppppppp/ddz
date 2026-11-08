using System;
using System.Collections.Generic;
using System.Linq;
using DDZ.Core;
using Xunit;

namespace DDZ.Core.Tests
{
    /// <summary>PatternRecognizer 的牌型识别测试。</summary>
    public class PatternTests
    {
        private static Card C(int id) => new(id);

        // 普通牌 id：花色*13 + 序号(3=0..A=11,2=12)。♠=0 ♥=1 ♣=2 ♦=3。王: 52/53。
        private static Card S(Rank r) => new(r, Suit.Spades);
        private static Card H(Rank r) => new(r, Suit.Hearts);
        private static Card D(Rank r) => new(r, Suit.Diamonds);
        private static Card Cl(Rank r) => new(r, Suit.Clubs);

        [Theory]
        [InlineData(1)] // 单张
        [InlineData(2)] // 对子
        [InlineData(3)] // 三张
        public void SimplePatterns(int count)
        {
            var cards = Enumerable.Range(0, count).Select(i => S(Rank.Five)).ToList();
            var p = PatternRecognizer.Recognize(cards);
            Assert.Equal((PatternType)count, p.Type);
        }

        [Fact]
        public void TripleWithSingle()
        {
            var p = PatternRecognizer.Recognize(new[] { S(Rank.Five), H(Rank.Five), D(Rank.Five), S(Rank.Three) });
            Assert.Equal(PatternType.TripleSingle, p.Type);
            Assert.Equal((int)Rank.Five, p.MainRank);
        }

        [Fact]
        public void TripleWithPair()
        {
            var p = PatternRecognizer.Recognize(new[]
            {
                S(Rank.Five), H(Rank.Five), D(Rank.Five), S(Rank.Seven), H(Rank.Seven),
            });
            Assert.Equal(PatternType.TriplePair, p.Type);
        }

        [Fact]
        public void Straight_Valid()
        {
            var p = PatternRecognizer.Recognize(new[]
            {
                S(Rank.Three), H(Rank.Four), D(Rank.Five), S(Rank.Six), H(Rank.Seven),
            });
            Assert.Equal(PatternType.Straight, p.Type);
            Assert.Equal(5, p.Length);
            Assert.Equal((int)Rank.Seven, p.MainRank);
        }

        [Fact]
        public void Straight_WithTwo_IsInvalid()
        {
            var p = PatternRecognizer.Recognize(new[]
            {
                S(Rank.Ten), H(Rank.Jack), D(Rank.Queen), S(Rank.King), H(Rank.Ace), S(Rank.Two),
            });
            Assert.Equal(PatternType.Invalid, p.Type);
        }

        [Fact]
        public void PairStraight_Valid()
        {
            var p = PatternRecognizer.Recognize(new[]
            {
                S(Rank.Three), H(Rank.Three), S(Rank.Four), H(Rank.Four), S(Rank.Five), H(Rank.Five),
            });
            Assert.Equal(PatternType.PairStraight, p.Type);
            Assert.Equal(3, p.Length);
        }

        [Fact]
        public void Plane_NoWings()
        {
            var p = PatternRecognizer.Recognize(new[]
            {
                S(Rank.Four), H(Rank.Four), D(Rank.Four),
                S(Rank.Five), H(Rank.Five), D(Rank.Five),
            });
            Assert.Equal(PatternType.Plane, p.Type);
            Assert.Equal(2, p.Length);
        }

        [Fact]
        public void Plane_WithSingles()
        {
            var p = PatternRecognizer.Recognize(new[]
            {
                S(Rank.Four), H(Rank.Four), D(Rank.Four),
                S(Rank.Five), H(Rank.Five), D(Rank.Five),
                S(Rank.Seven), S(Rank.Nine),
            });
            Assert.Equal(PatternType.PlaneSingle, p.Type);
        }

        [Fact]
        public void Bomb_And_Rocket()
        {
            var bomb = PatternRecognizer.Recognize(new[]
            {
                S(Rank.Two), H(Rank.Two), D(Rank.Two), Cl(Rank.Two),
            });
            Assert.Equal(PatternType.Bomb, bomb.Type);

            var rocket = PatternRecognizer.Recognize(new[] { C(52), C(53) });
            Assert.Equal(PatternType.Rocket, rocket.Type);

            Assert.True(rocket.Beats(bomb));
            Assert.True(bomb.Beats(new HandPattern(PatternType.Single, (int)Rank.BigJoker, 1, new[] { C(53) })));
        }

        [Fact]
        public void Beats_SameTypeCompare()
        {
            var low = PatternRecognizer.Recognize(new[] { S(Rank.Five) });
            var high = PatternRecognizer.Recognize(new[] { S(Rank.Eight) });
            var pair = PatternRecognizer.Recognize(new[] { S(Rank.Eight), H(Rank.Eight) });

            Assert.True(high.Beats(low));
            Assert.False(low.Beats(high));
            Assert.False(pair.Beats(high)); // 类型不同不能压
        }

        [Fact]
        public void FourTwo()
        {
            var fourTwo = PatternRecognizer.Recognize(new[]
            {
                S(Rank.Nine), H(Rank.Nine), D(Rank.Nine), Cl(Rank.Nine), S(Rank.Three), H(Rank.Four),
            });
            Assert.Equal(PatternType.FourTwo, fourTwo.Type);
        }
    }
}
