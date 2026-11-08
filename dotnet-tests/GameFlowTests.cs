using System;
using System.Collections.Generic;
using System.Linq;
using DDZ.Core;
using Xunit;

namespace DDZ.Core.Tests
{
    /// <summary>GameController 状态机 + AI 冒烟测试。</summary>
    public class GameFlowTests
    {
        [Fact]
        public void StartGame_Deals17EachAnd3Bottom()
        {
            var game = new GameController();
            game.StartGame();

            Assert.All(game.Players, p => Assert.Equal(17, p.HandCount));
            Assert.Equal(3, game.BottomCards!.Count);
            // 54 张全部发出且不重复
            var all = game.Players.SelectMany(p => p.Hand).Concat(game.BottomCards).Select(c => c.Id).ToList();
            Assert.Equal(54, all.Count);
            Assert.Equal(54, all.Distinct().Count());
            Assert.Equal(GamePhase.Bidding, game.Phase);
        }

        [Fact]
        public void Bid_Flow_ToLandlord()
        {
            var game = new GameController();
            game.StartGame();

            int first = game.BidTurnSeat;
            int landlord = -1;
            // 简单叫分：每人按 BidAI 决策
            for (int i = 0; i < 3 && landlord < 0; i++)
            {
                int seat = game.BidTurnSeat;
                int bid = BidAI.DecideBid(game.Players[seat].Hand, 0);
                landlord = game.Bid(seat, bid);
            }

            if (landlord >= 0)
            {
                Assert.Equal(GamePhase.Playing, game.Phase);
                Assert.Equal(landlord, game.LandlordSeat);
                Assert.Equal(20, game.Players[landlord].HandCount);
                Assert.Equal(landlord, game.CurrentSeat);
            }
            else
            {
                // 全不叫 → 保持 Bidding，等待重开
                Assert.Equal(GamePhase.Bidding, game.Phase);
            }
        }

        [Fact]
        public void Bid_InvalidSequence_Throws()
        {
            var game = new GameController();
            game.StartGame();
            int notTurn = (game.BidTurnSeat + 1) % 3;
            Assert.Throws<InvalidOperationException>(() => game.Bid(notTurn, 1));
        }

        /// <summary>完整 AI 对局：三 个 AI 打到分出胜负，全程不允许非法状态。</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void FullAiGame_Completes(int seed)
        {
            var game = new GameController();
            game.StartGame();

            // 重开直到有人叫分（全不叫时概率约 (不叫率)^3，少量重试内必然成功）
            int restarts = 0;
            while (game.Phase != GamePhase.Playing)
            {
                game.StartGame();
                if (++restarts > 50) throw new InvalidOperationException("50 次发牌都无人叫地主，AI 阈值可能过严");
            }

            // 叫分阶段
            while (game.Phase == GamePhase.Bidding)
            {
                int seat = game.BidTurnSeat;
                int bid = BidAI.DecideBid(game.Players[seat].Hand, 0);
                game.Bid(seat, bid);
            }

            // 打牌阶段：最多 500 手防止死循环
            int turns = 0;
            while (game.Phase == GamePhase.Playing)
            {
                if (++turns > 500) throw new InvalidOperationException("对局超过 500 手未结束");

                int seat = game.CurrentSeat;
                var move = PlayAI.Decide(game, seat);
                if (move == null)
                {
                    game.Pass(seat);
                }
                else
                {
                    game.PlayCards(seat, move);
                }
            }

            Assert.Equal(GamePhase.Settled, game.Phase);
        }

        [Fact]
        public void Pass_WhenFreePlay_Throws()
        {
            var game = new GameController();
            ForceLandlordPhase(game);
            // 地主自由出牌时不能 pass
            Assert.Throws<InvalidOperationException>(() => game.Pass(game.CurrentSeat));
        }

        [Fact]
        public void PlayCards_TwiceInARow_Throws()
        {
            var game = new GameController();
            ForceLandlordPhase(game);
            int seat = game.CurrentSeat;
            var one = new[] { game.Players[seat].Hand[0] };
            game.PlayCards(seat, one);
            Assert.NotEqual(seat, game.CurrentSeat);
            Assert.Throws<InvalidOperationException>(() => game.PlayCards(seat, one));
        }

        /// <summary>强制开局到出牌阶段：重开发牌直到 AI 叫出地主。</summary>
        private static void ForceLandlordPhase(GameController game)
        {
            game.StartGame();
            int guard = 0;
            while (game.Phase == GamePhase.Bidding)
            {
                int seat = game.BidTurnSeat;
                int bid = BidAI.DecideBid(game.Players[seat].Hand, 0);
                if (game.Bid(seat, bid) >= 0) break;
                if (++guard > 100)
                {
                    // 极小概率全不叫循环：重新开局
                    game.StartGame();
                    guard = 0;
                }
            }
        }
    }
}
