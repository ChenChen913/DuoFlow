# 验收脚本（acceptance harness）

这里的脚本是**真机量化验收**的执行器。CI 只能证明"能构建、能启动、属性对"，
证明不了"画面变形是否符合数学模型"、也证明不了"覆盖层是否真的透明/可穿透"——
那部分靠这里的脚本 + 应用内置的自测模式。

这些脚本都**不需要管理员**，除 `camera-probe-verify.ps1`（无头）外都要求
**真实交互式桌面**（GitHub Actions 的 windows-latest 是非交互会话，跑出来是黑的，
所以它们不进 CI）。

| 脚本 | 证明什么 | 耗时 | 会动鼠标 |
|---|---|---|---|
| `warp-selftest.ps1` | 折叠几何 + 光影方向 + 模糊方向 + 无滞回 | ~70 s | 否 |
| `overlay-p0-verify.ps1` | 覆盖层透明（P0-1）/ 真输入穿透（P0-2）/ 预览有活画面（P1-a） | ~90 s | 是 |
| `tuning-panel-verify.ps1` | 参数面板的旋钮真的走到了渲染数学（M2.5/M2.1/M2.2） | ~75 s | 否 |
| `demo-mode-verify.ps1` | 六种操作（快/慢开合、循环、暂停、中途停、中途反向）+ 速率合规（M2.4/M2.3） | ~70 s | 否 |
| `light-sweep-verify.ps1` | 光带真的存在、位置对、幅度受限、包络外不出现（M2.2） | ~3.5 min | 否 |
| `camera-probe-verify.ps1` | 摄像头能枚举/打开/协商格式/收到真解码帧，且不泄露任何像素（M3.1）；加 `-Detect` 再验屏幕检测契约（M3.2）；加 `-Lid` 再验盖子进度链路（M3.3） | ~10 s（`-Detect` ~20 s，`-Detect -Lid` ~30 s） | 否 |

---

## warp-selftest.ps1 — 折叠几何 + 光影方向

**它证明什么**

1. **几何**：拖动 Progress 时桌面产生的是**连续的单应形变**，不是缩放/位移冒充。
   拿渲染结果去对 `WarpGeometry` 的解析解，逐档比 `quadTop`（折叠四边形远边行号）。
2. **光衰减方向**（DD-046）：铰链侧必须是亮端，远端必须是暗端，中间单调。
3. **模糊方向**（DD-046）：铰链侧必须锐，远端必须糊。
4. **无滞回**：同一个 p 的上行档与下行档必须落在同一组数上。

**参考窗为什么是"白底 + 黑刻线"，而不是色带**

老版本画 10 条彩色色带（靠颜色做分类）。模型 v2 之后色带的两条前提都塌了：

- 色带自身亮度从 29（蓝）到 255（白），**9 倍摆幅**——而光衰减的信号只有 ~4 倍，
  彩色参考窗根本测不出衰减方向（信号被底色淹没）。
- DD-046 故意把远端压进阴影，那里颜色分类必然失效。

所以改成**均匀白底 + 每 80 px 一条 8 px 黑刻线**：

- 白底 → `亮度(y) == 255 × light(y)`，衰减剖面可以直接读，还能验证单调性；
- 刻线 → 高频探针。铰链处锐利（单行亮度跳变 85~255），远端被磨砂吃掉（跳变只剩几个单位）。

一次扫描同时服务几何和光影两类断言，不用跑两遍。

**怎么跑**

```powershell
# 1. 先构建（脚本会自己找 bin 下最新的 exe，找不到就报错）
dotnet build src/DuoFlow.App -p:Platform=x64

# 2. 跑完整流程：画全屏参考窗 → 启动 --warp-selftest → 等待 → 分析
.\scripts\acceptance\warp-selftest.ps1

# 只重跑分析（用上一次的 raw dump）
.\scripts\acceptance\warp-selftest.ps1 -AnalyzeOnly

# 指定输出目录 / exe / 超时
.\scripts\acceptance\warp-selftest.ps1 -OutputDir D:\tmp\run -TimeoutSeconds 180
```

**退出码**：0 = 全部通过；1 = 有档位超差、有滞回、或光影/模糊方向不对。

