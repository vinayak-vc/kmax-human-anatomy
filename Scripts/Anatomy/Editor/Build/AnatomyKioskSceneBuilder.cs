using KmaxXR;

using UnityEditor;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.Rendering;

using ViitorCloud.KmaxDisplay;
using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Builds <c>Scene/Main.unity</c>, the one persistent scene the kiosk runs in: the Kmax rig set for the
    /// 27" display, the stylus and event system, viewer-fixed lighting, the interface, the topic
    /// controller and the kiosk shell that runs it unattended. The scene never references a DOSCH-derived asset; the
    /// controller loads a topic by name, and it opens on the body map.
    ///
    /// <para>Like every builder here it converges on the spec: each step finds what exists and re-applies
    /// it, so running it twice, or after the scene has drifted, gives the same scene.</para>
    /// </summary>
    public static class AnatomyKioskSceneBuilder {
        private const string ScenePath = KmaxRigBuilder.ModuleRoot + "/Scene/Main.unity";
        private const string ExhibitName = "Exhibit";
        private const string ViewerName = "Viewer";
        private const string LogPrefix = "[Anatomy] ";

        /// <summary>The topic the exhibit opens on and returns to: the body map.</summary>
        private const string HubTopicId = "body";

        /// <summary>Distance from the eye to the model's centre. The eye is 0.5 m from the glass.</summary>
        private const float ViewerDistance = 0.47f;
        private const float ViewerDistanceMinimum = 0.44f;
        private const float ViewerDistanceMaximum = 0.5f;

        private static readonly Color BackgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);

        [MenuItem("Kmax/Anatomy/Build/Kiosk Scene")]
        public static void Build() {
            KmaxRigBuilder.EnsureScene(ScenePath);
            RemoveStockCameras();

            GameObject rig = KmaxRigBuilder.EnsureRig();
            if (rig == null) {
                return;
            }

            ConfigureScreen(rig);
            KmaxRigBuilder.ConfigureStereoCameras(rig, BackgroundColor);
            KmaxRigBuilder.EnsureEventSystem();
            StylusTip tip = KmaxRigBuilder.EnsureTip(rig, 0.004f, true);
            KmaxRigBuilder.EnsureDiagnostics(tip);
            EnsureLighting(rig);

            Camera eventCamera = rig.GetComponentInChildren<Camera>(true);
            AnatomyInterfaceParts parts = AnatomyInterfaceBuilder.Build(eventCamera, ReadWindowSize(rig));

            AnatomyTopicController controller = EnsureExhibit();
            ViewerFlyController viewer = EnsureViewer(rig, controller);
            WireExhibit(controller, viewer, parts, tip);
            WireShell(controller, parts, tip, rig);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"{LogPrefix}Kiosk scene built at {ScenePath}.");
        }

        /// <summary>
        /// The template's scene ships a stock Main Camera. The rig brings its own, and two audio listeners and
        /// two sets of cameras would fight, so the stock one goes.
        /// </summary>
        private static void RemoveStockCameras() {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++) {
                bool insideRig = cameras[i].GetComponentInParent<XRRig>() != null;
                if (!insideRig && cameras[i].gameObject.name == "Main Camera") {
                    Debug.Log($"{LogPrefix}Removed the template's stock Main Camera.");
                    Object.DestroyImmediate(cameras[i].gameObject);
                }
            }
        }

        /// <summary>
        /// Sets the virtual screen to 27". Applying the change makes the SDK recompute the window size in its
        /// own OnValidate, which is what stores the width and height the interface is sized from.
        /// </summary>
        private static void ConfigureScreen(GameObject rig) {
            SerializedObject serialized = new SerializedObject(rig.GetComponent<XRRig>());
            SerializedProperty screenType = serialized.FindProperty("screen.screenType");
            if (screenType == null) {
                Debug.LogWarning($"{LogPrefix}XRRig has no 'screen.screenType' field; the SDK changed and the screen size was not set.");
                return;
            }

            screenType.enumValueIndex = (int)VirtualScreen.ScreenType.Screen27;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector2 ReadWindowSize(GameObject rig) {
            SerializedObject serialized = new SerializedObject(rig.GetComponent<XRRig>());
            serialized.Update();
            SerializedProperty width = serialized.FindProperty("screen.width");
            SerializedProperty height = serialized.FindProperty("screen.height");
            if (width == null || height == null || width.floatValue <= 0f) {
                Debug.LogError($"{LogPrefix}Could not read the virtual screen size from the rig; the interface will be mis-sized in the scene view.");
                return new Vector2(0.5977f, 0.3362f);
            }

            return new Vector2(width.floatValue, height.floatValue);
        }

        /// <summary>
        /// Three directional lights parented to the rig. The viewer orbits the model by moving the rig, so
        /// lights that belong to the rig keep the same side lit whatever the angle, like a turntable in a studio.
        /// Shadows are off: stereo already gives depth, and shadow edges shimmer between the two eyes.
        /// </summary>
        private static void EnsureLighting(GameObject rig) {
            ConfigureLight(rig.transform, "Key Light", new Color(1f, 0.96f, 0.9f, 1f), 1.15f, Quaternion.Euler(38f, -28f, 0f));
            ConfigureLight(rig.transform, "Fill Light", new Color(0.7f, 0.8f, 1f, 1f), 0.35f, Quaternion.Euler(-12f, 42f, 0f));
            ConfigureLight(rig.transform, "Rim Light", new Color(0.6f, 0.75f, 1f, 1f), 0.7f, Quaternion.Euler(8f, 180f, 0f));

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.14f, 0.16f, 0.2f, 1f);
        }

        private static void ConfigureLight(Transform parent, string lightName, Color color, float intensity, Quaternion rotation) {
            GameObject host = KmaxRigBuilder.FindOrCreateChild(parent, lightName);
            Light light = host.GetComponent<Light>();
            if (light == null) {
                light = host.AddComponent<Light>();
            }

            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = rotation;
        }

        private static AnatomyTopicController EnsureExhibit() {
            GameObject host = KmaxRigBuilder.FindOrCreateRoot(ExhibitName);
            host.transform.position = Vector3.zero;
            host.transform.rotation = Quaternion.identity;

            AnatomyTopicController controller = host.GetComponent<AnatomyTopicController>();
            if (controller == null) {
                controller = host.AddComponent<AnatomyTopicController>();
            }

            if (host.GetComponent<AnatomyAudio>() == null) {
                host.AddComponent<AnatomyAudio>();
            }

            if (host.GetComponent<AnatomyZoom>() == null) {
                host.AddComponent<AnatomyZoom>();
            }

            if (host.GetComponent<KioskShell>() == null) {
                host.AddComponent<KioskShell>();
            }

            GameObject modelParent = KmaxRigBuilder.FindOrCreateChild(host.transform, "Model");
            modelParent.transform.localPosition = Vector3.zero;
            modelParent.transform.localRotation = Quaternion.identity;

            if (modelParent.GetComponent<ComfortDepthKeeper>() == null) {
                modelParent.AddComponent<ComfortDepthKeeper>();
            }
            return controller;
        }

        private static ViewerFlyController EnsureViewer(GameObject rig, AnatomyTopicController controller) {
            GameObject host = KmaxRigBuilder.FindOrCreateRoot(ViewerName);
            ViewerFlyController viewer = host.GetComponent<ViewerFlyController>();
            if (viewer == null) {
                viewer = host.AddComponent<ViewerFlyController>();
            }

            SerializedObject viewerObject = new SerializedObject(viewer);
            KmaxRigBuilder.SetReference(viewerObject, "rigRoot", rig.transform);
            KmaxRigBuilder.SetReference(viewerObject, "resetHandlerSource", controller);
            KmaxRigBuilder.SetFloat(viewerObject, "defaultDistance", ViewerDistance);
            KmaxRigBuilder.SetFloat(viewerObject, "minDistance", ViewerDistanceMinimum);
            KmaxRigBuilder.SetFloat(viewerObject, "maxDistance", ViewerDistanceMaximum);
            // The wheel, the W and S keys and the pen's push and pull zoom the model instead of moving the camera.
            KmaxRigBuilder.SetBool(viewerObject, "applyDollyToCamera", false);
            viewerObject.ApplyModifiedPropertiesWithoutUndo();

            StylusNavigation navigation = host.GetComponent<StylusNavigation>();
            if (navigation == null) {
                navigation = host.AddComponent<StylusNavigation>();
            }

            SerializedObject navigationObject = new SerializedObject(navigation);
            KmaxRigBuilder.SetReference(navigationObject, "flyController", viewer);
            KmaxRigBuilder.SetReference(navigationObject, "rigRoot", rig.transform);
            // Dollying moves the model's centre through the comfort volume, so the pen's push and pull zooms the
            // model instead: the viewer reports it and AnatomyZoom answers.
            KmaxRigBuilder.SetBool(navigationObject, "enableDolly", true);
            navigationObject.ApplyModifiedPropertiesWithoutUndo();
            return viewer;
        }

        /// <summary>Gives the shell what it runs: the controller, the buttons, the fader, and, if the rig has them, the pen and the head tracker.</summary>
        private static void WireShell(AnatomyTopicController controller, AnatomyInterfaceParts parts, StylusTip tip, GameObject rig) {
            SerializedObject shellObject = new SerializedObject(controller.GetComponent<KioskShell>());
            KmaxRigBuilder.SetReference(shellObject, "controller", controller);
            KmaxRigBuilder.SetReference(shellObject, "controls", parts.Controls);
            KmaxRigBuilder.SetReference(shellObject, "fader", parts.Fader);
            KmaxRigBuilder.SetReference(shellObject, "stylusTip", tip);
            KmaxRigBuilder.SetReference(shellObject, "headTracker", rig.GetComponentInChildren<HeadTracker>(true));
            shellObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireExhibit(AnatomyTopicController controller, ViewerFlyController viewer,
            AnatomyInterfaceParts parts, StylusTip tip) {
            SerializedObject controllerObject = new SerializedObject(controller);
            KmaxRigBuilder.SetReference(controllerObject, "modelParent", controller.transform.Find("Model"));
            KmaxRigBuilder.SetReference(controllerObject, "viewer", viewer);
            KmaxRigBuilder.SetReference(controllerObject, "infoPanel", parts.Panel);
            KmaxRigBuilder.SetReference(controllerObject, "controls", parts.Controls);
            KmaxRigBuilder.SetReference(controllerObject, "markers", parts.Markers);
            KmaxRigBuilder.SetReference(controllerObject, "zoom", controller.GetComponent<AnatomyZoom>());
            KmaxRigBuilder.SetReference(controllerObject, "sound", controller.GetComponent<AnatomyAudio>());
            KmaxRigBuilder.SetReference(controllerObject, "haptics", tip != null ? tip.GetComponent<StylusHaptics>() : null);
            KmaxRigBuilder.SetReference(controllerObject, "depthKeeper", controller.transform.Find("Model").GetComponent<ComfortDepthKeeper>());
            KmaxRigBuilder.SetReference(controllerObject, "layerPanel", parts.Layers);
            KmaxRigBuilder.SetReference(controllerObject, "stylusTip", tip);
            KmaxRigBuilder.SetString(controllerObject, "hubTopicId", HubTopicId);
            KmaxRigBuilder.SetString(controllerObject, "startTopicId", HubTopicId);
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject zoomObject = new SerializedObject(controller.GetComponent<AnatomyZoom>());
            KmaxRigBuilder.SetReference(zoomObject, "viewer", viewer);
            zoomObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}