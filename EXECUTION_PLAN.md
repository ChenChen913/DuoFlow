# DuoFlow 执行文档（EXECUTION PLAN）

> **本文档是 DuoFlow 执行进度的唯一权威来源（Single Source of Truth）。**
> 任何 AI Agent（Claude Code / Codex / 其他）接手本项目时，**第一件事就是通读本文档**，
> 然后按 §1 交接协议继续工作；每完成一个小任务必须回写本文档（打勾 + 更新快照 + 追加日志）。
> 交接目标是：新 Agent 不依赖任何历史对话，只看仓库内文档即可无缝继续。

| 文档信息 | 值 |
| --- | --- |
| 文档版本 | v1.1 |
| 创建日期 | 2026-09-15 |
| 最后更新 | 2026-10-22 |
| 当前阶段 | Phase 3 — M2 Visual Refinement（M0 / M1 已完成，见 §2 / §3） |
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
| 当前 Phase | **Phase 3 — M2 Visual Refinement**（M1 ✅；执行顺序 M2.0→M2.5→M2.4→M2.1→M2.2→M2.6→M2.3） |
| 当前小任务 | **M2.3 Visual Tuning**（客观部分 ✅ 2026-10-23；主观确认待用户 —— M2.0 ✅、M2.5 ✅、M2.4 ✅、M2.1 ✅、M2.2 ✅、M2.6 ✅） |
| 阻塞项 | M2.3 主观项需用户真机看一眼（六种操作）；另有「满折覆盖度」一个审美选择待拍板（DD-052 §3），不阻塞其余工作 |
| 最后更新 | 2026-10-23 · M2.3 客观部分：五项检查可测代理 + 端点加严 + loop 端点停留 + 覆盖度指标；单测 138/138、demo 22/22、selftest 9/9 · 八弟 |

---

## §3 执行总览（Phase × Milestone 对照）

| Phase | 对应 Milestone | 内容 | 状态 |
| --- | --- | --- | --- |
| Phase 0 | — | 立项 · 文档基线 · 仓库基建（README / Topics / 执行文档） | ✅ 已完成 |
| Phase 1 | M0 | Research & Feasibility：开发环境 / 屏幕捕获 / Overlay / 硬件调研 | ✅ 已完成（M0.1-M0.3 云端；M0.4 真机实测 2026-09-16 回填） |
| Phase 2 | M1 | Rendering MVP：LidState / Manual Progress / Warp / Mask / Blur / Dimming | ✅ 已完成（M1.1-M1.8；Mask/Blur/Dim 三件套随后被 DD-046 物理景深模型取代） |
| Phase 3 | M2 | Visual Refinement：Gradient / Light Sweep / Demo Mode / 参数面板 | 🔄 进行中（M2.0 ✅ · M2.5 ✅ · M2.4 ✅ · M2.1 ✅ · M2.2 ✅ · M2.6 ✅ · M2.3 客观 ✅ / 主观待确认） |
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

#### M1.5 Hinge Mask ✅（2026-09-19 完成：复用 warp 铰链系 + Debug 热力图可反解验收）
> ⚠️ **已被 DD-046 取代（2026-09-19）**：`DuoFlow.Render/HingeMask.cs` 与其测试已删除，
> 遮罩职责并入物理景深模型。本节保留作历史记录。

* [x] 创建 Hinge Mask（Hinge Position / Hinge Width / Falloff 可调——`DuoFlow.Render/HingeMask`：`m(y) = (1 − smoothstep(|y−center|/width))^exponent`，center=0 与 warp 折叠轴重合、width=0.35、exponent=2；**exponent 限 [1,8]**：<1 时收敛端斜率无界违反"过渡必须平滑"，校验层挡下）
* [x] 可视化 Debug Mask（DuoWarp.hlsl 加 MaskParams 常量 + debug 热力分支：`rgb=(m,0.2,1−m)` 红=铰链蓝=远端，alpha 规则与图像路径一致；**R/A、B/A 通道可精确反解遮罩值**——可视化同时是量化验收的编码载体）

**验收（数学层）**：HingeMaskTests 18 新用例（峰值/宽度外清零/单调/对称/中点闭式/指数生效/闭式全扫/连续性护栏/非有限 y/三类坏参数/默认值）——**合计 70/70 本地+CI 全绿** ✅。
**验收（渲染层）**：`--warp-selftest` mask 阶段——p=0 全幅剖面 5 采样行 vs 闭式解**误差 ≤0.004**，B 通道交叉验证 ≤0.004；p=0.5 折叠四边形正确裁剪（quadTop 486/484.7）且**剖面与 p=0 逐行一致**（遮罩 progress 无关性实证）；warp 9 档回归全 PASS + 无滞回；PNG 存档（m15-mask-debug-p0/p50.png）目视红蓝过渡带与参数吻合 ✅。
**产物**：`src/DuoFlow.Render/HingeMask.cs`（MaskOptions/HingeMaskProfile/HingeMask.Build）、`src/DuoFlow.Core.Tests/HingeMaskTests.cs`、DuoWarp.hlsl（MaskParams + debug 分支，常量缓冲 96→112 字节）、CaptureRenderer/WarpPipeline/OverlayWindow selftest 接线、DD-040、`_test/m14-selftest.ps1`（mask 分析 + PNG 导出）。
**踩坑记录**：① 连续性护栏测试的固定步长界不能跨指数通用——exp=8 的合法斜率可达 ~0.114/步，精确性交给闭式断言、护栏只防 O(0.3+) 的真断点，两者分工写进测试注释；② PowerShell 字符串内 `$row:` 会被解析为 drive 引用，`${row}` 转义。

#### M1.6 Blur ✅（2026-09-19 完成：单 Pass 13-tap 核 + 边缘过渡宽度法定量验收）
> ⚠️ 本节记录的 13-tap 核与 Hinge Mask 已在同日被 **DD-046（渲染模型 v2）** 取代并删除；
> 保留作历史记录，产物清单里的 `HingeMask.cs` / `DimOptions.cs` **已不存在**。

* [x] 创建 Blur Pass（局部 Blur，由 Hinge Mask 与 Progress 控制——DuoWarp.hlsl 内实现：`radius = mask × progress × maxBlur`，13-tap 六边形核，源空间半径经 SrcYRemap 线性映射；radius→0 恒等收敛=全开零模糊硬保证；`DuoFlow.Render/BlurOptions` 校验 MaxBlurPixels ∈ (0,64]）
* [x] 调整最大 Blur（MaxBlurPixels 默认 24 源像素，M2.5 参数面板暴露；核形状/半径定义全参数化——DD-041）
* [x] 测试性能（13-tap 全屏核 @600×338：平均 47.4 FPS vs 基线 48，损失可忽略；progress=0 时分支裁剪零成本）

**验收（数学层）**：BlurOptionsTests 5 新用例（归一化换算/极小值/非法半径/非法帧高/默认值）——**合计 80/80 本地+CI 全绿** ✅。
**验收（渲染层，边缘过渡宽度法）**：半屏黑白参考窗给出竖直锐边，逐行测 30%-70% 上升宽度——p=0.75 铰链区（mask≈1）**1px→3px = 3× 模糊**（三行一致）；p=0.5 远区（quad 内 mask=0 可见带）**1× 保持锐利**——"不全屏同时糊"由 mask+折叠几何双重保证（p=0.75 时整个可见四边形落在遮罩区内，远区不可见）✅。
**度量下限记录**：p=0.5 铰链核半径 ±1.9 面板像素低于 8-bit 量化精度，该断言为信息项；p=0.75 的 3× 已证明模糊链路（DD-041 §6）。
**产物**：`shaders/DuoWarp.hlsl`（BlurParams，cbuffer 128 字节）、`src/DuoFlow.Render/BlurOptions.cs`、`src/DuoFlow.Core.Tests/BlurOptionsTests.cs`、CaptureRenderer/WarpPipeline 接线、DD-041、`_test/m16-blur-verify.ps1` + `m16-analyze.ps1`（仓库外验收基建）。
**附带**：控制台关闭 → 全应用退出（`Environment.Exit(0)`；`Application.Exit()` 只停 DispatcherQueue 不杀进程——WinUI 3 非打包已知行为，trace+进程双重验证），解决"关控制台后覆盖层+黄框无路可关"的真实用户问题。

#### M1.7 Dimming ✅（2026-09-19 完成：公式逐字落地 + 白窗亮度剖面误差 ≤1.4）
> ⚠️ **已被 DD-046 取代（2026-09-19）**：`DimOptions.cs` 与独立 Dim Pass 已删除，
> 暗化改由模糊半径派生的光衰减承担。本节保留作历史记录（度量教训仍有效）。

* [x] 创建 Dimming Pass（由 Hinge Mask 与 Progress 控制，调整最大暗化程度——shader 一行乘法：`rgb *= 1 − mask × progress × maxDarkness`，mask/progress 复用 BlurParams 现值，四 Pass 共享同一常量流；`DuoFlow.Render/DimOptions` 校验 MaxDarkness ∈ [0,1]，默认 0.8 = PROJECT_SPEC §13 示例值）

**验收（数学层）**：DimOptionsTests 6 新用例（默认值/边界 0 与 1/负值/超界/NaN/∞）——**合计 86/86 本地+CI 全绿** ✅。
**验收（渲染层，白窗亮度剖面）**：全屏纯白参考窗（均匀色下模糊 no-op，测得变化纯属暗化）——p=0 全 255 ✅；p=0.75 铰链行 104/110/123 vs 预测 104.3/110.7/124.4（**误差 ≤1.4**）✅；p=0.5 铰链行误差 ≤0.8 ✅；p=0.5 远区（mask=0）255 不动 ✅；FPS 47.4 ✅。
**度量教训（DD-042）**：面板自身内容存在于捕获中，blur tap 会把递归暗内容混进采样——分析脚本扫描列必须避开面板在源画面的投影区（dest x ≤ 380），修正后误差从 21 降至 ≤1.4。
**产物**：`shaders/DuoWarp.hlsl`（DimParams，cbuffer 144 字节）、`src/DuoFlow.Render/DimOptions.cs`、`src/DuoFlow.Core.Tests/DimOptionsTests.cs`、CaptureRenderer/WarpPipeline 接线、DD-042、`_test/m17-dim-verify.ps1`/`m17-analyze.ps1`。

