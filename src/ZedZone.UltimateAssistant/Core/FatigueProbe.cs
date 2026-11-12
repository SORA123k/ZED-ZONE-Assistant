using System;
using System.Text;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 只读「疲劳方向」行为观测探针。
    ///
    /// 为什么需要它：
    ///   `fatigue` 的方向（越高越好 / 越高越糟）在静态层面**无法判定**：
    ///     - `HasFatigue(float)` / `CostFatigue(float)` 像"可消耗资源"（越高越好）；
    ///     - `fatigueWarningTimeFlag` / `CharacterTired()` 又像"疲劳累积"（越高越糟）；
    ///     - `BuffAttr` 里没有任何 fatigue 字段，说明它不由 Buff 驱动。
    ///   名称不能当证据，所以这里把 fatigue 与"可观测行为"同时采样，
    ///   由**实际数据**判断方向。
    ///
    /// 严格只读：只读字段与输入状态，不写任何属性、不装补丁、不碰存档。
    ///
    /// 采样内容：fatigue、energy（对照）、冲刺请求、移动输入、速度、移动强度、场景。
    /// 到达 `FatigueProbeSeconds` 后自动停止，并输出一段统计摘要。
    /// </summary>
    internal static class FatigueProbe
    {
        private static float _nextAt;
        private static float _startAt = -1f;
        private static int _samples;
        private static float _prevFatigue = float.NaN;
        private static float _prevEnergy = float.NaN;
        private static bool _finished;

        // 统计
        private static float _minFatigue = float.MaxValue, _maxFatigue = float.MinValue;
        private static float _minEnergy = float.MaxValue, _maxEnergy = float.MinValue;
        private static int _sprintSamples, _movingSamples;
        private static float _fatigueWhileSprintSum, _energyWhileSprintSum;
        private static float _fatigueWhileIdleSum, _energyWhileIdleSum;
        private static int _idleSamples;

        internal static bool Enabled => ModConfig.FatigueProbe?.Value ?? false;

        internal static void Tick()
        {
            if (!Enabled || _finished) return;

            float now = Time.unscaledTime;
            if (_startAt < 0f) _startAt = now;

            int maxSec = Mathf.Clamp(ModConfig.FatigueProbeSeconds?.Value ?? 600, 30, 7200);
            if (now - _startAt > maxSec)
            {
                _finished = true;
                LogSummary("达到采样时长");
                return;
            }

            int intervalMs = Mathf.Clamp(ModConfig.FatigueProbeIntervalMs?.Value ?? 500, 100, 5000);
            if (now * 1000f < _nextAt) return;
            _nextAt = now * 1000f + intervalMs;

            HumanCharacterController p = null;
            try { p = PlayerAccessor.Resolve(out _, out _); } catch { }
            if (p == null) return;

            CharacterStatusData sd = null;
            try { sd = PlayerDataAccess.GetStatusData(p, out _); } catch { }

            float fatigue, energy;
            try { fatigue = p.fatigue; } catch { return; }
            try { energy = p.energy; } catch { energy = float.NaN; }

            float maxFatigue = float.NaN, maxEnergy = float.NaN;
            if (sd != null)
            {
                try { maxFatigue = sd.maxFatigue; } catch { }
                try { maxEnergy = sd.maxEnergy; } catch { }
            }

            // ---- 行为侧（全部 try/catch，失败显示 ?） ----
            bool sprintReq = false, sprintFlag = false;
            Vector2 mov = default;
            float vel = float.NaN, intensity = float.NaN;

            try
            {
                var ci = CharacterInput.instance;
                if (ci != null)
                {
                    try { sprintReq = ci.IsSprintRequested(); } catch { }
                    try { sprintFlag = ci.controllerSprintFlag; } catch { }
                    try { mov = ci.movementInput; } catch { }
                }
            }
            catch { }

            try { vel = p.realVelocity.magnitude; } catch { }
            try { intensity = p.movementIntensity; } catch { }

            bool moving = Mathf.Abs(mov.x) + Mathf.Abs(mov.y) > 0.05f || (!float.IsNaN(vel) && vel > 0.3f);

            // ---- 统计 ----
            _samples++;
            _minFatigue = Mathf.Min(_minFatigue, fatigue);
            _maxFatigue = Mathf.Max(_maxFatigue, fatigue);
            if (!float.IsNaN(energy))
            {
                _minEnergy = Mathf.Min(_minEnergy, energy);
                _maxEnergy = Mathf.Max(_maxEnergy, energy);
            }
            if (sprintReq) { _sprintSamples++; _fatigueWhileSprintSum += fatigue; _energyWhileSprintSum += energy; }
            if (moving) _movingSamples++;
            if (!moving) { _idleSamples++; _fatigueWhileIdleSum += fatigue; _energyWhileIdleSum += energy; }

            // ---- 输出 ----
            float dF = float.IsNaN(_prevFatigue) ? 0f : fatigue - _prevFatigue;
            float dE = (float.IsNaN(_prevEnergy) || float.IsNaN(energy)) ? 0f : energy - _prevEnergy;

            var sb = new StringBuilder();
            sb.Append("[fatigue] t=").Append(DateTime.Now.ToString("HH:mm:ss"));
            sb.Append(" scene=").Append(PlayerAccessor.CurrentSceneName());
            sb.Append(" fatigue=").Append(fatigue.ToString("0.##"));
            if (!float.IsNaN(maxFatigue)) sb.Append('/').Append(maxFatigue.ToString("0.##"));
            sb.Append(" dF=").Append(dF.ToString("+0.00;-0.00;0.00"));
            sb.Append(" energy=").Append(energy.ToString("0.##"));
            if (!float.IsNaN(maxEnergy)) sb.Append('/').Append(maxEnergy.ToString("0.##"));
            sb.Append(" dE=").Append(dE.ToString("+0.00;-0.00;0.00"));
            sb.Append(" sprint=").Append(sprintReq ? 1 : 0);
            sb.Append(" sprintFlag=").Append(sprintFlag ? 1 : 0);
            sb.Append(" mov=(").Append(mov.x.ToString("0.00")).Append(',').Append(mov.y.ToString("0.00")).Append(')');
            sb.Append(" vel=").Append(float.IsNaN(vel) ? "?" : vel.ToString("0.00"));
            sb.Append(" intensity=").Append(float.IsNaN(intensity) ? "?" : intensity.ToString("0.00"));
            sb.Append(" moving=").Append(moving ? 1 : 0);

            Log.Info(sb.ToString());

            _prevFatigue = fatigue;
            _prevEnergy = energy;
        }

        internal static void LogSummary(string reason)
        {
            if (_samples == 0)
            {
                Log.Info("[fatigue] 摘要：无样本");
                return;
            }

            float sprintAvgF = _sprintSamples > 0 ? _fatigueWhileSprintSum / _sprintSamples : float.NaN;
            float sprintAvgE = _sprintSamples > 0 ? _energyWhileSprintSum / _sprintSamples : float.NaN;
            float idleAvgF = _idleSamples > 0 ? _fatigueWhileIdleSum / _idleSamples : float.NaN;
            float idleAvgE = _idleSamples > 0 ? _energyWhileIdleSum / _idleSamples : float.NaN;

            Log.Info("[fatigue] ===== 摘要（" + reason + "） =====");
            Log.Info($"[fatigue] 样本={_samples} 冲刺样本={_sprintSamples} 静止样本={_idleSamples} 移动样本={_movingSamples}");
            Log.Info($"[fatigue] fatigue 区间=[{_minFatigue:0.##}, {_maxFatigue:0.##}]");
            if (!float.IsNaN(_minEnergy))
                Log.Info($"[fatigue] energy 区间=[{_minEnergy:0.##}, {_maxEnergy:0.##}]");
            Log.Info($"[fatigue] 冲刺时均值 fatigue={sprintAvgF:0.##} energy={sprintAvgE:0.##}");
            Log.Info($"[fatigue] 静止时均值 fatigue={idleAvgF:0.##} energy={idleAvgE:0.##}");
            Log.Info("[fatigue] 判读方法：对比\"冲刺时均值\"与\"静止时均值\"——" +
                     "fatigue 冲刺更低/静止回升 → 它是可消耗资源（越高越好）；" +
                     "fatigue 冲刺更高/静止回落 → 它是疲劳累积（越高越糟）。");
            Log.Info("[fatigue] ====================");
        }

        internal static string Status()
            => $"FatigueProbe={(Enabled ? (_finished ? "finished" : "on") : "off")} samples={_samples}";

        /// <summary>供 GUI 手动触发摘要（只读）。</summary>
        internal static void ForceSummary() => LogSummary("手动触发");
    }
}
