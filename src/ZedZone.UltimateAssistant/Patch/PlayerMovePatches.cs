using System;
using HarmonyLib;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Patch
{
    /// <summary>
    /// 移动相关补丁：强制奔跑 / 移动加速 / Timer（玩家身体速度）。
    ///
    /// 依据（interop 元数据已确认存在）：
    ///  * `CharacterInput.IsSprintCanceledByCombatInput()` —— 攻击/开枪取消奔跑的判定入口；
    ///  * `HumanCharacterController.realMoveSpeed` —— 玩家实际移动速度（override getter）；
    ///  * `HumanCharacterController.OnTimeUpdate(float m_addTimeFloat)` —— 玩家自身的状态更新时间推进。
    ///
    /// 安全边界：
    ///  * 全部只对**本地玩家**生效；
    ///  * Timer 只放大玩家自己的 `OnTimeUpdate` 入参，**不触碰 Time.timeScale / 世界时间**；
    ///  * 关闭开关立即卸载补丁。
    /// </summary>
    internal static class PlayerMovePatches
    {
        internal static int Installed;
        private static bool _hooked;

        internal static void Install(Harmony harmony)
        {
            if (_hooked) return;
            Installed = 0;

            Try(harmony, "IsSprintCanceledByCombatInput", () =>
            {
                var m = AccessTools.Method(typeof(CharacterInput), "IsSprintCanceledByCombatInput");
                if (m == null) { Log.Warn("[move] 未找到 IsSprintCanceledByCombatInput"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(PlayerMovePatches), nameof(SprintCancelPostfix)));
                Installed++;
            });

            Try(harmony, "realMoveSpeed", () =>
            {
                var m = AccessTools.Method(typeof(HumanCharacterController), "get_realMoveSpeed");
                if (m == null) { Log.Warn("[move] 未找到 get_realMoveSpeed"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(PlayerMovePatches), nameof(MoveSpeedPostfix)));
                Installed++;
            });

            Try(harmony, "OnTimeUpdate", () =>
            {
                var m = AccessTools.Method(typeof(HumanCharacterController), "OnTimeUpdate", new[] { typeof(float) });
                if (m == null) { Log.Warn("[move] 未找到 OnTimeUpdate"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerMovePatches), nameof(TimeUpdatePrefix)));
                Installed++;
            });

            _hooked = true;
            Log.Info($"[move] 移动类补丁安装完成: {Installed} 个");
        }

        internal static void Uninstall(Harmony harmony)
        {
            if (harmony != null)
            {
                Unpatch(harmony, typeof(CharacterInput), "IsSprintCanceledByCombatInput", null);
                Unpatch(harmony, typeof(HumanCharacterController), "get_realMoveSpeed", null);
                Unpatch(harmony, typeof(HumanCharacterController), "OnTimeUpdate", new[] { typeof(float) });
            }
            _hooked = false;
            Installed = 0;
        }

        private static void Unpatch(Harmony h, Type t, string name, Type[] args)
        {
            try
            {
                var m = args == null ? AccessTools.Method(t, name) : AccessTools.Method(t, name, args);
                if (m != null) h.Unpatch(m, HarmonyPatchType.All, Plugin.PluginGuid);
            }
            catch { }
        }

        private static void Try(Harmony h, string name, Action a)
        {
            try { a(); }
            catch (Exception e) { Log.Warn($"[move] 补丁 {name} 失败: {e.GetType().Name}: {e.Message}"); }
        }

        private static bool IsLocalPlayer(BasicCharacterController c)
        {
            if (c == null) return false;
            try
            {
                var p = PlayerAccessor.Resolve(out _, out _);
                return p != null && c.Pointer == p.Pointer;
            }
            catch { return false; }
        }

        private static bool IsLocalPlayerInput()
        {
            try
            {
                var p = PlayerAccessor.Resolve(out _, out _);
                if (p == null) return false;
                var ci = CharacterInput.instance;
                if (ci == null) return false;
                return true;   // CharacterInput 是全局单例，作用于本地玩家控制
            }
            catch { return false; }
        }

        private static int _sprintLog, _speedLog, _timeLog;

        // ---------------- 补丁实现 ----------------

        /// <summary>强制奔跑：攻击/开枪不再取消奔跑。</summary>
        internal static void SprintCancelPostfix(ref bool __result)
        {
            try
            {
                if (!Features.AlwaysSprintOn) return;
                if (!IsLocalPlayerInput()) return;
                if (!__result) return;
                if (_sprintLog < 3) { _sprintLog++; Log.Info("[move] IsSprintCanceledByCombatInput 已拦截 → 保持奔跑"); }
                __result = false;
            }
            catch { }
        }

        /// <summary>移动加速：按倍率放大玩家实际移动速度。</summary>
        internal static void MoveSpeedPostfix(HumanCharacterController __instance, ref float __result)
        {
            try
            {
                if (!Features.MoveSpeedOn) return;
                if (!IsLocalPlayer(__instance)) return;
                float mult = Mathf.Clamp(ModConfig.MoveSpeedMultiplier?.Value ?? 1f, 1f, 3f);
                if (mult <= 1.0001f) return;
                __result *= mult;
                if (_speedLog < 3) { _speedLog++; Log.Info($"[move] realMoveSpeed x{mult:0.0} → {__result:0.##}"); }
            }
            catch { }
        }

        /// <summary>Timer：只放大玩家自身的时间推进（不影响世界时间）。</summary>
        internal static void TimeUpdatePrefix(HumanCharacterController __instance, ref float m_addTimeFloat)
        {
            try
            {
                if (!Features.TimerOn) return;
                if (!IsLocalPlayer(__instance)) return;
                float mult = Mathf.Clamp(ModConfig.TimerMultiplier?.Value ?? 1f, 1f, 5f);
                if (mult <= 1.0001f) return;
                m_addTimeFloat *= mult;
                if (_timeLog < 3) { _timeLog++; Log.Info($"[time] OnTimeUpdate dt x{mult:0.0}"); }
            }
            catch { }
        }
    }
}