#### M1.8 MVP Composite ✅（2026-09-19 完成：全屏合成实时运行 + 捕获/渲染解耦架构）
> ⚠️ 本节描述的"四 Pass 合成 + 全局不透明度 0.65"是 **DD-044 时期**的形态，
> 随后被 DD-045（源冻结，不透明度改 1.0）与 DD-046（单 Pass 物理景深）取代。
> 仍然有效的是架构部分：**捕获/渲染解耦 + 30ms 渲染心跳**。

* [x] Warp + Hinge Mask + Blur + Dimming 组合实时运行——SwapChainPanel 全屏化（面板像素=窗口客户区 1914×1074），四 Pass 在同一 shader/常量流下全屏实时合成；**捕获/渲染解耦**（DD-044，TECHNICAL_PROPOSAL §22 落地）：WGC 回调仅刷新常驻最新帧纹理，渲染心跳（30ms，UI 线程）独立执行 Tick→合成→Present——修复全屏下"静态屏幕无捕获帧→渲染停摆 1 FPS"的问题；**反馈稳定化**：全局不透明度 0.65 使全屏自反馈收敛为磨砂玻璃稳态（α=1 时无界复合真机实证数秒融化）；**全局淡入**：progress<0.15 合成透明=静止态隐形（无鬼影）

**M1 总验收 ✅**：Manual Progress → Warp → Blur → Dimming 全链路实时运行——真机全屏验证：折叠/虚化/暗化随滑块实时联动；三件套复验 P0-1 静止态透明 delta=0、P0-2 点击 0.4/拖动 0.29 全过。
**验收（selftest @1914×1074）**：quadTop p=0.25/0.5/0.75 = 215/483/774 vs 理论 213.5/482/773.9（**≤1.5px**）双向无滞回；mask 剖面（p=0.25/0.5）误差达标；p=0 淡入隐形、p=1 几何坍缩均为预期端点 ✅。
**验收（模糊/暗化 @全屏）**：铰链区边缘过渡 1px→7px（**7× 模糊**，1:1 无降采样）；暗化铰链行与公式误差 ≤1.4；FPS 32.2（心跳设计值）✅。
**踩坑记录**：① 全屏自反馈：捕获包含自身输出，α=1 时 fold^n(D) 无界复合数秒融化——全局不透明度 0.65 使反馈收敛（半透明=稳定机制也是美学，DD-044）；② 静态屏幕无 WGC 帧 → 回调驱动渲染全屏停摆 1 FPS——心跳架构修复；③ 三件套的透明度测试必须先归零滑块（套件自己驱动过滑块，M1.8 后折叠效果会真实改变屏幕——这是产品正确而非回归）。



### Phase 3 — M2 Visual Refinement

> **执行顺序（2026-10-22 定）**：M2.0 → M2.5 → M2.4 → M2.1 → M2.2 → M2.3。
> 理由：M2.5（参数面板）与 M2.4（Demo Mode）是 M2.3 调参的工具——没有它们，每改一个默认值
> 都要"改代码 → 构建 → 手动启动 → 手拖滑块"，而调参本身是 M2 的主工作量。
> 先做工具，再上效果，最后用工具调优。**编号保持原定义不变，只调整执行顺序。**

#### M2.0 验收基线补齐（新增）✅ 2026-10-22

* [x] `warp-selftest` 增加两项断言（复用现有 dump，不改 App）：
  * **光衰减方向**：沿中心列采样亮度，铰链侧必须显著亮于远端，且单调不增（容差见实现）
  * **模糊方向**：用色带边界的高频能量（相邻像素梯度）判定——铰链区必须显著锐于远端
* [x] `p0-realmachine-verify.ps1`（透明 / 穿透 / 真实拖动三件套）入库并参数化路径
* **验收**：两个脚本都能一键跑、退出码可判；`warp-selftest` 在模型 v2 下全绿

**实施记录（含三处必须记住的坑）**

1. **参考窗从"10 色带"改成"白底 + 黑刻线"**（8 px 厚 / 80 px 间距）。色带亮度 29~255、
   9 倍摆幅，而光衰减信号只有 ~4 倍——彩色参考窗**根本测不出衰减方向**。白底让
   `亮度(y) == 255 × light(y)` 直接可读；刻线当高频探针测模糊方向。一次扫描服务两类断言。
   色带分类逻辑随之删除（DD-046 之后远端本就分类失效，是死代码）。
2. **断言必须按模糊包络门控**。`BlurOptions.StartProgress = 0.30`，p=0.25 处模型**故意**
   没有模糊也没有衰减——第一版在这里判"方向反了"，是假阳性。现在 p ≤ 0.30 断言的是
   **反面**（必须平且锐），p > 0.30 才断言方向。这反而多抓一类回归：包络提前打开。
3. **刻线经模糊后变成周期正弦，会污染单调性判定**。第一版 20 分箱 + 中位数，箱高 ≈ 半个
   波纹周期，中位数随相位摆动，每个档位都冒出 1 次 9~11 单位的假"跌落"。修法是按局部
   雅可比解析算出刻线在目标图的**周期**（`Get-RipplePeriod`），用等宽盒滤波整周期抹掉，
   再分 10 箱取均值。修完 `drops=0`。
4. **盒滤波窗口不能截在分箱边界**，否则首尾几行是部分平均，远端偏亮、铰链端偏暗，
   表现为 `depth` 系统性少报 ~0.09。

**真机实测（1914×1074，2026-10-22）**

| p | quadTop 实测/预测 | hingeL | farL | depth | 模糊：铰链/远端单行跳变 |
|---|---|---|---|---|---|
| 0.25 | 215 / 213.5 | 229.3 | 229.4 | -0.000（包络外，应平） | 193 / 255（都锐，应然） |
| 0.50 | 483 / 482 | 227.7 | 148.4 | 0.348 | 252 / 3.4（**74×**） |
| 0.75 | 774 / 773.9 | 225.9 | 34.7 | 0.846 | 255 / 2.5（**104×**） |

上行与下行逐档同值（`quadTop` 差 0 px、`depth` 差 0.000）→ 无滞回。`OVERALL: True`、退出码 0。

#### M2.5 Parameter Panel（轻量版）✅ 2026-10-22

* [x] `WarpOptions` / `BlurOptions` 支持运行时替换（CaptureRenderer 持有可变引用）
* [x] 控制台加「参数」面板：折叠角度 (0~90)、相机距离 (1~6)、方向 (away/toward)、铰链边 (bottom/top)、最大模糊 (0~400)、包络起止 (0~1)
* [x] 改值即时生效（同一 UI 线程读写，无锁）；「恢复默认」按钮
* [x] `scripts/run.ps1` 一键构建 + 启动
* **验收**：UIAutomation 改旋钮 → trace 记录新值 → 画面几何随之变化（selftest 复跑 quadTop 随 MaxFold 变）

**实施记录**

- 决策口径见 **DD-048**：面板只写渲染配置，不碰输入链路（DD-002 语义不变）；选项按
  不可变实例**整体替换**，`RenderTick` 在折叠分支开头把两个字段读进局部变量，
  帧内不可能出现"一半新一半旧"；校验在 UI 侧、**整对拒绝并显示原因**
  （渲染异常会发生在 30ms 定时器回调里，表现为"覆盖层忽然不动了"，没有因果链）。
- 可测性设计：`SetOptions` 把**派生几何**写进 trace（`frame@1.0 M00=...`）。
  `M00 = r·cos(MaxFold)` 是唯一能证明"旋钮走到了渲染数学"而不是只改了 UI 的数字。
- 控制台宽度**刻意保持 580**：`overlay-p0-verify.ps1` 把白色参考窗放在屏宽 35%（1920 上 x=672），
  控制台是 TOPMOST——加宽会静默遮住参考窗、让透明测试失效。高度改为按工作区自适应（上限 960）。
- `ProgressSlider` 补 `AutomationId`：面板加了 6 个 Slider 之后"子树里第一个 Slider"
  不再可靠，`overlay-p0-verify.ps1` 已改为按 `AutomationId` 查找（保留旧查找作为回退）。

**验收证据**：新增 `scripts/acceptance/tuning-panel-verify.ps1`（9 步，UIAutomation 驱动 + 读 trace），
真机 **9/9 PASS、退出码 0**：

| 步骤 | 期望 | 实测 |
|---|---|---|
| 面板初始推送 | maxFold=90 r=2.50 away bottom M00=0.0000 | 一致 |
| MaxFold 90→60 | M00=1.2500 | `M00=1.2500` |
| r 2.5→4.0 | M00=2.0000 | `M00=2.0000` |
| 方向 away→toward | M21=+0.8660（符号翻转） | `M21=0.8660` |
| 铰链 底→顶 | hingeTop=True | 一致 |
| maxBlur 160→80 | blur max=80.0 | 一致 |
| 包络起点 0.80（满值仍 0.60） | 自动抬到 full=0.81 | `start=0.80 full=0.81` |
| 恢复默认 | 全部回默认 | warp + blur 两项均一致 |

同轮 smoke 探针：`WarpState=active`、`Capture=running`、`RenderFrameMs=0.11`、`QualityScale=1`、
七项窗口属性全 True、`HitTest.Pass=True`、`Warnings=[]`。

#### M2.4 Demo Mode ✅ 2026-10-22

