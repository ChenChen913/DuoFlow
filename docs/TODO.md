# DuoFlow — TODO

> 项目开发任务清单
> 所有任务按照 Milestone 顺序执行。

---

# 使用规则

AI Agent 开始工作前必须：

1. 阅读 `PROJECT_SPEC.md`
2. 阅读 `DESIGN_DECISIONS.md`
3. 检查本文件
4. 确认当前 Milestone
5. 只实现当前 Milestone 所需功能
6. 完成测试后才能勾选任务
7. 不得为了完成任务擅自扩大项目范围

---

# 状态

```text
[ ] 未完成
[-] 进行中
[x] 已完成
[!] 阻塞
```

---

# M0 — Research & Feasibility

## M0.1 开发环境

* [ ] 确认 Windows 11 开发环境
* [ ] 安装 .NET SDK
* [ ] 安装 Visual Studio / Build Tools
* [ ] 确认 Windows App SDK
* [ ] 确认 Direct3D 11 开发环境
* [ ] 创建最小 WinUI 3 项目
* [ ] 编译并运行 Hello World

### 验收

```text
项目可以成功 Build
程序可以启动
```

---

## M0.2 Windows Graphics Capture

* [ ] 创建最小屏幕捕获 Demo
* [ ] 获取主显示器
* [ ] 捕获桌面纹理
* [ ] 验证 GPU Texture
* [ ] 测试不同分辨率
* [ ] 测试高 DPI
* [ ] 测试高刷新率

### 验收

能够实时显示：

```text
Desktop
→ Captured Texture
```

---

## M0.3 Overlay

* [ ] 创建透明 Overlay Window
* [ ] 设置全屏
* [ ] 测试 Topmost
* [ ] 测试 Click Through
* [ ] 测试 No Activate
* [ ] 测试窗口切换
* [ ] 测试多显示器

### 验收

Overlay 不影响正常鼠标和键盘操作。

---

## M0.4 Hardware Research

* [ ] 检查 Windows Sensor API
* [ ] 检查 HID
* [ ] 检查 ACPI
* [ ] 检查联想相关硬件接口
* [ ] 记录实际测试结果
* [ ] 创建 `HARDWARE_COMPATIBILITY.md`

### 注意

不得根据网络资料直接判定当前设备支持某项传感器。

---

# M1 — Rendering MVP

## M1.1 LidState

* [ ] 创建 `LidState`
* [ ] 创建 `LidStateSource`
* [ ] 创建 `ILidStateProvider`
* [ ] 创建 `ManualProvider`

---

## M1.2 Manual Progress

* [ ] 创建 Progress Slider
* [ ] 支持 0~1
* [ ] 支持键盘控制
* [ ] Home → 0
* [ ] End → 1
* [ ] 显示当前 Progress

---

## M1.3 Animation Engine

* [ ] 创建 Animation Engine
* [ ] 实现 smoothing
* [ ] 实现 velocity
* [ ] 防止 Progress 跳变
* [ ] 添加单元测试

---

## M1.4 Perspective Warp

* [ ] 创建 `DuoWarp.hlsl`
* [ ] 实现基础 Perspective Warp
* [ ] 绑定 Progress
* [ ] 调整 Warp 曲线
* [ ] 测试 0 → 1
* [ ] 测试 1 → 0

### 验收

Progress 改变时，桌面产生连续空间变形。

---

## M1.5 Hinge Mask

* [ ] 创建 Hinge Mask
* [ ] 支持 Hinge Position
* [ ] 支持 Hinge Width
* [ ] 支持 Falloff
* [ ] 可视化 Debug Mask

---

## M1.6 Blur

* [ ] 创建 Blur Pass
* [ ] 实现局部 Blur
* [ ] 根据 Hinge Mask 控制
* [ ] 根据 Progress 控制
* [ ] 调整最大 Blur
* [ ] 测试性能

---

## M1.7 Dimming

* [ ] 创建 Dimming Pass
* [ ] 根据 Hinge Mask 控制
* [ ] 根据 Progress 控制
* [ ] 调整最大暗化程度

---

## M1.8 MVP Composite

* [ ] Warp
* [ ] Hinge Mask
* [ ] Blur
* [ ] Dimming
* [ ] Composite

