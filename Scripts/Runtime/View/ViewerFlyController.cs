using UnityEngine;
using UnityEngine.EventSystems;

namespace ViitorCloud.KmaxDisplay {
    /// <summary>
    /// Spherical orbit camera navigation for the Kmax XRRig.
    /// Always maintains the focal target in the exact center of the screen
    /// across all angles and distances.
    ///
    /// - Mouse drag (Left or Right click): Orbits yaw and pitch around the center.
    /// - W / S: Flies forward / backward in orbit (dollies toward or away from the center).
    /// - A / D: Flies in an orbital circle left / right around the center.
    /// - Q / E: Flies in an orbital arc down / up around the center.
    /// - Arrow Keys: Orbit yaw (Left/Right) and pitch (Up/Down).
    /// - Left Shift: 2.5x flight speed boost.
    /// - Scroll Wheel: Dollies in / out toward the center.
    /// - R key / Reset button / Back button: Smoothly restores starting pose (0, 0, 0).
    /// </summary>
    public class ViewerFlyController : MonoBehaviour {
        [Header("Rig & Controller References")]
        [SerializeField, Tooltip("The XRRig root. Position and rotation are driven in orbit.")]
        private Transform rigRoot;
        [SerializeField, Tooltip("Exhibit controller notified on reset, so a focused part or a " +
            "swapped material clears with the camera. Must implement IViewResetHandler. Found on " +
            "this object when left empty; the camera simply resets itself when there is none.")]
        private MonoBehaviour resetHandlerSource;
        [SerializeField, Tooltip("Enable orbit flying for the XRRig.")]
        private bool enableFly = true;
        [SerializeField, Tooltip("Move the camera when dolly input arrives from the wheel, the W and S keys or " +
            "the stylus. Turn off when something else should answer that input, such as a zoom; it is still " +
            "reported through DollyInput.")]
        private bool applyDollyToCamera = true;

        [Header("Orbit Targets & Limits")]
        [SerializeField, Tooltip("Target point in world coordinates to orbit around.")]
        private Vector3 focalCenter = Vector3.zero;
        [SerializeField, Tooltip("Default distance between camera and focal center in metres.")]
        private float defaultDistance = 0.5f;
        [SerializeField, Tooltip("Minimum distance to focal center in metres.")]
        private float minDistance = 0.12f;
        [SerializeField, Tooltip("Maximum distance to focal center in metres.")]
        private float maxDistance = 1.2f;
        [SerializeField, Tooltip("Yaw the view rests at and returns to, in degrees.")]
        private float homeYaw;
        [SerializeField, Tooltip("Pitch the view rests at and returns to, in degrees. Positive looks " +
            "down on the model. Zero is level with its centre, which is right for something roughly " +
            "spherical and wrong for anything standing on a surface - a floor seen from dead level " +
            "is a horizontal line.")]
        private float homePitch;

        [Header("Speeds")]
        [SerializeField, Tooltip("Degrees turned per pixel of mouse drag.")]
        private float mouseOrbitSpeed = 0.25f;
        [SerializeField, Tooltip("Degrees turned per second with keyboard (A/D/Q/E/Arrows).")]
        private float keyOrbitSpeed = 80f;
        [SerializeField, Tooltip("Metres per second when flying forward/backward (W/S).")]
        private float keyDollySpeed = 0.35f;
        [SerializeField, Tooltip("Metres per mouse scroll wheel notch.")]
        private float scrollDollySpeed = 0.05f;
        [SerializeField, Tooltip("Speed boost multiplier when holding Shift.")]
        private float boostMultiplier = 2.5f;

        [Header("Pitch Clamps")]
        [SerializeField, Tooltip("Minimum pitch angle to prevent flipping upside down.")]
        private float minPitch = -80f;
        [SerializeField, Tooltip("Maximum pitch angle to prevent flipping upside down.")]
        private float maxPitch = 80f;

        [Header("Smoothing")]
        [SerializeField, Tooltip("Smooth damping duration for rotation.")]
        private float orbitSmoothTime = 0.06f;
        [SerializeField, Tooltip("Smooth damping duration for distance.")]
        private float dollySmoothTime = 0.08f;
        [SerializeField, Tooltip("Duration of animated reset in seconds.")]
        private float resetDuration = 0.45f;

