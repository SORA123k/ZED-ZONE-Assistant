# PHASE_3A_API_RESEARCH.md — 一键恢复的 API 研究

> **阶段**：Phase 3A 技术研究（**本轮不实现写入**）
> **游戏**：ZED ZONE **1.0.16** / build 26576287 / Unity 2023.1.18f1 / IL2CPP x64
> **interop**：`BepInEx\interop\Assembly-CSharp.dll`（2026-11-12 18:37 生成，晚于 GameAssembly 15:48 → 对应当前版本）
> **日期**：2026-11-12

## 证据等级

| 标记 | 含义 |
|---|---|
| **A｜已确认存在** | 当前 interop 中核实到精确签名 |
| **B｜已确认可调用** | 在运行的游戏 1.0.16 中实际调用成功（有日志） |
| **C｜仅反编译发现** | 只有签名，运行期未调用 |
| **D｜尚未找到可靠实现** | 无满足需求的接口 |

> **本轮的 B 级证据范围**：只覆盖"不需要玩家对象"的调用（Buff 表、全局开关、联机标志、
> `GameController.instance`）。**所有玩家属性读写仍未取得 B 级** —— 原因见
> `PHASE_3_ROADMAP.md` §0（主菜单 `playerCharacter == null`，且本会话桌面 1463×914
> 与游戏 2560×1600 布局不匹配，无法进入存档）。因此下表大量条目是 **A/C**，这是如实标注。

---

## 1. 数据模型（已确认）

```
GameController.instance                          // A/B
  .playerCharacter  : HumanCharacterController   // A；运行期在主菜单为 null
       : BasicCharacterController
    .hp / .energy / .fatigue / .hunger / .thirst // 全部是**属性**（不是字段）
    .characterData : CharacterData               // A
        .characterStatusData : CharacterStatusData  // A；唯一真实存储
            hp, energy, fatigue, hunger, thirst, sanity, casEnergy,
            bloodVolume, bodyTemperature, radiationDose      // **裸字段**
            maxhp, maxEnergy, maxFatigue, maxHunger, maxThirst, maxSanity // **裸字段**
        .buffDataList : Il2Cpp List<BuffData>    // A
```

**关键结构事实**：

- 控制器上**只有 `maxHP`**，**没有** `maxEnergy/maxFatigue/maxHunger/maxThirst`
  → 上限只能从 `characterStatusData.max*` 取。
- `CharacterStatusData` 的相关成员都是**字段**（写入走 `il2cpp_field_get_offset` 裸内存），
  整个类只有 6 个 get/set 方法指针（属于 static 常量，如 `maxBloodVolume`）。
- 控制器上的属性：`hp` 是 `Virtual_New`（基类声明新槽）、`HumanCharacterController.hp` 是
  `Virtual`（**真正的覆写**）；`energy/fatigue/hunger/thirst/sanity/casEnergy` 均为
  **非虚** `Public_get/set`。

---

## 2. 逐项 API 表（Phase 3A 六项目标）

### 2.1 Health — `hp`

| 项 | 内容 | 等级 |
|---|---|---|
| 字段/属性 | `BasicCharacterController.hp`（`Public_Virtual_New` get/set）<br>`HumanCharacterController.hp`（`Public_Virtual` get/set，**覆写**） | **A** |
| 真实数据类型 | `float` | **A** |
| 读取 | `player.hp`（经虚分派到 Human 覆写） | **C**（运行期未取到玩家） |
| 写入 | `player.hp = v`（同样经虚分派） | **C** |
| 上限 | `player.maxHP`（控制器上的 `float`） | **A** |
| 另存的上限 | `characterStatusData.maxhp`（裸字段） | **A** |
| 下限 / 方向 | 0 为下限、**越高越好**（≤0 判定死亡） | **C**（推断，未见方法体） |
| 原生恢复接口 | ✅ `AddBuff(11, Time.time + t)` → `hpPerSecond = +50`（**B**：Buff 表实测） | **B** |
| 其他原生 | `FullHP`（float，语义未确认）、`HealLingeringDeadInstance(string)`（仅尸体，**不适用**） | **C** |
| 直接赋值会漏什么 | setter 内部逻辑不可见 → 可能漏受击/死亡判定、漏 `lastLethalDamage` 更新、漏 UI 刷新 | **C（风险项）** |
| 主线程 | 必须主线程（IL2CPP 互操作） | **A** |

