# Project Overview — Kmax Human Anatomy

## What this is

A Unity module for a **human anatomy exhibit on a Kmax head-tracked stereo display** with a 6-DOF
stylus: an unattended kiosk on the 27" screen, built from the DOSCH 3D Human Anatomy pack
(`Pending DOSCH Approval`). A launcher of nine exhibits: a body map, six topics (heart, brain, ear, eye, breathing, and skull, jaw and face)
and two activities that use the pen: putting loose organs back where they belong, and scanning the body with a lens at the pen's tip.

It is a prototype and the commercial pitch to DOSCH for the licence, so the goal is a working,
polished demonstration of the experience.

## What exists today

| Layer | Location | State |
|---|---|---|
| Kmax SDKs (XR Core 2.5.2, AIO K1 1.2.0) | `Plugins/Kmax/` | vendored, unmodified |
| Display framework (`KmaxDisplay`) | `Scripts/Runtime/` | ported, compiles clean in the Editor |
| Editor tooling (`KmaxDisplay.Editor`) | `Scripts/Editor/` | ported, compiles clean in the Editor |
| Anatomy pipeline (`KmaxAnatomy`) | `Scripts/Anatomy/` | importer done; the six topics, the body map and the two activities import and verify |
| DOSCH pack | `Source~/` | local, git-ignored, not imported by Unity |
| Generated models | `Generated/` | rebuilt from the pack, git-ignored |
| Topic experiences | `Content/Resources/Topics/` | heart, brain, ear, eye, breathing and skull done (numbered parts, glass, zoom; beating, exploding, hearing and magnifying, looking and focusing, breathing, chewing), and `body`, the body map: five glowing layers, six places that lead into the topics and two buttons that lead into the activities, `organs` and `scan` |
| Launcher | `Content/Resources/Launcher/launcher.json`, `Scripts/Anatomy/Runtime/Launcher/` | nine cards, the chosen exhibit's own model turning above them, a Load button; pictures rendered into `Generated/` |
| Scenes | `Scene/Main.unity` | the kiosk: opens on the launcher, fades between it and the topics, shows itself when idle |

The framework was ported from the `kmax-display-example` module. Only the domain-neutral half came
across: the stereo comfort model, the stylus interaction layer, the orbit camera, the stereo-aware UI and
audio helpers, and the scene-building tools.

## Runtime environment

- Unity **6000.3.9f1**, URP 17.3.0 with a 3D Universal renderer, **Gamma** colour space
- Target: **Windows Standalone x64** on Kmax hardware, **27"** display, Direct3D 11 first
- Default SDK backend: **XR Core 2.5.2** (`KMAX_AIO_K1` undefined)
- Namespaces: `ViitorCloud.KmaxDisplay` (framework), `ViitorCloud.KmaxAnatomy` (exhibit), each with an
  `.Editor` counterpart

## Where to start

1. [ai_handoff.md](ai_handoff.md) — current state, how to run and verify, the traps, and the next recommended task. Read first.
2. [architecture.md](architecture.md) — what each type is and why it is shaped that way.
3. [roadmap.md](roadmap.md) and [tasks.md](tasks.md) — where the project stands and what is next.
4. [decisions.md](decisions.md) — decisions already made, with their reasons.
5. [kmax-usage-guide.md](kmax-usage-guide.md) — how to build on the display framework. Read before touching the scene, the pen, the UI
   or rendering.

## Vocabulary

- **Topic:** one screen of the exhibit: a generated prefab (`Generated/Resources/Anatomy/<id>.prefab`) and its JSON
  (`Content/Resources/Topics/<id>.json`). The ids are `heart`, `brain`, `ear`, `eye`, `breathing`, `skull`, `body`, `organs` and `scan`.
- **Structure:** one named part of a topic's model, such as a heart chamber or an organ. Its id is the source material name and never changes.
- **Behaviour:** the code that makes a topic do its own thing (a heartbeat, a jaw, a puzzle). A topic names it in its data.
- **Launcher (hub):** the exhibit's menu, a screen of cards that is not a topic. **Body map:** the `body` topic, one of its cards.
  **Shell:** `KioskShell`, the unattended loop around them.
- **Activity:** a topic that is something to do and not something to read: `organs` and `scan`. It holds the view still and the pen's
  tip, not pointing, does the work.
- **Glass, window:** the screen plane, 0.598 x 0.336 m on the 27". **Volume space:** metres from the middle of the glass, x right, y up,
  z into the screen, so negative z pops out (`StereoVolume`). **Comfort budget:** how far in front of and behind the glass content may go.
- **Ghost:** a structure that has receded, drawn as glass. **Slot:** the outline of an organ's place in the puzzle. **Lens, slice:** the
  scan's two cuts. **Thin organ:** one too thin for the pen's tip to be inside of (the gall bladder's bile ducts, the windpipe); the pen
  is at it when within 3 mm.

## Non-goals

- Supporting both SDK backends simultaneously. The two SDKs collide on type names; the module targets
  XR Core and is compiled out under AIO K1.
- Carrying over the example module's exhibit content, art or scenes.
- Committing any DOSCH model data.
- Reproductive-system content.
