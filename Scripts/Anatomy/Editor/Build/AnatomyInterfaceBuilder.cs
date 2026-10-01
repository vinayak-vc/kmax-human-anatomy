using KmaxXR;

using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

using ViitorCloud.KmaxDisplay;
using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Builds the world-space interface: the title, the caption bar, the buttons and the numbered badges. It is
    /// built in code so it can be rebuilt from nothing, and everything is anchored or laid out by layout groups, so
    /// it holds at any window aspect and a hidden button leaves no gap.
    ///
    /// <para>The canvas is pinned to the screen plane by the SDK's <c>UIScaler</c>, where parallax is zero and
    /// text is sharpest, and drawn over the model by <see cref="UiAlwaysOnTop"/>. Sizes are in canvas units
    /// of a 1920 x 1080 reference; on the 27" window one unit is about 0.31 mm.</para>
    ///
    /// <para>The caption bar is kept short and low so the model, centred on the screen, clears it. Content
    /// that would crowd the bar goes in the right-hand column beside the caption, not below it.</para>
    ///
    /// <para>The badges and their lines are a fixed pool, built here and shown as the topic needs them. The
    /// lines are 3D objects at the scene root, not part of the canvas, so they can reach into the model's depth.</para>
    ///
    /// <para>The layer buttons of the body map are a fixed pool too, down the left edge. The Body map button stands alone
    /// above the column of controls, and the screen fader, a black panel, is the last thing on the canvas so it covers
    /// everything.</para>
    /// </summary>
    public static class AnatomyInterfaceBuilder {
        private const string InterfaceName = "Interface";
        private const string MarkerLinesName = "MarkerLines";
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float ButtonHeight = 92f;
        private const float ControlsWidth = 400f;
        private const float ControlsHeight = 560f;
        private const float ControlsTop = 260f;
        private const float ControlsSpacing = 18f;
        private const float RowSpacing = 16f;
        private const float ZoomButtonWidth = 110f;
        private const float ButtonFontSize = 38f;
        private const float SymbolFontSize = 60f;
        private const float CaptionHeight = 200f;
        private const float CaptionMainWidth = 1100f;
        private const float CaptionFactWidth = 600f;
        private const float CaptionMargin = 40f;
        public const int MarkerCount = 16;
        private const float MarkerSlotSize = 84f;
        private const float MarkerDiscSize = 64f;
        private const float MarkerRingSize = 72f;
        private const float MarkerFontSize = 36f;
        public const int LayerChipCount = 6;
        private const float LayerPanelWidth = 300f;
        private const float LayerPanelTop = 290f;
        private const float LayerHeadingHeight = 44f;
        private const float LayerChipHeight = 80f;
        private const float LayerChipSpacing = 14f;
        private const float LayerChipFontSize = 34f;
        private const float LayerSwatchSize = 30f;
        private const float HomeButtonTop = 64f;
        public const int LinkButtonCount = 2;
        private const float LinkButtonTop = 44f;
        private const float LinkButtonSpacing = 14f;
        private const float LinkFontSize = 34f;

        private static readonly Color PanelColor = new Color(0.03f, 0.05f, 0.09f, 0.78f);
        private static readonly Color ButtonColor = new Color(0.12f, 0.24f, 0.36f, 0.92f);
        private static readonly Color ExploreButtonColor = new Color(0.08f, 0.42f, 0.5f, 0.96f);
        private static readonly Color TitleColor = new Color(0.96f, 0.98f, 1f, 1f);
        private static readonly Color SubtitleColor = new Color(0.62f, 0.76f, 0.9f, 1f);
        private static readonly Color HeadingColor = new Color(0.55f, 0.9f, 1f, 1f);
        private static readonly Color BodyColor = new Color(0.94f, 0.96f, 1f, 1f);
        private static readonly Color FactColor = new Color(1f, 0.85f, 0.55f, 1f);

        /// <summary>Rebuilds the interface from scratch and returns what needs wiring.</summary>
        /// <param name="eventCamera">The rig's centre camera, which the Kmax raycasters measure against.</param>
        /// <param name="windowSize">The virtual screen's width and height in metres.</param>
        public static AnatomyInterfaceParts Build(Camera eventCamera, Vector2 windowSize) {
            DestroyExisting(InterfaceName);
            DestroyExisting(MarkerLinesName);

            GameObject root = new GameObject(InterfaceName, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = eventCamera;
            root.AddComponent<KmaxUIRaycaster>();
            root.AddComponent<UIScaler>();
            root.AddComponent<UiAlwaysOnTop>();

            // UIScaler keeps the size and pose in step at runtime; this makes the scene view match.
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
            rootRect.localScale = Vector3.one * (windowSize.x / ReferenceWidth);

            // Built first, so it is drawn and hit-tested behind the title, the caption and the buttons.
            AnatomyMarkers markers = BuildMarkers(rootRect);

            TMP_Text titleText;
            TMP_Text subtitleText;
            BuildHeader(rootRect, out titleText, out subtitleText);

            CanvasGroup captionGroup;
            TMP_Text headingText;
            TMP_Text bodyText;
            TMP_Text factText;
            TMP_Text stepText;
            BuildCaption(rootRect, out captionGroup, out headingText, out bodyText, out factText, out stepText);

            AnatomyInfoPanel panel = root.AddComponent<AnatomyInfoPanel>();
            SerializedObject panelObject = new SerializedObject(panel);
            KmaxRigBuilder.SetReference(panelObject, "titleText", titleText);
            KmaxRigBuilder.SetReference(panelObject, "subtitleText", subtitleText);
            KmaxRigBuilder.SetReference(panelObject, "captionGroup", captionGroup);
            KmaxRigBuilder.SetReference(panelObject, "headingText", headingText);
            KmaxRigBuilder.SetReference(panelObject, "bodyText", bodyText);
            KmaxRigBuilder.SetReference(panelObject, "factText", factText);
            KmaxRigBuilder.SetReference(panelObject, "stepText", stepText);
            panelObject.ApplyModifiedPropertiesWithoutUndo();

            AnatomyControls controls = BuildControls(rootRect);
            AnatomyLayerPanel layers = BuildLayerPanel(rootRect);

            // Last, so it is drawn over everything else.
            ScreenFader fader = BuildFader(rootRect);
            return new AnatomyInterfaceParts(panel, controls, markers, layers, fader);
        }

        private static void DestroyExisting(string objectName) {
            GameObject existing = GameObject.Find(objectName);
            if (existing != null) {
                Object.DestroyImmediate(existing);
            }
        }

        private static void BuildHeader(RectTransform parent, out TMP_Text titleText, out TMP_Text subtitleText) {
            GameObject header = new GameObject("Header", typeof(RectTransform));
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.SetParent(parent, false);
            Anchor(headerRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(64f, -44f), new Vector2(1300f, 150f));

            titleText = CreateText(headerRect, "Title", 72f, TitleColor, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Anchor(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(0f, 90f));

            subtitleText = CreateText(headerRect, "Subtitle", 36f, SubtitleColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(subtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -92f), new Vector2(0f, 50f));
        }

        /// <summary>
        /// A short bar along the bottom. The heading and body stack on the left and are centred vertically, so a
        /// one-line caption does not sit at the top of an empty box; the extra fact takes the right-hand column.
        /// </summary>
        private static void BuildCaption(RectTransform parent, out CanvasGroup group, out TMP_Text headingText,
            out TMP_Text bodyText, out TMP_Text factText, out TMP_Text stepText) {
            GameObject bar = new GameObject("Caption", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.SetParent(parent, false);
            Anchor(barRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 48f), new Vector2(-128f, CaptionHeight));

            Image background = bar.GetComponent<Image>();
            background.color = PanelColor;
            background.raycastTarget = false;
            group = bar.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            GameObject main = new GameObject("Main", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform mainRect = main.GetComponent<RectTransform>();
            mainRect.SetParent(barRect, false);
            Anchor(mainRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(CaptionMainWidth, 0f));

            VerticalLayoutGroup layout = main.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)CaptionMargin, 0, 16, 16);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            headingText = CreateText(mainRect, "Heading", 48f, HeadingColor, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            bodyText = CreateText(mainRect, "Body", 36f, BodyColor, FontStyles.Normal, TextAlignmentOptions.TopLeft);

            factText = CreateText(barRect, "Fact", 30f, FactColor, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            Anchor(factText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-CaptionMargin, 0f), new Vector2(CaptionFactWidth, -32f));

            stepText = CreateText(barRect, "Step", 30f, SubtitleColor, FontStyles.Normal, TextAlignmentOptions.TopRight);
            Anchor(stepText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-CaptionMargin, -16f), new Vector2(400f, 44f));
        }

        private static AnatomyMarkers BuildMarkers(RectTransform parent) {
            GameObject layerObject = new GameObject("Markers", typeof(RectTransform));
            RectTransform layer = layerObject.GetComponent<RectTransform>();
            layer.SetParent(parent, false);
            Anchor(layer, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Sprite disc = AnatomyMarkerSprites.EnsureDisc();
            Sprite ring = AnatomyMarkerSprites.EnsureRing();
            Material lineMaterial = AnatomyGlowMaterials.EnsureLine();
            GameObject lineRoot = new GameObject(MarkerLinesName);

            AnatomyMarker[] badges = new AnatomyMarker[MarkerCount];
            LineRenderer[] lines = new LineRenderer[MarkerCount];
            for (int i = 0; i < MarkerCount; i++) {
                badges[i] = CreateMarker(layer, i + 1, disc, ring);
                lines[i] = CreateLine(lineRoot.transform, i + 1, lineMaterial);
            }

            AnatomyMarkers markers = layerObject.AddComponent<AnatomyMarkers>();
            SerializedObject markersObject = new SerializedObject(markers);
            KmaxRigBuilder.SetReference(markersObject, "layer", layer);
            KmaxRigBuilder.SetReferences(markersObject, "badges", badges);
            KmaxRigBuilder.SetReferences(markersObject, "lines", lines);
            markersObject.ApplyModifiedPropertiesWithoutUndo();
            return markers;
        }

        /// <summary>
        /// One badge: a slot the layout moves, holding the badge itself, which never moves. The badge is a clear
        /// square as large as the slot, so it is easy to hit, with a disc, a ring and the number drawn inside it.
        /// </summary>
        private static AnatomyMarker CreateMarker(RectTransform parent, int number, Sprite discSprite, Sprite ringSprite) {
            GameObject slotObject = new GameObject("Marker" + number + "Slot", typeof(RectTransform));
            RectTransform slot = slotObject.GetComponent<RectTransform>();
            slot.SetParent(parent, false);
            Anchor(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(MarkerSlotSize, MarkerSlotSize));

            GameObject host = new GameObject("Marker" + number, typeof(RectTransform), typeof(Image));
            RectTransform hostRect = host.GetComponent<RectTransform>();
            hostRect.SetParent(slot, false);
            Anchor(hostRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image hitArea = host.GetComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);
            hitArea.raycastTarget = true;

            Image disc = CreateImage(hostRect, "Disc", discSprite, MarkerDiscSize);
            Image ring = CreateImage(hostRect, "Ring", ringSprite, MarkerRingSize);
            TextMeshProUGUI numberText = CreateText(hostRect, "Number", MarkerFontSize, TitleColor, FontStyles.Bold, TextAlignmentOptions.Center);
            Anchor(numberText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            AnatomyMarker marker = host.AddComponent<AnatomyMarker>();
            SerializedObject markerObject = new SerializedObject(marker);
            KmaxRigBuilder.SetReference(markerObject, "slot", slot);
            KmaxRigBuilder.SetReference(markerObject, "disc", disc);
            KmaxRigBuilder.SetReference(markerObject, "ring", ring);
            KmaxRigBuilder.SetReference(markerObject, "numberText", numberText);
            markerObject.ApplyModifiedPropertiesWithoutUndo();

            UiButtonMotion motion = host.AddComponent<UiButtonMotion>();
            SerializedObject motionObject = new SerializedObject(motion);
            KmaxRigBuilder.SetFloat(motionObject, "hoverScale", 1.18f);
            KmaxRigBuilder.SetFloat(motionObject, "hoverLift", 0f);
            KmaxRigBuilder.SetBool(motionObject, "pressFlash", false);
            motionObject.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }

        private static Image CreateImage(RectTransform parent, string objectName, Sprite sprite, float size) {
            GameObject host = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            Image image = host.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A leader line: a thin billboard that narrows towards the badge and swells at the structure, like a pin.
        /// Its ends are set every frame, in world space, so it reaches into the model's depth.
        /// </summary>
        private static LineRenderer CreateLine(Transform parent, int number, Material material) {
            GameObject host = new GameObject("MarkerLine" + number);
            host.transform.SetParent(parent, false);
            LineRenderer line = host.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 4;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.8f));
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.enabled = false;
            return line;
        }

        private static AnatomyControls BuildControls(RectTransform parent) {
            GameObject column = new GameObject("Controls", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform columnRect = column.GetComponent<RectTransform>();
            columnRect.SetParent(parent, false);
            Anchor(columnRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-64f, -ControlsTop), new Vector2(ControlsWidth, ControlsHeight));

            VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = ControlsSpacing;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject exploreSlot;
            GameObject homeSlot;
            GameObject tourSlot;
            GameObject explodeSlot;
            GameObject previousSlot;
            GameObject nextSlot;
            GameObject zoomOutSlot;
            GameObject zoomInSlot;
            GameObject resetSlot;
            TMP_Text exploreLabel;
            TMP_Text homeLabel;
            TMP_Text tourLabel;
            TMP_Text explodeLabel;
            TMP_Text previousLabel;
            TMP_Text nextLabel;
            TMP_Text zoomOutLabel;
            TMP_Text zoomInLabel;
            TMP_Text resetLabel;

            Button explore = CreateButton(columnRect, "Explore", "Explore", ButtonFontSize, 0f, out exploreSlot, out exploreLabel);
            explore.GetComponent<Image>().color = ExploreButtonColor;
            Button tour = CreateButton(columnRect, "Tour", "Start tour", ButtonFontSize, 0f, out tourSlot, out tourLabel);
            Button explode = CreateButton(columnRect, "Explode", "Explode", ButtonFontSize, 0f, out explodeSlot, out explodeLabel);

            RectTransform stepRow = CreateRow(columnRect, "StepRow", true);
            Button previous = CreateButton(stepRow, "Previous", "Previous", ButtonFontSize, 0f, out previousSlot, out previousLabel);
            Button next = CreateButton(stepRow, "Next", "Next", ButtonFontSize, 0f, out nextSlot, out nextLabel);

            RectTransform zoomRow = CreateRow(columnRect, "ZoomRow", false);
            Button zoomOut = CreateButton(zoomRow, "ZoomOut", "-", SymbolFontSize, ZoomButtonWidth, out zoomOutSlot, out zoomOutLabel);
            TextMeshProUGUI zoomText = CreateText(zoomRow, "ZoomReadout", ButtonFontSize, SubtitleColor, FontStyles.Bold, TextAlignmentOptions.Center);
            zoomText.text = "1.0x";
            zoomText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Button zoomIn = CreateButton(zoomRow, "ZoomIn", "+", SymbolFontSize, ZoomButtonWidth, out zoomInSlot, out zoomInLabel);

            Button reset = CreateButton(columnRect, "Reset", "Reset view", ButtonFontSize, 0f, out resetSlot, out resetLabel);

            // Beside Body map, in the same corner, a body map offers the activities as links. They are never shown together.
            GameObject[] linkSlots = new GameObject[LinkButtonCount];
            Button[] linkButtons = new Button[LinkButtonCount];
            TMP_Text[] linkLabels = new TMP_Text[LinkButtonCount];
            for (int i = 0; i < LinkButtonCount; i++) {
                linkButtons[i] = CreateButton(parent, "Link" + (i + 1), "Link", LinkFontSize, 0f, out linkSlots[i], out linkLabels[i]);
                float top = LinkButtonTop + i * (ButtonHeight + LinkButtonSpacing);
                Anchor(linkSlots[i].GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(-64f, -top), new Vector2(ControlsWidth, ButtonHeight));
            }

            // Above the column, in the corner, where a visitor can always find the way back to the body map.
            Button home = CreateButton(parent, "Home", "Body map", ButtonFontSize, 0f, out homeSlot, out homeLabel);
            Anchor(homeSlot.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-64f, -HomeButtonTop), new Vector2(ControlsWidth, ButtonHeight));

            AnatomyControls controls = column.AddComponent<AnatomyControls>();
            SerializedObject controlsObject = new SerializedObject(controls);
            KmaxRigBuilder.SetReference(controlsObject, "exploreSlot", exploreSlot);
            KmaxRigBuilder.SetReference(controlsObject, "exploreButton", explore);
            KmaxRigBuilder.SetReference(controlsObject, "homeSlot", homeSlot);
            KmaxRigBuilder.SetReference(controlsObject, "homeButton", home);
            KmaxRigBuilder.SetReference(controlsObject, "tourSlot", tourSlot);
            KmaxRigBuilder.SetReference(controlsObject, "tourButton", tour);
            KmaxRigBuilder.SetReference(controlsObject, "tourLabel", tourLabel);
            KmaxRigBuilder.SetReference(controlsObject, "explodeSlot", explodeSlot);
            KmaxRigBuilder.SetReference(controlsObject, "explodeButton", explode);
            KmaxRigBuilder.SetReference(controlsObject, "explodeLabel", explodeLabel);
            KmaxRigBuilder.SetReference(controlsObject, "stepRow", stepRow.gameObject);
            KmaxRigBuilder.SetReference(controlsObject, "zoomRow", zoomRow.gameObject);
            KmaxRigBuilder.SetReference(controlsObject, "resetLabel", resetLabel);
            KmaxRigBuilder.SetReferences(controlsObject, "linkSlots", linkSlots);
            KmaxRigBuilder.SetReferences(controlsObject, "linkButtons", linkButtons);
            KmaxRigBuilder.SetReferences(controlsObject, "linkLabels", linkLabels);
            KmaxRigBuilder.SetReference(controlsObject, "previousButton", previous);
            KmaxRigBuilder.SetReference(controlsObject, "nextButton", next);
            KmaxRigBuilder.SetReference(controlsObject, "zoomOutButton", zoomOut);
            KmaxRigBuilder.SetReference(controlsObject, "zoomOutLabel", zoomOutLabel);
            KmaxRigBuilder.SetReference(controlsObject, "zoomInButton", zoomIn);
            KmaxRigBuilder.SetReference(controlsObject, "zoomInLabel", zoomInLabel);
            KmaxRigBuilder.SetReference(controlsObject, "zoomText", zoomText);
            KmaxRigBuilder.SetReference(controlsObject, "resetButton", reset);
            controlsObject.ApplyModifiedPropertiesWithoutUndo();
            return controls;
        }

        /// <summary>
        /// The layer buttons of the body map, down the left edge below the title: a heading and a fixed pool of buttons the
        /// layout stacks from the top. The pool is hidden until a topic with layers binds it.
        /// </summary>
        private static AnatomyLayerPanel BuildLayerPanel(RectTransform parent) {
            GameObject panelObject = new GameObject("LayerPanel", typeof(RectTransform));
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.SetParent(parent, false);
            float panelHeight = LayerHeadingHeight + LayerChipCount * (LayerChipHeight + LayerChipSpacing);
            Anchor(panelRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 1f),
                new Vector2(64f, LayerPanelTop), new Vector2(LayerPanelWidth, panelHeight));

            GameObject contentObject = new GameObject("Chips", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.SetParent(panelRect, false);
            Anchor(content, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = LayerChipSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI heading = CreateText(content, "Heading", 30f, SubtitleColor, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            heading.text = "Show";
            heading.gameObject.AddComponent<LayoutElement>().preferredHeight = LayerHeadingHeight;

            Sprite disc = AnatomyMarkerSprites.EnsureDisc();
            AnatomyLayerChip[] chips = new AnatomyLayerChip[LayerChipCount];
            for (int i = 0; i < LayerChipCount; i++) {
                chips[i] = CreateLayerChip(content, i + 1, disc);
            }

            AnatomyLayerPanel panel = panelObject.AddComponent<AnatomyLayerPanel>();
            SerializedObject panelSerialized = new SerializedObject(panel);
            KmaxRigBuilder.SetReference(panelSerialized, "content", contentObject);
            KmaxRigBuilder.SetReferences(panelSerialized, "chips", chips);
            panelSerialized.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        /// <summary>One layer button in a slot the layout moves: a colour swatch on the left and the layer's name.</summary>
        private static AnatomyLayerChip CreateLayerChip(RectTransform parent, int number, Sprite discSprite) {
            GameObject slotObject = new GameObject("LayerChip" + number + "Slot", typeof(RectTransform), typeof(LayoutElement));
            RectTransform slot = slotObject.GetComponent<RectTransform>();
            slot.SetParent(parent, false);
            slotObject.GetComponent<LayoutElement>().preferredHeight = LayerChipHeight;

            GameObject host = new GameObject("LayerChip" + number, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform hostRect = host.GetComponent<RectTransform>();
            hostRect.SetParent(slot, false);
            Anchor(hostRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image background = host.GetComponent<Image>();
            background.color = ButtonColor;
            Button button = host.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;

            UiButtonMotion motion = host.AddComponent<UiButtonMotion>();
            SerializedObject motionObject = new SerializedObject(motion);
            KmaxRigBuilder.SetReference(motionObject, "tintTarget", background);
            motionObject.ApplyModifiedPropertiesWithoutUndo();

            Image swatch = CreateImage(hostRect, "Swatch", discSprite, LayerSwatchSize);
            Anchor(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(24f, 0f), new Vector2(LayerSwatchSize, LayerSwatchSize));

            TextMeshProUGUI label = CreateText(hostRect, "Label", LayerChipFontSize, TitleColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(33f, 0f), new Vector2(-86f, 0f));

            AnatomyLayerChip chip = host.AddComponent<AnatomyLayerChip>();
            SerializedObject chipObject = new SerializedObject(chip);
            KmaxRigBuilder.SetReference(chipObject, "slot", slotObject);
            KmaxRigBuilder.SetReference(chipObject, "button", button);
            KmaxRigBuilder.SetReference(chipObject, "swatch", swatch);
            KmaxRigBuilder.SetReference(chipObject, "label", label);
            chipObject.ApplyModifiedPropertiesWithoutUndo();
            return chip;
        }

        /// <summary>
        /// The screen fader: a black panel over the whole canvas, clear in the scene so it does not hide the model while it
        /// is being built or edited. The kiosk shell covers the screen with it when the scene starts.
        /// </summary>
        private static ScreenFader BuildFader(RectTransform parent) {
            GameObject host = new GameObject("Fader", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Anchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image panel = host.GetComponent<Image>();
            panel.color = Color.black;
            CanvasGroup group = host.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            return host.AddComponent<ScreenFader>();
        }

        /// <summary>A horizontal strip of buttons that is one button high. With <paramref name="share"/> they split its width equally.</summary>
        private static RectTransform CreateRow(RectTransform parent, string objectName, bool share) {
            GameObject host = new GameObject(objectName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            host.GetComponent<LayoutElement>().preferredHeight = ButtonHeight;

            HorizontalLayoutGroup layout = host.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = RowSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = share;
            layout.childForceExpandHeight = true;
            return rect;
        }

        /// <summary>
        /// A button in a slot. The layout group positions the slot and never touches the button, because
        /// <see cref="UiButtonMotion"/> re-applies the position it captured at start every frame and would undo
        /// the layout. Motion and button stay on one object: the input module only sends a click when the
        /// object that took the press is the one that handles the click.
        /// </summary>
        /// <param name="width">The slot's width; zero leaves it to the layout.</param>
        private static Button CreateButton(RectTransform parent, string objectName, string label, float fontSize,
            float width, out GameObject slot, out TMP_Text labelText) {
            slot = new GameObject(objectName + "Slot", typeof(RectTransform), typeof(LayoutElement));
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.SetParent(parent, false);
            LayoutElement element = slot.GetComponent<LayoutElement>();
            element.preferredHeight = ButtonHeight;
            if (width > 0f) {
                element.preferredWidth = width;
                element.flexibleWidth = 0f;
            }

            GameObject host = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(slotRect, false);
            Anchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image image = host.GetComponent<Image>();
            image.color = ButtonColor;
            Button button = host.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            UiButtonMotion motion = host.AddComponent<UiButtonMotion>();
            SerializedObject motionObject = new SerializedObject(motion);
            KmaxRigBuilder.SetReference(motionObject, "tintTarget", image);
            motionObject.ApplyModifiedPropertiesWithoutUndo();

            labelText = CreateText(rect, "Label", fontSize, TitleColor, FontStyles.Bold, TextAlignmentOptions.Center);
            Anchor(labelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            labelText.text = label;
            return button;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string objectName, float fontSize, Color color,
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

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta) {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }
}