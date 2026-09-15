# DuoFlow — 完整技术方案

> 本文档是项目的初始技术方案。项目最初以「Lenovo Duo」为暂定名进行设计，后正式定名 **DuoFlow**。
> 文中保留原始设计思路与推理过程，作为后续开发与架构评审的依据。
> 来源：项目立项阶段与 AI 的方案讨论记录（已沉淀整理）。

---

## 1. 项目目标

> 给普通 Windows 笔记本增加类似折叠手机的开合视觉效果。

目标不是复制苹果的具体素材，而是实现类似的 **"设备正在折叠，屏幕内容也跟着折叠"** 的视觉语言。

最终体验：

```text
打开笔记本
    ↓
桌面正常显示
    ↓
开始合盖
    ↓
桌面产生轻微透视
    ↓
屏幕边缘逐渐暗化
    ↓
靠近铰链区域逐渐模糊
    ↓
产生柔和光带
    ↓
画面像被折入铰链
    ↓
屏幕关闭 / Windows Sleep
```

打开则完全反向。

---

## 2. 最重要的设计原则

### 原则一：不要模拟"渐变色"

核心是：

**几何变形 + 模糊 + 遮罩 + 光照 + 亮度变化**

而不是：

```css
background: linear-gradient(...)
```

### 原则二：动画必须连续

不要做：

```text
开
↓
检测到合盖
↓
播放一个固定动画
```

而应该做：

```text
lidProgress = 0.00 → 1.00
```

所有视觉参数都由 `lidProgress` 驱动。

### 原则三：输入层和渲染层完全解耦

以后即使发现：

* 摄像头不好用
* 某些联想电脑有 Hall Sensor
* 某些电脑可以读取 Lid 状态

也不应该修改 Shader。

统一输入：

```ts
interface LidState {
  angle: number;
  progress: number;
  velocity: number;
  confidence: number;
  source: "camera" | "sensor" | "manual";
}
```

这样以后换输入方式非常容易。

---

## 3. 技术栈

| 模块          | 技术                        |
| ----------- | ------------------------- |
| 桌面程序        | C# / .NET 8               |
| Windows UI  | WinUI 3                   |
| Windows API | Win32 / Windows App SDK   |
| 摄像头         | Media Foundation / OpenCV |
| AI视觉识别      | MediaPipe 或自研几何检测         |
| GPU         | Direct3D 11               |
| Shader      | HLSL                      |
| 屏幕捕获        | Windows Graphics Capture  |
| 配置          | JSON                      |
| 安装          | MSIX                      |
| 日志          | Serilog                   |
| 自动启动        | Windows Startup           |
| 设置界面        | WinUI 3                   |

### 为什么不推荐 Electron？

因为这个东西属于：**系统级视觉效果**。

Electron 可以做 UI，但：

* 内存占用高
* 屏幕捕获链路更复杂
* GPU 合成控制不够漂亮
* 和 Windows 原生窗口交互麻烦

这个项目最终最好是一个：

```text
DuoFlow.exe
```

常驻后台。

---

## 4. 整体架构

```text
┌─────────────────────────────────────┐
│             DuoFlow                 │
├─────────────────────────────────────┤
│                                     │
│  Sensor Layer                       │
│  ┌─────────┐      ┌─────────────┐   │
│  │ Camera  │      │ Lid Sensor  │   │
│  └────┬────┘      └──────┬──────┘   │
│       └──────────┬───────┘          │
│                  ▼                  │
│          Lid State Engine           │
│                  │                  │
│                  ▼                  │
│          Animation Engine           │
│                  │                  │
│                  ▼                  │
│          Capture Engine             │
│                  │                  │
│                  ▼                  │
│          GPU Render Engine          │
│                  │                  │
│                  ▼                  │
│          Overlay Window             │
│                  │                  │
│                  ▼                  │
│             Windows                │
└─────────────────────────────────────┘
```

---

## 5. 第一阶段：先不要做摄像头

**MVP 第一版直接使用鼠标/键盘模拟开合角度。**

例如：

```text
Ctrl + ↑
    angle += 5°

Ctrl + ↓
    angle -= 5°
```

或者设置一个调试滑杆：

```text
Lid Angle

0° ─────────────── 180°
       ●
```

这样可以先把最难的部分——**Shader**——做好。

否则一开始就搞：摄像头识别 + Windows 捕获 + Shader + 窗口，出了问题根本不知道是哪一层。

---

## 6. 第二阶段：桌面捕获

这里是整个 Windows 实现的核心。

