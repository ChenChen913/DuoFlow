# DuoFlow — Project Specification

> Windows 11 笔记本开合视觉特效系统
> Project Specification / 项目规格说明

**项目名称：** DuoFlow
**项目代号：** `duoflow`
**曾用名：** Lenovo Duo（立项阶段暂定名，后正式定名 DuoFlow，理由见 README）
**目标平台：** Windows 11
**主要设备：** 联想笔记本电脑，兼容其他 Windows 笔记本
**项目状态：** Specification / 开发前
**文档版本：** 0.2.0（已合并摄像头硬件适配与电源架构修订）

---

# 1. 项目概述

## 1.1 项目目标

DuoFlow 是一个运行于 Windows 11 笔记本电脑上的桌面视觉特效程序。

项目的核心目标是：

> 当用户打开或关闭笔记本电脑屏幕时，让 Windows 桌面画面产生一种类似折叠设备屏幕随铰链发生空间折叠的视觉效果。

效果不是简单的渐变或淡出，而是由以下视觉效果共同构成：

* 屏幕透视变形
* 铰链区域遮罩
* 局部模糊
* 局部暗化
* 颜色渐变
* 柔和光带
* 连续、平滑的开合动画

项目不复制 Apple 的具体视觉素材、图形资源或专有实现。

---

# 2. 核心体验

## 2.1 正常打开状态

笔记本正常打开时：

* Windows 桌面完全正常显示
* 不产生任何额外视觉效果
* DuoFlow 可以保持后台运行
* CPU/GPU 占用应尽可能低

此时：

```text
lidProgress = 0.0
```

## 2.2 开始合盖

用户开始关闭屏幕后：

```text
lidProgress > 0
```

视觉效果逐渐出现：

1. 轻微透视变化
2. 靠近铰链区域产生轻微暗化
3. 局部模糊开始出现
4. 颜色发生轻微变化
5. 必要时出现低强度光带

所有效果必须连续变化，不允许突然跳变。

## 2.3 接近完全关闭

当：

```text
lidProgress → 1.0
```

视觉效果逐渐增强：

* 透视变形增强
* 铰链区域变暗
* 铰链区域模糊增强
* 屏幕内容逐渐消失
* 光效逐渐减弱或消失

最终由 Windows 正常执行屏幕关闭、锁定、睡眠或其他系统电源行为。

---

# 3. 非目标

以下内容不属于第一阶段目标：

* 修改 Windows Explorer
* 修改 Windows Shell
* 修改 Windows 任务栏
* 修改 Windows 锁屏 UI
* 修改 Windows 电源管理策略
* 修改 BIOS
* 修改联想固件
* 破解或绕过厂商硬件限制
* 强制阻止系统睡眠
* 永久替换 Windows 系统组件
* 复制 Apple 的专有实现
* 依赖云端 AI 服务才能运行

---

# 4. 核心设计原则

## 4.1 输入与渲染解耦

系统不能假设"开合角度一定来自摄像头"。

所有输入最终必须转换成统一的：

```text
LidState
```

输入来源可以包括：

* Hardware Sensor
* HID
* Windows Sensor API
* Camera
* Manual Debug Input

渲染系统只关心：

```text
progress
```

而不关心 progress 是如何得到的。

---

# 5. LidState 数据模型

核心数据模型：

```csharp
public sealed record LidState(
    double Angle,
    double Progress,
    double Velocity,
    double Confidence,
    LidStateSource Source
);
```

其中：

| 字段           | 含义          |
| ------------ | ----------- |
| `Angle`      | 当前估算开合角度    |
| `Progress`   | 归一化开合进度，0~1 |
| `Velocity`   | 开合变化速度      |
| `Confidence` | 当前检测结果可信度   |
| `Source`     | 数据来源        |

数据来源：

```csharp
public enum LidStateSource
{
    Manual,
    Camera,
    Sensor,
    Unknown
}
```

