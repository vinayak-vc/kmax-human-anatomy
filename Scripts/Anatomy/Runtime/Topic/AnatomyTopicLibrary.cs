using System;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Finds a topic's data and model by id. Both live under Resources, so they are found the same way in
    /// the Editor and in a build. The model is generated from the DOSCH pack and is absent on a fresh
    /// clone; that is reported plainly rather than left to fail later.
    /// </summary>
    public static class AnatomyTopicLibrary {
        private const string DataFolder = "Topics/";
        private const string ModelFolder = "Anatomy/";

        /// <summary>The topic's data, or null after logging why it could not be read.</summary>
        public static AnatomyTopicData LoadData(string topicId) {
            TextAsset asset = Resources.Load<TextAsset>(DataFolder + topicId);
            if (asset == null) {
                Debug.LogError($"[Anatomy] No data for topic '{topicId}'. Expected Resources/{DataFolder}{topicId}.json.");
                return null;
            }

            try {
                return JsonUtility.FromJson<AnatomyTopicData>(asset.text);
            } catch (ArgumentException exception) {
                Debug.LogError($"[Anatomy] Topic '{topicId}' has malformed JSON: {exception.Message}");
                return null;
            }
        }

        /// <summary>The topic's imported model prefab, or null after logging how to generate it.</summary>
        public static GameObject LoadModelPrefab(string topicId) {
            GameObject prefab = Resources.Load<GameObject>(ModelFolder + topicId);
            if (prefab == null) {
                Debug.LogError($"[Anatomy] No model for topic '{topicId}'. Place the DOSCH pack in Source~ and run " +
                    "Kmax > Anatomy > Import > All Topics.");
            }

            return prefab;
        }
    }
}