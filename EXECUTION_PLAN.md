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
| 当前 Phase | **Phase 2 — M1 Rendering MVP**（Phase 1 / M0 全部完成 ✅；M1.1-M1.4 ✅） |
| 当前小任务 | **M1.5 Hinge Mask**（Hinge Position / Hinge Width / Falloff 可调 + 可视化 Debug Mask）——数学放 DuoFlow.Render（复用 DD-039 铰链定义），渲染按 M1.4 同套路（hlsl + 常量缓冲 + --*-selftest 验收） |
| 下一步行动 | M1.5 Hinge Mask → M1.6 Blur → M1.7 Dimming → M1.8 MVP Composite；**真机环境问题待查**（见 HC §6.2：2026-09-19 起 SwapChainPanel 屏幕内容不可见，疑与 KB5129195 重启生效有关——渲染本身已被进程内自测证明正确，不阻塞 M1.5-M1.7 的同套路开发，但 M1.8 全链路视觉验收前必须解决） |
| 阻塞项 | 渲染无阻塞（M1.4 已按进程内自测路线验收 ✅）。**机器显示合成路径疑似被 2026-09-19 生效的 Windows 安全更新破坏**：SwapChainPanel 内容屏幕上不可见（warp/blit/基线 baaf444 三方复现），证据与排查线索见 HC §6.2 与 DD-039 §7——下一位接手若要做屏幕级视觉验证，先处理这条。CI 口径：本次 push 起以 GitHub Actions 实际 run 为准 |
| 最后更新 | 2026-09-19 · M1.4 Perspective Warp 完成（真机 GPU 实证 + 进程内自测 9 档 ≤3px 无滞回）· Super Z |

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

#### M0.3 Overlay ✅（2026-09-16 真机复验通过：两 P0 修复后「透明 + 穿透 + 预览」三件套同次运行全过，DD-037 配方真机成立；P1-a 一并关闭）

> ⚠ **真机首跑结论（2026-09-16）**：下表中的「Click Through 穿透 ✓」与「DWM 透明 ✓」当时是**假阳性**
> ——两项检查只验证了「style bit 被设上」「DWM API 返回 S_OK」，未验证行为。真机实测：
> ① 覆盖层不透明（黑层遮全屏，亮度采样 0.0~3.5 vs 移开后 254.7）；② 鼠标全部被吞
> （WindowFromPoint 命中 DesktopChildSiteBridge）。两 P0 的根因、修复与可证伪探针见 DD-037，
> 平台行为沉淀见 HARDWARE_COMPATIBILITY §6.2。下面原验收记录保留不动（历史）。
>
> ✅ **真机复验结论（2026-09-16 同日，三件套同次运行全过）**：① 透明——纯红参考窗 rgb(255,0,0)
> 原样透出，「置顶 vs 移出」全屏截图同点逐位相同（lum 55.6 / rgb(46.4,60.2,55.5)）；② 穿透——
> 真实点击轨道 0.40 → 滑块 0.4、真实拖动 0.70→0.29 全程跟随（控制组同次通过），exstyle 实测
> 0x080801A8 全 bit 在位；③ 预览——红标记镜像 rgb(251,69,69) + 面板裁剪可见缩小桌面 + 帧计数
> 47~49 FPS（P1-a 关闭）。唯一遗留：App 自带透明探针真机恒 0（假阴性，与 RDP 无关），已修复
> （异步化 + GDI 分级 trace + 双对照，DD-037 勘误节）。器材教训：拖动注入需 MOUSEEVENTF_VIRTUALDESK(0x4000)，
> 否则坐标按主屏而非虚拟桌面归一化；找窗口勿用 `*Overlay*` 模糊匹配（控制台标题也含 Overlay）。

* [x] 创建透明 Overlay Window（DWM ExtendFrame 全客户区透明 + 透明 XAML root；截图证实桌面完全透出）——⚠ 假阳性（云桌面本身黑看不出）；P0-1 已按 cnbluefire 配方重写（直接接口赋 alpha-0 画刷 + 官方 OS Compositor helper），透明实证=真机采样对比（亮度探针仅信息性——云上恒 0 的旧归因「RDP 会话」已被真机复验推翻，见 DD-037 勘误节）——✅ 真机复验通过（2026-09-16：红窗透出 + 置顶/移出逐位相同）
* [x] 设置全屏（无边框 OverlappedPresenter + MoveAndResize 到主屏 OuterBounds；断言窗口矩形 == 屏幕矩形 0,0 1024×768）
* [x] 测试 Topmost（IsAlwaysOnTop + SetWindowPos HWND_TOPMOST；WS_EX_TOPMOST 回读断言）
* [x] 测试 Click Through（WS_EX_TRANSPARENT 设置并回读断言；真实鼠标穿透行为留真机联测）——⚠ 假阳性：真机证实 TRANSPARENT 单独不穿透，P0-2 修复 = 顶层补 WS_EX_LAYERED（真机对照实验 B 组）——✅ 真机复验通过（2026-09-16：真实点击 0.40 / 拖动 0.70→0.29，控制组同次通过；IsAlwaysOnTop 与穿透同时成立）
* [x] 测试 No Activate（WS_EX_NOACTIVATE 设置并回读断言；真实焦点行为留真机联测）
* [x] 测试窗口切换（WS_EX_TOOLWINDOW 设置并回读断言 = 不出现在 Alt+Tab）
* [!] 测试多显示器（经典 Win32 EnumDisplayMonitors 枚举路径云端验证 1 台屏；多屏 overlay 布局/跨屏迁移需真机）——真机现为双 1920x1080：overlay 仅覆盖主屏，副屏无特效属预期（HARDWARE_COMPATIBILITY §6.2 已记录，本轮不实现）

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

