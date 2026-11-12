# PHASE_3B_2A_2_RUNTIME_EVIDENCE.md — 运行证据

> **游戏**：ZED ZONE 1.0.16 / build 26600653
> **日期**：2026-11-13
> **完整副本**：`research/phase3b2a2_evidence.txt`

## 关于日志轮转的说明

BepInEx 每次启动会重写 `LogOutput.log`。
**实验 A2 的日志块是在运行当时从该文件实时读取并保存到本报告的**；
随后为做"重载验证"重启了游戏，日志文件被新一轮内容覆盖，
因此 `research/phase3b2a2_evidence.txt` 中只保留了最后一次（VERIFY）的内容。
**A2 的原始文本见 §2（逐字复制当时读取到的行，未改写）。**

---

## 1. 环境

| 项 | 值 |
|---|---|
| 场景 | `Gaming`（离线单机） |
| 备份 | `D:\backups\zedzone_3b2a1_20261113_002654`（3941 文件，校验 0 差异） |
| 执行器 | 临时 `[Experiments] LimbExp`（none / A2 / VERIFY），**已从代码中删除** |
| 单变量 | 每次会话只调用 1 个接口 1 次，跑完锁死 |
| UI 入口 | 无 |
| `[Error]` | 0 |

---

## 2. 实验 A2 原始日志（`HealAllBodyPartInjuries()`）

### 2.1 Buff 全表（R13 核心证据）

```
[limbx] BEFORE buffCount=3
[limbx] BEFORE buff[0] id=35 name=torseRecovery debuff=0 end=2.71 remain=-5.16s owner=player
[limbx] BEFORE buff[1] id=37 name=legRecovery   debuff=0 end=2.71 remain=-5.16s owner=player
[limbx] BEFORE buff[2] id=36 name=armRecovery   debuff=0 end=2.78 remain=-5.09s owner=player
[limbx] AFTER buffCount=0
```

### 2.2 调用前后六部位快照

```
[limbx] BEFORE(0s) t=7.87 globalHp=2160 buffCount=3
idx=0 头部 hp=108/108     wounded=0 bleeding=0 fully=1 bandaged=0
idx=1 躯干 hp=22.428/138  wounded=1 bleeding=0 fully=0 bandaged=1
idx=2 左臂 hp=109.558/118 wounded=0 bleeding=0 fully=0 bandaged=1
idx=3 右臂 hp=118/118     wounded=0 bleeding=0 fully=1 bandaged=0
idx=4 左腿 hp=64.13/128   wounded=1 bleeding=0 fully=0 bandaged=1
idx=5 右腿 hp=64.385/128  wounded=1 bleeding=0 fully=0 bandaged=1

[limbx] AFTER(0s) t=7.87 globalHp=2160 buffCount=0
idx=0 头部 hp=108/108  wounded=0 bleeding=0 fully=1 bandaged=0
idx=1 躯干 hp=138/138  wounded=0 bleeding=0 fully=1 bandaged=1
idx=2 左臂 hp=118/118  wounded=0 bleeding=0 fully=1 bandaged=1
idx=3 右臂 hp=118/118  wounded=0 bleeding=0 fully=1 bandaged=0
idx=4 左腿 hp=128/128  wounded=0 bleeding=0 fully=1 bandaged=1
idx=5 右腿 hp=128/128  wounded=0 bleeding=0 fully=1 bandaged=1
```

### 2.3 时序（R14 绷带证据）

```
T+2s  t=7.89 globalHp=2160 buffCount=0
      idx=1 bandaged=1  idx=2 bandaged=1  idx=4 bandaged=1  idx=5 bandaged=1   （全部 hp=max）
T+4s  t=7.91 globalHp=2160 buffCount=0
      idx=1 bandaged=1  idx=2 bandaged=1  idx=4 bandaged=1  idx=5 bandaged=1   （全部 hp=max）
T+6s  t=9.15 globalHp=2160 buffCount=0
      idx=1 bandaged=0  idx=2 bandaged=0  idx=4 bandaged=0  idx=5 bandaged=0   ← 绷带移除
T+10s t=13.21 globalHp=2160 buffCount=0
      idx=1 bandaged=0  idx=2 bandaged=0  idx=4 bandaged=0  idx=5 bandaged=0
```

> 注：`t` 为游戏内 `Time.time`。T+2s 与 T+4s 之间游戏时间只前进 0.02s
> （当时游戏时间推进受加载/暂停影响），而墙钟确实经过了约 4 秒。
> 绷带变化发生在 T+4s 与 T+6s 之间。

---

## 3. 重载后的 VERIFY 快照（R15）

```
[limbx] ===== VERIFY（只读，不调用任何接口）=====
[limbx] VERIFY t=4.18 globalHp=1957.419 buffCount=2
[limbx] VERIFY idx=0 id=0 name=头部 hp=109.009/112 wounded=0 bleeding=0 fully=0 bandaged=0
[limbx] VERIFY idx=1 id=1 name=躯干 hp=29.882/142  wounded=1 bleeding=0 fully=0 bandaged=1
[limbx] VERIFY idx=2 id=2 name=左臂 hp=122/122     wounded=0 bleeding=0 fully=1 bandaged=0
[limbx] VERIFY idx=3 id=3 name=右臂 hp=122/122     wounded=0 bleeding=0 fully=1 bandaged=0
[limbx] VERIFY idx=4 id=4 name=左腿 hp=107.798/132 wounded=1 bleeding=0 fully=0 bandaged=1
[limbx] VERIFY idx=5 id=5 name=右腿 hp=48.388/132  wounded=1 bleeding=0 fully=0 bandaged=1
```

**上限变化**（升级）：108→112、138→142、118→122、128→132；
整体 maxHP 2160→2240；`conGained` 3→4。

---

## 4. 存档写入证据（**重要修正**）

| 时间 | 文件 | 说明 |
|---|---|---|
| 00:24:48 | `..._SlotIndex5.game_AutoBackUp` | 早前备份 |
| 00:45:50 | `26e8f5e0-..._SlotIndex5.game` | 有写盘，但**经用户指出：我随后强杀了进程，玩家状态未真正保存** |
| 00:47:40 | 重载进入同一存档 | 得到的既非治疗前、也非治疗后数值 → **对比无效** |

**修正**：本次"持久化验证"**不成立**，不得引用。
原因是实验后**未走游戏自身的保存流程**（强制结束进程），
属于我方实验方法缺陷，不是游戏行为问题。

要在干净条件下重做，需满足：安全区、游戏内显式保存、正常退出、重载后立即读数、全程不战斗。

---

## 5. 与上一阶段的对比

| 项 | 3B-2A.1 | 3B-2A.2 |
|---|---|---|
| Buff 变化 | 只知道 3→0 | **知道是哪 3 个（35/36/37 恢复类，且均已过期）** |
| 绷带 | 只在 6s 观察到 0 | **0/2/4s=1、6s=0、10s=0 完整时序** |
| 持久化 | 未测 | 已尝试，但被战斗干扰 → 仍未定论 |
| 整体 HP | 2160→2160 | 2160→2160（样本整体 HP 相同） |
