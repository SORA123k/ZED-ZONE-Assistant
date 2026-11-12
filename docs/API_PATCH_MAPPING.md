# API_PATCH_MAPPING.md — 功能到 API / 补丁的映射

> **文档**：API 与补丁映射（Phase 1 交付物 6/6）
> **状态**：已完成（静态分析）；**全部"实机验证"均未执行**
> **取证基准**：ZED ZONE build 26576287 / 游戏内 1.0.14 / Unity 2023.1.18f1 / IL2CPP x64
> **生成日期**：2026-11-12

---

## 0. 证据约定（先读）

| 标记 | 含义 |
|---|---|
| **【事实-程序集】** | 从 `BepInEx\interop\Assembly-CSharp.dll` 直接读到类型/签名 |
| **【事实-官方文档】** | 来自官方 MOD 文档离线副本（`research/official-mod-docs`） |
| **【事实-本地化】** | 来自 `ZEDZONE_Data\StreamingAssets\Localization\*.csv`（英文为源语言） |
| **【推断】** | 由签名推导，无方法体可证 |
| **【未验证】** | 必须实机确认 |

> **重要**：interop 程序集是 Il2CppInterop **桩**（方法体为 `il2cpp_runtime_invoke` 跳板）。
> 因此本文所有"存在性/签名"可信，**所有"行为"只能是推断**。
> `[CallerCount]` 是引用区间长度，**不是调用次数**，不得用于反推调用关系。

---

## 1. 四条真实写值链路（本文的骨架）

所有功能都挂在这四条链上。**这是 Phase 1 最重要的产出。**

### 链路 A — 玩家对象

```csharp
GameController.instance.playerCharacter        // 【事实-程序集】类型：HumanCharacterController
// 或
CharacterInput.instance.characterController    // 【事实-程序集】类型：HumanCharacterController
```

- `HumanCharacterController : BasicCharacterController`【事实-程序集】
- 两个入口**都有 setter**（运行期会被重新赋值）→ **禁止缓存引用，每次现取并判空**【推断，但极其重要】
- 兜底：`GameController.characterList` + `isPlayer`

### 链路 B — 状态数据

```
playerCharacter.characterData                       // CharacterData
  .characterStatusData                              // CharacterStatusData（唯一真实存储）
    hp / maxhp / hunger / maxHunger / thirst / maxThirst
    energy / maxEnergy / fatigue / maxFatigue / sanity / maxSanity
    bloodVolume / bodyTemperature / radiationDose / casEnergy
```

- 【事实-程序集】`CharacterStatusData` 的**唯一**引用者是 `CharacterData.characterStatusData`
- 控制器上的 `hp/hunger/thirst/energy/fatigue` 是**属性**，且控制器上**不存在** `maxHunger/maxThirst/...`【事实-程序集】
  → **这是最容易踩的"看起来改对了却没效果"的坑**
- `BasicCharacterController.hp` 是 `virtual` 属性，`HumanCharacterController.hp` **覆写**它
  → 玩家改血**必须走属性**，不要直接写 `characterStatusData.hp` 裸字段（会绕过覆写里的 UI/联机/致死逻辑）【推断，高置信】

### 链路 C — Buff

```
playerCharacter.characterData.buffDataList          // Il2CppSystem.Collections.Generic.List<BuffData>
  BuffData { int buffId; float buffEndtTime; }      // 注意拼写多一个 t
```

- 【事实-程序集】`BuffManager` 只是**静态定义表**（`instance` / `buffAttrs` / `buffDic` / `Init()` / `GetBuffAttrByID(int)`）——**没有增删方法**
- 【事实-程序集】`BasicCharacterController.RemoveBuff(int)`、`ClearDebuffs(bool dotOnly)`（virtual）、`RemoveAllBuffs()`
- `BuffAttr: ScriptableObject` 数据字段：`hpPerSecond, speedGain, bodyTemperaturePerSecond, hungerPerSecond/thirstPerSecond/energyPerSecond, meleeAttackMultiplier, immuneToAcid, immuneToSpore, causeDeathFlag, isDebuff, ...`【事实-官方文档】

### 链路 D — 全局/难度配置（**零补丁的关键**）

