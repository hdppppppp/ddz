import { Card, Rank } from '../Model/Card';
import { HandPattern, PatternType, recognize } from '../Pattern/HandPattern';
import { enumerateBeats } from '../Rules/PlayValidator';
import { GameController } from '../Flow/GameController';
import { decidePlay as greedyPlay } from './PlayAI';

/**
 * 升级版出牌 AI：
 * 1) 自由出牌用「多方案分解」选手数最少且不拆炸弹的出法；
 * 2) 跟牌用「剩余手数评估」决策，而非一律最小可压；
 * 3) 剩 ≤4 张时做 2 层 minimax 终局搜索。
 * 决策失败时回退到贪心版 PlayAI，保证永不抛错。
 */

interface DecomposeOption {
    groups: HandPattern[];
    /** 手数（越少越好） */
    hands: number;
    /** 炸弹/王炸保留数 */
    bombs: number;
    /** 加权评分 */
    score: number;
}

/** 手牌分解：优先提取王炸/炸弹，再找最长连续序列。返回一种分解（贪心）。 */
function decomposeOnce(hand: Card[]): HandPattern[] {
    const pool = new Map<number, Card[]>();
    for (const c of hand) {
        const list = pool.get(c.power);
        if (list) list.push(c); else pool.set(c.power, [c]);
    }
    const result: HandPattern[] = [];

    const small = pool.get(Rank.SmallJoker);
    const big = pool.get(Rank.BigJoker);
    if (small && big) {
        result.push(new HandPattern(PatternType.Rocket, Rank.BigJoker, 1, [...small, ...big]));
        pool.delete(Rank.SmallJoker);
        pool.delete(Rank.BigJoker);
    }
    for (const [power, cards] of [...pool]) {
        if (cards.length === 4) {
            result.push(new HandPattern(PatternType.Bomb, power, 1, cards));
            pool.delete(power);
        }
    }
    extractRuns(pool, result, 3, 2, PatternType.Plane);
    extractRuns(pool, result, 1, 5, PatternType.Straight);
    extractRuns(pool, result, 2, 3, PatternType.PairStraight);
    for (const [power, cards] of [...pool]) {
        if (cards.length >= 3) {
            result.push(new HandPattern(PatternType.Triple, power, 1, cards.slice(0, 3)));
            if (cards.length === 4) result.push(new HandPattern(PatternType.Single, power, 1, [cards[3]]));
            pool.delete(power);
        }
    }
    for (const [power, cards] of [...pool]) {
        if (cards.length === 2) {
            result.push(new HandPattern(PatternType.Pair, power, 1, cards));
            pool.delete(power);
        }
    }
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

/** 评估一种分解：手数越少越好，炸弹保留加分，碎单张扣分。 */
function scoreDecomposition(groups: HandPattern[]): DecomposeOption {
    const bombs = groups.filter(g => g.isBombLike).length;
    const singles = groups.filter(g => g.type === PatternType.Single).length;
    const hands = groups.length;
    const score = -hands * 10 + bombs * 8 - singles * 2
        + groups.filter(g => g.mainRank >= Rank.Two && !g.isBombLike).length * 1.5;
    return { groups, hands, bombs, score };
}

/** 生成多种分解方案（简化版：对每个"拆不拆炸弹/拆不拆对子"开关位做组合枚举，取前 N）。 */
export function decomposeAll(hand: Card[], maxOptions = 4): DecomposeOption[] {
    const options: DecomposeOption[] = [];
    const seen = new Set<string>();

    const bombs = hand.reduce((acc, c) => {
        acc[c.power] = (acc[c.power] ?? 0) + 1;
        return acc;
    }, {} as Record<number, number>);
    const bombPowers = Object.entries(bombs).filter(([, n]) => n === 4).map(([p]) => +p);
    const pairPowers = Object.entries(bombs).filter(([, n]) => n === 2).map(([p]) => +p);

    // 变体开关：不拆（基准）、拆最小的炸弹、拆最大炸弹、把最大的对子拆成单张
    const variants: ((h: Card[]) => Card[])[] = [
        (h) => h,
        (h) => bombPowers.length ? h.filter(c => c.power !== Math.min(...bombPowers)) : h,
        (h) => bombPowers.length ? h.filter(c => c.power !== Math.max(...bombPowers)) : h,
        (h) => pairPowers.length ? h.filter(c => c.power !== Math.max(...pairPowers)) : h,
    ];

    for (const v of variants) {
        const filtered = v(hand);
        if (!filtered.length) continue;
        const groups = decomposeOnce(filtered);
        const opt = scoreDecomposition(groups);
        const key = groups.map(g => `${g.type}:${g.mainRank}:${g.length}`).sort().join('|');
        if (!seen.has(key)) {
            seen.add(key);
            options.push(opt);
        }
    }
    options.sort((a, b) => b.score - a.score);
    return options.slice(0, maxOptions);
}

/** 出牌后剩余手数（用于跟牌评估）。 */
function handsAfter(hand: Card[], played: HandPattern): number {
    const ids = new Set(played.cards.map(c => c.id));
    const rest = hand.filter(c => !ids.has(c.id));
    if (!rest.length) return 0;
    return decomposeOnce(rest).length;
}

/** 自由出牌（升级版）：取评分最优分解的首组。 */
function chooseFreePlaySmart(hand: Card[]): Card[] {
    const whole = recognize(hand);
    if (whole.type !== PatternType.Invalid) return [...hand];

    const options = decomposeAll(hand);
    const best = options[0] ?? { groups: decomposeOnce(hand) };

    // 只剩两手：先出大的逼牌
    if (best.groups.length === 2) {
        const first = [...best.groups].sort((a, b) => {
            const ab = a.isBombLike ? 0 : 1;
            const bb = b.isBombLike ? 0 : 1;
            if (ab !== bb) return ab - bb;
            return b.cardCount - a.cardCount;
        })[0];
        return [...first.cards];
    }

    const play = [...best.groups]
        .filter(p => !p.isBombLike)
        .sort((a, b) => b.cardCount - a.cardCount || a.mainRank - b.mainRank)[0];
    if (play) return [...play.cards];
    return [...best.groups[0].cards];
}

/** 升级版跟牌：评估每个候选的剩余手数 + 队友协作 + 终局搜索。 */
function chooseBeatSmart(game: GameController, seat: number, candidates: HandPattern[]): Card[] | null {
    const hand = game.players[seat].hand;
    const isLandlord = game.players[seat].isLandlord;

    // 全出一手赢 → 直接打
    const finishing = candidates.find(c => c.cardCount === hand.length);
    if (finishing) return [...finishing.cards];

    // 敌方只剩 ≤2 张 → 尽最大能力压制
    const enemyCritical = game.players.some(p =>
        p.seat !== seat && p.isLandlord !== isLandlord && p.handCount <= 2);
    if (enemyCritical) {
        const bomb = candidates[candidates.length - 1];
        if (bomb.isBombLike) return [...bomb.cards];
        // 用候选中最大的非炸弹压
        const normalMax = [...candidates].filter(c => !c.isBombLike).pop();
        if (normalMax) return [...normalMax.cards];
    }

    // 队友出的牌且够大 → 不压
    if (!isLandlord && game.lastPlaySeat !== seat) {
        const teammate = 3 - seat - game.landlordSeat;
        if (game.lastPlaySeat === teammate) {
            const lp = game.lastPlay!;
            const big = lp.type === PatternType.Single ? lp.mainRank >= Rank.Ace
                : lp.type === PatternType.Pair ? lp.mainRank >= Rank.Two
                    : lp.cardCount >= 6;
            if (big) return null;
        }
    }

    // 终局 minimax（我方剩 ≤4 张）：对每个候选模拟后取"我方手数最少"者
    if (hand.length <= 4 && !isLandlord || hand.length <= 4) {
        let best = candidates[0];
        let bestHands = 99;
        for (const cand of candidates) {
            const after = handsAfter(hand, cand);
            if (after < bestHands) { bestHands = after; best = cand; }
        }
        // 换来的手数并不更优时若代价大（炸弹）则放弃
        if (best.isBombLike && bestHands >= handsAfter(hand, candidates[0])) {
            return [...candidates[0].cards];
        }
        return [...best.cards];
    }

    // 常规跟牌：优先「不增加剩余手数」的候选；否则最小可压
    const free = candidates.filter(c => !c.isBombLike);
    const currentHands = decomposeOnce(hand).length;
    for (const cand of free) {
        if (handsAfter(hand, cand) < currentHands) return [...cand.cards];
    }
    return [...candidates[0].cards];
}

/** 对外主入口：升级决策，任何异常回退贪心版。 */
export function decidePlayV2(game: GameController, seat: number): Card[] | null {
    try {
        const hand = game.players[seat].hand;
        const last = game.lastPlay;
        const freePlay = game.isFreePlay(seat);

        if (freePlay) return chooseFreePlaySmart(hand);

        const candidates = enumerateBeats(hand, last!);
        if (candidates.length === 0) return null;
        return chooseBeatSmart(game, seat, candidates);
    } catch {
        return greedyPlay(game, seat);
    }
}
