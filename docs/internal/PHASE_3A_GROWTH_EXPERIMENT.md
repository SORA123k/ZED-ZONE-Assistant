# PHASE_3A_GROWTH_EXPERIMENT.md — 动态成长只读实验工具与实验设计

> **目的**：验证"最大生命值随成长动态变化"，并确认插件始终读取真实有效上限。
> **工具**：`src/Core/GrowthProbe.cs`（**只读**）
> **游戏**：ZED ZONE 1.0.16
> **日期**：2026-11-12

---

## 1. 工具特性（严格只读）

| 要求 | 实现 |
|---|---|
| 只读取，不修改任何属性 | ✅ 代码中只有字段读取，无任何赋值 |
| 不修改全局配置 | ✅ 未触碰 `GameConfigs` |
| 不修改存档 | ✅ 未触碰存档文件 |
| 不新增行为修改 Harmony Patch | ✅ `[HarmonyPatch]` 数量仍为 **0** |
| 保留 v0.3.0 一键恢复 | ✅ 恢复逻辑未改动 |
| 不影响现有 GUI 与日志系统 | ✅ 仅在 Settings 页加两行状态，日志为新增前缀 |

配置（`BepInEx\config\zedzone.ultimateassistant.cfg`）：

```ini
[Diagnostics]
GrowthLog = true
GrowthLogIntervalSec = 10      ; 无变化时的心跳间隔
```

行为：
- 数值**发生变化** → 输出一条 `[growth] CHANGE ...`
- 无变化 → 输出 `[growth] heartbeat ...`
- 场景/玩家变化也会被识别为变化

---

## 2. 记录字段（对应用户清单）

| # | 用户要求字段 | 实现来源 | 等级 |
|---|---|---|---|
| 1 | 角色等级 | **`level=N/A`**（见 §3）+ `coopLevel` | 说明见下 |
| 2 | Constitution | `attrGained_constitution`（已获得点数）<br>`attrExp_constitution`（当前经验）<br>`appliedMaxHPConstitution`（已应用到最大 HP 的体质） | **A** |
| 3 | 当前 HP | `HumanCharacterController.hp` | **B** |
| 4 | `CharacterStatusData.maxhp` | 同名字段 | **B** |
| 5 | `appliedMaxHPConstitution` | `CharacterData` 字段 | **A** |
| 6 | `skillProperty_MaxHPFloat` | `CharacterData` 字段 | **A** |
| 7 | `appliedMaxHPSkillFloat` | `CharacterData` 字段 | **A** |
| 8 | `maxHPBalanceFloat` | `CharacterData` 字段 | **A** |
| 9 | 当前场景 | `SceneManager.GetActiveScene().name` | **B** |
| 10 | 记录时间 | `DateTime.Now`（HH:mm:ss） | **B** |

**附带的对照字段**（为定位成长来源）：`maxHP`（控制器字段）、`FullHP`、
`appliedMaxHPBalanceFloat`、`skillProperty_StaminaFloat`、
`maxEnergy`、`maxHunger`、`maxThirst`、`maxFatigue`。

### 3. 关于"角色等级"

经核查（`CharacterData` 全字段 + `GameData` 全字段扫描），**本游戏没有单一的角色等级字段**：
- 只有**逐属性成长**：`attrGained_constitution` / `attrExp_constitution`（力量/敏捷同理）；
- `coopLevel` 是**联机等级**，不是角色等级；
- `zLevel` 是**地下层数**（sbyte），与等级无关；
- 技能等级靠 `GetSkillLevel(int skillID)` 按技能 ID 查询。

因此日志中 `level=N/A` 是**如实记录**，不是漏采。若将来发现真正的等级字段，再补充。
**不伪造等级数值。**

---

## 3. 日志格式

变更为：

