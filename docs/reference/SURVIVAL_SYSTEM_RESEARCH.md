# ZED ZONE 生存系统（需求/状态）静态逆向研究报告

> **阶段**：Phase-1 静态代码调查（只读）。**未运行游戏，未安装/加载任何 mod，未改动游戏目录任何文件。**
> **目标**：为「生存辅助」（饱食/饥渴/体力/疲劳）功能确定真实字段、真实入口、真实风险。
> **结论强度约定**（全文遵守）：
>
> | 标记 | 含义 |
> |---|---|
> | 【事实-程序集】 | 由 `BepInEx\interop\Assembly-CSharp.dll` 反编译**直接确认**的存在性/类型/签名/静态性 |
> | 【事实-官方文档】 | 由官方 MOD 文档或游戏本地化表**直接确认**的文本/字段名 |
> | 【推断】 | 由上述事实推理得出，**方法体不可见**（interop 是 trampoline 桩，无实现） |
> | 【未验证】 | 静态手段无法判定，必须实机测试 |
>
> **关键方法论约束**：interop 程序集是 IL2CPP 互操作**桩代码**，方法体只会 `il2cpp_runtime_invoke` 转发。
> 因此「某方法存在、参数是什么、是字段还是属性、是否 static」= **事实**；「它内部怎么算」= **一律【推断】**。
> `[CallerCount(N)]` 是 Il2CppInterop 的**引用区间长度**，**不是调用次数**，本报告不将其作为任何证据。
> 也**不**根据类名/方法名字面猜测行为——所有行为结论均由多组事实交叉支撑后才列出。

---

## 0. 结论速览（TL;DR）

| 问题 | 结论 | 强度 |
|---|---|---|
| 真实存储位置 | **`CharacterData.characterStatusData`**（类型 `CharacterStatusData`）是唯一真实存储 | 【事实-程序集】 |
| 控制器上的 `hunger` 等 | 是**属性（get+set 访问器方法）**，不是字段；`maxHunger/maxThirst/maxEnergy/maxFatigue` 在控制器上**根本不存在** | 【事实-程序集】 |
| 四个核心需求字段 | `hunger` / `thirst` / `energy` / `fatigue`，均为 **`float` 实例字段**于 `CharacterStatusData` | 【事实-程序集】 |
| 最大值是否 0–100 | **不是**。`maxHunger/maxThirst` 是 `float` 实例字段，由 `CharacterData.SetMaxHunger()/SetMaxThirst()` 计算写入，且**受 Constitution 影响** | 【事实-程序集】+【事实-官方文档】 |
| 自然衰减入口 | **`BasicCharacterController.OnTimeUpdate(float)`**（virtual）→ `HumanCharacterController.OnTimeUpdate(float)`（override）；另有 `UpdateStatus()` 与 `ApplyBuffStatusTick(...)` 两条平行写入口 | 【事实-程序集】+【推断】 |
| 消耗倍率安全接口 | **有**，且**不需要 Harmony**：`GameConfigs.hungerCostPerDay` / `thirstCostPerDay` 等是 **static float 字段** | 【事实-程序集】 |
| 推荐实现 | 锁值用「只升不降钳制」；倍率用 static 配置字段；**不要**用 `CostEnergy` 前缀拦截 | 【推断】 |
| 联机覆盖风险 | **低**：`MsgPlayerState` 只同步 `hp`，**不同步** hunger/thirst/energy/fatigue | 【事实-程序集】 |

---

## 1. 四个核心需求的真实字段与最大值

### 1.1 存储层级（已定论）

```
GameController.instance.playerCharacter        // HumanCharacterController : BasicCharacterController
  .characterData                               // CharacterData        （字段, 非 static）
    .characterStatusData                       // CharacterStatusData  （字段, 非 static）★真实存储
      .hunger/.thirst/.energy/.fatigue          // float 实例字段
      .maxHunger/.maxThirst/.maxEnergy/.maxFatigue
```

`characterStatusData` 的**声明者只有一个**：`CharacterData`。
证据（全程序集扫描 `NativeClassPtr, "characterStatusData"`，唯一命中）：

```
CharacterData :: characterStatusData
```

### 1.2 控制器 vs `CharacterStatusData` —— 谁权威

这是本任务最关键、也最容易踩坑的一点。**控制器上的 `hunger/thirst/energy/fatigue/hp/sanity/casEnergy` 是属性，不是字段。**

`-t BasicCharacterController` 反编译后按「使用 `il2cpp_field_get_offset`（字段）」还是「使用 `NativeMethodInfoPtr_get_/set_` + `runtime_invoke`（属性）」分类：

| 成员 | 种类 | get 访问器 | set 访问器 |
|---|---|---|---|
| `hp` | **PROPERTY** | ✓ | ✓ |
| `energy` | **PROPERTY** | ✓ | ✓ |
| `casEnergy` | **PROPERTY** | ✓ | ✓ |
| `hunger` | **PROPERTY** | ✓ | ✓ |
| `thirst` | **PROPERTY** | ✓ | ✓ |
| `fatigue` | **PROPERTY** | ✓ | ✓ |
| `sanity` | **PROPERTY** | ✓ | ✓ |
| `maxhp` | FIELD | — | — |
| `maxEnergy` | **不存在** | — | — |
| `maxHunger` | **不存在** | — | — |
| `maxThirst` | **不存在** | — | — |
| `maxFatigue` | **不存在** | — | — |
| `staminaRegenScale` | **PROPERTY（只读）** | ✓ | 无 |
| `buffStaminaRegenScale` | FIELD | — | — |

【事实-程序集】

**含义（【推断】，但推论极强）**：控制器上的 `hunger` 是一对 `get_hunger` / `set_hunger` 方法，几乎必然只是把读写转发到
`characterData.characterStatusData.hunger`。**控制器的同名成员不持有独立状态**——这正是「看起来改对了但没效果」的经典陷阱来源。
真正权威、唯一需要盯住的是 **`CharacterStatusData` 的实例字段**。

> 之所以能断定「转发」而非「独立缓存」：控制器上**完全没有** `maxHunger/maxThirst/maxEnergy/maxFatigue` 这四个成员，
> 而一个持有独立数值的池子必然需要自己的上界。上界只存在于 `CharacterStatusData`，说明数值本身也只存在于那里。

### 1.3 字段清单

`CharacterStatusData`（`System.Serializable`，实例字段）：

| 字段 | 类型 | 归属 | 证据 |
|---|---|---|---|
| `hunger` / `maxHunger` | `float` | 实例 | 【事实-程序集】 |
| `thirst` / `maxThirst` | `float` | 实例 | 【事实-程序集】 |
| `energy` / `maxEnergy` | `float` | 实例 | 【事实-程序集】 |
| `fatigue` / `maxFatigue` | `float` | 实例 | 【事实-程序集】 |
| `sanity` / `maxSanity` | `float` | 实例 | 【事实-程序集】 |
| `hp` / `maxhp` | `float` | 实例 | 【事实-程序集】 |
| `casEnergy` | `float` | 实例 | 【事实-程序集】 |
| `bloodVolume`、`m_bodyTemperature`、`radiationDose`、`bodyTemperature` | `float` | 实例 | 【事实-程序集】 |
| `maxBloodVolume`、`deadBloodVolume`、`standardBodyTemperature`、`bodyTemperatureOffsetFloat`、`minBodyTemperature`、`maxBodyTemperature` | `float` | **static** | 【事实-程序集】 |

**注意**：`maxhp` 在 `CharacterStatusData` 上是**实例字段**；而在 `BasicCharacterController` 上 `maxhp` 也是**字段**（不是属性）。
两者不是同一个东西，改 HP 上限时须确认目标。

### 1.4 取值范围：不是固定 0–100

- 【事实-程序集】`maxHunger`、`maxThirst`、`maxEnergy`、`maxFatigue` 都是 `float` **数据字段**，不是常量。
- 【事实-程序集】`CharacterData` 提供 `SetMaxHunger()` / `SetMaxThirst()` / `SetMaxEnergy()` / `SetMaxFatigue()` / `SetMaxHP()` 用于**计算并写入**这些最大值，另有 `RefreshStatus()`。
- 【事实-官方文档】本地化 ID 2564：
  `CONSTITUTION — Affects max health and max stamina, out-of-combat health/stamina recovery (5s after being hit), debuff resistance, body temperature stability, **and hunger/thirst caps**.`
  → 饱食/饥渴**上限**由 Constitution 决定。

