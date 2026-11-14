# ZED ZONE 生命值 / 伤害 / 死亡系统静态研究报告

> Phase-1 静态调查（只读游戏安装，未运行游戏，未写任何 Mod 功能代码）
>
> 目标：为「生存辅助 Mod」的**生命保护**与**无敌**两类功能确定唯一可靠的技术锚点，
> 并明确哪些结论只是推断、必须实机验证。

---

## 0. 研究方法与证据强度（必读）

### 0.1 唯一的证据来源

| 项目 | 值 |
| --- | --- |
| 游戏 | `<游戏目录>`（只读） |
| 引擎 | Unity 2023.1.18f1 / IL2CPP x64 |
| 分析对象 | `BepInEx\interop\Assembly-CSharp.dll`（Il2CppInterop 生成的 **managed 桩程序集**） |
| 反编译工具 | `ilspycmd`（`ilspycmd.exe`） |
| 类型清单 | `<仓库目录>\ZED-ZONE-FPS-Optimization-Mod\research\type-list.txt` |

生成全量反编译（供复核，约 156 万行）：

```powershell
$env:DOTNET_ROOT="<dotnet>"; $env:DOTNET_ROLL_FORWARD="LatestMajor"
& "ilspycmd.exe" `
  "<游戏目录>\BepInEx\interop\Assembly-CSharp.dll" `
  -o <输出目录>
```

### 0.2 证据等级约定（全文严格遵守）

| 标记 | 含义 |
| --- | --- |
| 【事实-程序集】 | 由 interop 桩程序集直接读到：类型、字段、方法、签名、访问修饰符、虚函数槽位 |
| 【事实-官方文档】 | 游戏官方 datamine/文档（本次仅用于旁证 BUFF 数值，见 §5.4） |
| 【推断】 | 由结构（命名、签名、参数名、虚函数槽位差异）推导，未经运行时验证 |
| 【未验证】 | 完全无法从静态信息确认，**必须实机验证** |

### 0.3 three critical caveats（直接影响结论可靠性）

1. **interop 是桩，不是实现。** 每个方法体都是 `IL2CPP.il2cpp_runtime_invoke(...)` 蹦床。
   因此 `存在性 / 签名 / 类型 / 虚槽位` 是**事实**，`内部逻辑 / 调用关系 / 副作用` 是**【推断】**。
2. **`[CallerCount]` 不是调用次数。** 它是引用表区间的长度，**禁止**用来推断调用者数量。
   本报告完全未使用它作为调用证据。
3. **interop 把一切成员都渲染成 `public`，这不代表原始可访问性。**
   真正的访问修饰符藏在元数据指针的**命名**里，例如：

   ```
   NativeMethodInfoPtr_OnHit_Public_Virtual_New_Boolean_...        → Public, virtual(newslot)
   NativeMethodInfoPtr_ApplyBuffStatusTick_Private_Void_BuffAttr_... → Private, 非虚
   NativeMethodInfoPtr_CharacterDead_Protected_Virtual_New_Void_... → Protected, virtual(newslot)
   NativeMethodInfoPtr_set_hp_Public_Virtual_New_set_Void_Single_0  → Public virtual setter
   ```

   本报告的访问修饰符**全部**来自该命名，而非 C# 关键字。
   （`_Virtual_New_` = 新槽位声明；`_Virtual_` 无 `New` = 覆写同一槽位。）

4. **无法获得任何调用图。** 桩程序集没有任何 IL 体，所以"谁调用了 `OnHit`"在静态层面**不可知**。
   本报告因此把**所有**调用关系标为【推断】或【未验证】。
   唯一能补充调用图的手段是实机 Harmony `Prefix/Postfix` 打日志（见 §7）。

---

## 1. 生命值的真实存放位置

### 1.1 结论（TL;DR）

> **权威存储**是 `CharacterData.characterStatusData.hp`（`CharacterStatusData.hp`，纯字段）。
> **权威访问口**是虚属性 `BasicCharacterController.hp`，玩家侧被 `HumanCharacterController.hp` **覆写**。
> 二者几乎确定是同一块内存的两个视图（【推断】），但对**玩家**而言，
> 直接写 `characterStatusData.hp` 会绕过 `HumanCharacterController` 覆写里的逻辑（UI / 联机 / 死亡判定），
> 因此 **Mod 必须走 `hp` 属性，禁止直接写 `characterStatusData.hp`**。

### 1.2 完整对象链【事实-程序集】

```
BasicCharacterController
  └─ field: characterData                     // CharacterData
        └─ field: characterStatusData          // CharacterStatusData
              ├─ field: hp                     // float  ← 真正的血量存储
              ├─ field: maxhp                  // float
              ├─ field: energy / fatigue / hunger / thirst / sanity / casEnergy
              ├─ field: bloodVolume            // float  （失血）
              ├─ field: m_bodyTemperature      // float  （体温，背后字段）
              ├─ prop : bodyTemperature        // float  真实 get/set 方法（token 100671692/100671693）
              ├─ field: radiationDose          // float  （辐射）
              └─ static: maxBloodVolume / deadBloodVolume / standardBodyTemperature
                         bodyTemperatureOffsetFloat / minBodyTemperature / maxBodyTemperature
```

`CharacterStatusData` 的字段声明形态【事实-程序集】：`hp`、`maxhp`、`bloodVolume`、`radiationDose`、
`m_bodyTemperature` 全部是 **`il2cpp_field_get_offset` + 裸指针读写**的**字段**；
只有 `bodyTemperature` 是**带真实方法体**的属性（get/set 走 `il2cpp_runtime_invoke`，不是字段偏移）。

### 1.3 `hp` 是虚属性，不是字段【事实-程序集】

| 类型 | 声明 | 元数据指针 | 槽位含义 |
| --- | --- | --- | --- |
| `BasicCharacterController` | `public unsafe virtual float hp` | `get_hp_Public_Virtual_New_get_Single_0`（token 100667675）<br>`set_hp_Public_Virtual_New_set_Void_Single_0`（token 100667676） | 新槽位（声明） |
| `HumanCharacterController` | `public unsafe override float hp` | `get_hp_Public_Virtual_get_Single_0`（token 100670066）<br>`set_hp_Public_Virtual_set_Void_Single_0`（token 100670067） | 覆写（玩家用这个） |
| `FinalBossController` | `public unsafe override float hp` | token 100672363 / 100672364 | 覆写 |

关键推论（【推断】，但结构证据很强）：

- `hp` 的 getter / setter 都是**真实方法**（有 method token、走虚分派），**不是**内联字段访问。
  → 说明 getter/setter 里有逻辑（极可能包含 `characterStatusData.hp` 转发、UI 刷新、联机同步、致死判定）。
- 同类型的 `energy` 属性是 **`Public` 非虚**、`hunger/thirst/sanity` 也是 **`Public` 非虚**，
  唯独 `hp` 被做成 **virtual(new slot)** 并在 `HumanCharacterController` 覆写。
  → **玩家血量有独立实现**，这是刻意的，不是巧合。
- `maxHP` 是 `BasicCharacterController` 的**普通字段**（`NativeFieldInfoPtr_maxHP`），
  而 `FullHP` 是 **`Public` 只读、非虚的真实方法**（token 100667713）。
  → `FullHP` 是"计算出来的满血"（大概率 `maxHP * GetDifficultyHPFloat() * IslandBossHpScale`，【推断】）。
  相关存在：`difficultyFullHP` 字段、`GetDifficultyHPFloat()`、`GetMonsterHPBalanceFloat()`、
  `ApplyIslandBossHpScale(float)`、`RestampIslandBossHpScaleIfPristine(float)`、`StampIslandBossHpScale(float)`。

### 1.4 `CharacterStatusData` 是"活数据"还是"存档 DTO"？

这一条决定"直接写 `characterStatusData.hp` 有没有用"。

- 【事实-程序集】全程序集范围内，`CharacterStatusData` 这个类型名只出现在 4 处：
  `CharacterData.characterStatusData` 字段、`CharacterData` 自己的构造、
  `CharacterStatusData` 自身成员、以及 **`NPCAffinity.IsStarving(CharacterStatusData status)`**。
- 【推断】`NPCAffinity.IsStarving` 是 NPC 日常 AI 判定（饥饿），说明 `CharacterStatusData`
  在**运行时被实时读取**，不是只在存读档时刷新的 DTO。
  → 因此 `characterStatusData.hp` 极可能与 `hp` 属性**实时同源**。
- 【未验证】但"同源"只有实机能证明。**必须做 §7 的 T1 实验**：
  运行时同时打印 `player.hp` 与 `player.characterData.characterStatusData.hp`，
  再 `hp` setter 写一个值看后者是否同步变化。

### 1.5 玩家对象的获取路径【事实-程序集】

| 路径 | 类型 | 形态 |
| --- | --- | --- |
| `GameController.instance.playerCharacter` | `HumanCharacterController` | 静态 `instance` 字段 + 实例字段（**字段**，可能为 null） |
| `CharacterInput.instance.characterController` | `HumanCharacterController` | 静态 `instance` 字段 + 实例字段（**字段**，可能为 null） |

两条路径都必须在 `Update` 里做 **null 检查**（读档、死亡重生、切场景期间会短暂为 null）。

### 1.6 必须测试的内容

| 编号 | 测试 | 通过判据 |
| --- | --- | --- |
| T1 | 同时读 `hp` 属性与 `characterStatusData.hp` | 二者恒定相等 → 同源；仅存档后变化 → DTO |
| T2 | 经 `hp` setter 写入后读 `characterStatusData.hp` | 立即同步 → 同一存储 |
| T3 | 直接写 `characterStatusData.hp` 后读 `hp` | 立即同步 → 可绕过 UI/联机逻辑（危险） |
| T4 | `FullHP` 与实际满血上限 | 与 HUD 显示的上限一致 |

