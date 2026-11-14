# PHASE_3B_0_1_INDEX_MAPPING_REPORT.md — 肢体索引 / ID 映射研究

> **目标**：搞清 `GetBodyPartByIndex`、`bodyPartId`、`SetToDefaultBodyPart`、`GetBodyPartDataByID`
> 四个接口的参数含义，以及"视觉索引 / 数据索引 / 部位 ID"三者关系。
> **原则**：禁止假设三者相等；无证据不得调用 `SetToDefaultBodyPart`。
> **游戏**：ZED ZONE 1.0.16 / build 26600653
> **日期**：2026-11-12

## 证据标记

**【已通过源码确认】** / **【已通过实机验证】** / **【尚未验证】** / **【存在风险】**

---

## 0. 结论速览（先给答案）

| 概念 | 含义 | 证据等级 |
|---|---|---|
| **数据数组下标** `bodypartDataAry[i]` | 0 起，长度 = `humanBodyPartCount` = **6** | 实机验证 |
| **`bodyPartId`** | 逻辑部位 ID；本角色 **0..5**，与数据数组下标**同值** | 实机验证（单角色） |
| **视觉组件下标** `GetBodyPartByIndex(i)` | 0 起，长度 = **11**（≠ 6） | 实机验证 |
| **`bodypartDataID`**（视觉组件字段） | 指向**数据部位 ID**（9/11 与名称一致，2/11 不一致） | 实机验证 + 存疑 |
| **`GetBodyPartDataByID(int)`** | 按 `bodyPartId` 取数据部位 | 源码确认（未实机调用） |
| **`SetToDefaultBodyPart(int bodypartIndex)`** | 参数空间**仍未证明** | **【存在风险】阻塞** |

**一句话**：**视觉下标 ≠ 数据下标 ≠ 6**。视觉列表有 11 个元素，数据只有 6 个部位；
`bodypartDataID` 是通向数据侧的"外键"，但**不能**用视觉下标去索引数据。

---

## 1. 四个接口的定义（源码）

| 接口 | 所在类 | 签名 |
|---|---|---|
| `GetBodyPartByIndex` | `BasicCharacterController` | `CharacterBodyPart GetBodyPartByIndex(int index)` |
| `GetBodyPartIndex` | `BasicCharacterController` | `int GetBodyPartIndex(CharacterBodyPart part)` |
| `GetBodyPartDataByID` | `CharacterBodyPartsData` | `CharacterBodyPartData GetBodyPartDataByID(int bodyPartID)` |
| `SetToDefaultBodyPart` | `HumanCharacterController` | `void SetToDefaultBodyPart(int bodypartIndex)` |

**【已通过源码确认】参数命名区分了两种概念**：
- `GetBodyPartDataByID(int bodyPartID)` → **ID**
- `SetToDefaultBodyPart(int bodypartIndex)` → **Index**
- `GetBodyPartByIndex(int index)` → **Index**（返回视觉组件）

但"index 是哪个 index"没有写在签名里 —— 这正是必须实机验证的原因。

---

## 2. 实机证据：两份列表的长度与内容

### 2.1 数据侧（6 个）

```
humanBodyPartCount = 6
humanBodyPartTextIdAry    = [80, 81, 180, 181, 182, 183]
  -> 名称                  = [头部, 躯干, 左臂, 右臂, 左腿, 右腿]
humanBodyPartDefaultHPAry = [20, 50, 30, 30, 40, 40]
bodypartDataAry:
  [0] bodyPartId=0 bodyPartTextId=80  头部
  [1] bodyPartId=1 bodyPartTextId=81  躯干
  [2] bodyPartId=2 bodyPartTextId=180 左臂
  [3] bodyPartId=3 bodyPartTextId=181 右臂
  [4] bodyPartId=4 bodyPartTextId=182 左腿
  [5] bodyPartId=5 bodyPartTextId=183 右腿
```

→ **数据下标 i 与 bodyPartId 在本角色上完全一致（0..5）**，且与文本 ID 数组顺序一致。

### 2.2 视觉侧（11 个）

