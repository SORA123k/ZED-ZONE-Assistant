# PHASE_3B_1_IMPLEMENTATION_REPORT.md — 肢体状态显示 · 实施报告

> **阶段**：Phase 3B-1（只读 UI）
> **游戏**：ZED ZONE 1.0.16 / build 26600653
> **模组版本**：v0.3.0 + 肢体页（**一键恢复未改动**）
> **日期**：2026-11-12

## 证据标记

**【已通过源码确认】** / **【已通过实机验证】** / **【尚未验证】** / **【存在风险】**

---

## 0. 工程交付

| 项 | 值 |
|---|---|
| 构建结果 | **0 警告 / 0 错误** |
| DLL 大小 | **85,504** 字节 |
| **DLL MD5** | **`327689C6E98659F7941C65E9F955CC75`** |
| 构建产物 vs 已安装 | **哈希一致** ✓ |
| **Harmony Patch 数** | **0** |
| 离线回归测试 | **32 / 32 通过** |
| 旧 FPS 优化模组 | 未改动（已提交状态干净） |

---

## 1. 变更清单

| 文件 | 类型 | 说明 |
|---|---|---|
| `src/Core/LimbData.cs` | **新增** | 只读肢体快照读取器（UI 用） |
| `src/UI/PanelRenderer.cs` | 修改 | 新增「肢体」页 + 8 个标签；`DrawLimbTab` / 状态文本 / 样式 |
| `src/UI/AssistantPanel.cs` | 修改 | 数字键 1..8；节流块内刷新 `LimbData`；`StartTab` 生效 + 一行启动日志 |
| `src/Core/ModConfig.cs` | 修改 | 新增 `[UI] StartTab`（默认 0） |
| `src/Core/LimbProbe.cs` | 修改 | 清理未使用变量（消除 1 个编译警告） |

### 1.1 新增配置

```ini
[UI]
StartTab = 0    ; 0=玩家 1=生命 2=饱食 3=水分 4=体力/精力 5=状态 6=肢体 7=设置（默认 0，保持原行为）
```

> 该配置是**为验证新增**的（可在不看键盘的情况下直接打开指定页），默认值不改变原有行为。

---

## 2. 实现要点

### 2.1 数据读取（`LimbData.Refresh`）

```csharp
PlayerAccessor.Resolve()            // 每次重新解析，不缓存
  → player.characterData
      → characterData.bodypartsData
          → bodypartDataAry           // 遍历长度即实际部位数
              → CharacterBodyPartData // hp / maxHP / isWounded / isBleeding / isFullyRecovered
```

- 部位名：`humanBodyPartTextIdAry[i]` → `TextManager.instance.GetText(textId)`；
  数组不可用时回退到数据对象自带的 `bodyPartTextId`；再失败 → `未知(TextId=x)`。
- 每字段独立 `try/catch`：某字段失败只影响该字段（显示 `?`），不影响整行。

### 2.2 刷新（避免每帧）

```
AssistantPanel.Update()
  └─ 节流块（默认 100 ms，[Read] RefreshIntervalMs）
       ├─ Reader.Read()        // 玩家/生命快照（原有）
       ├─ LimbProbe.Tick()     // 日志探针（默认关闭）
       └─ LimbData.Refresh()   // ★ 本阶段新增：只读肢体快照
OnGUI()
  └─ PanelRenderer.Draw()       // 只读内存快照并绘制，零 interop
```

- 实测：`OnGUI` 每个事件都会调用 `Draw`，但**不会**触发任何 IL2CPP 封送；
- 本页**不产生日志**，避免刷屏。

### 2.3 状态文案

| 条件 | 文案 |
|---|---|
| 读取失败 | `读取失败(<异常名>)` |
| 上限无效（≤0/NaN） | `未知（上限无效）` |
| `isBleeding` | `流血`（红色） |
| `isWounded` | `受伤`（黄色） |
| `isFullyRecovered` | `已恢复` |
| 以上皆否 | `正常` |

多个状态可同时出现（如"受伤 流血"）。

---

## 3. 只读性审计（源码）

| 检查 | 结果 |
|---|---|
| 对游戏对象的字段赋值 | **0 处**（仅有 `info.Hp = d.hp` 这类"游戏→本地快照"的赋值，方向只读） |
| `HealAllBodyPartInjuries` 调用 | **0** |
| `SetToDefaultBodyPart` 调用 | **0** |
| `RefreshBodyPartRecoveryBuff` 调用 | **0** |
| `UseBandage` / `RemoveBandage` 调用 | **0** |
| `[HarmonyPatch]` | **0** |
| 修改 `GameConfigs` | **无引用** |
| 写存档 | **无** |
| 修改 Phase 3A 的 `PlayerWriteGate` / `RestoreService` | **一行未动** |

---

## 4. 与 Phase 3A 的隔离

| 项 | 状态 |
|---|---|
| 一键恢复按钮（玩家页 + 各资源页） | 保留，行为不变 |
| `PlayerWriteGate` / `RangeSelection` / `StatRange` | 未修改 |
| 离线单测 32 项（含 HP 截断 Bug 回归） | 全绿 |
| 面板新增页 | 仅追加，不改旧页内容 |

---

## 5. 实机表现（首帧证据）

启动后 `[ui] StartTab 配置=6 -> 实际 Tab=6`（测试用），面板直接打开在肢体页，
六行数据正常渲染（截图 `research/phase3b1_limb_panel.png`）：

```
共 6 个部位  受伤 2  流血 0  完全恢复 3
头部 104 / 104   100%  已恢复
躯干 72.95 / 134  54%  受伤
左臂 114 / 114   100%  已恢复
右臂 114 / 114   100%  已恢复
左腿 115.57 / 124 93%  正常
右腿 85.45 / 124  69%  受伤
```

`[Error]` = 0，无异常、无卡顿、无日志刷屏。

---

## 6. 未实现（按要求未做）

| 项 | 原因 |
|---|---|
| 肢体恢复按钮 | 属于 3B-2，未获批准 |
| 肢体保护 | 属于 3B-3，未获批准 |
| 肢体排序/筛选/自定义列 | 非本阶段要求 |
| 点击部位查看详情 | 非本阶段要求 |
