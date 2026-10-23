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

# DD-035：ManualProvider 驱动语义与 LidState 手动路径取值

**Status:** Accepted（M1.1 实现时提出，项目负责人于本轮任务指令中确认登记）

## 背景

M1.1 落地 `LidState` / `ILidStateProvider` / `ManualProvider`（`src/DuoFlow.Core`）时，
必须明确三件规格未定死的事，否则 M1.2（Slider）、M1.3（Animation Engine）、M4（Provider
Manager）会各自发明语义：

1. UI 如何驱动 ManualProvider；
2. 手动路径下 `Velocity` / `Confidence` / `Angle` 的取值；
3. 事件的触发时机与线程约束。

## 决定

1. **驱动方式**：`ManualProvider.SetProgress(double progress)` 是唯一 UI 入口（M1.2 的
   Slider 接它）。入参 clamp 到 0~1（NaN 抛 `ArgumentOutOfRangeException`），设置后**在
   调用线程同步**触发 `StateChanged`；重复设置同值也再次触发（手动输入是权威输入，下游
   必须保持同步）。`StartAsync` 额外发一次当前状态作为基线；`Start/Stop` 只管生命周期
   标记，不拦截 `SetProgress`。
2. **取值语义**：手动路径固定 `Source = Manual`、`Confidence = 1.0`（HARDWARE_COMPATIBILITY
   §4.3：Manual = 1.00，用户主动控制）、`Velocity = 0`（速度计算是 M1.3 Animation Engine
   的职责，见 PROJECT_SPEC §7 管线，Provider 不做）。
3. **Angle 占位映射**：手动路径用 PROJECT_SPEC §6 的示意线性映射作为默认
   （0.0 → 180°，1.0 → 30°，即 `Angle = 180 − 150 × Progress`）。这只是示意默认值，
   **不构成任何硬件声明**；设备校准 Profile（M3.5 / M4）落地后由校准替换。
4. **字段纪律**：`LidState` 的 Velocity / Confidence 字段即使当前只填 0 / 1 也必须保留
   并向后传递（M1.3 / M4 依赖）；渲染层只消费 Progress（DD-002），LidState 不得直接流进
   渲染层。
5. **线程约束**：M1.1 不做加锁（UI 线程使用）；M4 Provider Manager 接入时统一负责序列化
   Provider 访问。

## 原因

* M1 阶段的目标是"Slider → Progress → Shader 直接验证视觉效果"（DD-004），同步直发是
  最短路径，行为可预测、可测试；
* Velocity 属于平滑/防跳变管线（§7），提前在 Provider 里算会和 M1.3 职责重叠；
* Angle 若硬编码为某机型真实角度，违反 DD-003 与 §6 "不得假设所有笔记本机械结构相同"，
  用规格自带的示意映射既保字段完整又不引入硬件断言。

## 替代方案

* Provider 内做平滑/速度估计 → 与 M1.3 Animation Engine 职责冲突，放弃；
* Angle 填 NaN 表示"无角度" → 下游要处处防 NaN，可读性差，放弃；
* 事件改异步队列 → M1 阶段无并发需求，徒增复杂度，留待 M4 按需加固；
* `SetProgress` 在 Stop 后拒发事件 → 会让 M1.2 在未调用 Start 时"静默失效"，违背
  "设置后立即触发"的直观语义，放弃。

## 影响

* `src/DuoFlow.Core`（本决策的直接实现）；
* M1.2 Slider（只管调 `SetProgress`，不需要自己 clamp）；
* M1.3 Animation Engine（收到 Velocity=0 的原始输入是预期行为）；
* M4 Provider Manager（接管线程安全与生命周期）。

## 迁移方案

不适用（新增决策，无旧实现需要迁移）。

## 相关决策

- DD-002（输入与渲染解耦）
- DD-003（Progress 统一 0~1）
- DD-004（第一阶段必须支持 Manual Provider）

---

# DD-036：M1.2 Manual Progress UI 接线（驱动链、键盘方案、防回环与单一事实源）

**Status:** Accepted（M1.2 实现时提出，随本轮任务指令确认登记）

## 背景

DD-004/DD-035 冻结了 ManualProvider 的语义，但 M1.2 把它接进 UI 时仍有五件规格未定的事，
各实现路径行为差异明显，需要一次定死：

1. Slider 参数（范围 / 步进 / 初值）；
2. 键盘 Home/End 在 WinUI 3 的实现方案（`Window` 类**没有** `KeyDown` 事件，与 WPF 不同）；
3. 显示值与 Slider、Provider 三者的同步规则（单一事实源）；
4. `StateChanged` 回写 Slider 的防回环约束；
5. 显示文本格式。

## 决定

1. **驱动链（不变式）**：`Slider.ValueChanged` 与键盘加速键两条输入统一汇聚到
   `ManualProvider.SetProgress`（唯一 UI 入口，DD-035），事件再驱动显示。
   **Slider 的值不得直接喂渲染层**（DD-002 铁律自 M1.2 UI 起继续守住）；到 M1.4 Warp 的
   链路只能经由 Provider / LidState。
2. **Slider 参数**：`Minimum=0 / Maximum=1 / StepFrequency=0.01 / 初始 Value=0`。
   0.01 步进为肉眼细调与后续校准调试留分辨率；初值 0 与 DD-035 基线（0 = 全开）一致。
3. **键盘方案**：`Home → 0`、`End → 1` 用**根元素 KeyboardAccelerator**（挂在窗口根
   `ScrollViewer` 上，`ScopeOwner` 不设 = 全局 scope）——WinUI 3 的 `Window` 没有
   `KeyDown` 事件；`KeyboardAccelerator` 全局生效，不依赖焦点，避免了"让 Slider 抢焦点"
   的脆弱方案。键盘路径统一走 `DriveProgress`：先同步 Slider 可视值（回声被守卫吞掉），
   再显式调 `SetProgress`——保证同值重复按键时 DD-035 的"同值重发"语义不丢失。
4. **单一事实源**：显示文本只从 `ManualProvider.CurrentState` 拉取，UI 不另存状态副本；
   事件参数虽等价，拉取式让"显示 = Provider 状态"在处理顺序变化时依然成立。
5. **防回环守卫**：`StateChanged` → 回写 `Slider.Value` 必须同时满足两条——
   ① 新值与 Slider 当前值差异 > 0.0005（浮点 epsilon，避开 StepFrequency 舍入噪声）；
   ② 回写包在 `_syncingSlider` 守卫旗标内，吞掉回写引发的 `ValueChanged` 回声。
   违反任一条都会 `SetProgress → StateChanged → Slider → ValueChanged → SetProgress` 打转。
6. **显示格式**：`Progress 0.00 · Angle ≈ 180.0°（§6 示意映射）`——Progress 两位小数
   （0.01 步进无损显示），Angle 一位小数（便于肉眼核对 §6 示意端点 180°/30°）。
7. **依赖方向**：`DuoFlow.App` → `DuoFlow.Core` 单向引用；Core 永不反向依赖 App / WinUI
   / D3D / Windows App SDK（配合 DD-002，Core 侧保持纯 C# 可测）。

## 原因

* 键盘选择 KeyboardAccelerator：官方推荐路径，行为由框架保证（含菜单/无焦点场景），
  自研焦点管理是 M1 阶段最不需要的复杂度；
* 拉取式显示：M1.3 Animation Engine 接入后 Provider 状态会自主演进（平滑追赶），
  拉取式显示天然适配"UI 跟随状态"而非"UI 记账"；
* 同值重发保留：手动输入是权威输入（DD-035），键盘重复触发不应被 UI 层吞掉。

## 替代方案

* 根元素 `KeyDown` + `IsTabStop` + `Focus` 抢焦点 → 依赖焦点状态、与 Slider/按钮焦点
  逻辑互相干扰，放弃；
* `Microsoft.UI.Input.InputKeyboardSource` 低层键盘监听 → 需要自管焦点拓扑，过重，放弃；
* Slider ↔ ViewModel 双向绑定（MVVM）→ M1 阶段无 VM 基建（M6 参数面板再议），放弃；
* `StateChanged` 里无条件回写 Slider.Value → 触发 `ValueChanged` 回声，轻则多余事件、
  重则与 StepFrequency 舍入互相追逐，放弃；
* 显示值直接用事件参数（不拉取）→ 当前等价，但失去"显示永远等于 Provider 现值"的
  结构保证，放弃。

## 影响

* `src/DuoFlow.App/MainWindow.xaml(.cs)`（本决策的直接实现）；
* `src/DuoFlow.App/DuoFlow.App.csproj`（新增对 Core 的单向引用）；
* M1.3 Animation Engine（拉取式显示适配平滑追赶；键盘跳变会经同一条链路）；
* M4 Provider Manager（若未来多 Provider 切换，显示层无需改动——只认 `CurrentState`）。

## 迁移方案

不适用（新增决策，无旧实现需要迁移）。

## 相关决策

- DD-002（输入与渲染解耦）
- DD-003（Progress 统一 0~1）
- DD-004（第一阶段必须支持 Manual Provider）
- DD-035（ManualProvider 驱动语义与手动路径取值）

---

# DD-037：Overlay 透明与穿透的 WinUI 3 实现方案（真机 P0 修复）

**Status:** Accepted（M0.3 真机首跑发现两个 P0 后修复时提出，随任务指令确认登记；
2026-09-16 真机复验 P0-1/P0-2 双通过，文末附探针假阴性勘误节）

## 背景

M0.3 的 overlay 验收曾全绿，但 2026-09-16 真机首跑两项全不成立：

1. **P0-1 不透明**：`DwmExtendFrameIntoClientArea(-1)` 只处理 Win32 一层背景；
   `DesktopWindowXamlSource` 的 Composition Visual 背景仍是黑，覆盖层置顶后整屏被遮
   （覆盖区采样亮度 0.0~3.5 vs 移开后 254.7）。当时的验收只查了"DWM API 返回 S_OK"——假阳性。
2. **P0-2 吞输入**：只设 `WS_EX_TRANSPARENT` 时，WinUI 3 内容岛的
   `DesktopChildSiteBridge` 仍然吞掉全部鼠标输入（`WindowFromPoint` 命中它）。当时的
   验收只查了"style bit 被设上"——假阳性。

## 决定

1. **透明 = 双层处理**（缺一即黑屏）——实现方式经 CI 逐轮证伪后定为 cnbluefire 生产配方：
   * XAML 岛层：**绕过 SystemBackdrop 子类机制**，直接把窗口对象 cast 到 OS 接口赋
     alpha-0 画刷：`window.As<Windows.UI.Composition.ICompositionSupportsSystemBackdrop>()
     .SystemBackdrop = compositor.CreateColorBrush(ARGB(0,255,255,255))`。曾改用自定义
     SystemBackdrop 子类（castorix 的 TransparentBackdrop.cs 同款），brush 连接成功但屏幕
     仍黑（run 35077212761）——不生效；
   * 画刷的 Compositor 必须是 **新建的 Windows.UI.Composition.Compositor**（OS 类），且
     构造前必须用官方 CoreMessaging helper（`CreateDispatcherQueueController`，
     **DQTAT_COM_STA=2**）确保 Windows.System.DispatcherQueue——XAML 线程自带的
     Microsoft.UI.Dispatching 队列不被 OS 工厂认可（Access is denied，35074034434/
     35074411095），DQTAT_COM_NONE=0 建的 queue 同样被拒（35075183532），桌面投影无
     托管 CreateOnCurrentThread（CS0117，35074837054）；Microsoft.UI.Composition.Compositor
     与 OS Compositor 是**不同 WinRT 运行时类**，直接 cast 与 CsWinRT As<T> 重包装均失败
     （35075895577/35076653477）；
   * **backdrop brush 连接会令 WinUI 重写 GWL_EXSTYLE**（35079479453 实证
     ClickThrough/NoActivate/ToolWindow/LAYERED 全丢）——赋值后必须重新断言全部 style
     bits（幂等 EnableLayeredClickThrough）。
   * ~~colorkey 方案~~（35077849717 实证后回退）：island 的 DComp 底色不经 GDI 表面，
     WM_ERASEBKGND 填魔色 + LWA_COLORKEY 抠不到它，且 colorkey 组合令穿透失效；
   * Win32 层：`DwmExtendFrameIntoClientArea(MARGINS(0))` + `DwmEnableBlurBehindWindow
     (DWM_BB_ENABLE|DWM_BB_BLURREGION, 空区域 CreateRectRgn(-2,-2,-1,-1))`；
     WM_PAINT 子类填黑在非 layered 窗口有效（cnbluefire 原样），但 layered 窗口上与
     穿透回退相关（35079027989）——已移除，保留 WM_DWMCOMPOSITIONCHANGED 重应用。
2. **穿透 = 顶层补 `WS_EX_LAYERED`**（最小改动，真机对照实验 B 组实证）：
   同时必须 `SetLayeredWindowAttributes(LWA_ALPHA, 255)` 初始化（从未设置属性的 layered
   窗口不会被合成）+ `SetWindowPos(SWP_FRAMECHANGED)` 使 exstyle 生效。子窗口不动。
3. **验收可证伪（堵 M0.3 假阳性的坑）**：
   * 穿透（CI 门禁，API 级）：覆盖层置顶时 `WindowFromPoint(控制台中心)` 的 root 不得是
     覆盖层窗口——云端已实证 pass（修复前命中 DesktopChildSiteBridge）；真实输入
     （SendInput 点击/拖动）在真机复验；
   * 透明（信息性探针，不进 CI 门禁）：进程内创建白色参考窗口 → 覆盖层提到 TOPMOST 最前 →
     BitBlt 采样亮度。~~早期归因：「GitHub runner 桌面为 RDP 类会话，DWM 不按物理控制台
     处理 per-pixel alpha，云上 lum 恒 0」~~——**该归因已被 2026-09-16 真机复验推翻**：
     真机是物理控制台会话（SM_REMOTESESSION=0）探针同样恒 0，而同次运行的独立采样
     证明屏幕实际透明（假阴性在探针自身，见下方勘误节）。探针已改异步 + 全程 GDI
     分级 trace + 双对照（后台线程 BitBlt / GetPixel），**透明实证权威 = 真机**
     （截图采样，覆盖层置顶 vs 移出对比）不变；
   * 穿透与预览（帧计数 + 截图）**同一次运行一起看**——layered + SwapChainPanel + 透明
     是 microsoft-ui-xaml#1247 的已知问题组合，修好一个可能弄坏另一个。

