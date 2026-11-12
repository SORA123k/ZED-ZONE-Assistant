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
        RejectedUnreliableRange,
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
        internal string MaxSource;
        internal DateTime At = DateTime.Now;

        internal bool IsOk => Outcome == WriteOutcome.Ok;

        internal string OutcomeZh()
        {
            switch (Outcome)
            {
                case WriteOutcome.Ok: return "已恢复";
                case WriteOutcome.SkippedNoChange: return "无需恢复（只升不降）";
                case WriteOutcome.RejectedDisabled: return "功能已关闭";
                case WriteOutcome.RejectedMultiplayer: return "联机模式已拒绝执行";
                case WriteOutcome.RejectedNoPlayer: return "未找到玩家对象";
                case WriteOutcome.RejectedDead: return "玩家已死亡";
                case WriteOutcome.RejectedRespawnWindow: return "重生保护窗口内";
                case WriteOutcome.RejectedNotValidated: return "该功能尚未通过验证";
                case WriteOutcome.RejectedOutOfRange: return "数值越界/非法";
                case WriteOutcome.RejectedUnreliableRange: return "属性上限不可靠，拒绝写入";
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
    ///   5 死亡标志 → 6 重生窗口 → 7 **动态范围读取/校验** → 8 写入 → 9 回读校验
    ///
    /// 铁律：
    ///  * 只用**属性 setter**（`player.hp = v`），绝不写 `characterStatusData` 裸字段；
    ///  * 不写 GameConfigs、不写 skillProperty、不碰存档；
    ///  * **每次操作都重新读取当前值与上限**，绝不缓存；
    ///  * **只升不降**：恢复绝不把数值往下写（含"当前值高于上限"的情况）；
    ///  * **上限不可靠就拒绝**，不猜测、不回退到硬编码默认值。
    /// </summary>
    internal static class PlayerWriteGate
    {
        private const float Eps = 0.05f;

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
                    string mode = nm.mode.ToString();     // 运行期实测 Offline
                    if (mode != "Offline") { why = "mode=" + mode; return true; }
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

        internal static bool FatigueValidated(string dir)
            => !string.IsNullOrEmpty(dir) && dir != "unconfirmed";

        // ==================================================================
        internal static WriteResult Restore(StatId stat)
        {
            var r = new WriteResult { Stat = stat };

            // ---------- 1. 总开关 ----------
            if (!(ModConfig.EnableRestore?.Value ?? false))
                return Reject(r, WriteOutcome.RejectedDisabled, "一键恢复总开关关闭");

            // ---------- 2. 联机门禁（fail-closed） ----------
            if (IsMultiplayer(out string why))
                return Reject(r, WriteOutcome.RejectedMultiplayer, why);

            // ---------- 3. 功能开关 + 验证状态 ----------
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
                    if (!(ModConfig.RestoreFatigue?.Value ?? false))
                        return Reject(r, WriteOutcome.RejectedDisabled, "Fatigue 恢复已关闭（默认关闭）");
                    string dir = ModConfig.FatigueDirection?.Value ?? "unconfirmed";
                    if (!FatigueValidated(dir))
                        return Reject(r, WriteOutcome.RejectedNotValidated,
                            "Fatigue 方向未确认（G2）：观察冲刺后 fatigue 是升还是降，再把 FatigueDirection 设为 resource/exhaustion");
                    break;
                }
            }

            // ---------- 4. 每次重新解析玩家（禁止缓存） ----------
            HumanCharacterController player = null;
            try { player = PlayerAccessor.Resolve(out _, out _); }
            catch (Exception e) { Log.ErrorOnce("gate.resolve", e); }

            if (player == null)
                return Reject(r, WriteOutcome.RejectedNoPlayer, "playerCharacter == null");

            // ---------- 5. 死亡标志 ----------
            try { if (player.deadFlag) return Reject(r, WriteOutcome.RejectedDead, "deadFlag=true"); }
            catch (Exception e) { Log.WarnOnce("gate.dead", "deadFlag 读取失败: " + e.GetType().Name); }

            try { if (player.netDeadFlag) return Reject(r, WriteOutcome.RejectedDead, "netDeadFlag=true"); }
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

            // ---------- 7. 动态范围读取（每次重新读，绝不缓存） ----------
            StatRange range;
            try { range = StatRangeReader.Read(stat, player); }
            catch (Exception e)
            {
                Log.ErrorOnce("gate.range." + stat, e);
                return Reject(r, WriteOutcome.Failed, "范围读取异常: " + e.GetType().Name);
            }

            // 每次点击都完整记录（用户要求可定位"为什么恢复到错误数值"）
            Log.Info("[range] " + range.Describe());

            if (!range.Reliable)
            {
                r.MaxSource = range.MaxSource;
                return Reject(r, WriteOutcome.RejectedUnreliableRange,
                    string.IsNullOrEmpty(range.Error) ? "上限不可靠" : range.Error);
            }

            // ---------- 8. 目标值与"只升不降"判定 ----------
            float current = range.Current;
            float max = range.Max;
            float target = StatRangeReader.RecoveryTarget(stat, range);

            r.Before = current;
            r.Target = target;
            r.MaxSource = range.MaxSource;

            if (float.IsNaN(target) || float.IsInfinity(target))
                return Reject(r, WriteOutcome.RejectedOutOfRange, "target 非有限值");

            string fatigueDir = ModConfig.FatigueDirection?.Value ?? "unconfirmed";
            bool isLowering = stat == StatId.Fatigue && fatigueDir == "exhaustion";

            if (!isLowering)
            {
                // ★ 只升不降：当前值已达到或超过目标时一律不写。
                //   实测：玩家有效生命上限可超过 BasicCharacterController.maxHP（如 1840/1000），
                //   若无脑写上限会把血量降下去 —— 这就是本次严重 Bug。
                if (current >= target - Eps)
                {
                    r.After = current;
                    string note = current > max + Eps
                        ? $"（当前 {current:0.##} 高于读取到的上限 {max:0.##}，保持不动）"
                        : "";
                    return Reject(r, WriteOutcome.SkippedNoChange,
                        $"当前 {current:0.##} 已不低于目标 {target:0.##}{note}");
                }
            }
            else
            {
                if (current <= target + Eps)
                {
                    r.After = current;
                    return Reject(r, WriteOutcome.SkippedNoChange, $"当前 {current:0.##} 已为 0");
                }
            }

            // ---------- 9. 写入（只走属性 setter） ----------
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

            // ---------- 10. 回读校验 ----------
            r.After = TryReadCurrent(stat, player);
            r.Outcome = WriteOutcome.Ok;
            r.Message = $"before={r.Before:0.##} target={r.Target:0.##} after={r.After:0.##} maxSource={r.MaxSource}";

            Log.Info($"[write] stat={stat} {r.Message}");

            // 防回退校验：写入后不得低于写入前（除 fatigue=exhaustion 的刻意降低）
            if (!isLowering && r.After < r.Before - 1f)
                Log.Warn($"[write.regress] stat={stat} 写入后反而下降 before={r.Before:0.##} after={r.After:0.##}");

            if (Mathf.Abs(r.After - r.Target) > Mathf.Max(0.5f, max * 0.02f))
                Log.Warn($"[write.verify] stat={stat} 回读偏差较大 after={r.After:0.##} target={r.Target:0.##} max={max:0.##}");

            return r;
        }

        // ==================================================================
        private static WriteResult Reject(WriteResult r, WriteOutcome o, string msg)
        {
            r.Outcome = o;
            r.Message = msg;
            Log.WarnOnce("gate." + r.Stat + "." + o, $"[write.reject] stat={r.Stat} reason={o} detail={msg}");
            return r;
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
