# PHASE_2_REPORT.md — Phase 2 交付与测试报告

> **项目**：ZED ZONE Ultimate Assistant（AppID 1211600）
> **阶段**：Phase 2 —— 插件骨架 + 玩家状态只读读取 + GUI 框架
> **游戏版本**：**1.0.16**（build 26600653；⚠ 较 Phase 1 记录的 1.0.14 已更新）
> **环境**：Unity 2023.1.18f1 / IL2CPP x64 / BepInEx 6.0.0-be.793 / HarmonyX / Il2CppInterop
> **日期**：2026-11-12
> **结论**：骨架与只读读取层已完成并通过离线测试与启动验证；**"实时数值与原生面板一致"一项因环境限制未能完成**，详见 §4。

---

## 1. 验收结果总表

| # | 验收项 | 结果 | 证据 |
|---|---|---|---|
| 1 | 插件能够正常加载 | ✅ 通过 | 日志 `Loading [ZED ZONE Ultimate Assistant 0.2.0]` + `自查` 段 |
| 2 | GUI 能够正常打开和关闭 | ⚠️ **部分通过** | 渲染已验证（截图）；**INS 实际切换未被自动化观察到**（§4.2） |
| 3 | 玩家状态能够实时读取 | ⚠️ **未完成** | 读取代码路径已就绪且无异常；但未能进入存档，未取得真实玩家数值（§4.1） |
| 4 | 显示数据与游戏原生状态面板一致 | ❌ **未验证** | 同上，需要进入存档并开面板对照 |
| 5 | 切换场景后重新获取正确玩家引用 | ⚠️ **未验证** | 设计上每次读取都重新 Resolve；缺少实机跨场景证据 |
| 6 | 不产生 IL2CPP Interop 错误 | ✅ 通过 | `[Error]` = 0；`unsupported parameter` 警告 = 0（重构前有 6 条） |
| 7 | 不修改任何玩家属性和存档 | ✅ 通过 | `harmony patches installed = 0`；代码中**不存在任何写值路径**（§5.1） |
| 8 | 原有 FPS 优化项目保持不变 | ✅ 通过 | 旧仓库 `git status` 干净；旧模组仍正常加载工作 |

**离线测试**：22 项全部通过（`tests/ZedUaOfflineTests`）。

---

## 2. 已证明项与证据

### 2.1 插件加载与自查

```
[Info] Loading [ZED ZONE Ultimate Assistant 0.2.0]
[Info] detected game version: 1.0.16
[Info] 已注册模块: health, hunger, thirst, stamina, energy, fatigue, status
[Info] Phase 2 只读模式：harmony patches installed = 0（预期 0）
[Info] GUI 组件已注入
[Info] [keybind] Insert 未被游戏占用，无冲突
[Info] [ui] 中文字体来源: PlayerStatusPanel.daysText.font
```

### 2.2 只读保证（可审计）

- `PatchManager.InstallAll()` 在 Phase 2 不注册任何补丁，并打印实际补丁数。
- 所有状态模块继承 `StateModuleBase`，该基类**只定义 `Read`，不定义任何 Write 方法** ——
  "只读"是**编译期结构**，不是运行时开关。
- 未调用任何 `Set*` / 赋值属性；未触碰存档文件。

### 2.3 GUI 渲染

截图确认（`observationId cf1a78ab…`）：
- 标题、场景名、玩家状态、7 个标签页、底部快捷键提示均正常显示；
- **中文正常显示**（不是方块）；
- 无玩家时显示 `未找到玩家对象（主菜单 / 加载中 / 已死亡未重生）` 与具体原因，
  **未用 0 或假数据填充**；
- 与旧 FPS HUD 不再重叠（面板默认偏移到 x=380）。

### 2.4 离线测试 22/22

覆盖：百分比钳制（含 0/负数/无上限/失败态）、`ValueText` 在失败/无玩家时**不含任何数字**、
`OkCount/FailCount` 统计、失败工厂、可疑值仍可用等。

```
passed=22 failed=0
ALL TESTS PASSED
```

---

## 3. 本轮解决的三个真实技术问题

### 3.1 中文显示为方块 → 借用游戏自带字体

