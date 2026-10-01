using System.Collections.Generic;
using System.Globalization;
using System.IO;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Reads Wavefront material libraries: newmtl, Kd, Ks, Ns and map_Kd. Illumination models and
    /// ambient colours are ignored because the target shader does not use them.
    /// </summary>
    public static class MtlParser {
        private static readonly char[] TokenSeparators = new char[] { ' ', '\t' };

        /// <summary>
        /// Parses every material in the file, keyed by name. A missing file logs a warning and yields an
        /// empty set, so structures still import with default colours.
        /// </summary>
        public static Dictionary<string, MtlMaterial> Parse(string path) {
            Dictionary<string, MtlMaterial> materials = new Dictionary<string, MtlMaterial>();
            if (!File.Exists(path)) {
                Debug.LogWarning($"[Anatomy] Material library not found: {path}. Structures will use default colours.");
                return materials;
            }

            string folder = Path.GetDirectoryName(path);
            MtlMaterial current = null;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++) {
                string[] tokens = lines[i].Split(TokenSeparators, System.StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 2) {
                    continue;
                }

                if (tokens[0] == "newmtl") {
                    current = new MtlMaterial(tokens[1]);
                    materials[current.Name] = current;
                    continue;
                }

                if (current != null) {
                    ApplyProperty(current, tokens, folder);
                }
            }

            return materials;
        }

        private static void ApplyProperty(MtlMaterial material, string[] tokens, string folder) {
            Color color;
            float value;
            switch (tokens[0]) {
                case "Kd":
                    if (TryReadColor(tokens, out color)) {
                        material.DiffuseColor = color;
                    }

                    break;
                case "Ks":
                    if (TryReadColor(tokens, out color)) {
                        material.SpecularColor = color;
                    }

                    break;
                case "Ns":
                    if (float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out value)) {
                        material.Shininess = value;
                    }

                    break;
                case "map_Kd":
                    material.DiffuseTexturePath = Path.Combine(folder, tokens[tokens.Length - 1]);
                    break;
            }
        }

        private static bool TryReadColor(string[] tokens, out Color color) {
            float r;
            float g;
            float b;
            color = Color.white;
            if (tokens.Length < 4 || !TryParseFloat(tokens[1], out r) ||
                !TryParseFloat(tokens[2], out g) || !TryParseFloat(tokens[3], out b)) {
                return false;
            }

            color = new Color(r, g, b, 1f);
            return true;
        }

        private static bool TryParseFloat(string text, out float value) {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}