# Architecture

## Assemblies

```
KmaxXR.Core            (SDK)  !KMAX_AIO_K1
Kmax.InputModule       (SDK)  !KMAX_AIO_K1
Kmax.XR                (SDK)   KMAX_AIO_K1
  │
  ▼
KmaxDisplay                    !KMAX_AIO_K1   Scripts/Runtime
  │
  ▼
KmaxDisplay.Editor             !KMAX_AIO_K1   Scripts/Editor
KmaxDisplay.SdkBackend.Editor  (unconstrained) Scripts/Editor/SdkBackend
```

`KmaxDisplay.SdkBackend.Editor` is deliberately unconstrained and references nothing. It holds only
the backend-switching menu, which must survive whichever backend is active — constrained like the
rest, switching to AIO K1 would delete the menu that switches back.

## Runtime layer — `ViitorCloud.KmaxDisplay`

### Comfort (`Scripts/Runtime/Comfort/`)

| Type | Role |
|---|---|
| `StereoVolume` | Static. The display's comfortable depth budget, derived live from `XRRig` and `StereoCamera.DefaultDistance` rather than typed in, so it tracks `ViewScale` at runtime. `+Z` is into the screen: negative is pop-out, positive is depth. |
| `StereoVolumeGizmo` | Draws that budget in the scene view. |
| `ComfortOverlay` | Draws it at runtime, with the stylus tip's live position. Development aid. |

`StereoVolume` is a type and not a constant because a `0.13` copied into four scenes is wrong the
first time anyone changes the view scale. Its two magic numbers (`0.37`, `0.80` from the camera)
are lifted from `XRRig.DrawFrustum`, where the SDK keeps them as gizmo literals rather than API —
**re-check them when the SDK is updated.**

### Stylus (`Scripts/Runtime/Stylus/`)

Two interaction models, deliberately both present:

**Co-located touch.** `StylusTip` treats the pen as a finger — a tracked point with position,
radius and velocity that interacts with whatever it is physically inside. Contact comes from
`Physics.OverlapSphereNonAlloc` each frame, not trigger callbacks: a kinematic trigger at hand
speed tunnels through thin colliders and reports enter/exit out of order, and the grab system needs
the full current contact set anyway rather than a stream of events. `Grabbable` marks what can be
picked up and carries the per-object feel; `StylusGrab` drives held bodies **by velocity while they
stay dynamic**, so physics resolves contact and a held object reads as having weight. A `Grabbable` with `kinematicHold` set is
carried the other way, for an object that has nothing to collide with: its transform follows the tip with the same lag, it keeps its own
rotation and it stays where it is let go.

