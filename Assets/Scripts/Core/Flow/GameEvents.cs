using System;
using System.Collections.Generic;

namespace DDZ.Core
{
    /// <summary>对局结果。</summary>
    public sealed class GameResult
    {
        public int LandlordSeat { get; internal set; }
        public int WinnerSeat { get; internal set; }
        public bool LandlordWin { get; internal set; }
        public int BaseScore { get; internal set; }
        public int BombCount { get; internal set; }
        public bool Spring { get; internal set; }
        public int Multiplier { get; internal set; }

        /// <summary>三个座位的得分变化（地主 ±2 份，农民各 ±1 份）。</summary>
        public int[] ScoreDeltas { get; internal set; } = new int[3];
    }

    /// <summary>逻辑层 → 表现层的领域事件。</summary>
    public sealed class GameEvents
    {
        /// <summary>发牌完成：seat, 手牌（升序副本）。</summary>
        public event Action<int, List<Card>>? CardsDealt;

        /// <summary>一轮叫分全部不叫，重发。</summary>
        public event Action? BiddingRestarted;

        /// <summary>某家叫分：seat, 分数（0=不叫）。</summary>
        public event Action<int, int>? BidMade;

        /// <summary>地主确定：seat, 底牌。</summary>
        public event Action<int, List<Card>>? LandlordDecided;

        /// <summary>出牌：seat, 牌型。</summary>
        public event Action<int, HandPattern>? CardsPlayed;

        /// <summary>不出：seat。</summary>
        public event Action<int>? Passed;

        /// <summary>轮到某家出牌。</summary>
        public event Action<int>? TurnStarted;

        /// <summary>终局。</summary>
        public event Action<GameResult>? GameSettled;

        internal void RaiseCardsDealt(int seat, List<Card> hand) => CardsDealt?.Invoke(seat, hand);
        internal void RaiseBiddingRestarted() => BiddingRestarted?.Invoke();
        internal void RaiseBidMade(int seat, int score) => BidMade?.Invoke(seat, score);
        internal void RaiseLandlordDecided(int seat, List<Card> bottom) => LandlordDecided?.Invoke(seat, bottom);
        internal void RaiseCardsPlayed(int seat, HandPattern pattern) => CardsPlayed?.Invoke(seat, pattern);
        internal void RaisePassed(int seat) => Passed?.Invoke(seat);
        internal void RaiseTurnStarted(int seat) => TurnStarted?.Invoke(seat);
        internal void RaiseGameSettled(GameResult result) => GameSettled?.Invoke(result);
    }
}
