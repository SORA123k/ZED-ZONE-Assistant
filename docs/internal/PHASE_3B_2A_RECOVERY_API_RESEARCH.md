# PHASE_3B_2A_RECOVERY_API_RESEARCH.md — 原生恢复接口研究

> **阶段**：Phase 3B-2A（**只做研究，未修改任何肢体数据**）
> **游戏**：ZED ZONE 1.0.16 / build 26600653 / Unity 2023.1.18f1 / IL2CPP x64
> **日期**：2026-11-13
> **前提**：interop 是 Il2CppInterop **桩**——签名可靠，**方法体不可读**。
> 因此"接口效果"分两类：`【签名可证】`（参数/命名/字段）与 `【推断】`（由名称与关联字段推导，未运行验证）。

## 证据标记

**【已通过源码确认】** / **【已通过实机验证】** / **【尚未验证】** / **【存在风险】**

---

## 0. 总览

| 接口 | 类 | 参数 | 索引空间 | 是否改 HP | 等级 |
|---|---|---|---|---|---|
| `HealAllBodyPartInjuries()` | Human | 无 | — | **未验证** | 源码确认 |
| `FullyHealInjuries()` | Human | 无 | — | **未验证** | 源码确认 |
| `SetToDefaultBodyPart(int bodypartIndex)` | Human | index | **未证明** | 未验证 | **【存在风险】** |
| `RefreshBodyPartRecoveryBuff(int bodyPartId)` | Human | id | **bodyPartId（已实证 0..5）** | 间接（Buff） | 源码+实证 |
| `GetRecoveryBuffId(int bodyPartId)` | Human(static) | id | **bodyPartId（已实证）** | 否 | **已实机验证** |
| `TickRecover(...)` | BodyPartsData | 3 个 float | — | 是（推断） | **private，不可直接调用** |
| `OnSleepTimeUpdate(...)` | BodyPartsData | time, mult | — | 是（推断） | 源码确认 |
| `OnAwakeTimeUpdate(...)` | BodyPartsData | time, mult | — | 是（推断） | 源码确认 |
| `UseBandage(CharacterBodyPartData, ItemData)` | Human | **对象引用** | 无歧义 | 是（推断） | 源码确认 |
| `RemoveBandage(CharacterBodyPartData)` | Human | **对象引用** | 无歧义 | 可能 | 源码确认 |
| `ReadWoundInput(int bodyPartId, out float, out bool)` | Human | id | **bodyPartId（已实证）** | **否（只读）** | **已实机验证** |
| `IsBodyPartWounded(CharacterBodyPartData)` | Human(static) | 对象引用 | 无歧义 | 否 | **已实机验证** |
| `StopAllBleeding()` | Basic | 无 | — | 否（只止血） | 源码确认 |
| `RequestWoundVisualRefresh(int, bool)` | Human | id | bodyPartId（推断） | 否（仅视觉） | 源码确认 |
| `StartBleeding(CharacterBodyPartData, float)` | Human | 对象, 秒 | 无歧义 | 否 | 源码确认 |

---

## 1. `HealAllBodyPartInjuries()`

| # | 项 | 结论 | 等级 |
|---|---|---|---|
| 1 | 参数含义 | 无参数 | 源码确认 |
| 2 | 索引空间 | 无 | — |
| 3 | 是否改肢体 HP | **未验证**（推断：是） | 尚未验证 |
| 4 | 是否改伤口状态 | 未验证（`isWounded` 是派生属性，若 hp 变化则自动变） | 尚未验证 |
| 5 | 是否处理流血 | **未验证**（另有独立 `StopAllBleeding()`，暗示它可能不管流血） | 尚未验证 |
| 6 | 是否触发其他 Buff | 未验证 | 尚未验证 |
| 7 | 副作用 | 未验证；同类还有 `HealBodyRenderers()`（明确是视觉），说明"Heal*"系列混有视觉与状态两种语义 | **【存在风险】** |
| 8 | 是否影响存档 | 不直接写存档；只改运行期对象 | 源码确认 |
| 9 | 是否影响其他角色 | 实例方法，只作用于 `this` | 源码确认 |

**注意**：由于存在 `HealBodyRenderers()` / `HealBodyRenderer(SpriteRenderer, string, ref bool)`
这类**纯视觉**方法，`HealAllBodyPartInjuries()` **不能**想当然认为它会改 HP。
必须实测。**未通过验证前不得作为恢复方案的主路径。**

---

## 2. `FullyHealInjuries()`（本轮新发现）

| # | 项 | 结论 | 等级 |
|---|---|---|---|
| 1 | 参数含义 | 无参数 | 源码确认 |
| 2 | 索引空间 | 无 | — |
| 3 | 是否改肢体 HP | **未验证**；命名比 `HealAllBodyPartInjuries` 更强（"Fully"） | 尚未验证 |
| 4-6 | 伤口/流血/Buff | 未验证 | 尚未验证 |
| 7 | 副作用 | 未知；可能同时清视觉伤口覆盖 | **【存在风险】** |
| 8 | 存档 | 不直接写 | 源码确认 |
| 9 | 其他角色 | 实例方法 | 源码确认 |