* [x] `DemoPlayer`：30ms 步进驱动 `ManualProvider.SetProgress`（走既有链路，DD-002 合规）
* [x] 按钮：Play Opening / Play Closing / Pause / Loop；速度滑块（时长 0.5~4s）
* **验收**：点 Play → 完整走完 0→1 或 1→0 → 端点自动停；Loop 连续往返不跳变

**实施记录**

- `DemoPlayer` 只持有 `Action<double> setProgress`，接的就是 `ManualProvider.SetProgress`——
  演示无法绕过输入链路，引擎仍做平滑（DD-038）。它不引用渲染层。
- 计时用 `Stopwatch` 而不是 tick 计数：`DispatcherQueueTimer` 在负载下会漂移，
  演示"说 1.5 秒实际 2 秒"会让 M2.3 的快/慢检查失去意义。
- 从当前值续播时，**剩余时间按剩余距离缩放**，保证速度恒定（暂停后接着播，
  最后三分之一不会突然变慢）。
- Loop 到端点时交换起止并**保留溢出时间**（`_elapsed -= passSeconds`），
  所以端点点到一次、不重复、转向处不顿。
- 用户拖 Progress 滑块会**自动暂停演示**（否则鼠标和演示会互相打架）：
  判断依据是既有的 `_syncingSlider` 守卫——演示自己的回写被守卫吞掉，真拖动才落到暂停那行。

**验收证据**：新增 `scripts/acceptance/demo-mode-verify.ps1`（UIAutomation 驱动 + 每 120ms 采一次
Progress 滑块实时值），真机 **9/9 PASS、退出码 0**（单程 0.6s）：

| 检查 | 结果 |
|---|---|
| 开盖到达 1.0 | `0 0.23 0.49 0.72 0.93 1` |
| 开盖单调 | 无回退 |
| 开盖端点自动停 | 0.9s 后连续 5 次采样仍为 `1` |
| 合盖到达 0.0 / 单调 / 自动停 | `1 0.79 0.53 0.32 0.11 0`，之后 `0` |
| 循环两端都到 | `max=1 min=0` |
| 循环双向反向 | `rose=True fell=True`，样本 `0→1→0.07→0.28→1→0.01→…` |
| 暂停冻结 | `0.6075 → 0.6075` |

#### M2.1 Gradient Pass ✅ 2026-10-22

* [x] `GradientOptions`（Enabled / 起止色相 / 最大强度，强度上限 ≤0.2，DD-046 §迁移：作用于模型输出色）
* [x] shader 在 `light` 之后叠加：屏幕混合 `rgb += tint * strength * intensity * (1-rgb)`，防过曝
* [x] 面板加 Gradient 旋钮
* **验收**：单测（参数校验 / 强度上限 / p=0 恒等）；真机 smoke 仍 active；关闭时与 v2 逐像素无差

**实现记录**

1. **`DuoFlow.Render/GradientOptions.cs`（新）**：`Enabled` 默认 **false**、`Strength` 默认 0、
   色相 205°→275°（同一蓝紫家族；跨 180° 会读成彩虹而不是玻璃）。
   `EffectiveStrength` 关闭时返回**精确 0.0**——这是"关闭 = no-op"的机制，不是巧合。
   `HueToRgb` 固定 `S=0.65 / L=0.55`：只暴露色相，不暴露明度，否则调用方能重新引入过曝。
2. **`shaders/DuoWarp.hlsl`**：cbuffer 加 3 个 `float4`（8→11 个，176 字节）；
   叠加权重乘 **`(1 - light)`** 而不是平铺加色（理由见 **DD-049**：平铺加色会把远端
   亮度从 34.7 抬到 ~80，DD-046 的阴影白建）；再乘 `PhysicsParams.y`，因此渐变**跟模糊包络同源**，
   `p ≤ 0.30` 时自动消失。
3. **`WarpPipeline.Render(..., gradient)`**：可选参数，默认 `null` → 强度 0。
4. **`CaptureRenderer.SetOptions(warp, blur, gradient)`**：三个选项**一起**校验、一起替换，
   帧内读局部变量（DD-048 不变）。
5. **面板**：`GradientCheck` / `HueStartSlider` / `HueEndSlider` / `GradientStrengthSlider`，
   `OnResetTuning` 一并复位（从编译期默认值回种，不复制字面量）。
6. **单测 16 条**（`GradientOptionsTests.cs`）：默认关闭、关闭时 `EffectiveStrength == 0.0`（精确相等，
   不是近似）、0.2 上限钳制、`Validate` 拒绝 NaN/Inf/越界、`HueToRgb` 三原色与绕回、色值落在单位立方体。

**真机证据（2026-10-22）**

"关闭 = 逐像素无差"用三轮 A/B 证明（着色器是启动时从磁盘读的，可在**同一二进制**上只换文件）：

| 比对 | 变量 | 逐位相同的档位 | 最大差 | 差异像素 |
|---|---|---|---|---|
| A1 vs A2 | 同 shader，两轮 | 3/9 | 23 | 35~115 |
| A1 vs B | 旧 shader vs 新 shader | 3/9 | **23** | 92~337 |

两组的最大差**同为 23**、差异都落在鼠标指针与刻度线双线性重采样的边缘；
且量化指标四轮**逐位相同**（`p=0.75: hinge=225.85 far=34.69 depth=0.8464`，`quadTop=774/773.9`）。
结论：新增的 cbuffer 与分支在关闭态不产生可测差异，差异量与"同一份代码跑两遍"的噪声同级。

反向对照（临时把面板默认改成开 + 0.15，测完即还原）：

| p=0.75 远端 | R | G | B |
|---|---|---|---|
| 渐变关 | 24.30 | 24.30 | 24.30 |
| 渐变开 0.15 | 41.29 | 33.80 | 50.38 |

同档铰链侧 `227.23 → 227.23`（+0.02）——**只吃阴影侧**的设计约束被实测到。
`p=0.50` 远端 `148.37 → 150.03`（增量随遮光缺口缩小），符合 `(1-light)` 的预期。

**验收结果**：单测 79/79（含新增 16 条）；构建 0 warning 0 error；
`warp-selftest` 9/9 通过（exit 0）；`tuning-panel-verify` **14/14 通过**（新增 4 步渐变断言，exit 0）。

#### M2.2 Light Sweep ✅ 2026-10-22

* [x] `LightSweepOptions`（Enabled / 位置 / 宽度 / 强度 / 方向），默认强度 0.08（spec 建议 0.05~0.15）
* [x] shader：沿铰链法线方向的窄高斯亮带，位置随 Progress 从铰链扫向远端
* [x] 面板加 Light Sweep 旋钮
* **验收**：单测（宽度/强度边界、p=0 恒等）；关闭时无差；开启时峰值增量 ≤ 强度上限

**实现记录**

1. **`DuoFlow.Render/LightSweepOptions.cs`（新）**：`Enabled` 默认 false，`Strength` 0.08，
   `Width` 0.10（**半高全宽 FWHM**，不是 sigma——调参的人想的是"多宽"），`Position` 0，
   `Direction` HingeToFar。`CenterAt(progress)` 是纯函数：`saturate(p + Position)`，
   FarToHinge 取 `1 - 上式`。`Evaluate(sy, p)` 是纯高斯，因此"FWHM 真的是 FWHM"
   可以直接断言（中心两侧 `Width/2` 处必须正好是 0.5）。
2. **`shaders/DuoWarp.hlsl`**：cbuffer 11 → 12 个 `float4`（192 字节），加 `SweepParams`
   （中心 / `1/(2σ²)` / 强度 / **强度上限**）。保护用**剩余空间归一化**而不是乘法保护
   （理由与量级见 **DD-050**：照搬渐变的 `(1-light)` 会让折叠前半段只有 2/255，等于没有）。
   乘 `PhysicsParams.y`，与模糊包络同源。
3. **`WarpPipeline.Render(..., progress, gradient, sweep)`**：新增 `progress` 参数——
   `CenterAt` 需要原始 progress，从 `M00/M11` 反解 φ 也能得到，但换了 `MaxFoldDegrees`
   就会悄悄算错。
4. **`CaptureRenderer.SetOptions(warp, blur, gradient, sweep)`**：四个选项一起校验、一起替换；
   trace 里多一行 `tuning: sweep ... centre@p0.5=...`（派生量，不是输入量）。
5. **面板**：`SweepCheck` / `SweepPositionSlider` / `SweepWidthSlider` / `SweepStrengthSlider` /
   `SweepDirectionCombo`，`OnResetTuning` 一并复位。
6. **`--light-sweep` 启动参数**：把面板初始状态种成"开"，供验收脚本测量；
   面板仍是权威（DD-048）。
7. **单测 17 条**（`LightSweepOptionsTests.cs`）：默认关闭、关闭时精确 0.0、0.2 上限钳制、
   `CenterAt` 单调/夹紧/NaN 降级/双向、`Width` 真的是 FWHM（±Width/2 处 = 0.5）、
   窄带宽度的峰值更高、**p=0 交付强度精确为 0**（与 `BlurOptions.IntensityAt(0)` 的
   跨模块恒等式）、`Validate` 四类拒绝、宽度极值下 σ 与指数因子有限。

**真机证据（2026-10-22）**

`scripts/acceptance/light-sweep-verify.ps1` 三轮（off1 / off2 / on，同一二进制）：

| p | 噪声底 | 峰值增量 | 实测行 | 预测行 | 行误差 |
|---|---|---|---|---|---|
| 0.25 | 0.000 | 0.00（包络未开，**无光带** ✓） | — | — | — |
| 0.50 | 0.000 | 13.39 | 746 | 741.3 | 4.7（容差 44）|
| 0.75 | 0.010 | 20.60 | 833 | 832.6 | 0.4（容差 19）|

峰值上限 = 0.08 × 255 = 20.4，实测 20.60（含 0.09 的测量噪声）。

**关闭态 no-op**（同一二进制、只换 `shaders/DuoWarp.hlsl`）：

