using System;
using System.Collections.Generic;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 最大值"选源"算法 —— 纯 C#，不依赖 UnityEngine / 游戏类型，因此可离线自动化测试。
    ///
    /// 规则（对应本次严重 Bug 的修复）：
    ///  1. 按可信度顺序遍历候选，取第一个"有限且 > 0"的作为首选；
    ///  2. 若首选 **小于当前值**，说明该来源不可信（例如 BasicCharacterController.maxHP=1000
    ///     而真实最大生命是 2000）→ 回退到第一个"≥ 当前值"的候选，并标记 FallbackUsed；
    ///  3. 若没有任何候选 ≥ 当前值 → Unreliable，调用方必须**拒绝写入**；
    ///  4. 全无有效候选（<=0 / NaN / Inf）→ Unreliable。
    ///
    /// 这条算法保证：**即使选错来源，也只会拒绝，绝不会把数值改小。**
    /// </summary>
    internal static class RangeSelection
    {
        internal const float Eps = 0.05f;

        internal enum Result
        {
            Ok = 0,          // 首选可用
            FallbackUsed,    // 首选不可信，已回退到其它候选
            Unreliable,      // 无法确定合法上限 → 必须拒绝写入
        }

        internal struct Candidate
        {
            internal string Name;
            internal float Value;

            internal Candidate(string name, float value) { Name = name; Value = value; }
        }

        internal static bool IsUsable(float v)
            => !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f;

        /// <summary>
        /// 选择最大值来源。
        /// </summary>
        /// <param name="cands">候选（按可信度从高到低）</param>
        /// <param name="current">当前值（用于一致性校验）</param>
        /// <param name="chosen">选中的候选</param>
        /// <param name="reason">说明（失败或回退原因）</param>
        internal static Result Select(IList<Candidate> cands, float current, out Candidate chosen, out string reason)
        {
            chosen = default;
            reason = null;

            if (cands == null || cands.Count == 0)
            {
                reason = "没有任何候选最大值";
                return Result.Unreliable;
            }

            // 1) 首选：第一个可用
            int primaryIdx = -1;
            for (int i = 0; i < cands.Count; i++)
            {
                if (IsUsable(cands[i].Value)) { primaryIdx = i; break; }
            }

            if (primaryIdx < 0)
            {
                reason = "所有候选最大值均无效（<=0 或非有限值）";
                return Result.Unreliable;
            }

            var primary = cands[primaryIdx];

            // 2) 首选 >= 当前值 → 直接采用
            if (primary.Value >= current - Eps)
            {
                chosen = primary;
                return Result.Ok;
            }

            // 3) 首选 < 当前值 → 首选不可信，找 ≥ 当前值的其它候选
            for (int i = 0; i < cands.Count; i++)
            {
                if (i == primaryIdx) continue;
                if (!IsUsable(cands[i].Value)) continue;
                if (cands[i].Value >= current - Eps)
                {
                    chosen = cands[i];
                    reason = $"首选 {primary.Name}={primary.Value:0.##} 小于当前值 {current:0.##}，" +
                             $"回退到 {chosen.Name}={chosen.Value:0.##}";
                    return Result.FallbackUsed;
                }
            }

            // 4) 无候选能覆盖当前值 → 不可靠
            reason = $"首选 {primary.Name}={primary.Value:0.##} 小于当前值 {current:0.##}，" +
                     "且没有其它候选能覆盖当前值 → 拒绝写入";
            return Result.Unreliable;
        }
    }
}
