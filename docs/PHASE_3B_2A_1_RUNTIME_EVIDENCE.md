# PHASE_3B_2A_1_RUNTIME_EVIDENCE.md — 受控实验原始证据

> **性质**：原始日志摘录（未经改写）。所有内容来自 `BepInEx\LogOutput.log`。
> **游戏**：ZED ZONE 1.0.16 / build 26600653
> **日期**：2026-11-13
> **完整副本**：`research/phase3b2a1_experiment_evidence.txt`（48 行）

---

## 1. 环境与安全前提

| 项 | 值 |
|---|---|
| 场景 | `Gaming`（离线单机） |
| 备份路径 | `D:\backups\zedzone_3b2a1_20261113_002654` |
| 备份校验 | 3941 文件，逐文件 MD5，**差异 0**，32.45 MB |
| 实验执行器 | `[Experiments] LimbExp`（none/A/B），默认 none |
| 单变量保障 | 每次会话只执行一次；执行后 `_ran=true` 锁死，后续只做观察快照 |
| UI 接入 | **无**（该执行器没有任何按钮/菜单入口） |
| Harmony Patch | 0 |
| `[Error]` 计数 | 0 |

---

## 2. 实验 B 原始日志（`RefreshBodyPartRecoveryBuff(5)`，目标右腿）

```
[limbexp] ===== 开始实验 B（单变量、只调用一个接口）=====
[limbexp] scene=Gaming target=5
[limbexp] BEFORE idx=0 id=0 name=头部 hp=108/108 pct=1 wounded=0 bleeding=0 fully=1 bandaged=0
[limbexp] BEFORE idx=1 id=1 name=躯干 hp=22.428/138 pct=0.163 wounded=1 bleeding=0 fully=0 bandaged=1
[limbexp] BEFORE idx=2 id=2 name=左臂 hp=109.558/118 pct=0.928 wounded=0 bleeding=0 fully=0 bandaged=1
[limbexp] BEFORE idx=3 id=3 name=右臂 hp=118/118 pct=1 wounded=0 bleeding=0 fully=1 bandaged=0
[limbexp] BEFORE idx=4 id=4 name=左腿 hp=64.13/128 pct=0.501 wounded=1 bleeding=0 fully=0 bandaged=1
[limbexp] BEFORE idx=5 id=5 name=右腿 hp=64.385/128 pct=0.503 wounded=1 bleeding=0 fully=0 bandaged=1
[limbexp] BEFORE global hp=2160 buffCount=3
[limbexp] 目标 bodyPartId=5 对应恢复 Buff=37（只读查询）
[limbexp] 已调用 RefreshBodyPartRecoveryBuff(5)
[limbexp] AFTER  idx=0 id=0 name=头部 hp=108/108 pct=1 wounded=0 bleeding=0 fully=1 bandaged=0
[limbexp] AFTER  idx=1 id=1 name=躯干 hp=22.428/138 pct=0.163 wounded=1 bleeding=0 fully=0 bandaged=1
[limbexp] AFTER  idx=2 id=2 name=左臂 hp=109.558/118 pct=0.928 wounded=0 bleeding=0 fully=0 bandaged=1
[limbexp] AFTER  idx=3 id=3 name=右臂 hp=118/118 pct=1 wounded=0 bleeding=0 fully=1 bandaged=0
[limbexp] AFTER  idx=4 id=4 name=左腿 hp=64.13/128 pct=0.501 wounded=1 bleeding=0 fully=0 bandaged=1
[limbexp] AFTER  idx=5 id=5 name=右腿 hp=64.385/128 pct=0.503 wounded=1 bleeding=0 fully=0 bandaged=1
[limbexp] AFTER  global hp=2160 buffCount=3（整体HP变化=无，buff变化=0）
[limbexp] DELTA 变化部位数=0 
[limbexp] DELTA 六部位数据完全未变
```

时间序列（摘要，完整见证据文件）：

```
T+2s  global hp=2160 buffCount=3   六部位与 BEFORE 完全相同
T+6s  global hp=2160 buffCount=3   六部位与 BEFORE 完全相同
T+12s 躯干 22.428→23.164  左臂 →110.187  左腿 →64.813  右腿 →65.068   （全体自然恢复）
T+30s 躯干 →24.513        左臂 →111.341  左腿 →66.064  右腿 →66.319   （全体自然恢复）
```

**对照计算**：左腿（未指定）+1.934 / 右腿（已指定）+1.934 —— **完全相同**，百分比亦相同（1.51%）。

---

## 3. 实验 A 原始日志（`HealAllBodyPartInjuries()`）

