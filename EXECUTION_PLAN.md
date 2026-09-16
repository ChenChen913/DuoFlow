# DuoFlow 执行文档（EXECUTION PLAN）

> **本文档是 DuoFlow 执行进度的唯一权威来源（Single Source of Truth）。**
> 任何 AI Agent（Claude Code / Codex / 其他）接手本项目时，**第一件事就是通读本文档**，
> 然后按 §1 交接协议继续工作；每完成一个小任务必须回写本文档（打勾 + 更新快照 + 追加日志）。
> 交接目标是：新 Agent 不依赖任何历史对话，只看仓库内文档即可无缝继续。

| 文档信息 | 值 |
| --- | --- |
| 文档版本 | v1.0 |
| 创建日期 | 2026-09-15 |
| 最后更新 | 2026-09-16 |
| 当前阶段 | Phase 2 — M1 Rendering MVP（Phase 1 / M0 已全部完成，见 §2） |
| 维护者 | 执行当前阶段的 Agent（随阶段交接） |

---

## §1 交接协议（AI Agent 接手必读）

### 1.1 接手步骤

1. **按顺序阅读项目宪法**：
   `README.md` → `docs/PROJECT_SPEC.md` → `docs/DESIGN_DECISIONS.md` → `docs/HARDWARE_COMPATIBILITY.md` → `docs/TODO.md` → 本文档。
2. 查看 **§2 当前状态快照**：确认当前 Phase、当前小任务、下一步行动、阻塞项。
3. 查看 **§5 进度日志** 的最近几条：了解最近完成了什么、有没有未解决的遗留问题。
4. **只执行当前阶段的任务**，不得跳过前置阶段（除非项目负责人明确要求）。
5. 任务完成后**回写本文档**（三件事缺一不可）：
   - §4 中勾选对应复选框；
   - 更新 §2 快照（当前阶段 / 下一步行动 / 最后更新）；
   - 在 §5 追加一条进度日志。
6. 若执行中产生**新的设计决策**：不得只写进代码或对话，必须按 `docs/DESIGN_DECISIONS.md` 的变更模板同步登记（新 DD 条目）。
7. 提交纪律：一个有意义的小任务（或一个阶段收尾）一个 commit，信息格式 `phase(Mx.y): 摘要`，push 到 `main`。

### 1.2 任务标记约定

| 标记 | 含义 | 附加要求 |
| --- | --- | --- |
| `[ ]` | 未开始 | — |
| `[-]` | 进行中 | 标注负责人与开始日期 |
| `[x]` | 已完成 | **必须验收通过后才可打勾**，验收标准见各小节与 `docs/TODO.md` |
| `[!]` | 阻塞 | 必须注明原因 + 已尝试的方案，并写入 §5 日志 |

### 1.3 执行铁律

1. 不得为了完成任务擅自扩大项目范围。
2. 不得推翻 `docs/DESIGN_DECISIONS.md`（DD-001 ~ DD-034）中的既定决策；要改，走决策变更模板。
3. 不得根据网络资料臆断硬件能力——一切以真机实测为准，结果记录到 `docs/HARDWARE_COMPATIBILITY.md`。
4. 不得让动画与 Windows 电源管理对抗：Display Off ≠ Sleep，绝不阻止系统睡眠。
5. 摄像头数据不上传、不保存、不录制、不进日志（隐私红线）。
6. 屏幕捕获全程 GPU → GPU，禁止落地 CPU Bitmap（性能红线）。

---

## §2 当前状态快照

> ⚠️ 每次提交前必须更新本节。

| 项目 | 状态 |
| --- | --- |
| 当前 Phase | **Phase 2 — M1 Rendering MVP**（Phase 1 / M0 全部完成 ✅） |
| 当前小任务 | M1.1 LidState（创建 LidState / LidStateSource / ILidStateProvider / ManualProvider —— 无硬件依赖，可立即开始） |
| 下一步行动 | 按 §4 Phase 2 清单推进 M1.1→M1.3（纯 C# 逻辑 + 单元测试，云端可完成）；M4 Provider Manager 设计时注意：本机传感器全线缺位，真实输入 = ACPI 盖事件 + 摄像头 + Manual；M1.4 起需真机做 D3D11/Warp 调优 |
| 阻塞项 | M1.4 起 D3D11 GPU 确认与视觉调优需真机（真机双 GPU 已确认：RTX 4060 Laptop + Radeon 780M，1920x1080@144Hz）；Overlay 真实鼠标穿透/多屏/高DPI 联测仍待真机；ACPI 盖事件监听可行性待 M4/M5 预研 |
| 最后更新 | 2026-09-16 · M0.4 真机数据回填完成（探针两 bug 修复 + §5/§5.1 回填 + 归因勘误），Phase 1 / M0 全部关闭 · Super Z |

