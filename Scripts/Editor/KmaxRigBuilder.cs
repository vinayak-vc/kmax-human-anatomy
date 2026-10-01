using System.IO;
using KmaxXR;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ViitorCloud.KmaxDisplay.Editor {
    /// <summary>
    /// The parts every Kmax scene needs, built from an editor script rather than authored by
    /// hand: the XR rig, the event system running the Kmax input module, the tracked stylus tip,
    /// the comfort diagnostics, and the serialized-field helpers a scene builder needs.
    ///
    /// <para>Scene setup lives in code so a scene can be rebuilt from scratch and two scenes
    /// cannot silently drift apart - which is how a project ends up with one scene that behaves
    /// differently for reasons nobody can find.</para>
    ///
    /// <para><b>Every helper here converges on the spec.</b> Nothing returns early because the
    /// object already exists - it is found and then re-applied. A build step that hands back an
    /// existing object untouched while logging success is the most expensive bug in this file's
    /// history.</para>
    /// </summary>
    public static class KmaxRigBuilder {
        public const string ModuleRoot = "Assets/Games/kmax-human-anatomy";
        public const string XrRigPrefabPath =
            ModuleRoot + "/Plugins/Kmax/com.kmax.xr.core/Editor Resources/XRRig.prefab";

        private const string RigName = "XRRig";
        private const string EventSystemName = "EventSystem";
        private const string TipName = "StylusTip";
        private const string DiagnosticsName = "Diagnostics";

        /// <summary>
        /// Opens the scene at <paramref name="scenePath"/>, creating an empty one if it is not
        /// there yet.
        /// </summary>
        public static Scene EnsureScene(string scenePath) {
            EnsureFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'));
            if (File.Exists(scenePath)) {
                return UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            }
            Scene created = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(created, scenePath);
            return created;
        }

        /// <summary>
        /// Instantiates the SDK's rig prefab if the scene has none. The prefab carries the whole
        /// rig - the camera with <c>VRRenderer</c> and <c>HeadTracker</c>, its two sub-cameras,
        /// and the pen with <c>PenTracker</c> and <c>KmaxStylus</c> - so nothing else has to be
        /// assembled by hand.
        /// </summary>
        public static GameObject EnsureRig() {
            XRRig existing = Object.FindFirstObjectByType<XRRig>();
            if (existing != null) {
                return existing.gameObject;
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrRigPrefabPath);
            if (prefab == null) {
                Debug.LogError($"[Kmax] the Kmax rig prefab is missing at {XrRigPrefabPath}. " +
                    "No scene can be built without it.");
                return null;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = RigName;
            // Unpacked so the scene owns it and build steps can re-apply values without fighting
            // prefab overrides.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot,
                InteractionMode.AutomatedAction);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return instance;
        }

        /// <summary>
        /// Configures the rig's centre camera and returns it. The sub-cameras are created at
        /// runtime from this one.
        /// </summary>
        public static Camera ConfigureCamera(GameObject rig, Color background) {
            if (rig == null) {
                return null;
            }
            Camera camera = rig.GetComponentInChildren<Camera>(true);
            if (camera == null) {
                return null;
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 10f;
            return camera;
        }

        /// <summary>
        /// Configures every camera under the rig, not only the centre one. The SDK's rig prefab authors its
        /// left and right eye cameras, and those two are what actually draw; the centre camera is disabled at
        /// runtime. Configuring only the centre camera would leave the eyes on their prefab defaults, a
        /// skybox and a 100 m far plane.
        /// </summary>
        public static void ConfigureStereoCameras(GameObject rig, Color background) {
            if (rig == null) {
                return;
            }
            Camera[] cameras = rig.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++) {
                cameras[i].clearFlags = CameraClearFlags.SolidColor;
                cameras[i].backgroundColor = background;
                cameras[i].nearClipPlane = 0.02f;
                cameras[i].farClipPlane = 10f;
            }
        }

        /// <summary>
        /// An event system running <c>KmaxInputModule</c>. The standalone module is removed if it
        /// is there - two input modules dispatch every press twice, which an earlier exhibit found
        /// the hard way.
        /// </summary>
        public static void EnsureEventSystem() {
            EventSystem system = Object.FindFirstObjectByType<EventSystem>();
            GameObject host = system != null ? system.gameObject : FindOrCreateRoot(EventSystemName);
            if (host.GetComponent<EventSystem>() == null) {
                host.AddComponent<EventSystem>();
            }
            StandaloneInputModule standalone = host.GetComponent<StandaloneInputModule>();
            if (standalone != null) {
                Object.DestroyImmediate(standalone);
            }
            if (host.GetComponent<KmaxInputModule>() == null) {
                host.AddComponent<KmaxInputModule>();
            }
        }

        /// <summary>
        /// The tracked tip, its haptics and optionally the grab, parented under the rig.
        /// </summary>
        /// <param name="withGrab">Adds <see cref="StylusGrab"/>. Bloom does not need it.</param>
        public static StylusTip EnsureTip(GameObject rig, float tipRadius, bool withGrab) {
            Transform parent = rig != null ? rig.transform : null;
            GameObject host = FindOrCreateChild(parent, TipName);
            KmaxStylus stylus = rig != null ? rig.GetComponentInChildren<KmaxStylus>(true) : null;

            StylusTip tip = host.GetComponent<StylusTip>();
            if (tip == null) {
                tip = host.AddComponent<StylusTip>();
            }
            StylusHaptics haptics = host.GetComponent<StylusHaptics>();
            if (haptics == null) {
                haptics = host.AddComponent<StylusHaptics>();
            }

            SerializedObject tipObject = new SerializedObject(tip);
            SetReference(tipObject, "stylus", stylus);
            SetFloat(tipObject, "radius", tipRadius);
            SetBool(tipObject, "mouseFallback", true);
            tipObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject hapticsObject = new SerializedObject(haptics);
            SetReference(hapticsObject, "stylus", stylus);
            hapticsObject.ApplyModifiedPropertiesWithoutUndo();

            StylusGrab grab = host.GetComponent<StylusGrab>();
            if (withGrab) {
                if (grab == null) {
                    grab = host.AddComponent<StylusGrab>();
                }
                SerializedObject grabObject = new SerializedObject(grab);
                SetReference(grabObject, "tip", tip);
                SetReference(grabObject, "haptics", haptics);
                SetReference(grabObject, "stylus", stylus);
                grabObject.ApplyModifiedPropertiesWithoutUndo();
            } else if (grab != null) {
                Object.DestroyImmediate(grab);
            }
            return tip;
        }

        /// <summary>The comfort overlay and the scene-view gizmo, both development aids.</summary>
        public static void EnsureDiagnostics(StylusTip tip) {
            GameObject host = FindOrCreateRoot(DiagnosticsName);
            ComfortOverlay overlay = host.GetComponent<ComfortOverlay>();
            if (overlay == null) {
                overlay = host.AddComponent<ComfortOverlay>();
            }
            SerializedObject overlayObject = new SerializedObject(overlay);
            SetReference(overlayObject, "tip", tip);
            SetBool(overlayObject, "visibleOnStart", false);
            overlayObject.ApplyModifiedPropertiesWithoutUndo();

            if (host.GetComponent<StereoVolumeGizmo>() == null) {
                host.AddComponent<StereoVolumeGizmo>();
            }
        }

        /// <summary>
        /// Gives the pen the exhibit's beam in place of the SDK's ray. A <see cref="StylusBeam"/> goes on the object the stylus
        /// draws its visual on, using the SDK's own line, with a small bead at the end of it in place of the SDK's particle dot, and
        /// the SDK's <c>StylusRay</c> is removed: the stylus takes the first pointer visual it finds on that object.
        /// </summary>
        /// <param name="tipMaterialPath">Where the bead's unlit material is kept.</param>
        public static StylusBeam EnsureBeam(GameObject rig, string tipMaterialPath) {
            KmaxStylus stylus = rig != null ? rig.GetComponentInChildren<KmaxStylus>(true) : null;
            if (stylus == null) {
                Debug.LogWarning("[Kmax] the rig has no stylus, so the beam was not set up.");
                return null;
            }

            SerializedObject stylusObject = new SerializedObject(stylus);
            SerializedProperty visualProperty = stylusObject.FindProperty("stylus");
            Transform visual = visualProperty != null ? visualProperty.objectReferenceValue as Transform : null;
            LineRenderer line = visual != null ? visual.GetComponentInChildren<LineRenderer>(true) : null;
            if (visual == null || line == null) {
                Debug.LogWarning("[Kmax] the stylus has no visual with a line, so the beam was not set up.");
                return null;
            }

            Transform sdkPointer = visual.Find("pointer");
            if (sdkPointer != null) {
                sdkPointer.gameObject.SetActive(false);
            }

            GameObject bead = FindOrCreateChild(visual, "BeamTip");

            // The beam resizes and places the bead every frame; this is only what the scene view shows until then.
            bead.transform.localPosition = Vector3.zero;
            bead.transform.localScale = Vector3.one * 0.006f;
            MeshFilter beadMesh = bead.GetComponent<MeshFilter>();
            if (beadMesh == null) {
                beadMesh = bead.AddComponent<MeshFilter>();
            }

            beadMesh.sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            MeshRenderer beadRenderer = bead.GetComponent<MeshRenderer>();
            if (beadRenderer == null) {
                beadRenderer = bead.AddComponent<MeshRenderer>();
            }

            Material beadMaterial = EnsureUnlitMaterial(tipMaterialPath, Color.white);
            if (beadMaterial != null) {
                // Every surface in the suite draws both sides.
                beadMaterial.SetFloat("_Cull", 0f);
            }

            beadRenderer.sharedMaterial = beadMaterial;
            beadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beadRenderer.receiveShadows = false;

            StylusBeam beam = visual.GetComponent<StylusBeam>();
            if (beam == null) {
                beam = visual.gameObject.AddComponent<StylusBeam>();
            }

            SerializedObject beamObject = new SerializedObject(beam);
            SetReference(beamObject, "beam", line);
            SetReference(beamObject, "tip", bead.transform);
            SetReference(beamObject, "tipRenderer", beadRenderer);
            SetBool(beamObject, "alignTipToSurface", false);
            // The controller already ticks the pen when a structure is pointed at, and a second pulse on top would buzz.
            SetBool(beamObject, "vibrateOnHitEnter", false);
            beamObject.ApplyModifiedPropertiesWithoutUndo();

            StylusRay sdkRay = visual.GetComponent<StylusRay>();
            if (sdkRay != null) {
                Object.DestroyImmediate(sdkRay, true);
            }

            EditorUtility.SetDirty(beam);
            return beam;
        }

        public static GameObject FindOrCreateRoot(string rootName) {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) {
                if (roots[i].name == rootName) {
                    return roots[i];
                }
            }
            return new GameObject(rootName);
        }

        public static GameObject FindOrCreateChild(Transform parent, string childName) {
            if (parent == null) {
                return FindOrCreateRoot(childName);
            }
            Transform existing = parent.Find(childName);
            if (existing != null) {
                return existing.gameObject;
            }
            GameObject created = new GameObject(childName);
            created.transform.SetParent(parent, false);
            return created;
        }

        public static void EnsureFolder(string path) {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>
        /// Creates or updates a URP Lit material asset. Opaque only - the suite's scope guards rule
        /// out semi-transparent surfaces in front of solid ones, which are the case that genuinely
        /// will not fuse in stereo.
        /// </summary>
        public static Material EnsureLitMaterial(string path, Color color, float smoothness,
            float metallic) {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) {
                Debug.LogError("[Kmax] URP Lit shader not found.");
                return null;
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.shader != shader) {
                material.shader = shader;
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Creates or updates a URP Unlit material asset.
        ///
        /// Unlit where the colour itself is the signal rather than the surface - Probe's tunnel
        /// flashes red on contact, and a lit material would make that flash depend on where the
        /// struts happen to face. It also means the scene needs no lights at all.
        /// </summary>
        public static Material EnsureUnlitMaterial(string path, Color color) {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) {
                Debug.LogError("[Kmax] URP Unlit shader not found.");
                return null;
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.shader != shader) {
                material.shader = shader;
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void SetReference(SerializedObject target, string field, Object value) {
            SerializedProperty property = Find(target, field, "object");
            if (property == null) {
                return;
            }
            property.objectReferenceValue = value;
        }

        public static void SetFloat(SerializedObject target, string field, float value) {
            SerializedProperty property = Find(target, field, "float");
            if (property == null) {
                return;
            }
            property.floatValue = value;
        }

        public static void SetInt(SerializedObject target, string field, int value) {
            SerializedProperty property = Find(target, field, "int");
            if (property == null) {
                return;
            }
            property.intValue = value;
        }

        public static void SetBool(SerializedObject target, string field, bool value) {
            SerializedProperty property = Find(target, field, "bool");
            if (property == null) {
                return;
            }
            property.boolValue = value;
        }

        public static void SetString(SerializedObject target, string field, string value) {
            SerializedProperty property = Find(target, field, "string");
            if (property == null) {
                return;
            }
            property.stringValue = value;
        }

        public static void SetColor(SerializedObject target, string field, Color value) {
            SerializedProperty property = Find(target, field, "color");
            if (property == null) {
                return;
            }
            property.colorValue = value;
        }

        public static void SetBounds(SerializedObject target, string field, Bounds value) {
            SerializedProperty property = Find(target, field, "bounds");
            if (property == null) {
                return;
            }
            property.boundsValue = value;
        }

        public static void SetVector3(SerializedObject target, string field, Vector3 value) {
            SerializedProperty property = Find(target, field, "vector3");
            if (property == null) {
                return;
            }
            property.vector3Value = value;
        }

        public static void SetReferences(SerializedObject target, string field, Object[] values) {
            SerializedProperty property = Find(target, field, "array");
            if (property == null) {
                return;
            }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static SerializedProperty Find(SerializedObject target, string field, string kind) {
            SerializedProperty property = target.FindProperty(field);
            if (property == null) {
                // Loud, because the failure is otherwise invisible: the build logs success and the
                // value it meant to write is simply never applied.
                Debug.LogWarning($"[Kmax] no {kind} field '{field}' on " +
                    $"'{target.targetObject.GetType().Name}'. It was renamed or removed, and the " +
                    "build step is out of date.");
            }
            return property;
        }
    }
}
