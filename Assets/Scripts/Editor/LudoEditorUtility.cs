using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Shared helpers used by the "Ludo Tools" editor scripts: wiring
    /// serialized references, and building the same plain-styled UI building
    /// blocks (Canvas, Text, Button, Panel, Slider, Toggle, radial meter)
    /// so every UI-building tool looks and behaves consistently.
    /// </summary>
    internal static class LudoEditorUtility
    {
        private const string UndoLabel = "Ludo Tools - Build UI";

        // ---------------- Serialized field wiring ----------------

        /// <summary>
        /// Sets a private [SerializeField] object-reference field on a component
        /// through the serialization system, so Undo and the "unsaved scene" dirty
        /// flag work correctly (a plain reflection/field set would skip both).
        /// </summary>
        public static void SetField(Component target, string fieldName, Object value)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;

            prop.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        /// <summary>Sets a float [SerializeField] field through the serialization system (Object can't carry plain values).</summary>
        public static void SetFloatField(Component target, string fieldName, float value)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;

            prop.floatValue = value;
            so.ApplyModifiedProperties();
        }

        /// <summary>Sets an enum [SerializeField] field through the serialization system (Object can't carry enum values).</summary>
        public static void SetEnumField(Component target, string fieldName, System.Enum value)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;

            prop.enumValueIndex = System.Convert.ToInt32(value);
            so.ApplyModifiedProperties();
        }

        /// <summary>Sets an array/list [SerializeField] field (e.g. an Image[]) through the serialization system.</summary>
        public static void SetObjectArray(Component target, string fieldName, Object[] values)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) return;

            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedProperties();
        }

        // ---------------- Canvas / EventSystem ----------------

        public static Canvas GetOrCreateCanvas()
        {
            Canvas existing = Object.FindAnyObjectByType<Canvas>();
            if (existing != null) return existing;

            GameObject go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;

            GameObject go = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.AddComponent<InputSystemUIInputModule>(); // this project uses the new Input System exclusively
        }

        // ---------------- Low-level UI building blocks ----------------

        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);

            Text text = go.AddComponent<Text>();
            text.text = content;
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            Text labelText = CreateText(go.transform, "Label", label, 28, TextAnchor.MiddleCenter);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        public static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = color;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return go;
        }

        /// <summary>A minimal but functional slider (plain box, no fill/handle graphics - restyle later if you want).</summary>
        public static Slider CreateSlider(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);

            Image background = go.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.25f);

            Slider slider = go.AddComponent<Slider>();
            slider.targetGraphic = background;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            return slider;
        }

        /// <summary>A minimal but functional toggle (plain box, no checkmark graphic - restyle later if you want).</summary>
        public static Toggle CreateToggle(Transform parent, string name, string label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);

            Image background = go.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.25f);

            Toggle toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = background;

            Text labelText = CreateText(go.transform, "Label", label, 24, TextAnchor.MiddleLeft);
            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = new Vector2(1f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = new Vector2(10f, 0f);
            labelRect.sizeDelta = new Vector2(200f, 0f);

            return toggle;
        }

        /// <summary>
        /// A radially-filling Image (no sprite assigned, so it fills as a plain
        /// pie-wedge/rectangle reveal rather than a true circle) - good enough as a
        /// functional stand-in for a circular/semi-circular meter. Assign a round
        /// sprite later for a proper look; the fillAmount-driven behavior won't change.
        /// </summary>
        public static Image CreateRadialImage(Transform parent, string name, Color color, Image.FillMethod fillMethod, int originEnumValue)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.color = color;
            image.type = Image.Type.Filled;
            image.fillMethod = fillMethod;
            image.fillOrigin = originEnumValue;
            image.fillAmount = 0f;

            return image;
        }

        /// <summary>Unity's built-in rounded-rectangle UI sprite - the default look of a new UI Image.</summary>
        public static Sprite RoundedSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        /// <summary>
        /// A translucent, rounded-corner panel with a glowing colored outline - a plain-UI
        /// approximation of a "glassmorphic" frosted-glass panel. There's no real background
        /// blur (that needs a custom shader), but the rounded shape + soft tint + neon edge
        /// reads as "glass" at a glance. Restyle the color/outline freely afterwards.
        /// </summary>
        public static Image CreateGlassPanel(Transform parent, string name, Color glassTint, Color borderGlow)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = glassTint;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = borderGlow;
            outline.effectDistance = new Vector2(2f, 2f);

            return image;
        }

        public static void PositionRow(RectTransform rt, float verticalAnchor, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, verticalAnchor);
            rt.anchorMax = new Vector2(0.5f, verticalAnchor);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>A functional single-line text field: background + editable Text + italic Placeholder Text.</summary>
        public static InputField CreateInputField(Transform parent, string name, string placeholderText)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
            go.transform.SetParent(parent, false);

            Image background = go.AddComponent<Image>();
            background.sprite = RoundedSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(1f, 1f, 1f, 0.15f);

            InputField inputField = go.AddComponent<InputField>();
            inputField.targetGraphic = background;

            Text text = CreateText(go.transform, "Text", "", 24, TextAnchor.MiddleLeft);
            InsetFullRect(text.rectTransform, 16f, 6f);

            Text placeholder = CreateText(go.transform, "Placeholder", placeholderText, 24, TextAnchor.MiddleLeft);
            InsetFullRect(placeholder.rectTransform, 16f, 6f);
            placeholder.color = new Color(1f, 1f, 1f, 0.5f);
            placeholder.fontStyle = FontStyle.Italic;

            inputField.textComponent = text;
            inputField.placeholder = placeholder;

            return inputField;
        }

        /// <summary>
        /// A vertical-only scroll view (masked Viewport + auto-growing Content) for a
        /// scrolling list like a chat log. Parent new entries under the returned Content
        /// RectTransform; a VerticalLayoutGroup + ContentSizeFitter stack and size them
        /// automatically, so ScrollRect.verticalNormalizedPosition = 0f always means "bottom".
        /// </summary>
        public static (ScrollRect scrollRect, RectTransform content) CreateVerticalScrollView(Transform parent, string name)
        {
            GameObject scrollGO = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(scrollGO, UndoLabel);
            scrollGO.transform.SetParent(parent, false);
            scrollGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            ScrollRect scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(viewportGO, UndoLabel);
            viewportGO.transform.SetParent(scrollGO.transform, false);
            RectTransform viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f); // near-invisible; Mask needs a Graphic to clip against
            viewportGO.AddComponent<Mask>().showMaskGraphic = false;

            GameObject contentGO = new GameObject("Content", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(contentGO, UndoLabel);
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layoutGroup = contentGO.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.spacing = 4f;
            layoutGroup.padding = new RectOffset(8, 8, 8, 8);

            ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            return (scrollRect, contentRect);
        }

        /// <summary>Stretches a RectTransform to fill its parent, inset by the given horizontal/vertical margin.</summary>
        public static void InsetFullRect(RectTransform rt, float horizontalInset, float verticalInset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(horizontalInset, verticalInset);
            rt.offsetMax = new Vector2(-horizontalInset, -verticalInset);
        }

        /// <summary>Sets a RectTransform's anchors/offsets to a full-width horizontal band between two vertical fractions (0-1) of its parent, inset by the given margin.</summary>
        public static void PositionBand(RectTransform rt, float fromFraction, float toFraction, float margin)
        {
            rt.anchorMin = new Vector2(0f, fromFraction);
            rt.anchorMax = new Vector2(1f, toFraction);
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
        }
    }
}
