# 验收脚本（acceptance harness）

这里的脚本是**真机量化验收**的执行器。CI 只能证明"能构建、能启动、属性对"，
证明不了"画面变形是否符合数学模型"——那部分靠这里的脚本 + 应用内置的自测模式。

## warp-selftest.ps1 — 折叠几何验收

**它证明什么**：拖动 Progress 时，桌面产生的是**连续的单应形变**，而不是缩放/位移冒充。
做法是拿渲染结果去对 `WarpGeometry` 的解析解，逐档比 `quadTop`（折叠四边形的远边行号）。

**怎么跑**（Windows PowerShell 5.1，不需要管理员）：

```powershell
# 1. 先构建（脚本会自己找 bin 下最新的 exe，找不到就报错）
dotnet build src/DuoFlow.App -p:Platform=x64

# 2. 跑完整流程：画全屏色带参考窗 → 启动 --warp-selftest → 等待 → 分析
.\scripts\acceptance\warp-selftest.ps1

# 只重跑分析（用上一次的 raw dump）
.\scripts\acceptance\warp-selftest.ps1 -AnalyzeOnly

# 指定输出目录 / exe / 超时
.\scripts\acceptance\warp-selftest.ps1 -OutputDir D:\tmp\run -TimeoutSeconds 180
```

**退出码**：0 = 全部在容差内；1 = 有档位超差（或自测没跑完）。

**它会做什么**（注意：跑之前别在机器上干别的，约 1 分钟）：

1. 建一个全屏、置顶、无边框的 **10 色带参考窗**（色带是分类用的编码，不是装饰）；
2. 删掉旧的 trace 与 raw dump；
3. 启动 `DuoFlow.App.exe --warp-selftest`，应用自己把引擎按 0→0.25→0.5→0.75→1.0→0.75→0.5→0.25→0 走一遍，
   每档在 Present 前做一次 staging 回读，dump 成 BGRA raw（含 alpha）；
4. 把控制台窗口移出屏幕（否则它会进入捕获，污染分析列）；
5. 逐档分析：中心列的 alpha 边界 = 折叠四边形远边 → 与单应预测比对，容差 ±5px；
6. 检查**无滞回**：同一个 p 的上行档与下行档 `quadTop` 相差必须 ≤2px；
7. 结果写 `warp-selftest-result.json`。

**为什么用"进程内回读"而不是截屏**：2026-09-19 那台机器上 SwapChainPanel 的内容在屏幕上不可见
（Windows 更新破坏了 DPI 缩放合成路径，基线提交同样复现）。渲染正确性和显示正确性必须分开举证——
回读证明"渲染出来的东西对不对"，截屏只能证明"显示器上有没有"。

**已知边界**：

- 依赖 `$env:USERPROFILE\duoflow-trace.txt` 里的 `selftest: dumped <file> (WxH)` 行来拿 dump 尺寸，
  不再硬编码宽度（历史上 1914→600→1914 变过，硬编码会让行号全错）。
- p=0 与 p=1 的 `quadTop` 按设计是 -1（前者全局淡入为全透明，后者几何完全坍缩），不是失败。
- 色带分类在模型 v2 的远端深磨砂区失效（DD-046 故意把远端压暗），所以本脚本只验**几何**，
  光影梯度由 M1.6/M1.7 时期的剖面法验证。
