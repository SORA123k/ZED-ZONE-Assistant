# PHASE_3B_2A_INDEX_VERIFICATION.md — 索引空间只读验证

> **阶段**：Phase 3B-2A
> **方法**：只调用**只读**接口（`ReadWoundInput` / `GetRecoveryBuffId` / `IsBodyPartWounded`），
> **未调用任何恢复或写入接口**，未修改任何肢体数据。
> **游戏**：1.0.16 / build 26600653
> **证据原始文件**：`research/phase3b2a_api_probe_evidence.txt`

## 证据标记

**【已通过源码确认】** / **【已通过实机验证】** / **【尚未验证】** / **【存在风险】**

---

## 0. 结论速览

| 问题 | 结论 | 依据 |
|---|---|---|
| `bodyPartId` 是哪个空间 | **= 数据侧 `bodyPartId`（0..5）** | 逐 id 比对 `hpFloat` **完全一致** |
| 合法 ID 范围 | **0..5**；越界返回 `hpFloat = -1` 哨兵 | 实测 id 6–9 全部 -1 |
| `GetRecoveryBuffId` 参数空间 | 同 `bodyPartId` | 映射与 34–37 恢复 Buff 表吻合 |
| 越界行为 | 不抛异常，**静默返回 37（腿部恢复）** | 实测 id 6–9 → 37 |
| `SetToDefaultBodyPart` 参数 | **仍未证明** | 参数名 `bodypartIndex` 与 `bodyPartId` 不同 |
| 是否存在"真正安全的单部位恢复接口" | **有一个**：`RefreshBodyPartRecoveryBuff(int bodyPartId)`（参数已证；效果未证） | 见 §4 |

---

## 1. 实验设计（只读）

对 `id = 0..9` 依次调用：

```csharp
player.ReadWoundInput(id, out float hpFloat, out bool bandaged);   // 只读
HumanCharacterController.GetRecoveryBuffId(id);                    // static 纯查询
```

并与数据侧真值对照：

```csharp
var d = bodypartDataAry[i];
d.hp / d.maxHP / d.hpFloat / d.isWounded / d.isBleeding / d.isFullyRecovered
HumanCharacterController.IsBodyPartWounded(d)                      // static 判断
```

> 探针配置 `[Diagnostics] LimbApiProbe`，默认关闭，联机拒绝执行；验证后已改回 `false`。

---

## 2. 原始输出（逐字）

```
[limbapi] humanBodyPartCount=6 arrayLen=6
[limbapi] ---- 数据侧真值 ----
[limbapi]  data[0] id=0 textId=80  hp=108     max=108 hpFloat=1     wounded=0 bleeding=0 fully=1 IsBodyPartWounded=0 一致=yes
[limbapi]  data[1] id=1 textId=81  hp=12.492  max=138 hpFloat=0.091 wounded=1 bleeding=0 fully=0 IsBodyPartWounded=0 一致=NO
[limbapi]  data[2] id=2 textId=180 hp=101.062 max=118 hpFloat=0.856 wounded=1 bleeding=0 fully=0 IsBodyPartWounded=0 一致=NO
[limbapi]  data[3] id=3 textId=181 hp=118     max=118 hpFloat=1     wounded=0 bleeding=0 fully=1 IsBodyPartWounded=0 一致=yes
[limbapi]  data[4] id=4 textId=182 hp=54.914  max=128 hpFloat=0.429 wounded=1 bleeding=0 fully=0 IsBodyPartWounded=0 一致=NO
[limbapi]  data[5] id=5 textId=183 hp=55.169  max=128 hpFloat=0.431 wounded=1 bleeding=0 fully=0 IsBodyPartWounded=0 一致=NO

[limbapi] ---- ReadWoundInput(bodyPartId) 探测 ----
[limbapi]  id=0 ReadWoundInput={hpFloat=1,     bandaged=0} GetRecoveryBuffId=34
[limbapi]  id=1 ReadWoundInput={hpFloat=0.091, bandaged=1} GetRecoveryBuffId=35
[limbapi]  id=2 ReadWoundInput={hpFloat=0.856, bandaged=1} GetRecoveryBuffId=36
[limbapi]  id=3 ReadWoundInput={hpFloat=1,     bandaged=0} GetRecoveryBuffId=36
[limbapi]  id=4 ReadWoundInput={hpFloat=0.429, bandaged=1} GetRecoveryBuffId=37
[limbapi]  id=5 ReadWoundInput={hpFloat=0.431, bandaged=1} GetRecoveryBuffId=37
[limbapi]  id=6 ReadWoundInput={hpFloat=-1,    bandaged=0} GetRecoveryBuffId=37
[limbapi]  id=7 ReadWoundInput={hpFloat=-1,    bandaged=0} GetRecoveryBuffId=37
[limbapi]  id=8 ReadWoundInput={hpFloat=-1,    bandaged=0} GetRecoveryBuffId=37
[limbapi]  id=9 ReadWoundInput={hpFloat=-1,    bandaged=0} GetRecoveryBuffId=37
```

