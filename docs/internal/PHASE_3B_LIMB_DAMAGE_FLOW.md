# PHASE_3B_LIMB_DAMAGE_FLOW.md — 肢体伤害流程与拦截点分析

> **阶段**：Phase 3B 第二阶段（安全性研究，**未开发、未改代码**）
> **游戏**：ZED ZONE 1.0.16 / build 26600653 / Unity 2023.1.18f1 / IL2CPP x64
> **日期**：2026-11-12

## 证据标记

| 标记 | 含义 |
|---|---|
| **【已通过源码确认】** | 当前 interop 精确签名 |
| **【已通过实机验证】** | 运行期调用成功（本轮肢体相关 = 0 项） |
| **【尚未验证】** | 只有签名 |
| **【存在风险】** | 可能造成副作用 |

---

## 1. 伤害入口链条（源码确认的候选）

```
① CharacterHitContext.Apply(
       BasicCharacterController target, bool direct, float damage, Vector2 point, Vector2 force,
       CharacterBodyPart part = null, float deathMultiplier = 1f, bool notifyAI = true,
       bool showPanel = true, bool animation = true, bool causeDeath = true, int penetration = 0)
   public static  ← 静态，所有角色共用

② BasicCharacterController.OnDirectedHit(
       float damage, Vector2 point, Vector2 force, CharacterBodyPart part = null, ...)
   public 非虚

③ BasicCharacterController.OnHit(
       float m_damage, Vector2 hitPoint, Vector2 m_force, CharacterBodyPart m_characterBodyPart = null,
       float m_deathMultiplier = 1f, bool notifyAI = true, bool showPlayerOnHitPanel = true,
       bool m_hitAnimationFlag = true, bool hitCauseDeathFlag = true)
   public virtual（Virtual_New）→ HumanCharacterController 覆写

④ CharacterBodyPart.OnHit(
       float m_damage, ref Vector2 m_force, Vector2 hitWorldPosition,
       bool hitAnimationFlag = false, bool highLightEffect = true, bool applyEquipmentDurability = true)
   这是**表现层 + 装备耐久**处理（贴花、材质、护甲覆盖），不是肢体 HP 扣减
```

**调用顺序【推断】**：`① Apply → ② OnDirectedHit → ③ OnHit → ④ CharacterBodyPart.OnHit`。
依据：`Apply` 的参数是 `OnDirectedHit` 的超集（多 `direct`），而 `OnHit` 是它们的虚分派终点。
**这是推断，不是事实** —— 方法体是 IL2CPP 桩，无法直接确认。

### 1.1 关键空白：**肢体 HP 到底在哪一步被扣？**

源码中**没有**形如 `DamageBodyPart(...)` / `HurtBodyPart(...)` 的公开方法。
`CharacterBodyPartData.hp` 是**裸字段**，任何一步都可能直接写它。

**结论：肢体 HP 的扣减位置【尚未验证】。**
这直接决定了"肢体免伤"能否在 ③ 处拦截 —— **未证实前不得实现**。

---

## 2. 整体 HP 与肢体 HP 的区分（必须分清）

| 维度 | 整体生命 | 肢体生命 |
|---|---|---|
| 数据位置 | `CharacterStatusData.hp / maxhp` | `CharacterBodyPartData.hp / maxHP / defaultHP` |
| 容器链 | `playerCharacter.hp`（属性）→ `characterData.characterStatusData` | `characterData.bodypartsData` → `bodypartAry[i]` |
| 死亡判定 | `hp <= 0` → `CharacterDead(...)` | **不直接导致角色死亡**（【尚未验证】hp=0 的实际后果） |
| 平台期伤害 | `BuffAttr.hpPerSecond`（燃烧/酸/溺水等） | 未见肢体级 DoT 字段 |
| 恢复接口 | `hp` 属性 / `AddBuff(11)` | `HealAllBodyPartInjuries()` / `SetToDefaultBodyPart` / `TickRecover` |

> 用户明确要求"不要把角色整体 HP 与肢体 HP 混为一谈"。
> 本表即为设计红线：**任何肢体功能只允许触碰 `CharacterBodyPartData`，不得写 `CharacterStatusData.hp`。**

