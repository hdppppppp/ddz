import { Card } from '../Model/Card';
import { HandPattern, PatternType, recognize } from '../Pattern/HandPattern';

export interface ValidateResult {
    valid: boolean;
    pattern: HandPattern | null;
}

/** 出牌合法性校验 + 比牌。 */
export function validate(cards: Card[], freePlay: boolean, last: HandPattern | null): ValidateResult {
    const pattern = recognize(cards);
    if (pattern.type === PatternType.Invalid) return { valid: false, pattern: null };
    if (freePlay || !last) return { valid: true, pattern };
    return { valid: pattern.beats(last), pattern };
}

interface RankGroup { power: number; cards: Card[]; count: number; }

function groupByRank(hand: Card[]): Map<number, RankGroup> {
    const map = new Map<number, RankGroup>();
    for (const c of hand) {
        let g = map.get(c.power);
        if (!g) { g = { power: c.power, cards: [], count: 0 }; map.set(c.power, g); }
        g.cards.push(c);
        g.count++;
    }
    return map;
}

/**
 * 枚举手牌里所有能压过 last 的组合（同型按主牌升序，炸弹/王炸在尾部）。
 * 供 AI 跟牌与玩家提示。
 */
export function enumerateBeats(hand: Card[], last: HandPattern): HandPattern[] {
    const result: HandPattern[] = [];
    if (last.type === PatternType.Invalid) return result;

    const byRank = groupByRank(hand);
    const asc = [...byRank.values()].sort((a, b) => a.power - b.power);

    switch (last.type) {
        case PatternType.Single:
            for (const g of asc) if (g.power > last.mainRank)
                result.push(new HandPattern(PatternType.Single, g.power, 1, [g.cards[0]]));
            break;
        case PatternType.Pair:
            for (const g of asc) if (g.count >= 2 && g.power > last.mainRank)
                result.push(new HandPattern(PatternType.Pair, g.power, 1, g.cards.slice(0, 2)));
            break;
        case PatternType.Triple:
            for (const g of asc) if (g.count >= 3 && g.power > last.mainRank)
                result.push(new HandPattern(PatternType.Triple, g.power, 1, g.cards.slice(0, 3)));
            break;
        case PatternType.TripleSingle:
        case PatternType.TriplePair: {
            for (const g of asc) {
                if (g.count < 3 || g.power <= last.mainRank) continue;
                if (last.type === PatternType.TripleSingle) {
                    const kicker = asc.find(k => k.power !== g.power);
                    if (kicker) result.push(new HandPattern(PatternType.TripleSingle, g.power, 1,
                        [...g.cards.slice(0, 3), kicker.cards[0]]));
                } else {
                    const pair = asc.find(k => k.power !== g.power && k.count >= 2);
                    if (pair) result.push(new HandPattern(PatternType.TriplePair, g.power, 1,
                        [...g.cards.slice(0, 3), ...pair.cards.slice(0, 2)]));
                }
            }
            break;
        }
        case PatternType.Straight: {
            const len = last.length;
            for (let start = last.mainRank - len + 2; start + len - 1 <= 14; start++) {
                const cards = takeRun(byRank, start, len, 1);
                if (cards) result.push(new HandPattern(PatternType.Straight, start + len - 1, len, cards));
            }
            break;
        }
        case PatternType.PairStraight: {
            const pairs = last.length;
            for (let start = last.mainRank - pairs + 2; start + pairs - 1 <= 14; start++) {
                const cards = takeRun(byRank, start, pairs, 2);
                if (cards) result.push(new HandPattern(PatternType.PairStraight, start + pairs - 1, pairs, cards));
            }
            break;
        }
        case PatternType.Plane:
        case PatternType.PlaneSingle:
        case PatternType.PlanePair: {
            const n = last.length;
            for (let start = last.mainRank - n + 2; start + n - 1 <= 14; start++) {
                const body = takeRun(byRank, start, n, 3);
                if (!body) continue;
                if (last.type === PatternType.Plane) {
                    result.push(new HandPattern(PatternType.Plane, start + n - 1, n, body));
                    continue;
                }
                const need = last.type === PatternType.PlaneSingle ? n : n * 2;
                const used = new Set<number>();
                for (let p = start; p < start + n; p++) used.add(p);
                const kickers: Card[] = [];
                for (const g of asc) {
                    if (used.has(g.power)) continue;
                    for (const c of g.cards) {
                        if (kickers.length < need) kickers.push(c);
                    }
                }
                if (kickers.length >= need) {
                    result.push(new HandPattern(last.type, start + n - 1, n, [...body, ...kickers]));
                }
            }
            break;
        }
        case PatternType.FourTwo:
        case PatternType.FourTwoPair: {
            for (const g of asc) {
                if (g.count !== 4 || g.power <= last.mainRank) continue;
                const need = last.type === PatternType.FourTwo ? 2 : 4;
                const kickers: Card[] = [];
                for (const k of asc) {
                    if (k.power === g.power) continue;
                    for (const c of k.cards) {
                        if (kickers.length < need) kickers.push(c);
                    }
                }
                if (kickers.length >= need) {
                    result.push(new HandPattern(last.type, g.power, 1, [...g.cards, ...kickers]));
                }
            }
            break;
        }
    }

    // 炸弹与王炸
    if (last.type !== PatternType.Rocket) {
        for (const g of asc) {
            if (g.count === 4 && (last.type !== PatternType.Bomb || g.power > last.mainRank)) {
                result.push(new HandPattern(PatternType.Bomb, g.power, 1, g.cards));
            }
        }
        const small = byRank.get(18);
        const big = byRank.get(19);
        if (small && big) {
            result.push(new HandPattern(PatternType.Rocket, 19, 1, [...small.cards, ...big.cards]));
        }
    }

    result.sort((a, b) => {
        const ab = a.isBombLike ? 1 : 0;
        const bb = b.isBombLike ? 1 : 0;
        if (ab !== bb) return ab - bb;
        return a.mainRank - b.mainRank;
    });
    return result;
}

function takeRun(byRank: Map<number, RankGroup>, start: number, len: number, need: number): Card[] | null {
    const cards: Card[] = [];
    for (let p = start; p < start + len; p++) {
        const g = byRank.get(p);
        if (!g || g.count < need) return null;
        cards.push(...g.cards.slice(0, need));
    }
    return cards;
}
