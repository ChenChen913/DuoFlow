# DuoFlow — Hardware Compatibility

> 不同电脑硬件差异的备案库 + 兼容性策略 + 实测记录
> 本文档不是普通说明文档，它应该成为 AI Coding Agent 在处理硬件相关问题时的第一查询入口。

---

# 1. Compatibility Philosophy

DuoFlow 不假定所有 Windows 笔记本具有相同的：

- 摄像头位置
- 摄像头分辨率
- 摄像头视场角
- 摄像头方向
- 摄像头数量
- 摄像头类型
- 铰链传感器
- HID 设备

所有硬件能力必须通过 Capability Discovery 确认。

## 1.1 摄像头适配铁律（不可推翻）

> **1. 不假定摄像头位置。**
> **2. 不假定摄像头分辨率。**
> **3. 不假定摄像头方向、比例和视场角。**
> **4. 所有摄像头数据先进入标准化 Camera Profile，再进入视觉算法。**
> **5. 必须支持首次运行校准和校准失败后的降级方案。**
> **6. 摄像头只是 LidState 的一种 Provider，不能成为系统唯一依赖。**

## 1.2 核心认识

**分辨率差异本身不是最大的难点**；真正难的是：

* 摄像头视角
* 安装位置
* 镜头 FOV
* 屏幕在画面中的可见程度
* 不同机型的几何关系

所以设计重点应该从"兼容各种分辨率"升级成"**兼容各种摄像头几何条件**"。

千万不能设计成：

```text
摄像头画面
      ↓
固定坐标
      ↓
检测屏幕
      ↓
计算角度
```

这种方案非常容易变成："在我的联想电脑上能跑，在别人的电脑上寄了。"

正确原则：

> **摄像头不是"一个固定位置的传感器"，而是"一个需要经过发现、校准和适配的视觉输入设备"。**

---

# 2. Camera Adaptation

摄像头系统采用分层架构，摄像头本身和"摄像头如何用于测量屏幕开合角度"必须彻底分开：

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

## 2.1 Device Discovery

启动程序的时候不要直接拿默认摄像头。

先扫描：

```text
Camera 0
Camera 1
Camera 2
...
```

获得类似：

```text
CameraDevice
├── DeviceId
├── Name
├── Resolution
├── FrameRate
├── PixelFormat
├── Orientation
├── FieldOfView（如果能获得）
├── Front/Back
└── Availability
```

然后判断：

```text
有没有摄像头？
有没有权限？
能不能正常打开？
支持哪些分辨率？
支持哪些帧率？
```

注意：Windows 允许用户在系统设置里启用/禁用摄像头，而且某些 IR 摄像头等设备不会完全以普通 Camera 的方式出现在 Windows Camera 设置中，所以不能假定"系统里一定有一个普通 RGB 摄像头"。

## 2.2 Resolution

算法内部绝对不要依赖像素坐标。

不要：

```text
x = 640
y = 320
```

而应该全部归一化：

```text
x = 0.0 ~ 1.0
y = 0.0 ~ 1.0
```

例如：

```text
1920 × 1080 的某个点 x=960, y=540 → x=0.5, y=0.5
```

换成 1280 × 720、640 × 480、2560 × 1440，算法都不用改。

这也是为什么必须把 **Camera Resolution** 和 **Vision Coordinate System** 彻底分离。

## 2.3 Orientation

某些摄像头可能出现：

```text
正常
水平翻转
旋转 180°
裁剪
数字变焦
不同宽高比
```

Windows 的 UVC 摄像头栈本身就涉及不同格式、相机方向以及相关能力，不能把"摄像头输出画面"简单假设成一个固定的 16:9 RGB 图像。

所以 Camera Profile 应该增加：

```json
{
  "orientation": 0,
  "mirror": false,
  "crop": {
    "x": 0,
    "y": 0,
    "width": 1,
    "height": 1
  }
}
```

最终全部转换成：

```text
Normalized Camera Space
```

后面的视觉算法只认识这个空间。

## 2.4 FOV

如果能从设备获得视场角信息则记录；不能获得时，通过校准流程间接覆盖。不得在算法中假定固定 FOV。

## 2.5 Position

**不要试图通过"知道摄像头在哪里"来解决位置差异。**

不问"你的摄像头在屏幕左边还是右边？"，而是让视觉算法自己寻找：