#### M1.1 LidState ✅（2026-09-16 云端完成）

* [x] 创建 `LidState`（sealed record，签名逐字 PROJECT_SPEC §5；XML 注释写明 Progress 语义 0=全开 / 1=接近全关）
* [x] 创建 `LidStateSource`（enum，Manual/Camera/Sensor/Unknown 顺序与规格一致；单测锁定成员顺序防重排）
* [x] 创建 `ILidStateProvider`（接口签名逐字 §18：IsAvailable / StartAsync / StopAsync / StateChanged）
* [x] 创建 `ManualProvider`（DD-035 语义：SetProgress clamp 0~1 + 同步 StateChanged，Source=Manual、Confidence=1.0、Velocity=0；Angle 用 §6 示意线性映射占位，校准后续替换）

**验收**：四类型签名与 PROJECT_SPEC §5/§18 逐字一致 ✅；DuoFlow.Core 纯 C#（net8.0，零 WinUI/D3D/Windows App SDK 依赖）✅；单元测试 11/11 通过（本地 .NET 8.0.425 + CI core-tests job）✅。
**产物**：`src/DuoFlow.Core`（4 类型）、`src/DuoFlow.Core.Tests`（xunit，11 用例）、CI workflow 新增 `core-tests` job（WinUI 构建/探针两 job 不受影响）；新决策 DD-035（ManualProvider 驱动语义与 LidState 手动路径取值）。
**放置理由**：类型放 `DuoFlow.Core` 而非 App——项目结构规划里 Core 就是 LidState / AnimationEngine 的家；纯 C# 不依赖 WinUI/D3D/Windows App SDK，云端单测与后续重构成本最低（渲染侧只消费 Progress 的 DD-002 铁律从 M1.1 起守住：Core 不含任何渲染概念）。

#### M1.2 Manual Progress ✅（2026-09-16 云端完成 + **真机验收通过**：UIAutomation 读值 + 真实输入注入，Home/End/点击/拖动全项符合预期，角度严格等于 180−150×Progress，防回环无抖动；DD-036 冻结）

* [x] 创建 Progress Slider（范围 0~1）（StepFrequency 0.01 / 初值 0；挂在 M0.3 控制台窗口内，M0.3 报告区未动 —— 真机验收 ✅）
* [x] 支持 0~1（Slider 全量连续可拖；0/1 边界与 0.01 步进值经 14 用例中的新用例锁定 —— 真机点击轨道 80%/70% 实测值精确 ✅）
* [x] 支持键盘控制：Home → 0，End → 1（根元素 KeyboardAccelerator 全局 scope，无需焦点；真机实证免焦点响应 Home/End ✅；方案理由见 DD-036）
* [x] Home → 0（边界精确性单测：Progress=0 / Angle=180° §6 端点锁定）
* [x] End → 1（边界精确性单测：Progress=1 / Angle=30° §6 端点锁定）
* [x] 显示当前 Progress（`Progress 0.00 · Angle ≈ 180.0°`，拉取式读 ManualProvider.CurrentState —— 单一事实源；真机实测显示与 Slider 值逐项一致、无抖动 ✅；格式与守卫见 DD-036）

**验收（代码层）**：App 引用 Core（net8.0-windows → net8.0 单向引用）✅；Slider → ManualProvider.SetProgress → StateChanged → 显示 链路实现，防回环守卫（值差异 + 回声旗标）就位 ✅；Core 单测 14/14 通过（本地 .NET 8.0.425 + CI core-tests）✅；CI build-winui 连带构建 Core ✅。
**验收（视觉层）**：✅ 真机通过（2026-09-16）——UIAutomation 读取界面文本 + 真实输入注入：End→1.00/30.0°、Home→0.00/180.0°、边界重复按键不变、点击轨道 80%/70% 精确、拖动连续跟随、点选后↑有效；14/14 单测通过；构建用 winget 安装的 .NET 8 SDK 8.0.425（未装 VS Build Tools，dotnet build -p:Platform=x64 可用）。
**产物**：`src/DuoFlow.App`（MainWindow.xaml/.cs M1.2 区块 + csproj 引用）、`src/DuoFlow.Core.Tests`（+3 用例 = 14）、新决策 DD-036（驱动链/键盘方案/防回环/单一事实源/显示格式/依赖方向）。
**踩坑记录**：`WindowClosedEventArgs` 在 WinAppSDK 1.8 的 C# 投影中无法以显式类型引用（CI 实证 CS0246，run 35052712786）——与 M0.2 起 OverlayWindow 的处理一致，改用**类型推断 lambda**（`Closed += (_, _) => Cleanup();`）绕过；处理器内逻辑不变（退订 + StopAsync）。

#### M1.3 Animation Engine ✅（2026-09-16 云端完成：纯 C# + 单测 28/28；渲染接线留 M1.4；真机独立复现通过）

> ✅ **真机独立复现（2026-09-16）**：`dotnet test src/DuoFlow.Core.Tests` 本地 .NET 8 → 28/28 通过；
> AnimationEngineOptions 常量（τ=0.10s / 2.0 全程每秒 / maxDt=0.25s / ε=1e-6）与 DD-038 逐字一致；
> 引擎实现（dt 钳制 / ε 到位 / 速率上限分支）抽查齐全。DD-038 六条决定可直接作 M1.4 接口契约。

