using System.Collections.Generic;
using System.Linq;

namespace DDZ.Core
{
    /// <summary>叫分 AI：按手牌强度评分决定叫 0/1/2/3。</summary>
    public static class BidAI
    {
        /// <summary>手牌强度分。</summary>
        public static int Evaluate(IEnumerable<Card> hand)
        {
            var cards = hand.ToList();
            double score = 0;

            foreach (var c in cards)
            {
                switch (c.Rank)
                {
                    case Rank.BigJoker: score += 8; break;
                    case Rank.SmallJoker: score += 6; break;
                    case Rank.Two: score += 4; break;
                    case Rank.Ace: score += 3; break;
                    case Rank.King: score += 1.5; break;
                }
            }

            // 炸弹
            foreach (var g in cards.GroupBy(c => c.Rank).Where(g => g.Count() == 4))
            {
                score += g.Key >= Rank.Two ? 12 : 8;
            }

            // 双王组合额外加成
            if (cards.Any(c => c.Rank == Rank.BigJoker) && cards.Any(c => c.Rank == Rank.SmallJoker))
                score += 4;

            return (int)score;
        }

        /// <summary>决定叫分。minBid 之上才可能叫；返回 0/1/2/3。</summary>
        public static int DecideBid(IEnumerable<Card> hand, int currentHighest)
        {
            int strength = Evaluate(hand);
            int willing = strength >= 22 ? 3 : strength >= 16 ? 2 : strength >= 11 ? 1 : 0;
            return willing > currentHighest ? willing : 0;
        }
    }
}
