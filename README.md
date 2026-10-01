# kmax-human-anatomy

A human anatomy exhibit for the **Kmax** head-tracked stereo display with a 6-DOF stylus.

The module holds the Kmax SDKs, a domain-neutral display framework ported from `kmax-display-example`, and the
exhibit built on it. `Scene/Main.unity` opens on a **body map**: a glowing bust with layers to show and hide and six places to explore, the
**heart**, **brain**, **ear**, **eye**, **breathing** and **skull and face**. Each opens behind a fade and leads back with a Body
map button. Left alone, a topic returns to the body map and the body map shows itself. Two buttons on the body map open the
activities: **put the organs back** (carry each organ home with the pen) and **scan the body** (the pen's tip cuts into the torso).
In the Editor, F1 to F9 switch between the topics, the body map and the activities.

## Layout

```
Plugins/Kmax/       Kmax SDKs — vendor code, unmodified
  com.kmax.xr.core/   XR Core 2.5.2  (default backend)
  com.kmax.xr.aio/    AIO K1 1.2.0   (behind KMAX_AIO_K1)
Scripts/Runtime/    KmaxDisplay        — comfort, stylus, view, rendering, UI, audio
Scripts/Editor/     KmaxDisplay.Editor — scene building, comfort audit, backend switch
Scripts/Anatomy/    KmaxAnatomy        — DOSCH import pipeline and the anatomy exhibit
Source~/            DOSCH pack — git-ignored, and ignored by Unity because of the tilde
Generated/          Everything generated from it — git-ignored
docs/               how to use it, and why it is shaped this way
```

## Quick start

1. Open the project in **Unity 6000.3.9f1**.
2. Check **Kmax → SDK Backend → XR Core 2.5.2** is selected.
3. Read **[docs/kmax-usage-guide.md](docs/kmax-usage-guide.md)** before writing scene code.
4. Build a scene from `KmaxRigBuilder` rather than authoring one by hand.
5. Run **Kmax → Audit Comfort Volume** after every scene change.
6. Place the DOSCH pack in `Source~/` and run **Kmax → Anatomy → Import → All Topics**. Models are never
   committed; a fresh clone has none until this is done.

## The three things that matter most

- **Never hardcode the depth budget.** Read it from `StereoVolume`; it tracks `ViewScale` at
  runtime.
- **Measure `StylusTip.tipOffset` on the hardware.** It defaults to zero, which is wrong, and every
  co-located interaction is only as accurate as that number.
- **Verify on the display, not in the Game view.** A scene that looks fine on a 2D monitor can be
  unusable through the glasses.

## Docs

| File | What it covers |
|---|---|
| [project-overview.md](docs/project-overview.md) | What exists, what does not, where to start |
| [kmax-usage-guide.md](docs/kmax-usage-guide.md) | **How to use the framework.** Read first. |
| [architecture.md](docs/architecture.md) | Assemblies, types, and the reasons behind their shape |
| [decisions.md](docs/decisions.md) | Decisions already made, with their reasons |
| [roadmap.md](docs/roadmap.md) | Phases from here to a shippable exhibit |
| [tasks.md](docs/tasks.md) | Done, next, blocked |
| [ai_handoff.md](docs/ai_handoff.md) | Current state and the next recommended task |

Coding conventions are set by `AGENTS.md` at the project root and `.editorconfig`.