| 比对 | 变量 | 行剖面最大差（0–255） | 逐位相同档位 |
|---|---|---|---|
| off1 vs off2 | 同一份 shader，两轮 | 0.0080 | 3/9 |
| off1 vs m21 | pre-M2.2 shader vs M2.2，光带关闭 | 0.0087 | 3/9 |
| off1 vs on | 光带开启 | 20.60 | — |

即"新旧 shader 关闭态差异"与"同一份代码跑两遍"同级，真实信号是噪声的 **2500 倍**。

**验收结果**：单测 96/96（含新增 17 条）；构建 0 warning 0 error；
`light-sweep-verify` **14/14**（exit 0）；`tuning-panel-verify` **20/20**（新增 6 步，exit 0）；
`warp-selftest` 9/9（关闭态，exit 0）。

#### M2.6 运行期自愈与参数持久化（补做，DD-051）✅ 2026-10-22

* [x] 重建失败不再是一次性尝试：`_rebuildPending` 保留到成功 + 指数退避（≤3.6 s）+ 失败次数进 `WarpState`
* [x] 设备丢失可识别可恢复：`DeviceHealth` 分类 HRESULT，`CaptureRenderer` 停止并报告，`OverlayWindow` 重启捕获链一次
* [x] 帧级故障就地隔离：`RenderTick` 内部 try/catch，异常不再变成 30 Hz 未处理异常流
* [x] 空闲跳过渲染：特效不可见且已 present 透明帧时不再 33 FPS 空转（保留 DD-045 冻结记账与 dump 请求通道）
* [x] 参数持久化：`TuningPreset`（纯 C#）+ `%LOCALAPPDATA%\DuoFlow\tuning.json`，防抖写、关闭 flush、验收脚本 hermetic
* [x] 单测 +38（`TuningPresetTests` / `DeviceHealthTests`），合计 134/134
* [x] 顺带：blit 回退改 `CopySubresourceRegion`、不捕获光标、渐变默认强度 0.08、trace 4 MB 上限、面板错误去重
* **验收**：构建 0 warning；真机启动无 crash 且预设落盘；静止 14 s 帧计数停在 1；
  `--warp-selftest` 九帧几何误差 ≤0.5 px、无滞回、p=0 alpha 全 0；smoke JSON 通过 CI 全部断言

#### M2.3 Visual Tuning 🔄 客观部分 ✅ 2026-10-23 · 主观确认待用户

* [x] 六种操作 × 五项检查（慢/快/慢开/快开/中途停/中途反向 × 跳变/闪烁/过糊/过暗/过彩）
  —— 五项各配可测代理，`demo-mode-verify` 覆盖六种操作（DD-052）
* [x] 客观项：引擎单测覆盖中途停与反向的连续性；selftest 覆盖端点
* [ ] 主观项：用户真机确认，默认值固化回代码
* **验收**：六种操作全过（客观部分 ✅ 22/22）；默认值写进 `WarpOptions` / `BlurOptions` / `GradientOptions` / `LightSweepOptions`

**实现记录**

1. **五项检查的可测代理**（DD-052）：跳变=progress **速率**（不是相邻采样差——
   UIA 读一次 RangeValue 要 10~40 ms，固定阈值两头都不准）；闪烁=单调段回退 +
   静止段离散度 ≤0.02；过糊=p≤0.55 时铰链行间梯度 ≥150；过暗=远端亮度 ≥8/255；
   过彩=渐变**关闭**时全帧通道差 ≤2.0，且判据从 trace 的 `gradient enabled=`
   读出来（渐变开着时这项主动弃权，而不是判过）。
2. **端点断言加严**：p=0 从"中心列无不透明行"改成"全帧 alpha 逐像素为 0"——
   中心列透明、角落漏一块同样是失败。
3. **`DemoPlayer.DwellSeconds = 0.35`**：loop 折返前把目标钉在端点停留。
   引擎是 τ=0.10 的指数跟随，0.6 s 一趟在折返点落后 0.06~0.17；旧行为对目标值
   平滑，但屏幕上永远到不了 1.0/0.0。实测峰值 0.943 → **1.000**。
4. **满折覆盖度进指标**：`FarEdgeY = M00/(M11+|M21|) = r·cosφ/(r+sinφ)`，
   MaxFold=90° 时恒为 0；面板 `TuningStatus` 实时显示「满折覆盖 X%」。

**真机证据（2026-10-23）**

| 操作 | 实测 | 判定 |
|---|---|---|
| 快开 0→1（0.6 s） | `0 0.25 0.48 0.69 0.95 1 1 …`，峰值 1，速率 1.79/s | ✅ |
| 快合 1→0 | `1 0.76 0.55 0.29 0.03 0 …`，谷值 0，速率 1.95/s | ✅ |
| 慢开（3 s） | 25 点单调，速率 0.38/s（期望 0.33） | ✅ |
| 慢合（3 s） | 26 点单调，速率 0.39/s | ✅ |
| 中途停 | 停在 p=0.433，1.4 s 离散度 **0.0000** | ✅ |
| 中途反向 | p≈0.43 反向，方向变化 **1** 次，速率 0.55/s，终值 0.000 | ✅ |
| loop 往返 | 峰值 **1.000** 谷值 **0.000**，上下双向 | ✅ |

`warp-selftest` 9/9，新增指标：

| p | 覆盖度 | 色度 | 铰链行梯度 | 远端亮度 |
|---|---|---|---|---|
| 0.00 | 0%（全帧 alpha 0） | 0.00 | — | — |
| 0.25 | 74.51% | 0.09 | 193（包络未开，应锐） | 229.43 |
| 0.50 | 48.88% | 0.11 | 252 | 148.37 |
| 0.75 | 24.09% | 0.10 | 255 | 34.69 |
| 1.00 | **0%** | 0.00 | — | — |

**留给用户拍板的一件事（不是缺陷）**：`MaxFold=90°` 时 p=1 覆盖度恒为 0，
效果在最后 20% 快速消退。这是 `r·cosφ/(r+sinφ)` 的必然结果，**只有 MaxFold
影响它，r 不影响**（2.5→6.0 全是 0%）。80°→13%、70°→25%、60°→37%。
面板拖 MaxFold 可实时看到「满折覆盖」，属审美决策，见 DD-052 §3。

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

### 2026-10-23 · M2.3 客观部分（DD-052：五项检查的可测代理 + 端点停留 + 覆盖度真相） · 八弟

- **背景**：M2.3 是 M2 唯一需要用户参与的一步（主观项）。但"看不出问题"不等于"没问题"——
  本项目已经栽过两次假阳性（M0.3 Overlay 三件套、M2.2 验收脚本四项）。所以先把五项主观词
  各配一个可测代理，把"人看"的范围压到代理测不到的那部分，再请用户拍板。
- **五项代理**（DD-052）：跳变=progress **速率**（不是相邻采样差——UIA 读一次 RangeValue
  要 10~40 ms，固定阈值慢读误报、快读漏报）；闪烁=单调段回退 + 静止段离散度 ≤0.02；
  过糊=p≤0.55 时铰链行间梯度 ≥150；过暗=远端亮度 ≥8/255；过彩=渐变**关闭**时全帧
  通道差 ≤2.0。过彩的判据从 trace 的 `gradient enabled=` 读，渐变开着时**主动弃权**——
  硬编码"色度必须≈0"会在用户打开渐变后报假失败。
- **端点加严**：p=0 从"中心列无不透明行"改成"全帧 alpha 逐像素为 0"。中心列透明、
  角落漏一块不透明区域，同样是失败，而旧断言看不见这种情况。
- **发现一个真问题（DemoPlayer）**：loop 模式折返时峰值只到 **0.943**，屏幕上永远
  到不了 1.0/0.0。原因不是 bug 而是两个时间常数打架：引擎是 τ=0.10 的指数跟随，
  0.6 s 一趟在折返点落后 0.06~0.17；旧行为把超调带到下一趟（对目标值很平滑），
  代价是端点永远差一点。加 `DwellSeconds = 0.35` 钉住端点再折返 → 峰值 **1.000**。
  这一条直接决定 M2.3 能不能用眼睛判断端点。
- **发现一件需要拍板的事（不是缺陷）**：`FarEdgeY = M00/(M11+|M21|) = r·cosφ/(r+sinφ)`，
  在默认 `MaxFold=90°` 下 **p=1 恒为 0**——玻璃立成边缘朝向相机，一个像素都不画。
  实测覆盖度 74.5 / 48.9 / 24.1 / **0** %（p=.25/.50/.75/1.0）。用户 2026-10-22 说的
  "p=0.8 整个屏幕几乎倒下了"就是这条曲线的后半程。关键：**只有 MaxFold 影响它，
  r 不影响**（2.5→6.0 全是 0%）；80°→13%、70°→25%、60°→37%。
  面板 `TuningStatus` 已实时显示「满折覆盖 X%」，拖 MaxFold 即可看到后果（DD-048）。
- **验收**：单测 **138/138**（+4：中途停/中途反向/慢速跟随/满折覆盖度）；
  `demo-mode-verify` **22/22**（9 → 22，六操作全覆盖）；`warp-selftest` **9/9**
  （新增覆盖度/色度/过暗/过糊）；`tuning-panel-verify` **20/20** 回归；构建 0 warning。
- **遗留**：M2.3 主观项待用户真机确认（六种操作 + 满折覆盖度取舍）；确认后把默认值固化并勾掉。

### 2026-10-22 · M2.6 运行期自愈 + 参数持久化（DD-051：三条"静默死亡"与两个调优阻塞） · 八弟

- **背景**：M2.2 之后对 M0～M2 已完成功能做了一次代码级审查（5405 行全读 + 真机运行）。
  结论：渲染内核与验收方法学是第一梯队，但有**三条会让特效静默消失、不报错、不复原**的路径，
  外加两个直接妨碍 M2.3 调优的工程问题。本轮按"能弄死特效 → 妨碍调优 → 打磨"的顺序全修。
