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
| （待实测填写） | | | | | | | | | | |

记录项说明：

```text
电脑型号 / CPU / GPU / 摄像头 / 屏幕分辨率 / 刷新率
Sensor / HID / Camera Detection / D3D Capture / Performance
问题 / 解决方案
```

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