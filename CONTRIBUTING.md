# 贡献指南（CONTRIBUTING）

感谢你有兴趣一起维护这个项目！

   

## 一、如何参与

你可以用以下任意方式参与：

| 方式 | 说明 |
|   |   |
| 提 Issue | 报 Bug、提功能需求、反馈兼容性问题 |
| 提 PR | 修 Bug、加功能、改进 UI、补翻译、改文档 |
| 补翻译 | 简化字 / 繁体 / 英文的文案在 `src/.../UI/Localization.cs` |
| 写文档 | 安装教程、常见问题、逆向分析记录 |
| 测试反馈 | 在 Issue 里报告"某功能在某个版本是否生效" |

   

## 二、想成为长期维护者（写权限）

本项目欢迎共同维护。获取写权限的方式：

1. 先通过 1–2 个 PR 展示你的改动（修 Bug / 加功能 / 文档均可）；
2. 在 Issue 或 PR 里说明希望成为维护者；
3. 仓库所有者会把你加入 **Collaborators**（Settings → Collaborators → Add people）。

成为维护者后你可以：

  直接向 `main` 推送（建议仍走 PR）；
  审核并合并他人的 PR；
  使用 `tools/release.ps1` 发布新版本。

   

## 三、开发流程

```bash
# 1. fork & clone
git clone https://github.com/<you>/ZED ZONE Assistant.git

# 2. 建分支
git checkout  b feat/your feature

# 3. 构建（需先装 BepInEx 6 IL2CPP 并启动过一次游戏）
$env:ZEDZONE_GAME_ROOT = "D:\SteamLibrary\steamapps\common\ZED ZONE"
pwsh tools/build.ps1  SkipInstall     # 必须 0 警告 0 错误

# 4. 实机验证后提交
git commit  m "feat: 你的改动说明"
git push origin feat/your feature
```

然后在 GitHub 上开 PR。

   

## 四、代码规范

  C# / .NET 6，`LangVersion latest`，`Nullable disable`；
  **每个功能必须有独立开关**，关闭时立即 `Unpatch`，不留后台线程；
  补丁首行必须 `try/catch`，异常时放行原逻辑；
  只对**本地玩家**生效（`PlayerAccessor.Resolve()` 后比较 `Pointer`）；
  禁止：修改全局 `GameConfigs`、改存档结构、影响 NPC/其它角色、猜测字段偏移写内存；
  日志用 `Log.Info/Warn/Error`，高频日志必须限流（`WarnOnce` / `ErrorOnce` / 计数预算）；
  新增 UI 文案必须同时提供三语（`zhCN` / `en` / `zhTW`），走 `L.T("key")`；
- **不再有联机门禁**：`GateOnline` 已移除，开关行为与配置一致；
  漏掉会被打回；
  新增配置项要同步 `docs/CONFIG.md`；新增/改动补丁要同步 `docs/API_PATCH_MAPPING.md`；
  用 AI 助手改代码的话，先读 [AGENTS.md](AGENTS.md)。

详细的架构说明与"新增功能检查清单"见 [DEVELOPMENT.md](DEVELOPMENT.md)，
文档地图见 [docs/README.md](docs/README.md)。

   

## 五、PR 检查清单

提交 PR 前请确认：

  [ ] `pwsh tools/build.ps1  SkipInstall` → **0 警告 0 错误**
  [ ] `dotnet run   project tests/ZedUaOfflineTests/ZedUaOfflineTests.csproj  c Release` 通过
  [ ] `python3 tools/ci/check_links.py` 通过（改过文档时）
- **不再有联机门禁**：`GateOnline` 已移除，开关行为与配置一致；
  [ ] 新增功能：`docs/CONFIG.md` 已登记；补丁改动：`docs/API_PATCH_MAPPING.md` 已更新
  [ ] 已在游戏内实机验证（或明确说明"仅编译验证"）
  [ ] 未破坏已有功能（耐久 / 弹药 / 防护 / ESP / 菜单 / 配置）
  [ ] 未提交 `bin/`、`obj/`、`Diagnostics/*.log`、备份 DLL、`research/`（反编译产物）
  [ ] 改过版本号的地方一致（只改 `Directory.Build.props`，`Plugin.cs` 由脚本同步）
  [ ] 更新了 `README.md` / `README_EN.md` 功能表（如适用）
  [ ] 更新了 `CHANGELOG.md`（需要有 `## vX.Y.Z` 小节，发版脚本会校验）

PR 描述请包含：

1. 改了什么、为什么；
2. 测试方式与结果（或未测试原因）；
3. 影响的游戏版本（当前基线 1.0.17）。

   

## 六、Issue 模板

仓库提供了两个模板：

  **Bug report**：请附游戏版本、BepInEx 版本、`LogOutput.log` 片段、复现步骤；
  **Feature request**：请说明用途、期望行为、是否愿意自己实现。

> 提 Issue 前请先搜索是否已有相同问题。

   

## 七、发布流程（维护者）

发布新版本只需一条命令：

```powershell
pwsh tools/release.ps1  Version 1.0.1
```

脚本会自动完成：

1. 校验版本号格式、工作区干净；
2. **校验 `CHANGELOG.md` 里已有 `## vX.Y.Z` 小节**（没有就中止）；
3. 同时更新 `Directory.Build.props`（Version/AssemblyVersion/FileVersion）与
   `Plugin.cs` 的 `PluginVersion`（两处必须一致，CI 也会校验）；
4. `dotnet build  c Release` 并校验产物 `FileVersion`；
5. 计算 SHA256 / MD5；
6. 提交 "release vX.Y.Z"、打 tag、推送 `main` + tag；
7. `gh release create`：正文自动带 CHANGELOG 对应小节 + 校验值 + 安装说明。

发版前先把本次改动写进 `CHANGELOG.md`，并注明哪些功能已实机验证。

   

## 八、行为准则

  尊重所有贡献者，技术讨论对事不对人；
  不接受"外挂/联机作弊"相关需求；
  不提交他人代码 / 资源而不标注来源与许可；
  不提交游戏反编译产物、游戏二进制拷贝或运行日志（`research/`、`Diagnostics/` 已在 `.gitignore` 里）。

