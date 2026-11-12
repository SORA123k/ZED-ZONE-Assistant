# 负面状态（Buff / Debuff）静态调研

> Phase-1 静态代码调研。**未运行游戏，未修改游戏安装，未实机验证任何行为。**
> 目标程序集：`<游戏目录>\BepInEx\interop\Assembly-CSharp.dll`（Unity 2023.1.18f1 / IL2CPP x64，interop **桩代码**）。
> 官方离线文档：`<仓库目录>\ZED-ZONE-FPS-Optimization-Mod\research\official-mod-docs\docs\cn\11_Buff列表参考.md`（数据来源 `Assets/Resources/Properties/Buffs/`）。

## 0. 证据等级约定

| 标签 | 含义 |
|---|---|
| 【事实-程序集】 | 从 interop 元数据直接读出：类型名、字段名、字段类型、方法签名、访问修饰符（含在 native token 名里的 `_Public_` / `_Private_` / `_Protected_`）。**只有"存在性/形状"是事实，行为不是。** |
| 【事实-官方文档】 | 官方离线文档明文写着（buff ID / 引擎名 / Debuff 标记 / 数值效果）。 |
| 【推断】 | 由签名、命名、调用点形状推出的行为，未经实机验证。 |
| 【未验证】 / 【未确认】 | 无法从静态证据确定，需实机或进一步取证。 |

**重要前提**：interop 里所有方法体都是 `il2cpp_runtime_invoke` 跳板，**读不到任何实现**。因此"某方法做了什么"永远最多是【推断】。
**注意**：`[CallerCount(N)]` 是引用区间长度（`RefRangeStart`→`RefRangeEnd`），**不是调用次数**，本文件不据此做任何论断。

---

## 1. 摘要（结论先行）

1. **活跃负面状态不在 `BuffManager` 里。** `BuffManager` 是**静态定义表**（31 条 `BuffAttr` ScriptableObject 资产）——已验证：它只有 `instance` / `buffAttrs` / `buffDic` / `Init()` / `GetBuffAttrByID(int)`，**没有任何 add/remove 方法**。【事实-程序集】
2. **活跃状态容器是 `CharacterData.buffDataList`，类型 `Il2CppSystem.Collections.Generic.List<BuffData>`。** `BuffData` 只有 `buffId: int` + `buffEndtTime: float`（注意拼写是 `buffEndtTime`，多一个 `t`）。【事实-程序集】
3. **可达路径**：`GameController.instance.playerCharacter`（`HumanCharacterController : BasicCharacterController`）→ `.characterData`（`CharacterData`）→ `.buffDataList`。【事实-程序集】
4. **最安全的"清除负面状态"入口是 `BasicCharacterController.ClearDebuffs(bool dotOnly)`**（native `_Public_Virtual_New_`）。单条移除用 `RemoveBuff(int)`（native `_Public_`）。**不要用 `RemoveAllBuffs()`**，它按字面会连正面 buff 一起清。【事实-程序集 + 推断】
5. **三种"去 debuff"手段不等价**：`RemoveBuff` / `ClearDebuffs`（真实移除，会触发 `OnStatBuffChanged` + 刷新 FX/UI）> 把 `buffEndtTime` 清零（列表里仍留着该条，UI/AI 仍看得见）> 改 `buffImmuneToAcid/Spore`（派生标志位，`ExecuteBuffs` 会重算覆盖）。详见 §5.3。
6. 本文件所有"测试"列 = `未实机验证`。

---

## 2. Q1 —— 负面状态到底存在哪里

### 2.1 `BuffManager` 是静态定义表（该主张已验证为真）

【事实-程序集】`BuffManager : Il2CppSystem.Object`（**不是** MonoBehaviour），完整成员只有：

```csharp
public static BuffManager instance { get; set; }                 // 静态单例字段
public Il2CppSystem.Collections.Generic.List<BuffAttr> buffAttrs  // 全部定义，线性表
public Il2CppSystem.Collections.Generic.Dictionary<int, BuffAttr> buffDic  // buffId → 定义
public void Init();                                              // native _Public_
public BuffAttr GetBuffAttrByID(int m_buffId);                    // native _Public_
```

- 方法总数 = 2（`Init` / `GetBuffAttrByID`）+ 构造函数。**不存在** `AddBuff` / `RemoveBuff` / `Apply` / `Clear` / `Register` 任何形态。
- 对应 `BuffAttr : ScriptableObject`（**是** ScriptableObject，即 Unity 资产），字段即官方文档的"buff 配置"：`buffId, buffName, buffDescriptionTextID, effectName, effectSingleInstance, hpPerSecond, speedGain, bodyTemperaturePerSecond, hungerPerSecond, thirstPerSecond, energyPerSecond, meleeAttackMultiplier, immuneToAcid, immuneToSpore, showOnPlayerOnhitPanel, causeDeathFlag, hpDrainFloorPercent, isDebuff, armorPointsGain, perceptionScale, radiationIntakeScale, radiationDecayScale, hpRecoverScale, carryWeightGain, staminaRegenScale, isMealBuff`，另有只读属性 `BuffName` / `BuffDescription`。
- **结论**：`BuffManager` = 纯数据表（"字典/图鉴"），**不持有任何角色状态**。与 `BuffAttr` 的 ScriptableObject 身份、以及 `Assets/Resources/Properties/Buffs/` 数据来源完全吻合。【事实-程序集 + 事实-官方文档】

> 官方文档的字段说明表**未列出** `effectSingleInstance, hpDrainFloorPercent, armorPointsGain, perceptionScale, radiationIntakeScale, radiationDecayScale, hpRecoverScale, carryWeightGain, staminaRegenScale, isMealBuff`——文档不全，但已列出的每一项都能在程序集里逐一对应上，**无一项冲突**。

### 2.2 真正的活跃状态容器：`CharacterData.buffDataList`

【事实-程序集】

```csharp
// CharacterData : Il2CppSystem.Object   （存档数据类，非 MonoBehaviour）
public unsafe Il2CppSystem.Collections.Generic.List<BuffData> buffDataList;
public unsafe CharacterStatusData   characterStatusData;
public unsafe CharacterPropertyData characterPropertyData;
public unsafe CharacterBodyPartsData bodypartsData;
public unsafe CharacterEquipmentData characterEquipmentData;
public unsafe float debuffAttenuation;      // debuff 强度衰减系数（对应文档 isDebuff 说明）
public unsafe int   characterAttrId;
// ... 另有 positionX/Y, characterName, inventoryData, skillDataList 等存档字段
```

```csharp
// BuffData : Il2CppSystem.Object —— 只有两个字段
public unsafe int   buffId;        // Buff 定义 ID
public unsafe float buffEndtTime;  // 结束时间（游戏内时间轴，浮点）
```

**没有** `CharacterBuff*` 类型。**没有** `HashSet<int> buffIds` 或 `currentBuffs` 字段（对 `type-list.txt` 全量扫描 + 对 `BasicCharacterController` 全字段（约 430 个）逐条扫描，`buff` 相关字段仅 16 个，见下）。

### 2.3 `BasicCharacterController` 上的 16 个 buff 相关成员（全字段扫描结果）

【事实-程序集】这些**都是"聚合倍率 / 特效 / 静态 ID 常量"**，没有一个是活跃状态列表：

