# PHASE_3A_DYNAMIC_RANGE_RESEARCH.md — 动态属性范围研究

> **目的**：彻底查清每一项属性的"当前值 / 最大值"真实来源，以及最大值为什么是动态的。
> **游戏**：ZED ZONE **1.0.16** / build 26600653 / Unity 2023.1.18f1 / IL2CPP x64
> **interop**：`BepInEx\interop\Assembly-CSharp.dll`（2026-11-12 18:37，晚于 GameAssembly 15:48 → 对应当前版本）
> **日期**：2026-11-12

## 证据等级

| 标记 | 含义 |
|---|---|
| **A｜已确认存在** | 当前 interop 中核实到精确签名（字段/属性/方法） |
| **B｜已确认可调用** | 在运行的游戏 1.0.16 中实际读取/写入成功，有日志 |
| **C｜仅反编译发现** | 只有签名，运行期未验证 |
| **D｜尚未找到可靠实现** | 无满足需求的接口 |

---

## 1. 数据模型（已确认）

```
GameController.instance.playerCharacter            : HumanCharacterController
    : BasicCharacterController
      hp        : 属性（Virtual_New；HumanCharacterController 覆写）
      energy / fatigue / hunger / thirst : 属性（Public get/set，非虚）
      maxHP     : **字段**（float）        ← 陷阱：不等于有效上限
      FullHP    : **属性**（get only）     ← 也不是有效上限
    .characterData                             : CharacterData
        characterStatusData                    : CharacterStatusData
            hp / energy / fatigue / hunger / thirst          ：裸字段（当前值）
            maxhp / maxEnergy / maxFatigue / maxHunger / maxThirst ：裸字段（**有效上限**）
        SetMaxHP() / SetMaxEnergy() / SetMaxFatigue() /
        SetMaxHunger() / SetMaxThirst()                     ：计算并写入上面的 max*
        appliedMaxHPConstitution / appliedMaxHPSkillFloat /
        maxHPBalanceFloat / appliedMaxHPBalanceFloat /
        skillProperty_MaxHPFloat / skillProperty_StaminaFloat /
        skillProperty_HungerRateFloat / skillProperty_ThirstRateFloat
```

**关键结构事实（B 级，实机日志）**：

| 成员 | 实测值（同一时刻） | 能否作为上限 |
|---|---|---|
| `CharacterStatusData.maxhp` | **2000** | ✅ **是**（与原生面板一致） |
| `BasicCharacterController.FullHP` | 1000 | ❌ 否 |
| `BasicCharacterController.maxHP` | 1000 | ❌ 否（**旧 Bug 的来源**） |
| `CharacterStatusData.maxEnergy` | 180 / 168 | ✅ 是 |
| `CharacterStatusData.maxHunger` | 120 / 116 | ✅ 是 |
| `CharacterStatusData.maxThirst` | 120 / 116 | ✅ 是 |
| `CharacterStatusData.maxFatigue` | 100 | ✅ 是（方向未定，见 §4） |

> 同一日志行（`[vitals]`）曾同时出现 `hp=1840/1000`：
> 说明 `hp` 可以超过 `maxHP`(1000)，而玩家真实上限是 2000 —— 进一步证明 `maxHP` 不是有效上限。

---

## 2. 最大值为什么是动态的（机制）

### 2.1 客户端计算链

```
角色成长数据（CharacterData）
   ├─ appliedMaxHPConstitution      ← Constitution（体力）加点结果        【A】
   ├─ appliedMaxHPSkillFloat        ← 技能加成                            【A】
   ├─ maxHPBalanceFloat             ← 平衡/难度系数                       【A】
   ├─ appliedMaxHPBalanceFloat      ← 已应用的平衡系数                   【A】
   └─ skillProperty_MaxHPFloat / skillProperty_StaminaFloat              【A】
                    │
                    ▼  CharacterData.SetMaxHP() / SetMaxEnergy() / ...
        CharacterStatusData.maxhp / maxEnergy / maxFatigue / maxHunger / maxThirst
                    │
                    ▼  原生 UI（PlayerStatusPanel 的 hpBar/energyBar/fatigueBar/hungerBar/thirstBar）【A】
```