---

## §3 执行总览（Phase × Milestone 对照）

| Phase | 对应 Milestone | 内容 | 状态 |
| --- | --- | --- | --- |
| Phase 0 | — | 立项 · 文档基线 · 仓库基建（README / Topics / 执行文档） | ✅ 已完成 |
| Phase 1 | M0 | Research & Feasibility：开发环境 / 屏幕捕获 / Overlay / 硬件调研 | ✅ 已完成（M0.1-M0.3 云端；M0.4 真机实测 2026-09-16 回填） |
| Phase 2 | M1 | Rendering MVP：LidState / Manual Progress / Warp / Mask / Blur / Dimming | ⏳ 进行中 |
| Phase 3 | M2 | Visual Refinement：Gradient / Light Sweep / Demo Mode / 参数面板 | ⬜ |
| Phase 4 | M3 | Camera Provider：摄像头角度估计驱动动画 | ⬜ |
| Phase 5 | M4 | Sensor Provider：传感器接入 / Provider Manager | ⬜ |
| Phase 6 | M5 | Windows Integration：电源事件 / 开合动画 / 开机自启 | ⬜ |
| Phase 7 | M6 | Product UI：设置界面 / 性能模式 | ⬜ |
| Phase 8 | M7 | Testing：功能 / 动画 / Windows 场景 / 电源 | ⬜ |
| Phase 9 | M8 | Performance：CPU / GPU / Memory | ⬜ |
| Phase 10 | M9 | Privacy & Security | ⬜ |
| Phase 11 | M10 | Documentation | ⬜ |
| Phase 12 | M11 | Packaging | ⬜ |
| Phase 13 | M12 | Final Acceptance | ⬜ |

---

## §4 任务清单（打勾区）

> 打勾规则：完成并**验收通过**后才可勾选 `[x]`。
> 任务定义与验收标准的完整细节以 `docs/TODO.md` 为准，本节是执行层的打勾与细化区。

### Phase 0 — 项目立项与仓库基建 ✅（2026-09-15 完成）

- [x] 创建 GitHub 公开仓库 `ChenChen913/DuoFlow`（main 分支，auto_init）
- [x] 沉淀三份聊天记录为 5 份基线文档（PROJECT_SPEC / DESIGN_DECISIONS / TODO / HARDWARE_COMPATIBILITY / TECHNICAL_PROPOSAL）
- [x] 撰写 `.gitignore`（C# / VS / MSIX / shader 缓存 / 凭据排除）
- [x] 双语 README：中文 `README.md` ⇄ 英文 `README.en.md`（顶部互相切换）
- [x] 配置 GitHub Topics（20 个：windows / winui3 / foldable / hinge / dual-screen 等）与双语仓库描述
- [x] 创建本执行文档 `EXECUTION_PLAN.md`（AI Agent 交接入口）

### Phase 1 — M0 Research & Feasibility ⏳（当前阶段）

#### M0.1 开发环境 ✅（2026-09-15 于 GitHub Actions 云端 Windows 完成）

* [x] 确认 Windows 开发环境（Actions `windows-latest` = Windows Server 2025 Datacenter Build 26100，与 Win11 同核；真机 Windows 11 实测顺延至 M0.4/M5）
* [x] .NET SDK 可用（runner 预装 .NET 10.0.400，net8.0-windows TFM 构建通过）
* [x] Visual Studio MSBuild 可用（vswhere 定位；WinUI 3 必须用 VS MSBuild，dotnet CLI 报 MSB4062）
* [x] 确认 Windows App SDK 可用（Microsoft.WindowsAppSDK 1.6.250602001，自包含运行时自举成功）
* [ ] 确认 Direct3D 11 开发环境（GPU / D3D Debug Layer）——云端无独立 GPU，随 M1.4 渲染开发在真机确认
* [x] 创建最小 WinUI 3 项目（`src/DuoFlow.App`，未打包模式 WindowsPackageType=None）
* [x] 编译并运行 Hello World（CI 构建 ✅ + 进程启动 ✅ PID 8724 窗口标题 'DuoFlow' + 桌面截图）

**验收**：项目可成功 Build ✅；程序可启动并显示窗口 ✅（证据：`docs/media/m0-hello-world.png`，Actions run 34986217173）。
**产物**：`src/DuoFlow.App` 最小工程、`.github/workflows/m0-windows-build.yml`（云端构建门禁）、环境信息记录（§5）。

#### M0.2 Windows Graphics Capture ✅（2026-09-15 云端验证，帧实时显示 31FPS）