---

## 3. 普通伤害 / 持续伤害 / 特殊伤害是否共用逻辑

| 伤害类型 | 路径 | 是否经过 ③ `OnHit` | 等级 |
|---|---|---|---|
| 子弹 / 近战 | `Apply → OnDirectedHit → OnHit`（推断） | **推断为是** | 【尚未验证】 |
| 摔伤 | `GetFallInjuryBodyPart()` 选部位 → 同一路径 | 推断为是 | 【尚未验证】 |
| 车辆撞击 | `OnHitByVehicle(float, Vector2, Vector2)` | **独立入口**（不带部位） | 源码确认 |
| 角色互撞 | `OnHitByCharacter(BasicCharacterController, float, Vector2)` | **独立入口** | 源码确认 |
| 燃烧 / 酸 / 孢子 / 溺水（Buff DoT） | `ApplyBuffStatusTick(BuffAttr, ...)` 写**整体 hp** | **不经过** | 源码确认 |
| 流血（肢体） | `StartBleeding(CharacterBodyPartData, float)` + `bleedingStopTime` | 肢体级，**独立** | 源码确认 |
| 伤口 Debuff（21–24） | Buff 系统（`headWound/torseWound/armWound/legWound`） | 由肢体受伤派生/伴随 | 源码确认 |

**【存在风险】** 至少存在 4 条不同入口（`OnHit` / `OnHitByVehicle` / `OnHitByCharacter` / DoT）。
只在 ③ 处拦截**不足以**实现"肢体完全免伤" —— 车辆撞击与角色互撞会绕过。

---

## 4. 肢体免伤的可用拦截点（按可靠性排序）

| # | 拦截点 | 覆盖范围 | 能否逐部位 | 副作用面 | 评价 |
|---|---|---|---|---|---|
| 1 | `CharacterBodyPartData.maxHP` 抬高 | 只改上限，不拦伤害 | 是 | 会改变"完全恢复"判定与 UI 条比例 | 不是真免伤，**不推荐** |
| 2 | `HumanCharacterController.bodyPartWoundsEnabled` | 若为 true 才受伤 | 全局 | 只有 getter，**不可写** | **不可用** |
| 3 | `CharacterHitContext.Apply` Prefix | 所有走 Apply 的伤害 | **是**（`part` 参数） | **静态方法，影响所有角色**（含僵尸/NPC） | 需严格过滤 `target` |
| 4 | `BasicCharacterController.OnDirectedHit` Prefix | 走 OnDirectedHit 的伤害 | 是（`part`） | 基类，影响 NPC/僵尸 | 需过滤 `isPlayer` |
| 5 | `HumanCharacterController.OnHit` Prefix | 走 OnHit 的伤害 | 是（`m_characterBodyPart`） | 只影响玩家（Human 覆写） | **最小副作用面** |
| 6 | `OnHitByVehicle` / `OnHitByCharacter` | 车辆/互撞 | 否（无部位） | 玩家 | 需与 5 配合 |

**排序结论**：
- **优先 5（`HumanCharacterController.OnHit`）**：只影响玩家，且是虚分派终点；
- **必须叠加 6**：否则车辆撞击、角色互撞仍会伤到肢体；
- **3/4 作为备选**，但**必须过滤目标角色**，否则会保护僵尸和 NPC —— 这是最严重的越权风险。

---

## 5. 特殊伤害与绕过普通入口的情况

| 情况 | 机制 | 影响 |
|---|---|---|
| 车辆撞击 | `OnHitByVehicle` | 绕过 `OnHit` |
| 角色互撞 | `OnHitByCharacter` | 绕过 `OnHit` |
| 摔伤 | `GetFallInjuryBodyPart()` + 常规路径 | 推断走 `OnHit`，【尚未验证】 |
| 持续伤害 | `ApplyBuffStatusTick` 写整体 hp | 不影响肢体（但会杀角色） |
| 爆炸 / 环境 | 可能走 `CharacterHitContext.Apply` | 【尚未验证】 |
| 联机伤害抑制 | `netSuppressMeleeDamage`、`netLimbOnlyBodyPart`、`netVisualOnlyBuffIds` | 游戏自身已有"网络环境下只做表现"的抑制字段 |

