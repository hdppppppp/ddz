using System;
using System.Collections.Generic;
using System.Linq;

namespace DDZ.Core
{
    /// <summary>牌型类别。</summary>
    public enum PatternType
    {
        Single = 1,       // 单张
        Pair = 2,         // 对子
        Triple = 3,       // 三张
        TripleSingle = 4, // 三带一
        TriplePair = 5,   // 三带对
        Straight = 6,     // 顺子 (>=5 张连续单张, 3~A)
        PairStraight = 7, // 连对 (>=3 对连续, 3~A)
        Plane = 8,        // 飞机不带 (>=2 组连续三张, 3~A)
        PlaneSingle = 9,  // 飞机带单
        PlanePair = 10,   // 飞机带对
        FourTwo = 11,     // 四带二单
        FourTwoPair = 12, // 四带二对
        Bomb = 13,        // 炸弹
        Rocket = 14,      // 王炸
        Invalid = 0,
    }

    /// <summary>识别出的一个完整牌型。</summary>
    public sealed class HandPattern : IEquatable<HandPattern>
    {
        public static readonly HandPattern Invalid = new(PatternType.Invalid, 0, 0, Array.Empty<Card>());

        public HandPattern(PatternType type, int mainRank, int length, IReadOnlyList<Card> cards)
        {
            Type = type;
            MainRank = mainRank;
            Length = length;
            Cards = cards.ToArray();
        }

        /// <summary>牌型类别。</summary>
        public PatternType Type { get; }

        /// <summary>主牌的 Power 值（顺子/连对/飞机取最大那张的 Power；炸弹/三张取本身）。</summary>
        public int MainRank { get; }

        /// <summary>主体长度：顺子张数 / 连对对数 / 飞机三张组数；其他为 1。</summary>
        public int Length { get; }

        /// <summary>构成该牌型的所有牌。</summary>
        public Card[] Cards { get; }

        public int CardCount => Cards.Length;

        public bool IsBombLike => Type == PatternType.Bomb || Type == PatternType.Rocket;

        public bool Equals(HandPattern? other)
        {
            if (other is null) return false;
            return Type == other.Type && MainRank == other.MainRank && Length == other.Length;
        }

        public override bool Equals(object? obj) => Equals(obj as HandPattern);

        public override int GetHashCode() => HashCode.Combine(Type, MainRank, Length);

        public override string ToString()
        {
            string cards = string.Join(" ", Cards.Select(c => c.ToString()));
            return $"{Type}(main={MainRank},len={Length})[{cards}]";
        }

        /// <summary>
        /// 本牌型能否压过 other：
        /// 王炸压一切；炸弹压非炸弹、炸弹之间比大小；同型同长比主牌。
        /// </summary>
        public bool Beats(HandPattern other)
        {
            if (Type == PatternType.Invalid || other.Type == PatternType.Invalid)
                return false;
            if (other.Type == PatternType.Rocket)
                return false;
            if (Type == PatternType.Rocket)
                return true;
            if (Type == PatternType.Bomb && other.Type != PatternType.Bomb)
                return true;
            if (Type != other.Type || Length != other.Length)
                return false;
            return MainRank > other.MainRank;
        }
    }
}
