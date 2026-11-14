# 开发指南（DEVELOPMENT）

面向想继续开发本项目的开发者。项目基于 **BepInEx 6（IL2CPP）+ HarmonyX + Il2CppInterop**。

> 用 AI 助手改代码前，先读 [AGENTS.md](AGENTS.md)（里面是会被打回的硬约束）。
> 文档地图见 [docs/README.md](docs/README.md)。英文版：[DEVELOPMENT_EN.md](DEVELOPMENT_EN.md)。

---

## 1. 环境准备

| 工具 | 说明 |
|---|---|
| .NET SDK 6.0+（测试工程用 8.0） | `dotnet --version` |
| ZED ZONE 1.0.17 | 需要其 `BepInEx/interop/` 程序集作为编译引用 |
| BepInEx 6 IL2CPP | 首次运行游戏后生成 `interop/`（也可用创意工坊加载器的 `install.cmd`） |
| 可选：Ghidra / ILSpy | 逆向分析游戏原生逻辑 |

游戏目录**不再写死在 csproj 里**，优先级：

1. `-p:GameRoot=...`
2. 环境变量 `ZEDZONE_GAME_ROOT`

```powershell
$env:ZEDZONE_GAME_ROOT = "D:\SteamLibrary\steamapps\common\ZED ZONE"
```

两者都没有时构建会报一句可读的错误（`CheckGameRefs` 目标），而不是几十行"找不到类型"。

`Directory.Build.props` 里定义了 `GameRoot`/`BepInExCore`/`InteropDir` 的默认推导；
版本号也在这个文件里（唯一来源，见第 7 节）。

---

## 2. 构建与安装

```powershell
# 只编译
pwsh tools/build.ps1 -SkipInstall

# 编译 + 复制到 <游戏>\Mods\ZedZone.UltimateAssistant\
pwsh tools/build.ps1
```

脚本会自动从 Steam 注册表 + `libraryfolders.vdf` 找游戏目录（支持多库/非默认盘），
找不到时用 `-GameDir` 传。也可以直接用 dotnet：

```bash
dotnet build src/ZedZone.UltimateAssistant -c Release -p:GameRoot="E:\...\ZED ZONE"
```

产物：`src/ZedZone.UltimateAssistant/bin/ZedZone.UltimateAssistant.dll`

---

## 3. 代码架构

```
Plugin.cs
 └ ModConfig.Bind / BindFeatures        配置
 └ ModuleRegistry                        只读模块
 └ CheckGameVersion                      游戏版本兼容警告
 └ Features.SyncFromConfig              按开关安装/卸载 Harmony 补丁
 └ AssistantPanel                        MonoBehaviour 宿主
      ├ Update: 热键 / 输入守卫 / 定时读取 / 联机门禁自检 / 各项服务 Tick
      └ OnGUI : DynamicIsland → ZombieEsp → ClickGui
```

### 3.1 功能开关的统一模式

每个功能遵循同一套生命周期（见 `Core/Features.cs`）：

1. `ModConfig` 里声明 `ConfigEntry<bool>`；
2. `Features` 里声明 `XxxOn` 与 `SetXxx(bool)`；
3. `SetXxx` 第一行调用 `GateOnline(...)` —— **联机时拒绝启用**（fail-closed）；
4. `Features.Apply()` 里根据开关**安装/卸载**对应 Harmony 补丁；
5. UI 卡片调用 `ModConfig.Xxx.Value = v; Features.SetXxx(v);`；
6. `HotkeyManager` 注册该功能的快捷键与音效。

关闭开关 → 立即 `Unpatch`，不留后台循环。**新增功能请沿用这个模式，漏掉第 3 步会被打回。**

### 3.2 补丁规范

- 优先使用**公开托管 API**（interop 签名）而不是内存写入；
- 只对**本地玩家**生效：`PlayerAccessor.Resolve()` 后比较 `Pointer`；
- 每个补丁首行 `try/catch`，异常时放行原逻辑；
- 需要"只记录"的观察补丁使用 postfix，并在日志中限流（`Log.WarnOnce` / `Log.ErrorOnce`）；
- **不要修改全局 `GameConfigs`、存档结构、其它角色/NPC**；
- 修改未知内存 / 猜测字段偏移是被禁止的（以 `docs/internal/diagnostics/` 里的逆向证据为准）。

### 3.3 IL2CPP 注意事项

- `Update()` 每帧可能被调度多次 → 用 `Time.frameCount` 去重；
- 已销毁的 IL2CPP 对象访问会抛 `Handle is not initialized` → 每次使用前判空并 try/catch；
- 对象引用**不要缓存**（`PlayerAccessor` 每次重新解析），避免死亡/重生/场景切换后指向旧对象；
- 属性只有 getter 时不能赋值（如 `BasicRangedWeapon.durabilityFloat`），需要找底层字段或换方案。

### 3.4 新增一个功能的检查清单

