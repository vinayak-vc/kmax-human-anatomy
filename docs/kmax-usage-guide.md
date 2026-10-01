# Kmax Display — Usage Guide

How to build a scene on this module. Read this before writing any scene code.

---

## 1. What a Kmax display actually is

A Kmax is a glasses-based stereo display with **head tracking** and a **6-DOF stylus**. Three
consequences drive every design decision in this module:

1. **The frustum converges on the viewer's tracked eyes.** A rendered object at a given depth and
   the physical pen tip occupy the same point in the room. Co-located interaction — touching a
   thing with the pen rather than pointing a ray at it — is therefore possible, and is what the
   hardware is for.
2. **There is a comfortable depth budget and it is small.** Roughly **0.13 m of pop-out** in front
   of the glass and **0.30 m of depth** behind it, both scaled by `XRRig.ViewScale`. Content
   outside that budget does not fuse, and the viewer gets a headache rather than an error message.
   Never hardcode those numbers — read them from `StereoVolume`.
3. **The screen edge clips pop-out.** An object in front of the glass that crosses the frame edge
   is visible to one eye and not the other. It breaks fusion far more violently than depth does.

---

## 2. Choose an SDK backend first

Two Kmax SDKs are vendored under `Plugins/Kmax/`. **They both declare the `KmaxXR` namespace and
share type names, so only one can compile at a time.** Their assembly definitions are constrained
on the `KMAX_AIO_K1` scripting define:

| Backend | Define | Assemblies | Platforms |
|---|---|---|---|
| **XR Core 2.5.2** (default) | absent | `KmaxXR.Core`, `Kmax.InputModule` | Windows, Android, Linux, WebGL |
| **AIO K1 1.2.0** | `KMAX_AIO_K1` | `Kmax.XR` | Windows Standalone, Windows Store |

Switch with the menu: **Kmax → SDK Backend → XR Core 2.5.2** / **AIO K1 1.2.0**. The menu writes the
define across every supported build target and Unity recompiles.

> The menu lives in its own unconstrained assembly (`KmaxDisplay.SdkBackend.Editor`) on purpose.
> If it were constrained like everything else, switching to AIO K1 would delete the menu that
> switches back.

`KmaxDisplay` and `KmaxDisplay.Editor` are constrained to `!KMAX_AIO_K1` — **this module targets
XR Core**. Under the AIO backend it simply does not compile in, which is correct: none of its code
would resolve.

---

## 3. Build a scene

Everything below is driven from `KmaxRigBuilder` (editor-only). Write a scene builder rather than
authoring by hand, so the scene can be rebuilt from scratch and two scenes cannot drift apart.

```csharp
using UnityEditor;
using UnityEngine;
using ViitorCloud.KmaxDisplay;
using ViitorCloud.KmaxDisplay.Editor;

public static class MyExhibitBuilder {
    private const string ScenePath = "Assets/Games/kmax-human-anatomy/Scenes/MyExhibit.unity";

    [MenuItem("Kmax/Build/My Exhibit")]
    private static void Build() {
        KmaxRigBuilder.EnsureScene(ScenePath);

        GameObject rig = KmaxRigBuilder.EnsureRig();
        KmaxRigBuilder.ConfigureCamera(rig, new Color(0.05f, 0.06f, 0.08f, 1f));
        KmaxRigBuilder.EnsureEventSystem();

        StylusTip tip = KmaxRigBuilder.EnsureTip(rig, 0.004f, true);
        KmaxRigBuilder.EnsureDiagnostics(tip);

        // ... your own content here ...

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
    }
}
```

What each call does:

- **`EnsureScene`** — opens the scene, creating it and its folder if absent.
- **`EnsureRig`** — instantiates the SDK's `XRRig.prefab` (camera with `VRRenderer` and
  `HeadTracker`, its two sub-cameras, the pen with `PenTracker` and `KmaxStylus`) and unpacks it so
  the scene owns it.
- **`ConfigureCamera`** — solid background, near plane `0.02`, far plane `10`.
- **`EnsureEventSystem`** — an `EventSystem` running `KmaxInputModule`, with
  `StandaloneInputModule` removed. **Two input modules dispatch every press twice.**