* [x] 创建最小屏幕捕获 Demo（`src/DuoFlow.Capture` 类库 + App 内 `CaptureRenderer`）
* [x] 获取主显示器（`MonitorFromWindow` + `IGraphicsCaptureItemInterop.CreateForMonitor`，无需选屏 UI）
* [x] 捕获桌面纹理（FrameArrived → ID3D11Texture2D，实测云端 231 帧/7.5s）
* [x] 验证 GPU Texture（Device→FramePool→CopyResource→Composition SwapChain 全程 GPU→GPU，无 CPU Bitmap；D3D 设备 Hardware 档）
* [x] 实时显示（SwapChainPanel 组合交换链，云端实测 **31 FPS**，见 `docs/media/m0-capture.png` 镜像递归截图）
* [ ] 测试不同分辨率（真机项：多台不同分辨率显示器）
* [ ] 测试高 DPI（真机项：DPI 缩放 >100% 的屏幕）
* [ ] 测试高刷新率（真机项：90/120/144Hz 屏；云端虚拟屏仅 60Hz）

**验收**：Desktop → Captured Texture 实时显示 ✅（云端 31FPS；不同分辨率/高DPI/高刷属真机矩阵测试，顺延至 M1 联调）。
**踩坑记录**：① `IDirect3DDxgiInterfaceAccess` 必须 `InterfaceIsIUnknown`（.NET8 对 IInspectable ComImport 抛 PNSE）；② WinUI3 SwapChainPanel 绑定用 `Vortice.WinUI.ISwapChainPanelNative(panel)` 构造器；③ WinAppSDK 需 ≥1.8 配 Vortice.WinUI 3.8。

#### M0.3 Overlay

* [x] 创建透明 Overlay Window（DWM ExtendFrame 全客户区透明 + 透明 XAML root；截图证实桌面完全透出）
* [x] 设置全屏（无边框 OverlappedPresenter + MoveAndResize 到主屏 OuterBounds；断言窗口矩形 == 屏幕矩形 0,0 1024×768）
* [x] 测试 Topmost（IsAlwaysOnTop + SetWindowPos HWND_TOPMOST；WS_EX_TOPMOST 回读断言）
* [x] 测试 Click Through（WS_EX_TRANSPARENT 设置并回读断言；真实鼠标穿透行为留真机联测）
* [x] 测试 No Activate（WS_EX_NOACTIVATE 设置并回读断言；真实焦点行为留真机联测）
* [x] 测试窗口切换（WS_EX_TOOLWINDOW 设置并回读断言 = 不出现在 Alt+Tab）
* [!] 测试多显示器（经典 Win32 EnumDisplayMonitors 枚举路径云端验证 1 台屏；多屏 overlay 布局/跨屏迁移需真机）

**验收**：Overlay 窗口机制云端全量验证通过 —— smoke JSON 7 属性全 true + Warnings 空（run 34997516255）；捕获渲染已迁入 overlay 实时运行（357 帧 / 25 FPS，GPU→GPU）。"不影响正常鼠标键盘操作"的最终确认属真机项（M0.4 起联测）。
**踩坑记录**：① `DisplayArea.FindAll()` 在 WinAppSDK 1.8 投影下抛 InvalidCastException —— 改用经典 Win32 `EnumDisplayMonitors`/`GetMonitorInfo`；② 无头 CI 排障靠**文件追踪**：App 全链路 Trace 落盘 %USERPROFILE%\duoflow-trace.txt + 未处理异常落盘 duoflow-crash.txt + CI 打印，可秒级定位崩溃点；③ 云 runner 真实分辨率为 1024×768（勿假设 1920×1080）；④ console 控制台窗口需在 overlay 之后创建并 IsAlwaysOnTop，才能浮在 overlay 之上。

#### M0.4 Hardware Research ✅（2026-09-16 完成：云端准备 + 真机实测回填）

> 探针脚本 `scripts/hardware-probe.ps1`（2026-09-15 云端就绪）→ 2026-09-16 真机（Legion R7000 APH9）实测，
> 期间修复探针两个真 bug（见下方踩坑记录），补测 errors=0，数据已回填 `docs/HARDWARE_COMPATIBILITY.md` §5 / §5.1。

