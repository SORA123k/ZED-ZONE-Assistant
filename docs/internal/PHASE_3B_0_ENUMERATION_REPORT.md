# PHASE_3B_0_ENUMERATION_REPORT.md — 肢体只读枚举实施报告

> **阶段**：Phase 3B-0（只读枚举，**无任何写操作**）
> **游戏**：ZED ZONE 1.0.16 / build 26600653 / Unity 2023.1.18f1 / IL2CPP x64
> **模组版本**：v0.3.0（Phase 3A 基线保留，一键恢复未改动）
> **日期**：2026-11-12
> **DLL**：79,360 字节，MD5 `D2F843558602F1DBAF2DC4ACDB80FF6E`（构建产物 = 已安装文件）

## 证据标记

**【已通过源码确认】** / **【已通过实机验证】** / **【尚未验证】** / **【存在风险】**

---

## 1. 交付内容

### 1.1 新增模块（只读）

| 文件 | 作用 |
|---|---|
| `src/Core/LimbProbe.cs` | 肢体枚举采集器：读取并打印全部肢体字段；变化合并输出；联机拒绝 |
| `src/Core/ModConfig.cs` | 新增 `[Diagnostics]` 4 个配置项 |
| `src/UI/AssistantPanel.cs` | 在既有 tick 中调用 `LimbProbe.Tick()` |
| `src/UI/PanelRenderer.cs` | 修正面板上过时的 "Phase 2" 标题为 "Phase 3A"（仅文案） |

### 1.2 配置（默认关闭）

```ini
[Diagnostics]
LimbProbe = false              ; 默认关闭，需手动开启
LimbProbeIntervalMs = 500      ; 采样间隔
LimbProbeHeartbeatSec = 30     ; 心跳间隔
LimbProbeChangeFlushSec = 1    ; 变化日志合并间隔
```

### 1.3 采集字段（全部来自运行期对象）

`humanBodyPartCount`、`arrayLen`、`textIdAryLen`、`defaultHPAryLen`、
`appliedBodyPartConstitution`、`scene`、`ptr`、`isPlayer`，以及**每个肢体**：

| 字段 | 来源 |
|---|---|
| index | 遍历 `bodypartDataAry` 的下标 |
| bodyPartId | `CharacterBodyPartData.bodyPartId` |
| textId | `CharacterBodyPartData.bodyPartTextId` |
| name | `TextManager.instance.GetText(textId)` ← **游戏自身本地化** |
| hp / maxHP / defaultHP | 同名字段 |
| isWounded / isBleeding / isFullyRecovered | 派生只读属性 |
| hpFloat | 派生只读属性 |
| visualDataId / match | `GetBodyPartByIndex(i).bodypartDataID` 交叉校验 |

**没有硬编码任何部位名称、数组长度或取值。**

---

## 2. 核心发现（实机，单次运行）

### 2.1 肢体数量与名称（G-L1）

```
humanBodyPartCount=6  arrayLen=6  textIdAryLen=6  defaultHPAryLen=6
```

| idx | bodyPartId | textId | 名称 | hp | maxHP | defaultHP |
|---|---|---|---|---|---|---|
| 0 | 0 | 80 | 头部 | 100 | 100 | 20 |
| 1 | 1 | 81 | 躯干 | 92.55 | 130 | 50 |
| 2 | 2 | 180 | 左臂 | 110 | 110 | 20 → 30 |
| 3 | 3 | 181 | 右臂 | 110 | 110 | 30 |
| 4 | 4 | 182 | 左腿 | 94.13 | 120 | 40 |
| 5 | 5 | 183 | 右腿 | 64.01 | 120 | 40 |

**6 个肢体**，名称由游戏本地化提供（不是按 index 猜的）。

### 2.2 逐部位上限不同（G-L2）

`maxHP` = **100 / 130 / 110 / 110 / 120 / 120** —— 再次证明**不能假设统一最大值**。

### 2.3 `defaultHP` ≠ `maxHP`（重要）

`defaultHP` = 20 / 50 / 30 / 30 / 40 / 40，远小于 `maxHP`。

**结论：`defaultHP` 不能当作"回满目标"。** 它更像"基础值"（未乘成长系数）。
这条直接排除了一个看似合理的错误实现。

### 2.4 状态属性是派生的（B 项核心证据）

| 肢体 | hp/maxHP | hpFloat | isWounded | isFullyRecovered |
|---|---|---|---|---|
| 躯干 | 92.55/130 = 0.7119 | **0.71** | **1** | **0** |
| 左腿 | 94.13/120 = 0.7844 | **0.78** | **1** | **0** |
| 右腿 | 64.01/120 = 0.5334 | **0.53** | **1** | **0** |
| 头部/左臂/右臂 | 满值 | **1** | **0** | **1** |

→ `hpFloat == hp/maxHP`、`isWounded == (hp < maxHP)`、`isFullyRecovered == (hp >= maxHP)`，
**三个属性与 hp 完全同步**，与源码"只有 getter"一致。

