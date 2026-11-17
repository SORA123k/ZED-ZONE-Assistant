# 游戏版本兼容与适配（COMPATIBILITY）

## 已验证的版本

| 游戏版本 | Unity | BepInEx | 状态 | 备注 |
|---|---|---|---|---|
| **1.0.19** | 2023.1.18f1（IL2CPP x64） | 6.0.0-be.793（创意工坊加载器内置）/ 6.0.0-be.* 手动 | ✅ 实机验证 | 当前发布版本（v1.2.1）的适配基准 |
| 1.0.18 | 2023.1.18f1 | 6.0.0-be.* | ✅ 实机验证 | 辐射控制 / 连点器 在该版本验证 |
| 1.0.17 | 2023.1.18f1 | 6.0.0-be.* | ✅ 实机验证 | 上一发布基准 |
| 1.0.16 | 2023.1.18f1 | 6.0.0-be.* | ⚠️ 部分验证 | `NetworkManager.isOnline/isHost/isClient` 在此期间被实机验证可读；其余功能以 1.0.17 为准 |
| 其它版本 | — | — | ❓ 未验证 | 启动时日志会打印版本不匹配警告，功能可能失效 |

代码里的基准版本常量：`Plugin.TestedGameVersion = "1.0.19"`。
不等于该值时，插件**只告警不阻断**（宁可让用户试，也不因为版本号把可用功能锁死），
警告写在 `BepInEx\LogOutput.log`。

## 哪些东西会随游戏更新失效

按"最容易坏"到"最不容易坏"排序：

| 依赖点 | 位置 | 失效表现 |
|---|---|---|
| IL2CPP 方法签名（参数个数/类型） | `Patch/*.cs` 的 `[HarmonyPatch]` 目标 | 补丁装不上，日志出现 `Patching exception` / `method not found`；对应功能静默无效 |
| 字段 / 属性名 | `WeaponUtil.cs`、`DurabilityLock.cs`、`PlayerPatches.cs` | 反射取值抛异常，日志 `[xxx] 读取失败` |
| `ItemData.properties[0]` 的语义 | `DurabilityLock.cs`、`WeaponUtil.cs` | 枪械耐久不再锁定，或写错字段（可能影响其它属性） |
| `NetworkManager` 联机标志 | `PlayerWriteGate.IsMultiplayer` | 检测异常时**按联机处理**（宁可全禁用），日志 `联机状态检测失败，按联机处理` |
| URP 2D `Light2D` 全局光 | `FullBright.cs` | 地图全亮无效或闪烁 |
| UI 相关的 Unity EventSystem | `InputGuard.cs` | 菜单点不动 / 点击穿透（不影响存档） |

## 游戏更新后的重新适配步骤

> 前提：本机装有游戏 + .NET SDK 8。适配不需要修改游戏文件。

1. **重建 interop**（最关键）
   删除 `<游戏目录>\BepInEx\interop\`，用 Steam 启动一次游戏让 BepInEx 重新生成；
   或者重跑创意工坊加载器的 `install.cmd`。
2. **确认构建通过**
   ```powershell
   $env:ZEDZONE_GAME_ROOT = "D:\...\ZED ZONE"
   pwsh tools/build.ps1 -SkipInstall
   ```
   编译失败 = 签名变了，按报错定位到具体文件。
3. **打开诊断，跑一次实机**
   在 `zedzone.ultimateassistant.cfg` 里临时打开：
   ```ini
   [Diagnostics]
   DurabilityDiag = true
   MetaDiag = true
   DumpBuffTable = true
   ```
   进游戏后看 `BepInEx\LogOutput.log`：
   - `[self-check]` / `[features]` 段是否正常；
   - 各功能开关后有没有 `Patching exception`；
   - 耐久/枪械功能的写入是否被回读校验确认。
4. **对照补丁清单逐条验证**
   `docs/API_PATCH_MAPPING.md` 里列了每个补丁的目标方法。用 ilspycmd 看新的
   `Assembly-CSharp.dll` 是否还有同名同签名的方法：
   ```powershell
   ilspycmd "<游戏目录>\BepInEx\interop\Assembly-CSharp.dll" -t HumanCharacterController
   ```
5. **改代码 → 更新文档**
   - 改了补丁目标：更新 `docs/API_PATCH_MAPPING.md`
   - 改了配置项：更新 `docs/CONFIG.md`
   - 改了基准版本：更新本文件表格 + `Plugin.TestedGameVersion`
6. **回归测试清单**（每次适配至少跑一遍）

   - [ ] 菜单能开（`Insert`），分类页能切
   - [ ] 一键恢复四项数值正确（不超上限、不回退）
   - [ ] 无限弹药 / 无需换弹 生效（打完一梭子再打）
   - [ ] 工具耐久不消耗（挥砍 10 次看耐久）
   - [ ] 枪械耐久不下降（连射 30 发看 `props[0]`）
   - [ ] 不会卡壳（连射到过热后再射）
   - [ ] 不掉血 / 不掉体力 / 不掉饱食 / 不掉水分（各跑 1 分钟）
   - [ ] ESP 正常且不漏怪（夜间场景）
   - [ ] 地图全亮可开关、可还原
   - [ ] **联机检测**：进联机菜单，所有修改类开关应被拒绝并自动关闭
   - [ ] 退出游戏无残留（重启后菜单默认关闭、无异常日志）

## 日志在哪

| 内容 | 路径 |
|---|---|
| BepInEx 主日志 | `<游戏目录>\BepInEx\LogOutput.log` |
| 耐久/元数据诊断 | `<游戏目录>\Diagnostics\*.log`（开启对应开关后生成） |
| 模组配置 | `<游戏目录>\BepInEx\config\zedzone.ultimateassistant.cfg` |

## 提交兼容性 Issue 时请附

1. 游戏版本（主菜单或 `ZEDZONE_Data\globalgamemanagers` 里的版本号）
2. BepInEx 版本（`BepInEx\LogOutput.log` 前 10 行）
3. 完整 `LogOutput.log`
4. 哪些功能失效、哪些正常
5. 是否装了其它模组（尤其是也改 `HumanCharacterController` 的）

## 不要相信的文档

`docs/internal/` 下的阶段报告是**当时的结论**，可能已被后续版本推翻
（例如 v1.3.2 修过一次"枪械耐久自锁死"）。适配时以代码和实机日志为准。