**它会做什么**（跑之前别在机器上干别的，约 70 秒）：

1. 建一个全屏、置顶、无边框的**白底黑刻线参考窗**；
2. 记录 trace 的当前长度与时间戳（**不删旧文件**，见"已知边界"）；
3. 启动 `DuoFlow.App.exe --warp-selftest`，应用自己把引擎按
   0→0.25→0.5→0.75→1.0→0.75→0.5→0.25→0 走一遍，每档在 Present 前做一次
   staging 回读，dump 成 BGRA raw（含 alpha）；
4. 把控制台窗口移出屏幕（否则它会进入捕获，污染分析区）；
5. 逐档分析：
   - `quadTop`（alpha 边界）对单应预测，容差 ±5 px；
   - 20 段亮度剖面（每段取**中位数**，避开刻线）→ 铰链端 vs 远端 + 单调性；
   - 15% 窗口内的**单行最大亮度跳变** → 铰链端 vs 远端；
   - **覆盖度**（stride-4 网格上 alpha≥16 的占比）与**色度**（不透明像素的通道差均值）；
6. 跨档检查**衰减深度随 Progress 递增**（`depth(p)=1-farL/hingeL`）；
7. 检查**无滞回**：`quadTop` 差 ≤2 px，`depth` 差 ≤0.05；
8. M2.3 新增：**过暗**（远端亮度 ≥8/255）、**过糊**（p≤0.55 时铰链行梯度 ≥150）、
   **过彩**（渐变关闭时全帧色度 ≤2.0，判据从 trace 的 `gradient enabled=` 读出来）、
   **覆盖度结构**（随 Progress 单调递减，上下行同值 ≤1%）；
9. 结果写 `warp-selftest-result.json`。

**为什么覆盖度是个独立指标**：`FarEdgeY = M00/(M11+|M21|) = r·cosφ/(r+sinφ)`。
默认 `MaxFold=90°` 时它 **p=1 恒为 0**——玻璃立成边缘朝向相机，一个像素都不画。
实测 74.5 / 48.9 / 24.1 / 0 %（p=.25/.50/.75/1.0），这就是"后 20% 看不到效果"的量化证据。
它**只由 MaxFold 决定**，r 从 2.5 到 6.0 都不改变终局。见 DD-052 §3。

**为什么用"进程内回读"而不是截屏**：2026-09-19 那台机器上 SwapChainPanel 的内容在屏幕上不可见
（Windows 更新破坏了 DPI 缩放合成路径，基线提交同样复现）。渲染正确性和显示正确性必须分开举证——
回读证明"渲染出来的东西对不对"，截屏只能证明"显示器上有没有"。

**为什么每段亮度取中位数而不是均值**：黑刻线占 ~10% 的行，而它在每一段里的**密度取决于当地的
形变尺度**——均值会让每段被压低不同的量，正好污染要测的那个量；中位数落在背景行上。

**为什么先做 un-premultiply**：shader 输出的是 `rgb*a*fade`、alpha 也是 `a*fade`，
直接读 RGB 等于把覆盖率算进亮度（羽化行会被读成"更暗"）。除以 alpha 才拿到真实表面亮度。

**已知边界**

- 依赖 `$env:USERPROFILE\duoflow-trace.txt` 里的 `selftest: dumped <file> (WxH)` 行来拿 dump 尺寸，
  不再硬编码宽度（历史上 1914→600→1914 变过，硬编码会让行号全错）。
  trace 有 4 MB 上限会轮转，所以对**归档 dump** 跑 `-AnalyzeOnly` 时用 `-DumpGeometry 1914x1074`。
- p=0 与 p=1 的 `quadTop` 按设计是 -1（前者全局淡入为全透明，后者几何完全坍缩），不是失败；
  这两档没有不透明像素，所以不参与光影断言。
  **p=1 的"完全坍缩"断言绑死在 `MaxFold=90°` 上**——若 M2.3 决定降低默认角度，
  这一支要跟着改，`Coverage` 字段就是用来盯这件事的。