---

## 2. 伤害的真实入口

### 2.1 先给负面结论（很重要，能避免后续白干）

**不存在**面向角色的以下方法（全程序集检索，无匹配）【事实-程序集】：

- `TakeDamage(...)` — 不存在
- `Hurt(...)` — 不存在
- `ReceiveDamage(...)` — 仅 `ZedZone.HordeMode` 里针对 `MsgHordePlayerDamage` 的网络入口，非通用伤害
- `DealDamage(...)` — 仅存在于 `GOAPAgent` 系列（AI 攻击发起侧），签名 `(BasicCharacterController c, float damage, Vector2 force[, bool tick])`
- `ApplyDamage(...)` — 仅 `ApplyDamage(BuildingFloor floor, M_BuildingTileData wallTile, float damage, Vector2 force, Vector2 hitPoint, int damageType, BasicCharacterController attacker)`，**建筑**伤害，与角色无关

→ 所以"找一个 `TakeDamage` 打补丁"这条路**不存在**。

### 2.2 候选入口清单（按"单一漏斗可能性"排序）

#### 候选 1：`CharacterHitContext.Apply(...)` —— 最像唯一分发器【事实-程序集】

```csharp
public static bool Apply(
    BasicCharacterController target,
    bool  direct,               // ← 直击 / 非直击（溅射、爆炸、环境）
    float damage,
    Vector2 point,
    Vector2 force,
    CharacterBodyPart part = null,
    float deathMultiplier = 1f,
    bool  notifyAI = true,
    bool  showPanel = true,
    bool  animation = true,
    bool  causeDeath = true,
    int   penetration = 0)      // ← 护甲穿透
```

- 访问性：`Public Static`，**静态方法**，非虚。适用 Harmony `[HarmonyPatch(typeof(CharacterHitContext), nameof(Apply))]`。
- 同类静态字段：`directional`、`lastBlocked`、`armorPenetration`、`vehicleKnockdown`、`vehicleRunOver`
  （`CharacterHitContext` 是一个 `static class`）。
- 为什么它最可能唯一【推断】：
  1. 它是**静态**方法 + 显式 `target` 参数 → 典型"全局伤害分发入口"。
  2. 参数表与 `OnDirectedHit` **几乎逐项对应**（damage / point / force / part / deathMultiplier /
     notifyAI / showPanel / animation / causeDeath / penetration），**多出一个 `direct`**。
     → `direct` 很可能就是"调用 `OnDirectedHit` 还是走非直击路线"的开关。
  3. `bool` 返回值 + `lastBlocked` 静态字段 → 返回值大概率表示"是否被格挡/是否生效"。
  4. 联机协议 `ZedZone.Net.MsgDamageEvent` 的字段 `{directional, damage, hitX, hitY, forceX, forceY}`
     与 `Apply(target, direct, damage, point, force, ...)` 的参数**结构完全一致**（【推断】：收包端调用 `Apply`）。
- 风险：**静态方法 + 静态状态**，多线程/重入语义未知；`lastBlocked` 若被 Mod 弄脏会影响格挡判定。

#### 候选 2：`BasicCharacterController.OnDirectedHit(...)` —— "定向直击"实现【事实-程序集】

```csharp
public bool OnDirectedHit(float damage, Vector2 point, Vector2 force,
    CharacterBodyPart part = null, float deathMultiplier = 1f, bool notifyAI = true,
    bool showPanel = true, bool animation = true, bool causeDeath = true,
    int armorPenetration = 0)
```

- 访问性：**`Public`、非虚**（`OnDirectedHit_Public_Boolean_...`）。
- 与 `OnHit` 的关系【推断】：`OnDirectedHit` 带 `armorPenetration`，`OnHit` 不带；
  极可能 `OnDirectedHit` = 先做护甲结算 → 再转 `OnHit`。
- **非虚**是好消息也是坏消息：好消息是只有一个实现（不会被覆写绕过）；
  坏消息是它只在"定向直击"路径上，**非直击伤害（爆炸/溅射/环境/DoT）可能不进它**。

#### 候选 3：`BasicCharacterController.OnHit(...)` / `HumanCharacterController.OnHit(...)` —— 玩家真正要打的那个【事实-程序集】

```csharp
public virtual bool OnHit(
    float m_damage, Vector2 hitPoint, Vector2 m_force,
    CharacterBodyPart m_characterBodyPart = null,
    float m_deathMultiplier     = 1f,
    bool  notifyAI             = true,
    bool  showPlayerOnHitPanel = true,   // ← 受击 UI（屏幕红闪）
    bool  m_hitAnimationFlag   = true,   // ← 受击动画
    bool  hitCauseDeathFlag    = true)   // ← 本次伤害是否允许致死
```

**这是本报告最重要的一个技术细节：**

| 类型 | 声明 | 元数据指针 | 结论 |
| --- | --- | --- | --- |
| `BasicCharacterController` | `public unsafe virtual bool OnHit(...)` | `OnHit_Public_Virtual_New_...`（token 100667890） | 基类声明（新槽） |
| `HumanCharacterController` | `public unsafe override bool OnHit(...)` | `OnHit_Public_Virtual_...`（token 100670195） | **覆写** |
| `HumanCharacterController_ExoBoss` | `override` | — | 覆写 |
| `ZombieController` | `override` | — | 覆写 |
| `CarrionCharacterController` | `override` | — | 覆写 |
| `PetCharacterController` | `override` | — | 覆写 |
| `FinalBossController`、`MechController` 等 | `override` | — | 覆写（共 21 处 `OnHit` 覆写） |

> **⚠ 关键陷阱：Harmony 补丁打在 `BasicCharacterController.OnHit` 上，对玩家几乎一定无效。**
> 因为 `HumanCharacterController` **覆写了** `OnHit`，虚分派会直接进覆写，基类补丁不被执行。
> **玩家无敌必须把 `HumanCharacterController.OnHit` 作为补丁目标**（而不是基类）。
> 若要"对所有角色生效"，必须逐个覆写类型打补丁，或改打 `CharacterHitContext.Apply`。

玩家专属成员在子类：`GameController.instance.playerCharacter` 是 `HumanCharacterController`。

#### 候选 4：`OnHitByVehicle` / `OnHitByCharacter`【事实-程序集】

| 方法 | 声明 | 覆写者 |
| --- | --- | --- |
| `OnHitByVehicle(float m_damage, Vector2 hitPoint, Vector2 m_force)` → `bool` | `Public Virtual` `New`（Basic） | `HumanCharacterController`、`ZombieController` |
| `OnHitByCharacter(BasicCharacterController offenseCharacter, float damage, Vector2 force)` → `void` | `Public Virtual` `New`（Basic） | `HumanCharacterController` + 至少 8 个其它类型 |

- 命名语义【推断】：`OnHitByCharacter` 的语义更像**"被谁打的"通知/回调**（参数是攻击者），
  §5.1 的 `onHitCallBack` 也是这种形态。**不能据此认为它是伤害应用入口。**
- `OnHitByVehicle` 带 `damage/point/force` 且返回 `bool`，更像真正的车碾压伤害入口。

#### 候选 5：`CharacterBodyPart.OnHit(float, ref Vector2, Vector2, bool, bool, bool)`【事实-程序集】

- `Public Virtual New`，`void` 返回。这是**单部位**受击处理（贴图、护甲、血洞）。
- 同类型相关：`DamageArmor(bool, float)`（Public）、`RefreshArmorDamageFloat()`（Private）、
  字段 `damageFloat` / `defenseFloat` / `armorBaseDamageFloat` / `armorOverrides`。
- 用途【推断】：部位伤害系数与护甲结算的落点；**不是**全局血量入口。

#### 候选 6：DoT / 持续伤害路径（见 §5 详解）

- `BasicCharacterController.ApplyBuffStatusTick(BuffAttr buffAttr, float debuffScale, float buffPowerScale, float burningDamageScale)` — **`Private`、非虚**
- `BasicCharacterController.ExecuteBuffs()` — `Protected Virtual New`（Human 覆写）
- `BasicCharacterController.UpdateStatus()` — `Protected Virtual New`（Human 覆写）
- `BasicCharacterController.OnTimeUpdate(float m_addTimeFloat)` — `Public Virtual New`（Human 覆写）
- `BasicCharacterController.TickPoisonSmoke(float seconds, bool acidImmune)` — `Public`、非虚
- `HumanCharacterController.LossingBlood(float lossingBloodVolume)` — `Public`、非虚
- `HumanCharacterController.StopAllBleeding()` → `bool` — `Public`、非虚
- `HumanCharacterController.HealAllBodyPartInjuries()` — `Public`、非虚

### 2.3 `OnHitCallBack` 委托的真实性质（澄清一个容易踩的坑）

【事实-程序集】

```csharp
public sealed class OnHitCallBack : Il2CppSystem.MulticastDelegate
{
    public unsafe virtual void Invoke(float damage, Vector2 hitPoint, Vector2 force);
}
// BasicCharacterController 上的字段（interop 渲染为属性）：
public unsafe OnHitCallBack onHitCallBack { get; set; }
```

- 它是一个**通知型多播委托**（`On*CallBack` 命名 + `MulticastDelegate` + 内置 `+` / `-` 运算符），
  由 `BasicCharacterController` 上一个可直接赋值的字段承载。
- **【推断】它不是伤害入口，而是"受击后广播"**（典型的 `onHitCallBack?.Invoke(dmg, point, force)`）。
  把伤害归零补丁打在 `Invoke` 上是没意义的（`Invoke` 是委托自身的调用蹦床，且只在被调用时才发生）。