- **P1-1 重建失败 = 永久黑屏**：`_rebuildPending` 原来在重建**之前**就被清掉，一次瞬时失败
  （切分辨率/插拔显示器/DPI 变化）之后每个 tick 都在 `_latestFrame is null` 处静默返回，再也不重试。
  改为保持置位直到重建**成功**，失败按 tick 指数退避（2/4/…/120 tick，≈3.6 s 上限），
  失败次数进 `WarpState`（`rebuild failed (attempt N)`）——控制台能看见。
- **P1-2 `Present` 结果被丢弃 / 无设备丢失恢复**：这台机器是 780M + 4060 混合显卡，
  驱动更新或 GPU 偏好切换都可能让 D3D11 设备被移除，而 `Present` 的 `Result` 被直接丢掉，
  渲染器静默停画、控制台照常显示帧数。新增 `DuoFlow.Render/DeviceHealth`（纯 int，可单测）
  分类 `DEVICE_REMOVED`/`DEVICE_RESET`/`DRIVER_INTERNAL_ERROR`；`CaptureRenderer` 判定后停止并
  置 `ChainBroken`；`OverlayWindow`（设备的所有者）重启整条捕获链**一次**。渲染器不重建自己不拥有的设备。
- **P1-3 渲染心跳没有 try/catch**：App 层"吞 50 次未处理异常"的兜底在 30 Hz 故障源下只有 **1.7 秒**，
  一次坏帧就变成进程死亡。`RenderTick` 内部隔离，异常进 `WarpState`，trace 只在第 1 次与每 120 次写一行。
- **P2-4 空闲仍 33 FPS 满屏渲染**：直接违反 PROJECT_SPEC §2.1。改为 `visible = progress > 0.001`
  与 DD-045 同一阈值；不可见且已 present 过一帧透明帧时跳过 GPU 工作。
  **踩坑**：DD-045 的 `_sourceFrozen = false` 必须放在跳过之前，否则一次跳过会让捕获源永久冻结，
  下一次折叠回放几分钟前的桌面；重建后必须清 `_idlePresented`，否则新交换链永远不 present。
- **P2-5 参数无持久化**：`TuningPreset`（纯 record + System.Text.Json）+ `TuningStore`
  写 `%LOCALAPPDATA%\DuoFlow\tuning.json`。600 ms 防抖、关闭 flush、**只写"被 SetOptions 接受"的那一版**。
  抽出 `OptionSet.ValidateOrThrow/ValidateAll`，让面板推送与文件加载共用同一套校验（原来两边各一份）。
  **验收脚本 hermetic**：`automation = smoke || selfTest || lightSweep` 时既不读也不写——`warp-selftest`
  的预测公式写死 r=2.5/MaxFold=90，一份用户预设会让它静默跑偏。
- **P2-6 勾选渐变什么都不发生**：强度默认 0 而光带默认 0.08。`GradientOptions.DefaultStrength = 0.08`，
  XAML 初值同步；"恢复默认"改走与启动恢复同一条 seed 路径（`ApplyPresetToPanel(TuningPreset.Default)`）。
- **P2-7 blit 回退是颗地雷**：回退路径的 `CopyResource` 在源纹理带 11 级 mip 链时形状不一致，
  而 CI 断言 `WarpState=active` 意味着这条路从未被执行过。改 `CopySubresourceRegion`（level 0）。
- **P2-8 折叠里印着光标**：`IsCursorCaptureEnabled = true` + DD-045 冻结 = 活光标 + 烤进磨砂的第二个光标。改 false。
- **P3 小债**：trace 4 MB 上限；面板被拒后只在错误文本变化时写 trace（原来每秒 4 行）。
- **验收**：构建 0 warning 0 error；**单测 134/134**（+38：预设 21 条含 JSON 往返/未知字段/损坏/越界/枚举名，
  设备分类 3 条）。真机：启动无 crash，trace 走完 `no saved preset → preset saved`，`tuning.json` 内容与默认值逐项一致；
  **静止 14 秒 frames 停在 1**（改动前 ≈30/秒）；`--warp-selftest` 跑完 0→1→0，九帧全部落盘（8,222,544 B/帧）、
  累计 913 帧、无 `DEVICE LOST`/`tick failed`；对 dump 直接复算（不依赖脚本）：
  quadTop 214/482/774 对理论 213.5/482.0/773.9（**≤0.5 px**）、上下行同值（无滞回）、
  p=0.25→0.50→0.75 远端亮度 216→56→3.5（远端沉入阴影）、铰链侧 34→36 不变、p=0 alpha 最大 0；
  `--overlay-smoke` 的 JSON 通过 workflow 全部断言（本地预演 `CI GATE WOULD PASS`）。
- **未验证（诚实标注）**：设备丢失后的捕获链重启路径无法在本机触发（需要真的移除设备），
  只有代码审查与分类单测背书；重建退避同理（需要真的改分辨率触发失败）。
- **产物**：`DuoFlow.Render/TuningPreset.cs`、`DeviceHealth.cs`、`DuoFlow.App/TuningStore.cs`、
  `TuningPresetTests.cs`、`DeviceHealthTests.cs`、`CaptureRenderer`/`OverlayWindow`/`MainWindow`/
  `DesktopCapture`/`Trace`/`App`/`GradientOptions`/`MainWindow.xaml`/`README` 双语、DD-051、本文件三件套。

### 2026-10-22 · M2.4 演示模式（DemoPlayer + 控件 + 验收） · 八弟

- **背景**：M2.3 的"六种操作 × 五项检查"需要反复看完整开合过程。手拖滑块既慢又不匀速，
  判断不了"中途反向有没有跳变"。
- **`DemoPlayer`**：30ms 定时器驱动 `ManualProvider.SetProgress`——**接的是滑块的同一个入口**，
  所以演示无法绕过输入链路（DD-002），引擎仍做平滑（DD-038），它也不引用渲染层。
- **计时用 `Stopwatch` 而非 tick 计数**：`DispatcherQueueTimer` 在负载下会漂移，
  演示"说 1.5 秒实际 2 秒"会让 M2.3 的快/慢检查失去意义。
- **续播保持恒速**：从当前值续播时剩余时间按剩余距离缩放，暂停后接着播最后一段不会变慢。
- **Loop 保留溢出时间**（`_elapsed -= passSeconds`）：端点点到一次、不重复、转向不顿。
- **拖滑块自动暂停演示**：判断依据是既有的 `_syncingSlider` 守卫——演示自己的回写被守卫吞掉，
  真拖动才落到暂停那行。不加这条的话鼠标和演示会互相打架。
- **新增 `scripts/acceptance/demo-mode-verify.ps1`**：UIAutomation 驱动按钮 + 每 120ms 采一次
  Progress 滑块实时值。真机 **9/9 PASS、退出码 0**（单程 0.6s）：
  开盖 `0 0.23 0.49 0.72 0.93 1` 后 0.9s 内 5 次采样仍为 `1`（端点自动停）；
  合盖 `1 0.79 0.53 0.32 0.11 0`；循环 `max=1 min=0` 且 `rose/fell` 都为真
  （样本 `0→1→0.07→0.28→1→0.01`）；暂停 `0.6075 → 0.6075`。
- **回归**：单测 63/63。


### 2026-10-22 · M2.2 光带扫过（剩余空间归一化 + 位置量化验收） · 八弟

- **背景**：要在折叠上叠一条沿铰链法线的窄高斯亮带，随 Progress 从铰链扫向远端。
- **先算量级再动手**：光带中心 = progress，所以折叠前半段它落在**还很亮**的表面上。
  照搬 M2.1 渐变的 `(1 - light)` 保护，p=0.5 处只有 `0.08 × 0.20 × 255 ≈ 2/255`——
  和抖动同级，用户看到的是"前半程没反应，后半程突然出现一条线"。
- **DD-050 定口径**：改用**剩余空间归一化** `headroom = saturate((1 - maxChannel) / 强度上限)`。
  因为 `gain ≤ 上限`，所以 `gain × headroom ≤ 1 − maxChannel`，叠加后**必然不裁切**；
  "峰值增量 ≤ 强度上限"成了这一行的推论而不是另外加的钳制。用最大通道而不是亮度做保护，
  色相不被推偏。实测 p=0.5 从 2/255 提升到 **13.4/255**。
- **上限传给着色器**：`SweepParams.w` 就是 `MaxStrengthLimit`，HLSL 里不重复写 0.2，
  避免与 C# 常量漂移。
- **宽度用 FWHM 而不是 sigma**：调参的人想的是"亮带多宽"。代价是要在单测里真的断言
  `±Width/2` 处权重 = 0.5——这条断言是"这个旋钮有意义"的证明。
- **`CenterAt(progress)` 做成纯函数**：于是"随 Progress 从铰链扫到远端"变成可单测的陈述。
  面板 trace 里额外打 `centre@p0.5`，因为只打输入量的话，"滑条动了但模型没跟上"会看不出来。
- **验收脚本踩的坑（都已修）**：
  1. `Get-DumpSize` 里第二次 `-match` 覆盖了 `$Matches` → 尺寸读成 0×0，全程"无 quad"；
  2. 尺寸读坏之后脚本仍报 `PASS (0/0)` —— 加了"零项检查即失败"的护栏，这正是本项目
     反复防的假阳性；
  3. 方向那步的正则能匹配到**上一行**（trace 尾部还留着），于是拿旧值"通过"；
  4. `($steps | Where-Object ...).Count` 在只匹配一项时取到的是**字典键数 4**，
     把"1 项失败"打印成"4 of 20 失败"。三处脚本都改成 `@(...)`。
- **采样带收窄**：42% 处蹭到屏幕上一个偶发亮斑（21 px），足以把行剖面推动 1.6 单位——
  和要测的信号同量级。光带脚本改用 46% 起，噪声底从 1.57 降到 **0.006**。
