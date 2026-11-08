using System;
using UnityEngine;
using UnityEngine.UI;

namespace DDZ.Presentation
{
    /// <summary>叫分按钮条：不叫 / 1分 / 2分 / 3分。</summary>
    public sealed class BiddingView
    {
        private readonly GameObject _root;

        public BiddingView(GameObject container, Font font, Action<int> onBid)
        {
            _root = container;
            string[] labels = { "不叫", "1 分", "2 分", "3 分" };
            int[] scores = { 0, 1, 2, 3 };

            for (int i = 0; i < labels.Length; i++)
            {
                CreateButton(container.transform, labels[i], font, 220, new Vector2((i - 1.5f) * 240, 0), () => onBid(scores[i]));
            }
        }

        public void Show() => _root.SetActive(true);
        public void Hide() => _root.SetActive(false);

        internal static GameObject CreateButton(Transform parent, string label, Font font,
            float width, Vector2 pos, Action onClick, Color? color = null)
        {
            var go = new GameObject($"Btn_{label}", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color ?? new Color(0.16f, 0.55f, 0.3f);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 92);
            rect.anchoredPosition = pos;

            var textGo = new GameObject("Label", typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var t = textGo.GetComponent<Text>();
            t.font = font;
            t.fontSize = 38;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = label;
            t.rectTransform.sizeDelta = rect.sizeDelta;

            go.GetComponent<Button>().onClick.AddListener(() => onClick());
            return go;
        }
    }

    /// <summary>操作按钮条：不出 / 提示 / 出牌。</summary>
    public sealed class ActionBar
    {
        private readonly GameObject _root;
        private readonly GameObject _passBtn;

        public ActionBar(GameObject container, Font font, Action onPlay, Action onPass, Action onHint)
        {
            _root = container;
            _passBtn = BiddingView.CreateButton(container.transform, "不出", font, 200, new Vector2(-330, 0), onPass,
                new Color(0.55f, 0.4f, 0.1f));
            BiddingView.CreateButton(container.transform, "提示", font, 200, new Vector2(0, 0), onHint,
                new Color(0.15f, 0.4f, 0.7f));
            BiddingView.CreateButton(container.transform, "出牌", font, 200, new Vector2(330, 0), onPlay,
                new Color(0.75f, 0.2f, 0.2f));
        }

        public void Show(bool canPass)
        {
            _root.SetActive(true);
            _passBtn.SetActive(canPass);
        }

        public void Hide() => _root.SetActive(false);
    }
}