> ⚠ **本结论已在 Phase 3B-1 被部分更正**：
> 实机看到 `左腿 115.57/124 = 93%` 时 `isWounded = 0`（显示"正常"），
> 而 `54%`/`69%` 时为 `1`。因此 **`isWounded` 不是 `hp < maxHP`，而是带阈值的派生**，
> 阈值位于 69%–93% 之间（未精确测出）。
> `hpFloat == hp/maxHP` 仍然成立（多次验证一致）。
> 详见 `PHASE_3B_1_TEST_REPORT.md` §4。

### 2.5 实时性（A 项）

```
[limb] CHANGE idx=1 name=躯干 hp=92.55->92.66 | t=23:21:49
[limb] CHANGE idx=4 name=左腿 hp=94.13->94.23 | t=23:21:49
[limb] CHANGE idx=5 name=右腿 hp=64.01->64.11 | t=23:21:49
[limb] CHANGE idx=1 name=躯干 hp=92.66->92.77 | t=23:21:50
```

肢体自然恢复被 500ms 采样**逐次捕获**（约 +0.1/0.5s）。
说明 hp 是**持续更新的实时字段**，没有可见的 Tick 延迟（在采样精度内）。

### 2.6 【存在风险】ID 交叉校验全部不匹配

```
idx 0 → visual[0].bodypartDataID = 1
idx 1 → visual[1].bodypartDataID = 4
idx 2 → visual[2].bodypartDataID = 4
idx 3 → visual[3].bodypartDataID = 5
idx 4 → visual[4].bodypartDataID = 5
idx 5 → visual[5].bodypartDataID = 0
```

**`GetBodyPartByIndex(i)` 的索引空间与 `bodypartDataAry` 不一致**，且不是简单偏移。

**影响（重要）**：
- 数据侧的 `bodyPartId` **不能**用视觉侧 `bodypartDataID` 校验；
- 未来若用 `SetToDefaultBodyPart(int bodypartIndex)`，**必须确认它要的是哪个索引空间** ——
  否则会把"头部"重置成"右腿"。这正是本项目此前 HP 截断 Bug 的同类风险。

> 该交叉校验是主动加入的自检，**它成功暴露了一个真实的不一致**，而不是假装匹配。

---

## 3. 安全性核对（逐条）

| 要求 | 结果 |
|---|---|
| 仅离线单机 | ✅ 采集前调用 `PlayerWriteGate.IsMultiplayer()`，联机直接 return 并一次告警 |
| 联机拒绝执行 | ✅ 代码路径存在；【尚未验证】未在真实联机环境复现 |
| 不修改任何 HP / 肢体状态 | ✅ 源码中无任何对 `hp/maxHP/...` 的赋值 |
| 不调用恢复接口 | ✅ 未调用 `HealAllBodyPartInjuries` / `SetToDefaultBodyPart` / `UseBandage` 等 |
| 不修改 GameConfigs | ✅ 未引用 |
| 不修改存档 | ✅ 未触碰存档文件 |
| 不新增 Harmony Patch | ✅ 全项目 `[HarmonyPatch]` 计数 = **0** |
| 不影响旧 FPS 优化模组 | ✅ 未改动该仓库（仅有另一并行任务产生的未跟踪目录，非本次所为） |
| 不绕过 MOD 安全提示 | ✅ 未做任何隐藏/伪装 |
| 默认关闭 | ✅ `LimbProbe = false` |

---

## 4. 稳定性与日志量

**发现的问题**：初版每次采样都为每个变化输出一行 → 肢体自然恢复时约 **6 行/秒**，属于刷屏。

**已修复**：改为**按秒合并**（`LimbProbeChangeFlushSec = 1`），输出形如：

```
[limb] CHANGE(1s) 1:躯干 hp 92.55->93.53; 4:左腿 hp 94.13->95.03; 5:右腿 hp 64.01->64.91
```

日志量从 ~6 行/秒降到 **≤1 行/秒**（且仅在真有变化时输出）。

> 这个修复是在实机观察后做的，不是预先假设。

---

## 5. 本报告未覆盖的内容

| 项 | 原因 |
|---|---|
| 受伤前后对比 | 需要玩家在游戏中受伤；本会话无法进入存档（见测试报告） |
| 死亡 / 重生 | 同上 |
| 存档重载 | 同上 |
| 联机拒绝实测 | 需真实开房/加入 |
| 视觉组件映射的完整关系 | 新增的 visual dump 需在**有玩家**时才会输出，本轮尚未取得 |

---

## 6. 结论

**3B-0 的采集器已实现并可运行，已取得一份真实枚举数据**：

- 6 个肢体、名称由游戏本地化给出、逐部位上限不同 → **G-L1/G-L2 的核心事实已拿到**；
- 状态属性与 hp 完全同步 → **B 项已验证**；
- 实时性在 500ms 精度内成立 → **A 项已验证**；
- 同时**主动发现了一个真实风险**（视觉索引与数据索引不一致）。

但**完整验收还需在有玩家的会话中重跑一次**（新构建含 visual dump 与日志合并），
并完成受伤/死亡/读档/联机四项测试。**这些未做的项目如实标注为未验证。**