---

## 3. 证明 1：`bodyPartId` = 数据侧 `bodyPartId`（0..5）

| id | `ReadWoundInput(id).hpFloat` | `data[id].hpFloat` | 是否一致 |
|---|---|---|---|
| 0 | 1 | 1 | ✅ |
| 1 | 0.091 | 0.091 | ✅ |
| 2 | 0.856 | 0.856 | ✅ |
| 3 | 1 | 1 | ✅ |
| 4 | 0.429 | 0.429 | ✅ |
| 5 | 0.431 | 0.431 | ✅ |

**6/6 完全一致**（三位小数级别），且 `hpFloat = hp/maxHP`
（12.492/138 = 0.0905 → 0.091 ✓）。

这不是"看起来差不多"，而是**逐位精确匹配** —— 足以证明：
`ReadWoundInput(id)` 读的就是 `bodypartDataAry[id]`。

**结论（实机验证）**：`bodyPartId` 参数 = 数据侧 `CharacterBodyPartData.bodyPartId`，**0..5**。

### 3.1 越界 = `-1` 哨兵

`id = 6..9` 全部返回 `hpFloat = -1`、`bandaged = 0`，**且不抛异常**。

→ 说明内部有范围检查并返回哨兵；**不会**因为越界去读写别的部位内存。
这对安全是好事，但**也意味着"写错 ID 未必报错"**（见 §5）。

---

## 4. 证明 2：`GetRecoveryBuffId` 映射与 Buff 表吻合

| bodyPartId | 部位 | 返回 | 62 条 Buff 表中的对应项 |
|---|---|---|---|
| 0 | 头部 | **34** | 34 headRecovery |
| 1 | 躯干 | **35** | 35 torseRecovery |
| 2 | 左臂 | **36** | 36 armRecovery |
| 3 | 右臂 | **36** | 36 armRecovery |
| 4 | 左腿 | **37** | 37 legRecovery |
| 5 | 右腿 | **37** | 37 legRecovery |

**独立交叉验证成功**：这个映射与 Phase 3A 从 `BuffManager.GetBuffAttrByID` 读到的
62 条运行时 Buff 表**完全对应**——两条独立证据链互相印证。

同时说明：**手臂共享 36、腿共享 37**，即"部位 → 恢复 Buff"是 4 类而非 6 类。
因此**按 Buff 恢复无法区分左臂/右臂**（两者都会被挂同一个 armRecovery）。

---

## 5. 【存在风险】越界静默回退

`GetRecoveryBuffId(6..9)` **不抛异常**，一律返回 **37（腿部恢复）**。

**推论**：若将来把错误的值传给它（例如把视觉下标 6..10 当 bodyPartId），
不会报错，而是**默默按"腿部"处理** —— 典型的静默错误。

**设计要求**：调用前必须自己校验 `0 <= id < humanBodyPartCount`，不能依赖游戏报错。