**结论**：数值区间**由角色属性计算得出**，**不是**写死的 0–100。
任何 mod 都**禁止把恢复值写成硬编码常量**（例如直接把 hunger 写成 100），因为上限可能远大于 100 或小于 100。
正确写法：写 `maxHunger`（读取当前上限）。

### 1.5 歧义裁定：`energy` 到底是不是 UI 的「体力」

这是本任务明确要求裁定的问题。**静态证据不足以给出唯一答案**，必须如实分为两组。

#### 事实层（不可争辩）

【事实-程序集】`PlayerStatusPanel`（HUD 面板）**同时**拥有下列 Bar 字段：

```
hpBar, hungerBar, thirstBar, energyBar, fatigueBar, apBar   (+ fogBar / radDoseBar / thrusterFuelBar)
```

即：**`energy` 与 `fatigue` 是两个各自独立显示的状态条**，不存在「同一个条被两个字段驱动」的情况。
并且 `PlayerStatusPanel` **没有** `staminaBar`；`staminaBarFill` 只出现在 `TeamNPCUI`（NPC 队伍 UI）。

【事实-官方文档】本地化表（`Localization_en_to_ja.csv`，`#source_lang,English`）：
- ID 53 `STAMINA` → 持久力；ID 54 `ENERGY` → 活力；ID 56 `CONSTITUTION` → 体力
- ID 2562 `Stamina affects the character's maximum stamina. Running and attacking consume stamina.`
- ID 2563 `Energy affects the character's building and crafting abilities.`
- ID 5430 `Stamina Recovery Rate`；**ID 5431 `Adjusts the natural recovery rate of character stamina (energy).`**
- ID 900465–900470 是一整块**食物字段标签**：`HP / Hunger / Thirst / Fatigue / Sanity / Energy`

【事实-程序集】`GameConfigs`（**static 类**）中同时存在两组 per-second / per-day 常量：

| 组 | 字段 |
|---|---|
| energy 组 | `runningEnergyCostPerSecond`、`energyRecoverPerSecond`、`defaultHandAttackEnergyCost`、`grappleHookEnergyCost`、`casEnergyRecoverSpeed` |
| fatigue 组 | `meleeAttackFatigueCost`、`breakDoorFatigueCost`、`picklockFatigueCost`、`igniteFatigueCost`、`collectFatigueCost`、`rangedWeaponFireFatigueCost`、`sleepFatigueCostPerDay`、**`sleepFatigueRecoverPerDay`** |
| hunger/thirst 组 | `hungerCostPerDay`、`thirstCostPerDay`、`hungerThirstBaseCostFloat` |

【事实-程序集】`BuffAttr` 的每秒字段只有 `hungerPerSecond` / `thirstPerSecond` / `energyPerSecond` / `hpPerSecond` / `bodyTemperaturePerSecond` / `staminaRegenScale`，
**没有 `fatiguePerSecond`**。→ **fatigue 不受 buff 每秒驱动**（与官方文档 11_Buff列表参考.md 的列结构完全一致）。

#### 两个候选假设

| | H1（本报告主推，【推断】） | H2（备选，【推断】） |
|---|---|---|
| `fatigue` | = **体力/耐力（Stamina）可消耗池** | = 疲劳累积值（越大越累） |
| `energy` | = **精力（Energy）**，跑步/弓/爪钩/翻滚/处决消耗 | = 体力/耐力可消耗池 |
| 支持 | 全部 fatigue 相关常量都叫 `*FatigueCost`（**Cost=消耗**）；`sleepFatigueRecoverPerDay`（睡觉**恢复**它）；`CharacterTired()` + `fatigueWarningTimeFlag`（池子见底→「累了」+警告）；`staminaRegenScale`/`buffStaminaRegenScale` 作用于它 | 本地化 5431 把 stamina 直接括注为 `(energy)`；`GameConfigData.staminaRegenRate` 是难度档「Stamina Recovery Rate」 |
| 反对 | 本地化 2562 说「跑步和攻击消耗 stamina」，而跑步消耗的是 `runningEnergyCostPerSecond` | `energy` 组常量里也有 `defaultHandAttackEnergyCost`（攻击），与 2563「energy 影响建造/制作」不完全吻合 |

**两者都自洽，静态分析到此为止。** 名字本身**不能**作为定论依据（本任务明令禁止按名字猜行为），
且游戏自身文本就已混用（5431 把 stamina 与 energy 并列）。

#### 能判定它的精确实验（Phase-2）

用 BepInEx 打一条日志（`BasicCharacterController.Update` 或 `OnTimeUpdate` 的 Harmony Postfix），
同一帧同时打印四个值，然后在**受控顺序**下触发行为：

1. 静置（记录基准斜率）
2. 持续冲刺 10 秒 → 看 `energy` 还是 `fatigue` 下降
3. 挥砍一次 → 看哪个下降、降幅是否等于 `meleeAttackFatigueCost` / `MeleeAttackInfo.staminaCost`
4. 睡一觉 → 看哪个回升（预期 `fatigue`，因 `sleepFatigueRecoverPerDay`）
5. 用 `BuffManager.GetBuffAttrByID(12)`（EnergyRecover）后看哪个回升（预期 `energy`，因 `energyPerSecond`）

**判定规则**：
- 若「冲刺↓的是 energy」且「挥砍↓的是 fatigue」→ **H1 成立**（体力=fatigue，精力=energy）
- 若「冲刺↓的是 fatigue」且「挥砍↓的是 energy」→ **H2 成立**（体力=energy）
- 同时截图 HUD 的 energy 条 / fatigue 条变化，即可把**代码字段 ↔ 玩家可见 UI 条**彻底对上。

> 在此之前，**任何对外文案都不要写死「体力=energy」或「体力=fatigue」**。

### 1.6 UI 标签的中文映射：未证实（重要）

【事实-官方文档】本地化目录 `ZEDZONE_Data\StreamingAssets\Localization\` 只有
`en_to_{de,es,fr,ja,ko,pl,pt,ru}.csv` **共 8 个**，**没有 `zh`/`en_to_zh`**。
所有文件头均为 `#source_lang,English` → **英文是源语言，中文是引擎内嵌源文本，不在这些表里**。

因此：
- 上面所有 `English → 日本語` 的对照是**直接证据**；
- 但 **「饱食 / 饥渴 / 体力 / 疲劳」这些中文 UI 字样 ↔ 代码字段的映射，目前没有任何直接证据**，属【未验证】。
- 特别提醒：本任务下达时一度把 `STAMINA` 当作「体力」，而**本地化表实际是** `STAMINA→持久力`、`CONSTITUTION→体力`、`health→体力`（ID 5868，出现在「感染者永久提升」的变异词条里）。
  **同一个"体力"至少对应三个不同源词**，绝不能反推字段。

---

## 2. 自然衰减入口（"不消耗"必须拦截的地方）

### 2.1 主入口：`OnTimeUpdate(float)`

【事实-程序集】

```csharp
// BasicCharacterController
public unsafe virtual void OnTimeUpdate(float m_addTimeFloat);   // token 存在，Virtual_New
// HumanCharacterController
public unsafe virtual void OnTimeUpdate(float m_addTimeFloat);   // override（无 Virtual_New 标记）
// PetCharacterController
public unsafe virtual void OnTimeUpdate(float m_addTimeFloat);   // override
// GameController
public unsafe void OnTimeUpdate(float m_addTimeFloat);           // 无参重载 OnTimeUpdate() 亦存在
```

- 参数是 **`float` 时间增量**，与「按游戏内时间衰减」的语义完全吻合。
- `HumanCharacterController`（玩家）**重写了它** → 玩家专属的需求衰减逻辑极可能就在这个 override 里。【推断】
- `GameController.OnTimeUpdate(float)` 是最可能的**统一分发者**（把 delta 推给各计时器）。【推断】

### 2.2 平行入口：`UpdateStatus()`

【事实-程序集】

```csharp
// BasicCharacterController
protected unsafe virtual void UpdateStatus();    // protected + Virtual_New
// HumanCharacterController
protected unsafe virtual void UpdateStatus();    // override
// MechController
protected unsafe virtual void UpdateStatus();    // override
```

每帧/每逻辑步无参调用，且被玩家类重写 → 与 `OnTimeUpdate` 互补的另一条状态刷新路径。【推断】