- **正确用法是"观测点"**：`player.onHitCallBack += (Action<float,Vector2,Vector2>)((d,p,f)=>log(d,p,f))`
  → 用于 §7 的实验，实测"哪些伤害会经过 OnHit"。
  注意：若游戏自己给该字段赋过值，直接覆盖会**吞掉**原逻辑，必须用 `+`（Delegate.Combine）而非 `=`。

### 2.4 伤害入口小结表

| # | 入口 | 是否虚 | 玩家是否经过 | 单一漏斗可能性 | 打补丁价值 |
| --- | --- | --- | --- | --- | --- |
| 1 | `CharacterHitContext.Apply`（static） | 否 | 【未验证】 | **高** | ★★★★★ |
| 2 | `BasicCharacterController.OnDirectedHit` | 否 | 【未验证】 | 中 | ★★★★ |
| 3 | `HumanCharacterController.OnHit`（override） | 是（覆写） | **确定会**（基类 API） | 中 | ★★★★★（玩家） |
| 3b | `BasicCharacterController.OnHit`（基类） | 是（新槽） | 玩家**不经过** | — | ★（对玩家无效） |
| 4 | `OnHitByVehicle` / `OnHitByCharacter` | 是 | 【未验证】 | 低 | ★★ |
| 5 | `CharacterBodyPart.OnHit` | 是 | 【未验证】 | 低 | ★ |
| 6 | `ApplyBuffStatusTick`（Private） | 否 | DoT 专用 | — | ★★★★（DoT 无敌） |

---

## 3. 死亡流程

### 3.1 死亡相关成员全表【事实-程序集】

| 名称 | 签名 / 形态 | 访问性 | 备注 |
| --- | --- | --- | --- |
| `CharacterDead` | `void CharacterDead(Vector2 hitPoint, Vector2 force, bool ignoreDeadFlag = false)` | `Protected Virtual`（Basic 新槽 token 100667910；**Human 覆写** token 100670526） | **真正的死亡执行点** |
| `CharacterDeadCoroutine` | `IEnumerator CharacterDeadCoroutine(float waitTime, Vector2 hitPoint, Vector2 force)` | `Protected`、非虚（token 100667894） | 死亡延迟/演出协程 |
| `RunDeathChain` | `void RunDeathChain(Vector2 hitPoint, Vector2 force)` | `Private`、非虚（token 100667917） | 死亡链启动器【推断】 |
| `RaiseDeathCallbacks` | `void RaiseDeathCallbacks()` | `Protected`、非虚（token 100667909） | **死亡事件派发** |
| `OnDeathChainException` | `void OnDeathChainException(Exception e)` | `Protected`、非虚 | 死亡链异常保护 |
| `PrepareBodyPartsForDeathSplit` | `void PrepareBodyPartsForDeathSplit()` | `Protected Virtual`（Basic 新槽；Human 覆写） | 尸体肢体拆分 |
| `OnPlayerDeathChainAborted` | `void OnPlayerDeathChainAborted()` | `Protected Virtual`（Human 覆写） | **死亡链被中止时回调**（极重要） |
| `HealLingeringDeadInstance` | `void HealLingeringDeadInstance(string reason)` | `Private`（token 100667916） | 把"滞留尸体实例"复活/清除 |
| `TryHealLingeringDeadInstance` | `void TryHealLingeringDeadInstance()` | `Private`（token 100667915） | 同上 |
| `IsLingeringDeadInstanceCandidate` | `bool IsLingeringDeadInstanceCandidate()` | `Public`、非虚 | 判定是否滞留尸体 |
| `deadFlag` | `bool deadFlag { get; set; }` | `Public`、**真实 get/set 方法**（token 100667895/100667896） | **属性，带逻辑** |
| `netDeadFlag` | `bool` 字段 | `Public` 字段 | 联机死亡标记 |
| `deadLingerTicks` | `int` 字段 | `Public` 字段 | 尸体滞留计时 |
| `IsCataclysmInfected` | `bool IsCataclysmInfected()` | `Public Virtual` | 灾变感染 |
| `DeathPanel.OpenPanel()` | `void OpenPanel()` | `Public`（单例 `DeathPanel.instance`） | **玩家死亡 UI / 读档界面** |
| `MsgPlayerDeath` | `{playerId, posX, posY, zLevel, corpseConfigStr, corpseItemsBytes, standVz}` | 网络消息 | 死亡广播 + 尸体配置 |
| `CarrionController.OnDeathBegin` | `Il2CppSystem.Action` 事件（`add_`/`remove_`） | `Public` | 唯一的"死亡事件"订阅口（仅 Carrion） |
| `CataclysmRunStats.ReasonDeath` | `static int` | `Public` | 死亡原因统计 |

### 3.2 三个死亡标记 → **不同步（desync）风险确切存在**
【事实-程序集】同时存在三个独立的"死亡"位：

| 位置 | 形态 | 生命周期 |
| --- | --- | --- |
| `BasicCharacterController.deadFlag` | **属性**（有真实 getter/setter，token 100667895/100667896） | 运行时 |
| `BasicCharacterController.netDeadFlag` | **字段** | 联机运行时 |
| `CharacterData.deadFlag` | **字段**（`NativeFieldInfoPtr_deadFlag`，在 `CharacterData` 上） | **存档持久化** |

→ **结论（【推断】，结构证据强）**：这是本系统最大的隐患。
只改其中一个（尤其只改 `CharacterData.deadFlag` 或只改 `netDeadFlag`）会造成
"血量是满的但读档后还是死的"或"本地活着但主机认为你死了"。

`CharacterDead(..., bool ignoreDeadFlag = false)` 的 `ignoreDeadFlag` 参数**反证**了
`CharacterDead` 内部会检查 `deadFlag` 做幂等（否则不需要这个跳过参数）。

### 3.3 死亡是否由 `hp <= 0` 触发？还是由 `bloodVolume`？

- 【事实-程序集】`CharacterStatusData.deadBloodVolume` 是**静态 float**（`il2cpp_field_static_get_value`），
  与 `maxBloodVolume`、`standardBodyTemperature`、`bodyTemperatureOffsetFloat` 同族，都是**全局常量**。
- 【推断】`deadBloodVolume` 是"死亡时把 `bloodVolume` 归到的展示值/常量"，**不是**"血量低于它就死"的阈值
  —— 因为是 `static`，一个静态常量不可能做"每个角色各自的死亡阈值"。
  但**不能排除**它被当作全局阈值使用。**【未验证】**。
- 【推断】死亡主判据是 `hp`（`OnHit` 内依据 `m_damage` 与 `hitCauseDeathFlag` 判定 → 调 `CharacterDead`），
  旁证：`lastLethalDamage` 字段专门记录"致死的那一下"；`m_deathMultiplier`（处决倍率）参数；
  `hitCauseDeathFlag`（"本次是否允许致死"）参数。
- 【未验证】`bloodVolume`（失血）是否**独立**能致死。这是"把 hp 钳到 1 会不会还是死"的决定性问题。
  见 §7 的 **T5**。

### 3.4 "把 hp 限制在 ≥ 1" 是否足以阻止死亡？

**结论：单独使用不足够。**【推断 + 未验证】

理由（逐条）：

1. **致死判定可能在扣血之前**：若 `OnHit` 先判 `m_damage >= hp` → 直接 `CharacterDead`，
   那么同一次调用里"把 hp 钳到 1"发生在扣血之后，**救不回来**（除非补丁在 Prefix 阶段改 `m_damage`）。
2. **`hitCauseDeathFlag`** 说明存在"与血量无关的致死"（处决、灾变、脚本击杀）。
3. **DoT 旁路**（§5）：Buff 的 `hpPerSecond` 走 `ApplyBuffStatusTick`，可能不经 `OnHit`；
   `BuffAttr.causeDeathFlag` 甚至允许"某个 Buff 直接致死"。
4. **`ignoreDeadFlag` 路径**：`CharacterDead(..., ignoreDeadFlag: true)` 可绕过死亡标记，
   意味着存在"强制死亡"的调用方。
5. **失血致死**（§3.3 的 T5 若成立）与 hp 无直接关系。

→ **必须用"伤害入口拦截"而不是"血量钳制"来保证不死。**
血量钳制只能作为**第二层保险**（见 §4 的组合方案）。

---

## 4. 无敌 / 无限生命的可行实现（含排序与硬约束分析）

### 4.1 先看游戏自己有没有开关（最优先，先查再自己造）

【事实-程序集】**游戏内建了无敌/上帝模式配置**：

| 成员 | 声明 | 位置 |
| --- | --- | --- |
| `GameConfigs.playerInvincible` | `public unsafe static bool` | L85351（`GameConfigs`，L83109） |
| `GameConfigs.playerGodMode` | `public unsafe static bool` | L85365 |
| `GameConfigs.testMode` | `public unsafe static bool` | L85308 |
| `GameConfigs.enableTestCommands` | `public unsafe static bool` | L86125 |
| `GameConfigs.downedDamageImmuneSeconds` | `public unsafe static float` | L86195 |
| `BasicCharacterController.godModeExcludeFlag` | `public unsafe bool` 字段 | L168638 |
| `BasicCharacterController.SetGodModeCollisionExclusion(bool)` | `public unsafe void` | L174708 |

- **`GameConfigs.playerInvincible` 与 `playerGodMode` 是静态可写 bool 字段**
  → Mod 里一行赋值即可切换，**零补丁、零副作用、天然可开关**。
- 【未验证】二者语义差异：`playerGodMode` 伴生 `godModeExcludeFlag` / `SetGodModeCollisionExclusion`
  → 猜测 `playerGodMode` 是"调试上帝模式（可能含穿墙/无碰撞）"，
  `playerInvincible` 是"纯免伤"。**必须分别实测**（§7 T6/T7）。
