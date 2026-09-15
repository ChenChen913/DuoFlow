# DuoFlow — Design Decisions

> 项目关键设计决策记录
> 用于约束后续 AI Agent、Code Review 和重构行为。

---

# 使用规则

本文件记录已经经过项目负责人确认的重要技术和产品决策。

AI Agent 在修改项目时：

1. 必须阅读本文件。
2. 不得因为"存在更新的技术"而自动推翻已有决策。
3. 不得将"个人偏好"包装成"技术升级"。
4. 如果认为某项决策已经不合理，应提出变更建议，而不是直接修改。
5. 只有经过人工确认后，才能修改本文件中的 Accepted 决策。

---

# 状态定义

```text
Proposed   = 提议中
Accepted   = 已确认
Deprecated = 已废弃
Rejected   = 已否决
```

---

# DD-001：项目定位

**Status:** Accepted

## 决定

DuoFlow 是一个 Windows 笔记本开合视觉特效项目。

核心目标是：

> 让桌面内容随着笔记本开合产生空间折叠视觉效果。

## 不允许

将项目简化为：

> "Windows 合盖时播放一个渐变动画"。

## 原因

项目的核心价值是"内容随设备状态变化"，而不是简单播放动画。

---

# DD-002：输入与渲染解耦

**Status:** Accepted

## 决定

渲染系统不得直接依赖 Camera。

所有输入统一转换为：

```text
LidState
```

最终渲染系统主要消费：

```text
progress: 0~1
```

## 原因

未来可能使用：

* Hardware Sensor
* HID
* Camera
* Manual Debug

如果渲染系统直接绑定 Camera，会导致架构难以扩展。

---

# DD-003：Progress 使用 0~1

**Status:** Accepted

## 决定

统一使用：

```text
0 = Fully Open
1 = Fully Closed
```

## 原因

视觉系统不应该依赖某一台设备的真实物理角度。

---

# DD-004：第一阶段必须支持 Manual Provider

**Status:** Accepted

## 决定

在真实 Camera/Sensor 完成之前，必须支持手动修改 Progress。

## 原因

视觉系统与硬件检测系统应该独立开发。

## 目的

允许：

```text
Slider
→ Progress
→ Shader
```

直接验证最终视觉效果。

---

# DD-005：优先 GPU 渲染

**Status:** Accepted

## 决定

桌面视觉处理优先使用 GPU。

目标：

```text
GPU Capture
→ GPU Texture
→ Shader
→ GPU Composite
```

## 不推荐

```text
Screen
→ CPU Bitmap
→ CPU Processing
→ GPU
```

## 原因

降低延迟，提高帧率，减少 CPU 压力。

---

# DD-006：第一版使用 Direct3D 11 + HLSL

**Status:** Accepted

## 决定

第一阶段采用：

* Direct3D 11
* HLSL

## 原因

Windows 兼容性、成熟度以及桌面 GPU 渲染能力较适合本项目。

## 约束

AI Agent 不得仅因为：

* WebGPU 更新
* Vulkan 更新
* Direct3D 12 更新
* Three.js 更容易写

就自动替换渲染架构。

如果要更换，必须提出技术迁移方案。

---

# DD-007：不使用 Electron 作为核心渲染架构

**Status:** Accepted

## 决定

最终桌面程序采用 Windows 原生技术路线。

推荐：

```text
C#
.NET
WinUI 3
Win32
D3D11
HLSL
```

## 原因

项目属于系统级桌面视觉效果，而不是普通 Web App。

---

# DD-008：Camera 不是唯一数据源

**Status:** Accepted

## 决定

Camera 是一种 Provider，而不是整个系统的基础架构。

## 原因

不同 Windows 笔记本硬件差异较大。

---

# DD-009：不得假设联想设备一定存在角度传感器

**Status:** Accepted

## 决定

必须实际检测硬件能力。

程序只能报告：

```text
Supported
Unsupported
Unknown
```

## 禁止

未经测试直接声称：

> "所有联想笔记本都可以读取铰链角度。"

---

# DD-010：Camera 默认不保存数据

**Status:** Accepted

## 决定

摄像头数据只在内存中处理。

默认：

```text
No Upload
No Recording
No Persistent Storage
```

## 原因

隐私与安全。

---

# DD-011：Shader 效果必须模块化

**Status:** Accepted

## 决定

逻辑上至少拆分为：

```text
Warp
Hinge Mask
Blur
Dimming
Gradient
Light Sweep
```

## 原因

方便：

* 单独调参
* 单独关闭
* 性能优化
* A/B Testing

---

# DD-012：Gradient 不是核心效果

**Status:** Accepted

## 决定

颜色渐变只能作为辅助视觉效果。

核心：

```text
Geometry
+
Blur
+
Mask
+
Dimming
```

Gradient 不得覆盖或主导整个视觉效果。

---

# DD-013：光效必须克制

**Status:** Accepted

## 决定

Light Sweep 不能成为视觉中心。