- p=0 额外要求**全帧 alpha 逐像素为 0**（不只是中心列），否则角落漏一块也算失败。
- **衰减深度的绝对值依赖帧预算降级**：机器吃紧时 `qualityScale` 会降到 0.5，最大模糊半径
  160→80 px，深度从 .17/.44/.82 变成 .11/.36/.48。所以断言只卡**顺序**和**一个宽松下限**
  （`depth(.75) ≥ 0.20`），不卡具体数值。
- 刻线间距 80 px、厚 8 px。若以后改了参考窗，铰链端"锐度下限 20"这条断言会先报错
  （刻线缺失时单行跳变会塌到 ~1），提示你参考窗没画出来。
- 不删 trace、不删旧 dump：某些 shell 会拦删除（safe-delete fail-closed），
  而"记录 offset + 时间戳"比"删除成功"是更强的证据——旧文件残留会被 stale 检查挡住。

**怎么证明「新 pass 关闭时是 no-op」**（M2.1 用的手法，以后加 pass 照抄）

着色器是**启动时从磁盘读**的（`AppContext.BaseDirectory\shaders\DuoWarp.hlsl`），
所以可以在**同一个二进制**上只换这个文件，把"渲染数学变了"和"代码变了"彻底分开：

1. 换回 HEAD 版 shader，跑一次 → dump A1
2. **同一个** shader 再跑一次 → dump A2（这一对给出**逐像素噪声底**：鼠标指针 + 刻度线
   双线性重采样的边缘）
3. 换回新 shader 再跑 → dump B
4. `A1 vs B` 的差异必须落在 `A1 vs A2` 的噪声底之内（同样的最大差、同样的像素量级、
   同一批位置）

本机实测：噪声底最大差 23、差异像素 35~115 个；新旧 shader 之间最大差同样是 23、
差异像素 92~337 个（都在指针与刻度边缘），而**所有量化指标四轮逐位相同**
（`hinge=227.68 far=148.37 depth=0.3483` 等）。只做 `A1 vs B` 单次比对是不够的——
没有噪声底，就无法区分"1 LSB 抖动"和"真的改了一点"。

**反向对照同样必要**：把渐变临时打开再跑一次，确认它真的产生可测变化，
否则"关闭无差"可能只是"这个 pass 根本不起作用"：

| p=0.75 远端 | R | G | B |
|---|---|---|---|
| 渐变关 | 24.30 | 24.30 | 24.30（中性灰） |
| 渐变开 0.15 | 41.29 | 33.80 | 50.38（B>R>G，紫调） |

同一组里铰链侧 `227.23 → 227.23`（+0.02）——**只吃阴影侧**这条设计约束同时被验到。

---

## overlay-p0-verify.ps1 — 覆盖层 P0 三件套

**它证明什么**

| 项 | 方法 | 判据 |
|---|---|---|
| **P0-1 透明** | 白色参考窗分别"隔覆盖层采样"(S1) 与"覆盖层移出屏外采样"(S2) | `S1.Mean ≥ 150` 且 `|S1-S2| ≤ 40` |
| **P0-2 穿透·点击** | 覆盖层压到 TOPMOST 带顶部，真实光标点滑条 0.40 | UIAutomation 读到 `0.4` |
| **P0-2 穿透·拖动** | 真实 16 步拖动 0.70→0.30 | 读回落在 `[0.27, 0.35]` |
| **P1-a 预览** | 白参考窗放进预览映射区，比对有/无参考窗 | 面板内部非黑，或差分 ≥20 |

外加一组**因果 A/B**（清 `WS_EX_LAYERED` / `LWA_ALPHA=0` / 恢复 255），
这样将来一旦回归，能直接归因到某个 style bit，不用猜。

**先跑控制组**：脚本第一步把控制台置顶、点一下滑条，确认注入链路本身是通的。
控制组失败 → 后面所有结果作废（日志里会明说）。

**怎么跑**

```powershell
.\scripts\acceptance\overlay-p0-verify.ps1
.\scripts\acceptance\overlay-p0-verify.ps1 -OutputDir D:\tmp\p0
.\scripts\acceptance\overlay-p0-verify.ps1 -Exe <DuoFlow.App.exe 路径> -StartupSeconds 20
```

