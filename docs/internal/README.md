# 内部过程档案（docs/internal）

这里放的是**开发过程记录**，不是使用文档。保留它们是为了让"某个结论是怎么得出的"可以追溯，
但请注意：

- **结论可能已过时**：后续版本推翻过早期判断（例如 v1.3.2 修复过"枪械耐久自锁死"），
  排查问题时以代码和实机日志为准。
- **不是用户的必读材料**：安装/使用看仓库根的 [README.md](../../README.md)、[GUIDE.md](../../GUIDE.md)、[INSTALL.md](../../INSTALL.md)。
- **不要照抄里面的实验步骤**：部分实验是当时为了取证临时做的，可能有副作用。

## 关于逆向分析内容（务必阅读）

`diagnostics/` 下的文件包含对游戏原生二进制（`GameAssembly.dll`）的**只读**分析：
反汇编片段、方法偏移推导、以及据此还原的逻辑。

- 仅用于**单机离线**环境下的兼容性研究（判断耐久/战斗字段的真实来源，避免写错字段毁存档）；
- 仓库**不包含**游戏原始资源、`GameAssembly.dll`、`global-metadata.dat` 或运行日志；
- 请勿把这些材料用于联机环境，或再分发游戏本体/反编译产物；
- 如果你是该游戏的权利方并认为这些分析记录不妥，请通过 [SECURITY.md](../../SECURITY.md) 的联系渠道告知，我会移除。

## 阶段报告（Phase 3 / 3A / 3B）

| 文件 | 内容 |
|---|---|
| [PHASE_3_ROADMAP.md](PHASE_3_ROADMAP.md) | Phase 3 路线图与决策门 |
| [PHASE_3_SAFETY_DESIGN.md](PHASE_3_SAFETY_DESIGN.md) | 写入安全设计（写入门 / 校验顺序） |
| [PHASE_3_TEST_PLAN.md](PHASE_3_TEST_PLAN.md) | 测试方案 |
| [PHASE_3A_API_RESEARCH.md](PHASE_3A_API_RESEARCH.md) | 一键恢复的 API 研究 |
| [PHASE_3A_DYNAMIC_RANGE_RESEARCH.md](PHASE_3A_DYNAMIC_RANGE_RESEARCH.md) | 动态属性范围研究 |
| [PHASE_3A_FATIGUE_RESEARCH.md](PHASE_3A_FATIGUE_RESEARCH.md) | Fatigue 方向研究（只读） |
| [PHASE_3A_GROWTH_EXPERIMENT.md](PHASE_3A_GROWTH_EXPERIMENT.md) | 动态成长只读实验设计 |
| [PHASE_3A_GROWTH_EXPERIMENT_REPORT.md](PHASE_3A_GROWTH_EXPERIMENT_REPORT.md) | 成长只读实验 · 首轮报告 |
| [PHASE_3A_MAX_VALUE_BUG_REPORT.md](PHASE_3A_MAX_VALUE_BUG_REPORT.md) | 动态最大值读取错误（严重 Bug）报告 |
| [PHASE_3A_REGRESSION_TEST_REPORT.md](PHASE_3A_REGRESSION_TEST_REPORT.md) | 动态最大值修复 · 回归测试报告 |
| [PHASE_3B_LIMB_SYSTEM_RESEARCH.md](PHASE_3B_LIMB_SYSTEM_RESEARCH.md) | 肢体系统源码研究 |
| [PHASE_3B_LIMB_FEATURE_DESIGN.md](PHASE_3B_LIMB_FEATURE_DESIGN.md) | 肢体功能设计 |
| [PHASE_3B_LIMB_DAMAGE_FLOW.md](PHASE_3B_LIMB_DAMAGE_FLOW.md) | 肢体伤害流程与拦截点分析 |
| [PHASE_3B_LIMB_TEST_PLAN.md](PHASE_3B_LIMB_TEST_PLAN.md) | 肢体系统测试方案 |
| [PHASE_3B_0_ENUMERATION_REPORT.md](PHASE_3B_0_ENUMERATION_REPORT.md) | 肢体只读枚举实施报告 |
| [PHASE_3B_0_GATE_REVIEW.md](PHASE_3B_0_GATE_REVIEW.md) | 3B-0 门禁评审 |
| [PHASE_3B_0_RUNTIME_EVIDENCE.md](PHASE_3B_0_RUNTIME_EVIDENCE.md) | 肢体枚举运行证据 |
| [PHASE_3B_0_1_INDEX_MAPPING_REPORT.md](PHASE_3B_0_1_INDEX_MAPPING_REPORT.md) | 肢体索引 / ID 映射研究 |
| [PHASE_3B_0_1_VALIDATION_REPORT.md](PHASE_3B_0_1_VALIDATION_REPORT.md) | 补充实机验证报告 |
| [PHASE_3B_0_1_REGRESSION_REPORT.md](PHASE_3B_0_1_REGRESSION_REPORT.md) | 稳定性与回归报告 |
| [PHASE_3B_1_UI_DESIGN.md](PHASE_3B_1_UI_DESIGN.md) | 肢体状态显示设计 |
| [PHASE_3B_1_IMPLEMENTATION_REPORT.md](PHASE_3B_1_IMPLEMENTATION_REPORT.md) | 肢体状态显示 · 实施报告 |
| [PHASE_3B_1_TEST_REPORT.md](PHASE_3B_1_TEST_REPORT.md) | 肢体状态显示 · 测试报告 |
| [PHASE_3B_2A_RECOVERY_API_RESEARCH.md](PHASE_3B_2A_RECOVERY_API_RESEARCH.md) | 原生恢复接口研究 |
| [PHASE_3B_2A_RECOVERY_DESIGN.md](PHASE_3B_2A_RECOVERY_DESIGN.md) | 肢体恢复方案设计 |
| [PHASE_3B_2A_RISK_REVIEW.md](PHASE_3B_2A_RISK_REVIEW.md) | 恢复方案风险评审 |
| [PHASE_3B_2A_INDEX_VERIFICATION.md](PHASE_3B_2A_INDEX_VERIFICATION.md) | 索引空间只读验证 |
| [PHASE_3B_2A_1_RISK_UPDATE.md](PHASE_3B_2A_1_RISK_UPDATE.md) | 风险登记更新 |
| [PHASE_3B_2A_1_RUNTIME_EVIDENCE.md](PHASE_3B_2A_1_RUNTIME_EVIDENCE.md) | 受控实验原始证据 |
| [PHASE_3B_2A_1_TEST_REPORT.md](PHASE_3B_2A_1_TEST_REPORT.md) | 恢复接口受控实测报告 |
| [PHASE_3B_2A_2_BUFF_AUDIT.md](PHASE_3B_2A_2_BUFF_AUDIT.md) | Buff 清除审计（R13） |
| [PHASE_3B_2A_2_PERSISTENCE_REPORT.md](PHASE_3B_2A_2_PERSISTENCE_REPORT.md) | 绷带时序（R14）与存档持久性（R15） |
| [PHASE_3B_2A_2_RISK_REVIEW.md](PHASE_3B_2A_2_RISK_REVIEW.md) | 风险登记更新（第 3 版） |
| [PHASE_3B_2A_2_RUNTIME_EVIDENCE.md](PHASE_3B_2A_2_RUNTIME_EVIDENCE.md) | 运行证据 |