* [x] 检查 Windows Sensor API —— 真机实测 6/6 Not found（HingeAngleSensor 的 GetDefaultAsync 正常完成返回 null；其余 5 类 GetDefault() 返回 null —— API 在、硬件不在）
* [x] 检查 HID —— 真机实测：PnP Sensor 类 0 设备、无 HID 传感器集合 → HID 传感器路线在本机不可行
* [x] 检查 ACPI —— 真机实测：盖设备 PNP0C0D FOUND（"ACPI 盖子" / ACPI\PNP0C0D\2&DABA3FF&1 / OK）；睡眠按钮无；4 个 AMD ACPI 设备；总数 50
* [x] 检查联想等厂商硬件接口 —— 真机实测：detectedVendor=LENOVO；root/wmi 下 45 个 LENOVO_* 类（GAMEZONE_*/FAN/CPU/GPU/PANEL/MEMORY_METHOD 等）；三个专属命名空间均不存在
* [x] 记录实际测试结果 —— §5.1 A-E 五表实测值列 + §5 Device Test Matrix 真机行全部回填（2026-09-16）
* [x] 更新 `docs/HARDWARE_COMPATIBILITY.md` 实测区 —— §5.1 归因勘误（见踩坑记录 1）+ 本机路线结论已写入
* [x] 准备一键实测探针 `scripts/hardware-probe.ps1`：7 段只读检测（系统/WinRT Sensor API/HID/ACPI/厂商接口/摄像头/GPU 基线），分段隔离，PS 5.1/7 双兼容，JSON 报告；CI 新增探针 job（语法门禁 + PS 5.1 真实执行 + JSON 机器可读校验 + artifact）
* [x] 建立真机实测检查清单（HARDWARE_COMPATIBILITY §5.1，JSON 字段级判读依据）与执行指南（SETUP_WINDOWS §7）

**验收**：真机（LENOVO Legion R7000 APH9 / 83EG，Win11 家庭中文版 build 26200，PS 5.1）探针数据齐全，补测 errors=0，lidDevice[] / camera[] 字段完整（"ACPI 盖子" / Integrated Camera）；§5 / §5.1 已回填。

**踩坑记录**：
1. **WinRT 静态工厂方法名不同类不同名**：6 个传感器只有 HingeAngleSensor 是 `GetDefaultAsync()`（异步，返回 IAsyncOperation）；其余 5 类是 `GetDefault()`（同步，直接返回传感器对象或 null）。初版探针全部调 GetDefaultAsync，5 类报 "does not contain a method named 'GetDefaultAsync'"，曾被**误归因**为 "Windows Server SKU 投影裁剪"——真机与 CI 表现一致恰好证明与 SKU 无关（方法本来就不存在）。真机反射实证后已修复；判读规则写入 HARDWARE_COMPATIBILITY §5.1 与 SETUP_WINDOWS §7.5。
2. **PS 5.1 的 `ForEach-Object $变量scriptblock` 静默产出全空对象**（scriptblock 内含 param($d)，$d 不绑定）：控制台打印正常、JSON 字段全空；必须内联 scriptblock 用 `$_`。云端 CI 抓不到（那些数组在 VM 上为空）——再次证明 "CI 绿 ≠ 真机行为正确"，涉及真机硬件数据的段落必须真机验证。

### Phase 2 — M1 Rendering MVP

> 进入本阶段时，按实际情况在本节细化任务；以下为基线清单。

#### M1.1 LidState

* [ ] 创建 `LidState`
* [ ] 创建 `LidStateSource`
* [ ] 创建 `ILidStateProvider`
* [ ] 创建 `ManualProvider`

#### M1.2 Manual Progress

* [ ] 创建 Progress Slider（范围 0~1）
* [ ] 支持键盘控制：Home → 0，End → 1
* [ ] 显示当前 Progress

#### M1.3 Animation Engine

* [ ] 创建 Animation Engine
* [ ] 实现 smoothing 与 velocity
* [ ] 防止 Progress 跳变
* [ ] 添加单元测试

#### M1.4 Perspective Warp

* [ ] 创建 `DuoWarp.hlsl`
* [ ] 实现基础 Perspective Warp 并绑定 Progress
* [ ] 调整 Warp 曲线；测试 0 → 1 与 1 → 0

**验收**：Progress 改变时，桌面产生连续空间变形。

#### M1.5 Hinge Mask

* [ ] 创建 Hinge Mask（Hinge Position / Hinge Width / Falloff 可调）
* [ ] 可视化 Debug Mask

#### M1.6 Blur

* [ ] 创建 Blur Pass（局部 Blur，由 Hinge Mask 与 Progress 控制）
* [ ] 调整最大 Blur；测试性能

#### M1.7 Dimming

* [ ] 创建 Dimming Pass（由 Hinge Mask 与 Progress 控制，调整最大暗化程度）

#### M1.8 MVP Composite

* [ ] Warp + Hinge Mask + Blur + Dimming 组合实时运行