- **回归**：单测 96/96（新增 17 条）；构建 0 warning 0 error；
  `light-sweep-verify` 14/14、`tuning-panel-verify` 20/20、`warp-selftest` 9/9，全部 exit 0。


### 2026-10-22 · M2.1 渐变色相（寄生在阴影上 + 三轮 A/B 证明关闭态 no-op） · 八弟

- **背景**：要"折叠像一块有色玻璃"。最直觉的实现是着色器末尾平铺加色，但 DD-046 的 v2
  刚把远端压到 `light≈0.14`（`far=34.7/255`），平铺 0.15 强度会让那半边亮度几乎翻倍——
  **阴影白建**。
- **DD-049 定口径**：叠加权重乘 **遮光缺口 `(1 - light)`**，再乘 `PhysicsParams.y`。
  结果：铰链侧几乎不加色（实测 +0.02），远端加得最多（实测 `24.30→41.29 / 33.80 / 50.38`），
  且渐变**跟模糊包络同源**——`p ≤ 0.30` 时一起消失，不会出现"还没折叠就染色"。
  另有 `(1 - rgb)` 逐通道饱和保护。
- **"关闭 = no-op" 的机制**：`EffectiveStrength` 关闭时返回**精确 0.0**，
  着色器 `if (GradientParams.x > 0.0)` 因此是**跳过分支**而不是"加个零"。
- **怎么证**：着色器是启动时从磁盘读的 → 可在**同一二进制**上只换 `DuoWarp.hlsl`。
  跑三轮：A1（旧 shader）、A2（同旧 shader，给**逐像素噪声底**）、B（新 shader）。
  实测 A1vsA2 与 A1vsB 的**最大差同为 23**、差异像素同量级（35~115 vs 92~337），
  全部落在鼠标指针与刻度线双线性重采样的边缘；量化指标四轮**逐位相同**。
  ⚠️ 只做 A1vsB 单次比对是不够的——没有噪声底就分不清"1 LSB 抖动"和"真的改了一点"。
  方法已写进 `scripts/acceptance/README.md`，以后加 pass 照抄。
- **反向对照**：临时把面板默认改成开 + 0.15 再跑一次，确认渐变真的产生可测变化
  （否则"关闭无差"可能只是"这个 pass 根本不起作用"）；测完立即还原，未提交。
  过程中发现面板在启动时会用自己那套值（未勾选 + 强度 0）**覆盖** C# 默认值——
  这反而验证了 DD-048「面板才是权威」。
- **面板**：`GradientCheck` / 起色相 / 止色相 / 强度（0~0.2），`OnResetTuning` 一并复位。
  `tuning-panel-verify.ps1` 从 9 步扩到 **14 步**，新增断言的重点不是"复选框能点"，
  而是**关闭态把强度滑块推到 0.15 后 trace 仍须报 `strength=0.000`**。
- **回归**：单测 79/79（新增 16 条）；构建 0 warning 0 error；
  `warp-selftest` 9/9、`tuning-panel-verify` 14/14，两者 exit 0。


### 2026-10-22 · M2.5 参数面板（运行时替换 + 面板 + run.ps1） · 八弟

- **背景**：M2.3 视觉调优的主工作量是"改默认值 → 构建 → 启动 → 拖滑块 → 看"。
  `WarpOptions`/`BlurOptions` 是 `init`-only，构造后无法改，控制台也没有入口传进渲染层。
- **DD-048 划边界**：面板只写**渲染配置**，不碰输入链路——`Progress` 仍走
  `ManualProvider → AnimationEngine → WarpGeometry`。选项按不可变实例**整体替换**，
  `RenderTick` 在折叠分支开头把两个字段读进局部变量，帧内不可能出现混合参数组合。
- **校验在 UI 侧、整对拒绝**：非法组合会让 `WarpGeometry.Compute` 在 30ms 定时器回调里抛异常，
  表现为"覆盖层忽然不动了"，没有因果链。`SetOptions` 先用 `Compute(0)`/`Compute(1)` +
  `BlurOptions.Validate()` 验一遍，失败则整对拒绝并返回原因，上一组继续生效，控制台显示原因。
- **可测性**：`SetOptions` 把**派生几何**写进 trace（`frame@1.0 M00=...`）。`M00 = r·cos(MaxFold)`
  是唯一能证明旋钮走到渲染数学而不是只改 UI 的数字。
- **面板**：折叠角度 1~90 / 相机距离 1.1~6 / 方向 away·toward / 铰链边 bottom·top /
  最大模糊 1~400 / 包络起止 0~1 + 恢复默认。控制台高度按工作区自适应（上限 960），
  **宽度刻意保持 580**——`overlay-p0-verify.ps1` 的白色参考窗在屏宽 35%（x=672），
  控制台 TOPMOST，加宽会静默遮住参考窗、让透明测试失效。
- **`ProgressSlider` 补 `AutomationId`**：面板加了 6 个 Slider 后"子树里第一个 Slider"
  不再可靠，`overlay-p0-verify.ps1` 改为按 AutomationId 查找（旧查找保留为回退）。
- **`scripts/run.ps1`**：一键构建 + 启动（`-NoBuild` / `-Smoke` / `-SelfTest` / `-Configuration`）。
- **新增 `scripts/acceptance/tuning-panel-verify.ps1`**：9 步 UIAutomation 驱动 + 读 trace 断言。
  真机 **9/9 PASS、退出码 0**：M00 在 90°→0.0000、60°/r2.5→1.2500、60°/r4.0→2.0000，
  toward 后 M21 翻正为 0.8660，包络起点 0.80 自动把满值抬到 0.81，恢复默认全项复位。
- **踩到的坑**：WinUI ComboBox 的 popup 是**独立的 UIA 子树**，从 ComboBox 元素往下找不到
  ListItem——必须从桌面根节点搜；而且"Select 调用成功"不等于"选择真的变了"，
  脚本现在读 `SelectionPattern` 前后比对，选择没变就直接报错（否则只会看到一个莫名的 timeout）。
- **同轮回归**：单测 63/63；smoke 探针 `WarpState=active`、`RenderFrameMs=0.11`、`QualityScale=1`、
  七项窗口属性全 True、`Warnings=[]`。


### 2026-10-22 · M2.0 验收基线补齐（光影方向 / 模糊方向断言 + P0 脚本入库） · 八弟

- **背景**：M1 收尾时留下的验收盲区——`warp-selftest` 只验**几何**（`quadTop`），DD-046 的
  **光影与模糊方向**没有任何自动断言；而这两件事恰恰是"折叠看起来像不像折叠"的主体。
  同时 P0 三件套（透明/穿透/拖动）脚本还躺在仓库外的 `_test/`，接手者拿不到。
- **参考窗换血**：10 色带 → **白底 + 黑刻线**（8 px / 80 px）。色带亮度 29~255、9 倍摆幅，
  而光衰减信号只有 ~4 倍——**彩色参考窗在数学上就测不出衰减方向**。白底让
  `亮度(y) == 255 × light(y)` 直接可读；刻线当高频探针。色带分类代码（DD-046 后已是死代码）随之删除。
- **三项断言**：① 光衰减方向（铰链端亮于远端）+ 单调性（`drops == 0`）；
  ② 模糊方向（铰链区单行最大亮度跳变 ≥ 4× 远端）；③ 跨档衰减深度递增（`depth(.75) > depth(.50)`）。
  另加两条反向断言：p ≤ 0.30（包络外）必须**平且锐**、任何档位刻线都必须可见（参考窗没画出来就报错）。
- **踩到的三个坑（都已修，写进 README）**：
  1. **p=0.25 判"方向反了"是假阳性**——`BlurOptions.StartProgress=0.30`，包络未开时本就该平。
     断言改为按包络门控，并把"包络提前打开"也变成一条断言。
  2. **刻线经模糊后是周期正弦**，20 分箱 + 中位数的箱高 ≈ 半周期，中位数随相位摆动，
     每档冒出一次 9~11 单位的假跌落。改为按局部雅可比解析算周期（`Get-RipplePeriod`）+
     整周期盒滤波 + 10 箱均值 → `drops=0`。
  3. **盒滤波窗口截在分箱边界**会让首尾成为部分平均，远端偏亮、铰链偏暗，`depth` 系统性少报 ~0.09。
     窗口延伸到整个四边形（只裁分箱范围）后消除。
- **`overlay-p0-verify.ps1` 入库**：透明/穿透/拖动/预览四判据 + 因果 A/B（清 LAYERED / LWA_ALPHA=0 / 恢复），
  参数化 `RepoRoot`/`OutputDir`/`Exe`/`StartupSeconds`，新增 `overlay-p0-result.json` 与退出码。
  三个历史坑写进注释：`MOUSEEVENTF_VIRTUALDESK` 必带、窗口标题精确匹配（控制台标题也含 "Overlay"）、
  布局坐标用覆盖层自己的 rect。
- **真机实测（1914×1074）**：9/9 全绿、`OVERALL: True`、退出码 0。
  `quadTop` 215/483/774 对预测 213.5/482/773.9（≤1.1 px）；`depth` 0.348(p=.5) / 0.846(p=.75)；
  模糊比 74× / 104×；上行下行逐档同值（`quadTop` 差 0、`depth` 差 0.000）→ 无滞回。
- **未做**：不改 App 任何代码（本轮纯验收侧），不动渲染模型与默认值。


### 2026-10-22 · M1 工程债清理（显示拓扑自适应 / 帧预算 / CI 渲染门禁 / 验收脚本入库 / 文档漂移） · 八弟

