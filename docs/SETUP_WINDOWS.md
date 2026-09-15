# DuoFlow — Windows 11 开发环境搭建指引（M0.1）

> 本文档回答四个问题：**脚本放哪、用什么工具、怎么执行、执行后应该看到什么**。
> 对应 `EXECUTION_PLAN.md` Phase 1 的 M0.1 任务，脚本本体是 [`scripts/setup-windows.ps1`](../scripts/setup-windows.ps1)。

---

## 一、脚本放在哪里执行？

**结论：不需要单独下载脚本。把整个仓库克隆下来，脚本已经在正确位置了。**

`setup-windows.ps1` 会根据自己所在的目录推导仓库根目录（它必须在 `<仓库>\scripts\` 里运行），然后在根目录下创建 `src\DuoFlow.App`。所以：

- ✅ 正确做法：`git clone` 整个仓库（或下载 ZIP 解压），脚本保持在 `DuoFlow\scripts\` 原位不动；
- ❌ 错误做法：只把 .ps1 单独下载到桌面运行——工程会被建到桌面去，目录结构就乱了。

推荐目录示例：`C:\Dev\DuoFlow`（路径里**不要有中文和空格**，C# / MSIX 工具链对中文路径偶有兼容问题）。

## 二、用什么工具执行？

**Windows 自带的 PowerShell**（不要用 cmd，不要双击文件）。

- 双击 .ps1 默认只会用记事本打开，这是 Windows 的安全设计，不是坏了；
- 打开方式：开始菜单搜索 **PowerShell** 或 **终端（Terminal）** → 右键 → **以管理员身份运行**（管理员权限是给第三步安装 VS Build Tools 用的）。

前置条件只有两个：

1. **Git**（用来克隆仓库）。没装的话先执行 `winget install --id Git.Git -e` ，或者直接在浏览器打开 https://github.com/ChenChen913/DuoFlow → 绿色 Code 按钮 → **Download ZIP** 解压；
2. **网络**：脚本会用 winget 在线下载 SDK（几个 GB），建议 Wi-Fi / 有线，笔记本插电源。

## 三、怎么执行？（完整命令）

在**管理员 PowerShell** 里逐条执行（逐条粘贴、回车）：

```powershell
# 1. 进入你想放代码的目录（示例）
cd C:\Dev

# 2. 克隆仓库（或用 ZIP 解压，效果一样）
git clone https://github.com/ChenChen913/DuoFlow.git
cd DuoFlow

# 3. 解除当前窗口的脚本运行限制（只对这一个窗口生效，关掉就恢复，安全）
Set-ExecutionPolicy -Scope Process Bypass -Force

# 4. 运行环境搭建脚本
.\scripts\setup-windows.ps1
```

> 如果第 4 步报红字"禁止运行脚本"，说明第 3 条没执行或换窗口了，重新执行第 3、4 条即可。

脚本会自动做 6 件事（对应下面的输出）：检测系统版本 → 装 .NET 8 SDK → 装 VS 2022 Build Tools → 装 WinUI 3 项目模板 → 创建 `src\DuoFlow.App` 最小工程 → 构建验证。

## 四、执行后预期看到什么？

正常输出大致长这样（`[OK]` 绿色、`[!]` 黄色都是正常提示，不是报错）：

```text
==> Checking Windows version
    Microsoft Windows 11 专业版 | Build 22631
    [OK] OS check done

==> Checking .NET SDK
    [OK] .NET 8 SDK already installed          ← 已装则直接过；没装则自动 winget 安装

==> Checking Visual Studio 2022 / Build Tools
    [!] VS 2022 ... was not detected.
    Installing VS 2022 Build Tools via winget (this can take a while)...
    ← 没装则自动安装，【这一步最慢，10~40 分钟，取决于网速】，期间窗口安静属正常

==> Installing Windows App SDK (WinUI 3) dotnet templates
    [OK] Templates installed (dotnet new winuiapp)

==> Scaffolding minimal WinUI 3 project at src/DuoFlow.App
    [OK] Created src/DuoFlow.App