**M1 总验收**：Manual Progress → Warp → Blur → Dimming 全链路实时运行。

### Phase 3 — M2 Visual Refinement

* [ ] M2.1 Gradient Pass（颜色/强度可配置，随 Progress 调整，防颜色过度）
* [ ] M2.2 Light Sweep（位置/宽度/强度/开合方向）
* [ ] M2.3 Visual Tuning（慢速/快速/中途停止/中途反向 × 检查跳变/闪烁/过度模糊/过度暗化/过度炫彩）
* [ ] M2.4 Demo Mode（Play Opening/Closing / Pause / Loop / Speed Control）
* [ ] M2.5 Parameter Panel（Warp / Blur / Dimming / Gradient / Light Sweep / Smoothing）

### Phase 4 — M3 Camera Provider

> 实施时结合 `docs/HARDWARE_COMPATIBILITY.md` 的 4 层 Camera Adaptation 架构展开细化。

* [ ] M3.1 Camera Access（枚举 / 打开默认摄像头 / 获取帧 / 控制 FPS / 释放）
* [ ] M3.2 Screen Detection（Resize / Edge / Rectangle / Display Region / Confidence）
* [ ] M3.3 Angle Estimation（几何模型 → Angle / Progress / Confidence）
* [ ] M3.4 Smoothing（低通滤波 / 防抖 / 检测丢失与突变处理）
* [ ] M3.5 Calibration（Open / Mid / Close 校准，保存与重校准）
* [ ] M3.6 Camera Failure（摄像头不存在 / 被占用 / 权限拒绝 / 检测不到屏幕 / Confidence 太低——全部优雅降级）

### Phase 5 — M4 Sensor Provider

* [ ] M4.1 Sensor Discovery（Windows Sensor / HID / ACPI / 设备信息 / 兼容性表）
* [ ] M4.2 Sensor Provider（输出 LidState + Confidence，测试噪声与异常值）
* [ ] M4.3 Provider Manager（Auto → Sensor → Camera → Manual 自动选择 / Failure Fallback / 状态显示）

### Phase 6 — M5 Windows Integration

* [ ] M5.1 Power Events（Display Off / Sleep / Resume / Lock / Unlock / Session Change）
* [ ] M5.2 Closing Animation（检测关闭事件 → Pre-Sleep Finalization 播放最终动画 → Release Overlay → **不阻止 Sleep**）
* [ ] M5.3 Opening Animation（Resume → 初始化 Renderer → 播放开启动画 → 恢复正常桌面）
* [ ] M5.4 Startup（Windows 开机自启 / 托盘图标 / Enable-Disable / Exit）

### Phase 7 — M6 Product UI

* [ ] M6.1 Main Settings（General / Detection / Visual / Performance / Calibration / About）
* [ ] M6.2 Performance Modes（High Quality / Balanced / Battery Saver 三档）

### Phase 8 — M7 Testing

* [ ] Functional（Start / Stop / Enable / Disable / Camera / Sensor / Manual / Calibration / Demo Mode）
* [ ] Animation（Open→Close / Close→Open / 慢速 / 快速 / 反向 / 中途停止）
* [ ] Windows 场景（窗口切换 / 全屏应用 / 浏览器 / 视频播放 / 游戏 / 多显示器 / 高 DPI）
* [ ] Power（AC / Battery / 低电量 / Sleep / Resume / Lock / Unlock）

### Phase 9 — M8 Performance

* [ ] CPU（Idle / Camera / Rendering 占用测量）
* [ ] GPU（Idle / Rendering / Blur / 高刷新率）
* [ ] Memory（启动 / Idle / 渲染 / 反复开合后 / 内存泄漏检测）

### Phase 10 — M9 Privacy & Security

* [ ] 摄像头数据不持久化；无网络上传；无多余权限
* [ ] 日志审查：无屏幕内容、无摄像头画面
* [ ] 第三方依赖审查；安装器权限审查

### Phase 11 — M10 Documentation

* [ ] 同步更新 PROJECT_SPEC / DESIGN_DECISIONS / TODO / HARDWARE_COMPATIBILITY / README（中英）
* [ ] 安装说明 / 故障排查 / 开发说明

### Phase 12 — M11 Packaging

* [ ] Release 构建 / 安装器 / 卸载器 / 自启集成 / 版本信息 / 崩溃处理 / Release Notes

### Phase 13 — M12 Final Acceptance

* [ ] 体验验收（效果自然 / 动画连续 / 无明显延迟 / 无明显闪烁 / 无明显跳变 / 不影响正常使用）
* [ ] 技术验收（Build / Tests 通过 / 无严重错误 / 性能达标 / 摄像头与传感器 Fallback 生效 / 电源行为正常）
* [ ] 文档验收（全部文档同步 / 决策记录完整 / 已知限制成文 / 硬件兼容性成文）

