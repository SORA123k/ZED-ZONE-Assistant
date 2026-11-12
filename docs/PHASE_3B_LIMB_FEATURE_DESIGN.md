# PHASE_3B_LIMB_FEATURE_DESIGN.md — 肢体功能设计

> **阶段**：Phase 3B 第三阶段（设计，**未实现**）
> **游戏**：ZED ZONE 1.0.16 / build 26600653
> **基线版本**：v0.3.0（一键恢复）—— 本设计**不修改**其任何行为
> **日期**：2026-11-12

## 证据标记

**【已通过源码确认】** / **【已通过实机验证】** / **【尚未验证】** / **【存在风险】**
（本轮肢体相关实机验证 = **0 项**，全部为源码确认或未验证）

---

## 1. 设计原则（从 Phase 3A 继承，不重复踩坑）

| # | 原则 |
|---|---|
| P1 | **单一写入门**：肢体写入必须走新的 `LimbWriteGate`，与 `PlayerWriteGate` 并列、互不越界 |
| P2 | **禁止硬编码肢体 ID**：必须先枚举 `bodypartDataAry`，把 ID→名称 打到日志核对 |
| P3 | **禁止假设统一上限**：每个肢体读自己的 `maxHP`（`humanBodyPartDefaultHPAry` 已证明逐部位不同） |
| P4 | **只升不降**：恢复不把 hp 改小；hp ≥ maxHP 时跳过 |
| P5 | **上限不可靠即拒绝**（沿用 `RangeSelection` 的思路） |
| P6 | **联机一律拒绝**（fail-closed，沿用已验证的 `NetworkManager` 判定） |
| P7 | **不写整体 HP**：肢体功能绝不触碰 `CharacterStatusData.hp` |
| P8 | **保护必须按需装补丁**，关闭即卸载（IL2CPP 下"装了但 early-return"≠"没装"） |
| P9 | **不修改 GameConfigs / 存档结构 / 其它角色** |

---

## 2. 数据访问设计

### 2.1 读取链（每次重新解析，零缓存）

```
PlayerAccessor.Resolve()                      // 已有，禁止缓存
  → player.characterData                      // CharacterData
      → characterData.bodypartsData           // CharacterBodyPartsData
          → bodypartDataAry[i]                // CharacterBodyPartData（逐肢体）
```

### 2.2 建议接口（设计示例，**尚不存在于代码中**）

```csharp
internal sealed class LimbSnapshot {
    int    Index;            // 数组下标（= SetToDefaultBodyPart 的参数）
    int    BodyPartId;       // CharacterBodyPartData.bodyPartId
    int    TextId;           // bodyPartTextId → TextManager.GetText(id) 得本地化名
    string Name;             // 解析失败显示 "未知(TextId=xx)"，不伪造
    float  Hp;
    float  MaxHp;
    float  DefaultHp;
    bool   IsWounded;        // 派生只读属性
    bool   IsBleeding;       // 派生只读属性
    bool   IsFullyRecovered; // 派生只读属性
    string MaxSource;        // "CharacterBodyPartData.maxHP"
    bool   Reliable;         // MaxHp > 0 且非 NaN/Inf
    string Error;            // 不可靠原因
}

internal static class LimbData {
    internal static List<LimbSnapshot> ReadAll(HumanCharacterController p, out string error);
    internal static LimbSnapshot ReadOne(HumanCharacterController p, int index, out string error);
}

internal static class LimbWriteGate {
    internal static WriteResult RestoreAll();        // 全部肢体
    internal static WriteResult Restore(int index);  // 指定肢体
    internal static WriteResult SetProtection(int index, bool on); // Phase 3B 后期
}
```

**本地化名称**：`TextManager.GetText(int textId)`【已通过源码确认存在】→
部位名可直接取游戏官方翻译（HEAD/TORSO/LEFT ARM…），**无需自己维护名称表**。

---

## 3. 功能 A：肢体恢复

### 3.1 全部肢体恢复

**实现优先级**：

| 顺序 | 手段 | 依据 | 风险 |
|---|---|---|---|
| **A1（首选）** | 原生 `HealAllBodyPartInjuries()` | 游戏自带，覆盖全部肢体，最省事 | 【尚未验证】是否清流血/绷带 |
| A2（备选） | 逐部位 `SetToDefaultBodyPart(i)` | 原生、按索引 | 【尚未验证】是否等同"回满" |
| A3（兜底） | 逐部位写 `bodypartData.hp = maxHP` | 字段可写，`isWounded` 是派生会自动更新 | 绕过原生逻辑，可能漏绷带/流血处理 |