| 字段 | 类型 | 性质 |
|---|---|---|
| `buffSpeed` | `float` | 速度倍率聚合值 |
| `buffPerceptionScale` | `float` | 感知倍率聚合值 |
| `buffRadiationIntakeScale` | `float` | 辐射摄入倍率聚合值 |
| `buffRadiationDecayScale` | `float` | 辐射衰减倍率聚合值 |
| `buffHpRecoverScale` | `float` | HP 回复倍率聚合值 |
| `buffStaminaRegenScale` | `float` | 精力回复倍率聚合值 |
| `buffMeleeAttackMultiplier` | `float` | 近战伤害倍率聚合值 |
| `buffImmuneToAcid` | `bool` | 酸免疫派生标志 |
| `buffImmuneToSpore` | `bool` | 孢子免疫派生标志 |
| `buffEffectDic` | `Il2CppSystem.Collections.Generic.Dictionary<int, List<GameObject>>` | buffId → 特效 GameObject 列表（表现层） |
| `netVisualOnlyBuffIds` | `Il2CppSystem.Collections.Generic.HashSet<int>` | 仅表现层的 buff ID 集合（网络用） |
| `mealBuffResult` | `MealBuffResult` | 上次进食结果（内含 `appliedMealBuffIds: List<int>`） |
| `foodRecoveryBuffIds` | **static** `Il2CppStructArray<int>` | 食物回复类 buff 的 ID 白名单（**静态，值未读出**） |
| `characterData` | `CharacterData` | ← **活跃 buff 列表在这里面** |
| `isPlayer` | `bool` | |
| `maxHP` / `FullHP` / `sanity` / `deadFlag` | `float/bool` | 注意：`hp` 本身**不在** BCC 上（见 §6.4） |

### 2.4 三条独立旁证（把"buffDataList 是权威活跃集合"钉死）

1. **UI 直接吃 `BuffData`**：【事实-程序集】`BuffInfoUI` 持 `public BuffData buffData;` + `public BuffAttr buffAttr;`，入口 `Init(BuffData m_buffData)`，另有 `UpdateTimer()`；父容器 `BuffPanel` 持 `List<BuffInfoUI> buffInfoUIList` + `RefreshAllBuffs()`。→ UI 的每一条状态 = 一个 `BuffData`。【推断】其数据源就是这个 `List<BuffData>`。
2. **GOAP AI 直接吃 `BuffData`**：【事实-程序集】`GOAPAction_ClearDebuff_Burning` 与 `_Spore` 都有闭包方法 `_Execute_b__6_0(BuffData buff)` / `_Execute_b__4_0(BuffData x)` 及静态字段 `Il2CppSystem.Func<BuffData, bool>`。→ AI 在 `List<BuffData>` 上做**谓词筛选**（形状与 `List<T>.Find/Exists/RemoveAll` 一致）。【推断】具体是哪个 LINQ/List 方法。
3. **网络消息与 `BuffData` 同构**：【事实-程序集】`ZedZone.Net.MsgPlayerBuff` 只有 `public int buffId; public float buffEndTime;`——与 `BuffData{buffId, buffEndtTime}` 一一对应（仅拼写规范化）。存在 `public void NetApplyVisualBuff(int m_buffId, float m_buffEndTime)` 与 `MsgBuffFx`。→ 联机同步的原子单位是 **(buffId, endTime) 对**。

### 2.5 结论（Q1 直答）

> **"活跃负面状态"存在 `CharacterData.buffDataList : Il2CppSystem.Collections.Generic.List<BuffData>` 上**（每个角色一份，`BasicCharacterController.characterData` 引用）。
> `BuffManager` **只是静态定义表**——已验证：无任何增删方法，只有 `Init()` / `GetBuffAttrByID()` 两个方法 + 两个容器字段。
> 玩家取法：`GameController.instance.playerCharacter.characterData.buffDataList`（`playerCharacter` 字段类型确认是 `HumanCharacterController`）。
> 备用取法：`CharacterInput.instance.characterController`（同为 `HumanCharacterController`）。
> 全部角色：`GameController.instance.characterList : List<BasicCharacterController>`。

---

## 3. Q2 —— 完整的负面状态清单

### 3.1 官方 31 条（【事实-官方文档】逐条抄录，未增补未删减）

引擎名照抄文档原文（含原文拼写错误，如 `Serverely Over Weight`、`torseWound`），**以便插件里做字符串匹配时不出偏差**。

| ID | 中文名 | 引擎名（原文） | Debuff | 可致死 | 主要效果（文档原文） | 交叉证据 |
|---|---|---|---|---|---|---|
| 1 | 燃烧 | `Burning` | ✓ | ✓ | HP/秒 -20 | `GOAPAction_ClearDebuff_Burning` 存在 |
| 2 | 酸蚀 | `Acid` | ✓ | ✓ | HP/秒 -5；速度 -10% | `AcidTileMap` 类型存在 |
| 3 | 电击 | `Electrify` | ✓ | | 速度 -35% | — |
| 4 | 寒冷 | `Cold` | ✓ | | 体温/秒 -0.0025°C | — |
| 5 | 温暖 | `Warm` | | | 体温/秒 +0.005°C | 正面 |
| 6 | 失温 | `Hypothermia` | ✓ | | HP/秒 -0.5；速度 -10% | `GOAPAction_ClearDebuff_Hypothermia` 存在 |
| 7 | 发烧 | `Fever` | ✓ | | HP/秒 -2.5；速度 -20% | `GOAPAction_ClearDebuff_Fever` 存在 |
| 8 | 潮湿 | `Wet` | ✓ | | 速度 -5%；体温/秒 -0.002°C | 设置串 `Buff_Wet`（弱证据） |
| 9 | 饱腹恢复 | `HungerRecover` | | | 饥饿/秒 +5 | 正面 |
| 10 | 解渴 | `ThirstRecover` | | | 口渴/秒 +5 | 正面 |
| 11 | 生命恢复 | `HPRecover` | | | HP/秒 +50 | 正面 |
| 12 | 精力恢复 | `EnergyRecover` | | | 精力/秒 +5 | 正面 |
| 13 | 保暖(无上限) | `WarmNoClampBuff` | | | 体温/秒 +0.01°C | 正面 |
| 14 | 低烧 | `LowFever` | ✓ | | HP/秒 -0.2；速度 -5% | — |
| 15 | 食物中毒 | `FoodPoison` | ✓ | | HP/秒 -5；速度 -20%；体温/秒 -0.05；饥饿/口渴/精力/秒 -1 | — |
| 16 | 孢子感染 | `Spore Infected` | ✓ | ✓ | HP/秒 -5；速度 -10%；体温/秒 +0.02 | `GOAPAction_ClearDebuff_Spore` 存在 |
| 17 | 饥饿 | `Hunger` | ✓ | | HP/秒 -5；速度 -10%；体温/秒 -0.01 | — |
| 18 | 口渴 | `Thirst` | ✓ | | HP/秒 -5；速度 -15%；体温/秒 -0.005 | — |
| 19 | 超重 | `Over Weight` | ✓ | | 饥饿/秒 -0.025 | `GOAPAction_ClearDebuff_Overweight` 存在 |
| 20 | 严重超重 | `Serverely Over Weight` | ✓ | | HP/秒 -0.25；速度 -20%；饥饿/口渴/秒 -0.05 | 同上（同一 GOAP 动作覆盖 19/20 之一或两者，【未确认】） |
| 21 | 头部伤口 | `headWound` | ✓ | | — | `CharacterBodyPartsData` 存在 |
| 22 | 躯干伤口 | `torseWound` | ✓ | | — | 同上 |
| 23 | 手臂伤口 | `armWound` | ✓ | | — | 同上 |
| 24 | 腿部伤口 | `legWound` | ✓ | | 速度 -5% | 同上 |
| 25 | 溺水 | `Drowning` | ✓ | | HP/秒 -20；速度 -25%；体温/秒 -0.1；精力/秒 -2 | `GOAPAction_ClearDebuff_Drowning` 存在 |
| 26 | 治疗烟雾 | `HealingSmoke` | | | HP/秒 +50；速度 +10%；体温/秒 +0.05；精力/秒 +10 | 正面；物品 620「强效治疗烟雾」 |
| 27 | 失血 | `BloodLoss` | ✓ | | 速度 -20% | `CharacterStatusData.bloodVolume` 存在 |
| 28 | 耐力药剂 | `StaminaPotion` | | | 精力/秒 +3 | 正面；物品 270「能量药剂」 |
| 29 | 力量增益 | `Strength` | | | 近战伤害 ×1.5 | 正面；物品 272「力量增强药剂」 |
| 30 | 解毒剂 | `Antitoxin` | | | 免疫酸液；免疫孢子 | 正面；物品 273「抗毒性药剂」 |
| 31 | 速度增益 | `Speed` | | | 速度 +30% | 正面；物品 274「疾速药剂」 |