```text
摄像头画面
       ↓
寻找显示屏边缘
       ↓
寻找屏幕矩形
       ↓
寻找显示屏与摄像头之间的相对几何关系
       ↓
建立参考坐标
       ↓
计算屏幕姿态
```

不应该：

```text
cameraX = 320
cameraY = 100
```

而应该：

```text
camera position
      ↓
calibration
      ↓
normalized coordinates
      ↓
screen geometry
```

这样即使：

```text
左上角摄像头
顶部中央摄像头
顶部偏左摄像头
顶部偏右摄像头
刘海摄像头
IR + RGB 双摄像头
```

都可以进入同一个算法。

## 2.6 Crop / Digital Zoom

通过 Camera Profile 的 `crop` 归一化字段描述，进入算法前统一补偿。

## 2.7 Calibration

第一次运行的校准流程：

```text
欢迎使用 DuoFlow

为了适配你的笔记本摄像头，
需要进行一次简单校准。

① 将屏幕完全打开
   [开始校准]

② 将屏幕调整到约 90°
   [继续]

③ 将屏幕完全合上
   [完成]
```

程序自己建立：

```json
{
  "camera": {
    "deviceId": "...",
    "resolution": "1280x720",
    "orientation": 0
  },

  "calibration": {
    "openReference": 0.03,
    "closedReference": 0.96,
    "confidenceThreshold": 0.75
  }
}
```

以后就不用每次校准。

对应 PROJECT_SPEC 的三步校准：Open Reference → Mid Reference → Close Reference，之后只做相对变化检测。

## 2.8 Failure Recovery

* 摄像头完全不可用 → 降级到 Sensor 或 Manual
* 检测置信度持续过低 → 降级到 Manual / Safe Mode
* 校准失败 → 允许重试；重试仍失败则进入 Manual 模式
* 合盖末段摄像头来不及反应/画面丢失 → 进入 Prediction Mode（见 DD-034）

---

# 3. Sensor Adaptation

### 3.1 Hinge Sensor

Windows 存在 `HingeAngleSensor` API，但它主要面向双屏/铰链设备，**普通传统笔记本不能假定一定有这种传感器**。必须实际检测，报告 Supported / Unsupported / Unknown 三种状态。

### 3.2 HID

Windows 的 Sensor HID 体系支持设备方向、加速度计、陀螺仪、倾角计等传感器类型，可以把实际硬件能力纳入 Provider，而不是写死。

### 3.3 Accelerometer

### 3.4 Gyroscope

### 3.5 Inclinometer

以上传感器是否可用于推断开合角度，必须实机测试后记录到第 5 节 Device Test Matrix。

---

# 4. Capability Levels

## 4.1 摄像头能力等级

不是简单的"摄像头能用 / 不能用"，而是：

### Level 0：没有摄像头

```text
Camera unavailable
↓
Sensor
↓
Manual
```

### Level 1：有摄像头，但视觉检测不可靠

```text
Camera
↓
低置信度
↓
Fallback
```

### Level 2：普通 RGB 摄像头

```text
RGB Camera
↓
Calibration
↓
Screen Detection
↓
Angle Estimation
```

### Level 3：RGB + IR / Depth

如果设备具备更丰富的传感器，可以进一步利用。Windows 的 UVC 体系本身支持 RGB、IR、Depth 等不同类型的摄像头/传感器流。

## 4.2 系统输入能力等级

```text
Level 0 — No hardware input
Level 1 — Manual only
Level 2 — Camera
Level 3 — Sensor
Level 4 — Sensor + Camera
```

## 4.3 输入体系总原则

摄像头绝对不能成为唯一方案。整个输入体系应该是：

```text
                 ┌─ Hardware Hinge Sensor
                 │
                 ├─ HID / Sensor
                 │
                 ├─ Camera Vision
                 │
                 └─ Manual
                         ↓
                LidStateProvider
                         ↓
                     LidState
                         ↓
                 Animation Engine
```

而不是：

```text
Camera
  ↓
Everything
```

Provider 选择不是绝对优先级，而是基于可靠性评分：

```text
可靠性评分
     ↓
Confidence
     ↓
Provider Manager
     ↓
选择当前最可靠输入
```

例如：

```text
Sensor      confidence = 0.98
Camera      confidence = 0.72
Manual      confidence = 1.00（用户主动控制）
```

正常模式选择 Sensor；Sensor unavailable → Camera；Camera confidence < threshold → Manual / Safe Mode。