## 原因

* 两层背景是 WinUI 3 内容岛架构的固有结构，社区两条已验证路径——cnbluefire 的
  **直接接口赋值**（本项目采用，其生产实现即此方案）与 castorix 的 colorkey
  （其 repo 中 TransparentBackdrop 实际被注释，启用的是 LWA_COLORKEY）——先取后者后经
  CI 证伪回退到前者；
* LAYERED 是 Win32 命中测试排除 layered+transparent 窗口的标准开关，内容岛只是让它
  从"可选"变成"必需"；
* 命中链检查让穿透在云端直接断言行为；透明亮度为进程内自采样探针，**已知在真机上有
  假阴性历史**（旧同步版探针在改 z 序后阻塞 UI 线程采样，恒读 0；详见下方勘误节），
  故仅信息性记录，真机上用独立采样法目测——诚实标注能力边界，不在假环境里凑绿灯。

## 替代方案

* `window.SystemBackdrop = new MicaBackdrop()/DesktopAcrylicBackdrop()` → 材质背景有
  模糊/着色，不是全透明，放弃；
* 用 `TransparentBackdrop` 内置类 → WinAppSDK 1.8 winmd 中不存在，编译即错，放弃；
* 只修 LAYERED 不修 backdrop（或反之）→ #1247 组合必须整体验证，且单修任一项在真机上
  仍有黑屏/吞输入其一，放弃；
* 每秒重复 real-effect 探针 → z 序翻转闪烁，改为每进程一次 + 结果缓存，放弃。

## 影响

* `src/DuoFlow.App/OverlayBackdrop.cs`（TransparentBackdrop + Win32 消息子类，新增）；
* `src/DuoFlow.App/OverlayWindow.xaml.cs`（exstyle 加 LAYERED、双透明层、回读旗标）；
* `src/DuoFlow.App/OverlayNative.cs`（DWM/layered/采样/命中链 P/Invoke）；
* `src/DuoFlow.App/OverlayProbe.cs` + CI 门禁（Transparency/HitTest 探针断言）；
* M1.4 渲染（Warp 效果在透明层之上叠加时必须维持本配方）、M5/M6（窗口行为不回退）。

## 迁移方案

不适用（替换的是 M0.3 的错误实现；原 `ExtendFrame(-1)` 保留未调用作历史参考）。

## 相关决策

- DD-001（M0.3 overlay 架构）
- DD-002（输入与渲染解耦——穿透恢复后"overlay 不吞输入"才真正成立）

## 勘误（2026-09-16 真机复验轮，项目负责人确认后修正）

**1. 「云 RDP 会话不处理 per-pixel alpha」归因被推翻。** 真机复验在同一次运行里用三种
独立器材证明覆盖层透明（纯红参考窗 rgb(255,0,0) 原样透出、置顶/移出两张全屏截图逐位
相同、红标记点 rgb(251,69,69) 只能来自预览镜像），而 App 自带探针同次运行恒报 lum=0——
其中真机是**物理控制台会话（SM_REMOTESESSION=0）**，与 RDP 无关。另有两个对照实验
排除「参考窗未绘制」「采样代码路径有 bug」两条假设（原生不泵消息的白窗 450ms 后实测
rgb(255,255,255)；逐字复刻探针 GDI 序列在红窗位置读到 85 = 255/3，BitBlt 全部成功）。

**2. 假设的提出与裁决（两次云端运行定案，不靠猜）。** 旧同步流程的两个可疑点各立一个假设：

* **假设 A（UI 线程阻塞，已被证伪）**：旧版探针在 `ForceTopmost` 改 z 序后立即在 UI 线程
  `Thread.Sleep(450)`，重合成被自身阻塞 → DWM 把这一帧合成窗口背景色（黑）→ BitBlt 读 0。
  整改：等待改 `await Task.Delay`（UI 线程可泵消息）+ 采样前 `InvalidateRect+UpdateWindow`
  显式重绘一帧。**裁决（run 35086444842）**：后台线程 BitBlt（`Task.Run`，同矩形同刻）
  **同样读 0** → 与线程无关，假设 A 证伪。
* **新事实（run 35086444842，同进程同矩形同刻）**：UI 线程 BitBlt=0、后台线程 BitBlt=0、
  BitBlt 返回 TRUE 且 DIB 前 8 字节全 0（「成功」地读到黑）；**GetPixel 三点全部读到
  255,255,255（白参考窗透过覆盖层）**；采样矩形/主屏与虚拟桌面几何/DPI（manifest
  PerMonitorV2）全部正常——排除 API 失败与坐标换算。
* **假设 B（当前领先，未证实）**：**BitBlt-from-screen-DC 在覆盖层区域读到黑是进程内捕获
  伪影**——BitBlt 走的 GDI 表面把 per-pixel-alpha layered 窗口（疑似限自属窗口）区域呈现为
  黑，而 GetPixel 走的是最终合成帧。与真机旧观察吻合：独立进程（不拥有覆盖层窗口）的
  BitBlt 读到真实内容（红窗位 85、桌面 55.6），App 进程恒 0；且本机 CAPTUREBLT 无关。
* **GetPixel 暂不升为门禁**：参考窗本身是普通 GDI 窗口，若某捕获路径整体排除 layered
  窗口内容，则覆盖层即使不透明也会读到白（假阳性）。待真机用「控制台点对照」标定
  （GetPixel 打在被 WinUI 控制台窗口覆盖、无参考窗的位置，看能否读到控制台内容）
  后再决定是否升级。本轮探针已新增 `GetPixelPass`（窗内三点最小通道 ≥240，仅信息性）
  与窗外对照点 `GetPixelOutsideRgb`（下次真机运行即可拿到标定数据）。

**3. 探针定位为如实描述**：进程内自采样，信息性记录，不作为 CI 门禁；透明实证权威
保持「真机采样对比」不变。本文「决定 3」与「原因」两处旧表述已按此修正（保留
删除线痕迹）。穿透门禁（HitTest.Pass）不受影响；`presenter.IsAlwaysOnTop=true`
干扰穿透的排查线索已降级（真机实测置顶与穿透同时成立，见 HARDWARE_COMPATIBILITY §6.2）。

---

# DD-038：Animation Engine 平滑语义（限速指数逼近、可注入时间源、输出重建）

**Status:** Accepted（M1.3 实现时提出，随任务指令确认登记）

## 背景

PROJECT_SPEC §7 管线中 Provider 输出原始 LidState、Animation Engine 输出平滑状态、渲染只
消费 Progress（DD-002）。M1.3 实现 `DuoFlow.Core/AnimationEngine` 时有五件规格未定死的事，
M1.4（渲染接线）/ M3.4（摄像头平滑）/ M4（Provider Manager）会各自发明语义：

1. "防止 Progress 跳变"的具体算法与参数；
2. Velocity 字段的单位与符号约定（M1.1 恒为 0，从本决策起有真实值）；
3. 时间源（直接读系统时钟会让单测不稳定）；
4. 首次输入的对齐语义；
5. 输出 LidState 的 Angle / Source / Confidence 如何处理。

## 决定

1. **算法：限速指数逼近**。目标斜率 `v = (target − current) / τ`，硬钳在
   `±MaxProgressPerSecond`；每步 `current += v·dt`，越过目标即到位。默认
   `τ = 0.10 s`、`MaxProgressPerSecond = 2.0`（0→1 全程 ≥ 0.5 s）、`MaxDeltaTimeSeconds
   = 0.25`、`ArrivalEpsilon = 1e-6`（`AnimationEngineOptions` 可调，M2.3 视觉调优用）。
   任何输入（含键盘 Home/End 的 0↔1 跳变）都不可能让 Progress 瞬变——速率上限是硬保证。
2. **Velocity：有符号斜率，单位 = Progress 单位 / 秒**（1.0 = 每秒完成一次 0→1 全程）；
   正值 = 关闭方向（Progress 增大）。到位即 0（ε 内同样置 0，防无限渐近爬行）。
3. **时间源可注入**：`ITimeSource.GetTimestampTicks()`（TimeSpan tick，100 ns）；生产用
   `StopwatchTimeSource`，单测用 `ManualTimeSource`。引擎**禁止**直接读 DateTime.Now /
   Environment.TickCount。单步 dt 钳到 `[0, MaxDeltaTimeSeconds]`：负 dt（时钟回拨）不动，
   超大 dt（进程暂停/渲染卡死恢复）按 0.25 s 计，配合速率上限，任何间隙都不产生瞬移。
4. **首次输入 = 对齐（snap）**：新建引擎的第一次 `Update` 直接把 current 置为目标——
   引擎诞生时世界就是现状，不存在"跳变"；防跳变只作用于它**观察到过的变化**。
5. **输出 LidState 重建**：Progress / Velocity 用平滑值；**Angle 用 §6 示意映射从平滑后
   的 Progress 重建**（`180 − 150 × Progress`，与 ManualProvider 同一套常量）——显示保持
   连续，且延续 DD-035 的"示意占位、校准替换"语义；**Source / Confidence 原样透传**
   （引擎是数据通路，不发明可信度）。NaN 输入整帧忽略（保留上一次目标，仅透传元数据）。
6. **驱动双入口**：`Update(LidState)`（StateChanged 喂入）+ `Tick()`（渲染帧驱动，无新
   输入时推进模拟）——没有 Tick 的话，键盘跳变后平滑值会停在中途直到下一次事件；
   `Current` 为拉取式状态。引擎不含定时器/线程，调度由调用方（M1.4 渲染循环）承担。

## 原因

* 限速 + 指数的组合：远离目标时段速决定观感（稳定可预期），接近目标时指数收敛避免
  恒速"撞线"抖动；两者叠加让"中途反向"自然（符号随目标翻转）；
* dt 钳制与速率上限共同保证最坏情况（暂停 5 秒后恢复）单步位移 ≤ maxV × maxDt = 0.5，
  不会出现"恢复瞬间动画飞完"；
* Angle 重建而非透传：若透传原始 Angle，键盘跳变后显示角度会与平滑进度脱节，违背
  "观感的连续性"（§6.4 执行铁律）；重建让 Angle/Progress 恒自洽。

## 替代方案

* 纯指数 EMA（无速率上限）→ 大跳变初始速度 = err/τ 可达 10/s，等效瞬间跳变，违背
  "防止 Progress 跳变"的任务定义，放弃；
* 纯恒速斜坡（无指数项）→ 接近目标时硬着陆，且中途反向时速度不衰减，观感差，放弃；
* 弹簧/临界阻尼二阶系统 → 参数更多、超调难除，M1 阶段复杂度不划算，M2.3 再议，放弃；
* 引擎内置 DispatcherQueueTimer 自驱动 → 与渲染循环的节拍解耦困难（M1.4 才接渲染），
  且 Core 引入 UI 调度概念违反 DD-002 定位，放弃；
* Velocity 用最近两次输入差分 → 事件间隔不均匀时噪声大；用模拟斜率（当前 v）稳定且
  与位移一致，放弃差分。

## 影响

* `src/DuoFlow.Core/AnimationEngine.cs`、`AnimationEngineOptions.cs`、`ITimeSource.cs`（新增）；
* M1.4 渲染（以 `Tick()`/`Current` 为接口接入 Warp 的 progress uniform）；
* M3.4 摄像头平滑（可在同一引擎上叠加输入侧去抖，或替换参数）；
* M4 Provider Manager（引擎与 Provider 的接线与线程归属）。

## 迁移方案

不适用（新增组件；ManualProvider 的 DD-035 语义不受影响——引擎在 Provider 下游）。

## 相关决策

- DD-002（输入与渲染解耦——引擎输出仍是 0~1 Progress）
- DD-003（Progress 统一 0~1）
- DD-035（ManualProvider 驱动语义——Velocity=0 的原始输入是引擎的预期输入）

---

# DD-039：M1.4 Perspective Warp 的几何模型、参数默认值与进程内验收路线

**Status:** Accepted（M1.4 实现时提出，随任务指令确认登记）

## 背景

PROJECT_SPEC §10 只定了"必须是 Perspective/Projective Warp、不是 Scale、强度由 Progress 控制、
具体参数靠视觉测试"；TECHNICAL_PROPOSAL §7 给了合盖示意。M1.4 动手时有七件事规格未定死：
形变的数学模型、铰链位置与折叠方向、透视强度、p=1 端点行为、与透明覆盖层的叠加方式、
HLSL 的编译方式、以及——本轮真机暴露的——显示合成路径坏掉时的验收方法。

## 决定

1. **数学模型 = 绕水平铰链旋转的平面 + 针孔相机（真单应，非仿射）**。像素着色器逐像素求逆映射
   `(sx, sy) = (M00·x, M11·y) / (M00 + M21·y)`，其中 `M00 = r·cosφ`、`M11 = r`、
   `M21 = ±sinφ`、`φ = Progress × MaxFoldDegrees`（r = 相机距离/屏高）。铰链系坐标
   （x 横向 [-1,1]，y 自铰链 [0,1]）经 DestY/SrcY 两个线性重映射适配铰链在顶边或底边。
   纯 C# 模型在 **`DuoFlow.Render`（新建纯 net8.0 模块）**，shader 只消费常量——
   几何可单测，渲染不出可测数学。
2. **参数默认值**：`r = 2.5`（透视强度中等）、`MaxFoldDegrees = 90`、
   `TiltAwayFromViewer = true`（远边向内收缩、四边形始终在画面内，适合面板内验证）、
   铰链 = 底边（`HingeAtTop = false`）、边界羽化 `0.004`（归一化单位）。
   **方向与铰链边都是参数**——最终艺术方向（盖子朝你合上 = tilt-toward）留 M2.3 视觉调优定稿。
3. **p=1 端点 = 完全坍缩**：MaxFold=90° 时平面恰好侧对相机，投影高度为 0，叠加羽化后
   无任何不透明像素——"全关即消失"是数学事实也是可接受端点；要留残余画面把 MaxFold 调小
   （如 85°），参数已备好。单测覆盖两种方向的端点行为。
