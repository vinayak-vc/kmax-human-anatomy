# Decisions

Newest first. Each entry records what was decided and why, so it does not have to be re-argued.

---

## 2026-10-01 — Two activities: the pen carries organs, and the pen cuts the body

**Decision.** "Put the organs back" and "Pen as instrument" are two more topics, `organs` and `scan`. They open from two
buttons at the top of the body map (`links` in `body.json`: each names a topic and gets a button in the control column) and
are left by the Body map button. They are not regions of the body map, because they are things to do and not places to read
about. Both are made from the same pack, so everything in them is `Pending DOSCH Approval`.

**An activity is a topic that holds still.** A topic may be `stationary`: the view, the zoom and the depth are held, so
`ViewerFlyController.EnableFly` is off, there is no zoom, and the depth keeper does not track it. It may be `selectable:
false`: its structures have no pointer events at all, because the pen's tip does the touching and a ray that also
highlighted and sounded for each structure would only echo it. It may carry a `resetLabel`. A behaviour reaches the rest of the
exhibit through three small interfaces: `ITopicNarrator` (it says what the caption shows, as a `TopicMessage`; a new message with
the same key only updates the progress line, so a counter does not make the caption flicker), `ITopicAction` (it owns the action
button, which is the Explode button's place) and `IResetListener`. `AnatomyBehaviourContext` gives a behaviour the topic data, the
sound, the haptics and the pen's tip. The controller does not name either activity.

**Put the organs back.** The torso opens whole. After 1.1 seconds ten organs (the heart, both lungs, the liver, the stomach, the
spleen, both kidneys, the small and the large intestine) are thrown out to float 50 mm in front of the glass, each leaving a
faint outline of itself where it belongs. The visitor touches one with the pen, holds the button, carries it to its outline and
lets go. Close enough, and it settles home with a rising chime, a haptic pulse and its card in the caption (what it does, and
a fact). The last one sounds a fanfare and sends a wave of glow through the organs, and the action button becomes Play again. A
Hint button, or nothing happening for 25 seconds, pulses the outline of the loose organ nearest its place for 4 seconds. The
organs are the pack's own meshes in the places the pack gives them. The pancreas, gall bladder, bladder, windpipe and gullet are
not in the puzzle: they are too thin or too small to hold and to place. The scan has them.
- *Carried by the pen, not by physics.* A `Grabbable` with `kinematicHold` set is carried by `StylusGrab` moving its transform to the
  tip with the grab's lag (`followTime`, 0.04 s); it keeps its own rotation and stays where it is let go. The framework's default, a
  dynamic body driven by velocity, is for things that collide. These have nothing to knock into, and would need gravity off and
  would drift. The default is untouched. Each organ has a convex collider, because `StylusGrab` finds its grip with `ClosestPoint`,
  which only a convex collider has.
- *Snapping.* An organ must be let go within the larger of 20 mm and half its own size of its place, and an error in depth
  counts 0.3 of an error across the screen, because depth is the hardest thing to judge in the air. A pair settles only in its
  own place (a lung is not at home in the other lung's outline). A miss leaves the organ where it was let go and says nothing.
- *The loose organs are laid out on the glass, not in the room.* Something floating 50 mm in front of the glass looks
  `D/(D+z)` = 1.11 times larger, and as much further from the middle of the screen, than it is. The first layout, in the room's
  space, put organs under the buttons and over the edge of the window. `OrganScatter` packs boxes (a lung is tall and a
  kidney is not, so circles waste the room) in glass coordinates, keeping out of the title, the caption, the buttons
  (`keepClear`) and the organs' own places, so nothing hides an outline; the positions are then divided back to the depth
  they float at. It is a plain static class with a seeded random.

**Pen as instrument.** The torso is drawn solid, in layers (the muscles, the bones, the vessels and sixteen organs), and the
pen's tip cuts into it: everything between the viewer and the tip, within a lens 22 mm in radius round it, is cut away, so
pushing the pen deeper peels the muscle off, then the ribs and vessels, then reaches the organs. A second instrument on the same
button cuts the whole body at the depth of the tip, like a scan slice. The caption names the organ the tip is in or looking at,
with its card, and how deep the pen has gone, in centimetres at the body's real size.
- *Clipping, never transparency.* The `Kmax Anatomy/Section` shader is lit exactly as URP Lit is and discards what is inside
  the cut, so what is behind it is solid. Where a surface is cut away the inside of the surface behind shows, and its back faces
  are drawn flat in the colour of the thing (`_CapColor`), so a cut organ reads as solid and not as a hollow shell. A glowing
  ring marks the lens's edge on every surface it crosses, and a thin disc the slice plane. Nothing is alpha-blended in front of
  anything solid, which is the exhibit's rule for stereo.
- *The cut is global.* `ScanBehaviour` sets five global shader vectors once a frame (`_KmaxLens`, `_KmaxLensAxis`,
  `_KmaxLensColor`, `_KmaxBoxMin`, `_KmaxBoxMax`), so every Section material is cut the same way, nothing is cut while the radius is
  zero (which is what every other topic leaves them at) and no material needs a per-frame update. The scan zeroes them when it is
  destroyed.
- *The lens is a cylinder along the screen's axis,* from the tip's plane towards the viewer, in the room's space. Both eyes see
  the same hole, whichever way the pen is held, and the mouse can drive it (at the depth the wheel sets). The slice is the same cut
  with an enormous radius. Two rings and four lines in the air outline the tunnel.
- *Planar caps were tried and rejected.* A cap drawn at the cut by writing `SV_Depth` is wrong for the hollow muscle sheets,
  which have no inside to fill without a stencil pass. The flat colour of the back faces is right for all of them.
- *The organ under the tip is found from the surfaces, not from colliders.* The convex hull of the windpipe or the gullet, which
  curve down the middle of the chest, contains the heart and both lungs, and 7 of the 16 organs were misread with hulls.
  `OrganProbe` keeps about 1,500 surface points and their normals for each organ. A point is inside an organ if the nearest bit
  of its surface faces away from the point, and where organs overlap (they do, in the pack) the one with the nearer surface
  wins. If the tip is in none, the organ is the first one along the lens's line of sight within 4 mm of it. Organs too thin to
  be inside of are touched instead, and the tip is in one within 3 mm of its surface: the diaphragm (a 2.9 mm sheet), the gall
  bladder (the bile ducts, 0.8 mm), the windpipe (1.2 mm), the gullet (1.5 mm) and the pancreas (2 mm). The measure of an organ's thickness is
  twice its volume over its area.
- *How well it names.* Every organ mesh is closed (no open edges), so counting how many of its triangles a ray crosses (odd is inside) gives
  the truth to compare with. Before the thin organs were added, 549 of 568 points inside organs were named for an organ they were in
  (17 for another, 2 for none), and 616 of 632 points in none were left unnamed. With the thin organs, a pen 1.5 mm from the surface of one is
  named for it 70% (the pancreas) to 89% (the gall bladder) of the time, and otherwise for a neighbour that is nearer: the windpipe and
  the gullet lie against each other.

**Not verified.** Anything on the glass: whether the lens's hole fuses through the glasses (a ring on a surface at one depth
round a view of another); the pen's co-location, which every one of these interactions depends on (`StylusTip.tipOffset` is
still unmeasured); whether 0.04 s of lag feels right for an organ; the snap distances and the 25 second hint; the sounds and
haptics; the lens radius; and the clinical text of the cards. If touching an organ proves too hard, `StylusGrab.Selection` can be
set to Ray.