- [ ] `ModConfig` 新增配置项（独立 key，**默认关闭**）+ `docs/CONFIG.md` 登记
- [ ] `Features` 新增开关 + `GateOnline` 门禁 + 安装/卸载逻辑
- [ ] 补丁只作用于本地玩家，关闭即卸载 + `docs/API_PATCH_MAPPING.md` 补一行
- [ ] UI 新增卡片（`UI/ClickGui.cs`），三语文案（`UI/Localization.cs`）
- [ ] `HotkeyManager` 注册快捷键
- [ ] `pwsh tools/build.ps1 -SkipInstall` 0 警告 0 错误
- [ ] 离线测试通过 + `python3 tools/ci/check_links.py` 通过
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
| `docs/` | 文档（地图见 `docs/README.md`） |
| `docs/reference/` | 游戏机制研究（数据/生存/生命/状态/联机） |
| `docs/internal/` | 历史阶段报告、诊断报告、原始证据（存档用，不必读） |
| `tests/ZedUaOfflineTests/` | 离线单元测试（控制台断言器） |
| `tools/` | `build.ps1` / `release.ps1` / `ci/` |
| `.github/workflows/ci.yml` | CI：离线测试 + 版本一致性 + 文档链接 |

---

## 5. 测试

```powershell
dotnet run --project tests/ZedUaOfflineTests/ZedUaOfflineTests.csproj -c Release
```

> 注意是 `dotnet run` 而**不是** `dotnet test`：测试工程是 `<OutputType>Exe</OutputType>` 的
> 控制台断言器（自己实现 `Check()`），不是 xUnit 工程，`dotnet test` 不会执行 `Program.Main`。

离线测试只覆盖与引擎无关的纯逻辑（`PlayerSnapshot` / `RangeSelection` 等），
通过 `<Compile Include="../../src/...">` 直接链接源文件，因此**不需要游戏**。

**实机验证无法自动化**：本模组修改的是运行中的游戏状态，所有功能都需要在游戏里手动验证。
提交 PR 时请注明哪些功能已实机验证、哪些仅编译通过。回归清单见
[docs/COMPATIBILITY.md](docs/COMPATIBILITY.md) 第 6 步。

---

## 6. 逆向分析方法（可选）

`docs/internal/diagnostics/GUN_DURABILITY_NATIVE_REVERSE_ENGINEERING.md` 记录了一个完整案例：

1. 运行期通过 IL2CPP 元数据 API 拿到 `MethodInfo*` → `methodPointer` → RVA；
2. Ghidra headless 导入 `GameAssembly.dll`，在 RVA 处反编译；
3. 用元数据字段偏移交叉验证反编译里的 `[reg+0xNN]`；
4. 最终定位 `durabilityFloat = weaponData.properties[0] / weaponAttr.durabilityMax`。

脚本：`docs/internal/diagnostics/ghidra/DumpDurabilityGetter.java`。

---

## 7. 发布流程

维护者一条命令完成发版：

```powershell
pwsh tools/release.ps1 -Version 1.0.2
```

脚本依次执行：

1. 校验版本号格式（`x.y.z`）与工作区干净
2. **校验 `CHANGELOG.md` 已存在 `## vX.Y.Z` 小节**（没有就中止，可用 `-AllowNoChangelog` 跳过）
3. 同时修改 **`Directory.Build.props`**（Version / AssemblyVersion / FileVersion）
   与 **`Plugin.cs` 的 `PluginVersion`** —— 两处必须一致，CI 也会校验
4. `dotnet build -c Release`，并校验产物 `FileVersion` 与发布版本一致
5. 计算 SHA256 + MD5
6. 提交 `release vX.Y.Z` → 打 tag → 推送 `main` + tag
7. `gh release create`：正文自动带上 CHANGELOG 对应小节 + SHA256/MD5 + 安装说明

可选参数：

| 参数 | 作用 |
|---|---|
| `-Notes "..."` | 追加自定义 Release 说明 |
| `-Draft` | 创建草稿 Release |
| `-SkipPush` | 只本地提交与打 tag（不推送、不建 Release） |
| `-AllowNoChangelog` | 允许 CHANGELOG 缺当前版本小节（不建议） |
| `-GameDir "..."` | 指定游戏目录（默认读 `ZEDZONE_GAME_ROOT`） |

> 发版前请先更新 `CHANGELOG.md`，并把"实机验证结论"写进 Release 说明。

---

## 8. 提交规范

- 一个功能/修复一个 PR；
- Commit message 用中文即可，说明"改了什么 + 为什么"；
- **不要提交** `bin/`、`obj/`、`Diagnostics/*.log`、备份 DLL、`research/`（反编译产物与证据），`.gitignore` 已覆盖；
- 提交前本地跑一遍 CI 的三件事：离线测试、版本一致性、文档链接；
- PR 请附：构建结果（0 警告 0 错误）+ 实机验证结论（或说明未验证）。

```powershell
dotnet run --project tests/ZedUaOfflineTests/ZedUaOfflineTests.csproj -c Release
bash tools/ci/check-version.sh        # 需要 bash；Windows 可用 Git Bash / WSL
python3 tools/ci/check_links.py
```