        [Header("Keys")]
        [SerializeField] private KeyCode resetKey = KeyCode.R;
        [SerializeField] private KeyCode boostKey = KeyCode.LeftShift;

        private float _currentYaw;
        private float _currentPitch;
        private float _currentDistance;
        private float _targetYaw;
        private float _targetPitch;
        private float _targetDistance;

        private IViewResetHandler _resetHandler;

        private float _yawVelocity;
        private float _pitchVelocity;
        private float _distanceVelocity;

        private Vector2 _lastMousePosition;
        private bool _isPointerPressed;
        private bool _isPointerDragging;
        private float _dragThreshold = 4f;

        // One animated flight serves both the reset to home and the fly-to used when a part is
        // picked from a step-through control, so there is a single place that eases the camera.
        private bool _isFlying;
        private float _flightElapsed;
        private float _flightDuration;
        private float _flightFromYaw;
        private float _flightFromPitch;
        private float _flightFromDistance;
        private float _flightToYaw;
        private float _flightToPitch;
        private float _flightToDistance;

        /// <summary>
        /// Raised for every dolly input, from the wheel, the W and S keys and the stylus, in metres. Positive
        /// means closer. Raised whether or not the camera moves for it.
        /// </summary>
        public event System.Action<float> DollyInput;

        /// <summary>
        /// Whether the viewer may turn or dolly the camera. A scene whose interaction happens in the room's space, where the
        /// pen touches things where they appear, switches it off so nothing the pen or the mouse does moves the view. Switching
        /// it off ends any flight in progress where it stands.
        /// </summary>
        public bool EnableFly {
            get { return enableFly; }
            set {
                enableFly = value;
                if (!value) {
                    _isPointerPressed = false;
                    _isPointerDragging = false;
                    CancelFlight();
                }
            }
        }

        public Vector3 FocalCenter {
            get { return focalCenter; }
        }

        public float Distance {
            get { return _currentDistance; }
        }

        /// <summary>
        /// True while an animated flight is playing out. Attract mode reads this so its idle drift
        /// does not fight a flight in progress - any orbit delta cancels a flight, so drifting
        /// during one would cut every camera move short.
        /// </summary>
        public bool IsFlying {
            get { return _isFlying; }
        }

        private void Awake() {
            if (rigRoot == null) {
                Debug.LogError($"{nameof(ViewerFlyController)} on '{name}' has no {nameof(rigRoot)} assigned; flying is disabled.", this);
                enabled = false;
                return;
            }

            ResolveResetHandler();

            _currentYaw = homeYaw;
            _currentPitch = Mathf.Clamp(homePitch, minPitch, maxPitch);
            _currentDistance = defaultDistance;
            _targetYaw = _currentYaw;
            _targetPitch = _currentPitch;
            _targetDistance = defaultDistance;

            UpdateRigTransformImmediate();
        }

        /// <summary>
        /// Binds <see cref="_resetHandler"/> from the inspector field, or from this object when the
        /// field is empty. A component assigned to the field that does not implement
        /// <see cref="IViewResetHandler"/> is a wiring mistake, so it is reported rather than
        /// quietly ignored.
        /// </summary>
        private void ResolveResetHandler() {
            if (resetHandlerSource != null) {
                _resetHandler = resetHandlerSource as IViewResetHandler;
                if (_resetHandler == null) {
                    Debug.LogError($"{nameof(ViewerFlyController)} on '{name}' has " +
                        $"'{resetHandlerSource.GetType().Name}' assigned as its reset handler, but " +
                        $"that type does not implement {nameof(IViewResetHandler)}. Only the camera " +
                        "will be reset.", this);
                }

                return;
            }

            _resetHandler = GetComponent<IViewResetHandler>();
            resetHandlerSource = _resetHandler as MonoBehaviour;
        }

