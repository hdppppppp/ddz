using System;
using System.Collections.Generic;
using System.Linq;
using DDZ.Core;
using UnityEngine;

namespace DDZ.Presentation
{
    /// <summary>
    /// 表现层驱动器：持有 GameController 与 AiTicker，把领域事件转为 UI 刷新，
    /// 把玩家输入转回逻辑层调用。UI 元素通过 GameBoard 注入。
    /// </summary>
    public sealed class GamePresenter : MonoBehaviour
    {
        private GameController _game = new();
        private AiTicker _ticker = null!;
        private GameBoard _board = null!;
        private int _humanSeat;

        /// <summary>当前局累计积分（跨局）。</summary>
        private readonly int[] _scores = new int[3];

        public GameController Game => _game;
        public int HumanSeat => _humanSeat;

        private void Awake()
        {
            _board = GetComponent<GameBoard>() ?? throw new InvalidOperationException("GamePresenter 需要 GameBoard 组件");
            _ticker = new AiTicker(_game) { HumanSeat = -1 };
        }

        private void Update()
        {
            _ticker.Tick(Time.deltaTime);

            // 轮到玩家叫分时确保叫分条可见（AI 叫分后轮次回到玩家没有专门事件）
            if (_game.Phase == GamePhase.Bidding && _game.BidTurnSeat == _humanSeat)
                _board.EnableBidding(true);
        }

        /// <summary>开始新的一局（含叫分）。</summary>
        public void StartRound(bool humanPlays)
        {
            _humanSeat = humanPlays ? 0 : -1;
            _ticker.HumanSeat = _humanSeat;
            _game.StartGame();
        }

        private void OnEnable()
        {
            _game.Events.CardsDealt += OnCardsDealt;
            _game.Events.BidMade += OnBidMade;
            _game.Events.BiddingRestarted += OnBiddingRestarted;
            _game.Events.LandlordDecided += OnLandlordDecided;
            _game.Events.TurnStarted += OnTurnStarted;
            _game.Events.CardsPlayed += OnCardsPlayed;
            _game.Events.Passed += OnPassed;
            _game.Events.GameSettled += OnGameSettled;
        }

        private void OnDisable()
        {
            _game.Events.CardsDealt -= OnCardsDealt;
            _game.Events.BidMade -= OnBidMade;
            _game.Events.BiddingRestarted -= OnBiddingRestarted;
            _game.Events.LandlordDecided -= OnLandlordDecided;
            _game.Events.TurnStarted -= OnTurnStarted;
            _game.Events.CardsPlayed -= OnCardsPlayed;
            _game.Events.Passed -= OnPassed;
            _game.Events.GameSettled -= OnGameSettled;
        }

        // ---------- 玩家输入 ----------

        public void HumanBid(int score)
        {
            if (_game.Phase != GamePhase.Bidding || _game.BidTurnSeat != _humanSeat)
                return;
            _game.Bid(_humanSeat, score);
        }

        public void HumanPlay(IReadOnlyList<Card> cards)
        {
            if (_game.Phase != GamePhase.Playing || _game.CurrentSeat != _humanSeat)
                return;
            try
            {
                _game.PlayCards(_humanSeat, cards);
            }
            catch (InvalidOperationException e)
            {
                _board.ShowToast(e.Message);
            }
        }

        public void HumanPass()
        {
            if (_game.Phase != GamePhase.Playing || _game.CurrentSeat != _humanSeat)
                return;
            try
            {
                _game.Pass(_humanSeat);
            }
            catch (InvalidOperationException e)
            {
                _board.ShowToast(e.Message);
            }
        }

        /// <summary>提示：用 PlayAI 给人类玩家找一手可出的牌（或不出）。</summary>
        public IReadOnlyList<Card>? Hint()
        {
            if (_game.Phase != GamePhase.Playing || _game.CurrentSeat != _humanSeat)
                return null;
            return PlayAI.Hint(_game, _humanSeat);
        }

        // ---------- 事件 → UI ----------

        private void OnCardsDealt(int seat, List<Card> hand)
        {
            _board.RenderHand(seat, hand, seat == _humanSeat);
            _board.SetSeatInfo(seat, $"玩家{seat + 1}", 17, landlord: false);
            if (seat == _humanSeat)
                _board.EnableBidding(true);
        }

        private void OnBidMade(int seat, int score)
        {
            _board.ShowBubble(seat, score == 0 ? "不叫" : $"{score} 分");
            _board.EnableBidding(seat == _humanSeat && _game.Phase == GamePhase.Bidding);
        }

        private void OnBiddingRestarted()
        {
            _board.ShowToast("无人叫地主，重新发牌");
            StartRound(_humanSeat >= 0);
        }

        private void OnLandlordDecided(int seat, List<Card> bottom)
        {
            _board.RevealBottom(bottom, seat);
            _board.SetSeatInfo(seat, $"玩家{seat + 1}", _game.Players[seat].HandCount, landlord: true);
            if (seat == _humanSeat)
            {
                _board.RenderHand(seat, _game.Players[seat].Hand, true);
                _board.ShowToast("你是地主！");
            }
        }

        private void OnTurnStarted(int seat)
        {
            bool yourTurn = seat == _humanSeat && _game.Phase == GamePhase.Playing;
            _board.HighlightSeat(seat);
            _board.EnableActions(yourTurn, canPass: !(_game.LastPlay == null || _game.LastPlaySeat == seat));
        }

        private void OnCardsPlayed(int seat, HandPattern pattern)
        {
            _board.ShowPlayedCards(seat, pattern.Cards);
            _board.SetSeatInfo(seat, $"玩家{seat + 1}", _game.Players[seat].HandCount, _game.Players[seat].IsLandlord);
            if (seat != _humanSeat)
                _board.RenderHand(seat, _game.Players[seat].Hand, false);
        }

        private void OnPassed(int seat)
        {
            _board.ShowBubble(seat, "不出");
        }

        private void OnGameSettled(GameResult result)
        {
            for (int seat = 0; seat < 3; seat++)
                _scores[seat] += result.ScoreDeltas[seat];

            _board.ShowResult(result, _scores, _humanSeat, () => StartRound(_humanSeat >= 0));
        }
    }
}
