# 配置项参考（CONFIG）

配置文件位置：

```
<游戏目录>\BepInEx\config\zedzone.ultimateassistant.cfg
```

- 首次运行游戏时自动生成。
- **菜单里改的项会立即生效并写回文件**；**手动编辑文件后需要重启游戏**（BepInEx 只在启动时读取）。
- 键名（section / key）是稳定的，改名会破坏老用户配置，因此新增功能请**追加**新键，不要重命名旧键。
- 下面默认值是**新装用户**拿到的值；老配置文件保留你自己已有的值。

本文与 `src/ZedZone.UltimateAssistant/Core/ModConfig.cs`、`Core/HotkeyManager.cs` 一一对应，
共 **69 个功能配置项 + 16 个快捷键**。

---

## [General]

| 键 | 默认 | 说明 |
|---|---|---|
| `Enabled` | `true` | 模组总开关。`false` 时不注册任何模块、不装补丁、不注入 GUI。 |
| `ShowOnStart` | `false` | 进入游戏（存档加载后）自动打开菜单。 |

## [UI]

| 键 | 默认 | 范围 | 说明 |
|---|---|---|---|
| `ToggleKey` | `Insert` | Unity KeyCode | 打开/关闭菜单。与 `[Menu] MenuKeys` 叠加生效，`Insert` 永远可用。 |
| `FontScale` | `1.0` | 0.7–2.0 | 旧版（Phase 2 只读面板）的字号缩放，保留兼容。 |
| `PanelX` / `PanelY` | `380` / `40` | 0–4000 | 旧版面板位置，保留兼容。菜单位置由 `[Menu]` 段与拖动记忆控制。 |
| `StartTab` | `0` | 0–7 | 旧版面板默认页签，保留兼容。 |
| `ShowNotes` | `true` | — | 是否显示数据来源说明（排查用）。 |

## [Read]

| 键 | 默认 | 范围 | 说明 |
|---|---|---|---|
| `RefreshIntervalMs` | `100` | 16–2000 | 状态读取/后台巡检间隔。100ms 足够实时，同时显著降低 IL2CPP 封送开销。 |
| `LogOnPlayerChange` | `true` | — | 玩家引用变化（重生/换场景/读档）时写一条日志。 |
| `LogValuesSeconds` | `10` | 0–600 | 每隔多少秒把真实读数写进日志（`0` = 关闭）。用于和游戏原生面板逐项对照。 |

## [Restore]（一键恢复）

写入走唯一的写入门 `PlayerWriteGate`：总开关 → 联机 fail-closed → 死亡/重生窗口 → 动态上限校验 → 写入 → 回读。

| 键 | 默认 | 说明 |
|---|---|---|
| `EnableRestore` | `true` | **总开关**。`false` 时所有恢复操作直接拒绝。 |
| `Health` | `true` | 允许恢复生命。 |
| `Energy` | `true` | 允许恢复精力/体力（游戏里 `energy` 与 `stamina` 是同一数值）。 |
| `Hunger` | `true` | 允许恢复饱食度。 |
| `Thirst` | `true` | 允许恢复水分。 |
| `Fatigue` | `false` | 允许恢复疲劳。默认关闭：疲劳方向未确认。 |
| `FatigueDirection` | `unconfirmed` | `unconfirmed` / `resource`（越高越好）/ `exhaustion`（越高越糟）。`unconfirmed` 时禁止写入。 |

## [Modules]（只读信息模块）

| 键 | 默认 | 说明 |
|---|---|---|
| `Health` | `true` | 生命模块。 |
| `Hunger` | `true` | 饱食模块。 |
| `Thirst` | `true` | 水分模块。 |
| `Stamina` | `true` | 体力模块（只读，与 energy 同值）。 |
| `Energy` | `true` | 精力模块（只读，与 stamina 同值）。 |
| `Fatigue` | `true` | 疲劳模块。 |
| `Status` | `true` | 精神值 / 死亡标志 / Buff 数量。 |

关掉某个模块只是不采集，不影响功能补丁。

## [Features]（修改类功能）

> **联机保护**：本段所有功能在检测到联机状态时会拒绝启用，并主动关闭
> （fail-closed，见 `Features.GateOnline` / `EnforceOffline`）。

| 键 | 默认 | 说明 |
|---|---|---|
| `InfiniteAmmo` | `false` | 射击不消耗备用弹药。 |
| `NoReload` | `false` | 无需换弹（弹匣为 0 也能开火）。 |
| `NoJam` | `false` | 不会卡壳，并清除已有卡壳标记。 |
| `JamBypass` | `false` | 卡壳不阻止开火（游戏无公开清卡壳接口时的兜底）。 |
| `ItemDurabilityLock` | `false` | 工具/近战/装备耐久只增不减。 |
| `WeaponDurability` | `false` | 枪械耐久保持在历史最高（写 `ItemData.properties[0]`，会随存档保存）。 |
| `NoOverheat` | `false` | 自己枪械温度恒为 0（不影响 NPC）。 |
| `AutoSupply` | `false` | 制造/修复视为材料充足；岛屿船可强制完成建造。会改变任务流程，注意备份存档。 |
| `NoHpLoss` | `false` | 本地玩家受伤不掉血。 |
| `NoStaminaLoss` | `false` | 奔跑/跳跃不消耗体力。 |
| `NoHungerLoss` | `false` | 饱食度不减少（进食仍增加）。 |
| `NoThirstLoss` | `false` | 水分不减少（饮水仍增加）。 |
| `AlwaysSprint` | `false` | 停火自动奔跑；开火时自动取消奔跑。 |
| `MoveSpeed` | `false` | 移动加速开关。 |
| `MoveSpeedMultiplier` | `1.0` | 加速倍率，1.0–20.0（含翻滚加速）。 |
| `FullBright` | `false` | 夜间提升场景亮度（只影响画面，不改时间/天气/AI）。 |
| `ZombieEsp` | `false` | 僵尸 ESP（名称/距离/方框/屏幕边缘指示）。 |
| `PerformanceMode` | `false` | 降低 ESP/耐久/诊断刷新频率，响应略慢但更省帧。 |

