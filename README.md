# ZED ZONE Assistant（SORA助手）

<p align="right"><b>简体中文</b> | <a href="README_EN.md">English</a></p>

[![CI](https://github.com/SORA123k/ZED-ZONE-Assistant/actions/workflows/ci.yml/badge.svg)](https://github.com/SORA123k/ZED-ZONE-Assistant/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/SORA123k/ZED-ZONE-Assistant)](https://github.com/SORA123k/ZED-ZONE-Assistant/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

《ZED ZONE / 隔离区-丧尸末日生存》的**单人离线**辅助模组。基于 **BepInEx 6 (IL2CPP) + HarmonyX**，提供一套 ClickGUI 风格的游戏内菜单（默认 `INSERT` 打开），以及耐久、弹药、防护、移动、视觉等一整套辅助功能。

> **仅供单人离线使用。** 检测到联机状态时，所有**修改类功能**（弹药 / 耐久 / 过热 / 卡壳 / 生命 / 体力 / 饱食 / 水分 / 自动补充 / 移动）与 **ESP** 都会被拒绝启用并主动关闭（fail-closed）。请勿用于多人游戏，也不要在联机模式下尝试启用。

---

## 目录

- [新手教程](GUIDE.md)（萌新必看）
- [安装指南](INSTALL.md) / [配置项参考](docs/CONFIG.md) / [版本兼容](docs/COMPATIBILITY.md)
- [文档地图](docs/README.md)
- [功能一览](#功能一览)
- [环境要求](#环境要求)
- [安装（普通用户）](#安装普通用户)
- [使用说明](#使用说明)
- [配置文件](#配置文件)
- [从源码构建](#从源码构建)
- [项目结构](#项目结构)
- [已知限制](#已知限制)
- [免责声明](#免责声明)
- [参与开发](#参与开发)
- [许可](#许可)

---

## 功能一览

菜单分为 **Combat / Player / Visuals / Settings** 四个分类，每个功能都有独立开关、独立配置和可绑定的快捷键。

### 战斗（Combat）

| 功能 | 说明 |
|---|---|
| 无限弹药 Infinite Ammo | 射击不消耗备用弹药（仅本地玩家） |
| 无需换弹 No Reload | 弹匣为 0 也能开火；按 R 仍走原生换弹 |
| 不会卡壳 No Jam | 阻止卡壳判定，并清除已有的 `MalfunctionFlag` |
| 工具/近战耐久 Tool / Melee Durability | 工具、近战、装备耐久不消耗（只增不减） |
| 枪械耐久 Gun Durability | 枪械耐久保持在最大值（写入 `ItemData.properties[0]`） |
| 取消过热 No Overheat | 自己枪械温度恒为 0（不影响 NPC） |
| 自动补充材料 Auto Supply | 制造/修复时视为材料充足；岛屿船可强制完成建造 |

### 玩家（Player）

| 功能 | 说明 |
|---|---|
| 不会扣血 No HP Loss | 受到伤害时生命值不减少 |
| 不会扣体力 No Stamina Loss | 奔跑/跳跃等不消耗体力 |
| 不会扣饱食度 No Hunger Loss | 饱食度不减少（进食仍可增加） |
| 不会扣水分 No Thirst Loss | 水分不减少（饮水仍可增加） |
| 强制奔跑 Always Sprint | 停火自动奔跑；开火时自动取消奔跑，避免"当前状态无法开枪" |
| 移动加速 Movement Speed | 1.0x – 20.0x，含翻滚加速 |
| 一键恢复（按钮） | 生命 / 精力体力 / 饱食 / 水分，点一次恢复一次 |

### 视觉（Visuals）

| 功能 | 说明 |
|---|---|
| 僵尸 ESP | 标记已加载僵尸（名称/距离/方框，含视野外屏幕边缘指示） |
| 地图全亮 Full Bright | 夜间提升场景亮度（URP 2D 全局光 + 环境光），不改时间/天气/AI |

### 设置（Settings）

- **主题**：默认深色 / 黑金 / 蓝色科技 / 紫色 / 青绿 / 白色浅色 / 红黑（7 套）
- **字体大小**：12–24 px，全界面实时生效
- **语言**：简体中文 / English / 繁體中文，切换即时生效
- **界面缩放**、动画开关、打开菜单时阻止游戏操作、显示肢体状态
- **快捷键**：菜单键可多绑定（Insert 永远有效）；每个功能可绑定一个独立按键
- **音效**：开关功能时播放提示音（可在配置中替换）
- **灵动岛**：屏幕顶部胶囊提示，实时显示"功能 X 已启用/已关闭"

---

## 环境要求

| 项 | 要求 |
|---|---|
| 游戏 | ZED ZONE **1.0.17**（Steam AppID `1211600`） |
| 引擎 | Unity 2023.1.18f1 / IL2CPP **x64** |
| Mod 框架 | **BepInEx 6**（IL2CPP）。推荐用 [Steam 创意工坊的代码模组加载器](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258) 一键安装；也可手动装 BleedingEdge 6.0.0-be.* |
| 运行时 | .NET 6.0（BepInEx 自带） |
| 系统 | Windows 10/11 x64 |

> 游戏更新后，`Assembly-CSharp` 的 IL2CPP 签名可能变化。启动日志会打印"版本不匹配"警告，
> 处理步骤见 [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)。

---

## 安装（普通用户）

完整步骤与排错见 [INSTALL.md](INSTALL.md)，萌新请看 [GUIDE.md](GUIDE.md)。

### 前置：装 Mod 加载器（BepInEx 6）

两条路任选其一：

**A. Steam 创意工坊（推荐新手）**
[ZED ZONE 代码模组加载器 (BepInEx IL2CPP / DLL Mods)](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)
→ 订阅 → 打开 `steamapps\workshop\content\1211600\3834614258\` → 双击 `install.cmd`
（离线、自动定位游戏目录；游戏在 `Program Files` 时用管理员运行）→ 从 Steam 启动游戏
→ 主菜单左上角出现 `ZED MODS: N code mod(s) loaded` 即成功。

> 该加载器由创意工坊作者 **star fox** 提供，安装步骤整理自其创意工坊页面（详见 [GUIDE.md](GUIDE.md) 第 3 步）。

**B. 手动安装 BepInEx 6（IL2CPP BleedingEdge）**
到 `BepInEx/BepInEx` 的 Releases 下 `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.*`，解压到游戏根目录，
启动一次游戏让 `BepInEx\interop\` 生成。

### 安装本模组

1. 打开 **Releases**，下载最新 `ZedZone.UltimateAssistant.dll`（可选：核对 Release 说明里的 SHA256）；
2. 放到：
   ```
   <游戏目录>\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
   ```
   （也可以放到 `BepInEx\plugins\`，**二选一**，不要两份）
3. 从 Steam 启动游戏 → 进入存档 → 按 **INSERT**。

模块化安装的结构：

```
<游戏目录>\
  Mods\
    ZedZone.UltimateAssistant\
      ZedZone.UltimateAssistant.dll
  BepInEx\
    config\
      zedzone.ultimateassistant.cfg      ← 首次运行自动生成
```

### 安装后自检

查看 `<游戏目录>\BepInEx\LogOutput.log`，应能看到：

```
[Info   :ZED ZONE Ultimate Assistant] ZED ZONE Ultimate Assistant v1.0.1 loading
[Info   :ZED ZONE Ultimate Assistant] detected game version: 1.0.17
[Info   :ZED ZONE Ultimate Assistant] [patches] 启动时已挂载补丁数 = 0（功能开启后按需增加）
[Info   :ZED ZONE Ultimate Assistant] [ui] ClickGUI 已就绪（INSERT 开关，当前关闭）
```

补丁数是 0 是**正常**的：功能没开就完全不装补丁（IL2CPP 下的必要策略）。按 `INSERT` 能呼出菜单即安装成功。

---

## 使用说明

| 操作 | 说明 |
|---|---|
| `INSERT` | 打开 / 关闭菜单（永久有效，不可解绑） |
| 鼠标左键 | 点击卡片任意位置或右侧开关切换功能 |
| 拖动标题栏 | 移动菜单位置（自动保存） |
| 滚轮 | 在 Player / Settings 等长页面滚动 |
| 功能快捷键 | 每个功能卡片右侧的按键按钮：点击后按任意键绑定；按 `ESC` 清空 |
| 菜单多绑定 | 设置 → 控制 → 菜单按键：Insert 之外可再绑多个键 |

菜单打开时会：
- 阻止角色操作（禁用 `CharacterInput`）
- 禁用游戏 UI 的 `EventSystem` / `GraphicRaycaster`，防止点击穿透
- 强制显示鼠标指针

关闭菜单后全部自动还原。

---

## 配置文件

位置：

```
<游戏目录>\BepInEx\config\zedzone.ultimateassistant.cfg
```

- 菜单里改的项**立即生效**并写回文件；手动编辑文件后**重启游戏**生效。
- 新装用户的默认值：**所有修改类功能默认关闭**，诊断类默认关闭。
- **完整 85 项（69 配置 + 16 快捷键）见 [docs/CONFIG.md](docs/CONFIG.md)**，这里只列最常用的：

| 段 / 键 | 默认 | 作用 |
|---|---|---|
| `[General] Enabled` | `true` | 模组总开关 |
| `[UI] ToggleKey` | `Insert` | 菜单快捷键（Insert 永远有效） |
| `[Menu] MenuKeys` | 空 | 额外菜单键，逗号分隔 |
| `[Menu] Theme` / `Language` / `FontSize` | `Dark` / `zhCN` / `14` | 主题 / 语言 / 字号 |
| `[Menu] BlockGameInput` | `true` | 菜单打开时阻止角色操作 |
| `[Features] *` | 全 `false` | 修改类功能开关 |
| `[Restore] EnableRestore` | `true` | 一键恢复总开关 |
| `[Hotkeys] *` | `None` | 每个功能的独立快捷键 |
| `[Diagnostics] *` | 全 `false` | 只读诊断（排查时再开） |

---

## 从源码构建

### 前置

- .NET SDK 6.0+（`dotnet --version` 可用）
- 已安装 ZED ZONE + BepInEx 6（需要 `BepInEx/interop/` 里的 interop 程序集，否则无法编译）

### 指定游戏目录（三选一）

```powershell
# 1) 环境变量
$env:ZEDZONE_GAME_ROOT = "D:\SteamLibrary\steamapps\common\ZED ZONE"

# 2) 命令行参数
dotnet build src/ZedZone.UltimateAssistant -c Release -p:GameRoot="D:\...\ZED ZONE"

# 3) 一键脚本（会自动扫描 Steam 库目录，找不到再要求你传 -GameDir）
pwsh tools/build.ps1 -SkipInstall     # 只编译
pwsh tools/build.ps1                  # 编译 + 复制到 Mods\
```

没设置游戏目录时，构建会给出可读报错（而不是一堆"找不到类型"）。

### 产物

```
src/ZedZone.UltimateAssistant/bin/ZedZone.UltimateAssistant.dll
```

### 测试与检查

```powershell
# 离线单元测试（纯逻辑，不需要游戏）
dotnet run --project tests/ZedUaOfflineTests/ZedUaOfflineTests.csproj -c Release

# 文档相对链接检查（CI 也会跑）
python3 tools/ci/check_links.py
```

> `tests/ZedUaOfflineTests` 是控制台断言器，不是 xUnit 工程，所以是 `dotnet run` 而不是 `dotnet test`。
> 它只覆盖与引擎无关的纯逻辑（`PlayerSnapshot` / `RangeSelection`），补丁逻辑靠实机回归。

### 解决方案文件

`ZedZone.Assistant.sln` 同时包含插件与测试工程。注意：**插件工程必须能引用到游戏 interop 才能编译**，
所以在没有游戏的机器/CI 上只能单独构建测试工程（CI 就是这么做的）。

---

## 项目结构

```
Plugin.cs                   入口：配置绑定 / 模块注册 / 补丁安装 / 面板注入 / 版本检查
Directory.Build.props       版本号唯一来源（+ GameRoot 逻辑）
ZedZone.Assistant.sln       解决方案（插件 + 测试）

src/ZedZone.UltimateAssistant/
  Core/
    ModConfig.cs            全部配置项
    Features.cs             功能开关 + 联机门禁 + 补丁生命周期
    PlayerAccessor.cs       玩家解析（不缓存）
    PlayerWriteGate.cs      统一写入门（恢复功能 + fail-closed 联机判定）
    StatRange.cs / RangeSelection.cs   动态上限读取
    RestoreService.cs       一键恢复
    DurabilityLock.cs       耐久锁定（物品/枪械）
    WeaponUtil.cs           武器与物品工具（卡壳清理、原生修复）
    ZombieEsp.cs            僵尸 ESP
    FullBright.cs           地图全亮（URP 2D 全局光）
    RollSpeed.cs            翻滚加速
    AutoSupply.cs           自动补充材料 / 强制完成建造
    HotkeyManager.cs        功能快捷键 + 音效 + 灵动岛推送
    UiSound.cs              WAV 解析与播放
    Diagnostics.cs          只读诊断（Buff 表 / 全局开关 / 联机标志）
    *Probe.cs / *Diag.cs    只读探针（默认关闭，排查时开）
  Patch/
    PatchManager.cs         补丁计数 + 卸载兜底
    WeaponPatches.cs        弹药/换弹/卡壳/枪械耐久/温度
    PlayerPatches.cs        伤害/体力/饱食/水分/材料
    PlayerMovePatches.cs    强制奔跑 / 移动加速
  UI/
    AssistantPanel.cs       面板宿主（热键、输入守卫、绘制调度）
    ClickGui.cs             主界面（分类、滚动、滑条、卡片）
    ThemeManager.cs         7 套主题
    DynamicIsland.cs        灵动岛
    InputGuard.cs           输入阻断 / 鼠标 / 防点击穿透
    Localization.cs         三语本地化

tests/ZedUaOfflineTests/    离线单元测试（控制台断言器）
tools/
  build.ps1                 构建（自动找游戏目录）+ 可选安装
  release.ps1               发版：改版本 → 构建 → 打 tag → Release（含 SHA256）
  ci/check-version.sh       版本号一致性检查
  ci/check_links.py         Markdown 链接检查
docs/                       文档地图见 docs/README.md
  README.md                 文档索引
  CONFIG.md                 全部配置项
  COMPATIBILITY.md          游戏版本适配
  API_PATCH_MAPPING.md      补丁 → 游戏方法对照
  reference/                游戏机制研究（数据/生存/生命/状态/联机）
  internal/                 开发过程报告（历史存档，不必读）
.github/                    CI、Issue 模板、PR 模板
```

---

## 已知限制

- **仅离线单人**：联机时修改类功能与 ESP 会自动拒绝并关闭（含进入会话后才检测到的情况）。
  这个保护只作用于本模组自身行为，**不能**保证联机服务端的判定。
- **游戏版本绑定**：基于 1.0.17 的 IL2CPP interop 签名；不匹配时只会告警不会阻断，
  功能可能失效，适配步骤见 [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)。
- **自动补充材料**：对普通制造/建筑修复有效；"岛屿逃生船"这类特殊建造对象走独立路径，本模组通过强制完成建造处理，可能影响任务流程，请自行备份存档。
- **地图全亮**：依赖 URP 2D 全局光与环境光；若游戏改用自定义压暗方式，效果会减弱。
- **耐久锁定**：枪械耐久写入 `ItemData.properties[0]`，该值会随存档保存。
- **AI 编写的代码**：见下方免责声明；边界情况覆盖不全，欢迎反馈。

---

## 免责声明

本项目为**单机游戏辅助工具**，仅用于个人离线存档。使用前请自行备份存档（`%USERPROFILE%\AppData\LocalLow\LevenLiu\ZEDZONE\GameData`）。

作者不对因使用本模组导致的存档损坏、游戏异常、封禁或任何其他损失负责。请勿在联机/多人环境使用。

本项目未包含任何游戏原始资源；所有实现均基于对游戏公开 IL2CPP 元数据的只读分析与运行时 Harmony 补丁。
仓库不包含游戏原始资源、游戏二进制或运行日志（`research/`、`Diagnostics/` 已加入 `.gitignore`）。`docs/internal/diagnostics/` 保留了少量**只读**逆向分析记录（反汇编片段与推导），仅用于单机离线兼容性研究，请勿用于联机或再分发；范围说明见 [docs/internal/README.md](docs/internal/README.md)。

### 关于本项目的开发方式（请务必阅读）

本项目是 **vibe coding** 产物：绝大部分代码由 AI 编写，由人类提出需求、验证与把关。

这意味着：

- 代码风格、注释、结构可能不够"工程化"，也可能存在 AI 特有的疏漏；
- 功能经过实机测试的范围并不覆盖所有边界情况；
- 某些实现是"能跑通"优先，而不是"最优解"。

因此，如果你发现 Bug、逻辑缺陷、崩溃或任何让你不舒服的地方，**欢迎友善指出**：
提 Issue 时附上日志与复现步骤即可，不需要客气，也不必担心被责怪——指出问题本身就是对这个项目最大的帮助。

同样地，欢迎直接提交 PR 帮我修正这些由 AI 留下的坑。

---

## 参与开发

欢迎一起维护本项目（代码 / 翻译 / 文档 / 测试反馈都算）。

- 文档地图：[docs/README.md](docs/README.md)（先看这个，别在 40 多篇旧报告里迷路）
- English docs: [GUIDE_EN.md](GUIDE_EN.md) ｜ [INSTALL_EN.md](INSTALL_EN.md) ｜ [DEVELOPMENT_EN.md](DEVELOPMENT_EN.md) ｜ [CONTRIBUTING_EN.md](CONTRIBUTING_EN.md)
- 开发说明：[DEVELOPMENT.md](DEVELOPMENT.md)（架构、补丁规范、新增功能检查清单）
- 贡献流程：[CONTRIBUTING.md](CONTRIBUTING.md)（分支、PR 检查清单、**如何获得维护者权限**）
- AI 助手规则：[AGENTS.md](AGENTS.md)（用 AI 改代码前必读的硬约束）
- 配置项：[docs/CONFIG.md](docs/CONFIG.md) ｜ 适配：[docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)
- 发布流程：维护者执行 `pwsh tools/release.ps1 -Version x.y.z`，脚本会自动改版本（两处）→ 构建 → 校验版本一致性 → 打 tag → 推送 → 创建 Release 并附 DLL + SHA256
- Bug 反馈请使用仓库的 Issue 模板；安全问题走 [SECURITY.md](SECURITY.md)

> 想成为长期维护者：先提交 1–2 个 PR，然后在 Issue 里说明，仓库所有者会把加入 Collaborators（写权限）。

---

## 许可

[MIT](LICENSE) © 2026 SORA123k