```csharp
GameConfigs.playerInvincible            // static bool 【事实-程序集】
GameConfigs.playerGodMode               // static bool 【事实-程序集】
GameConfigs.downedDamageImmuneSeconds   // static float 【事实-程序集】
GameConfigs.hungerCostPerDay            // static float 【事实-程序集】
GameConfigs.thirstCostPerDay            // static float 【事实-程序集】
CharacterData.skillProperty_HungerRateFloat   // 玩家专属消耗倍率 【事实-程序集】
CharacterData.skillProperty_ThirstRateFloat
GameConfigData.hungerThirstRate         // int 枚举，仅 4 档 【事实-程序集】
```

---

## 2. 功能 → API 映射总表

> 「手段」列：**写** = 直接字段/属性写（零补丁）；**补** = Harmony；**调** = 调用游戏公开方法。

### A. 玩家生命控制

| 功能 | 目标 API | 手段 | 风险 | 验证 |
|---|---|---|---|---|
| 无限生命（免死） | `HumanCharacterController.OnHit` Prefix，`ref m_damage=0` + `ref hitCauseDeathFlag=false` | 补 | 中（受击动画/UI 受击面板仍走） | 未实机验证 |
| 无敌（含 DoT） | 同上 **+** `BasicCharacterController.ApplyBuffStatusTick` Prefix | 补×2 | 中高 | 未实机验证 |
| 游戏自带无敌开关 | `GameConfigs.playerInvincible` / `playerGodMode` | 写 | 低（若能生效） | **未验证是否在正式版接线，必须先测** |
| 一键恢复满血 | `playerCharacter.hp = playerCharacter.maxHP`（走属性） | 写 | 低 | 未实机验证 |
| 自动回血 | 定时检查 `hp < 阈值` → 写 `hp`（**只升不降**） | 写 | 低 | 未实机验证 |
| 自定义阈值/恢复量 | 同上，参数来自配置 | — | 低 | 未实机验证 |
| 受伤保护（减伤） | `OnHit` Prefix，`ref m_damage *= (1f - 减伤率)` | 补 | 低 | 未实机验证 |

**关键结论（health-analyst 已复核，Lead 已独立确认）**：
- `BasicCharacterController.OnHit` 是 `Virtual_New`（**声明新槽**），`HumanCharacterController.OnHit` 是 `override` → **打基类对玩家无效**，必须打 `HumanCharacterController.OnHit`。【事实-程序集，已双人复核】
- DoT（燃烧 -20/s、酸 -5/s、孢子 -5/s、饥渴 -5/s、溺水 -20/s —【事实-官方文档】）**绕过 OnHit**，落在 `ApplyBuffStatusTick(BuffAttr, float, float, float)`。**只打 OnHit 的无敌是不完整的。**
- 全程序集**不存在** 角色用的 `TakeDamage/Hurt/ReceiveDamage/ApplyDamage`。【事实-程序集】
- `OnHitCallBack.Invoke(float,Vector2,Vector2)` 是**受击后通知**，只能 `+=` 观察，不是入口。

### B/C. 饥饿与饥渴

| 功能 | 目标 API | 手段 | 风险 | 验证 |
|---|---|---|---|---|
| 无限饱食/水分 | `skillProperty_HungerRateFloat = 0` / `ThirstRateFloat = 0`，或 `GameConfigs.hungerCostPerDay = 0` | 写 | 低 | 未实机验证 |
| 锁值（不被清空） | `OnTimeUpdate` / `UpdateStatus` / `ApplyBuffStatusTick` 三处 Postfix 做 **只升不降** 钳制 | 补 | 中 | 未实机验证 |
| 一键恢复 | `characterStatusData.hunger = maxHunger` | 写 | 低 | 未实机验证 |
| 自动恢复 | 阈值触发写值 | 写 | 低 | 未实机验证 |
| 自定义数值 | 写当前值 | 写 | 低 | 未实机验证 |
| 消耗倍率 | 优先 `skillProperty_*RateFloat`（玩家专属）；`GameConfigs.*CostPerDay` 是全局 | 写 | 低 | 未实机验证 |