---

## §5 进度日志（append-only，新记录写在最上面）

### 2026-09-16 · Phase 1 / M0.4 收尾（真机实测回填） · Super Z (main agent)

- **真机信息**：LENOVO Legion R7000 APH9（型号 83EG）· BIOS PJCN05WW · AMD Ryzen 7 7840H（16 逻辑核）· Windows 11 家庭中文版 build 26200 · Windows PowerShell 5.1；探针原始产物在用户本地 D:\DuoFlow\probe-output\（仓库外，未污染仓库）
- **探针结果摘要**：WinRT 传感器 6/6 Not found（API 在、硬件不在）；PnP Sensor 类 0 设备（无传感器集线器，HID 路线不可行）；**ACPI 盖设备 PNP0C0D FOUND**（"ACPI 盖子"，Status OK）；厂商接口：45 个 LENOVO_* 类（root/wmi，GAMEZONE/FAN/CPU/GPU/PANEL/MEMORY_METHOD 等），无专属命名空间；摄像头 Integrated Camera（OK）；双 GPU（RTX 4060 Laptop + Radeon 780M，均 OK）；1920x1080@144Hz
- **修复探针两 bug（真机实测发现）**：① 5 个传感器的静态工厂方法是 `GetDefault()`（同步）而非 `GetDefaultAsync()`（仅 HingeAngleSensor 是异步）——**并作废 2026-09-15 日志中 "Server SKU 投影裁剪" 的错误归因**（真机与 CI 表现一致 = 方法名不存在，与 SKU 无关）；② `ForEach-Object $变量scriptblock` 在 PS 5.1 静默产出全空对象（控制台正常、JSON 字段空），三处改内联 scriptblock。两处修复均已在脚本内注释防回退
- **文档回填**：HARDWARE_COMPATIBILITY §5 真机行 + §5.1 A-E 五表实测值 + 方法名勘误段（含反射静态方法清单）；SETUP_WINDOWS §7.4 示例校准 + §7.5 方法名规则/判读更新；本文件 §4 M0.4 全项打勾 + Phase 1 关闭
- **路线影响**：M4 Sensor Provider 本机只剩 ACPI 盖事件 + 摄像头两条路；M3 摄像头与 M1.4 渲染硬件条件满足（混合双 GPU 架构，M1.4 需注意适配）
- **产物**：`scripts/hardware-probe.ps1`（修复版）、HARDWARE_COMPATIBILITY / SETUP_WINDOWS / EXECUTION_PLAN 回填；commits `aed21a4`（修脚本）+ 本 commit（回填）
- **遗留 / 阻塞**：M0 全部关闭，无阻塞；下一步 M1.1 LidState（无硬件依赖，云端可做）；M1.4 起需真机（D3D11 GPU / Warp 调优）

### 2026-09-15 · Phase 1 / M0.4（云端准备） · Super Z (main agent)

- **完成**：M0.4 云端可做部分全部完成——① 一键硬件探针 `scripts/hardware-probe.ps1`（7 段只读检测：0 系统 / 1 WinRT Sensor API / 2 HID / 3 ACPI / 4 厂商接口 / 5 摄像头 / 6 GPU 基线；分段隔离、PS 5.1/7 双兼容、JSON 报告）；② CI 新增 `hardware-probe` job（Parser 语法门禁 → Windows PowerShell 5.1 真实执行 → JSON 机器可读校验 → artifact 上传）；③ `docs/SETUP_WINDOWS.md` 第七节执行指南（放哪/工具/命令/预期输出/判读速查表/排错表）；④ `docs/HARDWARE_COMPATIBILITY.md` §5.1 真机实测检查清单（A-E 五层 × JSON 字段级判读依据 × 待填实测值）
- **云端实证（3 轮迭代，run 35035382145 → 35035937851 → 35036286869 全绿）**：① **HingeAngleSensor 全链路走通**——GetDefaultAsync 操作对象正常返回、await 正常完成、"API 可用但无默认传感器"路径正是真机普通笔记本最可能的结论，代码路径已完整覆盖；② **发现 Server SKU 投影裁剪假象**：Accelerometer 等 5 类在 Server 2025 的 PS 5.1 投影上静态方法缺失（报"does not contain a method named 'GetDefaultAsync'"），而桌面 Win11 上同样的类型字面量调用是社区标准模式——已写入 JSON `note` 字段防真机误判（真机同样报错才可判 API 不可用）；③ **WinRT 静态调用铁律**：必须用类型字面量直接调用 `[Ns.Type, Ns, ContentType = WindowsRuntime]::Method()`；经 `[Type]` 变量的 `$t::Method()` 在 PS 5.1 会抛"方法不存在"或静默返回 null（两种都实测踩到）
- **产物**：`scripts/hardware-probe.ps1`、CI `hardware-probe` job、`SETUP_WINDOWS §7`、`HARDWARE_COMPATIBILITY §5.1`；commits `e06538e` → `f965fdd` → `3735b00`
- **遗留 / 阻塞**：M0.4 前 6 项（Sensor API/HID/ACPI/厂商/记录/实测区回填）全部需要真机探针数据；真机操作路径已由 SETUP_WINDOWS §7 备好（30 秒只读，结果 JSON 交回即可回填打勾）；M1.1 Manual Provider 等无硬件依赖项可先行

