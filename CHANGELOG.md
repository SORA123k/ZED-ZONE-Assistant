# 更新日志（CHANGELOG）

本项目版本号在 v1.0.0 起重新编号为正式版序列。格式参考 Keep a Changelog，
版本号遵循语义化版本（主.次.修订）。

---

## v1.2.2 — 2026-11-18

### 修复

- 保持奔跑：修复换弹结束后卡在行走（必须切枪才恢复）的问题。改为奔跑全程不中断，
  并新增补丁使其在奔跑状态下也能正常换弹（奔跑时不收枪、不取消换弹）。
- 无限弹药：改为直接接管弹匣内子弹数（`GetLoadedAmmo`），弹匣显示无限、射击不消耗弹药，
  不再出现"显示 9999 但提示没有弹药"或"换弹只加 1 发"的情况。
- 一键维修小船：移除会持续抛异常的"强制完成建造"调用，只保留缺料补货。

### 变更

- 「无需换弹」说明改为：无需换弹，提高射速并取消狙击枪后摇。
- 「无限弹药」说明改为：射击不消耗弹药。

### 兼容

- 适配游戏 **1.0.19**（同时保留 1.0.17 / 1.0.18 的验证记录）。

---
## v1.2.1 — 2026-11-17（适配游戏 1.0.18）

### 新增

- 立即清空辐射（按钮，位于「一键恢复」分组）：把本地玩家辐射值置 0。
- 保持 0 辐射（开关，位于「防护」分组）：持续把辐射压到 0。
- 连点器（战斗辅助）：开关 + 0–1000ms 间隔滑条，系统级左键，仅游戏窗口前台时生效。
- ESP 血量显示：显示 `hp / FullHP` 数字与血条。

### 变更

- 「自动补充材料」更名为「一键维修小船」（配置键 `AutoSupply` 不变）。
- ESP 识别方式改为 `FindObjectsByType<BasicCharacterController>` + 排除人类/玩家/友方 + 存活过滤。
- 一键恢复（血量/体力/饱食/水分）改为与「立即清空辐射」一致的卡片样式。
- 战斗辅助移除「一键修复卡壳」按钮（`WeaponUtil.ClearMalfunction` 代码保留）。
- 音效模块重写：不再写死路径，改为多目录查找 + `[Sound] OnPath/OffPath` 配置（不内置音频文件）。

### 移除

- 静默自瞄、自动开火、墙体遮挡检测：相关配置、快捷键、菜单分组、文档一并删除。
- 联机自动强制关闭：`GateOnline`、`EnforceOffline`、写入门联机拦截、一键恢复与探针的联机拒绝全部移除；
  模组不再检测联机状态，也不再自动关闭任何功能。

---
## v1.0.3 — 2026-11-15（诊断日志路径修复 + 文档一致性）

### 修复

- **诊断日志写到作者机器上，别人永远写不出来**：`DurabilityDiag` / `Il2CppMetaDiag`
  的日志目录写死成作者本机的绝对路径，在别人机器上 `Directory.CreateDirectory` 直接失败
  （异常被吞掉），表现就是"诊断开关打开了但没有日志文件"。
  现在改为跟随游戏目录 `<游戏目录>\Diagnostics`（`BepInEx.Paths.GameRootPath`），
  取不到时回退到当前工作目录。
- **免责声明与事实不符**：README 写"仓库不包含反编译产物"，但
  `docs/internal/diagnostics/` 里确实有对 `GameAssembly.dll` 的反汇编片段。
  已改为准确表述，并在该报告顶部加"用途与边界"声明（仅单机研究、不含游戏本体文件、权利方可要求移除）。

### 新增

- `docs/internal/README.md`：**41 篇内部档案的总索引**（此前只能从目录列表逐个翻，
  等于没有发布）。
- `tools/ci/check_config_docs.py`：校验 `docs/CONFIG.md` 覆盖全部 69 个配置项与 16 个快捷键、
  且没有代码中不存在的 key；已接入 CI（第 5 个 job）。此前 CONFIG 文档只能靠人肉维护。
- 程序集版本 → `1.0.3`。

---

## v1.0.2 — 2026-11-15（文档与文案修复版）

### 新增

- **英文文档**：`GUIDE_EN.md` / `INSTALL_EN.md` / `DEVELOPMENT_EN.md` / `CONTRIBUTING_EN.md`，
  README_EN 与 docs/README 已互相链接（中文文档保持不变）。
- **CI 增加「UI 文案一致性」检查**：`tools/ci/check_localization.py` 校验本地化表每条有 3 种语言、
  非空，且 UI 引用的 key 都存在。

### 修复

- **菜单里有 5 处文案显示成原始 key**：`menu_keys` / `bind` / `clear` / `press_key` / `not_bound`
  在 `UI/Localization.cs` 里缺失（`L.T()` 找不到就返回 key 本身），已补齐三语。
- **归档文档脱敏**：`docs/internal/`、`docs/reference/` 里 28 处本机绝对路径
  （作者本机绝对路径）替换为 `<游戏目录>` / `<dotnet>` 等占位符。

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
- **构建不可移植**：csproj 与 `tools/build.ps1` 写死了作者本机的绝对路径，
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
- 移除 README 里"GameRoot 默认指向作者机器"的说明，改为两种显式指定方式。
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







