# Tasks

## Done — 2026-10-01 (the launcher and the house style)

- [x] Launcher: the exhibit opens on, and returns to, nine cards (a row of five over a row of four) with the chosen exhibit's own model
      turning above them on a turntable that pops out of the glass, a Load button below, and drifting dust on a slate backdrop
      (`AnatomyLauncher`, `LauncherCard`, `LauncherPreview`, `LauncherFit`; data in `Content/Resources/Launcher/launcher.json`). Press a
      card to choose it, press it again or Load to open it. It is a screen in `Main.unity`, not a topic: `KioskShell` shows and hides it
      behind the fade, and the topic's whole interface is one container (`TopicInterface`) that is off while the launcher is up.
- [x] Menu and Next exhibit: a pill at the top of every topic's button column. Menu goes to the launcher; Next exhibit steps through the
      cards' order and goes to the launcher after the last. Attract mode is now the launcher choosing its own cards.
- [x] Launcher pictures rendered from the models (`AnatomyLauncherThumbnails`, **Kmax → Anatomy → Build → Launcher Thumbnails**, also the
      last step of **Import → All Topics**), into `Generated/Resources/Anatomy/Thumbs`, which git ignores.
- [x] The interface restyled as dark glass at half size, every screen: `AnatomyUiStyle` (palette and `Scale`), `AnatomyGlassSprites`,
      `AnatomySpriteFiles`; `AnatomyInterfaceBuilder` rewritten onto them with the topic's interface in one container, the control column as
      one layout (no gap when a button is hidden) and the canvas at sorting order 100. Badges are smaller, their hit areas are not.
- [x] Buttons are damped springs (`UiSpring`, `UiButtonMotion`: hover 1.04, press 0.94 and a 2 mm sink, frequency 18, damping 0.7); every
      button also ticks on hover and drops on press (`UiButtonSound`).
- [x] Audio: `PersistentAudioDirector` plays a seamless pentatonic music bed and the button sounds across scene loads;
      `ProceduralAudio` gained `CreateUiHoverTick`, `CreateUiClick`, `CreateExpandSweep`, `CreateCollapseSweep`, `CreateHotspotChime` and
      `CreateAmbientMusic`; `AnatomyAudio` uses the bell and the sweeps and no longer plays its own pad.
- [x] Pen beam in four colours (`StylusBeam`, `StylusBeamStates`) and, for the first time, attached: `KmaxRigBuilder.EnsureBeam` swaps it for
      the SDK's ray.
- [x] Double-sided: audited (all 236 Lit materials, the Section shader and the new materials draw both sides; only the additive glow layers
      cull back, on purpose). **Validate Topic Data** reports a material that culls; **Enforce Double-Sided Materials** fixes it.
- [x] Validator: checks the launcher's cards (topic exists, no duplicates, fits the pool, has a picture).
- [x] Verified in the Editor, console clean, **Validate Topic Data** clean, whitespace check clean for `KmaxAnatomy`, `KmaxAnatomy.Editor` and
      the new framework files: all nine previews through a full turn (nearest -71 to -108 mm of the -115 limit, farthest under +50 mm, clear of
      the title and the cards); choosing, pressing again, Load, Menu, Next exhibit all the way round to the launcher, the idle return and the
      attract cycle with shortened timers; the camera backgrounds, dust and previews put back or cleared on leaving; a collider 60 mm in front
      of a card losing to it at sorting order 100 and winning at 0; the spring (4.6% overshoot, settled in 0.38 s) and the beam's four
      colours by simulated presses; the audio clips (durations, pitch, seam) and a simulated hover and press sounding through the director.

## Done — 2026-10-01 (Put the organs back, and Pen as instrument)

- [x] Two activities, opened from two buttons at the top of the body map (`links` in `body.json`) and left by Body map: `organs` and
      `scan`. A topic can now be `stationary` (the view, the zoom and the depth held) and `selectable: false`, can have a
      `resetLabel`, and a behaviour can say what the caption shows and own the action button (`ITopicNarrator`, `ITopicAction`,
      `IResetListener`, `TopicMessage`, `AnatomyBehaviourContext`).