**设计决策**：先 A1；若 A1 不能完全恢复，退回 A2；A3 仅在 A1/A2 都被证明无效时启用，
且必须做"写后重新读取校验"。

### 3.2 单独恢复指定肢体

| 顺序 | 手段 | 说明 |
|---|---|---|
| **B1（首选）** | `SetToDefaultBodyPart(int bodypartIndex)` | 原生逐部位 |
| B2（备选） | `RefreshBodyPartRecoveryBuff(int bodyPartId)` | 走恢复 Buff（34–37），是渐进恢复 |
| B3（兜底） | `bodypartData.hp = maxHP` | 直接写字段 |

**参数注意**：
- `SetToDefaultBodyPart` 接收 **index**（数组下标）
- `RefreshBodyPartRecoveryBuff` 接收 **bodyPartId**
- 两者**不是同一个值** → 必须分别取，不能混用【存在风险】

### 3.3 动态最大值与拒绝规则

```
每次点击：
  1. 重新 Resolve 玩家 → 重新取 bodypartsData → 重新取该肢体的 maxHP
  2. 校验：maxHP 有限、> 0、>= 当前 hp（否则该肢体上限不可靠 → 拒绝该肢体，其余继续）
  3. 目标 = maxHP；若 hp >= maxHP-ε → SkippedNoChange
  4. 执行恢复
  5. **重新读取** hp 并校验，偏差过大 → 记警告
```

**绝不允许**：用 100 / 1000 之类常量当肢体上限（已由 `humanBodyPartDefaultHPAry` 证明逐部位不同）。

---

## 4. 功能 B：肢体保护（**风险最高，需先过验证门**）

### 4.1 现状约束（来自 `PHASE_3B_LIMB_DAMAGE_FLOW.md`）

- **没有**可写的原生"逐部位免伤"开关（`bodyPartWoundsEnabled` 只有 getter）
- 至少 4 条伤害入口；`OnHit` 之外还有 `OnHitByVehicle` / `OnHitByCharacter` / DoT
- **肢体 HP 的扣减位置尚未验证** ← 这是硬阻塞

### 4.2 候选实现方案（按副作用从小到大）

| 方案 | 拦截点 | 覆盖 | 逐部位 | 必须满足的前提 |
|---|---|---|---|---|
| **L1** | `HumanCharacterController.OnHit` Prefix（`ref m_damage`） | 走 OnHit 的伤害 | ✅（`m_characterBodyPart`） | 证明肢体 hp 是在 OnHit 内扣的 |
| **L2** | 追加 `OnHitByVehicle` / `OnHitByCharacter` Prefix | 车辆/互撞 | ❌ 无部位信息 | 需定义"全部保护时才拦" |
| **L3** | `CharacterHitContext.Apply` Prefix（`target`, `part`） | 最广 | ✅ | **必须过滤 target == 本地玩家**，否则保护僵尸 |

**推荐组合**：**L1 + L2**（只动玩家类），**不用 L3**（static 共用，越权风险最大）。

### 4.3 保护开关语义

- 每个肢体一个开关（数组 + 配置位掩码）
- "全部保护"= 全开
- 关闭后必须 `UnpatchSelf()`，并**实测一次正常受伤**
- 保护期间：`hp` 不下降、不产生流血（若流血由伤害触发则自然不触发）

### 4.4 【存在风险】必须提前声明

1. 若肢体 hp 扣减**不在** OnHit 内，L1 会"看起来有效但实际无效" → 必须以实测证明；
2. 拦截 `OnHit` 可能同时抑制**受击动画/音效/AI 反应**（参数 `m_hitAnimationFlag`/`notifyAI`），
   设计上**只改 `m_damage`，不动其它参数**；
3. 联机下 `netLimbOnlyBodyPart`、`netSuppressMeleeDamage` 已存在游戏自己的抑制逻辑，
   叠加我们的一层可能产生"本地无伤、同伴看到受伤"的不一致 → **联机一律禁用**。

---

## 5. 功能 C：肢体状态显示

