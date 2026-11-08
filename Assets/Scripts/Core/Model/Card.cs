using System;

namespace DDZ.Core
{
    /// <summary>牌面点数，比较与匹配只看 Rank，不看花色。</summary>
    public enum Rank
    {
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14,
        Two = 16,
        SmallJoker = 18,
        BigJoker = 19,
    }

    /// <summary>花色，仅用于展示与发牌去重，不参与比牌。</summary>
    public enum Suit
    {
        Spades,
        Hearts,
        Clubs,
        Diamonds,
        None = 99,
    }

    /// <summary>一张牌。Id 在一副牌内唯一（0..53），保证可复制、可排序。</summary>
    public readonly struct Card : IEquatable<Card>
    {
        public static readonly string[] RankNames =
        {
            "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A", "2", "小王", "大王",
        };

        public Card(int id)
        {
            if (id < 0 || id > 53)
                throw new ArgumentOutOfRangeException(nameof(id), id, "牌 Id 必须在 0..53 之间");
            Id = id;
        }

        public Card(Rank rank, Suit suit)
        {
            if (rank == Rank.SmallJoker || rank == Rank.BigJoker)
            {
                Id = rank == Rank.SmallJoker ? 52 : 53;
                return;
            }

            int suitIndex = suit switch
            {
                Suit.Spades => 0,
                Suit.Hearts => 1,
                Suit.Clubs => 2,
                Suit.Diamonds => 3,
                _ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "普通牌必须指定花色"),
            };

            // 普通牌序号 0..12：Three..Ace 直接用枚举值偏移，Two 固定占 12 号位
            int ordinal = rank == Rank.Two ? 12 : (int)rank - (int)Rank.Three;
            Id = suitIndex * 13 + ordinal;
        }

        /// <summary>唯一编号 0..53。</summary>
        public int Id { get; }

        public Rank Rank => Id switch
        {
            52 => Rank.SmallJoker,
            53 => Rank.BigJoker,
            // 序号 0..11 → Three..Ace；12 → Two
            _ => Id % 13 == 12 ? Rank.Two : (Rank)(Id % 13 + (int)Rank.Three),
        };

        public Suit Suit => Id switch
        {
            52 or 53 => Suit.None,
            _ => (Suit)(Id / 13),
        };

        /// <summary>比牌权值（王 > 2 > A > K > …）。比牌统一用这个值。</summary>
        public int Power => (int)Rank;

        public bool IsJoker => Id >= 52;

        public bool Equals(Card other) => Id == other.Id;

        public override bool Equals(object? obj) => obj is Card other && Equals(other);

        public override int GetHashCode() => Id;

        public override string ToString()
        {
            string rankName = RankNames[(int)Rank - (int)Rank.Three];
            if (IsJoker)
                return rankName;

            char suitSymbol = Suit switch
            {
                Suit.Spades => '♠',
                Suit.Hearts => '♥',
                Suit.Clubs => '♣',
                Suit.Diamonds => '♦',
                _ => '?',
            };
            return $"{suitSymbol}{rankName}";
        }

        public static bool operator ==(Card a, Card b) => a.Id == b.Id;

        public static bool operator !=(Card a, Card b) => a.Id != b.Id;
    }
}