使用 **Windows Graphics Capture** 捕获当前显示器内容，然后：

```text
Desktop
   ↓
GPU Texture
   ↓
Shader
   ↓
Transformed Texture
   ↓
Fullscreen Overlay
```

注意：**不要截图 → CPU Bitmap → 修改 → 再显示。** 这种方案延迟高，而且性能很差。

应该尽可能：`GPU → GPU → GPU`。

---

## 7. Shader 设计：Pass 1 — Perspective Warp

这是整个项目最重要的部分。建议拆成多个 Pass。

根据 `progress = 0 → 1` 逐渐改变屏幕四边的位置。

正常：

```text
┌───────────────────┐
│                   │
│                   │
│                   │
└───────────────────┘
```

合盖：

```text
╲──────────────────╱
 ╲────────────────╱
  ╲──────────────╱
```

**不是简单 Scale。**

---

## 8. Pass 2 — Hinge Mask

建立一个铰链遮罩。

例如 `distanceToHinge` 越靠近铰链 `0 → 1`，然后：

```text
hingeMask = smoothstep(...)
```

效果：

```text
正常区域
██████████████████

铰链区域
░░▒▒▓▓████████████
```

---

## 9. Pass 3 — Blur

根据两个参数：

```text
blurRadius =
    hingeMask *
    progress *
    maxBlur
```

于是：

```text
progress = 0     → blur = 0
progress = 0.5   → blur = 中
progress = 1     → blur = 最大
```

注意不要整个屏幕一起糊。应该是：**越靠近折叠区域越模糊。**

---

## 10. Pass 4 — Dimming

```text
brightness =
    1.0 - hingeMask * progress * 0.8
```

于是：

```text
正常

██████████████

开始折叠

████████████▓▓

继续折叠

████████▓▓▒▒░░
```

---

## 11. Pass 5 — Light Sweep

最后加一条非常克制的光。

例如：

```text
lightPosition =
    lerp(start, hinge, progress)
```

透明度建议非常低：

```text
0.05 ~ 0.15
```

**不要把它做成彩虹灯。**

---

## 12. 最关键的一步：颜色渐变

"颜色渐变"可以单独做。

建议不要固定 `蓝 → 紫 → 粉`，而是采用：

```text
screenContent
      ↓
luminance
      ↓
colorTemperature
      ↓
subtleGradient
```

也就是说，渐变应该**跟随当前桌面内容**。

例如用户桌面是蓝色：

```text
蓝 → 青 → 紫
```

用户桌面是绿色：

```text
绿 → 青 → 蓝
```

这样会高级很多。

---

## 13. 开合动画参数

第一版建议：

```json
{
  "animation": {
    "maxBlur": 18,
    "maxDarkness": 0.72,
    "perspectiveStrength": 0.18,
    "lightIntensity": 0.08,
    "gradientIntensity": 0.12,
    "animationSmoothing": 0.85
  }
}
```

然后所有参数都允许用户调。

---

## 14. 摄像头识别方案

第二阶段再做。

摄像头看到的东西实际上是：

```text
       Camera

          ↓

     ┌─────────┐
     │ Display │
     │         │
     └─────────┘
          ╲
           ╲
            ╲
         Keyboard
```

AI 不需要识别"这是联想笔记本"，只需要识别**屏幕平面**，然后计算：

```text
screen orientation
```

---

## 15. 摄像头算法

推荐：

```text
Camera Frame
      ↓
Resize 640×360
      ↓
Edge Detection
      ↓
Detect Display Rectangle
      ↓
Perspective Transform
      ↓
Estimate Screen Angle
      ↓
Kalman Filter
      ↓
Lid Progress
```

没必要一开始就上大模型。

甚至认为：**传统计算机视觉 + 几何算法**可能比 AI 模型更适合。

因为需要的是：**稳定、低延迟、低功耗。** 不是"理解画面"。

---

## 16. 摄像头的一个巨大问题

**用户在使用电脑的时候，摄像头通常正对着人。**

所以必须考虑：

```text
摄像头
   ↓
人脸
   ↓
屏幕
```

这时候屏幕可能只占摄像头画面的一部分。

因此建议在**第一次运行**时让用户校准：

> 请将笔记本完全打开，然后点击"校准"。
>
> 请将笔记本关闭到约 90°，点击"下一步"。
>
> 请完全合上笔记本。

建立：

```text
openReference
midReference
closeReference
```

之后只做相对变化检测。

---

## 17. 第三阶段：尝试读取真实传感器

这一阶段才研究：

