using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DDZ.Presentation
{
    /// <summary>
    /// M1 冒烟引导：纯代码搭建最小 UI，证明云端构建产物是可运行的游戏壳。
    /// 后续里程碑在这里替换为完整牌桌视图。
    /// </summary>
    public class SceneBootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            var camera = Camera.main;
            if (camera != null)
                camera.backgroundColor = new Color(0.10f, 0.35f, 0.20f);

            BuildMinimalUi();
        }

        private void BuildMinimalUi()
        {
            var canvasGo = new GameObject(
                "Canvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;

            if (FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                esGo.transform.SetParent(transform, false);
            }

            Font font = LoadBuiltinFont();

            CreateText(
                canvasGo.transform,
                "Title",
                "DDZ - Dou Di Zhu\nCloud Build OK",
                font,
                72,
                new Vector2(0, 320));

            CreateText(
                canvasGo.transform,
                "Info",
                $"Cards: {Core.Deck.TotalCount}   Unity: {Application.unityVersion}   v{Application.version}",
                font,
                40,
                new Vector2(0, -420));
        }

        private static void CreateText(
            Transform parent,
            string name,
            string content,
            Font font,
            int fontSize,
            Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text = content;

            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(1080, 400);
            rect.anchoredPosition = anchoredPosition;
        }

        private static Font LoadBuiltinFont()
        {
            Font font = null;
            try
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                // Unity 2022 之前的内置字体名
            }

            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }
    }
}
