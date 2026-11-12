# PHASE_3B_0_1_REGRESSION_REPORT.md — 稳定性与回归报告

> **阶段**：Phase 3B-0.1（仅验证与研究，未开发功能）
> **游戏**：ZED ZONE 1.0.16 / build 26600653
> **模组**：v0.3.0 + `LimbProbe`（只读）
> **日期**：2026-11-12

## 证据标记

**【已通过源码确认】** / **【已通过实机验证】** / **【尚未验证】** / **【存在风险】**

---

## 0. 工程交付

| 项 | 值 |
|---|---|
| 构建结果 | **0 警告 / 0 错误** |
| DLL 大小 | 79,872 字节 |
| **DLL MD5** | **`8E2B5D2C733E61E940F0AB7CF932D693`**（构建产物 = 已安装文件，哈希一致） |
| **Harmony Patch 数** | **0** |
| 离线回归测试 | **32 / 32 通过**（v0.3.0 一键恢复逻辑未改动） |
| 旧 FPS 优化模组 | 未改动（已提交状态 `git diff` 为空） |
| `LimbProbe` 默认值 | 代码默认 `false`；测试后已把配置改回 **`false`** |

---

## 1. 稳定性检查（逐项）

### 1.1 重复采样 ✅

| 会话 | 采样数 | 心跳 | 异常 |
|---|---|---|---|
| 会话 A | 110 | 2 | 0 |
| 会话 B（增强版） | 54+ | 2 | 0 |

- 全程 `[Error]` 计数 = **0**
- 无 `NullReferenceException`、无 Il2CppInterop 警告
- 未出现对象被销毁后的访问错误

### 1.2 场景切换 / 对象重新获取 ✅

实测到的完整序列：

```
scene=MainMenu  player=null
   ↓（载入存档）
scene=Gaming    ptr=0x2A05F8D9000   → 触发"重新枚举"
   ↓（退出到主菜单）
[state] 玩家对象消失（场景=Loading）
[limb]  玩家对象消失（场景=Loading），等待重新出现。
scene=MainMenu  player=null
```

- ✅ 玩家出现 → 自动枚举
- ✅ 玩家消失 → 清空上一轮缓存并记录，**不访问已销毁对象**
- ✅ 跨会话指针变化（`0x217D1CC7000` → `0x2A05F8D9000`）→ 重新解析，未复用旧引用

### 1.3 空对象 / 无效对象处理 ✅

| 情况 | 处理 |
|---|---|
| `player == null` | 记录"玩家对象消失"并返回，**不抛异常** |
| `characterData == null` | `Enumerate` 返回错误字符串 → 只告警一次，不刷屏 |
| `bodypartsData == null` | 同上 |
| `bodypartDataAry == null` | 同上 |
| 数组元素为 null | 该行标记 `err=null element`，其余肢体继续 |
| `humanBodyPartCount` 与数组长度不一致 | 打一条警告，**以数组为准**（实测两者均为 6） |
| `TextManager` 不可用 / 取不到文本 | 显示 `未知(TextId=x,…)`，**不伪造名称** |
| `IsClothingBodyPart` 等属性抛异常 | 输出 `?`，不中断 |

### 1.4 日志频率限制 ✅（并附前后对比）

| 版本 | 行为 | 实测频率 |
|---|---|---|
| 初版 | 每个变化一行 | 肢体自然恢复时约 **6 行/秒**（刷屏） |
| 现版 | 按秒合并（`LimbProbeChangeFlushSec = 1`） | **≤1 行/秒**，且仅在真有变化时输出 |

合并后的格式：

```
[limb] CHANGE(1s) 1:躯干 hp 100.14->100.24; 4:左腿 hp 101.13->101.23; 5:右腿 hp 71.01->71.11
```

心跳默认 30 秒一条；完整枚举只在"玩家出现/场景变化"时输出一次。
**空闲时日志量接近零**（本次会话 B 在无变化期间只有心跳）。

### 1.5 `LimbProbe` 默认关闭 ✅

- 代码默认值：`cfg.Bind("Diagnostics","LimbProbe", false, …)` —— **【已通过源码确认】**
- 测试期间临时置 `true`；**测试后已改回 `false`**：

```ini
[Diagnostics]
LimbProbe = false
LimbProbeIntervalMs = 500
LimbProbeHeartbeatSec = 30
LimbProbeChangeFlushSec = 1
```

### 1.6 只读性审计 ✅

源码扫描结果：`LimbProbe.cs` 中**不存在**对游戏数据的赋值：

| 匹配到的行 | 实际含义 |
|---|---|
| `r.Hp = d.hp;` | 把**游戏字段读进本地快照对象** `Row`，方向是"游戏 → 本地" |
| `r.MaxHp = d.maxHP;` | 同上 |
| 注释行 `不调用 HealAllBodyPartInjuries…` | 仅是说明文字 |

