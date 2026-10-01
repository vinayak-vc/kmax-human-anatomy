using System;

namespace ViitorCloud.KmaxAnatomy.Editor {
    /// <summary>
    /// One triangle corner as an OBJ face declares it: an index into each of the file's vertex pools,
    /// or -1 where the face does not supply one.
    /// </summary>
    public struct ObjCorner : IEquatable<ObjCorner> {
        public ObjCorner(int position, int uv, int normal) {
            Position = position;
            Uv = uv;
            Normal = normal;
        }

        public int Position { get; private set; }
        public int Uv { get; private set; }
        public int Normal { get; private set; }

        public bool Equals(ObjCorner other) {
            return Position == other.Position && Uv == other.Uv && Normal == other.Normal;
        }

        public override bool Equals(object obj) {
            return obj is ObjCorner && Equals((ObjCorner)obj);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = Position;
                hash = hash * 486187739 + Uv;
                hash = hash * 486187739 + Normal;
                return hash;
            }
        }
    }
}