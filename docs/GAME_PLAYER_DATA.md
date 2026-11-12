# ZED ZONE 玩家数据结构技术档案（Phase-1 静态分析）

> 目标：为「生存助手」模组确定**本地玩家实例的稳定获取链**、**玩家状态数据的真实存储位置**、**最大值来源**与**存档持久性**。
>
> **本文全部结论来自静态分析，未在游戏中运行验证。** 任何「测试」栏一律为 `未实机验证`。

---

## 0. 证据等级与取证方法

### 0.1 标记约定

| 标记 | 含义 |
|---|---|
| 【事实-程序集】 | 直接从 `Assembly-CSharp.dll`（Il2CppInterop 镜像）的类型/字段/方法**签名**读出，签名可信 |
| 【事实-官方文档】 | 来自离线官方模组文档存档 `research/official-mod-docs/` |
| 【推断】 | 基于签名与结构证据的合理推断，**未读到方法体** |
| 【未验证】 | 无法从静态分析判定，需 Phase-2 实机测试 |

### 0.2 关键前提：interop 是 trampoline，不是实现

`BepInEx\interop\Assembly-CSharp.dll` 是 Il2CppInterop 生成的代理程序集：

- **类型、字段名、字段类型、方法签名、virtual/override 修饰、静态成员 —— 准确可信**；
- **方法体不可见**，全部是 `il2cpp_runtime_invoke` 跳板。因此**任何行为、调用关系、执行顺序都无法从本程序集读出**。

推论：本文可以断言「某字段存在且是 `float`」，但**不能**断言「它每帧递减」「A 写了 B」。所有涉及「谁写谁读」的结论一律降级为【推断】，并在第 7 节列出待验证项。

### 0.3 一个必须强调的取证陷阱

`[CallerCount(N)]` 是 Il2CppInterop 从 `MethodXrefScanCache.db` 带出的**引用范围长度（RefRangeEnd − RefRangeStart）**，**不是调用次数**，也不能说明谁调用了谁。本文**未使用**该值做任何调用关系推断。

### 0.4 官方文档存档的相关性说明

离线官方文档存档只覆盖：武器/近战/弹药/弹匣/枪械配件/箭矢/食物/容器/面罩 MOD 速查、Buff 列表、物品 ID、制作进阶。**没有任何一篇文档描述玩家类结构或脚本 API**。因此本文的【事实-官方文档】条目仅能支撑「数值语义」（如饥饿/口渴/精力按每秒变化），**不能**支撑任何代码结构结论。

### 0.5 本次实际使用的取证命令

```powershell
$env:DOTNET_ROOT="<dotnet>"; $env:DOTNET_ROLL_FORWARD="LatestMajor"
& "ilspycmd.exe" `
  "<游戏目录>\BepInEx\interop\Assembly-CSharp.dll" -t <TypeName>
```

游戏目录全程只读，未写入、未运行。

---

## 1. 玩家的真实类型

### 1.1 结论

**本地玩家是 `HumanCharacterController` 实例；`HumanCharacterController` 继承自 `BasicCharacterController`。**
`isPlayer` 是**基类 `BasicCharacterController` 上的 `bool` 字段**，不是专用子类的判据。

### 1.2 证据链

【事实-程序集】继承声明：

```csharp
public class BasicCharacterController : MonoBehaviour
public class HumanCharacterController : BasicCharacterController
```

【事实-程序集】**决定性证据**：游戏中两个「本地玩家」语义的权威字段，其声明类型都是 `HumanCharacterController`（而非 `BasicCharacterController`）：

| 宿主类型 | 成员 | 声明类型 |
|---|---|---|
| `GameController` | `playerCharacter` | `HumanCharacterController` |
| `CharacterInput` | `characterController` | `HumanCharacterController` |

若玩家只是「`isPlayer == true` 的 `BasicCharacterController`」，这两个字段不会用派生类作类型。这是**强类型证据**，无需读方法体。

【事实-程序集】`isPlayer` 归属确认（在 `BasicCharacterController` 的静态构造中注册）：

```
NativeFieldInfoPtr_isPlayer = IL2CPP.GetIl2CppField(
    Il2CppClassPointerStore<BasicCharacterController>.NativeClassPtr, "isPlayer");