- 【未验证】是否在**正式构建**里生效（`testMode`/`enableTestCommands` 的存在暗示这些是开发者开关，
  可能被条件编译或根本没接线）。
- 若其中任一真的生效 → **它是唯一"零风险、不破坏受击/动画/死亡"的方案**，应作为**首选**。

> ⚠ 但用户明确要求"不要破坏受击、动画、死亡状态和游戏事件"。
> 若 `playerInvincible` 让所有受击表现都消失，则它满足"无敌"但不满足"保留受击反馈"。
> **正确的产品化方式是同时具备两层**：`playerInvincible` 式绝对免伤（兜底）+ 可选的
> "减伤但不免疫"（保留受击表现）。

### 4.2 四个策略的横向对比

> 下表中"能拦住/拦不住"均为【推断】，依据是 §2 的结构证据，**全部需要实机验证**。

#### (a) 在伤害入口做 Prefix，把伤害归零 / 乘系数

**实现方式**

```csharp
// 目标必须是 HumanCharacterController.OnHit（覆写体），不是 BasicCharacterController
[HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.OnHit))]
static class Patch_OnHit
{
    // 原参数是 by-value，但 Harmony 允许 ref 覆盖实参
    static void Prefix(HumanCharacterController __instance,
                       ref float m_damage,
                       ref bool  hitCauseDeathFlag,
                       ref bool  m_hitAnimationFlag,
                       ref bool  showPlayerOnHitPanel,
                       ref bool  notifyAI)
    {
        if (!Config.Invincible) return;
        if (!IsLocalPlayer(__instance)) return;   // 只保护玩家
        m_damage = 0f;                 // 归零伤害
        hitCauseDeathFlag = false;     // 禁止本次致死
        m_hitAnimationFlag = true;     // 保留受击动画
        showPlayerOnHitPanel = true;   // 保留受击 UI
        notifyAI = true;               // 保留 AI 事件
    }
}
```

- 依据：`OnHit` 的参数表**把用户关心的每一件事都拆成了独立开关**
  （`m_hitAnimationFlag` 受击动画 / `notifyAI` AI 事件 / `hitCauseDeathFlag` 致死 /
  `showPlayerOnHitPanel` UI 面板 / `m_deathMultiplier` 处决倍率）。
  这是本系统**最贴合需求**的设计点。
- 【推断】把 `m_damage = 0` 而不是 `return false`（跳过原方法），
  可以保留原方法里的**受击动画、击退、AI 通知、血迹、镜头反馈**，只去掉血量变化。
  **这正是用户要的"能在不破坏受击动画和死亡事件的前提下钳制 hp"。**

**能拦住**：一切经过 `HumanCharacterController.OnHit` 的伤害（近战、子弹、爆炸溅射若走 OnHit、处决伤害）。
**拦不住**：
- 非 `OnHit` 路径（若 `OnDirectedHit` / `Apply` 有分支绕开 `OnHit`）→ 【未验证】
- Buff DoT（`ApplyBuffStatusTick`，§5）
- 失血致死（若存在）
- 环境伤害若走 `TickPoisonSmoke` 等独立 tick
**是否破坏受击/击退/状态**：可通过 ref 参数**精确控制**，不破坏。
**风险**：中。需覆盖 21 个 `OnHit` 覆写中的相关者；只打基类等于没打。
**联机影响**：高。见 §4.4。

#### (b) 周期性写 `hp = FullHP`（keep-alive tick）

**实现方式**：`Update`/`OnTimeUpdate` 里 `if (invincible) player.hp = player.FullHP;`

**能拦住**：所有"最终体现为血量下降"的伤害（含 DoT、失血扣血）。
**拦不住**：
- **`hitCauseDeathFlag` 触发的即死**（扣血与致死是两条路）→ 【推断】
- 处决（`ExecuteByCharacter` / `OnExecutionFinished` / `CanBeExecutedBy`）
- `ignoreDeadFlag: true` 的强制死亡
- 已经发生的死亡（`deadFlag` 已置位后再补血也没用）
**是否破坏受击/动画**：**会**。血量被反复拉满会让 HUD 抖动，
若游戏有"低血量触发"的视觉/音效/状态（`lowBloodVolumeWarningTimeFlag` 在 `HumanCharacterController` 上），
会出现闪烁。且每一帧写 `hp` 会触发 setter 里的 UI/联机逻辑 → 性能与网络噪音。
**风险**：中高。**等于"每帧刷新"**，联机时会产生大量 hp 同步。
**联机影响**：高（持续广播血量）。

#### (c) 只打死亡触发（`CharacterDead` / `deadFlag`）

**实现方式**

```csharp
[HarmonyPatch(typeof(HumanCharacterController), nameof(HumanCharacterController.CharacterDead))]
static class Patch_CharacterDead
{
    static bool Prefix(HumanCharacterController __instance) => !ShouldBlockDeath(__instance);
}
```

- 依据：`CharacterDead` 是 `Protected Virtual`（Human 覆写），`RunDeathChain` 是 private 启动器，
  `RaiseDeathCallbacks` 是 protected 事件派发 → **死亡确实收口在 `CharacterDead`**（【推断】）。
- 另有 `deadFlag` 属性（Public，真实 getter/setter）→ 可以置 `false` 强行"取消死亡标记"。

**能拦住**：所有死亡（这是死亡唯一执行点）。
**拦不住**：**血量照掉**。玩家会停在 0 血/负血但活着 → 所有依赖 hp 的系统（AI、HUD、体力、动作门控）都会错乱。
**是否破坏受击/动画**：不会破坏受击，但会造成"0 血站着"的严重状态不一致。
**风险**：**高**。这是"看起来能救命、实际造出一堆脏状态"的方案。
**联机影响**：高（主机侧仍判死）。

#### (d) 内建开关：`GameConfigs.playerInvincible` / `playerGodMode`

**实现方式**：`GameConfigs.playerInvincible = true;`（静态 bool 字段赋值）

**能拦住**：取决于游戏内部把它接到哪一层 → 【未验证】，若接在 `OnHit` 里则**全拦**。
**拦不住**：未知。
**是否破坏受击/动画**：未知；若它直接短路 `OnHit`，则受击表现**全部消失**（违背"不要破坏受击"）。
**风险**：**低**（不改游戏逻辑，只翻开关）。
**联机影响**：未知，可能仅本地视觉。

### 4.3 排序结论

| 排名 | 方案 | 定位 | 理由 |
| --- | --- | --- | --- |
| **1** | **(d) `GameConfigs.playerInvincible`** 作为**第一层兜底** | 绝对免伤 | 零补丁、天然开关、不触碰签名。**必须先实测它是否接线** |
| **2** | **(a) `HumanCharacterController.OnHit` 的 Prefix（`ref` 参数）** | 主力实现 | 唯一能**同时**做到"零伤害 + 保留受击动画 + 保留 AI/UI 事件 + 禁止致死"的入口；参数表就是为此设计的 |
| **3** | **(a′) `CharacterHitContext.Apply` 的 Prefix** | 防漏网 | 若 `Apply` 真是唯一分发器，它能兜住所有非 `OnHit` 路径；但静态状态有风险 |
| **4** | **(a″) `ApplyBuffStatusTick` 的 Prefix（DoT 专用）** | DoT 无敌 | 必须补，否则燃烧/酸/孢子仍会扣血 |
| **5** | **(b) keep-alive tick（写 `hp`）** | 第二层保险 | 只在 (a) 漏掉时兜底；频率要低（如 0.2s 而非每帧），且**必须走 `hp` 属性** |
| **6** | **(c) 只打 `CharacterDead`** | **不推荐单独使用** | 会造成 0 血存活的状态错乱；仅可作为"最后一道闸"（见下） |
| — | **绝对不要**：跳过整段玩家 `Update` | 禁止 | 用户硬约束；且会瘫痪输入/动画/交互 |

**要达到"完整无敌"，必须组合，单靠任一方案都不够：**

```
必选：(d) 或 (a)          —— 拦住直击伤害（主伤害流）
必选：(a″) ApplyBuffStatusTick —— 拦住 DoT（燃烧/酸/孢子/饥饿口渴）
必选：(b) 低频 hp 兜底      —— 拦住未知路径 / 失血（若 T5 证实失血会致死）
可选：(c) CharacterDead 闸门 —— 仅在检测到 hp<=0 且开启无敌时"取消本次死亡"，
                             并立刻补血到 FullHP（不能只取消死亡不补血）
```

**关键答案：能不能在不破坏受击动画和死亡事件的前提下钳制 hp？**
→ **能，但必须用"改伤害"而不是"改血量"。**
`HumanCharacterController.OnHit` 的 `ref float m_damage` + `ref bool hitCauseDeathFlag`
允许"伤害归零但方法照常执行"，受击动画/击退/AI/UI 全部保留。
反之，"钳 hp ≥ 1"（方案 b/c 的直接形式）**必然**破坏状态一致性（0 血存活 / HUD 抖动）。

### 4.4 联机（MP）影响 —— 必须写进风险表

【事实-程序集】

- `BasicCharacterController.netHpExternalAuthority`（`bool` 字段）→ **存在"血量由外部权威决定"的模式**。
  一旦为 true，本地写 `hp` 大概率被主机同步覆盖。
- `BasicCharacterController.netSuppressMeleeDamage`（`bool` 字段）→ 联机下近战伤害可被抑制（已有类似机制）。
- `ZedZone.Net.MsgDamageEvent { directional, damage, hitX, hitY, forceX, forceY }` → 伤害**以消息形式传输**。
- `ZedZone.Net.MsgPlayerDeath { playerId, pos*, zLevel, corpseConfigStr, corpseItemsBytes, standVz }`
  → 死亡会**广播并带尸体配置**。
