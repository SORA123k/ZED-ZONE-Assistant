# PHASE_3_ROADMAP.md — Phase 3 路线图与决策门

> **项目**：ZED ZONE Ultimate Assistant（AppID 1211600）
> **阶段**：Phase 3 技术规划（**本轮不实现任何写入功能**）
> **游戏版本**：**1.0.16** / build 26600653
> **interop 状态**：`Assembly-CSharp.dll` 时间戳（2026-11-12 18:37）**晚于** `GameAssembly.dll`（15:48）
> → 已随 1.0.16 重新生成，本文所有签名对应当前版本
> **日期**：2026-11-12

---

## 0. 证据等级约定（全文统一）

| 标记 | 含义 |
|---|---|
| **A｜已确认存在** | 类型/成员在当前 1.0.16 interop 中核实到精确签名 |
| **B｜已确认可调用** | **在运行的游戏里实际调用成功**，有日志证据 |
| **C｜仅反编译发现** | 只有签名，未在运行期调用过 |
| **D｜尚未找到可靠实现** | 没有满足需求的接口 |

> 本项目**不使用**"看起来应该可以"这种表述。凡未在运行期调用过的，一律 C。

### 本轮新增的运行期证据（B 级）

通过一个**默认关闭的只读诊断转储**（`[Diagnostics] DumpBuffTable`）在游戏 1.0.16 主菜单实际取得：

```
[diag] 共解析到 62 条 buff 定义
[diag] GameConfigs.playerInvincible = False
[diag] GameConfigs.playerGodMode = False
[diag] NetworkManager.isOnline = False | isHost = False | isClient = False
[diag] NetworkManager.instance = ok | mode(int)=Offline
[diag] GameController.instance = ok
[diag] playerCharacter = null
```

由此 **B 级**确立：`BuffManager.instance`、`GetBuffAttrByID(int)`、`BuffAttr.*` 字段、
`GameConfigs.playerInvincible/playerGodMode`（读）、`NetworkManager.isOnline/isHost/isClient`、
`NetworkManager.instance`、`NetworkManager.mode`（值 `Offline`）、`GameController.instance`。

**仍未取得 B 级**：任何与**玩家对象**相关的调用（主菜单下 `playerCharacter == null`，
而本会话桌面分辨率 1463×914 与游戏 2560×1600 布局不匹配，游戏 UI 被裁到屏幕外，无法进入存档）。
→ 所有玩家属性读写均停留在 **A/C 级**。

---

## 1. 总体目标与边界

把只读预览插件逐步升级为生存辅助插件。开发顺序固定为：

| 阶段 | 内容 | 写入风险 |
|---|---|---|
| **3A** | 一键恢复（6 项资源） | 低（一次性写回满值） |
| **3B** | 独立状态控制（保护/锁值） | 中（持续写） |
| **3C** | 高级自动化（自动恢复、负面状态管理） | 中高（事件+定时） |
| **3D** | 稳定性与兼容性测试 | — |

**本轮（规划）明确不做**：不写玩家属性、不新增写值功能、不装行为补丁、不改存档、不进入开发。

---

## 2. 关键结论：游戏"属性"与"运行时资源"是两套东西

本地化 2560–2564 揭示了游戏的**角色属性（attribute）**：

| id | 属性 | 影响 |
|---|---|---|
| 2560 | STRENGTH 力量 | 近战伤害、后坐力、负重 |
| 2561 | AGILITY 敏捷 | 冲刺速度、射击精度、装填、爬窗、弓伤害、翻滚 |
| 2562 | STAMINA 持久力 | 最大体力；**奔跑与攻击消耗体力** |
| 2563 | ENERGY 活力 | **建造与制作**能力 |
| 2564 | CONSTITUTION 体力 | 最大生命与最大体力、脱战回复、抗性、体温稳定、饥饿/口渴上限 |

而**运行时资源**（`CharacterStatusData` 的字段）只有：
`hp / energy / fatigue / hunger / thirst / sanity / casEnergy / bloodVolume / bodyTemperature / radiationDose`

**两者不是一一对应**。Phase 3 只能操作运行时资源，不能改属性（属性来自技能/加点，属存档数据）。

### 2.1 STAMINA 与 ENERGY 的最终裁定（**推翻 Phase 2 的初判**）

运行期 Buff 表给出了决定性证据：