4. **叠加方式 = 预乘 alpha + 不设混合状态**：四边形外 shader 输出 (0,0,0,0)，后备缓冲每帧
   先清透再画一次全屏三角形——输出本身就是预乘色，DWM 按缓冲 alpha 与桌面合成，
   混合状态在此场景是数学上的空操作。**DD-037 透明配方零改动**（warp 只在预览面板内部渲染）。
5. **数据流 = 引擎 Tick → WarpGeometry → 常量缓冲**：渲染循环每帧 `clock.Tick()`，
   只消费 `AnimationEngine` 的平滑输出（DD-002/DD-038 铁律，不读 Provider）。
   `LidAnimationClock`（App 层门面）用一把锁串行化 UI 线程的 `Update` 与捕获线程的 `Tick`——
   引擎本体零改动（单线程语义不变），M4 Provider Manager 接管线程归属后可去掉锁。
6. **HLSL 运行时编译**：`shaders/DuoWarp.hlsl` 作为 Content 随 exe 分发，启动时
   `Compiler.CompileFromFile`（Vortice.D3DCompiler）编译——构建期不依赖 FXC/VS 组件，
   CI 无需新增 workload；**初始化失败自动回退 M0.2 blit**（TryCreate 永不抛，云 WARP runner 保活）。
7. **验收 = 进程内缓冲区回读（--warp-selftest）**：真机显示合成路径本轮发现既有环境问题
   （SwapChainPanel 内容不可见，改动前基线 baaf444 同样复现，时间线与 KB5129195 于
   2026-09-19 凌晨重启后生效吻合——见 HC §6.2），屏幕采样不可用。自测模式由 App 驱动引擎
   0→1→0 九档，渲染线程在 Present 前 staging 回读整帧（含 alpha），分析脚本量色带边界行号
   对比单应理论值。**实测：8 档（p=0/0.25/0.5/0.75 双向）quadTop 与全部边界误差 ≤3px
   （多数 ≤1px），无滞回；p=1 完全坍缩符合预期。**

## 原因

* 单应是"平面绕轴旋转"的唯一精确投影模型；逐像素逆映射让 GPU 做全部插值，
  CPU 每帧只算 3 个标量（M00/M11/M21）；
* 参数化（方向/铰链边/强度/端点角）让"视觉调优"不碰代码——M2.3 只改 WarpOptions；
* 预乘 alpha 不设混合：少一个状态对象、少一类 premultiplied/straight 配置错误；
* 运行时编译 + 回退：M1.4 的交付物是渲染能力，不能因 shader 初始化失败让捕获预览（M0.2 资产）陪葬；
* 进程内验收：显示路径坏时不阻塞渲染里程碑；同时它比屏幕采样**更精确**（拿到的是带 alpha 的
  原始输出，无 DWM/窗口几何/DPI 干扰）——这套基建（selftest 模式 + 分析脚本）后续每个渲染
  Pass（M1.5-M1.7）都复用。

## 替代方案

* 顶点网格形变 → 需细分网格逼近投影、边缘锯齿难除；像素级单应精确且更简单，放弃；
* 仿射/双轴 scale 近似 → 违反 §10"不是简单 Scale"；单测以"远边宽高比 = 1/cosφ"签名防回归；
* 构建期 FXC 编译 .cso → CI 依赖 VS 组件路径，且 shader 调优期需反复重编；运行时编译零成本；
* 把引擎加锁下沉进 Core → 改 DD-038 单线程语义、污染纯 C# 域模型；锁留接线层，M4 收编；
* 用 DispatcherQueue 把每帧渲染调度回 UI 线程 → 帧率被 UI 队列钳制、跨线程延迟；锁更轻。

## 影响

* 新增 `src/DuoFlow.Render`（M1.5-M1.7 的 Mask/Blur/Dimming 数学同住此模块）；
* `CaptureRenderer` 渲染路径分叉（warp / blit 回退），新增 `WarpState` 诊断属性（信息性，不进门禁）；
* `--warp-selftest` + `_test/m14-selftest.ps1` 成为渲染类里程碑的验收基建（仓库外脚本 + 仓库内模式）；
* CI 无新门禁：WARP runner 上 shader 编译失败也只回退 blit，M0.3 门禁不受影响。

## 迁移方案

不适用（新增）。约束一条：**M1.5 Hinge Mask 必须复用 WarpOptions 的铰链定义**
（HingeAtTop + 同一铰链系坐标系），否则 Mask 与 Warp 折叠线错位。

## 相关决策

- DD-002（渲染只消费 Progress）
- DD-037（透明配方零改动——warp 在面板内部、四边形外全透明）
- DD-038（Tick/Current 双入口——本决策的 LidAnimationClock 是其接线实现）

---

# DD-040：M1.5 Hinge Mask 的定义、复用铰链系约束与 Debug 可视化编码

**Status:** ⚠️ **Superseded by DD-046（2026-09-19）** — 原 Accepted（M1.5 实现时提出，随任务指令确认登记）。
铰链遮罩已从实现中删除，其职责由 DD-046 的缝隙高度物理耦合承担。本文保留作历史记录，
**不要按本文实现**。

## 背景

PROJECT_SPEC §11 要求铰链遮罩可配置（Hinge Position / Hinge Width / Falloff / Direction），
遮罩是 M1.6 Blur（`blur = mask × progress × maxBlur`）与 M1.7 Dimming
（`brightness = 1 − mask × progress × maxDarkness`）的公共输入。规格未定死的有四件：
遮罩在哪个坐标系里定义、四个旋钮各自的确切语义、异常输入的防线、以及
"可视化 Debug Mask"在显示合成路径故障的机器上如何验收。

## 决定

1. **坐标系 = 复用 warp 的铰链系（DD-039 迁移约束的兑现）**：遮罩在铰链系 y
   （0 = 铰链边，1 = 远边）上定义，而该 y 由 warp 的 DestYRemap 给出——
   **MaskOptions 刻意不含方向字段**：方向由 warp 决定，遮罩只随动，结构上杜绝错位。
2. **旋钮语义**（`DuoFlow.Render/MaskOptions` → `HingeMaskProfile`，纯 C# 可单测）：
   `m(y) = (1 − smoothstep(|y − center| / width))^exponent`；
   center=0 默认与 warp 折叠轴重合（Hinge Position），width=0.35（Hinge Width），
   exponent=2（Falloff）。**exponent 限制在 [1, 8]**：<1 时收敛端斜率无界
   （数学上不平滑，违反 PROJECT_SPEC"过渡必须平滑"），从校验层排除。
3. **遮罩与 Progress 解耦**：遮罩是静态几何函数，progress 缩放发生在下游 Pass
   （M1.6/M1.7 公式）——selftest 已实证 p=0 与 p=0.5 两帧的遮罩剖面逐行一致。
4. **Debug 可视化 = shader 热力编码**：常量缓冲加 `MaskParams`（center/width/
   exponent/debug），debug 分支输出纯热力图 `rgb = (m, 0.2, 1−m)`（红=铰链、蓝=远端），
   alpha 规则与图像路径完全一致（四边形外透明）。**R/A 与 B/A 通道可精确反解遮罩值**
   （预乘下除回 alpha 即可），这使 Debug 图同时是**量化验收的编码载体**而不只是好看。
5. **验收 = 同一条 selftest 管线**：`--warp-selftest` 增加 mask 阶段
   （p=0 全幅剖面 + p=0.5 折叠裁剪），分析脚本解码 5 个采样行 vs 闭式解
   ——实测全部误差 ≤0.004，B 通道交叉验证 ≤0.004，quadTop 裁剪边界 486/484.7。

## 原因

* 结构性复用铰链系而非"约定对齐"：M1.5 最大的风险是遮罩与折叠线错位，
  让编译器别无选择比写注释可靠；
* 遮罩 progress 无关 → 一次构建常驻常量，每帧只随 warp 矩阵更新 3 个标量；
* 热力图可反解的设计让"可视化"与"可测"合一——M2.3 视觉调优时人眼看的和
  脚本量的是同一个东西；
* exponent 下界从校验层挡住不平滑曲线，比文档约束可靠。

## 替代方案

* 独立 DuoHingeMask.hlsl 第二个 Pass（第二交换链/第二次绘制）→ M1 阶段允许合并 Pass
  （PROJECT_SPEC §9），单绘制单交换链更少状态与延迟，逻辑模块化由 Render 模块保证，放弃；
* 高斯遮罩 → 旋钮更多、与 spec 的 smoothstep 示意不符；smoothstep^exp 一个旋钮覆盖
  平滑家族，放弃；
* Debug 用桌面 50/50 混合叠加 → 反解不再精确（像素值受捕获内容污染）；纯热力图才可测。

## 影响

* `MaskParams` 常量缓冲 96→112 字节；DuoWarp.hlsl 每 pixel 多 ~10 条指令（编译器在
  非 debug 路径可裁剪部分）；
* `CaptureRenderer` 持有 `HingeMaskProfile`（默认参数），M2.5 参数面板从它接线；
* M1.6/M1.7 直接消费 `mask`（shader 内已算好），blur/dim 半径公式挂接点已就位。

## 迁移方案

不适用（新增）。约束两条：**M1.6 Blur / M1.7 Dimming 必须消费同一个 mask 值**
（不要各自重算）；若未来 warp 支持非常数折叠轴（如透视后铰链弯曲），遮罩定义须同步升级。

## 相关决策

- DD-039（warp 几何与铰链系——本决策是其迁移约束的兑现）
- DD-002/DD-038（遮罩同样只依赖渲染侧坐标，不读 Provider）

---

# DD-041：M1.6 Blur 的单 Pass 十三 tap 核、源空间半径与度量方法

**Status:** ⚠️ **Superseded by DD-046（2026-09-19）** — 原 Accepted（M1.6 实现时提出，随任务指令确认登记）。
13-tap 中心加权核已被 16-tap Vogel 螺旋 + mip 预过滤取代。本文保留作历史记录，
**不要按本文实现**。

## 背景

PROJECT_SPEC §12：`blur = hingeMask × progress × maxBlur`，"正常打开几乎无模糊、不允许整个
屏幕同时严重模糊、必须平滑"。规格未定死：模糊在哪个空间定义（dest/源）、单 Pass 还是双 Pass
可分离高斯、核形状与最大半径的量纲、以及 600×338 面板分辨率下如何**定量**验收模糊。

## 决定

1. **单 Pass 十三 tap 六边形核**：中心 0.25 + 内环（0.5×radius）6 tap 各 0.5/6 + 外环
   （1.0×radius）6 tap 各 0.25/6。PROJECT_SPEC §9 允许 M1 阶段合并 Pass；独立双 Pass
   可分离高斯的中间纹理/两次绘制的复杂度留给 M2 若有画质需求。radius→0 时核收敛于中心
   tap——progress=0 处处无模糊是硬保证。
2. **半径定义在源帧空间**（归一化：`MaxBlurPixels / captureHeight`），tap 偏移经
   SrcYRemap 线性映射进源 uv。透视压缩下源空间恒定半径在远端观感略强——物理上可接受
   （M2.3 若需 dest 空间精确圆核，用逆单应雅可比修正，参数不变）。
3. **mask 在 dest 像素取值**（"模糊发生在折叠处"），核的纵横比用帧高宽比近似校正
   （sx 单位=半宽 vs sy 单位=全高，16:9 下 11% 椭圆，可忽略）。
4. **常量缓冲**：`BlurParams = (maxBlurNormalized, progress, frameH/frameW, -)`，
   cbuffer 8×16=128 字节。CPU 侧 `DuoFlow.Render/BlurOptions` 校验 MaxBlurPixels ∈
   (0, 64]（>64 源像素时 13 tap 开始 banding）。
5. **验收 = 边缘过渡宽度法**：半屏黑白参考窗给出一条竖直锐利边缘，逐行测 30%-70% 上升
   宽度。p=0.75 铰链区（mask≈1，radius≈18 源像素=5.6 面板像素）宽度应 ≥2.5× 基线；
   p=0.5 远区（quad 内 mask=0 的可见带）应 ≤1.5× 基线（"不全屏糊"的几何证明：p=0.75 时
   整个可见四边形都落在遮罩区内，远区根本不可见）。实测：铰链 1px→3px（3×，三行一致）、
   远区 1×、平均 47.4 FPS（基线 48，性能无损）。
6. **度量下限认知**：p=0.5 时铰链核半径仅 ±1.9 面板像素，30%-70% 宽度落入 8-bit 量化
   精度，无法可靠测量——该断言降级为信息项；p=0.75 的 3× 已证明整条模糊链路。

## 原因

* 单 Pass 合并是 M1 阶段的正确复杂度：模糊半径由 mask 逐像素控制，双 Pass 可分离核要为
  "逐像素变半径"付出两次全屏绘制 + 中间纹理 + 采样对齐的代价，M1 收益不成立；
* 源空间半径让 tap 采样完全线性映射（无需逐 tap 重解单应），省 10 倍 ALU；
* 边缘过渡宽度比对比度法灵敏 5 倍（对比度法在 0.31× 下采样后信噪比不足）。

## 替代方案

* 双 Pass 可分离高斯（先横后竖）→ 画质更优但需中间 RT + 逐像素半径拆分成两个方向分量，
  M1 复杂度不划算，M2.3 再议；
* mipmap/降采样链模糊 → 一次生成多级，采样级由 mask 混合——需预生成 mipmap 每帧更新，
  且级别离散导致"模糊跳变"，与"必须平滑"冲突，放弃；
* dest 空间逐 tap 重解单应 → 透视精确正确但 13 tap × 10 ALU 的开销换来的差异在 0.31×
  预览分辨率下不可见，放弃（M2 全屏化后重评）。

## 影响

* cbuffer 112→128 字节；shader 非 debug 路径增加 12 次采样（radius=0 时被分支裁剪，
  progress=0 无额外成本）；
* `CaptureRenderer` 持有 `BlurOptions` 并在初始化时换算归一化半径；
* M1.7 Dimming 挂接点就位：同一个 `mask` 值直接可用（DD-040 约束兑现）；
* M2.3 调优参数：MaxBlurPixels（现默认 24）。

## 迁移方案