**Ray pointing.** `StylusBeam` (implements the SDK's `IPointerVisualize`, replaces `StylusRay`)
colours the beam by what it hit. `StylusNavigation` maps the pen's three buttons onto
`ViewerFlyController`, because that controller reads `Input.GetMouseButton`, which a 6-DOF pen
never sets.

`StylusHaptics` rate-limits pulses to roughly one per quarter second; faster reads as a buzz.

### View (`Scripts/Runtime/View/`)

`ViewerFlyController` is a spherical orbit camera keeping the focal target centred at every angle
and distance. It is decoupled from exhibit content by two seams:

- **`IViewResetHandler`** — the scene's controller implements it; `RequestReset()` calls it so a
  focused part or swapped material clears along with the camera, and falls back to a plain camera
  reset when nothing does.
- **`ViewDragGate`** — a per-owner counted static latch any manipulator holds while it owns the
  pointer, so the view does not start orbiting underneath a drag. Static rather than a reference so
  neither side has to know the other exists, mirroring the SDK's own `KmaxPointer` registry.

Both seams replaced direct references to the example module's eye-exhibit types.

A third seam carries dolly input. The wheel, the W and S keys and the stylus's push and pull are reported through
**`DollyInput`** (metres, positive is closer). With `applyDollyToCamera` off the camera does not move for them,
so a scene can answer the same input some other way; the anatomy exhibit zooms the model. **`EnableFly`** switches the orbit and the
dolly off for a scene whose interaction happens in the room's space, where the pen touches things where they appear.

### Rendering, UI, audio

- `StereoPostProcessing` — URP post-processing for the stereo cameras. `VRRenderer` creates its
  sub-cameras at runtime without `UniversalAdditionalCameraData`, so URP Volumes never reach them;
  this exists to work around that.
- `UiAlwaysOnTop` — forces a UI subtree over the scene. A world-space canvas that intersects
  geometry is among the least fusible things on a stereo display.
- `UiButtonMotion` — press and hover motion. A button that only changes colour reads as flat in
  stereo.
- `ProceduralAudio` — static synthesis of pads, chimes, blips, whooshes, thuds and engine tones, so
  the module ships with no audio assets. Loops are frequency-snapped to wrap without a click.

## Editor layer — `ViitorCloud.KmaxDisplay.Editor`

| Type | Role |
|---|---|
| `KmaxSdkBackend` | `Kmax → SDK Backend` menu. Writes `KMAX_AIO_K1` across every supported build target. |
| `KmaxRigBuilder` | Everything a Kmax scene needs: rig, event system, stylus tip, diagnostics, materials, plus serialized-field helpers that log loudly when a field name is stale. |
| `KmaxComfortValidator` | `Kmax → Audit Comfort Volume`. Walks the open scene and reports what sits outside the budget. |

Scene setup lives in editor code rather than hand-authored scenes so a scene can be rebuilt from
scratch and two scenes cannot silently drift apart. **Every `KmaxRigBuilder` helper converges on the
spec** — nothing returns early because an object already exists; it is found and re-applied.

## Layering rules

- Runtime never references editor code.
- Nothing in `KmaxDisplay` references anatomy content. Content depends on the framework, never the
  reverse; new coupling goes through an interface or a gate, as `IViewResetHandler` and
  `ViewDragGate` do.
- SDK code under `Plugins/Kmax/` stays unmodified, so it can be replaced wholesale on an update.

## Anatomy layer — `ViitorCloud.KmaxAnatomy`

```
KmaxDisplay             !KMAX_AIO_K1   Scripts/Runtime
  │
  ├──▶ KmaxAnatomy          !KMAX_AIO_K1   Scripts/Anatomy/Runtime
  └──▶ KmaxAnatomy.Editor   !KMAX_AIO_K1   Scripts/Anatomy/Editor   (also references KmaxDisplay.Editor)
```

### Runtime

| Type | Role |
|---|---|
| `AnatomyModel` | Root of one imported topic at real size in metres. Centred on its own bounds, so it is placed by moving this object alone. Finds a structure by id. |
| `AnatomyStructure` | One named structure: a single material group of a source mesh. The id is the source material name and never changes, so data can refer to a structure without holding a reference. |

### Editor: the import pipeline (`Scripts/Anatomy/Editor/Import/`)

`ObjParser` and `MtlParser` read the source files into plain data (`ObjModel`, `ObjMaterialGroup`,
`ObjCorner`, `MtlMaterial`). `AnatomyMeshBuilder` turns a group into Unity geometry (axis change, winding
check, vertex sharing). `AnatomyMaterialFactory` and `AnatomyTextureFactory` make the URP Lit material and
import its texture. `AnatomyModelImporter` assembles the prefab, `AnatomyTopicSources` says which files make
up each topic, and `AnatomyPaths` says where things live. `AnatomyMenu` exposes it as
**Kmax → Anatomy → Import**.

Each structure gets, besides its solid material, a transparent twin (`<id>_ghost.mat`) and a reference to the
shared edge-glow material, and a label anchor: the vertex facing the viewer among those nearest the middle of the
structure, where a marker's line ends. `AnatomyGlowMaterials` makes the two shared materials that use
`Content/Shaders/AnatomyGlow.shader`. A motion that is baked into the meshes implements
`IAnatomyMotion` and uses `AnatomyFieldShapes`, which turns a displacement field into a blend shape with its normals.
`AnatomyHeartMotion` finds the heart's long axis from the chambers and bakes the beat; `AnatomyEarMotion` measures the
canal, the drum and the joints between the bones and bakes the vibration; `AnatomyEyeMotion` bakes the soft tissue's
gaze shapes, the pupil and the lens focus; `AnatomyBreathMotion` measures the chest and bakes one breath; `AnatomyJawMotion` measures the jaw's hinge and bite line and bakes the `Open` shape into the face muscles. A motion may
store a route (`AnatomyRoute`, named, on the model): the sound's way in, the air's way down. A structure with shapes gets a `SkinnedMeshRenderer` without bones, which follows its
own transform, so the highlight can still scale it.

The body map is imported by `AnatomyBodyImporter`, from the layer and region tables in `AnatomyBodySources`
(`AnatomyBodyLayerSpec`, `AnatomyBodyRegionSpec`). `AnatomySourceReader` (shared with the topic importer) reads the files;
`AnatomyMeshMerger` joins their groups into one piece; `AnatomyCropRegion` (a bust for the body map, a torso for the activities) fades the body out below the ribs and towards the
arms in the vertex-colour alpha and drops what has faded. `AnatomyMeshData` carries the vertex colours. A layer's material
is `Content/Materials/BodyLayer<Name>.mat`, made by `AnatomyGlowMaterials.EnsureLayer`; a region is built like a topic's
structure (`AnatomyModelImporter.AddStructureComponent`) but with a sphere target when it is small.

The two activities' torso is imported by `AnatomyTorsoImporter`, from `AnatomyTorsoSources` (which files) and
`AnatomyTorsoOrganSpec` (which organs). `ImportOrgans` builds the puzzle: a frame of the torso's bones drawn as glow, and ten organs as separate
solid structures in their true places, each with a convex collider, a kinematic body, a `Grabbable` with `kinematicHold` and a slot, a faint
outline copy of itself (`Content/Materials/OrganSlot.mat`) left where it belongs. `ImportScan` builds the scan: the muscles that cover the
torso, the bones, the vessels and sixteen organs, all on the Section material (`AnatomySectionMaterials`, one per structure with its own
cut-face colour; `Content/Shaders/AnatomySection.shader`), with no colliders and an `AnatomyCutBox` the figure is trimmed to.
`AnatomyPrefabParts` and `AnatomyMaterialLook` are the mesh, renderer and colour helpers it shares with the body map's importer. The organs'
meshes are built once, into `Generated/Meshes/torso/`, and shared by both prefabs.

Everything the pipeline writes to `Generated/` updates in place, so GUIDs survive regeneration. The two shared
materials and the badge sprites (`AnatomyMarkerSprites`) are not from the pack, so they live in `Content/` and are
committed. See [decisions.md](decisions.md) for why the pack is parsed directly and how the axes map.

### Runtime: a topic on screen

Data, logic and presentation are kept apart. A topic's words and settings are JSON (`Content/Resources/Topics/`),
read into `AnatomyTopicData`. The state of a visit lives in a plain-C# `AnatomyExplorer`. Everything that
draws or speaks reacts to it.

| Type | Role |
|---|---|
| `AnatomyTopicData`, `AnatomyStructureInfo`, `AnatomyTourStep` | The topic's text, scale, pop-out and tour, keyed by structure id. `AnatomyTopicLibrary` finds data and model by topic id under Resources. |
| `AnatomyExplorer` | Plain C#. What the pointer is on, what is picked, where the tour is, and the number each structure carries (its place in the topic's list). Picking and touring are mutually exclusive; `SelectNext` and `SelectPrevious` step through the numbers. Raises `Changed`; decides how each structure should be drawn (`StateOf`). |
| `AnatomyStructureTarget` | Turns the event system's enter, exit and click on one collider into C# events. The mouse and the stylus ray arrive identically. A release far from the press is ignored, so turning the model never picks what it ends over. |
| `StructureHighlight` | Owns each structure's size and eases it between `HighlightState`s through property blocks: brightness, glow, swell and opacity. A structure that recedes turns to glass: the transparent twin material is swapped in and an additive edge glow is drawn on a second material slot of the same renderer. `Previewed` is a receded structure the pointer is on. |
| `AnatomyTopicController` | The one MonoBehaviour that wires it together. Loads a topic (a topic that fails to load leaves the current one in place), feeds pointer events from structures and badges to the explorer, redraws on `Changed`. Implements `IViewResetHandler`. A structure whose text names a `topic` can be explored: the Explore button, or pressing it a second time, raises `TopicRequested`, which the shell answers (with nobody listening the topic simply loads). Binds the layer buttons when the topic has layers, and offers `BeginShowcase` and `AdvanceShowcase` for an exhibit showing itself. A topic's `links` become buttons that open other topics (the body map's two activities). A `stationary` topic holds the view, the zoom and the depth still, and a `selectable: false` topic gives its structures no pointer events. The controller hands a behaviour an `AnatomyBehaviourContext`, shows a narrator's `TopicMessage` in the caption and puts a topic's action on the Explode button's place. |
| `AnatomyInfoPanel`, `AnatomyControls` | Presentation only: title and caption bar, and the buttons (explore, tour, previous, next, zoom out and in with a readout, reset, and a Body map button on every topic but the body map). They show what they are told and report presses. |
| `AnatomyMarkers`, `AnatomyMarker`, `MarkerLayout` | Numbered badges, one per described structure. Badges are UI on the screen plane in two columns, level with their structures; a 3D line joins each to a point near the structure's middle. `MarkerLayout` is the arithmetic (which column, how to keep badges apart) and has no Unity dependencies beyond `Mathf`. |
| `ExplodedViewBehaviour` | Slides the structures that have an `explodeMm` offset out and back, eased, and enlarges those with an `explodeScale` together about their common middle so they stay joined. Moves and sizes transforms only, so colliders, badges, the depth keeper and the zoom focus follow. Sizes go through `StructureHighlight.SetSize`. |
| `AnatomyZoom` | Scales the model about what is in focus, from the buttons, the wheel and the pen, up to the topic's `maxZoom`. Takes charge of the model's scale and position. |
| `AnatomyAudio` | Ambience, cues and heartbeat, all synthesised by `ProceduralAudio`, each with an override slot. |
| `HeartbeatBehaviour`, `HearingBehaviour`, `BreathingBehaviour`, `EyeBehaviour`, `JawBehaviour`, `AnatomyBehaviours` | A topic names a behaviour in its data; a switch attaches it. The heartbeat times four blend-shape weights; the hearing behaviour runs a 4 second sound cycle (a tone, rings down the canal, one `Vibrate` weight for the drum and bones, a glow on the inner ear); the breathing behaviour a 5 second breath (one `Inhale` weight, rings of air down the windpipe, a glow on the lungs, a rush of breath). The eye behaviour is described below; the jaw behaviour hangs the jaw bone and lower teeth from a pivot at the joint and opens it every four seconds, stretching the face muscles by an `Open` shape and lighting the ones that work. The shapes are baked into the meshes at import. |
| `IFocusListener` | Implemented by a behaviour that wants to know which structures are being explained. The controller tells it on every change. |
| `EyeBehaviour`, `EyeGazeFrame`, `EyeMuscleActions`, `EyeLightRays`, `SoftColliderRefresher` | The eye follows the pen (the pen tip, or the mouse in the Editor), and explaining a muscle turns it the way that muscle pulls. The rigid parts of the globe hang from a pivot at the middle of the eyeball and are turned by it, so their colliders turn too; the muscles and the nerve's sheath are stretched by baked yaw and pitch shapes and their colliders are re-baked one a frame. `EyeGazeFrame` holds the turning signs for both the importer and the runtime, `EyeMuscleActions` which way each muscle pulls, `EyeLightRays` the light from the pen's bead to the macula. |
| `RouteRings`, `AnatomyRoute` | Rings that travel along a measured route, for a sound going down the canal or air down the windpipe. The route is data on the model; the rings are lines redrawn in the model's space, not depth-tested. |
| `BodyBehaviour`, `AnatomyLayer` | The body map. A layer is one merged mesh drawn as a glow (`AnatomyLayer` holds its id, label, tint, swatch and brightness); the behaviour shows and hides layers with a fade through a property block on the shared Glow material, lets the layers fall back while an organ is explained (`IFocusListener`), and glows the organs that have nothing said about them in a slow wave from the head to the chest. |
| `AnatomyLayerPanel`, `AnatomyLayerChip` | The layer buttons down the left of the body map: a fixed pool of six, bound to the body's behaviour when the body map loads and hidden in every other topic. |
| `KioskShell`, `KioskFlow`, `KioskState`, `ScreenFader` | The unattended loop. `KioskFlow` is plain C#: hub, topic and attract, and the clock that moves between them. The shell watches for a visitor (pen, mouse, keys, wheel, tracked eyes), switches topics behind a fade, runs the body map's tour as the attract mode, and in the Editor maps F1 to F9 to the topics, the body map and the activities. `ScreenFader` is a black panel, last on the canvas, so it covers everything. |
| `AnatomyBehaviourContext`, `ITopicNarrator`, `ITopicAction`, `IResetListener`, `TopicMessage` | How an activity, which owns what the caption says and what the action button does, plugs into the controller without the controller knowing it. The context gives a behaviour the topic data, the sound, the haptics and the pen's tip. A narrator offers a `TopicMessage` (key, heading, body, fact, progress; a new message with the same key only updates the progress line). An action offers a label and `PerformAction`, and says when the label changes. A reset listener is told when Reset is pressed. |
| `OrganPuzzleBehaviour`, `OrganPiece`, `OrganScatter` | Put the organs back. The behaviour runs the round (intro, scatter, play, complete), the snapping, the hint and the caption. A piece is one organ: its place, its snap distance, the glide out and the settle home, the glows of a touch, a hint and the finish, and its outline. `OrganScatter` packs boxes into the glass's window around the rectangles to keep clear (a plain static class with a seeded random). The pen does the carrying: `StylusGrab` and a kinematic `Grabbable`. |
| `ScanBehaviour`, `OrganProbe`, `AnatomyCutBox` | Pen as instrument. Each frame the behaviour sets the five global shader vectors the Section shader cuts by, from the pen's tip (a lens, or the whole plane at its depth), draws the lens's outline in the air, asks the probe which organ the tip is in or looking at, lights it, and says what it is and how deep the pen is. `OrganProbe` is plain C#: it knows each organ from a thinned cloud of its surface points and their normals, so the organs need no colliders. `AnatomyCutBox` is the box the figure is trimmed to. |

