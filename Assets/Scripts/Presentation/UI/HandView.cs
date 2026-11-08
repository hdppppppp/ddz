using System.Collections.Generic;
using DDZ.Core;
using UnityEngine;

namespace DDZ.Presentation
{
    /// <summary>人类玩家手牌条：横向重叠排布、点击选中/取消。</summary>
    public sealed class HandView
    {
        private readonly GameObject _container;
        private readonly List<CardView> _views = new();
        private readonly HashSet<int> _selected;
        private IReadOnlyList<Card> _cards = new List<Card>();

        public HandView(GameObject container, HashSet<int> selected)
        {
            _container = container;
            _selected = selected;
        }

        public void Render(IReadOnlyList<Card> cards, HashSet<int> selected)
        {
            _cards = cards;
            foreach (var v in _views) Object.Destroy(v.Root);
            _views.Clear();

            int n = cards.Count;
            if (n == 0) return;

            float overlap = n > 12 ? 52f : 62f;
            float totalW = CardView.Width + overlap * (n - 1);
            float startX = -totalW / 2 + CardView.Width / 2;

            for (int i = 0; i < n; i++)
            {
                var view = new CardView(_container.transform);
                view.Bind(cards[i]);
                var rt = view.Root.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(startX + i * overlap, 0);

                int cardId = cards[i].Id;
                view.SetSelected(selected.Contains(cardId));

                var button = view.Root.AddComponent<Button>();
                button.onClick.AddListener(() =>
                {
                    if (selected.Contains(cardId)) selected.Remove(cardId);
                    else selected.Add(cardId);
                    view.SetSelected(selected.Contains(cardId));
                });

                _views.Add(view);
            }
        }

        /// <summary>外部（提示）改变选中后调用。</summary>
        public void Refresh(IReadOnlyList<Card> cards, HashSet<int> selected)
        {
            for (int i = 0; i < _cards.Count && i < _views.Count; i++)
                _views[i].SetSelected(selected.Contains(_cards[i].Id));
        }
    }
}
