using System;
using System.Collections.Generic;
using System.Linq;

namespace DDZ.Core
{
    /// <summary>对局阶段。</summary>
    public enum GamePhase
    {
        NotStarted,
        Bidding,
        Playing,
        Settled,
    }

    /// <summary>
    /// 对局控制器：发牌 → 叫分 → 出牌 → 结算 的状态机。
    /// 纯逻辑无定时器：AI 的动作由宿主（表现层/测试）按需调用 AiAct() 推进，
    /// 人类玩家的动作用 Bid()/PlayCards()/Pass() 提交，非法操作抛 InvalidOperationException。
    /// </summary>
    public sealed class GameController
    {
        private readonly GameEvents _events = new();
        private readonly List<PlayerState> _players = new();
        private readonly Random _rng = new();

        private Card[]? _bottomCards;
        private int _bidTurn;          // 当前叫分座位
        private int _bidCount;         // 已叫人数
        private int _highestBid;       // 当前最高叫分
        private int _highestBidder = -1;

        private HandPattern? _lastPlay;
        private int _lastPlaySeat = -1;
        private int _passCount;        // 连续不出的人数
        private int _bombCount;
        private int _currentSeat;

        public GamePhase Phase { get; private set; } = GamePhase.NotStarted;
        public int LandlordSeat { get; private set; } = -1;
        public int BaseScore { get; private set; } = 1;
        public int Multiplier { get; private set; } = 1;
        public IReadOnlyList<PlayerState> Players => _players;
        public GameEvents Events => _events;
        public int CurrentSeat => _currentSeat;

        /// <summary>上次出牌（null 表示自由出牌）。</summary>
        public HandPattern? LastPlay => _lastPlay;

        /// <summary>上一手是谁出的（-1 = 本轮还没人出）。</summary>
        public int LastPlaySeat => _lastPlaySeat;

        /// <summary>底牌（地主确定后可读）。</summary>
        public IReadOnlyList<Card>? BottomCards => _bottomCards;

        /// <summary>开始新一局：洗牌、发牌、进入叫分。返回是否成功开局（全不叫会触发事件并由调用方决定重开）。</summary>
        public void StartGame()
        {
            _players.Clear();
            for (int seat = 0; seat < 3; seat++)
                _players.Add(new PlayerState(seat));

            var deck = Deck.Shuffled();
            for (int seat = 0; seat < 3; seat++)
            {
                _players[seat].Reset();
                _players[seat].Hand.AddRange(deck.Skip(seat * Deck.HandCount).Take(Deck.HandCount));
                Deck.SortAscending(_players[seat].Hand);
                _events.RaiseCardsDealt(seat, new List<Card>(_players[seat].Hand));
            }

            _bottomCards = deck.Skip(3 * Deck.HandCount).Take(Deck.BottomCount).ToArray();

            Phase = GamePhase.Bidding;
            _bidTurn = _rng.Next(3);
            _bidCount = 0;
            _highestBid = 0;
            _highestBidder = -1;
            _lastPlay = null;
            _lastPlaySeat = -1;
            _passCount = 0;
            _bombCount = 0;
            LandlordSeat = -1;
            BaseScore = 1;
            Multiplier = 1;
        }

        /// <summary>当前该叫分的座位。</summary>
        public int BidTurnSeat => Phase == GamePhase.Bidding ? _bidTurn : -1;

        /// <summary>提交叫分（0=不叫，1/2/3）。返回地主座位，未决出返回 -1。</summary>
        public int Bid(int seat, int score)
        {
            EnsurePhase(GamePhase.Bidding);
            if (seat != _bidTurn)
                throw new InvalidOperationException($"还没轮到座位 {seat} 叫分（当前 {_bidTurn}）");
            if (score != 0 && (score <= _highestBid || score > 3))
                throw new InvalidOperationException($"叫分 {score} 无效（当前最高 {_highestBid}）");

            _events.RaiseBidMade(seat, score);
            if (score > _highestBid)
            {
                _highestBid = score;
                _highestBidder = seat;
            }

            _bidCount++;

            // 叫3直接成地主；三人都叫完则最高者成地主（无人在叫则由调用方重开）
            if (_highestBid == 3 || _bidCount >= 3)
            {
                if (_highestBidder < 0)
                {
                    _events.RaiseBiddingRestarted();
                    return -1;
                }
                BecomeLandlord(_highestBidder);
                return LandlordSeat;
            }

            _bidTurn = (_bidTurn + 1) % 3;
            return -1;
        }