---

# 5. Device Test Matrix

实机测试记录表（每台设备一行，实测后填写，不得凭资料预填）：

| Device | CPU | GPU | Camera | Resolution | Position | Sensor | Camera Detection | D3D Capture | Performance | Result |
|---|---|---|---|---|---|---|---|---|---|---|
| LENOVO Legion R7000 APH9（型号 83EG）· Win11 家庭中文版 build 26200 · BIOS PJCN05WW（2026-09-16 实测） | AMD Ryzen 7 7840H（16 逻辑核） | RTX 4060 Laptop（驱动 32.0.15.6094）+ Radeon 780M（32.0.13058.1006），均 Status OK | Integrated Camera（USB\VID_5986&PID_118A&MI_00，Class=Camera，OK） | 1920x1080@144Hz | 待 M3 实测（前置摄像头，位置/FOV 未测） | WinRT 传感器 6/6 Not found（API 在、硬件不在）；PnP Sensor 类 0 设备 → HID 路线不可行；**ACPI 盖设备 PNP0C0D FOUND（“ACPI 盖子”，Status OK）**；root/wmi 45 个 LENOVO_* 类（GAMEZONE/FAN/CPU/GPU/PANEL/MEMORY_METHOD 等） | 待 M3 实测 | 探针确认双 GPU/驱动 OK；D3D11 Capture 真机确认待 M1.4（M0.2 已在云端验证链路） | 待 M1/M8 实测 | 硬件传感器全线缺位 → 本机主路线 = **ACPI 盖事件 + 摄像头视觉**，Manual 兜底；144Hz + 双 GPU 满足渲染条件 |

记录项说明：

```text
电脑型号 / CPU / GPU / 摄像头 / 屏幕分辨率 / 刷新率
Sensor / HID / Camera Detection / D3D Capture / Performance
问题 / 解决方案
```

## 5.1 真机实测检查清单（M0.4 · 由 hardware-probe.ps1 出数据）

> **如何使用**：在目标笔记本上跑一遍 `scripts/hardware-probe.ps1`
> （执行方法见 `docs/SETUP_WINDOWS.md` 第七节），把生成的
> `hardware-probe-result.json` 交给 AI Agent，按下表逐项回填"实测值"列。
> **规则**：每个格子只允许填 `Supported` / `Not found` / `Unknown(原因)`，禁止凭资料预填。

### A. Windows Sensor API 层（探针 Section 1，需 Windows PowerShell 5.1）

| 检测项 | 判定依据（JSON 字段） | 对 DuoFlow 的意义 | 实测值 |
| --- | --- | --- | --- |
| HingeAngleSensor | `sensorApi.sensors.HingeAngleSensor.supported` | 直接的铰链角度 API；双屏设备才有，普通笔记本预期 Not found | **Not found**（GetDefaultAsync() 正常完成并返回 null —— API 在、硬件不在） |
| Accelerometer | `sensorApi.sensors.Accelerometer` | 间接角度信号源（屏幕俯仰会影响重力分量），需实测噪声 | **Not found**（GetDefault() 返回 null） |
| Gyrometer | `sensorApi.sensors.Gyrometer` | 角速度信号，可用于动画速度估计 | **Not found**（GetDefault() 返回 null） |
| Inclinometer | `sensorApi.sensors.Inclinometer` | 姿态角（pitch/roll/yaw），最接近"开合角度"的通用传感器 | **Not found**（GetDefault() 返回 null） |
| SimpleOrientationSensor | `sensorApi.sensors.SimpleOrientationSensor` | 粗粒度方向（开合到临界角度可能触发），备用信号 | **Not found**（GetDefault() 返回 null） |
| LightSensor | `sensorApi.sensors.LightSensor` | 合盖遮光检测的潜在旁证（低优先级） | **Not found**（GetDefault() 返回 null） |

### B. HID / PnP 传感器设备层（探针 Section 2）

| 检测项 | 判定依据 | 对 DuoFlow 的意义 | 实测值 |
| --- | --- | --- | --- |
| PnP `Sensor` 类设备数 | `hid.sensorClassDeviceCount` | ≥1 说明有传感器集线器，HID 传感器路线可行性高 | **Not found**（= 0，无传感器集线器 → HID 传感器路线在本机不可行） |
| 具体传感器设备清单 | `hid.sensorClassDevices[]` | 记录 FriendlyName / InstanceId，M4 按 InstanceId 接入 | **Not found**（空） |
| HID 传感器集合设备 | `hid.hidSensorDevices[]` | "HID Sensor Collection" 是 Windows 传感器标准通道 | **Not found**（空） |