不适用（新增）。若 M2 换双 Pass 可分离核，`BlurParams` 语义保持（半径定义不变），
核形状变化对验收脚本透明（边缘宽度法与核形状无关）。

## 相关决策

- DD-039（warp 铰链系——半径的归一化基准）
- DD-040（mask 定义——模糊的逐像素权重来源）
- DD-002/DD-038（progress 来自引擎平滑输出）

---

# DD-042：M1.7 Dimming 的公式落地与递归污染度量教训

**Status:** ⚠️ **Superseded by DD-046（2026-09-19）** — 原 Accepted（M1.7 实现时提出，随任务指令确认登记）。
独立暗化 Pass 已删除，暗化改为由模糊半径派生的光衰减。本文的**递归污染度量教训**（§5）
仍然有效，保留作历史记录；实现部分不要按本文做。

## 背景

PROJECT_SPEC §13 公式已定：`brightness = 1 − hingeMask × progress × maxDarkness`。
未定死：maxDarkness 的量纲与默认值、与 Blur Pass 的关系（各自重算 mask 还是共用）、
纯白参考窗下如何定量验收。

## 决定

1. **公式逐字落地**：shader 在 blur 之后执行 `rgb *= 1 − mask × progress × maxDarkness`；
   mask 与 progress 直接复用 DD-040/BlurParams 的现值——**一个铰链、一份 mask、一个
   progress**，四个 Pass（warp/mask/blur/dim）共享同一套常量，不存在各 Pass 口径漂移。
2. **MaxDarkness 默认 0.8**（PROJECT_SPEC §13 示例值），校验 [0,1]：负值=提亮（无意义），
   >1=负光。M2.5 参数面板暴露。
3. **验收 = 纯白参考窗亮度剖面**：模糊对均匀白是 no-op，测得的亮度变化纯属暗化 Pass。
   实测（600×338 dump，扫描列限制在源 x<1290 避开面板递归区）：p=0 全 255 ✅；
   p=0.75 铰链行 104/110/123 vs 预测 104.3/110.7/124.4（**误差 ≤1.4**）✅；
   p=0.5 铰链行误差 ≤0.8 ✅；p=0.5 远区（mask=0）255 不动 ✅；FPS 47.4 ✅。

## 原因

* 四 Pass 共享常量是"单 Pass 合并"路线的直接收益：暗化不需要自己的采样，一行乘法；
* 纯白参考窗把暗化信号从模糊信号里干净分离（模糊对均匀色不可见）——两个 Pass 用两张
  参考窗分别验收，互不污染。

## 替代方案

* mask 重算（Blur/Dim 各算各的）→ 常量冗余且口径漂移风险，放弃；
* 纯黑参考窗（测提亮）→ 黑色下暗化不可见，白窗是唯一正确选择。

## 影响

* cbuffer 128→144 字节（DimParams.x = maxDarkness）；
* M1.8 合成的全部 ingredient 就位（warp/mask/blur/dim 同一常量流）；
* 度量教训沉淀：**递归污染**——面板自身内容会出现在捕获里，blur tap 会把它混进采样，
  分析脚本的扫描列必须避开面板在源画面中的投影区（本机：dest x ≤ 380）。

## 迁移方案

不适用（新增）。M1.8 合成时若拆分 Pass，DimParams 语义不变。

## 相关决策

- DD-040（mask——暗化的权重来源）
- DD-041（blur——progress/maxDarkness 共用常量流的先例）
- DD-039（铰链系坐标系）

---

# DD-043：效果强度包络（用户视觉反馈驱动的首个调优决策）

**Status:** Accepted（2026-09-19 真机体验反馈后登记）

## 背景

M1.6/M1.7 落地后用户真机体验：滑块拉到 0.8 时"整个屏幕几乎倒下了，根本看不到效果"，
要求**虚化从 0.35 左右开始可见**。根因：`radius = mask × progress × maxBlur` 对进度线性，
progress ≤ 0.5 时半径折算到面板像素不足 2px（不可见），等半径够大时（0.75+）折叠几何
又把画面压没了——**效果可见窗口与几何可见窗口错开**。这是 M2.3"视觉调优"的提前首发。

## 决定

1. **效果强度包络**：`IntensityAt(p) = clamp((p − StartProgress) / (FullProgress −
   StartProgress), 0, 1)`，默认 `StartProgress = 0.30`、`FullProgress = 0.60`——0.3 前
   无效果、0.3→0.6 线性增强、0.6 后保持最大。
2. **包络在 CPU 侧（CaptureRenderer 接线层）先作用于 progress 再进 shader**：
   `BlurParams.y = IntensityAt(p)`——blur 与 dim **共用同一条包络**（一个包络、一个故事），
   shader 零改动；几何（warp）不受包络影响（折叠的物理感优先）。
3. **参数归属**：包络旋钮（Start/Full）暂居 `BlurOptions`，M2.5 参数面板以
   "虚化起始/饱和进度"一对旋钮暴露；maxBlur 仍 24 源像素（增大受 13-tap 核 banding 限制，
   见 DD-041）。
4. **验收口径同步**：暗化预测公式更新为 `255 × (1 − mask × f(p) × 0.8)`——
   实测 p=0.75 铰链行 54/62/80 vs 预测 54/62.6/80.9（**误差 ≤0.9**）、p=0.5 铰链行 121/126
   vs 120.9/126.7（误差 ≤0.7）、远区 255 不动；模糊铰链区保持 3×、p=0.5 铰链过渡带从
   1px→3px（包络让中段模糊进入可测量区间）。FPS 47.4 不变。

## 原因

* 用户看到效果的位置在"中段折叠"——此时画面还立着、内容可辨，是虚化/暗化的最佳展示窗口；
  末段画面塌缩，任何强度都看不见；
* 包络放 CPU 侧而非 shader：四个 Pass 的 progress 语义统一改写，且随时可换曲线
  （smoothstep/缓动）而不动 shader；
* 线性坡而非 smoothstep：0.35 处需 ≥0.1 强度才"开始可见"，线性坡 f(0.35)=0.167 恰好，
  smoothstep(0.3,0.6) 只有 0.077。

## 替代方案

* 提高 maxBlur 到 60+ 让线性公式在早期也可感 → 13-tap 核在大半径下 banding 明显
  （DD-041 校验上限 64 的原因），放弃；
* smoothstep 包络 → 起坡太缓，0.35 处仍不可见，与反馈直接矛盾；
* 只给 blur 加包络、dim 保持线性 → 中段"糊了但不够暗"，两个效果脱节，放弃；
* 几何让步（MaxFold 降到 80° 让末段不塌）→ 改变了"合盖"的物理叙事，用户反馈针对的是
  虚化时机而非折叠幅度，不动几何。

## 影响

* `BlurOptions` 增加 Start/Full 进度对 + IntensityAt 纯函数（6 新单测，合计 91/91）；
* CaptureRenderer 传给 shader 的 progress uniform = 包络后值——**M1.8 合成与 M2 调优的
  progress 语义自此 = 包络后强度**，文档口径已同步；
* 两个验收脚本（m16/m17）的预测公式更新为含包络版本。

## 迁移方案

不适用（新增）。M2.3 若改曲线形状（缓动/贝塞尔），只动 IntensityAt 一个函数。

## 相关决策

- DD-041（blur——包络的宿主参数类）
- DD-042（dim——跟随同一包络）
- DD-038（引擎平滑输出的是物理进度；包络是渲染侧的表现层映射，两层不混）

---

# DD-044：M1.8 全屏合成——捕获/渲染解耦、反馈稳定化与静止态隐形

**Status:** Accepted（M1.8 实现时提出，随任务指令确认登记）

## 背景

M1 总验收要求"Warp + Mask + Blur + Dimming 组合实时运行"。四 Pass 的 shader 侧在
M1.6/M1.7 已就位，但渲染目标还是 800×450 角落预览面板。全屏化时暴露三个必须解决的问题：
① 捕获回调驱动渲染的旧循环在全屏下停摆（1 FPS）；② 全屏捕获画面包含我们自己的输出，
自反馈逐帧复合会把画面"融化"进铰链；③ 静止态（progress=0）全屏恒等镜像会让桌面出现
一帧延迟的鬼影。

## 决定

1. **捕获/渲染解耦（TECHNICAL_PROPOSAL §22 的落地）**：WGC 回调只把最新帧
   `CopyResource` 进一张常驻 GPU 纹理；渲染心跳（DispatchQueueTimer，30ms）在 UI 线程
   独立执行 引擎 Tick → 四 Pass 合成 → Present。捕获速率与渲染速率彻底解耦——
   静止屏幕（无 WGC 帧）不再拖死渲染。全部 context 使用经 `_renderGate` 串行化
   （心跳=UI 线程，帧回调=帧池工作线程）。
2. **~~反馈稳定化 = 全局不透明度 0.65~~（已被 DD-045 推翻）**：~~全屏捕获必然包含上一步的合成输出
   （WGC 显示器捕获无"排除自身"API），不透明度 = 1 时 `F^n(D)` 无界复合，数秒内
   画面融化进铰链（真机实证）。全局不透明度 α < 1 使反馈成为收敛几何级数
   `Σ α(1−α)^k · F^{k+1}(D)`——主导项是当前折叠，深层项衰减为"磨砂玻璃拖尾"，
   与 iPhone Duo 报道中的"半透明透视/玻璃融化"质感一致。α 是稳定机制也是美学。~~
   **修订（同日，用户真机反馈"那就是重影"）**：半透明稳定化让实时桌面以 35% 透过折叠图
   渗出 + 自反馈复合——用户看到的正是重影。根治 = DD-045 源冻结（效果可见期冻结捕获源），
   不透明度恢复 1.0。
3. **静止态隐形 = 全局淡入**：progress < 0.15 时合成透明度为 0（线性升至 1）——
   全开时覆盖层完全不可见（无鬼影），三件套的透明度门禁语义保持"静止=透明"。
4. **selftest 语义更新**：p=0 与 p=1 的 dump 均为"无可不透明行"（淡入隐藏/几何坍缩）；
   mask 剖面 dump 从 p=0 移至 p=0.25（淡入后首个可测点）；分析脚本 alpha 门限
   200→120（全局不透明度 0.65 下缓冲区 alpha 上限 = 166）。
5. **面板全屏化**：SwapChainPanel 铺满 Root 网格——面板像素尺寸 = 窗口客户区
   （1914×1074 物理），与交换链尺寸一致（HC §6.2 workaround 成立），对捕获纹理
   （1920×1080）仅 0.3% 缩放差。

## 原因

* §22 的解耦是项目提案期就定下的正确架构（"摄像头 15 FPS，Shader 60 FPS"），
  全屏化的 1 FPS 停摆只是让它从"可选优化"变成"必需品"；
* 反馈稳定化用一行乘法解决了"无法从捕获中排除自身"的平台限制，且半透明本身就是
  产品美学的一部分；
* 淡入让"静止=透明"的既有测试语义与门禁无需重写。

## 替代方案

* Desktop Duplication / 排除自身窗口的捕获 → 平台无此 API，放弃；
* 维护"干净桌面"离屏副本（从捕获中减去自身输出）→ 不透明区域下的桌面不可恢复，
  理论上就不成立，放弃；
* 淡入用 smoothstep → 线性已足够且可预测，M2.3 可换；
* 心跳放工作线程 → 与 WinUI 无交互需求，UI 线程 DispatcherQueueTimer 最简单且
  有 _renderGate 兜底，放弃专用线程。

## 影响

* cbuffer 144→160 字节（GlobalParams.x = 全局不透明度）；
* CaptureRenderer 结构性重写（常驻帧纹理 + 渲染门 + RenderTick）；
* OverlayWindow 持有渲染心跳定时器（Cleanup 时停止）；
* selftest dump 尺寸 1000×563 → 1914×1074，分析脚本 alpha 门限 120；
* **已知取舍**：全屏合成的捕获包含自身输出 → 反馈拖尾（几何级数衰减，视觉=磨砂拖尾）；
  M2 若需"纯净"渲染，需引入 Desktop Duplication + 自排除或双机差分，重估。

## 迁移方案

不适用（新增）。M2.1 Gradient / M2.2 Light Sweep 直接挂在同一心跳渲染的 Pass 链上。

## 相关决策

- DD-039..DD-042（四个 Pass——本决策将它们组合为全屏实时运行）
- DD-043（效果强度包络——心跳渲染消费包络后的强度）
- DD-022（TECHNICAL_PROPOSAL §22 捕获/渲染解耦的原始出处）
- DD-045（源冻结——推翻本决策第 2 条的稳定化方案）

---

# DD-045：折叠源冻结——自反馈重影的根治（推翻 DD-044 的半透明稳定化）

**Status:** Accepted（2026-09-19 用户真机反馈"那就是重影"后当日修订）

## 背景

DD-044 用全局不透明度 0.65 稳定全屏自反馈。用户真机体验的判定：**"那根本不是磨砂、
虚化、暗化，就是重影"**。诊断：不透明度 < 1 使实时桌面以 (1−α) 透过折叠图渗出
（α=0.65 → 桌面 35% 直渗），叠加自反馈的深层复合项——半透明稳定化本身就是重影的
来源，它把"融化"换成了"重影"，两者都是自反馈的症状而非解。

## 决定

1. **效果可见期冻结捕获源**：progress > 0.001 时 `_sourceFrozen = true`，帧回调停止
   更新常驻最新帧纹理——折叠只从**效果出现前最后一帧纯净桌面**渲染，自反馈从源头
   消失。progress 归零即解冻，恢复实时捕获（此时覆盖层已淡出、捕获纯净）。
2. **全局不透明度恢复 1.0**（DD-044 第 2 条推翻）：折叠图全不透明——无桌面渗透、
   无重影、无融化（反馈源已消失，不需要稳定化）。
3. **已知取舍**：折叠期间源内容冻结（长按滑块时折叠图里的桌面画面停在效果出现瞬间，
   钟表会停走）。物理隐喻自洽（"画面被玻璃带走"）；M2 若需部分实时，用覆盖掩码
   重建（透明区取实时捕获、折叠区取冻结帧），待用户反馈决定。

## 原因