**统计**：Debuff **20 条**（1,2,3,4,6,7,8,14,15,16,17,18,19,20,21,22,23,24,25,27）；正面 **11 条**（5,9,10,11,12,13,26,28,29,30,31）。合计 31 ✓。
**可致死** 仅 3 条：**1 Burning、2 Acid、16 Spore Infected**（`causeDeathFlag`）。

### 3.2 文档之外的真实 buff ID（重要：32 号以上存在）

【事实-程序集 强证据】以下字段**存在**，且其语义明确是 buff ID：

| 位置 | 字段 | 推断含义 |
|---|---|---|
| `HumanCharacterController` | `static int bloodlustBuffId1`、`static int bloodlustBuffId2` | 血怒系技能 buff（**不在** 31 条表内） |
| `HumanCharacterController` | `static int killStaminaRegenBuffId` | 击杀回精技能 buff（不在表内） |
| `RadiationDose` | `static int BuffMild`、`BuffModerate`、`BuffSevere` | 辐射病轻/中/重度 buff |
| `RadiationDose` | `static int BuffIodine`、`BuffAntiRad` | 碘片 / 抗辐射药 buff |
| `BasicCharacterController` | `static Il2CppStructArray<int> foodRecoveryBuffIds` | 食物回复类 buff ID 白名单 |

> **结论：【事实-程序集】buff ID 空间 > 31，且官方文档只是"食物/药剂 MOD 可引用的 31 条"，并非全集。**
> 运行时可在插件里直接读这些静态字段的真值（`Il2CppStructArray<int>` / `int`），但**本文件不给出数值**（未读出，不臆造）。→ 这直接决定了 §6 白名单必须按 ID **白名单**（而不是"非文档内 ID 一律清"）。

### 3.3 三源对账表（文档 / interop 类型 / GOAP）

| 来源 | 覆盖到的东西 | 与官方表的关系 |
|---|---|---|
| 官方 Buff 文档（31 条） | 全部 buff 定义、isDebuff、causeDeathFlag、数值 | **基准**，权威 |
| GOAP `ClearDebuff_{Burning, Drowning, Fever, Hypothermia, Overweight, Spore}` | 6 个可被 AI 主动处理的 debuff | 全部能映射到文档 ID：Burning→1、Drowning→25、Fever→7、Hypothermia→6、Overweight→19/20、Spore→16。**无一超出 31 条范围** |
| interop 类型名 | `BuffManager/BuffAttr/BuffData/BuffInfoUI/BuffPanel/BuffLoopAudio/BuffProceduralFX/BloodlustBuffFX/PoisonSmokeEffect/AcidTileMap/SporeFogField` | `BloodlustBuffFX` 对应上面 `bloodlustBuffId1/2`（表外）；`PoisonSmokeEffect` **不是 buff**（见 §6.2） |
| `gameSettings.settings` 字符串 `Buff_Wet/Hypothermia/Acid/Thirst/Spore/Overweight` | 6 个名字 | **弱证据**：这 6 个都能对上文档 ID 8/6/2/18/16/19，但**格式带 `Buff_` 前缀，与 `buffName` 原文不一致**，且无 `Buff_Burning`（最该有的一个反而没有）→ 判定为**设置界面/UI 的独立枚举或旧版残留**，不可作为"真实 buff 全集"依据。【推断】 |

**未见证据、因而不写的（拒绝臆造）**：`Infection`、`Virus`、`Disease`、`Sickness`（作为独立 buff 名）、`Radiation`（作为 buff 名）、`Poison`（作为 buff 名）。

---

## 4. Q3 —— 如何安全地读取当前状态

### 4.1 泛型陷阱（最容易踩的坑）

| 写法 | 结果 |
|---|---|
| `Il2CppSystem.Collections.Generic.List<BuffData>` | ✅ **正确**。这是 IL2CPP 侧真实类型（`CharacterData` 的 `using` 就是 `Il2CppSystem.Collections.Generic`）。 |
| `System.Collections.Generic.List<BuffData>` | ❌ 编不过 / 运行时是**另一个托管类型**。IL2CPP 的 `List<T>` 是 native 结构，`System.List<T>` 与它二进制不兼容、不能强转、不能 `Cast<>`。 |
| `list.ToArray()` / `.Where()` / `.Select()` / `foreach` | ⚠️ `foreach` 可用但每次都走接口枚举器（额外 GC + 虚调用）；`ToArray()` 会**复制**。高频轮询建议 `for (int i = 0; i < list.Count; i++)` + 索引器。 |
| `Dictionary<int, BuffAttr>` / `HashSet<int>` | 同陷阱：一律用 `Il2CppSystem.Collections.Generic.*`。 |

### 4.2 推荐的只读枚举（不触碰游戏状态）

```csharp
using Il2CppSystem.Collections.Generic;   // 关键：不要用 System.Collections.Generic

static void DumpBuffs(HumanCharacterController p)
{
    var data = p?.characterData;                 // CharacterData
    var list = data?.buffDataList;               // Il2CppSystem...List<BuffData>
    if (list == null) return;

    for (int i = 0; i < list.Count; i++)
    {
        BuffData bd = list[i];                  // 元素是 native 引用包装
        if (bd == null) continue;

        int   id      = bd.buffId;
        float endTime = bd.buffEndtTime;        // 注意拼写：buffEndtTime

        BuffAttr attr = BuffManager.instance?.GetBuffAttrByID(id);   // 可能为 null
        bool isDebuff = attr != null && attr.isDebuff;
        bool lethal   = attr != null && attr.causeDeathFlag;
        string name   = attr != null ? attr.buffName : $"<unknown:{id}>";

        // 只做记录，禁止在这里 AddBuff/RemoveBuff
    }
}
```

要点：
- `attr` **可能为 null**（表外 ID、或 `BuffManager` 未 `Init()` 完）→ 必须判空，别用 `attr.isDebuff` 裸取。
- **同一 `buffId` 可能在列表里出现多条**——依据：存在 `AddBuff_Overlay(int, float)`（"Overlay 叠加"语义）与 `buffEffectDic` 是 `Dictionary<int, List<GameObject>>`（一个 ID 多个特效）。【推断】因此**按 ID 去重后再判断**，别假设唯一。
- **不要跨帧缓存 `BuffData` / `BuffAttr` 包装对象**：`Il2CppObjectPool` 会复用包装；移除 buff 后旧包装可能变悬空或指向别处。【推断】只缓存 `int buffId` / `float endTime` 这样的**值**。
- **不要缓存 `buffDataList` 引用**：`RemoveAll` 之类操作可能替换内部数组。【推断】
- 轮询频率：每帧 × 每角色 × 每 buff 会明显掉帧，建议 ≥0.25s 一次或事件驱动。

### 4.3 玩家 / 角色取法（已验证签名）

```csharp
HumanCharacterController p = GameController.instance.playerCharacter;   // 字段，类型 HumanCharacterController
// 备用：CharacterInput.instance.characterController （同为 HumanCharacterController）
// 全部角色：GameController.instance.characterList （Il2CppSystem...List<BasicCharacterController>）
```

`BasicCharacterController.isPlayer : bool`、`deadFlag : bool` 可用于过滤。

---

## 5. Q4 —— 如何安全地添加 / 移除状态

### 5.1 真实方法清单（全部在 `BasicCharacterController` 上）

访问修饰符取自 native token 名（如 `NativeMethodInfoPtr_RemoveBuff_Public_Void_Int32_0`）——interop 把 `protected/private` 统一暴露成 `public`，但 **token 名保留了真实修饰符**，所以这一列是可靠事实。