**注意**：`maxHP`（控制器）与 `maxhp`（statusData）是**两份存储**，可能出现不一致。
写入时应以 `player.maxHP` 为上限源，并记录两者差异到日志（便于发现异常）。

### 2.2 Stamina / Energy — `energy`（同一数据源）

| 项 | 内容 | 等级 |
|---|---|---|
| 属性 | `BasicCharacterController.energy`（`Public_get/set`，非虚） | **A** |
| 类型 | `float` | **A** |
| 上限 | `characterStatusData.maxEnergy` | **A** |
| 下限/方向 | 0 下限，**越高越好** | **B（间接）**：`EnergyRecover`(+5/s) 与 `StaminaPotion`(+3/s) 均写 `energyPerSecond`，证明"加 energy = 恢复体力" |
| 原生恢复 | ✅ `AddBuff(12, …)`（+5/s）、`AddBuff(28, …)`（+3/s） | **B** |
| 语义裁定 | **游戏内 STAMINA（持久力）与 ENERGY（精力）是同一数值**；本地化 5431 与 Buff 表双向印证 | **B** |
| 直接赋值风险 | 与 `OnTimeUpdate` 的消耗逻辑竞争 → 需幂等写 + 协调器 | **C** |

**结论**：3A 中 **Stamina 与 Energy 必须共用同一个写入通道**，只是两个 UI 入口；
不允许两个模块各写各的目标值（详见 `PHASE_3_SAFETY_DESIGN.md` §4）。

### 2.3 Fatigue — `fatigue`

| 项 | 内容 | 等级 |
|---|---|---|
| 属性 | `BasicCharacterController.fatigue`（`Public_get/set`，非虚） | **A** |
| 类型 | `float` | **A** |
| 上限 | `characterStatusData.maxFatigue` | **A** |
| **方向** | **未确认**。`HasFatigue(float)` / `CostFatigue(float)` 的存在暗示它像"可消耗资源"（越高越好）；<br>但 `fatigueWarningTimeFlag` 又像"疲劳累积到阈值告警"（越高越糟）。两者互斥。 | **C（未决）** |
| Buff 支持 | `BuffAttr` **没有任何 fatigue 字段** → 疲劳不受 Buff 驱动 | **B**（62 条表实测无此字段） |
| 原生恢复接口 | **未找到**（无 `AddBuff` 路径、无专属恢复方法） | **D** |
| 更新入口 | `UpdateStatus()` / `OnTimeUpdate(float)`（均 virtual） | **A** |

**结论：Fatigue 在 G2 通过前不实现"恢复"。** 唯一安全的动作是"只读显示"。
若方向判反，把疲劳拉满会让玩家直接陷入疲劳状态 —— 这是本项目最危险的一个误判点。

### 2.4 Hunger — `hunger`

| 项 | 内容 | 等级 |
|---|---|---|
| 属性 | `BasicCharacterController.hunger`（`Public_get/set`） | **A** |
| 上限 | `characterStatusData.maxHunger` | **A** |
| 方向 | 0 下限，**越高越饱**（`HungerRecover` +5/s 证明） | **B（间接）** |
| 原生恢复 | ✅ `AddBuff(9, …)` → `hungerPerSecond = +5` | **B** |
| 缺口 | 若上限为 100 且当前 0，靠 buff 需 ~20 秒 → "一键恢复"体验差 | **C** |
| 直接赋值风险 | 不影响 `Hunger`(17) debuff 的移除；仅改数值可能仍带着 debuff | **C** |

### 2.5 Thirst — `thirst`

与 Hunger 完全同构：`AddBuff(10, …)` → `thirstPerSecond = +5`（**B**），上限 `maxThirst`。
同样存在"渐进而非瞬时"的问题。

---

## 3. 原生恢复接口总表

