using UnityEngine;
using UnityEngine.UI;

namespace NavianChallenge
{
    /// <summary>
    /// Small helpers for assembling uGUI in code. The panels are built at runtime rather than
    /// authored as prefabs so the whole app can be dropped into the base scene with a single
    /// component — no hand-wiring in the editor.
    /// </summary>
    public static class UIFactory
    {
        static GameObject New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>A world-space canvas scaled so <paramref name="pixelSize"/>.y maps to
        /// <paramref name="worldHeight"/> world units.</summary>
        public static Canvas WorldCanvas(string name, Vector2 pixelSize, float worldHeight, Camera cam)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;

            var rt = (RectTransform)canvas.transform;
            rt.sizeDelta = pixelSize;
            float s = worldHeight / pixelSize.y;
            rt.localScale = new Vector3(s, s, s);
            return canvas;
        }

        public static RectTransform Panel(Transform parent, Color bg, string name = "Panel")
        {
            var go = New(name, parent);
            go.AddComponent<Image>().color = bg;
            return (RectTransform)go.transform;
        }

        public static VerticalLayoutGroup VerticalLayout(GameObject go, int pad, float spacing)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.spacing = spacing;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HorizontalLayout(GameObject go, float spacing)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            return h;
        }

        public static RectTransform Row(Transform parent, float spacing, float height)
        {
            var go = New("Row", parent);
            HorizontalLayout(go, spacing);
            Size(go, h: height, flexW: 1f);
            return (RectTransform)go.transform;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var go = New("Label", parent);
            var t = go.AddComponent<Text>();
            t.font = UITheme.Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(Transform parent, string text, out Text label)
        {
            var go = New("Button", parent);
            var img = go.AddComponent<Image>();
            img.color = UITheme.ButtonBg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            label = Label(go.transform, text, 12, UITheme.TextPrimary, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return btn;
        }

        public static Image Swatch(Transform parent, Color color, float size)
        {
            var go = New("Swatch", parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            Size(go, w: size, h: size);
            return img;
        }

        /// <summary>Standard uGUI slider (background bar + fill + handle) built in code.</summary>
        public static Slider Slider(Transform parent, float value)
        {
            var go = New("Slider", parent);
            var slider = go.AddComponent<Slider>();

            var bg = New("Background", go.transform);
            bg.AddComponent<Image>().color = UITheme.SliderBg;
            Anchor((RectTransform)bg.transform, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));

            var fillArea = New("Fill Area", go.transform);
            var faRt = (RectTransform)fillArea.transform;
            Anchor(faRt, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), new Vector2(6f, 0f), new Vector2(-6f, 0f));
            var fill = New("Fill", fillArea.transform);
            fill.AddComponent<Image>().color = UITheme.Accent;
            var fillRt = (RectTransform)fill.transform;
            Anchor(fillRt, Vector2.zero, Vector2.one);
            fillRt.sizeDelta = new Vector2(10f, 0f);

            var handleArea = New("Handle Slide Area", go.transform);
            Anchor((RectTransform)handleArea.transform, Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
            var handle = New("Handle", handleArea.transform);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = UITheme.Handle;
            ((RectTransform)handle.transform).sizeDelta = new Vector2(12f, 0f);

            slider.fillRect = fillRt;
            slider.handleRect = (RectTransform)handle.transform;
            slider.targetGraphic = handleImg;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            return slider;
        }

        // --- rect helpers ---

        public static LayoutElement Size(GameObject go, float w = -1f, float h = -1f, float flexW = -1f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (w >= 0f) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0f) { le.preferredHeight = h; le.minHeight = h; }
            if (flexW >= 0f) le.flexibleWidth = flexW;
            return le;
        }

        public static void Stretch(RectTransform rt) =>
            Anchor(rt, Vector2.zero, Vector2.one);

        public static void Anchor(RectTransform rt, Vector2 min, Vector2 max,
                                  Vector2 offMin = default, Vector2 offMax = default)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
        }
    }
}