### 2.3 第三条入口：Buff 每秒写入（最容易被漏掉）

【事实-程序集】

```csharp
// BasicCharacterController
public unsafe virtual void ExecuteBuffs();
public unsafe void ApplyBuffStatusTick(BuffAttr buffAttr, float debuffScale, float buffPowerScale, float burningDamageScale);
// BuffManager
public unsafe BuffAttr GetBuffAttrByID(int id);
public unsafe void Init();
```

`BuffAttr` 的每秒字段（【事实-程序集】）：`hungerPerSecond`、`thirstPerSecond`、`energyPerSecond`、`hpPerSecond`、`bodyTemperaturePerSecond`、`staminaRegenScale`、`hpRecoverScale`、`radiationDecayScale`、`radiationIntakeScale`、`perceptionScale`、`hpDrainFloorPercent`、`armorPointsGain`。

【事实-官方文档】11_Buff列表参考.md 印证了这些字段确实按秒作用，例如：
- Buff 9 `HungerRecover`：饥饿/秒 **+5**
- Buff 10 `ThirstRecover`：口渴/秒 **+5**
- Buff 12 `EnergyRecover`：精力/秒 **+5**
- Buff 15 `FoodPoison`（debuff）：饥饿/秒 **−1**、口渴/秒 **−1**、精力/秒 **−1**
- Buff 17 `Hunger`（debuff）：HP/秒 −5、速度 −10%
- Buff 19/20 `Over Weight`：饥饿/秒 −0.025 / −0.05

**结论**：只锁 `OnTimeUpdate` 是不够的。**食物中毒（15）、超重（19/20）会绕过自然衰减入口，按秒持续扣 hunger/thirst/energy。**
任何"无限饱食"实现若只处理自然衰减，玩家一旦中食物中毒/超重就会看到数值仍然掉。
这是本报告对实现方案最重要的一条约束。

### 2.4 第四条入口：动作直扣（`Cost*` 系列）

【事实-程序集】`BasicCharacterController`：

```csharp
public unsafe virtual bool HasEnergy(float m_energy);
public unsafe virtual bool HasFatigue(float m_fatigue);
public unsafe virtual bool CostEnergy(float m_energy);    // HumanCharacterController 有 override
public unsafe virtual bool CostFatigue(float m_fatigue);  // 仅 BCC，Human 无 override
public unsafe void MarkEnergyCostTime();
```

【事实-程序集】`HumanCharacterController` 额外：

```csharp
public unsafe virtual bool CostEnergy(float m_energy);            // override
public unsafe bool PayDodgeEnergy();
public unsafe float GetMeleeAttackEnergyCost(int someInt);        // private
public unsafe void  RollingEnergyCost...                          // 字段 rollingEnergyCost
```

**关键缺口**：**全程序集内不存在 `CostHunger` / `CostThirst` / `AddHunger` / `DecayHunger` 之类的方法**
（按 `NativeMethodInfoPtr_*Hunger*` / `*Thirst*` 全量枚举，只有 `get_hunger`、`set_hunger`、`SetMaxHunger`、`GetPetHungerValue`、`AddPetHungerBuff`、`OnHover_HungerBar`、`IsHiredAndCriticallyThirsty` 等）。

→ **hunger / thirst 没有可单独 patch 的"扣减函数"**。它们的减少只能发生在
`OnTimeUpdate` / `UpdateStatus` 的**方法体内**，或 `ApplyBuffStatusTick` 内。
这对第 4 节策略选择是决定性约束。

### 2.5 衰减量级（配置来源）

【事实-程序集】`GameConfigs`（**static 类**，字段均为 `public unsafe static float`）：

| 字段 | 语义（由命名+本地化交叉确认，【推断】） |
|---|---|
| `hungerCostPerDay` | 饱食度每（游戏内）日下降量 |
| `thirstCostPerDay` | 口渴度每日下降量 |
| `hungerThirstBaseCostFloat` | 饱食/口渴基础系数 |
| `energyRecoverPerSecond` | 精力每秒自然恢复 |
| `runningEnergyCostPerSecond` | 跑步每秒精力消耗 |
| `sleepFatigueRecoverPerDay` | 睡眠每日恢复（fatigue 池） |
| `sleepFatigueCostPerDay` | 睡眠相关每日消耗项 |
| `meleeAttackFatigueCost` / `breakDoorFatigueCost` / `picklockFatigueCost` / `igniteFatigueCost` / `collectFatigueCost` / `rangedWeaponFireFatigueCost` | 各类动作的 fatigue 消耗 |
| `casEnergyRecoverSpeed` | `casEnergy` 恢复速度 |

【事实-程序集】`GameConfigData`（难度档）：

```csharp
public unsafe int   hungerThirstRate;          // 字段，整型枚举
public unsafe int   staminaRegenRate;          // 字段，整型枚举
public unsafe float hungerThirstRateFloat;     // 只读属性（get-only）
public unsafe float staminaRegenRateFloat;     // 只读属性（get-only）
```

【事实-官方文档】本地化 ID 5420–5429 正是这个枚举的 4 档文案：
`Hunger & Thirst Rate` — 半速 / 标准 / 快 50% / 双倍速。
ID 5430–5439 为 `Stamina Recovery Rate` 的 4 档。

→ **「消耗倍率」在游戏里本就是一等公民（难度设定）**，这是第 6 节的立足点。

### 2.6 NPC 共用同一套需求系统

【事实-程序集】
```csharp
GOAPGoal_SatisfyNeeds.GetPriority(GOAPAgentState)           // override
GOAPGoal_SatisfyNeeds.GetNeedPriority(float currentValue, float maxValue)  // private static
GOAPAgent.UpdateVitalStates()                               // protected
GOAPNPCManager.hungerThreshold / thirstThreshold            // 字段
GOAPAction_EatFood / GOAPAction_DrinkWater / GOAPAction_Sleep / GOAPAction_HealStamina / GOAPAction_ClearDebuff_Overweight
PetSystem.petHungerCostPerDay / petMaxHunger; PetData.get_hungerDecayScale
```

`GetNeedPriority(float currentValue, float maxValue)` 直接证明：需求系统以 **(当前值, 最大值)** 二元组计算优先级，**玩家与 NPC/宠物共用同一模型**。

**实现要求**：任何全局补丁必须**按控制器实例过滤**（只对 `GameController.instance.playerCharacter` 生效），
否则会让 NPC 永不饥饿、AI 目标失效（`GOAPGoal_SatisfyNeeds` 永不触发），属于功能性破坏。

---

## 3. 恢复入口（吃 / 喝 / 睡 / Buff）

### 3.1 进食（吃）

【事实-程序集】

```csharp
// BasicCharacterController
public unsafe virtual void UseFood(ItemData m_itemdata, Il2CppSystem.Func<bool> consumeFood);
public unsafe BasicCharacterController.MealBuffResult AddFoodBuffs(ItemData m_itemdata);
public unsafe bool ExecuteFoodRecoveryBuffsOffClock();
public unsafe static Il2CppStructArray<int> foodRecoveryBuffIds;   // ★ static
// HumanCharacterController
public unsafe virtual void UseFood(ItemData m_itemdata, Il2CppSystem.Func<bool> consumeFood);  // override
public unsafe void TriggerEatAnimation();
public unsafe void StopEatAnimation();
public unsafe void PlayMedicalUseSound(ItemAttr_Food attr);
```

`MealBuffResult`（嵌套类型，`BasicCharacterController.MealBuffResult`）只有两个成员【事实-程序集】：
```csharp
public unsafe bool        hot;                  // 是否"热食"
public unsafe List<int>   appliedMealBuffIds;   // 实际生效的 buff id 列表
```

`ItemAttr_Food` 的数值字段【事实-程序集】，与官方文档 07_食物MOD速查.md **完全一致**：
```
hp, hunger, thirst, fatigue, sanity, energy, bloodVolumeRecover,
buffConfigs, recoverBuffRealTime
```
另有 `GetCookedGainScale(Il2CppStructArray<float>, float)` —— **烹饪会按倍率放大收益**【推断】。