| 精确签名 | 真实访问级 | 返回 | 说明 |
|---|---|---|---|
| `virtual void AddBuff(int m_buffId, float m_buffEndTime)` | `_Public_Virtual_New_` | void | 加 buff；`HumanCharacterController` 有 override（native `_Public_Virtual_New_` 同名项） |
| `bool AddBuff_Overlay(int m_buffId, float m_addedInGameTime)` | `_Public_` | bool | 叠加式加 buff（时长**增量**语义，参数名 `m_addedInGameTime`）【推断】 |
| `void RemoveBuff(int m_buffId)` | `_Public_` | void | **按 ID 移除单条**（移除全部同 ID 条目 or 一条，【未确认】） |
| `virtual void ClearDebuffs(bool dotOnly)` | `_Public_Virtual_New_` | void | **批量清 debuff**；`dotOnly=true` 疑为"只清持续伤害类"【推断】 |
| `void ClearDebuffs(bool dotOnly, int keepBuffId)` | `_Protected_` | void | 同上，额外**保留**一个 ID（疑为"保留刚触发的那个"）【推断】 |
| `void RemoveAllBuffs()` | `_Public_` | void | 清**全部** buff（含正面）——**危险** |
| `void NetApplyVisualBuff(int m_buffId, float m_buffEndTime)` | `_Public_` | void | 网络"仅视觉"应用（配合 `netVisualOnlyBuffIds`），**不改数值** |
| `void RefreshBuffEffects(int m_buffId, bool refreshPanel = true)` | `_Private_` | void | 重建该 buff 的特效/UI |
| `void ApplyBuffStatusTick(BuffAttr buffAttr, float debuffScale, float buffPowerScale, float burningDamageScale)` | `_Private_` | void | 每 tick 施加数值效果（4 个缩放参数） |
| `virtual void ExecuteBuffs()` | `_Protected_Virtual_New_` | void | 每帧/每 tick 聚合所有 buff 到 `buffXxx` 倍率与免疫位 |
| `virtual void OnStatBuffChanged(int m_buffId)` | `_Protected_Virtual_New_` | void | 数值型 buff 变化回调（`HumanCharacterController` override） |
| `void InitBuffs()` | `_Protected_` | void | 初始化（建 `buffEffectDic` 等）【推断】 |
| `MealBuffResult AddFoodBuffs(ItemData m_itemdata)` | `_Public_` | `MealBuffResult` | 按食物数据加 buff，返回 `appliedMealBuffIds: List<int>` |
| `bool ExecuteFoodRecoveryBuffsOffClock()` | `_Public_` | bool | 食物回复类 buff 的非时钟推进路径 |
| 静态 `int GetRecoveryBuffId(int …)` / `void RefreshBodyPartRecoveryBuff(int …)` | `_Public_` / `_Public_` | int / void | 在 `HumanCharacterController`：部位伤害 → 回复 buff 的映射 |
| `static void RadiationDose.ApplySickness(HumanCharacterController h, int band)` | `_Public_` | void | 按辐射剂量档位施加"病"（→ `BuffMild/Moderate/Severe`）【推断】 |

**没有任何一个叫 `ApplyBuff` / `RemoveDebuff` / `ClearBuff` 的方法**（已全字段/全方法扫描 `BasicCharacterController` 与 `HumanCharacterController`）。别按这些名字去 patch。

### 5.2 添加的注意事项

- `AddBuff(id, endTime)` 的第二个参数是**绝对结束时间**（与 `BuffData.buffEndtTime`、`MsgPlayerBuff.buffEndTime` 同名同义），**不是**持续秒数。【推断，但三处字段同名同类型，一致性很高】
- `HumanCharacterController` override 了 `AddBuff` → 对玩家/人类调用会走子类逻辑（可能带额外的装备/属性联动）【推断】。**优先调 `AddBuff` 而不是直接往 `buffDataList.Add(new BuffData{...})`**——后者会跳过 `RefreshBuffEffects` / `OnStatBuffChanged` / 网络同步，造成"状态在但特效/倍率不在"的软锁。

### 5.3 ★ 三种"去 debuff"手段的对比（本文件的核心结论）

| 方案 | 做法 | 实际效果 | 风险 | 结论 |
|---|---|---|---|---|
| **A. 真实移除** | `RemoveBuff(id)` | 条目从 `buffDataList` 消失；走 `RefreshBuffEffects` + `OnStatBuffChanged`；FX 与 UI 同步 | 最低。**若源头发动者仍在**（站在酸液里、孢子雾里、超重、饥饿）→ 会被立刻重新施加 | ✅ **推荐（单条）** |
| **A'. 真实批量移除** | `ClearDebuffs(bool dotOnly)` | 按 `BuffAttr.isDebuff` 语义批量清；`dotOnly` 疑为只清 DoT 类 | 低。语义最贴合需求；但会清掉**全部** debuff（含伤口、饥饿、口渴）→ 可能与存档/身体系统不一致 | ✅ **推荐（UI 一键清）**，但建议先做白名单过滤，见 §6 |
| **B. 时长清零** | `bd.buffEndtTime = 当前时间`（或 0） | 列表**仍保留该条**；UI（`BuffInfoUI.UpdateTimer`）与 AI（GOAP 用 `Func<BuffData,bool>` 筛）**仍然看得见**；数值是否停止取决于 `ExecuteBuffs` 是否过滤过期项【未确认】 | 中-高。可能出现"状态栏还挂着但不再掉血"或"AI 一直去治一个治不好的病" | ⚠️ **不推荐**：语义不确定，且不可回滚地污染状态 |
| **C. 改免疫标志位** | `buffImmuneToAcid = true` / `buffImmuneToSpore = true` | 这两个是 **`ExecuteBuffs()` 从 buff 定义重算出来的派生聚合值**（`BuffAttr.immuneToAcid/immuneToSpore` → `buffImmuneToAcid/Spore`）【推断，依据是字段命名成对 + `ExecuteBuffs` 存在】 | 写进去会被下一 tick **覆盖**；若碰巧不被覆盖则会让 `Antitoxin`(30) 的 UI 与实际不符 | ❌ **无效/不稳定**。想要免疫就 `AddBuff(30, …)`（官方解毒剂语义） |
| **D. 全清** | `RemoveAllBuffs()` | 连 5/9/10/11/12/13/26/28/29/30/31 全清 | 高：清掉 `HealingSmoke`/`Speed`/`Strength` 会凭空削玩家；清掉 `Antitoxin` 会立刻被酸/孢子重新打 | ❌ **禁止** |

**免疫 vs 移除的语义差异（务必区分）**：
- **移除** = "现在没有这个 debuff"，源头仍会再打上。
- **免疫**（`Antitoxin` = buff 30，`immuneToAcid` + `immuneToSpore`）= "源头打不上"，是**持续态**，会进 UI 状态栏，到期自动消失。**这才是"抗性"的正确实现**。
- **辐射保护**是另一条独立链路：`ItemAttr_FaceMask.radiationProtectionFloat` + `RadiationDose.MaskProtection(ItemData)` + `ProtectionNow`，**不走 buff**。

### 5.4 IL2CPP 层面的可行性

- `RemoveBuff` / `ClearDebuffs` / `AddBuff` 的**真实访问级都是 public**（见 5.1 表）→ 可直接 `p.RemoveBuff(id)` 调用，**不需要** Harmony patch、不需要反射。
- 唯一的 `_Protected_` 重载 `ClearDebuffs(bool dotOnly, int keepBuffId)` 在 interop 里被暴露为 `public`，**可直调**（IL2CPP 不做访问检查）。但语义是"保留某 ID"，用错会漏清。【推断】
- `_Private_` 的 `RefreshBuffEffects` / `ApplyBuffStatusTick` 同样被 interop 暴露为 `public`，**技术上可直调**，但属于内部步骤，正常路径应由 `Add/RemoveBuff` 触发 → **不要手动调**（会重复施法/重复建特效）。

