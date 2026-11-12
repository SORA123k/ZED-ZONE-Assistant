# PHASE_3B_2A_2_BUFF_AUDIT.md — Buff 清除审计（R13）

> **实验**：`HealAllBodyPartInjuries()` 调用前后的 Buff 全表对比
> **游戏**：ZED ZONE 1.0.16 / build 26600653
> **日期**：2026-11-13
> **性质**：实机实验（单变量，只调用一次该接口）

## 证据标记

**【实机验证】** / **【源码推断】** / **【人工观察】** / **【尚未验证】**

---

## 0. 结论

| 项 | 结论 | 等级 |
|---|---|---|
| 是否清除 Buff | ✅ 是（3 → 0） | 【实机验证】 |
| 被清除的是哪些 | **35 torseRecovery / 37 legRecovery / 36 armRecovery** | 【实机验证】 |
| 它们是否属伤口恢复系统 | ✅ **是**，正是 `GetRecoveryBuffId` 映射的 34–37 恢复 Buff 家族 | 【实机验证】+【源码推断】 |
| 是否清除了"与肢体恢复无关"的状态 | ❌ **未观察到**（本次只有这 3 个 Buff，全部是恢复类且已过期） | 【实机验证】（样本有限） |
| 是否有 Buff 被替换/重建 | ❌ 没有（调用后为 0，后续 10 秒内仍为 0） | 【实机验证】 |

**关键发现：被清除的 3 个 Buff 的剩余时间都是负数（已过期），属于"过期残留"，
不是新增的、正在生效的 Buff。**

---

## 1. 实现方式（读取了什么）

| 数据 | 来源 | 说明 |
|---|---|---|
| Buff 列表 | `player.characterData.buffDataList`（`Il2CppSystem.Collections.Generic.List<BuffData>`） | 【源码确认】 |
| Buff ID | `BuffData.buffId`（int） | 【源码确认】 |
| 结束时间 | `BuffData.buffEndtTime`（float，游戏源码拼写如此） | 【源码确认】 |
| 剩余时间 | `buffEndtTime - Time.time` | 计算 |
| 名称 | `BuffManager.instance.GetBuffAttrByID(id).buffName` | 【源码确认】 |
| 是否负面 | `BuffAttr.isDebuff` | 【源码确认】 |
| 所属对象 | 该 `CharacterData` 属于本地玩家 → `owner=player` | 【源码确认】 |

> 按要求**没有**只比较 `buffCount`，而是逐条记录 ID / 名称 / 剩余时间 / 归属。

---

## 2. 调用前：完整 Buff 表

```
[limbx] BEFORE buffCount=3
[limbx] BEFORE buff[0] id=35 name=torseRecovery debuff=0 end=2.71  remain=-5.16s owner=player
[limbx] BEFORE buff[1] id=37 name=legRecovery   debuff=0 end=2.71  remain=-5.16s owner=player
[limbx] BEFORE buff[2] id=36 name=armRecovery   debuff=0 end=2.78  remain=-5.09s owner=player
```

| # | ID | 名称 | 类型 | 剩余 | 含义（来自 3A 的 62 条 Buff 表） |
|---|---|---|---|---|---|
| 0 | 35 | torseRecovery | 非 Debuff | **-5.16s（已过期）** | 躯干恢复 |
| 1 | 37 | legRecovery | 非 Debuff | **-5.16s（已过期）** | 腿部恢复 |
| 2 | 36 | armRecovery | 非 Debuff | **-5.09s（已过期）** | 手臂恢复 |

**没有出现任何"与肢体无关"的 Buff**（如饥饿/疲劳/感染类）——本次样本中它们本来就不存在。

---

## 3. 调用后

```
[limbx] AFTER buffCount=0
```

**3 个全部消失，没有新增，没有替换。**
后续 T+2s / +4s / +6s / +10s 快照中 `buffCount` 始终为 **0**（未被重新创建）。

---

## 4. 与恢复系统的关系（交叉验证）

Phase 3B-2A 已通过只读实验证实：

```
GetRecoveryBuffId(0)=34  GetRecoveryBuffId(1)=35  GetRecoveryBuffId(2)=36
GetRecoveryBuffId(3)=36  GetRecoveryBuffId(4)=37  GetRecoveryBuffId(5)=37
```

本次被清除的 **35 / 36 / 37** 正好落在这个映射里（躯干/手臂/腿部）。
而当时的受伤部位是 **躯干、左臂、左腿、右腿** —— 与 35/36/37 完全对应。

→ **`HealAllBodyPartInjuries()` 清除的是"这些部位对应的恢复 Buff"**，语义上是合理的收尾动作。

---

## 5. 不能据此断言的部分（重要）

| 想断言 | 能否 | 原因 |
|---|---|---|
| "它会清除所有 Buff" | ❌ **不能** | 本次只有 3 个 Buff，且全是恢复类；没有反例样本 |
| "它只清除恢复类 Buff" | ⚠ **只是本次观察** | 需要构造"同时存在无关 Buff（如饥饿/感染/增益）"的场景再测 |
| "它不会动有益 Buff" | ❌ **未验证** | 没有有益 Buff 样本 |

**因此 R13 只降到"已缓解"，不能标为"完全排除"。**

---

## 6. 剩余时间为何是负数

`remain = buffEndtTime - Time.time` 为 **-5.16s / -5.09s**，说明这些 Buff 的结束时间
**早于当前时间**，但**仍留在列表里**。

可能原因（未验证）：
- 恢复 Buff 到期后未被及时清理，属于"惰性清理"；
- `HealAllBodyPartInjuries()` 顺带做了一次清理。

**不影响结论**：无论原因如何，被清除的都是**已过期的恢复类 Buff**，不是正在生效的状态。

---

## 7. 对设计的直接影响

1. "恢复全部肢体"会**顺带清掉过期的恢复 Buff** —— 对玩家无害，甚至是"清理垃圾"；
2. 但 UI 文案**不应**宣称"只会恢复 HP，不影响任何状态"；
3. 若将来发现它也会清**正在生效**的有益 Buff，则必须在 UI 上明确警告。

---

## 8. 未验证清单

| # | 项 | 原因 |
|---|---|---|
| 1 | 存在无关有益 Buff 时是否被清除 | 未构造该场景 |
| 2 | 存在正在生效的恢复 Buff 时行为 | 本次样本均已过期 |
| 3 | 是否清 Debuff（如感染/流血） | 本次没有此类 Buff |
| 4 | 网络同步（MsgPlayerBuff）是否受影响 | 联机不测 |
