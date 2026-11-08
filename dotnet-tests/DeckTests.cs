using System;
using System.Collections.Generic;
using System.Linq;
using DDZ.Core;
using Xunit;

namespace DDZ.Core.Tests
{
    public class DeckTests
    {
        [Fact]
        public void CreateFull_Returns54Cards()
        {
            var deck = Deck.CreateFull();
            Assert.Equal(54, deck.Count);
            Assert.Equal(54, deck.Select(c => c.Id).Distinct().Count());
        }

        [Fact]
        public void Shuffled_KeepsAllCardsAndChangesOrder()
        {
            var original = Deck.CreateFull();
            var shuffled = Deck.Shuffled();

            Assert.Equal(54, shuffled.Count);
            Assert.Equal(original.Select(c => c.Id).OrderBy(x => x),
                         shuffled.Select(c => c.Id).OrderBy(x => x));

            // 顺序应与原序不同（概率上 1/54! 级别，不可能连续失败）
            Assert.NotEqual(original.Select(c => c.Id), shuffled.Select(c => c.Id));
        }

        [Fact]
        public void Shuffled_Distributes51ToHands3ToBottom()
        {
            var shuffled = Deck.Shuffled();
            var hands = new List<List<Card>>();
            int cursor = 0;
            for (int i = 0; i < 3; i++)
            {
                hands.Add(shuffled.Skip(cursor).Take(Deck.HandCount).ToList());
                cursor += Deck.HandCount;
            }
            var bottom = shuffled.Skip(cursor).Take(Deck.BottomCount).ToList();

            Assert.All(hands, h => Assert.Equal(17, h.Count));
            Assert.Equal(3, bottom.Count);
            Assert.Equal(54, hands.SelectMany(h => h).Concat(bottom).Select(c => c.Id).Distinct().Count());
        }

        [Fact]
        public void Card_JokersMapCorrectly()
        {
            var small = new Card(52);
            var big = new Card(53);

            Assert.Equal(Rank.SmallJoker, small.Rank);
            Assert.Equal(Rank.BigJoker, big.Rank);
            Assert.True(small.IsJoker);
            Assert.True(big.IsJoker);
            Assert.True(big.Power > small.Power);
        }

        [Fact]
        public void Card_ConstructorByRankSuit_RoundTrips()
        {
            var card = new Card(Rank.Two, Suit.Hearts);
            Assert.Equal(Rank.Two, card.Rank);
            Assert.Equal(Suit.Hearts, card.Suit);
            Assert.False(card.IsJoker);

            var ace = new Card(Rank.Ace, Suit.Spades);
            Assert.True(card.Power > ace.Power);
        }

        [Fact]
        public void Card_InvalidId_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Card(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Card(54));
        }

        [Fact]
        public void Card_All54Ids_RoundTripRankAndSuit()
        {
            for (int id = 0; id < 54; id++)
            {
                var card = new Card(id);
                var rebuilt = new Card(card.Rank, card.Suit);
                Assert.True(rebuilt == card, $"Id={id} round-trip 失败: {card} -> {card.Rank}/{card.Suit}");
            }

            // 关键映射抽查
            Assert.Equal(Rank.Two, new Card(12).Rank);            // ♠2
            Assert.Equal(Rank.Three, new Card(0).Rank);           // ♠3
            Assert.Equal(Rank.Ace, new Card(11).Rank);            // ♠A
            Assert.Equal(Rank.Two, new Card(25).Rank);            // ♥2
            Assert.Equal(Rank.Ace, new Card(24).Rank);            // ♥A

            // 每个点数恰好 4 张（王除外）
            var full = Deck.CreateFull();
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
            {
                if (rank == Rank.SmallJoker || rank == Rank.BigJoker)
                    continue;
                Assert.Equal(4, full.Count(c => c.Rank == rank));
            }
        }
    }
}
