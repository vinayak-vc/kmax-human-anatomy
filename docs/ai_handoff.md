# AI Handoff

**Last updated:** 2026-10-01

## Start here

1. **`AGENTS.md`** at the Unity project root outranks everything: code style, workflow and what to document. Then `.editorconfig`.
2. Read this file, then [architecture.md](architecture.md) and [roadmap.md](roadmap.md). Open [tasks.md](tasks.md) for what is next
   and what "done" means, and [decisions.md](decisions.md) for why things are as they are. Read
   [kmax-usage-guide.md](kmax-usage-guide.md) section 9a before touching rendering, the pen, the UI or the depth budget.
3. Summarise your understanding to the user before you write code, and say which of the traps below apply to the task.
4. Do not start anything the user has not asked for. "Next recommended task" is a recommendation, and the backlog in
   [roadmap.md](roadmap.md) is not agreed.
5. Finish by updating [tasks.md](tasks.md) (as you go), [decisions.md](decisions.md) (a major decision) and this file (last: the
   state, the files you changed, the next recommended task).

**Working boundaries**, set by the user and still in force:
- Project files go only in the nested repo, `Assets/Games/kmax-human-anatomy`, and docs in its `docs/`. The parent Unity project is a
  base template shared with other projects, so add nothing to it. The one unavoidable exception is the Editor's capture tooling, which
  writes screenshots to the parent's `Assets/Screenshots`: delete that folder and its `.meta` after use.
- Scope: a 27" display, an unattended kiosk, English only, reproductive structures excluded.
- DOSCH licensing is `Pending DOSCH Approval`. Record it as such where relevant; do not raise it, gate work on it, or remove or alter
  DOSCH assets on licensing grounds.
- Do not commit unless asked. The nested repo is on a detached HEAD at the initial commit; check out `main` first.
- No secrets or personal data in code, docs or logs, and a warning before anything destructive.

## State

The platform layer works, the DOSCH content pipeline works, and **the whole exhibit is built and interactive** in
`Scene/Main.unity`. It opens on a **body map**: a bust drawn as five glowing layers (skeleton, muscles, organs, vessels,
nerves, toggled from buttons on the left) with six places to explore set inside it. Point at one to read about it, press
Explore or press it again, and the screen fades into its topic: the heart (beats), the brain (explodes), the ear (hears,
magnifies), the eye (follows the pen, focuses, shows the light), breathing (the chest breathes twelve times a minute) or
the skull, jaw and face (the jaw opens and closes, the working muscles glow). A Body map button leads back. Point at a
structure, or its numbered badge, to read about it; press it to pick it and the rest turns to glass; Previous and Next
step through the numbers; zoom in on what is picked; follow a guided tour that sets its own view and zoom; reset. Left
alone, a topic returns to the body map after two minutes and the body map shows its own tour after 45 seconds. All
verified in the Editor with no console errors and inside the depth budget.

The body map also has two buttons at the top right, **Put the organs back** and **Scan the body**. The first throws ten organs
out in front of the glass: carry each back to its outline with the pen (hold the button), and it settles with a chime and its
card; the last one sounds a fanfare and Play again appears. The second shows the torso solid in layers, and the pen's tip cuts
it: a lens round the tip, or, with a second button, a slice of the whole body at the tip's depth, with the organ under the tip
named and the depth into the body read out. Both are verified in the Editor, with the mouse standing in for the pen.
**Nothing has run on Kmax hardware,** and the user has not yet reviewed the scenes or the two activities.

**Done:** the platform, the content pipeline, the six topics, the body map hub, the kiosk shell and the two activities.
**Not done:** the user's review, the polish pass, a Windows build, a comfort audit of every topic, any hardware run, clinical review of
the text, and any commit. [roadmap.md](roadmap.md) has the table; [tasks.md](tasks.md) has the detail.

## How to run it