---

## 6. Q5 —— 感染 / 中毒 / 疾病 / 温度 的真实机制映射

### 6.1 感染（Infection）

**结论：【未确认】游戏里没有名为 "Infection" 的一等机制。** 证据：

- 【事实-程序集】对 `type-list.txt` 全量扫描 `infect`：**零命中**（既无 `Infection*` 类型，也无 `*Infection*` 字段）。
- 官方 31 条 buff 里**没有** "感染"，最接近的是 **16 孢子感染 `Spore Infected`**（isDebuff + causeDeathFlag）。
- 因此"感染"在玩家口中至少可能指 **4 种不同东西**，静态证据无法区分：

| 候选 | 证据 | 强度 |
|---|---|---|
| 孢子感染 = buff 16 `Spore Infected` | 官方文档 + `GOAPAction_ClearDebuff_Spore` + `SporeFogField`/`SporeNestFX`/`TerrainObject_SporeNest` + `buffImmuneToSpore` | **强**（名字里明写"感染"，有专门 GOAP 与免疫位） |
| 食物中毒 = buff 15 `FoodPoison` | 官方文档 | 中 |
| 辐射病（radiation sickness） | `CharacterStatusData.radiationDose` + `RadiationDose.BuffMild/Moderate/Severe` + `ApplySickness(h, band)` + `SicknessRefreshDays` + `HintSickness` | **强**（有独立病种 buff 与档位系统） |
| 伤口 → 恶化 | buff 21–24 + `CharacterBodyPartsData.bodypartDataAry` + `HumanCharacterController.GetRecoveryBuffId/RefreshBodyPartRecoveryBuff` | 中（伤口是 buff，但是否"恶化成感染"**无证据**） |

**什么能证明**：实机看孢子雾暴露后状态栏出现的名字与 `BuffManager.GetBuffAttrByID(16).buffName` 是否一致；以及伤口长期不包扎是否新增 buff ID（记录 `buffDataList` 前后差集）。

### 6.2 中毒（Poison / Acid）

| 机制 | 类型 | 证据 | 判定 |
|---|---|---|---|
| 酸蚀 debuff | **buff 2 `Acid`**（isDebuff + causeDeathFlag，HP/秒 -5、速度 -10%） | 官方文档；`BuffAttr.immuneToAcid` | 【事实-官方文档】 |
| 酸蚀地形 | `AcidTileMap`（瓦片层） | 类型存在 | 【事实-程序集】地形伤害源，非状态 |
| 酸免疫 | `BasicCharacterController.buffImmuneToAcid` ← buff 30 `Antitoxin` | 字段存在 + 官方文档"免疫酸液" | 【事实】字段；【事实-官方文档】语义 |
| **毒烟不是 buff** | `PoisonSmokeEffect : MonoBehaviour`，字段 `Radius / Duration / DamagePerSecond / state / particles / lights`，方法 `Bind(M_TileStateData)` / `Dissipate()` / `static CreateArea(Vector2, sbyte)` | 【事实-程序集】 | ⚠️ 它是**区域持续伤害 hazard**（tile-state 驱动），**不会**在 `buffDataList` 里出现。想"清毒烟"必须销毁/驱散这些对象，清 buff 没用 |
| 面具防护 | `ItemAttr_FaceMask.maskEffectiveTime / maskProtectionFloat / radiationProtectionFloat`；`RadiationDose.MaskProtection(ItemData)`、`PassOf(ItemData, float)` | 【事实-程序集】 | 防护是**装备属性**链路，不走 buff |

### 6.3 疾病（Disease / Fever）

- **无 `Disease` 类型**（全量扫描零命中）。
- 现有"病"全部由 buff 表达：**7 `Fever`**、**14 `LowFever`**、**15 `FoodPoison`**、**16 `Spore Infected`**、辐射三类 `RadiationDose.BuffMild/Moderate/Severe`。【事实-官方文档 + 事实-程序集】
- **AI 会主动治病**：【事实-程序集】`GOAPAction_ClearDebuff_Fever` 有 `ShedWarmLayers / ShedLayeredList / ShedSingle / CanStow / FindWarmSourceCovering / static maxGulps / gulpInterval / giveUpSeconds`。【推断】逻辑 = 脱掉保暖层 + 找热源遮盖物 + 喝水（"gulp"），即**物理降温**，而不是"删 buff"。
  → **含义：清掉 `Fever` 不会让 NPC 停止"它在发烧"的认知锚点之外的事；但会让 `GOAPGoal_ClearDebuff` 的优先级表失效。**
- 【事实-程序集】`GOAPGoal_ClearDebuff` 持 `Dictionary<int,int> debuffPriorityDic`、`Dictionary<int,string> debuffStateDic`、`static IsSuppressed(GOAPAgentState, int buffId)`、`hypothermiaSuppressedUntilKey` / `feverSuppressedUntilKey`。→ AI 以 **buffId 为键**管理"该治谁、治过多久之内别再治"。

### 6.4 异常温度

**温度本身不是 buff，是角色状态数值。**【事实-程序集】`CharacterStatusData`：

```csharp
public float m_bodyTemperature;              // 当前体温
public float standardBodyTemperature;        // 标准体温
public float minBodyTemperature;             // 下限
public float maxBodyTemperature;             // 上限
public float bodyTemperatureOffsetFloat;     // 偏移量
public float radiationDose;                  // 辐射剂量
public float bloodVolume, maxBloodVolume, deadBloodVolume;
public float hp, maxhp, hunger, maxHunger, thirst, maxThirst;
public float energy, maxEnergy, fatigue, maxFatigue, sanity, maxSanity;
```

> 注意：**`hp` / `bodyTemperature` / `radiationDose` 都不在 `BasicCharacterController` 上**，在 `characterData.characterStatusData` 里。`BasicCharacterController` 只有 `maxHP` / `FullHP` / `sanity` / `deadFlag`。

温度相关 buff（由 `BuffAttr.bodyTemperaturePerSecond` 每 tick 推动 `m_bodyTemperature`）：

| ID | 名 | 体温/秒 | isDebuff |
|---|---|---|---|
| 4 | `Cold` | -0.0025 | ✓ |
| 5 | `Warm` | +0.005 | |
| 6 | `Hypothermia` | （文档未给体温项，仅 HP -0.5 / 速度 -10%） | ✓ |
| 8 | `Wet` | -0.002 | ✓ |
| 13 | `WarmNoClampBuff` | +0.01 | |
| 15 | `FoodPoison` | -0.05 | ✓ |
| 16 | `Spore Infected` | +0.02 | ✓ |
| 17 | `Hunger` | -0.01 | ✓ |
| 18 | `Thirst` | -0.005 | ✓ |
| 25 | `Drowning` | -0.1 | ✓ |
| 26 | `HealingSmoke` | +0.05 | |

→ **失温链条【推断】**：`Wet`(-0.002) + `Cold`(-0.0025) 把 `m_bodyTemperature` 压低 → 越过阈值 → `Hypothermia`（+ `GOAPAction_ClearDebuff_Hypothermia` 介入）。**只删 `Hypothermia` 而不升温，会很快再触发。**

---

## 7. Q6 —— "清除负面状态"的最小安全集合

### 7.1 判定原则

1. **只清"纯状态型" debuff**：效果只是 DoT / 减速，本身不对应任何实体系统（不映射重量、不映射饥饿条、不映射部位 HP）。
2. **不清"需求/重量/部位"型 debuff**：它们是**输入量的镜像**，清掉只是掩盖读数，下一 tick 由源头系统重建（且可能与存档、UI 不一致）。
3. **绝不清正面 buff**：官方 11 条 + 表外 ID（血怒/击杀回精/辐射药）。
4. **绝不清表外未知 ID**：因为 ID 空间 >31（§3.2），未知 ID 可能是玩家已购技能的正向增益。**必须用白名单，不用黑名单。**

### 7.2 白名单推荐

