using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using ViitorCloud.KmaxDisplay;
using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The look of the whole interface in one place: the glass palette, the typography and the scale. The topic interface and the
    /// launcher are built from it, so the two cannot drift apart.
    ///
    /// <para>The look is a dark slate glass panel (a translucent #0F172A) with a one-pixel slate outline, neon cyan for what
    /// is chosen or lit, white for headings and a pale slate for body text. The interface is built at half the size it was first
    /// designed at, small and kept to the corners so it does not crowd the model in the middle of the screen; every size in the
    /// builders is the first design's size times <see cref="Scale"/>.</para>
    /// </summary>
    public static class AnatomyUiStyle {
        /// <summary>The size of the interface relative to the first design. Change it here and rebuild the scene to resize all of it.</summary>
        public const float Scale = 0.5f;

        /// <summary>Translucent dark slate, #0F172A at 82%: the backing of every panel and button.</summary>
        public static readonly Color Glass = new Color32(15, 23, 42, 209);

        /// <summary>The slate outline of a panel, #334155 at 60%.</summary>
        public static readonly Color GlassOutline = new Color32(51, 65, 85, 153);

        /// <summary>A button under the pointer: the glass warmed with cyan.</summary>
        public static readonly Color GlassHover = new Color32(14, 63, 78, 235);

        /// <summary>The glass of what is chosen: the selected card, the main action.</summary>
        public static readonly Color GlassChosen = new Color32(8, 86, 104, 240);

        /// <summary>Neon cyan, #06B6D4: what is chosen or lit.</summary>
        public static readonly Color Accent = new Color32(6, 182, 212, 255);

        /// <summary>The brighter cyan, #22D3EE, for text and thin lines on glass.</summary>
        public static readonly Color AccentBright = new Color32(34, 211, 238, 255);

        /// <summary>Crisp white, #FFFFFF, for headings.</summary>
        public static readonly Color Heading = Color.white;

        /// <summary>Pale slate, #E2E8F0, for body text.</summary>
        public static readonly Color Body = new Color32(226, 232, 240, 255);

        /// <summary>Muted slate, #94A3B8, for secondary text on glass.</summary>
        public static readonly Color Muted = new Color32(148, 163, 184, 255);

        /// <summary>Warm amber for a fact set apart from the body.</summary>
        public static readonly Color Fact = new Color32(252, 211, 77, 255);

        /// <summary>
        /// Draws an image as a glass panel: the rounded fill in <paramref name="fill"/>, nine-sliced, and over it, as a child, the
        /// one-unit outline. The panel keeps the image's own raycast setting; the outline never takes a press.
        /// </summary>
        public static void ApplyGlass(Image image, Color fill) {
            image.sprite = AnatomyGlassSprites.EnsureFill();
            image.type = Image.Type.Sliced;
            image.color = fill;

            Transform existing = image.transform.Find("Outline");
            GameObject host = existing != null ? existing.gameObject : new GameObject("Outline", typeof(RectTransform), typeof(Image));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(image.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.SetAsFirstSibling();

            Image outline = host.GetComponent<Image>();
            outline.sprite = AnatomyGlassSprites.EnsureBorder();
            outline.type = Image.Type.Sliced;
            outline.color = GlassOutline;
            outline.raycastTarget = false;
        }

        /// <summary>
        /// Gives a button the feel every button has: the spring motion, tinting towards <paramref name="hoverTint"/> under the pointer
        /// and flashing cyan when pressed, and a tick on hover and a drop on press from the audio director. The two components
        /// stay on the button itself, which is the object the event system delivers the press to.
        /// </summary>
        public static UiButtonMotion AddButtonFeel(GameObject host, Image tintTarget, Color hoverTint, bool idlePulse) {
            UiButtonMotion motion = host.AddComponent<UiButtonMotion>();
            SerializedObject motionObject = new SerializedObject(motion);
            KmaxRigBuilder.SetReference(motionObject, "tintTarget", tintTarget);
            KmaxRigBuilder.SetColor(motionObject, "hoverTint", hoverTint);
            KmaxRigBuilder.SetColor(motionObject, "pressFlashTint", AccentBright);
            KmaxRigBuilder.SetBool(motionObject, "idlePulse", idlePulse);
            motionObject.ApplyModifiedPropertiesWithoutUndo();
            host.AddComponent<UiButtonSound>();
            return motion;
        }

        /// <summary>A text object in the interface's type: a TextMeshPro label that never takes a press and wraps by default.</summary>
        public static TextMeshProUGUI CreateText(RectTransform parent, string objectName, float fontSize, Color color,
            FontStyles style, TextAlignmentOptions alignment) {
            GameObject host = new GameObject(objectName, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            TextMeshProUGUI text = host.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Sets a rectangle's anchors, pivot, position and size in one call.</summary>
        public static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta) {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }
}