1. Open `Scene/Main.unity` and press Play. The Game view shows the window as seen from the nominal eye position,
   in mono. Mouse hover and click work; the stylus goes through the same event path. The wheel and W and S zoom the model (in
   the two activities, where the view is held still, the wheel moves the pen's stand-in in depth instead).
2. **In the Editor, F1 to F9 switch between heart, brain, ear, eye, breathing, skull, the body map, the organ puzzle
   and the scan** (compiled out of builds; `KioskShell` handles them). With the mouse, the wheel moves the pen's stand-in
   in depth (it starts about 52 mm in front of the glass) and the left button grabs. The scene opens on the body map. To
   start somewhere else, set **Start Topic Id** on the `Exhibit` object. To test the unattended loop without waiting,
   shorten the shell's three timings.
3. On a fresh clone the models do not exist. Place the DOSCH pack in `Source~/` and run
   **Kmax → Anatomy → Import → All Topics**, then **Kmax → Anatomy → Build → Kiosk Scene**.
4. **Kmax → Anatomy → Validate Topic Data** cross-checks topic text against the imported models.

## How to verify your work

Nothing here needs hardware. Do all of it before saying a change is done.

1. **Compile.** Stop Play mode, then refresh scripts in the Editor (with the unityMCP tools: `refresh_unity` with a compile
   requested, then `read_console` for errors and warnings). Compiling is asynchronous: wait for it; the modification time of
   `Library/ScriptAssemblies/KmaxAnatomy.dll` changes when it is done. A clean console is the bar.
2. **Format and syntax.** From the Unity project root, both must exit 0 (drop `--verify-no-changes` to fix in place):
   ```
   dotnet format whitespace KmaxAnatomy.csproj --no-restore --verify-no-changes
   dotnet format whitespace KmaxAnatomy.Editor.csproj --no-restore --verify-no-changes
   ```
   Then search `Scripts/Anatomy` for `var`, `new()` and `=>`. There are none: write an anonymous function as `delegate () { }`.
3. **Data.** **Kmax → Anatomy → Validate Topic Data** must end with "Topic data is consistent with the models." After changing an
   importer, re-import (**Kmax → Anatomy → Import → All Topics**, or the one topic); after changing the interface, rebuild
   (**Kmax → Anatomy → Build → Kiosk Scene**).
4. **Run it.** Play `Scene/Main.unity`; F1 to F9 switch topics. The mouse stands in for the pen.
5. **Drive it from code** (unityMCP `execute_code`: a CodeDom compiler, C# 6, so write `UnityEngine.Object`; the first call after
   entering Play mode can be dropped, so repeat it). Private members are reached by reflection.
   - Keep the shell off your topic: disable `KioskShell`, set its private `_flow` to a `KioskFlow(100000f, 100000f)` after
     `EnterHub()`, enable it, then call `AnatomyTopicController.LoadTopic("organs")`.
   - Move the pen: set `StylusTip.mouseFallback` to false and write its private `_position`; call `UpdateContacts`, then
     `StylusGrab.TryGrab` and `ReleaseHeld`, to carry something through the real path.
   - Run a long test as a callback on `EditorApplication.update` that stores its result in `SessionState`, and read it back with a
     second call. The Editor runs these slowly when it is not focused. A shell `sleep` over about 14 seconds is refused: poll in
     12-second steps.
6. **Look at it.** `manage_camera screenshot` pauses Play mode (set `EditorApplication.isPaused` to false and wait before judging
   motion) and writes into the parent project's `Assets/Screenshots`: copy what you need and delete the folder and its `.meta`.
7. **Measure, do not eyeball.** Layout: project vertices through `StereoVolume.CenterCamera`. Whether a point is inside an organ:
   count a ray's crossings of its closed mesh. What a screen point hits: a ray map (usage guide, section 9a).
8. **Hand over.** Update the docs as in "Start here".

## Files added since the platform layer

```
Scripts/Anatomy/Runtime/Topic/         AnatomyTopicData, AnatomyStructureInfo, AnatomyTourStep,
                                       AnatomyTopicLibrary, AnatomyTopicController
Scripts/Anatomy/Runtime/Interaction/   AnatomyExplorer, AnatomyStructureTarget, StructureHighlight, HighlightState,
                                       AnatomyZoom
Scripts/Anatomy/Runtime/Presentation/  AnatomyInfoPanel, AnatomyControls, AnatomyAudio,
                                       AnatomyMarkers, AnatomyMarker, MarkerLayout
Scripts/Anatomy/Runtime/Behaviours/    HeartbeatBehaviour, HearingBehaviour, SoundWaveRings, ExplodedViewBehaviour,
                                       AnatomyBehaviours
Scripts/Anatomy/Runtime/               AnatomyModel, AnatomyStructure, AnatomyLayer, AnatomyRoute
Scripts/Anatomy/Runtime/Kiosk/         KioskShell, KioskFlow, KioskState
Scripts/Anatomy/Runtime/Presentation/  ... and AnatomyLayerPanel, AnatomyLayerChip, ScreenFader
Scripts/Anatomy/Runtime/Behaviours/    ... and BodyBehaviour (also EyeBehaviour, JawBehaviour, BreathingBehaviour)
Scripts/Anatomy/Runtime/Topic/         ... and TourExplodeChange (what a tour step does to the exploded view)
Scripts/Anatomy/Editor/Import/         the OBJ/MTL importer (see architecture.md), IAnatomyMotion, AnatomyFieldShapes,
                                       the motions, AnatomyGlowMaterials, and the body map's AnatomyBodyImporter,
                                       AnatomyBodySources, AnatomyBodyLayerSpec, AnatomyBodyRegionSpec, AnatomyCropRegion,
                                       AnatomyMeshMerger, AnatomySourceReader
Scripts/Anatomy/Editor/Build/          AnatomyKioskSceneBuilder, AnatomyInterfaceBuilder, AnatomyInterfaceParts,
                                       AnatomyMarkerSprites, AnatomyTopicValidator
Scripts/Runtime/Comfort/ComfortDepthKeeper.cs
Content/Resources/Topics/              heart, brain, ear, eye, breathing, skull, body, organs and scan (.json) - each topic's text,
                                       numbering, tour and settings
Content/Shaders/AnatomyGlow.shader     the edge glow on receded structures, and the badges' lines
Content/Materials/                     AnatomyRim.mat, AnatomyLine.mat, BodyLayer*.mat (not from the pack, so committed)
Content/Textures/                      MarkerDisc.png, MarkerRing.png (generated, committed)

The two activities:
Scripts/Anatomy/Runtime/Behaviours/    OrganPuzzleBehaviour, OrganPiece, OrganScatter, ScanBehaviour, OrganProbe, and the plug-in
                                       seams TopicMessage, ITopicNarrator, ITopicAction, IResetListener, AnatomyBehaviourContext
Scripts/Anatomy/Runtime/               AnatomyCutBox;  Runtime/Topic/ AnatomyTopicLink
Scripts/Anatomy/Editor/Import/         AnatomyTorsoImporter, AnatomyTorsoSources, AnatomyTorsoOrganSpec, AnatomySectionMaterials,
                                       AnatomyPrefabParts, AnatomyMaterialLook; AnatomyCropRegion replaced AnatomyBustCrop
Content/Shaders/AnatomySection.shader  the lit surface of the scan, which clips
Content/Materials/OrganSlot.mat        the outline of an organ's place (not from the pack, so committed)
Content/Resources/Topics/              organs.json, scan.json
```

Modified framework files, all additive: `KmaxRigBuilder.cs` (`SetString`, `SetBounds`, `SetVector3`,
`SetReferences`, `ConfigureStereoCameras`), `ProceduralAudio.cs` (`CreateHeartbeat`, `CreateTonePulse`, `CreateFanfare`), `ViewerFlyController.cs`
(`DollyInput`, `applyDollyToCamera`, `EnableFly`), `StylusNavigation.cs` (a UI press does not start the zoom gesture),
`Grabbable.cs` (`kinematicHold`), `StylusGrab.cs` (`DriveKinematic`; a disabled grab does nothing), `StylusHaptics.cs` (`Success`),
`ComfortDepthKeeper.cs` (measures mesh extremes), `.gitignore`, `README.md`, `docs/*`.

## Next recommended task

The user's build order is complete (ear, eye, breathing, skull and jaw, the body map hub), and so are the two pitches that were
held back until the scenes were reviewed and that the user then asked for: **"Put the organs back"** and **"Pen as instrument"**.
Nothing is left on that list. Reproductive structures stay excluded.

Next, in this order: the user's review of the two activities in the Editor (the Game view, F8 and F9); the polish pass (a
post-processing Volume through `StereoPostProcessing`, MSAA in the URP asset, a subtle backdrop, emphasis and ghost strengths); a
Windows Standalone build; `Kmax → Audit Comfort Volume` per topic; and everything under "Needs hardware" in [tasks.md](tasks.md),
above all the pen's co-location (`StylusTip.tipOffset`), carrying an organ, the lens through the glasses and the shell's
timings. Clinical review of every text is outstanding. See [roadmap.md](roadmap.md).

