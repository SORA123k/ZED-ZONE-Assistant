# ZED ZONE Ultimate Assistant（SORA助手）

《ZED ZONE / 隔离区-丧尸末日生存》的**单人离线**辅助模组。基于 **BepInEx 6 (IL2CPP) + HarmonyX**，提供一套 ClickGUI 风格的游戏内菜单（默认 `INSERT` 打开），以及耐久、弹药、防护、移动、视觉等一整套辅助功能。

> **仅供单人离线使用。** 联机状态下所有状态修改类功能会自动禁用（fail-closed）。请勿用于多人游戏，也不要在联机模式下尝试启用。

---

## 目录

- [功能一览](#功能一览)
- [环境要求](#环境要求)
- [安装（普通用户）](#安装普通用户)
- [使用说明](#使用说明)
- [配置文件](#配置文件)
- [从源码构建](#从源码构建)
- [项目结构](#项目结构)
- [已知限制](#已知限制)
- [免责声明](#免责声明)
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
| Mod 框架 | **BepInEx 6**（IL2CPP，BleedingEdge 6.0.0-be.*） |
| 运行时 | .NET 6.0（BepInEx 自带） |
| 系统 | Windows 10/11 x64 |

> 游戏更新后，`Assembly-CSharp` 的 IL2CPP 签名可能变化。若加载失败，请先查看 `BepInEx/LogOutput.log` 里的报错，并在本仓库提交 Issue。

---

## 安装（普通用户）

### 方式一：下载 Release（推荐）

1. 打开右侧 **Releases**，下载最新版本的 `ZedZone.UltimateAssistant.dll`；
2. 确认游戏目录已有 BepInEx 6：
   ```
   <游戏目录>\
     BepInEx\
       core\
       interop\
       plugins\        ← 或使用 Mods\（本模组两者都支持）
     winhttp.dll
     ZEDZONE.exe
   ```
   若没有，请先按 [INSTALL.md](INSTALL.md) 安装 BepInEx 6（IL2CPP）。
3. 把 `ZedZone.UltimateAssistant.dll` 放到：
   ```
   <游戏目录>\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
   ```
   （也可以放到 `BepInEx\plugins\` 下，二选一）
4. 启动游戏，进入存档，按 **INSERT** 打开菜单。

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

### 方式二：从源码构建

见 [从源码构建](#从源码构建)。

### 安装后自检

启动游戏后查看 `<游戏目录>\BepInEx\LogOutput.log`，应能看到：

```
[Info   :ZED ZONE Ultimate Assistant] ZED ZONE Ultimate Assistant v1.0.0 loading
[Info   :ZED ZONE Ultimate Assistant] [features] ...
[Info   :ZED ZONE Ultimate Assistant] [weapons] 补丁安装完成，成功 N 个
```

按 `INSERT` 能呼出菜单即安装成功。

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

主要配置段：

```ini
[General]
Enabled = true
ShowOnStart = false
RefreshMs = 100
[UI]
ToggleKey = Insert
PanelX = 340
PanelY = 40
[Features]
InfiniteAmmo = false
NoReload = false
NoJam = false
ItemDurabilityLock = true
WeaponDurability = false
NoOverheat = false
AutoSupply = false
NoHpLoss = false
NoStaminaLoss = false
NoHungerLoss = false
NoThirstLoss = false
AlwaysSprint = false
MoveSpeed = false
MoveSpeedMultiplier = 1
FullBright = false
ZombieEsp = false
PerformanceMode = false
[Hotkeys]
infinite_ammo = None
...
[Menu]
Theme = Dark
FontSize = 14
Language = zhCN
MenuKeys =
Animations = true
Scale = 1.0
```

修改配置后重新打开菜单或重启游戏生效。

---

## 从源码构建

### 前置

- .NET SDK 6.0+（`dotnet --version` 可用）
- 已安装 ZED ZONE + BepInEx 6（需要 `BepInEx/interop/` 里的 interop 程序集）

### 构建

```bash
cd src/ZedZone.UltimateAssistant
dotnet build -c Release
```

产物：

```
src/ZedZone.UltimateAssistant/bin/ZedZone.UltimateAssistant.dll
```

### 一键构建 + 安装

```powershell
# 自动构建并复制到 Mods 目录（会拒绝在游戏运行时安装）
pwsh tools/build.ps1
```

脚本会把 DLL 复制到：

```
<游戏目录>\Mods\ZedZone.UltimateAssistant\
```

> `GameRoot` 默认为 `<游戏目录>\`，可在 `ZedZone.UltimateAssistant.csproj` 里改，或构建时传 `-p:GameRoot=...`。

---

## 项目结构

```
src/ZedZone.UltimateAssistant/
  Plugin.cs                 入口：配置绑定 / 模块注册 / 补丁安装 / 面板注入
  Core/
    ModConfig.cs            全部配置项
    Features.cs             功能开关与补丁生命周期
    PlayerAccessor.cs       玩家解析（不缓存）
    PlayerWriteGate.cs      统一写入门（恢复功能）
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
    Localization.cs         三语本地化
    Diagnostics/*           只读诊断与探针（耐久分析等）
  Patch/
    WeaponPatches.cs        弹药/换弹/卡壳/枪械耐久/温度
    PlayerPatches.cs        伤害/体力/饱食/水分/材料
    PlayerMovePatches.cs    强制奔跑 / 移动加速
  UI/
    AssistantPanel.cs       面板宿主（热键、输入守卫、绘制调度）
    ClickGui.cs             主界面（分类、滚动、滑条、卡片）
    ThemeManager.cs         7 套主题
    DynamicIsland.cs        灵动岛
    InputGuard.cs           输入阻断 / 鼠标 / 防点击穿透
docs/                       各阶段研究、验证与逆向报告
tests/                      离线单元测试
tools/build.ps1             构建安装脚本
```

---

## 已知限制

- **仅离线单人**：联机时所有状态修改功能自动禁用。
- **游戏版本绑定**：基于 1.0.17 的 IL2CPP interop 签名；游戏大版本更新后可能需要重新适配。
- **自动补充材料**：对普通制造/建筑修复有效；"岛屿逃生船"这类特殊建造对象走独立路径，本模组通过强制完成建造处理，可能影响任务流程，请自行备份存档。
- **地图全亮**：依赖 URP 2D 全局光与环境光；若游戏改用自定义压暗方式，效果会减弱。
- **耐久锁定**：枪械耐久写入 `ItemData.properties[0]`，该值会随存档保存。

---

## 免责声明

本项目为**单机游戏辅助工具**，仅用于个人离线存档。使用前请自行备份存档（`%USERPROFILE%\AppData\LocalLow\LevenLiu\ZEDZONE\GameData`）。

作者不对因使用本模组导致的存档损坏、游戏异常、封禁或任何其他损失负责。请勿在联机/多人环境使用。

本项目未包含任何游戏原始资源；所有实现均基于对游戏公开 IL2CPP 元数据的只读分析与运行时 Harmony 补丁。

---

## 许可

[MIT](LICENSE) © 2026 SORA123k