* 重影的机制链：半透明 → 桌面直渗 + 捕获含自身输出 → 采样到自己的输出 → 复合。
  冻结源直接消灭第二环，第一环随不透明度恢复消失——比稳定化少一个机制；
* 静态参考窗（色带/白窗）下冻结与实时逐像素无差，既有全部数值验收不受影响；
* 与 iPhone Duo 的物理机制对齐：真机是玻璃物理折叠带走画面，软件版冻结源 =
  "画面被玻璃带走"的软件等价物。

## 替代方案

* 保持 0.65 半透明 + 接受重影 → 用户明确否决；
* 覆盖掩码重建（透明区实时/折叠区冻结混合）→ M1.8 不需要（静态窗下无差），
  M2 若用户反馈"折叠区画面太死"再实现；
* 提高捕获帧率让复合收敛 → 复合速率随帧率提高，方向相反，放弃。

## 影响

* `GlobalOpacity` 0.65 → 1.0（常量保留作 M2.3 半透明质感旋钮——现在无重影风险）；
* 帧回调新增冻结门（一行条件）；`_sourceFrozen` 状态由 RenderTick 按进度维护；
* 静态参考窗下全部既有数值验收不变（冻结=实时，逐像素一致）。

## 迁移方案

不适用（同日修订）。

## 相关决策

- DD-044（被本决策第 2 条推翻的半透明稳定化；解耦/淡入架构保留）
- DD-039..DD-042（四 Pass——源冻结不改变任何 Pass 的数学）

---

# DD-046：渲染模型 v2——物理景深模型取代遮罩三件套（DuoFold 参照）

**Status:** Accepted（2026-09-19 用户提供 DuoFold-Android 参考实现后当日采纳）

## 背景

M1.8 全屏合成后用户真机体验仍不满意（先前还有"就是重影"的判定，DD-045 治了重影，
但磨砂质感依然不对）。用户提供 DuoFold-Android（仿 iPhone Duo 的系统级安卓实现）作为
基准："他实现的挺好的，看看他是怎么实现光影转变的"。逐行研究其 fold.frag /
bokeh.frag / BokehProjection 后发现：双方几何同构，**分歧在模糊/暗化模型**。

## 决定（渲染模型 v2）

1. **物理景深单模型取代遮罩三件套**：桌面 = 平面，屏幕 = 绕铰链倾斜的磨砂玻璃；
   每像素的**缝隙高度**（玻璃到桌面的距离）驱动全部光影：
   `normGap = sy × sin(φ)`（sy = 逆映射后的源点铰链系高度——**源侧**而非目标侧，
   圆锥截面属于物点）→ `ease = normGap²`（二次缓入：铰链处保持锐利，远端加速
   沉入深磨砂）→ `coc = saturate(ease × 1.5 × intensity)` → `radius = coc × 160px`
   → `light = saturate(1 − 0.006 × radius)`（倾斜面接光变少 = 光影转变）。
2. **Mipmap 预过滤**：捕获副本带全 mip 链（GenerateMips），模糊 tap 以
   `lod = coc × 3` 采样——大半径到达时高频已预过滤，"天鹅绒磨砂、零马赛克零光晕"。
   捕获副本更新改为 CopySubresourceRegion（level 0）+ GenerateMips 重建。
3. **16-tap Vogel 螺旋、等权、无中心 tap、IGN 逐像素旋转**：消灭中心 tap 峰值，
   高对比边缘与文字"完全溶解"进磨砂（旧 13-tap 中心加权 = "模糊的图"而非"磨砂玻璃"
   的根源之一）。
4. **边界外取纯黑 + 1-texel 软边**（桌面的无限黑边框），取代边缘拉伸。
5. **DD-040 铰链遮罩、DD-041 旧核、DD-042 独立暗化随之废弃**：它们的职责由缝隙
   高度的物理耦合承担；MaxBlurPixels（默认 160，上限 400）成为唯一的"磨砂深度"旋钮，
   强度包络（DD-043）保留。

## 原因

* 参考实现的质感优势全部来自物理耦合：一个变量（缝隙）同时驱动模糊、变暗、
  光衰减——浑然一体，无旋钮拼凑感；
* 方向修正：铰链处锐利、**远端翘起边缘**渐糊渐暗（物理：远端翘起最多、离焦最多、
  接光最少）——旧模型的"铰链附近模糊"方向反了；
* mip 预过滤是"大半径深磨砂无颗粒"的唯一可行解（旧 13-tap 24px 封顶的原因）。

## 替代方案

* 保留遮罩三件套只调参数 → 三个独立旋钮拼不出物理因果，用户已否决其观感；
* 双 Pass 可分离高斯 → 无 mip 预过滤仍会颗粒化，且失去"逐像素半径"的自然性；
* 目标侧缝隙（旧方案隐含）→ 远端内容欠模糊约 4 倍（中段折叠实测），物理错误，放弃。

## 影响

* cbuffer 160→128 字节（PhysicsParams + FadeParams 取代 Mask/Blur/Dim/Global）；
* 捕获副本 +11 级 mip 链 + 每帧 GenerateMips（GPU 成本可忽略）；RenderTick 采样
  改 SampleLevel；
* HingeMask/DimOptions 及其测试删除（-28 用例）；BlurOptions 语义更新（默认 160、
  上限 400、去归一化换算）；selftest mask 调试阶段删除；
* 光影方向反转：暗化/模糊自铰链（锐利）向远端（深磨砂+黑暗）渐变——PROJECT_SPEC
  §11"模糊发生在铰链附近"的表述被本模型修正为"远离铰链（翘起侧）模糊"。

## 迁移方案

不适用（同日替换）。M2.1 Gradient 若叠加，作用域 = 本模型的输出色。

## 相关决策

- DD-039（折叠几何——v2 继续沿用其单应，参数化对齐参考实现的 eye=1920 点）
- DD-043（强度包络——v2 的 intensity 输入）
- DD-045（源冻结——v2 无重影的前提）
- DD-040..DD-042（被取代的遮罩/旧核/独立暗化）

---

# DD-047：M1 工程债的处置口径——拓扑自适应、帧预算降级与 CI 渲染门禁

**Status:** Accepted（2026-10-22，M1 审查后清理）

## 背景

M1 收官一个月后对已完成功能做代码级审查，发现三类问题：显示拓扑变化无处理（`RecreateFramePool`
有定义零调用点）、渲染无帧预算（30ms 心跳跑在 UI 线程）、CI 不校验渲染管线状态（shader 编译失败
静默回退 blit 仍全绿）。另有一批文档与 DD-045/046 脱节。

## 决定

1. **拓扑变化走"捕获回调检测 + 心跳内重建"**：`GraphicsCaptureItem` 在 WinAppSDK 1.8 的 C# 投影里
   **没有 SizeChanged 事件**（编译期 CS1061 实证），因此在 `OnFrameArrived` 比较 `frame.ContentSize`
   与帧池尺寸；变化时丢弃该帧 → `RecreateFramePool` → 抛 `SizeChanged`。消费方（`CaptureRenderer`）
   只置 `_rebuildPending`，真正重建放在 `RenderTick` 内、持 `_renderGate` 执行——**重建永不发生在
   一帧的中间，也不与捕获线程并发**。
2. **帧预算采用"降质不跳帧"**：EMA 帧耗时超 25ms 时按步长下调磨砂半径（下限 0.5×），低于 60%
   预算时回升。理由：跳帧会破坏"Progress 连续变化"的核心体验，而降半径只损失磨砂深度。
3. **`WarpState` 升级为 CI 门禁**：`TryCreate` 的"永不抛异常、失败回退 blit"是好的运行时策略，
   但必须有一个地方证明 shader 真的跑了——否则渲染回归完全隐形。回退仍是运行时行为，
   **门禁只负责让 CI 红**。
4. **验收脚本必须入库**：`scripts/acceptance/warp-selftest.ps1` 是 M1.4 量化验收的执行器，
   放在仓库外等于下一个人无法复现结论。脚本内的几何尺寸一律从应用 trace 读取，不硬编码。

## 原因

* 显示拓扑变化是真实用户场景（改分辨率、拔插外接屏、RDP 重连），失配会导致画面错乱而非报错；
* UI 线程是单点：渲染一卡，控制台就失去响应，用户会以为程序挂了；
* "CI 绿但功能不在"比 CI 红危险得多——它会让后续所有里程碑建立在错误的信心上。

## 替代方案

* 拓扑变化用轮询 `Item.Size`：可行但需要额外定时器，且仍要解决重建时机，收益为零；
* 渲染搬到独立线程：SwapChainPanel 的绑定与 `Present` 有 UI 线程亲和性，M1 阶段迁移风险
  远大于收益（M8 性能阶段再评估）；
* 帧预算用"跳过 Present"：会停掉 `PresentedFrames` 计数与合成心跳，控制台 FPS 显示失真。

## 影响

* `DesktopCapture`：新增 `SizeChanged` 事件与帧尺寸检测；CA1416 用 `IsWindowsVersionAtLeast` 守卫。
* `CaptureRenderer`：`Initialize` 拆出 `CreateTargets`，新增 `RebuildTargets`、帧预算字段；
  `Dispose` 进入 `_renderGate`。
* `WarpPipeline`：SRV 缓存（原为每帧创建/释放）。
* `.github/workflows/m0-windows-build.yml`：新增 `WarpState` 断言。
* smoke JSON 新增 `RenderFrameMs` / `QualityScale`（信息性，M8 才做性能门禁）。

## 迁移方案

不适用（向后兼容的增量修复）。

## 相关决策

- DD-039（warp 管线与进程内验收路线）
- DD-044（捕获/渲染解耦——帧预算挂在这条心跳上）
- DD-045 / DD-046（本轮**不改**的视觉决策：源冻结与物理景深）

---

# DD-048：渲染参数在运行时可替换——参数面板的边界

**Status:** Accepted（2026-10-22，M2.5）

## 背景

M2.3 视觉调优的主要工作量是"改一个默认值 → 构建 → 手动启动 → 拖滑块 → 看"。
`WarpOptions` / `BlurOptions` 是 `init`-only 的不可变配置，构造后无法改；控制台也没有
任何入口能把它们传进渲染层。不解决这件事，M2.3 每调一次参数就要一轮完整构建。

同时必须回答一个架构问题：**参数面板是不是在绕过 DD-002（输入层与渲染层解耦）？**

## 决定

1. **面板只写渲染配置，不碰输入链路**。`Progress` 的路径仍是
   `ManualProvider → StateChanged → AnimationEngine → WarpGeometry`（DD-002 / DD-035 / DD-038）；
   `WarpOptions` / `BlurOptions` 描述的是"同样的 progress 该怎么画"，属于渲染配置。
   `MainWindow.ApplyTuning()` 是唯一写入口，它不引用 `ManualProvider`。
2. **选项按不可变实例整体替换**，不做字段级可变。`CaptureRenderer.SetOptions(warp, blur, gradient)`
   一次换一组；`RenderTick` 在折叠分支开头**把三个字段读进局部变量**，因此帧内不可能出现
   "一半新一半旧"的参数组合。
3. **校验在 UI 侧、整体拒绝**。`WarpOptions` 的非法组合会让 `WarpGeometry.Compute` 抛异常，
   而它在 30ms 的 `DispatcherQueueTimer` 回调里——异常会表现为"覆盖层忽然不动了"，
   没有可见的因果链。所以 `SetOptions` 先以 `Compute(0)` / `Compute(1)` +
   `BlurOptions.Validate()` / `GradientOptions.Validate()` 验一遍，失败则**整组拒绝并返回原因**，
   控制台把原因显示出来，上一组继续生效。
4. **线程模型不变**：`SetOptions` 与 `RenderTick` 都在 UI 线程（同一 `DispatcherQueue`），
   不加锁。`_renderGate` 保护的是与捕获线程共享的 GPU 资源，与这些字段无关。
5. **面板的可测性**：`SetOptions` 把**派生几何**写进 trace（`frame@1.0 M00=...`），
   因为 `M00 = r·cos(MaxFold)` 是唯一能证明"旋钮真的走到了渲染数学"而不是只改了 UI 的数字。

## 原因

* M2.3 是 M2 的主工作量，调参工具不是可选项；
* "整体替换 + 帧内读局部"用两行代码换掉了整个并发问题，比加锁或 volatile 更可靠；
* 校验失败必须**可见**。静默回退会让调参者以为参数生效了，从而得出错误结论——
  这正是 M2.3 最不能容忍的失败模式。

## 替代方案

* **把 `WarpOptions` / `BlurOptions` 改成可变类**：改动最小，但会破坏"同一 progress 恒定输出"
  这个可单测的性质，也让帧内读到混合状态成为可能；
* **把选项改成 `record` 用 `with` 复制**：语法更顺，但把 `with` 引入渲染配置类型只为省几行
  面板代码，收益不值；
* **通过 `ManualProvider` 转发参数**：会把渲染配置混进输入状态机，污染 DD-002 的语义；
* **用外部配置文件 + 热重载**：适合最终用户，不适合"边看边调"，M2.3 需要的是即时反馈。

## 影响

* `CaptureRenderer`：`_warpOptions` / `_blurOptions` 去掉 `readonly`，新增 `SetOptions`。
* `MainWindow`：新增 `ApplyTuning()` / `OnResetTuning()`，`Refresh()` 负责在渲染器就绪后
  补一次推送（两个窗口的 Loaded 顺序不保证）。
* `MainWindow.xaml`：参数面板 6 个控件 + 恢复默认；`ProgressSlider` 补 `AutomationId`——
  面板加了 6 个 Slider 之后，"子树里第一个 Slider"不再是可靠定位方式，
  `overlay-p0-verify.ps1` 已改为按 `AutomationId` 查找。
* 控制台宽度**刻意保持 580**：`overlay-p0-verify.ps1` 把白色参考窗放在屏宽 35%（1920 上 x=672），
  而控制台是 TOPMOST——加宽会静默遮住参考窗、让透明测试失效。

## 迁移方案

不适用（纯增量）。

## 相关决策

- DD-002（输入/渲染解耦——本决策划清面板的边界）
- DD-035（ManualProvider 语义）
- DD-038（AnimationEngine：progress 的唯一来源）
- DD-046（被调参的物理景深模型）

---

# DD-049：渐变色相只叠在阴影上——用「遮光缺口」缩放，而不是平铺加色

