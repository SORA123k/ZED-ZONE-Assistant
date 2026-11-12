# PHASE_3B_LIMB_SYSTEM_RESEARCH.md — 肢体系统源码研究

> **项目**：ZED ZONE Ultimate Assistant（AppID 1211600）
> **阶段**：Phase 3B 第一阶段（**只读研究，未开发、未改任何代码**）
> **游戏**：ZED ZONE **1.0.16** / Steam build **26600653** / Unity 2023.1.18f1 / IL2CPP x64
> **interop**：`BepInEx\interop\Assembly-CSharp.dll`（2026-11-12 18:37 重新生成，对应当前版本）
> **日期**：2026-11-12
> **当前稳定版本**：v0.3.0（一键恢复）—— 本轮**未修改**

## 证据标记（全文统一）

| 标记 | 含义 |
|---|---|
| **【已通过源码确认】** | 从当前 1.0.16 interop 读到精确类型/字段/签名 |
| **【已通过实机验证】** | 在运行的游戏里实际调用成功并有日志 |
| **【尚未验证】** | 只有签名，运行期未调用过 |
| **【存在风险】** | 已知可能造成副作用的点 |

> 本轮**没有新增任何代码**（按任务要求只做研究与设计），因此肢体系统相关结论
> **全部为【已通过源码确认】，实机验证数量 = 0**。这一点如实标注，不冒充。

---

## 1. 结论速览

| 问题 | 结论 | 等级 |
|---|---|---|
| 是否有可独立受伤的身体部位 | **有**，通过 `CharacterBodyPartData` 数组管理 | 源码确认 |
| 肢体数据存在哪个类 | **`CharacterBodyPartsData`（容器）→ `CharacterBodyPartData`（单肢体）** | 源码确认 |
| 每个肢体是否有独立当前/最大 HP | **有**：`hp`、`maxHP`、`defaultHP` 三个字段 | 源码确认 |
| 各肢体最大值是否相同 | **不同**：`humanBodyPartDefaultHPAry` 是**逐肢体默认 HP 数组** | 源码确认 |
| 受伤/流血等状态 | `isWounded` / `isBleeding` / `isFullyRecovered` 均为**只读派生属性**（无 setter） | 源码确认 |
| 原生肢体恢复方法 | **有**：`HealAllBodyPartInjuries()`、`SetToDefaultBodyPart(int)`、`RefreshBodyPartRecoveryBuff(int)`、`UseBandage(...)`、`TickRecover(...)` | 源码确认 |
| 原生肢体免伤机制 | **没有**可直接写的开关。仅有 `bodyPartWoundsEnabled`（**Protected、只读**）与全局 `GameConfigs.playerInvincible` | 源码确认 |
| 肢体状态是否网络同步 | **否**：`MsgPlayerState` 不含肢体字段；无 `Msg*BodyPart*` 消息 | 源码确认 |
| 肢体数量与 ID 映射 | **尚未验证**（`humanBodyPartCount` 是运行期静态字段，无法静态求值） | 尚未验证 |

---

## 2. 类型地图

```
BasicCharacterController（角色，含玩家 HumanCharacterController）
 ├─ CharacterBodyPart hitBodyPartTemp                     受击选中的肢体组件
 ├─ GetBodyPartByIndex(int) / GetBodyPartIndex(CharacterBodyPart)
 ├─ GetRandomCharacterBodyPartByName(string)
 ├─ GetFallInjuryBodyPart()                               摔伤用哪个部位
 ├─ UpdateBodyParts()
 ├─ OnHit(..., CharacterBodyPart m_characterBodyPart = null, ...)   ← 伤害入口（含部位）
 ├─ OnDirectedHit(damage, point, force, CharacterBodyPart part = null, ...)
 └─ characterData
      └─ CharacterData
           ├─ bodypartsData : CharacterBodyPartsData        ★ 肢体数据容器
           ├─ RefreshBodyPartsStatus()
           ├─ appliedBodyPartConstitution : int
           └─ CharacterBodyPartsData
                ├─ bodypartDataAry : Il2CppReferenceArray<CharacterBodyPartData>  ★ 全部肢体
                ├─ static humanBodyPartCount : int                              ★ 人类肢体数量
                ├─ static humanBodyPartTextIdAry : int[]                        ★ 各肢体本地化文本 ID
                ├─ static humanBodyPartDefaultHPAry : float[]                   ★ 各肢体默认最大 HP
                ├─ GetBodyPartDataByID(int) -> CharacterBodyPartData            ★ 按 ID 取
                ├─ OnCharacterGenerated()
                ├─ OnSleepTimeUpdate(addedTime, recoverMultiplier)              ★ 睡眠恢复
                ├─ OnAwakeTimeUpdate(addedTime, recoverMultiplier)
                └─ TickRecover(addedTime, percentPerDay, recoverMultiplier)     ★ 自然恢复
```

