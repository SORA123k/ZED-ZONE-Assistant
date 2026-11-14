# PHASE_3B_2A_RECOVERY_DESIGN.md — 肢体恢复方案设计

> **阶段**：Phase 3B-2A（**仅设计，不实现、不添加可执行按钮**）
> **约束**：不调用任何恢复接口、不修改肢体数据（本轮已遵守）
> **游戏**：1.0.16 / build 26600653
> **日期**：2026-11-13

## 证据标记

**【已通过源码确认】** / **【已通过实机验证】** / **【尚未验证】** / **【存在风险】**

---

## 1. 不可动摇的前提（来自已完成的验证）

| # | 前提 | 证据 |
|---|---|---|
| P1 | 部位寻址一律用 **`bodyPartId`（0..5）** | 只读实验 6/6 精确匹配 |
| P2 | 视觉下标（0..10）**绝不**当数据下标 | 3B-0/0.1 已证两者不同 |
| P3 | 恢复目标 = **`maxHP`**，每次重新读 | maxHP 跨会话变化（100→84→104→108） |
| P4 | **`defaultHP` 绝不当目标** | 固定 [20,50,30,30,40,40]，远小于 maxHP |
| P5 | `isWounded` 用 `CharacterBodyPartData.isWounded` | `IsBodyPartWounded` 实测不一致 |
| P6 | 越界 ID 不报错（静默回退） | `GetRecoveryBuffId(6..9)=37` |
| P7 | 只操作**本地玩家实例**，不碰 NPC/僵尸/其他玩家 | `HumanCharacterController` 实例方法 |
| P8 | 联机一律拒绝 | 沿用 fail-closed 判定 |

---

## 2. 方案 A：恢复全部肢体

### 2.1 候选路径（按优先级）

| 顺序 | 路径 | 优点 | 当前阻塞 |
|---|---|---|---|
| **A1** | `HealAllBodyPartInjuries()` | 无参数、无索引风险、语义最接近"一键" | **是否真的改 HP 未验证** |
| **A2** | `FullyHealInjuries()` | 语义更强（"Fully"） | 同上，且与 A1 关系未知 |
| **A3** | 逐部位 `RefreshBodyPartRecoveryBuff(id)` | 参数空间**已证实** | 效果是**渐进**（Buff 回血），不是瞬回 |
| **A4** | 逐部位写 `data.hp = data.maxHP` | 结果确定、可立即验证 | 绕过原生逻辑（绷带/流血/视觉可能不同步） |

### 2.2 推荐策略

```
第 1 步：A1（HealAllBodyPartInjuries）
   ↓ 写后回读 6 个部位
第 2 步：若有部位未达 maxHP → A2（FullyHealInjuries）再验
   ↓ 仍不足
第 3 步：对剩余部位用 A4（data.hp = maxHP），逐个写并回读
   ↓ 若存在流血/绷带残留
第 4 步：StopAllBleeding()（无参）+ 逐部位 RemoveBandage(d)（按对象，无索引风险）
   ↓
第 5 步：RequestWoundVisualRefresh(id, true) 让贴图与数值同步
```

> **为什么不是一开始就 A4**：原生接口若能完成，副作用最小；A4 是兜底。
> **为什么最后要刷新视觉**：实测存在伤口贴图叠加系统（`ApplyInjuryOverlay`、`HealBodyRenderers`），
> 只改数值可能出现"数值满血但身上还在流血"的视觉不一致。

### 2.3 必须做的验证（写后回读）

```
for each part:
    写前记录 hp_before / maxHP
    执行恢复
    hp_after = d.hp（重新读）
    判定：hp_after >= maxHP - ε  → 成功
          否则              → 记失败原因（不重试超过 1 次）
```

**整体 HP 不受影响**必须验证：写前后读 `CharacterStatusData.hp` 并比对。

---

## 3. 方案 B：单独恢复指定部位

### 3.1 可行路径

| 顺序 | 路径 | 参数安全 | 效果 | 状态 |
|---|---|---|---|---|
| **B1** | `RefreshBodyPartRecoveryBuff(int bodyPartId)` | ✅ 已实证 | 渐进（挂 34–37 恢复 Buff） | 可进入设计 |
| **B2** | `data.hp = data.maxHP`（按已枚举的对象） | ✅ 无歧义（对象引用） | **瞬时**、确定 | 可进入设计 |
| **B3** | `SetToDefaultBodyPart(int bodypartIndex)` | ❌ **未证明** | 未知 | **阻塞，禁止使用** |

### 3.2 推荐

- **瞬时恢复**：B2（写 `hp = maxHP`）+ 写后回读校验 + 视觉刷新；
- **渐进恢复**（更贴近"游戏原生"体验）：B1，但需告知用户"会随时间回满"；
- **B3 暂不使用**，直到 §4 的决定性实验给出结论。

### 3.3 单部位的安全边界

