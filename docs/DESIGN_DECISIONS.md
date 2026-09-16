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

**2. 领先假设（未证实，已按可证伪方式整改）**：旧版探针在 `ForceTopmost` 改 z 序后
立即在 UI 线程 `Thread.Sleep(450)`，改 z 序后的重合成被自身阻塞 → DWM 把这一帧合成
窗口背景色（黑）→ BitBlt 读到 0。与「恒 0.0」「任何环境恒 0」「独立采样器读不到黑」
三个事实吻合。已整改：等待改 `await Task.Delay`（UI 线程可泵消息）、采样前显式
InvalidateRect+UpdateWindow 重绘一帧、GDI 每步写 trace 与
`TransparencyProbe.Diagnostics`（GetDC/CreateDIBSection/BitBlt 返回值 + DIB 前 8 字节
+ 采样矩形 + 主屏/虚拟桌面几何 + DPI），并同次运行加两路对照（后台线程 BitBlt、
GetPixel 点采样）——若 UI 线程采样仍 0 而后台线程正常，即锁定线程阻塞机制。

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