- **`EnsureTip`** — a `StylusTip` (plus `StylusHaptics`, optionally `StylusGrab`) parented under
  the rig.
- **`EnsureDiagnostics`** — the `ComfortOverlay` and the scene-view `StereoVolumeGizmo`.

Also available: `EnsureLitMaterial`, `EnsureUnlitMaterial`, `EnsureFolder`, `FindOrCreateRoot`,
`FindOrCreateChild`, and the `SetReference` / `SetFloat` / `SetInt` / `SetBool` serialized-field
helpers, which log loudly when a field name no longer exists rather than silently skipping it.

**Every helper converges on the spec.** None returns early because the object already exists — it
is found and then re-applied. A build step that hands back an existing object untouched while
logging success is the most expensive class of bug in this code's history.

---

## 4. Stay inside the comfort budget

### Read the budget, never type it

`StereoVolume` (static, no scene wiring) derives the budget live from `XRRig` and
`StereoCamera.DefaultDistance`, so it stays correct when `ViewScale` changes at runtime.

Its own space is the rig transform's, and **+Z points away from the viewer, into the screen** —
negative Z is pop-out, positive Z is depth.

```csharp
if (!StereoVolume.IsReady) {
    return; // no XRRig in the scene
}

float depth = StereoVolume.DepthOf(worldPoint);    // signed, metres, relative to the glass
bool inside = StereoVolume.Contains(worldPoint);
Vector3 safe = StereoVolume.ClampToComfort(worldPoint);   // nearest comfortable point
```

Place content with `StereoVolume`, clamp anything the viewer can move with it (see
`Grabbable.clampToComfortVolume`), and keep pop-out away from the frame edge.

### Audit before you ship

**Kmax → Audit Comfort Volume** (`KmaxComfortValidator`) walks the open scene's renderers and
reports what sits outside the budget. Run it after every scene change; a scene that looks fine on a
2D monitor can be unusable through the glasses.

### Watch it live

`ComfortOverlay` draws the budget and the tip's position at runtime. Leave it in the scene with
`visibleOnStart` off and toggle it while testing.

---

## 5. Stylus input — two models, pick per interaction

### Co-located touch (`StylusTip`)

Treats the pen as a **finger**: a tracked point with a position, a radius and a velocity that
interacts with whatever it is physically inside. Contact is found with
`Physics.OverlapSphereNonAlloc` each frame — **not** trigger callbacks, which tunnel through thin
colliders at hand speed and report enter/exit out of order.

> **`StylusTip.tipOffset` defaults to zero, which is almost certainly wrong.** It is the gap between
> the pose the SDK reports and where the physical point of the pen actually is. Measure it on the
> hardware. Every co-located interaction is only as accurate as this number.

Pair with `Grabbable` and `StylusGrab` to pick things up. Held objects stay **dynamic, not
kinematic**: `StylusGrab` drives the body by velocity and lets physics resolve contact, so a held
object can be pushed off the hand by something solid — which is correct, and reads as weight.

### Ray pointing (`StylusBeam`, `StylusNavigation`)

Treats the pen as a **3D mouse**. Use it for UI, for distant objects, and for navigation.

`StylusBeam` implements the SDK's `IPointerVisualize` and replaces `StylusRay`; it colours the beam
by what the ray hit.

`StylusNavigation` maps the pen's three buttons onto `ViewerFlyController`:

| Index | Button | Action |
|---|---|---|
| 0 | front / primary | press to select; press and drag to orbit |
| 1 | secondary | tap to reset the view |
| 2 | centre | hold, then push/pull the pen to dolly |

Without it the stylus moves the pointer but cannot navigate — `ViewerFlyController` reads
`Input.GetMouseButton`, which a 6-DOF pen never sets. Both paths stay live, so mouse and stylus
behave identically.

`StylusHaptics` gives the pen a pulse on contact, rate-limited to about one pulse per quarter
second; faster reads as a buzz rather than a touch.

---

## 6. Camera and view state

`ViewerFlyController` is a spherical orbit camera that keeps the focal target dead centre at every
angle and distance. Mouse drag orbits, `W`/`S` dolly, `A`/`D`/`Q`/`E` and the arrow keys orbit,
scroll dollies, `Shift` boosts, `R` resets.

### Reset