- [x] Put the organs back: ten organs (`OrganPuzzleBehaviour`, `OrganPiece`, `OrganScatter`) thrown out in front of the glass, each with an
      outline where it belongs. Touch one with the pen, hold, carry, let go: a chime, a haptic pulse and the organ's card. A Hint
      button, and a hint by itself after 25 s; a fanfare and a wave of glow at the end; Play again. The framework gained
      `Grabbable.kinematicHold` (the pen carries by moving the transform), `StylusHaptics.Success` and `ProceduralAudio.CreateFanfare`.
- [x] Pen as instrument: the torso solid in layers (muscles, bones, vessels, sixteen organs) on a new shader, `Kmax Anatomy/Section`,
      that clips. A lens 22 mm in radius at the pen's tip cuts everything between it and the viewer, a second instrument cuts the whole
      body at the tip's depth, and a glowing ring marks the lens's edge on every surface. The caption names the organ the tip is in or
      looking at and how deep the pen has gone (`ScanBehaviour`, `OrganProbe`, `AnatomyCutBox`).
- [x] Importers: `AnatomyTorsoImporter` (`ImportOrgans`, `ImportScan`), `AnatomyTorsoSources`, `AnatomyTorsoOrganSpec`,
      `AnatomySectionMaterials`, `AnatomyCropRegion` (replaces `AnatomyBustCrop`: a bust and a torso), `AnatomyPrefabParts`,
      `AnatomyMaterialLook`. `AnatomyBodyImporter` was refactored onto them and re-imports unchanged. The validator knows both
      activities and that every `links` target has data. F8 and F9 in the Editor.
- [x] Verified in the Editor, console clean and **Validate Topic Data** clean: the puzzle end to end through the pen's real grab path (ten
      of ten placed, the completion card, a wrong place rejected, the hint, Play again, leaving the topic with an organ in the hand);
      the lens and the slice; the hub's two buttons both ways; `OrganProbe` against a ray count of the true insides (every organ mesh is
      closed): 549 of 568 points inside organs named for an organ they were in, 616 of 632 in none left unnamed; the thin organs named
      from 1.5 mm away 70% to 89% of the time. Whitespace check clean for `KmaxAnatomy` and `KmaxAnatomy.Editor`.

## Done — 2026-10-01 (body map hub and kiosk shell)

- [x] Body map: a `body` topic whose prefab `AnatomyBodyImporter` builds from the low-resolution pack: five glowing layers
      (skeleton, muscles, organs, vessels, nerves; 218,000 triangles) cropped to a bust, and nine solid regions (brain, two
      eyes, two ears, jaw and teeth, heart, two lungs). 490 x 575 x 274 mm; loads in 115 ms. Six numbered places, each
      with text, a fact and a topic to open; a six-step tour that turns the body.
- [x] Layers show and hide with a fade, from a column of buttons on the left; the organs glow slowly in a wave from the
      head to the chest, and the layers fall back while one is explained (`BodyBehaviour`, `AnatomyLayer`,
      `AnatomyLayerPanel`, `AnatomyLayerChip`).
- [x] Explore (a button, a second press on the picked region, or during a tour step) and a Body map button on every other
      topic. `AnatomyStructureInfo.topic`; `AnatomyTopicController.TopicRequested`.
- [x] Kiosk shell: `KioskShell`, `KioskFlow`, `KioskState`, `ScreenFader`. Opens on the body map, fades between topics,
      returns an untouched topic to the body map after 120 s, and shows the body map's tour by itself after 45 s.
      The Editor keys F1 to F7 moved from the controller to the shell.
- [x] Shared and tidied: `AnatomySourceReader`, `AnatomyMeshMerger`, `AnatomyBustCrop`, vertex colours in
      `AnatomyMeshData`, `_Floor` in the Glow shader, the validator covers the body map and `topic` references.
- [x] Verified in the Editor: the import, each layer, the regions by a ray audit over the screen, every tour step, the
      whole loop (body map, Explore, topic, Body map, attract, idle return), 16 combinations of angle, zoom and layers
      inside the depth budget (nearest -114 mm, furthest +249 mm), console clean, **Validate Topic Data** clean, whitespace
      check clean for `KmaxAnatomy` and `KmaxAnatomy.Editor`.