### M1 验收

必须实现：

```text
Manual Progress
       ↓
Warp
       ↓
Blur
       ↓
Dimming
```

并能够实时运行。

---

# M2 — Visual Refinement

## M2.1 Gradient

* [ ] 创建 Gradient Pass
* [ ] 支持颜色配置
* [ ] 支持强度配置
* [ ] 根据 Progress 调整
* [ ] 防止颜色过度

---

## M2.2 Light Sweep

* [ ] 创建 Light Sweep
* [ ] 控制位置
* [ ] 控制宽度
* [ ] 控制强度
* [ ] 添加开合方向

---

## M2.3 Visual Tuning

测试：

* [ ] 慢速关闭
* [ ] 快速关闭
* [ ] 慢速打开
* [ ] 快速打开
* [ ] 中途停止
* [ ] 中途反向

检查：

* [ ] 是否跳变
* [ ] 是否闪烁
* [ ] 是否过度模糊
* [ ] 是否过度暗化
* [ ] 是否过度炫彩

---

## M2.4 Demo Mode

* [ ] Play Opening
* [ ] Play Closing
* [ ] Pause
* [ ] Loop
* [ ] Speed Control

---

## M2.5 Parameter Panel

* [ ] Warp
* [ ] Blur
* [ ] Dimming
* [ ] Gradient
* [ ] Light Sweep
* [ ] Smoothing

---

# M3 — Camera Provider

## M3.1 Camera Access

* [ ] 枚举摄像头
* [ ] 打开默认摄像头
* [ ] 获取视频帧
* [ ] 控制 FPS
* [ ] 释放摄像头

---

## M3.2 Screen Detection

* [ ] Resize Frame
* [ ] Edge Detection
* [ ] Rectangle Detection
* [ ] Display Region Detection
* [ ] Confidence Calculation

---

## M3.3 Angle Estimation

* [ ] 建立几何模型
* [ ] 根据屏幕区域变化估计角度
* [ ] 输出 Angle
* [ ] 输出 Progress
* [ ] 输出 Confidence

---

## M3.4 Smoothing

* [ ] Low-pass Filter
* [ ] 防止抖动
* [ ] 处理检测丢失
* [ ] 处理突然变化

---

## M3.5 Calibration

* [ ] Open Calibration
* [ ] Mid Calibration
* [ ] Close Calibration
* [ ] 保存 Calibration
* [ ] 重新校准

---

## M3.6 Camera Failure

* [ ] 摄像头不存在
* [ ] 摄像头被占用
* [ ] 权限拒绝
* [ ] 检测不到屏幕
* [ ] Confidence 太低

全部必须优雅降级。

---

# M4 — Sensor Provider

## M4.1 Sensor Discovery

* [ ] 检测 Windows Sensor
* [ ] 检测 HID
* [ ] 检测 ACPI
* [ ] 记录设备信息
* [ ] 建立兼容性表

---

## M4.2 Sensor Provider

* [ ] 实现 `SensorProvider`
* [ ] 输出 LidState
* [ ] 输出 Confidence
* [ ] 测试噪声
* [ ] 测试异常值

---

## M4.3 Provider Manager

实现：

```text
Auto
 ↓
Sensor
 ↓
Camera
 ↓
Manual
```

* [ ] 自动选择 Provider
* [ ] Provider Failure Fallback
* [ ] Provider 状态显示

---

# M5 — Windows Integration

## M5.1 Power Events

* [ ] Display Off
* [ ] Sleep
* [ ] Resume
* [ ] Lock
* [ ] Unlock
* [ ] Session Change

---

## M5.2 Closing Animation

* [ ] 检测关闭事件
* [ ] 执行最终动画
* [ ] Release Overlay
* [ ] 不阻止 Sleep

---

## M5.3 Opening Animation

* [ ] Resume
* [ ] 初始化 Renderer
* [ ] 播放 Opening Animation
* [ ] 恢复正常桌面

---

## M5.4 Startup

* [ ] Windows Startup
* [ ] Tray Icon
* [ ] Enable / Disable
* [ ] Exit

---

# M6 — Product UI

## M6.1 Main Settings

* [ ] General
* [ ] Detection
* [ ] Visual
* [ ] Performance
* [ ] Calibration
* [ ] About