**【存在风险】** 存在 `netLimbOnlyBodyPart`（联机时"仅肢体表现"）等字段，
说明**游戏自己在联机下就会抑制部分伤害表现**。若我们在此之上再拦一层，
可能出现"本地无伤、他人看到受伤"的不一致。

---

## 6. 是否会影响其他角色（越权风险）

**会，且风险很高。** 原因：

1. 肢体系统是 `BasicCharacterController` 的**基类能力**，僵尸/NPC 同样有
   `CharacterBodyPartsData`（另有 `CharacterBodyPart_Millipede`、`SnakeBodyPart` 等变体）；
2. `CharacterHitContext.Apply` 是 **public static**，全角色共用；
3. `BasicCharacterController.OnHit` 有 **21 处覆写**（Zombie/Pet/Boss 等）。

**强制规则（写入设计方案）**：
- 只允许 patch `HumanCharacterController.*`（玩家专属覆写）；
- 或 patch 基类时**必须**同时校验 `target == PlayerAccessor.Resolve()` / `__instance.isPlayer`；
- 绝不允许无条件返回 `false` 跳过伤害。

---

## 7. 离线 / 在线判定（沿用 Phase 3A 已验证方案）

```csharp
ZedZone.Net.NetworkManager.isOnline   // static bool  【已通过实机验证 2026-11-12】
ZedZone.Net.NetworkManager.isHost     // static bool  【已通过实机验证】
ZedZone.Net.NetworkManager.isClient   // static bool  【已通过实机验证】
NetworkManager.instance.mode          // 实测值 "Offline"【已通过实机验证】
```
判定式（fail-closed，异常即视为联机）：
```
IsMultiplayer = isOnline || isHost || isClient || (instance != null && mode != Offline)
```

### 7.1 肢体状态是否网络同步（关键结论）

- `MsgPlayerState` 字段：`playerId/posX/posY/moveX/moveY/lookX/lookY/hp/speed/stateFlags/liftAttrId/tick/zLevel/teleportCounter/standVz/exoskeleton*` —— **没有肢体字段**【已通过源码确认】
- 全程序集**没有** `Msg*BodyPart*` / `Msg*Limb*` / `Msg*Wound*` 消息【已通过源码确认】
- → **肢体 HP/伤口不在网络同步范围内**，属本地状态。

**但这不等于联机安全**：
`HumanCharacterController.ApplyBodyPartEffects(ref float totalSpeedGain)` 表明**肢体伤口会影响移动速度**。
若只保护本地肢体，客机速度与主机预期不一致，属于**行为不同步**【存在风险】。

**结论：肢体功能同样必须"联机一律拒绝"**，与 Phase 3A 保持一致。

---

## 8. 关闭保护后能否恢复原有行为

设计上必须满足：
- 拦截补丁**按需安装**（关闭时不装），沿用 Phase 3A 的教训：
  IL2CPP 下"装了补丁 + 首行 return"≠"没装补丁"，参数封送本身就会抛异常；
- 卸载时 `Harmony.UnpatchSelf()`；
- 关闭后必须验证一次"正常受伤"（测试计划 T-07）。

---

## 9. 本轮安全性结论

| 问题 | 结论 |
|---|---|
| 伤害入口是否清楚 | 部分清楚：4 条入口已确认，**肢体 hp 扣减的确切位置未确认** |
| 能否只影响玩家 | 能（`HumanCharacterController` 覆写），但车辆/互撞需额外拦截 |
| 是否存在绕过入口 | **存在**，至少 `OnHitByVehicle` / `OnHitByCharacter` |
| 肢体状态是否同步 | **不同步**（本地状态），但速度影响可能导致表现不一致 |
| 免伤是否可行 | **技术上可行，但风险高**：需证明拦截点可靠 + 覆盖全部入口 + 严格限定玩家 |
| 本轮是否实现 | **没有**。按任务要求只做研究 |

**【存在风险】最重要的未决点**：**肢体 HP 究竟在哪一步被扣减，尚未验证。**
在证明这一点之前，"肢体免伤"不具备实现条件。
