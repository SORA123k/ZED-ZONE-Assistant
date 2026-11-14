# PHASE_3A_MAX_VALUE_BUG_REPORT.md — 动态最大值读取错误（严重 Bug）报告

> **项目**：ZED ZONE Ultimate Assistant（AppID 1211600）
> **严重级别**：**严重**（会把玩家生命值向下截断）
> **状态**：**已修复并已通过游戏实机验证**
> **游戏版本**：1.0.16 / build 26600653 / Unity 2023.1.18f1 / IL2CPP x64
> **日期**：2026-11-12
> **范围**：仅修复动态属性上限读取与一键恢复；**未开发 Phase 3B**

---

## 1. 现象（用户报告）

| 时点 | 玩家生命值 | 游戏原生面板显示的上限 |
|---|---|---|
| 点击"恢复生命"前 | 2000 | **2000** |
| 点击"恢复生命"后 | **1000** | 2000 |

即：恢复功能把生命值从 2000 **降到了 1000**，与"恢复"语义完全相反。

---

## 2. 根因（已定位并实机证实）

插件原先用 `BasicCharacterController.maxHP` 作为上限：

```csharp
// 旧代码（错误）
max = p.maxHP;          // 实测 = 1000
```

但该字段**不是**玩家的真实有效上限。运行期同时读取三个候选后得到的真实数据：

```
[range] stat=Hp current=1000.21 min=0 max=2000 source=CharacterStatusData.maxhp
        candidates=[
            CharacterStatusData.maxhp      = 2000   ← 真实有效上限
            BasicCharacterController.FullHP= 1000   ← 也不对
            BasicCharacterController.maxHP = 1000   ← 旧代码用的就是它（错）
        ]
        reliable=True
```

**结论**：
- 玩家真实最大生命值来自 **`CharacterData.characterStatusData.maxhp`**，
  由 `CharacterData.SetMaxHP()` 依据 Constitution / 技能 / 平衡系数计算并维护；
- `BasicCharacterController.maxHP` 是**另一个独立字段**（1000），与有效上限不同步；
- `BasicCharacterController.FullHP`（属性）也是 1000，同样不能用作上限；
- 因此旧逻辑把 2000 血写成 1000，属于**上限来源选错**，不是"数值算错"。

### 2.1 为什么其它属性没有暴露同样的问题

`energy / fatigue / hunger / thirst` 在控制器上**根本没有** `max*` 字段
（只有 `hp` 例外，控制器上有 `maxHP`）。旧代码对这四项本来就读
`characterStatusData.maxEnergy / maxFatigue / maxHunger / maxThirst`，所以是对的。
**HP 是唯一一个"控制器字段恰好存在但语义不对"的陷阱。**

---

## 3. 修复方案

### 3.1 新增统一动态范围读取器

`src/ZedZone.UltimateAssistant/Core/StatRange.cs` + `RangeSelection.cs`

```
StatRangeReader.Read(StatId, player) -> StatRange {
    Current, Min, Max, MaxSource, Candidates[], Reliable, Error, Warning
}
```

规则（按可信度顺序，**每次操作前重新读取，绝不缓存**）：

| 属性 | 候选顺序 |
|---|---|
| **Hp** | ① `CharacterStatusData.maxhp` ② `BasicCharacterController.FullHP` ③ `BasicCharacterController.maxHP` |
| Energy | `CharacterStatusData.maxEnergy` |
| Fatigue | `CharacterStatusData.maxFatigue` |
| Hunger | `CharacterStatusData.maxHunger` |
| Thirst | `CharacterStatusData.maxThirst` |

选源算法（`RangeSelection.Select`，纯函数、可离线测试）：

```
1. 取第一个"有限且 > 0"的候选作为首选
2. 若首选 >= 当前值        → Ok，采用首选
3. 若首选 <  当前值        → 首选不可信，回退到第一个 ">= 当前值" 的其它候选（记 WARN）
4. 若无任何候选 >= 当前值  → Unreliable → 拒绝写入
5. 若全部候选无效          → Unreliable → 拒绝写入
```

### 3.2 新增"只升不降"与拒绝写入

- 恢复**只允许把数值改大**；当前值 ≥ 目标值时返回 `SkippedNoChange`，不写；
- 当前值高于读取到的上限时**不截断**，保持不动并注明原因；
- 上限不可靠（`RejectedUnreliableRange`）时**拒绝**，不猜测、不回退硬编码。

### 3.3 每次点击都记录完整可定位日志

新增 `[range]` 日志行（每次点击必打）：

```
[range] stat=Hp current=... min=0 max=... source=<来源>
        candidates=[名=值, ...] reliable=true|false [warning=...] [error=...]
```

满足"必须能定位为什么某个属性会被恢复到错误数值"。

---

## 4. 修复前后对比

| 项 | 修复前 | 修复后 |
|---|---|---|
| HP 上限来源 | `maxHP` 字段（1000） | `statusData.maxhp`（2000），带交叉校验 |
| 上限与当前值冲突 | 直接按上限写 → **截断** | 回退到可信候选；无候选则**拒绝** |
| 当前值 > 上限 | 写小值（伤害玩家） | `SkippedNoChange`，保持不动 |
| 每次操作重读上限 | 是（但来源错） | 是，且记录全部候选 |
| 失败可见性 | 仅"已恢复" | 明确拒绝原因 + `[range]` 证据 |

### 4.1 实机验证记录（修复后，用户实际操作）

```
[range]  stat=Hp current=1000.21 max=2000 source=CharacterStatusData.maxhp
         candidates=[maxhp=2000, FullHP=1000, maxHP=1000] reliable=True
[write]  stat=Hp before=1000.21 target=2000 after=2000 maxSource=CharacterStatusData.maxhp

[range]  stat=Energy current=178 max=180 source=CharacterStatusData.maxEnergy reliable=True
[write]  stat=Energy before=178 target=180 after=180

# 再次点击（已满）→ 正确跳过，没有写入
[write.reject] stat=Hp reason=SkippedNoChange detail=当前 2000 已不低于目标 2000
```

**结论：2000 / 2000 不再被恢复成 1000。**

---

## 5. 影响面

- 仅影响一键恢复的**目标值计算**；
- 只读预览、GUI、模块结构、配置均未改变；
- Harmony 补丁数仍为 **0**；
- 未修改 `GameConfigs`、未修改存档、未绕过 1.0.16 的模组安全检测。

---

## 6. 仍存在的风险

| 风险 | 说明 |
|---|---|
| 仅 HP 有 3 个候选 | 其它属性只有 1 个来源，无法交叉校验；若该来源本身错误，只能靠"当前值 > 上限"发现 |
| `maxHunger/maxThirst` 会动态变化 | 实机观察到 120 → 116 的变化，说明上限受状态影响；已做到每次重读，但未逐项验证成因 |
| `fatigue` 方向未确认 | 仍默认关闭（见回归报告） |

---

## 7. 相关文件

| 文件 | 说明 |
|---|---|
| `src/Core/StatRange.cs` | 动态范围读取器（游戏侧） |
| `src/Core/RangeSelection.cs` | 选源算法（纯 C#，可离线测） |
| `src/Core/PlayerWriteGate.cs` | 统一写入门（接入新范围 + 只升不降 + 拒绝） |
| `docs/PHASE_3A_DYNAMIC_RANGE_RESEARCH.md` | 逐属性来源与动态加成研究 |
| `docs/PHASE_3A_REGRESSION_TEST_REPORT.md` | 回归测试报告 |
| `research/phase3a_maxvalue_fix_evidence.txt` | 实机日志摘录 |
