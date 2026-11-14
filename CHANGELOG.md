# 更新日志（CHANGELOG）

本项目版本号在 v1.0.0 起重新编号为正式版序列。格式参考 Keep a Changelog，
版本号遵循语义化版本（主.次.修订）。

---

## v1.0.1 — 2026-11-15（工程质量修复版）

本次发布**不新增玩法**，只修工程与安全问题，并把文档补齐。

### 修复

- **联机门禁漏洞**：此前只有「无限弹药 / 无需换弹 / ESP / 一键恢复」接入联机检测，
  README 却声称"所有状态修改类功能自动禁用"。现在
  「不会扣血 / 体力 / 饱食 / 水分」「自动补充材料」「强制奔跑」「移动加速」
  「工具耐久」「枪械耐久」「取消过热」「不会卡壳」全部接入
  `Features.GateOnline`，并在进入联机会话（检测发生在启用之后）时由
  `Features.EnforceOffline` **主动关闭**。拒绝启用时只关运行时开关、**不改写配置文件**（避免检测误判把用户偏好清掉），回到单机后自动按配置恢复。
- **枪械耐久单独开启不生效**：`SetWeaponDurability` 之前没有调用 `Features.Apply()`，
  只开这一个功能时补丁不会被安装。已修正。
- **卸载残留**：`Plugin.Unload` 现在先 `Features.Shutdown()`（卸载功能层补丁、还原全亮、
  清 ESP/耐久缓存）再清 `PatchManager`；`Features.Shutdown` 也会还原 `FullBright` /
  `ZombieEsp` / `DurabilityLock` 状态。
- **默认值不安全**：`WeaponDurability`、`ItemDurabilityLock` 的默认值与文案不符（文案写"默认关闭"、
  实际是 `true`）；`MetaDiag`、`DurabilityDiag`、`GrowthLog`、`FatigueProbe`
  这些诊断探针默认开启，每次启动都写日志。以上现在**全部默认关闭**。
- **版本号三处不一致**：csproj 里的 `<Version>` 一直是 `0.2.0`，导致发出去的 DLL
  程序集版本是 `0.2.0.0`。版本号改为由 `Directory.Build.props` 单点定义，
  发版脚本同时更新它和 `Plugin.cs`，CI 校验一致性。
- **构建不可移植**：csproj 与 `tools/build.ps1` 写死了 `<apps>\...` 本机路径，
  别人 clone 后必然失败。现在支持 `ZEDZONE_GAME_ROOT` 环境变量 / `-p:GameRoot=`，
  构建脚本会自动扫描 Steam 库目录；缺 interop 时给出可读报错。
- **删除死代码**：`UI/PanelRenderer.cs`（27KB，Phase 2 只读面板）已无任何调用方，移除；
  `Patch/PatchManager.cs` 里"Phase 2 必须为 0"的过时逻辑改为真实的补丁计数与卸载兜底。

### 新增

- **游戏版本兼容提示**：`Plugin.TestedGameVersion = 1.0.17`，版本不一致时在日志里给出
  醒目告警（只告警不阻断），并指向 `docs/COMPATIBILITY.md`。
- **文档体系**：
  - `docs/README.md` — 文档地图（54 篇 md 现在有索引了）
  - `docs/CONFIG.md` — 全部 69 个配置项 + 16 个快捷键
  - `docs/COMPATIBILITY.md` — 版本矩阵、失效点、游戏更新后的适配步骤与回归清单
  - `SECURITY.md` — 安全边界与漏洞上报
  - `AGENTS.md` — AI 编码助手的硬约束
  - `docs/internal/` — 原 Phase 报告 / 诊断报告 / 证据 txt 归档（不再散落在根目录与 `research/`）
  - `docs/reference/` — 游戏机制研究
- **CI**（`.github/workflows/ci.yml`）：离线单元测试、版本号一致性、Markdown 链接检查。
- **工程基建**：`ZedZone.Assistant.sln`、`.editorconfig`、`.gitattributes`、
  `tools/ci/check-version.sh`、`tools/ci/check_links.py`、Issue 模板选择页。
- **安装教程**：新增「Steam 创意工坊代码模组加载器」一键安装路径（含来源标注），
  与手动装 BepInEx 并列；新增 SHA256 校验说明。

