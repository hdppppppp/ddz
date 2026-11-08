import { describe, expect, test } from 'vitest';
import { Card } from '@core/Model/Card';
import { GamePhase, GameController } from '@core/Flow/GameController';
import { decideBid } from '@core/AI/BidAI';
import { decidePlay } from '@core/AI/PlayAI';

function runBidding(game: GameController): void {
    let guard = 0;
    while (game.phase === GamePhase.Bidding && !game.biddingNeedsRestart) {
        const seat = game.bidTurnSeat;
        game.bid(seat, decideBid(game.players[seat].hand, game.highestBid));
        if (++guard > 10) break;
    }
}

describe('GameController', () => {
    test('发牌 17/17/17+3 且不重复', () => {
        const game = new GameController();
        game.startGame();
        expect(game.players.map(p => p.handCount)).toEqual([17, 17, 17]);
        expect(game.bottom.length).toBe(3);
        const all = new Set<number>();
        for (const p of game.players) for (const c of p.hand) all.add(c.id);
        for (const c of game.bottom) all.add(c.id);
        expect(all.size).toBe(54);
        expect(game.phase).toBe(GamePhase.Bidding);
    });

    test('未轮到叫分抛错', () => {
        const game = new GameController();
        game.startGame();
        expect(() => game.bid((game.bidTurnSeat + 1) % 3, 1)).toThrow();
    });

    test('三场完整 AI 对局无异常', () => {
        for (let round = 0; round < 3; round++) {
            const game = new GameController();
            game.startGame();
            runBidding(game);

            let restarts = 0;
            while (game.phase !== GamePhase.Playing) {
                if (++restarts > 50) throw new Error('50 次发牌都无人叫地主');
                game.startGame();
                runBidding(game);
            }

            let turns = 0;
            while (game.phase === GamePhase.Playing) {
                if (++turns > 500) throw new Error('对局超过 500 手未结束');
                const seat = game.currentSeat;
                const move = decidePlay(game, seat);
                if (move) game.playCards(seat, move);
                else game.pass(seat);
            }

            expect(game.phase).toBe(GamePhase.Settled);
        }
    });

    test('自由出牌不能 pass', () => {
        const game = new GameController();
        game.startGame();
        runBidding(game);
        let restarts = 0;
        while (game.phase !== GamePhase.Playing) {
            game.startGame();
            runBidding(game);
            if (++restarts > 50) break;
        }
        expect(() => game.pass(game.currentSeat)).toThrow();
    });

    test('没轮到不能出牌', () => {
        const game = new GameController();
        game.startGame();
        runBidding(game);
        let restarts = 0;
        while (game.phase !== GamePhase.Playing) {
            game.startGame();
            runBidding(game);
            if (++restarts > 50) break;
        }
        const seat = game.currentSeat;
        game.playCards(seat, [game.players[seat].hand[0]]);
        expect(game.currentSeat).not.toBe(seat);
        expect(() => game.playCards(seat, [game.players[seat].hand[0]])).toThrow();
    });
});