        private void Update() {
            if (Input.GetKeyDown(resetKey)) {
                RequestReset();
                return;
            }

            if (!enableFly) {
                return;
            }

            // Grabbing the view during a flight takes it over immediately. Without this, clicking
            // Next twice in a row or reaching for the model mid-reset feels like a dead control.
            if (_isFlying && WantsManualControl()) {
                CancelFlight();
            }

            if (_isFlying) {
                UpdateFlight();
            } else {
                UpdateMouseOrbit();
                UpdateKeyboardFlight();
                UpdateScrollDolly();
                ApplySmoothing();
            }

            ApplyRigTransform();
        }

        /// <summary>
        /// Sets a custom focal center to orbit around (e.g. when inspecting a focused part).
        /// </summary>
        /// <summary>
        /// Runs the scene's reset: the handler's if one is wired, this camera's own otherwise.
        ///
        /// A handler is expected to call <see cref="ResetView"/> itself, so wire the Reset button
        /// and the stylus reset button here rather than to <see cref="ResetView"/> directly -
        /// otherwise the camera returns home while the rest of the exhibit stays where it was.
        /// </summary>
        public void RequestReset() {
            if (_resetHandler != null) {
                _resetHandler.ResetToHome();
                return;
            }

            ResetView(true);
        }

        /// <summary>
        /// Replaces the reset handler at runtime, for a scene that builds its controller after
        /// this component has already woken. Pass null to fall back to a plain camera reset.
        /// </summary>
        public void SetResetHandler(IViewResetHandler handler) {
            _resetHandler = handler;
            resetHandlerSource = handler as MonoBehaviour;
        }

        public void SetFocalCenter(Vector3 worldCenter) {
            focalCenter = worldCenter;
        }

        /// <summary>
        /// Resets the focal center back to world origin.
        /// </summary>
        public void ResetFocalCenter() {
            focalCenter = Vector3.zero;
        }

        /// <summary>
        /// Smoothly or immediately restores the camera rig to the authored home position and rotation.
        /// </summary>
        public void ResetView(bool animated = true) {
            focalCenter = Vector3.zero;
            FlyTo(homeYaw, homePitch, defaultDistance, animated);
        }

        /// <summary>
        /// Eases the camera to an absolute orbit pose. Used by the reset and by the part navigator,
        /// which picks an angle that shows the chosen structure rather than whatever the viewer
        /// happened to be looking from.
        /// </summary>
        /// <param name="yaw">Target yaw in degrees.</param>
        /// <param name="pitch">Target pitch in degrees, clamped to the configured limits.</param>
        /// <param name="distance">Target distance from the focal centre in metres.</param>
        /// <param name="animated">False snaps straight there.</param>
        /// <param name="duration">Flight time in seconds. Negative uses the configured default.</param>
        public void FlyTo(float yaw, float pitch, float distance, bool animated = true, float duration = -1f) {
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            distance = Mathf.Clamp(distance, minDistance, maxDistance);

            _targetYaw = yaw;
            _targetPitch = pitch;
            _targetDistance = distance;

            _isPointerPressed = false;
            _isPointerDragging = false;

            if (animated) {
                _isFlying = true;
                _flightElapsed = 0f;
                _flightDuration = duration > 0f ? duration : resetDuration;
                _flightFromYaw = _currentYaw;
                _flightFromPitch = _currentPitch;
                _flightFromDistance = _currentDistance;
                _flightToYaw = yaw;
                _flightToPitch = pitch;
                _flightToDistance = distance;
            } else {
                _isFlying = false;
                _currentYaw = yaw;
                _currentPitch = pitch;
                _currentDistance = distance;
                _yawVelocity = 0f;
                _pitchVelocity = 0f;
                _distanceVelocity = 0f;
                UpdateRigTransformImmediate();
            }
        }

        /// <summary>
        /// Orbits by a relative amount from an external input source - the stylus drag.
        /// Any flight in progress yields immediately, because the viewer taking hold of the view
        /// should never have to wait for an animation to finish.
        /// </summary>
        public void AddOrbitDelta(float yawDegrees, float pitchDegrees) {
            if (!enableFly) {
                return;
            }

            CancelFlight();
            _targetYaw += yawDegrees;
            _targetPitch = Mathf.Clamp(_targetPitch + pitchDegrees, minPitch, maxPitch);
        }