默认强度保持低水平。

原因：

> 项目追求"高级、自然、像设备本身产生的效果"，而不是炫技式特效。

---

# DD-014：不修改 Windows 系统组件

**Status:** Accepted

## 决定

DuoFlow 不修改：

* Explorer
* Shell
* Registry 中的系统关键配置
* Windows 核心文件
* BIOS
* Firmware

---

# DD-015：不接管 Windows 电源管理

**Status:** Accepted

## 决定

程序只能：

> 观察并配合 Windows 的电源行为。

不得为了播放动画：

* 强行阻止 Sleep
* 修改系统 Sleep Policy
* 修改 Lid Close Action

---

# DD-016：Demo Mode 必须存在

**Status:** Accepted

## 原因

真实合盖测试不方便。

Demo Mode 可以用于：

* 调试
* 参数调整
* 视频录制
* 展示

---

# DD-017：第一阶段不追求完整产品化

**Status:** Accepted

## 决定

第一阶段优先：

```text
Capture
+
Progress
+
Shader
```

暂不追求：

* 完整设置中心
* Installer
* 自动更新
* 多语言
* 云同步

---

# DD-018：AI Agent 不得主动扩大项目范围

**Status:** Accepted

## 决定

例如项目只要求：

> 实现开合 Shader。

AI Agent 不应该擅自加入：

* AI Assistant
* 云端账户
* 用户系统
* 在线统计
* AI 图像分析
* 自动上传诊断数据

---

# DD-019：技术升级不能自动推翻项目决策

**Status:** Accepted

## 决定

以下理由本身不足以推翻既有架构：

> "这个技术比较新。"
>
> "这个框架 GitHub Star 更多。"
>
> "这个方案代码更少。"
>
> "现在大家都用 XXX。"

必须证明：

```text
实际需求
+
性能
+
稳定性
+
维护成本
+
兼容性
```

发生了明显变化。

---

# DD-020：视觉效果优先于代码炫技

**Status:** Accepted

## 决定

最终评价标准：

> 用户实际开合笔记本时，效果是否自然。

而不是：

> 使用了多少高级技术。

如果简单算法能达到同样效果，优先简单方案。

---

# DD-021：允许 AI 提出替代方案

**Status:** Accepted

## 决定

AI Agent 可以提出：

> "我认为 D3D11 可以替换成 WebGPU，因为……"

但：

```text
Proposal
↓
Human Review
↓
Accepted / Rejected
```

不能：

```text
Proposal
↓
Automatic Refactor
```

---

# DD-022：重大架构修改必须记录

**Status:** Accepted

## 决定

以下属于重大修改：

* 渲染 API 更换
* 捕获方案更换
* Camera → AI Model
* Sensor → Camera
* WinUI → Electron
* D3D → WebGPU
* 修改电源行为
* 修改隐私模型

必须新增：

```text
DD-XXX
```

而不是直接覆盖旧决策。

---

# DD-023：Decision History 不删除

**Status:** Accepted

## 决定

即使决策废弃，也保留历史。

例如：

```text
DD-006
Status: Deprecated

Replacement:
DD-031
```

这样可以知道：

> 为什么当初这么设计，以及后来为什么改变。

---

# DD-024：测试结果优先于理论判断

**Status:** Accepted

## 决定

对于硬件相关问题：

```text
Documentation
< 实机测试
```

不能仅根据：

* 网上帖子
* AI 推测
* 某型号宣传页

判断某个传感器一定存在。

---

# DD-025：Compatibility First

**Status:** Accepted

## 决定

项目首先保证：

```text
Windows 11
+
普通 Windows Laptop
```

而不是只针对某一台联想电脑做硬编码。

---

# DD-026：默认关闭高成本功能

**Status:** Accepted

## 决定

如果用户使用电池：

可以降低：

* Camera FPS
* Blur Quality
* Light Sweep
* Rendering Quality

不得为了视觉效果无限消耗电池。

---

# DD-027：错误必须优雅降级

**Status:** Accepted

## 决定

任何组件失败：

```text
Camera failed
→ Disable Camera Provider

Sensor failed
→ Try Camera

Renderer failed
→ Disable Duo
```

不能：

```text
Duo failed
→ Windows failed
```

---

# DD-028：项目文档是工程的一部分

**Status:** Accepted

## 决定

以下文档必须随着项目同步维护：

```text
PROJECT_SPEC.md
DESIGN_DECISIONS.md
TODO.md
HARDWARE_COMPATIBILITY.md
README.md
```

代码改变而文档没有更新，视为开发不完整。

---

# DD-029：AI Code Review 必须尊重 Design Decisions

**Status:** Accepted

## 决定

Code Review 时：

如果发现代码与本文件冲突，应首先报告：

```text
Design Decision Conflict
```

而不是直接修改。

---

# DD-030：最终决策权属于项目负责人

**Status:** Accepted

## 决定

AI Agent 可以：

