# 耐久只读诊断报告（Durability Diagnostic Report）

- 采集时间：2026-11-13 22:33:12 → 22:36:32
- 采集对象：游戏内实际游玩（开火、切枪、近战/工具、场景切换）
- 日志原文：`Diagnostics\durability_20261113_223312.log`（398 行）
- 插件版本：v1.1.0（耐久写入逻辑已全部暂停，仅 postfix 记录）
- 结论摘要：**近战/工具/装备的耐久 = `ItemData.durability`（已实测有效）；枪械耐久 = `BasicRangedWeapon.durabilityFloat`（只读 getter，后备字段未暴露）**

---

## 1. 运行时与框架（实测）

| 项 | 值 |
|---|---|
| 游戏版本 | **1.0.17** |
| Unity | **2023.1.18f1** |
| 平台 | WindowsPlayer |
| 托管运行时 | **.NET 6.0.7**（`RuntimeInformation.FrameworkDescription`） |
| 进程位数 | x64 |
| BepInEx | 6.0.0.0 |
| HarmonyX | 2.10.2.0 |
| Il2CppInterop | 1.5.3.0 |
| 数据目录 | `<游戏目录>/ZEDZONE_Data` |

---

## 2. 实际识别到的物品类型（装备槽）

| 槽位 | itemId | 实例指针 | ItemData.durability | 属性表 |
|---|---|---|---|---|
| currentWeapon | 461 | 0x22362B8F9C0 | **100（不变）** | `GunTemperature`, `GunPlusOne`, `MalfunctionFlag` |
| currentWeapon | 410 | 0x22362B8F900 | **100（不变）** | `GunTemperature`, `MalfunctionFlag`, `GunPlusOne` |
| currentWeapon | 420 | 0x22362B8BA80 | **100（不变）** | `GunTemperature`, `GunPlusOne` |
| currentWeapon | 460 | 0x22362B8B9C0 | **100（不变）** | `GunTemperature`, `GunPlusOne` |
| currentWeapon | **321** | 0x22362B8FA80 | **100 → 92（变化）** | `-`（无属性） |
| lightingTool | 92 | 0x22362B8F300 | **95.7391** | `IsSwitchOn`, `BatterySlot0/1` |
| throwable | 600 | 0x22362B8FB40 | 100 | `-` |
| feet | 10013 | 0x223D0BF3000 | **54.2904** | `-` |
| backpack | 503 | 0x223D0BF8F00 | 100 | `-` |

**观察**：`ItemData.durability` 并非恒为 100 —— 照明工具 95.74、鞋子 54.29、近战武器 321 从 100 逐次下降到 92，说明**该字段对"非枪械物品"是真实耐久**。

---

## 3. 实际读取到的耐久字段

| 类别 | 字段 | 证据 | 可写性 |
|---|---|---|---|
| 近战 / 工具 / 装备 | `ItemData.durability`（float） | 实测 100→99→98→96→95→94→93→92；照明 95.74、鞋 54.29 | **可写（编译通过）** |
| **枪械** | `BasicRangedWeapon.durabilityFloat`（float，0~1） | 实测 1 → 0.5633 → 0.5567 → 0.5533 …；0.1547→0.1526… | **只读 getter（赋值编译报 CS0200）** |
| 枪械每枪消耗 | `BasicRangedWeapon.m_gunpartDurabilityCostFloat` | 实测值 1.2 / 0.35 / 0.8 等，**与耐久下降同步但数值不等于下降量** | 可写 |
| 最大耐久 | **未找到任何可用字段** | `ItemAttr` 无 durability 字段；`ItemData` 只有 `durability`；`GetRepairedDurabilityPerRepair()` 只是单次修复量 | 不可读 |

---

## 4. 耐久变化记录（实测节选）

### 4.1 枪械（组件层，ptr 稳定）
```
[WEAPON] ptr=0x223FEE91000 name=BasicRangedWeapon(Clone) durabilityFloat=0.5633 costFactor=1.2
[WEAPON-CHANGE] 0x223FEE91000 durabilityFloat 0.5633->0.5567 costFactor 1.2->1.2 t=93.141 frame=14360
[WEAPON-CHANGE] 0x223FEE91000 durabilityFloat 0.5567->0.5533 costFactor 1.2->1.2 t=93.664 frame=14408
[WEAPON-CHANGE] 0x223FEE91000 durabilityFloat 0.5533->0.5467 costFactor 1.2->1.2 t=94.204 frame=14450
[WEAPON-CHANGE] 0x223FF254000 durabilityFloat 0.1547->0.1526 costFactor 0.35->0.35 t=99.483 frame=14938
```
**同一次射击期间，`ItemData.durability` 保持 100 不变**：
```
[EQUIP-CHANGE] slot=currentWeapon id=461 dur 100->100 ... props[GunTemperature 0->9.108609]
```

