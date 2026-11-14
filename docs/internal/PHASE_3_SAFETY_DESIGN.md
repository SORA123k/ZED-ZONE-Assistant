# PHASE_3_SAFETY_DESIGN.md — 写入安全设计

> **阶段**：Phase 3 安全设计（**本轮不实现**，本文是 Phase 3A 的必须遵循契约）
> **游戏**：ZED ZONE **1.0.16**
> **触发原因**：项目要求"所有玩家数据修改都必须有统一的安全检查入口"，
> 且第一版只支持离线单机。
> **日期**：2026-11-12

---

## 1. 设计原则（不可协商）

| # | 原则 | 落地方式 |
|---|---|---|
| P1 | **单一写入门** | 任何写操作只能走 `PlayerWriteGate.TryWrite(...)`，其余路径在代码评审中一律拒绝 |
| P2 | **失败即拒绝（fail-closed）** | 任何一项安全检查无法确定 → 拒绝执行，不"猜测放行" |
| P3 | **联机一律拒绝** | 不做"仅主机放行"，因为玩家自身数值在联机中是客户端自报，改了会广播给队友 |
| P4 | **只写运行时资源，不写属性/配置/存档** | 禁止 `skillProperty_*`、禁止 `GameConfigs`、禁止存档文件 |
| P5 | **关闭功能不得回写数值** | `Disable()` 只停止写，绝不把数值重置为默认 |
| P6 | **幂等写，不做累加** | 写"目标值"而非"增量"（防 `Update()` 一帧多次派发导致翻倍） |
| P7 | **可审计** | 每次写入/拒绝都写日志（去重），包含原因与前后值 |
| P8 | **不隐藏、不伪装** | 不绕 1.0.16 的模组安全检测，不改 DLL 元数据/进程名 |

---

## 2. 统一写入门（PlayerWriteGate）

### 2.1 调用契约

```
WriteResult TryWrite(StatId stat, float target, WriteSource source)

stat   ∈ { Hp, Energy, Fatigue, Hunger, Thirst }
source ∈ { ManualRestore, AutoRestore, Lock }
```

**返回结果（穷举，必须全部有日志）**：

| 结果 | 含义 |
|---|---|
| `Ok` | 已写入（附 before/after） |
| `SkippedNoChange` | 目标值与当前值差 < ε，未写（省一次封送） |
| `RejectedDisabled` | 该功能被配置关闭 |
| `RejectedMultiplayer` | 检测到联机 |
| `RejectedNoPlayer` | 玩家对象为 null 或已销毁 |
| `RejectedDead` | `deadFlag` / `netDeadFlag` 为 true |
| `RejectedRespawnWindow` | 在重生无敌窗口内 |
| `RejectedOutOfRange` | 目标值不合法（NaN/Inf/越界） |
| `RejectedNotValidated` | 该 stat 尚未通过对应 Gate（如 Fatigue 未过 G2） |
| `Failed` | 调用抛异常（附异常类型） |

### 2.2 固定检查顺序（不得调换）

```
1. 配置开关（Enabled）                            → RejectedDisabled
2. 联机状态（fail-closed）                        → RejectedMultiplayer
3. Gate 验证状态（如 Fatigue 需 G2）               → RejectedNotValidated
4. 玩家对象（每次重新 Resolve + 判空）             → RejectedNoPlayer
5. 死亡标志 deadFlag / netDeadFlag                → RejectedDead
6. 重生无敌窗口                                   → RejectedRespawnWindow
7. 目标值合法性 + 钳制                             → RejectedOutOfRange
8. 场景一致性（场景变化则丢弃旧目标、重建上下文）     → 重试一次
9. 写入（属性 setter）                             → Ok / Failed
10. 回读校验（写完立刻读一次，偏差过大记警告）        → 可选但推荐
```

**为什么先查联机再查玩家**：联机判定是静态字段，不需要玩家对象，最便宜且最重要。

---

## 3. 联机门禁（**已运行期验证**）

### 3.1 信号（B 级，实测日志）

```
[diag] NetworkManager.isOnline = False | isHost = False | isClient = False
[diag] NetworkManager.instance = ok | mode(int)=Offline
```

| 成员 | 签名 | 等级 |
|---|---|---|
| `ZedZone.Net.NetworkManager.isOnline` | `public static bool` | **B（已确认可调用）** |
| `ZedZone.Net.NetworkManager.isHost` | `public static bool` | **B** |
| `ZedZone.Net.NetworkManager.isClient` | `public static bool` | **B** |
| `ZedZone.Net.NetworkManager.instance` | `public static NetworkManager` | **B** |
| `NetworkManager.mode` | `public NetMode`（实测值 `Offline`） | **B** |

### 3.2 判定逻辑（fail-closed）