**⚠ 关键约束**：全程序集**不存在 `CostHunger/CostThirst`**。所以"拦截消耗函数"这条路**不存在**，只能走「倍率」或「事后钳制」。【事实-程序集】

**⚠ 三个衰减入口**（必须全覆盖，否则会出现"饿得慢但还是在饿"）：
1. `BasicCharacterController.OnTimeUpdate(float)`（virtual）→ `HumanCharacterController` override
2. `UpdateStatus()`
3. `ApplyBuffStatusTick(...)` — **食物中毒(15)/超重(19,20) 会绕过自然衰减继续扣值**（对应 `hungerPerSecond/thirstPerSecond`）

### D. 体力 / 疲劳 —— **⚠ 语义未定，需先裁定**

**这一项是全文最不确定的地方，必须实机裁定后才能实现。**

【事实-本地化】本地化表（英文为源语言）：

| id | 英文 | 日文 |
|---|---|---|
| 53 | `STAMINA` | 持久力 |
| 54 | `ENERGY` | 活力 |
| 5868 | `health` | 体力 |
| — | `CONSTITUTION` | 体力 |

【事实-本地化】`2562`: "Stamina affects the character's maximum stamina. **Running and attacking consume stamina.**"
【事实-本地化】`2563`: "**Energy** affects the character's **building and crafting** abilities."

→ 中文「体力」很可能对应 **CONSTITUTION / health（体质、生命）**，而**不是** STAMINA/ENERGY。
而 `PlayerStatusPanel` 只有 `energyBar` 与 `fatigueBar`，**没有 `staminaBar`**。【事实-程序集】

**因此存在两个互斥假设**，Phase 2 必须裁定：

| 假设 | 内容 | 裁定方法 |
|---|---|---|
| H1 | 玩家 UI 的"体力"= `fatigue`（疲劳条反向） | 冲刺/挥砍时观察 `fatigue` 与 `energy` 哪个变化 |
| H2 | "体力"= `energy`；`fatigue` 是独立疲劳 | 建造/制作时观察 `energy` 是否下降 |

**裁定实验（5 步）**：① 静止读四值 → ② 连续冲刺读 → ③ 连续挥砍读 → ④ 建造一件读 → ⑤ 喝「精力恢复」(buff 12) 读。哪一步动哪个字段，就是映射关系。

> 在裁定前**不要实现**体力/疲劳功能。宁可少一个功能，也不要给用户一个改了没用的开关。

### E. 负面状态管理

| 功能 | 目标 API | 手段 | 风险 | 验证 |
|---|---|---|---|---|
| 一键清除负面状态 | `BasicCharacterController.ClearDebuffs(bool dotOnly)` | 调 | 中（`dotOnly` 语义未确认） | 未实机验证 |
| 单条移除 | `BasicCharacterController.RemoveBuff(int buffId)` | 调 | 低 | 未实机验证 |
| 感染控制 | **不存在独立"感染"机制**（全量类型表零命中）【事实-程序集】；候选映射：`Buff_Spore`(16) / `radiationDose` / `headWound`(21) | — | — | **未确认** |
| 中毒控制 | `buffId=2`(酸蚀) / `buffId=15`(食物中毒)；免疫用 `AddBuff(30,...)` 而非写 `buffImmuneToAcid` | 调 | 低 | 未实机验证 |
| 疾病控制 | `buffId=7`(发烧) / `14`(低烧) | 调 | 低 | 未实机验证 |
| 异常温度 | `buffId=4`(寒冷)/`6`(失温)/`8`(潮湿)；体温字段 `bodyTemperature` | 调 + 写 | 中 | 未实机验证 |

**清除白名单（建议第一版只清这 11 条）**【事实-官方文档 + 推断】：
`1 燃烧, 2 酸蚀, 3 电击, 4 寒冷, 6 失温, 7 发烧, 8 潮湿, 14 低烧, 15 食物中毒, 16 孢子感染, 25 溺水`

**绝不能清**：
- 11 条正面 buff：`5 温暖, 9 饱腹恢复, 10 解渴, 11 生命恢复, 12 精力恢复, 13 保暖, 26 治疗烟雾, 28 耐力药剂, 29 力量, 30 解毒剂, 31 速度`
- **表外 buff ID**：`bloodlustBuffId1/2`、`killStaminaRegenBuffId`、`RadiationDose.BuffMild/Moderate/Severe/Iodine/AntiRad`、`foodRecoveryBuffIds`
  → 说明 buff ID 空间 **> 31**，清除逻辑**必须用白名单，不能用"非文档 ID 一律清"**