```
[limbexp] ===== 开始实验 A（单变量、只调用一个接口）=====
[limbexp] scene=Gaming target=5
[limbexp] BEFORE idx=0 id=0 name=头部 hp=108/108 pct=1 wounded=0 bleeding=0 fully=1 bandaged=0
[limbexp] BEFORE idx=1 id=1 name=躯干 hp=22.428/138 pct=0.163 wounded=1 bleeding=0 fully=0 bandaged=1
[limbexp] BEFORE idx=2 id=2 name=左臂 hp=109.558/118 pct=0.928 wounded=0 bleeding=0 fully=0 bandaged=1
[limbexp] BEFORE idx=3 id=3 name=右臂 hp=118/118 pct=1 wounded=0 bleeding=0 fully=1 bandaged=0
[limbexp] BEFORE idx=4 id=4 name=左腿 hp=64.13/128 pct=0.501 wounded=1 bleeding=0 fully=0 bandaged=1
[limbexp] BEFORE idx=5 id=5 name=右腿 hp=64.385/128 pct=0.503 wounded=1 bleeding=0 fully=0 bandaged=1
[limbexp] BEFORE global hp=2160 buffCount=3
[limbexp] 已调用 HealAllBodyPartInjuries()
[limbexp] AFTER  idx=0 id=0 name=头部 hp=108/108 pct=1 wounded=0 bleeding=0 fully=1 bandaged=0
[limbexp] AFTER  idx=1 id=1 name=躯干 hp=138/138 pct=1 wounded=0 bleeding=0 fully=1 bandaged=1
[limbexp] AFTER  idx=2 id=2 name=左臂 hp=118/118 pct=1 wounded=0 bleeding=0 fully=1 bandaged=1
[limbexp] AFTER  idx=3 id=3 name=右臂 hp=118/118 pct=1 wounded=0 bleeding=0 fully=1 bandaged=0
[limbexp] AFTER  idx=4 id=4 name=左腿 hp=128/128 pct=1 wounded=0 bleeding=0 fully=1 bandaged=1
[limbexp] AFTER  idx=5 id=5 name=右腿 hp=128/128 pct=1 wounded=0 bleeding=0 fully=1 bandaged=1
[limbexp] AFTER  global hp=2160 buffCount=0（整体HP变化=无，buff变化=-3）
[limbexp] DELTA 变化部位数=4
   idx=1(躯干): hp 22.428->138 wounded 1->0 fully 0->1 ;
   idx=2(左臂): hp 109.558->118 fully 0->1 ;
   idx=4(左腿): hp 64.13->128 wounded 1->0 fully 0->1 ;
   idx=5(右腿): hp 64.385->128 wounded 1->0 fully 0->1 ;
```

后续观察（绷带延迟移除的证据）：

```
T+2s  idx=1 bandaged=1   idx=2 bandaged=1   idx=4 bandaged=1   idx=5 bandaged=1
T+6s  idx=1 bandaged=0   idx=2 bandaged=0   idx=4 bandaged=0   idx=5 bandaged=0   ← 绷带被移除
T+12s 全部 hp=max  bandaged=0  global hp=2160 buffCount=0
T+30s 全部 hp=max  bandaged=0  global hp=2160 buffCount=0      ← 状态稳定
```

---

## 4. 关键对照表（实验 A）

| 部位 | maxHP（动态） | 前 | 后 | 是否等于自身 maxHP |
|---|---|---|---|---|
| 头部 | 108 | 108 | 108 | 已满 |
| 躯干 | 138 | 22.428 | **138** | ✅ |
| 左臂 | 118 | 109.558 | **118** | ✅ |
| 右臂 | 118 | 118 | 118 | 已满 |
| 左腿 | 128 | 64.13 | **128** | ✅ |
| 右腿 | 128 | 64.385 | **128** | ✅ |

**四个上限互不相同（138/118/128/128），恢复后各自等于自己的上限** ——
证明它用的是**逐部位的动态 maxHP**，而不是任何固定常量。

---

## 5. 时间线（本地）

| 时间 | 事件 |
|---|---|
| 00:26:54 | 建立备份 `zedzone_3b2a1_20261113_002654`（3941 文件，校验 0 差异） |
| 00:30 | 启动游戏（实验 B 配置） |
| 00:32:0x | 进入存档 → 实验 B 自动执行 → T+2/6/12s 快照 |
| 00:33:0x | 关闭游戏，切换为实验 A |
| 00:34 | 重新启动并进入存档 → 实验 A 自动执行 → T+2/6/12/30s 快照 |
| 00:36 前后 | 关闭游戏，配置恢复 `LimbExp = none` |

---

## 6. 未包含在本轮的证据

| 项 | 原因 |
|---|---|
| 实验 C 日志 | **未执行**（参数空间无可靠证据，按禁令不得盲测） |
| 流血状态变化 | 实验期间所有部位 `bleeding=0`，无样例 |
| 视觉贴图 | 日志无法表达 |
| 存档持久化 | 未执行保存/读档流程 |
| 被清除的 3 个 Buff 的 ID | 本轮未记录 Buff ID |