## Decisions waiting on the user

The two activities were built on defaults that the user has not yet seen. Each is a small change if they object.

- They open from two buttons on the body map, not as places on the bust.
- The puzzle has ten organs; the pancreas, gall bladder, bladder, windpipe and gullet are only in the scan.
- Both hold the view still: no orbit and no zoom.
- A drop away from an organ's place is silent, and the hint comes by itself after 25 seconds.
- The scan's lens is 22 mm in radius and follows the pen's tip, not its direction; its cut faces are one flat colour, not a cross-section.

Open questions from the roadmap: languages beyond English, who reviews the text, and the delivery form of the DOSCH models.

## Traps worth knowing before you touch this

- **The DOSCH source is right-handed Z-up.** The importer maps `(x, y, z)` to `(x, z, y)` and reverses
  winding. Anything that reads source coordinates directly must do the same.
- **`Source~` is deliberately invisible to Unity.** Do not rename it; the tilde is what stops 526 OBJ files
  importing at default settings.
- **Never commit anything under `Generated/` or `Source~/`.** They are ignored; keep it that way. The shared
  materials and badge sprites in `Content/` are not from the pack and are committed.
- **Structures are single meshes centred on the midline.** The brain's lobes each span both hemispheres, so
  nothing can separate left from right, and a radial explosion does nothing useful. Offsets are authored.