Wire your Reset button and the stylus reset to **`ViewerFlyController.RequestReset()`**, not to
`ResetView()`. `RequestReset` calls your `IViewResetHandler` if one is wired, so the focused part,
the swapped material and the open panel clear along with the camera; it falls back to a plain
camera reset when nothing implements it.

```csharp
public class MyExhibitController : MonoBehaviour, IViewResetHandler {
    [SerializeField] private ViewerFlyController viewer;

    public void ResetToHome() {
        ClearFocus();
        RestoreMaterials();
        viewer.ResetView(true);   // the handler owns the camera move
    }
}
```

Assign the controller to the camera's `resetHandlerSource` field, or leave it empty and put both
components on the same GameObject.

### Stopping the view from fighting a drag

A press aimed at a manipulator belongs to that manipulator. Hold `ViewDragGate` while you own the
pointer:

```csharp
private void Update() {
    ViewDragGate.Set(this, isDragging || IsHandleHovered());
}

private void OnDisable() {
    // Never leave the view frozen because this was switched off mid-drag.
    ViewDragGate.Set(this, false);
}
```

Holds are counted per owner, so two manipulators active at once do not release each other. A static
gate rather than a reference, so a handle written next year suppresses the view without
`ViewerFlyController` being edited.

### Answering dolly input yourself

Dollying the camera moves content through the comfort volume, which is rarely what an exhibit wants. The wheel,
the W and S keys and the stylus's push and pull (button 2, `enableDolly` on `StylusNavigation`) all arrive as
`ViewerFlyController.DollyInput`, in metres, positive meaning closer. Turn `applyDollyToCamera` off and the camera
ignores them, and whatever subscribes can answer instead. The anatomy exhibit zooms the model (`AnatomyZoom`).
A pen press that lands on the UI does not start the gesture.

---

## 7. UI on a stereo display

- **`UiAlwaysOnTop`** forces a UI subtree to render over the scene. Necessary because a world-space
  canvas at the wrong depth gets buried in geometry or, worse, intersects it — an interpenetrating
  UI panel is one of the least fusible things you can put on this display.
- **`UiButtonMotion`** adds press and hover motion. On a stereo display a button that only changes
  colour reads as flat; a button that moves reads as a button.
- **World-space canvases, placed with `StereoVolume`.** The SDK's `UIScaler` sizes them for the
  display.

---

## 8. Rendering

`StereoPostProcessing` manages the URP post-processing setup for the stereo cameras.

Two traps worth knowing up front:

- **A URP renderer configured as 2D discards every 3D light.** Check the URP asset and its renderer
  before debugging a lighting problem for an afternoon.
- **`VRRenderer` creates its stereo sub-cameras at runtime without `UniversalAdditionalCameraData`**,
  so URP Volumes never apply to them. Post-processing has to be arranged around that, which is what
  `StereoPostProcessing` exists for.

### Transparent surfaces

`KmaxRigBuilder.EnsureLitMaterial` builds opaque materials only, because a flat semi-transparent pane in front of a
solid object does not fuse in stereo. The anatomy exhibit does draw receded structures as glass, and gives each
an edge glow at its own depth so there is something to fuse on. That is a judgement to confirm through the glasses,
not a fact; see [decisions.md](decisions.md). Make a URP Lit material transparent by setting `_Surface` and `_Blend`
and calling `BaseShaderGUI.SetupMaterialBlendMode`, never by setting the blend factors by hand.

---

## 9. Audio

`ProceduralAudio` synthesises pads, chimes, blips, whooshes, thuds and engine tones at runtime, so
the module ships with no audio assets and no licence question attached to them. Loops are
frequency-snapped so they wrap without a discontinuity.

Expose an override `AudioClip` for every sound in your director component, so swapping in recorded
audio later is an inspector edit and not a code change.

---

## 9a. Traps met while building the anatomy exhibit

- **`UiButtonMotion` fights layout groups.** It captures its `anchoredPosition` once and re-applies it every
  frame, so a button positioned by a `LayoutGroup` collapses to where it started. Put the button in a slot
  the layout positions, and keep the button and its motion on one object. Do not move the motion to an inner
  visual: the input module only sends a click when the object that took the press also handles the click.