### C. ACPI 层（探针 Section 3）

| 检测项 | 判定依据 | 对 DuoFlow 的意义 | 实测值 |
| --- | --- | --- | --- |
| ACPI 盖设备 PNP0C0D | `acpi.lidDevice[]` | BIOS 有盖设备 → 存在硬件盖事件，可探索监听（Windows 不一定对应用暴露） | **Supported**（FriendlyName "ACPI 盖子" · `ACPI\PNP0C0D\2&DABA3FF&1` · Status OK → M4/M5 盖事件监听的价值目标） |
| ACPI 睡眠按钮 PNP0C0E | `acpi.sleepButtonDevice[]` | 电源事件联动（M5 预研） | **Not found** |
| 厂商 ACPI 设备 | `acpi.vendorAcpiDevices[]` | 厂商**事件**设备（Lenovo ATK 等）才可能携带开合/坞接信号；AMD 平台设备（`ACPI\AMDIxxxx` 裸前缀）只是平台基础控制器，**不携带开合信号，不能作为 M4 信号源** | **Supported**（4 个 AMD 平台设备，均 Status OK：AMDI0030 GPIO Controller · AMDI0009 Micro PEP · AMDI0010 I2C Controller · AMDI0052 PPM Provisioning File）——**性质说明**：这 4 个是 AMD 平台基础控制器（GPIO/PEP/I2C/PPM），不是 Lenovo/ATK 类厂商事件设备；本机未发现任何携带开合信号的厂商 ACPI 事件设备。注：初版探针正则漏匹配 `ACPI\AMDIxxxx` 裸前缀（无 VEN_），2026-09-16 验收轮已修正，数组与脚本输出一致 |
| ACPI 设备总数 | `acpi.acpiDeviceCount` | 基线参考 | 50（基线计数，非判定项） |

### D. 厂商接口层（探针 Section 4）

| 检测项 | 判定依据 | 对 DuoFlow 的意义 | 实测值 |
| --- | --- | --- | --- |
| 机型厂商识别 | `vendor.detectedVendor` | LENOVO / HP / DELL / ASUS / unknown | **LENOVO** |
| root/wmi 厂商类清单 | `vendor.rootWmiVendorClasses[]` | 厂商私有 WMI 方法（如 Lenovo_SetBiosSetting），M4 评估可用接口 | **Supported**（45 个 LENOVO_* 类：UTILITY_EVENT、LIGHTING_EVENT、GAMEZONE_* 系列、FAN_METHOD、CPU_METHOD、GPU_METHOD、PANEL_METHOD、MEMORY_METHOD 等） |
| 专属 WMI 命名空间 | `vendor.namespaces` | root/HP/InstrumentedBIOS、root/dcim/sysman、root/Lenovo 存在性 | **Not found**（三个均 false —— LENOVO 类在 root/wmi 下，不在专属命名空间） |

### E. 摄像头与 GPU 基线（探针 Section 5/6，M3 / M1.4 铺垫）

| 检测项 | 判定依据 | 对 DuoFlow 的意义 | 实测值 |
| --- | --- | --- | --- |
| 摄像头清单 | `camera[]` | M3 Camera Provider 的设备基线（名称/类别/状态） | **Supported**（Integrated Camera · `USB\VID_5986&PID_118A&MI_00\7&14EC10AA&1&0000` · Class=Camera · Status OK） |
| GPU 型号与驱动 | `gpu[].name / driverVer` | M1.4 前确认 D3D11 能力（配合后续 D3D Debug Layer 实测） | **Supported**（NVIDIA GeForce RTX 4060 Laptop · 32.0.15.6094 · OK；AMD Radeon 780M Graphics · 32.0.13058.1006 · OK —— 双 GPU，混合输出架构 M1.4 需注意） |
| 当前显示模式 | `gpu[].currentMode` | 分辨率/刷新率基线（M0.2 捕获已在此环境验证） | **1920x1080@144Hz** |

