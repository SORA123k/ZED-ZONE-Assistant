# PHASE_3B_2A_RISK_REVIEW.md — 恢复方案风险评审

> **阶段**：Phase 3B-2A（评审对象：肢体恢复的**设计与接口**，非已实现代码）
> **游戏**：1.0.16 / build 26600653
> **日期**：2026-11-13

## 证据标记

**【已通过源码确认】** / **【已通过实机验证】** / **【尚未验证】** / **【存在风险】**

---

## 0. 评审结论

| 项 | 结论 |
|---|---|
| 是否可以直接进入实现 | **不可以** |
| 阻塞项数量 | **3 个硬阻塞**（R1 / R3 / R7） |
| 已可用（参数安全） | `RefreshBodyPartRecoveryBuff(bodyPartId)`、`ReadWoundInput(bodyPartId)`、`GetRecoveryBuffId(bodyPartId)` |
| 明确禁用 | `SetToDefaultBodyPart`、`TickRecover`、`OnSleepTimeUpdate`（作为恢复手段） |
| 风险总评 | **中高** —— 主要来自"索引语义"与"原生接口可能只改视觉"两方面 |

---

## 1. 风险登记表

| ID | 风险 | 严重度 | 证据 | 缓解措施 | 状态 |
|---|---|---|---|---|---|
| **R1** | `SetToDefaultBodyPart(int bodypartIndex)` 参数空间未知；若当成数据下标使用，可能**改错部位** | **高** | 参数名与 `bodyPartId` 不同，且同名字出现在 `OnUnEquipClothing` | **禁用**；需先做索引定性实验；同时用"写前后比对全部 6 部位"兜底检测 | **阻塞** |
| **R2** | `GetRecoveryBuffId` 越界**静默返回 37**（腿部），不报错 | 中 | 实测 id 6–9 → 37 | 调用前自校验 `0 ≤ id < humanBodyPartCount` | 可控 |
| **R3** | `HealAllBodyPartInjuries()` / `FullyHealInjuries()` **可能只改视觉**（同类存在 `HealBodyRenderers`） | **高** | 命名族混用；方法体不可读 | 必须实测写后回读 hp；不达标则不作为主路径 | **阻塞（G-R1）** |
| **R4** | 直接写 `hp` 可能与绷带/流血状态不同步（实测 4 个受伤部位 `bandaged=1`） | 高 | 探针实测 bandaged 分布 | 恢复后检查 `isBleeding`，必要时 `StopAllBleeding()` + `RemoveBandage(d)`；或改用原生路径 | 需验证 |
| **R5** | 只改数值不改贴图 → "数值满血但身上仍有伤口" | 中 | 存在 `ApplyInjuryOverlay` / `RequestWoundVisualRefresh` / `FlushWoundVisualRefresh` | 写后调用 `RequestWoundVisualRefresh(id, true)` | 需验证 |
| **R6** | `IsBodyPartWounded(d)` 与 `isWounded` **实测不一致**（4/6 相反） | 中 | 探针实测 | 统一使用 `data.isWounded`，禁用 `IsBodyPartWounded` | 已缓解 |
| **R7** | 手臂共享恢复 Buff 36、腿共享 37 → 恢复一条臂可能**连带**影响另一条 | **高** | `GetRecoveryBuffId` 实测映射 | 写后比对**全部**部位；若连带变化则放弃 Buff 路径 | **阻塞（需实测）** |
| **R8** | `UseBandage` 可能**消耗真实物品** | 中 | 参数含 `ItemData` | 不在"恢复"路径中使用；若必须用，先确认物品消耗语义 | 未使用 |
| **R9** | 联机下写入 → 行为不同步 | 高 | 肢体状态不走网络（3B 研究已证），但伤口影响移速 | 复用 fail-closed 判定，联机一律拒绝 | 已有机制 |
| **R10** | 误伤其他角色（NPC/僵尸） | 中 | `BasicCharacterController` 为基类 | 只对本地玩家实例调用；不 patch 基类 | 已有设计约束 |
| **R11** | 存档损坏 | 低 | 恢复只改运行期对象，不写存档文件 | 保持"不直接写存档" | 已有约束 |
| **R12** | 单次恢复后 `maxHP` 变化（成长中）导致目标过期 | 低 | 实测 maxHP 跨会话变化 | 每次操作前重读 maxHP | 已纳入设计 |

---

## 2. 三个硬阻塞的详细说明

### 2.1 R1：`SetToDefaultBodyPart` 索引不确定

- 若它是**数据下标/bodyPartId**（0..5），用对了就正常；
- 若它是**视觉下标**（0..10），则 `SetToDefaultBodyPart(0)` 会去改"躯干"（视觉 0 = 躯干），
  而使用者以为是"头部" —— **静默改错部位**；
- 且越界是否报错未知（`GetRecoveryBuffId` 的越界就是静默的，同一代码库风格）。