### 2026-09-15 · Phase 1 / M0.3 · Super Z (main agent)

- **完成**：透明 Overlay 窗口云端验证全通过 —— 无边框全屏覆盖主屏（0,0 1024×768）+ Topmost + Click-through + No-activate + Alt+Tab 隐藏 + DWM ExtendFrame 全客户区透明；M0.2 捕获渲染迁入 overlay（右下角 480×270 预览）实时运行 **357 帧 / 25 FPS**；左下控制台窗口实时渲染 8 项验证矩阵（全 ✓）；CI 新增 **M0.3 断言门**（smoke JSON 7 属性全 true + MonitorCount ≥ 1 才放行）
- **踩坑记录**：① `DisplayArea.FindAll()` 在 WinAppSDK 1.8 投影抛 InvalidCastException（连续两轮 run 失败的根因）→ 弃用之，改经典 Win32 `EnumDisplayMonitors` + `GetMonitorInfo` + `MonitorFromWindow`；② 无头 CI 排障方法论：App 侧全链路 `Trace.Log` 落盘 + `UnhandledException`/`AppDomain.UnhandledException` 落盘（duoflow-trace.txt / duoflow-crash.txt）+ CI 无论死活都打印 → 第三轮直接定位到 probe 崩溃行；③ smoke JSON 改为 Loaded 即写 + 每秒覆盖刷新，进程中途被杀也有报告；④ 云 runner 分辨率实为 **1024×768**；⑤ 控制台需在 overlay 之后创建并 `IsAlwaysOnTop` 才能浮在穿透层之上
- **产物**：`src/DuoFlow.App/`（OverlayWindow.xaml/.cs、OverlayNative.cs、OverlayProbe.cs、Trace.cs、MainWindow 控制台化、App 双窗口启动 + 异常落盘）、CI workflow（存活检测 + trace/crash 采集 + assert 门）、`docs/media/m0-overlay.png`
- **Commit / Run**：`47af3b0` → `9c75096`（4 轮迭代：首版崩溃 → 诊断加固 → trace 定位 → 根因修复）；成功 run 34997516255
- **遗留 / 阻塞**：真实鼠标穿透/焦点抢占行为、多屏 overlay 布局、高 DPI 缩放 → 真机联测（M0.4 起安排）；M0.4 硬件调研必须真机

> 记录格式：
>
> ```markdown
> ### YYYY-MM-DD · Phase x · Agent 名
> - **完成**：
> - **产物**：
> - **Commit**：
> - **遗留 / 阻塞**：
> ```

### 2026-09-15 · Phase 1 / M0.2 · Super Z (main agent)

- **完成**：屏幕捕获全链路云端验证——`DuoFlow.Capture` 类库（DesktopCapture + D3D11 interop）→ FrameArrived GPU 纹理 → CopyResource → Composition SwapChain → SwapChainPanel 实时显示；云端实测 **31 FPS / 231 帧**，截图呈现镜像递归效果（`docs/media/m0-capture.png`）
- **踩坑记录**：① Vortice API 用法以包内 XML 文档为准（ISwapChainPanelNative 已从 Vortice.WinUI 迁至 Vortice.DXGI，WinUI3 面板绑定用 `new Vortice.WinUI.ISwapChainPanelNative(panel)` 构造器）；② `IDirect3DDxgiInterfaceAccess` 必须 `InterfaceIsIUnknown`，.NET8 对 IInspectable ComImport 抛 PlatformNotSupportedException；③ WinAppSDK 升至 1.8（Vortice.WinUI 3.8.3 依赖）；④ TFM 升至 net8.0-windows10.0.22621
- **产物**：`src/DuoFlow.Capture`（4 文件）、`src/DuoFlow.App/CaptureRenderer.cs`、MainWindow 捕获化改造、`docs/media/m0-capture.png`
- **Commit / Run**：`2873946`→`b4f1d12`（10 轮迭代）；最终成功 run 34992429365
- **遗留 / 阻塞**：不同分辨率/高DPI/高刷测试矩阵需真机；FPS 提示逻辑已修（b4f1d12）

