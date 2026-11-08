/** AI 自对弈基准：跑 N 局完整对局，统计地主胜率、平均手数、异常数。 */
import { Card } from '@core/Model/Card';
import { GamePhase, GameController } from '@core/Flow/GameController';
import { decideBid } from '@core/AI/BidAI';
import { decidePlay } from '@core/AI/PlayAI';
import { decidePlayV2 } from '@core/AI/PlayAIv2';

interface RunStats {
    games: number;
    landlordWins: number;
    totalTurns: number;
    errors: number;
    stuck: number;
}

function runBidding(game: GameController): void {
    let guard = 0;
    while (game.phase === GamePhase.Bidding && !game.biddingNeedsRestart) {
        const seat = game.bidTurnSeat;
        game.bid(seat, decideBid(game.players[seat].hand, game.highestBid));
        if (++guard > 10) break;
    }
}

function playGames(count: number, useV2: boolean): RunStats {
    const stats: RunStats = { games: 0, landlordWins: 0, totalTurns: 0, errors: 0, stuck: 0 };

    for (let i = 0; i < count; i++) {
        const game = new GameController();
        game.startGame();
        runBidding(game);

        let restarts = 0;
        while (game.phase !== GamePhase.Playing) {
            if (++restarts > 50) { stats.stuck++; break; }
            game.startGame();
            runBidding(game);
        }
        if (game.phase !== GamePhase.Playing) continue;

        let turns = 0;
        try {
            while (game.phase === GamePhase.Playing) {
                if (++turns > 500) { stats.stuck++; break; }
                const seat = game.currentSeat;
                const move = useV2 ? decidePlayV2(game, seat) : decidePlay(game, seat);
                if (move) game.playCards(seat, move);
                else game.pass(seat);
            }
        } catch {
            stats.errors++;
        }

        stats.games++;
        stats.totalTurns += turns;
        // 地主是否赢：Settled 时谁的牌先出完
        // GameResult 通过事件上报，这里从 players 推断：手牌为 0 者
        if (game.phase === GamePhase.Settled) {
            const winner = game.players.findIndex(p => p.handCount === 0);
            if (winner === game.landlordSeat) stats.landlordWins++;
        }
    }
    return stats;
}

if (process.argv[1] && process.argv[1].includes('benchmark')) {
    const n = parseInt(process.argv[2] ?? '100', 10);
    console.log(`=== 贪心版 PlayAI（${n} 局）===`);
    const g = playGames(n, false);
    console.log(`对局: ${g.games}  地主胜率: ${(g.landlordWins / Math.max(1, g.games) * 100).toFixed(1)}%  平均手数: ${(g.totalTurns / Math.max(1, g.games)).toFixed(1)}  异常: ${g.errors}  卡死: ${g.stuck}`);

    console.log(`=== 升级版 PlayAIv2（${n} 局）===`);
    const v = playGames(n, true);
    console.log(`对局: ${v.games}  地主胜率: ${(v.landlordWins / Math.max(1, v.games) * 100).toFixed(1)}%  平均手数: ${(v.totalTurns / Math.max(1, v.games)).toFixed(1)}  异常: ${v.errors}  卡死: ${v.stuck}`);
}
