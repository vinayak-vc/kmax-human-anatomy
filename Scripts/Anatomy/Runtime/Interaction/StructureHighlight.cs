using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Draws one structure's emphasis, eased between states: brightness, glow, a small swell, and how solid it is.
    ///
    /// <para>A structure that recedes turns to glass. Its transparent twin is swapped in, an edge glow is drawn
    /// over it and its opacity falls, so whatever is being explained shows through the rest. The twin looks the
    /// same as the solid material at full opacity, so the swap cannot be seen.</para>
    ///
    /// <para>It works through property blocks, so the shared material assets are never touched, and it switches
    /// itself off while nothing is changing. On a stereo display a colour change alone reads as flat, so
    /// emphasis also swells the structure slightly - a change of size is a depth cue.</para>
    /// </summary>
    [RequireComponent(typeof(AnatomyStructure))]
    public class StructureHighlight : MonoBehaviour {
        private const float SettleThreshold = 0.002f;
        private const float FullOpacity = 1f;
        private const float DarkenedBrightness = 0.35f;
        private const float GlassPulseGain = 0.6f;
        private const float RestingRimStrength = 0.35f;
        private const int BodyMaterialIndex = 0;
        private const int RimMaterialIndex = 1;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color RimTint = new Color(0.72f, 0.92f, 1f, 1f);

        [SerializeField, Tooltip("Seconds an emphasis change takes to settle.")]
        private float blendTime = 0.2f;
        [SerializeField, Range(0f, 1f), Tooltip("How solid a structure is while it has receded. Lower is more see-through.")]
        private float ghostOpacity = 0.1f;
        [SerializeField, Range(0f, 1f), Tooltip("How solid a receded structure is while the pointer is on it.")]
        private float previewOpacity = 0.45f;
        [SerializeField, Range(0f, 1f), Tooltip("Strength of the glowing outline on a structure that has receded.")]
        private float rimStrength = 0.85f;

        private Renderer _renderer;
        private MaterialPropertyBlock _bodyBlock;
        private MaterialPropertyBlock _rimBlock;
        private Material[] _solidMaterials;
        private Material[] _glassMaterials;
        private Color _baseColor = Color.white;
        private Vector3 _restScale = Vector3.one;
        private Vector3 _importedScale = Vector3.one;
        private float _pulse;
        private float _restOpacity = FullOpacity;
        private float _recededSolidity = 1f;
        private float _recededGlow = 1f;
        private HighlightState _state = HighlightState.Normal;
        private Emphasis _emphasis;
        private bool _isGlass;

        public HighlightState State {
            get { return _state; }
        }

        /// <summary>
        /// Adds a highlight to a structure and gives it the resting look its topic's text asks for: how solid it rests, and how
        /// quietly it recedes. <paramref name="info"/> may be null for a structure the topic says nothing about.
        /// </summary>
        public static StructureHighlight Attach(AnatomyStructure structure, AnatomyStructureInfo info) {
            StructureHighlight highlight = structure.gameObject.AddComponent<StructureHighlight>();
            if (info != null) {
                highlight.SetRestOpacity(info.RestOpacity);
                highlight.SetRecededLook(info.RecededSolidity, info.RecededGlow);
            }

            return highlight;
        }

        private void Awake() {
            AnatomyStructure structure = GetComponent<AnatomyStructure>();
            _renderer = structure.StructureRenderer;
            _bodyBlock = new MaterialPropertyBlock();
            _rimBlock = new MaterialPropertyBlock();
            _restScale = transform.localScale;
            _importedScale = _restScale;

            Material solid = _renderer.sharedMaterial;
            if (solid != null && solid.HasProperty(BaseColorId)) {
                _baseColor = solid.GetColor(BaseColorId);
            }

            _solidMaterials = new Material[] { solid };
            if (structure.GhostMaterial != null && structure.RimMaterial != null) {
                _glassMaterials = new Material[] { structure.GhostMaterial, structure.RimMaterial };
            } else {
                Debug.LogWarning($"[Anatomy] '{name}' has no transparent material, so it will darken instead of " +
                    "turning to glass. Re-import the topic.", this);
            }

            _emphasis = TargetFor(HighlightState.Normal);
            Apply();
            enabled = false;
        }

        /// <summary>
        /// Sets how solid the structure is when nothing is being explained. Below one it rests as glass with a faint edge
        /// glow, and pointing at it or picking it makes it more solid, so a clear structure such as the cornea lets what
        /// lies behind it show. A structure without a transparent twin stays solid.
        /// </summary>
        public void SetRestOpacity(float opacity) {
            _restOpacity = _glassMaterials != null ? Mathf.Clamp01(opacity) : FullOpacity;
            _emphasis = TargetFor(_state);
            Apply();
        }

        /// <summary>
        /// Sets how this structure looks while it has receded, as shares of the usual: how solid it is and how bright its
        /// edge glow is. A structure with a great deal of edge, such as the ribs, is a bright tangle at the usual strength.
        /// </summary>
        public void SetRecededLook(float solidity, float glow) {
            _recededSolidity = Mathf.Max(0f, solidity);
            _recededGlow = Mathf.Max(0f, glow);
            _emphasis = TargetFor(_state);
            Apply();
        }

        /// <summary>Eases towards the emphasis for this state.</summary>
        public void SetState(HighlightState state) {
            if (state == _state) {
                return;
            }

            _state = state;
            enabled = true;
        }

        /// <summary>
        /// Sets how many times its imported size the structure rests at; emphasis swells it from there. A behaviour that
        /// enlarges a structure must do it here, or the next change of emphasis would put the size back.
        /// </summary>
        public void SetSize(float multiple) {
            _restScale = _importedScale * multiple;
            ApplyScale();
        }

        /// <summary>
        /// Adds a flash of glow on top of the current emphasis, for a behaviour that makes a structure light up. On a
        /// structure that has turned to glass the flash fills it with colour, because glass has no glow to brighten.
        /// </summary>
        public void SetPulse(float glow) {
            if (Mathf.Approximately(glow, _pulse)) {
                return;
            }

            _pulse = glow;
            Apply();
        }

        private void Update() {
            Emphasis target = TargetFor(_state);
            float step = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, blendTime));
            _emphasis = Emphasis.Lerp(_emphasis, target, step);

            if (_emphasis.IsCloseTo(target, SettleThreshold)) {
                _emphasis = target;
                enabled = false;
            }

            Apply();
        }

        private Emphasis TargetFor(HighlightState state) {
            bool canGhost = _glassMaterials != null;
            switch (state) {
                case HighlightState.Hovered:
                    return SolidEmphasis(1.12f, 0.28f, 1.03f, 0.35f);
                case HighlightState.Focused:
                    return SolidEmphasis(1.18f, 0.5f, 1.05f, 0.65f);
                case HighlightState.Dimmed:
                    return canGhost
                        ? new Emphasis(1f, 0f, 1f, ghostOpacity * _recededSolidity, rimStrength * _recededGlow)
                        : new Emphasis(DarkenedBrightness, 0f, 1f, FullOpacity, 0f);
                case HighlightState.Previewed:
                    return canGhost
                        ? new Emphasis(1.1f, 0.18f, 1.02f, previewOpacity, rimStrength * _recededGlow)
                        : new Emphasis(1f, 0.2f, 1.02f, FullOpacity, 0f);
                default:
                    return SolidEmphasis(1f, 0f, 1f, 0f);
            }
        }

        /// <summary>
        /// An emphasis for a structure that is being shown, not receded: as solid as at rest, plus this share of the way
        /// to fully solid. The edge glow is there only while it is not fully solid, so a structure that rests solid never
        /// shows it.
        /// </summary>
        private Emphasis SolidEmphasis(float brightness, float glow, float swell, float solidShare) {
            float opacity = Mathf.Lerp(_restOpacity, FullOpacity, solidShare);
            return new Emphasis(brightness, glow, swell, opacity, (FullOpacity - opacity) * RestingRimStrength);
        }

        private void Apply() {
            bool glass = _glassMaterials != null && _emphasis.Opacity < FullOpacity;
            if (glass != _isGlass) {
                _isGlass = glass;
                _renderer.sharedMaterials = glass ? _glassMaterials : _solidMaterials;
            }

            Color body = _baseColor * _emphasis.Brightness;
            body.a = glass ? Mathf.Clamp01(_emphasis.Opacity + _pulse * GlassPulseGain) : FullOpacity;
            _renderer.GetPropertyBlock(_bodyBlock, BodyMaterialIndex);
            _bodyBlock.SetColor(BaseColorId, body);
            _bodyBlock.SetColor(EmissionColorId, _baseColor * (_emphasis.Glow + _pulse));
            _renderer.SetPropertyBlock(_bodyBlock, BodyMaterialIndex);

            if (glass) {
                Color rim = Color.Lerp(_baseColor, RimTint, 0.65f);
                rim.a = _emphasis.Rim;
                _rimBlock.SetColor(BaseColorId, rim);
                _renderer.SetPropertyBlock(_rimBlock, RimMaterialIndex);
            }

            ApplyScale();
        }

        private void ApplyScale() {
            transform.localScale = _restScale * _emphasis.Swell;
        }

        /// <summary>One state's look: the values that are eased between states.</summary>
        private struct Emphasis {
            public readonly float Brightness;
            public readonly float Glow;
            public readonly float Swell;
            public readonly float Opacity;
            public readonly float Rim;

            public Emphasis(float brightness, float glow, float swell, float opacity, float rim) {
                Brightness = brightness;
                Glow = glow;
                Swell = swell;
                Opacity = opacity;
                Rim = rim;
            }

            public static Emphasis Lerp(Emphasis from, Emphasis to, float t) {
                return new Emphasis(
                    Mathf.Lerp(from.Brightness, to.Brightness, t),
                    Mathf.Lerp(from.Glow, to.Glow, t),
                    Mathf.Lerp(from.Swell, to.Swell, t),
                    Mathf.Lerp(from.Opacity, to.Opacity, t),
                    Mathf.Lerp(from.Rim, to.Rim, t));
            }

            public bool IsCloseTo(Emphasis other, float threshold) {
                return Mathf.Abs(Brightness - other.Brightness) < threshold
                    && Mathf.Abs(Glow - other.Glow) < threshold
                    && Mathf.Abs(Swell - other.Swell) < threshold
                    && Mathf.Abs(Opacity - other.Opacity) < threshold
                    && Mathf.Abs(Rim - other.Rim) < threshold;
            }
        }
    }
}