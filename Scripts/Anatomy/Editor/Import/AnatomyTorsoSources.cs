using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The source files behind the two activities' torso: the organs, drawn from the high-resolution set, and the bones, the
    /// muscles and the vessels around them, from the low-resolution set, which is all a layer needs. The torso is cropped
    /// from the base of the neck to the pelvis by <see cref="AnatomyCropRegion.Torso"/>. Reproductive structures are
    /// excluded from the exhibit.
    /// </summary>
    public static class AnatomyTorsoSources {
        /// <summary>The organs, the puzzle's ten pieces first, then the ones only the scan shows.</summary>
        public static AnatomyTorsoOrganSpec[] Organs() {
            return new AnatomyTorsoOrganSpec[] {
                Piece("heart", "Cardio_Vascular/male_heart-hi.obj"),
                Piece("lung_left", "Cardio_Vascular/male_lung_L-hi.obj"),
                Piece("lung_right", "Cardio_Vascular/male_lung_R-hi.obj"),
                Piece("liver", "Digestive/male_liver-hi.obj"),
                Piece("stomach", "Digestive/male_stomach-hi.obj"),
                Piece("spleen", "Lymphatic/male_spleen-hi.obj"),
                Piece("kidney_left", "Skeleton/male_kidney_L-hi.obj"),
                Piece("kidney_right", "Skeleton/male_kidney_R-hi.obj"),
                Piece("small_intestine", "Digestive/male_small_intestine-hi.obj"),
                Piece("large_intestine", "Digestive/male_large_intestine-hi.obj"),
                Scanned("pancreas", "Endocrine/male_pancreas-hi.obj"),
                Scanned("gall_bladder", "Digestive/male_gall_bladder-hi.obj"),
                Scanned("esophagus", "Digestive/male_esophagus-hi.obj"),
                Scanned("windpipe", "Cardio_Vascular/male_trachea-hi.obj", "Cardio_Vascular/male_bronchus-hi.obj"),
                Scanned("bladder", "Skeleton/male_urinary_bladder-hi.obj"),
                Scanned("diaphragm", "Musculature/male_abdominal_diaphragm-hi.obj")
            };
        }

        /// <summary>The bones of the torso and the shoulders: the frame the organs hang in.</summary>
        public static AnatomyBodyLayerSpec Frame() {
            AnatomyBodyLayerSpec frame = new AnatomyBodyLayerSpec("frame", "Skeleton", BoneFiles());
            frame.Tint = new Color(0.78f, 0.88f, 1f, 1f);
            frame.Intensity = 0.5f;
            frame.RimPower = 2.2f;
            frame.Floor = 0.06f;
            return frame;
        }

        /// <summary>The bones as a list of files, for the scan, which draws them solid.</summary>
        public static string[] BoneFiles() {
            return new string[] {
                "Skeleton/male_ribcage-lo.obj",
                "Skeleton/male_thoracic_vertibrae-lo.obj",
                "Skeleton/male_lumbar_vertibrae-lo.obj",
                "Skeleton/male_cervical_vertibrae-lo.obj",
                "Skeleton/male_pelvis-lo.obj",
                "Skeleton/male_clavicleL-lo.obj",
                "Skeleton/male_clavicleR-lo.obj",
                "Skeleton/male_scapulaL-lo.obj",
                "Skeleton/male_scapulaR-lo.obj",
                "Skeleton/male_humerusL-lo.obj",
                "Skeleton/male_humerusR-lo.obj"
            };
        }

        /// <summary>The muscles that cover the torso, each file kept apart because each is painted on its own atlas.</summary>
        public static string[] MuscleFiles() {
            return new string[] {
                "Musculature/male_chest-lo.obj",
                "Musculature/male_abdominal_external-lo.obj",
                "Musculature/male_abdominal_internal-lo.obj",
                "Musculature/male_back_deep-lo.obj",
                "Musculature/male_back_intermediate-lo.obj",
                "Musculature/male_back_superficial-lo.obj",
                "Musculature/male_head_neck-lo.obj",
                "Musculature/male_shoulder_arm_musclesL-lo.obj",
                "Musculature/male_shoulder_arm_musclesR-lo.obj",
                "Musculature/male_pelvic_muscles-lo.obj"
            };
        }

        /// <summary>The arteries and veins of the torso, in the pack's own colours.</summary>
        public static string[] VesselFiles() {
            return new string[] {
                "Cardio_Vascular/male_arteries_head_thoracic-lo.obj",
                "Cardio_Vascular/male_arteries_torso-lo.obj",
                "Cardio_Vascular/male_arteries_armL-lo.obj",
                "Cardio_Vascular/male_arteries_armR-lo.obj",
                "Cardio_Vascular/male_veins_head_thoracic-lo.obj",
                "Cardio_Vascular/male_veins_torso-lo.obj",
                "Cardio_Vascular/male_veins_armL-lo.obj",
                "Cardio_Vascular/male_veins_armR-lo.obj"
            };
        }

        private static AnatomyTorsoOrganSpec Piece(string id, string sourceFile) {
            return new AnatomyTorsoOrganSpec(id, new string[] { sourceFile }, true);
        }

        private static AnatomyTorsoOrganSpec Scanned(string id, params string[] sourceFiles) {
            return new AnatomyTorsoOrganSpec(id, sourceFiles, false);
        }
    }
}