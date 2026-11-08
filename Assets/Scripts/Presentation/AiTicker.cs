using System;
using System.Collections.Generic;
using DDZ.Core;

namespace DDZ.Presentation
{
    /// <summary>
    /// AI 玩家的节拍驱动器：轮到 AI 座位时延迟思考后落子。
    /// 由 GamePresenter 挂在 Unity 主循环（协程/MonoBehaviour）上调用 Tick()。
    /// </summary>
    public sealed class AiTicker
    {
        private readonly GameController _game;
        private readonly Func<int, float> _thinkTimeProvider;
        private float _remaining;
        private int _pendingSeat = -1;
        private bool _waiting;

        public AiTicker(GameController game, Func<int, float>? thinkTimeProvider = null)
        {
            _game = game;
            _thinkTimeProvider = thinkTimeProvider ?? (seat => 0.8f + 0.4f * (seat % 2));
        }

        /// <summary>每帧调用。deltaTime 秒。返回 true 表示本帧执行了一次 AI 动作。</summary>
        public bool Tick(float deltaTime)
        {
            if (_game.Phase == GamePhase.Settled)
                return false;

            int seat = _game.Phase == GamePhase.Bidding ? _game.BidTurnSeat : _game.CurrentSeat;
            if (seat < 0)
                return false;

            if (!_waiting)
            {
                // 新的回合：是 AI 才开始倒计时
                if (_game.Phase == GamePhase.Bidding || seat != _humanSeat)
                {
                    _pendingSeat = seat;
                    _remaining = _thinkTimeProvider(seat);
                    _waiting = true;
                }
                return false;
            }

            if (seat != _pendingSeat)
            {
                // 轮转变了（例如人刚出完牌），重新计时
                _waiting = false;
                return false;
            }

            _remaining -= deltaTime;
            if (_remaining > 0)
                return false;

            _waiting = false;
            ActAs(_pendingSeat);
            return true;
        }

        private int _humanSeat = -1;

        /// <summary>人类玩家的座位（-1 表示无/全员 AI）。</summary>
        public int HumanSeat
        {
            get => _humanSeat;
            set => _humanSeat = value;
        }

        private void ActAs(int seat)
        {
            if (_game.Phase == GamePhase.Bidding)
            {
                int bid = BidAI.DecideBid(_game.Players[seat].Hand, 0);
                _game.Bid(seat, bid);
            }
            else
            {
                var move = PlayAI.Decide(_game, seat);
                if (move == null)
                    _game.Pass(seat);
                else
                    _game.PlayCards(seat, move);
            }
        }
    }
}