* [x] 创建 Animation Engine（`DuoFlow.Core/AnimationEngine`：Provider 原始 LidState → 平滑 LidState 的中间层，零渲染概念——DD-002；位置/理由同 M1.1：Core 纯 C#，云端可测）
* [x] 实现 smoothing（限速指数逼近：v=(target−current)/τ 钓 ±maxV，默认 τ=0.10s / 2.0 全程每秒；DD-038）
* [x] 实现 velocity（有符号斜率，单位 = Progress/秒，正值=关闭方向；到位即 0——M1.1 恒为 0 的字段开始有真实值）
* [x] 防止 Progress 跳变（速率上限硬保证：任何输入含键盘 Home/End 跳变均不瞬变；dt 钳 [0, 0.25s] 防暂停恢复瞬移；首次输入对齐语义见 DD-038）
* [x] 添加单元测试（14 新用例：首次对齐/单调收敛/突变限速/往返反向/极值钓制/NaN 忽略/不规则 dt/大间隙钓制/速度真实值/速度符号翻转/Angle 重建自洽/元数据透传/混沌序列不越界；时间源 ManualTimeSource 注入，无墙钟依赖；合计 28/28 本地+CI 全绿）

**验收**：Core 单测 28/28（本地 .NET 8.0.425 + CI core-tests）✅；引擎不含任何 WinUI/D3D 概念 ✅；ManualProvider（DD-035）语义零改动——引擎在 Provider 下游 ✅；**渲染接线（Update/Tick → Warp uniform）属 M1.4**，本轮不接 UI/渲染（不扩范围）✅。
**产物**：`src/DuoFlow.Core`（AnimationEngine / AnimationEngineOptions / ITimeSource + Stopwatch/Manual 时间源）、`src/DuoFlow.Core.Tests/AnimationEngineTests.cs`（14 用例）、新决策 DD-038（限速指数逼近/Velocity 单位符号/可注入时间源/首帧对齐/输出重建/双驱动入口）。

#### M1.4 Perspective Warp ✅（2026-09-19 完成：真机 GPU 实证 + 进程内自测 9 档 ≤3px 无滞回）

* [x] 创建 `DuoWarp.hlsl`（绕铰链单应逆映射像素着色器：`(sx,sy)=(M00·x,M11·y)/(M00+M21·y)`，四边形外预乘透明 + 边界羽化；p=0 恒等；运行时 D3DCompile，失败自动回退 M0.2 blit）
* [x] 实现基础 Perspective Warp 并绑定 Progress（`DuoFlow.Render/WarpGeometry` 纯数学 + `WarpPipeline` 渲染管线；渲染每帧 `clock.Tick()` → `WarpGeometry.Compute(Current.Progress)` → 常量缓冲——引擎为唯一进度源 DD-002/DD-038；`LidAnimationClock` 门面锁串行化 UI Update × 捕获线程 Tick）
* [x] 调整 Warp 曲线；测试 0 → 1 与 1 → 0（参数化：r=2.5 / MaxFold=90° / TiltAwayFromViewer / HingeAtTop / 羽化 0.004——方向与铰链边一个参数翻转，定稿留 M2.3；单测以"远边宽高比=1/cosφ"投影签名防 scale 冒充）

**验收（数学层）**：Core.Tests 新增 24 用例（p=0 恒等/铺满、单调折叠、四角往返、前后向互逆、投影签名、r 敏感性、0→1→0 无滞回+连续性上界、tilt-toward、铰链镜像、NaN/越界/坏参数、MaxFold/羽化生效）——**合计 52/52 本地+CI 全绿** ✅。
**验收（渲染层）**：真机 GPU（RTX 4060/780M 混合架构，捕获设备上渲染）实证——shader 编译+管线初始化成功（trace: `warp: pipeline ready`）、首帧回读 alpha=255 不透明镜像 + 四角 alpha=24 羽化、48 FPS 出帧 ✅；**进程内自测 `--warp-selftest`**（渲染线程 Present 前 staging 回读整帧含 alpha，量色带边界 vs 单应预测）：p=0/0.25/0.5/0.75 双向 8 档 quadTop 与 9 条边界全部 ≤3px（多数 ≤1px）、无滞回、p=1 完全坍缩符合 MaxFold=90° 数学预期 ✅——"Progress 改变时，桌面产生连续空间变形"在本机显示合成路径故障的情况下以更强精度成立。
**真机三件套复验**：P0-1 透明 delta=0 ✅、P0-2 点击 0.4 + 拖动 0.70→0.29 ✅（首跑拖动 FAIL 为注入器材缺 `MOUSEEVENTF_VIRTUALDESK`——双屏虚拟桌面宽 3840 导致坐标压缩一半，修正后即过；器材教训再次写进 HC §6.2）；P1-a 屏幕层面 differential=0——**非本里程碑回归**，基线 baaf444 复现，见 HC §6.2 环境条目。
**产物**：`shaders/DuoWarp.hlsl`、`src/DuoFlow.Render/`（新纯 net8.0 模块：WarpOptions/WarpFrame/WarpGeometry）、`src/DuoFlow.App/`（WarpPipeline.cs、LidAnimationClock.cs 新增；CaptureRenderer/OverlayWindow/MainWindow/App/OverlayProbe/csproj 接线）、`src/DuoFlow.Core.Tests/WarpGeometryTests.cs`、DD-039、`_test/m14-selftest.ps1`（仓库外验收基建）。
**踩坑记录**：① Vortice 3.8.3 API 与常见资料差异大（`Compiler.CompileFromFile` 非 D3DCompileFromFile、`RenderTargetBlendDescription` 字段名、固定缓冲需 unsafe→改无混合方案、Blob 在 Vortice.DirectX）——反射程序集拿真实签名是唯一可靠路径；② WinUI 面板屏幕坐标在 DPI 125% 下是 DIP×1.25，屏幕采样前必须 `SetProcessDPIAware` 并换算；③ 显示合成路径故障时，**渲染正确性必须与显示正确性分开举证**（进程内回读是渲染侧的权威证据）。

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

