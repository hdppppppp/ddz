using System;
using System.Collections.Generic;
using System.Linq;

namespace DDZ.Core
{
    /// <summary>
    /// 出牌 AI（贪心 + 规则启发）：手牌分解 → 自由出牌选最长最小组 / 跟牌选最小可压组合，
    /// 带农民配合与炸弹时机判断。同一枚举逻辑供玩家"提示"复用。
    /// </summary>
    public static class PlayAI
    {
        /// <summary>决定一次 AI 动作。返回 null 表示不出（Pass）；否则返回要出的牌。</summary>
        public static IReadOnlyList<Card>? Decide(GameController game, int seat)
        {
            var hand = game.Players[seat].Hand;
            var last = game.LastPlay;
            bool freePlay = last == null || game.LastPlaySeat == seat;

            if (freePlay)
                return ChooseFreePlay(hand);
            var candidates = PlayValidator.EnumerateBeats(hand, last!);
            if (candidates.Count == 0)
                return null;

            // 全部一手走完 → 直接打
            var finishing = candidates.FirstOrDefault(c => c.CardCount == hand.Count);
            if (finishing != null)
                return finishing.Cards;

            if (ShouldUseBombOrHigh(game, seat, candidates, out var forced))
                return forced.Cards;

            // 常规跟牌：取最小可压（EnumerateBeats 已排序，炸弹在尾部）
            var normal = candidates[0];

            // 农民不压队友的大牌
            if (!game.Players[seat].IsLandlord && game.LastPlaySeat != seat)
            {
                int teammate = 3 - seat - game.LandlordSeat;
                if (game.LastPlaySeat == teammate && IsBigEnough(normal))
                    return null;
            }

            // 对手快跑完时能压就压（不留情面）
            int nextSeat = (seat + 1) % 3;
            if (!game.Players[nextSeat].IsLandlord == game.Players[seat].IsLandlord)
            {
                // 下家是队友且自己不是地主：不压小的
                if (normal.Type != PatternType.Single || normal.MainRank < (int)Rank.Ace)
                {
                    if (hand.Count > 4)
                        return null;
                }
            }

            return normal.Cards;
        }

        /// <summary>玩家"提示"：与 AI 同源，返回一组可出的牌或 null（不出）。</summary>
        public static IReadOnlyList<Card>? Hint(GameController game, int seat)
            => Decide(game, seat);

        private static IReadOnlyList<Card> ChooseFreePlay(IReadOnlyList<Card> hand)
        {
            // 只剩一手直接全出（拷贝，不返回手牌活引用）
            var whole = PatternRecognizer.Recognize(hand);
            if (whole.Type != PatternType.Invalid)
                return hand.ToList();

            // 手牌分解：优先拆出"最小主牌"的最小组合
            var decomposed = Decompose(hand);

            // 如果只剩两手，先出带翅膀的组合或大组合逼牌
            if (decomposed.Count == 2)
            {
                var first = decomposed
                    .OrderByDescending(p => p.Type == PatternType.Bomb || p.Type == PatternType.Rocket ? 0 : 1)
                    .ThenByDescending(p => p.CardCount)
                    .First();
                return first.Cards;
            }

            // 常规：先出最长的非炸弹组合（顺子/连对/飞机优先），再出单张最小
            var play = decomposed
                .Where(p => !p.IsBombLike)
                .OrderByDescending(p => p.CardCount)
                .ThenBy(p => p.MainRank)
                .FirstOrDefault();

            if (play != null)
                return play.Cards;

            // 全是炸弹（罕见）：出最小的炸弹
            return decomposed.OrderBy(p => p.MainRank).First().Cards;
        }

