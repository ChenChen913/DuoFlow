# DuoFlow 执行文档（EXECUTION PLAN）

> **本文档是 DuoFlow 执行进度的唯一权威来源（Single Source of Truth）。**
> 任何 AI Agent（Claude Code / Codex / 其他）接手本项目时，**第一件事就是通读本文档**，
> 然后按 §1 交接协议继续工作；每完成一个小任务必须回写本文档（打勾 + 更新快照 + 追加日志）。
> 交接目标是：新 Agent 不依赖任何历史对话，只看仓库内文档即可无缝继续。

| 文档信息 | 值 |
| --- | --- |
| 文档版本 | v1.0 |
| 创建日期 | 2026-09-15 |
| 最后更新 | 2026-09-15 |
| 当前阶段 | Phase 1 — M0 Research & Feasibility（未正式开始，见 §2） |
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
| 当前 Phase | **Phase 1 — M0 Research & Feasibility** |
| 当前小任务 | M0.1 开发环境（尚未开始） |
| 下一步行动 | 在 Windows 11 真机上运行 `scripts/setup-windows.ps1` 或手动安装 .NET 8 SDK / VS 2022 / Windows App SDK，创建最小 WinUI 3 工程并跑通 Hello World |
| 阻塞项 | 无硬阻塞。环境限制：M0.1 起需要 Windows 11 真机，Linux/云端沙箱无法构建 WinUI 3（详见 §6.1） |
| 最后更新 | 2026-09-15 · Phase 0 完成（双语 README / Topics / 本执行文档）· Super Z |

---

## §3 执行总览（Phase × Milestone 对照）

| Phase | 对应 Milestone | 内容 | 状态 |
| --- | --- | --- | --- |
| Phase 0 | — | 立项 · 文档基线 · 仓库基建（README / Topics / 执行文档） | ✅ 已完成 |
| Phase 1 | M0 | Research & Feasibility：开发环境 / 屏幕捕获 / Overlay / 硬件调研 | ⏳ 进行中（未开始实际任务） |
| Phase 2 | M1 | Rendering MVP：LidState / Manual Progress / Warp / Mask / Blur / Dimming | ⬜ 未开始 |
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

#### M0.1 开发环境

* [ ] 确认 Windows 11 开发环境（版本 / 内部号，记录到 §5 日志）
* [ ] 安装 .NET 8 SDK
* [ ] 安装 Visual Studio 2022 或 Build Tools（含 WinUI 3 / Windows App SDK workload）
* [ ] 确认 Windows App SDK 版本可用
* [ ] 确认 Direct3D 11 开发环境（GPU / D3D Debug Layer）
* [ ] 创建最小 WinUI 3 项目（`src/DuoFlow.App`）
* [ ] 编译并运行 Hello World

**验收**：项目可成功 Build；程序可启动并显示窗口。
**产物**：最小工程 + 环境信息记录（追加到 §5 日志）。

#### M0.2 Windows Graphics Capture

* [ ] 创建最小屏幕捕获 Demo
* [ ] 获取主显示器
* [ ] 捕获桌面纹理
* [ ] 验证 GPU Texture（DirectX / Direct3D11 interop，不落地 CPU Bitmap）
* [ ] 测试不同分辨率
* [ ] 测试高 DPI
* [ ] 测试高刷新率

**验收**：Desktop → Captured Texture 实时显示。

#### M0.3 Overlay

* [ ] 创建透明 Overlay Window
* [ ] 设置全屏
* [ ] 测试 Topmost
* [ ] 测试 Click Through
* [ ] 测试 No Activate
* [ ] 测试窗口切换
* [ ] 测试多显示器

**验收**：Overlay 不影响正常鼠标和键盘操作。

#### M0.4 Hardware Research

* [ ] 检查 Windows Sensor API
* [ ] 检查 HID
* [ ] 检查 ACPI
* [ ] 检查联想等厂商硬件接口
* [ ] 记录实际测试结果（禁止凭网络资料下结论）
* [ ] 更新 `docs/HARDWARE_COMPATIBILITY.md` 实测区

**验收**：当前开发机的传感器/接口能力有实测记录。

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

> 记录格式：
>
> ```markdown
> ### YYYY-MM-DD · Phase x · Agent 名
> - **完成**：
> - **产物**：
> - **Commit**：
> - **遗留 / 阻塞**：
> ```

### 2026-09-15 · Phase 0 · Super Z (main agent)

- **完成**：创建公开仓库；三份聊天记录（聊天记录01-03.txt）沉淀为 5 份基线文档；双语 README（中英互切）；GitHub Topics 20 个 + 双语仓库描述；创建本执行文档并确立交接协议
- **产物**：`README.md` / `README.en.md` / `EXECUTION_PLAN.md` / `docs/*.md`（5 份）/ `.gitignore`
- **Commit**：`7657281`（文档基线）+ 本次基建 commit（见 git log）
- **遗留 / 阻塞**：M0.1 起需要 Windows 11 真机环境（Linux 沙箱无法构建 WinUI 3，见 §6.1）；建议项目交接稳定后轮换 GitHub token（曾于对话中明文出现，见 §6.3）

---

## §6 交接注意事项（环境 / 限制 / 安全）

### 6.1 开发环境要求（Phase 1 起生效）

- **必须** Windows 11 真机（或开启 GPU 直通的虚拟机）；
- .NET 8 SDK + Visual Studio 2022（勾选 WinUI / Windows App SDK workload）+ 支持 D3D11 的 GPU（建议开启 D3D Debug Layer）；
- 辅助脚本 `scripts/setup-windows.ps1` 可自动完成 SDK 检测与最小工程脚手架（详见脚本注释）；
- **Linux / 云端沙箱只能做**：文档维护、接口与架构设计、纯 .NET 跨平台类库与单元测试；**不能**构建 WinUI 3 应用（Windows App SDK 仅支持 windows TFM）。

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