**解除条件**：完成"调用一次 + 全部位比对"的定性实验（需单独批准，且只在备份存档上）。

### 2.2 R3："Heal*" 可能是纯视觉

该类里同时存在：
- `HealBodyRenderers()` / `HealBodyRenderer(SpriteRenderer, string, ref bool)` —— **明确是视觉**；
- `ApplyInjuryOverlay(...)` / `RequestWoundVisualRefresh(...)` / `FlushWoundVisualRefresh()` —— 视觉；
- `HealAllBodyPartInjuries()` / `FullyHealInjuries()` —— **语义不清**。

若把 `HealAllBodyPartInjuries()` 当成"回满 HP"，而它其实只清了伤口贴图，
就会出现 **"UI 显示成功但 hp 没变"** 的假成功。这是本次评审认为最隐蔽的风险。

**解除条件**：实测——记录 hp → 调用 → 回读 hp；同时观察 `isWounded` 与贴图。

### 2.3 R7：恢复 Buff 是"按类别"的

`GetRecoveryBuffId` 返回 4 类（34 头 / 35 躯 / 36 臂 / 37 腿），
**左右臂都是 36、左右腿都是 37**。

若 `RefreshBodyPartRecoveryBuff(2)`（左臂）实际是"给角色挂 armRecovery"，
则右臂可能一起回血 → **单部位恢复变成双部位恢复**。

**解除条件**：实测单部位调用后比对全部 6 个部位。

---

## 3. 安全要求对照

| 要求 | 落实情况 |
|---|---|
| 不修改任何游戏数据（本阶段） | ✅ 只调用 3 个只读接口，未调用任何恢复/写入 |
| 不调用恢复接口 | ✅ 8 个恢复接口一个都没调用 |
| 不新增 Harmony Patch | ✅ 计数 = 0 |
| 不修改 GameConfigs | ✅ 无引用 |
| 不修改存档 | ✅ 无文件写入 |
| 不影响 NPC/僵尸/其他玩家 | ✅ 仅对本地玩家实例；探针为只读 |
| 在线一律拒绝 | ✅ 探针内联机检查（fail-closed）；已实机验证过同款判定 |
| 保留 Phase 3A v0.3.0 | ✅ 未改 `PlayerWriteGate` / `RestoreService` |
| 保留 Phase 3B-1 只读肢体面板 | ✅ 未改 `LimbData` / `DrawLimbTab` |

---

## 4. 验证状态汇总

| 项 | 状态 |
|---|---|
| `bodyPartId` = 数据侧 ID（0..5） | **已通过实机验证**（6/6 精确匹配） |
| 越界返回 `-1` 哨兵 | **已通过实机验证** |
| `GetRecoveryBuffId` 映射 34/35/36/36/37/37 | **已通过实机验证**（与 Buff 表交叉印证） |
| `GetRecoveryBuffId` 越界静默回退 37 | **已通过实机验证** |
| `IsBodyPartWounded` ≠ `isWounded` | **已通过实机验证** |
| 受伤部位 `bandaged=1` | **已通过实机验证** |
| `SetToDefaultBodyPart` 参数空间 | **尚未验证（阻塞）** |
| `HealAllBodyPartInjuries` / `FullyHealInjuries` 是否改 HP | **尚未验证（阻塞）** |
| `RefreshBodyPartRecoveryBuff` 实际效果 / 是否连带 | **尚未验证（阻塞）** |
| 恢复后绷带/流血变化 | 尚未验证 |
| 视觉贴图同步 | 尚未验证 |
| 整体 HP 不受影响 | 尚未验证（设计已纳入验收） |

---

## 5. 建议的推进顺序（供审核）

1. **G-R1**：在**备份存档**上实测 `HealAllBodyPartInjuries()` 是否改 hp（最高优先，决定方案 A 主路径）；
2. **G-R7**：实测 `RefreshBodyPartRecoveryBuff(bodyPartId)` 是否连带影响另一条腿/臂；
3. **G-R6 / R1**：实测 `SetToDefaultBodyPart` 的参数空间（决定 B3 是否可用）；
4. **G-R3 / R4 / R5**：确认恢复后的伤口、流血、绷带、贴图；
5. 全部通过后再写恢复代码与按钮。

以上 4 步**都会修改肢体状态**，必须在单独批准 + 备份存档的前提下进行，
不在当前 Phase 3B-2A 范围内。

---

## 6. 最终结论

- 接口研究已完成，**索引问题的一半已被只读方式彻底解决**（`bodyPartId`）；
- **另有一半（`bodypartIndex`）仍未解决**，且是本次最高风险；
- 恢复方案已设计出可执行的优先级链（原生 → 兜底直写 → 视觉刷新），
  但**三个阻塞项未解除前不得实现**；
- 本阶段**未修改任何游戏数据**，符合全部安全要求。