**与 `HealAllBodyPartInjuries()` 的关系未知**（谁包含谁、是否一个调另一个）。两者都需实测。

---

## 3. `SetToDefaultBodyPart(int bodypartIndex)`

| # | 项 | 结论 | 等级 |
|---|---|---|---|
| 1 | 参数含义 | 参数名 `bodypartIndex`（**不是** `bodyPartId`） | 源码确认 |
| 2 | 索引空间 | **未证明**（候选：数据数组 0..5 / 视觉组件 0..10） | **【存在风险】** |
| 3 | 是否改肢体 HP | 未验证 | 尚未验证 |
| 4 | 伤口状态 | 未验证 | 尚未验证 |
| 5 | 流血 | 未验证 | 尚未验证 |
| 6 | Buff | 未验证 | 尚未验证 |
| 7 | 副作用 | **可能只是"把视觉贴图恢复默认"**（同类有 `HealBodyRenderers`、`RequestWoundVisualRefresh`、`ApplyInjuryOverlay`） | **【存在风险】** |
| 8 | 存档 | 不直接写 | 源码确认 |
| 9 | 其他角色 | 实例方法 | 源码确认 |

**关键疑点**：同一类里还有 `OnUnEquipClothing(int bodypartIndex)`——`bodypartIndex` 这个名字在**衣物**语境里也出现。
说明它未必等于"数据数组下标"。

**结论：本接口暂列阻塞，不进入恢复方案。**

---

## 4. `RefreshBodyPartRecoveryBuff(int bodyPartId)`

| # | 项 | 结论 | 等级 |
|---|---|---|---|
| 1 | 参数含义 | 部位 ID | 源码确认 |
| 2 | 索引空间 | **`bodyPartId`（0..5）——已实证**，见 `PHASE_3B_2A_INDEX_VERIFICATION.md` | **实机验证** |
| 3 | 是否改肢体 HP | **间接**：会按部位挂"恢复 Buff"（34–37），由 Buff 逐秒回血 | 推断（字段+命名） |
| 4 | 伤口状态 | 间接（hp 上升后 `isWounded` 派生变化） | 推断 |
| 5 | 流血 | **未验证**（与 `StartBleeding`/`bandage` 的交互未知） | 尚未验证 |
| 6 | 触发其他 Buff | **是**：会挂恢复类 Buff（`GetRecoveryBuffId` 给出 34/35/36/37） | 推断 |
| 7 | 副作用 | 可能覆盖现有 Buff 状态、可能重置 Buff 计时 | **【存在风险】** |
| 8 | 存档 | 不直接写 | 源码确认 |
| 9 | 其他角色 | 实例方法 | 源码确认 |

**优点**：参数空间已证实，**是当前唯一"参数安全"的按部位恢复接口**。
**缺点**：恢复是**渐进**（靠 Buff 每秒回），不是瞬间回满。

---

## 5. `GetRecoveryBuffId(int bodyPartId)`（static）

| # | 项 | 结论 | 等级 |
|---|---|---|---|
| 1 | 参数含义 | 部位 ID → 恢复 Buff ID | 源码确认 |
| 2 | 索引空间 | `bodyPartId`（0..5） | **实机验证** |
| 3-6 | 不修改任何状态 | 纯查询 | **实机验证**（连续调用 10 次无异常） |
| 7 | 副作用 | **越界不抛异常，静默返回 37（腿部恢复）** | **【存在风险】**（已实测） |
| 8 | 存档 | 否 | — |
| 9 | 其他角色 | static，与实例无关 | — |

**实测映射**（与 62-Buff 表完全吻合）：

| bodyPartId | 部位 | 返回 Buff | Buff 名（来自 3A 的 62 条表） |
|---|---|---|---|
| 0 | 头部 | **34** | headRecovery |
| 1 | 躯干 | **35** | torseRecovery |
| 2 | 左臂 | **36** | armRecovery |
| 3 | 右臂 | **36** | armRecovery |
| 4 | 左腿 | **37** | legRecovery |
| 5 | 右腿 | **37** | legRecovery |
| 6–9 | 越界 | **37**（静默回退） | — |

---

## 6. `TickRecover(float addedTime, float percentPerDay, float recoverMultiplier)`

| # | 项 | 结论 | 等级 |
|---|---|---|---|
| 1 | 参数含义 | 增加时间 / 每天恢复百分比 / 恢复倍率 | 源码确认（名称） |
| 2 | 索引空间 | 无（内部遍历全部部位） | 源码确认 |
| 3-6 | 改 HP / 伤口 / 流血 / Buff | 推断：按 `percentPerDay` 恢复各部位 hp | 推断 |
| 7 | 副作用 | 未知 | 尚未验证 |
| 8 | 存档 | 否 | — |
| 9 | 其他角色 | 操作该 `CharacterBodyPartsData`，不涉他人 | 源码确认 |

**调用性**：`NativeMethodInfoPtr_TickRecover_Private_Void_...` → **private**。
**不能直接调用**（除非反射，本项目不采用）。**排除**。

---