- **The SDK's rig prefab authors its left and right eye cameras.** They are what draw, and the centre camera
  is disabled at runtime. `KmaxRigBuilder.ConfigureCamera` only touches the centre camera; use
  `ConfigureStereoCameras` to set the background, near and far planes on all of them.
- **A model turned end-on and side-on has different depth.** Place it once and it will break the pop-out
  limit at some angle. Put `ComfortDepthKeeper` on the object that carries it and hand it the content.
- **`ViewerFlyController` always centres its focal point on the screen.** Content cannot sit off-centre;
  lay the interface out around it.
- **In the Editor, the capture tooling pauses Play mode**, and a paused game advances no frames, so fades,
  animation and audio all look stuck. Unpause (`EditorApplication.isPaused = false`) and wait before judging
  anything that moves.
- **A blend-shape mesh needs a `SkinnedMeshRenderer`, but not bones.** Without bones the renderer follows its own
  transform, so scaling it still works. Its `MeshCollider` stays at the rest pose, and its `localBounds` must be
  enlarged by the largest shape or it is culled mid-motion.
- **Extra materials on one renderer are a cheap overlay pass.** Materials beyond the submesh count redraw the last
  submesh, and the overlay inherits the renderer's deformation. Give each its own property block with the
  material-index overloads (`SetPropertyBlock(block, index)`); the plain overload writes to all of them.
- **A URP Lit ghost needs `_BlendModePreserveSpecular` at 0.** At its default the highlights stay at full strength
  however transparent the surface is, and the glass turns milky. Alpha output follows the `_Surface` property, not
  the transparency keyword.
- **A new shader renders flat cyan for the first seconds.** The Editor compiles it asynchronously and draws a
  placeholder meanwhile. Wait for `ShaderUtil.anythingCompiling` to clear before judging a screenshot.
- **Do not measure depth with bounding boxes.** A box around tilted content has corners far beyond the surface,
  and the error grows with scale. `ComfortDepthKeeper` measures the mesh's outermost vertices instead.
- **Some DOSCH structures are open shells.** The left atrium is a partial cuff and is almost invisible from the
  front with back-face culling on, so the materials draw both faces.
- **`manage_camera screenshot` writes into the parent project's `Assets/Screenshots`.** That project is a shared
  template. Copy what is needed elsewhere and delete the folder.
- **The real mouse is live during automated Play-mode tests.** A wheel notch or a hover changes the zoom and the
  highlights while a test runs, so read the state back before trusting it. The Editor can also be closed
  underneath a test; the MCP then reports only the other open project.
- **New C# is normalised by a script, and the script must not add a trailing character.** `.editorconfig` wants
  CRLF, a BOM and no final newline; a lone carriage return at the end of a file fails `dotnet format` with
  "Fix final newline".

---

