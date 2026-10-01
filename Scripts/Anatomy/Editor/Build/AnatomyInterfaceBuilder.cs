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
    /// Builds the world-space interface: the launcher, and, in a container of their own, the topic's title, caption bar, buttons
    /// and numbered badges. It is built in code so it can be rebuilt from nothing, and everything is anchored or laid out by
    /// layout groups, so it holds at any window aspect and a hidden button leaves no gap.
    ///
    /// <para>The canvas is pinned to the screen plane by the SDK's <c>UIScaler</c>, where parallax is zero and
    /// text is sharpest, and drawn over the model by <see cref="UiAlwaysOnTop"/>. It has a high sorting order so the
    /// pen's ray meets a button before it meets a model's collider that happens to float in front of it. Sizes are in canvas
    /// units of a 1920 x 1080 reference; on the 27" window one unit is about 0.31 mm. Everything is drawn at
    /// <see cref="AnatomyUiStyle.Scale"/> of the size it was first designed at, as dark glass panels pinned to the corners, so the
    /// interface stays out of the way of the model in the middle.</para>
    ///
    /// <para>The topic's interface is one container that the kiosk shell switches off while the launcher is up. In it the caption
    /// bar is a short card low in the middle so the model, centred on the screen, clears it; the title is at the top left; the
    /// buttons are one column down the right, led by the navigation pill; and the layer buttons are at the left.</para>
    ///
    /// <para>The badges and their lines are a fixed pool, built here and shown as the topic needs them. The
    /// lines are 3D objects at the scene root, not part of the canvas, so they can reach into the model's depth.</para>
    ///
    /// <para>The screen fader, a black panel, is the last thing on the canvas so it covers everything.</para>
    /// </summary>
    public static class AnatomyInterfaceBuilder {
        private const string InterfaceName = "Interface";
        private const string MarkerLinesName = "MarkerLines";
        private const string TopicInterfaceName = "TopicInterface";
        private const float S = AnatomyUiStyle.Scale;
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const int SortingOrder = 100;
        private const float Margin = 64f * S;
        private const float ButtonHeight = 92f * S;
        private const float ControlsWidth = 440f * S;
        private const float ControlsHeight = 560f * S;
        private const float ControlsTop = 64f * S;
        private const float ControlsSpacing = 18f * S;
        private const float RowSpacing = 16f * S;
        private const float ZoomButtonWidth = 110f * S;
        private const float ButtonFontSize = 38f * S;
        private const float PillFontSize = 34f * S;
        private const float SymbolFontSize = 60f * S;
        private const float TitleFontSize = 72f * S;
        private const float SubtitleFontSize = 36f * S;
        private const float HeaderTop = 44f * S;
        private const float CaptionWidth = 1040f;
        private const float CaptionHeight = 124f;
        private const float CaptionBottom = 24f;
        private const float StepRowWidth = 340f;
        private const float StepRowBottomGap = 12f;
        private const float CaptionMainWidth = 640f;
        private const float CaptionFactWidth = 340f;
        private const float CaptionMargin = 20f;
        private const float CaptionHeadingSize = 24f;
        private const float CaptionBodySize = 18f;
        private const float CaptionFactSize = 15f;
        private const float CaptionStepSize = 15f;
        public const int MarkerCount = 16;
        private const float MarkerSlotSize = 56f;
        private const float MarkerDiscSize = 32f;
        private const float MarkerRingSize = 36f;
        private const float MarkerFontSize = 18f;
        private const float MarkerSpacing = 52f;
        public const int LayerChipCount = 6;
        private const float LayerPanelWidth = 300f * S;
        private const float LayerPanelTop = 290f * S;
        private const float LayerHeadingHeight = 44f * S;
        private const float LayerChipHeight = 80f * S;
        private const float LayerChipSpacing = 14f * S;
        private const float LayerChipFontSize = 34f * S;
        private const float LayerHeadingFontSize = 30f * S;
        private const float LayerSwatchSize = 30f * S;
        public const int LinkButtonCount = 2;
        private const float LinkFontSize = 34f * S;

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
            canvas.sortingOrder = SortingOrder;
            root.AddComponent<KmaxUIRaycaster>();
            root.AddComponent<UIScaler>();
            root.AddComponent<UiAlwaysOnTop>();

            // UIScaler keeps the size and pose in step at runtime; this makes the scene view match.
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
            rootRect.localScale = Vector3.one * (windowSize.x / ReferenceWidth);

            // The launcher first, so the topic's interface is drawn and hit-tested over it if both were ever up.
            AnatomyLauncher launcher = AnatomyLauncherBuilder.BuildInterface(rootRect);

            GameObject topicInterface = new GameObject(TopicInterfaceName, typeof(RectTransform));
            RectTransform topicRect = topicInterface.GetComponent<RectTransform>();
            topicRect.SetParent(rootRect, false);
            AnatomyUiStyle.Anchor(topicRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // Built first, so it is drawn and hit-tested behind the title, the caption and the buttons.
            AnatomyMarkers markers = BuildMarkers(topicRect);

            TMP_Text titleText;
            TMP_Text subtitleText;
            BuildHeader(topicRect, out titleText, out subtitleText);

            CanvasGroup captionGroup;
            TMP_Text headingText;
            TMP_Text bodyText;
            TMP_Text factText;
            TMP_Text stepText;
            BuildCaption(topicRect, out captionGroup, out headingText, out bodyText, out factText, out stepText);

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

            AnatomyControls controls = BuildControls(topicRect);
            AnatomyLayerPanel layers = BuildLayerPanel(topicRect);

            // Last, so it is drawn over everything else.
            ScreenFader fader = BuildFader(rootRect);
            return new AnatomyInterfaceParts(panel, controls, markers, layers, fader, topicInterface, launcher);
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
            AnatomyUiStyle.Anchor(headerRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -HeaderTop), new Vector2(700f, 80f));

            titleText = AnatomyUiStyle.CreateText(headerRect, "Title", TitleFontSize, AnatomyUiStyle.Heading, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            AnatomyUiStyle.Anchor(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                Vector2.zero, new Vector2(0f, 46f));

            subtitleText = AnatomyUiStyle.CreateText(headerRect, "Subtitle", SubtitleFontSize, AnatomyUiStyle.AccentBright, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            AnatomyUiStyle.Anchor(subtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -46f), new Vector2(0f, 28f));
        }

        /// <summary>
        /// A short card low in the middle. The heading and body stack on the left and are centred vertically, so a one-line
        /// caption does not sit at the top of an empty box; the extra fact takes the right-hand column.
        /// </summary>
        private static void BuildCaption(RectTransform parent, out CanvasGroup group, out TMP_Text headingText,
            out TMP_Text bodyText, out TMP_Text factText, out TMP_Text stepText) {
            GameObject bar = new GameObject("Caption", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.SetParent(parent, false);
            AnatomyUiStyle.Anchor(barRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, CaptionBottom), new Vector2(CaptionWidth, CaptionHeight));

            Image background = bar.GetComponent<Image>();
            background.raycastTarget = false;
            AnatomyUiStyle.ApplyGlass(background, AnatomyUiStyle.Glass);
            group = bar.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            GameObject main = new GameObject("Main", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform mainRect = main.GetComponent<RectTransform>();
            mainRect.SetParent(barRect, false);
            AnatomyUiStyle.Anchor(mainRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(CaptionMainWidth, 0f));

            VerticalLayoutGroup layout = main.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)CaptionMargin, 0, 10, 10);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            headingText = AnatomyUiStyle.CreateText(mainRect, "Heading", CaptionHeadingSize, AnatomyUiStyle.AccentBright, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            bodyText = AnatomyUiStyle.CreateText(mainRect, "Body", CaptionBodySize, AnatomyUiStyle.Body, FontStyles.Normal, TextAlignmentOptions.TopLeft);

            factText = AnatomyUiStyle.CreateText(barRect, "Fact", CaptionFactSize, AnatomyUiStyle.Fact, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            AnatomyUiStyle.Anchor(factText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-CaptionMargin, 0f), new Vector2(CaptionFactWidth, -20f));

            stepText = AnatomyUiStyle.CreateText(barRect, "Step", CaptionStepSize, AnatomyUiStyle.Muted, FontStyles.Normal, TextAlignmentOptions.TopRight);
            AnatomyUiStyle.Anchor(stepText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-CaptionMargin, -8f), new Vector2(260f, 24f));
        }

        private static AnatomyMarkers BuildMarkers(RectTransform parent) {
            GameObject layerObject = new GameObject("Markers", typeof(RectTransform));
            RectTransform layer = layerObject.GetComponent<RectTransform>();
            layer.SetParent(parent, false);
            AnatomyUiStyle.Anchor(layer, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

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
            KmaxRigBuilder.SetFloat(markersObject, "spacing", MarkerSpacing);
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
            AnatomyUiStyle.Anchor(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(MarkerSlotSize, MarkerSlotSize));

            GameObject host = new GameObject("Marker" + number, typeof(RectTransform), typeof(Image));
            RectTransform hostRect = host.GetComponent<RectTransform>();
            hostRect.SetParent(slot, false);
            AnatomyUiStyle.Anchor(hostRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image hitArea = host.GetComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);
            hitArea.raycastTarget = true;

            Image disc = CreateImage(hostRect, "Disc", discSprite, MarkerDiscSize);
            Image ring = CreateImage(hostRect, "Ring", ringSprite, MarkerRingSize);
            TextMeshProUGUI numberText = AnatomyUiStyle.CreateText(hostRect, "Number", MarkerFontSize, AnatomyUiStyle.Heading, FontStyles.Bold, TextAlignmentOptions.Center);
            AnatomyUiStyle.Anchor(numberText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            numberText.raycastTarget = false;

            AnatomyMarker marker = host.AddComponent<AnatomyMarker>();
            SerializedObject markerObject = new SerializedObject(marker);
            KmaxRigBuilder.SetReference(markerObject, "slot", slot);
            KmaxRigBuilder.SetReference(markerObject, "disc", disc);
            KmaxRigBuilder.SetReference(markerObject, "ring", ring);
            KmaxRigBuilder.SetReference(markerObject, "numberText", numberText);
            markerObject.ApplyModifiedPropertiesWithoutUndo();

            // A badge swells more than a button does: it is a small target that has to say it has been found.
            UiButtonMotion motion = host.AddComponent<UiButtonMotion>();
            SerializedObject motionObject = new SerializedObject(motion);
            KmaxRigBuilder.SetFloat(motionObject, "hoverScale", 1.18f);
            KmaxRigBuilder.SetBool(motionObject, "pressFlash", false);
            motionObject.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }

        private static Image CreateImage(RectTransform parent, string objectName, Sprite sprite, float size) {
            GameObject host = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            AnatomyUiStyle.Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
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
            line.positionCount = 25;
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

        /// <summary>
        /// The column of buttons down the right edge, stacked from the top by one layout so a button that is not shown leaves no
        /// gap: the navigation pill (Menu and Next side by side), the links a topic offers, Explore, the tour, the exploded view,
        /// Previous and Next, the zoom and Reset.
        /// </summary>
        private static AnatomyControls BuildControls(RectTransform parent) {
            GameObject column = new GameObject("Controls", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform columnRect = column.GetComponent<RectTransform>();
            columnRect.SetParent(parent, false);
            AnatomyUiStyle.Anchor(columnRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-Margin, -ControlsTop), new Vector2(ControlsWidth, ControlsHeight));

            VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
            layout.spacing = ControlsSpacing;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject exploreSlot;
            GameObject tourSlot;
            GameObject explodeSlot;
            GameObject previousSlot;
            GameObject nextSlot;
            GameObject zoomOutSlot;
            GameObject zoomInSlot;
            GameObject resetSlot;
            GameObject homeSlot;
            GameObject nextExhibitSlot;
            TMP_Text exploreLabel;
            TMP_Text tourLabel;
            TMP_Text explodeLabel;
            TMP_Text previousLabel;
            TMP_Text nextLabel;
            TMP_Text zoomOutLabel;
            TMP_Text zoomInLabel;
            TMP_Text resetLabel;
            TMP_Text homeLabel;
            TMP_Text nextExhibitLabel;

            // The pill takes the top of the column, and its row holds both halves. Next exhibit is the wider half, because its words
            // are longer, and its name sets it apart from the Next that steps through a topic's structures.
            RectTransform pillRow = CreateRow(columnRect, "NavigationPill", true);
            Button home = CreateButton(pillRow, "Home", "Menu", PillFontSize, 0f, out homeSlot, out homeLabel);
            Button nextExhibit = CreateButton(pillRow, "NextExhibit", "Next exhibit", PillFontSize, 0f, out nextExhibitSlot, out nextExhibitLabel);
            homeSlot.GetComponent<LayoutElement>().flexibleWidth = 1f;
            nextExhibitSlot.GetComponent<LayoutElement>().flexibleWidth = 1.7f;

            // A topic that leads to others offers them here, ahead of the topic's own buttons; they are never all shown at once.
            GameObject[] linkSlots = new GameObject[LinkButtonCount];
            Button[] linkButtons = new Button[LinkButtonCount];
            TMP_Text[] linkLabels = new TMP_Text[LinkButtonCount];
            for (int i = 0; i < LinkButtonCount; i++) {
                linkButtons[i] = CreateButton(columnRect, "Link" + (i + 1), "Link", LinkFontSize, 0f, out linkSlots[i], out linkLabels[i]);
            }

            Button explore = CreateButton(columnRect, "Explore", "Explore", ButtonFontSize, 0f, out exploreSlot, out exploreLabel);
            AnatomyUiStyle.ApplyGlass(explore.GetComponent<Image>(), AnatomyUiStyle.GlassChosen);
            Button tour = CreateButton(columnRect, "Tour", "Start tour", ButtonFontSize, 0f, out tourSlot, out tourLabel);
            Button explode = CreateButton(columnRect, "Explode", "Explode", ButtonFontSize, 0f, out explodeSlot, out explodeLabel);

            // Previous and Next sit centered low on the screen, directly above the caption bar, so stepping through
            // structures is grouped with the caption readout.
            RectTransform stepRow = CreateRow(parent, "StepRow", true);
            AnatomyUiStyle.Anchor(stepRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, CaptionBottom + CaptionHeight + StepRowBottomGap), new Vector2(StepRowWidth, ButtonHeight));
            Button previous = CreateButton(stepRow, "Previous", "Previous", ButtonFontSize, 0f, out previousSlot, out previousLabel);
            Button next = CreateButton(stepRow, "Next", "Next", ButtonFontSize, 0f, out nextSlot, out nextLabel);
            previousSlot.GetComponent<LayoutElement>().flexibleWidth = 1f;
            nextSlot.GetComponent<LayoutElement>().flexibleWidth = 1f;

            // Zoom is driven directly via the stylus (push/pull dolly) and mouse wheel, so the on-screen scale buttons are hidden.
            RectTransform zoomRow = CreateRow(columnRect, "ZoomRow", false);
            Button zoomOut = CreateButton(zoomRow, "ZoomOut", "-", SymbolFontSize, ZoomButtonWidth, out zoomOutSlot, out zoomOutLabel);
            TextMeshProUGUI zoomText = AnatomyUiStyle.CreateText(zoomRow, "ZoomReadout", ButtonFontSize, AnatomyUiStyle.AccentBright, FontStyles.Bold, TextAlignmentOptions.Center);
            zoomText.text = "1.0x";
            zoomText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Button zoomIn = CreateButton(zoomRow, "ZoomIn", "+", SymbolFontSize, ZoomButtonWidth, out zoomInSlot, out zoomInLabel);
            zoomRow.gameObject.SetActive(false);

            Button reset = CreateButton(columnRect, "Reset", "Reset view", ButtonFontSize, 0f, out resetSlot, out resetLabel);

            AnatomyControls controls = column.AddComponent<AnatomyControls>();
            SerializedObject controlsObject = new SerializedObject(controls);
            KmaxRigBuilder.SetReference(controlsObject, "exploreSlot", exploreSlot);
            KmaxRigBuilder.SetReference(controlsObject, "exploreButton", explore);
            KmaxRigBuilder.SetReference(controlsObject, "homeSlot", pillRow.gameObject);
            KmaxRigBuilder.SetReference(controlsObject, "homeButton", home);
            KmaxRigBuilder.SetReference(controlsObject, "nextExhibitButton", nextExhibit);
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
            AnatomyUiStyle.Anchor(panelRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 1f),
                new Vector2(Margin, LayerPanelTop), new Vector2(LayerPanelWidth, panelHeight));

            GameObject contentObject = new GameObject("Chips", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.SetParent(panelRect, false);
            AnatomyUiStyle.Anchor(content, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = LayerChipSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI heading = AnatomyUiStyle.CreateText(content, "Heading", LayerHeadingFontSize, AnatomyUiStyle.Muted, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
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
            AnatomyUiStyle.Anchor(hostRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image background = host.GetComponent<Image>();
            AnatomyUiStyle.ApplyGlass(background, AnatomyUiStyle.Glass);
            Button button = host.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            AnatomyUiStyle.AddButtonFeel(host, background, AnatomyUiStyle.GlassHover, false);

            Image swatch = CreateImage(hostRect, "Swatch", discSprite, LayerSwatchSize);
            AnatomyUiStyle.Anchor(swatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(12f, 0f), new Vector2(LayerSwatchSize, LayerSwatchSize));

            TextMeshProUGUI label = AnatomyUiStyle.CreateText(hostRect, "Label", LayerChipFontSize, AnatomyUiStyle.Heading, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            AnatomyUiStyle.Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(16.5f, 0f), new Vector2(-43f, 0f));

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
            AnatomyUiStyle.Anchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

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

            // A row that forced its height to expand would take any room the column has to spare, and the pill would swell when a
            // topic shows only a few buttons. Each button is already as tall as the row.
            layout.childForceExpandHeight = false;
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
            AnatomyUiStyle.Anchor(rect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image image = host.GetComponent<Image>();
            AnatomyUiStyle.ApplyGlass(image, AnatomyUiStyle.Glass);
            Button button = host.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            AnatomyUiStyle.AddButtonFeel(host, image, AnatomyUiStyle.GlassHover, false);

            labelText = AnatomyUiStyle.CreateText(rect, "Label", fontSize, AnatomyUiStyle.Heading, FontStyles.Bold, TextAlignmentOptions.Center);
            AnatomyUiStyle.Anchor(labelText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            labelText.text = label;
            return button;
        }
    }
}