==> Building src/DuoFlow.App (toolchain verification)
    Restore complete (0.8s)
    DuoFlow.App -> C:\Dev\DuoFlow\src\DuoFlow.App\bin\Debug\...\DuoFlow.App.exe
    [OK] Build succeeded - M0.1 toolchain is ready.

Next steps (M0.1 checklist in EXECUTION_PLAN.md):
  1. Launch the app (dotnet run or F5 in VS) and confirm the window shows.
  2. Tick M0.1 items in EXECUTION_PLAN.md section 4, ...
  3. Commit: 'phase(M0.1): dev environment verified + minimal WinUI 3 project'.
```

**成功标志（满足其一即可）：**

1. 最后一屏出现绿色 `Build succeeded - M0.1 toolchain is ready.`；
2. 仓库里多出了 `src\DuoFlow.App` 文件夹（里面有 .csproj、App.xaml 等）。

**可选的最终验证（跑出 Hello World 窗口）：**

```powershell
cd C:\Dev\DuoFlow\src\DuoFlow.App
dotnet run
```

几秒后屏幕上**弹出一个空白的桌面窗口** = M0.1 全部达成。关掉窗口即可。

**耗时参考**：本机已装齐 → 3~5 分钟；全新系统全量安装 → 30~60 分钟（大头是 VS Build Tools，约 3~4 GB）。

## 五、常见问题排查

| 现象 | 原因与处理 |
| --- | --- |
| 双击 .ps1 打开了记事本 | 正常现象。按第三节用 PowerShell 命令行运行 |
| 红字：`在此系统上禁止运行脚本` | 没执行 `Set-ExecutionPolicy -Scope Process Bypass -Force`，或换了新窗口 |
| 红字：`winget` 不是内部或外部命令 | 打开 Microsoft Store 搜 **应用安装程序 (App Installer)** 更新；或手动装：.NET 8 SDK → https://dotnet.microsoft.com/download/dotnet/8.0 ，VS 2022 Community → https://visualstudio.microsoft.com/zh-hans/ （勾选".NET 桌面开发"） |
| `dotnet new winuiapp` 找不到模板 | 第 4 步模板安装失败；在 VS 安装器里勾选"Windows 应用程序 SDK C# 模板"组件，或重跑脚本 |
| Build 失败 | 把**最后 30 行报错**原样复制发给 AI Agent（或贴到 issue），按提示修复后重跑脚本 |
| 安装中途长时间无输出 | VS Build Tools 静默安装就是这样的，等即可；不要关窗口 |

## 六、跑完之后

按 `EXECUTION_PLAN.md` 的交接协议（§1）：

1. 启动过应用、确认窗口能弹出的项目在 §4 打勾 M0.1；
2. 在 §2 快照更新"当前小任务 / 下一步行动"；
3. 在 §5 追加一条进度日志（记下 Windows 版本号、.NET SDK 版本）；
4. commit：`phase(M0.1): dev environment verified + minimal WinUI 3 project`。

---

## 七、M0.4 硬件实测探针（hardware-probe.ps1）

> 对应 `EXECUTION_PLAN.md` 的 M0.4 Hardware Research，脚本本体是 [`scripts/hardware-probe.ps1`](../scripts/hardware-probe.ps1)。
> **它做什么**：只读检测你这台笔记本的传感器 / HID / ACPI / 厂商接口能力，生成一份 JSON 报告，交给 AI Agent 回填 `docs/HARDWARE_COMPATIBILITY.md`。**全程只读，不上传任何数据，不改系统任何设置。**

### 7.1 脚本放在哪里执行？

和第六节一样：**整个仓库克隆或解压后，脚本保持在 `DuoFlow\scripts\` 原位**，位置不对会找不到输出目录（默认输出到当前目录，所以进仓库根目录跑最省事）。

### 7.2 用什么工具执行？

**Windows 自带的 Windows PowerShell 5.1**（开始菜单搜 "PowerShell"，蓝色图标的那个；不要选 "PowerShell 7 (pwsh)"）。

原因：传感器 API（WinRT）只有在 Windows PowerShell 5.1 里能调用。用 pwsh 7 也能跑，但第一节传感器检测会被跳过（脚本会明确提示），还得用 5.1 重跑一遍。**不需要管理员权限**，但管理员身份能多看到个别设备，建议照第六节习惯用管理员窗口。

### 7.3 怎么执行？（完整命令）

```powershell
# 1. 进入仓库根目录（按你的实际路径调整）
cd C:\Dev\DuoFlow

