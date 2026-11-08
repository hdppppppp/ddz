using System.Collections.Generic;
using System.Linq;

namespace DDZ.Core
{
    /// <summary>
    /// 牌型识别器：输入一组牌，识别为唯一的最合理牌型（同组牌取最高优先级解释）。
    /// 纯静态实现，无状态。
    /// </summary>
    public static class PatternRecognizer
    {
        /// <summary>识别一组牌的牌型；不合法返回 Invalid。</summary>
        public static HandPattern Recognize(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0 || cards.Count > 20)
                return HandPattern.Invalid;

            var byRank = GroupByRank(cards);
            var pattern = TryRocket(byRank, cards)
                       ?? TryBomb(byRank, cards)
                       ?? TrySimple(byRank, cards)
                       ?? TryTripleFamily(byRank, cards)
                       ?? TryFourTwo(byRank, cards)
                       ?? TryStraight(byRank, cards)
                       ?? TryPairStraight(byRank, cards)
                       ?? TryPlane(byRank, cards);

            return pattern ?? HandPattern.Invalid;
        }

        private static SortedDictionary<int, List<Card>> GroupByRank(IReadOnlyList<Card> cards)
        {
            var dict = new SortedDictionary<int, List<Card>>();
            foreach (var c in cards)
            {
                if (!dict.TryGetValue(c.Power, out var list))
                    dict[c.Power] = list = new List<Card>();
                list.Add(c);
            }
            return dict;
        }

        // ---------- 王炸 / 炸弹 ----------

        private static HandPattern? TryRocket(SortedDictionary<int, List<Card>> byRank, IReadOnlyList<Card> cards)
        {
            if (cards.Count == 2 && byRank.ContainsKey((int)Rank.SmallJoker) && byRank.ContainsKey((int)Rank.BigJoker))
                return new HandPattern(PatternType.Rocket, (int)Rank.BigJoker, 1, cards);
            return null;
        }

        private static HandPattern? TryBomb(SortedDictionary<int, List<Card>> byRank, IReadOnlyList<Card> cards)
        {
            if (cards.Count == 4 && byRank.Count == 1)
            {
                var kv = byRank.First();
                return new HandPattern(PatternType.Bomb, kv.Key, 1, cards);
            }
            return null;
        }

        // ---------- 单/对/三张 ----------

        private static HandPattern? TrySimple(SortedDictionary<int, List<Card>> byRank, IReadOnlyList<Card> cards)
        {
            if (byRank.Count != 1)
                return null;
            var kv = byRank.First();
            return kv.Value.Count switch
            {
                1 => new HandPattern(PatternType.Single, kv.Key, 1, cards),
                2 => new HandPattern(PatternType.Pair, kv.Key, 1, cards),
                3 => new HandPattern(PatternType.Triple, kv.Key, 1, cards),
                _ => null,
            };
        }

        // ---------- 三带一 / 三带对 ----------

        private static HandPattern? TryTripleFamily(SortedDictionary<int, List<Card>> byRank, IReadOnlyList<Card> cards)
        {
            if (cards.Count != 4 && cards.Count != 5)
                return null;

            var triple = byRank.FirstOrDefault(kv => kv.Value.Count == 3);
            if (triple.Value == null || triple.Value.Count != 3)
                return null;

            int others = cards.Count - 3;
            bool kickerOk = others == 1
                ? byRank.Count == 2  // 三带一：另一张必须是不同点数的单张
                : byRank.Count == 2 && byRank.Any(kv => kv.Value.Count == 2); // 三带对

            if (!kickerOk)
                return null;

            var type = others == 1 ? PatternType.TripleSingle : PatternType.TriplePair;
            return new HandPattern(type, triple.Key, 1, cards);
        }

        // ---------- 四带二 ----------

        private static HandPattern? TryFourTwo(SortedDictionary<int, List<Card>> byRank, IReadOnlyList<Card> cards)
        {
            if (cards.Count != 6 && cards.Count != 8)
                return null;

            var quad = byRank.FirstOrDefault(kv => kv.Value.Count == 4);
            if (quad.Value == null || quad.Value.Count != 4)
                return null;

            if (cards.Count == 6)
            {
                // 四带二单：剩余 2 张任意单牌（可以是两张同点=一个对子，也允许）
                return new HandPattern(PatternType.FourTwo, quad.Key, 1, cards);
            }

            // 四带二对：剩余 4 张必须组成两对（或一个炸弹点数? 不允许，炸弹只能单独出）
            var rest = byRank.Where(kv => kv.Key != quad.Key).ToList();
            bool twoPairs = rest.All(kv => kv.Value.Count == 2) && rest.Count == 2;
            bool oneQuadKicker = rest.Count == 1 && rest[0].Value.Count == 4; // 带两个对子同点=4张同点，视为不合法
            if (twoPairs)
                return new HandPattern(PatternType.FourTwoPair, quad.Key, 1, cards);
            return null;
        }