---

## 2026-10-01 — The kiosk shell is a flat three-state loop, and it fades through the canvas

**Decision.** `KioskShell` (a MonoBehaviour on the `Exhibit` object) runs the exhibit unattended. Its clock and states are
`KioskFlow`, plain C# with no Unity types. The exhibit opens on the body map (`startTopicId` and `hubTopicId` are both
`body`). A visitor exploring a region raises `AnatomyTopicController.TopicRequested`; the shell fades the screen to black,
loads the topic, and fades back in. A Body map button, top right on every topic but the body map, comes back the same
way. With nobody listening to `TopicRequested` the controller just loads the topic, so it still works without a shell.

**The loop.** *Hub* (the body map, waiting), *Topic*, *Attract*. A body map that nobody touches for 45 seconds starts
showing itself: its own tour, stepped every 7 seconds, which turns and zooms the body from place to place. Any activity
ends it and puts the body map back to its opening state. A topic that nobody touches for 120 seconds goes back to the
body map. All three numbers are serialized on the shell and are guesses to be tuned on site. A visitor is anything that
moves the pen or the mouse, presses a key or a button, turns the wheel, or, when the rig has a head tracker, has eyes the
tracker sees (`HeadTracker.EyeVisible`). All times are unscaled.

**The fade.** `ScreenFader` is a black panel, the last child of the interface canvas. That canvas is drawn at render queue
4000 with the depth test off (`UiAlwaysOnTop`), after every 3D pass, so nothing shows through it: not the badges' lines,
not the route rings. A panel on a separate overlay canvas is not safe on the real display, where the SDK composes the two
eyes.

**Also.** The Editor shortcuts (F1 to F7, and F1 to F9 once the two activities were added) moved from the controller into the shell,
so they fade as a visitor's clicks do.
Explore is a button at the top of the control column, shown while what is picked names a topic (`topic` in the structure's
text), and during a tour step. Pressing a picked region a second time explores it too; on the body map there is therefore
no putting a region down by pressing it again, only Reset view.

**Not verified.** That `HeadTracker.EyeVisible` is a dependable sign that someone is there; the three timings; whether a
second press that explores is too easy to do by accident with the pen.

---

## 2026-10-01 — The body map is a bust of glowing layers with the organs set inside it

**Decision.** The hub is one more topic, `body`, so it gets hover, picking, numbered badges, previous and next, zoom, the
tour, the caption and the depth keeper for nothing. Its model is a bust (head, neck and chest) of the DOSCH
low-resolution body, drawn as five see-through layers, with the places that lead into topics set inside it as solid organs.

**Layers.** Skeleton, muscles, organs, vessels and nerves. Each merges all of its source files into one mesh
(`AnatomyBodyImporter`, from `AnatomyBodySources`): 218,000 triangles in all, where one object per structure would be
thousands. They are drawn by the Glow shader in additive, depth-tested form, brightest at the edges with a per-layer floor
(`_Floor`), so layers stack without sorting (the skull showed that stacked alpha blending sorts badly) and read as a
hologram. The vessels keep the pack's colours, red arteries and blue veins, in their vertex colours. A layer button shows
or hides a layer with a fade (`BodyBehaviour`, `AnatomyLayerPanel`); the skeleton and the organs are on when the map opens.

**A bust, not the standing body.** The whole body is 1.8 m tall, so on a 27" screen it would be shown at about a tenth of
its size and an eye would be a speck to point at. The bust is shown about two and a half times larger. `AnatomyCropRegion`
(`AnatomyBustCrop` at the time) fades it out below the lowest ribs and towards the arms, in the alpha of the vertex colours, instead of cutting it, and
drops the triangles that have faded away. Legs, pelvis and hands are not imported at all.