## 诊断与逆向（diagnostics/）

| 文件 | 内容 |
|---|---|
| [DURABILITY_DIAGNOSTIC_REPORT.md](diagnostics/DURABILITY_DIAGNOSTIC_REPORT.md) | 耐久字段只读诊断结论 |
| [GUN_DURABILITY_NATIVE_REVERSE_ENGINEERING.md](diagnostics/GUN_DURABILITY_NATIVE_REVERSE_ENGINEERING.md) | 枪械耐久原生逆向分析（**含反汇编片段**，见上方说明） |
| [PHASE4_11_REPORT.md](diagnostics/PHASE4_11_REPORT.md) | Phase 4–11 连续开发交付报告 |
| [ghidra/DumpDurabilityGetter.java](diagnostics/ghidra/DumpDurabilityGetter.java) | Ghidra 脚本：导出 getter 的反汇编 |

## 交付报告（reports/）

| 文件 | 内容 |
|---|---|
| [FINAL_RELEASE_REPORT.md](reports/FINAL_RELEASE_REPORT.md) | v1.0.0 最终交付报告 |
| [PHASE_2_REPORT.md](reports/PHASE_2_REPORT.md) | Phase 2 交付与测试报告 |

## 其它（research/、evidence/）

| 文件 | 内容 |
|---|---|
| [research/LEGACY_PROJECT_PATTERNS.md](research/LEGACY_PROJECT_PATTERNS.md) | 从旧项目（FPS 优化模组）提取的可复用工程模式 |
| [evidence/phase3a_fatigue_stats.txt](evidence/phase3a_fatigue_stats.txt) | Phase 3A 疲劳采样原始数据 |
| [evidence/phase3a_first_run_evidence.txt](evidence/phase3a_first_run_evidence.txt) | Phase 3A 首轮运行证据 |
| [evidence/phase3a_maxvalue_fix_evidence.txt](evidence/phase3a_maxvalue_fix_evidence.txt) | 最大值修复的证据 |
| [evidence/phase3a_probe_first_run.txt](evidence/phase3a_probe_first_run.txt) | 成长探针首轮输出 |
| [evidence/phase3b01_evidence.txt](evidence/phase3b01_evidence.txt) | 3B-0.1 索引映射证据 |
| [evidence/phase3b2a_api_probe_evidence.txt](evidence/phase3b2a_api_probe_evidence.txt) | 3B-2A API 探针证据 |
| [evidence/phase3b2a1_experiment_evidence.txt](evidence/phase3b2a1_experiment_evidence.txt) | 3B-2A.1 受控实验证据 |
| [evidence/phase3b2a2_evidence.txt](evidence/phase3b2a2_evidence.txt) | 3B-2A.2 证据 |
| [evidence/zz_original_display_settings.txt](evidence/zz_original_display_settings.txt) | 修改前的原始显示设置（回滚参考） |

## 维护约定

- 新档案放对应子目录，并在本文件加一行；
- 档案内容不要回链到会变的文档（链接检查会挡 404）；
- 档案里的本机路径一律写占位符（`<游戏目录>`、`<dotnet>` 等）。