        // ---------- 顺子 ----------

        private static HandPattern? TryStraight(SortedDictionary<int, List<Card>> byRank, IReadOnlyList<Card> cards)
        {
            if (cards.Count < 5 || byRank.Count != cards.Count)
                return null; // 顺子里不允许任何重复点数
            if (!IsConsecutiveNoTwo(byRank))
                return null;

            return new HandPattern(PatternType.Straight, byRank.Last().Key, cards.Count, cards);
        }

        // ---------- 连对 ----------

        private static HandPattern? TryPairStraight(SortedDictionary<int, List<Card>> byRank, IReadOnlyList<Card> cards)
        {
            if (cards.Count < 6 || cards.Count % 2 != 0)
                return null;
            if (byRank.Count != cards.Count / 2 || byRank.Any(kv => kv.Value.Count != 2))
                return null;
            if (!IsConsecutiveNoTwo(byRank))
                return null;

            return new HandPattern(PatternType.PairStraight, byRank.Last().Key, byRank.Count, cards);
        }

        // ---------- 飞机 ----------

        private static HandPattern? TryPlane(SortedDictionary<int, List<Card>> byRank, IReadOnlyList<Card> cards)
        {
            // 找最长连续三张序列
            var tripleRanks = byRank.Where(kv => kv.Value.Count >= 3).Select(kv => kv.Key).ToList();
            if (tripleRanks.Count == 0)
                return null;

            // 从每个起点找最长连续序列（不含 2 及以上：Power<=14）
            int bestStart = -1, bestLen = 0;
            foreach (int start in tripleRanks)
            {
                if (start > (int)Rank.Ace)
                    continue;
                int len = 1;
                while (tripleRanks.Contains(start + len) && start + len <= (int)Rank.Ace)
                    len++;
                if (len > bestLen)
                {
                    bestLen = len;
                    bestStart = start;
                }
            }

            if (bestLen < 2)
                return null;

            int tripleCount = bestLen;
            int mainMax = bestStart + bestLen - 1;

            // 飞机不带
            if (cards.Count == tripleCount * 3 && byRank.Count == tripleCount)
            {
                // 需要恰好是连续三张（无多余牌）
                var used = byRank.Where(kv => kv.Key >= bestStart && kv.Key <= mainMax).ToList();
                if (used.Count == tripleCount && used.All(kv => kv.Value.Count == 3))
                    return new HandPattern(PatternType.Plane, mainMax, tripleCount, cards);
                return null;
            }

            // 飞机带单：n 组三张 + n 张单（翅膀可以是任意单张，包括拆对，但不可拆炸弹——校验器层面限制）
            if (cards.Count == tripleCount * 4)
            {
                // 三张主体必须是 bestStart..mainMax 各 3 张，其余是翅膀
                var body = byRank.Where(kv => kv.Key >= bestStart && kv.Key <= mainMax).ToList();
                if (body.Count != tripleCount)
                    return null;
                // 翅膀数量校验：总张数满足且剩下的都是单张性质（允许同点）
                return new HandPattern(PatternType.PlaneSingle, mainMax, tripleCount, cards);
            }

            // 飞机带对：n 组三张 + n 个对子
            if (cards.Count == tripleCount * 5)
            {
                var body = byRank.Where(kv => kv.Key >= bestStart && kv.Key <= mainMax).ToList();
                if (body.Count != tripleCount)
                    return null;
                return new HandPattern(PatternType.PlanePair, mainMax, tripleCount, cards);
            }

            return null;
        }

        /// <summary>判断 byRank 的键是否连续（差1递增），且不含 2 及以上（Power<=14）。</summary>
        private static bool IsConsecutiveNoTwo(SortedDictionary<int, List<Card>> byRank)
        {
            int prev = -1;
            foreach (int power in byRank.Keys)
            {
                if (power > (int)Rank.Ace)
                    return false;
                if (prev >= 0 && power != prev + 1)
                    return false;
                prev = power;
            }
            return true;
        }
    }
}
