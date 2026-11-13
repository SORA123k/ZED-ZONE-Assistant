# ZED ZONE Ultimate Assistant

《隔离区-丧尸末日生存》（ZED ZONE, Steam AppID **1211600**）单人**离线**生存辅助模组。

| 项 | 值 |
|---|---|
| 版本 | **v1.0.0（最终版）** |
| 游戏版本 | 游戏内 **1.0.17** / Steam build **26627937** |
| 引擎 | Unity **2023.1.18f1** / **IL2CPP x64** / URP 15.0.7 |
| 技术栈 | BepInEx 6 IL2CPP + HarmonyX + Il2CppInterop |
| 菜单 | **ClickGUI 风格**，按 **INSERT** 开关 |
| 旧项目 | `<仓库目录>\ZED-ZONE-FPS-Optimization-Mod`（**未改动**） |

---

## 功能

| 分类 | 功能 | 说明 |
|---|---|---|
| **Player** | Health Restore | 恢复到当前角色真实上限（动态读取，只升不降） |
| | Energy / Stamina Restore | 同一数值（游戏内 STAMINA 与 ENERGY 同源） |
| | Hunger Restore | 动态上限 |
| | Thirst Restore | 动态上限 |
| | 肢体状态（只读） | 六个部位 HP / 伤口 / 绷带，只读展示 |
| **Combat** | Infinite Ammo | 射击不消耗备用弹药（仅本地玩家） |
| | No Reload | 弹匣为 0 也可射击；按 R 仍走原生换弹 |
| | Weapon Durability | 携带武器/装备耐久持续补回（只增不减） |
| | No Overheat | 取消自己枪械的过热（不影响 NPC） |
| | 一键修复卡壳 | 卡壳不再阻止开火（游戏无公开清除接口，走兜底） |
| **Visuals** | Zombie ESP | 标记已加载僵尸（名称 / 距离 / 方框），只读 |

## 使用

1. 启动游戏（**离线单机**），进入存档；
2. 按 **INSERT** 打开菜单，左侧切换 `Combat / Player / Visuals / Settings`；
3. 点击功能卡片切换开关；`Player` 页点「恢复」按钮；
4. 按住标题栏可**拖动**菜单位置（松手自动保存）；
5. 再按 **INSERT** 关闭菜单，恢复角色操作。

配置文件：`BepInEx\config\zedzone.ultimateassistant.cfg`（`[Features]` / `[Esp]` / `[Menu]` 等段）。

## 安全约束

- 仅**离线单机**；联机下弹药类功能与 ESP 一律拒绝（fail-closed）；
- 不修改全局 `GameConfigs`、不修改存档结构、不修改其他角色/NPC；
- 不绕过 / 隐藏游戏 MOD 安全提示；
- 玩家对象**每次重新解析**，不缓存；正确处理死亡、重生、场景切换；
- 所有功能默认关闭，可独立开关；关闭补丁立即卸载。

## 详细报告

- [FINAL_RELEASE_REPORT.md](FINAL_RELEASE_REPORT.md) — 最终交付报告
- `docs/` — 各阶段研究与验证文档（Phase 1 / 2 / 3A / 3B）

---
## 状态

**Phase 1（研究）→ Phase 2（只读骨架 + GUI）→ Phase 3A（一键恢复）均已完成。**

- ✅ 6 份研究文档 + Phase 3 四份规划文档（`docs/`）
- ✅ Phase 2 只读插件：7 个读取模块 + GUI + 22 项离线测试
- ✅ **Phase 3A：一键恢复（生命 / 精力 / 饱食 / 水分），全部经统一写入门**
- ⏳ 疲劳恢复默认关闭（方向未确认，见下）
- ❌ Phase 3B/3C/3D 未开始

### Phase 3A 使用方式

游戏内按 **INSERT** 打开面板：

| 标签页 | 按钮 |
|---|---|
| 玩家 | 恢复生命 / 恢复精力 / 恢复饱食 / 恢复水分 / 恢复疲劳 |
| 生命 | 恢复生命 |
| 饱食 | 恢复饱食 |
| 水分 | 恢复水分 |
| 体力/精力 | 恢复精力/持久力 / 恢复疲劳 |

**安全机制（全部实测生效）**：

- 唯一写入门 `PlayerWriteGate`，固定检查顺序：配置 → **联机(fail-closed)** → 验证状态 → 重取玩家 → 死亡标志 → 重生窗口 → 钳制 → 写入 → 回读校验
- **联机一律拒绝**（`NetworkManager.isOnline/isHost/isClient`）
- **只升不降**：实测玩家有效生命上限可超过 `maxHP`（日志见 `hp=1840/1000`），
  因此恢复绝不把数值往下写
- 关闭功能只停止写，**不重置**玩家数值

⚠ **疲劳（fatigue）默认不可恢复**：它的方向（越高越好 / 越高越糟）尚未确认。
在 `[Restore] FatigueDirection` 里填 `resource` 或 `exhaustion` 并打开 `Fatigue` 开关后才会生效。