## 7. `OnSleepTimeUpdate(float addedTime, float recoverMultiplier = 1f)`

| # | 项 | 结论 | 等级 |
|---|---|---|---|
| 1 | 参数含义 | 睡眠推进的时长 / 倍率 | 源码确认 |
| 2-3 | 索引空间 / 改 HP | 无索引；推断按睡眠速率恢复 | 推断 |
| 4-6 | 伤口/流血/Buff | 未验证 | 尚未验证 |
| 7 | **副作用** | **会推进"整段睡眠"的恢复逻辑**，可能同时影响饥饿/疲劳等由睡眠驱动的系统（该方法在 `BodyPartsData` 上，范围限于肢体，但调用它等于"假装睡了一觉"） | **【存在风险】** |
| 8 | 存档 | 否 | — |
| 9 | 其他角色 | 实例方法 | 源码确认 |

**评价**：语义是"时间推进式恢复"，与一键恢复不是同一类操作。**不建议**用于"一键恢复全部"。

---

## 8. `OnAwakeTimeUpdate(float addedTime, float recoverMultiplier = 1f)`

与 §7 同构，对应"清醒时间"的恢复速率（`GetBodyPartRecoverMultiplier(bool sleeping)` 表明清醒/睡眠倍率不同）。
**同样不建议**用于即时恢复。

---

## 9. `UseBandage(CharacterBodyPartData bodyPartData, ItemData m_bandageData)`

| # | 项 | 结论 | 等级 |
|---|---|---|---|
| 1 | 参数含义 | 目标部位数据对象 + 绷带物品 | 源码确认 |
| 2 | 索引空间 | **无（按对象引用定位）→ 不会指错部位** | 源码确认 |
| 3 | 是否改 HP | 未验证（推断：不直接回满，主要处理流血与绷带状态） | 尚未验证 |
| 4 | 伤口状态 | 未验证 | 尚未验证 |
| 5 | 流血 | 推断：是（与 `bleedingStopTime`/`bandageExpireTime` 关联） | 推断 |
| 6 | Buff | 未验证 | 尚未验证 |
| 7 | **副作用** | **可能需要消耗真实物品 `ItemData`**；传入非绷带物品的后果未知 | **【存在风险】** |
| 8 | 存档 | 物品可能被消耗 → **间接触及存档内容** | **【存在风险】** |
| 9 | 其他角色 | 只作用于传入的对象 | 源码确认 |

---

## 10. `RemoveBandage(CharacterBodyPartData bodyPartData)`

- 参数：目标部位对象（无索引歧义）；
- 作用：移除该部位的绷带（推断，与 `bandageData`/`bandageExpireTime` 对应）；
- **实测发现：受伤部位当前 `bandaged=1`**（见索引验证报告），所以"恢复"时可能需要一并处理绷带；
- 副作用：可能导致该部位重新开始流血（若移除绷带而伤口未愈）→ **【存在风险】**。

---

## 11. 其他相关接口（本轮新发现）

| 接口 | 作用（推断） | 价值 |
|---|---|---|
| **`ReadWoundInput(int bodyPartId, out float hpFloat, out bool bandaged)`** | **只读**：按部位 ID 取归一化 HP 与是否绷带 | **极高**：可在不改数据的前提下验证 ID 空间 |
| `IsBodyPartWounded(CharacterBodyPartData)`（static） | 判断部位是否受伤 | 实测与 `isWounded` **不一致**（见风险报告），不可混用 |
| `StopAllBleeding()` | 停止全部流血 | 若恢复需要止血，这是无参安全接口 |
| `RequestWoundVisualRefresh(int bodyPartId, bool immediate)` | 刷新伤口视觉 | 直接改 hp 后可能需要它来同步贴图 |
| `FlushWoundVisualRefresh()` | 强制刷新视觉 | 同上 |
| `HealBodyRenderers()` / `HealBodyRenderer(...)` | **纯视觉**愈合 | 说明"Heal*"系列≠一定改数值 |
| `WoundGroupKeyUnchanged(int group)` / `CaptureWoundGroupKey(int group)` | 伤口视觉分组去重 | 与视觉缓存相关 |

---

## 12. 本阶段结论

1. **参数空间已解决**（只读实证）：`bodyPartId` = 数据侧 `bodyPartId`（0..5），
   越界返回 `-1` 哨兵；`GetRecoveryBuffId` 越界静默回退 37。
2. **`SetToDefaultBodyPart` 仍是阻塞项**：参数名 `bodypartIndex` 与 `bodyPartId` 不同，
   且同名字也出现在 `OnUnEquipClothing`（衣物语境）。**未证明前不得使用。**
3. **`TickRecover` 是 private**，不可直接调用，已排除。
4. **"Heal*"系列存在视觉/状态语义混淆风险**，`HealAllBodyPartInjuries()`/`FullyHealInjuries()`
   是否真的改 HP **必须实测**才能进入方案。
5. **唯一"参数已证实安全"的按部位接口**是 `RefreshBodyPartRecoveryBuff(int bodyPartId)`，
   但它走的是**渐进恢复 Buff**，不是瞬回。