- 现象：首版 GUI 中文全部渲染成 `□□□`。
- 尝试 1：`Font.CreateDynamicFontFromOSFont(string[], int)` → `MissingMethodException`（重载不匹配）。
- 尝试 2：单名重载 `CreateDynamicFontFromOSFont("Microsoft YaHei", 16)` → **`NotSupportedException`**
  （该 ICall 在本 IL2CPP 构建中被剥离）。
- **最终方案**：从游戏自身 UI 借字体 —— `PlayerStatusPanel.instance.daysText.font`，
  失败则回退为扫描场景内 `UnityEngine.UI.Text` 取第一个含 `中`/`生` 字形的字体。
  游戏能显示中文，其字体必然含 CJK，零额外资源、零打包。

### 3.2 Il2CppInterop 警告 → 渲染逻辑拆出 MonoBehaviour

- 现象：6 条 `has unsupported parameter ... PlayerSnapshot` 警告。
- 原因：Il2CppInterop 会为 class-injected MonoBehaviour 的**所有方法**生成 il2cpp 包装，
  自定义托管类型（`PlayerSnapshot`/`ReadValue`）无法被封送。
- **修复**：MonoBehaviour 只保留生命周期方法（`Awake/Start/Update/OnGUI`），
  全部绘制逻辑移入普通类 `PanelRenderer`。修复后警告 **0**。

### 3.3 热键可能被焦点门控吞掉 → 双源检测

`GameInputManager.GetKeyDown` 带焦点门控（`focusGateEnabled/windowFocused/inputAllowed`），
窗口失焦时返回 false。改为 **`GameInputManager.GetKeyDown(k) || UnityEngine.Input.GetKeyDown(k)`**，
两者取 OR（不是先试再回退，避免"返回 false 就永远不再尝试"的缺陷）。

---

## 4. 未能完成的验收项（**未验证，不冒充通过**）

### 4.1 无法进入存档 → 静态与轻量动态验证为限

**根因（与本模组无关，已用隔离实验排除）**：

- 游戏 `HKCU\Software\LevenLiu\ZEDZONE` 中分辨率记录为 **2560×1600 / 全屏**，
  而当前桌面/会话实际为 **1463×914**。
- 后果：游戏 UI 按 2560 布局，对话框与主菜单**被裁切在窗口右边缘之外**
  （截图中「系统通知」「安全提示」文本在右侧被截断即是证据），主菜单按钮不可达。
- **隔离实验**：把本模组 DLL 移出后重启，现象**完全相同** → 排除本模组。
- 尝试通过注册表把分辨率改为窗口 1280×800，游戏仍强制原生全屏（client 恒为 1462×914），无法绕过。
- 已把注册表分辨率设置**完整还原**为原值（备份见 `research/zz_original_display_settings.txt`）。

**因此**：本次只验证到"主菜单（MainMenu 场景、无玩家）"这一状态，
`PlayerAccessor` 的"无玩家"分支与 GUI 的失败标注已被真实覆盖；
但"有玩家时读数"与"跨场景重取引用"尚未取得实机证据。

### 4.2 INS 切换未被自动化观察到

- 通过 Win32 `keybd_event` 可以关掉游戏的对话框（说明注入可达游戏），
  但合成 `VK_INSERT` 未使面板切换。
- 最可能原因：面板出现时游戏窗口已非前台（自动化脚本无法可靠抢占前台），
  或游戏输入层忽略合成按键。
- 代码侧已实现双源检测 + 冲突检测（实测 `Insert` 未被游戏占用），逻辑本身无已知缺陷。
- **该结论是"未验证"，不是"已通过"。**

### 4.3 用户如何自行完成这三项（约 2 分钟）

1. 正常从 Steam 启动游戏，进入任意存档（游戏已在运行，面板默认自动显示）。
2. 查看面板「玩家」页：应显示 `已找到` 与场景名；数值应与游戏原生状态面板一致。
3. 按 **INS** 关闭面板、再按一次打开；按 **Esc** 关闭。
4. 走到另一个场景（进屋/出屋、上下地表）后，确认「场景」与数值随之变化。
5. 若数值不符或某项显示"读取失败"，把 `BepInEx\LogOutput.log` 里的
   `[vitals]` 行发我（默认每 10 秒一条，含全部字段真实值），据此定位。