```ini
[Restore]
EnableRestore = true
Fatigue = false            ; 默认关闭
FatigueDirection = unconfirmed   ; resource | exhaustion
```

---

## Phase 2 摘要（历史）

```
玩家：GameController.instance.playerCharacter  (HumanCharacterController)
状态：playerCharacter.characterData.characterStatusData
Buff：playerCharacter.characterData.buffDataList
```

- **只读保证**：所有模块继承 `StateModuleBase`，该基类**只有 `Read`，没有 `Write`**；
  `PatchManager` 在 Phase 2 安装的 Harmony 补丁数恒为 **0**。
- **不缓存玩家引用**：每次读取都重新 `Resolve()`。
- **不伪造数据**：读不到显示"读取失败"并给异常类型，不用 0 填充。
- **中文**：借用游戏自带 CJK 字体（OS 字体 ICall 在 IL2CPP 构建中被剥离）。
- **热键**：`Insert` 开关、`Esc` 关闭、数字键 1-7 切页；启动时自动检测按键冲突。

⚠ **属性语义更正**：游戏内 `STAMINA（持久力）` 与 `ENERGY（精力）` 是**同一个数值 `energy`**
（本地化 id 5431），`FATIGUE（疲劳）` 才是独立第三项。模块分开但同源，不伪造第二个数字。

---

## Phase 1 文档

| 文档 | 内容 |
|---|---|
| [GAME_PLAYER_DATA.md](docs/GAME_PLAYER_DATA.md) | 玩家真实类型、访问入口、数据链、存档与加载 |
| [HEALTH_SYSTEM_RESEARCH.md](docs/HEALTH_SYSTEM_RESEARCH.md) | 生命值权威位置、伤害入口、死亡链、无敌方案排序 |
| [SURVIVAL_SYSTEM_RESEARCH.md](docs/SURVIVAL_SYSTEM_RESEARCH.md) | 饥饿/饥渴/精力/疲劳、衰减入口、消耗倍率 |
| [STATUS_EFFECT_RESEARCH.md](docs/STATUS_EFFECT_RESEARCH.md) | Buff 系统、31 条官方 Buff、清除白名单 |
| [MULTIPLAYER_COMPATIBILITY.md](docs/MULTIPLAYER_COMPATIBILITY.md) | 联机架构、221 个网络消息分类、门禁设计 |
| [API_PATCH_MAPPING.md](docs/API_PATCH_MAPPING.md) | **功能 → API/补丁 的总映射**、风险分级、Phase 2 验证清单 |
| [LEGACY_PROJECT_PATTERNS.md](research/LEGACY_PROJECT_PATTERNS.md) | 上一项目的可复用模式与两个必须规避的坑 |

---

## Phase 1 核心结论（摘要）

### 四条写值链路

```
A 玩家：GameController.instance.playerCharacter        // HumanCharacterController
B 状态：playerCharacter.characterData.characterStatusData  // 唯一真实存储
C Buff：playerCharacter.characterData.buffDataList
D 全局：GameConfigs.playerInvincible / hungerCostPerDay / ...
         CharacterData.skillProperty_HungerRateFloat / ...
```

### 必须知道的三件事

1. **玩家类型是 `HumanCharacterController`**（继承 `BasicCharacterController`），且两个访问入口都有 setter → **禁止缓存引用**。
2. **`HumanCharacterController.OnHit` 才是玩家的伤害入口**（基类同名方法是 `Virtual_New` 新槽，打基类对玩家无效）；**DoT 绕过它**，落在 `ApplyBuffStatusTick`。
3. **联机检测不能用 `isSimulationAuthority`**（主机与单机都为 true）→ 必须用 `NetworkManager.isOnline` / `NetMode.Offline`。

### 尚未定论、Phase 2 必须先裁定

- 玩家 UI 的「体力」到底对应 `energy` 还是 `fatigue`（本地化表显示 `STAMINA→持久力`、`CONSTITUTION→体力`）。
- `GameConfigs.playerInvincible` 在正式版是否接线。
- 改值是否跨存档持久。

详见 [API_PATCH_MAPPING.md](docs/API_PATCH_MAPPING.md) 第 9、10 节。

---

## 设计原则（贯穿全项目）

1. **先研究，后开发** —— 已完成 Phase 1。
2. **先读取真实字段，再实现修改** —— 不靠名字猜功能。
3. **所有功能独立开关**，默认关闭。
4. **关闭功能不得重置玩家数值**（只停止写值，不回写默认值）。
5. **第一版仅单机** —— 检测到联机则硬禁用状态修改。
6. **不修改** `GameAssembly.dll` / `global-metadata.dat` / 存档结构。
7. **不伪造测试结果** —— 未实机验证的内容一律如实标注。

---

## 存储规范

本项目全部文件位于 **D 盘**：

| 用途 | 路径 |
|---|---|
| 项目源码/文档 | `<仓库目录>\ZED-ZONE-Ultimate-Assistant` |
| .NET SDK | `<dotnet>` |
| ILSpy 反编译器 | `<tools>` |
| NuGet 包缓存 | `<nuget-cache>` |
| dotnet CLI 缓存 | `<dotnet>-home` |