```text
Windows Sensor API
HID
ACPI
Embedded Controller
Hall Sensor
Accelerometer
```

但是这里必须特别谨慎。

**不要假设联想一定提供铰链角度。** 不同型号差异非常大。

因此程序应该：

```text
SensorDetector
       ↓
是否存在可用 Lid Sensor？
       │
   ┌───┴───┐
   │       │
  Yes      No
   │       │
   ▼       ▼
Sensor   Camera
```

---

## 18. 输入优先级

最终：

```text
Hardware Sensor
      ↓
Camera
      ↓
Manual
```

* 有真实传感器 → 用传感器。
* 没有 → 用摄像头。
* 摄像头失败 → 自动降级。

---

## 19. Windows 合盖机制

这个地方非常重要。

**不要让我们的程序和 Windows 抢控制权。**

应该监听：

```text
Power Events
Display Events
Session Events
```

当 Windows 准备 `Display Off / Sleep / Hibernate` 时，程序进入：

```text
ClosingAnimation
```

播放最后：

```text
progress = 0.8 → 1.0
```

然后退出 Overlay。

---

## 20. 一个很重要的体验细节

**不要等 Windows 真的关闭屏幕之后才播放。**

否则：

```text
合盖
 ↓
Windows 立即黑屏
 ↓
用户什么都看不到
```

正确逻辑：

```text
开始合盖
      ↓
Duo Animation
      ↓
达到关闭阈值
      ↓
Windows Display Off
```

但是这里必须尊重系统电源策略。**不能为了动画强行阻止 Windows 睡眠。**

> 修订：该主题在后续讨论中进一步深化为 Pre-Sleep Finalization 机制，见 PROJECT_SPEC.md 的 Power & Sleep Architecture 章节，以及 DESIGN_DECISIONS.md 的 DD-031 ~ DD-034。

---

## 21. 设置界面

建议做一个非常简单的 WinUI 3 设置页面：

```text
DuoFlow
────────────────────────

状态
● 正在运行

检测方式
○ 摄像头
○ 传感器
○ 自动

────────────────────────

视觉效果

透视强度
━━━━━━●━━━━

模糊
━━━━●━━━━━

暗化
━━━━━━●━━━

颜色
━━━━●━━━━━

光带
━━●━━━━━━

────────────────────────

□ Windows 启动时自动运行

□ 使用电池时降低效果

[开始校准]
```

---

## 22. 性能策略

这是项目能不能真正长期使用的关键。

正常情况下：

```text
Camera: 15 FPS
Shader: 60 FPS
```

不需要摄像头 60 FPS。摄像头只负责：**告诉 Shader 当前进度是多少。**

```text
Camera 15 FPS → angle
Shader 60/120 FPS → smooth animation
```

这样性能会好很多。

---

## 23. 电池模式

```text
AC Power → Full Quality
Battery  → Low Power
```

低功耗：

```text
Camera 10 FPS
Shader 30 FPS
降低 Blur
关闭 Light Sweep
```

甚至：

```text
Battery < 20% → Disable
```

---

## 24. 项目目录

```text
DuoFlow/
│
├─ src/
│  ├─ DuoFlow.App/          App.xaml / MainWindow.xaml / Settings
│  ├─ DuoFlow.Core/         LidState.cs / LidStateEngine.cs / AnimationEngine.cs
│  ├─ DuoFlow.Capture/      ScreenCapture.cs / MonitorManager.cs
│  ├─ DuoFlow.Camera/       CameraService.cs / ScreenDetector.cs / AngleEstimator.cs
│  ├─ DuoFlow.Sensors/      SensorDetector.cs / LidSensor.cs
│  ├─ DuoFlow.Render/       D3DRenderer.cs / ShaderManager.cs / OverlayWindow.cs
│  └─ DuoFlow.Power/        PowerEventMonitor.cs
│
├─ shaders/
│  ├─ DuoWarp.hlsl
│  ├─ HingeMask.hlsl
│  ├─ Blur.hlsl
│  ├─ Dimming.hlsl
│  └─ LightSweep.hlsl
│
├─ config/
│  └─ default.json
│
├─ tests/
│
├─ docs/
│  ├─ PROJECT_SPEC.md
│  ├─ DESIGN_DECISIONS.md
│  ├─ TODO.md
│  ├─ HARDWARE_COMPATIBILITY.md
│  └─ TECHNICAL_PROPOSAL.md
│
└─ README.md
```

---

## 25. 关于 DESIGN_DECISIONS.md

这与**"项目初期把不可随意修改的东西沉淀成文档"**的思想完全一致。这个项目尤其应该有。