#### ✅ 第一档：可以放心清（11 条，纯状态型）

`1 Burning`、`2 Acid`、`3 Electrify`、`4 Cold`、`6 Hypothermia`、`7 Fever`、`8 Wet`、`14 LowFever`、`15 FoodPoison`、`16 Spore Infected`、`25 Drowning`

理由：
- 效果在官方文档里**只有** `hpPerSecond` / `speedGain` / `bodyTemperaturePerSecond` 这类连续量，**不绑定**任何实体系统字段。
- 全部 `isDebuff = true`。
- 其中 1/2/16 带 `causeDeathFlag` → 清它们**收益最大**。
- ⚠️ 例外说明：`4 Cold` / `8 Wet` 虽安全，但**源头（天气/水）仍在时会立刻回来**——不是风险，只是"看起来没生效"。
- ⚠️ `25 Drowning` 清掉后若人还在水里，会马上重上；且溺水可能与位移/氧气逻辑耦合。【未验证】

#### ⚠️ 第二档：技术上能清，但**不建议默认清**（6 条，镜像型）

`17 Hunger`、`18 Thirst`、`19 Over Weight`、`20 Serverely Over Weight`、`21 headWound`、`22 torseWound`、`23 armWound`、`24 legWound`、`27 BloodLoss`

理由（逐条）：
- `17/18`：镜像 `characterStatusData.hunger/thirst`。清 buff 不改数值 → 立刻重建，且**可能掩盖"该吃饭了"的提示**。
- `19/20`：镜像背包总重；`GOAPAction_ClearDebuff_Overweight` 的做法是 `priorityRemoveItemIDs` + `RemoveOneItem`（**丢东西降重**），而不是删 buff → 删 buff 后重量仍超标，系统会重加。
- `21–24`：镜像 `bodypartsData.bodypartDataAry` 的部位 HP；`HumanCharacterController.GetRecoveryBuffId/RefreshBodyPartRecoveryBuff` 表明伤口 buff 由部位恢复系统**回填**。删 buff ≠ 治伤 → 只会造成"伤口不见了但 HP 缺口还在"。
- `27 BloodLoss`：镜像 `bloodVolume`；删 buff 不回血。

> **若产品上确实要"一键清负面"，建议做两档 UI**：默认只清第一档；第二档作为"高级/作弊"开关，并在 UI 里明示"不会治愈伤口/不会补饥饿"。

#### ❌ 绝对不清（11 条正面 + 全部表外 ID）

`5 Warm`、`9 HungerRecover`、`10 ThirstRecover`、`11 HPRecover`、`12 EnergyRecover`、`13 WarmNoClampBuff`、`26 HealingSmoke`、`28 StaminaPotion`、`29 Strength`、`30 Antitoxin`、`31 Speed`
＋ `HumanCharacterController.bloodlustBuffId1 / bloodlustBuffId2 / killStaminaRegenBuffId`
＋ `RadiationDose.BuffMild / BuffModerate / BuffSevere / BuffIodine / BuffAntiRad`（这些**本身是 debuff 家族**，但只能由剂量系统管理，手动删会与 `radiationDose`/`SicknessRefreshDays` 脱节）
＋ `foodRecoveryBuffIds` 里的任何 ID（静态白名单，值未读出 → **先读出来再决定**）

> 特别警告：`30 Antitoxin` 虽然"名字像解药"，它是**正面 buff**（免疫酸/孢子）。任何"清所有名字含 toxin/poison 的 buff"的过滤器都会误伤它。**只按 ID 白名单，禁止按名字模糊匹配。**

### 7.3 推荐实现（风险从低到高）

1. **只读展示**（零风险）：读 `buffDataList` + `BuffManager.GetBuffAttrByID` 显示状态列表/剩余时间。
2. **单条精确清除**：`RemoveBuff(id)`，且**仅当** `id ∈ 第一档白名单` 且 `attr.isDebuff == true`。这是"最小安全集合"。
3. **一键清**：对白名单逐个 `RemoveBuff(id)`（**不要**用 `ClearDebuffs`，因为它会连带清第二档与表外 debuff）；或 `ClearDebuffs(dotOnly: true)` 若实机证明 `dotOnly` 语义正好等于"只清 DoT"。**【未验证 —— 必须实机确认 `dotOnly` 的行为后再依赖它】**。
4. **免疫类需求**：`AddBuff(30, endTime)`（酸/孢子免疫）而不是改 `buffImmuneToAcid/Spore`。

### 7.4 会破坏 AI/剧情/存档的清单

| 动作 | 破坏点 | 依据 |
|---|---|---|
| 清 `Hypothermia` / `Fever` | NPC 不再去取暖/降温；`GOAPGoal_ClearDebuff.debuffPriorityDic` 与 `IsSuppressed` 的抑制窗口逻辑失配 | 【事实-程序集】字段存在 + 【推断】行为 |
| 清 `19/20 OverWeight` | NPC 不再丢东西减重，卡在"说不清为什么被卡"的状态 | 【推断】 |
| 清 `21–24 伤口` | 部位 HP 与 buff 永久不一致；`RefreshBodyPartRecoveryBuff` 可能重建 | 【推断】 |
| 清辐射三档 buff | 与 `radiationDose`、`SicknessRefreshDays`、碘片/抗辐射药 buff 冲突 | 【推断】 |
| `RemoveAllBuffs()` | 正面 buff 全灭（含 `HealingSmoke` 的 +50HP/s 与 `Antitoxin` 免疫） | 【事实-程序集】方法存在，语义按命名 |
| 直接 `buffDataList.Add/RemoveAt` | 绕过 `RefreshBuffEffects` / `OnStatBuffChanged` / 网络同步 → 特效与倍率不同步 | 【推断】 |
| 联机时客户端单方面改 | `MsgPlayerBuff` / `NetApplyVisualBuff` 存在 → 有服务端权威路径，客户端改动可能被回滚或造成不同步 | 【未验证】 |

---

## 8. Q7 —— 技术记录表