### 2026-09-19 · Phase 2 / M1.4 Perspective Warp（真机 GPU 实证 + 进程内自测验收） · Super Z (main agent)

- **完成**：M1.4 三项任务全勾——① `shaders/DuoWarp.hlsl`：绕水平铰链的真单应逆映射像素着色器（`(sx,sy)=(M00·x,M11·y)/(M00+M21·y)`，四边形外预乘透明、边界羽化、p=0 恒等）；② `DuoFlow.Render` 新纯 net8.0 模块（WarpOptions/WarpFrame/WarpGeometry：几何可单测、shader 只吃常量）+ `WarpPipeline` 渲染管线（运行时 D3DCompile、失败自动回退 M0.2 blit）+ `LidAnimationClock` 线程安全门面（UI Update × 捕获线程 Tick，引擎本体零改动 DD-038）；③ 曲线参数化（r=2.5/MaxFold=90°/TiltAway/HingeAtTop/羽化 0.004——方向与铰链边一个参数翻转，定稿留 M2.3）+ 0↔1 双向验证
- **验收（渲染层，进程内路线）**：真机显示合成路径本轮发现**既有环境故障**（SwapChainPanel 屏幕内容不可见，改动前基线 baaf444 同样复现——非本里程碑回归，时间线与 KB5129195 于 2026-09-19 07:20 重启后生效吻合，详见 HC §6.2）→ 按DD-039 §7 改走 **`--warp-selftest` 进程内回读验收**：渲染线程 Present 前 staging 回读整帧（含 alpha），色带参考窗 + 9 档进度 sweep，边界行号 vs 单应理论值——**p=0/0.25/0.5/0.75 双向 8 档 quadTop+全边界误差 ≤3px（多数 ≤1px）、无滞回、p=1 完全坍缩符合预期**；首帧回读另证 alpha=255 镜像 + 48 FPS 出帧（RTX 4060/780M 混合架构，渲染在捕获设备上）
- **单测 24 新用例（合计 52/52 本地+CI）**：p=0 恒等/铺满、单调折叠、四角往返、前后向互逆、投影签名（宽高比=1/cosφ，防 scale 冒充）、r 敏感性、双向无滞回+连续性上界、tilt-toward 端点、铰链上下镜像、NaN/越界/坏参数、MaxFold/羽化生效
- **真机三件套复验（渲染路径变更纪律）**：P0-1 透明 delta=0 ✅；P0-2 点击 0.4 ✅ + 拖动 0.70→0.29 ✅（首跑拖动 FAIL=注入器材缺 MOUSEEVENTF_VIRTUALDESK 0x4000，双屏虚拟桌面宽 3840 致坐标压缩一半——9/16 交接已警示的坑再次应验，HC §6.2 已再强调）；P1-a 屏幕层面 differential=0=环境故障（非回归），渲染正确性由进程内自测独立举证
- **踩坑记录**：① Vortice 3.8.3 真实 API 与常见资料差异大（Compiler.CompileFromFile / RenderTargetBlendDescription 字段名 / 固定缓冲需 unsafe→改无混合方案 / Blob 在 Vortice.DirectX）——**反射程序集拿真实签名是唯一可靠路径**；② DPI 125% 下面板物理坐标 = DIP×1.25，屏幕采样前必须 SetProcessDPIAware + 换算；③ **渲染正确性与显示正确性必须分开举证**——进程内回读是渲染侧权威证据，屏幕采样只对 DWM 合成负责；④ 旧验证脚本 `$dir` 硬编码旧解压目录，复验前先核对被测二进制路径（本轮首跑三件套实际测的是 DuoFlow-main 旧构建）
- **产物**：`shaders/DuoWarp.hlsl`、`src/DuoFlow.Render/`、`src/DuoFlow.App/`（WarpPipeline/LidAnimationClock 新增 + CaptureRenderer/OverlayWindow/MainWindow/App/OverlayProbe/csproj 接线）、`src/DuoFlow.Core.Tests/WarpGeometryTests.cs`、DD-039、HC §6.2 环境条目、`_test/m14-*.ps1`（仓库外验收脚本与产物）
- **遗留 / 阻塞**：渲染无阻塞 → M1.5 Hinge Mask（数学放 DuoFlow.Render，复用 DD-039 铰链定义，验收复用 --*-selftest 套路）；**机器显示合成路径故障待查**（KB5129195/回滚/干净环境对照——M1.8 全链路屏幕级视觉验收前必须解决，否则只能全程走进程内验收）；GitHub token 建议轮换（本/上轮对话明文出现过）

### 2026-09-16 · M0.3 真机复验收尾（三件套通过 + 探针假阴性修复 + 三处小债清理） · Super Z (main agent)