```
IsMultiplayer =
      NetworkManager.isOnline
   || NetworkManager.isHost
   || NetworkManager.isClient
   || (NetworkManager.instance != null && NetworkManager.instance.mode != NetMode.Offline)
```

- **任一读取抛异常 → 视为联机（拒绝）**，并记一条 `WarnOnce`。
- 每次写操作前**实时求值**，不缓存结果（联机状态可在运行中变化）。
- **不使用** `NetSim.isSimulationAuthority` 作为判据：主机与单机都为 true，
  会在开房时被误判为单机而放行。

### 3.3 为什么联机必须硬拒绝（不是"保守"，是机制原因）

- 世界模拟主机权威，但**玩家自身 hp/视野内 buff 是客户端自报**（`MsgPlayerState.hp` → 其他端
  `ApplyPuppetHp` 只写影子），**没有主机回写本地 hp 的路径**。
- `NetStateAudit` 的审核域是 `chunks/monsterIds/itemIds/vehicleIds/modSetHash`，
  **不含玩家数值** → 改值**不会被哈希判定为异常**。
- 因此真实后果不是"被覆盖"，而是：**假值被广播给队友、写进存档、与派生缓存矛盾**。
  这比被检测更糟，且影响他人体验。

**结论**：第一版联机一律拒绝，不提供任何"强制启用"开关。

---

## 4. Energy 共享数据源的写入协调（Stamina / Energy）

`energy` 同时是 STAMINA（持久力）与 ENERGY（精力）的底层数值（**B 级证据**：
`StaminaPotion` 的 `energyPerSecond = +3`、本地化 5431 `stamina (energy)`）。

### 4.1 协调器规则

```
EnergyCoordinator:
  * 全插件只有一处可写 energy；
  * 维护单一 target（不是两个模块各一个 target）；
  * 每 tick 最多写一次；
  * 注册优先级：Lock > AutoRestore > ManualRestore > 无；
  * 同一 tick 多个请求 → 取优先级最高的 target，其余丢弃并记 debug 日志；
  * 模块只提交"请求"，不直接写。
```

### 4.2 禁止事项

- ❌ Stamina 模块与 Energy 模块各自写不同目标值（会造成每帧抖动）
- ❌ 两个模块都做"每 tick 写满"（重复封送、双倍开销）
- ❌ 用"累加"式恢复（`energy += 5`）——`Update()` 一帧多次派发时会翻倍
  （这是旧 FPS 项目实测踩过的坑：FPS 显示翻倍）

### 4.3 UI 表现

- 「体力/精力」页只显示**一个** `energy` 数值 + 一个说明
  「游戏内 STAMINA 与 ENERGY 为同一数值（本地化 5431）」
- 两个功能入口可以各自有开关，但**最终指向同一 target**；
  若两者同时开启，日志记录仲裁结果。

---

## 5. 死亡 / 重生 / 场景守卫

| 守卫 | 成员 | 等级 | 规则 |
|---|---|---|---|
| 死亡标志 | `deadFlag`（属性） | **A** | true → `RejectedDead` |
| 联机死亡影子 | `netDeadFlag`（字段） | **A** | true → `RejectedDead` |
| 重生无敌窗口 | `netRespawnInvincibleUntil`（float） | **A** | `Time.time < 该值` → 拒绝 |
| 玩家引用 | 每次 `Resolve()` + `(UnityEngine.Object)null` 判断 | **A** | null → `RejectedNoPlayer` |
| 场景 | `SceneManager.GetActiveScene().name` | **A** | 变化 → 重置上下文，重取引用后重试一次 |

**设计决定**：死亡时**不**尝试"写入保命"。保命属于 3B 的"生命值保护"，
必须是"事件拦截 + 钳制"的组合，而不是在死亡后写 hp —— 那会造成 0 血存活的状态错乱。

---

## 6. 数值边界与方向

| 资源 | 下限 | 上限来源 | 方向 | 状态 |
|---|---|---|---|---|
| hp | 0 | `player.maxHP`（另存 `statusData.maxhp`） | 越高越好 | A（方向 C 推断） |
| energy | 0 | `statusData.maxEnergy` | **越高越好** | **B（间接证据）** |
| hunger | 0 | `statusData.maxHunger` | **越高越饱** | **B（间接证据）** |
| thirst | 0 | `statusData.maxThirst` | **越高越好** | **B（间接证据）** |
| **fatigue** | 0 | `statusData.maxFatigue` | **未知** | **C（未决，G2 阻塞）** |

**钳制规则**：

```
target = clamp(target, 0, maxSource)
if float.IsNaN(target) || float.IsInfinity(target) → RejectedOutOfRange
if maxSource <= 0 → 不写，记警告（上限异常）
```

**上限不是固定 0–100**：由 `CharacterData.SetMaxHP/SetMaxEnergy/...` 依 Constitution 计算。
**禁止硬编码 100**。

---

## 7. 关闭功能的语义（用户明确要求）