- **Blend-shape weights may be negative and may exceed 100.** Unity extrapolates linearly, so one shape serves for a
  motion that swings both ways (the ear's vibration). A motion baked into the mesh also grows with the structure.
- **A `LineRenderer`'s width is in world units even when its positions are local.** Under a scaled parent it keeps the
  same thickness on screen, which is what a thin glowing ring wants.
- **A structure's scale belongs to `StructureHighlight`.** Anything that enlarges one goes through `SetSize`, or the
  next change of emphasis puts it back.
- **To judge an animation in a screenshot, freeze it.** `manage_camera screenshot` pauses Play mode at an
  unpredictable moment. Stop the looping audio, set the behaviour's start time by reflection and set
  `Time.timeScale` to 0 in the same call, so the frame is the one you chose. Set it back to 1 and unpause after.
- **Measure layout, do not eyeball it.** Project a mesh's vertices through `StereoVolume.CenterCamera` and read the
  pixel and depth extents; the usable band is about y 115 to 470 of a 619 px frame, clear of the header and caption.

- **Material names in the pack are cut at 31 characters**, so a structure id can differ from the name in the catalogue
  (`eye_levator_palpae_superioris_m`). Read the import log and **Validate Topic Data**; do not guess an id.
- **Two structures from different files may share a name.** The importer makes the second `name_2`. Decide which is
  which by measuring, not by the suffix (both lungs are `lungs`).
- **A clear structure needs a resting opacity,** or it hides what is behind it: the cornea hides the iris. `opacity` in
  the topic data makes it rest as faint glass.
- **A structure with a great deal of edge recedes badly.** The glass rim is on every silhouette, so the ribs become a
  white lattice. `recededSolidity` and `recededGlow` turn it down for that structure alone.
- **Anything that pops out from the model, a light source or a label, must be placed against the comfort volume and the
  window frame, not just the model.** `StereoVolume.DepthOf` and `Camera.WorldToViewportPoint` do it.
- **A model deeper than the budget cannot be fixed by moving it.** The zoom has to back off; it does, from
  `ComfortDepthKeeper.Extent` and `Budget`.
- **A mesh collider does not follow a blend shape.** Re-bake a deforming structure's collider, one a frame, or turn the
  rigid parts by a parent transform instead, as the eye does.
- **Lines that should be seen through a wall are not depth-tested** (`_ZTest` Always on the route rings' material).
- **A `MaterialPropertyBlock` cannot be created in a field initializer of a MonoBehaviour.** It throws an
  `ArgumentNullException` ("dest") the first time it is used. Create it in `Awake` or in the method that starts the component.
- **To fade the whole screen, put a black panel last on the world-space canvas.** `UiAlwaysOnTop` draws the canvas at render
  queue 4000 with the depth test off, after every 3D pass, so the panel covers lines and rings that render at 3000. An
  overlay canvas is not safe on the real display, where the SDK composes the two eyes.
- **Stacked alpha-blended meshes sort badly; additive ones do not.** The body map's layers are additive with the depth test
  on and no depth write, and stack in any order.
- **A vertex colour is used as stored.** The project is Gamma, so a DOSCH colour goes straight in; `AnatomyBodyImporter`
  converts to linear only if the project is ever switched to Linear.
- **Audit a hit target by ray, not by eye.** Cast `StereoVolume.CenterCamera.ViewportPointToRay` over a grid of the screen
  and print the structure each ray hits as a letter: it shows at once which target wins where two overlap. A sphere round
  the brain took in the cheeks; the ray map found it.
- **`EditorApplication.update` slows down when the Editor is not focused,** so a background audit driven by timers takes
  two or three times as long as they say. Poll its result; do not assume it is done.
- **A convex hull fills in the hollows.** The hull of a windpipe or a gullet contains the organs beside it, so a collider cannot
  answer which organ a point is in. `OrganProbe` answers from surface points and their normals; counting a ray's crossings of a
  closed mesh's triangles is the test.
- **Perspective moves what floats.** Something at depth `z` in front of the glass looks `D/(D+z)` times larger and further from the
  middle of the screen (1.11 at 50 mm). Lay out content that floats as it will look, then divide back.
- **A pen can carry a thing without physics.** `Grabbable.kinematicHold` moves the transform to the tip with `StylusGrab.followTime`
  of lag and keeps its rotation. A dynamic hold only makes sense for things that collide. `StylusGrab` finds the grip by
  `ClosestPoint`, so a carried thing needs a convex collider.
- **One effect over many materials: global shader vectors.** The scan sets five once a frame and every Section material cuts by them;
  zero them when the topic ends.
- **Reveal by clipping, not by transparency.** Discard in the fragment shader and draw the back faces of the cut as a flat colour. An
  alpha-blended layer in front of a solid sorts wrongly and fuses badly. A cap written with `SV_Depth` is wrong for hollow sheets
  without a stencil.

## 10. Checklist for a new scene

- [ ] SDK backend is **XR Core** (`Kmax → SDK Backend`)
- [ ] Scene built from an editor builder, not by hand
- [ ] Exactly one `EventSystem`, running `KmaxInputModule`, no `StandaloneInputModule`
- [ ] `StylusTip.tipOffset` measured on the hardware, not left at zero
- [ ] All content placed via `StereoVolume`; nothing hardcodes 0.13 or 0.30
- [ ] Pop-out content clear of the frame edge
- [ ] **Kmax → Audit Comfort Volume** run and clean
- [ ] Reset wired to `RequestReset()`, not `ResetView()`
- [ ] Every `ViewDragGate` hold released in `OnDisable`
- [ ] Verified through the glasses on real hardware, not only in the Game view