**所有"测试"列 = `未实机验证`**（本次调研未运行游戏、无日志）。

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
|---|---|---|---|---|---|---|
| `BuffManager.instance` | `static BuffManager` | 定义表单例 | `BuffManager.instance`（静态属性） | 只读；不要替换 | 若为 null 说明未 `Init()`，取 `BuffAttr` 会 NPE | 未实机验证 |
| `BuffManager.buffAttrs` | `Il2CppSystem.Collections.Generic.List<BuffAttr>` | 全部 31+ 条定义（线性） | 只读枚举 | 禁止增删（会破坏 `buffDic` 一致性） | 高（改定义表污染全局） | 未实机验证 |
| `BuffManager.buffDic` | `Il2CppSystem.Collections.Generic.Dictionary<int, BuffAttr>` | buffId→定义 | 只读查表 | 禁止增删 | 高 | 未实机验证 |
| `BuffManager.GetBuffAttrByID(int)` | `BuffAttr` | 按 ID 取定义 | `public`，直调 | 不适用（纯查询） | 低，**但可能返回 null** | 未实机验证 |
| `BuffManager.Init()` | `void` | 初始化定义表 | `public` | 不要在插件里调（会重建/覆盖） | 中 | 未实机验证 |
| `CharacterData.buffDataList` | `Il2CppSystem.Collections.Generic.List<BuffData>` | **★ 活跃 buff 权威列表** | `p.characterData.buffDataList`，public 字段 | 推荐经 `AddBuff`/`RemoveBuff`；**禁止**直接 `Add/RemoveAt` | 直改绕过 FX/UI/网络同步 | 未实机验证 |
| `CharacterData.debuffAttenuation` | `float` | debuff 强度衰减（体质削弱） | public 字段 | 不建议改（玩家会永久变强/变弱） | 中 | 未实机验证 |
| `CharacterData.characterStatusData` | `CharacterStatusData` | 体温/HP/饥饿/辐射/血液/理智 | public 字段，只读 | 改它=改存档数值，超出"清状态"范围 | 高 | 未实机验证 |
| `BuffData.buffId` | `int` | 该条状态的 buff ID | `list[i].buffId`，public 字段 | 不建议改（等于换 buff，FX 不跟随） | 高 | 未实机验证 |
| `BuffData.buffEndtTime` | `float` | 结束时间（**拼写多一个 t**） | public 字段 | 可写，但**不推荐**（条目仍在列表里，UI/AI 仍可见，见 §5.3-B） | 中-高 | 未实机验证 |
| `BuffAttr.buffId` | `int` | 定义 ID | public 字段 | **禁止**改（ScriptableObject 全局共享） | 极高 | 未实机验证 |
| `BuffAttr.buffName` | `string` | 引擎名（如 `Spore Infected`） | public 字段 | **禁止**改 | 极高 | 未实机验证 |
| `BuffAttr.isDebuff` | `bool` | 是否负面 | public 字段 | **禁止**改（`ClearDebuffs` 依赖它） | 极高 | 未实机验证 |
| `BuffAttr.causeDeathFlag` | `bool` | 是否可致死（1/2/16） | public 字段 | **禁止**改 | 极高 | 未实机验证 |
| `BuffAttr.immuneToAcid` / `immuneToSpore` | `bool` | 免疫酸/孢子（buff 30） | public 字段 | **禁止**改 | 极高 | 未实机验证 |
| `BuffAttr.hpPerSecond` / `speedGain` / `bodyTemperaturePerSecond` / `hungerPerSecond` / `thirstPerSecond` / `energyPerSecond` / `meleeAttackMultiplier` | `float` | 每 tick 数值效果 | public 字段 | **禁止**改（改一个 buff 影响所有角色全局） | 极高 | 未实机验证 |
| `BuffAttr.radiationIntakeScale` / `radiationDecayScale` / `hpRecoverScale` / `staminaRegenScale` / `perceptionScale` / `carryWeightGain` / `armorPointsGain` / `hpDrainFloorPercent` | `float` | 扩展倍率（文档未列） | public 字段 | **禁止**改 | 极高 | 未实机验证 |
| `BasicCharacterController.buffSpeed` / `buffPerceptionScale` / `buffRadiationIntakeScale` / `buffRadiationDecayScale` / `buffHpRecoverScale` / `buffStaminaRegenScale` / `buffMeleeAttackMultiplier` | `float` | 每 tick 聚合的派生倍率 | public 字段，**只读观察** | 不要写（`ExecuteBuffs` 会覆盖） | 中（写了会被覆盖或短时错乱） | 未实机验证 |
| `BasicCharacterController.buffImmuneToAcid` / `buffImmuneToSpore` | `bool` | 派生免疫位 | public 字段，只读 | 不要写（`ExecuteBuffs` 重算）→ 用 `AddBuff(30,…)` | 中 | 未实机验证 |
| `BasicCharacterController.buffEffectDic` | `Dictionary<int, List<GameObject>>` | buffId→特效对象 | public 字段，只读 | 禁止直改（改由 `RefreshBuffEffects` 管） | 高 | 未实机验证 |
| `BasicCharacterController.netVisualOnlyBuffIds` | `HashSet<int>` | 仅表现层 buff ID | public 字段，只读 | 禁止直改 | 高 | 未实机验证 |
| `BasicCharacterController.foodRecoveryBuffIds` | `static Il2CppStructArray<int>` | 食物回复 buff ID 白名单 | public 静态，**可读** | 不建议改 | 中 | 未实机验证 |
| `BasicCharacterController.AddBuff(int, float)` | `virtual void` | 加 buff（endTime 为**绝对时间**） | `_Public_Virtual_New_`，直调；`HumanCharacterController` 有 override | 正常入口 | 低-中（走子类联动） | 未实机验证 |
| `BasicCharacterController.AddBuff_Overlay(int, float)` | `bool` | 叠加式加 buff（时长增量语义【推断】） | `_Public_` | 正常入口 | 低 | 未实机验证 |
| `BasicCharacterController.RemoveBuff(int)` | `void` | **★ 按 ID 移除单条** | `_Public_`，直调 | 正常入口 | 低 | 未实机验证 |
| `BasicCharacterController.ClearDebuffs(bool dotOnly)` | `virtual void` | **★ 批量清 debuff** | `_Public_Virtual_New_`，直调 | 正常入口 | 低-中（会清全部 debuff，含镜像型） | 未实机验证 |
| `BasicCharacterController.ClearDebuffs(bool, int keepBuffId)` | `void` | 批量清但保留一个 ID | `_Protected_`（interop 暴露为 public，可直调） | 备选 | 中（保留错 ID 会漏清） | 未实机验证 |
| `BasicCharacterController.RemoveAllBuffs()` | `void` | 清**所有** buff | `_Public_` | **禁止使用** | **高**（清正面 buff） | 未实机验证 |
| `BasicCharacterController.NetApplyVisualBuff(int, float)` | `void` | 仅视觉应用（网络） | `_Public_` | 仅联机视觉路径 | 中（不改变数值） | 未实机验证 |
| `BasicCharacterController.RefreshBuffEffects(int, bool)` | `void` | 重建特效/UI | `_Private_`（interop 暴露为 public） | 不要手动调 | 中（重复建特效） | 未实机验证 |
| `BasicCharacterController.ApplyBuffStatusTick(BuffAttr, float, float, float)` | `void` | 每 tick 施加数值 | `_Private_` | 不要手动调 | **高**（重复施法） | 未实机验证 |
| `BasicCharacterController.ExecuteBuffs()` | `virtual void` | 聚合所有 buff → `buff*` 字段 | `_Protected_Virtual_New_` | 不要手动调 | 中-高 | 未实机验证 |
| `BasicCharacterController.OnStatBuffChanged(int)` | `virtual void` | 数值型 buff 变化回调 | `_Protected_Virtual_New_` | 由 Remove/Add 自动触发 | 中 | 未实机验证 |
| `BasicCharacterController.InitBuffs()` | `void` | 初始化 buff 表现 | `_Protected_` | 不要手动调 | 高（重建 FX 字典） | 未实机验证 |
| `BasicCharacterController.AddFoodBuffs(ItemData)` | `MealBuffResult` | 按食物加 buff | `_Public_` | 正常入口 | 低 | 未实机验证 |
| `BasicCharacterController.ExecuteFoodRecoveryBuffsOffClock()` | `bool` | 食物回复 buff 非时钟路径 | `_Public_` | 一般不需要调 | 中 | 未实机验证 |
| `HumanCharacterController.bloodlustBuffId1/2`、`killStaminaRegenBuffId` | `static int` | 表外技能 buff ID | public 静态，**可读** | 只读 | 低（读）；**禁止**当 debuff 清 | 未实机验证 |
| `HumanCharacterController.GetRecoveryBuffId(int)` | `static int` | 部位→回复 buff 映射 | `_Public_` | 只读 | 低 | 未实机验证 |
| `HumanCharacterController.RefreshBodyPartRecoveryBuff(int)` | `void` | 部位恢复 buff 回填 | `_Public_` | 不要手动调 | 中 | 未实机验证 |
| `RadiationDose.BuffMild/BuffModerate/BuffSevere/BuffIodine/BuffAntiRad` | `static int` | 辐射病档位 / 碘片 / 抗辐射 buff ID | public 静态，**可读** | 只读（值需运行时读出） | 低（读） | 未实机验证 |
| `RadiationDose.ApplySickness(HumanCharacterController, int)` | `static void` | 按剂量档位施加辐射病 | `_Public_` | 不要手动调 | 中 | 未实机验证 |
| `RadiationDose.Tick(HumanCharacterController, float)` | `static void` | 辐射剂量推进 | `_Public_` | 不要手动调（会打乱剂量） | 中-高 | 未实机验证 |
| `RadiationDose.MaskProtection(ItemData)` / `PassOf(ItemData, float)` | `static float` | 面具防护值 | `_Public_` | 只读查询 | 低 | 未实机验证 |
| `CharacterStatusData.m_bodyTemperature` / `standardBodyTemperature` / `minBodyTemperature` / `maxBodyTemperature` / `bodyTemperatureOffsetFloat` | `float` | 体温系统 | public 字段 | 可写=直接改体温（超出"清状态"范围） | 高 | 未实机验证 |
| `CharacterStatusData.radiationDose` | `float` | 辐射剂量 | public 字段 | 可写=直接改剂量 | 高 | 未实机验证 |
| `CharacterStatusData.hp` / `bloodVolume` / `hunger` / `thirst` / `energy` / `fatigue` / `sanity` 及 `max*` | `float` | 基础需求/生存数值 | public 字段 | 可写=改存档数值 | 高 | 未实机验证 |
| `BuffPanel.RefreshAllBuffs()` / `BuffInfoUI.Init(BuffData)` | `void` | 状态栏 UI | `_Public_` | 一般不需调（Remove/Add 自动刷） | 低 | 未实机验证 |
| `GOAPGoal_ClearDebuff.debuffPriorityDic` / `debuffStateDic` | `Dictionary<int,int>` / `Dictionary<int,string>` | AI 的 debuff 优先级/状态表 | public 字段，只读观察 | 禁止改（会扰乱 AI） | 高 | 未实机验证 |
| `GOAPGoal_ClearDebuff.IsSuppressed(GOAPAgentState, int)` | `static bool` | AI 抑制窗口判定 | `_Public_` | 只读 | 低 | 未实机验证 |
| `PoisonSmokeEffect.Radius` / `Duration` / `DamagePerSecond` / `Dissipate()` / `CreateArea(Vector2, sbyte)` | 混合 | 毒烟区域伤害（**非 buff**） | public | 想清毒烟需销毁这些对象 | 中 | 未实机验证 |
| `ItemAttr_FaceMask.maskEffectiveTime` / `maskProtectionFloat` / `radiationProtectionFloat` | `float` | 面具防毒/防辐射 | public 字段 | 改=改装备属性（超出范围） | 中 | 未实机验证 |
| `ZedZone.Net.MsgPlayerBuff.buffId` / `buffEndTime` | `int` / `float` | 联机 buff 同步帧 | 消息类型 | 不要伪造（联机不同步） | 高 | 未实机验证 |