**调用路径（【推断】，方法体不可见，但结构唯一自洽）**：
```
玩家使用食物 → HumanCharacterController.UseFood(item, consumeFood)
   ├─ 读取 ItemAttr_Food.hunger/thirst/energy/fatigue/sanity/hp (+ bloodVolumeRecover)
   │     → 直接加到 CharacterData.characterStatusData 对应字段（正向累加）
   ├─ AddFoodBuffs(item) → 返回 MealBuffResult{ hot, appliedMealBuffIds }
   │     └─ ItemAttr_Food.buffConfigs 中 itemId 即 buffId（官方文档 11 明确此复用）
   └─ ExecuteFoodRecoveryBuffsOffClock()
         —— 名字含 "OffClock"，【推断】为"把这些恢复型 buff 脱离正常时钟立即结算一次"，
            使食物收益即时体现，而不是等下一 tick；返回 bool 表示是否执行。
foodRecoveryBuffIds（static int[]）是"哪些 buffId 属于食物恢复类"的白名单，
   供 AddFoodBuffs / ExecuteFoodRecoveryBuffsOffClock 判定。
```

→ **食物回饱食的真正落点是 `CharacterStatusData.hunger` 直接累加**，不经过任何 `CostHunger` 反向函数。
所以"禁止扣减"类补丁**不会**影响食物恢复；而"只升不降钳制"也**不会**阻止食物增益。这是好消息。

### 3.2 喝水

【事实-程序集】`HumanCharacterController`：
```csharp
public unsafe void DrinkLiquid(ItemData m_itemdata, float someFloat);
public unsafe void DrinkLiquidFromContainer(ItemData m_itemdata);
```
`ItemAttr_Liquid` 同样带 `hunger / thirst / energy / fatigue` 四个字段【事实-程序集】。
→ 液体与食物走**不同入口**，实现时两处都要覆盖。

### 3.3 睡眠

【事实-程序集】
```csharp
// HumanCharacterController
public unsafe void    StartSleeping(UnityEngine.Vector3 pos, int someInt, float someFloat);
public unsafe void    StartSleepingNPC();
public unsafe bool    get_isSleeping();               // 属性
public unsafe float   get_sleepingWakeupTime();       // 属性
public unsafe void    ExtendNPCSleep(float);
public unsafe bool    TickNPCSleepPose();
public unsafe void    ExitNPCSleepPose(string, bool);
// 相关类型
TimeController._SleepCoroutine / _SleepCoroutineInner
DeployableItem_Sleepingbag, SleepingPanel
ZedZone.Net.MsgSleepState, NetSleepVote, MsgSleepVote/MsgSleepVoteRequest/MsgSleepVoteReply
GameConfigs.sleepFatigueRecoverPerDay / sleepFatigueCostPerDay
```
【事实-官方文档】`sleepFatigueRecoverPerDay` 的存在说明睡眠**按「日」结算恢复**。
【推断】睡眠 = 推进游戏内时间 + 按 `sleepFatigueRecoverPerDay` 结算 `fatigue`（这正是判定 H1/H2 的实验 4）。

**联机注意**：`NetSleepVote` 说明多人睡眠需要投票；单机改睡眠结算会影响时间推进，
不建议在"生存辅助"里拦截睡眠，只做读取。

### 3.4 Buff 系统

【事实-程序集】
```csharp
BuffManager.GetBuffAttrByID(int) → BuffAttr
BuffManager.Init()
BasicCharacterController.AddBuff(int m_buffId, float m_buffEndTime);        // virtual
BasicCharacterController.AddBuff_Overlay(int m_buffId, float m_addedInGameTime); // 返回 bool
BasicCharacterController.NetApplyVisualBuff(int m_buffId, float m_buffEndTime);
BasicCharacterController.RefreshBuffEffects(int m_buffId, bool refreshPanel = true);
BasicCharacterController.RemoveBuff(int m_buffId);
BasicCharacterController.ClearDebuffs(bool dotOnly);  /  (bool, int keepBuffId)
BasicCharacterController.OnStatBuffChanged(int m_buffId);                  // virtual
BasicCharacterController.ExecuteBuffs();
BasicCharacterController.ApplyBuffStatusTick(BuffAttr, float, float, float);
```
`OnStatBuffChanged(int)` 的存在【推断】表明：Buff 增删会触发一次"属性重算"，
很可能就是 `CharacterData.RefreshStatus()` / `SetMax*()` 的调用来源之一。

---

## 4. 三种「锁值/恢复」实现策略对比

> 前提共识：**真实存储 = `CharacterData.characterStatusData.<field>`**；控制器同名成员是转发属性。
> 因此所有写操作都应直达 `CharacterStatusData` 的字段（或使用其 setter），**不要只写控制器属性**（多一次转发+可能被重算）。

### 策略 A：每个 tick 写回满值（写字段）

**做法**：在 mod 的 `Update()` 或 `BasicCharacterController.UpdateStatus()` 的 Postfix 中，
把 `statusData.hunger = statusData.maxHunger`（thirst/energy/fatigue 同理）。

| 维度 | 评价 |
|---|---|
| 稳定性 | 中。能压过自然衰减与 buff tick（因为频率通常更高或执行更晚），但**依赖执行顺序**：若原方法在你之后写，当帧仍会掉一点再被下一帧补回 → **数值抖动/UI 闪烁** |
| 被覆盖风险 | 有。`RefreshStatus()` / `SetMax*()` 会重算 max 并在某些时机重写状态；读档时 `CharacterStatusData` 由存档反序列化覆盖（`ZedZoneSerializer.dll` 参与）|
| 联机 | **低风险**：`MsgPlayerState` 不同步需求字段（见 §7 证据），不会被服务端快照打回 |
| 风险 | **违反需求 5**：启用即把值拉到满，是"擅自重置玩家数值"的典型；若玩家当前 20/100，开启瞬间变 100/100 并可能**写进存档** |
| 需要 Harmony | 否（自己 `Update` 即可），或需要 1 个 Postfix |
| 适用 | 明确的"一键回满"用户动作；**不适合**作为常驻开关的默认实现 |

### 策略 B：Patch 消耗入口使其不扣（Prefix 跳过或改 delta）

**做法**：Harmony Prefix `CostEnergy` / `CostFatigue`，跳过原方法体。

| 维度 | 评价 |
|---|---|
| 对 `energy` / `fatigue` | **可行**：两者都有干净的 `CostEnergy(float)` / `CostFatigue(float)` 虚方法入口 |
| 对 `hunger` / `thirst` | **不可行**：全程序集**不存在** `CostHunger`/`CostThirst`（§2.4）。扣减内嵌在 `OnTimeUpdate`/`UpdateStatus` 方法体里 → 无法"只跳过扣减那一句" |
| 稳定性 | 高（精确、不抖动），但**极易写错**：`CostEnergy`/`CostFatigue` 返回 `bool`，语义是"是否扣款成功"。**Prefix 返回 false 会阻断原方法并让返回值默认 false → 调用方认为"钱不够"→ 直接禁用该动作**（例如无法挥砍/无法破门）。正确写法必须是 **Prefix 返回 false 且 `__result = true`**，同时**不能**跳过副作用 |
| 覆盖风险 | 低（除了绕过入口的其他写入，如存档、buff） |
| 风险 | 中高：会改变动作可用性判定；`HumanCharacterController` 还重写了 `CostEnergy`，**必须同时对基类和重写打补丁**，否则补丁不生效 |
| 需要 Harmony | **是**（至少 2 个类 × 2 个方法，再加 `CostFatigue`） |

### 策略 C：Patch 衰减入口后置回写（Postfix 后"只升不降"钳制）

**做法**：Postfix 挂在 `BasicCharacterController.OnTimeUpdate(float)` 与 `UpdateStatus()`（含 `HumanCharacterController` 的 override 与 `ApplyBuffStatusTick`），
记录"进入前值"，出栈后若变小则写回进入前的值。

| 维度 | 评价 |
|---|---|
| 稳定性 | **高**。一个补丁点覆盖该方法体内**所有**内联扣减，无需知道内部实现 |
| 覆盖完整性 | 需覆盖 **3 个**入口：`OnTimeUpdate`、`UpdateStatus`、`ApplyBuffStatusTick`；遗漏任一都会有"数值仍缓慢下降"的残余（食物中毒/超重正是走 buff tick） |
| 覆盖风险 | 中。食物/喝水的**正向**改变不受影响（因为只在"变小"时才回写），符合"不干扰恢复" |
| 联机 | 低风险（需求不同步） |
| 风险 | 低-中。逻辑简单、可回退；主要风险是"合法下降被误拦"（例如玩家主动吃腐坏食物导致扣值——但那是食物路径，不经此入口） |
| 需要 Harmony | **是**，3 个 Postfix（BCC + Human override + ApplyBuffStatusTick） |

