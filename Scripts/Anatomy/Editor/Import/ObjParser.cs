using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// Reads the subset of Wavefront OBJ that anatomical models use: v, vt, vn, f, usemtl and mtllib.
    /// Polygons are triangulated as a fan and faces are grouped by the material in force when they were
    /// declared. Everything else is skipped on purpose.
    /// </summary>
    public static class ObjParser {
        private const string DefaultMaterialName = "default";
        private const int AbsentIndex = -1;
        private const int UnresolvedIndex = int.MinValue;

        private static readonly char[] TokenSeparators = new char[] { ' ', '\t' };
        private static readonly char[] IndexSeparator = new char[] { '/' };

        /// <summary>Parses the file, or logs and returns null when it cannot be read.</summary>
        public static ObjModel Parse(string path) {
            if (!File.Exists(path)) {
                Debug.LogError($"[Anatomy] OBJ not found: {path}");
                return null;
            }

            ObjModel model = new ObjModel();
            Dictionary<string, ObjMaterialGroup> groupsByName = new Dictionary<string, ObjMaterialGroup>();
            List<ObjCorner> face = new List<ObjCorner>(8);
            ObjMaterialGroup current = null;
            int malformedLines = 0;

            using (StreamReader reader = new StreamReader(path)) {
                string line = ReadLogicalLine(reader);
                while (line != null) {
                    string[] tokens = line.Split(TokenSeparators, System.StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length > 0 && !ReadLine(tokens, model, groupsByName, face, ref current)) {
                        malformedLines++;
                    }

                    line = ReadLogicalLine(reader);
                }
            }

            if (malformedLines > 0) {
                Debug.LogWarning($"[Anatomy] {Path.GetFileName(path)}: skipped {malformedLines} malformed line(s).");
            }

            return model;
        }

        /// <summary>
        /// Reads one logical line. OBJ lets a line end in a backslash to continue on the next physical
        /// line, and the DOSCH files use it for faces with more than a few corners.
        /// </summary>
        private static string ReadLogicalLine(StreamReader reader) {
            string line = reader.ReadLine();
            if (line == null || !EndsWithContinuation(line)) {
                return line;
            }

            StringBuilder joined = new StringBuilder(line.Length * 2);
            while (line != null && EndsWithContinuation(line)) {
                joined.Append(line.TrimEnd().TrimEnd('\\')).Append(' ');
                line = reader.ReadLine();
            }

            if (line != null) {
                joined.Append(line);
            }

            return joined.ToString();
        }

        private static bool EndsWithContinuation(string line) {
            int end = line.Length;
            while (end > 0 && char.IsWhiteSpace(line[end - 1])) {
                end--;
            }

            return end > 0 && line[end - 1] == '\\';
        }

        /// <summary>Applies one tokenised line. False means the line was recognised but unusable.</summary>
        private static bool ReadLine(string[] tokens, ObjModel model, Dictionary<string, ObjMaterialGroup> groupsByName,
            List<ObjCorner> face, ref ObjMaterialGroup current) {
            switch (tokens[0]) {
                case "v":
                    return TryAddVector3(tokens, model.Positions);
                case "vn":
                    return TryAddVector3(tokens, model.Normals);
                case "vt":
                    return TryAddVector2(tokens, model.Uvs);
                case "usemtl":
                    if (tokens.Length < 2) {
                        return false;
                    }

                    current = GetOrAddGroup(model, groupsByName, tokens[1]);
                    return true;
                case "mtllib":
                    if (tokens.Length < 2) {
                        return false;
                    }

                    model.MaterialLibrary = tokens[1];
                    return true;
                case "f":
                    if (current == null) {
                        current = GetOrAddGroup(model, groupsByName, DefaultMaterialName);
                    }

                    return TryAddFace(tokens, model, face, current);
                default:
                    return true;
            }
        }

        private static ObjMaterialGroup GetOrAddGroup(ObjModel model, Dictionary<string, ObjMaterialGroup> groupsByName, string name) {
            ObjMaterialGroup group;
            if (!groupsByName.TryGetValue(name, out group)) {
                group = new ObjMaterialGroup(name);
                groupsByName.Add(name, group);
                model.Groups.Add(group);
            }

            return group;
        }

        private static bool TryAddVector3(string[] tokens, List<Vector3> target) {
            float x;
            float y;
            float z;
            if (tokens.Length < 4 || !TryParseFloat(tokens[1], out x) ||
                !TryParseFloat(tokens[2], out y) || !TryParseFloat(tokens[3], out z)) {
                return false;
            }

            target.Add(new Vector3(x, y, z));
            return true;
        }

        private static bool TryAddVector2(string[] tokens, List<Vector2> target) {
            float u;
            float v;
            if (tokens.Length < 3 || !TryParseFloat(tokens[1], out u) || !TryParseFloat(tokens[2], out v)) {
                return false;
            }

            target.Add(new Vector2(u, v));
            return true;
        }

        private static bool TryAddFace(string[] tokens, ObjModel model, List<ObjCorner> face, ObjMaterialGroup group) {
            face.Clear();
            for (int i = 1; i < tokens.Length; i++) {
                ObjCorner corner;
                if (!TryParseCorner(tokens[i], model, out corner)) {
                    return false;
                }

                face.Add(corner);
            }

            if (face.Count < 3) {
                return false;
            }

            for (int i = 1; i < face.Count - 1; i++) {
                group.Corners.Add(face[0]);
                group.Corners.Add(face[i]);
                group.Corners.Add(face[i + 1]);
            }

            return true;
        }

        private static bool TryParseCorner(string token, ObjModel model, out ObjCorner corner) {
            string[] parts = token.Split(IndexSeparator);
            int position = ResolveIndex(parts[0], model.Positions.Count);
            int uv = parts.Length > 1 ? ResolveIndex(parts[1], model.Uvs.Count) : AbsentIndex;
            int normal = parts.Length > 2 ? ResolveIndex(parts[2], model.Normals.Count) : AbsentIndex;
            corner = new ObjCorner(position, uv, normal);

            return position >= 0 && position < model.Positions.Count
                && (uv == AbsentIndex || (uv >= 0 && uv < model.Uvs.Count))
                && (normal == AbsentIndex || (normal >= 0 && normal < model.Normals.Count));
        }

        /// <summary>
        /// Converts an OBJ index to a zero-based one. OBJ counts from 1, and a negative index counts back
        /// from the end of the pool as it stands when the face is read.
        /// </summary>
        private static int ResolveIndex(string text, int poolCount) {
            if (text.Length == 0) {
                return AbsentIndex;
            }

            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value == 0) {
                return UnresolvedIndex;
            }

            return value > 0 ? value - 1 : poolCount + value;
        }

        private static bool TryParseFloat(string text, out float value) {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}