例如：

```markdown
# Design Decisions

## DD-001

### 决定
GPU Shader 使用 HLSL + Direct3D 11。

### 原因
需要低延迟、高帧率的桌面级视觉效果。

### 不允许 AI 自动修改为
Electron Canvas / CSS Filter / CPU Bitmap Processing

除非人工重新评估。

---

## DD-002

### 决定
LidState 使用 0~1 progress 表示。

### 原因
输入设备与渲染系统解耦。

---

## DD-003

### 决定
摄像头不是最终唯一数据源。

允许未来增加：
- Hall Sensor
- HID
- Accelerometer
```

这能防止以后 AI Code Review 时说"建议把 HLSL 换成 WebGL"，然后直接把架构改掉。

完整版见 `DESIGN_DECISIONS.md`。

---

## 26. 开发路线

不建议一次让 AI 把整个项目写完。按照 Milestone 推进：

### Milestone 1

```text
模拟 LidProgress
+
Windows Graphics Capture
+
Shader
+
Overlay
```

目标：**滑动进度条，桌面真的像折叠一样。**

### Milestone 2

```text
摄像头
+
Screen Detection
+
Angle Estimation
```

目标：**实际合笔记本能够驱动动画。**

### Milestone 3

```text
Sensor Detection
+
Camera Fallback
```

目标：**尽可能自动选择最佳输入源。**

### Milestone 4

```text
Windows Power Events
+
Sleep
+
Display Off
```

目标：**真正成为系统级体验。**

### Milestone 5

```text
WinUI Settings
+
Startup
+
Battery Mode
+
Installer
```

最终：**普通用户双击安装即可使用。**

> 注：以上里程碑在 PROJECT_SPEC.md / TODO.md 中被细化为 M0 ~ M12。

---

## 27. 验收标准

不建议用"代码有没有报错"作为主要标准，而应该用**视觉体验验收**。

### S级

合盖过程中：

* 动画连续
* 无明显跳帧
* 无撕裂
* 无闪屏
* 无突然黑屏
* 透视自然
* 铰链区域有空间感
* 模糊随着角度变化
* 光带非常克制

打开和关闭：**完全对称。**

### 性能指标

目标：

```text
CPU idle       < 2~3%
GPU idle       < 5%
Memory         < 150 MB

Animation      ≥ 60 FPS
Camera         10~20 FPS
```

具体数值还需要根据实际机器实测调整，不应该写死成硬性兼容指标。

---

## 28. Demo Mode

设置里面：

```text
Demo Mode

[▶ Play Closing]

[▶ Play Opening]

Angle
━━━━━━●━━━━

Speed
━━━●━━━━━━
```

这样即使**没有打开/合上电脑**，也能展示效果。

以后录视频、做项目 Demo、发公众号，都特别方便。

---

## 29. 最终产品体验

```text
                DuoFlow

              ┌───────────────┐
              │               │
              │    Windows    │
              │               │
              │               │
              └───────────────┘
                     ↓

               开始合盖

              ┌─────────────╲
              │              ╲
              │   Windows      ╲
              │                ╲
              └─────────────────╲

                     ↓

              ┌───────────╲
              │ ░░░░░░░░░  ╲
              │  ░░░░░░░    ╲
              └──────────────╲

                     ↓

              ┌────────╲
              │ ░░░░░░ ╲
              └─────────╲

                     ↓

                   黑屏
```

真正优秀的版本应该让用户感觉：

> **不是电脑突然播放了一个动画，而是电脑的屏幕真的随着铰链在折叠。**

---

## 30. 技术路线评分

| 方向                       |   推荐度 |
| ------------------------ | ----: |
| WinUI 3                  | ⭐⭐⭐⭐⭐ |
| C#/.NET                  | ⭐⭐⭐⭐⭐ |
| Windows Graphics Capture | ⭐⭐⭐⭐⭐ |
| Direct3D 11              | ⭐⭐⭐⭐⭐ |
| HLSL                     | ⭐⭐⭐⭐⭐ |
| 摄像头视觉识别                  |  ⭐⭐⭐⭐ |
| Windows Sensor/HID       |  ⭐⭐⭐⭐ |
| Electron                 |    ⭐⭐ |
| WebView + CSS            |    ⭐⭐ |
| Python 做最终版本             |    ⭐⭐ |
| 单纯 CSS Gradient          |     ⭐ |

**最关键的一点：先把"视觉引擎"做出来，再解决"怎么知道电脑开合了多少"。**

这会让整个项目的开发难度下降很多。
