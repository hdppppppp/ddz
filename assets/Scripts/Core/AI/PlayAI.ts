import { Card, Rank } from '../Model/Card';
import { HandPattern, PatternType, recognize } from '../Pattern/HandPattern';
import { enumerateBeats } from '../Rules/PlayValidator';
import { GameController } from '../Flow/GameController';

/** 决定一次 AI 动作。null = 不出（Pass）。 */
export function decidePlay(game: GameController, seat: number): Card[] | null {
    const hand = game.players[seat].hand;
    const last = game.lastPlay;
    const freePlay = game.isFreePlay(seat);

    if (freePlay) return chooseFreePlay(hand);

    const candidates = enumerateBeats(hand, last!);
    if (candidates.length === 0) return null;

    // 全部一手走完 → 直接打
    const finishing = candidates.find(c => c.cardCount === hand.length);
    if (finishing) return [...finishing.cards];

    // 对手剩牌 ≤2 → 有炸弹就压
    const isLandlord = game.players[seat].isLandlord;
    for (const p of game.players) {
        if (p.seat === seat || p.isLandlord === isLandlord) continue;
        if (p.handCount <= 2) {
            const bomb = candidates[candidates.length - 1];
            if (bomb.isBombLike) return [...bomb.cards];
        }
    }

    // 常规跟牌：最小可压
    const normal = candidates[0];

    // 农民不压队友的大牌
    if (!isLandlord && game.lastPlaySeat !== seat) {
        const teammate = 3 - seat - game.landlordSeat;
        if (game.lastPlaySeat === teammate && isBigEnough(normal)) return null;
    }

    return [...normal.cards];
}

function isBigEnough(p: HandPattern): boolean {
    switch (p.type) {
        case PatternType.Single: return p.mainRank >= Rank.Ace;
        case PatternType.Pair: return p.mainRank >= Rank.Two;
        case PatternType.Triple: return p.mainRank >= Rank.Two;
        default: return p.cardCount >= 6 || p.isBombLike;
    }
}

/** 自由出牌：剩一手全出；否则先出最长最小组。 */
function chooseFreePlay(hand: Card[]): Card[] {
    const whole = recognize(hand);
    if (whole.type !== PatternType.Invalid) return [...hand];

    const decomposed = decompose(hand);

    // 只剩两手：先出大的逼牌
    if (decomposed.length === 2) {
        const first = [...decomposed].sort((a, b) => {
            const ab = a.isBombLike ? 0 : 1;
            const bb = b.isBombLike ? 0 : 1;
            if (ab !== bb) return ab - bb;
            return b.cardCount - a.cardCount;
        })[0];
        return [...first.cards];
    }

    const play = [...decomposed]
        .filter(p => !p.isBombLike)
        .sort((a, b) => b.cardCount - a.cardCount || a.mainRank - b.mainRank)[0];

    if (play) return [...play.cards];

    return [...decomposed.sort((a, b) => a.mainRank - b.mainRank)[0].cards];
}

/** 贪心手牌分解：王炸/炸弹保留 → 飞机 → 顺子 → 连对 → 三张 → 对子 → 单张。 */
export function decompose(hand: Card[]): HandPattern[] {
    const pool = new Map<number, Card[]>();
    for (const c of hand) {
        const list = pool.get(c.power);
        if (list) list.push(c); else pool.set(c.power, [c]);
    }
    const result: HandPattern[] = [];

    // 王炸
    const small = pool.get(Rank.SmallJoker);
    const big = pool.get(Rank.BigJoker);
    if (small && big) {
        result.push(new HandPattern(PatternType.Rocket, Rank.BigJoker, 1, [...small, ...big]));
        pool.delete(Rank.SmallJoker);
        pool.delete(Rank.BigJoker);
    }
    // 炸弹
    for (const [power, cards] of [...pool]) {
        if (cards.length === 4) {
            result.push(new HandPattern(PatternType.Bomb, power, 1, cards));
            pool.delete(power);
        }
    }
    // 飞机（≥2 组连续三张）
    extractRuns(pool, result, 3, 2, PatternType.Plane);
    // 顺子（≥5）
    extractRuns(pool, result, 1, 5, PatternType.Straight);
    // 连对（≥3）
    extractRuns(pool, result, 2, 3, PatternType.PairStraight);
    // 三张
    for (const [power, cards] of [...pool]) {
        if (cards.length === 3) {
            result.push(new HandPattern(PatternType.Triple, power, 1, cards));
            pool.delete(power);
        }
    }
    // 对子
    for (const [power, cards] of [...pool]) {
        if (cards.length === 2) {
            result.push(new HandPattern(PatternType.Pair, power, 1, cards));
            pool.delete(power);
        }
    }
    // 单张
    for (const [power, cards] of [...pool]) {
        if (cards.length >= 1) {
            result.push(new HandPattern(PatternType.Single, power, 1, [cards[0]]));
            pool.delete(power);
        }
    }
    return result;
}

function extractRuns(
    pool: Map<number, Card[]>, result: HandPattern[],
    take: number, minLen: number, type: PatternType,
): void {
    let extracted = true;
    while (extracted) {
        extracted = false;
        const powers = [...pool.keys()]
            .filter(k => pool.get(k)!.length >= take && k <= Rank.Ace)
            .sort((a, b) => a - b);

        let start = -1, len = 1;
        for (const p of [...powers, Number.MAX_SAFE_INTEGER]) {
            if (start < 0) { start = p; len = 1; continue; }
            if (p === start + len) { len++; continue; }

            if (len >= minLen && start + len - 1 <= Rank.Ace) {
                const cards: Card[] = [];
                for (let q = start; q < start + len; q++) {
                    const list = pool.get(q)!;
                    cards.push(...list.slice(0, take));
                    if (list.length === take) pool.delete(q);
                    else pool.set(q, list.slice(take));
                }
                result.push(new HandPattern(type, start + len - 1, len, cards));
                extracted = true;
                break;
            }
            start = p;
            len = 1;
        }
    }
}
