<div align="right">

**简体中文** ｜ [English](README.en.md)

</div>

<h1 align="center">DuoFlow</h1>

<p align="center">
Windows 11 笔记本开合视觉特效系统<br>
让桌面画面随笔记本开合，像折叠设备一样随铰链产生空间折叠的视觉效果。
</p>

<p align="center">
<a href="https://github.com/ChenChen913/DuoFlow/actions/workflows/m0-windows-build.yml"><img src="https://github.com/ChenChen913/DuoFlow/actions/workflows/m0-windows-build.yml/badge.svg" alt="CI"></a>
<img src="https://img.shields.io/badge/status-M1%20complete-green" alt="status">
<img src="https://img.shields.io/badge/platform-Windows%2011-lightgrey" alt="platform">
<img src="https://img.shields.io/badge/tech-C%23%20%2B%20D3D11%20%2B%20HLSL-purple" alt="tech">
</p>

---

## 目录

- [这是什么](#这是什么)
- [当前状态](#当前状态)
- [快速开始](#快速开始)
- [工作原理](#工作原理)
- [核心设计原则](#核心设计原则)
- [为什么叫 DuoFlow](#为什么叫-duoflow)
- [技术栈](#技术栈)
- [文档导航](#文档导航ai-coding-项目宪法)
- [开发路线](#开发路线摘要)
- [项目结构](#项目结构当前)
- [隐私](#隐私)
- [License](#license)

## 这是什么

DuoFlow 是一个运行于 Windows 11 上的桌面视觉特效程序：

> 当用户"合上"笔记本屏幕时，让 Windows 桌面画面产生绕铰链空间折叠的视觉效果——桌面在铰链处保持清晰明亮，向远端逐渐沉入磨砂虚化与阴影，如同折叠设备的屏幕被玻璃带走。

效果不是简单的渐变或淡出，而是**透视折叠 + 物理景深磨砂 + 光影衰减**共同构成的连续动画，由开合进度（0→1）实时驱动。

最终体验：

```text
拖动进度（或未来由开合驱动）
→ 桌面绕铰链产生透视折叠
→ 远端逐渐沉入磨砂虚化与阴影
→ 画面像被折入铰链 → 屏幕关闭 / Windows Sleep
```

打开则完全反向。

目标体验：**不是电脑突然播放了一个动画，而是电脑的屏幕真的随着铰链在折叠。**

## 当前状态

**M0（可行性）与 M1（渲染 MVP）全部完成 ✅，现已进入 M2 视觉打磨。**

现在就能玩的演示：

1. 按[快速开始](#快速开始)构建并运行
2. 拖动左下角控制台的进度滑块
3. 整个桌面实时折叠：铰链处保持锐利明亮，远端逐渐磨砂化并沉入阴影

- 单元测试 63/63 通过（`dotnet test src/DuoFlow.Core.Tests/DuoFlow.Core.Tests.csproj`）
- 渲染采用物理景深模型：缝隙高度驱动磨砂半径与光衰减，mip 预过滤 + Vogel 螺旋采样
- 透明度、点击穿透（click-through）、折叠几何均已真机验证

## 快速开始

要求：Windows 11（build 22621+）、支持 D3D11 的 GPU、.NET 8 SDK（8.0.425 验证通过）。

```powershell
git clone https://github.com/ChenChen913/DuoFlow.git
cd DuoFlow
dotnet build src/DuoFlow.App/DuoFlow.App.csproj -p:Platform=x64
.\src\DuoFlow.App\bin\x64\Debug\net8.0-windows10.0.22621.0\DuoFlow.App.exe
```

运行后拖动左下角控制台的滑块即可看到折叠效果；关闭控制台窗口即整体退出。

## 工作原理

```text
Windows Graphics Capture（GPU 捕获桌面）
→ 常驻最新帧纹理（捕获与渲染解耦，渲染心跳 ~30 FPS 独立驱动）
→ 折叠投影（绕铰链的单应变换，progress 驱动）
→ 物理景深：缝隙高度 → 二次缓入模糊半径 → mip 预过滤 Vogel 螺旋采样
→ 光影衰减：远端随磨砂加深沉入阴影
→ 全屏合成（半透明预算经 DWM 与真实桌面混合）
```

四个设计要点：

1. **捕获与渲染解耦**——静态屏幕不产生捕获帧，渲染心跳独立驱动，互不拖累
2. **源冻结**——效果可见期间捕获源冻结在效果出现前的纯净桌面，自反馈重影从源头消除
3. **物理景深模型**——模糊与光影由同一物理量（玻璃与桌面的缝隙高度）驱动，浑然一体
4. **透明层配方**——分层窗口 + alpha-0 背景画刷 + 点击穿透，全屏覆盖层不挡鼠标

## 核心设计原则

1. **输入层和渲染层完全解耦** — 渲染系统只消费 `lidProgress = 0.00 → 1.00`，不关心角度来自摄像头、传感器还是手动模拟。
2. **动画必须连续** — 所有视觉参数由 progress 驱动，不做"检测到合盖就播一个固定动画"。
3. **几何变形 + 模糊 + 光影衰减**，而不是模拟一个渐变色。
4. **不与 Windows 电源管理对抗** — Display Off ≠ Sleep；采用 Pre-Sleep Finalization 在系统睡眠前完成动画，绝不阻止系统睡眠。
5. **不假定硬件** — 摄像头位置/分辨率/方向/FOV 全部通过 Discovery + Calibration + Profile 适配；摄像头只是 LidState 的一种 Provider。

## 为什么叫 DuoFlow

项目立项时曾以「Lenovo Duo」为暂定名。命名讨论的结论是：

* 「联想」会把项目锁死在 Lenovo；「Win11」会锁死在 Windows 11；这类名字限制了未来输入方式（摄像头 → Hinge Sensor → HID → 厂商 API → 人工控制）和设备品牌（Lenovo / ASUS / HP / Dell...）的扩展。
* 备选名单中 WinDuo、DuoFlow、DuoFold、HingeFlow、DuoFX 为第一梯队；考虑到 WinDuo 与已有项目重名，最终选定：

> **DuoFlow** —— 强调开合过程中的**连续流动**，保留核心概念，又不把品牌、厂商、Windows 版本写死。

## 技术栈

| 模块 | 技术 |
| --- | --- |
| 桌面程序 | C# / .NET 8 |
| Windows UI | WinUI 3（Windows App SDK 1.8） |
| 屏幕捕获 | Windows Graphics Capture（GPU → GPU，禁止走 CPU Bitmap） |
| GPU 渲染 | Direct3D 11 + HLSL（物理景深折叠着色器） |
| 摄像头（规划） | Media Foundation / OpenCV |
| 单元测试 | xUnit |

## 文档导航（AI Coding 项目宪法）

无论是 Claude Code、Codex 还是其他 Coding Agent，**每次开始任务先读这几份文件**，而不是扫描代码后自行理解项目：

| 文档 | 回答的问题 |
| --- | --- |
| [EXECUTION_PLAN.md](EXECUTION_PLAN.md) | 项目**执行到哪一步了、接下来做什么**（交接入口：进度打勾 / 状态快照 / 进度日志，AI Agent 接手必读） |
| [docs/PROJECT_SPEC.md](docs/PROJECT_SPEC.md) | 项目**要做什么**（完整规格说明 v0.2，含摄像头适配层与 Power & Sleep Architecture） |
| [docs/DESIGN_DECISIONS.md](docs/DESIGN_DECISIONS.md) | 项目**已经决定怎么做**（DD-001 ~ DD-046，约束后续 AI 与重构的"事实层"） |
| [docs/TODO.md](docs/TODO.md) | 项目**任务如何定义**（M0 ~ M12 开发路线图与验收标准） |
| [docs/HARDWARE_COMPATIBILITY.md](docs/HARDWARE_COMPATIBILITY.md) | 项目**真实环境到底是什么**（摄像头适配预案、能力等级、实测记录库） |
| [docs/TECHNICAL_PROPOSAL.md](docs/TECHNICAL_PROPOSAL.md) | 项目**最初怎么想的**（立项阶段完整技术方案，保留原始设计推理） |

## 开发路线（摘要）

```text
M0  Research & Feasibility   — 验证捕获 / Overlay / 传感器 / 摄像头可行性 ✅
M1  Rendering MVP            — 滑杆驱动 折叠 + 磨砂 + 光影 ✅
M2  Visual Refinement        — Gradient + Light Sweep + Demo Mode（进行中）
M3  Camera Provider          — 真实开合驱动动画
M4  Sensor Provider          — 优先使用真实硬件信息
M5  Windows Integration      — 电源事件 / 开机自启
M6+ Productization           — 设置界面 / 安装包 / 完整测试
```

执行进度逐项见 [EXECUTION_PLAN.md](EXECUTION_PLAN.md)。

## 项目结构（当前）

```text
DuoFlow/
├── EXECUTION_PLAN.md           # 项目执行文档（AI Agent 交接入口）
├── shaders/
│   └── DuoWarp.hlsl            # 折叠投影 + 物理景深 + 光影衰减着色器
├── src/
│   ├── DuoFlow.App/            # WinUI 3 应用（覆盖层 / 控制台 / 渲染管线）
│   ├── DuoFlow.Core/           # LidState / AnimationEngine / Provider 契约
│   ├── DuoFlow.Core.Tests/     # xUnit 单元测试
│   ├── DuoFlow.Capture/        # Windows Graphics Capture 与 D3D 互操作
│   └── DuoFlow.Render/         # 折叠几何 / 渲染参数（纯 C#，可单测）
├── scripts/                    # 环境准备 / 硬件探针
├── docs/                       # 规格 / 设计决策 / 路线图 / 硬件兼容性
└── .github/workflows/          # Windows CI（构建 / 冒烟 / 单测）
```

<!-- TODO: 待确认 --> DuoFlow.Camera / DuoFlow.Sensors / DuoFlow.Power 为规划中的模块，随 M3/M4/M5 落地。

## 隐私

摄像头只用于检测笔记本屏幕状态：默认不上传、不保存、不录制、不联网。日志不包含屏幕内容与摄像头画面。

## License

暂未确定，正式发布前补充。
