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
        private const string ThumbnailFolder = "Anatomy/Thumbs/";
        private const string LauncherAsset = "Launcher/launcher";

        /// <summary>The launcher's wording and the exhibits it offers, or null after logging why they could not be read.</summary>
        public static AnatomyLauncherData LoadLauncher() {
            TextAsset asset = Resources.Load<TextAsset>(LauncherAsset);
            if (asset == null) {
                Debug.LogError($"[Anatomy] No launcher data. Expected Resources/{LauncherAsset}.json.");
                return null;
            }

            try {
                return JsonUtility.FromJson<AnatomyLauncherData>(asset.text);
            } catch (ArgumentException exception) {
                Debug.LogError($"[Anatomy] The launcher data has malformed JSON: {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// The picture on a topic's launcher card, or null when it has not been generated. Like the models it is made from the
        /// DOSCH pack, so it is generated locally and absent on a fresh clone; the card shows without one.
        /// </summary>
        public static Sprite LoadThumbnail(string topicId) {
            return Resources.Load<Sprite>(ThumbnailFolder + topicId);
        }

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

        /// <summary>
        /// Starts loading a topic's model in the background and returns the request. The first load of a heavy model from disk
        /// takes a good fraction of a second, which as a pause in the middle of a turning preview is felt; loading ahead of
        /// time moves that off the main thread, and the ordinary load afterwards finds it ready.
        /// </summary>
        public static ResourceRequest LoadModelPrefabAsync(string topicId) {
            return Resources.LoadAsync<GameObject>(ModelFolder + topicId);
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