### 2.1 `CharacterBodyPartData`（单肢体，**真正的数据模型**）

| 成员 | 类型 | 读/写 | 说明 | 等级 |
|---|---|---|---|---|
| `bodyPartId` | `int` | 读 | 肢体 ID | 源码确认 |
| `bodyPartTextId` | `int` | 读 | 本地化文本 ID（对应 HEAD=80 / TORSO=81 / LEFT ARM=180 …） | 源码确认 |
| `hp` | `float` **字段** | 读/写 | 当前肢体 HP | 源码确认 |
| `maxHP` | `float` **字段** | 读/写 | 该肢体最大 HP（**逐肢体不同**） | 源码确认 |
| `defaultHP` | `float` **字段** | 读/写 | 默认 HP（恢复基准） | 源码确认 |
| `bandageData` | `ItemData` | 读 | 当前绷带物品 | 源码确认 |
| `bleedingStopTime` | `float` | 读 | 流血停止时间 | 源码确认 |
| `bandageExpireTime` | `float` | 读 | 绷带到期时间 | 源码确认 |
| `hpFloat` | **属性，仅 get** | 只读 | 归一化/显示用 HP | 源码确认 |
| `isBleeding` | **属性，仅 get** | 只读 | 是否流血（**派生**） | 源码确认 |
| `isWounded` | **属性，仅 get** | 只读 | 是否受伤（**派生**） | 源码确认 |
| `isFullyRecovered` | **属性，仅 get** | 只读 | 是否完全恢复（**派生**） | 源码确认 |

> **重要**：`isWounded/isBleeding/isFullyRecovered` **没有 setter**（interop 中只有 `get_` token），
> 说明它们由 `hp/maxHP/bleedingStopTime/bandageExpireTime` 派生。
> 这对设计是好消息：**只改 `hp`，伤口/流血状态会自动重新派生**，不会出现"标志位与数值不一致"。

### 2.2 `CharacterBodyPart`（场景组件，**不是 HP 池**）

public 成员多为表现层：`damageFloat`、`defenseFloat`、`breakable`、`bodypartDataID`、
`bloodColorIntensity`、`bloodAreaSize`、`bodyPartSprites`、`armorOverrides`、
`OnHit(float, ref Vector2, Vector2, bool, bool, bool)`、`UpdateOnHitTexture(...)`、
`TryConsumeEquipmentDurability(...)`、`OnHit_Equipment(float)`。

**结论：这个类是"受击视觉 + 装备耐久 + 护甲覆盖"，不存肢体 HP。**
它通过 `bodypartDataID` 关联到 `CharacterBodyPartData`，通过 `GetBodyPartIndex(part)` 取索引。

> 这一条直接回答用户的警戒："不要把整体 HP 与肢体 HP 混为一谈" ——
> 同时也要注意 `CharacterBodyPart` 与 `CharacterBodyPartData` 是**两个不同层次**，别拿错。

---

## 3. "肢体数量与 ID"目前只能到哪一步