```
[growth] CHANGE t=22:59:22 scene=Gaming level=N/A coopLevel=0 conGained=2 conExp=100.09
         hp=2000 maxhp=2000 maxHPfield=1000 FullHP=1000
         appliedMaxHPConstitution=20 skillProperty_MaxHPFloat=1 appliedMaxHPSkillFloat=1
         maxHPBalanceFloat=1 appliedMaxHPBalanceFloat=0 skillProperty_StaminaFloat=0
         maxEnergy=180 maxHunger=120 maxThirst=120 maxFatigue=100
```

（实际为单行，此处为便于阅读换行）

`heartbeat` 行内容相同，仅前缀不同；**做实验只需在操作前后各看一条 CHANGE 行**。

---

## 4. 五个实验（用户要求）

统一前提：
- 探针已开启（默认开）；
- 每次操作后**停留约 10 秒**再读日志，确保心跳刷新；
- **实验前后各截取一条 `[growth]` 行**；
- 全程不点"恢复"按钮（避免干扰数值）。

### 实验 1 — 升级前后对比

| 步 | 操作 |
|---|---|
| 1 | 记录当前 `[growth]` 行（记 `conGained` / `appliedMaxHPConstitution` / `maxhp`） |
| 2 | 正常游玩直到升级/属性点提升 |
| 3 | 再记录一条 `[growth]` 行 |
| 4 | 对比 `maxhp` 是否变化，以及哪个字段先变 |

**期望**：`maxhp` 变化时应能在同一条日志里看到 `appliedMaxHPConstitution` 或 `skillProperty_MaxHPFloat` 的对应变化 —— 这能反推计算公式。

### 实验 2 — 装备更换前后对比

| 步 | 操作 |
|---|---|
| 1 | 脱下所有可能加生命的装备 → 记录 |
| 2 | 穿戴一件加生命/体质装备 → 记录 |
| 3 | 再脱掉 → 记录 |
| 4 | 观察 `maxhp` 是否随穿脱变化；若不变，说明该装备不加最大生命 |

### 实验 3 — 技能/属性变化前后对比

| 步 | 操作 |
|---|---|
| 1 | 记录基线 |
| 2 | 使用属性点/技能点提升体质或与生命相关的技能 |
| 3 | 记录变化 |
| 4 | 观察 `appliedMaxHPConstitution` / `appliedMaxHPSkillFloat` / `maxHPBalanceFloat` 三者谁动了 |

### 实验 4 — 场景切换前后对比

| 步 | 操作 |
|---|---|
| 1 | 场景 A 记录 |
| 2 | 进入场景 B（进屋/出屋/上下地表） |
| 3 | 记录 |
| 4 | 观察 `scene` 变化时 `maxhp` 是否保持；玩家引用是否重建（`[state]` 会有新指针） |

### 实验 5 — 重新加载存档前后对比

| 步 | 操作 |
|---|---|
| 1 | 记录当前 |
| 2 | 存档 → 退出到主菜单 → 重新读档 |
| 3 | 记录 |
| 4 | 观察 `maxhp` 是否与存档前一致（验证最大值的持久化） |

---

## 5. 判读规则（避免事后解释）

| 现象 | 结论 |
|---|---|
| `maxhp` 随某个输入字段变化且**能对上** | 已找到成长公式的输入项 |
| `maxhp` 变化但**所有输入字段都没变** | 仍有未知来源 → 继续标注未验证，插件仍然"每次重读"作为兜底 |
| `maxhp` 始终不变（即使体质提升） | 说明当前角色的上限不由该路径计算 → 需要重新找来源 |
| `maxhp` 与 `maxHPfield` 差距变化 | 进一步证明控制器字段不可用 |

---

## 6. 验收标准

| 项 | 标准 |
|---|---|
| 探针不产生写操作 | 源码中无赋值；`harmony patches=0` |
| 探针稳定 | 连续运行 ≥10 分钟无 `[Error]`、无日志爆炸（默认 10s 一条） |
| 能识别变化 | 场景切换能产生 `CHANGE` 行（已验证） |
| 字段完整 | 用户清单 10 项全部出现在日志中（等级以 `N/A` 如实标注） |
| **不伪造** | 读不到的字段输出 `?`，不填 0、不猜值 |