- `BasicCharacterController.netRespawnInvincibleUntil`（`float` 字段）→ 复活后有一段原生的"无敌到某时刻"。

【推断】结论：

1. **单机/主机（Host）**：客户端补丁能完全生效。
2. **客机（Client）**：主机下发的 `MsgDamageEvent` / hp 同步会覆盖本地结果 → **无敌可能失效**。
3. **本地拦截死亡** → 主机仍认为你死了 → 会生成你的尸体（`corpseItemsBytes`）→ **物品复制/丢失风险**。
4. `netRespawnInvincibleUntil` 是一个**待验证的"原生无敌时间戳"**：如果 `OnHit` 真的读它，
   那么 `netRespawnInvincibleUntil = float.MaxValue` 可能提供零补丁的绝对无敌（【未验证】，见 T8）。

→ **设计建议：Mod 默认只在单机/主机生效；检测到客机时禁用或仅做"减伤"提示。**

### 4.5 与用户硬约束的对应

| 用户约束 | 本方案如何满足 |
| --- | --- |
| 不要简单跳过整个玩家 `Update` | 所有拦截都是**单方法级** Prefix（`OnHit` / `ApplyBuffStatusTick` / `CharacterDead`），不触碰 `Update` |
| 不要破坏受击、动画、死亡状态和游戏事件 | 用 `ref` 参数只改 `m_damage` 与 `hitCauseDeathFlag`，`m_hitAnimationFlag`/`notifyAI`/`showPlayerOnHitPanel` 保持原值；死亡事件（`RaiseDeathCallbacks`）在不该死时根本不该触发 |
| 每个功能独立开关、默认关闭 | 见 §4.6 开关矩阵；每个 Harmony 补丁开头 `if (!cfg.EnableXxx) return;`，配置默认 `false` |

### 4.6 建议的开关矩阵（默认全部 `false`）

| 开关 | 作用 | 实现锚点 | 默认 |
| --- | --- | --- | --- |
| `EnableDamageReduction` | 受伤保护（伤害 × 系数，不死） | `OnHit` Prefix 改 `m_damage` | **关** |
| `DamageReductionPercent` | 减伤百分比（如 0.5 = 减半） | 同上 | 0 |
| `EnableInvincible` | 完全无敌（伤害归零 + 禁致死） | `OnHit` Prefix | **关** |
| `EnableDotImmunity` | DoT 免疫（燃烧/酸/孢子/饥渴掉血） | `ApplyBuffStatusTick` Prefix | **关** |
| `EnableKeepAliveTick` | 低频 hp 兜底 | 独立 timer，走 `hp` 属性 | **关** |
| `EnableGodModeFlag` | 使用游戏内建 `playerInvincible` | `GameConfigs` 静态字段 | **关** |
| `EnableBleedImmunity` | 止血（`StopAllBleeding`） | `HumanCharacterController.StopAllBleeding()` | **关** |
| `EnableInjuryHeal` | 治愈部位伤 | `HealAllBodyPartInjuries()` | **关** |
| `ApplyToTeammates` | 是否同样保护队友 NPC | `isPlayer == false` 分支 | **关** |

> 设计要点：**减伤与无敌共用同一个补丁点**，只是系数不同（减伤 `m_damage *= k`，无敌 `m_damage = 0`）。
> 这让"受伤保护"和"无敌"成为同一实现的两个档位，代码量最小、行为最一致。

---

## 5. 免伤但不能免状态的边界（受伤保护 ≠ 无敌）

用户要求把"受伤保护"和"无敌"分开。这一节列出**即使伤害归零，仍会继续发生**的一切。

### 5.1 与伤害相邻、但**独立于 hp** 的通道

| 通道 | 证据 | 是否会随"伤害归零"一起消失 |
| --- | --- | --- |
| **受击动画** | `m_hitAnimationFlag`；`HitAnimationData` 嵌套类字段 `hitTransform` / `hitLocalPositionTemp` / `hitVelocity` / `hitTimeFlag` / `hitLastTime`；`hitClips` / `hitAnimationDataList` / `hitReactionTransformList` / `hitRecoverFloat` / `hitRecoverTime`；`CharacterBodyPart.BodyPartHitAnimationConfig` | **不会自动消失**（受 `m_hitAnimationFlag` 控制）→ 正是用户要保留的 |
| **击退 / 受击位移** | `Vector2 m_force` 参数；`hitVelocity`；`KnockdownCause{LegHit,Vehicle,Grapple,Net,Impact}`；`knockdownEndTime` / `knockdownEnterTime` / `knockdownOwnsPhysical`；`InitVehicleKnockdownPhysics`、`HoldKnockdownUnderVehicle` | 【推断】不会消失（force 是独立参数）→ 用户要求保留 |
| **受击 UI 红闪** | `m_showPlayerOnHitPanel` → `PlayerHitEffectPanel.PlayerOnHit()`（单例，`playerHitTimeFlag`） | 由 `showPlayerOnHitPanel` 控制；**保持 true 就保留** |
| **AI 感知 / 仇恨** | `notifyAI` 参数；`MakeNoise(float, float)`；`OnSpotEnemy()` | 由 `notifyAI` 控制 |
| **血迹 / 血雾 / 血渍** | `EmitBloodMistBurst(Vector2,int)`、`TakeBloodDecalCount(float)`、`BloodDecalMaxPerHit`、`BloodDecalMaxPerCharacterPerFrame`、`bloodDecalBudgetFrame`、`bloodCoatWritten`、`bloodCoatColor`、`bloodFootprintFloat`、`bloodmistParticle`、`OnHitUpdateBodypartsTextures(...)`、`CharacterBodyPart.OnHit(...)`、`damagedSprite`/`initBulletHoleGameObject` | 【推断】不会消失（走独立贴图/粒子路径）→ 可能出现"零伤害但满身是血" |
| **护甲损耗** | `CharacterBodyPart.DamageArmor(bool, float)`、`armorBaseDamageFloat`、`armorOverrides`、`RefreshArmorDamageFloat()`、`ApplyArmorOverride/RemoveArmorOverride` | 【推断】**会**：护甲按伤害扣 → 伤害归零可能护甲不掉了（这反而是"免伤"该有的） |
| **部位伤（injury）** | `CharacterBodyPartsData`；`CharacterBodyPart` 的 `damageFloat`/`defenseFloat`/`deathDestroyFlag`；`HealAllBodyPartInjuries()` 的存在本身证明"部位伤"独立存在；`CharacterData.skillProperty_LimbDamageReduceFloat` / `appliedBodyPartConstitution` | 【未验证】很可能**独立于 hp 累积** → 需要单独处理 |
| **失血（bloodVolume）** | `CharacterStatusData.bloodVolume`；`HumanCharacterController.LossingBlood(float)`；`StopAllBleeding()`；`lowBloodVolumeWarningTimeFlag`；`CharacterData.skillProperty_BleedResistFloat` | 【未验证】很可能**独立** → 免伤不等于止血 |
| **体温（bodyTemperature）** | `CharacterStatusData.bodyTemperature`（真实 get/set）；`standardBodyTemperature`、`min/maxBodyTemperature`、`bodyTemperatureOffsetFloat`；`buffTemperaturePerSecond`；`CharacterData.totalTemperatureGain`、`skillProperty_ColdResistFloat`；`HumanCharacterController.sleepBodyTemperatureTemp` | 【推断】**独立** → 冻伤不因无敌消失 |
| **辐射（radiationDose）** | `CharacterStatusData.radiationDose`；`buffRadiationIntakeScale` / `buffRadiationDecayScale` | 【推断】**独立** |
| **饥饿 / 口渴 / 精神** | `hunger`/`thirst`/`sanity`（Basic 上的 Public 非虚真实属性）；`buffHungerPerSecond`/`thirstPerSecond`；`NPCAffinity.IsStarving(CharacterStatusData)` | 【推断】**独立** |
| **酸 / 孢子** | `buffImmuneToAcid`、`buffImmuneToSpore` 字段；`TickPoisonSmoke(float seconds, bool acidImmune)`；`GameConfigs.acidDamage_Character`、`sporeFogDamagePerDay`；`GetClothingHazardDamageMultiplier(ClothingHazard)` | 有**原生免疫位**可置 true（这正是"免状态"的正规做法） |
| **燃烧** | `BuffAttr.hpPerSecond`（燃烧类 DoT）；`GameConfigs.fireDamage_Character`；`burningDamageMultiplier_NonHuman`；`ExecuteBuffs`/`ApplyBuffStatusTick(burningDamageScale)` | 需走 DoT 拦截；**没有** `buffImmuneToBurning` 这样的位（不存在） |
| **击倒 / 倒地** | `KnockdownCause` 枚举、`knockdown*` 字段、`UpdateDownedAnimation()`、`netDownedFlag`、`GameConfigs.downedDamageImmuneSeconds`、`downedFinishDamage` | 【推断】**独立** → 免伤仍可能被击倒 |
| **处决** | `CanBeExecutedBy`、`ExecutionCharacter`、`ExecuteByCharacter`、`OnExecutionFinished`、`CanDoExecution`、`ExecutionExecute`、`OnEnterPlayerExecuteRange`、`RefreshPlayerExecuteInteract`、`skillProperty_ExecutionHPFloat` | 【未验证】处决很可能绕过普通伤害 → 无敌必须单独处理 |
| **灾变感染** | `IsCataclysmInfected()` | 【未验证】 |

### 5.2 DoT（持续伤害）为什么可能绕过 `OnHit` —— 本报告最重要的"拦不住"边界

【事实-程序集】