> **实测状态（2026-09-16 回填完成）**：A-E 五表"实测值"列已由真机数据填毕
> （LENOVO Legion R7000 APH9 / 83EG · Win11 家庭中文版 build 26200 · Windows PowerShell 5.1）。
>
> **方法名勘误（必读，防止再犯）**：初版探针给 6 个传感器全部调用 `::GetDefaultAsync()`，
> 5 个类型报"does not contain a method named 'GetDefaultAsync'"。当时被**错误归因**为
> "Windows Server SKU 投影裁剪"（见 EXECUTION_PLAN 2026-09-15 日志，该归因已作废）。
> **真相（真机反射实证）**：这 6 类的静态工厂方法不同名——只有 HingeAngleSensor 是
> `GetDefaultAsync()`（异步，返回 IAsyncOperation），其余 5 类（Accelerometer / Gyrometer /
> Inclinometer / SimpleOrientationSensor / LightSensor）是 `GetDefault()`（同步，直接返回
> 传感器对象或 null）。反射拿到的静态方法清单：
>
> ```text
> Accelerometer            : FromIdAsync, GetDeviceSelector, GetDefault
> Gyrometer                : GetDeviceSelector, FromIdAsync, GetDefault
> Inclinometer             : GetDeviceSelector, FromIdAsync, GetDefault, GetDefaultForRelativeReadings
> SimpleOrientationSensor  : GetDeviceSelector, FromIdAsync, GetDefault
> LightSensor              : GetDeviceSelector, FromIdAsync, GetDefault
> HingeAngleSensor         : GetDeviceSelector, GetDefaultAsync, GetRelatedToAdjacentPanelsAsync, FromIdAsync
> ```
>
> CI（Server 2025）与真机（Win11 家庭版）表现完全一致，恰好证明与 SKU 无关——方法本来就不存在。
> 探针脚本已按正确方法名修复（真机补测 errors=0）。
>
> **PS 5.1 坑（同批修复）**：`ForEach-Object $变量scriptblock`（内含 param($d)）在 PS 5.1
> 会**静默产出全空对象**（控制台打印正常、JSON 字段全空）；必须内联 scriptblock 用 `$_`。
> 云端 CI 没抓到是因为那些设备数组在 VM 上本来为空。
>
> **对本机路线的结论**：硬件传感器（Hinge/Accelerometer/Inclinometer 等）全线缺位 →
> M4 Sensor Provider 在本机只剩两条路：**ACPI 盖事件（PNP0C0D 已确认存在且 OK）** 与
> **摄像头视觉（M3）**；Manual Provider 兜底不变。摄像头与 144Hz 双 GPU 到位 → M3 与
> M1.4 渲染条件满足（D3D11 Debug Layer 仍待 M1.4 真机确认）。

---

# 6. Known Issues

> 已确认的硬件相关问题与解决方案记录。

## 6.1 熄屏临界点问题（已分析，方案已定）

**现象**：盖子快合上时 Windows 已经熄屏，熄屏后程序进入静默/受限状态，动画无法继续。

**原因**：屏幕熄灭 ≠ 电脑立即睡眠，但熄屏后 Windows 可能进一步进入 Modern Standby / Sleep；进入真正的低功耗阶段后，桌面应用会被 Desktop Activity Moderator（DAM）暂停或限制。这是正常的 Windows 电源管理行为。

**方案**：

1. 区分 Display Off 与 Sleep（DD-032）
2. Pre-Sleep Finalization：在 Sleep 之前完成动画（PROJECT_SPEC §23.1）
3. Prediction Mode：输入丢失后用 Angle + Velocity + Last Known State 短时预测（DD-034）
4. Graceful Degradation：时间不足时缩短动画（DD-031 / DD-034）
5. 摄像头观察屏幕物理结构（边框/反射/轮廓/平面）而非屏幕显示内容，熄屏后仍可能继续检测——需要实机测试不同笔记本的摄像头视角验证

---

## 6.2 真机已知问题（平台行为 · M0.3 真机首跑实测，2026-09-16）

> 本节记录的是**平台行为**（WinUI 3 + WinAppSDK 1.8 的固有特性），不是一次性 bug——
> 后续任何涉及 overlay 窗口的工作（M1.4 渲染、M5 集成、M6 产品化）都必须带着这些约束做。
> 真机环境：Legion R7000 APH9 / Win11 build 26200 / 双 1920x1080@144Hz。

### P0-1：WinUI 3 窗口有两层背景，DWM 玻璃框扩展只解决其中一层

