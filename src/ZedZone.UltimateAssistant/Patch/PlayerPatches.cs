using System;
using HarmonyLib;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Patch
{
    /// <summary>
    /// 玩家生命 / 体力「防扣除」补丁（独立开关，仅本地玩家）。
    ///
    /// 生命：`HumanCharacterController.OnHit(...)` 是玩家受伤的统一入口，
    ///       前缀直接跳过并返回 false（未命中），因此生命值不会被扣。
    /// 体力：`HumanCharacterController.CostEnergy(float)` 是消耗体力的统一入口，
    ///       前缀跳过并返回 true（视为已支付），体力不减少；
    ///       另外 `RunInput(bool costEnergy)` 提供 costEnergy 开关，
    ///       一并置 false 作为第二层保险（不影响动画与恢复）。
    ///
    /// 只在开关开启时安装，关闭立即 Unpatch，不留后台任务。
    /// </summary>
    internal static class PlayerPatches
    {
        internal static int Installed;
        private static bool _hooked;

        internal static void Install(Harmony harmony)
        {
            if (_hooked) return;
            Installed = 0;

            Try(harmony, "OnHit", () =>
            {
                var m = AccessTools.Method(typeof(HumanCharacterController), "OnHit");
                if (m == null) { Log.Warn("[player] 未找到 OnHit"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerPatches), nameof(OnHitPrefix)));
                Installed++;
            });

            Try(harmony, "CostEnergy", () =>
            {
                var m = AccessTools.Method(typeof(HumanCharacterController), "CostEnergy", new[] { typeof(float) });
                if (m == null) { Log.Warn("[player] 未找到 CostEnergy"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerPatches), nameof(CostEnergyPrefix)));
                Installed++;
            });

            Try(harmony, "set_hunger", () =>
            {
                var m = AccessTools.Method(typeof(BasicCharacterController), "set_hunger", new[] { typeof(float) });
                if (m == null) { Log.Warn("[player] 未找到 set_hunger"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerPatches), nameof(HungerPrefix)));
                Installed++;
            });

            Try(harmony, "set_thirst", () =>
            {
                var m = AccessTools.Method(typeof(BasicCharacterController), "set_thirst", new[] { typeof(float) });
                if (m == null) { Log.Warn("[player] 未找到 set_thirst"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerPatches), nameof(ThirstPrefix)));
                Installed++;
            });

            Try(harmony, "RunInput", () =>
            {
                var m = AccessTools.Method(typeof(HumanCharacterController), "RunInput", new[] { typeof(bool) });
                if (m == null) { Log.Warn("[player] 未找到 RunInput"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerPatches), nameof(RunInputPrefix)));
                Installed++;
            });

            _hooked = true;
            Log.Info($"[player] 玩家防扣除补丁安装完成: {Installed} 个");
        }

        internal static void Uninstall(Harmony harmony)
        {
            if (harmony != null)
            {
                Unpatch(harmony, typeof(HumanCharacterController), "OnHit", null);
                Unpatch(harmony, typeof(HumanCharacterController), "CostEnergy", new[] { typeof(float) });
                Unpatch(harmony, typeof(HumanCharacterController), "RunInput", new[] { typeof(bool) });
                Unpatch(harmony, typeof(BasicCharacterController), "set_hunger", new[] { typeof(float) });
                Unpatch(harmony, typeof(BasicCharacterController), "set_thirst", new[] { typeof(float) });
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
            catch (Exception e) { Log.Warn($"[player] 补丁 {name} 失败: {e.GetType().Name}: {e.Message}"); }
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

        private static int _hpLog, _staLog, _hungerLog, _thirstLog;

        // ---------------- 补丁实现 ----------------

        /// <summary>
        /// 不会扣饱食度：拦截 set_hunger，**只阻止减少**（进食仍可增加）。
        /// set_hunger 是 BasicCharacterController 的公开 setter（元数据已确认存在）。
        /// </summary>
        internal static bool HungerPrefix(BasicCharacterController __instance, ref float value)
        {
            try
            {
                if (!Features.NoHungerLossOn) return true;
                if (!IsLocalPlayer(__instance)) return true;

                float cur = __instance.hunger;
                if (value < cur - 0.0001f)
                {
                    value = cur;
                    if (_hungerLog < 3) { _hungerLog++; Log.Info($"[player] set_hunger 已拦截减少（{cur:0.##} <- 请求更低值）"); }
                }
                return true;
            }
            catch { return true; }
        }

        /// <summary>不会扣水分：拦截 set_thirst，只阻止减少。</summary>
        internal static bool ThirstPrefix(BasicCharacterController __instance, ref float value)
        {
            try
            {
                if (!Features.NoThirstLossOn) return true;
                if (!IsLocalPlayer(__instance)) return true;

                float cur = __instance.thirst;
                if (value < cur - 0.0001f)
                {
                    value = cur;
                    if (_thirstLog < 3) { _thirstLog++; Log.Info($"[player] set_thirst 已拦截减少（{cur:0.##} <- 请求更低值）"); }
                }
                return true;
            }
            catch { return true; }
        }

        /// <summary>不会扣血：跳过玩家受伤逻辑。</summary>
        internal static bool OnHitPrefix(HumanCharacterController __instance, ref bool __result)
        {
            try
            {
                if (!Features.NoHpLossOn) return true;
                if (!IsLocalPlayer(__instance)) return true;
                if (_hpLog < 3)
                {
                    _hpLog++;
                    Log.Info("[player] OnHit 已拦截 → 本次伤害不扣血");
                }
                __result = false;
                return false;
            }
            catch { return true; }
        }

        /// <summary>不会扣体力：跳过体力消耗并返回成功。</summary>
        internal static bool CostEnergyPrefix(HumanCharacterController __instance, float m_energy, ref bool __result)
        {
            try
            {
                if (!Features.NoStaminaLossOn) return true;
                if (!IsLocalPlayer(__instance)) return true;
                if (_staLog < 3)
                {
                    _staLog++;
                    Log.Info($"[player] CostEnergy 已拦截（请求 {m_energy:0.###}）→ 体力不减少");
                }
                __result = true;
                return false;
            }
            catch { return true; }
        }

        /// <summary>不会扣体力：把 RunInput 的 costEnergy 置 false（第二层保险）。</summary>
        internal static void RunInputPrefix(HumanCharacterController __instance, ref bool costEnergy)
        {
            try
            {
                if (!Features.NoStaminaLossOn) return;
                if (!IsLocalPlayer(__instance)) return;
                costEnergy = false;
            }
            catch { }
        }
    }
}

