using System;
using System.Text;
using DDZ.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DDZ.Presentation
{
    /// <summary>结算弹窗：胜负、倍数明细、三方得分、"再来一局"。</summary>
    public sealed class ResultDialog
    {
        private readonly GameObject _root;
        private readonly Text _title;
        private readonly Text _detail;
        private GameObject _restartBtn = null!;

        public ResultDialog(RectTransform canvas, Font font)
        {
            var go = new GameObject("ResultDialog", typeof(Image));
            go.transform.SetParent(canvas, false);
            _root = go;
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.82f);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _title = CreateText(go.transform, "Title", "", font, 64, new Vector2(0, 330), new Vector2(1080, 110));
            _detail = CreateText(go.transform, "Detail", "", font, 36, new Vector2(0, 60), new Vector2(1080, 420));

            _restartBtn = BiddingView.CreateButton(go.transform, "再来一局", font, 320, new Vector2(0, -260), () => { },
                new Color(0.8f, 0.5f, 0.1f));

            go.SetActive(false);
        }

        private static Text CreateText(Transform parent, string name, string content, Font font, int size,
            Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = content;
            t.rectTransform.sizeDelta = sizeDelta;
            t.rectTransform.anchoredPosition = pos;
            return t;
        }

        public void Show(GameResult result, int[] scores, int humanSeat, Action onRestart)
        {
            bool humanWin = result.LandlordWin == (humanSeat == result.LandlordSeat);
            _title.text = humanWin ? "胜 利 🎉" : "失 败";
            _title.color = humanWin ? new Color(1f, 0.85f, 0.2f) : new Color(0.8f, 0.35f, 0.35f);

            var sb = new StringBuilder();
            sb.AppendLine($"底分 {result.BaseScore} × 倍数 {result.Multiplier}" +
                          (result.BombCount > 0 ? $"（炸弹×{result.BombCount}）" : "") +
                          (result.Spring ? "（春天）" : ""));
            sb.AppendLine();
            for (int seat = 0; seat < 3; seat++)
            {
                string role = seat == result.LandlordSeat ? "地主" : "农民";
                string you = seat == humanSeat ? "（你）" : "";
                int delta = result.ScoreDeltas[seat];
                sb.AppendLine($"玩家{seat + 1}{you} [{role}]  {delta:+0;-0}   累计 {scores[seat]}");
            }
            _detail.text = sb.ToString();

            // 重建按钮（避免旧 lambda 闭包）
            var old = _restartBtn;
            _restartBtn = BiddingView.CreateButton(_root.transform, "再来一局", UiFont.Get(), 320,
                new Vector2(0, -260), onRestart, new Color(0.8f, 0.5f, 0.1f));
            if (old != null) UnityEngine.Object.Destroy(old);

            _root.SetActive(true);
        }

        public void Hide() => _root.SetActive(false);
    }
}