- `humanBodyPartCount` 是 **运行期静态字段**（`static int`），interop 只有签名，**静态分析取不到值** → **【尚未验证】**
- 本地化表给出的人类部位名（【已通过源码确认，来自 `Localization_en_to_ja.csv`】）：

| 文本 ID | 英文 | 日文 |
|---|---|---|
| 80 | HEAD | 頭部 |
| 81 | TORSO | 胴体 |
| 85 | ARM | 腕 |
| 86 | HAND | 手 |
| 180 | LEFT ARM | 左腕 |
| 181 | RIGHT ARM | 右腕 |
| 182 | LEFT LEG | 左脚 |
| 183 | RIGHT LEG | 右脚 |

- 伤口类 Buff 只有 **4 个类别**（21 headWound / 22 torseWound / 23 armWound / 24 legWound），
  对应恢复 Buff 34–37；且存在 `GetRecoveryBuffId(int bodyPartId)` 做"部位 ID → 恢复 Buff"映射
  → 说明**多个部位可映射到同一类别**。

**因此**：真实肢体列表必须**运行期枚举**（`GetBodyPartDataByID(0..humanBodyPartCount-1)`），
**不能**按上面的文本 ID 猜测。设计文档已给出枚举方案，但本轮不实现。

> **【存在风险】** 若按猜测的 ID 直接写 `hp`，可能写到错误的肢体（例如把"左臂"当"头"）。
> 因此设计上**禁止硬编码部位 ID**，必须先枚举并把 ID→名称 打印出来核对。

---

## 4. 状态系统：受伤 / 流血 / 恢复

| 状态 | 载体 | 说明 | 等级 |
|---|---|---|---|
| 受伤 | `isWounded`（派生） | 由 hp 与 maxHP 关系派生 | 源码确认 |
| 流血 | `isBleeding`（派生）+ `bleedingStopTime`（字段） | `StartBleeding(CharacterBodyPartData, float bleedingTime)` 启动 | 源码确认 |
| 绷带 | `bandageData` / `bandageExpireTime` + `UseBandage(...)` / `RemoveBandage(...)` | 玩家用绷带止血 | 源码确认 |
| 完全恢复 | `isFullyRecovered`（派生） | 由 hp 达到上限派生 | 源码确认 |
| 感染 / 骨折 | **未找到"骨折"独立机制**；"感染"在 Phase 1 已确认无独立类型 | 相关负面效果走 Buff（孢子感染 16、伤口 21–24、失血 27） | **尚未验证** |
| 整体 HP 关联 | 肢体受伤与整体 `hp` 是两套：`hp` 在 `CharacterStatusData`，肢体 `hp` 在 `CharacterBodyPartData` | **不要混用** | 源码确认 |

### 4.1 原生恢复路径（全为公开方法）

| 方法 | 签名 | 用途 | 等级 |
|---|---|---|---|
| `HealAllBodyPartInjuries` | `public void HealAllBodyPartInjuries()` | 治疗全部肢体伤口 | 源码确认 |
| `SetToDefaultBodyPart` | `public void SetToDefaultBodyPart(int bodypartIndex)` | 把某索引肢体重置为默认 | 源码确认 |
| `RefreshBodyPartRecoveryBuff` | `public void RefreshBodyPartRecoveryBuff(int bodyPartId)` | 按部位刷新恢复 Buff（34–37） | 源码确认 |
| `GetRecoveryBuffId` | `public static int GetRecoveryBuffId(int bodyPartId)` | 部位 → 恢复 Buff ID | 源码确认 |
| `UseBandage` | `public void UseBandage(CharacterBodyPartData, ItemData)` | 使用绷带 | 源码确认 |
| `RemoveBandage` | `public void RemoveBandage(CharacterBodyPartData)` | 移除绷带 | 源码确认 |
| `TickRecover` | `public void TickRecover(float addedTime, float percentPerDay, float recoverMultiplier)` | 自然恢复（按天百分比） | 源码确认 |
| `OnSleepTimeUpdate` | `public void OnSleepTimeUpdate(float addedTime, float recoverMultiplier = 1f)` | 睡眠时恢复 | 源码确认 |
| `GetBodyPartRecoverMultiplier` | `public float GetBodyPartRecoverMultiplier(bool sleeping)` | 恢复倍率 | 源码确认 |