**产出**：`p0-verify.txt`（人读日志）、`p0-*.png`（取证截图）、
`overlay-p0-result.json`（机器判据）。**退出码** 0 = 4/4 通过。

**已知边界 / 坑**

- **白参考窗撞白桌面 = 白测白，结论无效**。P0-1 请用非白壁纸跑；
  即便如此，A/B 那组仍能抓住"覆盖层变成不透明"这类回归。
- **找窗口别用 `-like '*Overlay*'`**：控制台标题里也有 "Overlay"，会抢走匹配。
  脚本用的是 `-eq 'DuoFlow Overlay'` + `-like 'DuoFlow Console*'`。
- **`mouse_event(MOVE|ABSOLUTE)` 必须带 `MOUSEEVENTF_VIRTUALDESK (0x4000)`**，
  否则按**主屏**归一化；多显示器下坐标会被压缩（曾经因此误判拖动 FAILED）。
- 布局坐标一律用覆盖层自己的 `GetWindowRect`（主屏 rect），不要用 `SM_CXVIRTUALSCREEN`。
- App 冷启动可能 >3 s，脚本默认等 14 s（`-StartupSeconds` 可调）。
- 会占用真实鼠标约 30 秒。

---

## tuning-panel-verify.ps1 — 参数面板真的接上渲染了吗

**它证明什么**：控制台的旋钮不是只改了 UI。每一步都用 UIAutomation 驱动控件（和读屏器
同一条通道，不靠像素猜），再读应用自己 trace 里 `CaptureRenderer` 从 `WarpGeometry.Compute`
**算出来的**几何。

关键断言是全折叠时的 `M00 = r · cos(MaxFold)`：

| 旋钮 | 期望 M00 |
|---|---|
| MaxFold 90°、r 2.5 | `0.0000`（平面侧对相机，无像） |
| MaxFold 60°、r 2.5 | `1.2500` |
| MaxFold 60°、r 4.0 | `2.0000` |
| 同上 + 方向 toward | `M21 = +0.8660`（符号翻转） |

只改 UI 不改渲染的话，`M00` 会一直停在 `0.0000`，上面每一档都会失败。

渐变的四步（M2.1）断言的不是「复选框能点」，而是**关闭时强度必须归零**：
把强度滑块推到 0.15 之后 trace 仍须报 `strength=0.000`——那个 0 就是像素着色器
`if (GradientParams.x > 0.0)` 的分支条件，也是「关闭 = 无操作」的机制本身。
像素级证明在 `warp-selftest` 那边（见上）。

**怎么跑**

```powershell
dotnet build src/DuoFlow.App -p:Platform=x64
.\scripts\acceptance\tuning-panel-verify.ps1
.\scripts\acceptance\tuning-panel-verify.ps1 -OutputDir D:\tmp\tune
```

**产出**：`tuning-panel-verify.txt` + `tuning-panel-result.json`。**退出码** 0 = 20/20。

**覆盖的 20 步**：初始推送 / MaxFold 90→60 / r 2.5→4.0 / 方向 away→toward / 铰链 底→顶 /
maxBlur 160→80 / 包络起点越界自动抬满值 / 关闭态忽略强度 / 勾选后强度生效 /
起色相 205→300 / 再次关闭回到 0.000 / 光带关闭态忽略强度 / 光带勾选后强度生效 /
光带位置 0.30 → 中心 0.8000 / 光带方向反转 → 中心 0.2000 / 光带再次关闭 /
恢复默认（warp + blur + gradient + sweep 四项）。

**已知边界 / 坑**

- **WinUI ComboBox 的 popup 是独立的 UIA 子树**：从 ComboBox 元素往下 `FindAll(ListItem)`
  是空的，必须从桌面根节点搜。而且 `SelectionItemPattern.Select()` 调用成功 ≠ 选择真的变了，
  脚本会读 `SelectionPattern` 前后比对——不比对的话，失败只会表现为一个莫名的 timeout。
- 脚本用 `AutomationId` 定位控件。**M2.5 之前构建的二进制没有这些 id**，会报 `control 'X' not found`。
- `MaxBlurPixels` 的合法区间是 `(0, 400]`，所以滑块最小值是 1 而不是 0；
  包络起点必须小于满值点，越界时面板会把满值抬到 `起点 + 0.01`（这是 UI 状态，不是渲染状态）。
