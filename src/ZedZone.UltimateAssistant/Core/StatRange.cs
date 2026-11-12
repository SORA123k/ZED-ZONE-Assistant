using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 一项属性的动态范围快照（每次恢复前重新读取，绝不缓存）。
    /// </summary>
    internal sealed class StatRange
    {
        internal StatId Stat;
        internal float Current;
        internal float Min;          // 游戏内资源下限恒为 0（【推断】所有 Cost*/Has* 按 >=0 语义）
        internal float Max;
        internal string MaxSource = "";
        internal readonly List<string> Candidates = new List<string>();   // "name=value"
        internal bool Reliable;
        internal string Error;
        internal string Warning;

        internal float Span => Max - Min;

        internal string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("stat=").Append(Stat)
              .Append(" current=").Append(Current.ToString("0.##"))
              .Append(" min=").Append(Min.ToString("0.##"))
              .Append(" max=").Append(Max.ToString("0.##"))
              .Append(" source=").Append(MaxSource)
              .Append(" candidates=[");
            for (int i = 0; i < Candidates.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Candidates[i]);
            }
            sb.Append(']')
              .Append(" reliable=").Append(Reliable);
            if (!string.IsNullOrEmpty(Warning)) sb.Append(" warning=").Append(Warning);
            if (!string.IsNullOrEmpty(Error)) sb.Append(" error=").Append(Error);
            return sb.ToString();
        }
    }

    /// <summary>
    /// 动态属性范围读取器。
    ///
    /// 背景（本次严重 Bug 的根因）：
    ///   `BasicCharacterController.maxHP` 是**字段**，实测为 1000，而玩家真实最大生命值是 2000
    ///   （受 Constitution / 技能 / 装备影响）。游戏原生面板显示 2000。
    ///   直接用它做恢复目标会把 2000 血截断到 1000 —— 反向伤害玩家。
    ///
    /// 因此这里不再信任单一来源，而是：
    ///   1. 采集**所有**可读候选（HP 有 3 个：statusData.maxhp / FullHP / maxHP）；
    ///   2. 优先用 `CharacterStatusData.maxhp`（由 `CharacterData.SetMaxHP()` 计算维护）；
    ///   3. 若首选 **小于当前值**（说明它不可信），回退到"≥当前值"的其它候选，并记 WARN；
    ///   4. 若没有任何候选 ≥ 当前值 → 判定不可靠，**拒绝写入**；
    ///   5. 每次恢复前重新读取，不缓存。
    ///
    /// 这样即使选错来源，也只会"拒绝"，不会把数值改小。
    /// </summary>
    internal static class StatRangeReader
    {
        private const float Eps = 0.05f;

        internal static StatRange Read(StatId stat, HumanCharacterController player)
        {
            var r = new StatRange { Stat = stat, Min = 0f };

            if (player == null) { r.Error = "player == null"; return r; }

            CharacterStatusData sd = null;
            try { sd = PlayerDataAccess.GetStatusData(player, out string e); if (sd == null) r.Error = e; }
            catch (Exception ex) { r.Error = ex.GetType().Name; }

            if (sd == null)
            {
                r.Error = string.IsNullOrEmpty(r.Error) ? "characterStatusData 不可用" : r.Error;
                return r;
            }

            // ---------- 当前值 ----------
            try
            {
                switch (stat)
                {
                    case StatId.Hp: r.Current = player.hp; break;
                    case StatId.Energy: r.Current = player.energy; break;
                    case StatId.Fatigue: r.Current = player.fatigue; break;
                    case StatId.Hunger: r.Current = player.hunger; break;
                    case StatId.Thirst: r.Current = player.thirst; break;
                }
            }
            catch (Exception ex)
            {
                r.Error = "读取当前值失败: " + ex.GetType().Name;
                return r;
            }

            // ---------- 候选（按可信度排序） ----------
            var cands = new List<KeyValuePair<string, float>>();

            switch (stat)
            {
                case StatId.Hp:
                    AddCand(cands, "CharacterStatusData.maxhp", () => sd.maxhp, r);
                    AddCand(cands, "BasicCharacterController.FullHP", () => player.FullHP, r);
                    AddCand(cands, "BasicCharacterController.maxHP", () => player.maxHP, r);
                    break;
                case StatId.Energy:
                    AddCand(cands, "CharacterStatusData.maxEnergy", () => sd.maxEnergy, r);
                    break;
                case StatId.Fatigue:
                    AddCand(cands, "CharacterStatusData.maxFatigue", () => sd.maxFatigue, r);
                    break;
                case StatId.Hunger:
                    AddCand(cands, "CharacterStatusData.maxHunger", () => sd.maxHunger, r);
                    break;
                case StatId.Thirst:
                    AddCand(cands, "CharacterStatusData.maxThirst", () => sd.maxThirst, r);
                    break;
            }

            foreach (var kv in cands)
                r.Candidates.Add(kv.Key + "=" + kv.Value.ToString("0.##"));

            // ---------- 选择（纯函数，可离线测试） ----------
            var list = new List<RangeSelection.Candidate>(cands.Count);
            foreach (var kv in cands) list.Add(new RangeSelection.Candidate(kv.Key, kv.Value));

            var sel = RangeSelection.Select(list, r.Current, out var chosen, out string reason);

            if (sel == RangeSelection.Result.Unreliable)
            {
                r.Error = reason;
                return r;
            }

            if (sel == RangeSelection.Result.FallbackUsed)
            {
                r.Warning = reason;
                Log.WarnOnce("range.fallback." + stat, "[range] " + reason);
            }

            r.Max = chosen.Value;
            r.MaxSource = chosen.Name;
            r.Reliable = true;
            return r;
        }

        private static void AddCand(List<KeyValuePair<string, float>> list, string name,
            Func<float> get, StatRange r)
        {
            try { list.Add(new KeyValuePair<string, float>(name, get())); }
            catch (Exception e) { r.Candidates.Add(name + "=<" + e.GetType().Name + ">"); }
        }

        /// <summary>恢复目标：正常填满；fatigue=exhaustion 时清零。</summary>
        internal static float RecoveryTarget(StatId stat, StatRange range)
        {
            bool lowering = stat == StatId.Fatigue
                            && (ModConfig.FatigueDirection?.Value ?? "unconfirmed") == "exhaustion";
            float t = lowering ? range.Min : range.Max;
            return Mathf.Clamp(t, range.Min, range.Max);
        }
    }
}