---

# 6. Progress 定义

统一定义：

```text
0.0 = 完全打开
1.0 = 接近完全关闭
```

示意：

```text
180° ───────── 0.0
150° ───────── 0.2
120° ───────── 0.4
 90° ───────── 0.6
 60° ───────── 0.8
 30° ───────── 1.0
```

实际角度映射必须允许设备校准。

不得假设所有笔记本的机械结构完全相同。

---

# 7. Animation Engine

Animation Engine 接收原始 LidState：

```text
Raw LidState
      ↓
Noise Filtering
      ↓
Smoothing
      ↓
Velocity Calculation
      ↓
Animation State
```

目标：

* 消除摄像头抖动
* 消除传感器噪声
* 避免动画跳变
* 保证视觉连续性

建议使用：

* Exponential smoothing
* Low-pass filter
* 必要时 Kalman Filter

第一阶段不要求复杂算法。

---

# 8. Screen Capture

使用 Windows Graphics Capture 获取当前显示器内容。

原则：

> 尽可能保持 GPU → GPU 数据路径。

禁止第一版采用：

```text
屏幕截图
→ CPU Bitmap
→ CPU 修改
→ 上传 GPU
→ 显示
```

推荐：

```text
Windows Desktop
      ↓
Windows Graphics Capture
      ↓
GPU Texture
      ↓
D3D Renderer
      ↓
HLSL Shader
      ↓
Overlay
```

---

# 9. Rendering Pipeline

渲染系统采用 GPU Shader。

建议初期使用：

* Direct3D 11
* HLSL

渲染 Pipeline：

```text
Desktop Texture
      ↓
Pass 1: Perspective Warp
      ↓
Pass 2: Hinge Mask
      ↓
Pass 3: Blur
      ↓
Pass 4: Dimming
      ↓
Pass 5: Gradient
      ↓
Pass 6: Light Sweep
      ↓
Final Composite
```

第一阶段允许将多个 Pass 合并，以降低开发复杂度。

但逻辑上必须保持模块独立。

---

# 10. Perspective Warp

目标：让屏幕内容产生类似"正在绕铰链折叠"的空间变化。

不能只使用：

```text
Scale
```

必须支持：

```text
Perspective / Projective Warp
```

变形强度由 `progress` 控制。

建议：

```text
progress = 0     → warp = 0
progress = 0.5   → warp = medium
progress = 1     → warp = maximum
```

具体参数必须通过实际视觉测试确定。

---

# 11. Hinge Mask

定义屏幕中的铰链区域。

第一版允许使用：

```text
Vertical / Horizontal normalized distance
```

建立遮罩。

示意：

```text
普通区域
████████████████████

过渡区域
████████████▒▒▒▒▓▓

铰链区域
████████▒▒▓▓░░░░
```

遮罩必须可配置。

不同设备允许调整：

* Hinge Position
* Hinge Width
* Falloff
* Direction

---

# 12. Blur

模糊主要发生在铰链附近。

基本逻辑：

```text
blurAmount =
    hingeMask
    × progress
    × maxBlur
```

要求：

* 正常打开时几乎无模糊
* 合盖过程中逐渐增强
* 不允许整个屏幕同时变成严重模糊
* 模糊必须平滑

第一版可以使用 Gaussian Blur 或近似实现。

---

# 13. Dimming

铰链区域随着合盖逐渐变暗。

基本逻辑：

```text
brightness =
    1.0 -
    hingeMask *
    progress *
    maxDarkness
```

最终参数必须通过视觉测试确定。

---

# 14. Gradient

颜色渐变属于辅助效果。

原则：

> Gradient 不能成为主要视觉效果。

第一版支持：

* 静态渐变
* 基于 progress 的渐变强度
* 可配置色相

后续可以研究：

* 从屏幕内容提取平均颜色
* 根据亮度自动调整
* 根据当前桌面主色生成渐变