**Regions.** The brain, each eye, each ear, the jaw and teeth, the heart and each lung are real organ meshes drawn solid
(with the pack's atlas when all of a region's files share one), and they are the structures of the model, so the ordinary
highlight turns the rest to glass when one is picked. A small organ is reached through a sphere larger than itself, placed
from the pack's measurements; the brain and the lungs are their own targets. The eyes, ears and lungs come in pairs, one of
each numbered. A ray audit over the screen confirmed each is hit where it should be, and that the heart's target is ahead
of the lungs' and the brain's is the brain (a sphere round it took in the cheeks). `AnatomyStructureInfo.topic` names the
topic a region opens.

**Pending DOSCH Approval.** The body map is made from the same pack, so everything in it is `Pending DOSCH Approval`.

**Not verified.** That the additive layers fuse comfortably through the glasses; that the pen can hit an eye (a 26 mm sphere,
about 7 mm on the screen) reliably; how the layers look from well off the nominal eye position.

---

## 2026-10-01 — The jaw hangs from its hinge; the face muscles stretch by one baked shape

**Decision.** The jaw bone and the lower teeth are rigid, so `JawBehaviour` hangs them from a pivot at the joint in front
of the ears and opens the jaw by turning it, exactly as the eye's globe turns: the colliders turn with them. The
twenty muscles of the face and neck are soft, so `AnatomyJawMotion` bakes one shape, `Open`, into each: a point below the
line where the teeth meet turns with the jaw, one well above it stays, and between the two it fades; a point well ahead of
the joint turns and one behind it, where the neck muscles are fixed to the skull, does not. The joint (from the back of
each side of the jaw bone's upper end), the bite line (from the two rows of teeth) and which way is down (the chin) are
measured from the meshes; the importer stores the hinge and the chin as a two-point `AnatomyRoute` named `jaw`.
`JawBehaviour.OpenTurn` is shared by importer and runtime, as `EyeGazeFrame` is for the eye.

**The cycle.** The jaw opens, is held, closes and rests with the teeth together, every four seconds. The muscles that pull
it down and forward (lateral pterygoid, digastric, mylohyoid, geniohyoid) glow as it opens and while it is held; the
ones that lift it (masseter, medial pterygoid) glow as it closes. Explaining one of them glows it fully in its own part of
the cycle and mutes the others. No audio and no expression animation: a muscle of expression is shown by lighting it.

**Also.** The skull's 265,000 triangles load in 157 ms, because the collider meshes are cooked into the prefab. The skull
is left fully solid: a resting opacity below one gave it grey patches where its transparent twin sorted against itself.
Sixteen of the twenty-six structures are numbered, the ten smaller ones described without a number.

---

## 2026-10-01 — The badges are spread by the closest placement, not by nudging

**Decision.** `MarkerLayout.Spread` now finds the placement closest to where each badge wants to be that keeps the gap,
by pooling neighbours that break it (pool adjacent violators in a space where each badge's place in the order is added
back), and then slides the column into the range. A badge with room does not move; a crowd at one height is spread about
where it wanted to be and pushed along as a group when it meets an end of the range; where the range is too small for
the gap, the gap shrinks evenly.

**Why.** The old version nudged overlapping pairs apart for twelve passes and clamped after each, which left eight badges
that all want the mouth's height piled on top of one another at the foot of the column. The skull has that crowd.

---

## 2026-10-01 — Breathing: one baked breath, a see-through chest, and rings of air

**Decision.** The chest breathes by one blend shape, `Inhale`, baked by `AnatomyBreathMotion` into the ribs, the costal
cartilage, the breastbone, the diaphragm, the airway and both lungs, each by its own smooth field of position so what
touches stays touching. The ribs swing up and out, front ends most (pump handle above, bucket handle below); the
diaphragm's dome drops and its rim rides the lower ribs; each lung fills out from its inner edge, by the heart, and its
base follows the diaphragm down; the airway uses the lung's own field beyond 14 to 40 mm of the midline, so the
branches stay inside the lung as it expands, and lengthens as the windpipe does. The amounts are about three times
real, so a breath reads across a room. `BreathingBehaviour` runs a 5 second cycle (twelve breaths a minute): in over
two fifths, held, out over nearly half, a rest. Rings of air (`RouteRings` along the `air` route measured from the
windpipe) go down as the breath goes in and back up as it goes out, the lungs glow while air moves, and a filtered-noise
breath (`ProceduralAudio.CreateBreath`) sets the pace: the animation reads the sound's own position.

**See-through chest.** The lungs rest at 45% and the ribs, cartilage and breastbone at about 50%, through a new
per-structure `opacity`, so the airway shows through the lungs with nothing picked. The ribs have more edge than
anything else in the pack (63,000 triangles), so the usual glass treatment for a receded structure, a bright rim on
every silhouette, made a white lattice that hid the lungs. `recededSolidity` and `recededGlow` let a structure recede
more quietly than the rest: the ribs do, at 35% and 25%.

**Also.** The pack calls both lungs `lungs` (a second is made `lungs_2`); which is on the patient's left is measured
from where each lies. The chest is 463 mm tall with the voice box, so it is shown at 0.38. The ear's sound route became
a general `AnatomyRoute` (named routes on a model), shared with the air. Route rings are no longer depth-tested, so a
wave can be followed down the ear canal and a breath down the windpipe through their walls.

---

## 2026-10-01 — The eye turns as a pivot plus baked soft tissue, looks at the pen, and shows the light

**Decision.** The eye follows the pen (`StylusTip.Position`, which the mouse drives in the Editor). The rigid parts of
the globe (sclera, cornea, iris, lens, retina, macula, ora serrata) are hung from one pivot at the middle of the
eyeball when the topic loads, and turned by turning it, so their colliders turn with them and picking stays exact. The
soft tissue round the globe (six muscles, the levator and the sheath of the optic nerve) cannot be turned as one, so
`AnatomyEyeMotion` bakes two shapes into each, yaw and pitch: a point within 14 mm of the eyeball's middle turns with
it, one beyond 24 mm stays, and between the two it fades, which bends a muscle from its tendon ring at the back of the
socket to where it grips the globe. The soft structures' colliders are re-baked one a frame (`SoftColliderRefresher`,
0.2 to 0.8 ms each). `EyeGazeFrame` holds the signs, shared by the importer and the runtime, so they cannot disagree.
Checked in model space: the lateral rectus moves the cornea 5.5 mm sideways, the medial the opposite way, the superior up
and the inferior down.