| buffId | 名称 | 字段效果 |
|---|---|---|
| 9 | HungerRecover | `hungerPerSecond = +5` |
| 10 | ThirstRecover | `thirstPerSecond = +5` |
| 11 | HPRecover | `hpPerSecond = +50` |
| 12 | **EnergyRecover** | **`energyPerSecond = +5`** |
| 28 | **StaminaPotion** | **`energyPerSecond = +3`** |

**「耐力药剂 StaminaPotion」恢复的是 `energy`** → 游戏内部 **"Stamina" 就是 `energy`**。
这与本地化 5431 `character stamina (energy)` 完全一致。

**新版结论**（取代 Phase 2 报告的表述）：

- **`energy` = 持久力 STAMINA = 精力 ENERGY**（同一数值，游戏自己混用两个词）
- **`fatigue` = 疲劳**，是**独立**的第二个资源；`BuffAttr` **没有任何 fatigue 字段**，
  说明疲劳不由 Buff 驱动，走的是另一条更新路径（`UpdateStatus` / `OnTimeUpdate`）
- **`fatigue` 的方向（越高越好 / 越高越糟）仍未确认** → 见 §5 决策门 G2

这直接决定 Phase 3A 的设计：**不能把 Stamina 与 Energy 当成两个可分别写满的资源**。

---

## 3. Phase 3A — 一键恢复

### 3.1 目标

对 6 项资源提供"一键恢复到满"：

| 资源 | 数据 | 目标值 |
|---|---|---|
| Health | `hp` | `maxHP` |
| Stamina | `energy` | `statusData.maxEnergy` |
| Energy | `energy` | 同上（**与 Stamina 同一数据源**） |
| Fatigue | `fatigue` | 待 G2 裁定方向 |
| Hunger | `hunger` | `statusData.maxHunger` |
| Thirst | `thirst` | `statusData.maxThirst` |

### 3.2 两条实现路线

| 路线 | 手段 | 优点 | 缺点 |
|---|---|---|---|
| **R1 原生恢复（推荐先做）** | `AddBuff(11/12/9/10, Time.time + 秒数)` | 走游戏自身逻辑，事件/UI/特效/Buff 栏都正确 | **是渐进的**（+5~50/秒），不是瞬时 |
| **R2 直接写值** | `player.hp = maxHP` 等属性 setter | 瞬时、精确 | **绕过**
setter 内部逻辑；可能漏事件、漏 UI、漏判定 |

**设计决定**：3A 同时提供两种模式，**默认 R1**；R2 必须经过 G1/G3/G4 验证后才允许默认可用。

### 3.3 不做的事

- 不用 `RemoveAllBuffs()`、不碰 `GameConfigs`、不写 `characterStatusData` 裸字段（只走属性 setter）。

---

## 4. Phase 3B — 独立状态控制

| 功能 | 实现思路 | 风险 |
|---|---|---|
| 生命值保护 | Redirect/Clamp：定时把 `hp` 钳到下限，或 `OnHit` 降伤 | 中（与受击/死亡判定交互） |
| Energy 控制 | 锁值（只升不降钳制） | 中（与奔跑/建造消耗竞争） |
| Fatigue 控制 | **等 G2 裁定** | 高（方向未知） |
| Hunger 控制 | 锁值 | 低 |
| Thirst 控制 | 锁值 | 低 |

### 4.1 统一写入协调机制（**硬性要求**）

Stamina 与 Energy 共享 `energy`。因此：

```
所有对 energy 的写入必须经过同一 Write Coordinator：
  * 单一 target 值（不是两个模块各写各的）
  * 每个 tick 只写一次
  * 记录"谁请求的"（stamina / energy / auto），冲突时按注册优先级仲裁
  * 禁止两个模块交替写不同值造成抖动
```

设计详见 `PHASE_3_SAFETY_DESIGN.md` §4。

---

## 5. 决策门（Gate）—— 未通过不得进入对应开发

| Gate | 问题 | 通过标准 | 阻塞范围 |
|---|---|---|---|
| **G1** | 属性 setter 是否真的生效且不破坏状态？ | 运行时把 `hp` 设为 `maxHP-1`，观察 UI/受击/存档均正常 | 3A-R2 全部 |
| **G2** | `fatigue` 方向？ | 五步实验（冲刺/挥砍/睡觉/喝耐力药剂/建造），看 `fatigue` 上升还是下降、UI 条怎么动 | 3A 的 Fatigue、3B 的 Fatigue |
| **G3** | `AddBuff` 第二参数语义 | 传 `Time.time + 5` 后 5 秒 buff 消失；传 `5` 则立即消失 | 3A-R1 全部 |
| **G4** | `energy` 与 `fatigue` 各自对应哪根 UI 条 | 与原生状态面板逐条对照 | 3B 的 Energy/Fatigue |
| **G5** | 联机门禁是否 fail-closed | 开房/加入后所有写功能被拒（日志可证） | 3A 之后所有阶段 |
| **G6** | 死亡/重生/切场景时写值是否安全 | 死亡瞬间不写、重生后再写、切场景重取引用 | 3A 之后所有阶段 |