- 只跑 UIAutomation，**不动鼠标**；但应用仍会全屏接管约 35 秒。

---

## light-sweep-verify.ps1 — 光带真的在，而且在它该在的地方

**它证明什么**（M2.2）：四条，缺一条这个 pass 就不算成立。

1. **存在**：开启后行剖面出现可测增量（不是"参数传进去了"）；
2. **位置对**：增量峰值落在模型算出的行上，而不是随便哪一行；
3. **幅度受限**：峰值 ≤ 强度上限（0.08 × 255 = 20.4）；
4. **包络外不出现**：`p ≤ 0.30` 时强度为 0，光带必须完全消失。

**为什么用差分而不是"找最亮的那一行"**

默认强度 0.08 下，光带在折叠前半段只加十几个亮度单位，而铰链→远端的斜坡本身有 ~190 单位。
直接找最亮行会永远找到铰链，什么也证明不了。所以脚本跑**三轮同一二进制**：

| 轮次 | 参数 | 用途 |
|---|---|---|
| `off1` | 无 | 差分基准 |
| `off2` | 无 | **逐行噪声底**（同代码跑两遍差多少） |
| `on` | `--light-sweep` | 光带 |

`on − off1` 的峰值就是光带；`off2 − off1` 说明这个峰值不是噪声。

**位置怎么预测**：`Y = M00·sy/(M11 − M21·sy)`，`row = h·(1 − Y)`，
`M00 = r·cos φ`、`M11 = r`、`M21 = −sin φ`、`φ = p·π/2`。容差取 0.75 倍 FWHM
（把 `Width` 从折叠高度换算成行数），因为亮带本身就有宽度。

**本机实测**（1914×1074，默认 0.08 / 0.10）

| p | 噪声底 | 峰值增量 | 实测行 | 预测行 |
|---|---|---|---|---|
| 0.25 | 0.000 | 0.00（包络未开，无光带 ✓） | — | — |
| 0.50 | 0.000 | 13.39 | 746 | 741.3 |
| 0.75 | 0.010 | 20.60 | 833 | 832.6 |

**怎么跑**

```powershell
dotnet build src/DuoFlow.App -p:Platform=x64
.\scripts\acceptance\light-sweep-verify.ps1
.\scripts\acceptance\light-sweep-verify.ps1 -AnalyzeOnly      # 复用已有 dump，秒级
```

**产出**：`light-sweep-verify.txt` + `light-sweep-result.json`。**退出码** 0 = 14/14。

**已知边界 / 坑**

- **采样带从 46% 起，不是 42%**。42% 处会蹭到屏幕上一个偶发亮斑（21 px），
  足以把行剖面推动 **1.6 单位**——和要测的信号同量级。收窄后噪声底 0.006。
- **零项检查必须判失败**。第一版尺寸读错时全程"无 quad"，脚本照样打印
  `PASS (0/0)`——这正是本项目反复防的假阳性，已加护栏。
- **`-match` 会覆盖 `$Matches`**：`Get-DumpSize` 里先匹配尺寸、再匹配文件名，
  第二次匹配把第一次的捕获组冲掉了，尺寸变成 0×0。必须先把 `$Matches` 存进变量。
- **`($steps | Where-Object {...}).Count` 在只匹配一项时是字典键数（4）**，
  会把"1 项失败"打印成"4 of 20 失败"。要用 `@(...)` 包一层。
- 需要 `--light-sweep` 这个启动参数（把面板初始状态种成"开"）；旧二进制没有它，
  三轮会完全一样，于是"光带不存在"会被误判成"no-op 成立"。

---

## demo-mode-verify.ps1 — 六种操作都走得对吗

**它证明什么**（M2.4 演示链路 + M2.3 六种操作）：演示播放器驱动的是**真实**的 progress
链路（provider → 引擎 → 渲染），并且每一种操作都停在它该停的地方、以它该有的速度走。

