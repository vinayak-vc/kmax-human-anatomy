using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The source files behind the body map. The low-resolution set throughout: the bust is seen from a distance and
    /// every structure of a layer is merged into one mesh, where the high-resolution set would be several million
    /// triangles. Reproductive structures are excluded from the exhibit, and so are the legs, which the bust crop
    /// would only discard.
    /// </summary>
    public static class AnatomyBodySources {
        /// <summary>The see-through layers, back to front as the layer buttons list them.</summary>
        public static AnatomyBodyLayerSpec[] Layers() {
            AnatomyBodyLayerSpec skeleton = new AnatomyBodyLayerSpec("skeleton", "Skeleton", new string[] {
                "Skeleton/male_skeleton_full-lo.obj"
            });
            skeleton.Tint = new Color(0.78f, 0.88f, 1f, 1f);
            skeleton.Swatch = skeleton.Tint;
            skeleton.Intensity = 0.55f;
            skeleton.RimPower = 2.2f;
            skeleton.Floor = 0.06f;

            AnatomyBodyLayerSpec muscles = new AnatomyBodyLayerSpec("muscles", "Muscles", new string[] {
                "Musculature/male_chest-lo.obj",
                "Musculature/male_abdominal_external-lo.obj",
                "Musculature/male_abdominal_internal-lo.obj",
                "Musculature/male_abdominal_diaphragm-lo.obj",
                "Musculature/male_back_deep-lo.obj",
                "Musculature/male_back_intermediate-lo.obj",
                "Musculature/male_back_superficial-lo.obj",
                "Musculature/male_head_neck-lo.obj",
                "Musculature/male_face-lo.obj",
                "Musculature/male_shoulder_arm_musclesL-lo.obj",
                "Musculature/male_shoulder_arm_musclesR-lo.obj"
            });
            muscles.Tint = new Color(1f, 0.42f, 0.3f, 1f);
            muscles.Swatch = muscles.Tint;
            muscles.Intensity = 0.5f;
            muscles.RimPower = 2f;
            muscles.Floor = 0.12f;
            muscles.StartsShown = false;

            AnatomyBodyLayerSpec organs = new AnatomyBodyLayerSpec("organs", "Organs", new string[] {
                "Digestive/male_esophagus-lo.obj",
                "Digestive/male_gall_bladder-lo.obj",
                "Digestive/male_large_intestine-lo.obj",
                "Digestive/male_liver-lo.obj",
                "Digestive/male_small_intestine-lo.obj",
                "Digestive/male_stomach-lo.obj",
                "Digestive/male_tongue-lo.obj",
                "Endocrine/male_adrenal_glands-lo.obj",
                "Endocrine/male_pancreas-lo.obj",
                "Endocrine/male_pineal_gland-lo.obj",
                "Endocrine/male_pituitary_gland-lo.obj",
                "Endocrine/male_thyroid_gland-lo.obj",
                "Lymphatic/male_spleen-lo.obj",
                "Lymphatic/male_thymus-lo.obj",
                "Lymphatic/male_tonsils-lo.obj",
                "Skeleton/male_kidney_L-lo.obj",
                "Skeleton/male_kidney_R-lo.obj",
                "Skeleton/male_ureter_L-lo.obj",
                "Skeleton/male_ureter_R-lo.obj",
                "Cardio_Vascular/male_bronchus-lo.obj",
                "Cardio_Vascular/male_trachea-lo.obj"
            });
            organs.Tint = new Color(1f, 0.78f, 0.45f, 1f);
            organs.Swatch = organs.Tint;
            organs.Intensity = 0.6f;
            organs.RimPower = 2f;
            organs.Floor = 0.12f;

            AnatomyBodyLayerSpec vessels = new AnatomyBodyLayerSpec("vessels", "Vessels", new string[] {
                "Cardio_Vascular/male_arteries_head_thoracic-lo.obj",
                "Cardio_Vascular/male_arteries_torso-lo.obj",
                "Cardio_Vascular/male_arteries_armL-lo.obj",
                "Cardio_Vascular/male_arteries_armR-lo.obj",
                "Cardio_Vascular/male_veins_head_thoracic-lo.obj",
                "Cardio_Vascular/male_veins_torso-lo.obj",
                "Cardio_Vascular/male_veins_armL-lo.obj",
                "Cardio_Vascular/male_veins_armR-lo.obj"
            });
            vessels.Tint = Color.white;
            vessels.Swatch = new Color(1f, 0.4f, 0.38f, 1f);
            vessels.Intensity = 0.85f;
            vessels.RimPower = 1.5f;
            vessels.Floor = 0.35f;
            vessels.StartsShown = false;
            vessels.UsesSourceColours = true;

            AnatomyBodyLayerSpec nerves = new AnatomyBodyLayerSpec("nerves", "Nerves", new string[] {
                "Nervous/male_spinal_cord-lo.obj",
                "Nervous/male_nerves_armL-lo.obj",
                "Nervous/male_nerves_armR-lo.obj"
            });
            nerves.Tint = new Color(1f, 0.92f, 0.45f, 1f);
            nerves.Swatch = nerves.Tint;
            nerves.Intensity = 0.9f;
            nerves.RimPower = 1.5f;
            nerves.Floor = 0.35f;
            nerves.StartsShown = false;

            return new AnatomyBodyLayerSpec[] { skeleton, muscles, organs, vessels, nerves };
        }

        /// <summary>
        /// The organs that lead into topics. A small organ is reached through a sphere larger than itself, measured from the
        /// pack: the middle of the organ's own file, moved forward where a neighbour would otherwise be hit first. A large one,
        /// such as the brain or a lung, is its own target.
        /// </summary>
        public static AnatomyBodyRegionSpec[] Regions() {
            return new AnatomyBodyRegionSpec[] {
                Surface("brain", new string[] { "Nervous/male_brain-lo.obj" }),
                Sphere("eye_left", new string[] { "Sensory/male_eye_sclera_optic_nerveL-lo.obj" }, 0.030f, 1.708f, -0.075f, 0.026f),
                Sphere("eye_right", new string[] { "Sensory/male_eye_sclera_optic_nerveR-lo.obj" }, -0.030f, 1.708f, -0.075f, 0.026f),
                Sphere("ear_left", new string[] { "Sensory/male_ear_inner_outer_L-lo.obj" }, 0.078f, 1.685f, 0.018f, 0.034f),
                Sphere("ear_right", new string[] { "Sensory/male_ear_inner_outer_R-lo.obj" }, -0.078f, 1.685f, 0.018f, 0.034f),
                Sphere("face", new string[] {
                    "Skeleton/male_jaw-lo.obj",
                    "Skeleton/male_teeth_upper-lo.obj",
                    "Skeleton/male_teeth_lower-lo.obj"
                }, 0f, 1.638f, -0.07f, 0.048f),
                Sphere("heart", new string[] { "Cardio_Vascular/male_heart-lo.obj" }, 0.006f, 1.397f, -0.022f, 0.058f),
                Surface("lung_left", new string[] { "Cardio_Vascular/male_lung_L-lo.obj" }),
                Surface("lung_right", new string[] { "Cardio_Vascular/male_lung_R-lo.obj" })
            };
        }

        private static AnatomyBodyRegionSpec Sphere(string id, string[] sourceFiles, float x, float up, float back, float radius) {
            AnatomyBodyRegionSpec spec = new AnatomyBodyRegionSpec(id, sourceFiles);
            spec.HotspotCentre = new Vector3(x, up, back);
            spec.HotspotRadius = radius;
            return spec;
        }

        private static AnatomyBodyRegionSpec Surface(string id, string[] sourceFiles) {
            return new AnatomyBodyRegionSpec(id, sourceFiles);
        }
    }
}