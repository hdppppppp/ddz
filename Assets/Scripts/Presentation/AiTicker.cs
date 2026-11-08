using System;
using DDZ.Core;
using UnityEngine;

namespace DDZ.Presentation
{
    /// <summary>
    /// AI 玩家的节拍驱动器：轮到 AI 座位时延迟思考后落子。
    /// 由 GamePresenter 每帧调用 Tick()；人类座位完全由 UI 驱动。
    /// </summary>
    public sealed class AiTicker
    {
        private readonly GameController _game;
        private readonly Func<int, float> _thinkTimeProvider;
        private float _remaining;
        private int _pendingSeat = -1;
        private bool _waiting;
        private int _humanSeat = -1;

        public AiTicker(GameController game, Func<int, float>? thinkTimeProvider = null)
        {
            _game = game;
            _thinkTimeProvider = thinkTimeProvider ?? (_ => UnityEngine.Random.Range(0.7f, 1.4f));
        }

        /// <summary>人类玩家的座位（-1 表示无/观战）。</summary>
        public int HumanSeat
        {
            get => _humanSeat;
            set
            {
                _humanSeat = value;
                _waiting = false;
            }
        }

        /// <summary>每帧调用。返回 true 表示本帧执行了一次 AI 动作。</summary>
        public bool Tick(float deltaTime)
        {
            if (_game.Phase is not (GamePhase.Bidding or GamePhase.Playing))
                return false;

            int seat = _game.Phase == GamePhase.Bidding ? _game.BidTurnSeat : _game.CurrentSeat;
            if (seat < 0 || seat == _humanSeat)
            {
                _waiting = false;
                return false;
            }

            if (!_waiting || seat != _pendingSeat)
            {
                _pendingSeat = seat;
                _remaining = _thinkTimeProvider(seat);
                _waiting = true;
                return false;
            }

            _remaining -= deltaTime;
            if (_remaining > 0)
                return false;

            _waiting = false;
            ActAs(seat);
            return true;
        }

        private void ActAs(int seat)
        {
            if (_game.Phase == GamePhase.Bidding)
            {
                int bid = BidAI.DecideBid(_game.Players[seat].Hand, _game.HighestBid);
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