**Gaze.** The direction to the pen is measured from the direction to the viewer and compressed, so a pen near the eye
does not always send it to its limit; then limited to 32 degrees sideways and 26 up or down. With the pen still for six
seconds the eye glances about. While something is being explained it settles to look straight ahead, so it can be seen.
Explaining a muscle turns the eye the way that muscle pulls, and the muscle lights (`StructureHighlight.SetPulse`) in
proportion to how hard it is working. A tour step naming all six cycles through them. A behaviour learns what is being
explained through `IFocusListener`.

**Seeing.** The pupil closes as the pen comes near and the lens thickens to focus on it (`Pupil` and `Focus` shapes
baked into the iris and the lens), or does so slowly by itself while the iris or the lens is explained. Explaining the
cornea, lens, retina or macula shows light: rays from a bead on the eye's axis, in through the cornea and the lens to
the macula (`EyeLightRays`). The bead is placed 0.9 to 1.4 eyeball widths out, brought in until it is inside the depth
budget and inside the window, because the eye often faces the viewer, whose side of the glass has no room. The tour
shows those steps from the side and zoomed to 1.7, where the light comes in from the left at the eye's own depth.

**Also.** Structures can be described without a number (`unnumbered`), because the badge pool holds 16 and the eye has
20; Previous and Next pass them by. A structure can rest at less than full opacity (`opacity`): the cornea rests as faint
glass so the iris and pupil show through it. The eye explodes by sliding the cornea, iris, lens and ora serrata forward
along its own axis; they hang from the pivot, so they stay on the axis as the eye turns. The levator's id in the pack is
cut at 31 characters (`eye_levator_palpae_superioris_m`), which the log showed and a guess would have missed.

---

## 2026-10-01 — The zoom yields to the depth budget

**Decision.** `ComfortDepthKeeper` exposes how deep the content is (`Extent`) and the depth the budget allows
(`Budget`), and `AnatomyZoom` backs off, only while the model would be too deep, to the zoom that fits. Depth grows in
step with the zoom, so the zoom that fits is the current one scaled by how far over budget the model is.

**Why.** The eye is 71 mm long and the budget 430 mm. From the side it fits at zoom 2; end-on it is 436 to 564 mm deep at
the same zoom, and the keeper, which keeps the near edge, lets the optic nerve run 140 to 260 mm past the far limit. A
fixed `maxZoom` low enough for the end-on view would have taken the side views' zoom away. Checked over 22 angles, zooms
and exploded states: zoom 1 fits everywhere, and the cap was needed only at zoom 2.

---

## 2026-09-30 — The ear magnifies its middle ear as one assembly, and its vibration is a baked shape

**Decision.** The ear's drum and three bones are too small to see (the stirrup is 1 mm), and zooming the whole model
cannot fix it: at a scale where the stirrup reads, the pinna alone would be half a metre across and the model would
burst the depth budget. So the exploded view gained a size. A structure's `explodeScale` enlarges it in the exploded
view, and every structure given one grows together, about the middle of them all, so the drum and bones stay joined
exactly as they were. `explodeMm` lifts them towards the viewer (15 mm of the model, about 40 mm on the glass), and
the topic words its own button (`explodeLabel`, `assembleLabel`: Magnify and Shrink back). A structure flagged
`recedesWhenExploded` (pinna, canal, middle-ear chamber, inner ear, nerve, tube) turns to glass while the view is
exploded, and so does the drum until it is pointed at, so the three bones are the solid stars. Picking a structure
that has an `explodeScale` explodes the view: a visitor who picks the stirrup gets to see a stirrup.
`StructureHighlight.SetSize` is how anything changes a structure's size, because the highlight owns each
structure's scale and would put it back at the next change of emphasis.

**Tours.** A tour step may now also set the exploded view (`apart` or `together`), turn the view (`setsView`, `yaw`,
`pitch`) and set the zoom (`zoom`). The ear's tour goes from the outside in without a press: pinna and canal from the
side, the drum and bones magnified, then the ear put back together for the inner ear so the stirrup is seen meeting
it. A tour that an unattended visitor starts must frame itself.

**Why the inner ear is not magnified.** The DOSCH "cochlea" is the whole bony labyrinth: the snail and the three
semicircular canals. Enlarged with the bones it made the assembly 400 px tall and dwarfed them. Left at its true
size it is faint glass behind the magnified chain. The cost is that the stirrup is not touching it while magnified.