| 操作 | 正确行为 | 错误行为（禁止） |
|---|---|---|
| 关闭"一键恢复" | 停止提供按钮 | 把数值回滚到开启前 |
| 关闭"锁值" | 停止写 | 把值重置为默认/满值 |
| 关闭插件 | 停止一切写入 | 写一次"恢复原版"数值 |
| 卸载插件 | 什么都不做 | 在 `Unload()` 里改数值 |

**理由**：开启期间玩家可能正常消耗/受伤，回滚旧值会凭空制造状态跳变。

**唯一允许的"还原"**：如果实现过对**全局静态**的临时修改，必须还原 ——
但本项目**禁止修改 `GameConfigs`**，所以当前不存在需要还原的全局状态。

---

## 8. 失败处理与审计日志

### 8.1 日志规范

- 每次 `Ok`：`[write] stat=energy before=12.3 after=100.0 src=ManualRestore`（INFO）
- 每次拒绝：`[write.reject] stat=hp reason=RejectedMultiplayer`（WARN，**去重**，同一原因 60s 内只打一次）
- 每次异常：`[write.error] stat=hunger Type=NullReferenceException`（ERROR，`ErrorOnce` 去重）
- 汇总：每 N 秒一条 `[write.stats] ok=3 rejected=12 failed=0`

### 8.2 去重原则（继承旧项目）

- key = `原因 + stat`，**不含消息文本**（消息会变，去重会失效）
- 高频拒绝（如联机持续拒绝）只打第一次 + 每分钟一次，避免日志本身变成卡顿源
  （旧项目实测：某社区模组日志占了 `LogOutput.log` 的 50.5% 行）

---

## 9. 禁止清单（代码评审红线）

| 禁止 | 原因 |
|---|---|
| 写 `characterStatusData.*` 裸字段 | 绕过 setter，最容易造成 UI/判定不一致 |
| 写 `GameConfigs.*` 全局静态 | 影响所有存档与难度；用户明确禁止 |
| 写 `skillProperty_*`（角色属性） | 属于角色成长数据，等同改存档 |
| 修改存档文件 / 结构 | 明确禁止 |
| `RemoveAllBuffs()` | 会清掉正面 Buff |
| 在非主线程调用任何游戏 API | IL2CPP 互操作线程约束 |
| 用增量写（`x += n`） | 一帧多次派发会翻倍 |
| 在 `Unload()` 中改数值 | 卸载不应改变玩家状态 |
| 任何形式的 DLL 伪装/隐藏 | 规避官方安全检测，明确禁止 |
| 修改其他玩家数据 | 明确禁止 |

---

## 10. 与 1.0.16 模组安全检测的关系

1.0.16 新增 `MainMenuController.ShowModRiskWarningWhenIdle`（游戏日志实证），
启动时弹「安全提示：检测到带有可执行文件的MOD」并列出 BepInEx 与各 DLL。

**本项目立场**：

- **不隐藏** DLL、不改文件名、不伪造签名、不尝试绕过该提示；
- 该检测只影响"提示"，不影响加载；
- 若将来该检测升级为"阻断"，本项目应**如实失败并告知用户**，而不是规避。

---

## 11. 数据流（Phase 3A 目标形态）

```
UI 按钮 / 自动触发器
        │
        ▼
PlayerWriteGate.TryWrite(stat, target, source)
        │
        ├─ 1 配置开关 ──────────► RejectedDisabled
        ├─ 2 联机门禁 ──────────► RejectedMultiplayer
        ├─ 3 Gate 验证 ─────────► RejectedNotValidated
        ├─ 4 重取玩家 ──────────► RejectedNoPlayer
        ├─ 5 死亡标志 ──────────► RejectedDead
        ├─ 6 重生窗口 ──────────► RejectedRespawnWindow
        ├─ 7 钳制/校验 ─────────► RejectedOutOfRange
        └─ 8 写属性 setter ─────► Ok / Failed
                    │
                    ▼
          审计日志（去重） + GUI 状态反馈
```

---

## 12. Phase 3A 实现前必须确认的安全相关 Gate

| Gate | 与安全的关系 | 未通过的后果 |
|---|---|---|
| G1（setter 生效且不破坏状态） | 决定能否用 R2 直接写 | 可能造成 UI/死亡判定不一致 |
| G2（fatigue 方向） | 决定 Fatigue 是否可写 | 拉满疲劳 = 让玩家更难受 |
| G5（联机 fail-closed） | 决定联机安全 | 可能影响他人体验 |
| G6（死亡/重生/切场景守卫） | 决定稳定性 | 可能卡死或状态错乱 |

**G5 的验证方式**：在单机下用日志确认 `isOnline=False`；开房/加入后确认
`isOnline=True` 且所有写操作返回 `RejectedMultiplayer`。**这一项必须实测**，
因为它决定本项目是否会对他人造成影响。
