using UnityEditor;

using UnityEngine;

using ViitorCloud.KmaxDisplay.Editor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Cross-checks every topic's text against its imported model, so a renamed structure or a typo in an id
    /// is reported here and not discovered as a silently blank caption in front of a visitor. It also checks the launcher's cards
    /// against the topics they open, and that every surface of the models is drawn on both sides.
    /// </summary>
    public static class AnatomyTopicValidator {
        private const string LogPrefix = "[Anatomy] ";
        private const string GlowShaderName = "Kmax Anatomy/Glow";

        [MenuItem("Kmax/Anatomy/Validate Topic Data")]
        public static void ValidateAll() {
            AnatomyImportSpec[] specs = AnatomyTopicSources.All();
            int problems = 0;
            for (int i = 0; i < specs.Length; i++) {
                problems += Validate(specs[i].ModelId);
            }

            problems += Validate(AnatomyBodyImporter.ModelId);
            problems += Validate(AnatomyTorsoImporter.OrgansModelId);
            problems += Validate(AnatomyTorsoImporter.ScanModelId);
            problems += ValidateLauncher();
            problems += CountSingleSidedMaterials(false);

            if (problems == 0) {
                Debug.Log($"{LogPrefix}Topic data is consistent with the models.");
            } else {
                Debug.LogWarning($"{LogPrefix}{problems} problem(s) found in topic data.");
            }
        }

        /// <summary>
        /// Makes every material of the models, and of the interface's own, draw both sides: a cut-away or an open shell seen from
        /// inside would otherwise show nothing. The importers already write them that way; this puts right anything that was
        /// edited by hand. The additive glow layers cull their back faces on purpose, because stacked additive layers would
        /// otherwise brighten wherever a back face lies behind a front one, so they are left alone.
        /// </summary>
        [MenuItem("Kmax/Anatomy/Enforce Double-Sided Materials")]
        public static void EnforceDoubleSided() {
            int fixedCount = CountSingleSidedMaterials(true);
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix}{fixedCount} material(s) were drawn on one side only and now draw both.");
        }

        /// <summary>
        /// Counts the materials that cull a side, and puts them right when asked to. The Section shader culls nothing, in its source,
        /// and the glow layers are the one exception, so what is left is the URP materials with a cull mode other than off.
        /// </summary>
        private static int CountSingleSidedMaterials(bool fix) {
            string[] guids = AssetDatabase.FindAssets("t:Material", new string[] { AnatomyPaths.GeneratedRoot, KmaxRigBuilder.ModuleRoot + "/Content" });
            int found = 0;
            for (int i = 0; i < guids.Length; i++) {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null || material.shader.name == GlowShaderName || !material.HasProperty("_Cull")) {
                    continue;
                }

                if (!Mathf.Approximately(material.GetFloat("_Cull"), 0f)) {
                    found++;
                    if (fix) {
                        material.SetFloat("_Cull", 0f);
                        EditorUtility.SetDirty(material);
                    } else {
                        Debug.LogWarning($"{LogPrefix}'{path}' culls one side of its surface. Run Kmax > Anatomy > Enforce Double-Sided Materials.");
                    }
                }
            }

            return found;
        }

        /// <summary>Checks that every card opens a topic that exists, that no topic is offered twice, and that the cards fit the pool.</summary>
        private static int ValidateLauncher() {
            AnatomyLauncherData data = AnatomyTopicLibrary.LoadLauncher();
            if (data == null) {
                return 1;
            }

            int problems = 0;
            if (data.Entries.Count > AnatomyLauncherBuilder.CardCount) {
                Debug.LogError($"{LogPrefix}The launcher lists {data.Entries.Count} exhibits but the interface has only " +
                    $"{AnatomyLauncherBuilder.CardCount} cards. Rebuild the scene with a larger pool.");
                problems++;
            }

            for (int i = 0; i < data.Entries.Count; i++) {
                AnatomyLauncherEntry entry = data.Entries[i];
                if (string.IsNullOrEmpty(entry.Label)) {
                    Debug.LogError($"{LogPrefix}Launcher card {i + 1} has no name.");
                    problems++;
                }

                if (Resources.Load<TextAsset>("Topics/" + entry.Topic) == null) {
                    Debug.LogError($"{LogPrefix}Launcher card '{entry.Label}' opens topic '{entry.Topic}', which has no data.");
                    problems++;
                }

                for (int other = 0; other < i; other++) {
                    if (data.Entries[other].Topic == entry.Topic) {
                        Debug.LogError($"{LogPrefix}The launcher offers topic '{entry.Topic}' twice.");
                        problems++;
                    }
                }

                if (AnatomyTopicLibrary.LoadThumbnail(entry.Topic) == null) {
                    Debug.LogWarning($"{LogPrefix}Launcher card '{entry.Label}' has no picture. Run Kmax > Anatomy > Build > Launcher Thumbnails.");
                    problems++;
                }
            }

            return problems;
        }

        private static int Validate(string topicId) {
            if (Resources.Load<TextAsset>("Topics/" + topicId) == null) {
                Debug.Log($"{LogPrefix}'{topicId}': no topic data yet.");
                return 0;
            }

            AnatomyTopicData data = AnatomyTopicLibrary.LoadData(topicId);
            GameObject prefab = AnatomyTopicLibrary.LoadModelPrefab(topicId);
            if (data == null || prefab == null) {
                return 1;
            }

            AnatomyModel model = prefab.GetComponent<AnatomyModel>();
            int problems = 0;

            for (int i = 0; i < data.Structures.Count; i++) {
                string id = data.Structures[i].Id;
                if (model.FindStructure(id) == null) {
                    Debug.LogError($"{LogPrefix}'{topicId}': text is written for '{id}', which is not in the model.");
                    problems++;
                }
            }

            for (int i = 0; i < model.Structures.Count; i++) {
                string id = model.Structures[i].StructureId;
                if (data.FindStructure(id) == null) {
                    Debug.LogWarning($"{LogPrefix}'{topicId}': structure '{id}' has no text and will show its fallback name.");
                    problems++;
                }
            }

            int numbered = 0;
            for (int i = 0; i < data.Structures.Count; i++) {
                if (data.Structures[i].IsNumbered) {
                    numbered++;
                }
            }

            if (numbered > AnatomyInterfaceBuilder.MarkerCount) {
                Debug.LogError($"{LogPrefix}'{topicId}': {numbered} structures are numbered but there are only " +
                    $"{AnatomyInterfaceBuilder.MarkerCount} badges. Mark the smallest as unnumbered.");
                problems++;
            }

            for (int i = 0; i < data.Structures.Count; i++) {
                AnatomyStructureInfo info = data.Structures[i];
                if (info.OpensTopic && Resources.Load<TextAsset>("Topics/" + info.Topic) == null) {
                    Debug.LogError($"{LogPrefix}'{topicId}': '{info.Id}' opens topic '{info.Topic}', which has no data.");
                    problems++;
                }
            }

            for (int i = 0; i < data.Links.Count; i++) {
                if (Resources.Load<TextAsset>("Topics/" + data.Links[i].Topic) == null) {
                    Debug.LogError($"{LogPrefix}'{topicId}': the button '{data.Links[i].Label}' opens topic '{data.Links[i].Topic}', which has no data.");
                    problems++;
                }
            }

            for (int step = 0; step < data.Tour.Count; step++) {
                if (data.Tour[step].HasUnknownExplodeKey) {
                    Debug.LogError($"{LogPrefix}'{topicId}': tour step {step + 1} asks for an exploded view it does not know; " +
                        "use apart, together or nothing.");
                    problems++;
                }

                if (data.Tour[step].SetsZoom && data.Tour[step].Zoom > data.MaxZoom) {
                    Debug.LogWarning($"{LogPrefix}'{topicId}': tour step {step + 1} zooms to {data.Tour[step].Zoom}, " +
                        $"beyond the topic's maxZoom of {data.MaxZoom}; it will stop at the limit.");
                    problems++;
                }

                string[] ids = data.Tour[step].Structures;
                for (int i = 0; i < ids.Length; i++) {
                    if (model.FindStructure(ids[i]) == null) {
                        Debug.LogError($"{LogPrefix}'{topicId}': tour step {step + 1} names '{ids[i]}', which is not in the model.");
                        problems++;
                    }
                }
            }

            return problems;
        }
    }
}