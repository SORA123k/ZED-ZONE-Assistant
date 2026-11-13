using System;
using System.Text;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 只读「动态成长」实验探针。
    ///
    /// 用途：观察角色属性成长（等级/体质/装备/技能）如何影响最大生命值，
    /// 以验证 `CharacterStatusData.maxhp` 是否始终是真实有效上限。
    ///
    /// 严格只读：
    ///   * 只读取字段，不写任何属性；
    ///   * 不写 GameConfigs、不碰存档、不装补丁。
    ///
    /// 输出：
    ///   * 数值发生变化时立刻输出一条带 "CHANGE" 的对比行；
    ///   * 无变化时按配置间隔输出一条心跳行（便于确认探针在工作）。
    ///
    /// 关于「角色等级」：经核查，本游戏**不存在单一的角色等级字段**
    /// （只有逐属性成长 `attrGained_constitution` / `attrExp_constitution`、`coopLevel`、
    ///  以及按技能 ID 查询的 `GetSkillLevel(int)`）。
    /// 因此日志里 level 一栏写 N/A，并同时输出体质成长指标 —— 不伪造等级。
    /// </summary>
    internal static class GrowthProbe
    {
        private static string _lastSig;
        private static float _nextHeartbeatAt;
        private static int _samples;
        private static int _changes;

        internal static bool Enabled => ModConfig.GrowthLog?.Value ?? false;

        internal static void Tick()
        {
            if (!Enabled) return;

            float now = Time.unscaledTime;
            int interval = Mathf.Clamp(Mathf.Max(20, ModConfig.GrowthLogIntervalSec?.Value ?? 10), 5, 600);
            if (now < _nextHeartbeatAt) return;
            _nextHeartbeatAt = now + interval;

            string line = BuildLine(out string sig);
            if (line == null) return;

            _samples++;
            if (sig != _lastSig)
            {
                bool first = _lastSig == null;
                _lastSig = sig;
                if (!first) _changes++;
                Log.Info($"[growth] {(_changes > 0 || !first ? "CHANGE " : "INIT ")}{line}");
            }
            else
            {
                Log.Info($"[growth] heartbeat {line}");
            }
        }

        private static string BuildLine(out string sig)
        {
            sig = null;
            var sb = new StringBuilder();

            HumanCharacterController p = null;
            try { p = PlayerAccessor.Resolve(out _, out _); }
            catch (Exception e) { Log.ErrorOnce("growth.resolve", e); }

            string scene = PlayerAccessor.CurrentSceneName();
            sb.Append("t=").Append(DateTime.Now.ToString("HH:mm:ss"))
              .Append(" scene=").Append(scene);

            if (p == null)
            {
                sb.Append(" player=null");
                sig = sb.ToString();
                return sb.ToString();
            }

            CharacterData cd = null;
            CharacterStatusData sd = null;
            try { cd = p.characterData; } catch { }
            try { sd = PlayerDataAccess.GetStatusData(p, out _); } catch { }

            // ---- 等级相关（本游戏无单一 level 字段） ----
            sb.Append(" level=N/A");
            if (cd != null)
            {
                sb.Append(" coopLevel=").Append(Safe(() => cd.coopLevel));
                sb.Append(" conGained=").Append(Safe(() => cd.attrGained_constitution));
                sb.Append(" conExp=").Append(SafeF(() => cd.attrExp_constitution));
            }

            // ---- 生命与最大生命（三个来源全记） ----
            sb.Append(" hp=").Append(SafeF(() => p.hp));
            if (sd != null) sb.Append(" maxhp=").Append(SafeF(() => sd.maxhp));
            sb.Append(" maxHPfield=").Append(SafeF(() => p.maxHP));
            sb.Append(" FullHP=").Append(SafeF(() => p.FullHP));

            // ---- 最大 HP 的计算输入 ----
            if (cd != null)
            {
                sb.Append(" appliedMaxHPConstitution=").Append(Safe(() => cd.appliedMaxHPConstitution));
                sb.Append(" skillProperty_MaxHPFloat=").Append(SafeF(() => cd.skillProperty_MaxHPFloat));
                sb.Append(" appliedMaxHPSkillFloat=").Append(SafeF(() => cd.appliedMaxHPSkillFloat));
                sb.Append(" maxHPBalanceFloat=").Append(SafeF(() => cd.maxHPBalanceFloat));
                sb.Append(" appliedMaxHPBalanceFloat=").Append(SafeF(() => cd.appliedMaxHPBalanceFloat));
                sb.Append(" skillProperty_StaminaFloat=").Append(SafeF(() => cd.skillProperty_StaminaFloat));
            }

            // ---- 其它上限（用于同一实验对照） ----
            if (sd != null)
            {
                sb.Append(" maxEnergy=").Append(SafeF(() => sd.maxEnergy));
                sb.Append(" maxHunger=").Append(SafeF(() => sd.maxHunger));
                sb.Append(" maxThirst=").Append(SafeF(() => sd.maxThirst));
                sb.Append(" maxFatigue=").Append(SafeF(() => sd.maxFatigue));
            }

            string s = sb.ToString();
            // 签名不含时间戳，否则永远判为变化
            sig = s.Substring(s.IndexOf(" scene=", StringComparison.Ordinal));
            return s;
        }

        private static string Safe(Func<int> f)
        {
            try { return f().ToString(); } catch { return "?"; }
        }

        private static string SafeF(Func<float> f)
        {
            try { return f().ToString("0.##"); } catch { return "?"; }
        }

        internal static string Status()
            => $"GrowthLog={(Enabled ? "on" : "off")} samples={_samples} changes={_changes}";
    }
}

