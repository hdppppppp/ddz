using System.Collections.Generic;
using System.Linq;

namespace DDZ.Core
{
    /// <summary>出牌合法性校验 + 比牌。</summary>
    public static class PlayValidator
    {
        /// <summary>
        /// 校验一次出牌：
        /// freePlay=true 表示自由出牌（没人压或自己是上家出牌者），只要本身构成合法牌型即可；
        /// 否则必须能压过 last。
        /// </summary>
        public static bool Validate(IReadOnlyList<Card> cards, bool freePlay, HandPattern? last, out HandPattern pattern)
        {
            pattern = PatternRecognizer.Recognize(cards);
            if (pattern.Type == PatternType.Invalid)
                return false;

            if (freePlay)
                return true;

            if (last == null)
                return true;

            return pattern.Beats(last);
        }

        /// <summary>手牌里所有能压过 last 的组合（同型最小优先，含炸弹/王炸排在最后）。供 AI 跟牌与玩家提示。</summary>
        public static List<HandPattern> EnumerateBeats(IReadOnlyList<Card> hand, HandPattern last)
        {
            var result = new List<HandPattern>();
            if (last.Type == PatternType.Invalid)
                return result;

            var byRank = hand.GroupBy(c => c.Power).ToDictionary(g => g.Key, g => g.ToList());

            switch (last.Type)
            {
                case PatternType.Single:
                    AddSingles(byRank, last.MainRank, result);
                    break;
                case PatternType.Pair:
                    AddPairs(byRank, last.MainRank, result);
                    break;
                case PatternType.Triple:
                    AddTriples(byRank, last.MainRank, result);
                    break;
                case PatternType.TripleSingle:
                    AddTripleWithKickers(byRank, last, result, single: true);
                    break;
                case PatternType.TriplePair:
                    AddTripleWithKickers(byRank, last, result, single: false);
                    break;
                case PatternType.Straight:
                    AddStraights(byRank, last, result);
                    break;
                case PatternType.PairStraight:
                    AddPairStraights(byRank, last, result);
                    break;
                case PatternType.Plane:
                case PatternType.PlaneSingle:
                case PatternType.PlanePair:
                    AddPlanes(byRank, last, result);
                    break;
                case PatternType.FourTwo:
                case PatternType.FourTwoPair:
                    AddFourTwo(byRank, last, result);
                    break;
            }

            // 炸弹与王炸永远可以压（除王炸之外）
            if (last.Type != PatternType.Rocket)
            {
                foreach (var kv in byRank.Where(kv => kv.Value.Count == 4 && kv.Key > last.MainRank || kv.Value.Count == 4 && last.Type != PatternType.Bomb))
                {
                    // 炸弹必须比上家炸弹大（若上家是炸弹）
                    if (last.Type != PatternType.Bomb || kv.Key > last.MainRank)
                        result.Add(new HandPattern(PatternType.Bomb, kv.Key, 1, kv.Value));
                }

                if (byRank.TryGetValue((int)Rank.SmallJoker, out var small) &&
                    byRank.TryGetValue((int)Rank.BigJoker, out var big))
                {
                    result.Add(new HandPattern(
                        PatternType.Rocket, (int)Rank.BigJoker, 1,
                        small.Concat(big).ToList()));
                }
            }

            // 同型在前按主牌升序，炸弹王炸在后
            result.Sort((a, b) =>
            {
                bool aBomb = a.IsBombLike, bBomb = b.IsBombLike;
                if (aBomb != bBomb) return aBomb ? 1 : -1;
                return a.MainRank.CompareTo(b.MainRank);
            });
            return result;
        }

        private static void AddSingles(Dictionary<int, List<Card>> byRank, int minPower, List<HandPattern> result)
        {
            foreach (var kv in byRank.Where(kv => kv.Key > minPower))
                result.Add(new HandPattern(PatternType.Single, kv.Key, 1, new[] { kv.Value[0] }));
        }

        private static void AddPairs(Dictionary<int, List<Card>> byRank, int minPower, List<HandPattern> result)
        {
            foreach (var kv in byRank.Where(kv => kv.Value.Count >= 2 && kv.Key > minPower))
                result.Add(new HandPattern(PatternType.Pair, kv.Key, 1, kv.Value.Take(2).ToList()));
        }

        private static void AddTriples(Dictionary<int, List<Card>> byRank, int minPower, List<HandPattern> result)
        {
            foreach (var kv in byRank.Where(kv => kv.Value.Count >= 3 && kv.Key > minPower))
                result.Add(new HandPattern(PatternType.Triple, kv.Key, 1, kv.Value.Take(3).ToList()));
        }

