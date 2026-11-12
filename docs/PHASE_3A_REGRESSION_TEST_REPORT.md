# PHASE_3A_REGRESSION_TEST_REPORT.md — 动态最大值修复 · 回归测试报告

> **被测版本**：ZED ZONE Ultimate Assistant **v0.3.0**（含动态范围修复）
> **游戏**：ZED ZONE **1.0.16** / build 26576287
> **DLL**：`ZedZone.UltimateAssistant.dll`，59,904 字节
> MD5 `139EF5B8E1ED532EA0EBBE253DDC2DD2`（构建产物与已安装文件**哈希一致**）
> **日期**：2026-11-12

---

## 0. 结论速览

| 类别 | 结果 |
|---|---|
| **已通过源码验证** | 上限来源改为 `CharacterStatusData.maxhp`；多候选交叉校验；只升不降；不可靠即拒绝 |
| **已通过自动化测试** | **32 / 32 通过**（含 10 项本次 Bug 专项） |
| **已通过游戏实机测试** | HP `1000.21 → 2000`（不再截断）；满值时 `SkippedNoChange`；Energy `178 → 180` |
| **尚未验证** | 升级 / 装备 / 技能 对上限的逐项影响；`fatigue` 方向；跨场景上限值对比 |

---

## 1. 构建

```
$ dotnet build -c Release -v q -nologo
已成功生成。
    0 个警告
    0 个错误
```

安装：复制到 `<游戏目录>\Mods\ZedZone.UltimateAssistant\`，启动校验通过（`[Error]` = 0）。

---

## 2. 自动化测试（离线，无需游戏）

命令：

```
dotnet run --project tests/ZedUaOfflineTests -c Release
```

结果：**passed=32 failed=0 / ALL TESTS PASSED**

### 2.1 原有 22 项（读数展示层，回归无损）

百分比钳制、失败态文本不含数字、无玩家态、快照统计、失败工厂、可疑值仍可用等 —— 全部通过。

### 2.2 新增 10 项（本次 Bug 专项）

| # | 用例 | 期望 | 结果 |
|---|---|---|---|
| 1 | 首选 ≥ 当前值 | `Ok`，选 `maxhp=2000` | ✅ |
| 2 | **首选 1000 < 当前 2000，但 FullHP=2000 可覆盖** | `FallbackUsed`，选 `FullHP=2000` | ✅ |
| 3 | 所有候选 < 当前值 | `Unreliable`（必须拒绝，**绝不截断**） | ✅ |
| 4 | 全无效（0 / 负数 / NaN / Inf） | `Unreliable` | ✅ |
| 5 | 首选无效但第二个有效 | 跳过无效，用第二个 | ✅ |
| 6 | 空候选 | `Unreliable` | ✅ |
| 7 | 当前值 == 上限 | `Ok`（不误判不可靠） | ✅ |
| 8 | 当前值略高于上限（容差内） | `Ok`（不误判） | ✅ |
| 9 | 首选 2000 且当前 1500 | `Ok`，选 `maxhp` | ✅ |
| 10 | 首选在无回退时明确报不可靠 | `Unreliable` + 原因文本非空 | ✅ |

> 这些用例直接复现了"`maxHP=1000` 而真实上限 2000"的组合，是本次 Bug 的**可执行断言**。

---

## 3. 游戏实机测试（用户实际操作）

### 3.1 关键证据（修复后）

```
[range] stat=Hp current=1000.21 min=0 max=2000 source=CharacterStatusData.maxhp
        candidates=[CharacterStatusData.maxhp=2000, BasicCharacterController.FullHP=1000, BasicCharacterController.maxHP=1000]
        reliable=True

[write] stat=Hp before=1000.21 target=2000 after=2000 maxSource=CharacterStatusData.maxhp

[range] stat=Energy current=178 min=0 max=180 source=CharacterStatusData.maxEnergy reliable=True
[write] stat=Energy before=178 target=180 after=180

