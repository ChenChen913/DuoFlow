<div align="right">

[简体中文](README.md) ｜ **English**

</div>

# DuoFlow

> A lid-aware desktop visual effects system for Windows 11 laptops.
> Make the desktop picture bend with the hinge as the laptop opens and closes — like the screen of a foldable device.

[![Project Status](https://img.shields.io/badge/status-specification-blue)]() [![Platform](https://img.shields.io/badge/platform-Windows%2011-lightgrey)]() [![Tech](https://img.shields.io/badge/tech-C%23%20%2B%20D3D11%20%2B%20HLSL-purple)]()

---

## What is this

DuoFlow is a desktop visual effects program that runs on Windows 11 laptops:

> When the user opens or closes the laptop lid, the Windows desktop picture plays a continuous animation that looks like the screen of a foldable device folding around its hinge.

The effect is not a simple fade or gradient — it is a continuous animation composed of **perspective warp + hinge mask + localized blur + localized dimming + color gradient + a soft light sweep**.

The intended experience:

```text
Open the laptop → desktop renders normally → start closing the lid
→ the desktop begins to warp in perspective → screen edges darken gradually
→ the area near the hinge blurs progressively → a soft light sweep appears
→ the picture appears to fold into the hinge → screen off / Windows Sleep
```

Opening the lid plays the exact same animation in reverse.

A truly great version should make the user feel: **the computer did not suddenly play an animation — the screen itself is really folding with the hinge.**

## Why "DuoFlow"

The project was originally drafted under the tentative name "Lenovo Duo". The naming discussion concluded that:

* "Lenovo" would lock the project to Lenovo hardware; "Win11" would lock it to Windows 11. Such names restrict both future input methods (camera → hinge sensor → HID → vendor API → manual control) and future device brands (Lenovo / ASUS / HP / Dell...).
* Among the shortlist (WinDuo, DuoFlow, DuoFold, HingeFlow, DuoFX), WinDuo was already taken by an existing project, so the final choice was:

> **DuoFlow** — emphasizes the **continuous flow** of the open/close motion, keeps the core concept, and hard-codes neither a brand, a vendor, nor a Windows version.

## Core design principles

1. **Input and rendering are fully decoupled** — the rendering system only consumes `lidProgress = 0.00 → 1.00`; it does not care whether the angle comes from a camera, a sensor, or manual simulation.
2. **The animation must be continuous** — every visual parameter is driven by progress; never "a fixed animation triggered on lid-close detection".
3. **Geometric warp + blur + mask + lighting + brightness changes**, not a simulated gradient color.
4. **Never fight the Windows power manager** — Display Off ≠ Sleep; a Pre-Sleep Finalization step completes the animation before the system sleeps and never blocks sleep.
5. **Never assume hardware** — camera position/resolution/orientation/FOV are all adapted via Discovery + Calibration + Profile; the camera is just one possible LidState provider.

## Tech stack

| Module | Technology |
| --- | --- |
| Desktop app | C# / .NET 8 |
| Windows UI | WinUI 3 |
| Screen capture | Windows Graphics Capture (GPU → GPU, CPU Bitmap forbidden) |
| GPU rendering | Direct3D 11 + HLSL |
| Camera | Media Foundation / OpenCV |
| Config / install / logging | JSON / MSIX / Serilog |

## Documentation map (the AI-coding constitution)

Whether you are Claude Code, Codex, or any other coding agent — **read these documents before starting any task**, instead of scanning the code and guessing:

| Document | The question it answers |
| --- | --- |
| [EXECUTION_PLAN.md](EXECUTION_PLAN.md) | **Where does execution stand, what is next?** (hand-off entry point: checkboxes / status snapshot / progress log — read this first) |
| [docs/PROJECT_SPEC.md](docs/PROJECT_SPEC.md) | **What is the project supposed to do?** (full specification v0.2, incl. camera adaptation layer and Power & Sleep Architecture) |
| [docs/DESIGN_DECISIONS.md](docs/DESIGN_DECISIONS.md) | **What has already been decided?** (DD-001 ~ DD-034, the "fact layer" that constrains future AI agents and refactors) |
| [docs/TODO.md](docs/TODO.md) | **How are tasks defined?** (M0 ~ M12 roadmap and acceptance criteria) |
| [docs/HARDWARE_COMPATIBILITY.md](docs/HARDWARE_COMPATIBILITY.md) | **What does the real environment look like?** (camera adaptation plans, capability levels, field-test records) |
| [docs/TECHNICAL_PROPOSAL.md](docs/TECHNICAL_PROPOSAL.md) | **How was it originally conceived?** (complete proposal from the inception phase, preserving the original design reasoning) |

## Roadmap (summary)

```text
M0  Research & Feasibility   — validate capture / overlay / sensor / camera feasibility
M1  Rendering MVP            — slider-driven Warp + Hinge Mask + Blur + Dimming
M2  Visual Refinement        — Gradient + Light Sweep + Demo Mode
M3  Camera Provider          — real lid motion drives the animation
M4  Sensor Provider          — prefer real hardware information
M5  Windows Integration      — power events / autostart
M6+ Productization           — settings UI / installer / full test suite
```

Current status: **M0 fully complete ✅ (M0.4 real-machine probe on Legion R7000 APH9: hardware sensors all absent → primary input routes = ACPI lid event + camera vision; probe data backfilled) → now in M1 Rendering MVP. See [EXECUTION_PLAN.md](EXECUTION_PLAN.md) for live execution progress.**

## Project structure (planned)

```text
DuoFlow/
├── EXECUTION_PLAN.md       # execution document (AI agent hand-off entry point)
├── src/
│   ├── DuoFlow.App/        # WinUI 3 app entry & settings UI
│   ├── DuoFlow.Core/       # LidState / AnimationEngine
│   ├── DuoFlow.Capture/    # Windows Graphics Capture
│   ├── DuoFlow.Camera/     # camera discovery / calibration / angle estimation
│   ├── DuoFlow.Sensors/    # sensor providers
│   ├── DuoFlow.Render/     # D3D renderer / overlay
│   └── DuoFlow.Power/      # power event listeners
├── shaders/                # HLSL: Warp / HingeMask / Blur / Dimming / Gradient / LightSweep
├── scripts/                # environment setup / helper scripts
├── config/
├── tests/
└── docs/
```

## Privacy

The camera is used only to detect the state of the laptop screen: nothing is uploaded, saved, recorded, or sent to the network by default. Logs never contain screen content or camera frames.

## License

To be decided before the first official release.
