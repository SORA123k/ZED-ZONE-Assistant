using System;
using UnityEngine;
using ZedZone.Net;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>可恢复的资源。</summary>
    internal enum StatId
    {
        Hp = 0,
        Energy = 1,
        Fatigue = 2,
        Hunger = 3,
        Thirst = 4,
    }

    /// <summary>写入结果（穷举，每种都必须有日志与 UI 反馈）。</summary>
    internal enum WriteOutcome
    {
        Ok = 0,
        SkippedNoChange,
        RejectedDisabled,
        RejectedMultiplayer,
        RejectedNoPlayer,
        RejectedDead,
        RejectedRespawnWindow,
        RejectedNotValidated,
        RejectedOutOfRange,
        Failed,
    }

    internal sealed class WriteResult
    {
        internal StatId Stat;
        internal WriteOutcome Outcome;
        internal float Before;
        internal float After;
        internal float Target;
        internal string Message;
        internal DateTime At = DateTime.Now;

        internal bool IsOk => Outcome == WriteOutcome.Ok;

        internal string OutcomeZh()
        {
            switch (Outcome)
            {
                case WriteOutcome.Ok: return "已恢复";
                case WriteOutcome.SkippedNoChange: return "已是满值";
                case WriteOutcome.RejectedDisabled: return "功能已关闭";
                case WriteOutcome.RejectedMultiplayer: return "联机模式已拒绝执行";
                case WriteOutcome.RejectedNoPlayer: return "未找到玩家对象";
                case WriteOutcome.RejectedDead: return "玩家已死亡";
                case WriteOutcome.RejectedRespawnWindow: return "重生保护窗口内";
                case WriteOutcome.RejectedNotValidated: return "该功能尚未通过验证";
                case WriteOutcome.RejectedOutOfRange: return "数值越界/非法";
                case WriteOutcome.Failed: return "执行异常";
                default: return "未知";
            }
        }
    }

    /// <summary>
    /// 统一写入门 —— Phase 3 唯一允许修改玩家状态的入口。
    ///
    /// 固定检查顺序（不得调换，见 docs/PHASE_3_SAFETY_DESIGN.md §2.2）：
    ///   1 配置开关 → 2 联机(fail-closed) → 3 验证状态 → 4 重取玩家 →
    ///   5 死亡标志 → 6 重生窗口 → 7 钳制校验 → 8 写入 → 9 回读校验
    ///
    /// 铁律：
    ///  * 只用**属性 setter**（`player.hp = v`），绝不写 `characterStatusData` 裸字段；
    ///  * 不写 GameConfigs、不写 skillProperty、不碰存档；
    ///  * 幂等：写"目标值"，不做 `x += n`；
    ///  * 关闭功能只停止写，绝不回写数值。
    /// </summary>
    internal static class PlayerWriteGate
    {
        /// <summary>
        /// 联机判定（fail-closed：任何异常都当作联机）。
        /// 依据：NetworkManager.isOnline/isHost/isClient 为 static bool，已在游戏 1.0.16 运行期验证可调用。
        /// </summary>
        internal static bool IsMultiplayer(out string why)
        {
            why = null;
            try
            {
                if (NetworkManager.isOnline) { why = "isOnline=true"; return true; }
                if (NetworkManager.isHost) { why = "isHost=true"; return true; }
                if (NetworkManager.isClient) { why = "isClient=true"; return true; }

                var nm = NetworkManager.instance;
                if (nm != null)
                {
                    // mode 是 NetMode 枚举；运行期实测 Offline 时 ToString()=="Offline"。
                    // 这里用 ToString 比较以避免依赖未在 interop 中列出的 NetMode 类型。
                    string mode = nm.mode.ToString();
                    if (mode != "Offline")
                    {
                        why = "mode=" + mode;
                        return true;
                    }
                }
                return false;
            }
            catch (Exception e)
            {
                Log.WarnOnce("gate.net", "联机状态检测失败，按联机处理（fail-closed）: " + e.GetType().Name);
                why = "检测异常:" + e.GetType().Name;
                return true;
            }
        }

        /// <summary>Fatigue 方向是否已确认（未确认则拒绝写入）。</summary>
        internal static bool FatigueValidated(string dir)
            => !string.IsNullOrEmpty(dir) && dir != "unconfirmed";

        /// <summary>
        /// 一键恢复到满值。
        /// </summary>
        internal static WriteResult Restore(StatId stat)
        {
            var r = new WriteResult { Stat = stat };

            // ---------- 1. 总开关 ----------
            if (!(ModConfig.EnableRestore?.Value ?? false))
            {
                return Reject(r, WriteOutcome.RejectedDisabled, "一键恢复总开关关闭");
            }

            // ---------- 2. 联机门禁（fail-closed） ----------
            if (IsMultiplayer(out string why))
            {
                return Reject(r, WriteOutcome.RejectedMultiplayer, why);
            }

            // ---------- 3. 该资源的功能开关 + 验证状态 ----------
            switch (stat)
            {
                case StatId.Hp:
                    if (!(ModConfig.RestoreHealth?.Value ?? true)) return Reject(r, WriteOutcome.RejectedDisabled, "Health 恢复已关闭");
                    break;
                case StatId.Energy:
                    if (!(ModConfig.RestoreEnergy?.Value ?? true)) return Reject(r, WriteOutcome.RejectedDisabled, "Energy 恢复已关闭");
                    break;
                case StatId.Hunger:
                    if (!(ModConfig.RestoreHunger?.Value ?? true)) return Reject(r, WriteOutcome.RejectedDisabled, "Hunger 恢复已关闭");
                    break;
                case StatId.Thirst:
                    if (!(ModConfig.RestoreThirst?.Value ?? true)) return Reject(r, WriteOutcome.RejectedDisabled, "Thirst 恢复已关闭");
                    break;
                case StatId.Fatigue:
                {
                    if (!(ModConfig.RestoreFatigue?.Value ?? false)) return Reject(r, WriteOutcome.RejectedDisabled, "Fatigue 恢复已关闭（默认关闭）");
                    string dir = ModConfig.FatigueDirection?.Value ?? "unconfirmed";
                    if (!FatigueValidated(dir))
                        return Reject(r, WriteOutcome.RejectedNotValidated,
                            "Fatigue 方向未确认（G2）：请先做冲刺/挥砍观察，再把 FatigueDirection 设为 resource 或 exhaustion");
                    break;
                }
            }

            // ---------- 4. 每次重新解析玩家（禁止缓存） ----------
            HumanCharacterController player = null;
            try { player = PlayerAccessor.Resolve(out _, out string perr); }
            catch (Exception e) { Log.ErrorOnce("gate.resolve", e); }

            if (player == null)
                return Reject(r, WriteOutcome.RejectedNoPlayer, "playerCharacter == null");

            // ---------- 5. 死亡标志 ----------
            try
            {
                if (player.deadFlag) return Reject(r, WriteOutcome.RejectedDead, "deadFlag=true");
            }
            catch (Exception e) { Log.WarnOnce("gate.dead", "deadFlag 读取失败: " + e.GetType().Name); }

            try
            {
                if (player.netDeadFlag) return Reject(r, WriteOutcome.RejectedDead, "netDeadFlag=true");
            }
            catch (Exception e) { Log.WarnOnce("gate.netdead", "netDeadFlag 读取失败: " + e.GetType().Name); }

            // ---------- 6. 重生无敌窗口 ----------
            try
            {
                float until = player.netRespawnInvincibleUntil;
                if (until > 0f && Time.time < until)
                    return Reject(r, WriteOutcome.RejectedRespawnWindow,
                        $"重生保护中（剩余 {until - Time.time:0.0}s）");
            }
            catch (Exception e) { Log.WarnOnce("gate.respawn", "netRespawnInvincibleUntil 读取失败: " + e.GetType().Name); }

            // ---------- 7. 计算目标值 + 钳制 ----------
            float current, max;
            if (!TryRead(stat, player, out current, out max, out string readErr))
                return Reject(r, WriteOutcome.Failed, "读取当前值失败: " + readErr);

            // 上限必须 > 0，否则无法确定合法范围
            if (float.IsNaN(max) || float.IsInfinity(max) || max <= 0f)
                return Reject(r, WriteOutcome.RejectedOutOfRange, $"上限异常 max={max}");

            string fatigueDir = ModConfig.FatigueDirection?.Value ?? "unconfirmed";
            bool isLowering = (stat == StatId.Fatigue && fatigueDir == "exhaustion");
            float target = isLowering ? 0f : max;

            if (float.IsNaN(target) || float.IsInfinity(target))
                return Reject(r, WriteOutcome.RejectedOutOfRange, "target 非有限值");

            target = Mathf.Clamp(target, 0f, max);

            r.Before = current;
            r.Target = target;

            // ★ "恢复"必须是只升不降。
            //   实测：玩家有效生命上限会超过 maxHP（例如 hp=1840 而 maxHP=1000，来自装备/Buff 加成），
            //   若无脑写 maxHP 会把 1840 血降到 1000 —— 反向伤害玩家。
            //   因此：当前值已达到或超过目标时一律不写。
            if (!isLowering)
            {
                if (current >= target - 0.05f)
                {
                    r.After = current;
                    string extra = current > target + 0.05f
                        ? $"（当前 {current:0.##} 高于上限 {max:0.##}，可能来自装备/Buff，保持不动）"
                        : "";
                    return Reject(r, WriteOutcome.SkippedNoChange,
                        $"当前 {current:0.##} 已不低于目标 {target:0.##}，恢复只升不降{extra}");
                }
            }
            else
            {
                if (current <= target + 0.05f)
                {
                    r.After = current;
                    return Reject(r, WriteOutcome.SkippedNoChange, $"当前 {current:0.##} 已为 0");
                }
            }

            // ---------- 8. 写入（只走属性 setter） ----------
            try
            {
                Write(stat, player, target);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("gate.write." + stat, e);
                r.Outcome = WriteOutcome.Failed;
                r.Message = e.GetType().Name + ": " + e.Message;
                r.After = TryReadCurrent(stat, player);
                Log.Warn("[write.error] stat=" + stat + " " + r.Message);
                return r;
            }

            // ---------- 9. 回读校验 ----------
            r.After = TryReadCurrent(stat, player);
            r.Outcome = WriteOutcome.Ok;
            r.Message = $"before={r.Before:0.##} target={r.Target:0.##} after={r.After:0.##}";

            Log.Info($"[write] stat={stat} {r.Message}");

            // 回读偏差过大 → 额外告警（不改结果，只提示）
            if (Mathf.Abs(r.After - r.Target) > Mathf.Max(0.5f, max * 0.02f))
                Log.Warn($"[write.verify] stat={stat} 回读值与目标偏差较大 after={r.After:0.##} target={r.Target:0.##}");

            return r;
        }

        // ------------------------------------------------------------------
        private static WriteResult Reject(WriteResult r, WriteOutcome o, string msg)
        {
            r.Outcome = o;
            r.Message = msg;
            // 拒绝日志去重，避免联机/死亡时刷屏
            Log.WarnOnce("gate." + r.Stat + "." + o, $"[write.reject] stat={r.Stat} reason={o} detail={msg}");
            return r;
        }

        private static bool TryRead(StatId stat, HumanCharacterController p, out float current, out float max, out string err)
        {
            current = 0f; max = 0f; err = null;
            try
            {
                var sd = PlayerDataAccess.GetStatusData(p, out string sdErr);
                if (sd == null) { err = sdErr; return false; }

                switch (stat)
                {
                    case StatId.Hp:
                        current = p.hp;
                        max = p.maxHP;          // 控制器上的 maxHP（不是 statusData.maxhp）
                        break;
                    case StatId.Energy:
                        current = p.energy;
                        max = sd.maxEnergy;
                        break;
                    case StatId.Fatigue:
                        current = p.fatigue;
                        max = sd.maxFatigue;
                        break;
                    case StatId.Hunger:
                        current = p.hunger;
                        max = sd.maxHunger;
                        break;
                    case StatId.Thirst:
                        current = p.thirst;
                        max = sd.maxThirst;
                        break;
                }
                return true;
            }
            catch (Exception e)
            {
                err = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        private static void Write(StatId stat, HumanCharacterController p, float v)
        {
            switch (stat)
            {
                case StatId.Hp: p.hp = v; break;
                case StatId.Energy: p.energy = v; break;
                case StatId.Fatigue: p.fatigue = v; break;
                case StatId.Hunger: p.hunger = v; break;
                case StatId.Thirst: p.thirst = v; break;
            }
        }

        private static float TryReadCurrent(StatId stat, HumanCharacterController p)
        {
            try
            {
                switch (stat)
                {
                    case StatId.Hp: return p.hp;
                    case StatId.Energy: return p.energy;
                    case StatId.Fatigue: return p.fatigue;
                    case StatId.Hunger: return p.hunger;
                    case StatId.Thirst: return p.thirst;
                }
            }
            catch { }
            return float.NaN;
        }
    }
}