### 2026-09-15 · Phase 1 / M0.1 · Super Z (main agent)

- **完成**：无需用户真机——在 GitHub Actions 云端 Windows（windows-latest = Server 2025 Build 26100）完成 M0.1 全套验证：VS MSBuild 定位 → `src/DuoFlow.App`（最小 WinUI 3，未打包自包含）构建成功 → 应用启动成功（PID 8724，窗口标题 'DuoFlow'）→ 桌面截图留证
- **踩坑记录**：WinUI 3 用 `dotnet CLI` 构建报 MSB4062（PRI 任务 DLL 随 VS 分发，dotnet SDK 内没有）；修复 = CI 改用 vswhere 定位 VS MSBuild /restore 构建
- **产物**：`src/DuoFlow.App`（7 文件）、`.github/workflows/m0-windows-build.yml`（每次 push 自动构建+启动+截图）、`docs/media/m0-hello-world.png`
- **Commit / Run**：`0b0b572` → `90a5cc7`；Actions run 34986217173（success）
- **遗留 / 阻塞**：D3D11 Debug Layer 需真机 GPU（M1.4 前确认）；Windows 11 真机实测顺延至 M0.4 硬件调研

### 2026-09-15 · Phase 0 · Super Z (main agent)

- **完成**：创建公开仓库；三份聊天记录（聊天记录01-03.txt）沉淀为 5 份基线文档；双语 README（中英互切）；GitHub Topics 20 个 + 双语仓库描述；创建本执行文档并确立交接协议
- **产物**：`README.md` / `README.en.md` / `EXECUTION_PLAN.md` / `docs/*.md`（5 份）/ `.gitignore`
- **Commit**：`7657281`（文档基线）+ 本次基建 commit（见 git log）
- **遗留 / 阻塞**：M0.1 起需要 Windows 11 真机环境（Linux 沙箱无法构建 WinUI 3，见 §6.1）；建议项目交接稳定后轮换 GitHub token（曾于对话中明文出现，见 §6.3）

---

## §6 交接注意事项（环境 / 限制 / 安全）

### 6.1 开发环境要求（Phase 1 起生效）

- **构建级验证已上云**：GitHub Actions `m0-windows-build` 工作流在 `windows-latest`（Windows Server 2025 + 预装 VS + .NET SDK）上自动完成 编译 → 启动 → 截图，每次 push 自动触发；WinUI 3 必须用 VS MSBuild 构建（dotnet CLI 会报 MSB4062）；
- 本地开发仍建议 Windows 11 真机 + VS 2022 + 支持 D3D11 的 GPU（D3D Debug Layer）；可用 `scripts/setup-windows.ps1` 一键准备；
- 辅助脚本 [`scripts/setup-windows.ps1`](scripts/setup-windows.ps1) 可自动完成 SDK 检测与最小工程脚手架；**脚本放哪 / 用什么工具 / 怎么执行 / 预期输出 / 故障排查，见 [`docs/SETUP_WINDOWS.md`](docs/SETUP_WINDOWS.md)**；
- **Linux 沙箱只能做**：文档维护、代码编写、git 操作；WinUI 3 的 Windows 构建验证由 Actions 云端承担（Windows App SDK 仅支持 windows TFM，且必须 VS MSBuild）。

### 6.2 架构与文档同步关系

```text
docs/TODO.md                任务怎么定义（参考）
EXECUTION_PLAN.md           执行到哪了（唯一权威打勾区 = 本文档）
docs/DESIGN_DECISIONS.md    已经决定怎么做（事实层，不得擅改）
docs/HARDWARE_COMPATIBILITY.md  真实硬件实测记录（随 M0.4 / M3 / M4 持续追加）
README.md / README.en.md    项目门面（对外介绍，随大阶段更新）
```

### 6.3 安全提醒

- 仓库创建过程中，GitHub Personal Access Token 曾在对话中明文出现；建议交接稳定后立即在 GitHub → Settings → Developer settings → Personal access tokens 中 **revoke 并重新生成**。
- 任何情况下不得把 token / 凭据提交进仓库（`.gitignore` 已排除常见凭据文件）。

### 6.4 对新 Agent 的最后一句话

> 这个项目的成败在"观感的连续性"，不在"功能清单的完成度"。
> 宁可少做一个 Pass，也不要让动画产生跳变。慢慢做，按 Phase 走。
