using UnityEngine;
using UnityEngine.UI;

namespace DDZ.Presentation
{
    /// <summary>内置字体加载（Unity 2022: LegacyRuntime.ttf，旧版 Arial）。</summary>
    public static class UiFont
    {
        private static Font? _font;

        public static Font Get()
        {
            if (_font != null) return _font;
            try
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _font;
        }
    }

    /// <summary>程序化卡面：左上角点数+花色符号，红黑双色，选中上浮由 HandView 控制。</summary>
    public sealed class CardView
    {
        public const float Width = 92f;
        public const float Height = 128f;

        public readonly GameObject Root;
        private readonly Text _cornerText;
        private readonly Image _bg;

        public CardView(Transform parent)
        {
            Root = new GameObject("Card", typeof(RectTransform), typeof(Image));
            Root.transform.SetParent(parent, false);

            _bg = Root.GetComponent<Image>();
            _bg.color = Color.white;

            var cornerGo = new GameObject("Corner", typeof(Text));
            cornerGo.transform.SetParent(Root.transform, false);
            _cornerText = cornerGo.GetComponent<Text>();
            _cornerText.font = UiFont.Get();
            _cornerText.fontSize = 34;
            _cornerText.alignment = TextAnchor.UpperLeft;
            _cornerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rect = _cornerText.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(6, -4);
            rect.sizeDelta = new Vector2(Width - 12, 40);

            var rankGo = new GameObject("Center", typeof(Text));
            rankGo.transform.SetParent(Root.transform, false);
            _centerText = rankGo.GetComponent<Text>();
            _centerText.font = UiFont.Get();
            _centerText.fontSize = 56;
            _centerText.alignment = TextAnchor.MiddleCenter;
            var crect = _centerText.rectTransform;
            crect.anchorMin = Vector2.zero;
            crect.anchorMax = Vector2.one;
            crect.offsetMin = Vector2.zero;
            crect.offsetMax = Vector2.zero;

            var rt = Root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(Width, Height);
        }

        private readonly Text _centerText;

        public void Bind(Card card)
        {
            bool red = card.Suit is Suit.Hearts or Suit.Diamonds;
            bool joker = card.IsJoker;
            _bg.color = joker
                ? new Color(0.85f, 0.75f, 0.95f)
                : Color.white;

            string suitSymbol = card.Suit switch
            {
                Suit.Spades => "♠",
                Suit.Hearts => "♥",
                Suit.Clubs => "♣",
                Suit.Diamonds => "♦",
                _ => "",
            };

            string rankName = card.Rank switch
            {
                Rank.SmallJoker => "小王",
                Rank.BigJoker => "大王",
                Rank.Ten => "10",
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                Rank.Ace => "A",
                Rank.Two => "2",
                _ => ((int)card.Rank).ToString(),
            };

            _cornerText.text = joker ? rankName : $"{rankName}\n{suitSymbol}";
            _cornerText.color = red || joker ? new Color(0.85f, 0.15f, 0.15f) : Color.black;
            _centerText.text = joker ? "🃏" : suitSymbol;
            _centerText.color = _cornerText.color;
        }

        public void SetSelected(bool selected)
        {
            var rt = Root.GetComponent<RectTransform>();
            rt.anchoredPosition = selected ? new Vector2(rt.anchoredPosition.x, 40) : new Vector2(rt.anchoredPosition.x, 0);
            _bg.color = selected ? new Color(1f, 0.92f, 0.6f) : Color.white;
        }
    }
}