        /// <summary>
        /// Dollies by a relative amount from an external input source. Positive moves closer,
        /// matching a pen pushed towards the display.
        /// </summary>
        public void AddDollyDelta(float metres) {
            if (!enableFly) {
                return;
            }

            RaiseDollyInput(metres);
            if (!applyDollyToCamera) {
                return;
            }

            CancelFlight();
            _targetDistance = Mathf.Clamp(_targetDistance - metres, minDistance, maxDistance);
        }

        private void RaiseDollyInput(float metres) {
            if (DollyInput != null && !Mathf.Approximately(metres, 0f)) {
                DollyInput(metres);
            }
        }

        /// <summary>
        /// Drops out of an animated flight and hands control back to the smoothed targets from
        /// wherever the camera currently is, so there is no snap.
        /// </summary>
        private void CancelFlight() {
            if (!_isFlying) {
                return;
            }

            _isFlying = false;
            _targetYaw = _currentYaw;
            _targetPitch = _currentPitch;
            _targetDistance = _currentDistance;
        }

        private void UpdateMouseOrbit() {
            // A press aimed at a scale handle belongs to that handle. The handle's drag threshold
            // is larger than this one, so without standing down here the view would start turning
            // before the drag was ever recognised.
            if (ViewDragGate.IsSuppressed) {
                _isPointerPressed = false;
                _isPointerDragging = false;
                return;
            }

            bool isHeld = Input.GetMouseButton(0) || Input.GetMouseButton(1);
            Vector2 mousePosition = Input.mousePosition;

            if (isHeld) {
                if (!_isPointerPressed) {
                    if (IsPointerOverUI()) {
                        return;
                    }

                    _isPointerPressed = true;
                    _isPointerDragging = false;
                    _lastMousePosition = mousePosition;
                } else {
                    Vector2 delta = mousePosition - _lastMousePosition;
                    if (!_isPointerDragging && delta.magnitude >= _dragThreshold) {
                        _isPointerDragging = true;
                    }

                    if (_isPointerDragging) {
                        _lastMousePosition = mousePosition;
                        _targetYaw += delta.x * mouseOrbitSpeed;
                        _targetPitch -= delta.y * mouseOrbitSpeed;
                        _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
                    }
                }
            } else {
                _isPointerPressed = false;
                _isPointerDragging = false;
            }
        }

        private void UpdateKeyboardFlight() {
            float forward = 0f;
            float strafe = 0f;
            float rise = 0f;

            if (Input.GetKey(KeyCode.W)) {
                forward += 1f;
            }

            if (Input.GetKey(KeyCode.S)) {
                forward -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) {
                strafe += 1f;
            }

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) {
                strafe -= 1f;
            }