但这些不是 MVP 必须功能。

---

# 15. Light Sweep

可选光带。

原则：

* 强度低
* 面积窄
* 速度快
* 不喧宾夺主

建议默认强度：

```text
0.05 ~ 0.15
```

实际值以视觉测试为准。

---

# 16. Overlay Window

视觉效果最终通过透明 Overlay Window 呈现。

Overlay 必须：

* 无边框
* 透明
* 全屏
* 不抢焦点
* 默认不接收鼠标输入
* 不影响用户正常操作

窗口应尽可能做到：

```text
Click Through
No Activation
Topmost / Appropriate Z-order
```

但不得破坏正常 Windows 应用交互。

---

# 17. 多显示器

第一阶段：

> 支持主显示器。

第二阶段：

支持：

* 多显示器
* 独立捕获
* 每个显示器独立渲染
* 主屏/副屏策略

如果用户使用外接显示器：

默认不对外接显示器执行 Duo 效果，除非后续明确支持。

---

# 18. Lid Detection

检测系统采用可插拔 Provider。

接口：

```csharp
public interface ILidStateProvider
{
    bool IsAvailable { get; }

    Task StartAsync();

    Task StopAsync();

    event EventHandler<LidState> StateChanged;
}
```

Provider：

```text
SensorProvider
CameraProvider
ManualProvider
```

---

# 19. Provider 优先级

默认：

```text
Hardware Sensor
        ↓
Camera
        ↓
Manual
```

实际选择必须经过能力检测。

不得假设某个联想型号一定存在可用角度传感器。

---

# 20. Camera Provider

摄像头方案主要用于没有可用硬件传感器的设备。

流程：

```text
Camera Frame
      ↓
Resize
      ↓
Display Detection
      ↓
Geometry Estimation
      ↓
Angle Estimation
      ↓
Smoothing
      ↓
LidState
```

第一版不要求使用大型 AI 模型。

优先尝试：

* Edge detection
* Rectangle detection
* Perspective geometry
* Feature tracking

目标是低延迟和低功耗。

## 20.1 摄像头硬件适配层（修订）

> 修订说明：不同笔记本的摄像头位置、分辨率、视角、方向、裁剪方式都可能不同。
> 摄像头不能被设计成"一个固定位置的传感器"，而是"一个需要经过发现、校准和适配的视觉输入设备"。

摄像头系统采用 4 层架构：

```text
Camera Device
      ↓
Camera Discovery
      ↓
Camera Profile
      ↓
Calibration
      ↓
Vision / Geometry
      ↓
Angle Estimation
      ↓
LidState
```

也就是说，**摄像头本身和"摄像头如何用于测量屏幕开合角度"必须彻底分开。**

要点：

1. **Camera Discovery**：启动时扫描所有摄像头（Camera 0/1/2...），获得 `CameraDevice`（DeviceId、Name、Resolution、FrameRate、PixelFormat、Orientation、FOV、Front/Back、Availability），判断有没有摄像头、有没有权限、能不能打开、支持哪些分辨率/帧率。不得假定"系统里一定有一个普通 RGB 摄像头"。
2. **Camera Profile**：不试图通过"知道摄像头在哪里"解决问题，而是让程序建立 Camera Calibration Profile（首次运行校准：完全打开 → 约90° → 完全合上），程序自己建立 open/closed reference。
3. **自动发现**：视觉算法自己寻找屏幕边缘 → 屏幕矩形 → 屏幕与摄像头的相对几何关系 → 建立参考坐标 → 计算屏幕姿态。不应硬编码 camera position。
4. **归一化坐标**：算法内部绝对不依赖像素坐标，全部使用 `x = 0.0~1.0, y = 0.0~1.0` 归一化坐标。Camera Resolution 与 Vision Coordinate System 彻底分离，分辨率差异因此成为最容易解决的问题。
5. **画面方向**：Camera Profile 必须包含 orientation / mirror / crop 信息，最终全部转换成 Normalized Camera Space。

