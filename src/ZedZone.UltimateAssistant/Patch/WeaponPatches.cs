using System;
using HarmonyLib;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Patch
{
    /// <summary>
    /// 武器相关 Harmony 补丁（仅在功能开启时安装，关闭即卸载）。
    ///
    /// 拦截点（游戏 1.0.16 / build 26627937 确认存在）：
    ///   1. BasicRangedWeapon.GetInventoryAmmoTotal(character, weaponData, ammoId)
    ///      —— "背包里共有多少可用弹药"的查询入口。
    ///   2. HumanCharacterController.RemoveAmmoItemData(ammoItemData, externalInventory)
    ///      —— 实际扣除弹药物品的入口（返回 bool）。
    ///   3. BasicRangedWeapon.Fire(float)  —— 开火；用于"无需换弹"时补满弹匣。
    ///   4. BasicRangedWeapon.Reload()     —— 换弹；开启免换弹时直接跳过。
    ///
    /// 安全约束：
    ///   * 只对**本地玩家**生效（用 __instance.character / __instance 与玩家比对）；
    ///   * 所有补丁首行做 try/catch，任何异常都放行原逻辑（绝不让游戏卡死）；
    ///   * 功能关闭时补丁被卸载，恢复游戏原始行为。
    /// </summary>
    internal static class WeaponPatches
    {
        internal static int Installed;
        private static bool _hooked;

        private const int FakeAmmo = 9999;

        internal static void Install(Harmony harmony)
        {
            if (_hooked) return;
            Installed = 0;

            Try(harmony, "GetInventoryAmmoTotal", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "GetInventoryAmmoTotal",
                    new[] { typeof(BasicCharacterController), typeof(ItemData), typeof(int) });
                if (m == null) { Log.Warn("[weapons] 未找到 GetInventoryAmmoTotal"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(WeaponPatches), nameof(AmmoTotalPrefix)));
                Installed++;
            });

            Try(harmony, "RemoveAmmoItemData", () =>
            {
                var m = AccessTools.Method(typeof(HumanCharacterController), "RemoveAmmoItemData");
                if (m == null) { Log.Warn("[weapons] 未找到 RemoveAmmoItemData"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(WeaponPatches), nameof(RemoveAmmoPrefix)));
                Installed++;
            });

            Try(harmony, "Fire", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "Fire", new[] { typeof(float) });
                if (m == null) { Log.Warn("[weapons] 未找到 Fire(float)"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(WeaponPatches), nameof(FirePostfix)));
                Installed++;
            });

            Try(harmony, "Reload", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "Reload");
                if (m == null) { Log.Warn("[weapons] 未找到 Reload"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(WeaponPatches), nameof(ReloadPrefix)));
                Installed++;
            });

            _hooked = true;
            Log.Info($"[weapons] 补丁安装完成，成功 {Installed} 个");
        }

        internal static void Uninstall(Harmony harmony)
        {
            try
            {
                if (harmony != null)
                {
                    harmony.Unpatch(typeof(BasicRangedWeapon).GetMethod("GetInventoryAmmoTotal",
                        new[] { typeof(BasicCharacterController), typeof(ItemData), typeof(int) }), HarmonyPatchType.All, Plugin.PluginGuid);
                }
            }
            catch { }
            try
            {
                if (harmony != null)
                    harmony.Unpatch(typeof(HumanCharacterController).GetMethod("RemoveAmmoItemData"), HarmonyPatchType.All, Plugin.PluginGuid);
            }
            catch { }
            try
            {
                if (harmony != null)
                    harmony.Unpatch(typeof(BasicRangedWeapon).GetMethod("Fire", new[] { typeof(float) }), HarmonyPatchType.All, Plugin.PluginGuid);
            }
            catch { }
            try
            {
                if (harmony != null)
                    harmony.Unpatch(typeof(BasicRangedWeapon).GetMethod("Reload"), HarmonyPatchType.All, Plugin.PluginGuid);
            }
            catch { }
            _hooked = false;
            Installed = 0;
        }

        private static void Try(Harmony h, string name, Action a)
        {
            try { a(); }
            catch (Exception e) { Log.Warn($"[weapons] 补丁 {name} 安装失败: {e.GetType().Name}: {e.Message}"); }
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

        // ---------------- 补丁实现 ----------------

        /// <summary>无限弹药：告诉游戏"背包里弹药充足"。</summary>
        internal static bool AmmoTotalPrefix(BasicCharacterController character, ref int __result)
        {
            try
            {
                if (!Features.InfiniteAmmoOn) return true;
                if (!IsLocalPlayer(character)) return true;
                __result = FakeAmmo;
                return false;
            }
            catch { return true; }
        }

        /// <summary>无限弹药：扣除弹药时直接当作成功，不真正减少。</summary>
        internal static bool RemoveAmmoPrefix(HumanCharacterController __instance, ref bool __result)
        {
            try
            {
                if (!Features.InfiniteAmmoOn) return true;
                if (!IsLocalPlayer(__instance)) return true;
                __result = true;
                return false;
            }
            catch { return true; }
        }

        /// <summary>无需换弹：每次开火后把弹匣补满。</summary>
        internal static void FirePostfix(BasicRangedWeapon __instance)
        {
            try
            {
                if (!Features.NoReloadOn) return;
                if (!IsLocalPlayer(__instance.character)) return;
                __instance.OnFillComplate(FakeAmmo);
            }
            catch { }
        }

        /// <summary>无需换弹：换弹流程直接跳过（弹匣已由 FirePostfix 维持）。</summary>
        internal static bool ReloadPrefix(BasicRangedWeapon __instance, ref bool __result)
        {
            try
            {
                if (!Features.NoReloadOn) return true;
                if (!IsLocalPlayer(__instance.character)) return true;
                __result = true;
                return false;
            }
            catch { return true; }
        }
    }
}

