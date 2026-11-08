using System;
using System.Collections.Generic;
using System.Linq;
using DDZ.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DDZ.Presentation
{
    /// <summary>
    /// 牌桌视图：纯代码构建 UGUI（无预制体），提供 GamePresenter 需要的全部渲染接口。
    /// 卡面为程序化绘制（文字+花色符号+色块），后续可替换为图集。
    /// </summary>
    public sealed class GameBoard : MonoBehaviour
    {
        private const int RefW = 1080, RefH = 1920;

        private Canvas _canvas = null!;
        private RectTransform _canvasRect = null!;
        private readonly Dictionary<int, SeatView> _seats = new();
        private readonly List<CardView> _humanCardViews = new();
        private readonly HashSet<int> _selected = new();
        private HandView? _handView;
        private BottomCardsView? _bottomView;
        private ActionBar? _actions;
        private BiddingView? _bidding;
        private ResultDialog? _result;
        private Text? _toast;
        private Text? _multiplierText;
        private float _toastTimer;

        private void Awake()
        {
            BuildStaticUi();
        }

        private void Update()
        {
            if (_toastTimer > 0)
            {
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0 && _toast != null)
                    _toast.gameObject.SetActive(false);
            }
        }

        // ================= 构建静态 UI =================

        private void BuildStaticUi()
        {
            var camera = Camera.main;
            if (camera != null)
                camera.backgroundColor = new Color(0.07f, 0.30f, 0.17f);

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 1f;
            _canvasRect = canvasGo.GetComponent<RectTransform>();

            var font = UiFont.Get();

            // 顶部：倍数
            _multiplierText = CreateText(_canvasRect, "Multiplier", "倍数 x1", font, 36,
                new Vector2(0, RefH / 2 - 90), new Vector2(400, 60));

            // 底牌区（顶部中央下方）
            _bottomView = new BottomCardsView(CreateContainer("Bottom", new Vector2(0, RefH / 2 - 170), new Vector2(300, 130)));

            // 左右 AI 座位
            _seats[1] = new SeatView(CreateContainer("Seat1", new Vector2(-RefW / 2 + 130, 120), new Vector2(230, 260)), font);
            _seats[2] = new SeatView(CreateContainer("Seat2", new Vector2(RefW / 2 - 130, 120), new Vector2(230, 260)), font);
            _seats[0] = new SeatView(CreateContainer("Seat0", new Vector2(-RefW / 2 + 130, -RefH / 2 + 260), new Vector2(230, 260)), font);

            // 中央出牌区
            _seats[1].PlayedArea = CreateContainer("Played1", new Vector2(-RefW / 4, 330), new Vector2(360, 260));
            _seats[2].PlayedArea = CreateContainer("Played2", new Vector2(RefW / 4, 330), new Vector2(360, 260));
            _seats[0].PlayedArea = CreateContainer("Played0", new Vector2(0, -20), new Vector2(460, 260));

            // 手牌
            _handView = new HandView(CreateContainer("Hand", new Vector2(0, -RefH / 2 + 340), new Vector2(RefW - 60, 260)));

            // 操作按钮
            _actions = new ActionBar(CreateContainer("Actions", new Vector2(0, -RefH / 2 + 560), new Vector2(RefW, 130)),
                font, OnPlayClicked, OnPassClicked, OnHintClicked);

            // 叫分按钮
            _bidding = new BiddingView(CreateContainer("Bidding", new Vector2(0, -RefH / 2 + 560), new Vector2(RefW, 130)),
                font, OnBidClicked);

            // 结算弹窗
            _result = new ResultDialog(_canvasRect, font);

            // Toast
            _toast = CreateText(_canvasRect, "Toast", "", font, 40, new Vector2(0, 240), new Vector2(900, 90));
            _toast.gameObject.SetActive(false);

            _actions.Hide();
            _bidding.Hide();
        }

        private GameObject CreateContainer(string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_canvasRect, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return go;
        }

        private static Text CreateText(Transform parent, string name, string content, Font font, int size,
            Vector2 pos, Vector2? sizeDelta = null)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = content;
            var rect = t.rectTransform;
            rect.sizeDelta = sizeDelta ?? new Vector2(400, 60);
            rect.anchoredPosition = pos;
            return t;
        }

        // ================= GamePresenter 调用的接口 =================

        public void RenderHand(int seat, IReadOnlyList<Card> hand, bool interactive)
        {
            if (!interactive)
            {
                _seats.TryGetValue(seat, out var sv);
                if (sv != null) sv.UpdateHandCount(hand.Count);
                return;
            }

            _selected.Clear();
            _handView!.Render(hand, _selected);
        }

        public void SetSeatInfo(int seat, string name, int handCount, bool landlord)
        {
            if (_seats.TryGetValue(seat, out var sv))
                sv.UpdateInfo(name, handCount, landlord);
        }

        public void HighlightSeat(int seat)
        {
            foreach (var kv in _seats)
                kv.Value.SetHighlight(kv.Key == seat);
        }

        public void ShowBubble(int seat, string text)
        {
            if (_seats.TryGetValue(seat, out var sv))
                sv.ShowBubble(text);
            // 出牌气泡与中央牌面互斥：出牌时清掉
            if (text != "不出")
                sv?.ClearPlayed();
        }

        public void ShowPlayedCards(int seat, IReadOnlyList<Card> cards)
        {
            if (_seats.TryGetValue(seat, out var sv))
                sv.ShowPlayed(cards);
        }

        public void RevealBottom(IReadOnlyList<Card> bottom, int landlordSeat)
        {
            _bottomView!.Render(bottom);
            _multiplierText!.text = $"倍数 x{_gameMultiplier}";
        }

        public void EnableBidding(bool enabled)
        {
            if (enabled) { _bidding!.Show(); _actions!.Hide(); }
        }

        public void EnableActions(bool enabled, bool canPass)
        {
            _bidding!.Hide();
            if (enabled)
                _actions!.Show(canPass);
            else
                _actions!.Hide();
        }

        public void ShowToast(string message)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toast.gameObject.SetActive(true);
            _toastTimer = 2f;
        }

        private int _gameMultiplier = 1;

        public void ShowResult(GameResult result, int[] scores, int humanSeat, Action onRestart)
        {
            _gameMultiplier = result.Multiplier;
            _multiplierText!.text = $"倍数 x{result.Multiplier}";
            _result!.Show(result, scores, humanSeat, onRestart);
        }

        // ================= 按钮回调 =================

        private void OnBidClicked(int score) => presenter?.HumanBid(score);
        private void OnPassClicked() => presenter?.HumanPass();
        private void OnHintClicked()
        {
            var hint = presenter?.Hint();
            if (hint == null) return;
            _selected.Clear();
            foreach (var c in hint) _selected.Add(c.Id);
            _handView!.Render(presenter!.Game.Players[presenter.HumanSeat].Hand, _selected);
        }

        private GamePresenter? presenter;
        private void Start() => presenter = GetComponent<GamePresenter>();

        private void OnPlayClicked()
        {
            if (presenter == null) return;
            var hand = presenter.Game.Players[presenter.HumanSeat].Hand;
            var chosen = hand.Where(c => _selected.Contains(c.Id)).ToList();
            if (chosen.Count == 0)
            {
                ShowToast("请先选牌");
                return;
            }
            presenter.HumanPlay(chosen);
            _selected.Clear();
        }
    }
}
