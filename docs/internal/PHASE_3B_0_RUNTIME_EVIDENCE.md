# PHASE_3B_0_RUNTIME_EVIDENCE.md — 肢体枚举运行证据

> **说明**：本文只记录**实际运行得到**的日志与测试结果。
> 未执行的测试明确写"未执行 + 原因"，**不用源码推断代替实机验证**。
> **游戏**：ZED ZONE 1.0.16 / build 26600653
> **日期**：2026-11-12

---

## 1. 环境

| 项 | 值 |
|---|---|
| 游戏版本 | 1.0.16（日志 `detected game version: 1.0.16`） |
| 场景 | `Gaming`（存档已加载） |
| 玩家 | `isPlayer=1`，`ptr=0x217D1CC7000` |
| 配置 | `[Diagnostics] LimbProbe = true`，间隔 500ms，心跳 30s |
| 已装模组（日志确认 7 个） | AETerminal 1.9.10 / ZedModFramework 0.1.0 / EnemyHealthBar 0.2.0 / FireflyBodySkin 0.3.19 / FPS Optimizer 1.0.0 / NestedInventoryFix 1.7.0 / **Ultimate Assistant 0.3.0** |
| 异常 | `[Error]` 计数 = **0** |

---

## 2. 原始枚举样本（逐字复制，未改写）

```
[limb] ===== 肢体枚举 (重新枚举) =====
[limb] scene=Gaming ptr=0x217D1CC7000 isPlayer=1
[limb] humanBodyPartCount=6 arrayLen=6 textIdAryLen=6 defaultHPAryLen=6 appliedBodyPartConstitution=20
[limb] idx | bodyPartId | textId | name | hp | maxHP | defaultHP | wounded | bleeding | recovered | hpFloat | visualDataId | match
[limb] 0 | 0 | 80 | 头部 | 100 | 100 | 20 | 0 | 0 | 1 | 1 | 1 | MISMATCH
[limb] 1 | 1 | 81 | 躯干 | 92.55 | 130 | 50 | 1 | 0 | 0 | 0.71 | 4 | MISMATCH
[limb] 2 | 2 | 180 | 左臂 | 110 | 110 | 30 | 0 | 0 | 1 | 1 | 4 | MISMATCH
[limb] 3 | 3 | 181 | 右臂 | 110 | 110 | 30 | 0 | 0 | 1 | 1 | 5 | MISMATCH
[limb] 4 | 4 | 182 | 左腿 | 94.13 | 120 | 40 | 1 | 0 | 0 | 0.78 | 5 | MISMATCH
[limb] 5 | 5 | 183 | 右腿 | 64.01 | 120 | 40 | 1 | 0 | 0 | 0.53 | 0 | MISMATCH
[limb] ===== 枚举结束（6 个肢体，ID 交叉校验不匹配 6 个）=====
```

### 2.1 心跳样本

```
[limb] heartbeat scene=Gaming samples=56 changes=0 |
        头部=100/100; 躯干=92.55/130W; 左臂=110/110; 右臂=110/110; 左腿=94.13/120W; 右腿=64.01/120W
```

（`W` = isWounded）

### 2.2 变化（实时性）样本

```
[limb] CHANGE idx=1 id=1 name=躯干 hp=92.55->92.66 | t=23:21:49
[limb] CHANGE idx=4 id=4 name=左腿 hp=94.13->94.23 | t=23:21:49
[limb] CHANGE idx=5 id=5 name=右腿 hp=64.01->64.11 | t=23:21:49
[limb] CHANGE idx=1 id=1 name=躯干 hp=92.66->92.77 | t=23:21:50
[limb] CHANGE idx=4 id=4 name=左腿 hp=94.23->94.33 | t=23:21:50
[limb] CHANGE idx=5 id=5 name=右腿 hp=64.11->64.21 | t=23:21:50
```

> 注：以上为**第一版构建**的输出（未做日志合并，故每行独立）。
> 第二版已改为按秒合并，格式：
> `[limb] CHANGE(1s) 1:躯干 hp 92.55->93.53; 4:左腿 hp 94.13->95.03`

---

## 3. 启动证据（第二版构建）