官方本地化 id **2564**（CONSTITUTION / 体力，【事实-官方文档】）明确写着：

> `Affects max health and max stamina, out-of-combat health/stamina recovery (5s after being hit), debuff resistance, body temperature stability, and hunger/thirst caps.`

→ **最大生命、最大体力、饥饿/口渴上限** 都受 Constitution 影响，因此**必然是动态的**。

### 2.2 实测到的动态变化（B 级）

同一次游戏会话的日志中，同一角色的上限发生过变化：

| 属性 | 观测值序列 | 结论 |
|---|---|---|
| `maxHunger` | 120 → 116 | 会变 |
| `maxThirst` | 120 → 116 | 会变 |
| `maxEnergy` | 180 → 168 → 180 | 会变 |
| `maxhp` | 2000 | 本次稳定（但机制上受成长影响） |

**因此"缓存上限"是错误做法**；本项目已改为**每次恢复前重新读取**。

---

## 3. 逐属性完整核对（用户要求的 6 项）

### 3.1 Health

| 项 | 结论 | 等级 |
|---|---|---|
| 当前值来源 | `HumanCharacterController.hp`（覆写属性，与 UI 同源） | **B** |
| 最大值真实来源 | `CharacterStatusData.maxhp` | **B**（=2000，与面板一致） |
| 最小值 | 0 | C（游戏语义推断） |
| 原生 UI 计算方式 | `PlayerStatusPanel.hpBar` 由 hp / maxhp 驱动（方法体不可见） | C |
| 是否有等级/装备/技能加成 | **有**：`appliedMaxHPConstitution`、`appliedMaxHPSkillFloat`、`maxHPBalanceFloat`、`skillProperty_MaxHPFloat` | **A**（字段存在）+ **B**（实测 2000 ≠ 控制器 1000） |
| 上限是否动态变化 | 机制上是；本次会话内未观测到 maxhp 变化 | A/C |
| 恢复目标是否合法 | = `maxhp`（只升不降） | **B** |

**旧 Bug 定性**：不是"上限算错"，而是**读错了来源**（用了 `maxHP` 字段）。

### 3.2 Energy / Stamina（同一数据源）

| 项 | 结论 | 等级 |
|---|---|---|
| 当前值来源 | `BasicCharacterController.energy` | **B** |
| 最大值来源 | `CharacterStatusData.maxEnergy` | **B**（180） |
| 最小值 | 0 | C |
| 动态加成 | `skillProperty_StaminaFloat`、Constitution（2564 提到 max stamina） | **A** |
| 上限是否动态 | **是**，实测 180 → 168 → 180 | **B** |
| Stamina 与 Energy 是否同源 | **是**：同一行日志 `stamina=147.33/168 energy=147.33/168` 完全相同；`StaminaPotion` 的 `energyPerSecond=+3` | **B** |
| 恢复目标 | = `maxEnergy`，**Stamina/Energy 两个入口写同一个通道** | **B** |

### 3.3 Fatigue

| 项 | 结论 | 等级 |
|---|---|---|
| 当前值来源 | `BasicCharacterController.fatigue` | **B** |
| 最大值来源 | `CharacterStatusData.maxFatigue`（实测 100） | **B** |
| 动态加成 | 未发现专门技能字段；`BuffAttr` 无 fatigue 字段 | A（Buff 侧） |
| **方向（越高越好 / 越高越糟）** | **未确认** | **C（未决）** |
| 恢复目标 | 方向未定时**拒绝写入**（`FatigueDirection=unconfirmed`） | — |

### 3.4 Hunger