| 接口 | 签名（A/C 级） | 用途 | 适用性 |
|---|---|---|---|
| `AddBuff` | `public virtual void AddBuff(int m_buffId, float m_buffEndTime)` | 加 Buff | ✅ **首选恢复手段**（hp 50/s、energy 5/s、hunger 5/s、thirst 5/s） |
| `RemoveBuff` | `public void RemoveBuff(int m_buffId)` | 移除单条 | 3C 用 |
| `ClearDebuffs` | `public virtual void ClearDebuffs(bool dotOnly)`<br>`public void ClearDebuffs(bool dotOnly, int keepBuffId)` | 清 debuff | 3C 用 |
| `RemoveAllBuffs` | `public void RemoveAllBuffs()` | 清全部 | ❌ **禁用**（会清正面） |
| `HealAllBodyPartInjuries` | `public void HealAllBodyPartInjuries()`（Human） | 治疗身体部位伤口 | 3C 候选，需验证 |
| `DrinkLiquid` | `public void DrinkLiquid(ItemData liquidData, float drinkVolume)` | 原生喝水 | ⚠ 需 `ItemData`，属于"消耗物品"，不适合辅助恢复 |
| `DrinkLiquidFromContainer` | `public void DrinkLiquidFromContainer(ItemData containerData, float drinkVolume)` | 从容器喝 | 同上 |
| `UseHoldingItem` | `public void UseHoldingItem()`（Human） | 使用手持物 | 同上 |

### 3.1 `AddBuff` 第二参数的语义（**未决，G3**）

签名写作 `float m_buffEndTime`，且 `BuffData` 的字段是 `buffEndtTime`（拼写如此）。
→ **强推断是"绝对结束时间"**，应传 `Time.time + 持续秒数`。
但方法体不可见，**必须实测**（传 5 vs 传 `Time.time+5`，看 buff 何时消失）。
**G3 未通过前，不允许把 R1 设为默认。**

### 3.2 恢复类 Buff ID（**运行期实测值**）

| ID | 名称 | 每秒效果 | 从 0 恢复满值(假设上限 100) |
|---|---|---|---|
| 11 | HPRecover | hp **+50**/s | ~2 s |
| 12 | EnergyRecover | energy **+5**/s | ~20 s |
| 28 | StaminaPotion | energy **+3**/s | ~33 s |
| 9 | HungerRecover | hunger **+5**/s | ~20 s |
| 10 | ThirstRecover | thirst **+5**/s | ~20 s |
| 26 | HealingSmoke | hp **+50**/s、energy **+10**/s、体温 +0.05/s | — |
| 33 | Respawn | hp **+2**/s、energy **+3**/s | — |

> 上限并非固定 100（由 `SetMax*()` 依 Constitution 计算），上表仅示意；
> 实际必须读 `characterStatusData.max*`。

**结论**：R1 对 HP 够快（2 s），对 energy/hunger/thirst **太慢**。
因此 3A 若追求"一键即满"，**必须**使用 R2（直接写属性 setter），这意味着 **G1 是硬前置**。

---

## 4. 运行期 Buff 表（62 条，**B 级**）—— 与官方文档的差异

官方 `mod-docs` 的 `11_Buff列表参考.md` 写"共 31 个 Buff"。运行期实际 **62 条**。

**新增且与项目相关的**：

| ID | 名称 | 性质 | 效果 |
|---|---|---|---|
| **32** | **Bleeding** | **debuff** | hp **-3**/s ← 官方文档中没有，3C 清除白名单必须补上 |
| 33 | Respawn | 正面 | hp +2/s、energy +3/s |
| 34–37 | headRecovery / torseRecovery / armRecovery / legRecovery | 正面 | 伤口恢复类 |
| 38/39 | Bloodlust / Bloodlust2 | 正面 | speedGain 1.08 / 1.12 |
| 40 | KillStaminaRegen | 正面 | energy +3/s |
| 41–48 | Pet* 系列 | 混合 | 宠物用；48 `PetShred` **可致死 -25/s** |
| 49 | ScaleArmor | 正面 | armorPointsGain +40 |
| 51–53 | Radiation Sickness (Mild/Moderate/Severe) | **debuff** | hp 0 / -1 / -3 |
| 54/55 | Iodine Shield / Anti-Radiation Purge | 正面 | 辐射相关 |
| 56–62 | 食物类 Buff | 正面 | 含 hunger +0.02/s、hp +0.3~0.5/s |

**对 Phase 3 的直接影响**：
1. 3C 的"负面状态清除白名单"**必须基于这 62 条重建**，不能沿用 Phase 1 的 11 条；
2. `32 Bleeding`、`48 PetShred`、`51–53 辐射病` 是旧文档遗漏的 debuff；
3. Buff ID 空间 > 62 未知，**清除逻辑必须白名单，不能"非白名单即清"**。

---

## 5. "直接赋值会漏什么"—— 逐项风险分析

