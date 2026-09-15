<div align="right">

**简体中文** ｜ [English](README.en.md)

</div>

# DuoFlow

> Windows 11 笔记本开合视觉特效系统
> 让桌面画面随笔记本开合，像折叠设备一样随铰链产生空间折叠的视觉效果。

[![Project Status](https://img.shields.io/badge/status-specification-blue)]() [![Platform](https://img.shields.io/badge/platform-Windows%2011-lightgrey)]() [![Tech](https://img.shields.io/badge/tech-C%23%20%2B%20D3D11%20%2B%20HLSL-purple)]()

---

## 这是什么

DuoFlow 是一个运行于 Windows 11 笔记本上的桌面视觉特效程序：

> 当用户打开或关闭笔记本电脑屏幕时，让 Windows 桌面画面产生一种类似折叠设备屏幕随铰链发生空间折叠的视觉效果。

效果不是简单的渐变或淡出，而是由**透视变形 + 铰链遮罩 + 局部模糊 + 局部暗化 + 颜色渐变 + 柔和光带**共同构成的连续动画。

最终体验：

```text
打开笔记本 → 桌面正常显示 → 开始合盖 → 桌面产生轻微透视
→ 屏幕边缘逐渐暗化 → 靠近铰链区域逐渐模糊 → 产生柔和光带
→ 画面像被折入铰链 → 屏幕关闭 / Windows Sleep
```

打开则完全反向。

真正优秀的版本应该让用户感觉：**不是电脑突然播放了一个动画，而是电脑的屏幕真的随着铰链在折叠。**

## 为什么叫 DuoFlow

项目立项时曾以「Lenovo Duo」为暂定名。命名讨论的结论是：

* 「联想」会把项目锁死在 Lenovo；「Win11」会锁死在 Windows 11；这类名字限制了未来输入方式（摄像头 → Hinge Sensor → HID → 厂商 API → 人工控制）和设备品牌（Lenovo / ASUS / HP / Dell...）的扩展。
* 备选名单中 WinDuo、DuoFlow、DuoFold、HingeFlow、DuoFX 为第一梯队；考虑到 WinDuo 与已有项目重名，最终选定：

> **DuoFlow** —— 强调开合过程中的**连续流动**，保留核心概念，又不把品牌、厂商、Windows 版本写死。

## 核心设计原则

1. **输入层和渲染层完全解耦** — 渲染系统只消费 `lidProgress = 0.00 → 1.00`，不关心角度来自摄像头、传感器还是手动模拟。
2. **动画必须连续** — 所有视觉参数由 progress 驱动，不做"检测到合盖就播一个固定动画"。
3. **几何变形 + 模糊 + 遮罩 + 光照 + 亮度变化**，而不是模拟一个渐变色。
4. **不与 Windows 电源管理对抗** — Display Off ≠ Sleep；采用 Pre-Sleep Finalization 在系统睡眠前完成动画，绝不阻止系统睡眠。
5. **不假定硬件** — 摄像头位置/分辨率/方向/FOV 全部通过 Discovery + Calibration + Profile 适配；摄像头只是 LidState 的一种 Provider。

## 技术栈

| 模块 | 技术 |
| --- | --- |
| 桌面程序 | C# / .NET 8 |
| Windows UI | WinUI 3 |
| 屏幕捕获 | Windows Graphics Capture（GPU → GPU，禁止走 CPU Bitmap） |
| GPU 渲染 | Direct3D 11 + HLSL |
| 摄像头 | Media Foundation / OpenCV |
| 配置 / 安装 / 日志 | JSON / MSIX / Serilog |

## 文档导航（AI Coding 项目宪法）

无论是 Claude Code、Codex 还是其他 Coding Agent，**每次开始任务先读这几份文件**，而不是扫描代码后自行理解项目：

| 文档 | 回答的问题 |
| --- | --- |
| [EXECUTION_PLAN.md](EXECUTION_PLAN.md) | 项目**执行到哪一步了、接下来做什么**（交接入口：进度打勾 / 状态快照 / 进度日志，AI Agent 接手必读） |
| [docs/PROJECT_SPEC.md](docs/PROJECT_SPEC.md) | 项目**要做什么**（完整规格说明 v0.2，含摄像头适配层与 Power & Sleep Architecture） |
| [docs/DESIGN_DECISIONS.md](docs/DESIGN_DECISIONS.md) | 项目**已经决定怎么做**（DD-001 ~ DD-034，约束后续 AI 与重构的"事实层"） |
| [docs/TODO.md](docs/TODO.md) | 项目**任务如何定义**（M0 ~ M12 开发路线图与验收标准） |
| [docs/HARDWARE_COMPATIBILITY.md](docs/HARDWARE_COMPATIBILITY.md) | 项目**真实环境到底是什么**（摄像头适配预案、能力等级、实测记录库） |
| [docs/TECHNICAL_PROPOSAL.md](docs/TECHNICAL_PROPOSAL.md) | 项目**最初怎么想的**（立项阶段完整技术方案，保留原始设计推理） |

## 开发路线（摘要）

```text
M0  Research & Feasibility   — 验证捕获 / Overlay / 传感器 / 摄像头可行性
M1  Rendering MVP            — 滑杆驱动 Warp + Hinge Mask + Blur + Dimming
M2  Visual Refinement        — Gradient + Light Sweep + Demo Mode
M3  Camera Provider          — 真实开合驱动动画
M4  Sensor Provider          — 优先使用真实硬件信息
M5  Windows Integration      — 电源事件 / 开机自启
M6+ Productization           — 设置界面 / 安装包 / 完整测试
```

当前状态：**Specification 与仓库基建完成，执行进度见 [EXECUTION_PLAN.md](EXECUTION_PLAN.md)。**

## 项目结构（规划）

```text
DuoFlow/
├── EXECUTION_PLAN.md       # 项目执行文档（AI Agent 交接入口）
├── src/
│   ├── DuoFlow.App/        # WinUI 3 应用入口与设置界面
│   ├── DuoFlow.Core/       # LidState / AnimationEngine
│   ├── DuoFlow.Capture/    # Windows Graphics Capture
│   ├── DuoFlow.Camera/     # 摄像头发现 / 校准 / 角度估计
│   ├── DuoFlow.Sensors/    # 传感器 Provider
│   ├── DuoFlow.Render/     # D3D Renderer / Overlay
│   └── DuoFlow.Power/      # 电源事件监听
├── shaders/                # HLSL：Warp / HingeMask / Blur / Dimming / Gradient / LightSweep
├── scripts/                # 环境准备 / 辅助脚本
├── config/
├── tests/
└── docs/
```

## 隐私

摄像头只用于检测笔记本屏幕状态：默认不上传、不保存、不录制、不联网。日志不包含屏幕内容与摄像头画面。

## License

暂未确定，正式发布前补充。
