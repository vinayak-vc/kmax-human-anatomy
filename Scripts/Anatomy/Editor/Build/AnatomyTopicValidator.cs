using UnityEditor;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Cross-checks every topic's text against its imported model, so a renamed structure or a typo in an id
    /// is reported here and not discovered as a silently blank caption in front of a visitor.
    /// </summary>
    public static class AnatomyTopicValidator {
        private const string LogPrefix = "[Anatomy] ";

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

            if (problems == 0) {
                Debug.Log($"{LogPrefix}Topic data is consistent with the models.");
            } else {
                Debug.LogWarning($"{LogPrefix}{problems} problem(s) found in topic data.");
            }
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