| # | 操作 | 断言 |
|---|---|---|
| 1 | 快开 0→1（0.6 s） | 到 1.0、单调、**速率 ≤ 2.0/s**、之后保持不动 |
| 2 | 快合 1→0 | 到 0.0、单调、速率合规、之后保持不动 |
| 3 | 循环往返 | **两端都到**（`max≈1`、`min≈0`）且两个方向都出现过 |
| 4 | 暂停 | 数值冻结 |
| 5 | 慢开 3 s | 单调、到 1.0、速率 ~0.33/s |
| 6 | 慢合 3 s | 单调、到 0.0、速率合规 |
| 7 | 中途停 | 停在两端之间，且 **1.4 s 内离散度 ≤0.02**（不弹不抖） |
| 8 | 中途反向 | 方向**只变一次**、速率合规、终值回到 0 |

**为什么「跳变」要用速率而不是相邻采样差**：`RangeValuePattern.Current.Value` 读一次要
10~40 ms，采样间隔是浮动的。固定阈值要么在慢读时误报（0.3 的差在 400 ms 里完全合规），
要么在快读时漏报（真跳 0.3 也判过）。所以每个采样带时间戳，速率 = Δprogress/Δt，
上限取引擎自己的 `MaxProgressPerSecond`（2.0/s）再给 30% 采样余量。

**为什么 loop 模式要先在端点停 0.35 s**：引擎是 τ=0.10 的指数跟随，0.6 s 一趟在折返点
落后 0.06~0.17。不做停留时屏幕上永远到不了 1.0/0.0（实测峰值只有 **0.943**），
而"端点好不好看"正是 M2.3 要用眼睛判断的东西。停留后峰值 **1.000**。

**怎么跑**

```powershell
dotnet build src/DuoFlow.App -p:Platform=x64
.\scripts\acceptance\demo-mode-verify.ps1
.\scripts\acceptance\demo-mode-verify.ps1 -PassDuration 0.6 -OutputDir D:\tmp\demo
```

**产出**：`demo-mode-verify.txt`（含每一次采样，能直接看出曲线）+ `demo-mode-result.json`。
**退出码** 0 = 22/22。

**已知边界**

- 采样间隔 120 ms，而 `RangeValuePattern.Current.Value` 是**实时值**（不是缓存），
  所以曲线是真曲线；单程太短（< 0.3s）会采不到点，`-PassDuration` 别设太小。
- 默认单程 0.6 s；整轮约 70 s（六种操作 + 两趟 3 s 慢速）。
- 判"单调"用 0.02 容差、判"反向"用 0.03 阈值——都是为了避开 UIA 采样抖动。
- 脚本结束会强杀 `DuoFlow.App`。

---

## camera-probe-verify.ps1 — 摄像头能打开，而且不泄露任何像素

包住 `DuoFlow.App.exe --camera-probe=<dir>`（无头模式：不开窗、不碰屏幕、不动鼠标），
把探针的 JSON 当作被测对象，逐项断言。

**18 项检查**

| # | 检查 | 判据 |
|---|---|---|
| 1 | 探针自己给出结论 | exit code 0（不是"跑起来了"就算过） |
| 2 | 报告可解析 | `camera-probe-result.json` 存在且能 parse |
| 3 | 枚举 | 至少 1 台设备，并打印 `[Enclosure] Name` |
| 4 | 选择 | 选出了设备（策略是确定性的，见 DD-053） |
| 5 | 协商格式 | **打开后** W/H > 0 且像素格式非空 |
| 6 | 帧预算 | 报告里的 `RequestedFrames` > 0 |
| 7 | 收帧 | `FramesReceived >= RequestedFrames` |
| 8 | 不虚报 | `FramesOffered >= FramesReceived` |
| 9 | 速率 > 0 | `MeasuredFps > 0` 且 `CameraFps > 0` |
| 10 | 速率一致 | 两者相差 ≤ 35%（证明瓶颈不在我们的 governor） |
| 11 | **缓冲区结构** | `AlphaMean ≥ 250` |
| 12 | 非空 | `ByteMax > 0` |
| 13 | 健康分类 | `Health = None` |
| 14 | 隐私 | `StoredPixels = false` |
| 15 | 隐私 | 除 `StoredPixels` 外无键匹配 `/pixel/` |
| 16 | 隐私 | 无逐帧数值数组（报告只能是标量） |
| 17 | 隐私 | JSON < 64 KB（放不下一帧） |
| 18 | 场景（可选） | `-RequireContrast` 时要求 `SceneFlat = false` |