- **The DOSCH heart's left atrium is an open shell,** and the brain's `cerebrum` is the inner core, not the
  whole. The materials draw both faces. Do not trust a structure's name to describe its shape; look at it.
- **A blend-shape mesh needs a `SkinnedMeshRenderer`, with no bones.** It follows its own transform, so the highlight
  can still scale it. Its collider stays at the rest pose.
- **Property blocks on a renderer with two materials must use the material-index overloads.** The plain overload
  writes to both.
- **Preserved specular must be off on a ghost,** or the glass turns milky. Set `_Surface` and `_Blend`, then call
  `BaseShaderGUI.SetupMaterialBlendMode`; never set blend factors by hand.
- **The body map's layers are additive glow, not lit meshes.** They use the Glow shader with `_Floor`, the vertex colour
  carries the crop's fade in its alpha (and the pack's colours for the vessels), and `BodyBehaviour` sets their brightness
  through a property block. Do not give them a transparent lit material: alpha-blended layers sort against each other.
- **Regions are reached through spheres, not their meshes,** for the small organs: see `AnatomyBodySources`. Change a sphere,
  then re-run the ray map (`docs/kmax-usage-guide.md` section 9a) to see who owns what.
- **`KioskShell` owns topic switching.** Anything that changes topic should call the controller's `TopicRequested` path (a
  region, Explore, Body map) or the shell's `RequestTopic`, so the screen fades; calling `LoadTopic` directly cuts.
- **A structure's scale belongs to `StructureHighlight`.** It resets `localScale` to its rest size times a swell at every
  change of emphasis. Enlarge a structure only through `SetSize`.
- **Blend-shape weights may be negative.** One shape serves a motion that swings both ways. A `LineRenderer`'s width is
  in world units even under a scaled parent.
- **Do not measure depth with bounding boxes.** They overestimate on tilted content, and the error grows with zoom.
- **`UiButtonMotion` re-applies its start position every frame**, so it cannot sit on anything a layout group
  positions. Use a slot. See [kmax-usage-guide.md](kmax-usage-guide.md) section 9a.
- **The Editor's capture tooling pauses Play mode and writes into the parent project's `Assets/Screenshots`.** A
  paused game advances no frames, so fades, animation and audio look stuck. Unpause and wait before judging motion,
  and delete the folder afterwards. The real mouse is live during tests; a wheel notch changes the zoom.
- **A new shader shows flat cyan for a few seconds** while the Editor compiles it. It is not a bug.
- **New C# is formatted by the tool, not by hand.** From the Unity project root:
  `dotnet format whitespace KmaxAnatomy.csproj --no-restore` (and `KmaxAnatomy.Editor.csproj`). It enforces
  CRLF, the BOM, no final newline, and blank lines between `using` groups of different root namespaces. A script
  that adds a stray trailing carriage return fails it with "Fix final newline".
- **Two input modules dispatch every press twice.** `KmaxRigBuilder.EnsureEventSystem` removes
  `StandaloneInputModule`; do not add it back.
- **A URP renderer configured as 2D discards every 3D light.** This project's renderer is 3D; keep it so.
- **`VRRenderer`'s sub-cameras have no `UniversalAdditionalCameraData`**, so URP Volumes never apply to
  them. `StereoPostProcessing` exists for that. Configure every camera under the rig, not only the centre one.
- **Vibrating an absent pen can throw from vendor code.** Check `KmaxStylus.Visible`, not just null.
- **`StereoVolume`'s `0.37` / `0.80` come from `XRRig.DrawFrustum` gizmo literals**, not SDK API.
- **Wire Reset to `ViewerFlyController.RequestReset()`, not `ResetView()`.**
- **`VirtualScreen.ScreenType` order is `Screen15_6, Screen27, Screen24`.** 27" is index 1.
- **`KmaxStylus.PrimaryKey` ships as Middle.** UI clicks come from pen button 2, which is also the pen's zoom
  gesture, until that is changed.
