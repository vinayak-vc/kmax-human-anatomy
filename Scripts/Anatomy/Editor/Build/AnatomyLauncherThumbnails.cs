using System.Collections.Generic;

using UnityEditor;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Renders the picture on each launcher card from the topic's own model: a three-quarter view, lit like the exhibit, framed to
    /// fill most of the card. They are written into <c>Generated/Resources/Anatomy/Thumbs</c>, next to the models they are made
    /// from, so like the models they are made from the DOSCH pack, ignored by git and absent on a fresh clone; a card without a
    /// picture shows its name alone.
    ///
    /// <para>Run it again after a model is re-imported. A structure that rests as glass in its topic is drawn as glass here too.</para>
    /// </summary>
    public static class AnatomyLauncherThumbnails {
        public const string Folder = AnatomyPaths.PrefabFolder + "/Thumbs";

        private const int Width = 512;
        private const int Height = 288;
        private const float FieldOfView = 26f;
        private const float Yaw = -28f;
        private const float Pitch = 12f;
        private const float FillShare = 0.84f;
        private const int FitPasses = 5;
        private const int MaxSamples = 12000;

        private static readonly Color Background = new Color(0.043f, 0.063f, 0.114f, 1f);
        private static readonly Color RimTint = new Color(0.72f, 0.92f, 1f, 1f);

        /// <summary>Renders a picture for every exhibit the launcher offers.</summary>
        public static void RenderAll() {
            AnatomyLauncherData data = AnatomyTopicLibrary.LoadLauncher();
            if (data == null) {
                return;
            }

            int made = 0;
            for (int i = 0; i < data.Entries.Count; i++) {
                EditorUtility.DisplayProgressBar("Launcher thumbnails", data.Entries[i].Topic, (float)i / data.Entries.Count);
                if (Render(data.Entries[i].Topic)) {
                    made++;
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Anatomy] Rendered {made} of {data.Entries.Count} launcher thumbnails into {Folder}.");
        }

        /// <summary>Renders one topic's picture. Returns false, after saying why, when its model has not been imported.</summary>
        public static bool Render(string topicId) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AnatomyPaths.PrefabFolder + "/" + topicId + ".prefab");
            AnatomyTopicData topic = AnatomyTopicLibrary.LoadData(topicId);
            if (prefab == null || topic == null) {
                Debug.LogWarning($"[Anatomy] No imported model for '{topicId}', so it has no launcher thumbnail. Import it first.");
                return false;
            }

            byte[] png = Capture(prefab, topic);
            AnatomySpriteFiles.Ensure(Folder + "/" + topicId + ".png", png, Vector4.zero);
            return true;
        }

        private static byte[] Capture(GameObject prefab, AnatomyTopicData topic) {
            PreviewRenderUtility utility = new PreviewRenderUtility();
            try {
                GameObject instance = Object.Instantiate(prefab);
                utility.AddSingleGO(instance);
                RestLook(instance, topic);

                List<Vector3> corners = Corners(instance);
                Camera camera = utility.camera;
                camera.fieldOfView = FieldOfView;
                camera.aspect = Width / (float)Height;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Background;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;
                Frame(camera, corners);

                utility.lights[0].intensity = 1.2f;
                utility.lights[0].transform.rotation = Quaternion.Euler(38f, -28f, 0f);
                utility.lights[1].intensity = 0.5f;
                utility.lights[1].transform.rotation = Quaternion.Euler(-12f, 42f, 0f);

                utility.BeginStaticPreview(new Rect(0, 0, Width, Height));
                camera.Render();
                Texture rendered = utility.EndStaticPreview();
                return Encode(rendered);
            } finally {
                utility.Cleanup();
            }
        }

        /// <summary>
        /// The look a topic opens with, which the prefab alone does not have: the body map's layers that start hidden are hidden,
        /// and a structure that rests as glass is drawn as glass, the way <see cref="StructureHighlight"/> draws it.
        /// </summary>
        private static void RestLook(GameObject instance, AnatomyTopicData topic) {
            AnatomyLayer[] layers = instance.GetComponentsInChildren<AnatomyLayer>(true);
            for (int i = 0; i < layers.Length; i++) {
                if (!layers[i].StartsShown) {
                    layers[i].LayerRenderer.enabled = false;
                }
            }

            AnatomyModel model = instance.GetComponent<AnatomyModel>();
            int baseColour = Shader.PropertyToID("_BaseColor");
            for (int i = 0; i < model.Structures.Count; i++) {
                AnatomyStructure structure = model.Structures[i];
                AnatomyStructureInfo info = topic.FindStructure(structure.StructureId);
                if (info == null || info.RestOpacity >= 1f || structure.GhostMaterial == null || structure.RimMaterial == null) {
                    continue;
                }

                Renderer renderer = structure.StructureRenderer;
                Color solid = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(baseColour)
                    ? renderer.sharedMaterial.GetColor(baseColour)
                    : Color.white;
                renderer.sharedMaterials = new Material[] { structure.GhostMaterial, structure.RimMaterial };

                MaterialPropertyBlock body = new MaterialPropertyBlock();
                Color bodyColour = solid;
                bodyColour.a = info.RestOpacity;
                body.SetColor(baseColour, bodyColour);
                renderer.SetPropertyBlock(body, 0);

                MaterialPropertyBlock rim = new MaterialPropertyBlock();
                Color rimColour = Color.Lerp(solid, RimTint, 0.65f);
                rimColour.a = (1f - info.RestOpacity) * 0.35f;
                rim.SetColor(baseColour, rimColour);
                renderer.SetPropertyBlock(rim, 1);
            }
        }

        /// <summary>
        /// The outline of the drawn model in world space: its vertices, thinned to an even sample so that fitting the frame to
        /// them is quick. The sample is spread along the meshes' own order, which follows the surface, so it reaches the extremes
        /// closely; the frame is not filled to its edge, which leaves room for what the sample misses.
        /// </summary>
        private static List<Vector3> Corners(GameObject instance) {
            List<Vector3> all = new List<Vector3>();
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++) {
                if (!renderers[i].enabled || (!(renderers[i] is MeshRenderer) && !(renderers[i] is SkinnedMeshRenderer))) {
                    continue;
                }

                LauncherFit.AppendOutline(renderers[i], renderers[i].transform.localToWorldMatrix, all);
            }

            int stride = Mathf.Max(1, all.Count / MaxSamples);
            List<Vector3> sample = new List<Vector3>(MaxSamples + 1);
            for (int i = 0; i < all.Count; i += stride) {
                sample.Add(all[i]);
            }

            return sample;
        }

        /// <summary>
        /// Looks at the middle of the model from the three-quarter angle and backs away until its outline fills
        /// <see cref="FillShare"/> of the frame. A few passes, because perspective makes the fit not quite proportional to distance.
        /// </summary>
        private static void Frame(Camera camera, List<Vector3> corners) {
            Vector3 low = corners[0];
            Vector3 high = corners[0];
            for (int i = 1; i < corners.Count; i++) {
                low = Vector3.Min(low, corners[i]);
                high = Vector3.Max(high, corners[i]);
            }

            Vector3 centre = (low + high) * 0.5f;
            Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            camera.transform.rotation = rotation;
            float distance = (high - low).magnitude * 0.5f / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad);
            for (int pass = 0; pass < FitPasses; pass++) {
                camera.transform.position = centre - rotation * Vector3.forward * distance;
                float reach = 0f;
                for (int i = 0; i < corners.Count; i++) {
                    Vector3 view = camera.WorldToViewportPoint(corners[i]);
                    reach = Mathf.Max(reach, Mathf.Abs(view.x - 0.5f) * 2f, Mathf.Abs(view.y - 0.5f) * 2f);
                }

                if (reach > Mathf.Epsilon) {
                    distance *= reach / FillShare;
                }
            }

            camera.transform.position = centre - rotation * Vector3.forward * distance;
        }

        private static byte[] Encode(Texture rendered) {
            Texture2D readable = new Texture2D(rendered.width, rendered.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(rendered.width, rendered.height, 0);
            Graphics.Blit(rendered, temporary);
            RenderTexture.active = temporary;
            readable.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
            return AnatomySpriteFiles.Encode(readable);
        }
    }
}