### 推荐结论

**分而治之，而不是三选一：**

1. **`hunger` / `thirst`（无限饱食/饥渴）→ 策略 C（只升不降钳制）**，并且必须同时覆盖
   `OnTimeUpdate` + `UpdateStatus` + `ApplyBuffStatusTick` 三处。
   *理由*：两者**没有** `Cost*` 入口（策略 B 不可用），也不应每帧强写满值（策略 A 违反需求 5 + 抖动）。
   策略 C 是唯一既精确又不会擅自"补满"的方案。

2. **`energy` / `fatigue`（无限体力/精力）→ 策略 C 为主，策略 B 作为可选增强**。
   *理由*：C 已能覆盖动作直扣（因为 `CostEnergy`/`CostFatigue` 通常在 `OnTimeUpdate`/动作流程内被调用，Postfix 时值已被扣）；但若实测发现某些扣减发生在这些入口之外，再补 `CostEnergy`/`CostFatigue` 的 Prefix（**`__result = true` 且不阻断动作**）。
   **不要一上来就用策略 B**，因为它会改动动作可用性判定，风险显著高于 C。

3. **策略 A 仅保留给显式用户动作**（"立即填满"按钮），且**必须**写入 `maxHunger` 而非常量。

> **稳定性排序**：C > B > A（对 hunger/thirst）；B ≥ C > A（对 energy/fatigue，且 B 需正确写 `__result`）。
> **风险排序（低→高）**：C < A < B。

---

## 5. 「关闭辅助后不得擅自重置玩家数值」的设计规则

### 5.1 问题本质

"锁值"功能天然有两种状态机：**读取时覆盖**（getter 返回满值）与**写入时纠正**（发现变小就改回）。
若实现成"启用时写入一个满值、关闭时再写入一个默认值"，那么关闭瞬间玩家的真实数值就被抹掉了 —— 这正是禁止的行为。
**核心原则：mod 只能"阻止变化"，绝不能"创造数值"。**

### 5.2 具体规则（可直接照做）

**规则 1 —— 只升不降（monotonic clamp），永不主动填满**
```csharp
// 伪代码：Postfix 于衰减入口
float before = __state_before;      // Prefix 记录
float after  = statusData.hunger;   // Postfix 读取
if (featureEnabled && after < before)
    statusData.hunger = before;     // 只把"被扣掉的"还回去，绝不写 maxHunger
```
效果：数值只能维持或上升（食物恢复仍生效），**永远不会凭空变成满值**。
玩家 20/100 开启 → 保持 20/100 不掉；关闭 → 仍然是 20/100。**无跳变**。

**规则 2 —— 关闭时不写任何值**
关闭路径里**只**做：置 `featureEnabled = false`、移除/失活 Harmony 补丁、清空缓存。
**禁止**在关闭路径写 `statusData.*`，**禁止**调用 `RefreshStatus()` / `SetMax*()`（它们会重算并可能改状态）。

**规则 3 —— 若一定要"恢复开启前数值"，必须做一致性校验**
```csharp
// 仅当"当前值仍是我最后一次写入的值"时才回滚
if (Math.Abs(statusData.hunger - _lastWritten) < 1e-4f)
    statusData.hunger = _valueBeforeEnable;
// 否则说明期间被食物/存档/其他系统改过 —— 放弃回滚，保持现值
```
`_valueBeforeEnable` 在**启用那一刻**记录一次；`_lastWritten` 每次钳制写入时更新。
这是唯一安全的"可回滚"实现。

**规则 4 —— 只对玩家生效**
`if (controller != GameController.instance.playerCharacter) return;`
否则会同时冻结 NPC/宠物的需求（`GOAPGoal_SatisfyNeeds` 失效 → AI 行为异常）。

**规则 5 —— 不写硬编码常量**
上限来自 `maxHunger`/`maxThirst`（受 Constitution 影响，§1.4）。写 `100f` 在吃了加体质道具后会变成"半饱"或"溢出"。

**规则 6 —— 不改最大值**
不要为了让条"看起来满"去改 `maxHunger`。`SetMaxHunger()` 是游戏自己的计算函数，改字段会在下次 `RefreshStatus()` 被覆盖，
且会连带影响 `GOAPGoal_SatisfyNeeds.GetNeedPriority(current, max)` 的 AI 判定。

**规则 7 —— 存档时机**
若功能是"常驻钳制"，关闭后不写值 → 存档里是玩家真实值，安全。
若用了"填满"按钮，需提示用户该值会被写入存档（因为 `CharacterStatusData` 是 `[System.Serializable]` 普通字段，随 `CharacterData` 一起序列化）。

### 5.3 一句话验收标准
> **开→关 前后，四个数值必须逐位相等（bit-identical）**，除非玩家在此期间自己吃了/喝了/睡了。

---

## 6. 消耗倍率是否有安全接口

### 6.1 结论：**有**，且**不需要 Harmony 补丁**，难度 **低**。

理由：衰减速率不是散落在各消耗点的魔数，而是集中在**两类配置**里。

#### (1) `GameConfigs` —— static 配置类（首选）

【事实-程序集】字段全部为 `public unsafe static float`：

| 字段 | 作用 |
|---|---|
| `hungerCostPerDay` | 饱食度每日衰减 |
| `thirstCostPerDay` | 口渴度每日衰减 |
| `hungerThirstBaseCostFloat` | 饱食/口渴基础系数 |
| `energyRecoverPerSecond` | 精力每秒恢复（倍率可反向放大） |
| `sleepFatigueRecoverPerDay` | 睡眠恢复量 |
| `casEnergyRecoverSpeed` | casEnergy 恢复 |

因为它们**是 static 字段而非只读属性**，可以直接在插件加载后写一个倍率：
```csharp
GameConfigs.hungerCostPerDay *= 0.0f;   // 完全停止自然衰减
GameConfigs.thirstCostPerDay *= 0.0f;
```
**零 Harmony、零逐点补丁** → 稳定性最高、可逆性最好、对 NPC 的影响也统一（可控）。

**风险**：
- 【推断】这些值很可能在**游戏启动/读档/新开局时由配置文件重新填充**，因此必须在"配置就绪之后"写入，并考虑重新应用（例如挂一次 `GameController.OnTimeUpdate` 的首帧，或监听开局事件）。
- 它们是**全局**的：改了会同时影响 NPC（`GOAPNPCManager` 的 hunger/thirst 阈值行为）。若要只影响玩家，应改用 (2)。

#### (2) `GameConfigData` —— 难度档（次选）

【事实-程序集】
```csharp
public unsafe int   hungerThirstRate;      // 字段：难度枚举
public unsafe float hungerThirstRateFloat; // 只读属性
public unsafe int   staminaRegenRate;
public unsafe float staminaRegenRateFloat; // 只读属性
```
【事实-官方文档】本地化 5420–5429 给出 4 档：半速 / 标准 / 快 50% / 双倍。
→ 写 `hungerThirstRate`（int）即可切档，但**只有 4 个离散档位**，不能做任意倍率（如 0.37×）。

#### (3) `CharacterData.skillProperty_HungerRateFloat` / `skillProperty_ThirstRateFloat` —— 角色级倍率

【事实-程序集】两者都是 `CharacterData` 上的 **实例 float 字段**（非属性）：

```
skillProperty_HungerRateFloat   FIELD
skillProperty_ThirstRateFloat   FIELD
skillProperty_StaminaFloat      FIELD
skillProperty_StaminaRecoveryFloat FIELD
```

这是**最贴合"只影响玩家"的倍率接口**：写玩家的这两个字段即可。

**⚠ 风险（必读）**：`CharacterData` 上有 `RefreshStatus()` 以及 `SetMaxHP/SetMaxEnergy/SetMaxFatigue/SetMaxHunger/SetMaxThirst`。
【推断】`RefreshStatus()` 会依据"属性 + perk + 难度档"**重算并覆盖**这些 `skillProperty_*` 字段。
因此直接写这两个字段**可能被下一次 `RefreshStatus()` 抹掉**（升级、换装、perk 变化、开局时都可能触发）。
若采用此方案，**必须**在 `RefreshStatus()` 之后重新应用（Harmony Postfix 一次即可），
或改为拦截读取它的地方（那就退化成策略 B，复杂度上升）。