**Status:** Accepted（2026-10-22，M2.1）

## 背景

M2.1 要的是"折叠像一块有色玻璃"，即在折过去的画面上叠一条色相渐变。
最直觉的实现是在像素着色器末尾直接加色：

```hlsl
rgb += tint * strength;      // 平铺加色
```

DD-046 的 v2 模型刚刚花掉 M1 全部时间建立"铰链亮、远端暗"的物理景深：
远端 `light` 掉到 0.14 左右（`far=34.7/255`）。在这么暗的表面上平铺加一条
0.15 强度的色相，亮度会几乎翻倍——**折过去的那半边会重新亮起来，DD-046 的阴影白建**。

## 决定

渐变的合成权重乘上**遮光缺口** `(1 - light)`：

```hlsl
if (GradientParams.x > 0.0)
{
    float3 tint = lerp(GradientStart.rgb, GradientEnd.rgb, saturate(sy));
    rgb += tint * (GradientParams.x * PhysicsParams.y * (1.0 - light) * (1.0 - rgb));
}
```

1. **`(1 - light)`**：亮的地方（铰链）几乎不加色，暗的地方（远端）加得最多。
   这正好是"有色玻璃"的物理直觉——你透过玻璃看到的是它挡掉的那部分光。
2. **`PhysicsParams.y`**（模糊强度）：渐变跟模糊包络同源。包络没开（`p ≤ 0.30`）
   时 `intensity = 0`，渐变**一起消失**，不会出现"画面还没折叠就已经染色"的怪状态。
3. **`(1 - rgb)`**：逐通道饱和保护，亮表面不会被推到过曝。
4. **`if (GradientParams.x > 0.0)`**：`x` 由 `GradientOptions.EffectiveStrength` 给出，
   关闭时返回**精确的 0.0**（不是 1e-9）。因此关闭态在着色器里是**跳过分支**，
   而不是"加一个零"——这是"关闭 = no-op"在编译层面的保证。
5. **强度上限 0.2 写死在 `GradientOptions.MaxStrengthLimit`**，`Validate()` 与
   `EffectiveStrength` 双处执行（面板不是唯一调用方）。超过 0.2 就不再是"染色"，
   而是"滤镜"。

## 原因

* 视觉上，渐变必须让折叠更"实"，而不是把折叠的明暗关系抹平；
* 把权重绑到 `(1 - light)` 之后，M2.3 调参时**不可能**通过调渐变把 DD-046 的阴影破坏掉，
  两个 pass 之间不是"叠加"而是"寄生"关系；
* 与 `PhysicsParams.y` 同源，省掉了"渐变自己的包络"这个额外旋钮，也避免两套包络打架。

## 替代方案

* **平铺加色**：最省事，直接把远端亮度抬到接近铰链，DD-046 失效（已量化：远端
  `34.7 → 47.2` 是可接受的染色；平铺加色会到 `~80`，阴影基本消失）；
* **在模糊之前染色**：色相会被 mip 链和 16-tap 采样一起模糊，边缘出现色边；
* **用屏幕混合 / 叠加混合模式**：需要额外读回或第二个 RT，成本远高于一行乘加；
* **HSL 空间插值**：`lerp` 在 RGB 空间已经足够（两端色相只差 70°，都在蓝紫家族），
  转 HSL 只为 70° 的插值不值得。

## 影响

* `DuoWarp.hlsl`：cbuffer 增加 3 个 `float4`（`GradientParams` / `GradientStart` /
  `GradientEnd`），总长 8 → 11 个 `float4`（176 字节），`WarpPipeline.Constants` 同步。
* `DuoFlow.Render/GradientOptions.cs`（新）：`Enabled` 默认 **false**，
  `HueToRgb` 固定 `S=0.65 / L=0.55`——只暴露色相，不暴露明度，否则调用方能把
  过曝重新引进来。
* `WarpPipeline.Render(...)` 增加可选参数 `gradient`（默认 `null` → 强度 0）。
* `CaptureRenderer.SetOptions(warp, blur, gradient)`：三个选项**一起**校验、一起替换。
* 面板新增 4 个控件（开关 + 起色相 + 止色相 + 强度）。

## 验证

"关闭 = no-op"这条不是靠读代码，而是靠真机三轮比对（方法见
`scripts/acceptance/README.md`「怎么证明新 pass 关闭时是 no-op」）：
同一二进制、只换磁盘上的 `DuoWarp.hlsl`，`A1 vs A2`（同 shader）给出逐像素噪声底，
`A1 vs B`（新旧 shader）必须落在噪声底之内。实测两者最大差同为 23、差异像素同量级，
且**所有量化指标逐位相同**（`hinge=227.68 far=148.37 depth=0.3483`）。

反向对照：临时打开渐变（强度 0.15）后，`p=0.75` 远端 RGB 从 `24.30/24.30/24.30`
变为 `41.29/33.80/50.38`（B>R>G，紫调），同档铰链侧 `227.23 → 227.23`（+0.02）。
"只吃阴影侧"这条设计约束因此也被实测到了。

## 迁移方案

不适用（纯增量，默认关闭）。

## 相关决策

- DD-046（被渐变压在其上的物理景深模型——本决策的全部约束来源）
- DD-048（参数运行时可替换——渐变三件套走同一条 `SetOptions` 通道）
- DD-002（渲染配置与输入链路的分界）

---

# DD-050：光带是「光」不是「色」——用剩余空间归一化，而不是乘法保护

**Status:** Accepted（2026-10-22，M2.2）

## 背景

M2.2 要在折叠上叠一条沿铰链法线的窄高斯亮带，位置随 Progress 从铰链扫向远端。
DD-049 已经给渐变定了一个保护方式：乘遮光缺口 `(1 - light)`，让染色只出现在暗处。

照搬到光带上会有一个不显眼但致命的后果。先算一下量级：

| p | 光带中心处 `light` | `(1 - light)` | 强度 0.08 的增量 |
|---|---|---|---|
| 0.50 | ≈0.80 | 0.20 | **≈2 / 255** |
| 0.75 | ≈0.31 | 0.69 | ≈14 / 255 |

光带中心 = progress，所以**折叠前半段的光带正好落在还很亮的表面上**。
`(1 - light)` 在那里只有 0.2，增量 2/255 —— 8 bit 下和抖动噪声同级，
用户看到的是"前半程什么都不发生，后半程突然出现一条线"。

## 决定

用**剩余空间**做归一化，而不是用乘法保护：

```hlsl
if (SweepParams.z > 0.0)
{
    float d = sy - SweepParams.x;
    float band = exp(-(d * d) * SweepParams.y);
    float maxChannel = max(rgb.r, max(rgb.g, rgb.b));
    float headroom = saturate((1.0 - maxChannel) / max(SweepParams.w, 1e-4));
    rgb += (SweepParams.z * PhysicsParams.y * band * headroom);
}
```

1. `SweepParams.w` 是**强度上限本身**（`LightSweepOptions.MaxStrengthLimit` = 0.2），
   由 C# 传进来，不在 HLSL 里重复写一个 0.2。
2. 因为 `gain ≤ MaxStrengthLimit`，`gain × headroom ≤ 1 − maxChannel`，
   所以**叠加后必然 ≤ 1.0**：不会裁切、不会出现白色平台，而且
   "峰值增量 ≤ 强度上限" 是这一行的**推论**，不是另外加的钳制。
3. 用**最大通道**而不是亮度做保护：保护量三通道相同，色相不被推偏——
   这是"光带是光不是色"的实现方式。
4. 表面 ≤ 0.8 时 `headroom = 1`，光带拿满强度；只有接近白的地方才衰减。
   实测 p=0.5 从 2/255 提升到 **13.4/255**，p=0.75 为 **20.6/255**。

## 原因

* 光带和渐变是两件事：渐变改**色相**，必须躲在阴影里；光带加**亮度**，本来就该
  在亮的地方也看得见（只是别过曝）。用同一个保护因子是把两件事混为一谈；
* "按剩余空间归一化" 让上限在数学上成立，而不是靠调参调出来；
* 把上限本身传给着色器，避免 HLSL 里出现一个和 C# 常量可能漂移的字面量。

## 替代方案

* **直接乘 `(1 - rgb)`**：就是上面那张表里的 2/255，前半程等于没有；
* **纯加法 + 依赖 RT 钳制**：p≈0.4 时底面已经 0.97，0.2 强度会溢出 2~4%，
  在本来就接近白的地方出现一小块硬白；
* **`1 - smoothstep(0.8, 1.0, maxChannel)`**：过渡更柔和，但 0.85 处
  `0.84 × 0.2 = 0.168 > 0.15`，仍会轻微裁切——不满足"证明不裁切"这个目标；
* **在模糊之前加**：亮带会被 16-tap 采样抹平，等于没加。

## 影响

* `DuoWarp.hlsl`：cbuffer 增加 1 个 `float4`（`SweepParams`），11 → 12 个（192 字节）；
  `WarpPipeline.Constants` 同步。
* `DuoFlow.Render/LightSweepOptions.cs`（新）：`Width` 用**半高全宽**（FWHM）而不是
  sigma——调参的人想的是"亮带多宽"，不是"标准差多少"；`CenterAt(progress)` 是纯函数，
  所以"随 Progress 从铰链扫到远端"是可单测的陈述，不靠肉眼看。
* `WarpPipeline.Render(...)` 增加 `progress` 参数（`CenterAt` 需要原始 progress；
  从 `M00/M11` 反解 φ 也能得到，但换了 `MaxFoldDegrees` 就会悄悄算错）和 `sweep` 参数。
* `CaptureRenderer.SetOptions(warp, blur, gradient, sweep)`：四个选项一起校验、一起替换。
* 面板新增 5 个控件（开关 + 位置 + 宽度 + 强度 + 方向）。
* `--light-sweep` 启动参数：把面板初始状态种成"开"，供 `light-sweep-verify.ps1` 测量。
  面板仍是权威（DD-048），这个参数等价于"用户自己勾上那个框"。

## 验证

关态 no-op 与开态可测性用**同一二进制、只换磁盘上的 `DuoWarp.hlsl`** 证明
（方法同 DD-049）：

| 比对 | 变量 | 行剖面最大差（0–255） |
|---|---|---|
| off1 vs off2 | 同一份 shader，两轮 | 0.0080 |
| off1 vs m21 | pre-M2.2 shader vs M2.2 shader，光带关闭 | 0.0087 |
| off1 vs on | 光带开启（默认 0.08） | **20.60** |

即"新旧 shader 关闭态的差异"与"同一份代码跑两遍"同级，而真实信号是噪声的 2500 倍。
光带位置也被量化验证：p=0.5 峰值落在第 746 行（预测 741.3），p=0.75 落在第 833 行
（预测 832.6）。

## 迁移方案

不适用（纯增量，默认关闭）。

## 相关决策

- DD-046（被光带叠在其上的物理景深模型）
- DD-049（渐变的 `(1 - light)` 保护——本决策解释为什么不能照搬）
- DD-048（参数运行时可替换——光带四件套走同一条 `SetOptions` 通道）

---

# DD-051：运行期自愈与参数持久化——把三条"静默死亡"变成有状态、可观测、能恢复

**Status:** Accepted（2026-10-22，M2.6 补做）

## 背景

M2.2 完成后对 M0～M2 已完成功能做了一次代码级审查（5405 行全读 + 真机运行），
发现三个**不会报错、只会让特效安静地消失**的缺口，外加两个直接妨碍 M2.3 调优的工程问题：

| # | 问题 | 后果 |
|---|---|---|
| 1 | `_rebuildPending` 在重建**之前**被清掉 | 一次瞬时失败 = 永久黑屏，不再重试 |
| 2 | `Present` 的 `Result` 被丢弃 | 设备被移除（混合显卡切换/驱动更新）后静默停画，控制台仍显示正常 |
| 3 | 渲染心跳没有任何 try/catch | 帧级故障 → 30 Hz 未处理异常 → 50 次吞异常预算在 **1.7 秒**内耗尽 → 进程死 |
| 4 | 没有任何持久化 | 每次重启参数全丢，M2.3 调优等于每次重调 |
| 5 | 空闲时仍 33 FPS 全屏 Pass | 直接违反 PROJECT_SPEC §2.1"CPU/GPU 占用应尽可能低" |

## 决定

### 1. 重建失败是一个**状态**，不是一次尝试

`_rebuildPending` 保持置位直到重建**成功**，失败按 tick 指数退避
（2/4/8/…/120 tick，上限 ~3.6 s）。失败次数进 `WarpState`（`rebuild failed (attempt N)`），
控制台可见。

### 2. 设备丢失由**所有者**恢复，渲染器只负责报告

`DuoFlow.Render/DeviceHealth`（纯 C#，可单测）把 HRESULT 分类为"设备已消失"
（`DEVICE_REMOVED` / `DEVICE_RESET` / `DRIVER_INTERNAL_ERROR`）。
`CaptureRenderer` 一旦判定即停止绘制、置 `ChainBroken` 并记录原因；
`OverlayWindow`（捕获设备的真正所有者）在心跳末尾看到标记后**重启整条捕获链一次**。
渲染器不去重建自己不拥有的设备——这是所有权边界，不是偷懒。

**为什么不无限重试**：设备重建要重新创建 WGC item 与整个 D3D11 设备，
在真故障下无限重启只会把资源耗尽；一次自动恢复 + 明确的可读状态，比"永远在重试"
更接近产品行为。

### 3. 帧级故障就地隔离

`RenderTick` 内部 try/catch，异常进 `WarpState = "tick failed (T)"`，
trace 只在第 1 次与每 120 次写一行。App 层"吞 50 次异常"的兜底保留，
但它不再是帧级容错的唯一防线。

### 4. 空闲跳过渲染，但冻结/解冻记账必须照常跑

`visible = progress > 0.001`（与 DD-045 解冻阈值同一个数）。
`!visible && 已 present 过一帧透明 && 没有 dump 请求` → 直接 return。
**关键细节**：DD-045 的 `_sourceFrozen = false` 必须在跳过之前执行，
否则一次跳过会让捕获源永远冻结，下一次折叠回放的是几分钟前的桌面。
另外"进入可见"那一刻必须重新渲染，因此 `_idlePresented` 在重建后清零。

