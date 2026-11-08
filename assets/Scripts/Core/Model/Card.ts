/** 牌面点数（power 即比牌权值：王 > 2 > A > K > …）。 */
export enum Rank {
    Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7, Eight = 8, Nine = 9,
    Ten = 10, Jack = 11, Queen = 12, King = 13, Ace = 14, Two = 16,
    SmallJoker = 18, BigJoker = 19,
}

/** 花色（仅展示用）。 */
export enum Suit { Spades, Hearts, Clubs, Diamonds, None = 99 }

const RANK_NAMES = ['3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K', 'A', '2', '小王', '大王'];

const SUIT_SYMBOLS: Record<number, string> = {
    [Suit.Spades]: '♠', [Suit.Hearts]: '♥', [Suit.Clubs]: '♣', [Suit.Diamonds]: '♦',
};

/** 一张牌。id 0..53 唯一：花色*13 + 序号(3=0..A=11, 2=12)；52/53=双王。 */
export class Card {
    static readonly TOTAL = 54;

    constructor(public readonly id: number) {
        if (id < 0 || id > 53) throw new RangeError(`invalid card id: ${id}`);
    }

    static byRank(rank: Rank, suit: Suit): Card {
        if (rank === Rank.SmallJoker) return new Card(52);
        if (rank === Rank.BigJoker) return new Card(53);
        const suitIndex = suit === Suit.Spades ? 0 : suit === Suit.Hearts ? 1 : suit === Suit.Clubs ? 2 : 3;
        const ordinal = rank === Rank.Two ? 12 : (rank as number) - Rank.Three;
        return new Card(suitIndex * 13 + ordinal);
    }

    get rank(): Rank {
        if (this.id >= 52) return this.id === 52 ? Rank.SmallJoker : Rank.BigJoker;
        const ordinal = this.id % 13;
        return ordinal === 12 ? Rank.Two : (ordinal + Rank.Three) as Rank;
    }

    get suit(): Suit {
        if (this.id >= 52) return Suit.None;
        return this.id / 13 | 0;
    }

    get power(): number { return this.rank as number; }
    get isJoker(): boolean { return this.id >= 52; }

    equals(other: Card): boolean { return this.id === other.id; }

    toString(): string {
        if (this.isJoker) return this.rank === Rank.SmallJoker ? '小王' : '大王';
        const ordinal = this.rank === Rank.Two ? 12 : (this.rank as number) - Rank.Three;
        return `${SUIT_SYMBOLS[this.suit]}${RANK_NAMES[ordinal]}`;
    }
}

export function sortAsc(cards: Card[]): Card[] {
    return [...cards].sort((a, b) => a.power - b.power);
}

export function sortDesc(cards: Card[]): Card[] {
    return [...cards].sort((a, b) => b.power - a.power);
}

export function createFullDeck(): Card[] {
    const deck: Card[] = [];
    for (let id = 0; id < Card.TOTAL; id++) deck.push(new Card(id));
    return deck;
}

/** Fisher–Yates 洗牌，返回新数组。 */
export function shuffle(): Card[] {
    const cards = createFullDeck();
    for (let i = cards.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [cards[i], cards[j]] = [cards[j], cards[i]];
    }
    return cards;
}

export const HAND_COUNT = 17;
export const BOTTOM_COUNT = 3;