#### (4) 是否需要逐点补丁？

**不需要。** 我已全量枚举程序集内所有含 `hunger|thirst|fatigue|energy` 的字段（见 §7 表），
衰减速率**没有**出现在任何"每个伤害/每个动作"的散点上，而是集中在 `GameConfigs` / `GameConfigData`。
唯一例外是**动作型消耗**（`meleeAttackFatigueCost`、`runningEnergyCostPerSecond`、`grappleHookEnergyCost` 等），
它们本身就是 `GameConfigs` 的 static 字段，同样可整体缩放。

### 6.2 难度评级

| 方案 | 难度 | Harmony | 精度 | 覆盖范围 | 风险 |
|---|---|---|---|---|---|
| `GameConfigs.*CostPerDay` 置 0/缩放 | **低** | 否 | 任意倍率 | 全局（含 NPC） | 中（可能被重载） |
| `GameConfigData.hungerThirstRate` 切档 | **低** | 否 | 仅 4 档 | 全局 | 低 |
| `CharacterData.skillProperty_*RateFloat` | **低-中** | 是（1 个 Postfix 防覆盖） | 任意倍率 | **仅玩家** | 中（会被 RefreshStatus 覆盖） |
| 逐点 patch 每个消耗点 | **高** | 是（多处） | 任意 | 精确 | 高（易漏、易崩） |

**推荐**：倍率功能优先 `GameConfigs`（最简单）；若要求"只影响玩家"，用
`CharacterData.skillProperty_HungerRateFloat` + 一个 `RefreshStatus()` Postfix 重新应用。

---

## 7. 技术记录表

