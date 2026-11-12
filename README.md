# ZED ZONE Ultimate Assistant

《隔离区-丧尸末日生存》（ZED ZONE, Steam AppID **1211600**）单人生存辅助模组。

| 项 | 值 |
|---|---|
| 游戏版本 | build **26576287**（游戏内 1.0.14） |
| 引擎 | Unity **2023.1.18f1** / **IL2CPP x64** / URP 15.0.7 |
| 技术栈 | BepInEx 6 IL2CPP + HarmonyX + Il2CppInterop |
| 上一项目 | `<仓库目录>\ZED-ZONE-FPS-Optimization-Mod`（已完成的 FPS 优化模组，本项目继承其工程经验） |
| 当前阶段 | **Phase 1 代码调查 —— 已完成，等待确认** |

---

## 状态

**Phase 1（仅研究，不写功能）已完成。** 按项目要求，在报告确认前不进入 Phase 2 开发。

- ✅ 6 份研究文档（`docs/`）
- ✅ 旧项目工程模式提取（`research/LEGACY_PROJECT_PATTERNS.md`）
- ❌ 尚未编写任何模组功能代码
- ❌ 尚未进行任何实机测试（所有文档中的「测试」列均为 `未实机验证`）

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