- **真机复验结论（用户提供，同一次 App 运行 + 独立器材）**：三件套全过——① 透明：纯红参考窗 rgb(255,0,0) 原样透出、「置顶 vs 移出」全屏截图同点逐位相同（lum 55.6 / rgb(46.4,60.2,55.5)），覆盖层未改变任何像素；② 穿透：真实点击轨道 0.40 → 滑块 0.4、真实拖动 0.70→0.29 全程跟随（中间值 0.60/0.50/0.40），控制组（控制台置顶）同次通过，exstyle 实测 0x080801A8 全 bit 在位；③ 预览（P1-a 关闭）：红标记镜像 rgb(251,69,69) + 面板裁剪可见缩小桌面 + 帧计数 47~49 FPS。M1.3 真机独立复现 28/28 + 常量与 DD-038 逐字一致。CI 口径订正：CI 绿 = 45a7d77（最后一次代码提交，run 35079881457 #45）；afc573b 为纯文档提交，按 paths 过滤不触发 CI（设计如此，非漏跑）
- **唯一新问题——App 自带透明探针假阴性（lum 恒 0）已修**：真机是物理控制台会话（SM_REMOTESESSION=0）探针照样恒 0，且同次运行独立采样证明屏幕透明——「云 RDP 会话不处理 per-pixel alpha」旧归因**被推翻**（三条对照实验：不泵消息的原生白窗 450ms 后 rgb(255,255,255)、逐字复刻探针 GDI 序列读到 85=255/3、CAPTUREBLT 无关）。领先假设（未证实）：旧同步流程在 `ForceTopmost` 改 z 序后立即在 UI 线程 `Thread.Sleep(450)`，重合成被自身阻塞 → DWM 合成黑背景 → BitBlt 读 0（与「恰为 0.0」「任何环境恒 0」「独立采样器读不到黑」三事实吻合）
- **判别实验定案（两次云端 CI 运行，假设裁决不靠猜）**：run 35086444842 实测——① UI 线程 BitBlt=0 且**后台线程 BitBlt（Task.Run 同矩形同刻）=0** → 「UI 线程阻塞」假设 A **证伪**；② 同刻 GetPixel 三点全读 255,255,255 = 白参考窗透过覆盖层，BitBlt 返回 TRUE 且 DIB 前 8 字节全 0，几何/DPI 全部正常 → 排除 API 失败与坐标换算；③ **假设 B（当前领先，未证实）：BitBlt-from-screen-DC 在覆盖层区域读黑是进程内捕获伪影**（疑似限自属 per-pixel-alpha layered 窗口），与真机「独立进程 BitBlt 正常、App 进程恒 0」的旧观察吻合；④ GetPixel 暂不升门禁（参考窗是普通 GDI 窗口的混淆：整体排除 layered 内容的捕获路径在不透明覆盖层下也会读白）——探针已加 GetPixelPass（信息性）与窗外对照点 GetPixelOutsideRgb，下轮真机运行用「控制台点对照」标定后再定
- **探针整改（可证伪）**：① 等待改 `await Task.Delay(450)`（UI 线程可泵消息，DWM 可重合成）；② 采样前 `InvalidateRect + UpdateWindow` 显式重绘一帧；③ GDI 每步写 trace 与 `TransparencyProbe.Diagnostics`（GetDC/CreateCompatibleDC/CreateDIBSection/BitBlt 返回值 + DIB 前 8 字节 + 采样矩形 + 主屏/虚拟桌面几何 + DPI，manifest=PerMonitorV2）；④ 同次运行双对照：后台线程 BitBlt（`Task.Run`）与 GetPixel 点采样采同一矩形——若 UI 线程仍 0 而后台正常即锁定线程阻塞机制；⑤ 探针定位如实改写：进程内自采样、信息性记录、不进 CI 门禁，透明实证权威=真机采样对比不变（穿透门禁 HitTest.Pass 不动）。修复后真机重验待下轮复验顺带确认
- **三处小债清理**：① 代码注释与实现矛盾——`OverlayWindow.xaml.cs` 类级/BackdropApplied XML 注释删「TransparentBackdrop」表述、构造函数 colorkey 叙事改为 LWA_ALPHA 现状（colorkey 已证伪回退）、Win32 层注释删「WM_PAINT 填充」（已移除）、`OverlayBackdrop.cs` 第 4 点同步修正 + 补真机验证结论；② `EXECUTION_PLAN` §5 M0.3 条目首段旧 TransparentBackdrop 方案加删除线废弃标记；③ HC §6.2 `IsAlwaysOnTop` 排查线索降级为社区传闻（真机实证置顶与穿透同时成立，复验不过先疑器材）
- **产物**：`src/DuoFlow.App/OverlayProbe.cs`（异步化 + 诊断字段）、`OverlayNative.cs`（分级采样/GetPixel/GetSystemMetrics/GetDpiForSystem/UpdateWindow）、`OverlayWindow.xaml.cs`、`OverlayBackdrop.cs`（注释纠偏）、`.github/workflows/m0-windows-build.yml`（RDP 归因注释修正 + 诊断输出）、`docs/DESIGN_DECISIONS.md`（DD-037 勘误节）、`docs/HARDWARE_COMPATIBILITY.md`（§6.2 复验结论/勘误/降级/P1-a 关闭）、本文件三件套
- **遗留 / 阻塞**：无；下一步 M1.4 Perspective Warp（真机 D3D11；AnimationEngine.Update/Tick/Current 为渲染侧唯一入口，渲染层必须在透明层之上叠加，任何改动后重跑三件套；Warp 视觉验收建议网格/棋盘格参考图量格线形变）

### 2026-09-16 · Phase 2 / M1.3 Animation Engine · Super Z (main agent)