- **背景**：对 M1 已完成功能做全面审查（代码级 + 实测），发现的问题按 P1/P2/P3 分级处理。**本轮不改渲染模型，不动 DD-045/DD-046 的视觉决策。**
- **P1-1 显示拓扑自适应**：`GraphicsCaptureItem` 在本投影里**没有 SizeChanged 事件**（编译期实证 CS1061），改为在 `OnFrameArrived` 比较 `frame.ContentSize` 与池尺寸——检测到变化时丢弃该帧、`RecreateFramePool`、抛 `SizeChanged`；`CaptureRenderer` 把重建**延迟到渲染心跳内、持锁执行**（`_rebuildPending` → `RebuildTargets`），一次性重建常驻帧（含 mip 链）、交换链、后备缓冲与 WarpPipeline。DPI 变化时重读 `XamlRoot.RasterizationScale`。**之前 `RecreateFramePool` 有定义零调用点，改分辨率/拔插显示器会尺寸失配。**
- **P1-2 帧预算**：`RenderTick` 在 UI 线程，加 EMA 帧耗时测量；超过 25ms 预算时把磨砂半径按 0.05/帧下调（下限 0.5×），低于 60% 预算时按 0.02/帧回升。**是降质不是跳帧**（跳帧会破坏动画连续性）。`AverageFrameMs`/`QualityScale` 进 smoke JSON 与控制台状态行。
- **P2-3 CI 渲染门禁**：workflow 断言新增 `WarpState -eq 'active'`。此前 shader 编译失败会静默回退 blit，**CI 全绿而屏幕只剩镜像**——这是最大的绿灯盲区。
- **P2-4 验收脚本入库**：`scripts/acceptance/warp-selftest.ps1`（来自 `_test/m14-selftest.ps1`，参数化 RepoRoot/OutputDir/Exe/Timeout）+ README。**同时修掉三个真问题**：① dump 宽度硬编码 1914/600（历史变过两次，行号会全错）→ 改为解析 trace 里的 `selftest: dumped (WxH)`；② mask 分析整段是死代码（DD-046 已删遮罩）→ 删除；③ 滞回检查比较的是模型 v2 之后**恒为空的 Boundaries 表**，`HYSTERESIS OK` 是空断言 → 改为比较 `quadTop`（±2px）。
- **P2-5 小债**：`WarpPipeline` 的 SRV 改为缓存（原来每帧 new/dispose，30-47 次/秒 COM 分配）；`CaptureRenderer.Dispose` 进入 `_renderGate`（避免在帧回调持有时拆纹理）；关闭控制台先 `_overlay.Close()` 走 `Cleanup()` 再 `Environment.Exit(0)`（原为直接 Exit，跳过全部释放）；CA1416 用 `OperatingSystem.IsWindowsVersionAtLeast(10,0,19041)` 显式守卫（构建回到 0 警告）。
- **P3 文档漂移**：`PROJECT_SPEC` §2.2/§2.3/§12/§13 的模糊与暗化方向**与 DD-046 相反**（原文"铰链附近模糊"），已加修订说明并保留原文引用；DD-040/041/042 补 `Superseded by DD-046` 状态；`EXECUTION_PLAN` 删掉重复的 M1.6 未勾选段、重复的"阻塞项"行、Phase 2 状态改 ✅，M1.5~M1.8 补"已被 DD-046 取代"标注；README"半透明预算"改为与 `GlobalOpacity = 1.0` 一致；新增 `LICENSE`（MIT）。
- **未处理（需产品决策，不是代码问题）**：**DD-045 源冻结**——折叠期间桌面是效果出现前的静帧，视频/指针不跟随。这是物理模型自洽的取舍（玻璃盖住了桌面，看到的只能是盖住前的桌面），硬改会退回重影；DD-045 已登记 M2 可用覆盖掩码重建部分实时化。**本轮不动。**
- **验收**：`dotnet build -p:Platform=x64` 0 错 0 警；`dotnet test` 63/63；`--overlay-smoke` 真机跑通且 `WarpState=active`。

### 2026-09-19 · Phase 2 / M1.8 MVP Composite（全屏合成 + 捕获/渲染解耦——M1 收官） · Super Z (main agent)

- **完成**：SwapChainPanel 全屏化（面板像素 = 窗口客户区 1914×1074，与交换链一致 = HC §6.2 workaround 成立，对捕获纹理仅 0.3% 缩放差）；**捕获/渲染解耦**（DD-044，TECHNICAL_PROPOSAL §22 落地）——WGC 回调仅刷新常驻最新帧纹理，渲染心跳（30ms DispatchQueueTimer，UI 线程）独立执行 Tick → 四 Pass 合成 → Present，`_renderGate` 串行化心跳与帧回调的全部 context 使用；**反馈稳定化**：全局不透明度 0.65（GlobalParams）——全屏捕获必然包含自身输出，α<1 使自反馈收敛为几何级数（磨砂玻璃拖尾），α=1 无界复合真机实证数秒融化；**全局淡入**：progress<0.15 合成透明（静止态隐形，无鬼影）
- **真机验收**：selftest @1914×1074——quadTop p=0.25/0.5/0.75 = 215/483/774 vs 理论 213.5/482/773.9（≤1.5px）双向无滞回；mask 剖面（p=0.25/p=0.5）PASS；p=0 淡入隐形、p=1 几何坍缩均为预期端点；模糊铰链区边缘过渡 **1px→7px（7×）**（1:1 无降采样）；暗化铰链行与公式误差 ≤1.4；FPS 32.2（心跳设计值）
- **三件套复验**：P0-2 点击 0.4/拖动 0.29 ✅；P0-1 透明 S1=255 delta=0 ✅（套件先归零滑块——TEST 1 自己驱动过滑块，M1.8 后折叠效果会真实改变屏幕，静止态测试前必须复位——**产品正确而非回归**）；P1-a 预览差分=0 属预期（静止态淡入隐形，内容验证已由 selftest dump 覆盖）
- **踩坑记录**：① 全屏化首先暴露**捕获/渲染耦合**：静态屏幕无 WGC 帧 → 回调驱动渲染停摆 1 FPS → 按 §22 解耦（常驻帧 + 心跳）；② **自反馈复合**：全屏捕获含自身输出，α=1 时 fold^n(D) 数秒融化进铰链 → 全局不透明度收敛（同时就是半透明美学的来源）；③ selftest 分析列被面板递归污染 → selftest 期间折叠 Root（覆盖层隐藏后捕获纯净，swapchain 照常渲染与回读）；④ 色度分类器（除以最大通道）对暗化不敏感——绝对 RGB 分类在暗化行全军覆没
- **产物**：`src/DuoFlow.App/`（OverlayWindow.xaml 全屏面板 + 渲染心跳、CaptureRenderer 重写：常驻帧/RenderTick/渲染门、WarpPipeline GlobalParams cbuffer 160 字节、shaders/DuoWarp.hlsl 全局不透明度）、DD-044、EXECUTION_PLAN/TODO 同步、`_test/m14/m15/m16/m17-*`（分析脚本全屏适配）
- **遗留 / 阻塞**：**Phase 2 / M1 全部完成** ✅ → M2.1 Gradient Pass（挂心跳 Pass 链）；M2.3 视觉调优需用户真机参与（包络/透明度/maxBlur 旋钮已参数化待调）；M1.8 已知取舍：全屏自反馈拖尾（几何级数衰减，视觉=磨砂拖尾，M2 重评）

### 2026-09-19 · 效果强度包络（用户真机反馈：虚化 0.35 起可见） · Super Z (main agent)

- **用户反馈**：滑块 0.8 时"整个屏幕几乎倒下了，根本看不到效果"，要求虚化从 ~0.35 开始可见——**M2.3 视觉调优的第一次真实输入，来自项目所有者本人**
- **根因**：`radius = mask × progress × maxBlur` 对进度线性 → progress ≤ 0.5 时半径 <2 面板像素不可见，可见时（0.75+）几何已塌——**效果可见窗口与几何可见窗口错开**
- **修复（DD-043）**：效果强度包络 `IntensityAt(p)` = 线性坡（Start=0.30 → Full=0.60），CPU 侧先作用于 progress 再进 shader——blur 与 dim 共用同一条包络，shader 零改动；0.3 前无效果、0.35 起可感、0.6 后最大；几何不动（合盖物理感优先）
- **验收**：91/91 单测（+5 包络用例）；暗化预测公式同步为含包络版本——p=0.75 铰链行 54/62/80 vs 预测 54/62.6/80.9 误差 ≤0.9、p=0.5 铰链行 121/126 误差 ≤0.7、远区 255 不动；模糊铰链 3× 保持、p=0.5 铰链过渡带 1px→3px（中段模糊进入可测量区间）；FPS 47.4
- **度量教训二则**：① 全屏纯色参考窗无法区分镜像与透底（镜像同色）——参考窗必须带结构；② 暗化会压低参考窗绝对对比度（白侧 255→62），边缘检测门限必须相对化（range ≥30 而非 ≥100）
- **产物**：`src/DuoFlow.Render/BlurOptions.cs`（Start/Full + IntensityAt）、`src/DuoFlow.Core.Tests/BlurOptionsTests.cs`（+5）、DD-043、`_test/m17-analyze.ps1`（含包络预测公式）
- **遗留 / 阻塞**：无 → M1.8 MVP Composite；M2.3 调优时用户再体验一轮，包络旋钮（Start/Full）届时进参数面板

### 2026-09-19 · Phase 2 / M1.6 Blur（单 Pass 13-tap 核 + 边缘宽度法定量验收） · Super Z (main agent)

