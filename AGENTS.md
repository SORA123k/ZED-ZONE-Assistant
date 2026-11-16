# 给 AI 编码助手的项目规则（AGENTS.md）

本项目的绝大部分代码由 AI 编写、由人类提需求与验证。为了避免"能跑就行"的改动破坏已有约束，
任何 AI 助手（Copilot / Claude / Cursor / Codex / DSH 等）在本仓库改代码前请先读本文件。

## 硬约束（改动前必须确认，破坏会直接被打回）

1. **补丁生命周期**：`Features.Apply()` 是唯一安装/卸载 Harmony 补丁的地方。
   功能没开 → 不装补丁。不要用"装了补丁 + prefix 里 early return"代替。
   原因：IL.CPP 下 Prefix 参数封送在补丁体之前，遇到已销毁对象会抛
   `Handle is not initialized`，try/catch 拦不住（见 `Patch/PatchManager.cs` 注释）。
2. **无联机门禁**：模组不检测联机状态、不自动关闭功能（`GateOnline` 已移除）；
   新增功能时照着现有 `Set*` 方法写。`PlayerWriteGate.IsMultiplayer` 检测异常时按"联机"处理。
3. **单一写入门**：玩家属性写入只能走 `PlayerWriteGate.Restore`（属性 setter + 动态上限校验 + 回读）。
   不要新增"直接给字段赋值"的代码路径。
4. **不缓存玩家引用**：每次读取通过 `PlayerAccessor.Resolve` 重新解析，不要把它存进静态字段。
5. **只增不减**：耐久/数值类写回只能往上，不允许把玩家数据写低。
6. **不伪造数据**：读不到就标记 `ReadState.Failed` 并在 UI 上红字显示，不要用 0 或猜测值填充。

## 版本号

- 唯一事实来源是仓库根的 `Directory.Build.props`（`Version`）。
- `Plugin.cs` 的 `PluginVersion` 必须与之相同；发版只跑 `pwsh tools/release.ps1 -Version x.y.z`。
- 不要手改任意一处版本号就提交，CI 会校验不一致（`tools/ci/check-version.sh`）。

## 改动自检清单

```bash
# 1. 编译（需要游戏 + BepInEx interop）
pwsh tools/build.ps1 -SkipInstall

# .. 离线测试
dotnet run --project tests/ZedUaOfflineTests/ZedUaOfflineTests.csproj -c Release

# 3. 文档链接
python3 tools/ci/check_links.py
```

- 新增配置项：同时在 `Core/ModConfig.cs` 里定义、在 `docs/CONFIG.md` 里登记。
- 新增用户可见功能：更新 `README.md` 功能表 + `CHANGELOG.md` + 菜单本地化（`UI/Localization.cs` 三语）。
- 新增补丁：在 `docs/API_PATCH_MAPPING.md` 里补一行（目标方法 / 前缀后缀 / 作用）。
- 删除/移动文档：跑一次链接检查，别留 404（历史上出现过 Release 说明指向改名前的仓库）。

## 代码风格

- 保持现有风格：`internal static` 工具类 + `try/catch` 包裹所有 IL.CPP 调用。
- 注释写"为什么"（尤其逆向结论、绕过某个坑的原因），不写"这行在赋值"。
- 不要为了好看重排整个文件：本项目 diff 需要可审查。