```csharp
// BuffAttr : ScriptableObject（数据资产，只读定义）
int   buffId; string buffName; string effectName;
float hpPerSecond;                 // ★ 每秒血量变化
float bodyTemperaturePerSecond;
float hungerPerSecond; float thirstPerSecond; float energyPerSecond;
float hpDrainFloorPercent;         // ★ DoT 的"血量下限百分比"
bool  causeDeathFlag;              // ★ Buff 自己就能致死
bool  showOnPlayerOnhitPanel;
bool  isDebuff; bool isMealBuff;
float armorPointsGain; float meleeAttackMultiplier;
float perceptionScale; float radiationIntakeScale; float radiationDecayScale;
float hpRecoverScale; float carryWeightGain; float staminaRegenScale;
bool  immuneToAcid; bool  immuneToSpore;   // ★ 原生免疫位
```

```csharp
// BasicCharacterController（无任何覆写）
private void ApplyBuffStatusTick(BuffAttr buffAttr, float debuffScale,
                                 float buffPowerScale, float burningDamageScale);
protected virtual void ExecuteBuffs();     // Human 覆写
protected virtual void UpdateStatus();     // Human 覆写
public virtual void OnTimeUpdate(float);   // Human 覆写
public void TickPoisonSmoke(float seconds, bool acidImmune);
```

【推断】（结构证据强）：

1. Buff 的伤害是**数据驱动**的 `hpPerSecond`，由 **`ApplyBuffStatusTick`** 落地。
   该方法参数是 `BuffAttr` + 三个缩放系数 → 它就是"把 buff 数值转成角色数值变化"的地方。
2. `ApplyBuffStatusTick` 是 **`Private` 且无覆写** → 一个收口点，适合打补丁。
3. `BuffAttr.causeDeathFlag` 与 `OnHit.hitCauseDeathFlag` **是两个独立概念** →
   说明 buff 有**自己的致死路径**，不经 `OnHit`。
4. `hpDrainFloorPercent` 的存在说明"DoT 有血量下限"（例如不会把血扣到 0 以下/以下的某比例）
   → 这可能解释"为什么纯 DoT 烧不死人"，但也意味着**不能依赖它**。
5. 因此：**在 `OnHit` 上做无敌，不能阻止燃烧/酸/孢子/饥渴的掉血。** 这是【推断】，必须实测（T9）。

**额外结论（直接可用）**：`BuffAttr.immuneToAcid` / `immuneToSpore` 映射到角色上的
`BasicCharacterController.buffImmuneToAcid` / `buffImmuneToSpore` 两个 **bool 字段**
→ 想要"免酸/免孢子"**不需要补丁**，直接置 true 即可（但要注意这会被 Buff 系统重算覆盖，需施加后重写）。

### 5.3 "受伤保护"的推荐语义（与无敌分开）

| 功能 | 实现 | 效果 |
| --- | --- | --- |
| 受伤保护（减伤） | `OnHit` Prefix：`m_damage *= (1 - reduction)` | 掉血变慢、**受击动画/击退/UI/血迹/护甲/部位伤全部保留**，死亡仍可能（会真的死） |
| 免死（保底） | `OnHit` Prefix：`if (player.hp - m_damage <= 1) { m_damage = player.hp - 1; hitCauseDeathFlag = false; }` | 保留一切反馈，但永不死 ← **这就是"钳 hp 而不破坏动画"的正确形态** |
| 完全无敌 | `OnHit` Prefix：`m_damage = 0; hitCauseDeathFlag = false;` + DoT 拦截 + 低频 hp 兜底 | 完全不受伤害 |

> 三者共用同一个补丁，仅参数策略不同；**这才是"受伤保护"与"无敌"可分档的技术基础**。

### 5.4 官方 BUFF 数值（旁证，用于验证 DoT 拦截是否生效）

【事实-官方文档】社区整理的 Buff 副作用（**用于构造实机测试用例，不作为程序事实**）：

- 燃烧 Burning：约 **-20 HP/s**
- 酸蚀 Acid：约 **-5 HP**
- 溺水 Drowning：约 **-20 HP**
- 饥饿 / 口渴：约 **-5 HP** 级别

→ 实机验证"无敌是否漏 DoT"的最佳用例：**点燃自己 / 踩酸 / 溺水 / 饿到 0**，观察 hp 是否仍下降。

---

## 6. 技术记录表

> 说明：
> - 「访问方式」= interop 中的形态（字段 / 属性 / 方法），**括号内为元数据指针揭示的真实修饰符**。
> - 「修改方式」= Mod 侧可行的写入/补丁手段。
> - 「测试」列**全部为 `未实机验证`**（本阶段未运行游戏，无任何真实日志行）。
> - 所有"用途"若涉及内部逻辑，均为【推断】。

### 6.1 生命值 / 状态存储

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
| --- | --- | --- | --- | --- | --- | --- |
| `BasicCharacterController.hp` | `float` 虚属性 | 血量权威访问口｜【推断】转发到 `characterStatusData.hp` | 属性；`get_hp_Public_Virtual_New` / `set_hp_Public_Virtual_New`（token 100667675/100667676） | 直接赋值；或 Harmony patch 虚属性访问器 | 中：setter 内有逻辑（UI/联机/致死），直接写可能触发副作用 | 未实机验证 |
| `HumanCharacterController.hp` | `float` 虚属性（**override**） | **玩家**的血量访问口 | 属性；`get/set_hp_Public_Virtual`（token 100670066/100670067） | 同上；**补丁必须打在这里** | 中：覆写逻辑未知 | 未实机验证 |
| `FinalBossController.hp` | `float` 虚属性（override） | Boss 血量 | 属性；token 100672363/100672364 | 无需修改 | 低 | 未实机验证 |
| `CharacterStatusData.hp` | `float` **字段** | 血量的**真实存储** | 字段偏移裸指针（`NativeFieldInfoPtr_hp`） | 可直接改，但**会绕过** `hp` 属性逻辑 | **高**：UI 不刷新、联机不同步 | 未实机验证 |
| `CharacterStatusData.maxhp` | `float` 字段 | 状态数据里的血量上限 | 字段偏移 | 可直接改 | 中：与 `BasicCharacterController.maxHP` 可能不一致 | 未实机验证 |
| `BasicCharacterController.maxHP` | `float` 字段 | 角色血量上限（字段） | 字段偏移（`NativeFieldInfoPtr_maxHP`） | 可直接改 | 中：`FullHP` 可能再乘系数 | 未实机验证 |
| `BasicCharacterController.FullHP` | `float` 只读属性 | 【推断】计算出的满血（`maxHP`×难度×Boss 系数） | 属性；`get_FullHP_Public_get`（token 100667713），**非虚** | 只读，不可写 | 低 | 未实机验证 |
| `BasicCharacterController.difficultyFullHP` | `float` 字段 | 难度修正后的满血 | 字段偏移 | 可直接改 | 中：影响难度平衡 | 未实机验证 |
| `CharacterData.characterStatusData` | `CharacterStatusData` 字段 | 状态数据容器 | 字段（引用类型） | 可整体替换（危险） | 高：替换会丢失所有状态 | 未实机验证 |
| `BasicCharacterController.characterData` | `CharacterData` 字段 | 角色数据根 | 字段 | 只读为宜 | 高 | 未实机验证 |
| `CharacterStatusData.bloodVolume` | `float` 字段 | 失血量｜【推断】可独立致死 | 字段偏移 | 可直接改 | 中：致死关系未验证 | 未实机验证 |
| `CharacterStatusData.deadBloodVolume` | `static float` | 【推断】死亡时的失血常量，非阈值 | 静态字段 | 可改（全局） | 中：全局影响所有角色 | 未实机验证 |
| `CharacterStatusData.bodyTemperature` | `float` 属性 | 体温（**真实 get/set 方法**） | `get/set_bodyTemperature_Public_*`（token 100671692/100671693） | 直接赋值 | 低 | 未实机验证 |
| `CharacterStatusData.radiationDose` | `float` 字段 | 辐射累积 | 字段偏移 | 可直接改 | 低 | 未实机验证 |
| `BasicCharacterController.hunger/thirst/sanity` | `float` 属性（**非虚真实访问器**） | 饥饿/口渴/精神 | `get/set_hunger_Public_*` 等（token 100667683~100667688） | 直接赋值 | 低 | 未实机验证 |
| `BasicCharacterController.isPlayer` | `bool` 字段 | 区分玩家/NPC | 字段偏移 | 可读即可（**不要改**） | 高：改动会破坏大量分支 | 未实机验证 |
| `BasicCharacterController.lastLethalDamage` | `float` 字段 | 【推断】记录致死伤害值 | 字段偏移 | 只读 | 低 | 未实机验证 |
| `BasicCharacterController.hitDamageThreshold` | `float` 字段 | 【推断】触发受击表现的伤害阈值 | 字段偏移 | 可改（调高=更少受击表现） | 中：影响表现 | 未实机验证 |
| `BasicCharacterController.damageEffect` | `bool` 字段 | 【未验证】伤害特效开关 | 字段偏移 | 可改 | 中 | 未实机验证 |
| `BasicCharacterController.mutationDamageTakenFloat` | `float` 字段 | 【推断】变异带来的受伤倍率 | 字段偏移 | 可置 0 尝试减伤 | **中高**：可能只作用于部分伤害路径 | 未实机验证 |