- **完成**：`DuoFlow.Core/AnimationEngine`（+ Options + ITimeSource，纯 C# 零渲染概念）——Provider 原始 LidState → 平滑 LidState 的中间层。算法=**限速指数逼近**（v=(target−current)/τ 钓 ±maxV，默认 τ=0.10s / 2.0·全程每秒）：远离目标时段速稳定、接近目标时指数收敛、中途反向符号自动翻转；越过目标即到位（ε 内速度置 0，无渐近爬行/无超调抖动）
- **Velocity 真实值**：有符号斜率、单位 Progress/秒（正值=关闭方向）——M1.1 恒为 0 的字段自本里程碑起有真实语义（DD-038）
- **防跳变三重保证**：① 速率上限硬保证（键盘 Home/End 跳变也不瞬变，0→1 ≥ 0.5s）；② dt 钳 [0, 0.25s]（暂停恢复/渲染卡死不瞬移，最坏单步位移 ≤ maxV×maxDt=0.5）；③ 首次输入对齐语义（新建引擎采采纳现状，防跳变只作用于观察到过的变化）
- **时间源可注入**：ITimeSource（Stopwatch 生产 / ManualTimeSource 单测），禁止 DateTime.Now——全部 28 用例确定性通过，无墙钟不稳定
- **输出重建**：Progress/Velocity=平滑值；Angle 用 §6 示意映射从平滑 Progress 重建（显示连续，延续 DD-035 占位语义）；Source/Confidence 原样透传；NaN 输入整帧忽略
- **单测 14 新用例**：首次对齐/单调收敛/突变限速/往返反向/极值钓制/NaN 忽略/不规则 dt/大间隙钓制/速度真实值与符号翻转/Angle 自洽/元数据透传/混沌序列（固定种子）不越界；**合计 28/28** 本地（.NET 8.0.425）+ CI core-tests 全绿；修了 2 个初版测试自身未考虑 dt 钳制语义的误断（实现行为正确）
- **产物**：`src/DuoFlow.Core`（3 文件）、`src/DuoFlow.Core.Tests/AnimationEngineTests.cs`、DD-038 登记、本文件三件套；注：引擎代码文件随 M0.3 CS0115 修复 commit（6f44605）一并入库（add -A 时已在工作树，不重写已推历史），本 commit 为 M1.3 文档与验证收尾
- **边界**：引擎不接 UI/渲染（M1.4 以 Update/Tick/Current 接口接 Warp uniform）；ManualProvider（DD-035）零改动；不扩范围
- **遗留 / 阻塞**：无；M1.4 Perspective Warp 需真机 D3D11（可先云端备 shader 框架 + 构建验证）；M0.3 P0 修复真机复验待做（透明+穿透+预览同次运行）

### 2026-09-16 · Phase 1 / M0.3 P0 修复（真机首跑发现的两个假阳性） · Super Z (main agent)

- **真机首跑结论**：M0.3 当时的「Click Through ✓」「DWM 透明 ✓」两项为假阳性（只验了 style bit / API 返回值，没验行为）——真机上覆盖层①不透明（黑层遮全屏：覆盖区亮度 0.0~3.5 vs 移开后 254.7，CAPTUREBLT 重测排除采样假象）②吞鼠标（WindowFromPoint 命中 DesktopChildSiteBridge，点击/拖动/键盘全无效）。两者必须在 M1.4 前修掉，否则后续所有视觉效果要么看不见要么没法交互
- **P0-1 修复（透明=双层缺一即黑屏）**：~~① XAML 岛层——新建 `TransparentBackdrop`（自定义 SystemBackdrop 子类，OnTargetConnected 里对 ICompositionSupportsSystemBackdrop 设 alpha=0 画刷；WinAppSDK 1.8 无内置 TransparentBackdrop，已查 winmd 确认）~~（**首段为当日早间旧方案，已被本条目末尾【CI 迭代终态补充】推翻：SystemBackdrop 子类 brush 连接成功但屏幕仍黑，勿照抄；终态=直接 window.As<> 接口赋 alpha-0 画刷，见 DD-037**）；② Win32 层——DwmExtendFrameIntoClientArea 改 **MARGINS(0)**（弃用 -1 老写法）+ DwmEnableBlurBehindWindow 空区域（CreateRectRgn(-2,-2,-1,-1)）+ SetWindowSubclass 处理 WM_ERASEBKGND（填黑 return 1，premultiplied alpha 下全透明）与 WM_DWMCOMPOSITIONCHANGED（RDP/驱动重置后重应用）
- **P0-2 修复（穿透）**：顶层补 **WS_EX_LAYERED**（真机对照实验 B 组的最小改动）+ SetLayeredWindowAttributes(LWA_ALPHA,255) 初始化（未设属性的 layered 窗口不会被合成）+ SetWindowPos(SWP_FRAMECHANGED) 生效；子窗口不动
- **假阳性堵漏（可证伪探针进 CI 门禁）**：OverlayProbe 新增 Section 5——① 透明探针：进程内创建白色参考窗口（非 TOPMOST 天然在覆盖层下，避开 console/装饰位置）→ 覆盖层提到 TOPMOST 最前 → BitBlt 采样亮度 ≥80 判过（修复失败 ≈0）；② 命中探针：WindowFromPoint(被覆盖的 console 中心) 的 root 不得是 overlay（API 级穿透证据；真实 SendInput 注入由真机复验）；每进程只跑一次（z 序翻转避免每秒闪烁），检查完恢复调试 z 序；CI 断言新增 Transparency.Pass / HitTest.Pass / LayeredApplied / BackdropApplied
- **同次运行三件套纪律**：layered + SwapChainPanel + 透明 = microsoft-ui-xaml#1247 已知问题组合——透明/穿透/预览内容（帧计数+截图）必须在同一次运行里同时验证，云端 CI 已按此断言
- **P1-a/P1-b 结论**：预览黑屏待真机结论（候选：WGC 自反馈 / #1247 家族；本轮不动捕获链），HC §6.2 已记录检查方法；z 序「console 在 overlay 之上」确认为有意调试台行为（M6 产品化再改）；多显示器现状（仅主屏覆盖）已记录
- **产物**：`src/DuoFlow.App/OverlayBackdrop.cs`（新增）、`OverlayWindow.xaml.cs`、`OverlayNative.cs`、`OverlayProbe.cs`、`.github/workflows/m0-windows-build.yml`、`docs/DESIGN_DECISIONS.md`（DD-037）、`docs/HARDWARE_COMPATIBILITY.md`（§6.2 真机已知问题）、本文件三件套
- **遗留 / 阻塞**：无；下一步 M1.3 Animation Engine
- **【CI 迭代终态补充，同日】**：修复共经 13 轮 CI 迭代（35072771848→35079881457），逐轮证伪三条路径后定型：① SystemBackdrop 子类 brush 连接成功但屏幕仍黑（不生效）；② colorkey（castorix 实际启用路径）抠不到 island DComp 底色且令穿透失效；③ XAML compositor 复用（cast/As<T> 均失败——两个 Compositor 是不同 WinRT 运行时类）。终态=cnbluefire 生产配方（window.As<> 直接赋 alpha-0 画刷 + 官方 CoreMessaging helper 建 OS Compositor[DQTAT_COM_STA]）+ **backdrop 连接后重断言 style bits**（brush 连接会重写 GWL_EXSTYLE，35079479453 实证穿透连带失效）。**云端终态**：穿透探针 pass=True（API 级穿透恢复实证）+ 预览正常 + style bits 齐全 + App 存活；亮度探针在云 RDP 会话恒 0（DWM 不处理 per-pixel alpha）→ 降为信息性指标，透明实证权威=真机采样对比。全部死路与判据已沉泄 DD-037 与 HC §6.2