- **An activity is a stationary topic.** `stationary` holds the view, the zoom and the depth still and switches
  `ViewerFlyController.EnableFly` off; `selectable: false` removes the structures' pointer events. A new activity that moves its
  content must not be `stationary` unless it keeps the depth budget itself.
- **A convex collider is not an organ.** A hull fills in every hollow: the windpipe's and the gullet's contain the heart and both
  lungs. Anything that asks which organ a point is in uses `OrganProbe` (surface points and normals). A carried organ does need its
  hull, because `StylusGrab` finds its grip with `ClosestPoint`, which only a convex collider has.
- **Lay out in glass space.** Something floating in front of the glass looks larger by `D/(D+z)` (1.11 at 50 mm, with `D` 0.5 m) and
  further from the middle of the screen. `OrganPuzzleBehaviour` works out where things will appear, packs them there and divides back.
- **The Section shader's cut is global.** `_KmaxLens`, `_KmaxLensAxis`, `_KmaxLensColor`, `_KmaxBoxMin` and `_KmaxBoxMax` are global
  shader vectors, set by `ScanBehaviour` and zero everywhere else. A new surface in the scan must use a Section material
  (`AnatomySectionMaterials.Ensure`) or it will not be cut.
- **Clip, do not blend.** Do not give the scan's layers a transparent material to see inside: alpha-blended layers sort against each
  other and against the organs. Cut in the shader. A cap drawn at the cut by writing `SV_Depth` was tried and is wrong for hollow
  sheets without a stencil.
- **Test a claim about the inside of something against a ray count.** Every organ mesh in the scan is closed, so the number of
  triangles a ray crosses (odd is inside) is the truth to compare a probe with.
- **Check which Editor the tools are talking to.** The unityMCP default instance once pointed at the sibling
  `kmax-display-example` project. Read `Application.dataPath` first, and pin the instance with `set_active_instance`.
- **DOSCH licensing is `Pending DOSCH Approval`.** Record it as such where relevant; it does not gate
  development, and nothing in the pack is to be removed or altered on licensing grounds.

## Conventions

`AGENTS.md` at the Unity project root governs: no `var`, no `new()`, no expression-bodied members, K&R
braces, `[SerializeField] private` camelCase fields, public PascalCase, private non-serialized instance
fields `_camelCase` (matching the ported code). New C# files follow `.editorconfig` and pass
`dotnet format whitespace`.

## Starting a new session

Paste this at the start of a new chat, and write the task at the end. It points at the docs for the state, so it does not go stale.

```
You are continuing work on the Kmax human-anatomy exhibit: a Unity 6000.3.9f1 and URP project for a head-tracked stereo Kmax display
(27", unattended kiosk) with a 6-DOF pen, built from the DOSCH 3D Human Anatomy pack.

Project: D:\Unity\kmax-human-anatomy-base-project\Assets\Games\kmax-human-anatomy, a nested git repo inside the Unity project
D:\Unity\kmax-human-anatomy-base-project. Put project files and docs only in that nested repo; the parent is a shared base template.

Before you do anything else:
1. Follow AGENTS.md at the Unity project root (code style, workflow, documentation duties) and .editorconfig. They outrank everything
   else. In short: no `var`, no `new()`, no expression-bodied members, K&R braces, [SerializeField] private camelCase fields; new C#
   files are CRLF, UTF-8 with BOM, no final newline, and must pass `dotnet format whitespace` (commands in docs/ai_handoff.md).
2. Read docs/ai_handoff.md first, then docs/architecture.md and docs/roadmap.md. Open docs/tasks.md and docs/decisions.md as needed,
   and docs/kmax-usage-guide.md section 9a before touching rendering, the pen, the UI or the depth budget.
3. Reply with a short summary of your understanding: what is built, what is verified and what is not, and which traps apply to my
   task. Ask if anything is ambiguous. Do not write code until I confirm.

Standing rules:
- Everything is verified in the Editor only. Nothing has run on Kmax hardware, and nothing is committed (detached HEAD: check out
  `main` before any commit, and commit only if I ask).
- DOSCH licensing is `Pending DOSCH Approval`: do not raise it or gate work on it, and never remove or alter DOSCH assets on
  licensing grounds.
- Scope: 27" display, unattended kiosk, English only, reproductive structures excluded. Do not start backlog ideas I have not asked for.
- No secrets or personal data in code, docs or logs. Warn me before anything destructive.
- When you finish: update docs/tasks.md as you go, docs/decisions.md for a major decision, and docs/ai_handoff.md last (state, files
  changed, next recommended task).

My task: [write it here. If it is blank, recommend the next step from "Next" in docs/tasks.md and wait for my answer.]
```
