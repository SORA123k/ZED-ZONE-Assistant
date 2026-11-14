## 这个 PR 做了什么

<!-- 一句话说明 -->

## 为什么

<!-- 背景 / 问题 / 关联 Issue -->

## 测试情况

- [ ] `pwsh tools/build.ps1 -SkipInstall` → 0 警告 0 错误
- [ ] 离线测试通过：`dotnet run --project tests/ZedUaOfflineTests/ZedUaOfflineTests.csproj -c Release`
- [ ] 文档链接检查通过：`python3 tools/ci/check_links.py`（改过文档时）
- [ ] 已在游戏内实机验证（说明验证了哪些功能）
- [ ] 仅编译验证（说明原因）

## 影响范围

- 游戏版本基线：1.0.17（若变更，请同步 `docs/COMPATIBILITY.md` 与 `Plugin.TestedGameVersion`）
- 是否影响已有功能：
- 是否新增/变更配置项（需同步 `docs/CONFIG.md`）：
- 是否变更补丁目标（需同步 `docs/API_PATCH_MAPPING.md`）：

## 检查清单

- [ ] 新功能有独立开关，关闭即卸载补丁
- [ ] 新功能经过 `Features.GateOnline(...)`（联机 fail-closed）
- [ ] 补丁只作用于本地玩家
- [ ] 未修改 GameConfigs / 存档结构
- [ ] UI 文案三语齐全
- [ ] 未提交 bin/obj/日志/备份 DLL/`research/`（反编译产物）
- [ ] 版本号只在 `Directory.Build.props` 维护，`Plugin.cs` 与之一致
