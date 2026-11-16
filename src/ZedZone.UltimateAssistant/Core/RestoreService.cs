using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// Phase 3A 一键恢复服务。
    ///
    /// 本身**不直接写数据**：全部委托给 <see cref="PlayerWriteGate"/>，
    /// 保证检查顺序与日志只有一处实现。
    /// 所有调用都发生在 OnGUI（Unity 主线程），因此满足 IL2CPP 线程约束。
    /// </summary>
    internal static class RestoreService
    {
        internal static readonly StatId[] AllOrder =
        {
            StatId.Hp, StatId.Energy, StatId.Hunger, StatId.Thirst, StatId.Fatigue
        };

        private static WriteResult _last;
        private static float _lastAt = -999f;
        private static string _summary;
        private static float _summaryAt = -999f;

        /// <summary>最近一次单次操作结果（用于 UI 反馈）。</summary>
        internal static WriteResult Last => _last;
        internal static float LastAge => Time.unscaledTime - _lastAt;

        internal static string Summary => _summary;
        internal static float SummaryAge => Time.unscaledTime - _summaryAt;

        internal static WriteResult Restore(StatId stat)
        {
            WriteResult r;
            try
            {
                r = PlayerWriteGate.Restore(stat);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("restore." + stat, e);
                r = new WriteResult
                {
                    Stat = stat,
                    Outcome = WriteOutcome.Failed,
                    Message = e.GetType().Name + ": " + e.Message
                };
            }
            _last = r;
            _lastAt = Time.unscaledTime;
            return r;
        }

        /// <summary>一键恢复全部（逐项走同一门禁，单项失败不影响其它项）。</summary>
        internal static void RestoreAll()
        {
            var ok = 0;
            var rejected = 0;
            var failed = 0;
            var reasons = new StringBuilder();

            for (int i = 0; i < AllOrder.Length; i++)
            {
                var stat = AllOrder[i];
                var r = Restore(stat);
                switch (r.Outcome)
                {
                    case WriteOutcome.Ok:
                    case WriteOutcome.SkippedNoChange:
                        ok++;
                        break;
                    case WriteOutcome.Failed:
                        failed++;
                        reasons.Append(stat).Append("(异常) ");
                        break;
                    default:
                        rejected++;
                        // 只把"值得说明"的拒绝原因写进摘要
                        if (r.Outcome != WriteOutcome.RejectedDisabled &&
                            r.Outcome != WriteOutcome.RejectedNotValidated)
                            reasons.Append(stat).Append('(').Append(r.OutcomeZh()).Append(") ");
                        break;
                }
            }

            _summary = $"全部恢复：成功/已满 {ok} 项，拒绝 {rejected} 项，异常 {failed} 项"
                       + (reasons.Length > 0 ? "；" + reasons : "");
            _summaryAt = Time.unscaledTime;
            Log.Info("[restore.all] " + _summary);
        }

        /// <summary>供 UI 判断某资源是否"当前可点"（不做实际写入）。</summary>
        internal static bool IsLikelyAvailable(StatId stat, out string reason)
        {
            reason = null;

            if (!(ModConfig.EnableRestore?.Value ?? false)) { reason = "总开关已关闭"; return false; }

            switch (stat)
            {
                case StatId.Hp: if (!(ModConfig.RestoreHealth?.Value ?? true)) { reason = "已关闭"; return false; } break;
                case StatId.Energy: if (!(ModConfig.RestoreEnergy?.Value ?? true)) { reason = "已关闭"; return false; } break;
                case StatId.Hunger: if (!(ModConfig.RestoreHunger?.Value ?? true)) { reason = "已关闭"; return false; } break;
                case StatId.Thirst: if (!(ModConfig.RestoreThirst?.Value ?? true)) { reason = "已关闭"; return false; } break;
                case StatId.Fatigue:
                    if (!(ModConfig.RestoreFatigue?.Value ?? false)) { reason = "默认关闭（方向未确认）"; return false; }
                    if (!PlayerWriteGate.FatigueValidated(ModConfig.FatigueDirection?.Value)) { reason = "方向未确认（G2）"; return false; }
                    break;
            }
            return true;
        }
    }
}