```
[Info :ZED ZONE Ultimate Assistant] ZED ZONE Ultimate Assistant v0.3.0 loading (Phase 3A = 一键恢复)
[Info :ZED ZONE Ultimate Assistant]   阶段              : Phase 3A 一键恢复（含写值）
[Info :ZED ZONE Ultimate Assistant]   写入路径          : 唯一入口 PlayerWriteGate（只写属性 setter）
[Info :ZED ZONE Ultimate Assistant]   Harmony 补丁数    : 0（要求 0）
[Info :ZED ZONE Ultimate Assistant]   恢复项开关        : HP=True Energy=True Hunger=True Thirst=True Fatigue=False（方向=unconfirmed）
[Info :ZED ZONE Ultimate Assistant] [ui] 中文字体来源: PlayerStatusPanel.daysText.font
```

同日模组清单（证明与既有模组共存无冲突）：

```
- Shelter Smart Storage Terminal 1.9.10
- ZED ZONE Mod Framework 0.1.0
- Enemy Health Bar 0.2.0
- Firefly Body Skin 0.3.19
- ZED ZONE FPS Optimizer 1.0.0
- Nested Inventory Fix 1.7.0
- ZED ZONE Ultimate Assistant 0.3.0
```

---

## 4. 测试执行结果（逐项）

| # | 测试 | 结果 | 证据 / 原因 |
|---|---|---|---|
| T-01 | **初始肢体枚举** | ✅ **已通过实机验证** | 见 §2：6 个肢体、名称、hp/maxHP/defaultHP/三状态全部读出 |
| T-02 | 受伤前后数据对比 | ❌ **未执行** | 需要玩家在游戏中**受到攻击**；本会话无法进入存档（见 §5）。第一版运行时观察到的是**自然恢复**（hp 上升），不是受伤下降 |
| T-03 | 肢体状态变化观察 | 🟡 **部分验证** | 已观察到 `isWounded=1 ⇔ hp<maxHP`、`hpFloat=hp/maxHP`、满血时 `isFullyRecovered=1`（见 §2 表格）。**未观察到 wounded 0→1 的瞬间跃变** |
| T-04 | 场景切换后重新枚举 | 🟡 **部分验证** | 观察到 `MainMenu(player=null)` → `Gaming(player=...)` 触发了"重新枚举"；**未测试"游戏内换场景"** |
| T-05 | 死亡与重生后重新枚举 | ❌ **未执行** | 需要玩家死亡；本会话无法操作 |
| T-06 | 存档重载后重新枚举 | ❌ **未执行** | 需要读档；同上 |
| T-07 | 联机模式拒绝执行 | ❌ **未执行** | 需要真实开房/加入；**代码路径已具备**但未实机复现 |
| T-08 | 重复采集稳定性 | 🟡 **部分验证** | 单次运行 56 个采样点无异常、无 `[Error]`；发现并修复了日志刷屏（6 行/秒 → ≤1 行/秒）。**第二版尚未在有玩家时重跑** |
| T-09 | 视觉组件映射核对 | 🟡 **部分验证** | 已发现 6/6 不匹配（见报告 §2.6）；完整 visual 清单需新构建在有玩家时输出，**尚未取得** |

---

## 5. 未能执行测试的原因（如实说明）

本会话的自动化环境**无法进入存档**：

1. 游戏以 **2560×1600** 渲染，而桌面为 **1463×914**，主菜单/存档列表部分位于窗口外或坐标不可靠
2. 会话策略**自动拒绝**需要审批的输入注入（`click` 返回 `action denied by approval`）
3. 早前已验证：游戏的合成按键（`keybd_event`）**不被游戏内部输入系统接收**（INS 键从未响应）

因此**所有需要"在游戏中操作角色"的测试（受伤、死亡、读档、换场景、联机）都需要由人工执行**。
本轮**没有**用源码分析代替这些实机验证。

---

## 6. 可直接复现的操作步骤（供人工执行）

1. 启动游戏 → 载入存档（`人物名: SORA`，最后保存 23:22）
2. 等待 2 秒，日志出现 `[limb] ===== 肢体枚举 =====`（含 visual 清单）
3. **受伤**：让僵尸攻击几次 → 观察 `CHANGE` 行里 `hp` 下降、`wounded 0->1`
4. **换场景**：进出建筑 → 观察是否输出"重新枚举"与新 `ptr`
5. **死亡重生**：自杀/被打死 → 重生 → 观察重新枚举、`ptr` 变化
6. **读档**：存档 → 回主菜单 → 读档 → 观察重新枚举与 hp 是否与存档前一致
7. **联机**：开房 → 观察是否出现 `[limb] 联机状态（...），肢体采集模块拒绝执行。`
8. 全程记录 `BepInEx\LogOutput.log`

---

## 7. 日志位置

```
<游戏目录>\BepInEx\LogOutput.log
```

搜索关键字：`[limb]`