---

## 9. Q8 —— 无法确认的清单

| # | 未确认项 | 为什么不确定 | 需要什么才能确认 |
|---|---|---|---|
| 1 | `ClearDebuffs(bool dotOnly)` 中 `dotOnly=true` 的确切过滤条件 | 方法体不可读；参数名是最好的线索（"dot only"） | 实机：构造 `Burning`(DoT) + `OverWeight`(非 DoT) 同时存在，调 `ClearDebuffs(true)` 看哪个留下 |
| 2 | `ClearDebuffs(bool, int keepBuffId)` 的 `keepBuffId` 用途 | 同上 | 实机对比调用前后 `buffDataList` 差集 |
| 3 | `RemoveBuff(int)` 是移除**一条**还是**该 ID 全部** | 无实现可读 | 用 `AddBuff_Overlay` 造出同 ID 两条，再 `RemoveBuff` 看剩几条 |
| 4 | `AddBuff(id, endTime)` 第二参数是**绝对时间**还是**持续时长** | 字段名一致（`buffEndTime`）但无实现 | 实机：传 `endTime = 当前 + 60` 与 `endTime = 60` 两种，看 UI 倒计时 |
| 5 | `AddBuff_Overlay` 的"overlay"是叠层数还是延长时间 | 无实现 | 实机连续调用两次，观察 `buffDataList` 条数与 `endTime` |
| 6 | 表外 buff ID 的实际数值（`bloodlustBuffId1/2`、`killStaminaRegenBuffId`、`RadiationDose.BuffMild/Moderate/Severe/Iodine/AntiRad`、`foodRecoveryBuffIds`） | 静态字段在 native 侧初始化 | 运行期读这些静态字段并打印；或 `BuffManager.buffAttrs` 全量 dump（**只读**，零风险） |
| 7 | "感染"到底是不是单一机制 | 无 `Infection` 类型；4 个候选都成立 | 实机记录：孢子雾暴露 / 吃腐食 / 伤口不包扎 / 高辐射 四种情形下 `buffDataList` 的新增 ID |
| 8 | 伤口 buff（21–24）是否会被 `RefreshBodyPartRecoveryBuff` 无条件重建 | 只知道方法存在 | 实机：清掉 `legWound` 后观察若干游戏分钟是否重上 |
| 9 | `ClearDebuffs` 清 `Hypothermia`/`Fever` 后 AI 是否立刻重上 | 需要 AI 行为观测 | 实机：在 NPC 附近清 debuff，看 `GOAPGoal_ClearDebuff` 优先级/行为日志 |
| 10 | 联机环境下移除 debuff 是否被服务端回滚 | 需要联机环境 | 双人实测 `MsgPlayerBuff` 往返 |
| 11 | `stone` 类"免疫标志位"是否真的每 tick 被 `ExecuteBuffs` 覆盖 | `ExecuteBuffs` 体不可读 | 实机：写 `buffImmuneToAcid = true` 后跨帧读回是否仍为 true |
| 12 | `gameSettings.settings` 里 `Buff_*` 字符串队列的真实来源 | 只有字符串，无类型引用 | 全程序集字符串交叉检索 / 该文件写回时机观察 |
| 13 | 32 号以上是否还有更多 buff（如护甲、药剂系列） | 只发现 5 组具名字段 | 运行期 dump `BuffManager.buffAttrs`（最可靠） |
| 14 | `PoisonSmokeEffect` 的伤害是否也走 `Buff_Acid` 或其他 buff | 只知 `DamagePerSecond` 字段 | 实机站在毒烟里看 `buffDataList` 是否新增条目 |
| 15 | `sanity`（`CharacterStatusData.sanity` / `BCC.sanity`）是否由某个 buff 驱动 | 无对应 buff 名 | 运行期关联分析 |

---

## 10. 复现命令（供后续取证）

```bash
# 环境
export DOTNET_ROOT="D:\\apps\\dotnet"
export DOTNET_ROLL_FORWARD=LatestMajor
ILSPY="ilspycmd"
DLL="<游戏目录>/BepInEx/interop/Assembly-CSharp.dll"

# 定义表
"$ILSPY" "$DLL" -t BuffManager
"$ILSPY" "$DLL" -t BuffAttr
"$ILSPY" "$DLL" -t BuffData

# 活跃容器
"$ILSPY" "$DLL" -t CharacterData
"$ILSPY" "$DLL" -t CharacterStatusData

# 增删方法（真实访问级看 NativeMethodInfoPtr_* 里的 _Public_/_Protected_/_Private_）
"$ILSPY" "$DLL" -t BasicCharacterController | grep -E "AddBuff|RemoveBuff|ClearDebuff|RemoveAllBuffs|InitBuffs|ExecuteBuffs"

# 全字段名提取（确认"没有别的容器"）
"$ILSPY" "$DLL" -t BasicCharacterController \
  | grep 'NativeFieldInfoPtr_.* = IL2CPP.GetIl2CppField' \
  | sed 's/.*"\(.*\)".*/\1/' | sort -u

# AI 侧
"$ILSPY" "$DLL" -t GOAPGoal_ClearDebuff
"$ILSPY" "$DLL" -t GOAPAction_ClearDebuff_Burning

# 辐射
"$ILSPY" "$DLL" -t RadiationDose
```

---

**版本说明**：本文件基于 2026-11-12 读到的 `Assembly-CSharp.dll`（interop）与离线官方文档 `11_Buff列表参考.md`（rev `1d3b2230`）。游戏更新后需重新核对 buff ID。
**未做的事**：未运行游戏、未实机验证任何一条行为、未修改游戏安装、未写入游戏目录。
