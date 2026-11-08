import { describe, expect, test } from 'vitest';
import { Card, Rank, Suit } from '@core/Model/Card';
import { PatternType, recognize } from '@core/Pattern/HandPattern';

const C = (id: number) => new Card(id);
const S = (r: Rank) => Card.byRank(r, Suit.Spades);
const H = (r: Rank) => Card.byRank(r, Suit.Hearts);
const D = (r: Rank) => Card.byRank(r, Suit.Diamonds);
const Cl = (r: Rank) => Card.byRank(r, Suit.Clubs);

describe('Card', () => {
    test('全 54 张 id↔rank/suit 回环', () => {
        for (let id = 0; id < 54; id++) {
            const card = new Card(id);
            const rebuilt = Card.byRank(card.rank, card.suit);
            expect(rebuilt.id).toBe(id);
        }
    });

    test('关键映射', () => {
        expect(new Card(12).rank).toBe(Rank.Two);
        expect(new Card(0).rank).toBe(Rank.Three);
        expect(new Card(25).rank).toBe(Rank.Two);
        expect(new Card(52).rank).toBe(Rank.SmallJoker);
        expect(new Card(53).rank).toBe(Rank.BigJoker);
    });

    test('非法 id 抛错', () => {
        expect(() => new Card(-1)).toThrow();
        expect(() => new Card(54)).toThrow();
    });

    test('大王压小王压2压A', () => {
        expect(new Card(53).power).toBeGreaterThan(new Card(52).power);
        expect(new Card(52).power).toBeGreaterThan(S(Rank.Two).power);
        expect(S(Rank.Two).power).toBeGreaterThan(S(Rank.Ace).power);
    });

    test('toString 不越界', () => {
        for (let id = 0; id < 54; id++) {
            expect(new Card(id).toString().length).toBeGreaterThan(0);
        }
    });
});

describe('HandPattern 识别', () => {
    test('单/对/三', () => {
        expect(recognize([S(Rank.Five)]).type).toBe(PatternType.Single);
        expect(recognize([S(Rank.Five), H(Rank.Five)]).type).toBe(PatternType.Pair);
        expect(recognize([S(Rank.Five), H(Rank.Five), D(Rank.Five)]).type).toBe(PatternType.Triple);
    });

    test('三带一/三带对', () => {
        const p = recognize([S(Rank.Five), H(Rank.Five), D(Rank.Five), S(Rank.Three)]);
        expect(p.type).toBe(PatternType.TripleSingle);
        expect(p.mainRank).toBe(Rank.Five);
        expect(recognize([S(Rank.Five), H(Rank.Five), D(Rank.Five), S(Rank.Seven), H(Rank.Seven)]).type)
            .toBe(PatternType.TriplePair);
    });

    test('顺子', () => {
        const p = recognize([S(Rank.Three), H(Rank.Four), D(Rank.Five), Cl(Rank.Six), H(Rank.Seven)]);
        expect(p.type).toBe(PatternType.Straight);
        expect(p.length).toBe(5);
    });

    test('顺子带2不合法', () => {
        expect(recognize([S(Rank.Ten), H(Rank.Jack), D(Rank.Queen), Cl(Rank.King), H(Rank.Ace), S(Rank.Two)]).type)
            .toBe(PatternType.Invalid);
    });

    test('连对', () => {
        const p = recognize([S(Rank.Three), H(Rank.Three), S(Rank.Four), H(Rank.Four), S(Rank.Five), H(Rank.Five)]);
        expect(p.type).toBe(PatternType.PairStraight);
        expect(p.length).toBe(3);
    });

    test('飞机不带', () => {
        const p = recognize([S(Rank.Four), H(Rank.Four), D(Rank.Four), S(Rank.Five), H(Rank.Five), D(Rank.Five)]);
        expect(p.type).toBe(PatternType.Plane);
        expect(p.length).toBe(2);
    });

    test('飞机带单', () => {
        const p = recognize([
            S(Rank.Four), H(Rank.Four), D(Rank.Four),
            S(Rank.Five), H(Rank.Five), D(Rank.Five),
            S(Rank.Seven), S(Rank.Nine),
        ]);
        expect(p.type).toBe(PatternType.PlaneSingle);
    });

    test('炸弹与王炸', () => {
        const bomb = recognize([S(Rank.Two), H(Rank.Two), D(Rank.Two), Cl(Rank.Two)]);
        expect(bomb.type).toBe(PatternType.Bomb);
        const rocket = recognize([C(52), C(53)]);
        expect(rocket.type).toBe(PatternType.Rocket);
        expect(rocket.beats(bomb)).toBe(true);
        expect(bomb.beats(recognize([C(53)]))).toBe(true);
    });

    test('同型比大小', () => {
        const high = recognize([S(Rank.Eight)]);
        const low = recognize([S(Rank.Five)]);
        const pair = recognize([S(Rank.Eight), H(Rank.Eight)]);
        expect(high.beats(low)).toBe(true);
        expect(low.beats(high)).toBe(false);
        expect(pair.beats(high)).toBe(false);
    });

    test('四带二', () => {
        const p = recognize([
            S(Rank.Nine), H(Rank.Nine), D(Rank.Nine), Cl(Rank.Nine),
            S(Rank.Three), H(Rank.Four),
        ]);
        expect(p.type).toBe(PatternType.FourTwo);
    });
});
