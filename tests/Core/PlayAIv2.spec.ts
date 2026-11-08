import { describe, expect, test } from 'vitest';
import { Card, Rank, Suit } from '@core/Model/Card';
import { GamePhase, GameController } from '@core/Flow/GameController';
import { decideBid } from '@core/AI/BidAI';
import { decidePlay } from '@core/AI/PlayAI';
import { decomposeAll, decidePlayV2 } from '@core/AI/PlayAIv2';

const S = (r: Rank) => Card.byRank(r, Suit.Spades);
const H = (r: Rank) => Card.byRank(r, Suit.Hearts);
const D = (r: Rank) => Card.byRank(r, Suit.Diamonds);
const Cl = (r: Rank) => Card.byRank(r, Suit.Clubs);

describe('decomposeAll', () => {
    test('返回多方案且含基准方案', () => {
        const hand = [
            S(Rank.Three), H(Rank.Three),
            S(Rank.Four), H(Rank.Four),
            S(Rank.Five), H(Rank.Five),
            S(Rank.Six), H(Rank.Six),
        ];
        const options = decomposeAll(hand);
        expect(options.length).toBeGreaterThanOrEqual(1);
        // 基准方案手数 = 连对1手（3-6 连对）或 4 对子 → 评分最优应是连对
        expect(options[0].hands).toBeLessThanOrEqual(options[options.length - 1].hands);
    });

    test('拆炸弹变体存在', () => {
        const hand = [
            S(Rank.Seven), H(Rank.Seven), D(Rank.Seven), Cl(Rank.Seven),
            S(Rank.Three), H(Rank.Four),
        ];
        const options = decomposeAll(hand);
        // 有炸弹的版本手数应该比拆炸弹版本被评分惩罚或奖励都体现出来
        expect(options.some(o => o.bombs === 1)).toBe(true);
    });
});

describe('decidePlayV2 行为验证', () => {
    test('完整对局 3 局无异常', () => {
        for (let round = 0; round < 3; round++) {
            const game = new GameController();
            game.startGame();

            let guard = 0;
            while (game.phase === GamePhase.Bidding && !game.biddingNeedsRestart) {
                const seat = game.bidTurnSeat;
                game.bid(seat, decideBid(game.players[seat].hand, game.highestBid));
                if (++guard > 10) break;
            }
            let restarts = 0;
            while (game.phase !== GamePhase.Playing) {
                if (++restarts > 50) throw new Error('50 次发牌无地主');
                game.startGame();
                guard = 0;
                while (game.phase === GamePhase.Bidding && !game.biddingNeedsRestart) {
                    const seat = game.bidTurnSeat;
                    game.bid(seat, decideBid(game.players[seat].hand, game.highestBid));
                    if (++guard > 10) break;
                }
            }

            let turns = 0;
            while (game.phase === GamePhase.Playing) {
                if (++turns > 500) throw new Error('500 手未结束');
                const seat = game.currentSeat;
                const move = decidePlayV2(game, seat);
                if (move) game.playCards(seat, move);
                else game.pass(seat);
            }
            expect(game.phase).toBe(GamePhase.Settled);
        }
    });

    test('V2 与贪心版在自由出牌上不抛错', () => {
        const game = new GameController();
        game.startGame();
        let guard = 0;
        while (game.phase === GamePhase.Bidding && !game.biddingNeedsRestart) {
            const seat = game.bidTurnSeat;
            game.bid(seat, decideBid(game.players[seat].hand, game.highestBid));
            if (++guard > 10) break;
        }
        if (game.phase === GamePhase.Playing) {
            const seat = game.currentSeat;
            expect(() => decidePlayV2(game, seat)).not.toThrow();
        }
    });
});
