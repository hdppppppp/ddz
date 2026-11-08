import { Card, Rank } from '../Model/Card';

/** 叫分 AI：手牌强度评分决定叫 0/1/2/3。 */
export function evaluateHand(hand: Card[]): number {
    let score = 0;
    for (const c of hand) {
        switch (c.rank) {
            case Rank.BigJoker: score += 8; break;
            case Rank.SmallJoker: score += 6; break;
            case Rank.Two: score += 4; break;
            case Rank.Ace: score += 3; break;
            case Rank.King: score += 1.5; break;
        }
    }
    const byRank = new Map<number, number>();
    for (const c of hand) byRank.set(c.rank, (byRank.get(c.rank) ?? 0) + 1);
    for (const [rank, count] of byRank) {
        if (count === 4) score += rank >= Rank.Two ? 12 : 8;
    }
    const hasSmall = hand.some(c => c.rank === Rank.SmallJoker);
    const hasBig = hand.some(c => c.rank === Rank.BigJoker);
    if (hasSmall && hasBig) score += 4;
    return Math.floor(score);
}

/** 决定叫分。返回 0/1/2/3。 */
export function decideBid(hand: Card[], currentHighest: number): number {
    const strength = evaluateHand(hand);
    const willing = strength >= 22 ? 3 : strength >= 16 ? 2 : strength >= 11 ? 1 : 0;
    return willing > currentHighest ? willing : 0;
}