### 2026-09-16 · Phase 2 / M1.2 Manual Progress · Super Z (main agent)

- **完成（代码层）**：Manual 这条路推进到 UI 层 —— ① `DuoFlow.App.csproj` 引用 `DuoFlow.Core`（net8.0-windows → net8.0 单向，零障碍）；② `MainWindow.xaml` 在 M0.3 控制台内新增 M1.2 区块（M0.3 报告区零改动）：Slider（Minimum=0 / Maximum=1 / StepFrequency=0.01 / 初值 0）+ `ProgressText` 显示（Progress 两位小数 + Angle 一位小数，便于肉眼核对 §6 示意端点 180°/30°）+ 根元素 `KeyboardAccelerator`（Home/End）；③ `MainWindow.xaml.cs` 接线：Slider.ValueChanged → ManualProvider.SetProgress → StateChanged → 更新显示；窗口 Closed 时退订 + StopAsync，ManualProvider 生命周期跟随窗口
- **键盘方案**：WinUI 3 的 Window 类没有 KeyDown 事件（与 WPF 不同），Home→0 / End→1 用根元素 KeyboardAccelerator 全局 scope 实现——无需焦点即响应，避免 Slider 抢焦点方案；键盘路径统一 DriveProgress（先同步 Slider 可视值，再显式 SetProgress），同值重复按键也保留 DD-035 的同值重发语义
- **防回环 + 单一事实源**：显示值只从 ManualProvider.CurrentState 拉取（UI 不存状态副本）；StateChanged 回写 Slider.Value 双重守卫——值差异 > 0.0005（浮点 epsilon 避开 StepFrequency 舍入噪声）+ _syncingSlider 旗标吞回声；Slider 值不直接喂渲染层（DD-002 铁律从 M1.2 UI 起守住）
- **单测**：Core.Tests 新增 ManualProviderDrivingTests 3 用例（Home/End 边界精确性落在 §6 端点 180°/30°、0.01 步进 101 档值保持=「显示=CurrentState」契约背书、Home×2 同值重发稳定显示），14/14 通过（本地 .NET 8.0.425 + CI core-tests job）；键盘映射/格式化属 UI 层 1~2 行薄逻辑，按「不为测试把 UI 代码写别扭」原则未抽取，行为已由上述契约用例背书
- **⚠ 视觉验收待真机环境（明确标注，不计入已完成）**：开发机无 .NET SDK（仅 .NET 7 runtime），本地构建不了 App、跑不了 UI——Slider 拖动连续变化 / Home、End 免焦点响应 / 数值实时刷新三项目测验收挂起，待真机执行 scripts/setup-windows.ps1（装 .NET 8 SDK + VS 2022 Build Tools，估 30~60 分钟）后进行；代码层已由 CI build-winui 构建验证 + App 可启动性由 overlay-smoke job 守护
- **新决策 DD-036**（按变更模板登记，Accepted）：M1.2 UI 接线六件套——驱动链不变式（两输入汇于 SetProgress，Slider 不直喂渲染层）、Slider 参数（0.01 步进/初值 0）、键盘方案（根 KeyboardAccelerator + 全局 scope + DriveProgress 保同值重发）、单一事实源（拉取式 CurrentState）、防回环守卫（epsilon + 旗标）、显示格式与依赖方向（App→Core 单向）
- **产物**：`src/DuoFlow.App/DuoFlow.App.csproj`、`MainWindow.xaml`、`MainWindow.xaml.cs`（M1.2 驱动链 + 防回环 + 生命周期清理）、`src/DuoFlow.Core.Tests/ManualProviderTests.cs`（+3 用例）、`docs/DESIGN_DECISIONS.md`（DD-036）、本文件三件套
- **遗留 / 阻塞**：M1.2 视觉验收待真机（非代码阻塞，CI 已绿）；下一步 M1.3 Animation Engine（纯 C#，云端可完成）