`ComfortDepthKeeper` (in `KmaxDisplay`, because it knows nothing about anatomy) keeps content inside the depth
budget however it is turned, and reports how deep the content is (`Extent`) against what the budget allows (`Budget`),
so the zoom can back off for a model that is too deep to fit whatever is done: it measures the content's extent in glass space each frame and eases it along
the view axis only as far as needed, near edge first. It measures the meshes, not their bounding boxes: each mesh
is reduced once to its outermost vertex along 26 directions, because a box around tilted content has corners far
beyond the surface and the error grows with zoom.

### Editor: building the scene

`AnatomyKioskSceneBuilder` (**Kmax → Anatomy → Build → Kiosk Scene**) rebuilds `Scene/Main.unity` from
nothing: 27" rig, event system, viewer-fixed lighting, `AnatomyInterfaceBuilder`'s world-space interface (the
title, the caption bar, the buttons, and a pool of 16 badges with their lines), the viewer and the exhibit. The
viewer is set to report dolly input without moving the camera, and the pen's dolly is on, so both zoom the model.
`AnatomyTopicValidator` (**Validate Topic Data**) cross-checks each topic's text against its imported model, the
body map included, and that every `topic` a structure names has data.

The interface also holds the Explore and Body map buttons, the pool of six layer buttons on the left, and the screen
fader, last. The scene builder adds `KioskShell` to the `Exhibit` object, points the controller at the body map as its
start and hub topic, and gives the shell the controller, the controls, the fader, the pen and, if the rig has one, the
head tracker.