---

## 6. 【重要】`IsBodyPartWounded` 与 `isWounded` 不一致

| 部位 | `data.isWounded` | `IsBodyPartWounded(d)` | 一致？ |
|---|---|---|---|
| 0 头部 | 0 | 0 | ✅ |
| 1 躯干 | **1** | **0** | ❌ |
| 2 左臂 | **1** | **0** | ❌ |
| 3 右臂 | 0 | 0 | ✅ |
| 4 左腿 | **1** | **0** | ❌ |
| 5 右腿 | **1** | **0** | ❌ |

**结论：两者不是同一判据。**

可能原因（均未验证）：
- `IsBodyPartWounded` 依赖某些静态/上下文状态（例如只在特定渲染流程中有效）；
- 它用的是不同阈值；
- 它在没有 `bandaged` 上下文时返回 false。

**设计要求**：
- **UI 与验证统一使用 `CharacterBodyPartData.isWounded`**（Phase 3B-1 已采用）；
- **禁止**使用 `IsBodyPartWounded` 做判定，除非另行验证。

---

## 7. 附带发现：受伤部位当前是"已包扎"状态

`bandaged` 实测：头部 0、躯干 **1**、左臂 **1**、右臂 0、左腿 **1**、右腿 **1**。

与 `isWounded` 高度重合（4 个受伤部位 bandaged=1，2 个满值部位为 0）。

**含义**：这些部位上**还挂着绷带**（`bandageData` / `bandageExpireTime`）。
将来"恢复"时若不处理绷带，可能出现"HP 回满但绷带仍在/流血逻辑异常"。
→ 设计中必须把 `RemoveBandage` / `StopAllBleeding` 纳入待验证清单。

---

## 8. 仍未证明的事项

| # | 事项 | 状态 |
|---|---|---|
| 1 | `SetToDefaultBodyPart(int bodypartIndex)` 的参数空间 | **未证明**（阻塞） |
| 2 | `RefreshBodyPartRecoveryBuff(bodyPartId)` 的**实际效果**（只证明了它的参数空间） | 未验证 |
| 3 | `HealAllBodyPartInjuries()` / `FullyHealInjuries()` 是否改 HP | 未验证 |
| 4 | 恢复后绷带/流血状态如何变化 | 未验证 |
| 5 | `hpFloat` 在 `ReadWoundInput` 中的 -1 是否也代表"部位不存在"还是"未初始化" | 未验证（只知道越界为 -1） |

---

## 9. 给下一阶段的"决定性实验"建议（需另行批准，本阶段不做）

**目的**：一次性确定 `SetToDefaultBodyPart` 的参数空间。

**做法**（必须先在备份存档上进行）：
1. 记录 6 个部位的 `hp`；
2. 只调用**一次** `SetToDefaultBodyPart(k)`（k 取一个特征值，如 2）；
3. 立刻用只读探针读回 6 个部位的 `hp` / `isWounded` / `bandaged`；
4. 看**哪一个**部位的数值发生了变化：
   - 若变化的是 `data[2]`（左臂）→ 参数是**数据数组下标 / bodyPartId**；
   - 若变化的是"视觉下标 2 对应的部位"（按 3B-0 的映射是左腿）→ 参数是**视觉下标**；
   - 若什么都没变 → 它可能只改视觉贴图，不适合做恢复。

这个实验**会修改一处肢体状态**，因此必须单独批准，且只在备份存档上做。

---

## 10. 结论

1. **`bodyPartId` 索引空间已由只读实验证实**（6/6 精确匹配 + 越界哨兵 + Buff 映射双重印证）。
2. `GetRecoveryBuffId` 的 34/35/36/36/37/37 映射与运行时 Buff 表完全一致。
3. **越界不报错**是必须防的风险（自己校验范围）。
4. **`IsBodyPartWounded` 不可信**，与 `isWounded` 冲突。
5. **`SetToDefaultBodyPart` 仍然阻塞**，未证明前不得进入恢复方案。