| 项 | 结论 | 等级 |
|---|---|---|
| 当前值来源 | `BasicCharacterController.hunger` | **B** |
| 最大值来源 | `CharacterStatusData.maxHunger` | **B**（120 / 116） |
| 动态加成 | Constitution（2564：hunger/thirst caps）、`skillProperty_HungerRateFloat`（速率，非上限） | **A** + 官方文档 |
| 上限是否动态 | **是**，实测 120 → 116 | **B** |
| 恢复目标 | = `maxHunger`（只升不降） | **B**（恢复成功且随后点击正确跳过） |

### 3.5 Thirst

与 Hunger 完全同构：`maxThirst`（120 / 116，动态），恢复目标 = `maxThirst`。**B**。

---

## 4. 原生 UI 对照（能做到哪一步）

| 我们能拿到的 | 状态 |
|---|---|
| 游戏原生面板的**上限文本** | ❌ 无法可靠读取（面板只有 `hpBar/energyBar/…` 的 `Image`，无 max 文本字段） |
| 游戏原生面板的**填充比例** | C（`Image.fillAmount` 可能存在，但未验证驱动方式） |
| 替代校验手段 | 用"当前值 > 上限"这一矛盾来反证上限错误（本次正是这样发现 `maxHP` 不可信） |

**结论**：无法直接读 UI 文本做自动比对。项目采用**"上限必须 ≥ 当前值"**作为可自动执行的一致性校验，
并在日志里输出全部候选，供人工与面板核对。

---

## 5. 统一范围读取接口（已实现）

```
StatRangeReader.Read(StatId stat, HumanCharacterController player) -> StatRange
    { Current, Min, Max, MaxSource, Candidates[], Reliable, Error, Warning }

StatRangeReader.RecoveryTarget(StatId, StatRange) -> float
RangeSelection.Select(candidates, current) -> Ok | FallbackUsed | Unreliable
```

- 全部在**每次操作前**重新读取，无任何缓存字段；
- 不依赖硬编码常量（1000/2000/100 都没有写死）；
- 上限不可靠 → 返回 `Unreliable` → 写入门拒绝（`RejectedUnreliableRange`）。

---

## 6. 尚未验证的部分（如实标注）

| 项 | 状态 | 原因 |
|---|---|---|
| 角色**初始**最大生命值 | **未验证** | 需要新开档对比 |
| **升级后**最大生命值变化 | **未验证** | 需要受控升级测试 |
| **装备**改变最大生命值 | **未验证**（机制存在，未逐件测试） | 需要逐件穿戴对比 |
| **技能/被动**改变最大生命值 | **未验证** | 同上 |
| 切换场景后上限重读 | **部分验证**：每次点击都重读；跨场景日志存在（`[state]`），但未做"场景前后上限值对比" | 需要专项测试 |
| 退出存档再进入后重读 | **部分验证**：本次实机是重新进入游戏后读取到 maxhp=2000 正确 | 需要多轮对比 |
| 满值时执行恢复 | **已验证**（`SkippedNoChange`，未写入） | — |
| 当前值超过上限时安全拒绝 | **自动化已验证**（Unreliable 用例） | 实机未复现该组合 |
| `fatigue` 方向 | **未验证**（阻塞其恢复功能） | 需冲刺/挥砍观察 |

---

## 7. 结论

1. **玩家真实最大生命值来自 `CharacterStatusData.maxhp`，不是控制器上的 `maxHP`/`FullHP`。**
   三个来源实测为 2000 / 1000 / 1000，原生面板显示 2000。
2. 其余四项属性本来就取自 `CharacterStatusData.max*`，来源正确，但**上限确实会动态变化**
   （实测 120→116、180→168→180），因此必须每次重读。
3. 上限的动态来源可追溯到 Constitution 与技能字段（官方文档 + 程序集双重印证）。
4. `fatigue` 的方向仍未知，因此其恢复功能保持关闭。
