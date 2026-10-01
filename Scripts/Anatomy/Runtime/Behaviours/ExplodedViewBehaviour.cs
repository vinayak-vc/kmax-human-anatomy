using System.Collections.Generic;

using UnityEngine;

namespace ViitorCloud.KmaxAnatomy {
    /// <summary>
    /// Pulls a model's structures apart and puts them back together. Each structure that the topic gives an offset
    /// slides from where it rests to that far away, all together and eased, so the parts can be seen side by side and
    /// the deep ones are no longer hidden behind the outer ones.
    ///
    /// <para>A structure may also be given a scale, for parts too small to see at their true size. Every structure
    /// with a scale grows at once, about the middle of them all, so they stay joined to one another exactly as they
    /// were: a chain of tiny bones becomes a chain of big bones, not a scatter of them.</para>
    ///
    /// <para>It moves and sizes the structures' transforms, and nothing else: their colliders, the markers' anchors and
    /// the depth keeper's measurement all follow, and the zoom's focus does too, because it reads the live
    /// positions.</para>
    ///
    /// <para>It switches itself off while nothing is moving.</para>
    /// </summary>
    public class ExplodedViewBehaviour : MonoBehaviour {
        private const float Settled = 0.0005f;

        [SerializeField, Tooltip("Seconds the structures take to move apart or back together.")]
        private float smoothTime = 0.5f;

        private Transform[] _parts;
        private StructureHighlight[] _highlights;
        private Vector3[] _rest;
        private Vector3[] _offsets;
        private float[] _scales;
        private Vector3 _pivot;
        private float _amount;
        private float _target;
        private float _velocity;

        /// <summary>Finds the structures the topic moves or enlarges, remembering where each rests.</summary>
        public void Begin(AnatomyModel model, AnatomyTopicData data) {
            List<Transform> parts = new List<Transform>();
            List<Vector3> offsets = new List<Vector3>();
            List<float> scales = new List<float>();
            for (int i = 0; i < data.Structures.Count; i++) {
                AnatomyStructureInfo info = data.Structures[i];
                if (!info.MovesWhenExploded) {
                    continue;
                }

                AnatomyStructure structure = model.FindStructure(info.Id);
                if (structure == null) {
                    Debug.LogWarning($"[Anatomy] '{data.Id}' explodes '{info.Id}', which the model does not have.", model);
                    continue;
                }

                parts.Add(structure.transform);
                offsets.Add(info.ExplodeOffset);
                scales.Add(info.ExplodeScale);
            }

            _parts = parts.ToArray();
            _offsets = offsets.ToArray();
            _scales = scales.ToArray();
            _rest = new Vector3[_parts.Length];
            _highlights = new StructureHighlight[_parts.Length];

            Vector3 enlargedSum = Vector3.zero;
            int enlarged = 0;
            for (int i = 0; i < _parts.Length; i++) {
                _rest[i] = _parts[i].localPosition;
                _highlights[i] = _parts[i].GetComponent<StructureHighlight>();
                if (_scales[i] > 1f) {
                    enlargedSum += _rest[i];
                    enlarged++;
                }
            }

            _pivot = enlarged > 0 ? enlargedSum / enlarged : Vector3.zero;
            enabled = false;
        }

        /// <summary>Moves the structures apart, or back together.</summary>
        public void SetExploded(bool exploded) {
            float target = exploded ? 1f : 0f;
            if (Mathf.Approximately(target, _target)) {
                return;
            }

            _target = target;
            enabled = true;
        }

        private void Update() {
            _amount = Mathf.SmoothDamp(_amount, _target, ref _velocity, smoothTime);
            if (Mathf.Abs(_amount - _target) < Settled && Mathf.Abs(_velocity) < Settled) {
                _amount = _target;
                _velocity = 0f;
                enabled = false;
            }

            for (int i = 0; i < _parts.Length; i++) {
                float size = Mathf.Lerp(1f, _scales[i], _amount);
                _parts[i].localPosition = _rest[i] + (_rest[i] - _pivot) * (size - 1f) + _offsets[i] * _amount;
                if (_scales[i] > 1f && _highlights[i] != null) {
                    _highlights[i].SetSize(size);
                }
            }
        }
    }
}