- ❌ 未调用 `HealAllBodyPartInjuries` / `SetToDefaultBodyPart` / `UseBandage` / `TickRecover`
- ❌ 未写 `hp` / `maxHP` / `isWounded` / `bleedingStopTime`
- ❌ 未引用 `GameConfigs`
- ❌ 未触碰存档文件

---

## 2. v0.3.0 一键恢复的回归

### 2.1 离线单测

```
passed=32 failed=0
ALL TESTS PASSED
```

32 项包含 Phase 3A 的 10 项"最大值选源"回归（HP 截断 Bug 的可执行断言）。

### 2.2 实机（由用户操作触发，日志记录）

```
[write] stat=Energy before=152    target=156 after=156 maxSource=CharacterStatusData.maxEnergy
[write] stat=Hunger before=62.62  target=112 after=112 maxSource=CharacterStatusData.maxHunger
[write] stat=Thirst before=94     target=112 after=112 maxSource=CharacterStatusData.maxThirst
[write.reject] stat=Hp reason=SkippedNoChange detail=当前 1680 已不低于目标 1680
```

- ✅ 恢复功能正常
- ✅ `maxSource` 显示上限来自 `CharacterStatusData.*`（正确的动态来源）
- ✅ **"只升不降"生效**：hp=1680 高于上限时没有被下调，而是 `SkippedNoChange`
- ✅ 未出现异常

> 注：这些写入由**用户点击**触发，不是本轮代码所为；本轮没有调用任何恢复接口。

---

## 3. 存档安全

| 项 | 结果 |
|---|---|
| 测试前备份 | `D:\backups\zedzone_3b01_20261112_233724`（`GameData` 3628 文件 + `CloudSave`） |
| 备份校验 | 逐文件 MD5，**差异 0** |
| 备份时机 | 关闭游戏后复制（避免半写文件） |
| 测试后变化 | 99 个文件，全部为**游戏自身正常保存**（`.game` / `_AutoBackUp` / 地图 chunk） |
| 是否损坏 | 否，游戏正常退出，无崩溃标记 |
| 是否需要回滚 | **不需要** |

---

## 4. 与既有模组的共存

本次运行日志确认同时加载 7 个代码模组，无冲突报错：

```
Shelter Smart Storage Terminal 1.9.10
ZED ZONE Mod Framework 0.1.0
Enemy Health Bar 0.2.0
Firefly Body Skin 0.3.19
ZED ZONE FPS Optimizer 1.0.0
Nested Inventory Fix 1.7.0
ZED ZONE Ultimate Assistant 0.3.0
```

---

## 5. 本轮发现的缺陷与修正

| # | 问题 | 处理 |
|---|---|---|
| 1 | 初版日志刷屏（6 行/秒） | 已改为按秒合并（≤1 行/秒） |
| 2 | 上一轮"视觉/数据 6/6 MISMATCH"结论**方法错误**（拿 visual[i] 比 data[i]） | 已更正：输出完整视觉清单，改为按 `bodypartDataID` 归类比对；结论写入索引映射报告 |
| 3 | `mismatch` 变量残留导致 1 个编译警告 | 已清理，当前 **0 警告** |

> 第 2 条是本轮最重要的自我更正：上一轮报告的"不匹配"是**我的比较方式错了**，
> 而不是游戏数据错了。真实情况是"视觉 11 个 / 数据 6 个，顺序不同"。

---

## 6. 未验证清单（与验证报告一致）

| # | 项 | 原因 |
|---|---|---|
| 1 | 受伤瞬间数值/状态跃变 | 未取得受伤样本（自动化无法安全制造伤害） |
| 2 | `isBleeding=1` 行为 | 两次会话流血全为 0 |
| 3 | 进出建筑后的数值一致性 | 未走位测试 |
| 4 | 主动存档→读档后的数值一致性 | 未执行完整流程 |
| 5 | 死亡/重生 | 未执行（避免损坏进度） |
| 6 | 联机拒绝实测 | 未开房 |
| 7 | `GetBodyPartDataByID` 实机调用 | 本轮用数组遍历，未走该方法 |
| 8 | `SetToDefaultBodyPart` 参数空间 | **禁止实测**，保持阻塞 |

---

## 7. 结论

- ✅ 探针在两次会话、110+ 采样中**零异常**
- ✅ 生命周期（出现/消失/换会话）处理正确，**不缓存、不访问失效对象**
- ✅ 日志频率已受控（≤1 行/秒）
- ✅ 只读性经源码审计确认（无任何游戏数据赋值）
- ✅ v0.3.0 一键恢复未受影响（32/32 + 实机写入正常 + 只升不降生效）
- ✅ 存档已备份校验，测试后无损坏
- ✅ `LimbProbe` 已恢复默认关闭
- ⚠ 8 项未验证，已如实列出，未用推断替代