### 5. 参数持久化 = 面板的**已接受**快照

`TuningPreset`（纯 C# record + `System.Text.Json`）是唯一线格式；
`OptionSet.ValidateOrThrow/ValidateAll` 让**面板推送**与**文件加载**走同一套校验。
写入走 600 ms 防抖（拖一次滑块几十次 ValueChanged），窗口关闭时强制 flush。
**验收脚本运行时不读也不写**（`automation = smoke || selfTest || lightSweep`）：
`warp-selftest` 的预测公式写死 r=2.5 / MaxFold=90，一份用户预设会让它静默跑偏。

### 6. 顺带修掉的具体缺陷

* blit 回退路径的 `CopyResource` → `CopySubresourceRegion`：源纹理带 11 级 mip 链，
  `CopyResource` 要求整资源形状一致，那条"安全网"本身是颗地雷；
* `IsCursorCaptureEnabled = false`：DD-045 冻结会把光标烤进折叠画面（活光标 + 印在磨砂里的光标）；
* 渐变默认强度 0 → 0.08：勾选"启用渐变色相"原本什么都不发生，与光带默认 0.08 不一致；
* trace 文件单次写入前检 4 MB 上限（原来无上限增长）；
* 控制台面板被拒后每秒重试并刷 4 行 trace → 只在错误文本变化时写一次。

## 原因

* **静默失败是这类全屏常驻程序最贵的缺陷**：用户不会看到异常，只会看到"特效偶尔不出现"，
  而开发者在 CI 里永远复现不到；
* 恢复逻辑必须放在**拥有该资源的那一层**（`OverlayWindow` 拥有捕获设备），否则会写出
  "渲染器偷偷 new 一个 device"这种更难维护的结构；
* 持久化必须**跟在已接受值之后**，否则一个被拒绝的面板状态会被写进文件，下次启动直接坏；
* 空闲跳过是把 PROJECT_SPEC 的明文要求落地，不是性能优化——它同时是电池与风扇问题。

## 替代方案

* **重建失败无限重试（每 tick）**：真故障下 30 Hz 创建/销毁 GPU 资源，噪音大且无收益；
* **渲染器自己重建 device**：越过所有权边界，且 WGC item 也要重建，必然写歪；
* **设备丢失后直接退出进程**：体验更差；丢失后控制台仍可用，重启一次就能救回来；
* **持久化写在 UI 事件里（不防抖）**：拖一次滑块几十次文件写入，UI 线程被 IO 打断；
* **持久化"面板当前值"**：被拒绝的非法组合会进文件，下次启动被判无效 → 回默认，
  看起来像"设置丢了"；
* **空闲时把定时器整个停掉**：再启动有首帧延迟，且心跳停了引擎不再 Tick，反而更脆；
  只跳过 GPU 工作是可逆且零延迟的。

## 影响

* `DuoFlow.Render/TuningPreset.cs`（新）：线格式 + `OptionSet` 校验入口；
* `DuoFlow.Render/DeviceHealth.cs`（新）：HRESULT 分类（纯 int，可在无 GPU 环境断言）；
* `DuoFlow.App/TuningStore.cs`（新）：`%LOCALAPPDATA%\DuoFlow\tuning.json` 读写，失败只 trace；
* `CaptureRenderer`：`ChainBroken`/`ChainBrokenReason`、重建退避、心跳隔离、空闲跳过、
  `Present` 结果分类、回退改成 `CopySubresourceRegion`；
* `OverlayWindow`：`BuildCaptureChain()` 抽出（启动与重启共用）、`OnRenderTick()`、
  `RestartCaptureChain()`（每进程一次）；
* `MainWindow`：启动恢复 + 防抖保存 + 关闭 flush + `automation` 保护；
  "恢复默认"改走同一条 seed 路径；
* `DesktopCapture`：不捕获光标；
* `GradientOptions.DefaultStrength`；`MainWindow.xaml` 强度初值 0.08；
* `Trace`：4 MB 上限。

## 验证

* **单测 134/134**（+38）：`TuningPresetTests` 21 条（JSON 往返/缺省字段/未知字段/大小写/
  损坏/越界/枚举名）、`DeviceHealthTests` 3 条（3 个致命码 + 5 个非致命码 + 常量核对）；
  渐变默认值断言同步为 0.08；
* **构建**：0 warning 0 error；
* **真机启动**：无 crash log；trace 出现 `preset persistence on` → `no saved preset` →
  `preset saved`；`tuning.json` 落盘内容与面板默认值逐项一致；
* **空闲跳过**：静止 14 秒，`frames` 停在 **1**（改动前约为 30/秒）；
* **可见态未受影响**：`--warp-selftest` 跑完整 0→1→0，9 帧全部落盘（1914×1074×4 =
  8,222,544 B），累计 **913** 帧渲染，无 `DEVICE LOST` / `tick failed` / crash；
* **几何/光影独立复算**（直接测 dump，不依赖脚本）：
  quadTop = 214/482/774 对理论 213.5/482.0/773.9（**误差 ≤0.5 px**）；
  上行下行逐档同值（**无滞回**）；p=0.25（包络未启动）远端亮度 216、
  p=0.50 降至 56、p=0.75 降至 3.5（**远端沉入阴影**），铰链侧保持内容亮度 34→36；
  p=0 全帧 alpha 最大 **0**（静止态完全透明，无鬼影）；
* **CI 门禁本地预演**：`--overlay-smoke` 的 JSON 通过 workflow 的全部断言
  （9 个布尔 + MonitorCount + HitTest + `WarpState=active`）。

## 迁移方案

* 无故障时行为不变：设备不丢失、不重建，渲染输出与 M2.2 逐像素相同（几何复算已证）；
* 首次启动会**新建** `tuning.json`（默认值），旧版本用户无迁移成本；
* 验收脚本不受影响：automation 运行既不读也不写预设，仍以编译默认值为基准。

## 相关决策

- DD-045（源冻结——空闲跳过必须保留它的解冻记账）
- DD-046（物理景深模型——回退路径的 mip 形状约束来自这里的 mip 链）
- DD-048（参数运行时可替换——持久化保存的是"已被 SetOptions 接受"的那一版）
- DD-049 / DD-050（渐变的默认强度、光带的默认值）

---

# DD-052：M2.3 的验收口径——把「跳变/闪烁/过糊/过暗/过彩」变成可测的量

## 背景

M2.3 的清单原本是「六种操作 × 五项检查」，五项全是主观词：跳变、闪烁、过糊、
过暗、过彩。主观项最终必须由人看（这是 M2.3 唯一需要项目所有者参与的地方），
但**「看不出问题」不等于「没问题」**——这个项目已经栽过两次假阳性（M0.3 的
Overlay 三件套、M2.2 的验收脚本四项）。所以五项各配一个**可测代理**，
把"人看"的范围压缩到代理测不到的那部分。

## 决定

### 1. 五项检查的可测代理

| 主观项 | 代理指标 | 判据 | 为什么是它 |
|---|---|---|---|
| 跳变 | progress 对**时间**的速率 | ≤ 2.0/s × 1.3 | 见下 |
| 闪烁 | 单调段回退次数；静止段离散度 | 0 次；≤ 0.02 | 闪烁在数据上就是"该单调的地方不单调" |
| 过糊 | 铰链侧行间梯度 | p ≤ 0.55 时 ≥ 150/255 | 刻度还在 = 还能认出是桌面 |
| 过暗 | 远端亮度 | ≥ 8/255 | 有阴影，但不是黑洞 |
| 过彩 | 全帧通道差（max−min） | 渐变关闭时 ≤ 2.0/255 | 关闭态必须严格中性 |

**「跳变」必须用速率，不能用相邻采样点的差。** UIAutomation 读一次
`RangeValue` 要 10~40 ms，采样间隔是浮动的。固定阈值要么在慢读时误报
（0.3 的差在 400 ms 里完全合规），要么在快读时漏报（真的跳 0.3 也判过）。
所以每个采样带时间戳，速率 = Δprogress / Δt，上限直接取引擎自己的
`MaxProgressPerSecond`（2.0/s），再给 30% 采样余量。

**「过彩」必须读 trace 里的 `tuning: gradient enabled=`，不能假设渐变是关的。**
渐变开着时色度是设计输出；硬编码"色度必须≈0"会在用户打开渐变后报假失败。
读到 `enabled=True` 时这项**主动弃权**，而不是判过。

**「p=0 不可见」要逐像素。** 原来只查中心列有没有不透明行。中心列透明、
屏幕角落漏一块不透明区域，同样算失败——所以改为扫描全帧 alpha 字节，
最大值必须为 0。

### 2. DemoPlayer 在 loop 模式端点停留

`DwellSeconds = 0.35`：到达端点后把目标**钉住** 0.35 s 再折返。

原因是目标值和画面不是一回事：引擎是 τ=0.10 s 的指数跟随，0.6 s 一趟在折返点
会落后 0.06~0.17。旧行为（把超调量带到下一趟，速度连续）对**目标值**很平滑，
但屏幕上永远到不了 1.0/0.0。实测：修前 loop 峰值 **0.943**，修后 **1.000**。
而"端点好不好看"正是 M2.3 要用眼睛判断的东西——看不见端点就没法判断。

### 3. 满折覆盖度：一个必须说清楚的模型后果

`WarpFrame.FarEdgeY = M00/(M11+|M21|) = r·cosφ/(r+sinφ)`（φ = p·MaxFold）。

在默认 `MaxFold=90°` 下它**恒等于 0**：玻璃立成边缘朝向相机，progress 1 时
一个像素都不画。真机实测覆盖度：

| p | .25 | .50 | .75 | 1.0 |
|---|---|---|---|---|
| 覆盖 | 74.5% | 48.9% | 24.1% | **0%** |

用户 2026-10-22 反馈的"p=0.8 整个屏幕几乎倒下了，根本看不到效果"，
在这张表里就是 24%→19%→9%→0 的后半程。**这不是 bug，是模型的必然结果。**

关键结论：**只有 MaxFold 影响终局覆盖度，r 不影响**（r 从 2.5 到 6.0，p=1 都是 0）：

| MaxFold | .25 | .50 | .75 | .90 | 1.0 |
|---|---|---|---|---|---|
| 90°（当前默认） | 80% | 55% | 28% | 11% | 0% |
| 80° | 83% | 61% | 37% | 22% | 13% |
| 70° | 85% | 67% | 46% | 34% | **25%** |
| 60° | 88% | 72% | 55% | 44% | 37% |

所以是否降低 MaxFold 是**审美决策，不是缺陷修复**，交给项目所有者。
面板的 `TuningStatus` 已实时显示「满折覆盖 X%」，拖动 MaxFold 就能看到后果，
不需要重新构建（DD-048）。

## 替代方案

* **给后半程补一层全局压暗**，让"效果消失"读成"屏幕熄灭"。这是新 pass，
  属于设计变更（DD-046 明确移除了 dim），不在 M2.3 范围内。
* **用 sigma 而不是 FWHM 描述光带宽度**——已否决（DD-050）。
* **把五项检查全部做成纯客观断言**，不需要人看。否决：过糊/过暗的阈值
  只能保证"没有坏到离谱"，好不好看只有人知道。

## 影响

* `demo-mode-verify.ps1`：9 → **22 项**（六操作全覆盖，含速率判定）；
* `warp-selftest.ps1`：新增覆盖度/色度/过暗/过糊四项指标与 `COVERAGE OK`、
  `CHROMA OK` 两条汇总；`-AnalyzeOnly` 增加 `-DumpGeometry`（trace 有 4 MB
  上限会轮转，旧 dump 的尺寸行可能已不在）；
* `DemoPlayer`：新增 `DwellSeconds`，loop 折返前停留；
* 面板：`TuningStatus` 增加「满折覆盖 X%」。

## 验证（真机 2026-10-23）

* 单测 **138/138**（新增中途停/中途反向/慢速跟随 3 条 + 满折覆盖度 1 条）；
* `demo-mode-verify` **22/22**：快开峰值 1.0 速率 1.79/s、快合 1.95/s、
  慢开 0.38/s（期望 0.33）、中途停 p=0.433 后 1.4 s 离散度 **0.0000**、
  中途反向方向变化 **1** 次、loop 峰值 **1.000** 谷值 **0.000**；
* `warp-selftest` **9/9**：覆盖度 74.51/48.88/24.09/0%（模型 74.6/48.9/24.1，
  误差来自步长采样）、色度 0.09~0.11（严格中性）、p=0 全帧 alpha **0**；
* `tuning-panel-verify` **20/20**（回归）。

## 迁移方案

无。新增指标全部是叠加的；`DwellSeconds` 只在 loop 模式生效，单趟演示不变。

## 相关决策

- DD-038（动画引擎——速率上限是"跳变"判据的来源）
- DD-039（折叠几何——覆盖度公式出自这里的单应矩阵）
- DD-046（物理景深模型——过暗/过糊的阈值对应它的 light/coc）
- DD-048（参数运行时可替换——覆盖度因此能在面板上实时看到）

---

# DD-053：摄像头接入——设备与算法在程序集层分离，探针只对"管线"设门禁

**Status:** Accepted

## 背景

M3.1 是第一次让 DuoFlow 碰摄像头。`HARDWARE_COMPATIBILITY.md` §1.1 的六条铁律里，
第 4 条要求「所有摄像头数据先进入标准化 Camera Profile，再进入视觉算法」。
铁律是原则，不是结构——只要摄像头代码和视觉算法在同一个程序集里，
下一个人（或下一个 AI）就会在算法里顺手读一个 `frame.Width`，铁律就破了，
而且破得没人发现。

同时有一个验收上的现实问题：CI 跑在云端，**没有摄像头**。
如果视觉几何的单测依赖 WinRT，这些测试就只能在老板的笔记本上跑，
那它们等于不存在。

## 决定

### 1. 三个程序集，按"能不能看到像素坐标"切

| 程序集 | TFM | 允许做什么 | 硬约束 |
|---|---|---|---|
| `DuoFlow.Vision` | `net8.0`（纯） | 几何、选型、限速、健康分类 | **任何类型都不许出现像素坐标** |
| `DuoFlow.Camera` | `net8.0-windows` | MediaCapture / MediaFrameReader | 只有它碰设备 |
| `DuoFlow.App` | `net8.0-windows` | 组装 + 探针 | — |