> 「测试」列**全部为 `未实机验证`** —— 本阶段未运行游戏，无任何日志/截图证据。
> 「访问方式」= 从 BepInEx 插件中拿到/读写该值的途径。

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
|---|---|---|---|---|---|---|
| `CharacterStatusData.hunger` | `float` 实例字段 | 饱食度当前值（真实存储） | `GameController.instance.playerCharacter.characterData.characterStatusData.hunger` | 直接赋值（字段偏移写） | 低；会被存档反序列化覆盖 | 未实机验证 |
| `CharacterStatusData.maxHunger` | `float` 实例字段 | 饱食度上限（非固定 100） | 同上 `.maxHunger` | 不推荐改（会被 `SetMaxHunger` 覆盖） | 中 | 未实机验证 |
| `CharacterStatusData.thirst` | `float` 实例字段 | 口渴度当前值 | 同上 `.thirst` | 直接赋值 | 低 | 未实机验证 |
| `CharacterStatusData.maxThirst` | `float` 实例字段 | 口渴度上限 | 同上 `.maxThirst` | 不推荐改 | 中 | 未实机验证 |
| `CharacterStatusData.energy` | `float` 实例字段 | 精力当前值（H1 下=精力；H2 下=体力） | 同上 `.energy` | 直接赋值 | 低 | 未实机验证 |
| `CharacterStatusData.maxEnergy` | `float` 实例字段 | 精力上限 | 同上 `.maxEnergy` | 不推荐改 | 中 | 未实机验证 |
| `CharacterStatusData.fatigue` | `float` 实例字段 | 疲劳/体力池当前值（H1 下=体力） | 同上 `.fatigue` | 直接赋值 | 低 | 未实机验证 |
| `CharacterStatusData.maxFatigue` | `float` 实例字段 | 疲劳/体力上限 | 同上 `.maxFatigue` | 不推荐改 | 中 | 未实机验证 |
| `CharacterStatusData.sanity` | `float` 实例字段 | 精神值 | 同上 `.sanity` | 直接赋值 | 低 | 未实机验证 |
| `CharacterStatusData.casEnergy` | `float` 实例字段 | casEnergy（用途未定） | 同上 `.casEnergy` | 直接赋值 | 中 | 未实机验证 |
| `BasicCharacterController.characterData` | `CharacterData` 字段 | 通往状态数据的必经节点 | `playerCharacter.characterData` | 只读 | 低 | 未实机验证 |
| `CharacterData.characterStatusData` | `CharacterStatusData` 字段 | ★唯一真实存储容器 | `characterData.characterStatusData` | 只读（改其内字段） | 低 | 未实机验证 |
| `BasicCharacterController.hunger` | **属性** get+set | 饱食度转发访问器 | `playerCharacter.hunger` | 走 setter（多一次转发） | 中；**可能不是真实存储** | 未实机验证 |
| `BasicCharacterController.thirst` | **属性** get+set | 口渴度转发 | `playerCharacter.thirst` | 同上 | 中 | 未实机验证 |
| `BasicCharacterController.energy` | **属性** get+set | 精力转发 | `playerCharacter.energy` | 同上 | 中 | 未实机验证 |
| `BasicCharacterController.fatigue` | **属性** get+set | 疲劳转发 | `playerCharacter.fatigue` | 同上 | 中 | 未实机验证 |
| `BasicCharacterController.hp` | **属性** get+set | HP 转发 | `playerCharacter.hp` | 同上 | 中 | 未实机验证 |
| `BasicCharacterController.maxhp` | `float` 字段 | 控制器侧 HP 上限 | `playerCharacter.maxhp` | 直接赋值 | 中 | 未实机验证 |
| `BasicCharacterController.maxHunger` 等四个 max | **不存在** | —— | —— | —— | —— | 未实机验证 |
| `BasicCharacterController.OnTimeUpdate(float)` | `virtual void` | ★自然衰减主入口（时间增量） | Harmony Postfix 首个候选 | Prefix/Postfix | 中；体量大，须按实例过滤 | 未实机验证 |
| `HumanCharacterController.OnTimeUpdate(float)` | `override void` | 玩家专属衰减实现 | Harmony Postfix（须与基类分别打） | Prefix/Postfix | 中 | 未实机验证 |
| `BasicCharacterController.UpdateStatus()` | `protected virtual void` | ★第二条状态刷新入口 | Harmony Postfix | Prefix/Postfix | 中 | 未实机验证 |
| `HumanCharacterController.UpdateStatus()` | `protected override void` | 玩家覆盖实现 | Harmony Postfix | Prefix/Postfix | 中 | 未实机验证 |
| `BasicCharacterController.ApplyBuffStatusTick(BuffAttr,float,float,float)` | `void` | ★Buff 每秒写入（食物中毒/超重） | Harmony Postfix | Prefix/Postfix（按 buffId 过滤） | 中；影响所有 buff | 未实机验证 |
| `BasicCharacterController.ExecuteBuffs()` | `virtual void` | Buff 总调度 | Harmony Postfix | Prefix/Postfix | 中 | 未实机验证 |
| `BuffAttr.hungerPerSecond` | `float` 字段 | 单 buff 的饱食每秒变化 | `BuffManager.GetBuffAttrByID(id).hungerPerSecond` | 直接赋值 | 中 | 未实机验证 |
| `BuffAttr.thirstPerSecond` | `float` 字段 | 单 buff 口渴每秒变化 | 同上 | 直接赋值 | 中 | 未实机验证 |
| `BuffAttr.energyPerSecond` | `float` 字段 | 单 buff 精力每秒变化 | 同上 | 直接赋值 | 中 | 未实机验证 |
| `BuffAttr.fatiguePerSecond` | **不存在** | fatigue 不受 buff 每秒驱动 | —— | —— | —— | 未实机验证 |
| `BuffManager.GetBuffAttrByID(int)` | `BuffAttr` 方法 | 取 buff 数值 | `BuffManager` 实例 | 只读 | 低 | 未实机验证 |
| `BasicCharacterController.HasEnergy(float)` | `virtual bool` | 判断精力是否足够 | 直接调用 | 不修改 | 低 | 未实机验证 |
| `BasicCharacterController.CostEnergy(float)` | `virtual bool` | ★精力扣减入口（Human 有 override） | 直接调用 / Harmony | Prefix（**需 `__result=true`**） | **高**；返回 false 会禁用动作 | 未实机验证 |
| `HumanCharacterController.CostEnergy(float)` | `override bool` | 玩家精力扣减实现 | Harmony Prefix | 同上 | **高** | 未实机验证 |
| `BasicCharacterController.HasFatigue(float)` | `virtual bool` | 判断 fatigue 是否足够 | 直接调用 | 不修改 | 低 | 未实机验证 |
| `BasicCharacterController.CostFatigue(float)` | `virtual bool` | ★fatigue 扣减入口（无 override） | 直接调用 / Harmony | Prefix（需 `__result=true`） | **高** | 未实机验证 |
| `BasicCharacterController.CharacterTired()` | `virtual void` | 疲劳/体力见底反应 | Harmony Postfix | 拦截 | 中 | 未实机验证 |
| `HumanCharacterController.PayDodgeEnergy()` | `bool` 方法 | 翻滚/闪避消耗精力 | Harmony | 拦截 | 中 | 未实机验证 |
| `BasicCharacterController.MarkEnergyCostTime()` | `void` | 记录精力消耗时刻（影响恢复延迟） | 直接调用 | 不修改 | 低 | 未实机验证 |
| `BasicCharacterController.staminaRegenScale` | **只读属性** `float` | 体力/精力恢复倍率（计算值） | `playerCharacter.staminaRegenScale` | 只读（不可写） | 低 | 未实机验证 |
| `BasicCharacterController.buffStaminaRegenScale` | `float` 字段 | Buff 侧恢复倍率修正 | `playerCharacter.buffStaminaRegenScale` | 直接赋值 | 中 | 未实机验证 |
| `CharacterData.skillProperty_HungerRateFloat` | `float` 字段 | ★玩家级饱食消耗倍率 | `characterData.skillProperty_HungerRateFloat` | 直接赋值（需在 `RefreshStatus` 后重申） | 中；可能被重算覆盖 | 未实机验证 |
| `CharacterData.skillProperty_ThirstRateFloat` | `float` 字段 | ★玩家级口渴消耗倍率 | 同上 | 同上 | 中 | 未实机验证 |
| `CharacterData.RefreshStatus()` | `void` 方法 | 重算状态/上限（覆盖风险源） | Harmony Postfix | 追加重新应用逻辑 | 中 | 未实机验证 |
| `CharacterData.SetMaxHunger()` | `void` 方法 | 计算并写入 `maxHunger` | 可调用 | 不建议调用 | 中 | 未实机验证 |
| `CharacterData.SetMaxThirst()` | `void` 方法 | 计算并写入 `maxThirst` | 可调用 | 不建议调用 | 中 | 未实机验证 |
| `CharacterData.SetMaxEnergy()` | `void` 方法 | 计算并写入 `maxEnergy` | 可调用 | 不建议调用 | 中 | 未实机验证 |
| `CharacterData.SetMaxFatigue()` | `void` 方法 | 计算并写入 `maxFatigue` | 可调用 | 不建议调用 | 中 | 未实机验证 |
| `GameConfigs.hungerCostPerDay` | **static float** | ★饱食每日衰减（倍率首选） | `GameConfigs.hungerCostPerDay` | 直接赋值/缩放 | 中；可能被配置文件重载；影响 NPC | 未实机验证 |
| `GameConfigs.thirstCostPerDay` | **static float** | ★口渴每日衰减 | 同上 | 直接赋值/缩放 | 中 | 未实机验证 |
| `GameConfigs.hungerThirstBaseCostFloat` | **static float** | 饱食/口渴基础系数 | 同上 | 直接赋值 | 中 | 未实机验证 |
| `GameConfigs.energyRecoverPerSecond` | **static float** | 精力每秒恢复 | 同上 | 直接赋值/缩放 | 中 | 未实机验证 |
| `GameConfigs.runningEnergyCostPerSecond` | **static float** | 跑步精力消耗 | 同上 | 直接赋值/缩放 | 中 | 未实机验证 |
| `GameConfigs.sleepFatigueRecoverPerDay` | **static float** | 睡眠恢复 fatigue | 同上 | 直接赋值/缩放 | 中；影响时间推进结算 | 未实机验证 |
| `GameConfigs.meleeAttackFatigueCost` | **static float** | 近战 fatigue 消耗 | 同上 | 直接赋值/缩放 | 中 | 未实机验证 |
| `GameConfigData.hungerThirstRate` | `int` 字段 | ★难度档（半速/标准/快50%/双倍） | `GameConfigData` 实例 | 直接赋值（仅 4 档） | 低 | 未实机验证 |
| `GameConfigData.staminaRegenRate` | `int` 字段 | 体力恢复难度档 | 同上 | 直接赋值（仅 4 档） | 低 | 未实机验证 |
| `GameConfigData.hungerThirstRateFloat` | **只读属性** `float` | 难度档的浮点展开 | 读取 | 不可写 | 低 | 未实机验证 |
| `GameConfigData.staminaRegenRateFloat` | **只读属性** `float` | 同上 | 读取 | 不可写 | 低 | 未实机验证 |
| `BasicCharacterController.UseFood(ItemData,Func<bool>)` | `virtual void` | ★进食总入口（Human override） | Harmony | Prefix/Postfix | 中 | 未实机验证 |
| `HumanCharacterController.UseFood(ItemData,Func<bool>)` | `override void` | 玩家进食实现 | Harmony | Prefix/Postfix | 中 | 未实机验证 |
| `BasicCharacterController.AddFoodBuffs(ItemData)` | `MealBuffResult` | 计算并挂载食物 buff | Harmony Postfix | 读取结果 | 中 | 未实机验证 |
| `BasicCharacterController.MealBuffResult.hot` | `bool` 字段 | 是否热食（影响收益） | 返回对象 | 只读 | 低 | 未实机验证 |
| `BasicCharacterController.MealBuffResult.appliedMealBuffIds` | `List<int>` 字段 | 实际生效的 meal buff id | 返回对象 | 只读 | 低 | 未实机验证 |
| `BasicCharacterController.ExecuteFoodRecoveryBuffsOffClock()` | `bool` 方法 | 脱离时钟立即结算恢复 buff | Harmony | 拦截/只读 | 中 | 未实机验证 |
| `BasicCharacterController.foodRecoveryBuffIds` | **static** `Il2CppStructArray<int>` | 食物恢复类 buffId 白名单 | `BasicCharacterController.foodRecoveryBuffIds` | 只读（不建议改） | 中；全局 | 未实机验证 |
| `ItemAttr_Food.hunger` | `float` 字段 | 食物饱食增益 | `item.attr as ItemAttr_Food` | 直接赋值 | 中；改属性影响所有同类物品 | 未实机验证 |
| `ItemAttr_Food.thirst` | `float` 字段 | 食物口渴增益 | 同上 | 直接赋值 | 中 | 未实机验证 |
| `ItemAttr_Food.energy` | `float` 字段 | 食物精力增益 | 同上 | 直接赋值 | 中 | 未实机验证 |
| `ItemAttr_Food.fatigue` | `float` 字段 | 食物 fatigue 增益 | 同上 | 直接赋值 | 中 | 未实机验证 |
| `ItemAttr_Food.buffConfigs` | 集合字段 | 食物挂载的 buff 列表 | 同上 | 直接改（风险高） | 高 | 未实机验证 |
| `ItemAttr_Food.recoverBuffRealTime` | `float` 字段 | Buff 持续（现实秒） | 同上 | 直接赋值 | 中 | 未实机验证 |
| `ItemAttr_Food.GetCookedGainScale(...)` | `float` 方法 | 烹饪收益倍率 | 调用 | 只读 | 低 | 未实机验证 |
| `ItemAttr_Liquid.hunger/thirst/energy/fatigue` | `float` 字段 ×4 | 饮品的四项增益 | `ItemAttr_Liquid` | 直接赋值 | 中 | 未实机验证 |
| `HumanCharacterController.DrinkLiquid(ItemData,float)` | `void` 方法 | ★饮用入口 | Harmony | Prefix/Postfix | 中 | 未实机验证 |
| `HumanCharacterController.DrinkLiquidFromContainer(ItemData)` | `void` 方法 | 从容器饮用入口 | Harmony | Prefix/Postfix | 中 | 未实机验证 |
| `HumanCharacterController.StartSleeping(Vector3,int,float)` | `void` 方法 | 进入睡眠 | Harmony | 拦截 | 中；多人需投票 | 未实机验证 |
| `HumanCharacterController.get_isSleeping` | 只读属性 `bool` | 是否在睡 | 读取 | 不可写 | 低 | 未实机验证 |
| `HumanCharacterController.get_sleepingWakeupTime` | 只读属性 `float` | 醒来时间 | 读取 | 不可写 | 低 | 未实机验证 |
| `PlayerStatusPanel.hungerBar` | UI 字段 | HUD 饱食条 | 反射/读取 | 不建议改 | 中 | 未实机验证 |
| `PlayerStatusPanel.thirstBar` | UI 字段 | HUD 口渴条 | 同上 | 不建议改 | 中 | 未实机验证 |
| `PlayerStatusPanel.energyBar` | UI 字段 | HUD 精力条 | 同上 | 不建议改 | 中 | 未实机验证 |
| `PlayerStatusPanel.fatigueBar` | UI 字段 | HUD 疲劳/体力条 | 同上 | 不建议改 | 中 | 未实机验证 |
| `PlayerStatusPanel.UpdateStatus()` | `void` 方法 | HUD 状态刷新（读取路径） | 观察 | 不修改 | 低 | 未实机验证 |
| `PlayerStatusPanel.OnHover_HungerBar/ThirstBar/EnergyBar(bool)` | `void` 方法 | 悬停提示（用于确认标签） | 观察 | 不修改 | 低 | 未实机验证 |
| `GOAPGoal_SatisfyNeeds.GetNeedPriority(float,float)` | `static float` 方法 | NPC 需求优先级（共用模型） | 只读 | 不可写 | 低 | 未实机验证 |
| `GOAPNPCManager.hungerThreshold/thirstThreshold` | `float` 字段 | NPC 需求阈值 | 只读 | 改则影响 AI | 高 | 未实机验证 |
| `MsgPlayerState.hp` / `qHp` | 网络消息字段 | 联机同步的血量 | 只读观察 | 不改 | 低 | 未实机验证 |
| `MsgPlayerState` 的 hunger/thirst/energy/fatigue | **不存在** | 需求**不**参与逐帧联机同步 | —— | —— | —— | 未实机验证 |
| `MsgPlayerBuff.buffId/buffEndTime` | 网络消息字段 | 联机同步 buff（客户端各自结算每秒效果） | 只读观察 | 不改 | 中 | 未实机验证 |