**为什么第 11 项才是承重的那一项**

`NV12` 不携带 alpha，所以一次**正确**的 `NV12 → BGRA8` 转换必然把 alpha 合成 255。
读错缓冲区（错格式、错步长、未初始化的分配）就不会是 255。它证明的是
"这堆字节是解码出来的图像"——比"画面有变化"更接近我们真正想知道的事。

**为什么场景对比度不是门禁**

画面有没有对比度取决于**房间**，不取决于代码。锁屏 + 镜头被遮时，
一台完全正常的摄像头会给出整帧均匀的画面（本机实测：每个字节都是 15，
alpha 255，4.9 fps，30 帧标准差全为 0）。拿它当门禁，脚本会因为一个它修不了的原因失败。
所以默认只观测，灯开着、快门打开时再加 `-RequireContrast` 证明活画面。

**真机基线（2026-10-23，LENOVO 83EG，会话锁屏）**

| 观测 | 值 | 读法 |
|---|---|---|
| 枚举 → 打开 | 1 台 `Integrated Camera` → **1280×720 @30fps NV12** | 枚举报 0×0 是故意的 |
| `FramesOffered/Received/Dropped` | 32 / 30 / 2 | 不是我们在丢帧 |
| `CameraFps` / `MeasuredFps` | 4.88 / 4.57 | 摄像头自己只有 4.9 fps（暗场拉长曝光） |
| `AlphaMean` | **255** | 真 BGRA |
| `ByteMin` / `ByteMax` | 15 / 15 | 整帧均匀，15 = NV12 有限范围黑电平（Y=16） |
| `ContrastDelta` | 0 | `SceneFlat=True` |

**怎么跑**

```powershell
dotnet build src/DuoFlow.App -p:Platform=x64
.\scripts\acceptance\camera-probe-verify.ps1
.\scripts\acceptance\camera-probe-verify.ps1 -RequireContrast      # 灯开着时
.\scripts\acceptance\camera-probe-verify.ps1 -OutputDir D:\tmp\cam
```

**产出**：`camera-probe-verify.txt` + `camera-probe-verify-result.json` +
`probe\camera-probe-result.json`（探针原始报告）。
**退出码** 0 = 18/18。

**已知边界**

- 无头运行，不需要交互式桌面。
- 帧预算和目标帧率由**探针**决定（30 帧 / 10 fps），脚本从报告里读回来——
  不提供 `-Frames` / `-TargetFps`，因为探针不读它们，那种参数只会假装能配。
- 摄像头被别的程序独占时可能 `Health = InUse`：先关掉会议软件再跑。

### 加 `-Detect`：把 M3.2 屏幕检测也验一遍

```powershell
.\scripts\acceptance\camera-probe-verify.ps1 -Detect
.\scripts\acceptance\camera-probe-verify.ps1 -Detect -RequireContrast   # 灯亮时
```

多跑一个 `--camera-detect`（同样是 30 帧 / 10 fps），然后断言 **9 项**（合计 27）：

| # | 检查 | 判据 |
|---|---|---|
| 19 | 检测探针退出 0 | exit code 0 |
| 20 | 报告可解析 | `camera-detect-result.json` |
| 21 | **每帧都给出决定** | `FramesDetected >= RequestedFrames` |
| 22 | **从不抛异常** | `DetectorThrows = 0` |
| 23 | 不落像素 | `StoredPixels = false` |
| 24 | 四边形已归一化 | `BestQuad` 恰好 8 个数，全部落在 0..1 |
| 25 | （`-RequireContrast` 时）真的找到了 | `UsableFrames > 0` |

**第 21/22 项才是门禁，第 25 项不是。** 镜头被遮或房间很暗时画面里没有屏幕可找，
一个在那种画面上"找到"四边形的检测器才是 bug。所以默认只要求
"每帧都做了决定、且没抛异常"，检测质量用 `[INFO]` 行报告。
灯亮着、快门打开时再加 `-RequireContrast`，那时才要求真找到。