## [Esp]

| 键 | 默认 | 范围 | 说明 |
|---|---|---|---|
| `ShowName` | `true` | — | 显示僵尸名称/类型。 |
| `ShowDistance` | `true` | — | 显示距离。 |
| `MaxDistance` | `200` | 5–1000 | 最大显示距离（米）。 |
| `DrawBox` | `true` | — | 绘制方框（关闭则只画标记点）。 |
| `RefreshMs` | `150` | 50–2000 | ESP 刷新间隔，越大越省性能。 |

## [Menu]

| 键 | 默认 | 可选值 / 范围 | 说明 |
|---|---|---|---|
| `Theme` | `Dark` | `Dark` `BlackGold` `BlueTech` `Purple` `Teal` `Light` `RedBlack` | 主题配色。 |
| `Scale` | `1.0` | 0.7–1.6 | 菜单整体缩放。 |
| `Animations` | `true` | — | 菜单动画。 |
| `FontSize` | `14` | 12–24 | 菜单字号（px）。 |
| `Language` | `zhCN` | `zhCN` `en` `zhTW` | 菜单语言。 |
| `MenuKeys` | 空 | KeyCode 名称，逗号分隔 | 额外菜单快捷键（`A,B,C`）。`Insert` 永远有效。 |
| `BlockGameInput` | `true` | — | 菜单打开时阻止角色操作（移动/射击/使用物品）。 |
| `ShowLimbs` | `true` | — | 在 Player 页显示肢体状态（只读）。 |

## [Diagnostics]（只读诊断，排查问题时才开）

全部为**只读**：只打印/记录数值，不写游戏数据。默认全部关闭；打开后会写日志到
`<游戏目录>\BepInEx\` 与 `Diagnostics\` 目录，有轻微性能开销。

| 键 | 默认 | 范围 | 说明 |
|---|---|---|---|
| `DumpBuffTable` | `false` | — | 启动时转储游戏 Buff 定义表与全局开关。 |
| `GrowthLog` | `false` | — | 记录成长相关数值（HP 上限/加成系数/场景）。 |
| `GrowthLogIntervalSec` | `10` | 2–600 | 成长探针心跳间隔。 |
| `FatigueProbe` | `false` | — | 采样 fatigue/energy 与移动输入，判断疲劳方向。 |
| `FatigueProbeIntervalMs` | `500` | 100–5000 | 疲劳探针采样间隔。 |
| `FatigueProbeSeconds` | `600` | 30–7200 | 疲劳探针最长采样时长。 |
| `LimbProbe` | `false` | — | 打印每个肢体的 index/hp/maxHP/受伤/流血。 |
| `LimbProbeIntervalMs` | `500` | 100–5000 | 肢体探针采样间隔。 |
| `LimbProbeHeartbeatSec` | `30` | 5–600 | 肢体探针心跳间隔。 |
| `LimbProbeChangeFlushSec` | `1` | 1–60 | 肢体变化日志合并间隔（避免刷屏）。 |
| `LimbApiProbe` | `false` | — | 校验恢复接口的参数空间（**只调用只读接口**）。 |
| `MetaDiag` | `false` | — | IL2CPP 私有字段元数据探针，输出到 `Diagnostics\`。 |
| `DurabilityDiag` | `false` | — | 耐久只读诊断（装备槽/耐久字段/调用栈），输出到 `Diagnostics\`。 |

## [Hotkeys]（每个功能一个快捷键）

值填 Unity `KeyCode` 名称（如 `F1`、`Keypad5`、`None`）。菜单里点卡片右侧的按键按钮可以直接录键，
按 `ESC` 清空。

| 键 | 对应功能 |
|---|---|
| `infinite_ammo` | 无限弹药 |
| `no_reload` | 无需换弹 |
| `no_jam` | 不会卡壳 |
| `item_dur` | 工具/装备耐久锁定 |
| `gun_dur` | 枪械耐久锁定 |
| `no_overheat` | 取消过热 |
| `no_hp` | 不会扣血 |
| `no_sta` | 不会扣体力 |
| `no_hunger` | 不会扣饱食度 |
| `no_thirst` | 不会扣水分 |
| `always_sprint` | 强制奔跑 |
| `move_speed` | 移动加速 |
| `esp` | 僵尸 ESP |
| `fullbright` | 地图全亮 |
| `auto_supply` | 自动补充材料 |
| `performance` | 性能模式 |

全部默认 `None`（未绑定）。被游戏占用的按键会在启动日志里给出 `[keybind] ... 可能冲突` 提示。

---

## 常见改法

```ini
; 想默认就开无敌，但只在单机：改 Features 段（联机时会自动关掉）
[Features]
NoHpLoss = true
NoHungerLoss = true

; 想关掉所有修改类功能，只当只读查看器
[General]
Enabled = true
[Restore]
EnableRestore = false
```

改完 `Features` 段后**重启游戏**；进游戏后仍可在菜单里随时切换。

## 相关文档

- [README.md](../README.md) — 功能一览
- [docs/COMPATIBILITY.md](COMPATIBILITY.md) — 游戏版本与适配