```
视觉组件数量 = 11（GetBodyPartByIndex 遍历到 null 为止）
visual[0]  bodypartDataID=1  nameTextId=81  躯干   clothing=1
visual[1]  bodypartDataID=4  nameTextId=182 左腿   clothing=1
visual[2]  bodypartDataID=4  nameTextId=182 左腿   clothing=1
visual[3]  bodypartDataID=5  nameTextId=182 左腿   clothing=1   ← 不一致
visual[4]  bodypartDataID=5  nameTextId=182 左腿   clothing=1   ← 不一致
visual[5]  bodypartDataID=0  nameTextId=80  头部   clothing=1
visual[6]  bodypartDataID=0  nameTextId=80  头部   clothing=1
visual[7]  bodypartDataID=2  nameTextId=180 左臂   clothing=1
visual[8]  bodypartDataID=2  nameTextId=180 左臂   clothing=1
visual[9]  bodypartDataID=3  nameTextId=181 右臂   clothing=1
visual[10] bodypartDataID=3  nameTextId=181 右臂   clothing=1
```

### 2.3 按 `bodypartDataID` 归类的视觉组件数量

| 数据部位 | dataID | 视觉组件数 | 视觉下标 |
|---|---|---|---|
| 头部 | 0 | 2 | 5, 6 |
| 躯干 | 1 | **1** | 0 |
| 左臂 | 2 | 2 | 7, 8 |
| 右臂 | 3 | 2 | 9, 10 |
| 左腿 | 4 | 2 | 1, 2 |
| 右腿 | 5 | 2 | 3, 4 |
| **合计** | | **11** | |

**决定性结论**：
- 视觉列表长度 **11 ≠ 6**，且**一个数据部位对应多个视觉组件**（躯干 1 个，其余各 2 个）；
- 视觉列表的**排列顺序**是 `躯干 → 左腿×2 → 右腿×2 → 头部×2 → 左臂×2 → 右臂×2`，
  与数据顺序 `头部 → 躯干 → 左臂 → 右臂 → 左腿 → 右腿` **完全不同**。

> 所以上一轮我标注的 "MISMATCH（6/6 不匹配）" 是**比较方法本身错了**：
> 我拿 `visual[i]` 去比 `data[i]`，而两者根本不是同一序列。
> 本轮已修正：不再逐行比对，改为输出完整视觉清单。**这条自我更正已写入报告。**

---

## 3. `bodypartDataID` 到底指什么

### 3.1 支持"指向数据部位 ID"的证据

- 11 个组件中 **9 个**的 `bodypartDataID` 与其 `nameTextId` 对应的数据部位**一致**
  （例如 visual[0]: dataID=1 ↔ 躯干 textId=81；data 侧 id=1 也是躯干）；
- 若 `bodypartDataID` 是"视觉表自己的 ID"，不太可能与数据 ID 在这 9 个上全部吻合。

### 3.2 不一致的 2 个（必须记录）

| 视觉 | bodypartDataID | nameTextId | name | 数据侧该 ID 的名称 |
|---|---|---|---|---|
| visual[3] | 5 | 182 | 左腿 | id=5 → **右腿**(183) |
| visual[4] | 5 | 182 | 左腿 | id=5 → **右腿**(183) |

两种可能：
1. `nameTextId` 在这两个组件上是**过期/未刷新**的值 → 以 `bodypartDataID` 为准（=右腿）；
2. `bodypartDataID` 错了 → 以 nameTextId 为准（=左腿）。

**目前无法判定哪一种是真相**（方法体不可读，且不能通过写操作去试）。
→ **【尚未验证】**，并列为使用 `bodypartDataID` 作路由键的风险。

### 3.3 `IsClothingBodyPart` 的语义不能用名字猜

实机 11 个组件**全部** `clothing=1`。
如果它表示"这是一个衣物层"，不可能全部为真 → 该字段更可能是
"该部位**可穿衣物**"之类的属性，而非"本组件是衣物"。

**【尚未验证】**：不据此做任何判断，也不用于区分皮肤层/衣物层。

---

## 4. `SetToDefaultBodyPart(int bodypartIndex)` 的参数空间 —— 仍阻塞

### 4.1 已知事实

- 它是 `HumanCharacterController` 上的 **public void**（源码确认）；
- 参数名为 `bodypartIndex`（源码确认），不是 `bodyPartID`；
- 同类上还有 `GetBodyPartByIndex(int)`（视觉）与 `GetBodyPartDataByID(int)`（数据），
  说明 `index` 与 `ID` 是两个明确区分过的概念；
- `GameController` 另有 `GetDefaultBodySprite(int bodyPartIndex)` —— 又一个 "bodyPartIndex" 消费者。

### 4.2 两种候选