**这是本轮最重要的好消息**：肢体恢复**有原生接口**，不需要自己写数值逻辑。

### 4.2 原生免伤

| 候选 | 可写性 | 结论 | 等级 |
|---|---|---|---|
| `HumanCharacterController.bodyPartWoundsEnabled` | `Protected_Virtual_New`，**仅 getter** | **不可写**，只能反射读 | 源码确认 |
| `GameConfigs.playerInvincible` / `playerGodMode` | `static bool`（有 setter） | 全局开关，**按项目规则禁止修改** | 源码确认 |
| `CharacterBodyPartData.maxHP` 抬高 | 字段可写 | 等于"提高部位血上限"，非真正免伤 | 源码确认 |
| `OnHit` / `OnDirectedHit` / `CharacterHitContext.Apply` 拦截 | 需 Harmony | 唯一能做到"逐部位免伤"的方式 | 源码确认 + **【存在风险】** |

**结论：不存在"逐部位免伤"的原生开关。** 逐部位保护若要实现，必须在伤害入口拦截
（详见 `PHASE_3B_LIMB_DAMAGE_FLOW.md` 与 `PHASE_3B_LIMB_FEATURE_DESIGN.md`）。

---

## 5. 死亡 / 重生 / 场景 / 读档

| 场景 | 相关成员 | 说明 | 等级 |
|---|---|---|---|
| 死亡 | `PrepareBodyPartsForDeathSplit()`、`dropHitBodyPartOnDeath`、`CharacterBodyPart.deathDestroyFlag` | 死亡时可能拆分肢体（表现层） | 源码确认 |
| 生成 | `CharacterBodyPartsData.OnCharacterGenerated()`、`EnsureHumanBodyParts(CharacterBodyPartsData)` | 生成角色时确保肢体数据存在 | 源码确认 |
| 读档 | `HumanCharacterController.LoadBodyparts()`、`CharacterData.RefreshBodyPartsStatus()` | 读档后重载肢体并刷新状态 | 源码确认 |
| 场景 | 随玩家对象一起重建（肢体数据挂在 `characterData` 上） | 与整体状态一样，**必须每次重新解析玩家** | 源码确认 / 【尚未验证】 |
| 睡眠 | `OnSleepTimeUpdate(...)` | 睡眠推进肢体恢复 | 源码确认 |

**设计含义**：任何肢体功能都必须走现有的 `PlayerAccessor.Resolve()`（不缓存），
并在拿到 `player.characterData.bodypartsData` 后**每次重新校验非空**。

---

## 6. 未验证清单（本轮无法覆盖）

| # | 项 | 原因 |
|---|---|---|
| 1 | `humanBodyPartCount` 实际值 | 静态字段，需运行期读 |
| 2 | 真实肢体 ID → 名称映射 | 需运行期枚举 + 本地化对照 |
| 3 | 各肢体 `maxHP`/`defaultHP` 实际数值 | 需运行期读 |
| 4 | `hpFloat` 的确切归一化方式 | 派生属性，方法体不可读 |
| 5 | `isWounded` 的判定边界（hp < maxHP？阈值？） | 同上 |
| 6 | `HealAllBodyPartInjuries()` 是否清流血/绷带 | 方法体不可读 |
| 7 | `SetToDefaultBodyPart(index)` 是否等同"回满" | 方法体不可读 |
| 8 | `bodyPartWoundsEnabled` 默认值与切换条件 | 只有 getter |
| 9 | 肢体 hp=0 的后果（禁用/断肢/仅表现） | 方法体不可读 |
| 10 | 肢体状态是否写入存档 | 需存档前后对比 |

> 这些**不是"大概是这样"就能糊过去的**：设计文档把每一项都变成 Phase 3B 开发前必须先做的验证项。