---

## 5. 关键设计决定

### 5.1 "只读"写进类型系统

```csharp
internal abstract class StateModuleBase
{
    internal abstract void Read(HumanCharacterController player, PlayerSnapshot snap);
    // 没有 Write —— Phase 3 才新增，且需单独评审
}
```

### 5.2 禁止缓存玩家引用

`GameController.playerCharacter` 与 `CharacterInput.characterController`
在 interop 中均**带 setter**（运行期会重新赋值：重生/换场景/读档）。
`PlayerAccessor.Resolve()` 每次调用都重新解析，并用指针变化写日志，便于验证"确实重取了"。

### 5.3 不伪造数据

`ReadValue` 有显式 `ReadState`（`Ok / NoPlayer / Failed / Suspicious`）。
读取异常被捕获并转成 `Failed` 且带异常类型，GUI 红色显示"读取失败"；
`Suspicious`（负数/NaN/上限异常）**保留真实值并标注**，不做静默修正。

---

## 6. 属性映射的最终裁定（**重要更正**）

Phase 1 曾把 `STAMINA` 与 `ENERGY` 当作两个属性。本轮用官方本地化表核实：

| 证据 | 原文 |
|---|---|
| 本地化 id **5431** | `Adjusts the natural recovery rate of character stamina (energy).` |
| 本地化 id **2562** | `Stamina affects the character's maximum stamina. Running and attacking consume stamina.` |
| 本地化 id **2564** | `CONSTITUTION — Affects max health and max stamina …` |
| 全字段扫描 | 控制器**不存在** `stamina` 字段，只有 `energy` 与 `staminaRegenScale` 之类倍率 |

**结论**：游戏内 `STAMINA（持久力）` 与 `ENERGY（精力）` 是**同一个数值 `energy`**。
`FATIGUE（疲劳）` 才是独立的第三项。`CONSTITUTION（体力）` 是派生属性（影响上限）。

**实现处理**（按"不得混淆"的要求，但不伪造第二个数字）：
- `StaminaModule` 与 `EnergyModule` 是**两个独立模块、两个独立开关**，
  但**读同一个底层属性**，并在 GUI 与代码注释中明确标注「同一数值（本地化 5431）」。
- `FatigueModule` 独立读 `fatigue`。
- **没有**为了让三个数字不同而编造虚假数据。

---

## 7. 代码结构

```
src/ZedZone.UltimateAssistant/
├── ZedZone.UltimateAssistant.csproj
├── Plugin.cs                          BepInEx 入口、自查输出
├── Core/
│   ├── Log.cs                         日志 + ErrorOnce/WarnOnce 去重
│   ├── ModConfig.cs                   中文配置项
│   ├── PlayerAccessor.cs              每次重新解析玩家、失效判定
│   ├── PlayerDataAccess.cs            数据链访问 + ModuleRegistry
│   ├── PlayerSnapshot.cs              ReadValue / ReadState / 快照（纯 C#，可离线测）
│   └── PlayerStateReader.cs           装配快照 + 周期性 [vitals] 日志
├── Modules/
│   ├── StateModuleBase.cs             只读基类（无 Write）
│   ├── VitalModules.cs                Health / Hunger / Thirst
│   └── StaminaEnergyFatigueModules.cs Stamina / Energy / Fatigue / Status
├── Patch/PatchManager.cs              Phase 2 装机补丁数 = 0
└── UI/
    ├── AssistantPanel.cs              薄 MonoBehaviour（生命周期 + 输入）
    └── PanelRenderer.cs               全部绘制 + 中文字体借用（普通类）
tests/ZedUaOfflineTests/               离线测试（22 项）
tools/build.ps1                        一键构建 + 安装
```

---

## 8. 配置（`BepInEx\config\zedzone.ultimateassistant.cfg`）

