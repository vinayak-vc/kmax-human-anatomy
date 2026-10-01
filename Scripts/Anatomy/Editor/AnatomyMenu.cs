using UnityEditor;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>Menu entry points for the anatomy content pipeline.</summary>
    public static class AnatomyMenu {
        private const string ImportRoot = "Kmax/Anatomy/Import/";

        [MenuItem(ImportRoot + "Heart")]
        private static void ImportHeart() {
            AnatomyModelImporter.Import(AnatomyTopicSources.Heart());
        }

        [MenuItem(ImportRoot + "Brain")]
        private static void ImportBrain() {
            AnatomyModelImporter.Import(AnatomyTopicSources.Brain());
        }

        [MenuItem(ImportRoot + "Ear")]
        private static void ImportEar() {
            AnatomyModelImporter.Import(AnatomyTopicSources.Ear());
        }

        [MenuItem(ImportRoot + "Eye")]
        private static void ImportEye() {
            AnatomyModelImporter.Import(AnatomyTopicSources.Eye());
        }

        [MenuItem(ImportRoot + "Breathing")]
        private static void ImportBreathing() {
            AnatomyModelImporter.Import(AnatomyTopicSources.Breathing());
        }

        [MenuItem(ImportRoot + "Skull and Face")]
        private static void ImportSkull() {
            AnatomyModelImporter.Import(AnatomyTopicSources.Skull());
        }

        [MenuItem(ImportRoot + "Body Map")]
        private static void ImportBody() {
            AnatomyBodyImporter.Import();
        }

        [MenuItem(ImportRoot + "Organ Puzzle")]
        private static void ImportOrgans() {
            AnatomyTorsoImporter.ImportOrgans();
        }

        [MenuItem(ImportRoot + "Scan Figure")]
        private static void ImportScan() {
            AnatomyTorsoImporter.ImportScan();
        }

        [MenuItem(ImportRoot + "All Topics")]
        private static void ImportAllTopics() {
            AnatomyImportSpec[] specs = AnatomyTopicSources.All();
            try {
                for (int i = 0; i < specs.Length; i++) {
                    EditorUtility.DisplayProgressBar("Importing anatomy", specs[i].ModelId, (float)i / (specs.Length + 3));
                    AnatomyModelImporter.Import(specs[i]);
                }

                EditorUtility.DisplayProgressBar("Importing anatomy", AnatomyBodyImporter.ModelId, (float)specs.Length / (specs.Length + 3));
                AnatomyBodyImporter.Import();
                EditorUtility.DisplayProgressBar("Importing anatomy", AnatomyTorsoImporter.OrgansModelId, (float)(specs.Length + 1) / (specs.Length + 3));
                AnatomyTorsoImporter.ImportOrgans();
                EditorUtility.DisplayProgressBar("Importing anatomy", AnatomyTorsoImporter.ScanModelId, (float)(specs.Length + 2) / (specs.Length + 3));
                AnatomyTorsoImporter.ImportScan();
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}