| 可能遗漏的点 | 依据 | 等级 | 缓解 |
|---|---|---|---|
| 受击/死亡判定未重算 | 未见 setter 体 | C | G1：设 `hp=maxHP-1` 后测受击与死亡 |
| UI 不刷新 | `PlayerStatusPanel` 有 `hpBar/energyBar/fatigueBar/hungerBar/thirstBar`（A），但刷新方式未知 | C | 目视对照（G4） |
| `FullHP` / `lastLethalDamage` 未更新 | 两者是独立字段/属性 | C | 不依赖它们做判断 |
| 饥饿/口渴 debuff（17/18）残留 | Buff 与数值是两套 | C | 3C 处理；3A 只改数值 |
| 存档不持久 | 字段是否被序列化未验证 | C | 测试：改值→存档→读档 |
| 与消耗逻辑竞争导致回退 | `OnTimeUpdate` 每 tick 扣值 | C | 3B 用"只升不降钳制"；3A 一次性写 |

---

## 6. 主线程要求

- 所有 IL2CPP 互操作（属性读写、`AddBuff`）**必须在 Unity 主线程**执行。
- 现有架构的 `Update()` / `OnGUI()` 都在主线程，可直接使用。
- **禁止**在 `System.Threading.Timer`、`Task.Run`、`ThreadPool` 中调用这些 API。
- 若将来需要定时器，一律用 `Time.unscaledTime` 在 `Update()` 内做时间判断，不开后台线程。

---

## 7. 死亡 / 重生 / 场景切换的安全性

| 守卫 | 成员 | 等级 | 用法 |
|---|---|---|---|
| 死亡标志 | `deadFlag`（**属性** get/set） | A | 为 true 时**拒绝一切写值** |
| 联机死亡影子 | `netDeadFlag`（字段） | A | 同上（联机时以它为准） |
| 重生无敌窗口 | `netRespawnInvincibleUntil`（float 字段） | A | 与 `Time.time` 比较，窗口内不写（避免与复活逻辑打架） |
| 玩家引用失效 | `(UnityEngine.Object)player == null` | A | 每次操作前重取 + 判空 |
| 场景切换 | `SceneManager.GetActiveScene().name` | A | 变化时丢弃缓存的目标值、重取玩家 |

**规则**：`deadFlag || netDeadFlag` → 直接拒绝并记日志；引用为 null → 拒绝；
场景变化 → 重建写入上下文。

---

## 8. 1.0.16 兼容性

| 项 | 结论 | 依据 |
|---|---|---|
| interop 是否对应当前版本 | ✅ 是 | interop 时间戳晚于 GameAssembly |
| 关键签名是否变化 | 未发现变化（`hp/energy/fatigue/hunger/thirst`、`AddBuff`、`CharacterStatusData` 均在） | A |
| 新增风险 | 1.0.16 新增**代码模组安全检测**（`MainMenuController.ShowModRiskWarningWhenIdle`） | B（游戏日志 + 实测弹窗） |
| Buff 数量变化 | 官方文档 31 → 实际 62 | B |
| `ModLoader` 行号漂移 | `:613/:706` → `:623/:737` | B（游戏日志） |

**兼容性策略**：不依赖行号、不依赖方法体；只依赖**类型+成员名**，并在启动时做一次
"签名自检"（缺失则禁用对应功能并日志告警），沿用旧项目的 `TryPatch` 降级思路。

---

## 9. 逐项可行性结论

| 目标 | 结论 | 条件 |
|---|---|---|
| Health 恢复 | ✅ 可做（R1 够快；R2 更精确） | R2 需 G1 |
| Energy / Stamina 恢复 | ✅ 可做，但**必须合并为一个写入通道** | G4（确认对应哪根条） |
| Hunger 恢复 | ✅ 可做；R1 太慢 → 建议 R2 | G1 |
| Thirst 恢复 | ✅ 同上 | G1 |
| **Fatigue 恢复** | ⛔ **暂不建议实现** | G2 未通过 |
| Sanity / casEnergy | 不在 3A 范围（读数已就绪，写入未研究） | — |

---

## 10. 下一步（Phase 3A 开发首日应完成的验证）

1. **G2 五步实验**（fatigue 方向）——只需玩家操作一次；
2. **G3**：`AddBuff(11, 5)` vs `AddBuff(11, Time.time+5)` 观察 buff 持续时间；
3. **G1**：`hp = maxHP - 1` 后检查 UI、受击、死亡、存档；
4. **G4**：`energy` / `fatigue` 分别对应原生状态面板哪根条。

这四项都是**分钟级**验证，但缺一项就不能把对应功能设为默认可用。