---

## 8. 无法确认的清单

### 8.1 必须实机才能定论（最高优先级）

1. **四个状态条 ↔ 代码字段的准确对应**（`energy` / `fatigue` 谁是真"体力"）。
   - 现有证据只能证明：`energy` 与 `fatigue` 是**两个独立显示的状态**，且游戏文本本身混用 "stamina"/"energy"。
   - 定论方法：见 §1.5 的五步受控实验。**这是 Phase-2 的第一件事。**
2. **中文 UI 标签（饱食/饥渴/体力/疲劳）对应的英文源词**。
   - 【事实-官方文档】本地化目录**没有 zh 表**（只有 de/es/fr/ja/ko/pl/pt/ru），中文是引擎内嵌源文本。
   - 已证实的只是英文→日文：`STAMINA→持久力`、`ENERGY→活力`、`CONSTITUTION→体力`、`health→体力`。
   - 结论：**「体力」一词在游戏里至少对应 3 个不同英文词**，无法从代码反推中文 UI。
3. **`maxHunger` / `maxThirst` 的实际数值**（是否 100、是否随 Constitution 线性变化）。
   - 已证：是 `float` 数据字段 + 由 `SetMax*()` 计算；未证：具体数值与公式。
4. **`OnTimeUpdate` 内部的真实扣减公式**（每 tick 扣多少、是否乘 `hungerCostPerDay`）。
   - 已证：方法存在、被玩家类 override、接受 `float` 时间增量；未证：内部实现（trampoline 桩）。
5. **`GameConfigs` 静态字段的生命周期**：是否在开局/读档时被配置文件重新填充、写一次能否持久。
6. **`RefreshStatus()` 是否会覆盖 `skillProperty_HungerRateFloat`**（决定倍率方案是否需要 Postfix 重申）。
7. **`CharacterTired()` 的触发条件**（`fatigue` 降到 0？还是升到 `maxFatigue`？）——直接决定 H1/H2。
8. **`casEnergy` 到底是什么**（有独立的 `casEnergyRecoverSpeed`、`skillProperty_casTotal/casRecover/casDamage`，语义未明）。
9. **`PlayerStatusPanel.apBar` 的 AP 含义**（可能与 `BuffAttr.armorPointsGain` 相关，未证实）。
10. **存档/读档是否会重写四个字段**（`CharacterStatusData` 是 `[System.Serializable]`，随 `CharacterData` 序列化；`ZedZoneSerializer.dll` 参与）。
11. **联机场景下需求是否完全由各客户端自行模拟**（`MsgPlayerState` 不含需求字段，但可能存在其他同步通道或主机权威逻辑）。
12. **睡眠结算是否会覆盖手动写入的 fatigue**（`sleepFatigueRecoverPerDay` 按日结算，未知是否含重置）。
13. **`ExecuteFoodRecoveryBuffsOffClock()` 的返回 `bool` 语义**（是否"成功结算"）。
14. **`foodRecoveryBuffIds` 的具体 id 列表内容**（静态数组，运行时可读，静态不可知）。
15. **烹饪倍率 `GetCookedGainScale` 的具体系数**。

### 8.2 已完全排除（避免后续重复调查）

- ✅ **不存在 `CostHunger` / `CostThirst` / `AddHunger` / `DecayHunger` 一类方法**（全程序集方法名枚举）。
- ✅ **`BasicCharacterController` 上不存在 `maxHunger/maxThirst/maxEnergy/maxFatigue`**。
- ✅ **不存在名为 `stamina` 的"当前值"字段/属性**（玩家侧）；`stamina`/`maxStamina` 仅存在于 `GOAPNPCData`（NPC）。
- ✅ **`BuffAttr` 没有 `fatiguePerSecond`** → fatigue 不经 buff 每秒驱动。
- ✅ **`PlayerStatusPanel` 没有 `staminaBar`**（`staminaBarFill` 属于 `TeamNPCUI`）。
- ✅ **`MsgPlayerState` 不含需求字段**。

---

## 附录 A：证据获取命令（可复现）

```powershell
$env:DOTNET_ROOT="<dotnet>"; $env:DOTNET_ROLL_FORWARD="LatestMajor"
$ILSPY="ilspycmd.exe"
$ASM="<游戏目录>\BepInEx\interop\Assembly-CSharp.dll"

# 单类型
& $ILSPY $ASM -t BasicCharacterController | Out-File D:\tmp\BCC.cs

# 全程序集（单文件，约 81 MB / 137 万行）
& $ILSPY $ASM -o D:\tmp\full

# 字段归属：一行同时给出"类型 + 字段名"
Select-String -Path D:\tmp\full\Assembly-CSharp.decompiled.cs `
  -Pattern 'GetIl2CppField\(Il2CppClassPointerStore<([A-Za-z0-9_]+)>\.NativeClassPtr, "([^"]+)"\)'
```

判据（区分字段 / 属性）：
- 字段 → 使用 `NativeFieldInfoPtr_*` + `IL2CPP.il2cpp_field_get_offset`
- 属性 → 使用 `NativeMethodInfoPtr_get_*` / `set_*` + `IL2CPP.il2cpp_runtime_invoke`

## 附录 B：引用文件清单

| 文件 | 用途 |
|---|---|
| `BepInEx\interop\Assembly-CSharp.dll` | 【事实-程序集】全部来源（只读） |
| `ZEDZONE_Data\StreamingAssets\Localization\Localization_en_to_ja.csv` | 【事实-官方文档】英文源字符串（8 种语言、无 zh） |
| `research\official-mod-docs\docs\cn\07_食物MOD速查.md` | 【事实-官方文档】`ItemAttr_Food` 字段与官方说明 |
| `research\official-mod-docs\docs\cn\11_Buff列表参考.md` | 【事实-官方文档】31 个 buff 的 per-second 语义 |
| `research\type-list.txt` | 类型候选索引 |

---

**文档状态**：Phase-1 静态调查完成。所有行为结论均标注【推断】，**无一条**声称已实机验证。
下一步（Phase-2）建议顺序：§1.5 的 energy/fatigue 裁定实验 → `GameConfigs` 写入持久性验证 → `OnTimeUpdate`/`UpdateStatus`/`ApplyBuffStatusTick` 三入口的 Postfix 钳制原型。