## Done — 2026-10-01 (skull, jaw and face)

- [x] Skull topic: 26 structures (skull, jaw bone, both tooth rows, twenty muscles, two nasal parts), sixteen numbered, a
      six-step tour.
- [x] The jaw opens and closes every four seconds (`JawBehaviour`, `AnatomyJawMotion`): the jaw bone and lower teeth hang
      from a pivot at the joint, the face muscles stretch by an `Open` shape, the muscles that work glow, and explaining
      one makes it glow fully in its own part of the cycle.
- [x] `MarkerLayout.Spread` replaced by an exact placement: sixteen badges no longer pile up where their structures cluster.
- [x] `ComfortDepthKeeper` measures only mesh renderers (the ring lines were inflating a model's depth to 400 mm and
      tripping the zoom's budget cap).
- [x] Verified in the Editor: the jaw shut and open, the tour, 11 combinations of angle and zoom inside the budget.

## Done — 2026-10-01 (breathing)

- [x] Breathing topic: 14 structures (two lungs, the airway, seven parts of the voice box, breastbone, ribs, costal
      cartilage, diaphragm), ten of them numbered along the path of a breath, and a seven-step tour.
- [x] One breath baked as an `Inhale` shape across the ribs, cartilage, breastbone, diaphragm, airway and both lungs
      (`AnatomyBreathMotion`); `BreathingBehaviour` runs it at twelve breaths a minute with rings of air down the
      windpipe, a glow on the lungs and a synthesised breath (`ProceduralAudio.CreateBreath`).
- [x] A structure may rest at less than full opacity (`opacity`) and recede more quietly (`recededSolidity`,
      `recededGlow`): the lungs and ribs rest as glass so the airway shows; the ribs recede to faint lines.
- [x] `AnatomyRoute` and `RouteRings` replace the ear's sound path and rings; rings are not depth-tested.

## Done — 2026-10-01 (eye)

- [x] Eye topic: 20 structures, 16 numbered (`unnumbered` for the rest), an eight-step tour, and an opening view turned
      50 degrees. The exploded view slides the cornea, iris, lens and ora serrata forward along the optic axis.
- [x] The eye follows the pen; the muscle that pulls lights up; explaining a muscle turns the eye its way; the pupil
      closes and the lens thickens as the pen comes near; explaining the cornea, lens, retina or macula shows the light
      (`EyeBehaviour`, `AnatomyEyeMotion`, `EyeLightRays`). Checked numerically in model space for all four directions.
- [x] `IFocusListener`: a behaviour is told what is being explained. `SoftColliderRefresher` re-bakes the stretched
      muscles' colliders one a frame (0.2 to 0.8 ms each).
- [x] The zoom backs off where the depth budget would be exceeded (`ComfortDepthKeeper.Extent` and `Budget`).
- [x] Verified in the Editor: the tour step by step, the light's source kept inside the window and the comfort volume,
      22 combinations of angle, zoom and exploded state (zoom 1 fits everywhere; zoom 2 needed the cap end-on).

## Done — 2026-09-30 (ear)

- [x] Ear topic: names, text and numbering for its ten structures along the path of sound (`ear.json`), an eight-step
      tour that goes from the outside in, and an opening view turned 20 degrees and tipped 8.
- [x] Magnify: the exploded view can now enlarge. `explodeScale` on a structure, `explodeLabel` and `assembleLabel`
      on the topic, `recedesWhenExploded` for context that turns to glass. The drum and bones grow 7.5 times about
      their common middle and lift towards the viewer; picking one of them magnifies by itself.
- [x] Tour steps can set the exploded view, turn the view and set the zoom (`exploded`, `setsView`, `yaw`, `pitch`,
      `zoom`), so a tour frames itself. `AnatomyZoom.ZoomTo`.
- [x] Hearing: `HearingBehaviour` runs one sound going in, over and over. A soft tone (`ProceduralAudio.CreateTonePulse`),
      amber rings down the canal (`SoundWaveRings`, `AnatomySoundPath`), the drum and three bones vibrating by one
      baked blend shape (`AnatomyEarMotion`), the inner ear lighting (`StructureHighlight.SetPulse`).
- [x] `AnatomyFieldShapes` and `IAnatomyMotion` shared by the heart and the ear. The heart re-imports bit-identical.
- [x] Verified in the Editor: vibration weights and geometry at rest and at both peaks, the tour step by step, free
      picking (a bone magnifies, the pinna does not, Reset restores), 22 combinations of angle, magnify and zoom all
      inside the depth budget (-116 to +292 mm), no console errors, **Validate Topic Data** clean, whitespace check
      clean for `KmaxAnatomy` and `KmaxAnatomy.Editor`.

## Done — 2026-09-30 (brain)

- [x] Brain topic: names, text and numbering for its 12 regions (`brain.json`), an eight-step tour that follows a
      signal from the eye to the muscles, and an opening view turned 50 degrees and tipped 10.
- [x] Exploded view: an `explodeMm` offset per structure in the data, `ExplodedViewBehaviour`, an Explode button that
      appears only for a topic that can explode, and `AnatomyExplorer.IsExploded`. Reset puts the brain back together.
- [x] The zoom's focus follows the structures' live positions, so it keeps up when they are pulled apart.
- [x] Badges choose their column from where their structure appears on the screen at the opening view, not from
      the model's axes.
- [x] `LoadTopic` keeps the current topic when the new one cannot load. In the Editor only, F1 to F4 switch topics.
- [x] Verified in the Editor: exploded, the brain spans -115 to +95 mm in depth at the opening view and -117 to
      +108 mm at 40 degrees, inside the budget; its screen extent clears the header and the caption; topic switching
      leaves nothing behind; **Validate Topic Data** is clean for heart and brain.

## Done — 2026-09-30 (interaction pass on the heart)

- [x] Receded structures turn to glass instead of going dark: a transparent twin material, an additive edge glow on a
      second material slot of the same renderer, opacity about 10%; pointing at one brings it part-way forward
      (`HighlightState.Previewed`). Both faces are drawn and the ghost fades its highlights.
- [x] Numbered badges (`AnatomyMarkers`, `AnatomyMarker`, `MarkerLayout`): a pool of 16 built with the scene, two
      columns, 3D lines to a point near each structure's middle; pointing at or pressing a badge is pointing at or
      pressing its structure. The number leads the caption.
- [x] Previous and Next are always shown: numbered structures outside a tour, tour steps during one.
- [x] Zoom (`AnatomyZoom`): buttons, wheel, W and S, and the pen's push and pull, to the topic's `maxZoom` (2.4 for
      the heart), closing on the picked structure. `ViewerFlyController.DollyInput` and `applyDollyToCamera`.
- [x] `ComfortDepthKeeper` measures the mesh's outermost vertices instead of bounding boxes. At 2.4x the heart's
      vertices span -115 to +199 mm; side-on at 1x, -115 to +133 mm.
- [x] The heartbeat is rebuilt as blend shapes baked from one smooth field (`AnatomyHeartMotion`): the ventricles
      shorten, squeeze and wring, the atria contract, the arteries swell. No seams open between chambers.
- [x] A release far from the press no longer picks a structure, so turning the model does not pick what it ends over.
- [x] A pen press on the UI no longer starts a zoom.
- [x] `dotnet format whitespace --verify-no-changes` passes on `KmaxAnatomy` and `KmaxAnatomy.Editor`.

## Done — 2026-09-30 (heart topic)

- [x] Topic runtime: `AnatomyTopicData` from JSON, `AnatomyExplorer` (hover, pick, tour), `StructureHighlight`,
      `AnatomyStructureTarget`, `AnatomyTopicController`, `AnatomyInfoPanel`, `AnatomyControls`, `AnatomyAudio`.
- [x] Heart: nine structures with text, an eight-step "follow the blood" tour, a heartbeat timed to a synthesised
      heartbeat (`ProceduralAudio.CreateHeartbeat`).
- [x] `AnatomyKioskSceneBuilder` builds `Scene/Main.unity`: 27" rig, viewer-fixed lighting, world-space interface,
      viewer, exhibit. The template's stock camera is removed.
- [x] `ComfortDepthKeeper` in the framework: content stays inside the depth budget at every angle.
- [x] `KmaxRigBuilder.ConfigureStereoCameras`, `AnatomyTopicValidator`.

## Done — 2026-09-30 (content pipeline)

- [x] Confirm the Editor has imported and compiled the module: 0 compile errors, `.meta` files present.
- [x] URP restored (Universal pipeline, a 3D Universal renderer). Colour space is Gamma, set by `GameInfoSO`.
- [x] Move the DOSCH pack into `Source~/`; ignore `Source~/` and `Generated/` in git.
- [x] `KmaxAnatomy` and `KmaxAnatomy.Editor` assemblies; OBJ and MTL importer with axis change, winding check,
      per-structure meshes, URP Lit materials (solid and transparent twin), textures and one prefab per topic.
- [x] Import heart (9 structures), brain (12), ear (10) and eye (20). Orientation verified by region position.
- [x] Correct `StereoVolume.Clamp` to `ClampToComfort` in the usage guide.

## Done — 2026-09-30 (platform)

- [x] Vendor both Kmax SDKs under `Plugins/Kmax/` with `.meta` files intact; drop the SDK's own `Samples` folder.
- [x] Port the domain-neutral display framework, the editor tooling and the SDK backend switch; decouple
      `ViewerFlyController` and `StylusNavigation` via `IViewResetHandler` and `ViewDragGate`.
- [x] Assembly definitions; compile-verify with MSBuild, then in the Editor.
- [x] Write the `docs/` set plus `kmax-usage-guide.md`.

## Next

The user's build order is complete (eye, breathing, skull, the body map hub), and so are the two activities that were held back until
the scenes were reviewed. In priority order. None of these needs hardware; what does is under "Needs hardware".

1. [ ] **The user's review** of the launcher, every scene and both activities (the Game view, F1 to F10), and the changes it asks for.
       These come before everything below. *Done when:* the user has a list of changes, or has signed the scenes off.
2. [ ] **Polish.** A post-processing Volume through `StereoPostProcessing`, MSAA in the URP asset (thin lines and vessels alias at
       1x), a subtle backdrop, a pass on emphasis strength, ghost opacity and the rings' width, and brighter cut faces in the scan
       (`AnatomySectionMaterials`). *Done when:* every topic has been looked at again in the Game view and the console is clean.
3. [ ] **Windows Standalone build** through the template's build tooling; add `Scene/Main.unity` to `GameInfoSO`. *Done when:* a build
       opens on the launcher and every topic loads. Topics load from `Resources`, and the materials reference the Glow and Section
       shaders directly. `ComfortOverlay` is the one runtime `Shader.Find` (URP Unlit): keep it out of the shipped scene or include that shader.
4. [ ] **`Kmax -> Audit Comfort Volume`** clean for every topic, zoomed and turned, and for the two activities (the loose organs float
       50 mm in front of the glass, and the lens's outline reaches 30 mm nearer than the tip). *Done when:* the audit reports nothing outside the budget.
5. [ ] **Commit,** only when the user asks. The nested repo is on `main`; the user committed the exhibit before the launcher
       (`46327ec`), and the launcher, the glass interface and the docs since are uncommitted. `Generated/` (which now holds the launcher's
       pictures) and `Source~/` stay ignored, and no DOSCH data is committed.
6. [ ] **Clinical review** of every topic's text and of the activities' cards. Not a coding task: ask the user who reviews it.

Ideas that were pitched and not agreed are in the backlog in [roadmap.md](roadmap.md). Do not start them unasked.

## Needs hardware

- [ ] **Judge the half-size interface.** Body text is 18 canvas units (about 5.6 mm), buttons 46 units (about 14 mm), badges 32 with a 56
      unit hit area. Does the caption read at 0.5 m, and can the pen hit a button and a badge? `AnatomyUiStyle.Scale` is one number; rebuild
      the scene to change it.
- [ ] Judge the launcher through the glasses: does the model fuse at 30 mm out, do the dust motes (-90 to +210 mm) read as depth or as noise,
      does the slate backdrop (`AnatomyLauncher.backdropColor`) suit, are the cards easy to hit?
- [ ] Judge the beam: are the four colours (cyan, emerald, amber, violet) told apart, and does the bead at its end show? The HDR emission is
      limited to the display range until the cameras have bloom (`StylusBeam.limitToDisplayRange`).
- [ ] Listen to the music bed (`PersistentAudioDirector.musicVolume`, 0.14) and the button tick and drop on the display's speakers; the
      levels were set blind.
- [ ] Verify `Kmax → SDK Backend` shows XR Core checked, and that switching both ways works.
- [ ] Run the Main scene on the 27" display: head tracking, convergence, stylus pose, buttons, haptics.
- [ ] Confirm which pen button is primary and reconcile `KmaxStylus.PrimaryKey` with `StylusNavigation`. The pen's
      zoom gesture is on the same button as UI clicks until this is settled.
- [ ] **Measure `StylusTip.tipOffset` on the physical pen** and record it in `roadmap.md`.
- [ ] Judge depth, text legibility (36 pt body at 0.5 m; 36 pt numbers in 64-unit badges) and thin-vessel shimmer.
- [ ] **Judge the glass.** Do the receded structures fuse, with the focus showing through? If not, raise
      `ghostOpacity` or fall back to dimming (one field on `StructureHighlight`).
- [ ] Judge the zoom: does the model overlapping the caption and the buttons at 2x cause discomfort where the
      overlapped part is in front of the glass?
- [ ] Judge the heartbeat's size (`HeartbeatBehaviour.amplitude`, 1.25), the exploded brain's offsets, and the ear:
      the magnified bones' size and lift, the vibration (`HearingBehaviour.amplitude`), and whether the amber rings
      read through the glasses.
- [ ] Listen to the heartbeat and ambience on the display's speakers; the levels were set blind.
- [ ] Judge the body map through the glasses: do the additive layers fuse, can the pen hit an eye (a 26 mm sphere, about
      7 mm on the screen) and an ear, are the layer buttons at the left edge easy to read and press.
- [ ] Tune the shell on site: `attractAfterSeconds` (45), `returnAfterSeconds` (120) and `attractStepSeconds` (7) are
      guesses, and `HeadTracker.EyeVisible` counting as a visitor is unproven.
- [ ] Judge the scan on the glass: does the lens's hole fuse (a ring on a surface at one depth round a view of another); is 22 mm
      the right radius (`ScanBehaviour.lensRadius`); do the flat cut-face colours read as solid; can the pen be held at a depth for a
      moment and read the depth in centimetres?
- [ ] Carry an organ: is `StylusGrab.followTime` (0.04 s) right for a pen in the air; are the snap distances (half an organ's size, at
      least 20 mm, depth counting 0.3) too generous or too strict; are the touch, the chime and the haptic pulse welcome? If touching an
      organ proves too hard, set `StylusGrab.Selection` to Ray.
- [ ] Is a hint after 25 s too soon or too late for an unattended kiosk, and does anybody press Play again?

## Blocked

- Nothing for technical work. DOSCH licensing is `Pending DOSCH Approval` and does not gate development.

## Known gaps

- The organ puzzle has ten organs; the pancreas, gall bladder, bladder, windpipe and gullet are only in the scan. The scan's gall bladder
  is the bile ducts and under a millimetre thick, so the pen names it only by touch (within 3 mm), and the tip has to be that close.
- The scan's cut faces are one flat colour for each structure, not a section of what is inside. Where the pack makes two organs overlap
  (the windpipe's branches in the lungs, the bile ducts in the liver) the organ with the nearer surface is the one named.
- The scan's lens follows the pen's tip and not its direction, so tilting the pen changes nothing. A mouse drives it at the depth the
  wheel sets.
- An organ let go away from its place stays where it was let go, with no sound; nothing says that it was wrong.
- The chime's pitches, the fanfare and `StylusHaptics.Success` were set blind.
- In about one layout of the loose organs in 150 the packer cannot keep the whole gap, and a bounding box touches a button or a
  neighbour by up to 3 mm (measured over 600 seeds). A restart when it fails would remove that; it is not done.
- The body map is a bust: no legs, pelvis, forearms or hands. The crop fades the lowest ribs and the abdominal organs.
- The jaw-and-teeth region has no texture, because the jaw and the teeth use different atlases, so it is one plain
  bone-coloured tint. The heart and the eye are drawn in one averaged tint of their groups, with their atlas.
- The brain's target has a gap about a millimetre wide down the middle, where the two hemispheres part.
- Vessels and nerves are thin lines at the opening zoom, and faint; the nerves layer is mostly the spinal cord.
- The attract mode makes no cue sound of its own, but the music bed plays under it as it does everywhere.
- The launcher's previews of the eye and of the skull and face are still: their behaviours work on soft colliders, which a preview does not
  have. The heart, the breathing chest, the ear and the body map move.
- The launcher's cards show their names alone on a fresh clone, until the models are imported and **Launcher Thumbnails** is run.
- The body map keeps its two activity links, which the launcher's cards now duplicate. Remove `links` from `body.json` if one way in is
  wanted.
- The launcher and the topics' screens were laid out at half size by scaling the first design, not by redesigning it: the caption is
  still a card low in the middle and the buttons a column at the right, not the top-left and top-right panels of the house style's wording.
- A second press on a picked region explores it, so on the body map a region cannot be put down by pressing it again.
- The launcher and the glass interface are not committed. The user committed everything before them on `main` (`46327ec`).
- The DOSCH eye's macula lies about 24 degrees above the optic axis, so the light rays meet it at an angle.
- The breathing topic has no heart between its lungs; the ribs, cartilage and breastbone are the only solid bone.
- The DOSCH ossicles are about 40% of real size (the stirrup is 1 mm; a real one is about 3 mm), so the text gives real
  sizes, not the model's. The DOSCH "cochlea" is the whole bony labyrinth, snail and semicircular canals.
- While the ear is magnified the stirrup is not touching the inner ear, which stays at true size.
- Rings are hidden inside the ear canal while it is opaque; they are visible through the glass when magnified or
  when another structure is picked.
- The anatomical text for every topic, and the cards of the two activities, is a draft written for a lay audience and needs
  clinical review before public use. So do the heartbeat's amplitudes and timings.
- The DOSCH left atrium is an open shell, almost invisible from the front. Both faces are drawn to help. The brain's
  `cerebrum` is the pale inner core under the lobes, and is named that way.
- Deep structures that the brain's core encloses (thalamus, chiasma) stay inside it when exploded; small ones
  such as the 20 mm thalamus are still small at the 2.2x limit.
- Badge columns are fixed at 420 canvas units, so a zoomed model is overlapped by the badges.
- Colliders stay at the rest pose while the heart beats, so picking is off by up to 10 mm at the height of a beat.
- `StereoVolume`'s comfort-zone constants (`0.37`, `0.80` from the camera) are lifted from `XRRig.DrawFrustum`
  gizmo code, not from SDK API. Re-verify on any SDK update.
- `KmaxStylus.PrimaryKey` ships as Middle in the SDK's `pen.prefab`, so UI clicks come from pen button 2, which
  `StylusNavigation` uses for the zoom gesture. The docs assume button 0 is the click button.
- `StylusGrab` and `StylusNavigation` both default to pen button 0, and nothing coordinates them.
- `ViewerFlyController` hardcodes 0.5 m and a 0.12–1.2 m dolly range; the built scene narrows it to
  0.44–0.50 m and no longer moves the camera for dolly input.
- The ported framework files are LF without a BOM, which `.editorconfig` does not want; `dotnet format`
  flags every one. New code follows `.editorconfig` and passes. The old files are left alone to keep diffs scoped.
- Some ported comments still refer to the source project's Stack, Bloom and Probe games and its `S0-x`
  spikes, and `ProceduralAudio` still carries two Volvo engine synths.
- The template logs `[CheckforPlugin] Beesly` errors because this repo has no PluginInfo asset yet.
- `GameInfoSO` lists portrait orientation, which is irrelevant on Windows Standalone.
- The Editor's `manage_camera screenshot` writes into the parent project's `Assets/Screenshots`; delete it after use.