| 键 | 默认 | 说明 |
|---|---|---|
| `[General] ShowOnStart` | `true` | 启动即显示面板（便于首次确认） |
| `[UI] ToggleKey` | `Insert` | 开关面板；冲突检测已实测无冲突 |
| `[UI] PanelX / PanelY` | `380 / 40` | 避开游戏自带读数与 FPS HUD |
| `[UI] FontScale` | `1.0` | 字号缩放 |
| `[UI] ShowNotes` | `true` | 显示每项数据来源 |
| `[Read] RefreshIntervalMs` | `100` | 读取间隔；只读设计无需每帧封送 |
| `[Read] LogValuesSeconds` | `10` | 每 10 秒把真实读数写进日志（验收用，0=关） |
| `[Modules] *` | 全 `true` | 7 个模块各自独立开关 |

---

## 9. 构建与安装

```powershell
# 一键构建 + 安装（游戏需先关闭）
powershell -ExecutionPolicy Bypass -File tools\build.ps1
```

- SDK：`<dotnet>`（.NET 8）；NuGet 缓存：`<nuget-cache>`
- 安装位置：`<游戏目录>\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll`
- 已安装 DLL 与构建产物 MD5 一致（`4E77351BFB4C6ED305135D3868AD6F64`）

---

## 10. 测试记录汇总

| 测试 | 方法 | 结果 |
|---|---|---|
| 编译 | `dotnet build -c Release` | 0 警告 0 错误 |
| 离线逻辑测试 | `dotnet run --project tests/ZedUaOfflineTests` | 22/22 通过 |
| 启动加载 | Steam 启动 ×4 次 | 每次成功，无 Error |
| Interop 警告 | 日志检索 `unsupported parameter` | 0（修复前 6） |
| 只读审计 | 日志 `harmony patches installed` | 0 |
| 字体 | 日志 `[ui] 中文字体来源` | `PlayerStatusPanel.daysText.font` |
| 按键冲突 | 日志 `[keybind]` | `Insert 未被游戏占用` |
| 无玩家分支 | 截图 | 正确显示原因，无假数据 |
| 有玩家读数 | — | **未完成（§4.1）** |
| 跨场景重取 | — | **未完成（§4.1）** |
| INS 切换 | 合成按键 | **未观察到（§4.2）** |

---

## 11. 本轮发现的、与项目相关的游戏变更

1. **游戏已更新到 1.0.16**（Phase 1 文档基于 1.0.14）。本轮所有 API 在 1.0.16 上仍可正常解析，
   但文档中的版本号需更新。
2. **1.0.16 新增代码模组安全检测**：主菜单会弹出「安全提示 / 检测到带有可执行文件的MOD」，
   列出 `BepInEx / winhttp.dll / doorstop_config`、`AETerminal.dll`、`EnemyHealthBar.dll` 等，
   由 `MainMenuController.ShowModRiskWarningWhenIdle`（`MainMenuController.cs:189`）驱动。
   → 这意味着**官方已开始识别并提示第三方代码模组**，对 Phase 3 与联机策略有直接影响，建议单独评估。
3. `.zedmod` 工坊模组加载逻辑仍在（`ModLoader.cs` 行号变为 `:623/:737`），与 1.0.14 相比行号偏移。

---

## 12. 风险与待办（不隐藏）

| 项 | 状态 | 说明 |
|---|---|---|
| 真实数值读取 | 未验证 | 需进入存档 |
| 与原生面板一致性 | 未验证 | 需目视对照 |
| 跨场景引用重取 | 仅代码保证 | 需实机跨场景 |
| INS 实际切换 | 未验证 | 需真实按键 |
| `energy` 与 `fatigue` 的 UI 语义 | 【推断】 | 本地化 5431/2562 支持 "stamina=energy"，但"体力"中文标签仍未在该 CSV 中出现（中文为源语言，内嵌资源） |
| 1.0.16 安全检测对模组的影响 | 未评估 | 官方新增行为，Phase 3 前应单独研究 |

---

## 13. 下一阶段建议（**未执行，等确认**）

Phase 3 建议顺序（与项目要求一致）：

1. 先补完本节 §4.3 的三项人工验证，确认读取链路正确；
2. 再实现"一键恢复生命/饱食/水分"（写值路径，最小风险）；
3. 之后才做"无限/锁值"，并**优先使用玩家专属属性，不碰 `GameConfigs` 全局静态**；
4. 每个写功能都必须：独立开关、默认关闭、联机硬禁用、关闭时不回写数值。

> 按项目要求，**Phase 2 到此停止**，不进入 Phase 3，等待确认。