- 伤口类 `21/22/23/24`、失血 `27`：**镜像型**（由身体部位伤害派生），清 buff 可能只掩盖症状不治伤 → 默认不清

**禁用**：`RemoveAllBuffs()`（会连 11 条正面一起清）；清零 `buffEndtTime`（条目仍在列表里，UI/AI 仍可见）。

### F. 综合操作

| 功能 | 实现 |
|---|---|
| 一键恢复所有生存属性 | 依次写 `hp/hunger/thirst/(energy/fatigue 待裁定)` 到各自 max |
| 一键开启所有已验证保护 | 逐个模块 `Enable()`，**跳过未验证项** |
| 一键关闭所有辅助 | 逐模块 `Disable()`，**只停止写值，不重置玩家数值** |
| 一键恢复原版 | 停写 + 恢复被改过的 static 字段（`GameConfigs.*`）到保存的初值 |
| 配置保存/加载 | BepInEx `Config` 已有；额外需要「运行期改动快照」用于还原 |

---

## 3. "关闭功能不得重置玩家数值"的设计规则

用户明确要求。落地方案：

```
规则 1（停止写值即可）：Disable() 只置 enabled=false，绝不写任何字段回默认值。
规则 2（不回溯历史）：不保存"开启前的值"再恢复 —— 因为期间玩家可能正常消耗，
                      恢复旧值反而会凭空回血/回饱食。
规则 3（只升不降钳制）：钳制类功能用 max(current, floor)，永不向下写。
规则 4（静态字段需还原）：只有 GameConfigs.* 这类被我们改写的全局静态，Disable 时恢复初值；
                      玩家自身属性一律不回写。
```

**反例（必须避免）**：`Disable()` 里写 `hp = maxHP` 或 `hunger = 100` —— 这是"关掉外挂被补满"，属于擅自改数值。

---

## 4. 免补丁（零 Harmony）清单 —— 优先做这些

按稳定性排序，**能用写值就不要用补丁**：

| 优先级 | 功能 | 手段 |
|---|---|---|
| 1 | 无限生命（若能生效） | `GameConfigs.playerInvincible = true`（**先测**） |
| 1 | 无限饱食/饥渴 | `skillProperty_HungerRateFloat = 0` |
| 2 | 一键恢复生命/饱食/饥渴 | 写 `hp/hunger/thirst` 属性 |
| 2 | 自动恢复（阈值触发） | 定时读+写 |
| 3 | 清除负面状态 | `ClearDebuffs()` / `RemoveBuff()` 调用 |
| 3 | 玩家护甲/减伤 | — |

**只有以下两项必须补丁**：`OnHit`（伤害拦截）、`ApplyBuffStatusTick`（DoT 拦截）、三处衰减入口（锁值钳制）。

---

## 5. 快捷键与输入（含冲突检测）

【事实-程序集】游戏自带输入抽象：

```csharp
GameInputManager.keyMapping_keyboard : Dictionary<string, KeyCode>
GameInputManager.GetKeyMap(string buttonName) -> KeyCode
GameInputManager.GetButtonNamesByKeycode(KeyCode keycode) -> List<string>   // ★ 冲突检测
GameInputManager.GetKey(KeyCode) / GetKeyDown(KeyCode) / GetKeyUp(KeyCode)  // ★ 尊重焦点门控
GameInputManager.IsDefaultBinding(string action, KeyCode key, bool isController)
GameInputManager.SetKeyMap(string buttonName, KeyCode newKey, bool isController)
GameInputManager.focusGateEnabled / windowFocused / suppressMouseUntilRelease / inputAllowed
```

