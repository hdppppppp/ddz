using System.Collections.Generic;
using DDZ.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DDZ.Presentation
{
    /// <summary>一个座位：头像框 + 剩牌数 + 地主标识 + 不出气泡 + 中央出牌区。</summary>
    public sealed class SeatView
    {
        private readonly Text _infoText;
        private readonly Image _frame;
        private readonly Text _bubble;
        private readonly GameObject _bubbleGo;
        private readonly Color _normal = new(0f, 0f, 0f, 0.35f);
        private readonly Color _highlight = new(1f, 0.85f, 0.2f, 0.85f);

        public GameObject? PlayedArea { get; set; }
        private readonly List<GameObject> _playedViews = new();

        public SeatView(GameObject container, Font font)
        {
            var rect = container.GetComponent<RectTransform>();

            var frameGo = new GameObject("Frame", typeof(Image));
            frameGo.transform.SetParent(rect, false);
            _frame = frameGo.GetComponent<Image>();
            _frame.color = _normal;
            var frameRect = frameGo.GetComponent<RectTransform>();
            frameRect.sizeDelta = new Vector2(220, 110);

            var infoGo = new GameObject("Info", typeof(Text));
            infoGo.transform.SetParent(rect, false);
            _infoText = infoGo.GetComponent<Text>();
            _infoText.font = font;
            _infoText.fontSize = 28;
            _infoText.alignment = TextAnchor.MiddleCenter;
            _infoText.color = Color.white;
            _infoText.rectTransform.sizeDelta = new Vector2(220, 110);

            var bubbleGo = new GameObject("Bubble", typeof(Image));
            bubbleGo.transform.SetParent(rect, false);
            _bubbleGo = bubbleGo;
            var bubbleImg = bubbleGo.GetComponent<Image>();
            bubbleImg.color = new Color(0f, 0f, 0f, 0.7f);
            bubbleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 70);
            bubbleGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(140, 60);

            var bubbleTextGo = new GameObject("Text", typeof(Text));
            bubbleTextGo.transform.SetParent(bubbleGo.transform, false);
            _bubble = bubbleTextGo.GetComponent<Text>();
            _bubble.font = font;
            _bubble.fontSize = 34;
            _bubble.alignment = TextAnchor.MiddleCenter;
            _bubble.color = Color.white;
            _bubble.rectTransform.sizeDelta = new Vector2(150, 70);
            bubbleGo.SetActive(false);
        }

        public void UpdateInfo(string name, int handCount, bool landlord)
        {
            string landlordMark = landlord ? " 👑" : "";
            _infoText.text = $"{name}{landlordMark}\n剩 {handCount} 张";
        }

        public void UpdateHandCount(int handCount)
        {
            // 仅更新数字：从文本中替换（简单实现：保留第一行）
            var lines = _infoText.text.Split('\n');
            _infoText.text = $"{lines[0]}\n剩 {handCount} 张";
        }

        public void SetHighlight(bool on) => _frame.color = on ? _highlight : _normal;

        public void ShowBubble(string text)
        {
            _bubble.text = text;
            _bubbleGo.SetActive(true);
        }

        public void ClearPlayed()
        {
            foreach (var go in _playedViews) Object.Destroy(go);
            _playedViews.Clear();
            _bubbleGo.SetActive(false);
        }

        public void ShowPlayed(IReadOnlyList<Card> cards)
        {
            ClearPlayed();
            if (PlayedArea == null) return;

            int n = cards.Count;
            if (n == 0) return;

            float overlap = n > 8 ? 40f : n > 5 ? 52f : 66f;
            float scale = n > 12 ? 0.75f : 0.85f;
            float w = CardView.Width * scale;
            float h = CardView.Height * scale;
            float totalW = w + overlap * (n - 1);
            float startX = -totalW / 2 + w / 2;

            for (int i = 0; i < n; i++)
            {
                var view = new CardView(PlayedArea.transform);
                view.Bind(cards[i]);
                var rt = view.Root.GetComponent<RectTransform>();
                rt.localScale = Vector3.one * scale;
                rt.anchoredPosition = new Vector2(startX + i * overlap, 0);
                _playedViews.Add(view.Root);
            }
        }
    }

    /// <summary>顶部底牌 3 张展示（叫地主后翻开）。</summary>
    public sealed class BottomCardsView
    {
        private readonly GameObject _container;
        private readonly List<GameObject> _views = new();

        public BottomCardsView(GameObject container)
        {
            _container = container;
        }

        public void Render(IReadOnlyList<Card> cards)
        {
            foreach (var go in _views) Object.Destroy(go);
            _views.Clear();

            float w = CardView.Width * 0.55f;
            float h = CardView.Height * 0.55f;
            for (int i = 0; i < cards.Count; i++)
            {
                var view = new CardView(_container.transform);
                view.Bind(cards[i]);
                var rt = view.Root.GetComponent<RectTransform>();
                rt.localScale = Vector3.one * 0.55f;
                rt.anchoredPosition = new Vector2((i - 1) * (w + 10), 0);
                _views.Add(view.Root);
            }
        }
    }
}