```

`isPlayer` 是**实例字段**（`bool`），带 getter 与 setter。它存在实例字段这一点说明运行期可被赋值 —— 【推断】基类同时服务玩家与 NPC，`isPlayer` 用来在基类逻辑中区分阵营/输入归属。

【事实-程序集】`HumanCharacterController` **覆写**了 `hp`（第 10263 行，`public unsafe override float hp`）。这证明派生类不是空壳，玩家生命值走的是**覆写实现**，与基类实现不同。

### 1.3 候选类型排除表

类型清单来自 `research/type-list.txt`（4341 条）+ `ilspycmd -l c` 全量枚举。

| 候选类型 | 实际继承 | 判定 | 排除理由 |
|---|---|---|---|
| `BasicCharacterController` | `: MonoBehaviour` | ❌ 非玩家具体类型 | 它是**基类**，玩家实际对象是派生类实例 |
| **`HumanCharacterController`** | `: BasicCharacterController` | ✅ **玩家类型** | 被 `GameController.playerCharacter` 与 `CharacterInput.characterController` 同时声明为类型 |
| `HumanCharacterController_Looter` | `: HumanCharacterController` | ❌ NPC | 掠夺者 NPC；玩家不用此类型 |
| `HumanCharacterController_ExoBoss` | `: HumanCharacterController` | ❌ Boss | 外骨骼 Boss |
| `BasicCharacterController_Frame` | `: BasicCharacterController` | ❌ | 见下（`PetCharacterController` 的基类） |
| `PetCharacterController` | `: BasicCharacterController_Frame` | ❌ 宠物 | 宠物狗等 |
| `CrawlCharacterController` | `: BasicCharacterController` | ❌ 爬行敌人 | 爬行类敌人 |
| `MutationCharacterController` | `: BasicCharacterController` | ❌ 变异体 | 怪物 |
| `SnakeCharacterController` | `: BasicCharacterController` | ❌ 蛇 | 怪物 |
| `CarrionCharacterController` | `: BasicCharacterController` | ❌ 食腐者 | 怪物 |
| `PlayerStatusManager` | `: MonoBehaviour` | ❌ 纯 UI | **只有 1 个字段 `statustext`（`UnityEngine.UI.Text`）**，见 1.4 |
| `PlayerStatusPanel` / `PlayerPanel` / `PlayerInventoryPanel` | UI | ❌ UI | 面板类，非数据模型 |

**注意**：`PlayerStatusManager` / `PlayerStatusPanel` 名字里有 "PlayerStatus"，极易被误认作状态存储。两者都不是 —— 见 1.4。

### 1.4 明确澄清：`PlayerStatusManager` 不是状态存储

【事实-程序集】`PlayerStatusManager` 的**全部**字段与属性：

```csharp
public class PlayerStatusManager : MonoBehaviour
{
    private static readonly IntPtr NativeFieldInfoPtr_statustext;
    public unsafe Text statustext          // UnityEngine.UI.Text
}
```

整个类只有**一个** `Text` 引用，没有任何数值字段、没有 `CharacterStatusData`、没有 `BasicCharacterController` 引用。

**结论**：`PlayerStatusManager` 是一个 UI 文本持有者，**与玩家属性数值无关**。后续任何「通过 PlayerStatusManager 改饥饿」的方案都是错的。

【事实-程序集】`PlayerStatusPanel` 中检索 `CharacterStatusData` / `BasicCharacterController` / `HumanCharacterController` / `characterData` **均无命中** —— 该面板也不持有状态数据，【推断】它通过 `GameController` 间接取值。

### 1.5 额外澄清：`BuffManager` 不是状态存储

【事实-程序集】玩家声称，本文未独立复核 `BuffManager`：它是静态 Buff **定义表**（Buff ID → 效果描述），不是玩家当前状态容器。玩家当前的 Buff 实例列表在 `CharacterData.buffDataList`（`List<BuffData>`）。

---

## 2. 如何稳定拿到「本地玩家」实例（核心交付物）

### 2.1 推荐链路（一句话）

```csharp
GameController.instance?.playerCharacter   // HumanCharacterController
```

### 2.2 已找到的全部访问入口

#### 入口 A：`GameController.instance.playerCharacter` ★首选

【事实-程序集】

```csharp
// GameController : MonoBehaviour
public unsafe static GameController instance      // 静态属性（静态字段支撑）
public unsafe HumanCharacterController playerCharacter   // 实例属性，getter/setter 直接按字段偏移读写
NativeFieldInfoPtr_playerCharacter
NativeFieldInfoPtr_instance
```

- **到达方式**：`GameController.instance` → `.playerCharacter`
- **可靠性**【推断】：高。`GameController` 是游戏主控 `MonoBehaviour`，`instance` 为静态单例；`playerCharacter` 被声明为 `HumanCharacterController`，是官方自己的「玩家」字段语义。
- **可写性**：`playerCharacter` 有 setter，【推断】会在玩家生成/重生时被赋值，也可能被置 null。

#### 入口 B：`CharacterInput.instance.characterController` ★备选

【事实-程序集】

```csharp
// CharacterInput : MonoBehaviour
public unsafe static CharacterInput instance
public unsafe HumanCharacterController characterController
NativeFieldInfoPtr_instance
NativeFieldInfoPtr_characterController
```

- **到达方式**：`CharacterInput.instance` → `.characterController`
- **可靠性**【推断】：中高。类型同样是 `HumanCharacterController`，但 `CharacterInput` 是**输入组件**，其生命周期【推断】绑定在当前场景/玩家对象上，跨场景或玩家销毁时**更可能为 null 或指向旧对象**。

#### 入口 C：`GameController.instance.characterList` 全量扫描 + `isPlayer` 过滤

【事实-程序集】

```csharp
public unsafe List<BasicCharacterController> characterList
public unsafe void AddCharacter(BasicCharacterController character)
public unsafe void RemoveCharacter(BasicCharacterController character)
public unsafe BasicCharacterController GetCharacterByGUID(string guid)
public unsafe static bool IsPlayerSideCharacter(BasicCharacterController m_character)  // 静态
public unsafe static bool IsSameSideForDamage(BasicCharacterController a, BasicCharacterController b)
```

- **到达方式**：遍历 `characterList`，对每项读 `isPlayer`（`bool` 字段），取为 true 者后强转 `HumanCharacterController`。
- **可靠性**【推断】：中。作为**兜底**有价值（A/B 都为 null 时仍可能找到），但 `characterList` 是否包含玩家、【未验证】；且遍历有 O(n) 开销与并发修改风险。
- **`IsPlayerSideCharacter` 语义【未验证】**：名字暗示「玩家阵营」，但**可能**包含队友 NPC。**不可**用它判定「本地玩家」，更不能用它替代 `isPlayer`。

#### 入口 D：`GameController.GetCharacterByGUID(string)`

【事实-程序集】`public unsafe BasicCharacterController GetCharacterByGUID(string guid)`

- 需要先知道玩家 GUID。玩家 GUID 在 `CharacterData.characterId`（`string`）里。
- **可靠性**【推断】：中。多一次「找 GUID」的间接步骤，且返回值是基类，需强转。**不适合**作为主路径。

#### 入口 E：`InGameController.gameData.playerData`

【事实-程序集】

```csharp
// InGameController
public unsafe GameData gameData
// GameData
public unsafe CharacterData playerData
```

- 这条链只到**数据模型**（`CharacterData`），**拿不到场景对象**（没有 `BasicCharacterController` 引用）。
- 用途：**修改存档态数据**（玩家不在场时、或需要直接改 data 时）。
- **可靠性**【推断】：`InGameController` 是否存在对应静态 `instance` 【未验证】（本次未确认），若只能从场景中查找，则可靠性下降。

#### 入口 F：`GameController._gameData`（字段）vs `GameController.gameData`（属性）

【事实-程序集】

```csharp
public unsafe GameData _gameData        // 属性，getter 直接读字段 NativeFieldInfoPtr__gameData
public unsafe GameData gameData         // 属性，getter 通过 il2cpp_runtime_invoke 调方法
```

二者**不是同一个实现**：`_gameData` 是**直接字段读取**，`gameData` 是**方法调用**。

【推断】`gameData` 可能在多人/主机-客户端模式下返回不同对象（如客户端代理）。**若需要最原始的内存数据，用 `_gameData`；若需要游戏认为的"当前有效 GameData"，用 `gameData`。** 该差异【未验证】。

#### 入口 G（已排除）：`PlayerStatusManager`

见 1.4，无玩家引用。排除。

### 2.3 各入口对比

| 入口 | 签名 | 类型 | 拿到场景对象 | 拿到数据模型 | 可靠性【推断】 | 建议用途 |
|---|---|---|---|---|---|---|
| A `GameController.instance.playerCharacter` | `static GameController instance` / `HumanCharacterController playerCharacter` | 属性 | ✅ | ✅（.characterData） | **高** | **主路径** |
| B `CharacterInput.instance.characterController` | `static CharacterInput instance` / `HumanCharacterController characterController` | 属性 | ✅ | ✅ | 中高 | 备选/交叉校验 |
| C `characterList` + `isPlayer` | `List<BasicCharacterController> characterList` / `bool isPlayer` | 字段+属性 | ✅ | ✅ | 中 | 兜底 |
| D `GetCharacterByGUID(string)` | `BasicCharacterController GetCharacterByGUID(string)` | 方法 | ✅（需强转） | ✅ | 中 | 按 GUID 精确取 |
| E `InGameController.gameData.playerData` | `GameData gameData` / `CharacterData playerData` | 属性 | ❌ | ✅ | 中 | 只改数据 |
| F `GameController._gameData` | `GameData _gameData` | 属性（直读字段） | ❌ | ✅ | 中高 | 原始数据/多人排查 |

### 2.4 生命周期与缓存风险（重要）

**核心建议：不要缓存 `playerCharacter`，每次使用时现取并判空。**

理由：

1. 【事实-程序集】`playerCharacter` 是**可写**属性（有 setter）→ 运行期必然被重新赋值。
2. 【推断】玩家死亡/重生、`LoadCharacterData` 读档、切换场景/回主菜单时，旧的 `HumanCharacterController` GameObject 会被销毁，`playerCharacter` 指向新实例；**缓存的引用会变成「已销毁对象」**（Il2CppInterop 上表现为访问抛异常或读到垃圾数据）。
3. 【推断】主菜单阶段 `GameController.instance` 可能为 null（尚未进入游戏），或 `playerCharacter` 为 null（尚未 spawn）。**这两点必须实机确认**（见第 8 节 T1/T2）。

**推荐取用模式**（防御式，未实机验证）：

```csharp
static HumanCharacterController GetLocalPlayer()
{
    var gc = GameController.instance;          // 静态单例，每次现取
    if (gc == null) return null;
    var pc = gc.playerCharacter;               // 可能为 null
    if (pc != null) return pc;

    var ci = CharacterInput.instance;          // 备选
    if (ci != null && ci.characterController != null) return ci.characterController;

    return null;                               // 兜底可再扫 characterList
}
```

> ⚠️ Il2CppInterop 下 `== null` 对已销毁的 Unity 对象不可靠。对 `UnityEngine.Object` 系请用 `Object.op_Implicit`/`?.` 之外的方式，或捕获访问异常。此点【未验证】，Phase-2 需测「玩家重生后旧引用访问行为」。

### 2.5 重生 / 场景切换时 `playerCharacter` 是否被换掉

- 【事实-程序集】无法判定：setter 存在，但赋值点不可见（trampoline）。
- 【推断】**极可能被换掉**，依据：玩家是场景内 `MonoBehaviour`（`BasicCharacterController : MonoBehaviour`），死亡重生通常销毁/重建 GameObject；且存在 `CharacterData.netPendingRespawn`、`deadFlag`、`lastDeathPosition` 等重生相关字段，说明重生是一条正式流程。
- **结论**：按「每次现取」设计，不要依赖缓存。Phase-2 用 T3 验证。

---

## 3. `CharacterStatusData` 与 `BasicCharacterController` 的关系

### 3.1 数据链（各环节签名均已在程序集中核实）

```
GameController.instance.playerCharacter              // HumanCharacterController
  └─ .characterData                                  // CharacterData   （继承自 BasicCharacterController 的字段）
       └─ .characterStatusData                       // CharacterStatusData