### 文档修正

- README / README_EN 的 fail-closed 描述改为与实际代码一致（逐项列出被禁用的功能）。
- `DEVELOPMENT.md` 的测试命令 `dotnet test` 改为 `dotnet run`（测试工程是控制台断言器）。
- 移除 README 里"GameRoot 默认 <apps>\..."的说明，改为两种显式指定方式。
- 仓库不再包含运行日志、备份 DLL、游戏截图（`.gitignore` 覆盖 `research/`、`Diagnostics/`）。

### 已知限制

- 测试仍只覆盖与引擎无关的纯逻辑（`PlayerSnapshot` / `RangeSelection`），补丁逻辑依赖实机回归。
- 历史提交里仍留有已删除的日志与旧 DLL（blob 未清理），如需彻底清除要重写 git 历史。
- `GUIDE.md` / `INSTALL.md` / `DEVELOPMENT.md` / `CONTRIBUTING.md` 仍为中文单语。

---

## v1.0.0 — 2026-11-15（首个正式版）

### 菜单 / UI

- ClickGUI 风格主界面：分类侧栏（Combat / Player / Visuals / Settings）+ 卡片式功能项
- 内容区垂直滚动（滚轮、滚动条同步、按页记忆）
- 滑条支持点击跳转 + 按住拖动 + 实时数值（修复了"滑条无法拖动"）
- 7 套主题：默认深色 / 黑金 / 蓝色科技 / 紫色 / 青绿 / 白色浅色 / 红黑
- 字体大小 12–24 px 实时调整，行高自适应
- 三语本地化：简体中文 / English / 繁體中文，切换即时生效
- 灵动岛：顶部胶囊提示，开关功能时显示"功能 · 已启用/已关闭"，支持多条目与快速覆盖
- 功能开关音效（启用/关闭两种提示音）
- 菜单可拖动，位置自动保存
- 打开菜单时：阻止角色操作、禁用游戏 UI 射线（防点击穿透）、强制显示鼠标；关闭后全部还原

### 战斗

- 无限弹药：不消耗备用弹药
- 无需换弹：弹匣为 0 也能开火，R 键换弹保持原生
- 不会卡壳：覆盖卡壳判定并清除 `MalfunctionFlag`
- 工具/近战/装备耐久锁定（`ItemData.durability`，只增不减）
- 枪械耐久锁定（写入 `ItemData.properties[0]`，逆向确认真实字段）
- 取消过热（仅玩家武器）
- 自动补充材料：制造/修复视为材料充足；岛屿船可强制完成建造

### 玩家

- 不会扣血 / 不会扣体力 / 不会扣饱食度 / 不会扣水分（四个独立开关）
- 一键恢复生命 / 精力体力 / 饱食 / 水分（按钮，点一次执行一次）
- 强制奔跑：开火时自动取消奔跑、停火自动恢复
- 移动加速：1.0x–20.0x（含翻滚加速）
- 只读肢体状态面板

### 视觉

- 僵尸 ESP：名称 / 距离 / 方框，含视野外屏幕边缘指示
- 地图全亮：夜间提升 URP 2D 全局光与环境光，不改时间/天气/AI

### 其它

- 每个功能独立快捷键（可绑单键，ESC 清空）
- 菜单快捷键支持多绑定，Insert 永久有效
- 性能模式：降低 ESP/耐久/诊断刷新频率以提升帧数
- 全功能配置独立持久化

### 逆向与诊断（仓库内文档）

- `docs/internal/diagnostics/DURABILITY_DIAGNOSTIC_REPORT.md`：耐久字段实测结论
- `docs/internal/diagnostics/GUN_DURABILITY_NATIVE_REVERSE_ENGINEERING.md`：Ghidra 逆向完整报告
- `docs/internal/diagnostics/PHASE4_11_REPORT.md`：原生字段探查与实现评估
- 运行期只读诊断模块（耐久字段探针、IL2CPP 元数据导出）

### 已知限制

- 仅支持离线单人；联机自动禁用状态修改
- 基于游戏 1.0.17 的 interop 签名，游戏大版本更新后可能需要适配
- 岛屿逃生船的强制完成建造可能影响任务流程，请先备份存档
