// The primitives every screen in the game is drawn from. Nothing in this project ships a
// prefab or a UI atlas — the whole interface is constructed in code against procedural
// sprites from IconFactory — so these are the closest thing the game has to a design system.
//
// GameHud predates this file and keeps its own private wrappers with identical signatures;
// they forward here rather than duplicating the bodies, which is why adding a control style
// only ever has to happen once.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SliceBlast.UI
{
    /// <summary>
    /// A menu control: the tap target, the glyph and the word as three separate objects, so
    /// the label is not at the mercy of the button's own Selectable colour transition.
    /// </summary>
    public sealed class MenuControl
    {
        public RectTransform Root;
        public Button Button;
        public Image Icon;
        public Text Label;
    }

    public static class UiKit
    {
        public static readonly Color Gold = new Color(1f, 0.79f, 0.29f);
        public static readonly Color Mint = new Color(0.36f, 0.91f, 0.77f);
        public static readonly Color Ink = new Color(0.04f, 0.05f, 0.09f);
        public static readonly Color Panel = new Color(0.16f, 0.18f, 0.3f);
        public static readonly Color Dim = new Color(1f, 1f, 1f, 0.45f);

        public static RectTransform CreateChild(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            RectTransform rect = CreateChild(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A rounded panel, drawn from the nine-sliced Panel sprite.</summary>
        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            Image image = CreateImage(name, parent, color);
            image.sprite = IconFactory.GetSprite(IconShape.Panel);
            image.type = Image.Type.Sliced;
            return image;
        }

        public static Text CreateText(
            Font font,
            string name,
            Transform parent,
            int size,
            FontStyle style,
            Color color,
            TextAnchor anchor,
            bool outlined = false)
        {
            RectTransform rect = CreateChild(name, parent);

            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            // A word sitting on a coloured panel needs a rim, not a drop shadow, to survive
            // whatever the panel behind it happens to be.
            if (outlined)
            {
                Outline outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
                outline.effectDistance = new Vector2(2.5f, -2.5f);
            }
            else
            {
                Shadow shadow = rect.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
                shadow.effectDistance = new Vector2(3f, -3f);
            }

            return text;
        }

        public static MenuControl CreateButton(
            Font font,
            string name,
            Transform parent,
            string label,
            int fontSize,
            Color background,
            Color foreground,
            IconShape icon)
        {
            MenuControl control = new MenuControl();

            RectTransform root = CreateChild(name, parent);
            control.Root = root;

            // The tap target: a rounded panel with nothing underneath it to tint.
            RectTransform hit = CreateChild("Hit", root);
            Stretch(hit);

            Image image = hit.gameObject.AddComponent<Image>();
            image.sprite = IconFactory.GetSprite(IconShape.Panel);
            image.type = Image.Type.Sliced;
            image.color = background;
            image.raycastTarget = true;

            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.74f, 0.74f, 0.74f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            control.Button = button;

            bool hasLabel = !string.IsNullOrEmpty(label);

            if (icon != IconShape.None)
            {
                Image glyph = CreateImage("Icon", root, foreground);
                glyph.sprite = IconFactory.GetSprite(icon);
                glyph.preserveAspect = true;

                RectTransform glyphRect = glyph.rectTransform;
                glyphRect.anchorMin = new Vector2(hasLabel ? 0f : 0.5f, 0.5f);
                glyphRect.anchorMax = glyphRect.anchorMin;
                glyphRect.pivot = new Vector2(0.5f, 0.5f);
                glyphRect.sizeDelta = hasLabel ? new Vector2(74f, 74f) : new Vector2(62f, 62f);
                glyphRect.anchoredPosition = hasLabel ? new Vector2(92f, 0f) : Vector2.zero;

                control.Icon = glyph;
            }

            if (hasLabel)
            {
                Text text = CreateText(font, name + "Label", root, fontSize, FontStyle.Bold, foreground, TextAnchor.MiddleCenter, true);
                Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(160f, 0f), new Vector2(-40f, 0f));
                text.text = label;
                control.Label = text;
            }

            // Leaving a control selected keeps it highlighted for the rest of the run.
            button.onClick.AddListener(Deselect);

            return control;
        }

        public static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
            {
                return;
            }

            Color c = graphic.color;
            c.a = alpha;
            graphic.color = c;
        }

        public static void Deselect()
        {
            EventSystem events = EventSystem.current;

            if (events != null)
            {
                events.SetSelectedGameObject(null);
            }
        }

        /// <summary>
        /// Unity's built-in font, under whichever name the running version knows it by, with
        /// an OS font as the last resort. A null font renders every label as nothing at all,
        /// so this never returns without having tried all three.
        /// </summary>
        public static Font ResolveFont()
        {
            Font font = null;

            try
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (Exception)
            {
                font = null;
            }

            if (font == null)
            {
                try
                {
                    font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch (Exception)
                {
                    font = null;
                }
            }

            if (font == null)
            {
                string[] installed = Font.GetOSInstalledFontNames();

                if (installed != null && installed.Length > 0)
                {
                    font = Font.CreateDynamicFontFromOSFont(installed[0], 48);
                }
            }

            return font;
        }
    }
}