**设计决定**：
1. **冲突检测**：启动时对用户配置的 GUI 键调用 `GetButtonNamesByKeycode(key)`，返回非空即冲突 → 记日志并在 UI 上警告，自动回退到备用键。**不要静默占用游戏按键。**
2. **输入读取**：用 `GameInputManager.GetKeyDown(KeyCode)` 而**不是** `UnityEngine.Input`，因为它已经处理了窗口失焦/鼠标抑制，能避免"在游戏 UI 里打字时误触发"。
3. **Ins 键状态**：【未确认】—— 无法在静态阶段枚举 `keyMapping_keyboard` 的运行时内容。**Phase 2 启动时打印 `GetButtonNamesByKeycode(KeyCode.Insert)` 即可定论。**

---

## 6. 联机门禁（第一版仅单机）

### 6.1 检测信号（**必须用这个，不能用别的**）

```csharp
NetworkManager.isOnline                                    // 主判据
|| NetworkManager.instance.mode != NetMode.Offline          // 兜底；NetMode { Offline, Host, Client }
```

### 6.2 ⚠ 关键更正：**不能用 `isSimulationAuthority` 判联机**

- `ZedZone.Net.NetSim.isSimulationAuthority` 是 `public static bool { get; }`，**全局静态、只读**。
- 语义是"我是否为模拟方"：**主机 = true，单机 = true**。
- 用 `!isSimulationAuthority` 判"是否单机" → **主机会被误判为单机而放行**。
- 正确做法：用 §6.1 的 `NetworkManager` 信号。

### 6.3 为什么联机必须硬禁用（机制原因）

【事实-程序集 + 推断】
- 世界模拟（怪物/地形/容器）**主机权威**；`NetMonsterSync` 的 `Host*` / `Client*Puppet` 严格对称。
- 但**玩家自身 hp/buff 是客户端自报**：`SampleOwnState` → `MsgPlayerState.hp` → 其他端 `ApplyPuppetHp` 只写影子。**没有主机回写本地 hp 的路径。**
- `MsgPlayerState` 只同步 `hp`，**不同步饥饿/口渴/精力/疲劳**。
- `NetStateAudit` 审核域仅 `chunks/monsterIds/itemIds/vehicleIds/modSetHash`，**不含玩家数值**；`NetKickReason` 无 Cheat/Desync 项。

→ **真实风险不是"被覆盖"，而是"假值被广播给队友 + 写进存档 + 与派生缓存矛盾"**。这比被覆盖更糟，所以必须硬禁用。

### 6.4 门禁策略

```
1. 每次操作前实时求值（不缓存）：IsMultiplayer = NetworkManager.isOnline || mode != Offline
2. 联机时：状态修改模块全部硬禁用（不只是隐藏 UI），UI 上显示"联机模式：辅助功能已禁用"
3. 禁用时不写任何字段、不恢复数值
4. 不提供任何"强制启用"开关（第一版）
```

---

## 7. 两个必须规避的坑（继承自上一个项目）

### 坑 1：`Update()` 一帧内多次派发

上一项目实测：HUD 报 176 FPS，游戏自带读数 87 FPS，**恰好 2 倍**。原因是一帧内 `Update()` 被多次调用，`1/Time.unscaledDeltaTime` 于是翻倍。

**对本项目更危险**：任何"每 tick 扣 0.5"的**增量**写都会速度翻倍。

**规则**：
```csharp
if (Time.frameCount == _lastFrame) return;   // 去重
_lastFrame = Time.frameCount;
```
并且**写值应幂等**（"设为 X"而非"加 X"）。

### 坑 2：IL2CPP 已销毁对象的封送异常

只要 Harmony Prefix 声明了 `BasicCharacterController __instance` 这类参数，Harmony 每次进入该原生方法都要把 `IntPtr` **封送**成托管代理。对已销毁/未初始化实例，这会在**进入补丁方法体之前**抛 `Handle is not initialized` —— `try/catch` 拦不到。

**所以"装了补丁 + 首行 early-return" ≠ "没装补丁"**：省下的只有方法体执行，封送与抛异常的概率一分没省。

**规则**：**功能没开就根本不装补丁**（条件安装）。默认配置的日志应能打印 `harmony patches installed: 0`。

---

## 8. 风险分级与默认值策略