### 4.2 近战武器（数据层）
```
[EQUIP-CHANGE] slot=currentWeapon id=321 dur 100->99  t=174.952 frame=21449
[EQUIP-CHANGE] slot=currentWeapon id=321 dur  99->98  t=184.462 frame=22293
[EQUIP-CHANGE] slot=currentWeapon id=321 dur  98->96  t=185.520 frame=22388
[EQUIP-CHANGE] slot=currentWeapon id=321 dur  96->95  t=186.046 frame=22434
[EQUIP-CHANGE] slot=currentWeapon id=321 dur  95->94  t=186.581 frame=22478
[EQUIP-CHANGE] slot=currentWeapon id=321 dur  94->93  t=187.627 frame=22569
[EQUIP-CHANGE] slot=currentWeapon id=321 dur  93->92  t=188.153 frame=22616
```

### 4.3 卡壳状态（属性层，实测出现）
```
[EQUIP-CHANGE] slot=currentWeapon id=410 props[GunTemperature=0,MalfunctionFlag=false]
            -> [GunTemperature=0,MalfunctionFlag=true]   t=102.656 frame=15183
[EQUIP-CHANGE] slot=currentWeapon id=410 MalfunctionFlag true -> false  t=107.502 frame=15585
```

---

## 5. 调用链证据

### 5.1 已观测到的调用（`HCC.CostItemDurability`，共 105 次）
```
[CALL HCC.CostItemDurability] result=False itemId=90  itemDur=99.98 t=92.078
[CALL HCC.CostItemDurability] result=False itemId=90  itemDur=99.96 t=96.091
[CALL HCC.CostItemDurability] result=False itemId=90  itemDur=99.94 t=97.096
...
```
- 调用分布：`itemId=90` 87 次、`itemId=321` 16 次、`itemId=10011` 2 次；
- **返回值恒为 `False`**（`result=False`），且调用时 `itemDur` 已在下降 → 说明该方法更像是"检查/尝试"，**真正写入发生在它之外**；
- **没有任何一次调用关联到 461/410/420/460 这些枪械 id** → 枪械掉耐久**不经过** `HCC.CostItemDurability`。

### 5.2 `durabilityFloat` 读取栈（20 条，全部相同形态）
```
[READ-STACK] DMD<BasicRangedWeapon::get_durabilityFloat>
  <- (il2cpp -> managed) get_durabilityFloat
  <- IL2CPP.il2cpp_runtime_invoke
  <- BasicRangedWeapon.get_durabilityFloat
  <- Trampoline_VoidThis ZedZone.UltimateAssistant.UI.AssistantPanelUpdate
```
**结论（不夸大）**：栈里能看到的最外层是**本模组自己的 `AssistantPanelUpdate`**（因为我们的探针在调用它）。
IL2CPP 的 native→managed trampoline 把游戏侧托管帧隐藏了，**因此本次无法取得"游戏内部读取者/写入者"的调用栈证据**。

### 5.3 未能定位的证据
- 枪械 `durabilityFloat` 的**后备存储字段**：类型 `BasicRangedWeapon` 上不存在名字含 durability 的私有字段（interop 只暴露 `durabilityFloat` 只读属性与 `m_gunpartDurabilityCostFloat`）；
- 枪械耐久的**写入方法**：未发现公开/可拦截的写入 API。

---

## 6. 无法读取的数据及原因

| 数据 | 状态 | 原因（有证据） |
|---|---|---|
| 物品最大耐久 | **无法读取** | `ItemData` 只有 `durability`；`ItemAttr` 无 durability 字段；`GetRepairedDurabilityPerRepair()` 只是"每次修理量" |
| 枪械真实耐久存储字段 | **无法定位** | `durabilityFloat` 仅 getter（CS0200）；无同名后备字段暴露 |
| 枪械耐久写入调用链 | **无法取证** | IL2CPP trampoline 隐藏游戏侧托管栈；无公开写入方法 |
| 枪械 `ItemData.durability` 与真实耐久的关系 | **已证伪** | 实测枪械 `ItemData.durability` 恒 100，同时 `durabilityFloat` 从 1 降到 0.56 |
| `HCC.CostItemDurability` 与枪械耐久的关系 | **已排除** | 105 次调用中无一条 itemId 属于枪械（461/410/420/460） |

---

## 7. 结论与建议

### 7.1 可以安全实现的
**近战武器 / 工具 / 装备的耐久锁定**：
- 字段 `ItemData.durability` 已被实测证明是真实耐久（100→92、95.74、54.29）；
- 该字段可写；
- 安全策略：**只增不减**（仅当当前值低于记录的"已知最高值"时才写回），不会破坏高耐久物品。

### 7.2 目前**不具备**安全实现条件的
**枪械耐久锁定**：
- 真实值在 `BasicRangedWeapon.durabilityFloat`（只读 getter），后备存储不可见；
- 无法写入、无法拦截写入点、无法取证调用链；
- 任何"猜一个私有字段去写"的做法都违反"不猜测"原则，可能损坏武器数据。

### 7.3 若要继续攻枪械耐久，需要的前置证据
1. 用 IL2CPP 类元数据（`il2cpp_class_get_fields` 遍历含私有字段）确认 `BasicRangedWeapon` 是否存在 `durability`/`m_durability` 私有字段及其偏移；
2. 若存在，验证写入该字段能改变 `durabilityFloat` 且不破坏存档；
3. 若不存在，需转向"拦截每枪耐久消耗"的调用点（需要 method hook 到 native 层，风险高）。

在上述证据取得前，**不建议**对枪械耐久做任何写入。
