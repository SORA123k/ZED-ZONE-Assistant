# PHASE_3A_GROWTH_EXPERIMENT_REPORT.md — 成长只读实验 · 首轮报告

> **工具版本**：ZED ZONE Ultimate Assistant v0.3.0（含 `GrowthProbe`）
> **游戏**：ZED ZONE 1.0.16 / build 26600653
> **日期**：2026-11-12
> **性质**：只读观测报告。**未修改任何属性、未改全局配置、未改存档、未新增 Harmony Patch。**

---

## 0. 结论速览

| 项 | 状态 |
|---|---|
| 探针是否工作 | ✅ **已验证**（产出 INIT / CHANGE / heartbeat 行） |
| 是否影响游戏 | ✅ 无异常（`[Error]` = 0），一键恢复功能正常保留 |
| 首轮数据是否揭示了成长来源 | ✅ **部分**：`maxhp=2000` 而技能/平衡系数均为中性（1.0），说明当前 2000 主要来自 **Constitution** |
| 五个成长实验 | ⏳ **全部尚未执行**（需用户在游戏中操作，见 `PHASE_3A_GROWTH_EXPERIMENT.md` §4） |

---

## 1. 探针实际输出（首轮真实日志）

```
[growth] INIT t=22:58:29 scene=Loading player=null
[growth] CHANGE t=22:58:39 scene=MainMenu player=null
[growth] CHANGE t=22:59:12 scene=MainMenu player=null
[growth] CHANGE t=22:59:22 scene=Gaming  level=N/A coopLevel=0 conGained=2 conExp=100.09
         hp=2000 maxhp=2000 maxHPfield=1000 FullHP=1000
         appliedMaxHPConstitution=20 skillProperty_MaxHPFloat=1 appliedMaxHPSkillFloat=1
         maxHPBalanceFloat=1 appliedMaxHPBalanceFloat=0 skillProperty_StaminaFloat=0
         maxEnergy=180 maxHunger=120 maxThirst=120 maxFatigue=100
```

（实际为单行；此处换行仅为可读）

### 1.1 这行数据说明了什么

| 观察 | 含义 |
|---|---|
| `maxhp=2000` vs `maxHPfield=1000` vs `FullHP=1000` | **再次确认**：控制器上的两个值都不是有效上限（与严重 Bug 报告一致） |
| `hp=2000` 且 `maxhp=2000` | 玩家处于满血；修复后的"只升不降"不会再把它压成 1000 |
| `skillProperty_MaxHPFloat=1`、`appliedMaxHPSkillFloat=1`、`maxHPBalanceFloat=1` | **技能与平衡系数当前都是中性（×1）**，没有贡献额外生命 |
| `appliedMaxHPConstitution=20` | 唯一明显非中性的输入 → **2000 很可能来自体质** |
| `conGained=2` 而 `appliedMaxHPConstitution=20` | 若基础体质为 18，则 18+2=20（【推断】，需实验 1/3 确认） |
| `maxEnergy=180 / maxHunger=120 / maxThirst=120 / maxFatigue=100` | 其它上限的实际值（均非 100 固定值） |
| 场景 `Loading → MainMenu → Gaming` 都产生了 `CHANGE` | 探针能正确捕捉场景与玩家对象变化 |

### 1.2 成长公式：目前有多个候选，**不能定论**

仅凭一个数据点（constitution=20 → maxhp=2000，且 `maxHPfield=1000`），
下列公式**全部能拟合**：

| 候选 | 公式 | 代入 con=20 |
|---|---|---|
| A | `maxhp = base + con × 50` | 1000 + 1000 = 2000 ✅ |
| B | `maxhp = base × (1 + con/20)` | 1000 × 2 = 2000 ✅ |
| C | `maxhp = base × con/10` | 1000 × 2 = 2000 ✅ |

**因此不在代码里写任何公式**，只依赖 `CharacterStatusData.maxhp` 这个"游戏自己算好的结果"。
要区分 A/B/C，需要**第二个数据点**（体质变化后的 `maxhp`）——即实验 1 或实验 3。