* **实测**：仅 `DwmExtendFrameIntoClientArea(-1)` 时，覆盖层置顶后整个屏幕被不透明黑层遮住
  （覆盖区采样亮度 0.0~3.5；把覆盖层移出屏幕后同一区域 30.6~254.7；`BitBlt CAPTUREBLT`
  重测数值不变，排除 GDI 采样假象）。覆盖层自身装饰（预览面板边框/状态条）正常绘制——
  不是"没渲染"，是"背景不透明"。
* **根因**：WinUI 3 窗口背景两层——Win32 窗口背景 + `DesktopWindowXamlSource` 的
  Composition Visual 背景。前者由 DWM 玻璃框扩展处理，后者必须用
  `ICompositionSupportsSystemBackdrop` 设一个 **alpha=0 画刷**框架才会移除黑底。
  WinAppSDK 1.8 的 winmd 里**不存在** `TransparentBackdrop` 内置类（只有
  SystemBackdrop/MicaBackdrop/DesktopAcrylicBackdrop），必须自己实现
  （最终实现见 `src/DuoFlow.App/OverlayBackdrop.cs`——cnbluefire/WinUI3TransparentBackground
  生产配方：直接接口赋值，非 SystemBackdrop 子类；子类机制被 CI 证伪，见修复状态与 DD-037）。
* **修复状态**：已实现（DD-037，cnbluefire 生产配方：直接 window.As<> 接口赋 alpha-0 画刷
  + 官方 CoreMessaging helper 建 OS Compositor；自定义 SystemBackdrop 子类与 colorkey 两条
  路均被 CI 证伪后废弃）。**✅ 真机复验通过（2026-09-16 同日）**：① 覆盖层置顶时纯红
  参考窗 rgb(255,0,0) 原样透出；② 「置顶 vs 移出」两张全屏截图同点逐位相同
  （lum 55.6 / rgb(46.4,60.2,55.5)）——覆盖层未改变任何像素；③ 覆盖层自身装饰
  （状态条/预览面板/边框）正常绘制在最上层。配方在真机成立，13 轮 CI 迭代有效。
  **⚠ 归因勘误（2026-09-16）**：旧版探针在云上恒读 lum=0 曾被归因为「runner 桌面是
  RDP 类会话，DWM 不按物理控制台处理 per-pixel alpha」——**该归因错误**：真机
  （物理控制台会话 SM_REMOTESESSION=0）用旧探针同样恒 0，而同次运行独立采样证明
  屏幕透明，假阴性在探针自身（详见 DD-037 勘误节）。探针已改异步 + GDI 分级 trace +
  双对照（后台线程/GetPixel），仅信息性记录；透明实证权威 = 真机采样对比（本条上述三证据即
  最新一轮结果）。**云端判别实验（run 35086444842）**：UI 线程与后台线程 BitBlt 同矩形同刻
  均读 0（线程阻塞假设证伪），而 **GetPixel 同刻三点全读 255,255,255（白参考窗透过覆盖层）**——
  BitBlt-from-screen-DC 在覆盖层区域读黑是进程内捕获伪影（当前领先假设，未证实），
  GetPixel 有望成为进程内透明实证手段，待真机控制台点标定后再定是否升级门禁。

### P0-2：WS_EX_TRANSPARENT 单独不足以让 WinUI 3 内容岛穿透鼠标

* **实测**：只设 WS_EX_TRANSPARENT 时，`WindowFromPoint` 命中覆盖层的
  `Microsoft.UI.Content.DesktopChildSiteBridge`（铺满客户区），真实点击/拖动/键盘全部被吞。
* **对照实验**（真实输入注入，每组以"覆盖层移开后点击有效"做控制组）：顶层补
  **WS_EX_LAYERED** 后穿透恢复；子窗口补 LAYERED|TRANSPARENT 或只留 TRANSPARENT 均非必需——
  **最小修复 = 顶层一个 WS_EX_LAYERED**。改完 exstyle 必须 `SetWindowPos(SWP_FRAMECHANGED)`
  才生效。单独给子窗口加 TRANSPARENT（不加 LAYERED）无效。