> **G2 必须先做**：它是唯一"做错方向会直接让玩家更难受"的门。

---

## 6. Phase 3C — 高级自动化（规划，暂不细化）

- 自动回血（阈值触发）
- 自动恢复饱食/水分（阈值触发）
- 负面状态管理（基于 **62 条** 运行期 Buff 表，而非官方文档的 31 条）

**重要修正**：Phase 1 文档中的"31 条 Buff、清除白名单 11 条"**已过时**。
运行期实际 **62 条**，且包含文档里没有的 `32 Bleeding`（-3 hp/s，**是 debuff**）等。
→ 3C 的清除白名单必须基于运行期表重建，**不能沿用旧名单**。

---

## 7. Phase 3D — 稳定性与兼容性

- 游戏更新后 interop 重建、签名漂移检测（日志已有 `patch skipped` 机制可复用）
- 多次进出场景/读档/重生
- 长时间运行（写值是否引起累积误差或 GC）
- 与已装的其他代码模组共存（本机装了 6 个）

---

## 8. 风险寄存器

| # | 风险 | 影响 | 缓解 |
|---|---|---|---|
| R-1 | `fatigue` 方向搞反 | 玩家更难受 | G2 不通过不实现 |
| R-2 | Stamina/Energy 双写冲突 | 数值抖动、日志刷屏 | 统一 Write Coordinator |
| R-3 | 直接写 setter 漏事件 | UI 不同步、受击/死亡判定异常 | 默认走 R1；R2 需 G1 |
| R-4 | 联机误判 | 破坏他人体验/被判异常 | `NetworkManager.isOnline` 硬门禁 + fail-closed |
| R-5 | 死亡/重生瞬间写值 | 卡死、状态错乱 | `deadFlag` / `netDeadFlag` / `netRespawnInvincibleUntil` 守卫 |
| R-6 | 1.0.16 新增 MOD 安全检测 | 可能影响加载或提示 | **不隐藏、不伪装**；如实接受官方提示 |
| R-7 | 写值频率过高 | 掉帧（IL2CPP 每次调用都有封送成本） | 节流 + 幂等写（不累加） |

---

## 9. 明确不做的清单

| 项目 | 原因 |
|---|---|
| 修改 `GameConfigs` 全局静态 | 影响所有存档与难度；用户明确禁止 |
| 写 `characterStatusData` 裸字段 | 绕过 setter，最容易造成 UI/判定不一致 |
| `RemoveAllBuffs()` | 会连正面 Buff 一起清 |
| 改存档结构 / 改存档文件 | 明确禁止 |
| 绕过/隐藏 1.0.16 的 MOD 安全检测 | 明确禁止，且不道德 |
| 改其他玩家数据 | 明确禁止 |
| 伪装 DLL / 进程 | 明确禁止 |
| 修改旧 FPS 项目 | 明确禁止 |

---

## 10. 下一轮（Phase 3A 正式开发）前置条件

1. 用户批准本规划；
2. **G2（fatigue 方向）出结果** —— 只需在游戏里做一次五步观察；
3. G1/G3 的小规模实测（可在 3A 开发首日完成，不阻塞编码，阻塞"默认启用"）。

在 G2 之前，3A 可以先行实现 Health / Energy(Stamina) / Hunger / Thirst 四项，
**Fatigue 单独的开关保持"未启用（等待验证）"状态**。

---

## 11. 相关文档

| 文档 | 内容 |
|---|---|
| `docs/PHASE_3A_API_RESEARCH.md` | 逐项 API 表、原生恢复接口、62 条 Buff 表、方向与边界 |
| `docs/PHASE_3_SAFETY_DESIGN.md` | 统一写入门、联机 fail-closed、死亡/重生守卫、关闭语义 |
| `docs/PHASE_3_TEST_PLAN.md` | 测试分级、用例、验收标准 |
| `PHASE_2_REPORT.md` | 上一阶段交付与未验证项 |
| `docs/GAME_PLAYER_DATA.md` 等 6 份 | Phase 1 研究（注意 Buff 数量已更正） |