        private static void AddTripleWithKickers(Dictionary<int, List<Card>> byRank, HandPattern last, List<HandPattern> result, bool single)
        {
            foreach (var kv in byRank.Where(kv => kv.Value.Count >= 3 && kv.Key > last.MainRank))
            {
                var kickers = byRank
                    .Where(k2 => k2.Key != kv.Key)
                    .SelectMany(k2 => k2.Value)
                    .Take(single ? 1 : 2)
                    .ToList();

                if (single)
                {
                    if (kickers.Count >= 1)
                        result.Add(new HandPattern(PatternType.TripleSingle, kv.Key, 1,
                            kv.Value.Take(3).Concat(kickers.Take(1)).ToList()));
                }
                else
                {
                    // 找一个≥2张的牌组当对子
                    var pair = byRank.FirstOrDefault(k2 => k2.Key != kv.Key && k2.Value.Count >= 2);
                    if (pair.Value != null)
                        result.Add(new HandPattern(PatternType.TriplePair, kv.Key, 1,
                            kv.Value.Take(3).Concat(pair.Value.Take(2)).ToList()));
                }
            }
        }

        private static void AddStraights(Dictionary<int, List<Card>> byRank, HandPattern last, List<HandPattern> result)
        {
            int len = last.Length;
            // 起点 from last.MainRank - len + 1 + 1 = main-len+2 → 保证终点 > last.MainRank
            for (int start = last.MainRank - len + 2; start + len - 1 <= (int)Rank.Ace; start++)
            {
                if (TryTakeRun(byRank, start, len, 1, out var cards))
                    result.Add(new HandPattern(PatternType.Straight, start + len - 1, len, cards));
            }
        }

        private static void AddPairStraights(Dictionary<int, List<Card>> byRank, HandPattern last, List<HandPattern> result)
        {
            int pairs = last.Length;
            for (int start = last.MainRank - pairs + 2; start + pairs - 1 <= (int)Rank.Ace; start++)
            {
                if (TryTakeRun(byRank, start, pairs, 2, out var cards))
                    result.Add(new HandPattern(PatternType.PairStraight, start + pairs - 1, pairs, cards));
            }
        }

        private static void AddPlanes(Dictionary<int, List<Card>> byRank, HandPattern last, List<HandPattern> result)
        {
            int n = last.Length;
            for (int start = last.MainRank - n + 2; start + n - 1 <= (int)Rank.Ace; start++)
            {
                if (!TryTakeRun(byRank, start, n, 3, out var body))
                    continue;

                int need = last.Type switch
                {
                    PatternType.Plane => 0,
                    PatternType.PlaneSingle => n,
                    _ => n * 2,
                };

                if (need == 0)
                {
                    result.Add(new HandPattern(PatternType.Plane, start + n - 1, n, body));
                    continue;
                }

                var usedPowers = new HashSet<int>(Enumerable.Range(start, n));
                var kickers = byRank
                    .Where(kv => !usedPowers.Contains(kv.Key))
                    .SelectMany(kv => kv.Value)
                    .Take(need)
                    .ToList();

                // 带对时简化处理：任意凑 2n 张（严格对子校验在 Recognize 层兜底）
                if (kickers.Count >= need)
                    result.Add(new HandPattern(last.Type, start + n - 1, n,
                        body.Concat(kickers).ToList()));
            }
        }

        private static void AddFourTwo(Dictionary<int, List<Card>> byRank, HandPattern last, List<HandPattern> result)
        {
            foreach (var kv in byRank.Where(kv => kv.Value.Count == 4 && kv.Key > last.MainRank))
            {
                int need = last.Type == PatternType.FourTwo ? 2 : 4;
                var kickers = byRank
                    .Where(k2 => k2.Key != kv.Key)
                    .SelectMany(k2 => k2.Value)
                    .Take(need)
                    .ToList();
                if (kickers.Count >= need)
                    result.Add(new HandPattern(last.Type, kv.Key, 1, kv.Value.Concat(kickers).ToList()));
            }
        }

        /// <summary>从 byRank 里取 start..start+len-1 每点 need 张；不足返回 false。</summary>
        private static bool TryTakeRun(
            Dictionary<int, List<Card>> byRank, int start, int len, int need, out List<Card> cards)
        {
            cards = new List<Card>();
            for (int p = start; p < start + len; p++)
            {
                if (!byRank.TryGetValue(p, out var list) || list.Count < need)
                    return false;
                cards.AddRange(list.Take(need));
            }
            return true;
        }
    }
}
