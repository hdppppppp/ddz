using System;
using System.Collections.Generic;
using System.Linq;

namespace DDZ.Core
{
    /// <summary>一副 54 张牌：3~2 各 4 张 + 双王。提供洗牌与按需发牌。</summary>
    public static class Deck
    {
        /// <summary>整副牌数量（含双王）。</summary>
        public const int TotalCount = 54;

        /// <summary>每人手牌数量（3 人局）。</summary>
        public const int HandCount = 17;

        /// <summary>底牌数量。</summary>
        public const int BottomCount = 3;

        private static readonly Random Rng = new();

        /// <summary>生成完整一副牌。</summary>
        public static List<Card> CreateFull()
        {
            var cards = new List<Card>(TotalCount);
            for (int id = 0; id < TotalCount; id++)
                cards.Add(new Card(id));
            return cards;
        }

        /// <summary>Fisher–Yates 洗牌，返回打乱后的新列表，不改入参。</summary>
        public static List<Card> Shuffled()
        {
            var cards = CreateFull();
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
            return cards;
        }

        /// <summary>按点数降序排序（大→小）。</summary>
        public static void SortDescending(List<Card> cards)
        {
            cards.Sort((a, b) => b.Power.CompareTo(a.Power));
        }

        /// <summary>按点数升序排序（小→大）。</summary>
        public static void SortAscending(List<Card> cards)
        {
            cards.Sort((a, b) => a.Power.CompareTo(b.Power));
        }

        /// <summary>统计每个 Rank 的张数，按 Power 升序返回。</summary>
        public static Dictionary<Rank, int> CountByRank(IEnumerable<Card> cards)
        {
            return cards
                .GroupBy(c => c.Rank)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}
