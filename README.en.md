<div align="center">

[简体中文](README.md) ｜ **English**

</div>

<h1 align="center">DuoFlow</h1>

<p align="center">
A lid-aware desktop visual effects system for Windows 11 laptops<br>
Make the desktop picture bend with the hinge as the laptop opens and closes — like the screen of a foldable device.
</p>

<p align="center">
<a href="https://github.com/ChenChen913/DuoFlow/actions/workflows/m0-windows-build.yml"><img src="https://github.com/ChenChen913/DuoFlow/actions/workflows/m0-windows-build.yml/badge.svg" alt="CI"></a>
<img src="https://img.shields.io/badge/status-M1%20complete-green" alt="status">
<img src="https://img.shields.io/badge/platform-Windows%2011-lightgrey" alt="platform">
<img src="https://img.shields.io/badge/tech-C%23%20%2B%20D3D11%20%2B%20HLSL-purple" alt="tech">
</p>

---

## Table of contents

- [What is this](#what-is-this)
- [Current status](#current-status)
- [Quick start](#quick-start)
- [How it works](#how-it-works)
- [Core design principles](#core-design-principles)
- [Why "DuoFlow"](#why-duoflow)
- [Tech stack](#tech-stack)
- [Documentation map](#documentation-map-the-ai-coding-constitution)
- [Roadmap](#roadmap-summary)
- [Project structure](#project-structure-current)
- [Privacy](#privacy)
- [License](#license)

## What is this

DuoFlow is a desktop visual effects program that runs on Windows 11:

> When the user "closes" the laptop lid, the Windows desktop picture folds around a hinge — sharp and bright at the hinge, sinking progressively into frosted blur and shadow toward the far edge, like a foldable screen being carried away by glass.

The effect is not a simple fade or gradient — it is a continuous animation composed of **perspective folding + physical depth-of-field frost + light falloff**, driven in real time by the open/close progress (0→1).

The intended experience:

```text
Drag the progress (or, later, drive it by the real lid)
→ the desktop folds in perspective around the hinge
→ the far edge melts into frosted blur and shadow
→ the picture appears to fold into the hinge → screen off / Windows Sleep
```

Opening the lid plays the exact same animation in reverse.

A truly great version should make the user feel: **the computer did not suddenly play an animation — the screen itself is really folding with the hinge.**

## Current status

**M0 (feasibility) and M1 (rendering MVP) are fully complete ✅; the project is now in M2 visual refinement.**

A demo you can run today:

1. Build and run per [Quick start](#quick-start)
2. Drag the progress slider on the console at the bottom-left
3. The whole desktop folds in real time: sharp and bright at the hinge, progressively frosted and shadowed toward the far edge

- Unit tests 63/63 passing (`dotnet test src/DuoFlow.Core.Tests/DuoFlow.Core.Tests.csproj`)
- Rendering uses a physical depth-of-field model: the gap height drives the frost radius and the light falloff, with mip pre-filtering and Vogel-spiral sampling
- Transparency, click-through and the fold geometry are all verified on real hardware

## Quick start

Requirements: Windows 11 (build 22621+), a D3D11-capable GPU, .NET 8 SDK (8.0.425 verified).

```powershell
git clone https://github.com/ChenChen913/DuoFlow.git
cd DuoFlow
dotnet build src/DuoFlow.App/DuoFlow.App.csproj -p:Platform=x64
.\src\DuoFlow.App\bin\x64\Debug\net8.0-windows10.0.22621.0\DuoFlow.App.exe
```

After it starts, drag the slider on the console at the bottom-left to see the fold; closing the console window exits the whole app.

## How it works

```text
Windows Graphics Capture (GPU capture of the desktop)
→ persistent latest-frame texture (capture and render decoupled; a render
  heartbeat drives the composite at ~30 FPS)
→ fold projection (homogeneous transform around the hinge, driven by progress)
→ physical depth of field: gap height → quadratic ease-in blur radius →
  mip pre-filtered Vogel-spiral sampling
→ light falloff: the far edge sinks into shadow as the frost deepens
→ fullscreen composite (blended with the real desktop through DWM)
```

Four design points:

1. **Capture and render are decoupled** — a static screen produces no capture frames; the render heartbeat keeps animating independently.
2. **Source freeze** — while the fold is visible the capture source is frozen at the last clean desktop, so self-feedback ghosting is eliminated at the source.
3. **Physical depth-of-field model** — one physical quantity (the gap between glass and desktop) drives blur, light falloff and shadow together.
4. **Transparent overlay recipe** — layered window + alpha-0 backdrop brush + click-through; the fullscreen overlay never blocks the mouse.

## Core design principles

1. **Input and rendering are fully decoupled** — the rendering system only consumes `lidProgress = 0.00 → 1.00`; it does not care whether the angle comes from a camera, a sensor, or manual simulation.
2. **The animation must be continuous** — every visual parameter is driven by progress; never "a fixed animation triggered on lid-close detection".
3. **Geometric warp + frost + light falloff**, not a simulated gradient color.
4. **Never fight the Windows power manager** — Display Off ≠ Sleep; a Pre-Sleep Finalization step completes the animation before the system sleeps and never blocks sleep.
5. **Never assume hardware** — camera position/resolution/orientation/FOV are all adapted via Discovery + Calibration + Profile; the camera is just one possible LidState provider.

## Why "DuoFlow"

The project was originally drafted under the tentative name "Lenovo Duo". The naming discussion concluded that:

* "Lenovo" would lock the project to Lenovo hardware; "Win11" would lock it to Windows 11. Such names restrict both future input methods (camera → hinge sensor → HID → vendor API → manual control) and future device brands (Lenovo / ASUS / HP / Dell...).
* Among the shortlist (WinDuo, DuoFlow, DuoFold, HingeFlow, DuoFX), WinDuo was already taken by an existing project, so the final choice was:

> **DuoFlow** — emphasizes the **continuous flow** of the open/close motion, keeps the core concept, and hard-codes neither a brand, a vendor, nor a Windows version.

## Tech stack

| Module | Technology |
| --- | --- |
| Desktop app | C# / .NET 8 |
| Windows UI | WinUI 3 (Windows App SDK 1.8) |
| Screen capture | Windows Graphics Capture (GPU → GPU, CPU Bitmap forbidden) |
| GPU rendering | Direct3D 11 + HLSL (physical depth-of-field fold shader) |
| Camera (planned) | Media Foundation / OpenCV |
| Unit tests | xUnit |

## Documentation map (the AI-coding constitution)

Whether you are Claude Code, Codex, or any other coding agent — **read these documents before starting any task**, instead of scanning the code and guessing:

| Document | The question it answers |
| --- | --- |
| [EXECUTION_PLAN.md](EXECUTION_PLAN.md) | **Where does execution stand, what is next?** (hand-off entry point: checkboxes / status snapshot / progress log — read this first) |
| [docs/PROJECT_SPEC.md](docs/PROJECT_SPEC.md) | **What is the project supposed to do?** (full specification v0.2, incl. camera adaptation layer and Power & Sleep Architecture) |
| [docs/DESIGN_DECISIONS.md](docs/DESIGN_DECISIONS.md) | **What has already been decided?** (DD-001 ~ DD-046, the "fact layer" that constrains future AI agents and refactors) |
| [docs/TODO.md](docs/TODO.md) | **How are tasks defined?** (M0 ~ M12 roadmap and acceptance criteria) |
| [docs/HARDWARE_COMPATIBILITY.md](docs/HARDWARE_COMPATIBILITY.md) | **What does the real environment look like?** (camera adaptation plans, capability levels, field-test records) |
| [docs/TECHNICAL_PROPOSAL.md](docs/TECHNICAL_PROPOSAL.md) | **How was it originally conceived?** (complete proposal from the inception phase, preserving the original design reasoning) |

## Roadmap (summary)

```text
M0  Research & Feasibility   — validate capture / overlay / sensor / camera feasibility ✅
M1  Rendering MVP            — slider-driven fold + frost + light falloff ✅
M2  Visual Refinement        — Gradient + Light Sweep + Demo Mode (in progress)
M3  Camera Provider          — real lid motion drives the animation
M4  Sensor Provider          — prefer real hardware information
M5  Windows Integration      — power events / autostart
M6+ Productization           — settings UI / installer / full test suite
```

See [EXECUTION_PLAN.md](EXECUTION_PLAN.md) for the itemized execution progress.

## Project structure (current)

```text
DuoFlow/
├── EXECUTION_PLAN.md           # execution document (AI agent hand-off entry point)
├── shaders/
│   └── DuoWarp.hlsl            # fold projection + physical DoF + light falloff shader
├── src/
│   ├── DuoFlow.App/            # WinUI 3 app (overlay / console / render pipeline)
│   ├── DuoFlow.Core/           # LidState / AnimationEngine / provider contracts
│   ├── DuoFlow.Core.Tests/     # xUnit unit tests
│   ├── DuoFlow.Capture/        # Windows Graphics Capture & D3D interop
│   └── DuoFlow.Render/         # fold geometry / render parameters (pure C#, testable)
├── scripts/                    # environment setup / hardware probe
├── docs/                       # spec / design decisions / roadmap / hardware compatibility
└── .github/workflows/          # Windows CI (build / smoke / unit tests)
```

<!-- TODO: 待确认 --> DuoFlow.Camera / DuoFlow.Sensors / DuoFlow.Power are planned modules, landing with M3/M4/M5.

## Privacy

The camera is used only to detect the state of the laptop screen: nothing is uploaded, saved, recorded, or sent to the network by default. Logs never contain screen content or camera frames.

## License

To be decided before the first official release.