            if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.UpArrow)) {
                rise += 1f;
            }

            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.DownArrow)) {
                rise -= 1f;
            }

            if (Mathf.Approximately(forward, 0f) && Mathf.Approximately(strafe, 0f) && Mathf.Approximately(rise, 0f)) {
                return;
            }

            float multiplier = Input.GetKey(boostKey) ? boostMultiplier : 1f;
            float dt = Time.deltaTime;

            float dollyMetres = forward * keyDollySpeed * multiplier * dt;
            RaiseDollyInput(dollyMetres);
            if (applyDollyToCamera) {
                _targetDistance -= dollyMetres;
                _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
            }

            _targetYaw += strafe * keyOrbitSpeed * multiplier * dt;
            _targetPitch += rise * keyOrbitSpeed * multiplier * dt;
            _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
        }

        private void UpdateScrollDolly() {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f)) {
                return;
            }

            if (IsPointerOverUI()) {
                return;
            }

            float dollyMetres = scroll * scrollDollySpeed;
            RaiseDollyInput(dollyMetres);
            if (applyDollyToCamera) {
                _targetDistance -= dollyMetres;
                _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
            }
        }

        private void ApplySmoothing() {
            float dt = Time.deltaTime;
            _currentYaw = Mathf.SmoothDamp(_currentYaw, _targetYaw, ref _yawVelocity, orbitSmoothTime, Mathf.Infinity, dt);
            _currentPitch = Mathf.SmoothDamp(_currentPitch, _targetPitch, ref _pitchVelocity, orbitSmoothTime, Mathf.Infinity, dt);
            _currentDistance = Mathf.SmoothDamp(_currentDistance, _targetDistance, ref _distanceVelocity, dollySmoothTime, Mathf.Infinity, dt);
        }

        private void UpdateFlight() {
            _flightElapsed += Time.deltaTime;
            float t = _flightDuration > 0f ? Mathf.Clamp01(_flightElapsed / _flightDuration) : 1f;
            float eased = Mathf.SmoothStep(0f, 1f, t);

            // Yaw is interpolated the short way round, so flying from 350 to 10 degrees crosses
            // zero rather than sweeping the long way back through the whole model.
            _currentYaw = Mathf.LerpAngle(_flightFromYaw, _flightToYaw, eased);
            _currentPitch = Mathf.Lerp(_flightFromPitch, _flightToPitch, eased);
            _currentDistance = Mathf.Lerp(_flightFromDistance, _flightToDistance, eased);

            _targetYaw = _currentYaw;
            _targetPitch = _currentPitch;
            _targetDistance = _currentDistance;

            if (t < 1f) {
                return;
            }

            _isFlying = false;
            _currentYaw = _flightToYaw;
            _currentPitch = _flightToPitch;
            _currentDistance = _flightToDistance;
            _targetYaw = _flightToYaw;
            _targetPitch = _flightToPitch;
            _targetDistance = _flightToDistance;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _distanceVelocity = 0f;
        }

        private void ApplyRigTransform() {
            if (rigRoot == null) {
                return;
            }

            Quaternion rot = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 pos = focalCenter + rot * new Vector3(0f, 0f, 0.5f - _currentDistance);
            rigRoot.SetPositionAndRotation(pos, rot);
        }

        private void UpdateRigTransformImmediate() {
            if (rigRoot == null) {
                return;
            }

            Quaternion rot = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 pos = focalCenter + rot * new Vector3(0f, 0f, 0.5f - _currentDistance);
            rigRoot.SetPositionAndRotation(pos, rot);
        }

        /// <summary>
        /// True when the viewer is reaching for the view with the mouse, the wheel or the flight
        /// keys, which should take precedence over any flight still playing out.
        ///
        /// The stylus does not need checking here: its input arrives through
        /// <see cref="AddOrbitDelta"/> and <see cref="AddDollyDelta"/>, which cancel the flight
        /// themselves.
        /// </summary>
        private bool WantsManualControl() {
            // Dolly input that something else answers does not move the camera, so it must not stop a flight.
            bool scrolled = applyDollyToCamera && !Mathf.Approximately(Input.mouseScrollDelta.y, 0f);
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || scrolled) {
                // A press on a button is aimed at that button, not at the view behind it.
                return !IsPointerOverUI();
            }

            bool dollyKeys = applyDollyToCamera && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S));
            return dollyKeys ||
                Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
                Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E) ||
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow);
        }

        /// <summary>
        /// True only when the pointer is over an actual interface element.
        ///
        /// Deliberately not <c>EventSystem.IsPointerOverGameObject</c>. That reports any object the
        /// event system hit, and once a model gains mesh colliders and the camera a physics
        /// raycaster, it is true whenever the pointer is anywhere on the model - which silently
        /// stopped mouse drag from orbiting over the very thing it is meant to turn.
        /// </summary>
        private bool IsPointerOverUI() {
            EventSystem events = EventSystem.current;
            if (events == null) {
                return false;
            }

            if (_pointerData == null) {
                _pointerData = new PointerEventData(events);
            }

            _pointerData.Reset();
            _pointerData.position = Input.mousePosition;

            RaycastScratch.Clear();
            events.RaycastAll(_pointerData, RaycastScratch);

            for (int i = 0; i < RaycastScratch.Count; i++) {
                GameObject hit = RaycastScratch[i].gameObject;
                if (hit != null && hit.GetComponentInParent<Canvas>() != null) {
                    return true;
                }
            }

            return false;
        }

        private static readonly System.Collections.Generic.List<RaycastResult> RaycastScratch =
            new System.Collections.Generic.List<RaycastResult>();
        private PointerEventData _pointerData;
    }
}