* **修复状态**：已实现（DD-037，含 `SetLayeredWindowAttributes(LWA_ALPHA,255)` 初始化——
  从未设置属性的 layered 窗口根本不会被合成），**云端 CI 实证穿透恢复**（WindowFromPoint
  不再命中覆盖层 bridge，run 35079881457 pass）。**✅ 真机复验通过（2026-09-16，真实输入
  注入 + 控制组同次运行）**：置顶覆盖层下真实点击轨道 0.40 → 滑块 0.4；真实拖动
  0.70→0.30 全程跟随（中间值 0.60/0.50/0.40，终值 0.29）；控制组（控制台置顶）同次通过
  证明器材有效。覆盖层 exstyle 实测 0x080801A8（TOPMOST|TRANSPARENT|TOOLWINDOW|
  LAYERED|NOACTIVATE 全在）。另真机实证：**IsAlwaysOnTop=true 不破坏穿透**（置顶与穿透
  同时成立），见下方排查线索降级说明。
* **额外发现的坑（CI 实证）**：backdrop brush 连接会令 WinUI 重写 GWL_EXSTYLE——我们的
  ClickThrough/NoActivate/ToolWindow/LAYERED 全部丢失（35079479453）→ 赋值后必须重新
  断言全部 style bits（幂等 EnableLayeredClickThrough），否则穿透连带失效。

### 组合风险：layered + SwapChainPanel + 透明（microsoft/microsoft-ui-xaml#1247）

* 这是官方长期 issue：自 WinAppSDK 1.1.0 起"layered window + SwapChainPanel + 透明"
  可能出现黑底/白底（DirectComposition 窗口）。本项目的覆盖层正是这个组合
  （LAYERED 穿透修复 × 透明修复 × 右下角捕获预览），**两个 P0 修好后必须三件事一起复验**：
  透明 + 穿透 + 预览内容正常。云端 CI 已把这三项做成可证伪探针（亮度采样 / WindowFromPoint /
  捕获帧计数+截图）。**✅ 真机三件套同次运行全部通过（2026-09-16）**，#1247 组合在本机
  未爆雷。真机附带观察一条（不影响结论）：把覆盖层**移出主显示器再移回**后，预览面板
  内容不再出现（同次运行内不自愈，新开进程正常）——疑似 DComp/SwapChainPanel 在窗口
  移出屏幕后的重合成问题（#1247 家族）；生产姿态不会移出屏幕，仅记录，M1.4 接渲染
  管线时顺手排查。

### ✅ 已修复：SwapChainPanel 屏幕内容不可见（2026-09-19 发现，同日修复）

* **现象**：本机（Lenovo R7000，Win11 build 26200）自 2026-09-19 起，SwapChainPanel 的
  合成交换链内容不再出现在屏幕上（面板区域透底，XAML 装饰正常）。warp / blit /
  改动前基线 baaf444 三方复现 → 非 M1.4 回归。KB5129195（InstalledOn 2026-09-15）
  于当日 07:20 重启后生效，时间线吻合。
* **根因定位（区分实验链）**：① WinAppSDK 1.8.* 还原版本 9/16 与 9/19 完全一致 → 排除包升级；
  ② DXGI 查询双输出均 SDR 8bit（RgbFullG22NoneP709）→ 排除 HDR/高级颜色；③ **决定性实验**：
  AlphaMode 换 Ignore 后内容出现但**按 1:1 原样裁切显示（未拉伸到面板）**——
  交换链（1920×1080）大于面板像素尺寸（600×338，DPI 125%）时，更新后的 DWM/合成器
  既不做缩放也不正确处理 premultiplied alpha。结论：**"合成交换链尺寸 ≠ 面板像素尺寸"
  的 DPI 缩放路径被系统更新破坏**（#1247 家族新变体）。
* **修复（生产级 workaround）**：交换链尺寸改为**面板的物理像素尺寸**
  （`CapturePanel.ActualWidth/Height × XamlRoot.RasterizationScale`，本机 600×338）——
  面板 1:1 显示、无缩放需求，premultiplied alpha 恢复正常。warp/mask shader 全程使用
  归一化铰链系坐标，分辨率无关，捕获纹理（1920×1080）照常采样渲染。
  blit 回退路径因 CopyResource 要求同尺寸，在 shader 初始化失败时自动把交换链
  重建成捕获尺寸（M0.2 行为）。修复后：镜像可见、缩放正确（10 条色带每条 ~34px =
  108 × 0.3125 与理论一致）、透明/穿透/预览三件套全绿（预览差分 0 → 122.71）。
