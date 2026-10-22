# 验收脚本（acceptance harness）

这里的脚本是**真机量化验收**的执行器。CI 只能证明"能构建、能启动、属性对"，
证明不了"画面变形是否符合数学模型"、也证明不了"覆盖层是否真的透明/可穿透"——
那部分靠这里的脚本 + 应用内置的自测模式。

三个脚本都**不需要管理员**，都要求**真实交互式桌面**（GitHub Actions 的
windows-latest 是非交互会话，跑出来是黑的，所以它们不进 CI）。

| 脚本 | 证明什么 | 耗时 | 会动鼠标 |
|---|---|---|---|
| `warp-selftest.ps1` | 折叠几何 + 光影方向 + 模糊方向 + 无滞回 | ~70 s | 否 |
| `overlay-p0-verify.ps1` | 覆盖层透明（P0-1）/ 真输入穿透（P0-2）/ 预览有活画面（P1-a） | ~90 s | 是 |
| `tuning-panel-verify.ps1` | 参数面板的旋钮真的走到了渲染数学（M2.5/M2.1） | ~60 s | 否 |
| `demo-mode-verify.ps1` | 演示模式走完 0↔1、端点自动停、循环双向反向、暂停冻结（M2.4） | ~30 s | 否 |

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
6. 跨档检查**衰减深度随 Progress 递增**（`depth(p)=1-farL/hingeL`）；
7. 检查**无滞回**：`quadTop` 差 ≤2 px，`depth` 差 ≤0.05；
8. 结果写 `warp-selftest-result.json`。

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
- p=0 与 p=1 的 `quadTop` 按设计是 -1（前者全局淡入为全透明，后者几何完全坍缩），不是失败；
  这两档没有不透明像素，所以不参与光影断言。
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

**产出**：`tuning-panel-verify.txt` + `tuning-panel-result.json`。**退出码** 0 = 14/14。

**覆盖的 14 步**：初始推送 / MaxFold 90→60 / r 2.5→4.0 / 方向 away→toward / 铰链 底→顶 /
maxBlur 160→80 / 包络起点越界自动抬满值 / 关闭态忽略强度 / 勾选后强度生效 /
起色相 205→300 / 再次关闭回到 0.000 / 恢复默认（warp + blur + gradient 三项）。

**已知边界 / 坑**

- **WinUI ComboBox 的 popup 是独立的 UIA 子树**：从 ComboBox 元素往下 `FindAll(ListItem)`
  是空的，必须从桌面根节点搜。而且 `SelectionItemPattern.Select()` 调用成功 ≠ 选择真的变了，
  脚本会读 `SelectionPattern` 前后比对——不比对的话，失败只会表现为一个莫名的 timeout。
- 脚本用 `AutomationId` 定位控件。**M2.5 之前构建的二进制没有这些 id**，会报 `control 'X' not found`。
- `MaxBlurPixels` 的合法区间是 `(0, 400]`，所以滑块最小值是 1 而不是 0；
  包络起点必须小于满值点，越界时面板会把满值抬到 `起点 + 0.01`（这是 UI 状态，不是渲染状态）。
- 只跑 UIAutomation，**不动鼠标**；但应用仍会全屏接管约 35 秒。

---

## demo-mode-verify.ps1 — 演示模式真的走完并停住吗

**它证明什么**（M2.4）：演示播放器驱动的是**真实**的 progress 链路（provider → 引擎 → 渲染），
并且停在它该停的地方。

1. **开盖 0→1**：到 1.0、单调不回退、**之后保持不动**（端点自动停，不爬不弹）
2. **合盖 1→0**：到 0.0 并保持
3. **循环往返**：**两端都到**（`max≈1`、`min≈0`）且**两个方向都出现过**
   —— 只会单向走、或者端点跳变的实现会在这里失败
4. **暂停**：数值冻结

**怎么跑**

```powershell
dotnet build src/DuoFlow.App -p:Platform=x64
.\scripts\acceptance\demo-mode-verify.ps1
.\scripts\acceptance\demo-mode-verify.ps1 -PassDuration 0.6 -OutputDir D:\tmp\demo
```

**产出**：`demo-mode-verify.txt`（含每一次采样，能直接看出曲线）+ `demo-mode-result.json`。
**退出码** 0 = 9/9。

**已知边界**

- 采样间隔 120 ms，而 `RangeValuePattern.Current.Value` 是**实时值**（不是缓存），
  所以曲线是真曲线；单程太短（< 0.3s）会采不到点，`-PassDuration` 别设太小。
- 默认单程 0.6 s：够快（整轮 ~30 s）又够慢（能采到 ~5 个点/单程）。
- 判"单调"用 0.02 容差、判"反向"用 0.03 阈值——都是为了避开 UIA 采样抖动。
- 脚本结束会强杀 `DuoFlow.App`。