| 等级 | 功能 | 默认 |
|---|---|---|
| **低** | 一键恢复生命/饱食/饥渴、清除负面状态、配置读写 | 关闭（用户手动触发也安全） |
| **中** | 无限饱食/饥渴（倍率写）、自动回血 | 关闭 |
| **中高** | 无敌（含 DoT 拦截） | 关闭 |
| **高** | `GameConfigs.*` 全局静态改写（影响所有存档/难度） | **默认关闭，且退出时还原** |
| **禁止** | `RemoveAllBuffs()`、直接写 `characterStatusData` 裸字段改玩家血、联机下任何状态修改 | — |

---

## 9. Phase 2 实机验证清单（按优先级）

| # | 验证项 | 方法 | 不通过则 |
|---|---|---|---|
| T1 | 联机检测是否正确 | 单机/开房/加入各打印 `isOnline` 与 `mode` | 换信号 |
| T2 | `GameConfigs.playerInvincible` 是否生效 | 置 true 后挨打看 hp | 退回 OnHit 方案 |
| T3 | `OnHit` 打 Human 能否拦下伤害 | Prefix 打印并零伤 | 改 `CharacterHitContext.Apply` |
| T4 | DoT 是否真的绕过 OnHit | 点燃自己 + OnHit 零伤，看 hp | 加 `ApplyBuffStatusTick` |
| T5 | 三处衰减入口是否覆盖全部扣减 | 分别关掉看哪个还在扣 | 补入口 |
| T6 | `skillProperty_*RateFloat = 0` 是否被 `RefreshStatus()` 覆盖 | 写入后观察是否回弹 | 改 `GameConfigs.*` |
| T7 | 玩家"体力"到底是 `energy` 还是 `fatigue` | §2-D 五步实验 | 按结果实现 |
| T8 | `hp` 属性 vs 裸字段写差异 | 两种都试，看 UI/存档 | 只用属性 |
| T9 | 改值是否跨读档存续 | 改→存档→读档 | 决定是否每次进场景重写 |
| T10 | Ins 键是否冲突 | 打印 `GetButtonNamesByKeycode(Insert)` | 换键 |
| T11 | `ClearDebuffs(dotOnly)` 语义 | 两种参数各试一次 | 改用 `RemoveBuff` 逐条 |
| T12 | 关闭功能后数值是否被重置 | 开关切换观察 | 修 Disable 逻辑 |

---

## 10. 未确认项汇总（不许当成已知）

1. `GameConfigs.playerInvincible/playerGodMode` 在正式版是否接线。
2. `ApplyBuffStatusTick` 的访问级别：interop 显示 `public unsafe void`，健康研究报告称 private —— **两说需实机/反编译确认**；但无论哪种都可 patch。
3. 玩家"体力"↔ `energy`/`fatigue` 的映射。
4. 中文 UI 标签不在 CSV（中文是源语言，内嵌在 Unity 资源里）→ `饱食/饥渴/体力/疲劳` 与字段的对应仍属【推断】。
5. `RemoveBuff(int)` 是否清除**同 ID 的全部**条目。
6. `AddBuff` 第二参数是绝对时间还是持续时间。
7. `ClearDebuffs(dotOnly=true)` 的筛选语义。
8. 改值是否跨存档持久（链路 B 是否被序列化）。
9. `NetworkManager` 的确切命名空间/成员（mp-analyst 报告，Lead 未独立复核）。
10. 全程序集 4341 类型未做逐类扫描，可能存在未发现的替代入口。

---

## 11. 文档索引

| 文档 | 内容 |
|---|---|
| `docs/GAME_PLAYER_DATA.md` | 玩家类型、访问入口、存档链路 |
| `docs/HEALTH_SYSTEM_RESEARCH.md` | 生命值、伤害入口、死亡链、无敌方案排序 |
| `docs/SURVIVAL_SYSTEM_RESEARCH.md` | 饥饿/饥渴/精力/疲劳、衰减入口、倍率 |
| `docs/STATUS_EFFECT_RESEARCH.md` | Buff 系统、31 条官方 Buff、清除白名单 |
| `docs/MULTIPLAYER_COMPATIBILITY.md` | 联机架构、221 个 Msg 类型、门禁设计 |
| `docs/API_PATCH_MAPPING.md` | 本文 |
| `research/LEGACY_PROJECT_PATTERNS.md` | 上一项目可复用模式与两个坑 |