        /// <summary>
        /// 贪心手牌分解：优先提取王炸/炸弹（保留），然后飞机、顺子、连对、三张、对子、单张。
        /// </summary>
        public static List<HandPattern> Decompose(IReadOnlyList<Card> hand)
        {
            var pool = hand.GroupBy(c => c.Power).ToDictionary(g => g.Key, g => g.ToList());
            var result = new List<HandPattern>();

            // 王炸
            if (pool.TryGetValue((int)Rank.SmallJoker, out var small) &&
                pool.TryGetValue((int)Rank.BigJoker, out var big))
            {
                result.Add(new HandPattern(PatternType.Rocket, (int)Rank.BigJoker, 1,
                    small.Concat(big).ToList()));
                pool.Remove((int)Rank.SmallJoker);
                pool.Remove((int)Rank.BigJoker);
            }

            // 炸弹保留
            foreach (var kv in pool.Where(kv => kv.Value.Count == 4).ToList())
            {
                result.Add(new HandPattern(PatternType.Bomb, kv.Key, 1, kv.Value));
                pool.Remove(kv.Key);
            }

            // 飞机（连续三张 >=2 组）
            ExtractRuns(pool, result, 3, 2, PatternType.Plane);

            // 顺子（>=5）
            ExtractRuns(pool, result, 1, 5, PatternType.Straight);

            // 连对（>=3 对）
            ExtractRuns(pool, result, 2, 3, PatternType.PairStraight);

            // 三张
            foreach (var kv in pool.Where(kv => kv.Value.Count == 3).ToList())
            {
                result.Add(new HandPattern(PatternType.Triple, kv.Key, 1, kv.Value));
                pool.Remove(kv.Key);
            }

            // 对子
            foreach (var kv in pool.Where(kv => kv.Value.Count == 2).ToList())
            {
                result.Add(new HandPattern(PatternType.Pair, kv.Key, 1, kv.Value));
                pool.Remove(kv.Key);
            }

            // 单张
            foreach (var kv in pool.Where(kv => kv.Value.Count >= 1).ToList())
            {
                result.Add(new HandPattern(PatternType.Single, kv.Key, 1,
                    new List<Card> { kv.Value[0] }));
                pool.Remove(kv.Key);
            }

            return result;
        }

        /// <summary>从池里提取连续序列（每个点取 take 张，至少 minLen 个点），提取后从池中扣除。</summary>
        private static void ExtractRuns(
            Dictionary<int, List<Card>> pool, List<HandPattern> result,
            int take, int minLen, PatternType type)
        {
            bool extracted = true;
            while (extracted)
            {
                extracted = false;
                var powers = pool.Keys.Where(k => pool[k].Count >= take && k <= (int)Rank.Ace).OrderBy(k => k).ToList();

                int start = -1, len = 1;
                foreach (var p in powers.Concat(new[] { int.MaxValue }))
                {
                    if (start < 0) { start = p; len = 1; continue; }
                    if (p == start + len) { len++; continue; }

                    if (len >= minLen && start + len - 1 <= (int)Rank.Ace)
                    {
                        var cards = new List<Card>();
                        for (int q = start; q < start + len; q++)
                        {
                            cards.AddRange(pool[q].Take(take));
                            if (pool[q].Count == take) pool.Remove(q); else pool[q].RemoveRange(0, take);
                        }
                        result.Add(new HandPattern(type, start + len - 1, len, cards));
                        extracted = true;
                        break; // 重扫
                    }
                    start = p;
                    len = 1;
                }
            }
        }

        private static bool IsBigEnough(HandPattern p)
        {
            return p.Type switch
            {
                PatternType.Single => p.MainRank >= (int)Rank.Ace,
                PatternType.Pair => p.MainRank >= (int)Rank.Two,
                PatternType.Triple => p.MainRank >= (int)Rank.Two,
                _ => p.CardCount >= 6 || p.IsBombLike,
            };
        }

        private static bool ShouldUseBombOrHigh(
            GameController game, int seat, List<HandPattern> candidates, out HandPattern chosen)
        {
            chosen = candidates[candidates.Count - 1]; // 通常是王炸/最大炸弹
            var hand = game.Players[seat].Hand;

            // 1) 出完这手就能赢 → 用最大
            var finishByAny = candidates.FirstOrDefault(c => c.CardCount == hand.Count);
            if (finishByAny != null) { chosen = finishByAny; return true; }

            // 2) 对手剩牌 <= 2 → 有炸弹/大牌就压
            bool isLandlord = game.Players[seat].IsLandlord;
            foreach (var p in game.Players)
            {
                if (p.Seat == seat || p.IsLandlord == isLandlord) continue;
                if (p.HandCount <= 2)
                {
                    var bomb = candidates.LastOrDefault(c => c.IsBombLike);
                    if (bomb != null) { chosen = bomb; return true; }
                }
            }

            // 3) 自己下一手能走完（剩两手，这手不是炸弹时）：保留炸弹，用普通牌压
            return false;
        }
    }
}
