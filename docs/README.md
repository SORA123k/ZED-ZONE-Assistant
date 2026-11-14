# 文档地图

先看这张表，别在 40 多篇旧报告里迷路。

## 面向玩家（怎么装、怎么用）

| 文档 | 内容 |
|---|---|
| [README.md](../README.md) / [README_EN.md](../README_EN.md) | 功能一览、环境要求、安装、配置文件、已知限制 |
| [GUIDE.md](../GUIDE.md) / [GUIDE_EN.md](../GUIDE_EN.md) | 新手教程：从零装到能用，含报错排查 |
| [INSTALL.md](../INSTALL.md) / [INSTALL_EN.md](../INSTALL_EN.md) | 安装细节：创意工坊加载器 / 手动 BepInEx 两条路 |
| [CHANGELOG.md](../CHANGELOG.md) | 每个版本改了什么 |

## 面向开发者（怎么改、怎么发）

| 文档 | 内容 |
|---|---|
| [DEVELOPMENT.md](../DEVELOPMENT.md) / [DEVELOPMENT_EN.md](../DEVELOPMENT_EN.md) | 架构、构建、补丁规范、新增功能检查清单 |
| [CONTRIBUTING.md](../CONTRIBUTING.md) / [CONTRIBUTING_EN.md](../CONTRIBUTING_EN.md) | 分支 / PR 流程、代码风格、获得维护权限 |
| [AGENTS.md](../AGENTS.md) | AI 编码助手的项目规则（本项目的硬约束） |
| [CONFIG.md](CONFIG.md) | **全部配置项**（69 项，按 section 列出默认值/范围/说明） |
| [COMPATIBILITY.md](COMPATIBILITY.md) | 游戏版本适配矩阵 + 游戏更新后的重新适配步骤 |
| [API_PATCH_MAPPING.md](API_PATCH_MAPPING.md) | 每个 Harmony 补丁的目标方法 / 类型 / 作用 |
| [SECURITY.md](../SECURITY.md) | 安全边界、漏洞上报、存档风险 |

## 参考研究（游戏内部机制，写新功能时查）

| 文档 | 内容 |
|---|---|
| [reference/GAME_PLAYER_DATA.md](reference/GAME_PLAYER_DATA.md) | 玩家数据结构与字段来源 |
| [reference/SURVIVAL_SYSTEM_RESEARCH.md](reference/SURVIVAL_SYSTEM_RESEARCH.md) | 饥饿/水分/体力/体温等生存系统 |
| [reference/HEALTH_SYSTEM_RESEARCH.md](reference/HEALTH_SYSTEM_RESEARCH.md) | 生命值、伤害、肢体 |
| [reference/STATUS_EFFECT_RESEARCH.md](reference/STATUS_EFFECT_RESEARCH.md) | Buff / 状态效果 |
| [reference/MULTIPLAYER_COMPATIBILITY.md](reference/MULTIPLAYER_COMPATIBILITY.md) | 联机检测依据（为什么 fail-closed） |

## 内部过程记录（历史存档，不必读）

`internal/` 下是开发过程的阶段报告、风险评估、运行证据和一次性报告。
它们**保留是为了可追溯**（某个结论是怎么得出的），但结论可能已被后续版本推翻，
不要当现行文档使用。

| 目录 | 内容 |
|---|---|
| [internal/README.md](internal/README.md) | 内部档案总索引（41 篇，含各阶段报告说明与逆向内容声明） |
| [internal/reports/](internal/reports/) | `FINAL_RELEASE_REPORT.md`（v1.0.0 交付报告）、`PHASE_2_REPORT.md` |
| [internal/diagnostics/](internal/diagnostics/) | 耐久诊断报告、Ghidra 原生逆向记录与脚本 |
| [internal/research/](internal/research/) | 旧项目（FPS 优化模组）可复用工程模式提取 |
| [internal/evidence/](internal/evidence/) | 各阶段探针跑出来的原始 txt 证据 |

## 文档维护约定

- 新增/移动文档后跑 `python3 tools/ci/check_links.py`（CI 也会跑），别留 404。
- 面向玩家的文档放仓库根，面向开发者的放 `docs/`，过程产物放 `docs/internal/`。
- 配置项改了要同步 `docs/CONFIG.md`；补丁改了要同步 `docs/API_PATCH_MAPPING.md`。