* 分析
* 建议
* 实现
* 测试
* 提出风险

但不能自行决定：

* 产品方向
* 架构重大变更
* 隐私政策
* 数据采集范围
* 电源行为
* 核心视觉方向

---

# DD-031：不得阻止 Windows 正常进入睡眠

**Status:** Accepted

## 背景

实机测试发现：盖子几乎合上时 Windows 已经熄屏，熄屏后部分软件进入静默状态或不工作，动画无法继续。

## 决定

不得为了播放完整动画而阻止 Windows 睡眠（不使用 Power Request 等机制对抗电源管理）。

## 原因

用户合上笔记本盖子，Windows 应该睡就让它睡。否则会得到：合盖后 CPU/摄像头/GPU 继续工作、电池消耗增加、发热、用户以为电脑已睡眠实际还在运行。这会直接破坏产品体验。

## 替代方案

使用 Pre-Sleep Finalization：在 Windows 真正进入 Sleep 之前，把动画尽可能完成。

## 相关决策

- DD-015（不接管 Windows 电源管理）
- DD-034（短时预测完成动画）

---

# DD-032：电源状态必须独立于 LidState 建模

**Status:** Accepted

## 背景

进入 Sleep 后，桌面应用会被 Desktop Activity Moderator（DAM）暂停或限制；仅仅 Display Off 时系统和应用仍然可以正常运行。两者必须区分。

## 决定

新增独立的 `PowerState`：

```csharp
public enum SystemPowerState
{
    Active,
    DisplayOff,
    PreparingSleep,
    Sleeping,
    Resuming
}
```

整个系统状态 = `LidState + PowerState`。

## 影响

Animation Engine 据此判断："现在不是普通动画阶段，而是收尾阶段。"

## 相关决策

- DD-031（不得阻止睡眠）
- DD-033（Display Off ≠ Lid Closed）

---

# DD-033：Display Off 不得直接等同于 Lid Closed

**Status:** Accepted

## 背景

Display Off 可能来自：用户按了关闭显示器、系统自动关闭显示器、外接显示器、投影、锁屏、合盖等。

## 决定

`Display Off` 只是一个 **Power Signal**。真正的 `Lid Closed` 应由 Sensor / Camera / Lid Switch / ACPI Event 共同判断。

## 原因

把 Display Off 直接当作合盖，会在用户只是熄屏时误触发合盖动画。

## 相关决策

- DD-032（PowerState 独立建模）

---

# DD-034：睡眠前允许使用短时预测完成动画，但不得依赖 Sleep 状态持续渲染

**Status:** Accepted

## 背景

临界点时序：

```text
t = 0.00  Angle = 20°
t = 0.05  Angle = 15°
t = 0.10  Angle = 10°
t = 0.15  Windows Display Off
t = 0.20  Camera 停止
```

如果仍然要求 `Camera → Angle → Render`，最后 10% 动画很可能丢失。

## 决定

最后阶段不再依赖摄像头实时输入，使用：

```text
Angle
+
Velocity
+
Last Known State
```

进行短时预测（Prediction Mode），完成剩余动画后释放 Overlay。

## 约束

* 不得依赖 Sleep 状态持续渲染（"Sleep 后继续 60 FPS 渲染"本身就是逆着 Windows 的电源模型走）。
* 如果没有足够时间完成动画，必须 Graceful Degradation（缩短动画、快速 Fade、立即释放）。

## 优先级

```text
Windows 电源安全
        ↓
系统稳定
        ↓
资源释放
        ↓
动画完整度
```

## 相关决策

- DD-031（不得阻止睡眠）

---

# 摄像头适配铁律

> 来源：硬件适配讨论结论，应作为摄像头模块不可推翻的原则。
> 详细展开见 `HARDWARE_COMPATIBILITY.md`。

> **1. 不假定摄像头位置。**
> **2. 不假定摄像头分辨率。**
> **3. 不假定摄像头方向、比例和视场角。**
> **4. 所有摄像头数据先进入标准化 Camera Profile，再进入视觉算法。**
> **5. 必须支持首次运行校准和校准失败后的降级方案。**
> **6. 摄像头只是 LidState 的一种 Provider，不能成为系统唯一依赖。**

补充结论：**分辨率差异本身不是最大的难点**；真正难的是摄像头视角、安装位置、镜头 FOV、屏幕在画面中的可见程度以及不同机型的几何关系。设计重点应从"兼容各种分辨率"升级成"**兼容各种摄像头几何条件**"。

---

# Decision Change Template

新增设计决策时使用：

```markdown
# DD-XXX：标题

**Status:** Proposed / Accepted / Deprecated / Rejected

## 背景

为什么需要这个决策？

## 决定

最终决定是什么？

## 原因

为什么这样决定？

## 替代方案

考虑过哪些方案？

## 影响

会影响哪些模块？

## 迁移方案

如果替换旧决策，需要如何迁移？

## 相关决策

- DD-XXX
- DD-XXX
```