| 项 | 设计 |
|---|---|
| 位置 | 新增「肢体」标签页（不动现有 7 个页） |
| 每个肢体一行 | `名称  hp/maxHP  [受伤][流血][完全恢复]` |
| 名称 | `TextManager.GetText(bodyPartTextId)`；失败显示 `未知(TextId=xx)` |
| 进度条 | 用 hp/maxHP，**不用整体血量比例** |
| 数据缺失 | 显示 `未知` / `读取失败: <原因>`，**绝不用 0 或假数据** |
| 刷新 | 跟随现有 100ms 读取节流；场景切换/重生后**重新解析玩家**（现有 `PlayerAccessor` 已保证） |
| 联机 | 顶部红字提示"联机模式：修改功能已禁用"，显示仍只读保留 |

**只读展示可以先做**，因为它不写任何数据 —— 但仍需先完成"肢体枚举验证"才知道有几行。

---

## 6. 开发顺序与门禁

| 阶段 | 内容 | 前置门禁 |
|---|---|---|
| **3B-0** | 只读枚举：把 `humanBodyPartCount`、每个 `bodyPartId/textId/maxHP/defaultHP/hp/状态` 打到日志 | 无（只读） |
| **3B-1** | 肢体状态显示（功能 C） | 3B-0 通过 |
| **3B-2** | 肢体恢复（功能 A） | 3B-1 + 原生接口验证 |
| **3B-3** | 肢体保护（功能 B） | 3B-2 + **G-L4 拦截点证明** |

### 6.1 决策门（未通过不得进入对应开发）

| Gate | 问题 | 通过标准 | 阻塞 |
|---|---|---|---|
| **G-L1** | 肢体数量与 ID 映射 | 运行期枚举结果与本地化名称对应，且与游戏原生面板一致 | 3B-1 |
| **G-L2** | 各肢体 maxHP 真实值 | 能读到有限且 >0 的值，且与 UI 条一致 | 3B-2 |
| **G-L3** | 原生恢复接口是否够用 | `HealAllBodyPartInjuries()` 实测能把全部肢体恢复到满（含清流血） | 3B-2 首选路径 |
| **G-L4** | **肢体 HP 在哪扣减** | 通过拦截实验证明：拦 `OnHit` 后肢体 hp 不再下降 | **3B-3（硬阻塞）** |
| **G-L5** | 是否只影响玩家 | 拦截后僵尸/NPC 仍能正常受伤 | 3B-3 |
| **G-L6** | 关闭保护后恢复原样 | 卸载补丁后受伤行为与原版一致 | 3B-3 |
| **G-L7** | 联机拒绝 | 开房/加入后全部肢体写操作被拒 | 3B-2 之后 |

---

## 7. 安全与合规对照（逐条）

| 要求 | 设计如何满足 |
|---|---|
| 仅支持离线单机 | 复用 `NetworkManager` fail-closed 判定，联机全部拒绝 |
| 在线拒绝执行 | `LimbWriteGate` 第二位检查联机（与 `PlayerWriteGate` 同一实现） |
| 不绕过 MOD 安全提示 | 不做任何 DLL 隐藏/伪装 |
| 不修改全局 GameConfigs | 设计中没有该路径 |
| 不修改存档结构 | 只通过游戏属性/方法修改运行期状态；不写存档文件 |
| 不影响其他玩家 | 只 patch `HumanCharacterController`（玩家覆写）；禁止无条件拦截 |
| 不引入未确认的 Harmony Patch | 3B-3 前置 G-L4 实测证明后才允许装补丁 |
| 保留 v0.3.0 一键恢复 | 新代码独立，不改 `PlayerWriteGate` / `RestoreService` |

---

## 8. 明确"本阶段不做"的清单

| 项 | 原因 |
|---|---|
| 肢体恢复代码 | 本轮只设计；且未过 G-L2/G-L3 |
| 肢体保护代码 | 未过 G-L4（扣减位置未证明） |
| 肢体 UI | 未过 G-L1（肢体数量未知） |
| 修改 `CharacterBodyPartData.maxHP` | 会改变"完全恢复"判定与 UI 比例，不属于免伤 |
| 修改 `CharacterStatusData.hp` | 违反 P7，且与肢体无关 |
| 任何新的 Harmony Patch | 本轮禁止 |

---

## 9. 结论

**功能 A（恢复）与 C（显示）在技术上清晰可行**：有原生恢复接口、有逐部位 maxHP、状态是派生属性。

**功能 B（保护）目前不具备实现条件**：核心未知是"肢体 HP 到底在哪一步被扣减"，
以及至少两条绕过入口（车辆/互撞）。在 G-L4 被实测证明之前，**不建议写任何补丁**。

下一步应是 **3B-0 只读枚举**（一个默认关闭的只读日志模块），它同时解决 G-L1/G-L2，
并为 G-L4 的实验提供观测基础。
