using UnityEngine;
using UnityEngine.UI;

namespace TangentLudoEmpire.Wallet.UI
{
    /// <summary>Tiny helpers so the Phase 3 UI scripts can self-build a plain uGUI hierarchy
    /// (Text + Button only, no art) without a prefab. Editor authoring can replace any of this later.</summary>
    internal static class WalletUiKit
    {
        public static readonly Color Panel  = new Color(0.10f, 0.11f, 0.14f, 0.96f);
        public static readonly Color Accent = new Color(0.20f, 0.55f, 0.95f, 1f);
        public static readonly Color Danger = new Color(0.85f, 0.30f, 0.30f, 1f);
        public static readonly Color Text   = new Color(0.95f, 0.96f, 0.98f, 1f);

        public static Canvas EnsureCanvas(string name)
        {
            var existing = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in existing)
                if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name == name)
                    return c;

            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            return rt;
        }

        public static GameObject Container(Transform parent, string name, Vector2 size, Vector2 anchoredPos, Color? bg = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = Rect(go);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            if (bg.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = bg.Value;
            }
            return go;
        }

        public static Text Label(Transform parent, string text, int fontSize, Vector2 size, Vector2 anchoredPos,
                                 TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = Rect(go);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.color = Text;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        public static Button Btn(Transform parent, string text, Vector2 size, Vector2 anchoredPos,
                                 UnityEngine.Events.UnityAction onClick, Color? tint = null)
        {
            var go = new GameObject("Button_" + text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = Rect(go);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var img = go.AddComponent<Image>();
            img.color = tint ?? Accent;
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(onClick);
            Label(go.transform, text, 34, size, Vector2.zero);
            return btn;
        }

        /// <summary>Minimal legacy uGUI Dropdown (caption + a one-item Toggle template) - just enough for
        /// a provider picker, no art.</summary>
        public static Dropdown DropdownField(Transform parent, string[] options, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject("Dropdown", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = Rect(go);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.10f);
            var dd = go.AddComponent<Dropdown>();

            var caption = Label(go.transform, options.Length > 0 ? options[0] : "", 30,
                                size - new Vector2(60, 0), Vector2.zero, TextAnchor.MiddleLeft);
            dd.captionText = caption;

            // Legacy Dropdown needs a Template (hidden) -> Viewport -> Content -> Item(Toggle) chain to open a list.
            var template = Container(go.transform, "Template", new Vector2(size.x, 160), new Vector2(0, -(size.y / 2f + 80f)));
            template.SetActive(false);
            var templateRt = Rect(template);
            templateRt.pivot = new Vector2(0.5f, 1f);

            var viewport = Container(template.transform, "Viewport", new Vector2(size.x, 160), Vector2.zero,
                                     new Color(0.05f, 0.05f, 0.07f, 0.98f));
            viewport.AddComponent<RectMask2D>();
            var scroll = template.AddComponent<ScrollRect>();
            scroll.viewport = Rect(viewport);
            scroll.horizontal = false;

            var content = Container(viewport.transform, "Content", new Vector2(size.x, 40), Vector2.zero);
            var contentRt = Rect(content);
            contentRt.pivot = new Vector2(0.5f, 1f);
            scroll.content = contentRt;

            var item = new GameObject("Item", typeof(RectTransform));
            item.transform.SetParent(content.transform, false);
            var itemRt = Rect(item);
            itemRt.sizeDelta = new Vector2(size.x, 40);
            var itemBg = item.AddComponent<Image>();
            itemBg.color = new Color(1f, 1f, 1f, 0.06f);
            var itemToggle = item.AddComponent<Toggle>();
            itemToggle.targetGraphic = itemBg;
            var itemLabel = Label(item.transform, "Option", 26, new Vector2(size.x - 20, 40), Vector2.zero, TextAnchor.MiddleLeft);

            dd.template = templateRt;
            dd.itemText = itemLabel;

            dd.options.Clear();
            foreach (var o in options) dd.options.Add(new Dropdown.OptionData(o));
            dd.value = 0;
            dd.RefreshShownValue();

            return dd;
        }

        public static InputField Input(Transform parent, string placeholder, Vector2 size, Vector2 anchoredPos,
                                       bool numeric)
        {
            var go = new GameObject("Input_" + placeholder, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = Rect(go);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.10f);

            var field = go.AddComponent<InputField>();

            var ph = Label(go.transform, placeholder, 30, size - new Vector2(24, 0), Vector2.zero, TextAnchor.MiddleLeft);
            ph.color = new Color(1f, 1f, 1f, 0.4f);
            var txt = Label(go.transform, "", 30, size - new Vector2(24, 0), Vector2.zero, TextAnchor.MiddleLeft);

            field.placeholder = ph;
            field.textComponent = txt;
            field.contentType = numeric ? InputField.ContentType.DecimalNumber : InputField.ContentType.Standard;
            return field;
        }
    }
}