详细的适配策略、能力等级与铁律见 `HARDWARE_COMPATIBILITY.md`。

---

# 21. Camera Calibration

第一次运行时提供校准流程。

### Step 1

用户完全打开笔记本。

记录：

```text
Open Reference
```

### Step 2

用户将屏幕调整到中间位置。

记录：

```text
Mid Reference
```

### Step 3

用户接近关闭屏幕。

记录：

```text
Close Reference
```

系统建立设备相关映射。

---

# 22. Sensor Provider

Sensor Provider 用于探测：

* Windows Sensor API
* HID
* ACPI
* 可用的厂商硬件接口

注意：

> 不得在没有明确硬件证据的情况下声称"设备支持真实铰链角度"。

必须提供：

```text
Supported
Unsupported
Unknown
```

三种状态。

---

# 23. Power Event Integration

程序必须监听 Windows 电源/显示相关事件。

可能涉及：

* Display Off
* Sleep
* Resume
* Lock
* Session Change

原则：

> DuoFlow 不接管 Windows 电源管理。

当系统已经决定关闭屏幕或进入睡眠时：

```text
Windows Power Event
      ↓
Duo Final Animation
      ↓
Release Overlay
      ↓
Windows continues normal behavior
```

不得为了动画强行阻止系统睡眠。

## 23.1 Power & Sleep Architecture（修订）

> 修订说明：实机测试发现，"盖子快合上时 Windows 已经熄屏"，熄屏后程序被限制，导致动画无法继续。
> 这不是开源软件写得不好，而是 Windows 电源管理的正常行为。微软明确区分了 Modern Standby 的
> **Screen Off** 和 **Sleep** 两个阶段；进入 Sleep 后，桌面应用会被 Desktop Activity Moderator（DAM）暂停或限制。

必须把两个概念彻底分开：

* **A. 屏幕关闭（Display OFF）** — 仅仅关闭显示器时，系统和应用仍然可以正常运行。
* **B. 系统进入睡眠（System Sleep / Modern Standby）** — 普通桌面程序不能指望继续以正常帧率运行。

电源策略重新定义为：

```text
1. Display Off 不等于 Sleep。
2. 软件必须区分 Active / DisplayOff / PreparingSleep / Sleeping / Resuming。
3. 不得通过阻止系统 Sleep 来保证动画完整。
4. Display Off 后进入 Pre-Sleep Watch。
5. 如果存在足够时间，执行 Pre-Sleep Finalization。
6. 如果输入源丢失，允许短时间 Prediction Mode。
7. 如果没有足够时间完成动画，必须 Graceful Degradation。
8. Sleep 状态下不要求桌面应用持续运行。
9. Resume 后必须重新初始化 Camera / Capture / GPU Resource。
```

**Pre-Sleep Finalization（睡眠前收尾）** 流程：

```text
             Camera / Sensor
                    │
                    ▼
               LidState
                    │
                    ▼
             Animation Engine
                    │
             ┌──────┴──────┐
             │             │
       正常开合动画      Power Event
             │             │
             │             ▼
             │       Pre-Sleep Mode
             │             │
             │             ▼
             │       快速完成动画
             │             │
             └─────────────┤
                           ▼
                    Release Resources
                           │
                           ▼
                     Windows Sleep
```

要点：