### 6.2 伤害入口

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
| --- | --- | --- | --- | --- | --- | --- |
| `CharacterHitContext.Apply` | `static bool` 方法 | 【推断】全局伤害分发器（`direct` 决定走直击/非直击） | 静态方法，`Public`，非虚 | Harmony `Prefix`（可改 `damage`/`causeDeath`/`animation`） | 中高：静态字段 `lastBlocked`/`directional` 可被污染 | 未实机验证 |
| `BasicCharacterController.OnDirectedHit` | `bool` 方法 | 【推断】护甲结算 + 直击伤害 | `Public`，**非虚**（唯一实现） | Harmony `Prefix`/`Postfix` | 中：非虚=好打，但可能只覆盖直击 | 未实机验证 |
| `BasicCharacterController.OnHit` | `virtual bool` 方法 | 基类受击实现（9 参数，含 4 个行为开关） | `Public Virtual` **新槽**（token 100667890） | **对玩家无效**（被覆写） | 低（无效） | 未实机验证 |
| `HumanCharacterController.OnHit` | `override bool` 方法（9 参数） | **玩家受击唯一实现** | `Public Virtual` **覆写**（token 100670195） | **Harmony `Prefix`，`ref float m_damage` + `ref bool hitCauseDeathFlag`** ← 首选 | 中：需确认无其它路径 | 未实机验证 |
| `OnHit` 的 21 个覆写（Zombie/Boss/Carrion/Pet/…） | `override bool` | 各怪物受击 | 同签名 | 逐个 patch 或忽略 | 低（只需玩家） | 未实机验证 |
| `BasicCharacterController.OnHitByVehicle` | `virtual bool`（3 参数） | 【推断】车辆碾压伤害 | `Public Virtual` 新槽；Human/Zombie 覆写 | Harmony patch | 中 | 未实机验证 |
| `BasicCharacterController.OnHitByCharacter` | `virtual void`（3 参数） | 【推断】"被谁打"通知 | `Public Virtual` 新槽；≥9 处覆写 | patch（观测用） | 低 | 未实机验证 |
| `CharacterBodyPart.OnHit` | `virtual void`（6 参数，含 `ref Vector2`） | 单部位受击（贴图/护甲/血洞） | `Public Virtual` 新槽 | patch | 中：高频 | 未实机验证 |
| `BasicCharacterController.onHitCallBack` | `OnHitCallBack` 委托字段 | 【推断】受击广播（观测点） | 字段；`OnHitCallBack.Invoke(float,Vector2,Vector2)` | **用 `+=` 追加，禁止 `=` 覆盖** | 低（只读） | 未实机验证 |
| `onHitCallBack.Invoke` | `virtual void`（委托） | 委托自身调用蹦床 | `Public Virtual New` | **不要 patch**（不是入口） | — | 未实机验证 |
| `BasicCharacterController.OnHitUpdateBodypartsTextures` | `void`（5 参数） | 部位贴图/血洞刷新 | `Public`，非虚 | 只读观测 | 低 | 未实机验证 |
| `BasicCharacterController.ApplyBuffStatusTick` | `Private void`（`BuffAttr`, 3×float） | **DoT 落地**（`hpPerSecond` 等） | `Private`，非虚（token 100667973） | Harmony `Prefix`（改 `buffPowerScale`/跳过） | 中：会同时影响正面 Buff 收益 | 未实机验证 |
| `BasicCharacterController.ExecuteBuffs` | `virtual void` | 【推断】每 tick 遍历 Buff | `Protected Virtual` 新槽；Human 覆写 | patch（慎） | 高：会停掉所有 Buff 逻辑 | 未实机验证 |
| `BasicCharacterController.UpdateStatus` | `virtual void` | 【推断】状态（体温/辐射/失血）tick | `Protected Virtual` 新槽；Human 覆写 | patch（慎） | 高 | 未实机验证 |
| `BasicCharacterController.OnTimeUpdate` | `virtual void(float)` | 游戏时间推进 tick | `Public Virtual` 新槽；Human 覆写 | patch（慎） | 高 | 未实机验证 |
| `BasicCharacterController.TickPoisonSmoke` | `void(float, bool)` | 毒烟/酸雾伤害 | `Public`，非虚（token 100667953） | patch | 中 | 未实机验证 |
| `BasicCharacterController.StopAllBleeding` | `bool()` | 止血 | `Public`，非虚（token 100667962） | 直接调用 | 低 | 未实机验证 |
| `HumanCharacterController.LossingBlood` | `void(float)` | 失血 | `Public`，非虚（token 100670181） | patch 或置 0 调用 | 低 | 未实机验证 |
| `HumanCharacterController.HealAllBodyPartInjuries` | `void()` | 治愈全部部位伤 | `Public`，非虚（token 100670516） | 直接调用 | 低 | 未实机验证 |
| `BasicCharacterController.gameConfigs` 侧 `GameConfigs.playerInvincible` | `static bool` 字段 | 游戏内建"玩家无敌"开关 | 静态字段（L85351） | 直接赋值 true/false | **低**（不改逻辑） | 未实机验证 |
| `GameConfigs.playerGodMode` | `static bool` 字段 | 游戏内建"上帝模式" | 静态字段（L85365） | 直接赋值 | 低-中（可能含穿墙） | 未实机验证 |
| `GameConfigs.downedDamageImmuneSeconds` | `static float` 字段 | 倒地免疫时长 | 静态字段（L86195） | 可改（全局） | 中：全局 | 未实机验证 |
| `BasicCharacterController.netRespawnInvincibleUntil` | `float` 字段 | 【未验证】原生"无敌到某时刻"时间戳 | 字段偏移（token 区 L169908） | 置 `float.MaxValue` 尝试 | 中：可能只用于复活逻辑/仅联机 | 未实机验证 |
| `BasicCharacterController.mutationDamageTakenFloat` | `float` 字段 | 【推断】受伤倍率 | 字段偏移 | 置 0 | 中高 | 未实机验证 |
| `BasicCharacterController.buffImmuneToAcid` | `bool` 字段 | 酸免疫 | 字段偏移 | 置 true（每帧重写以防被覆盖） | 低 | 未实机验证 |
| `BasicCharacterController.buffImmuneToSpore` | `bool` 字段 | 孢子免疫 | 字段偏移 | 同上 | 低 | 未实机验证 |
| `CharacterBodyPart.damageFloat` / `defenseFloat` | `float` 字段 | 【推断】部位受伤倍率 / 护甲值 | 字段偏移 | 可改（单部位减伤） | 中：只影响该部位 | 未实机验证 |
| `CharacterBodyPart.DamageArmor` | `bool(bool,float)` | 护甲损耗 | `Public`，非虚（token 100667629） | patch | 低 | 未实机验证 |
| `CharacterBodyPart.OnHit_Equipment` | `Private bool` | 装备受击 | `Private`（token 100667623） | 一般不动 | 低 | 未实机验证 |

### 6.3 死亡

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
| --- | --- | --- | --- | --- | --- | --- |
| `BasicCharacterController.CharacterDead` | `virtual void(Vector2,Vector2,bool)` | **死亡执行点** | `Protected Virtual` 新槽（token 100667910） | Harmony `Prefix` return false 阻止 | **高**：只阻止死亡不补血 → 0 血存活状态错乱 | 未实机验证 |
| `HumanCharacterController.CharacterDead` | `override void(Vector2,Vector2,bool ignoreDeadFlag=false)` | **玩家死亡执行点** | `Protected Virtual` 覆写（token 100670526） | 同上；或改 `ignoreDeadFlag` | **高**：同上 | 未实机验证 |
| `BasicCharacterController.CharacterDeadCoroutine` | `IEnumerator(float,Vector2,Vector2)` | 死亡延迟/演出 | `Protected`，非虚（token 100667894） | 一般不动 | 高 | 未实机验证 |
| `BasicCharacterController.RunDeathChain` | `void(Vector2,Vector2)` | 【推断】死亡链启动 | `Private`（token 100667917） | patch（观测） | 高 | 未实机验证 |
| `BasicCharacterController.RaiseDeathCallbacks` | `void()` | **死亡事件派发** | `Protected`，非虚（token 100667909） | Postfix 观测 | 中（观测低风险） | 未实机验证 |
| `BasicCharacterController.OnDeathChainException` | `void(Exception)` | 死亡链异常保护 | `Protected`，非虚 | 不动 | — | 未实机验证 |
| `BasicCharacterController.deadFlag` | `bool` 属性（**真实 get/set**） | 运行时死亡标记 | `get/set_deadFlag_Public_*`（token 100667895/100667896） | 直接置 false（**危险**） | **高**：与 `netDeadFlag`/`CharacterData.deadFlag` 会不同步 | 未实机验证 |
| `BasicCharacterController.netDeadFlag` | `bool` 字段 | 联机死亡标记 | 字段偏移（L169856） | 直接改（联机危险） | **高** | 未实机验证 |
| `CharacterData.deadFlag` | `bool` 字段 | **存档**死亡标记 | 字段偏移（`CharacterData`，L295760） | 直接改（存档级） | **高**：只改它会与运行时不一致 | 未实机验证 |
| `BasicCharacterController.deadLingerTicks` | `int` 字段 | 尸体滞留计时 | 字段偏移 | 只读 | 低 | 未实机验证 |
| `BasicCharacterController.IsLingeringDeadInstanceCandidate` | `bool()` | 滞留尸体判定 | `Public`，非虚 | 只读 | 低 | 未实机验证 |
| `BasicCharacterController.HealLingeringDeadInstance` | `void(string)` | 复活滞留尸体 | `Private`（token 100667916） | 可调用（"撤回死亡"） | 中高：状态可能残留 | 未实机验证 |
| `BasicCharacterController.TryHealLingeringDeadInstance` | `void()` | 同上 | `Private`（token 100667915） | 同上 | 中高 | 未实机验证 |
| `BasicCharacterController.PrepareBodyPartsForDeathSplit` | `virtual void()` | 尸体肢体拆分 | `Protected Virtual` 新槽；Human 覆写 | patch 可阻止碎尸 | 中 | 未实机验证 |
| `BasicCharacterController.OnPlayerDeathChainAborted` | `virtual void()` | **死亡链被中止回调** | `Protected Virtual`；Human 覆写（token 100670527） | Postfix 观测 = 判断"死亡被成功阻止"的信号 | 低（观测） | 未实机验证 |
| `BasicCharacterController.damageEffect` | `bool` 字段 | 【未验证】伤害特效 | 字段偏移 | 可改 | 中 | 未实机验证 |
| `DeathPanel.instance` / `OpenPanel()` | 单例 / `void()` | 玩家死亡 UI + 读档 | `Public` 静态实例 + 方法 | Postfix 观测 | 低（观测） | 未实机验证 |
| `ZedZone.Net.MsgPlayerDeath` | 网络消息（7 字段） | 死亡广播 + 尸体配置 | 结构体式消息 | 不宜修改 | **高**（联机不同步） | 未实机验证 |
| `CarrionController.OnDeathBegin` | `Il2CppSystem.Action` 事件 | 【事实】游戏内唯一显式死亡事件（仅 Carrion） | `add_/remove_OnDeathBegin` | 可订阅 | 低 | 未实机验证 |
| `CataclysmRunStats.ReasonDeath` | `static int` | 死亡原因统计 | 静态字段 | 只读 | 低 | 未实机验证 |

