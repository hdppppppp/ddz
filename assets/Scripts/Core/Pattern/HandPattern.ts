import { Card, Rank } from '../Model/Card';

/** 牌型类别。 */
export enum PatternType {
    Invalid = 0,
    Single = 1, Pair = 2, Triple = 3,
    TripleSingle = 4, TriplePair = 5,
    Straight = 6, PairStraight = 7,
    Plane = 8, PlaneSingle = 9, PlanePair = 10,
    FourTwo = 11, FourTwoPair = 12,
    Bomb = 13, Rocket = 14,
}

/** 识别出的牌型。 */
export class HandPattern {
    static readonly Invalid = new HandPattern(PatternType.Invalid, 0, 0, []);

    constructor(
        public readonly type: PatternType,
        /** 主牌 power（顺子/连对/飞机取最大一张）。 */
        public readonly mainRank: number,
        /** 主体长度：顺子张数 / 连对对数 / 飞机三张组数；其余 1。 */
        public readonly length: number,
        public readonly cards: Card[],
    ) { }

    get cardCount(): number { return this.cards.length; }
    get isBombLike(): boolean { return this.type === PatternType.Bomb || this.type === PatternType.Rocket; }

    equals(other: HandPattern): boolean {
        return this.type === other.type && this.mainRank === other.mainRank && this.length === other.length;
    }

    toString(): string {
        return `${PatternType[this.type]}(main=${this.mainRank},len=${this.length})[${this.cards.map(c => c.toString()).join(' ')}]`;
    }

    /** 能否压过 other。 */
    beats(other: HandPattern): boolean {
        if (this.type === PatternType.Invalid || other.type === PatternType.Invalid) return false;
        if (other.type === PatternType.Rocket) return false;
        if (this.type === PatternType.Rocket) return true;
        if (this.type === PatternType.Bomb && other.type !== PatternType.Bomb) return true;
        if (this.type !== other.type || this.length !== other.length) return false;
        return this.mainRank > other.mainRank;
    }
}

interface RankGroup { power: number; cards: Card[]; count: number; }

function groupByRank(cards: Card[]): RankGroup[] {
    const map = new Map<number, Card[]>();
    for (const c of cards) {
        const list = map.get(c.power);
        if (list) list.push(c); else map.set(c.power, [c]);
    }
    return [...map.entries()]
        .sort((a, b) => a[0] - b[0])
        .map(([power, cs]) => ({ power, cards: cs, count: cs.length }));
}

/** 判断分组 power 是否连续且 ≤ A。 */
function consecutiveNoTwo(groups: RankGroup[]): boolean {
    let prev = -1;
    for (const g of groups) {
        if (g.power > Rank.Ace) return false;
        if (prev >= 0 && g.power !== prev + 1) return false;
        prev = g.power;
    }
    return true;
}

/** 牌型识别器：一组牌取最合理（最高优先级）解释。 */
export function recognize(cards: Card[]): HandPattern {
    if (!cards.length || cards.length > 20) return HandPattern.Invalid;

    const byRank = groupByRank(cards);

    // 王炸
    if (cards.length === 2 && byRank.some(g => g.power === Rank.SmallJoker) && byRank.some(g => g.power === Rank.BigJoker)) {
        return new HandPattern(PatternType.Rocket, Rank.BigJoker, 1, cards);
    }
    // 炸弹
    if (cards.length === 4 && byRank.length === 1) {
        return new HandPattern(PatternType.Bomb, byRank[0].power, 1, cards);
    }
    // 单/对/三
    if (byRank.length === 1) {
        const g = byRank[0];
        if (g.count === 1) return new HandPattern(PatternType.Single, g.power, 1, cards);
        if (g.count === 2) return new HandPattern(PatternType.Pair, g.power, 1, cards);
        if (g.count === 3) return new HandPattern(PatternType.Triple, g.power, 1, cards);
        return HandPattern.Invalid;
    }
    // 三带一/三带对
    if (cards.length === 4 || cards.length === 5) {
        const triple = byRank.find(g => g.count === 3);
        if (triple) {
            const others = byRank.filter(g => g.power !== triple.power);
            if (cards.length === 4 && others.length === 1 && others[0].count === 1) {
                return new HandPattern(PatternType.TripleSingle, triple.power, 1, cards);
            }
            if (cards.length === 5 && others.length === 1 && others[0].count === 2) {
                return new HandPattern(PatternType.TriplePair, triple.power, 1, cards);
            }
        }
    }
    // 四带二/四带两对
    if (cards.length === 6 || cards.length === 8) {
        const quad = byRank.find(g => g.count === 4);
        if (quad) {
            const rest = byRank.filter(g => g.power !== quad.power);
            if (cards.length === 6) {
                return new HandPattern(PatternType.FourTwo, quad.power, 1, cards);
            }
            if (rest.length === 2 && rest.every(g => g.count === 2)) {
                return new HandPattern(PatternType.FourTwoPair, quad.power, 1, cards);
            }
        }
    }
    // 顺子
    if (cards.length >= 5 && byRank.length === cards.length && consecutiveNoTwo(byRank)) {
        return new HandPattern(PatternType.Straight, byRank[byRank.length - 1].power, cards.length, cards);
    }
    // 连对
    if (cards.length >= 6 && cards.length % 2 === 0
        && byRank.length === cards.length / 2
        && byRank.every(g => g.count === 2)
        && consecutiveNoTwo(byRank)) {
        return new HandPattern(PatternType.PairStraight, byRank[byRank.length - 1].power, byRank.length, cards);
    }
    // 飞机（不带/带单/带对）
    const plane = tryPlane(byRank, cards);
    if (plane) return plane;

    return HandPattern.Invalid;
}

function tryPlane(byRank: RankGroup[], cards: Card[]): HandPattern | null {
    const triplePowers = byRank.filter(g => g.count >= 3 && g.power <= Rank.Ace).map(g => g.power);
    if (triplePowers.length < 2) return null;

    // 找最长连续三张序列
    let bestStart = -1, bestLen = 0;
    for (const start of triplePowers) {
        let len = 1;
        while (triplePowers.includes(start + len)) len++;
        if (len > bestLen) { bestLen = len; bestStart = start; }
    }
    if (bestLen < 2) return null;

    const n = bestLen;
    const mainMax = bestStart + n - 1;

    if (cards.length === n * 3 && byRank.length === n) {
        return new HandPattern(PatternType.Plane, mainMax, n, cards);
    }
    if (cards.length === n * 4) {
        // 带单
        const body = byRank.filter(g => g.power >= bestStart && g.power <= mainMax);
        if (body.length === n && body.every(g => g.count === 3)) {
            return new HandPattern(PatternType.PlaneSingle, mainMax, n, cards);
        }
        return null;
    }
    if (cards.length === n * 5) {
        // 带对
        const body = byRank.filter(g => g.power >= bestStart && g.power <= mainMax);
        if (body.length === n && body.every(g => g.count === 3)) {
            return new HandPattern(PatternType.PlanePair, mainMax, n, cards);
        }
        return null;
    }
    return null;
}