* **不要幻想"进入 Sleep 之后还让 HLSL 每帧跑起来"**。正确方向是：在 Windows 真正进入 Sleep 之前，把动画尽可能完成。
* **不要阻止 Windows 睡眠**。用户合上盖子，Windows 应该睡就让它睡。否则会出现：CPU/摄像头/GPU 继续工作、电池消耗增加、发热、用户以为已睡眠实际还在运行。
* **接近合盖时提前进入低成本模式**：Camera/Vision 降频 → Pre-Sleep Finalization 时 Camera 停止、Vision 停止、Animation 使用最后状态预测、GPU 完成最后几帧、Overlay 释放。
* **Prediction Mode**：最后阶段不再依赖摄像头实时输入，而是依靠 `Angle + Velocity + Last Known State` 短时间预测完成动画（例如当前 Angle = 10°、Velocity = -80°/s，预测 0.05s → 6°、0.10s → 2°、0.15s → 0°）。这也解决了"摄像头来不及反应"的问题。
* **Graceful Degradation**：如果 Windows 太快进入 Sleep，程序没有足够时间做最后动画，则不要强行保证动画完成——缩短动画、快速 Fade、立即释放。动画完整度不是最高优先级。
* **优先级必须是**：

```text
Windows 电源安全
        ↓
系统稳定
        ↓
资源释放
        ↓
动画完整度
```

* **区分"屏幕熄灭"和"合盖"**：Display Off 可能来自按关闭显示器、系统自动关闭、外接显示器、投影、锁屏、合盖等，它只是一个 Power Signal。真正的 Lid Closed 应由 Sensor / Camera / Lid Switch / ACPI Event 共同判断。

系统最终形成三个独立的"输入/状态"：

```text
① Lid State     → 盖子到底开了多少
② Power State   → Windows 现在允许程序干什么
③ Render State  → 当前动画应该渲染到哪里
```

> 不要试图让程序战胜 Windows 的电源管理，而是让动画在 Windows 进入不可运行状态之前完成自己的生命周期。

---

# 24. Debug Mode

必须提供开发者调试模式。

支持：

```text
Manual Progress
Manual Angle
Animation Speed
Blur
Warp
Dimming
Gradient
Light Sweep
```

例如：

```text
Progress
0.00 ─────────────── 1.00
             ●
```

允许键盘控制：

```text
Arrow Up
Arrow Down
```

以及：

```text
Home → 0
End  → 1
```

---

# 25. Demo Mode

必须提供 Demo Mode。

功能：

```text
Play Opening
Play Closing
Pause
Loop
Speed
```

用途：

* 开发调试
* 录制视频
* 展示项目
* 不实际合盖时测试效果

---

# 26. Settings

设置页面至少包含：

## General

* Start with Windows
* Enable Duo
* Enable Demo Mode

## Detection

* Auto
* Sensor
* Camera
* Manual

## Visual

* Perspective
* Blur
* Dimming
* Gradient
* Light Sweep

## Performance

* High Quality
* Balanced
* Battery Saver

## Calibration

* Start Calibration

---

# 27. Performance

目标：

### Animation

```text
Target: 60 FPS
```

支持更高刷新率设备，但不强制。

### Camera

建议：

```text
10~20 FPS
```

无需 60 FPS。

### GPU

尽可能使用 GPU 处理。

### Battery

电池模式降低：

* Camera FPS
* Shader quality
* Blur quality
* Light Sweep

---

# 28. Failure Handling

任何检测失败都不能导致：

* Windows 崩溃
* Explorer 崩溃
* 黑屏
* 鼠标失效
* 键盘失效
* 无法正常关机

例如：

```text
Camera unavailable
      ↓
Fallback
      ↓
Manual / Disable
```

如果 Overlay 创建失败：

```text
Disable Duo
→ Log Error
→ Keep Windows Normal
```

---

# 29. Logging

日志用于：

* Provider 检测
* Camera 状态
* Sensor 状态
* Capture 状态
* Renderer 状态
* Shader 错误
* Power Event
* Performance

不得记录：

* 摄像头视频
* 用户屏幕内容
* 用户个人数据

除非用户主动开启明确的 Debug Capture 功能。

---

# 30. Privacy

摄像头只用于检测笔记本屏幕状态。

默认：

* 不上传
* 不保存
* 不录制
* 不联网

Camera Frame 尽可能：

