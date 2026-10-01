# Roadmap

## Where the project stands (2026-10-01)

| Area | State |
|---|---|
| Platform: Kmax SDKs, display framework, editor tooling | Done. Compiles clean in the Editor; never run on a Kmax display |
| Content pipeline: DOSCH importer, topic data, validator | Done |
| Six topics: heart, brain, ear, eye, breathing, skull and face | Done. Verified in the Editor |
| Body map hub and kiosk shell | Done. The shell's timings are guesses |
| Two activities: Put the organs back, Pen as instrument | Done. Verified in the Editor with the mouse as the pen |
| The user's review of the scenes and the two activities | Outstanding |
| Polish: post-processing, MSAA, backdrop | Not started |
| Windows Standalone build | Not started |
| Comfort audit of every topic | Not done: only ad hoc checks of depth |
| Hardware verification and tuning | Not started: needs a Kmax display |
| Clinical review of every text | Outstanding |
| Source control | Nothing committed: the nested repo is on a detached HEAD at the initial commit |

## What comes next

1. **The user's review** of every scene and both activities (Editor, F1 to F9). What it asks to change comes before anything below.
2. **Polish** (Phase 5), then a **Windows Standalone build** and a **comfort audit** of every topic (Phase 6). None of these needs hardware.
3. **When a display is available:** Phase 1, then the tuning items under "Needs hardware" in [tasks.md](tasks.md).
4. **Before public use:** clinical review of every text, a decision on languages, and the delivery form of the DOSCH models
   (`Pending DOSCH Approval`).
5. **Backlog:** ideas that were pitched and not agreed (the end of this file). Do not start them unasked.

The detail, with what "done" means for each, is in [tasks.md](tasks.md).

## Phase 0 — Platform (done)

Kmax SDKs vendored, display framework ported and compile-verified, docs written.

## Phase 1 — First light on hardware (runs alongside phases 2 to 4)

The framework has been compile-checked but **never run on a Kmax display in this project**. Content is
being built ahead of this (see [decisions.md](decisions.md)), so this phase now gates final tuning, not
the build.

- Confirm the backend is XR Core (`Kmax → SDK Backend`) and that both assemblies build.
- Run `Scene/Main.unity` on the 27" display. Verify head tracking, stereo convergence, stylus pose,
  buttons and haptics.
- Confirm which pen button the SDK treats as primary. `KmaxStylus.PrimaryKey` ships as Middle; the docs
  and `StylusNavigation` assume button 0. Reconcile them.
- **Measure `StylusTip.tipOffset` on the physical pen** and record the number here. It defaults to zero.
- Judge depth through the glasses and settle the URP MSAA setting: thin vessels shimmer without it.

## Phase 2 — Content pipeline (done)

- DOSCH pack in `Source~/`, git-ignored; importer writes one prefab per topic to `Generated/`.
- Every topic imports and verifies: the six topics, the body map and the two activities.
- Each topic's names, descriptions and tour steps are JSON in `Content/Resources/Topics/`. They are drafts written for a lay
  audience and need clinical review.

## Phase 3 — Topics, in order (done)

1. **Heart (done).** Half of it popping out. Point at a chamber or vessel, or at its numbered badge, for a
   highlight, a haptic tick and a label; the rest turns to glass; previous and next step through the numbers;
   zoom to 2.4x; a chamber-by-chamber tour; a heartbeat that shortens, squeezes and wrings, with audio.
2. **Brain (done).** Twelve regions, each with a function card and a number. An Explode button slides the lobes,
   cerebellum and brainstem apart to show the deep core; a tour follows a message from the eye to the muscles.
3. **Ear (done).** One sound going in, repeated: a tone, rings down the canal, the drum and three bones vibrating, the
   inner ear lighting. Magnify lifts the drum and bones 7.5 times larger; the tour frames itself.
4. **Eye (done).** The eyeball floating, the optic nerve trailing away into depth; it follows the pen with the working
   muscle lit, the pupil closes and the lens thickens as the pen comes near, and explaining the lens shows the light
   coming to a focus on the retina.
5. **Breathing (done).** Ribs, diaphragm, lungs and airway breathe together, twelve times a minute, with rings of air
   in the windpipe; the chest is see-through so the airway shows inside the lungs.
6. **Skull, jaw and face muscles (done).** The jaw opens and closes, the muscles that do the work glow, and a picked
   muscle shows its part of the cycle; twenty muscles and both rows of teeth.
7. **Body map hub (done).** A glowing bust on the glass with five layers (skeleton, muscles, organs, vessels,
   nerves) and six places to explore; point at one, press Explore or press it again, and the screen fades into its
   topic. It is also the kiosk's menu (see Phase 4).
8. **Put the organs back (done).** Ten organs thrown out in front of the glass; carry each home with the pen (`StylusGrab`), hear a
   chime and read its card; a hint, a fanfare and Play again. Opened from a button on the body map.
9. **Pen as instrument (done).** The torso solid in layers, cut by a lens at the pen's tip or by a slice at its depth, by clipping and
   not by transparency; the caption names the organ the pen is in and how deep it is. Opened from a button on the body map.

## Phase 4 — Kiosk shell (built, to be tuned on hardware)

- Done: the body map as the menu, fades, an idle return to the body map, and an attract mode (the body map's tour). A
  tracked pair of eyes counts as a visitor.
- Still to do: tune the timings on site, prove that the head tracker's presence is dependable, and look for a state the
  loop cannot recover from.
- No debug UI, no keyboard or mouse dependence.

## Phase 5 — Presentation (partly done)

- Done: viewer-fixed lighting for a stereo display, a 3D URP renderer (checked), synthesised ambience and cues from
  `ProceduralAudio` with override clips exposed.
- Not started, the polish pass: a post-processing Volume through `StereoPostProcessing`, MSAA in the URP asset, a subtle
  backdrop, emphasis and ghost strengths, and brighter cut faces in the scan.

## Phase 6 — Hardening (not started)

- Comfort audit clean across every topic.
- Focus loss: `HeadTracker` stops tracking and 3D when the app loses focus, so the installation must keep
  the OS from taking it.
- Long-session soak test on hardware.
- Windows Standalone x64 build and install.

## Open questions

- Which languages beyond English? (`GameInfoSO` has `usesLocalization: 0`; the exhibit is English only.)
- Who reviews the anatomical text before public use?
- Final delivery form of the DOSCH models: `Pending DOSCH Approval`.

## Backlog

Pitched at the start of the project, or noticed since. **None of it is agreed or started; ask before beginning any.**

- A "find it" quiz with a haptic right and wrong.
- Male and female pelvis side by side. **This touches the exclusion of reproductive structures** (the scope decision of
  2026-09-30), so ask the user first.
- Hand and foot bones and tendons. Needs a finger rig.
- The activities: a soft buzz and a nudge back for a wrong drop (`StylusHaptics.Error`); a lens that follows the pen's direction; true
  cross-sections at the cut (needs a stencil pass); more organs in the puzzle (needs a way to hold thin ones); an attract mode for each.
- Automated tests for the plain-C# types that were written without Unity dependencies for the purpose (`KioskFlow`,
  `MarkerLayout`, `OrganScatter`, `OrganProbe`, `AnatomyExplorer`). There are none yet; everything has been checked with Editor harnesses.
- Localisation, once the question above is answered.