**真机基线（2026-10-23，同一台机器，差别只在环境）**

| 场景 | 摄像头帧率 | 整帧亮度 | 可用帧 | 最佳置信度 |
|---|---|---|---|---|
| 镜头被遮 / 暗房 | 4.88 fps | 15 | 0/30（`FlatScene`） | — |
| 灯亮、活画面 | **30.13 fps** | 135 | **30/30** | **0.864** |

这张表同时是 M3.1 那个"4.88 fps 是不是 bug"的对照组：灯一亮摄像头自己回到 30 fps，
说明低帧率是暗场拉长曝光，不是代码。

**`-Detect` 的已知边界**

- 约多 10 s（同样 30 帧，但每帧多一次检测）。
- 报告里只有四边形（归一化坐标）和置信度——四边形是几何，不是图片。
- **检测器不知道那块区域是什么。** 它找的是画面里主导的平面区域；
  至于那是笔记本屏幕、外接显示器还是桌沿，属于 M3.3 的问题（见 DD-054 末节）。
- ⚠️ **已知会误报一次**：在开发机上盖子全开时，它报 30/30 可用、置信度 0.513，
  四边形几乎铺满画面（`(0,0) (0.9875,0.6) (0.9938,0.9889) (0,0.9889)`）。
  原因是 `AreaFraction` 取的是**四边形面积**（约 0.68，被斜切了一角），
  覆盖度否决没触发。**没有修**：M3.3 不走这条打分器，而修它要动一个已通过验收的模块。
  详见 DD-055 §9——将来若复活"外接显示器"那条路，别以为这里干净。

---

### 加 `-Lid`：把 M3.3 盖子进度链路也验一遍

```powershell
.\scripts\acceptance\camera-probe-verify.ps1 -Lid
.\scripts\acceptance\camera-probe-verify.ps1 -Detect -Lid
```

多跑一个 `--camera-lid`（30 帧 / 10 fps），然后断言 **8 项**（合计 33）：

| # | 检查 | 判据 |
| --- | --- | --- |
| 26 | 探针退出 0 | `exit=0` |
| 27 | 报告已写出 | `camera-lid-result.json` 存在且可解析 |
| 28 | 每帧都产出状态 | `States >= RequestedFrames` |
| 29 | 视觉链路从不抛 | `Failures` 为空 |
| 30 | 报告不含像素 | `StoredPixels = false` |
| 31 | `Progress` 在 0~1 | `ProgressMin >= 0 && ProgressMax <= 1` |
| 32 | `Coverage` 在 0~1 | `CoverageMin >= 0 && CoverageMax <= 1` |
| 33 | **无测量则无进度** | `ConfidenceMax = 0` 时 `ProgressMax` 必须为 0 |

第 33 项是这里唯一"有内容"的断言：它抓的是"冻结的进度被当成测量值放出去"。
一个没有测量却报进度的 tracker，比一个什么都不报的 tracker 更坏。

**`-Lid` 的已知边界（很重要，别误读成"能跟踪合盖"）**

- 探针运行时**盖子是不动的**。所以进度本来就该是一条直线；
  进度动了才是 bug。它证明的是**管线跑通 + 判定稳定**，不是跟踪精度。
- **盖子全开时没有可用测量**，这是正确行为，不是失败。真机实测：

| 指标 | 值 |
| --- | --- |
| `DeckReasons` | `NoFarBoundary` × 30 |
| `TouchesTopFrames` | 30/30 |
| `Coverage` | 0.500~0.507（std 0.0018） |
| `Progress` | 0（冻结） |
| `Confidence` | 0 |

  原因：全开时键盘面不在画面里，画面下半部那块暗区**顶到画面顶边**，
  也就是没有"远边界"——而 `Coverage` 的定义就是"远边界以下的面积"。
  没有边界，这个量不成立，所以拒绝给数字（DD-055 §3/§4）。

- 真正的 0→1 跟踪**需要用户实际合一次盖子**才能采到。当前没有自动化手段。