---

## M6.2 Performance Modes

### High Quality

* [ ] Camera high quality
* [ ] Full shader
* [ ] Full effects

### Balanced

* [ ] Normal Camera FPS
* [ ] Normal Blur

### Battery Saver

* [ ] Low Camera FPS
* [ ] Reduced Blur
* [ ] Disable Light Sweep if necessary

---

# M7 — Testing

## Functional

* [ ] Start
* [ ] Stop
* [ ] Enable
* [ ] Disable
* [ ] Camera
* [ ] Sensor
* [ ] Manual
* [ ] Calibration
* [ ] Demo Mode

---

## Animation

* [ ] Open → Close
* [ ] Close → Open
* [ ] Slow movement
* [ ] Fast movement
* [ ] Reverse
* [ ] Stop midway

---

## Windows

* [ ] Window switching
* [ ] Fullscreen application
* [ ] Browser
* [ ] Video playback
* [ ] Game
* [ ] Multiple monitors
* [ ] High DPI

---

## Power

* [ ] AC
* [ ] Battery
* [ ] Low battery
* [ ] Sleep
* [ ] Resume
* [ ] Lock
* [ ] Unlock

---

# M8 — Performance

## CPU

* [ ] Idle CPU measurement
* [ ] Camera CPU measurement
* [ ] Rendering CPU measurement

## GPU

* [ ] Idle GPU
* [ ] Rendering GPU
* [ ] Blur GPU
* [ ] High refresh rate test

## Memory

* [ ] Startup
* [ ] Idle
* [ ] Rendering
* [ ] After repeated open/close
* [ ] Detect memory leak

---

# M9 — Privacy & Security

* [ ] Camera data not persisted
* [ ] No network upload
* [ ] No unnecessary permissions
* [ ] Logs reviewed
* [ ] No screen content in logs
* [ ] No camera frame in logs
* [ ] Verify third-party dependencies
* [ ] Review installer permissions

---

# M10 — Documentation

* [ ] PROJECT_SPEC.md updated
* [ ] DESIGN_DECISIONS.md updated
* [ ] TODO.md updated
* [ ] HARDWARE_COMPATIBILITY.md updated
* [ ] README.md
* [ ] Installation instructions
* [ ] Troubleshooting
* [ ] Development instructions

---

# M11 — Packaging

* [ ] Release build
* [ ] Installer
* [ ] Uninstaller
* [ ] Startup integration
* [ ] Version information
* [ ] Crash handling
* [ ] Release notes

---

# M12 — Final Acceptance

## Experience

* [ ] 效果自然
* [ ] 动画连续
* [ ] 没有明显延迟
* [ ] 没有明显闪烁
* [ ] 没有明显跳变
* [ ] 不影响正常使用

## Technical

* [ ] Build successful
* [ ] Tests successful
* [ ] No critical errors
* [ ] Performance acceptable
* [ ] Camera fallback works
* [ ] Sensor fallback works
* [ ] Windows power behavior normal

## Documentation

* [ ] All documents synchronized
* [ ] Design decisions recorded
* [ ] Known limitations documented
* [ ] Hardware compatibility documented

---

# Current Milestone

```text
Current: M0

Next:
M0.1 → M0.2 → M0.3 → M0.4
```

AI Agent 每次开始任务前必须确认当前 Milestone。

不得跳过前置阶段直接实现后续复杂功能，除非项目负责人明确要求。

---

# 附：AI Coding 项目宪法

本项目文档体系的设计思想：

```text
PROJECT_SPEC
    ↓
项目"要做什么"

DESIGN_DECISIONS
    ↓
项目"已经决定怎么做"

TODO
    ↓
项目"现在做到哪里"

HARDWARE_COMPATIBILITY
    ↓
项目"真实环境到底是什么"

README
    ↓
项目"最终怎么使用"
```

以后无论是 Claude Code、Codex 还是其他 Coding Agent，**每次开始任务先读这 4 份文件**，而不是让 AI 直接扫描代码后自行理解项目。

尤其 `DESIGN_DECISIONS.md` 的价值很大：它实际上是**"项目早期与用户共同确定、后期 AI 不得擅自推翻的事实层"**。

这比单纯写一个 `AGENTS.md` 更适合这个项目。