        /// <summary>地主确定：拿底牌、翻倍、进入出牌阶段。</summary>
        private void BecomeLandlord(int seat)
        {
            LandlordSeat = seat;
            BaseScore = Math.Max(_highestBid, 1);
            _players[seat].IsLandlord = true;
            if (_bottomCards != null)
                _players[seat].Hand.AddRange(_bottomCards);
            Deck.SortAscending(_players[seat].Hand);
            _events.RaiseLandlordDecided(seat, new List<Card>(_bottomCards!));

            Phase = GamePhase.Playing;
            _currentSeat = seat;
            _events.RaiseTurnStarted(seat);
        }

        /// <summary>出牌。校验合法性，成功后推进到下一家。</summary>
        public void PlayCards(int seat, IReadOnlyList<Card> cards)
        {
            EnsurePhase(GamePhase.Playing);
            if (seat != _currentSeat)
                throw new InvalidOperationException($"还没轮到座位 {seat}（当前 {_currentSeat}）");

            bool freePlay = _lastPlay == null || _lastPlaySeat == seat;
            if (!PlayValidator.Validate(cards, freePlay, freePlay ? null : _lastPlay, out var pattern))
                throw new InvalidOperationException($"出牌不合法: {pattern.Type}");

            var player = _players[seat];
            var cardSet = new HashSet<int>(cards.Select(c => c.Id));
            if (cardSet.Count != cards.Count || !cards.All(c => player.Hand.Any(h => h.Id == c.Id)))
                throw new InvalidOperationException("出牌包含不持有的牌");
            foreach (var c in cards)
                player.Hand.RemoveAll(h => h.Id == c.Id);

            player.PlayCount++;
            if (pattern.IsBombLike)
            {
                _bombCount++;
                Multiplier *= 2;
            }

            _lastPlay = pattern;
            _lastPlaySeat = seat;
            _passCount = 0;
            _events.RaiseCardsPlayed(seat, pattern);

            if (player.HandCount == 0)
            {
                Settle(seat);
                return;
            }

            AdvanceTurn();
        }

        /// <summary>不出（仅当场上有牌可压时允许）。</summary>
        public void Pass(int seat)
        {
            EnsurePhase(GamePhase.Playing);
            if (seat != _currentSeat)
                throw new InvalidOperationException($"还没轮到座位 {seat}（当前 {_currentSeat}）");

            bool freePlay = _lastPlay == null || _lastPlaySeat == seat;
            if (freePlay)
                throw new InvalidOperationException("自由出牌时不能不出");

            _passCount++;
            _events.RaisePassed(seat);

            // 连续两家不出 → 上家获得自由出牌权
            if (_passCount >= 2)
            {
                _lastPlay = null;
                _lastPlaySeat = -1;
                _passCount = 0;
            }

            AdvanceTurn();
        }

        private void AdvanceTurn()
        {
            _currentSeat = (_currentSeat + 1) % 3;
            _events.RaiseTurnStarted(_currentSeat);
        }

        private void Settle(int winnerSeat)
        {
            Phase = GamePhase.Settled;
            bool landlordWin = winnerSeat == LandlordSeat;

            bool spring = landlordWin && _players.All(p => p.IsLandlord || p.PlayCount == 0);
            bool antiSpring = !landlordWin && _players[LandlordSeat].PlayCount <= 1;
            if (spring || antiSpring)
                Multiplier *= 2;

            var result = new GameResult
            {
                LandlordSeat = LandlordSeat,
                WinnerSeat = winnerSeat,
                LandlordWin = landlordWin,
                BaseScore = BaseScore,
                BombCount = _bombCount,
                Spring = spring || antiSpring,
                Multiplier = Multiplier,
            };

            int unit = BaseScore * Multiplier;
            result.ScoreDeltas = new int[3];
            for (int seat = 0; seat < 3; seat++)
            {
                bool isLandlord = seat == LandlordSeat;
                bool win = isLandlord == landlordWin;
                int share = isLandlord ? 2 : 1;
                result.ScoreDeltas[seat] = (win ? 1 : -1) * unit * share;
            }

            _events.RaiseGameSettled(result);
        }

        private void EnsurePhase(GamePhase phase)
        {
            if (Phase != phase)
                throw new InvalidOperationException($"当前阶段 {Phase}，需要 {phase}");
        }
    }
}