| 检查 | 规则 |
|---|---|
| 目标定位 | 只用遍历时取得的 `bodyPartId`；**禁止**用手写常量 |
| 目标上限 | 每次重新读该部位的 `maxHP`；`<=0`/NaN → **拒绝该项** |
| 只升不降 | `hp >= maxHP - ε` → 跳过（不写） |
| 误伤检查 | 写前记录**全部 6 个**部位的 hp；写后比对，**除目标外任何一个变化都要告警并记录** |
| 失败处理 | 任一步异常 → 停止该项，保留其它部位状态，记录原因 |

> "写前记录全部、写后比对全部" 是针对 **B3 索引不确定** 的兜底检测手段：
> 即使将来用了错误的索引，也能在日志里立刻发现"改到了别的部位"。

---

## 4. 索引问题的处理策略

| 接口 | 参数 | 结论 | 处置 |
|---|---|---|---|
| `RefreshBodyPartRecoveryBuff` | `bodyPartId` | ✅ 0..5 已实证 | **可用**（需范围自校验） |
| `GetRecoveryBuffId` | `bodyPartId` | ✅ 已实证 | **可用**（只读） |
| `ReadWoundInput` | `bodyPartId` | ✅ 已实证 | **可用于写后校验** |
| `SetToDefaultBodyPart` | `bodypartIndex` | ❓ 未证明 | **禁止使用** |

### 4.1 让 B3 转正所需的一次实验（需另行批准）

见 `PHASE_3B_2A_INDEX_VERIFICATION.md` §9：在**备份存档**上调用一次 `SetToDefaultBodyPart(2)`，
用只读探针观察**哪一个部位**的 hp 发生变化，即可定性参数空间。

> 该实验会修改一处肢体状态，**不在本阶段范围内**。

---

## 5. UI 规划（**仅设计，本轮不添加按钮**）

### 5.1 布局草案（沿用现有「肢体」页）

```
肢体状态（只读）　场景=Gaming
共 6 个部位　受伤 2　流血 0　完全恢复 3

[ 全部恢复 ]                      ← 单个按钮，位于概览行下方
部位      当前 / 上限     百分比    状态        [操作]
头部      104 / 104      100%     已恢复       [恢复]
躯干      72.95 / 134     54%     受伤         [恢复]
左臂      114 / 114      100%     已恢复       [恢复]
右臂      114 / 114      100%     已恢复       [恢复]
左腿      115.57 / 124    93%     正常         [恢复]
右腿      85.45 / 124     69%     受伤         [恢复]
只读展示，不会修改任何数值。         ← 恢复功能上线后改为"操作结果会写入日志"
```

### 5.2 交互规则（设计）

| 规则 | 说明 |
|---|---|
| 二次确认 | 点"全部恢复"需二次确认（避免误触）；单部位不需要 |
| 逐项反馈 | 每次操作后显示：`✓ 躯干 12.5 → 138` 或 `✗ 右腿：上限不可靠，已跳过` |
| 汇总反馈 | 全部恢复后显示"成功 5 / 跳过 1 / 失败 0" |
| 联机 | 按钮**不渲染**（沿用现有联机禁用逻辑） |
| 满值 | 按钮置灰或点击后返回 `SkippedNoChange` |
| 失败原因 | 至少覆盖：无玩家 / 上限无效 / 写入异常 / 回读不一致 / 联机 / 已满 |

### 5.3 日志字段（每次操作）

```
[limbrestore] stat=part target=bodyPartId=1 name=躯干
              before=12.492 max=138 source=CharacterBodyPartData.maxHP
              after=138 ok=1 elapsedMs=3 othersUnchanged=1
```

`othersUnchanged` = 写后比对其余 5 个部位是否未被误改（防索引错误）。

---

## 6. 门禁（进入实现前必须通过）

| Gate | 内容 | 阻塞对象 |
|---|---|---|
| **G-R1** | `HealAllBodyPartInjuries()` 是否真的把 hp 提到 maxHP（实测） | 方案 A 的主路径 |
| **G-R2** | `FullyHealInjuries()` 的行为与 A1 的差异 | 方案 A 的兜底 |
| **G-R3** | 恢复后 `isWounded` / `isBleeding` / `bandaged` 如何变化 | 状态正确性 |
| **G-R4** | 恢复是否影响整体 HP（必须为"否"） | 安全红线 |
| **G-R5** | 视觉贴图是否同步（需要时用 `RequestWoundVisualRefresh`） | 体验一致性 |
| **G-R6** | `SetToDefaultBodyPart` 参数空间（决定 B3 是否可用） | 方案 B 的可选项 |

---

## 7. 明确不做的事

| 项 | 原因 |
|---|---|
| 使用 `SetToDefaultBodyPart` | 参数空间未证明 |
| 使用 `TickRecover` | private，不可直接调用 |
| 用 `OnSleepTimeUpdate` 做"一键恢复" | 语义是时间推进，副作用未知 |
| 用 `defaultHP` 当目标 | 已证远小于 maxHP |
| 用视觉下标寻址 | 已证不同空间 |
| 修改 `GameConfigs.playerInvincible` | 全局开关，禁止 |
| 对 NPC/僵尸调用任何恢复 | 越权 |
| 联机启用 | 一律拒绝 |