### 2026-09-16 · Phase 2 / M1.1 LidState · Super Z (main agent)

- **完成**：新建 `src/DuoFlow.Core`（纯 C#，net8.0，零 WinUI/D3D/Windows App SDK 依赖）——`LidState`（sealed record，签名逐字 PROJECT_SPEC §5）、`LidStateSource`（枚举成员与顺序同规格）、`ILidStateProvider`（签名逐字 §18）、`ManualProvider`（UI 直接驱动：SetProgress clamp 0~1 → 同步 StateChanged，Source=Manual / Confidence=1.0 / Velocity=0，Angle 用 §6 示意映射占位）。`src/DuoFlow.Core.Tests`（xunit）11 用例：record 值相等、枚举顺序锁定、clamp、NaN 抛异常、同值重发、多订阅者、退订、Start 基线事件、Angle 单调性等，本地与 CI 全绿
- **放置理由**：Core 独立于 App——DD-002 铁律（渲染只消费 Progress）从 M1.1 起守住，Core 不含任何渲染概念；纯 net8.0 让单测在任何 OS/CI 都能跑（dotnet CLI 即可，只有 WinUI App 需要 VS MSBuild）；M1.3/M4 直接复用
- **新决策 DD-035**：ManualProvider 驱动语义（SetProgress 唯一 UI 入口、同步触发、Stop 不拦截）+ 手动路径取值（Confidence=1.0 引 HC §4.3、Velocity=0 留给 M1.3、Angle=§6 示意映射占位非硬件声明）+ 线程约束（M1.1 不加锁，M4 统一接管）
- **CI**：workflow 新增 `core-tests` job（setup-dotnet 固定 8.0.x → build → test），显示名改 "DuoFlow Windows CI"；既有 build-winui / hardware-probe 两 job 未动（M0 产物零改动）
- **产物**：`src/DuoFlow.Core`（5 文件）、`src/DuoFlow.Core.Tests`（2 文件）、`.github/workflows/m0-windows-build.yml`、`docs/DESIGN_DECISIONS.md`（DD-035）、本文件三件套
- **遗留 / 阻塞**：无；下一步 M1.2 Manual Progress（Slider 接 ManualProvider.SetProgress，App 需引用 Core）

### 2026-09-16 · Phase 1 / M0.4 验收收尾（真机验收 + vendorAcpiDevices 正则勘误） · Super Z (main agent)

- **真机验收（修复版探针 aed21a4+cc3b72f 完整跑一遍，2026-09-16）**：LENOVO Legion R7000 APH9（83EG）· Win11 家庭版 build 26200 · Windows PowerShell 5.1（Desktop）——**errors 0**（修复前 5 条）；6 个传感器全部输出 "API available; no default sensor is present"（不再报 GetDefaultAsync 方法不存在）；`acpi.lidDevice[]`（"ACPI 盖子" / ACPI\PNP0C0D\2&DABA3FF&1 / OK）与 `camera[]`（Integrated Camera / USB\VID_5986&PID_118A&MI_00\7&14EC10AA&1&0000 / OK）字段完整；sensorClassDeviceCount=0 · acpiDeviceCount=50 · 45 个 LENOVO_* 类 · RTX 4060 Laptop + Radeon 780M · 1920x1080@144Hz，与 §5/§5.1 回填值一致——**上一轮两处修复确认有效**。产物在 D:\DuoFlow\probe-verified\（仓库外，未污染）
- **验收顺带发现并修正**：`vendorAcpiDevices` 正则第二分支漏 AMDI——AMD 平台设备 InstanceId 是 `ACPI\AMDIxxxx` **裸前缀**（无 VEN_），原两个分支都匹配不上，仓库脚本跑出的 JSON 里该数组恒为空，与 §5.1 C 表已回填的"4 个 AMD 设备"（数据本身真实，来自真机补测）矛盾。修复：第二分支 `(LEN|LNO|ATK|HPQ|ASUS)` → `(LEN|LNO|AMDI|ATK|HPQ|ASUS)` + 脚本内注释防回退；§5.1 C 表同步勘误——明确这 4 个是 **AMD 平台基础控制器**（GPIO/PEP/I2C/PPM），不是 Lenovo/ATK 类厂商事件设备，不携带开合信号，**防止 M4 误把 AMD GPIO 当盖事件源**
- **顺带加固**：`meta.computer` 在 `$env:COMPUTERNAME` 为空时兜底 `[Environment]::MachineName`（非交互宿主下该环境变量可能为空 → JSON 变 null；本轮真机验收实测出现过）
- **验证**：正则新旧对照测试 18 用例全过（真机 4 个 AMD id 新正则 4/4 匹配、旧正则 0/4，盖设备/睡眠按钮/USB 设备无假阳性）；CI hardware-probe job 全绿（PS 5.1 语法门禁 + 真实执行 + JSON 校验）
- **遗留 / 阻塞**：无——M0.4 关闭结论不变，下一步 M1.1 LidState（云端可做）

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