**Vibration.** One blend shape, `Vibrate`, baked by `AnatomyEarMotion` into the drum and the three bones, swung either
side of zero (Unity accepts negative weights, and they extrapolate linearly). The drum bulges along the canal, most
at the hammer and not at all at its rim. The hammer and anvil rock together about a line through the top of the
hammer's head, chosen perpendicular to both the handle and the canal so the handle swings along the canal. The
stirrup is carried by the tip of the anvil, so the joints stay closed. Everything is measured from the meshes
(the canal direction from the canal's furthest vertex, the joints from the closest pairs of vertices), so a
re-import adapts. A motion in a mesh grows with the bones, which a transform animation would not. It is slowed
down and exaggerated (0.3 mm of drum travel; the real movement is under a micrometre), and the topic says so.

**The sound.** `HearingBehaviour` runs a 4 second cycle: a soft tone, four amber rings travelling down the canal
(`SoundWaveRings`, along a route `AnatomyEarMotion` measured from the canal and stored as `AnatomySoundPath`), the
drum and bones vibrating as they arrive, the inner ear lighting (`StructureHighlight.SetPulse`, which fills a
glass structure with colour since glass has no glow). The animation reads the tone's own position, so a long day
cannot let the two drift apart. The rings are alpha-blended and warm, because an additive one vanishes against the
pale pinna.

**Shared code.** The heart's blend-shape baking was split: `AnatomyFieldShapes` (field to shape, with normals from the
field's derivative) and `IAnatomyMotion` (the heart and the ear implement it). Re-importing the heart through it
gives bit-identical shapes, checked with a checksum of every delta before and after.

**Checked.** In the Editor, at 22 combinations of angle (yaw -60 to 80, pitch -35 to 45), magnify and zoom, the ear
stays between -116 and +292 mm of depth, inside the budget. Magnified, the bones and drum fill 157 x 324 px of a
usable 355 px band.

---

## 2026-09-30 — The brain explodes by authored offsets, not by a rule

**Decision.** Each structure in a topic's data may carry `explodeMm`, the millimetres it moves in the exploded view,
along the model's own axes (x to the patient's left, y up, z towards the back). The Explode button, shown only
when some structure has an offset, slides every such structure out and back together, eased.
`ExplodedViewBehaviour` moves the structures' transforms and nothing else. Reset puts the model back together.

**Why.** The obvious rule, pushing each structure away from the model's centre, fails on the DOSCH brain:
its four lobes are one mesh each and every one is centred on the midline, so a radial rule cannot separate left
from right and barely moves the deep structures. Authored offsets let the lobes slide out along the axes that
show the core, keep the exploded brain inside the window (the offsets are millimetres of the real model, so they
are tuned once on screen), and are data a reviewer can change without touching code.

**Also.** The DOSCH `cerebrum` is not the whole cerebrum. It is the pale inner core under the four folded lobes,
so the topic calls it the inner cerebrum and says it is mostly white matter. The brain opens turned 50 degrees and
tipped 10, a three-quarter view from the patient's front left, so the lobes separate along both the screen and the
depth. Measured with the mesh, the exploded brain spans -115 to +95 mm in depth at the opening view and -117 to
+108 mm at 40 degrees, inside the budget of -130 to +300 mm.

**Cost.** Deep structures the core encloses (thalamus, chiasma) stay inside it when exploded, because there is
no route out that does not pass through the core. They are reached through their badges and shown through glass.

---

## 2026-09-30 — The heartbeat is baked into the meshes as blend shapes, from one smooth field

**Decision.** The heart beats by turning four blend-shape weights (`Contract`, `Twist`, `Squeeze`, `Pulse`)
up and down. The shapes are generated at import by `AnatomyHeartMotion` from a motion model of a real heart
and stored in the structure meshes; `HeartbeatBehaviour` only times the weights. Nothing was bought or
authored by hand.

**Why.** Scaling each chamber about its own centre, which is what the first version did, opens dark seams
between neighbouring chambers on every beat and moves nothing the way a heart moves. The new field is a
function of position alone and is applied to every vertex of every structure, so surfaces that touch keep
touching. It follows the anatomy: the ventricles shorten towards a nearly still apex (11% of their length),
their walls squeeze in (10%), the apex and base wring against each other (-12 and +5 degrees), the atria
contract just before, and the great arteries swell as blood arrives. The wring unwinds faster than the squeeze
relaxes, as it does in life. Normals are carried through the field's own derivative, so lighting stays right.
The whole heart is 20 thousand vertices, so blend shapes cost nothing.

**Considered and not taken.**

