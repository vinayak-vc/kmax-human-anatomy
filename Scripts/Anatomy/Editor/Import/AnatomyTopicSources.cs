namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// The source files behind each topic. High-resolution meshes throughout: every topic is a single
    /// organ or organ system seen close up, where the low-resolution set is visibly faceted. The left
    /// side is used for paired organs. Reproductive structures are excluded from the exhibit.
    /// </summary>
    public static class AnatomyTopicSources {
        public static AnatomyImportSpec Heart() {
            AnatomyImportSpec spec = new AnatomyImportSpec("heart", new string[] {
                "Cardio_Vascular/male_heart-hi.obj"
            });
            spec.BakesHeartbeat = true;
            return spec;
        }

        public static AnatomyImportSpec Brain() {
            return new AnatomyImportSpec("brain", new string[] {
                "Nervous/male_brain-hi.obj"
            });
        }

        public static AnatomyImportSpec Ear() {
            AnatomyImportSpec spec = new AnatomyImportSpec("ear", new string[] {
                "Sensory/male_ear_inner_outer_L-hi.obj",
                "Sensory/male_ear_tympanic_membraneL-hi.obj",
                "Sensory/male_ear_ossiclesL-hi.obj",
                "Sensory/male_ear_cochleaL-hi.obj",
                "Sensory/male_ear_vestibulocochlear_nerveL-hi.obj"
            });
            spec.BakesHearing = true;
            return spec;
        }

        public static AnatomyImportSpec Eye() {
            AnatomyImportSpec spec = new AnatomyImportSpec("eye", new string[] {
                "Sensory/male_eye_sclera_optic_nerveL-hi.obj",
                "Sensory/male_eye_corneaL-hi.obj",
                "Sensory/male_eye_lensL-hi.obj",
                "Sensory/male_eye_musculatureL-hi.obj",
                "Sensory/male_eye_lacrimalL-hi.obj"
            });
            spec.BakesGaze = true;
            return spec;
        }

        public static AnatomyImportSpec Breathing() {
            AnatomyImportSpec spec = new AnatomyImportSpec("breathing", new string[] {
                "Cardio_Vascular/male_lung_L-hi.obj",
                "Cardio_Vascular/male_lung_R-hi.obj",
                "Cardio_Vascular/male_bronchus-hi.obj",
                "Cardio_Vascular/male_trachea-hi.obj",
                "Skeleton/male_ribcage-hi.obj",
                "Musculature/male_abdominal_diaphragm-hi.obj"
            });
            spec.BakesBreathing = true;
            return spec;
        }

        public static AnatomyImportSpec Skull() {
            AnatomyImportSpec spec = new AnatomyImportSpec("skull", new string[] {
                "Skeleton/male_skull_hi.obj",
                "Skeleton/male_jaw-hi.obj",
                "Skeleton/male_teeth_upper-hi.obj",
                "Skeleton/male_teeth_lower-hi.obj",
                "Musculature/male_face-hi.obj"
            });
            spec.BakesJaw = true;
            return spec;
        }

        public static AnatomyImportSpec[] All() {
            return new AnatomyImportSpec[] { Heart(), Brain(), Ear(), Eye(), Breathing(), Skull() };
        }
    }
}