### 6.4 联机

| 名称 | 类型 | 用途 | 访问方式 | 修改方式 | 风险 | 测试 |
| --- | --- | --- | --- | --- | --- | --- |
| `BasicCharacterController.netHpExternalAuthority` | `bool` 字段 | 【推断】血量由外部（主机）权威决定 | 字段偏移（L169804） | 只读判断，决定是否启用本地无敌 | **高**（决定方案可行性） | 未实机验证 |
| `BasicCharacterController.netSuppressMeleeDamage` | `bool` 字段 | 抑制近战伤害（已有机制） | 字段偏移（L169817） | 可置 true 作为"近战免伤" | 中 | 未实机验证 |
| `BasicCharacterController.netDownedFlag` | `bool` 字段 | 倒地标记（联机） | 字段偏移（L169869） | 只读 | 中 | 未实机验证 |
| `ZedZone.Net.MsgDamageEvent` | 消息 `{directional,damage,hitX,hitY,forceX,forceY}` | 伤害同步 | 结构 | 不宜修改 | 高 | 未实机验证 |

---

## 7. 无法确认的清单（必须实机验证）

> **本阶段未运行游戏，以下全部为未验证。** 每条都给出可执行的验证方法。

### 7.1 阻断性未知（决定方案成败）

| 编号 | 未知项 | 为什么关键 | 验证方法 |
| --- | --- | --- | --- |
| **U1** | `hp` 属性 getter/setter 是否真的转发到 `characterStatusData.hp` | 决定写哪一处 | Harmony 给 `get_hp`/`set_hp` 打 Postfix 打日志，同时打印两处值 |
| **U2** | `HumanCharacterController.OnHit` 是否是玩家**唯一**承伤路径 | 决定"打一个点"够不够 | 给 `HumanCharacterController.OnHit`、`CharacterHitContext.Apply`、`OnDirectedHit`、`OnHitByVehicle`、`ApplyBuffStatusTick` 各打一条日志，分别用**枪/近战/爆炸/坠落/燃烧/酸/溺水/饿** 8 种来源各打一次，看日志命中哪个 |
| **U3** | `CharacterHitContext.Apply` 是否真的是唯一分发器 | 若是，一个补丁即可全覆盖 | 同 U2 的日志矩阵；观察 `Apply` 是否每次都先出现 |
| **U4** | `GameConfigs.playerInvincible` / `playerGodMode` 在正式版是否接线 | 若接线，是零风险最佳方案 | 直接置 true，重复 U2 的 8 种伤害，看 hp 是否变化；同时观察受击动画/UI 是否仍在 |
| **U5** | DoT 是否绕过 `OnHit` | 决定是否必须额外 patch `ApplyBuffStatusTick` | 在 `OnHit` 打日志后，仅施加"燃烧"Buff，看 `OnHit` 是否被调用；若 hp 掉而日志没出现 → 确认绕过 |
| **U6** | `bloodVolume` 是否能独立致死 | 决定"钳 hp"是否够 | 只降低 `bloodVolume`（不改 hp），观察是否死亡 |
| **U7** | `netRespawnInvincibleUntil` 是否被 `OnHit` 读取 | 若是，可能是零补丁无敌 | 置 `float.MaxValue` 后重复 U2 |
| **U8** | `mutationDamageTakenFloat = 0` 是否等于受伤免疫 | 可能是最轻量的减伤 | 置 0 后执行 U2，比较伤害数值变化 |
| **U9** | MP 客机是否会覆盖本地伤害结果 | 决定联机下是否禁用 | 双人联机，主机打客机，客机侧开无敌补丁，观察 hp 与主机 HUD |
| **U10** | `deadFlag` 三个副本的同步时机 | 决定能否安全"取消死亡" | Patch `set_deadFlag` 打日志，覆盖死亡/读档/复活三种场景 |

### 7.2 实现细节未知（影响体验）

| 编号 | 未知项 | 验证方法 |
| --- | --- | --- |
| U11 | `m_damage = 0` 时受击动画/击退/血迹是否仍出现 | 设 0 后连续受击，录像对比开关前后 |
| U12 | `hitCauseDeathFlag = false` 是否足够阻止"伤害致死" | 用超高伤害（如 99999）打玩家，看在 hp 极低下是否存活 |
| U13 | 处决（`ExecuteByCharacter` / `OnExecutionFinished`）是否绕过 `OnHit` | 让僵尸执行处决动作，观察日志 |
| U14 | 部位伤（injury）是否独立于 hp 累积 | 多次小伤害后调 `HealAllBodyPartInjuries()`，看部位状态是否变化 |
| U15 | `hpDrainFloorPercent` 的语义（DoT 是否会扣到 0） | 长时间燃烧至死，记录 hp 曲线 |
| U16 | `damageEffect` 字段真实作用 | 切换后观察受击特效 |
| U17 | 连续写 `hp` 对 HUD/性能/联机的影响 | 0.2s 一次 vs 每帧，测量帧时间与网络包量 |
| U18 | `StopAllBleeding` 的返回值语义与副作用 | 调用后检查是否清除流血状态与血迹 |
| U19 | `buffImmuneToAcid/Spore` 是否每帧被 Buff 系统重置 | 置 true 后施加酸/孢子，观察是否仍中毒 |
| U20 | `BasicCharacterController_Frame`（`PetCharacterController` 的父类）是否与玩家路径共享伤害逻辑 | 对比日志 |

### 7.3 结构性限制（静态阶段无法突破）

1. **无调用图**：interop 无 IL 体，无法静态确认"谁调用谁"。所有调用关系只能靠实机打日志。
2. **无 RVA / 无 native 地址**：`NativeMethodInfoPtr_*` 是运行时 token，无法映射到 `GameAssembly.dll` 偏移，
   因此无法用反汇编器补足调用图（除非另做 Il2CppDumper + 反汇编，超出本阶段范围）。
3. **参数默认值可信，参数语义不可信**：默认值（如 `hitCauseDeathFlag = true`）是元数据事实；
   但"这个 bool 到底控制什么"只能靠实测。
4. **`CharacterStatusData` 内部一致性未知**：无法静态确认 `hp` 与 `maxhp` 的钳制关系。
5. **未验证任何 Mod 补丁本身能加载**（BepInEx + HarmonyX 对 IL2CPP 的补丁可用性）。

---

## 8. 结论摘要（给实现阶段的行动清单）

1. **权威血量** = `characterStatusData.hp`（存储）/ `BasicCharacterController.hp` 虚属性（访问口），
   玩家必须用 `HumanCharacterController.hp`（覆写）。
2. **最佳伤害入口** = `HumanCharacterController.OnHit(...)` 的 **Harmony Prefix + `ref m_damage`**；
   **绝不要**只打 `BasicCharacterController.OnHit`（会被覆写绕过）。
   备选/补充 = `CharacterHitContext.Apply`（静态，疑似总分发器）。
3. **必须额外处理 DoT** = `BasicCharacterController.ApplyBuffStatusTick`（Private，`BuffAttr.hpPerSecond` 的落地）。
   仅打 `OnHit` 的"无敌"**不完整**。
4. **死亡收口** = `CharacterDead(Vector2, Vector2, bool ignoreDeadFlag)`；
   三个死亡标记（`deadFlag` 属性 / `netDeadFlag` 字段 / `CharacterData.deadFlag` 存档）存在**不同步风险**。
5. **先测 `GameConfigs.playerInvincible` / `playerGodMode`** —— 游戏自带无敌开关，若接线则是零风险方案。
6. **能同时"钳制血量"与"保留受击动画"的唯一正确做法**：改 `m_damage`（Prefix ref），
   而不是改 `hp`；`m_hitAnimationFlag` / `notifyAI` / `showPlayerOnHitPanel` 保持 true。
7. **免伤 ≠ 免状态**：失血、体温、辐射、饥渴、部位伤、击倒、处决很可能独立存在，
   需分别用 `StopAllBleeding()` / `HealAllBodyPartInjuries()` / `buffImmuneToAcid|Spore` 等手段处理。
8. **联机**：`netHpExternalAuthority` + `MsgDamageEvent`/`MsgPlayerDeath` 表明客机侧本地无敌不可靠，
   默认应在客机禁用或降级为"减伤"。

---

*本文件为 Phase-1 静态调查产物。所有行为性结论均标注为【推断】或【未验证】，未经实机验证。*