> 这也说明为什么修复方案选择"读 maxhp"而不是"自己算 maxhp"：
> 公式未定，自己算一定出错。

---

## 2. 五个实验的执行状态

| # | 实验 | 状态 | 备注 |
|---|---|---|---|
| 1 | 升级前后对比 | ⏳ 未执行 | 需自然升级或加属性点 |
| 2 | 装备更换前后对比 | ⏳ 未执行 | 需逐件穿脱 |
| 3 | 技能/属性变化前后对比 | ⏳ 未执行 | 需加点 |
| 4 | 场景切换前后对比 | 🟡 **部分**：已确认场景变化触发 `CHANGE` 行；尚未做"切换前后 maxhp 数值对比" | 探针已具备能力 |
| 5 | 重新加载存档前后对比 | ⏳ 未执行 | 需存档→退出→读档 |

**没有把未执行的实验写成"通过"。**

---

## 3. 工具质量检查

| 检查项 | 结果 |
|---|---|
| 编译 | 0 警告 0 错误 |
| 启动加载 | 成功，版本 v0.3.0 |
| 运行时异常 | `[Error]` = 0 |
| 是否写数据 | 否（源码中无赋值；仅读字段） |
| Harmony Patch | 0 |
| 日志量 | 默认 10 秒/条（心跳），变化时额外一条 → 不会刷屏 |
| 与已有功能共存 | 一键恢复、只读预览、GUI 均正常 |
| 读不到时是否伪造 | 否，输出 `?`；等级输出 `N/A` |

---

## 4. 日志量评估

- 心跳：每 `GrowthLogIntervalSec`（默认 10s）一条；
- 变化：场景切换、玩家出现/消失、数值成长时各一条；
- 一次 10 分钟游戏约 60~80 行 —— 相比旧项目曾观测到的"某模组占日志 50%"要低两个数量级；
- 若嫌吵可设 `GrowthLog = false`，恢复按钮不受影响。

---

## 5. 如何使用这份工具做实验（简要）

1. 玩游戏到需要记录的时刻，暂停操作 **~10 秒**（等心跳）；
2. 打开 `BepInEx\LogOutput.log`，搜索 `[growth]`；
3. 取**操作前最后一条**与**操作后第一条**，逐字段对比；
4. 重点看：`maxhp`、`appliedMaxHPConstitution`、`skillProperty_MaxHPFloat`、
   `appliedMaxHPSkillFloat`、`maxHPBalanceFloat`；
5. 若 `maxhp` 变了但所有输入都没变 → 说明还有未知来源，需要继续找。

---

## 6. 验证状态汇总（严格区分）

| 类别 | 内容 |
|---|---|
| **已通过源码验证** | 探针只读、字段来源正确、无赋值、无 Patch |
| **已通过自动化测试** | 编译 0 错；构建产物与安装文件哈希一致；既有一键恢复 32/32 单测仍全绿（本轮未改动该逻辑） |
| **已通过游戏实机测试** | 探针实际产出 INIT/CHANGE 行；场景切换被捕捉；`[Error]=0`；`maxhp=2000` 与 `maxHPfield=1000` 共存被如实记录 |
| **尚未验证** | 五个成长实验全部未执行；成长公式未定；等级字段不存在（已如实标注 N/A） |

---

## 7. 与严重 Bug 修复的关系

本轮**没有修改** v0.3.0 的动态范围读取与一键恢复逻辑，因此：

- `CharacterStatusData.maxhp` 仍是唯一上限来源；
- 多候选交叉校验、只升不降、上限不可靠即拒绝 均保持生效；
- 成长实验的作用是**验证**这套读取在成长前后依然正确，而不是替换它。

---

## 8. 下一步（等用户执行实验）

建议优先做 **实验 3（属性加点）**：它最可能一次性区分 A/B/C 三个公式，
并能确认 `appliedMaxHPConstitution` 是否就是 `maxhp` 的唯一驱动项。
完成后把 `[growth]` 前后两行发我（或直接让我读日志），即可补上"成长后仍正确"的验证。
