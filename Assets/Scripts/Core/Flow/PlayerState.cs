using System.Collections.Generic;

namespace DDZ.Core
{
    /// <summary>座位上的玩家状态。</summary>
    public sealed class PlayerState
    {
        public PlayerState(int seat)
        {
            Seat = seat;
            Hand = new List<Card>();
        }

        /// <summary>座位号 0..2。</summary>
        public int Seat { get; }

        /// <summary>当前手牌。</summary>
        public List<Card> Hand { get; }

        public bool IsLandlord { get; internal set; }

        /// <summary>本局出手次数（用于春天/反春判定）。</summary>
        public int PlayCount { get; internal set; }

        public int HandCount => Hand.Count;

        public void Reset()
        {
            Hand.Clear();
            IsLandlord = false;
            PlayCount = 0;
        }
    }
}