`DuoFlow.Vision` 引不到 WinRT，所以它**编译期就**不可能依赖摄像头——
这比写一句注释强得多。像素坐标唯一的入口是 `CameraProfile`，
顺序固定为 **旋转 → 镜像 → 裁剪+归一化**，并且可逆
（`ToNormalized` / `ToSourcePixel` 互为逆，8 种朝向×裁剪组合的单测容差 9 ulp）。

**枚举时 `Width/Height/FrameRate` 一律报 0，是故意的。** 这三个值是
"协商出来的源"的属性，不是设备的属性。设备宣称 1080p、共享模式下给 720p 是常态；
把宣称值写进 trace 就是让日志撒谎。真实值在 `OpenAsync` 之后从
`MediaFrameSource.CurrentFormat` 读回来。本机实测：枚举 0×0，打开后 1280×720 NV12 @30。

### 2. 探针的门禁只覆盖"管线"，不覆盖"画面里有没有东西"

一次成功的摄像头采集要证明两件性质不同的事：

- **结构性**：设备在、能打开、格式协商了、帧到了、缓冲区是解码后的真数据。
  这是代码和驱动的事，**任何机器上都该成立** → 设为门禁。
- **场景性**：画面有对比度、能看见东西。这是**房间**的事 → 只观测，不门禁。

把场景性设成门禁会得到一个修不好的红灯。本机 2026-10-23 的实测就是例子：

| 观测 | 值 | 说明 |
|---|---|---|
| `FramesOffered` / `Received` / `Dropped` | 32 / 30 / 2 | 不是我们在丢帧 |
| `CameraFps` | 4.88 | 摄像头自己只有 4.9 fps（暗场拉长曝光） |
| `AlphaMean` | **255** | NV12 无 alpha，255 是转换器合成的 → 真 BGRA |
| `ByteMin` / `ByteMax` | 15 / 15 | 整帧均匀，15 = NV12 有限范围黑电平（Y=16） |
| `ContrastDelta` | 0 | 30 帧标准差全为 0 |

管线完全正常，画面是纯黑（锁屏 + 镜头被遮 / 暗房）。**如果门禁是
`ContrastDelta > 0`，这次会失败**——而代码里没有任何一行该改。

所以结构判据换成 **`AlphaMean ≥ 250`**：NV12 不携带 alpha，一次正确的
BGRA 转换必然把它合成 255；读错缓冲区（错格式、错步长、未初始化的分配）
就不会是 255。它证明的是"这堆字节是解码出来的图像"，
比"画面有变化"更接近我们真正想知道的事。
`-RequireContrast` 开关留给环境允许时（灯开着、快门打开）额外证明活画面。

### 3. `FramesOffered` 与 `FramesReceived` 必须分开报

两者从外面看是同一个现象（"帧很少"），修法完全相反：
摄像头慢 → 管线必须容忍低帧率；我们在丢 → 目标帧率设错了。
所以 `MediaCaptureCamera` 在 governor **之前**就记一次数，并记下首帧/末帧时间戳，
`CameraFps = (offered - 1) / span`。不拿 "OpenAsync 返回" 当计时起点——
那会把驱动唤醒时间算到摄像头头上，报出一个偏低的速率，
然后有人会去"修"这个不存在的慢。

### 4. 目标帧率变化时不重置限速时钟

`FrameRateGovernor` 在 `TargetFps` 变化时**不**把 `_lastAcceptedSeconds` 清成 −∞。
最小间隔每次调用都从当前目标现算，所以降速天然会把下一帧挡住。
第一版写了"变更即重置"，单测立刻抓到：10 fps → 1 fps 后 `Accept(0.05)`
仍然放行——那正好是降速的反面。

## 原因

- 程序集边界是唯一不会随时间腐化的约束。注释会被忽略，编译错误不会。
- 验收判据必须**只覆盖被测对象能控制的东西**。让摄像头测试去为房间的亮度负责，
  等于让它永远无法通过。
- 分开报 `offered` / `received` 的成本是一个 `long`，收益是把"该改哪"变成读一个数字。

## 替代方案

- **单程序集 + 命名约定**（`Camera*` 前缀不碰算法）：已否决。约定靠人守，
  而这里的下游是 AI Agent。
- **`DuoFlow.Vision` 也用 `net8.0-windows`**：已否决。那样 CI 里的视觉单测
  就要拉 WinRT，云端跑不起来，等于把 64 个测试锁在老板的笔记本上。
- **用 `ContrastDelta > 0` 当门禁**：已否决，见上表——它在锁屏/暗房下误报。
- **用 `ByteMax > 15` 当门禁**：已否决。它等价于"房间是亮的"，同样是场景性条件。
- **探针存一帧 PNG 便于排查**：已否决，违反 `PROJECT_SPEC` 铁律 5（不落盘任何像素）。
  报告 1.8 KB，全部是标量。

## 影响

- `src/DuoFlow.Vision/`（新增 4 个类型）、`src/DuoFlow.Camera/`（新增）、
  `src/DuoFlow.App/CameraProbe.cs`（新增）、`App.xaml.cs` 的 `--camera-probe` 分支。
- 新增 64 个单测（合计 202），全部在 `DuoFlow.Core.Tests` 里，云端可跑。
- `scripts/acceptance/camera-probe-verify.ps1`：18 项检查，默认不含场景门禁。

## 迁移方案

无，全新模块。M3.2 的屏幕检测必须从 `CameraProfile.ToNormalized` 拿归一化坐标，
不得直接读 `CameraFrame.Bgra` 的像素下标。

## 相关决策

- DD-052（验收口径——同一套"把主观项变成可测量"的方法）
- `HARDWARE_COMPATIBILITY.md` §1.1（摄像头适配六条铁律）
- `HARDWARE_COMPATIBILITY.md` §1.2（设备/算法在程序集层分离）
- DD-038（动画引擎——限速时钟的语义与这里一致）

---

# DD-054：屏幕检测——确定性几何，而不是"看起来像屏幕"的加权打分

**Status:** Accepted

## 背景

M3.2 要在摄像头画面里找到笔记本屏幕，输出**归一化四边形 + 置信度**，
并且按 `HARDWARE_COMPATIBILITY` §4.1 Level 1 的要求：找不到就低置信度降级，
**绝不抛异常**。§2.5 又规定不许通过"知道摄像头装在哪"来解决位置差异——
左上角、顶部中央、刘海、外接，全部要走同一个算法。

这三个约束合起来否掉了两条常见路线：**训练一个检测模型**（对别人的笔记本
天然不可解释，且云端 CI 跑不动），和**假设屏幕是画面里最亮的矩形**（明亮房间
里屏幕往往比背景暗）。

## 决定

### 1. 全部是确定性几何/统计，没有模型

流水线：`GrayImage → 降采样(≤160) → Otsu → 4 邻接连通域 → 凸包 →
Visvalingam 消去至 4 点 → 归一化四边形 → 置信度`。每一步都能手算、能单测、
能在没有摄像头的机器上跑。283 个单测里 100+ 个属于这一块。

### 2. 尺度不变性是被测出来的，不是被声称的

DD-053 说"Vision 里不许出现像素坐标"，但检测算法必须索引输入网格。
所以规则落在**两端**：像素只从 `GrayImage.FromBgra` 进来，结果一律归一化。
中间靠一条可测性质兜底：**同一合成场景在 480×270 / 960×540 / 1280×720
三种分辨率下渲染，归一化四边形必须一致（≤1.5%）**。
这条测试不过，就说明算法偷偷依赖了像素数。

降采样用**面积平均**而不是最近邻：最近邻每块只取一个像素，
输入平移一格就换一个，检测出的边会整整跳一个输出像素。

### 3. 三种"完美的矩形但不是屏幕"必须能否决，不能只扣分

第一版把长宽比、边缘对比、整帧覆盖都当**加权项**。真机与单测各抓到一个假阳性：

| 假阳性 | 长什么样 | 第一版得分 | 为什么加权不够 |
|---|---|---|---|
| 一条亮横线 | 凸、矩形、边缘锐利、对比强 | 0.72 | 只有长宽比（18:1）能识破，权重 0.10 只扣 7% |
| 镜头被遮的传感器噪声 | 同上 | 0.57 | 边界对比只有 ~8 灰阶，权重 0.18 扣不动 |
| 反相极性下的背景本身 | 占画面 98% 的"完美矩形" | 0.56 | 面积项已归零，但矩形度/填充率各 1.0 撑住了 |

所以三者改成**乘法否决**：`score = base × 长宽比 × 对比度 × 覆盖率`。
否决必须能把分数打到 0，而不是削掉一截。

另加一条绝对守卫：**整帧标准差 < 2.0 → `FlatScene`**。
Otsu 会把近均匀帧的噪声底切成"两个模式"并给出阈值，
不设这条守卫，传感器噪声就会被当成一个"区域"。真机实测（镜头被遮）：
守卫前 30 帧里 24 帧被判成可用屏幕，最高置信度 0.57，四边形是整幅画面。

### 4. 两种极性都跑，取置信度高的

明亮房间里屏幕常比周围暗。只找"最亮矩形"的实现在开发者夜里好用、
同一台机器中午就失效。极性作为结果字段报出来，不假设。

### 5. 长宽比必须在物理尺度上算

`NormalizedQuad` 早期版本直接用归一化距离求长宽比。归一化把 16:9 的画面
压成单位正方形，于是**一个正方形物体在 16:9 帧里被算成 0.5625**——
正好落在 0.5 的否决边界上，会被当成"不是屏幕"。
同一段代码在 4:3 摄像头上行为又不一样，这正是本项目要避免的分辨率依赖。

改为 `AspectRatioFor(imageAspect)`，图像长宽比是**必填参数**而不是隐含假设。
真机上就是靠这个才发现：那个四边形的归一化长宽比 1.33，
补偿成物理尺度后是 2.33。

### 6. 被画面边界裁掉的目标单独标记

`TouchesFrameEdge`：任一角落距边框 < 1% 即为真。这种四边形的真实角点在图外，
**长宽比和角角度都有未知偏差**。检测仍然有效（判断"盖子在动"够用），
但 M3.3 不能把它当成完整可见的屏幕来算角度。只报告，不否决。

## 原因

- 判据必须只覆盖被测对象能控制的东西：一条亮线的长宽比是 18:1 这件事，
  和房间亮度无关，所以它可以是硬否决；而"画面有没有对比度"是房间的属性，
  只能是降级理由。
- 否决与加权是两种不同的断言。"这个不可能"必须能归零，"这个弱一点"才该加权。
  把前者写成后者，就会得到"三个独立的致命缺陷各扣一点，加起来仍然及格"。

## 替代方案

- **训练一个矩形检测模型**：已否决。不可解释、对未见机型不可预测、
  云端 CI 无法验证，与 §2.5"不假设安装位置"的方向相反。
- **固定阈值**：已否决。房间亮度变化直接失效；Otsu 按当前帧的直方图自适应，
  且天然分辨率无关。
- **自适应局部阈值**：已否决。需要以像素为单位的块大小，
  正是本决策第 2 条要消灭的依赖。
- **最小外接矩形 / 轴对齐包围盒**：已否决。会把梯形压平，
  而梯形正是 M3.3 要用的角度信号。
- **按 `x+y` / `x−y` 极值取四角**：已否决。笔记本稍微滚转几度就取错角；
  Visvalingam 按面积消去对旋转不敏感。
- **保存一帧 PNG 便于调参**：已否决，违反 `PROJECT_SPEC` 铁律 5。

## 影响

- 新增 `GrayImage` / `OtsuThreshold` / `ConnectedComponents` / `ConvexHull` /
  `QuadFit` / `ScreenDetector` 六个文件（全部 `DuoFlow.Vision`，纯 `net8.0`）。
- 新增 100+ 单测（合计 283），全部云端可跑，不需要摄像头。
- `--camera-detect` 真机探针 + `camera-probe-verify.ps1 -Detect`（27 项检查）。
- `ScreenDetection.Describe()` 输出一行纯数字诊断，不含任何像素内容。

## 真机实测（2026-10-23，LENOVO 83EG）

| 场景 | 帧率 | 整帧亮度 | 可用帧 | 最佳置信度 |
|---|---|---|---|---|
| 镜头被遮 / 暗房 | 摄像头 4.88 fps | 15（NV12 黑电平） | 0/30（`FlatScene`） | — |
| 灯亮、活画面 | 摄像头 30.13 fps | 135 | **30/30** | **0.864** |

同一台机器、同一段代码，两次运行的差别只在环境。这同时反证了 M3.1 里
"4.88 fps 是暗场拉长曝光"的解释：灯一亮，摄像头自己就回到 30 fps。

## 未解决（M3.3 之前必须回答）

**盖子上的摄像头看不到自己的屏幕。** 内置摄像头装在屏幕边框上、朝用户，
画面里不可能出现这台笔记本自己的显示屏；它看到的是房间、用户，
以及可能的外接显示器。而 §2.5 的流程图写的是"寻找显示屏边缘 → 寻找屏幕矩形"。

真机实测的稳定检测目标在画面右下、物理长宽比 2.33、且**触碰画面顶边**——
那更像是外接显示器或桌沿，不是这台笔记本的屏幕。

这直接影响 M3.3 测什么角度：候选解释有
(a) 检测的是外接显示器（对笔记本自身开合无信息量）；
(b) 应该检测的是**键盘面/掌托**在画面中随盖子转动而出现的比例；
(c) 本项目假定使用外接摄像头从侧面看笔记本。

**在选定其中一条之前，M3.3 的几何模型无从谈起。** 本决策只交付一个
"找画面里主导的平面区域"的检测器——它不关心那块区域是什么，
所以三条路都能复用它。

## 相关决策

- DD-053（设备/算法分离、探针只对管线设门禁——同一套"结构性 vs 场景性"思路）
- `HARDWARE_COMPATIBILITY.md` §2.4 / §2.5（不假定 FOV 与位置）、§4.1 Level 1
- `HARDWARE_COMPATIBILITY.md` §2.8（合盖末段 → Prediction Mode）

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