- *Asset Store animated hearts* ([Animated Realistic Heart 3D](https://assetstore.unity.com/packages/3d/characters/animated-realistic-heart-3d-101942),
  [Human Heart Animated](https://assetstore.unity.com/packages/3d/characters/human-heart-animated-138129),
  [Heart Animated](https://assetstore.unity.com/packages/3d/characters/humanoids/humans/heart-animated-11986),
  [Animated Heart AR VR](https://assetstore.unity.com/packages/3d/characters/animated-heart-ar-vr-74323) and others)
  are finished models with their own meshes. They would replace the DOSCH heart and its named, pickable
  structures, not animate them. Not evaluated beyond their listings.
- *Vertex animation textures* bake an animation that already exists; there is none to bake.
- *Soft-body physics* gives a wobble, not a cardiac cycle.
- *Hand-authored shape keys in a DCC tool* would work and remain possible: shapes with the same four names can
  replace the generated ones without touching runtime code.

**Cost and limits.** Amplitude and timing are tuned by eye and by the constants at the top of
`AnatomyHeartMotion`; a clinician has not reviewed them. Colliders stay at the rest pose, which differs from the
drawn surface by under 10 mm at the height of a beat. Changing a constant means re-importing the heart.

---

## 2026-09-30 — Zoom scales the model; the wheel, the pen and the buttons all end in one place

**Decision.** `AnatomyZoom` magnifies the model, up to a per-topic `maxZoom` (2.4 for the heart), about whatever
is in focus. The buttons, the mouse wheel, the W and S keys and the stylus's push and pull all drive it:
`ViewerFlyController` reports them through `DollyInput` and, with `applyDollyToCamera` off, no longer moves the
camera for them. With a structure picked or a tour step showing, zooming closes on that structure and pulls it to
the middle of the screen; at 1x nothing moves. Reset returns to 1x.

**Why.** Dollying the camera only pushes the model further out of the glass, and `ComfortDepthKeeper` would push it
straight back, so the old wheel did almost nothing (its range was 60 mm). Scaling is the only zoom that keeps
the budget. A pen press that lands on the UI does not start a zoom, because `KmaxStylus.PrimaryKey` still ships
as the same button.

**Depth keeper measures the mesh.** Bounding boxes around tilted content have corners far beyond the surface, and
the error grows with scale: at 2.4x the box measurement pushed the heart 130 mm deeper than needed. The keeper
now reduces each mesh once to its outermost vertex along 26 directions and carries those through the glass's
space. At 2.4x the heart's true vertex depth is -115 to +199 mm against a budget of -130 to +300.

**Cost.** Zoomed content overlaps the caption and the buttons, which are drawn over it at the screen plane. Where
the overlapped part of the model is in front of the glass, that is an occlusion and parallax conflict. Behind the
glass it is consistent. To be judged on the hardware.

---

## 2026-09-30 — Numbered markers: badges on the screen plane, leader lines in depth

**Decision.** Each structure a topic describes gets a numbered badge, in the order the topic lists them, so the
data file sets the numbering. Badges stand in two columns, one either side of the model, level with the structure
they name, and a thin line in depth joins each to a point near the structure's middle. Pointing at a badge
previews its structure and pressing it picks it, exactly as for the structure itself. The number also leads
the caption ("3. Right ventricle"). The pool is 16.

**Why.** A structure can be hidden behind others, and one is barely visible from the front (the DOSCH left atrium
is an open shell), so it cannot always be pointed at. A badge is always reachable. Badges are UI on the screen plane,
where stereo is sharpest and the pen has a flat target. Only the lines are 3D, drawn without a depth test so they
always reach their structure. Which column a badge stands in is decided once, when the topic loads, from where its
structure sits; after that only its height follows the model, so numbers do not jump sides as it turns.

**Cost.** The columns are fixed at 420 canvas units from the middle, which clears the heart at 1x and the buttons
on the right. A topic wider than that, or zoomed in, is overlapped by the badges.

---

## 2026-09-30 — Previous and Next are always there; the tour shares them

**Decision.** Previous and Next are always shown. Outside a tour they step through the numbered structures,
wrapping at either end; during a tour they step through the tour, Previous from the first step leaving the tour.

**Why.** One pair of buttons that means "the next thing" is easier to learn than two pairs, and it is how a
visitor reaches a structure without aiming at it.

---

## 2026-09-30 — Receded structures turn to glass

**Decision.** When something is being explained, every other structure becomes transparent instead of dark: its
transparent twin material is swapped in, an additive edge glow is drawn over it on a second material slot of
the same renderer, and its opacity falls to about 10%. Pointing at one brings it part-way forward (about 45%) so
it can be read without hiding the focus. Both faces of every structure are drawn, and the ghost fades its
highlights with the rest instead of keeping them at full strength.

**Why.** Dimming made a hidden structure harder to see, not easier: it stayed in front of the thing being
explained. Glass shows the focus through the rest and keeps the rest readable by its edges. The twin looks the
same as the solid material at full opacity, so the swap is not visible. The edge glow is on the same renderer,
so it follows the heartbeat's deformation for free. Two-sided because several structures are open shells: seen
into from outside, their far wall would vanish. Preserved specular is off because at its default the highlights
stay at full strength however transparent the surface is, and the glass turns milky.

**Cost and risk.** `KmaxRigBuilder.EnsureLitMaterial` still says semi-transparent surfaces in front of solid ones
do not fuse in stereo. That was the source project's finding for flat, featureless panes. The edge glow gives
each ghost high-contrast features at its own depth, which is what fusion needs, but it has to be checked
through the glasses. If it fails, raise `ghostOpacity` or cut the glass and dim instead: it is one field on
`StructureHighlight`.

---

## 2026-09-30 — Orbit by moving the rig, light from the rig, no dolly, and keep depth in budget

**Decision.** The heart (and every topic) is turned by `ViewerFlyController` orbiting the rig around the
model at the world origin. The three directional lights are children of the rig, so the lit side stays put
relative to the viewer. `StylusNavigation`'s dolly is off, and the viewer's distance is limited to
0.44–0.50 m. `ComfortDepthKeeper` then holds the model's near edge 15 mm inside the pop-out limit at every
angle.

**Why.** Reusing the framework's viewer is what AGENTS.md asks for, and it already handles smoothing, the
stylus, and reset. Orbiting the rig would light the model differently at every angle if the lights were in
the world; parenting them to the rig makes it read as a turntable in a studio. Dollying moves the model's
centre through the comfort volume, so scale is set per topic instead. And placing a model once cannot keep
it comfortable: the heart is 248 mm wide at its display scale, so turned side-on it would pop out about
150 mm. Measured in the Editor at 90°, the keeper eased it back 39 mm to a near edge of -115 mm.

**Cost.** Content is always centred on the screen, because the viewer keeps its focal point there. Layout
around it (the caption bar, the buttons) has to stay out of the model's way instead.

---

## 2026-09-30 — Interaction: hover previews, click picks, one tour; the buttons work whichever pen button is primary

**Decision.** Pointing at a structure highlights it and shows its text; clicking pins it and dims the rest;
clicking it again lets go. A guided tour steps through structures in order. Everything runs on the event
system's enter, exit and click, so the mouse and the stylus ray behave identically. The stylus's own
button mapping is left as the SDK ships it.

**Why.** Hover-to-read means an unattended visitor gets an answer with no button at all. It also does not
depend on which pen button the SDK treats as primary, which is still unresolved (see
[tasks.md](tasks.md)). Co-located touch is deferred until `StylusTip.tipOffset` has been measured.

---

## 2026-09-30 — Scope: a 27" unattended kiosk with four topics

**Decision.** The exhibit targets the 27" Kmax display (`XRRig` screen type `Screen27`, a 598 x 336 mm
window) and runs as an unattended kiosk. Four topics, in this order: heart, brain, ear, eye.
Reproductive structures are excluded everywhere: topics, menus and any future body map. English only
for now, with every string keyed so a language can be added later.

**Why.** Set by the project owner. At 27" the comfort volume is 598 x 336 mm across by 130 mm of
pop-out and 300 mm of depth, so a heart (216 x 131 x 114 mm) or a brain (138 x 140 x 159 mm) fits at
true size. `VirtualScreen.ScreenType` is declared `Screen15_6, Screen27, Screen24`, so 27" is index 1,
not the last value. Unattended means attract mode, idle reset and no dependence on a keyboard or mouse.
The heart comes first because it has the most visual impact for the least engineering risk; ear and eye
come last because they need mechanism animation.

**Excluded by name.** Male: `penis`, `prostate_gland`, `scrotum`, `testicles`, `urethra`. Female:
`ovaries`, `vagina_uterus`, and `mammaryL` / `mammaryR` on the conservative reading of "reproductive".

---

## 2026-09-30 — DOSCH assets are source-only, local and git-ignored; licensing is Pending DOSCH Approval

**Decision.** The DOSCH pack lives in `Source~/` and everything generated from it lives in `Generated/`.
Both are ignored by git, so no model data is ever committed. Licensing is a pending commercial
agreement with DOSCH, handled separately: it is recorded as `Pending DOSCH Approval` where it matters
and does not gate technical work. Nothing in the pack is modified, replaced or removed on licensing
grounds.

**Why.** This project is a prototype and the commercial pitch to DOSCH for the licence. Prototype and
final commercial deployment are different things and stay apart: the prototype runs from a local copy
of the pack; the final delivery form, distribution rights and attribution are `Pending DOSCH Approval`.

**How.** `Source~` ends in a tilde so Unity does not import it; the importer reads only the files a
topic needs. A fresh clone has code, docs and data but no models until the pack is placed in
`Source~/` and **Kmax → Anatomy → Import → All Topics** is run. The pack is 661 MB, but the four topics
need 13.5 MB of OBJ and generate 9.3 MB of assets, so size is not a concern at this scope.

---

## 2026-09-30 — Anatomy code is its own pair of assemblies, built on the framework

**Decision.** `KmaxAnatomy` (`Scripts/Anatomy/Runtime`) and `KmaxAnatomy.Editor`
(`Scripts/Anatomy/Editor`) hold everything anatomy-specific. Both reference `KmaxDisplay`;
`KmaxDisplay` references neither.

**Why.** The layering rule in [architecture.md](architecture.md), applied: content depends on the
framework, never the reverse. Same `!KMAX_AIO_K1` constraint as the framework assemblies.

---

## 2026-09-30 — Import DOSCH by parsing the OBJ directly; one prefab per topic

**Decision.** An editor importer reads each OBJ and MTL itself and writes one prefab per topic to
`Generated/Resources/Anatomy/`. Every named material group becomes its own child object with a mesh, a
URP Lit material and a `MeshCollider`. `AnatomyModel` on the root and `AnatomyStructure` on each child
carry the ids.

**Why.** Unity's OBJ importer produces one mesh per file with anonymous submeshes and would import all
526 files at default settings. The material names are the structure names, which is exactly what
selection and labels need, and splitting on them gives every structure its own collider, pivot and
material.

**Details worth keeping.**

- The source is right-handed and Z-up, with the patient facing -Y and their left on +X (measured from
  vertex data). `(x, y, z)` becomes `(x, z, y)` with reversed winding, so the patient faces -Z toward a
  viewer looking down +Z, with their left on the viewer's right. Winding is checked against the file's
  own normals rather than trusted.
- Real size in metres. The model is centred on its bounds and each structure on its own, so a structure
  can be scaled or moved about its own middle.
- Every write updates in place (meshes are refilled, the prefab is saved over), so GUIDs survive
  regeneration.
- DOSCH OBJ files wrap large polygons across lines with a trailing backslash. Without joining them the
  heart loses 9,645 lines to parse errors.
- The MTL tint multiplies the painted atlas texture, which is what URP Lit does with `_BaseColor` and
  `_BaseMap`.
- Verified in the Editor on all four topics, and orientation checked by region position rather than by
  eye: the brain's frontal lobe sits at -36 mm and its occipital lobe at +48 mm.

---

## 2026-09-30 — The kiosk is one persistent scene; topics are prefabs plus data

**Decision.** `Scene/Main.unity` holds the rig, lighting, audio, UI and the kiosk state machine, and
stays loaded. Each topic is a generated prefab plus a data file, instantiated into it by key.

**Why.** One rig means the SDK's stereo overlay is never rebuilt mid-session, transitions can fade
instead of load, and idle reset has one place to live. `Main.unity` never references a DOSCH-derived
asset by GUID, so it stays valid without the pack: a missing topic logs an error instead of breaking the
scene. Adding a topic is content work, not scene wiring.

**Not used.** The template's `StateMachine` module is a push/pop stack of named menu states built on its
own `Singleton` and static `Utility` helpers. A kiosk needs a small flat machine with timed
transitions, so this project has its own.

---

## 2026-09-30 — Build content before the hardware run, and say so

**Decision.** Topic work proceeds ahead of Phase 1 in [roadmap.md](roadmap.md). Everything is verified in
the Editor with the mouse fallback and the numeric comfort audit.

**Why.** The prototype supports a licensing pitch, so a working demonstration comes first. What this
cannot settle is unchanged: stylus feel, `StylusTip.tipOffset`, which pen button is primary, and whether
the depth reads well through the glasses. Those need the hardware and gate final tuning, not the build.

---

## 2026-09-30 — Colour space is Gamma

**Decision.** Keep Gamma, as set by `GameInfoSO` (`colorSpace: 0`). Materials and lighting are tuned for
it.

**Why.** It is the project's configured value. URP Lit is correct in either space, and the XR Core SDK
tells its stereo overlay which space is active.

---

## 2026-09-30 — Port only the domain-neutral half of `kmax-display-example`

**Decision.** Bring across the Kmax SDKs and the display framework. Leave behind every eye-anatomy,
vehicle and showcase-game type, all scenes, all art and all data assets.

**Why.** The subject matter of this project is different. Carrying exhibit code over would have
meant either deleting it later or letting it rot in the tree as a false example of how to build
here.

**Taken:** `StereoVolume`, `StereoVolumeGizmo`, `ComfortOverlay`, `StylusTip`, `Grabbable`,
`StylusGrab`, `StylusHaptics`, `StylusBeam`, `StylusNavigation`, `ViewerFlyController`,
`StereoPostProcessing`, `UiAlwaysOnTop`, `UiButtonMotion`, `ProceduralAudio`, `KmaxSdkBackend`,
`KmaxRigBuilder`, `KmaxComfortValidator`.

**Left behind:** `Eye*`, `Vehicle*`, `Volvo*`, `Exhibit*` content types, `ShowcaseScene` /
`ShowcaseState` / `ShowcaseAudio`, the Stack / Probe / Bloom games, and the SDK's own
`com.kmax.xr.core/Samples` folder.

---

## 2026-09-30 — One namespace, `ViitorCloud.KmaxDisplay`

**Decision.** Merge the source module's two namespaces (`ViitorCloud.KmaxDisplayExample` and
`ViitorCloud.KmaxShowcase`) into one.

**Why.** The split there was historical — an exhibit built first and a showcase suite added later.
What arrived here is a single platform layer with one audience, and a second namespace would have
implied a distinction that does not exist.

---

## 2026-09-30 — Decouple `ViewerFlyController` with an interface and a static gate

**Decision.** Replace its two references to exhibit types with `IViewResetHandler` (implemented by
whatever owns the scene's state) and `ViewDragGate` (a per-owner counted static latch).

**Why.** The camera had a hard reference to `EyeAnatomyController` for reset and read
`EyeScaleBox.SuppressViewDrag` to stand down during a handle drag. Both are real requirements; only
the coupling was wrong. The gate is static rather than a reference for the same reason the SDK's
`KmaxPointer` registry is: a manipulator written next year should suppress the view without the
camera being edited.

**Cost.** `RequestReset()` is now the correct thing to wire a Reset button to, and `ResetView()` is
the lower-level call a handler makes itself. Wiring the button to `ResetView()` silently resets the
camera and nothing else.

---

## 2026-09-30 — `KmaxSdkBackend` lives in its own unconstrained assembly

**Decision.** `Scripts/Editor/SdkBackend/` has its own asmdef with no `defineConstraints`, separate
from `KmaxDisplay.Editor` (which is `!KMAX_AIO_K1`).

**Why.** The menu switches the `KMAX_AIO_K1` define. Constrained like the rest of the module, it
would compile out the moment it was used to select AIO K1 — leaving no way back except editing
Player Settings by hand.

---

## 2026-09-30 — The module targets XR Core, not AIO K1

**Decision.** `KmaxDisplay` and `KmaxDisplay.Editor` are constrained to `!KMAX_AIO_K1`. Both SDKs
are still vendored.

**Why.** The two SDKs declare the same `KmaxXR` namespace and share type names, so only one can
compile at a time. Every ported type is written against XR Core's API. AIO K1 stays in the tree
because it is the backend for K1 hardware and switching is a menu item, not a re-integration — but
this module does not claim to support it today.

---

## 2026-09-30 — SDK vendored under `Assets/`, not as a UPM package

**Decision.** Both SDKs sit under `Plugins/Kmax/` inside the module, copied with their `.meta`
files so GUIDs match the source project.

**Why.** It is how the SDK ships and how the example project consumed it, and preserving the GUIDs
means prefabs and scene references from that project still resolve if any are ever brought across.
`Plugins/Kmax/` is treated as read-only vendor code: no local edits, so an SDK update is a
wholesale replacement.