# 2. 解除当前窗口的脚本运行限制（只对本窗口生效，安全）
Set-ExecutionPolicy -Scope Process Bypass -Force

# 3. 运行硬件探针（30 秒内完成）
.\scripts\hardware-probe.ps1
```

### 7.4 执行后预期看到什么？

正常输出是 7 段（`[FOUND]` 绿色 = 检测到，`[NOT FOUND]` 黄色 = 没检测到，**黄色不是报错**，"没这个硬件"本身就是有效结论）：

```text
==> Section 0: System basics
    OS          : Microsoft Windows 11 专业版 (build 22631)
    Machine     : LENOVO 82XXXXXXXX
    CPU         : 12th Gen Intel(R) Core(TM) ...

==> Section 1: Windows Sensor API (WinRT)
    [NOT FOUND] HingeAngleSensor : GetDefaultAsync() returned null   ← 普通笔记本的预期结果
    [FOUND] Accelerometer supported | acc=(...) g

==> Section 2: HID / PnP sensor devices
    ...
==> Section 3: ACPI layer
    [FOUND] ACPI lid device present: ...        ← 有盖设备很有价值，记下来
    ...
==> Section 4: Vendor WMI interfaces
    ...
==> Section 5: Camera inventory
    ...
==> Section 6: GPU / display
    [FOUND] GPU: Intel(R) Iris(R) Xe Graphics | 31.0.101.xxxx
==> Writing result JSON
    [FOUND] JSON written: C:\Dev\DuoFlow\hardware-probe-result.json
==> SUMMARY
    Sections executed: 7 | errors: 0
    NEXT STEP: send hardware-probe-result.json back to the AI Agent.
```

**成功标志**：最后 SUMMARY 行 `errors: 0`（或只有个别 `[ERROR]`），且仓库根目录出现 `hardware-probe-result.json`。

**做完后**：把 JSON 文件内容（或整段控制台输出截图）发给 AI Agent，它会：① 回填 `docs/HARDWARE_COMPATIBILITY.md` 第 5 节实测行；② 在 `EXECUTION_PLAN.md` 打勾 M0.4；③ 更新快照与日志。

### 7.5 结果怎么判读？（速查表）

| 检测项 | 结果 | 含义 |
| --- | --- | --- |
| HingeAngleSensor | NOT FOUND | 普通笔记本普遍如此（该 API 主要面向双屏设备）→ 传感器 Provider 走 HID/摄像头路线 |
| HingeAngleSensor | FOUND | 少见的高价值硬件，记录 DeviceId，M4 直接接入 |
| Accelerometer / Inclinometer | FOUND | 可作为开合角度的间接信号源（需实测噪声，M4 验证） |
| PnP `Sensor` 类设备 | ≥1 | 存在传感器集线器（Sensor Hub），HID 传感器路线可行性高 |
| ACPI 盖设备 PNP0C0D | FOUND | BIOS 层有盖事件，可探索监听方案（Windows 对普通笔记本不一定暴露） |
| 厂商 WMI 类（Lenovo 等） | FOUND | 记录类名清单，M4 探索厂商私有接口 |

### 7.6 常见问题排查

| 现象 | 原因与处理 |
| --- | --- |
| 提示 "SKIPPED: pwsh 7 ..." | 你用的是 PowerShell 7，换用 Windows PowerShell 5.1 重跑（开始菜单搜 PowerShell，蓝图标） |
| `Get-PnpDevice` 报红字 | 少数精简系统缺 PnpDevice 模块；用管理员窗口重跑；仍失败则把报错发给 AI Agent |
| 某段出现 `[ERROR]` 行 | 单段失败不影响其他段，照常把 JSON 发回，AI Agent 会标注该段待重测 |
| JSON 文件找不到 | 看最后 SUMMARY 上方的 `[FOUND] JSON written: <路径>`，按那个路径找；或确认你在仓库根目录运行的 |