```text
Camera
 ↓
Memory
 ↓
Analysis
 ↓
Discard
```

不写入磁盘。

---

# 31. 项目目录

```text
DuoFlow/
│
├── src/
│   ├── DuoFlow.App/
│   ├── DuoFlow.Core/
│   ├── DuoFlow.Capture/
│   ├── DuoFlow.Camera/
│   ├── DuoFlow.Sensors/
│   ├── DuoFlow.Render/
│   └── DuoFlow.Power/
│
├── shaders/
│   ├── DuoWarp.hlsl
│   ├── HingeMask.hlsl
│   ├── Blur.hlsl
│   ├── Dimming.hlsl
│   ├── Gradient.hlsl
│   └── LightSweep.hlsl
│
├── config/
│   └── default.json
│
├── tests/
│
├── docs/
│   ├── PROJECT_SPEC.md
│   ├── DESIGN_DECISIONS.md
│   ├── TODO.md
│   ├── HARDWARE_COMPATIBILITY.md
│   └── TECHNICAL_PROPOSAL.md
│
└── README.md
```

---

# 32. Milestone

## M0 — Research

确认：

* Windows Graphics Capture 可行性
* D3D11 Overlay 可行性
* 联想设备传感器可访问性
* Camera detection 可行性

## M1 — Rendering Prototype

实现：

* Desktop capture
* Overlay
* Manual Progress
* Perspective
* Hinge Mask
* Blur
* Dimming

目标：

> 不依赖真实开合，仅通过滑杆就能看到完整视觉效果。

## M2 — Visual Refinement

加入：

* Gradient
* Light Sweep
* Smoothing
* Demo Mode
* 参数调节

目标：

> 视觉效果达到可以录视频展示的水平。

## M3 — Camera Provider

实现：

* Camera access
* Display detection
* Angle estimation
* Calibration
* Smoothing

目标：

> 用户实际开合笔记本可以驱动动画。

## M4 — Sensor Provider

调查并实现：

* Sensor detection
* HID detection
* Lid state detection

目标：

> 在支持的硬件上优先使用真实硬件信息。

## M5 — Windows Integration

实现：

* Startup
* Power Events
* Sleep integration
* Lock integration
* Multi-monitor basic handling

## M6 — Productization

实现：

* WinUI Settings
* Installer
* Logging
* Error handling
* Battery mode
* Documentation

---

# 33. Acceptance Criteria

## Visual

* [ ] 打开状态无明显额外效果
* [ ] 合盖过程中动画连续
* [ ] 没有明显跳变
* [ ] 没有明显撕裂
* [ ] 没有明显闪烁
* [ ] 铰链区域具有空间感
* [ ] 模糊具有位置差异
* [ ] 暗化具有位置差异
* [ ] 光带不过度
* [ ] 打开/关闭动画基本对称

## System

* [ ] 不影响鼠标
* [ ] 不影响键盘
* [ ] 不影响正常窗口操作
* [ ] 不强制阻止 Windows Sleep
* [ ] Overlay 失败可以自动退出
* [ ] Camera 失败可以降级
* [ ] Sensor 不可用不会导致程序退出

## Performance

* [ ] 正常状态低 CPU
* [ ] 正常状态低 GPU
* [ ] 动画目标 ≥ 60 FPS
* [ ] Camera 不需要 60 FPS
* [ ] 电池模式可以降低资源消耗

## Privacy

* [ ] 默认不保存摄像头数据
* [ ] 默认不上传数据
* [ ] 不需要云端服务
* [ ] 日志不包含屏幕内容

---

# 34. Definition of Done

一个 Milestone 只有同时满足：

1. 功能实现
2. 编译成功
3. 基本测试通过
4. 性能没有明显回归
5. 文档同步更新
6. Git Commit 清晰
7. 没有明显破坏已有功能

才算完成。

AI Agent 不得仅以"代码已经生成"作为完成标准。