* **修复后的代价与注意**：selftest 回读的 raw 从 1920×1080 变为面板像素尺寸（600×338），
  分析脚本已改为从文件大小推导宽高、预测行号按实际高度缩放（精度 ±1px 保持）；
  **DPI/显示器变更时面板像素尺寸会变**，当前实现只在启动时取一次——跨 DPI 移动窗口
  需重建交换链（M1.8 前实现 SizeChanged/ScaleChanged 监听，见 DD-040 影响节）。
* **器材教训（本轮再应验）**：真机拖动注入缺 `MOUSEEVENTF_VIRTUALDESK (0x4000)` 时，
  `mouse_event(MOVE|ABSOLUTE)` 按主屏而非虚拟桌面归一化——双屏（虚拟桌面宽 3840）下
  坐标被压缩一半，拖动终点漂移（0.30→实测 0.04）；补 0x4000 后同脚本同机即 PASS。
  **拖动 FAIL 先查器材，再查应用。**另外：复验脚本 `$dir` 若硬编码旧解压目录，
  会静默测错二进制——跑前先核对被测 exe 路径。

### 排查线索（若修复后仍异常）

* ~~`presenter.IsAlwaysOnTop = true` 有社区报告会让穿透失效——复验不过时第一个查它~~
  **已降级为社区传闻（2026-09-16）**：真机实测 OverlayWindow 的 IsAlwaysOnTop=true 与
  穿透**同时成立**（TOPMOST 置顶下真实点击/拖动穿透均通过），本机未复现该传闻；
  复验不过时**不必先拆它**，优先怀疑器材（如拖动注入未带 MOUSEEVENTF_VIRTUALDESK
  0x4000 时坐标按主屏而非虚拟桌面归一化）与 z 序姿态。
* WM_DWMCOMPOSITIONCHANGED（RDP 会话切换、GPU 驱动重置）会重置 DWM 状态——已通过
  `OverlayWin32Subclass` 在消息里重应用透明配方。

### P1-a：覆盖层内捕获预览真机黑屏 ✅（已关闭，2026-09-16 真机复验通过）

* ~~现象：真机上预览面板边框正常、控制台帧计数持续增长（47~48 FPS），但面板内容黑。~~
* **关闭结论**：该「纯黑」是 P0-1 不透明时代的症状（整个内容岛黑底，预览自然不可见），
  P0-1 修复后一并消失。真机复验三证据：① 桌面 (100,100) 放 400x300 纯红标记，置顶时
  预览内映射点 (1439,809) 读到 rgb(251,69,69)（偏红），而该屏幕位置真实桌面为纯白——
  红色只能来自面板内镜像；② 全屏截图裁剪面板区域可见缩小后的桌面内容（侧边栏图标列）
  与面板背后深色桌面明显不同；③ 帧计数持续增长（running · 47~49 FPS · 累计 600+ 帧）。
  捕获链 + SwapChainPanel 预览真机为活。
* 残留观察（已上移至 #1247 组合风险条目）：覆盖层移出主屏再移回后预览内容不再出现，
  M1.4 接渲染管线时顺手排查。

### P1-b：默认 z 序为「控制台在覆盖层之上」（确认有意为之）

* 两者都 TOPMOST，后创建的 console 浮在 overlay 之上——作为调试台是有意行为，M6 产品化时
  再改（届时 console 变设置界面，生产模式不显示）。probe 做 real-effect 检查时会临时把
  overlay 提到 console 之上（生产姿态），检查完恢复调试姿态。

### 多显示器现状（记录，本轮不实现）

* 覆盖层只覆盖 `DisplayArea.Primary`（主屏）。真机为双 1920x1080：副屏无覆盖、无特效，
  属预期。多屏覆盖在 M0.3 已留 [!] 待真机项，实现排在后续 milestone。

---

# 7. Calibration Profiles

> 校准 Profile 存储规范。每台设备一份，绑定 deviceId。

```json
{
  "camera": {
    "deviceId": "...",
    "name": "...",
    "resolution": "1280x720",
    "frameRate": 30,
    "orientation": 0,
    "mirror": false,
    "crop": { "x": 0, "y": 0, "width": 1, "height": 1 }
  },

  "calibration": {
    "openReference": 0.03,
    "midReference": null,
    "closedReference": 0.96,
    "confidenceThreshold": 0.75
  }
}
```

规则：

* 校准 Profile 必须可重置、可重新校准。
* 更换摄像头设备（deviceId 变化）后必须重新校准。
* 校准数据属于设备本地数据，不上传。