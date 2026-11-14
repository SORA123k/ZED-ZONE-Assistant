# 开发指南（DEVELOPMENT）

面向想继续开发本项目的开发者。项目基于 **BepInEx 6（IL2CPP）+ HarmonyX + Il2CppInterop**。

---

## 1. 环境准备

| 工具 | 说明 |
|---|---|
| .NET SDK 6.0+ | `dotnet --version` |
| ZED ZONE 1.0.17 | 需要其 `BepInEx/interop/` 程序集作为编译引用 |
| BepInEx 6 IL2CPP | 首次运行游戏后生成 `interop/` |
| 可选：Ghidra / ILSpy | 逆向分析游戏原生逻辑 |

编译引用路径在 `src/ZedZone.UltimateAssistant/ZedZone.UltimateAssistant.csproj` 中：

```xml
<GameRoot><游戏目录>\</GameRoot>
<BepInExCore>$(GameRoot)BepInEx\core\</BepInExCore>
<InteropDir>$(GameRoot)BepInEx\interop\</InteropDir>
```

换机器时修改 `GameRoot` 或构建时传参：

```bash
dotnet build -c Release -p:GameRoot="E:\Steam\steamapps\common\ZED ZONE\"
```

---

## 2. 构建与安装

```bash
cd src/ZedZone.UltimateAssistant
dotnet build -c Release
```

产物：`bin/ZedZone.UltimateAssistant.dll`

一键构建 + 安装（会检查游戏是否在运行）：

```powershell
pwsh tools/build.ps1
```

---

## 3. 代码架构

```
Plugin.cs
 └ ModConfig.Bind / BindFeatures        配置
 └ ModuleRegistry                        只读模块（Phase 2）
 └ Features.SyncFromConfig              按开关安装/卸载 Harmony 补丁
 └ AssistantPanel                        MonoBehaviour 宿主
      ├ Update: 热键 / 输入守卫 / 定时读取 / 各项服务 Tick
      └ OnGUI : DynamicIsland → ZombieEsp → ClickGui
```

### 3.1 功能开关的统一模式

每个功能遵循同一套生命周期（见 `Core/Features.cs`）：

1. `ModConfig` 里声明 `ConfigEntry<bool>`；
2. `Features` 里声明 `XxxOn` 与 `SetXxx(bool)`；
3. `Features.Apply()` 里根据开关**安装/卸载**对应 Harmony 补丁；
4. UI 卡片调用 `ModConfig.Xxx.Value = v; Features.SetXxx(v);`
5. `HotkeyManager` 注册该功能的快捷键与音效。

关闭开关 → 立即 `Unpatch`，不留后台循环。**新增功能请沿用这个模式**。

### 3.2 补丁规范

- 优先使用**公开托管 API**（interop 签名）而不是内存写入；
- 只对**本地玩家**生效：`PlayerAccessor.Resolve()` 后比较 `Pointer`；
- 每个补丁首行 `try/catch`，异常时放行原逻辑；
- 需要"只记录"的观察补丁使用 postfix，并在日志中限流（`Log.WarnOnce` / `Log.ErrorOnce`）；
- **不要修改全局 `GameConfigs`、存档结构、其它角色/NPC**；
- 修改未知内存 / 猜测字段偏移是被禁止的（见 `Diagnostics/NativeAnalysis/` 里的逆向报告，一切以证据为准）。

### 3.3 IL2CPP 注意事项

- `Update()` 每帧可能被调度多次 → 用 `Time.frameCount` 去重；
- 已销毁的 IL2CPP 对象访问会抛 `Handle is not initialized` → 每次使用前判空并 try/catch；
- 对象引用**不要缓存**（`PlayerAccessor` 每次重新解析），避免死亡/重生/场景切换后指向旧对象；
- 属性只有 getter 时不能赋值（如 `BasicRangedWeapon.durabilityFloat`），需要找底层字段或换方案。

### 3.4 新增一个功能的检查清单

- [ ] `ModConfig` 新增配置项（独立 key，默认关闭）
- [ ] `Features` 新增开关 + 安装/卸载逻辑
- [ ] 补丁只作用于本地玩家，关闭即卸载
- [ ] UI 新增卡片（`UI/ClickGui.cs`），三语文案（`UI/Localization.cs`）
- [ ] `HotkeyManager` 注册快捷键
- [ ] `dotnet build` 0 警告 0 错误
- [ ] 实机验证（不能只凭编译通过）
- [ ] 更新 `README.md` 功能表与 `CHANGELOG.md`

---

## 4. 目录说明

| 目录 | 内容 |
|---|---|
| `src/ZedZone.UltimateAssistant/` | 插件源码 |
| `src/.../Core/` | 业务逻辑（无 Unity UI 依赖） |
| `src/.../Patch/` | Harmony 补丁 |
| `src/.../UI/` | IMGUI 菜单 |
| `docs/` | 各阶段研究、验证报告 |
| `Diagnostics/` | 耐久/枪械原生逆向报告与诊断说明 |
| `tests/` | 离线单元测试（`dotnet test`） |
| `tools/build.ps1` | 构建 + 安装脚本 |

---

## 5. 测试

```bash
cd tests/ZedUaOfflineTests
dotnet test
```

离线测试只覆盖纯逻辑（数值范围、范围选择等），不涉及 Unity 运行时。

**实机验证无法自动化**：本模组修改的是运行中的游戏状态，所有功能都需要在游戏里手动验证。提交 PR 时请注明哪些功能已实机验证、哪些仅编译通过。

---

## 6. 逆向分析方法（可选）

`Diagnostics/NativeAnalysis/GUN_DURABILITY_NATIVE_REVERSE_ENGINEERING.md` 记录了一个完整案例：

1. 运行期通过 IL2CPP 元数据 API 拿到 `MethodInfo*` → `methodPointer` → RVA；
2. Ghidra headless 导入 `GameAssembly.dll`，在 RVA 处反编译；
3. 用元数据字段偏移交叉验证反编译里的 `[reg+0xNN]`；
4. 最终定位 `durabilityFloat = weaponData.properties[0] / weaponAttr.durabilityMax`。

脚本：`Diagnostics/NativeAnalysis/ghidra_scripts/DumpDurabilityGetter.java`。

---

## 7. 发布流程

维护者一条命令完成发版：

```powershell
pwsh tools/release.ps1 -Version 1.0.1
```

脚本依次执行：

1. 校验版本号格式（`x.y.z`）
2. 检查工作区是否干净
3. 修改 `Plugin.cs` 的 `PluginVersion`
4. `dotnet build -c Release`（0 警告 0 错误才继续）
5. 提交 `release vX.Y.Z`
6. 打 tag `vX.Y.Z` 并推送 `main` + tag
7. `gh release create` 创建 GitHub Release 并附 DLL（打印大小与 MD5）

可选参数：

| 参数 | 作用 |
|---|---|
| `-Notes "..."` | 自定义 Release 说明 |
| `-Draft` | 创建草稿 Release |
| `-SkipPush` | 只本地提交与打 tag（不推送、不建 Release） |
| `-Repo owner/name` | 指定仓库（默认本仓库） |

> 发版前请先更新 `CHANGELOG.md`，并把"实机验证结论"写进 Release 说明。

## 8. 提交规范

- 一个功能/修复一个 PR；
- Commit message 用中文即可，说明"改了什么 + 为什么"；
- 不要提交 `bin/`、`obj/`、`Diagnostics/*.log`、备份 DLL（`.gitignore` 已覆盖）；
- PR 请附：构建结果（0 警告 0 错误）+ 实机验证结论（或说明未验证）。