```

【事实-程序集】各环节归属确认：

| 环节 | 声明 | 归属类 |
|---|---|---|
| `characterData` | `public unsafe CharacterData characterData` | `BasicCharacterController` |
| `characterStatusData` | `public unsafe CharacterStatusData characterStatusData` | `CharacterData` |

【事实-程序集】**控制器不直接持有 `CharacterStatusData`**。在 `BasicCharacterController` 的字段注册表中检索 `NativeFieldInfoPtr_*statusData*` **无命中**；`CharacterStatusData` 只在 `CharacterData` 里出现（本次反编译的 17 个类型转储范围内，仅 `CharacterData.cs` 引用它）。因此**唯一合法路径**是经由 `characterData`。

### 3.2 两套表示并存

【事实-程序集】同一批数值在两处都有声明：

**（甲）控制器上的运行期属性**（`BasicCharacterController`，均已在 `Il2CppClassPointerStore<BasicCharacterController>` 中注册）：

| 成员 | 形式 | virtual | 备注 |
|---|---|---|---|
| `hp` | **属性**（get/set） | ✅ virtual（token 100667675） | `HumanCharacterController` 覆写它 |
| `FullHP` | **属性**（仅 get） | — | 无 setter |
| `energy` | 属性（get/set） | ❌ | token 100667677 |
| `fatigue` | 属性（get/set） | ❌ | — |
| `hunger` | 属性（get/set） | ❌ | token 100667683 |
| `thirst` | 属性（get/set） | ❌ | — |
| `sanity` | 属性（get/set） | ❌ | — |
| `casEnergy` | 属性（get/set） | ❌ | — |
| `maxHP` | **字段**（`float`） | — | `NativeFieldInfoPtr_maxHP` |
| `isPlayer` | **字段**（`bool`） | — | `NativeFieldInfoPtr_isPlayer` |
| `deadFlag` | **字段**（`bool`） | — | — |

**（乙）数据模型上的字段**（`CharacterStatusData`，全部是 `float` 字段）：

`hp, maxhp, energy, maxEnergy, fatigue, maxFatigue, hunger, maxHunger, thirst, maxThirst, sanity, maxSanity, casEnergy, bloodVolume, radiationDose, m_bodyTemperature`
另有静态字段 `maxBloodVolume, deadBloodVolume, standardBodyTemperature, bodyTemperatureOffsetFloat, minBodyTemperature, maxBodyTemperature`，以及属性 `bodyTemperature`（get/set，【推断】读写 `m_bodyTemperature` 并叠加偏移）。

**关键差异**：`bloodVolume`、`radiationDose`、`bodyTemperature`、以及**全部 `max*`** 只存在于 `CharacterStatusData`（乙），控制器（甲）上没有对应成员。反之 `maxHP` 只在（甲）上有。

### 3.3 哪一个是权威的？（静态分析能给到的最强结论）

**【推断】`CharacterStatusData` 大概率是唯一真实存储；控制器上的 `energy/hunger/thirst/fatigue/sanity/casEnergy` 是转发属性（forwarding properties），并非独立存储。**

支撑证据（均为【事实-程序集】，结论为【推断】）：

1. 这些成员是**属性**（由 `NativeMethodInfoPtr_get_hunger_...` 方法 token 支撑），**不是字段**。若是 C# 自动属性，编译器会生成 `_hunger_k__BackingField` 之类的后备字段。
2. **后备字段不存在**：在包含 10 个类型、约 500 条字段指针的 `BasicCharacterController` 转储中，检索 `NativeFieldInfoPtr_*BackingField*` **只有 2 条**（`_isKnockedDown_k__BackingField`、`_vehicleKnockdownMass_k__BackingField`），**没有任何** `_hunger_/_thirst_/_energy_/_fatigue_/_sanity_` 后备字段。
3. 既然属性无后备字段，其实现**必须**从别处取值 —— 而 `characterData.characterStatusData` 恰好拥有**同名同类型**的全部字段。
4. 控制器自身持有 `characterData` 字段，访问链天然可达。

**反面说明（不能断言的部分）**：方法体是 trampoline，我**没有**直接读到「`get_hunger` 返回 `characterData.characterStatusData.hunger`」。理论上它们也可能转发到某个未识别的第三方对象，但在已枚举的引用关系里没有这样的候选。因此标记为【推断】而非事实，并列入 Phase-2 必测项（第 8 节 T4）。

### 3.4 是否存在显式的同步方法？

【事实-程序集】**在 `BasicCharacterController` 上没有找到任何 `SaveTo*` / `LoadFrom*` / `Apply*Data` / `Sync*Status` / `InitStatus*` / `RefreshStatus*` 形式的公开方法。**

对该类型转储中的全部公开方法按关键词 `Status|SaveData|Apply|LoadFrom|SaveTo|Sync` 检索，命中项只有以下 6 个，且全部与状态数值无关：

```
ApplyWaterShadowState(bool)
ApplyIslandBossHpScale(float)
ApplyCorpsePileAppearance()
ApplyBuffStatusTick(BuffAttr, float, float, float)
NetApplyVisualBuff(int, float)
ApplySortingGroupOrder()
```

与数值相关的仅 `MarkEnergyCostTime()` 及 `HasEnergy/CostEnergy/HasFatigue/CostFatigue/CharacterTired`（见 3.6）。

**结论**：控制器侧**不存在显式同步方法**。这与「转发属性」假说一致 —— 如果属性直接转发，本来就不需要同步。若是两份独立存储，则同步只能发生在**读档/存档时的逐字段拷贝**（不可见）或 **`HumanCharacterController.Init()/Awake()` 等覆写方法内部**（方法体不可见）。

### 3.5 数值变动（衰减）写在哪一侧？

- 【事实-程序集】`BasicCharacterController` 上**没有**名为 `*Hunger*`/`*Thirst*` 的衰减方法（仅 `MarkEnergyCostTime`）。
- 【事实-官方文档】游戏把饥饿/口渴/精力变化建模为**按秒速率**：
  - Buff `9 HungerRecover`：饥饿/秒 5；`10 ThirstRecover`：口渴/秒 5；`12 EnergyRecover`：精力/秒 5；
  - Buff `17 Hunger`：HP/秒 −5、速度 −10%、体温/秒 −0.01℃；`18 Thirst`：HP/秒 −5、速度 −15%、体温/秒 −0.005℃；
  - Buff `15 FoodPoison`：HP/秒 −5、饥饿/秒 −1、口渴/秒 −1、精力/秒 −1；
  - 食物 MOD 字段直接叫 `hunger`（饱食度变化）、`thirst`（口渴度变化）、`energy`（能量变化）。
- 【事实-程序集】`BasicCharacterController.ApplyBuffStatusTick(BuffAttr buffAttr, float debuffScale, float buffPowerScale, float burningDamageScale)` 签名与上述「按秒速率」模型吻合。
- **【推断】**：衰减由 Buff 系统按 tick 施加到**状态数据**上（Buff 持有 `hungerPerSecond/thirstPerSecond/energyPerSecond` 之类字段，官方文档第 66 行提到这三个键名），控制器属性只是读取视图。**未验证**：tick 究竟写 `CharacterStatusData` 还是写控制器。

### 3.6 控制器上也有一组「操作型」方法（可安全利用）

【事实-程序集】`BasicCharacterController`：

```csharp
public unsafe virtual bool HasEnergy(float m_energy)
public unsafe virtual bool HasFatigue(float m_fatigue)
public unsafe virtual bool CostEnergy(float m_energy)      // 返回 bool
public unsafe virtual bool CostFatigue(float m_fatigue)
public unsafe virtual void CharacterTired()
```

这些方法是 `virtual`，【推断】内部会对「能量/疲劳」做判定与扣减，是**游戏自己的改值入口**，比直接写字段更贴近原逻辑。但它们的扣减方向（加/减、是否 clamp）【未验证】。

### 3.7 面板/UI 读哪一侧

- 【事实-程序集】`PlayerStatusPanel` 不引用 `CharacterStatusData` 也不引用控制器 → 只能间接取值，具体取哪一侧**无法从签名判定**【未验证】。
- 【推断】若 3.3 的转发假说成立，则「读控制器属性」与「读 `characterStatusData`」**结果相同**，UI 读哪一侧都无所谓。

### 3.8 对模组的直接结论

| 问题 | 结论 | 等级 |
|---|---|---|
| 改 `characterStatusData.hunger` 能否影响游戏内饥饿？ | **大概率能**（若转发假说成立） | 【推断】 |
| 改 `bcc.hunger` 能否影响存档？ | **大概率能**（若属性 setter 转发写入 statusData） | 【推断】 |
| 两者会不会不一致？ | **若转发假说成立则不会**；若不成立则会，且 `maxHP` vs `maxhp` **已经**是两份独立存储 | 【推断】 |
| `maxHP`（控制器字段）与 `maxhp`（statusData 字段）是否同值？ | **未知，且很可能不同步** —— 这是已确认存在的重复存储 | 【未验证】 |

---

## 4. 最大值的来源

### 4.1 结论概要

**最大值不是玩家自由设定的，也不是直接写死在存档里的常量**；它由 `CharacterData` 一侧从「平衡系数 + 属性点 + 技能/天赋加成」**计算后写入** `CharacterStatusData.max*`。

### 4.2 证据

【事实-程序集】`CharacterData` 上的计算入口（**全部无参数**，说明其输入来自 `CharacterData` 自身字段）：

```csharp
public unsafe void RefreshStatus()
public unsafe void RefreshBodyPartsStatus()
public unsafe void SetMaxHP()
public unsafe void SetMaxEnergy()
public unsafe void SetMaxFatigue()
public unsafe void SetMaxHunger()
public unsafe void SetMaxThirst()
public unsafe void InitHumanCharacterData()
public unsafe float GetMaxHPBalanceFloat()
public unsafe float GetMaxHPBalanceRebaseFloat()
```

`SetMaxHP()` 等**不接受任何参数**，因此【推断】它们是从 `CharacterData` 自身的系数/加成字段算出结果，再写入 `characterStatusData.maxhp` 等字段。

【事实-程序集】`CharacterData` 上参与计算的相关字段（部分）：

| 字段 | 类型 | 【推断】作用 |
|---|---|---|
| `maxHPBalanceFloat` | `float` | 平衡系数（难度/模式给的乘区） |
| `appliedMaxHPBalanceFloat` | `float` | 已应用的平衡系数（记录上次值，用于增量重算） |
| `appliedMaxHPConstitution` | `int` | 已应用的体质属性点 |
| `appliedMaxHPSkillFloat` | `float` | 已应用的技能加成 |
| `skillProperty_MaxHPFloat` | `float` | 技能提供的最大生命加成 |
| `skillProperty_HungerRateFloat` | `float` | 技能提供的饥饿速率系数 |
| `skillProperty_ThirstRateFloat` | `float` | 技能提供的口渴速率系数 |

`applied*` 前缀字段的存在，【推断】是为了「只重算变化量」，即最大值可随升级/加点/装备**动态重算**。

【事实-程序集】难度与全局速率在**新游戏配置**里，不在玩家身上：

```csharp
// GameConfigData
public unsafe int difficulty
public unsafe int hungerThirstRate
public unsafe float hungerThirstRateFloat
public unsafe int staminaRegenRate
public unsafe float staminaRegenRateFloat
public unsafe int gameMode
public unsafe int permadeathMode
public unsafe int exploreMode
```

【推断】`difficulty` / `gameMode` 通过 `maxHPBalanceFloat` 影响最大生命；`hungerThirstRate*` 影响**饥饿/口渴的变化速率**（而非上限）。

【事实-程序集】`NewGameConfigData` 把三者打包：

```csharp
public unsafe MapConfigData mapConfigData
public unsafe CharacterData characterData
public unsafe GameConfigData gameConfigData
```

说明**角色数据与难度配置在建档时就绑定**。

### 4.3 「谁写的」——能找到什么，找不到什么

- 【事实-程序集】能看到 `SetMaxHP()` 等**无参写入入口**的存在，能确认 `CharacterStatusData` 的 `max*` 都是**裸字段**（`CharacterStatusData` 上除 `bodyTemperature` 外**没有任何** setter 方法，只有一个无参构造函数）→ 意味着**任何代码都能直接改这些字段，没有封装保护**。
- 【未验证】`SetMaxHP()` 的方法体（trampoline 不可读），因此**无法确认它写的是 `characterStatusData.maxhp` 还是 `BasicCharacterController.maxHP`**。
- 【未验证】`maxHP`（控制器字段）的赋值点。

**这是个真实风险**：存在两处 `maxhp`（控制器字段 + statusData 字段），静态分析**无法判定哪一个被 UI / 伤害计算使用**。见第 8 节 T5。

---

## 5. 存档与加载

### 5.1 结论

**`CharacterStatusData` 会随存档持久化**。存档的玩家数据是 `GameData.playerData`（`CharacterData`），`CharacterData` 内含 `characterStatusData` 字段。

**对「无限饥饿」的含义**：如果模组改的是 `characterStatusData.hunger`（且 3.3 转发假说成立），那么**改动会进入存档并跨读档保留** —— 【推断】，需 T6 验证。

### 5.2 证据链

【事实-程序集】可序列化性：

```csharp
[System.Serializable] public class CharacterStatusData : Il2CppSystem.Object
[System.Serializable] public class CharacterData        : Il2CppSystem.Object
```

【事实-程序集】存档载具：`GameData`（`Il2CppSystem.Object`）持有 `CharacterData playerData`：

```csharp
// GameData
public unsafe CharacterData playerData
```

【事实-程序集】角色存档独立于世界存档，有专用管理器：

```csharp
// CharacterDataManager : Il2CppSystem.Object
public unsafe static CharacterDataManager instance
public unsafe static string characterSuffix            // 静态：存档后缀
public unsafe StringBuilder characterSavePathSB        // 实例：存档路径拼接器
public unsafe void SaveCharacterData(CharacterData characterData)
public unsafe CharacterData LoadCharacterData(string characterId)
public unsafe CharacterData LoadLegacyTemplate(string characterId)   // 旧版兼容
public unsafe void DeleteCharacterData(string characterId)
public unsafe string GetCharacterSaveFilePath(string characterId)
public unsafe Il2CppStringArray GetCharacterIDAry()
public unsafe List<CharacterData> GetCharacterDatas()
```

这说明：**角色数据以 `characterId` 为键、独立读写**，且存在 `LoadLegacyTemplate` → 存在**存档版本迁移**，【推断】旧档字段会被补齐（这对「跨版本改值是否保留」有影响）。

【事实-程序集】运行期与存档的桥：

```csharp
// InGameController
public unsafe GameData gameData
// GameController
public unsafe GameData _gameData
public unsafe GameData gameData
```

【事实-程序集】`GameDataManager` 存在一整套异步存档协程（类型名可证）：

```
GameDataManager._SaveGameDataAsync_d__9
GameDataManager._SerializeGameDataAsync_d__10
InGameController._SaveCurrentGameDataCoroutine_d__51
InGameController._SaveWorldThenGameCoroutine_d__60
InGameController._ReturnToMainMenuWithSaveCoroutine_d__56
GameDataInitController._LoadCloudSaveCoroutine_d__16
GameDataInitController._BackupLocalSavesBeforeOverwrite_d__19
```

【推断】存档为**异步序列化 + 可能有云存档覆盖**。`_BackupLocalSavesBeforeOverwrite` 说明云同步会覆盖本地 —— **这是「改了被回滚」的一条现实风险路径**。

### 5.3 哪些字段持久化？

- 【事实-程序集】`CharacterData` 是 `[System.Serializable]`，`characterStatusData` 是它的**普通公开字段**（非 `[NonSerialized]` 标记可见）→ 【推断】随 `CharacterData` 一起序列化。
- 【事实-程序集】`CharacterStatusData` 的 16 个实例字段全部是裸 `public float`，无 `[NonSerialized]` 标注（反编译输出中未见）→ 【推断】全部持久化。
- 【未验证】实际存档文件的字段命中情况（未读取存档文件，且不允许改游戏目录）。**建议 Phase-2 直接读存档文本核对键名**（T7）。

### 5.4 模组改动的存续性判定

| 改动对象 | 是否进存档 | 跨读档保留 | 等级 |
|---|---|---|---|
| `characterData.characterStatusData.hunger` 等 | 是（同一对象） | **大概率是** | 【推断】 |
| `bcc.hunger`（属性 setter） | 取决于 setter 是否转发 | 转发生效则**是**，否则**否** | 【推断】 |
| `bcc.maxHP`（控制器字段） | 若不被拷入 `characterStatusData.maxhp` 则**否** | 可能**被读档覆盖** | 【未验证】 |
| `CharacterStatusData` 静态字段（如 `maxBloodVolume`） | **否**（静态，不参与实例序列化） | 仅当次进程有效 | 【推断】 |

**重要**：静态字段（`maxBloodVolume`、`deadBloodVolume`、`standardBodyTemperature`、`bodyTemperatureOffsetFloat`、`minBodyTemperature`、`maxBodyTemperature`）是**全局常量性质**，改它们不需要存续性考虑，但**会影响所有角色**（含 NPC）。风险见第 6 节。

### 5.5 「无限饥饿」到底该改哪
- 饿得慢 → 改速率侧：`GameConfigData.hungerThirstRateFloat`（全局难度，影响所有存档）或 `CharacterData.skillProperty_HungerRateFloat`（角色级）。
- 饥饿值不掉 → 每帧把 `characterStatusData.hunger` 顶回上限（需 T4 先确认权威侧）。
- **不要**只改 `bcc.hunger` 而不改 statusData，除非 T4 证明转发成立。

---

## 6. 完整字段技术记录表

> 访问方式统一前提：BepInEx + Il2CppInterop，`Assembly-CSharp.dll` 已由 interop 生成；表中 `→` 表示成员访问链。
> 修改方式均为**计划**，未执行。测试栏统一 `未实机验证`。

### 6.1 `CharacterStatusData`（数据模型，`[Serializable]`）

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
|---|---|---|---|---|---|---|
| `hp` | 字段 `float` | 当前生命值【推断】 | `playerCharacter.characterData.characterStatusData.hp` | 直接赋值 | 与控制器 `hp`（virtual，被 HCC 覆写）可能双份；可能被伤害/治疗逻辑立刻覆盖 | 未实机验证 |
| `maxhp` | 字段 `float` | 生命上限 | 同上 `.maxhp` | 直接赋值；或调 `characterData.SetMaxHP()` 重算 | 与控制器字段 `maxHP` 重复存储，可能被 `SetMaxHP()` 覆盖 | 未实机验证 |
| `energy` | 字段 `float` | 精力 | 同上 `.energy` | 直接赋值 | 与 `bcc.energy` 属性关系【未验证】；可能被 `CostEnergy`/Buff tick 覆盖 | 未实机验证 |
| `maxEnergy` | 字段 `float` | 精力上限 | `.maxEnergy` | 直接赋值 / `SetMaxEnergy()` | 同上 | 未实机验证 |
| `fatigue` | 字段 `float` | 疲劳 | `.fatigue` | 直接赋值 | 与 `bcc.fatigue` 关系【未验证】 | 未实机验证 |
| `maxFatigue` | 字段 `float` | 疲劳上限 | `.maxFatigue` | 直接赋值 / `SetMaxFatigue()` | 同上 | 未实机验证 |
| `hunger` | 字段 `float` | 饥饿（或饱食度，符号未定）【未验证】 | `.hunger` | 直接赋值；「无限饥饿」= 每帧顶回 `maxHunger` | 符号约定未定（官方文档中 `hunger` 食物字段为「饱食度变化」）；写反会导致加速饥饿 | 未实机验证 |
| `maxHunger` | 字段 `float` | 饥饿上限 | `.maxHunger` | 直接赋值 / `SetMaxHunger()` | 直接改上限不重算，UI 条比例可能失真 | 未实机验证 |
| `thirst` | 字段 `float` | 口渴 | `.thirst` | 同上 | 同 `hunger` | 未实机验证 |
| `maxThirst` | 字段 `float` | 口渴上限 | `.maxThirst` | 直接赋值 / `SetMaxThirst()` | 同上 | 未实机验证 |
| `sanity` | 字段 `float` | 理智 | `.sanity` | 直接赋值 | 与 `bcc.sanity` 关系【未验证】 | 未实机验证 |
| `maxSanity` | 字段 `float` | 理智上限 | `.maxSanity` | 直接赋值 | **无对应 `SetMaxSanity()` 方法**，只能裸写 | 未实机验证 |
| `casEnergy` | 字段 `float` | CAS（外骨骼/主动技能）能量 | `.casEnergy` | 直接赋值 | 与 `bcc.casEnergy` 关系【未验证】 | 未实机验证 |
| `bloodVolume` | 字段 `float` | 血量（血液体积） | `.bloodVolume` | 直接赋值 | **控制器上无对应成员**，是唯一存储；与 `hp` 的换算关系未知 | 未实机验证 |
| `radiationDose` | 字段 `float` | 辐射累积剂量 | `.radiationDose` | 直接赋值 | **控制器上无对应成员**；与 Buff/疾病系统联动未知 | 未实机验证 |
| `m_bodyTemperature` | 字段 `float` | 体温原始值 | `.m_bodyTemperature` | 优先改 `bodyTemperature` 属性 | 绕过属性可能丢失偏移/夹取逻辑 | 未实机验证 |
| `bodyTemperature` | 属性 `float`（get/set） | 体温（含偏移）【推断】 | `.bodyTemperature` | 用 setter | 【推断】叠加 `bodyTemperatureOffsetFloat` 与 min/max 夹取，直接写可能被夹取 | 未实机验证 |
| `maxBloodVolume` | **静态字段** `float` | 最大血容量 | `CharacterStatusData.maxBloodVolume` | 静态赋值 | **全局**，影响所有角色含 NPC；不持久化 | 未实机验证 |
| `deadBloodVolume` | **静态字段** `float` | 死亡判定血量阈值 | 同上 | 静态赋值 | 全局；改错会导致误判死亡/不死 | 未实机验证 |
| `standardBodyTemperature` | **静态字段** `float` | 标准体温 | 同上 | 静态赋值 | 全局；影响所有角色体温模型 | 未实机验证 |
| `bodyTemperatureOffsetFloat` | **静态字段** `float` | 体温偏移步长 | 同上 | 静态赋值 | 全局 | 未实机验证 |
| `minBodyTemperature` | **静态字段** `float` | 体温下限 | 同上 | 静态赋值 | 全局；影响低温伤害阈值 | 未实机验证 |
| `maxBodyTemperature` | **静态字段** `float` | 体温上限 | 同上 | 静态赋值 | 全局；影响高温伤害阈值 | 未实机验证 |
| *（无参构造）* | 方法 `void .ctor()` | 唯一方法，除此仅有 `bodyTemperature` 访问器 | — | 不建议自行 new | 自行 new 出的对象不挂到任何角色，改它无效 | 未实机验证 |

### 6.2 `BasicCharacterController`（运行期视图）

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
|---|---|---|---|---|---|---|
| `hp` | **属性** `float`（get/set，**virtual**，token 100667675） | 当前生命 | `playerCharacter.hp`（`HumanCharacterController` 覆写，走虚派发） | 用 setter | 覆写实现未知；可能与 `statusData.hp` 双份 | 未实机验证 |
| `FullHP` | **属性** `float`（**仅 get**，token 100667713） | 满血值【推断】 | `.FullHP` | **不能写**（无 setter） | 只读；【推断】可能是 `maxHP` 的派生 | 未实机验证 |
| `maxHP` | **字段** `float` | 生命上限（控制器侧） | `.maxHP` | 直接赋值 | **与 `statusData.maxhp` 重复**；哪一侧被用【未验证】 | 未实机验证 |
| `energy` | 属性 `float`（get/set，非 virtual，token 100667677） | 精力 | `.energy` | 用 setter | 【推断】转发到 `statusData.energy`；若否即为第二份存储 | 未实机验证 |
| `fatigue` | 属性 `float`（get/set，非 virtual） | 疲劳 | `.fatigue` | 用 setter | 同上 | 未实机验证 |
| `hunger` | 属性 `float`（get/set，非 virtual，token 100667683） | 饥饿 | `.hunger` | 用 setter | 同上；**无后背字段**（已核实） | 未实机验证 |
| `thirst` | 属性 `float`（get/set，非 virtual） | 口渴 | `.thirst` | 用 setter | 同上 | 未实机验证 |
| `sanity` | 属性 `float`（get/set，非 virtual） | 理智 | `.sanity` | 用 setter | 同上 | 未实机验证 |
| `casEnergy` | 属性 `float`（get/set，非 virtual） | CAS 能量 | `.casEnergy` | 用 setter | 同上 | 未实机验证 |
| `isPlayer` | **字段** `bool` | 是否本地玩家【推断】 | `.isPlayer` | 不建议改 | 改它可能破坏 AI 阵营/输入判定 | 未实机验证 |
| `characterData` | **字段** `CharacterData` | 数据模型根（通往 statusData 的唯一入口） | `.characterData` | 不应替换引用 | 替换会导致 UI/存档错乱 | 未实机验证 |
| `deadFlag` | **字段** `bool` | 死亡标志 | `.deadFlag` | 不建议改 | 可能导致双死/不死 | 未实机验证 |
| `HasEnergy(float)` | 方法 `bool`，**virtual** | 能量是否足够 | `.HasEnergy(x)` | 只调不改 | 语义（阈值比较方向）未验证 | 未实机验证 |
| `HasFatigue(float)` | 方法 `bool`，**virtual** | 疲劳是否足够 | `.HasFatigue(x)` | 只调不改 | 同上 | 未实机验证 |
| `CostEnergy(float)` | 方法 `bool`，**virtual** | 扣能量 | `.CostEnergy(x)` | 传负值可「加」能量？【未验证】 | 负值行为未验证，可能触发断言或反向 | 未实机验证 |
| `CostFatigue(float)` | 方法 `bool`，**virtual** | 扣疲劳 | `.CostFatigue(x)` | 同上 | 同上 | 未实机验证 |
| `CharacterTired()` | 方法 `void`，**virtual** | 触发「疲劳」状态 | `.CharacterTired()` | 调用 | 副作用未知（可能加 Buff/动画） | 未实机验证 |
| `MarkEnergyCostTime()` | 方法 `void` | 记录能量消耗时间点 | `.MarkEnergyCostTime()` | 一般不用 | 内部计时用途【推断】 | 未实机验证 |
| `ApplyBuffStatusTick(BuffAttr, float, float, float)` | 方法 `void` | Buff 按秒结算 | `.ApplyBuffStatusTick(...)` | **不建议手动调** | 参数为倍率，误用会瞬间结算大量数值 | 未实机验证 |
| `IsPlayerSideCharacter(BasicCharacterController)` | **静态方法** `bool` | 玩家阵营判定 | `BasicCharacterController.IsPlayerSideCharacter(c)` | 只读调用 | **不能**当「本地玩家」判据（可能含队友 NPC） | 未实机验证 |
| `IsSameSideForDamage(a, b)` | **静态方法** `bool` | 同阵营判定 | 同上 | 只读调用 | — | 未实机验证 |
| `IsHostileCamp(CharacterData)` | **静态方法** `bool` | 敌对阵营判定 | 同上 | 只读调用 | — | 未实机验证 |

### 6.3 `HumanCharacterController`（玩家具体类型，仅记录与状态无关的差异项）

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
|---|---|---|---|---|---|---|
| `hp` | **override 属性** `float`（第 10263 行） | 覆写基类生命值 | `playerCharacter.hp` | 用 setter | 覆写实现不可见，行为与基类不同【推断】 | 未实机验证 |
| `equipmentData` | 字段 `CharacterEquipmentData` | 装备数据 | `.equipmentData` | 走装备系统 | 与 `characterData.characterEquipmentData` 是否同一对象【未验证】 | 未实机验证 |
| `eatenFoodData` | 字段 `ItemData` | 最近进食数据【推断】 | `.eatenFoodData` | 不建议改 | 可能影响食物 Buff 结算 | 未实机验证 |
| `badStomachBool` / `foodAttr` | 字段 | 肠胃/食物属性【推断】 | `.badStomachBool` / `.foodAttr` | 不建议改 | 影响食物中毒判定 | 未实机验证 |
| `thirstWarningTimeFlag` / `hungerWarningTimeFlag`** | 字段 | 口渴/饥饿预警计时 | `.thirstWarningTimeFlag` | 无需改 | 仅 UI 预警【推断】 | 未实机验证 |

> ** 这两个字段属于 `HumanCharacterController`，名字含 `hunger`/`thirst` 但**不是数值**，检索时勿误认。

### 6.4 `CharacterData`（存档模型，最大值计算方）

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
|---|---|---|---|---|---|---|
| `characterStatusData` | 字段 `CharacterStatusData` | 状态数据根 | `playerCharacter.characterData.characterStatusData` | 不替换引用，只改其字段 | 替换引用会与存档/UI 脱钩 | 未实机验证 |
| `characterId` | 字段 `string` | 角色唯一 ID | `.characterId` | 只读 | 改它会指向别的存档文件 | 未实机验证 |
| `characterName` | 字段 `string` | 角色名 | `.characterName` | 可改 | 可能触发存档重写 | 未实机验证 |
| `RefreshStatus()` | 方法 `void` | 刷新状态【推断】 | `.RefreshStatus()` | 调用来让改动生效 | 可能重算并覆盖手改值 | 未实机验证 |
| `RefreshBodyPartsStatus()` | 方法 `void` | 刷新身体部位 | `.RefreshBodyPartsStatus()` | 调用 | 同上 | 未实机验证 |
| `SetMaxHP()` | 方法 `void`（无参） | 重算生命上限 | `.SetMaxHP()` | 调用后读 `maxhp` 校验 | **可能覆盖手改的 `maxhp`** | 未实机验证 |
| `SetMaxEnergy()` | 方法 `void`（无参） | 重算精力上限 | `.SetMaxEnergy()` | 同上 | 同上 | 未实机验证 |
| `SetMaxFatigue()` | 方法 `void`（无参） | 重算疲劳上限 | `.SetMaxFatigue()` | 同上 | 同上 | 未实机验证 |
| `SetMaxHunger()` | 方法 `void`（无参） | 重算饥饿上限 | `.SetMaxHunger()` | 同上 | 同上；**「无限饥饿」若靠改上限会被重算覆盖** | 未实机验证 |
| `SetMaxThirst()` | 方法 `void`（无参） | 重算口渴上限 | `.SetMaxThirst()` | 同上 | 同上 | 未实机验证 |
| `InitHumanCharacterData()` | 方法 `void` | 初始化人类角色数据 | `.InitHumanCharacterData()` | **不要调** | 可能重置整个角色数据 | 未实机验证 |
| `GetMaxHPBalanceFloat()` | 方法 `float` | 取平衡系数 | `.GetMaxHPBalanceFloat()` | 只读 | — | 未实机验证 |
| `GetMaxHPBalanceRebaseFloat()` | 方法 `float` | 取重定基平衡系数 | `.GetMaxHPBalanceRebaseFloat()` | 只读 | 与上者区别【未验证】 | 未实机验证 |
| `maxHPBalanceFloat` | 字段 `float` | 生命上限平衡系数 | `.maxHPBalanceFloat` | 改它 + 调 `SetMaxHP()` | 影响上限计算基准 | 未实机验证 |
| `appliedMaxHPBalanceFloat` | 字段 `float` | 已应用的平衡系数 | `.appliedMaxHPBalanceFloat` | **不要单独改** | 与 `maxHPBalanceFloat` 不一致会导致重算逻辑错乱 | 未实机验证 |
| `appliedMaxHPConstitution` | 字段 `int` | 已应用体质点 | 同上 | 不单独改 | 同上 | 未实机验证 |
| `appliedMaxHPSkillFloat` | 字段 `float` | 已应用技能加成 | 同上 | 不单独改 | 同上 | 未实机验证 |
| `skillProperty_MaxHPFloat` | 字段 `float` | 技能最大生命加成 | `.skillProperty_MaxHPFloat` | 改后调 `SetMaxHP()` | 被技能重算覆盖【推断】 | 未实机验证 |
| `skillProperty_HungerRateFloat` | 字段 `float` | 饥饿速率系数 | `.skillProperty_HungerRateFloat` | 改它（影响速率而非上限） | 被技能重算覆盖【推断】 | 未实机验证 |
| `skillProperty_ThirstRateFloat` | 字段 `float` | 口渴速率系数 | `.skillProperty_ThirstRateFloat` | 同上 | 同上 | 未实机验证 |
| `buffDataList` | 字段 `List<BuffData>` | 当前 Buff 实例列表 | `.buffDataList` | 走 Buff 系统 | 直接增删可能与 UI/存档不同步 | 未实机验证 |
| `deadFlag` | 字段 `bool` | 死亡标志（数据侧） | `.deadFlag` | 不建议改 | 与控制器 `deadFlag` 双份【未验证】 | 未实机验证 |

### 6.5 访问器与管理器

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
|---|---|---|---|---|---|---|
| `GameController.instance` | **静态属性** | 主控单例 | `GameController.instance` | 只读 | 主菜单可能为 null【推断】 | 未实机验证 |
| `GameController.playerCharacter` | 属性 `HumanCharacterController`（get/set，直读字段） | **本地玩家** | `GameController.instance.playerCharacter` | 不建议写 | 写入非法引用会崩；重生时被游戏改写 | 未实机验证 |
| `GameController.characterList` | 属性 `List<BasicCharacterController>` | 全部角色 | `.characterList` | 不建议直接改 | 并发修改异常；不走 `Add/RemoveCharacter` 会漏维护 | 未实机验证 |
| `GameController.AddCharacter(BasicCharacterController)` | 方法 `void` | 注册角色 | `.AddCharacter(c)` | — | 重复注册可能重复入列 | 未实机验证 |
| `GameController.RemoveCharacter(BasicCharacterController)` | 方法 `void` | 注销角色 | `.RemoveCharacter(c)` | — | 误删玩家 | 未实机验证 |
| `GameController.GetCharacterByGUID(string)` | 方法 `BasicCharacterController` | 按 GUID 取角色 | `.GetCharacterByGUID(guid)` | — | 返回基类需强转；GUID 不存在时行为未知 | 未实机验证 |
| `GameController._gameData` | 属性 `GameData`（直读字段） | 原始存档对象 | `.«_gameData»` | 只读推荐 | 与 `gameData` 不一致风险【未验证】 | 未实机验证 |
| `GameController.gameData` | 属性 `GameData`（方法调用） | 当前有效存档对象 | `.gameData` | 只读推荐 | 多人模式下语义不同【推断】 | 未实机验证 |
| `GameController.AddItemToPlayer(ItemData, bool)` | 方法 `bool` | 给玩家加物品 | `.AddItemToPlayer(item, true)` | 调用 | 背包满时行为由参数决定 | 未实机验证 |
| `GameController.TryAddItemToCharacter(BasicCharacterController, ItemData, bool, bool)` | 方法 `bool` | 给指定角色加物品 | `.TryAddItemToCharacter(c, item, true, true)` | 调用 | 同上 | 未实机验证 |
| `CharacterInput.instance` | **静态属性** | 输入单例 | `CharacterInput.instance` | 只读 | 场景切换后可能为 null【推断】 | 未实机验证 |
| `CharacterInput.characterController` | 属性 `HumanCharacterController` | 当前受控角色 | `CharacterInput.instance.characterController` | 不建议写 | 生命周期短于 `playerCharacter`【推断】 | 未实机验证 |
| `CharacterDataManager.instance` | **静态属性** | 角色存档管理器 | `CharacterDataManager.instance` | 只读 | — | 未实机验证 |
| `CharacterDataManager.SaveCharacterData(CharacterData)` | 方法 `void` | 写角色存档 | `.SaveCharacterData(cd)` | 调用可强制落盘 | 可能触发云同步覆盖；频率过高伤性能 | 未实机验证 |
| `CharacterDataManager.LoadCharacterData(string)` | 方法 `CharacterData` | 读角色存档 | `.LoadCharacterData(id)` | 只读 | 读回的对象是否与运行期同一实例【未验证】 | 未实机验证 |
| `CharacterDataManager.characterSuffix` | **静态字段** `string` | 存档文件后缀 | `CharacterDataManager.characterSuffix` | 只读 | — | 未实机验证 |
| `CharacterDataManager.GetCharacterSaveFilePath(string)` | 方法 `string` | 存档路径 | `.GetCharacterSaveFilePath(id)` | 只读 | 用于 Phase-2 定位存档文件 | 未实机验证 |
| `GameData.playerData` | 字段 `CharacterData` | 存档中的玩家数据 | `GameController.instance._gameData.playerData` | 只改其内部字段 | 若运行期用另一对象则改动不生效 | 未实机验证 |
| `InGameController.gameData` | 属性 `GameData` | 局内存档对象 | — | 只读 | 静态入口未确认【未验证】 | 未实机验证 |
| `GameConfigData.hungerThirstRateFloat` | 字段 `float` | 全局饥饿/口渴速率 | `…gameConfigData.hungerThirstRateFloat` | 直接赋值 | **全局**，影响所有角色；可能被新游戏配置重写 | 未实机验证 |
| `GameConfigData.difficulty` | 字段 `int` | 难度 | 同上 `.difficulty` | 直接赋值 | 可能不同步到已生成的平衡系数，需配 `SetMaxHP()` | 未实机验证 |
| `PlayerStatusManager.statustext` | 属性 `Text` | **仅 UI 文本** | `.statustext` | 只读 | **与数值无关**，勿用于读写状态 | 未实机验证 |

---

## 7. 无法确认的清单

以下项目**静态分析无法判定**，必须在 Phase-2 用运行时手段解决。切勿在文档或代码里把它们当已知事实。

### 7.1 行为层面（trampoline 不可读）

1. `BasicCharacterController.hunger/energy/thirst/fatigue/sanity/casEnergy` 的 getter/setter **是否真的转发**到 `characterData.characterStatusData` 同名字段。**这是全篇最关键的不确定点。**
2. `HumanCharacterController.hp` 的覆写实现做了什么（与基类区别）。
3. `CharacterData.SetMaxHP()/SetMaxEnergy()/SetMaxFatigue()/SetMaxHunger()/SetMaxThirst()` 的输入字段与写入目标（`statusData.max*` 还是 `bcc.maxHP`）。
4. `BasicCharacterController.FullHP` 的派生公式。
5. `CostEnergy(float)` / `CostFatigue(float)` 传入负值/超限值的行为。
6. `CharacterTired()` 的副作用。
7. `CharacterStatusData.bodyTemperature` 属性与 `m_bodyTemperature` 的换算与夹取逻辑。
8. `Physics/伤害` 结算读的是 `bcc.hp` 还是 `statusData.hp`。
9. `PlayerStatusPanel` 的数值来源方（控制器属性 or statusData）。

### 7.2 生命周期层面

10. `GameController.instance` 在主菜单/加载中是否非 null。
11. `GameController.playerCharacter` 在何时被赋值、何时被置 null；玩家重生是否换实例。
12. `CharacterInput.instance` 与 `characterController` 的存活期是否短于 `GameController`。
13. `characterList` 是否包含本地玩家。
14. 读档后 `GameController.playerCharacter.characterData` 是否就是 `GameData.playerData` **同一实例**（还是深拷贝）。

### 7.3 存档层面

15. 存档实际文件格式、字段键名、`characterSuffix` 的具体值。
16. `GameData` 未见 `[System.Serializable]` 标注，其序列化机制（Json.NET / 自定义 / 二进制）未知。
17. 云存档（`PlayFab`、`GameDataInitController._LoadCloudSaveCoroutine_d__16`）是否会覆盖本地改名/改值，以及 `_BackupLocalSavesBeforeOverwrite` 的覆盖策略。
18. `LoadLegacyTemplate` 的迁移逻辑是否会把新字段重置为默认值。
19. 手改 `characterStatusData` 后，需要显式 `SaveCharacterData()` 才落盘，还是退出时自动落盘。

### 7.4 领域定义层面

20. `hunger` 的**符号约定**：是「饥饿度」（越大越饿）还是「饱食度」（越大越饱）。官方文档中物品字段 `hunger` 标注为「饱食度变化」，而 Buff 17 名为「饥饿」且带 HP 惩罚 —— 二者语义可能相反，**未验证**。
21. `sanity` 是否真的被游戏逻辑使用（可能只是数据占位）。
22. `bloodVolume` 与 `hp` 的换算关系。
23. 静态字段（`maxBloodVolume` 等）是否在运行时被某处重新赋值（若被赋值，改它无效）。
24. `GameConfigData.hungerThirstRateFloat` 是速率**乘数**还是**倒数**。

### 7.5 覆盖范围层面

25. `CharacterStatusData` 是否**仅**被 `CharacterData` 引用。本次只在反编译的 17 个类型转储范围内核对（仅 `CharacterData.cs` 命中），**未做全程序集（4341 类型）扫描**。若其他类型直接持有它，入口不止一处。
26. `PlayerStatusManager` / `PlayerStatusPanel` 之外是否还有别的 UI 直接读状态数值。

---

## 8. Phase-2 必做测试清单（用于封闭第 7 节）

> 全部为**待执行**，本文不含任何实测结果。建议做成一个独立的 `DebugProbe` 插件，只读 + 单点写入，日志定级。

| 编号 | 目的 | 方法概要 | 判定标准 |
|---|---|---|---|
| **T1** | `GameController.instance` 何时可用 | 在主菜单、加载中、进游戏后分别打日志 | 记录各阶段是否为 null |
| **T2** | `playerCharacter` 何时可用 | 进游戏后逐帧记录 `instance`/`playerCharacter` 是否 null | 明确「可安全取玩家」的时机 |
| **T3** | 重生是否换实例 | 记录 `playerCharacter` 的 `Pointer`（`Il2CppObjectBase.Pointer`）与 `GetInstanceID()`；死亡→重生前后对比 | 指针变化即证实需「每次现取」 |
| **T4** | **转发假说（最关键）** | ① 读 `bcc.hunger` 与 `statusData.hunger` 比对；② 写 `statusData.hunger = X`，立刻读 `bcc.hunger` 是否 == X；③ 反向写 `bcc.hunger = Y`，读 `statusData.hunger` | 双向一致 ⇒ 单一存储（转发成立）；不一致 ⇒ 双份存储，必须两边都写 |
| **T5** | `maxHP` vs `maxhp` 谁生效 | 记 `bcc.maxHP`、`statusData.maxhp`、`bcc.FullHP`；改其中一个，看 UI 血条上限与受伤上限变化 | 确定唯一权威上限 |
| **T6** | 存档存续性 | 改 `statusData.hunger` → 存档 → 退到主菜单 → 读档 → 读回值；另测「不显式 Save 直接退出」 | 值是否保留 + 是否需显式保存 |
| **T7** | 存档字段实况 | 用 `GetCharacterSaveFilePath(id)` 定位文件，只读拷贝到临时目录后检查键名 | 确认 `characterStatusData` 是否真实落盘、键名拼写 |
| **T8** | 饥饿符号 | 记录 `hunger` 在进食/时间流逝后的变化方向 | 确定 0 与 max 各代表什么 |
| **T9** | 云存档覆盖 | 本地改值 → 触发云同步 → 复查 | 是否被回滚 |
| **T10** | 静态字段是否被重写 | 记录 `maxBloodVolume` 等在各阶段的取值 | 若变化则不可改 |
| **T11** | 衰减写入方 | 挂 `ApplyBuffStatusTick` 前后读两侧数值 | 确认 tick 改哪一侧 |

---

## 9. 给模组实现的阶段性建议

以下为**基于本文证据的计划**，未执行、未验证。

1. **取玩家**：统一走 `GetLocalPlayer()`（第 2.4 节模式），**不缓存**，每次现取 + 判空；`GameController.instance.playerCharacter` 为主，`CharacterInput.instance.characterController` 为备，`characterList` 扫描为兜底。
2. **先做 T4 再写任何改值代码**。在 T4 出结论前，任何「改一个就能生效」的假设都可能是错的。
3. **改值策略（保守）**：优先写 `characterData.characterStatusData.*`（唯一确认存在、唯一确认持久化的存储），并在 T4 证明属性不转发时**同时**写控制器属性。
4. **改上限**：不要裸改 `maxhp`。改 `maxHPBalanceFloat` / `skillProperty_MaxHPFloat` 后调用 `CharacterData.SetMaxHP()`，让游戏自己把结果写到它认为正确的地方；T5 之后再决定是否需要补写 `bcc.maxHP`。
5. **不要**碰 `applied*` 系列字段、`InitHumanCharacterData()`、`deadFlag`、`isPlayer`。
6. **不要**碰 `CharacterStatusData` 的静态字段（全局影响 NPC）。
7. **避免每帧写**：用「低于阈值才顶回」的方式，减少与游戏自身 tick 的写入竞争（谁后写谁赢，顺序不可控）。
8. **存档**：优先依赖游戏自身的保存流程；若非必要不主动 `SaveCharacterData()`，以免与云同步（T9）冲突。

---

## 10. 关键结论速查

| 问题 | 答案 | 等级 |
|---|---|---|
| 玩家类型 | `HumanCharacterController`（`BasicCharacterController` 的派生类） | 【事实-程序集】 |
| `isPlayer` 在哪 | `BasicCharacterController` 的 `bool` **实例字段**，带 setter | 【事实-程序集】 |
| 本地玩家最稳入口 | `GameController.instance.playerCharacter` | 【事实-程序集】+【推断】可靠性 |
| 备选入口 | `CharacterInput.instance.characterController` | 【事实-程序集】 |
| 状态数据在哪 | `playerCharacter.characterData.characterStatusData` | 【事实-程序集】 |
| 控制器是否直接持有 statusData | **否** | 【事实-程序集】 |
| 谁是权威存储 | 大概率是 `characterStatusData`，控制器属性为转发 | 【推断】（证据强，T4 待验） |
| 有无显式同步方法 | **无**（BCC 上不存在 `Sync*/SaveTo*/LoadFrom*` 状态方法） | 【事实-程序集】 |
| 最大值来源 | `CharacterData` 由 `maxHPBalanceFloat`+体质+技能计算，经 `SetMax*()` 写入 | 【推断】 |
| 难度是否影响最大值 | `GameConfigData.difficulty` 存在；是否影响上限【未验证】 | 【未验证】 |
| 是否持久化 | 是，`CharacterData`/`CharacterStatusData` 均 `[System.Serializable]`，经 `CharacterDataManager` 落盘 | 【事实-程序集】+【推断】 |
| 改值能否跨读档 | 改 `statusData` 大概率能；改 `bcc.maxHP` 存疑 | 【推断】/【未验证】 |
| `PlayerStatusManager` | 纯 UI `Text` 持有者，**与数值无关** | 【事实-程序集】 |

---

*本文档仅为 Phase-1 静态分析产物。游戏目录未被修改，游戏未被运行，所有运行时行为均未验证。*