| 候选 | 取值 | 若猜错的后果 |
|---|---|---|
| A：数据数组下标 | 0..5 | 若实际要视觉下标 → 索引越界或指向错误部位 |
| B：视觉组件下标 | 0..10 | 若实际要数据下标 → 5 之后全部越界；0..5 会错位（如 0→躯干而非头部） |

**两者在 0..5 范围内都会"看起来能跑"**，但会**改错部位** —— 这正是最危险的失败模式。

### 4.3 为什么不能实测

用户明确要求："禁止在没有证据时调用 `SetToDefaultBodyPart` 进行实际恢复测试。"
该调用会**修改肢体状态**，违反本轮"不修改肢体 HP / 不调用恢复接口"的安全限制。
因此**没有实测**。

### 4.4 处置

- **保持为阻塞项**，3B-2（肢体恢复）不得使用该 API；
- 若将来必须用，需要先取得证据（例如：只对**一个已知受伤部位**调用后用只读探针核对，
  且必须由人工在备份存档上执行）；
- 备选路径（可行）：直接写 `bodypartDataAry[i].hp = maxHP`（数据侧，无需猜测索引空间），
  但属于绕过原生逻辑，需单独评审。

---

## 5. `GetBodyPartDataByID(int)` 的参数含义

- 参数名 `bodyPartID`，与 `CharacterBodyPartData.bodyPartId` 同名同义 → **【已通过源码确认】** 高置信；
- 返回值是 `CharacterBodyPartData`（数据对象），不是视觉组件 → 指向数据侧；
- **【尚未验证】** 未实机调用（本轮用 `bodypartDataAry[i]` 直接遍历，未走该方法）。

**建议**：3B 后续应优先使用 `GetBodyPartDataByID(bodyPartId)`，因为它是**按 ID 而非下标**访问，
天然不受数组顺序变化影响。

---

## 6. 三者映射关系（最终图）

```
                    ┌──────────────────────────────┐
   数据侧 (6)        │ bodypartDataAry[0..5]         │
                    │   .bodyPartId      0..5       │  ← 逻辑 ID（本角色与下标同值）
                    │   .bodyPartTextId  80/81/180… │  ← 游戏本地化名
                    │   .hp / .maxHP / .defaultHP   │
                    └────────────▲─────────────────┘
                                 │ GetBodyPartDataByID(bodyPartId)  ← 推荐入口
                                 │
                    ┌────────────┴─────────────────┐
   视觉侧 (11)       │ GetBodyPartByIndex(0..10)     │  ← 视觉下标（≠ 数据下标！）
                    │   .bodypartDataID  → 数据 ID   │  ← 外键（9/11 吻合，2/11 存疑）
                    │   .nameTextId      → 文本 ID   │
                    │   .IsClothingBodyPart = 1 全部 │  ← 语义未明
                    └──────────────────────────────┘

   未知              SetToDefaultBodyPart(int bodypartIndex) → 参数属于哪个空间：**未证明**
```

---

## 7. 安全使用规则（给后续开发）

| 规则 | 理由 |
|---|---|
| R1 只用 `bodyPartId` 作为部位键 | 它是数据侧唯一稳定标识 |
| R2 禁止用 `GetBodyPartByIndex(i)` 的下标去索引 `bodypartDataAry` | 长度 11 vs 6，顺序不同 |
| R3 禁止把 `bodypartDataID` 单独当作真相 | 2/11 与名称不一致 |
| R4 `SetToDefaultBodyPart` 一律不用，直到参数空间被证明 | 会在 0..5 内静默改错部位 |
| R5 部位数量/顺序一律运行期读取（`humanBodyPartCount` / `textIdAry`） | 不同角色类型可能不同（蛇/蜈蚣变体） |
| R6 若必须写数据，用 `bodypartDataAry[i].hp`，i 来自**遍历时记录的 bodyPartId** | 不依赖视觉层 |

---

## 8. 结论

1. **三种索引确实不相等**，且已在实机证实：视觉 11 个 vs 数据 6 个，顺序也不同。
2. `bodyPartId` 与数据数组下标在本角色上**恰好同值**，但这是**观察结果**，不能当作通用规律。
3. `bodypartDataID` 是数据侧的"外键"，但存在 2/11 的名称不一致，**不能单独信任**。
4. `SetToDefaultBodyPart` 的参数空间**仍未证明**，按用户要求保持阻塞，**未调用**。
5. 上一轮"6/6 MISMATCH"的判断已在本文更正为"比较方法错误"，并给出正确的比较方式。