- **完成**：DuoWarp.hlsl 加 Blur Pass——`radius = mask × progress × maxBlur`，**单 Pass 13-tap 六边形核**（中心 0.25 + 双环 12 tap；radius→0 恒等收敛 = 全开零模糊硬保证），源空间半径经 SrcYRemap 线性映射（无逐 tap 重解单应，省 10× ALU）；mask 在 dest 像素取值（"糊在折叠处"）；`DuoFlow.Render/BlurOptions`（MaxBlurPixels ∈ (0,64] 校验，24 默认）+ 归一化换算；cbuffer 112→128 字节
- **验收（边缘过渡宽度法，比对比度法灵敏 5×）**：半屏黑白参考窗 → 逐行测 30%-70% 上升宽度——p=0.75 铰链区 1px→3px（**3× 模糊**，三行一致）；p=0.5 远区（quad 内 mask=0 可见带）**1× 锐利保持**；**"不全屏同时糊"的几何证明**：p=0.75 时整个可见四边形都落在遮罩区内，远区根本不可见——mask 与折叠几何共同保证 PROJECT_SPEC §12
- **性能**：13-tap 全屏 @600×338 平均 **47.4 FPS** vs 基线 48——损失可忽略；progress=0 分支裁剪零成本
- **单测 5 新用例（合计 80/80 本地+CI）**：归一化换算/极小值/非法半径（0/负/64.1/NaN/∞）/非法帧高/默认值
- **度量下限教训**：p=0.5 铰链核半径 ±1.9 面板像素低于 8-bit 量化精度 → 该断言降为信息项，p=0.75 的 3× 证明链路——**验收断言必须设计在度量能力的可分辨区**；另：全屏纯色参考窗无法区分镜像与透底（镜像同色），参考窗必须带结构
- **附带**：控制台关闭 → 全应用退出（WinUI 3 非打包 `Application.Exit()` 只停 DispatcherQueue 不杀进程——trace+进程双重验证后改 `Environment.Exit(0)`），解决真实用户"关控制台后覆盖层+黄框无路可关"
- **产物**：`shaders/DuoWarp.hlsl`、`src/DuoFlow.Render/BlurOptions.cs`、`src/DuoFlow.Core.Tests/BlurOptionsTests.cs`、`src/DuoFlow.App/`（CaptureRenderer/WarpPipeline/MainWindow 接线）、DD-041、`_test/m16-blur-verify.ps1`/`m16-analyze.ps1`
- **遗留 / 阻塞**：无 → M1.7 Dimming（`brightness = 1 − mask × progress × maxDarkness`，同一 mask 挂接点，亮度剖面验收套路同本条）

### 2026-09-19 · Phase 2 / M1.7 Dimming（公式逐字落地 + 白窗亮度剖面误差 ≤1.4） · Super Z (main agent)

- **完成**：DuoWarp.hlsl 加 Dimming Pass——`rgb *= 1 − mask × progress × maxDarkness`（PROJECT_SPEC §13 逐字），一行乘法置于 blur 之后；mask/progress 直接复用 MaskParams/BlurParams 现值——**一个铰链、一份 mask、一个 progress，四 Pass 共享同一常量流**（DD-040 口径统一承诺兑现）；`DuoFlow.Render/DimOptions`（MaxDarkness 默认 0.8 = §13 示例值，校验 [0,1]）；cbuffer 128→144 字节
- **验收（白窗亮度剖面）**：全屏纯白参考窗（模糊对均匀色 no-op → 测得变化纯属暗化）——p=0 全 255（progress=0 不动）✅；p=0.75 铰链行 104/110/123 vs 预测 104.3/110.7/124.4 **误差 ≤1.4** ✅；p=0.5 铰链行误差 ≤0.8 ✅；p=0.5 远区（mask=0）255 不动 ✅；FPS 47.4 ✅
- **单测 6 新用例（合计 86/86 本地+CI）**：默认值/边界 0 与 1/负值/超界/NaN/∞
- **度量教训（DD-042 沉淀）**：**递归污染**——面板自身内容存在于捕获画面中，blur tap 会把面板投影区的递归暗内容混进采样（首个分析轮次 row 326 误差 21 的根因）；分析脚本扫描列限制在 dest x ≤ 380（source x < 1290 = 面板投影区之外）后误差降至 ≤1.4。**凡涉及"采样源画面"的度量都必须避开面板投影区**
- **产物**：`shaders/DuoWarp.hlsl`、`src/DuoFlow.Render/DimOptions.cs`、`src/DuoFlow.Core.Tests/DimOptionsTests.cs`、`src/DuoFlow.App/`（CaptureRenderer/WarpPipeline 接线）、DD-042、`_test/m17-dim-verify.ps1`/`m17-analyze.ps1`
- **遗留 / 阻塞**：无 → **M1.8 MVP Composite**（Warp+Mask+Blur+Dimming 全链路实时运行）——shader 侧 ingredient 全部就位且各自定量验收，M1 收尾在望；M1.8 需要决策全屏化渲染与面板预览的分工（真机屏幕级验收已恢复可用）

### 2026-09-19 · 显示合成回归修复（SwapChainPanel 内容不可见 → 已解决） · Super Z (main agent)

- **现象与排查**：本机自 9/19 起 SwapChainPanel 内容不上屏（透底），warp/blit/基线 baaf444 三方复现（非 M1.4 回归）。排除链：① WinAppSDK 1.8.* 还原版本两日完全一致（排除包浮动升级）；② DXGI 查询双输出 SDR 8bit（排除 HDR/高级颜色）；③ **决定性实验 AlphaMode=Ignore**：内容出现但按 1:1 裁切显示、未拉伸到面板（600×338）——定位根因 = **"合成交换链尺寸 ≠ 面板像素尺寸"时的 DPI 缩放路径被 KB5129195（9/19 重启生效）破坏**，premultiplied alpha 合成一并失效（#1247 家族新变体）
- **修复**：交换链尺寸改为面板物理像素尺寸（`ActualWidth/Height × RasterizationScale`，本机 600×338）——面板 1:1 显示无缩放需求，premultiplied alpha 恢复；warp/mask shader 归一化坐标天然分辨率无关，捕获纹理照常采样；blit 回退路径（CopyResource 需同尺寸）在 shader 初始化失败时自动重建捕获尺寸交换链
- **回归验证**：色带窗截图确认镜像可见且缩放精确（10 条带每条 ~34px = 108×0.3125）；selftest 全量重跑（raw 变为 600×338，分析脚本改为按文件大小推导宽高）——warp 9 档 quadTop ≤1px + 无滞回，mask 剖面误差 ≤0.009、折叠裁剪 152/152；三件套 **P1-a 预览差分 0 → 122.71**，透明 delta=0、穿透 0.4/0.29 全绿——屏幕级验收能力恢复
- **遗留**：面板像素尺寸只在启动取一次，DPI/显示器变更需重建交换链（挂 SizeChanged/ScaleChanged 监听，M1.8 前做）；HC §6.2 已更新为"已修复"条目（根因/实验链/代价全记录）

### 2026-09-19 · Phase 2 / M1.5 Hinge Mask（复用铰链系 + Debug 可反解热力图） · Super Z (main agent)

- **完成**：`DuoFlow.Render/HingeMask`（MaskOptions → HingeMaskProfile.Build + Evaluate 纯函数）——`m(y) = (1 − smoothstep(|y−center|/width))^exponent`，Hinge Position（center，默认 0 与 warp 折叠轴重合）/ Hinge Width（width=0.35）/ Falloff（exponent=2）三旋钮齐备；**坐标系复用 warp 铰链系（DD-039 迁移约束兑现）**——MaskOptions 刻意不含方向字段，方向由 warp 的 DestYRemap 决定，结构上杜绝遮罩与折叠线错位；遮罩与 Progress 解耦（progress 缩放属 M1.6/M1.7 公式），selftest 实证 p=0/p=0.5 剖面逐行一致
- **Debug Mask**：DuoWarp.hlsl 加 MaskParams 常量（center/width/exponent/debug，96→112 字节）+ 热力分支 `rgb=(m,0.2,1−m)`（红=铰链蓝=远端，alpha 规则同图像路径）；**R/A、B/A 可精确反解 m**——可视化和量化验收共用同一编码（DD-040）
- **单测 18 新用例（合计 70/70 本地+CI）**：峰值/宽度外清零/单调下降/中心对称/中点闭式（exp=1→0.5、exp=2→0.25）/指数生效对比/闭式全扫/连续性护栏/非有限 y 抛错/坏 center/width/exponent 抛错/默认值
- **真机自测验收**：`--warp-selftest` 新增 mask 阶段（p=0 全幅 + p=0.5 折叠裁剪）——5 采样行 vs 闭式解**误差 ≤0.004**、B 通道交叉验证 ≤0.004、quadTop 486/484.7、p=0/p=0.5 剖面一致；warp 9 档回归全 PASS + 无滞回；PNG 存档目视吻合（m15-mask-debug-p0/p50.png）
- **踩坑记录**：① **FalloffExponent 限 [1,8] 的原因**：exp<1 时遮罩收敛端斜率无界（数学不平滑，违反 PROJECT_SPEC"过渡必须平滑"），校验层挡下而非文档约定；② 连续性护栏的固定步长界不跨指数通用（exp=8 合法斜率 ~0.114/步）——护栏防 O(0.3+) 真断点，精确性由闭式断言负责，分工写进注释；③ PS 字符串 `$row:` 被解析为 drive 引用需 `${row}`
- **产物**：`src/DuoFlow.Render/HingeMask.cs`、`src/DuoFlow.Core.Tests/HingeMaskTests.cs`、`shaders/DuoWarp.hlsl`、`src/DuoFlow.App/`（CaptureRenderer mask 持有 + RequestDump(debug) 开关、WarpPipeline Render 签名、OverlayWindow selftest mask 阶段）、DD-040、本文件三件套、`_test/m14-selftest.ps1`（mask 分析 + PNG 导出）
- **遗留 / 阻塞**：渲染无阻塞 → M1.6 Blur（消费 DD-040 同一个 mask 值，shader 内实现 + selftest 验收：blur 半径 = mask × progress × maxBlur，量化验证建议"棋盘格模糊带宽测量"）；机器显示合成路径故障待查（HC §6.2，M1.8 屏幕级验收前必须解决）

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