# 再次点击（已满）
[write.reject] stat=Hp reason=SkippedNoChange detail=当前 2000 已不低于目标 2000
```

### 3.2 判定

| 检查点 | 结果 |
|---|---|
| 是否仍把 2000 截断成 1000 | **否**（`after=2000`） |
| 上限来源是否与原生面板一致（2000） | **是**（`maxhp=2000`） |
| 满值时是否重复写入 | **否**（`SkippedNoChange`） |
| 是否产生异常 | 否（`[Error]` = 0） |
| 是否写入存档/改动全局 | 否 |
| Harmony 补丁数 | 0 |

---

## 4. 用户要求的回归项逐条核对

| # | 回归项 | 要求 | 状态 |
|---|---|---|---|
| 1 | Health 2000/2000 不得被恢复成 1000 | 必须 | ✅ **实机通过**（`after=2000`；满值时跳过） |
| 2 | Hunger 使用当前实际合法上限 | 必须 | ✅ 实机：`max=120`（曾观测 116）且恢复后再次点击跳过 |
| 3 | Thirst 使用当前实际合法上限 | 必须 | ✅ 同上（`max=120` / 116 动态） |
| 4 | Energy 使用真实来源，不与 Stamina 重复写入 | 必须 | ✅ 单一 `energy` 通道；日志 `maxEnergy` 单来源；同源已由相同值证明 |
| 5 | Fatigue 确认方向与恢复目标 | 必须 | ⛔ **未完成**（方向未确认，功能默认关闭并拒绝写入） |

> 第 5 项是**唯一未完成的回归项**，原因不是代码缺陷，而是缺少"冲刺后 fatigue 升/降"的观察。
> 在 `[Restore] FatigueDirection` 仍为 `unconfirmed` 时，点击"恢复疲劳"会返回
> `RejectedNotValidated` 并给出原因 —— 这是**设计上的正确拒绝**，不是失败。

---

## 5. 动态成长测试矩阵（用户要求 8 项）

| # | 测试 | 方法 | 状态 |
|---|---|---|---|
| 1 | 角色初始最大生命值 | 新开档读取 `maxhp` | **未验证**（未开新档） |
| 2 | 升级后最大生命值变化 | 升级前后各读一次 | **未验证** |
| 3 | 装备/属性加成改变最大生命值 | 逐件穿脱对比 `maxhp` | **未验证**（机制存在：`appliedMaxHPConstitution` 等） |
| 4 | 技能/被动改变最大生命值 | 加点前后对比 | **未验证** |
| 5 | 切换场景后重新读取 | 场景前后各点一次恢复，看 `[range]` | **部分验证**（每次点击必重读；未做前后数值对比） |
| 6 | 退出存档再进入后重新读取 | 重进后点一次恢复 | **部分验证**（本次即重进游戏后读到 `maxhp=2000`） |
| 7 | 当前值已满时执行恢复 | 满血点击 | ✅ **实机通过**（`SkippedNoChange`） |
| 8 | 当前值超过读取上限时安全拒绝 | 构造 current > max | ✅ **自动化通过**（`Unreliable`）；实机未复现该组合 |

**说明**：第 1–4 项需要通过"受控成长"操作才能验证，本轮未做，**不标记为支持**。
但代码对它们的处理是统一的：**每次操作前重读最大值**，因此成长发生后下一次点击即生效。

---

## 6. 日志可定位性检查（用户要求 §六）

每次点击必然输出 `[range]`，包含：

| 用户要求 | 是否满足 | 字段 |
|---|---|---|
| 当前角色标识（不含敏感信息） | ✅ | 通过 `[state]` 的指针 + `isPlayer`；不记录账号 |
| 当前场景 | ✅ | `[vitals]` 内含 `场景=` |
| 当前属性值 | ✅ | `current=` |
| 插件读取的最大/最小值 | ✅ | `max=` / `min=` |
| 读取来源/API 名称 | ✅ | `source=CharacterStatusData.maxhp` |
| 原生 UI 对应最大值 | ⚠ **无法可靠取得** | 已在研究文档说明；用"候选列表 + 当前值矛盾"替代 |
| 最终恢复目标 | ✅ | `[write] target=` |
| 写入前后数值 | ✅ | `before=` / `after=` |
| 是否通过一致性校验 | ✅ | `reliable=true/false` |
| 成功/失败原因 | ✅ | `[write.reject] reason=... detail=...` |

---

## 7. 未验证与风险

| 项 | 状态 | 影响 |
|---|---|---|
| 升级/装备/技能对上限的逐项影响 | 未验证 | 不影响正确性（每次重读），但**未证明**在这些操作后仍正确 |
| 跨场景上限值对比 | 部分验证 | 机制上每次重读，风险低 |
| `fatigue` 方向 | 未验证 | 该功能默认关闭，无误操作风险 |
| 原生 UI 最大值自动比对 | 不可实现 | 已用"上限 ≥ 当前值"替代校验 |

---

## 8. 最终判定

| 判定 | 内容 |
|---|---|
| **已通过源码验证** | 上限来源、选源算法、只升不降、拒绝路径 |
| **已通过自动化测试** | 32/32，含 10 项 Bug 专项 |
| **已通过游戏实机测试** | HP 1000.21→2000、满值跳过、Energy 178→180、无异常 |
| **尚未验证** | 成长机制的逐项影响、fatigue 方向、跨场景上限对比 |

**严重 Bug（HP 被截断到 1000）已修复并实机确认不再复现。**
本轮未开发 Phase 3B，等待用户验收。
