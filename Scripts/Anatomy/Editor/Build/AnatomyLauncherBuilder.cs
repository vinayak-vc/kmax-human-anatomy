using System.IO;

using TMPro;

using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

using ViitorCloud.KmaxDisplay;
using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Builds the launcher in two parts. The interface, built with the rest of the world-space interface, is the heading, a
    /// grid of cards and the Load button. The stage, built with the exhibit, is the turntable the chosen exhibit's model turns on
    /// and the dust that drifts behind it, which live in the scene's space and not on the canvas.
    ///
    /// <para>Sizes are canvas units of the 1920 x 1080 reference. The cards are laid out five to a row and then the rest, centred,
    /// so nine cards make a row of five over a row of four. The pool is fixed, like the badges and the layer buttons, and the
    /// launcher hides the cards it has no exhibit for.</para>
    /// </summary>
    public static class AnatomyLauncherBuilder {
        public const int CardCount = 9;

        private const string LauncherName = "Launcher";
        private const string StageName = "LauncherStage";
        private const string DustName = "LauncherDust";
        private const string DustMaterialPath = KmaxRigBuilder.ModuleRoot + "/Content/Materials/LauncherDust.mat";
        private const string DustShaderName = "Kmax Anatomy/Dust";
        private const int CardsPerRow = 5;
        private const float CardWidth = 172f;
        private const float CardHeight = 132f;
        private const float CardSpacing = 12f;
        private const float ThumbnailMargin = 8f;
        private const float ThumbnailWidth = 156f;
        private const float ThumbnailHeight = 88f;
        private const float LabelHeight = 26f;
        private const float LabelFontSize = 14f;
        private const float SelectBarHeight = 3f;
        private const float SelectBarInset = 18f;
        private const float LoadWidth = 300f;
        private const float LoadHeight = 42f;
        private const float LoadBottom = 26f;
        private const float LoadGap = 14f;
        private const float LoadFontSize = 17f;
        private const float TitleTop = 22f;
        private const float TitleFontSize = 30f;
        private const float TitleLetterSpacing = 6f;
        private const float SubtitleFontSize = 17f;

        private static readonly Color CardHover = new Color32(16, 110, 134, 245);
        private static readonly Color LoadFill = new Color32(8, 86, 104, 240);
        private static readonly Color LoadHover = new Color32(14, 124, 148, 250);

        /// <summary>Builds the heading, the cards and the Load button under the canvas, and returns the launcher they belong to. It starts hidden.</summary>
        public static AnatomyLauncher BuildInterface(RectTransform canvas) {
            GameObject root = new GameObject(LauncherName, typeof(RectTransform));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(canvas, false);
            AnatomyUiStyle.Anchor(rootRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            TMP_Text titleText;
            TMP_Text subtitleText;
            BuildHeader(rootRect, out titleText, out subtitleText);

            LauncherCard[] cards = BuildCards(rootRect);

            Button loadButton;
            TMP_Text loadLabel;
            BuildLoadButton(rootRect, out loadButton, out loadLabel);

            AnatomyLauncher launcher = root.AddComponent<AnatomyLauncher>();
            SerializedObject launcherObject = new SerializedObject(launcher);
            KmaxRigBuilder.SetReferences(launcherObject, "cards", cards);
            KmaxRigBuilder.SetReference(launcherObject, "titleText", titleText);
            KmaxRigBuilder.SetReference(launcherObject, "subtitleText", subtitleText);
            KmaxRigBuilder.SetReference(launcherObject, "loadButton", loadButton);
            KmaxRigBuilder.SetReference(launcherObject, "loadLabel", loadLabel);
            launcherObject.ApplyModifiedPropertiesWithoutUndo();

            // The shell brings it up once the screen is covered.
            root.SetActive(false);
            return launcher;
        }

        /// <summary>
        /// Builds the turntable, under the same parent as a topic's model so both stand at the middle of the scene, and the dust, at
        /// the scene's root, and gives the launcher what it needs of the scene.
        /// </summary>
        public static void BuildStage(AnatomyLauncher launcher, Transform modelParent, ViewerFlyController viewer, AnatomyAudio sound) {
            GameObject stage = KmaxRigBuilder.FindOrCreateChild(modelParent, StageName);
            stage.transform.localRotation = Quaternion.identity;
            LauncherPreview preview = stage.GetComponent<LauncherPreview>();
            if (preview == null) {
                preview = stage.AddComponent<LauncherPreview>();
            }

            ParticleSystem dust = BuildDust();

            SerializedObject launcherObject = new SerializedObject(launcher);
            KmaxRigBuilder.SetReference(launcherObject, "preview", preview);
            KmaxRigBuilder.SetReference(launcherObject, "dust", dust);
            KmaxRigBuilder.SetReference(launcherObject, "viewer", viewer);
            KmaxRigBuilder.SetReference(launcherObject, "sound", sound);
            launcherObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildHeader(RectTransform parent, out TMP_Text titleText, out TMP_Text subtitleText) {
            GameObject header = new GameObject("Header", typeof(RectTransform));
            RectTransform headerRect = header.GetComponent<RectTransform>();
            headerRect.SetParent(parent, false);
            AnatomyUiStyle.Anchor(headerRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -TitleTop), new Vector2(1200f, 80f));

            TextMeshProUGUI title = AnatomyUiStyle.CreateText(headerRect, "Title", TitleFontSize, AnatomyUiStyle.Heading,
                FontStyles.Bold, TextAlignmentOptions.Top);
            title.characterSpacing = TitleLetterSpacing;
            AnatomyUiStyle.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(0f, 40f));

            TextMeshProUGUI subtitle = AnatomyUiStyle.CreateText(headerRect, "Subtitle", SubtitleFontSize, AnatomyUiStyle.AccentBright,
                FontStyles.Normal, TextAlignmentOptions.Top);
            AnatomyUiStyle.Anchor(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -42f), new Vector2(0f, 28f));

            titleText = title;
            subtitleText = subtitle;
        }

        /// <summary>
        /// The grid: a container at the foot of the screen, above the Load button, holding rows that centre their cards. Each card
        /// is a button in a slot the layout moves, with its picture, its name and the cyan bar of the chosen card inside it.
        /// </summary>
        private static LauncherCard[] BuildCards(RectTransform parent) {
            int rows = (CardCount + CardsPerRow - 1) / CardsPerRow;
            float width = CardsPerRow * CardWidth + (CardsPerRow - 1) * CardSpacing;
            float height = rows * CardHeight + (rows - 1) * CardSpacing;

            GameObject grid = new GameObject("Cards", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform gridRect = grid.GetComponent<RectTransform>();
            gridRect.SetParent(parent, false);
            AnatomyUiStyle.Anchor(gridRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, LoadBottom + LoadHeight + LoadGap), new Vector2(width, height));

            VerticalLayoutGroup gridLayout = grid.GetComponent<VerticalLayoutGroup>();
            gridLayout.spacing = CardSpacing;
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.childControlWidth = true;
            gridLayout.childControlHeight = true;
            gridLayout.childForceExpandWidth = true;
            gridLayout.childForceExpandHeight = false;

            LauncherCard[] cards = new LauncherCard[CardCount];
            RectTransform row = null;
            for (int i = 0; i < CardCount; i++) {
                if (i % CardsPerRow == 0) {
                    row = CreateRow(gridRect, "Row" + (i / CardsPerRow + 1));
                }

                cards[i] = CreateCard(row, i + 1);
            }

            return cards;
        }

        private static RectTransform CreateRow(RectTransform parent, string rowName) {
            GameObject host = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            host.GetComponent<LayoutElement>().preferredHeight = CardHeight;

            HorizontalLayoutGroup layout = host.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = CardSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private static LauncherCard CreateCard(RectTransform row, int number) {
            GameObject slotObject = new GameObject("Card" + number + "Slot", typeof(RectTransform), typeof(LayoutElement));
            RectTransform slot = slotObject.GetComponent<RectTransform>();
            slot.SetParent(row, false);
            LayoutElement element = slotObject.GetComponent<LayoutElement>();
            element.preferredWidth = CardWidth;
            element.preferredHeight = CardHeight;

            GameObject host = new GameObject("Card" + number, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform hostRect = host.GetComponent<RectTransform>();
            hostRect.SetParent(slot, false);
            AnatomyUiStyle.Anchor(hostRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image fill = host.GetComponent<Image>();
            AnatomyUiStyle.ApplyGlass(fill, AnatomyUiStyle.Glass);
            Button button = host.GetComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            UiButtonMotion motion = AnatomyUiStyle.AddButtonFeel(host, fill, CardHover, false);

            Image thumbnail = new GameObject("Thumbnail", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            thumbnail.rectTransform.SetParent(hostRect, false);
            AnatomyUiStyle.Anchor(thumbnail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -ThumbnailMargin), new Vector2(ThumbnailWidth, ThumbnailHeight));
            thumbnail.raycastTarget = false;
            thumbnail.preserveAspect = false;

            Image selectBar = new GameObject("SelectBar", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            selectBar.rectTransform.SetParent(hostRect, false);
            AnatomyUiStyle.Anchor(selectBar.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -1f), new Vector2(-2f * SelectBarInset, SelectBarHeight));
            selectBar.color = AnatomyUiStyle.Accent;
            selectBar.raycastTarget = false;
            selectBar.enabled = false;

            TextMeshProUGUI label = AnatomyUiStyle.CreateText(hostRect, "Label", LabelFontSize, AnatomyUiStyle.Body,
                FontStyles.Bold, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = LabelFontSize;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            AnatomyUiStyle.Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 4f), new Vector2(-12f, LabelHeight));

            LauncherCard card = host.AddComponent<LauncherCard>();
            SerializedObject cardObject = new SerializedObject(card);
            KmaxRigBuilder.SetReference(cardObject, "slot", slotObject);
            KmaxRigBuilder.SetReference(cardObject, "button", button);
            KmaxRigBuilder.SetReference(cardObject, "motion", motion);
            KmaxRigBuilder.SetReference(cardObject, "thumbnail", thumbnail);
            KmaxRigBuilder.SetReference(cardObject, "label", label);
            KmaxRigBuilder.SetReference(cardObject, "selectBar", selectBar);
            KmaxRigBuilder.SetReference(cardObject, "outline", host.transform.Find("Outline").GetComponent<Image>());
            KmaxRigBuilder.SetColor(cardObject, "restFill", AnatomyUiStyle.Glass);
            KmaxRigBuilder.SetColor(cardObject, "chosenFill", AnatomyUiStyle.GlassChosen);
            KmaxRigBuilder.SetColor(cardObject, "restOutline", AnatomyUiStyle.GlassOutline);
            KmaxRigBuilder.SetColor(cardObject, "chosenOutline", AnatomyUiStyle.AccentBright);
            KmaxRigBuilder.SetColor(cardObject, "restLabel", AnatomyUiStyle.Body);
            KmaxRigBuilder.SetColor(cardObject, "chosenLabel", AnatomyUiStyle.Heading);
            cardObject.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        /// <summary>The Load button: a pill of cyan-tinted glass with a cyan outline, under the cards, that breathes a little while it waits.</summary>
        private static void BuildLoadButton(RectTransform parent, out Button button, out TMP_Text label) {
            GameObject slotObject = new GameObject("LoadSlot", typeof(RectTransform));
            RectTransform slot = slotObject.GetComponent<RectTransform>();
            slot.SetParent(parent, false);
            AnatomyUiStyle.Anchor(slot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, LoadBottom), new Vector2(LoadWidth, LoadHeight));

            GameObject host = new GameObject("Load", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform hostRect = host.GetComponent<RectTransform>();
            hostRect.SetParent(slot, false);
            AnatomyUiStyle.Anchor(hostRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image fill = host.GetComponent<Image>();
            AnatomyUiStyle.ApplyGlass(fill, LoadFill);
            host.transform.Find("Outline").GetComponent<Image>().color = AnatomyUiStyle.AccentBright;
            button = host.GetComponent<Button>();
            button.targetGraphic = fill;
            button.transition = Selectable.Transition.None;
            AnatomyUiStyle.AddButtonFeel(host, fill, LoadHover, true);

            TextMeshProUGUI text = AnatomyUiStyle.CreateText(hostRect, "Label", LoadFontSize, AnatomyUiStyle.Heading,
                FontStyles.Bold, TextAlignmentOptions.Center);
            text.text = "Load";
            // The longest name, Put the Organs Back, has to fit the pill.
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = LoadFontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            AnatomyUiStyle.Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(-24f, 0f));
            label = text;
        }

        /// <summary>
        /// The dust: a slow cloud of faint round motes in a box behind and about the model, in world space, so it holds still when
        /// the launcher's view does. It is placed and started afresh each time the launcher is shown.
        /// </summary>
        private static ParticleSystem BuildDust() {
            GameObject host = KmaxRigBuilder.FindOrCreateRoot(DustName);
            host.SetActive(true);
            ParticleSystem particles = host.GetComponent<ParticleSystem>();
            if (particles == null) {
                particles = host.AddComponent<ParticleSystem>();
            }

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 26f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.003f, 0.008f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.0012f, 0.0028f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.62f, 0.86f, 1f, 0.25f), new Color(0.62f, 0.86f, 1f, 0.6f));
            main.gravityModifier = 0f;
            main.maxParticles = 140;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 7f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.62f, 0.36f, 0.3f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.004f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.1f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            Gradient fade = new Gradient();
            fade.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(fade);

            ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = EnsureDustMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // The launcher switches it on and plays it when it is shown.
            host.SetActive(false);
            return particles;
        }

        private static Material EnsureDustMaterial() {
            KmaxRigBuilder.EnsureFolder(Path.GetDirectoryName(DustMaterialPath).Replace('\\', '/'));
            Shader shader = Shader.Find(DustShaderName);
            if (shader == null) {
                Debug.LogError($"[Anatomy] The shader '{DustShaderName}' is missing, so the launcher's dust cannot be drawn.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(DustMaterialPath);
            if (material == null) {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, DustMaterialPath);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", AnatomyGlassSprites.EnsureSoftDot().texture);
            material.SetColor("_BaseColor", Color.white);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}