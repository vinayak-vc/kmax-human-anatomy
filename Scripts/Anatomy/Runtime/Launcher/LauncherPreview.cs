using System.Collections.Generic;

using UnityEngine;

using ViitorCloud.KmaxDisplay;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// The model that floats in front of the glass on the launcher: the chosen exhibit's own model, turning slowly on a turntable
    /// that leans a little towards the viewer, with a gentle bob. Choosing another exhibit lets the old one shrink away while the
    /// new one springs up in its place.
    ///
    /// <para>A preview is an instance of the topic's model, made when it is first chosen and kept while the launcher is up, so
    /// choosing an exhibit again costs nothing. It has no colliders, so the pen's beam and its pointer events pass through to the
    /// buttons, and it rests as the topic does (the breathing lungs as glass, for one). Where a topic's motion is only a motion,
    /// the preview runs it silently; otherwise the model turns still.</para>
    ///
    /// <para>Each model is scaled so that, however far round it has turned, it stays inside the depth budget and clear of the cards
    /// and the title: see <see cref="LauncherFit"/>. That holds by construction, so the depth keeper is not used here: it measures a
    /// mesh again for every swap, which on these models costs a tenth of a second each.</para>
    /// </summary>
    public class LauncherPreview : MonoBehaviour {
        [SerializeField, Tooltip("Height of the middle of the model above the middle of the screen, in metres.")]
        private float centreHeight = 0.038f;
        [SerializeField, Tooltip("Degrees a second the model turns.")]
        private float spinDegreesPerSecond = 22f;
        [SerializeField, Tooltip("Degrees the turntable leans towards the viewer, so the top of the model is seen a little.")]
        private float tiltDegrees = 8f;
        [SerializeField, Tooltip("The most a model may reach in front of or behind its middle as it turns, in metres. The middle sits " +
            "30 mm in front of the glass, so this keeps the nearest edge inside the pop-out limit.")]
        private float maxDepthRadius = 0.08f;
        [SerializeField, Tooltip("The most a model may reach above or below its middle, in metres: the room between the title and the cards.")]
        private float maxHalfHeight = 0.066f;
        [SerializeField, Tooltip("The most a model may reach to either side of its middle, in metres.")]
        private float maxHalfWidth = 0.15f;
        [SerializeField, Tooltip("How far the model rises and falls as it floats, in metres.")]
        private float bobMetres = 0.003f;
        [SerializeField, Tooltip("Radians a second of the bob.")]
        private float bobSpeed = 1.8f;
        [SerializeField, Range(0.2f, 0.95f), Tooltip("The size, as a share of full size, a model springs up from when it is chosen.")]
        private float popStartScale = 0.6f;
        [SerializeField, Range(4f, 40f), Tooltip("Stiffness of the spring a model pops up on.")]
        private float popFrequency = 16f;
        [SerializeField, Range(0.2f, 1.2f), Tooltip("Damping of that spring. Below one the model overshoots a little.")]
        private float popDamping = 0.55f;
        [SerializeField, Tooltip("Seconds a model takes to shrink away when another is chosen.")]
        private float retireSeconds = 0.18f;

        private readonly Dictionary<string, Preview> _previews = new Dictionary<string, Preview>();
        private readonly List<Preview> _retiring = new List<Preview>();
        private readonly List<Vector3> _outline = new List<Vector3>();
        private Preview _current;

        /// <summary>The id of the topic on the turntable, or null when there is none.</summary>
        public string CurrentTopicId {
            get { return _current != null ? _current.TopicId : null; }
        }

        /// <summary>The root of the model on the turntable, or null when there is none. For measuring where it is drawn.</summary>
        public Transform CurrentModel {
            get { return _current != null ? _current.Model : null; }
        }

        /// <summary>The scale the model on the turntable was fitted to, as a multiple of its real size.</summary>
        public float CurrentFitScale {
            get { return _current != null ? _current.FitScale : 0f; }
        }

        /// <summary>Puts this topic's model on the turntable, and lets the one that was there shrink away.</summary>
        public void Show(string topicId) {
            if (_current != null && _current.TopicId == topicId) {
                return;
            }

            if (_current != null) {
                _retiring.Add(_current);
                _current = null;
            }

            Preview next;
            if (!_previews.TryGetValue(topicId, out next)) {
                next = Create(topicId);
                if (next == null) {
                    return;
                }

                _previews[topicId] = next;
            }

            _retiring.Remove(next);
            next.PopScale = popStartScale;
            next.PopVelocity = 0f;
            next.Pivot.SetActive(true);
            _current = next;
        }

        /// <summary>Takes every model away.</summary>
        public void Clear() {
            foreach (KeyValuePair<string, Preview> pair in _previews) {
                Destroy(pair.Value.Pivot);
            }

            _previews.Clear();
            _retiring.Clear();
            _current = null;
        }

        private void Update() {
            float dt = Time.deltaTime;
            transform.localPosition = new Vector3(0f, centreHeight + Mathf.Sin(Time.time * bobSpeed) * bobMetres, 0f);

            if (_current != null) {
                UiSpring.Step(ref _current.PopScale, ref _current.PopVelocity, 1f, popFrequency, popDamping, dt);
                Turn(_current, dt);
            }

            for (int i = _retiring.Count - 1; i >= 0; i--) {
                Preview preview = _retiring[i];
                preview.PopScale -= retireSeconds > 0f ? dt / retireSeconds : 1f;
                if (preview.PopScale <= 0.02f) {
                    preview.Pivot.SetActive(false);
                    _retiring.RemoveAt(i);
                } else {
                    Turn(preview, dt);
                }
            }
        }

        /// <summary>Spins a preview on its turntable and sets its size from its fit and its spring.</summary>
        private void Turn(Preview preview, float deltaTime) {
            preview.Spin = Mathf.Repeat(preview.Spin + spinDegreesPerSecond * deltaTime, 360f);
            preview.Pivot.transform.localRotation = Quaternion.Euler(tiltDegrees, 0f, 0f) * Quaternion.AngleAxis(preview.Spin, Vector3.up);
            preview.Pivot.transform.localScale = Vector3.one * (preview.FitScale * Mathf.Max(0f, preview.PopScale));
        }

        /// <summary>Makes a quiet, fitted instance of a topic's model, or returns null after logging why it could not be made.</summary>
        private Preview Create(string topicId) {
            GameObject prefab = AnatomyTopicLibrary.LoadModelPrefab(topicId);
            AnatomyTopicData data = AnatomyTopicLibrary.LoadData(topicId);
            if (prefab == null || data == null) {
                return null;
            }

            GameObject pivot = new GameObject("Preview_" + topicId);
            pivot.transform.SetParent(transform, false);
            GameObject model = Instantiate(prefab, pivot.transform);
            model.name = topicId;

            // Nothing on the turntable is to be pointed at or picked.
            Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) {
                colliders[i].enabled = false;
            }

            AnatomyModel anatomy = model.GetComponent<AnatomyModel>();
            for (int i = 0; i < anatomy.Structures.Count; i++) {
                StructureHighlight.Attach(anatomy.Structures[i], data.FindStructure(anatomy.Structures[i].StructureId));
            }

            // Measured before any behaviour adds its lines, which are not part of the model's shape.
            Vector3 centre;
            float radius;
            float halfHeight;
            Measure(pivot.transform, model, out centre, out radius, out halfHeight);
            AnatomyBehaviours.AttachForPreview(model, data.Behaviour);

            Preview preview = new Preview();
            preview.TopicId = topicId;
            preview.Pivot = pivot;
            preview.Model = model.transform;
            preview.FitScale = LauncherFit.ScaleFor(radius, halfHeight, tiltDegrees, maxDepthRadius, maxHalfHeight, maxHalfWidth);
            model.transform.localPosition = -centre;
            pivot.transform.localScale = Vector3.one * (preview.FitScale * popStartScale);
            return preview;
        }

        /// <summary>
        /// The middle of a model, its horizontal reach from that middle and half its height, at real size, from every vertex of
        /// every drawn part: the true outline, which is what the model must stay inside as it turns.
        /// </summary>
        private void Measure(Transform pivot, GameObject model, out Vector3 centre, out float radius, out float halfHeight) {
            _outline.Clear();
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++) {
                if (!(renderers[i] is MeshRenderer) && !(renderers[i] is SkinnedMeshRenderer)) {
                    continue;
                }

                LauncherFit.AppendOutline(renderers[i], pivot.worldToLocalMatrix * renderers[i].transform.localToWorldMatrix, _outline);
            }

            if (_outline.Count == 0) {
                centre = Vector3.zero;
                radius = 0.1f;
                halfHeight = 0.1f;
                return;
            }

            Vector3 low = _outline[0];
            Vector3 high = _outline[0];
            for (int i = 1; i < _outline.Count; i++) {
                low = Vector3.Min(low, _outline[i]);
                high = Vector3.Max(high, _outline[i]);
            }

            centre = (low + high) * 0.5f;
            halfHeight = (high.y - low.y) * 0.5f;
            radius = 0f;
            for (int i = 0; i < _outline.Count; i++) {
                float x = _outline[i].x - centre.x;
                float z = _outline[i].z - centre.z;
                radius = Mathf.Max(radius, Mathf.Sqrt(x * x + z * z));
            }

            // A model's outline runs to hundreds of thousands of points, and is not needed again.
            _outline.Clear();
            _outline.TrimExcess();
        }

        /// <summary>One model on the turntable, and where it is in its turn and its spring.</summary>
        private class Preview {
            public string TopicId;
            public GameObject Pivot;
            public Transform Model;
            public float FitScale;
            public float Spin;
            public float PopScale;
            public float PopVelocity;
        }
    }
}