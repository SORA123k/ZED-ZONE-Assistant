using System;
using HarmonyLib;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Patch
{
    /// <summary>
    /// 武器相关 Harmony 补丁（仅在功能开启时安装，关闭即卸载）。
    ///
    /// 游戏 1.0.17 的弹药/开火模型（来自 interop 签名）：
    ///   * `BasicRangedWeapon.CanFire()`        —— **开火闸门**（virtual bool），弹匣空/枪故障时返回 false；
    ///   * `BasicRangedWeapon.GetLoadedAmmo(ItemData)` —— 弹匣内剩余弹药（static 查询）；
    ///   * `BasicRangedWeapon.GetInventoryAmmoTotal(...)` —— 背包可用弹药总数（换弹时的来源判断）；
    ///   * `HumanCharacterController.RemoveAmmoItemData(...)` —— 实际扣除弹药物品；
    ///   * `BasicRangedWeapon.OnFillComplate(int)` —— 装填完成回调（可用于补满弹匣）。
    ///
    /// 两个功能的分工：
    ///   **Infinite Ammo**：不消耗备用弹药（改 GetInventoryAmmoTotal + RemoveAmmoItemData）。
    ///   **No Reload**   ：不因弹匣为空而无法射击（改 CanFire）+ 开火后补满弹匣（OnFillComplate）。
    ///
    /// 安全：
    ///   * 只对**本地玩家**生效（`__instance.character` 与玩家指针比对）；
    ///   * 每个补丁首行 try/catch，异常一律放行原逻辑；
    ///   * 前 3 次触发会写日志，便于确认补丁是否真的被调用。
    /// </summary>
    internal static class WeaponPatches
    {
        internal static int Installed;
        private static bool _hooked;

        private const int FakeAmmo = 9999;

        private static int _logCanFire, _logAmmoTotal, _logRemove, _logFire, _logReload;

        // ==================================================================
        internal static void Install(Harmony harmony)
        {
            if (_hooked) return;
            Installed = 0;

            // 1) 开火闸门（No Reload 的关键）
            Try(harmony, "CanFire", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "CanFire");
                if (m == null) { Log.Warn("[weapons] 未找到 CanFire"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(WeaponPatches), nameof(CanFirePrefix)));
                Installed++;
            });

            // 2) 背包弹药总数（Infinite Ammo）
            Try(harmony, "GetInventoryAmmoTotal", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "GetInventoryAmmoTotal",
                    new[] { typeof(BasicCharacterController), typeof(ItemData), typeof(int) });
                if (m == null) { Log.Warn("[weapons] 未找到 GetInventoryAmmoTotal"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(WeaponPatches), nameof(AmmoTotalPrefix)));
                Installed++;
            });

            // 3) 扣除弹药（Infinite Ammo）
            Try(harmony, "RemoveAmmoItemData", () =>
            {
                var m = AccessTools.Method(typeof(HumanCharacterController), "RemoveAmmoItemData");
                if (m == null) { Log.Warn("[weapons] 未找到 RemoveAmmoItemData"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(WeaponPatches), nameof(RemoveAmmoPrefix)));
                Installed++;
            });

            // 4) 开火后补满弹匣（No Reload 的第二层保险）
            Try(harmony, "Fire", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "Fire", new[] { typeof(float) });
                if (m == null) { Log.Warn("[weapons] 未找到 Fire(float)"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(WeaponPatches), nameof(FirePostfix)));
                Installed++;
            });

            // 5) 跳过换弹流程（No Reload）
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
            if (harmony != null)
            {
                Unpatch(harmony, typeof(BasicRangedWeapon), "CanFire", null);
                Unpatch(harmony, typeof(BasicRangedWeapon), "GetInventoryAmmoTotal",
                    new[] { typeof(BasicCharacterController), typeof(ItemData), typeof(int) });
                Unpatch(harmony, typeof(HumanCharacterController), "RemoveAmmoItemData", null);
                Unpatch(harmony, typeof(BasicRangedWeapon), "Fire", new[] { typeof(float) });
                Unpatch(harmony, typeof(BasicRangedWeapon), "Reload", null);
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

        private static void Once(ref int counter, string msg)
        {
            try
            {
                if (counter >= 3) return;
                counter++;
                Log.Info("[weapons] " + msg + (counter == 3 ? "（后续同类不再记录）" : ""));
            }
            catch { }
        }

        // ---------------- 补丁实现 ----------------

        /// <summary>无需换弹：把开火闸门强制置为"可以开火"。</summary>
        internal static bool CanFirePrefix(BasicRangedWeapon __instance, ref bool __result)
        {
            try
            {
                if (!Features.NoReloadOn) return true;
                if (__instance == null) return true;
                if (!IsLocalPlayer(__instance.character)) return true;

                Once(ref _logCanFire, "CanFire 已拦截 → 强制可开火");
                __result = true;
                return false;
            }
            catch { return true; }
        }

        /// <summary>无限弹药：告诉游戏"背包里弹药充足"。</summary>
        internal static bool AmmoTotalPrefix(BasicCharacterController character, ref int __result)
        {
            try
            {
                if (!Features.InfiniteAmmoOn) return true;
                if (!IsLocalPlayer(character)) return true;
                Once(ref _logAmmoTotal, "GetInventoryAmmoTotal 已拦截 → 返回充足弹药");
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
                Once(ref _logRemove, "RemoveAmmoItemData 已拦截 → 不消耗弹药");
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
                if (__instance == null) return;
                if (!IsLocalPlayer(__instance.character)) return;
                Once(ref _logFire, "Fire 后置 → 补满弹匣");
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
                if (__instance == null) return true;
                if (!IsLocalPlayer(__instance.character)) return true;
                Once(ref _logReload, "Reload 已拦截 → 跳过换弹");
                __result = true;
                return false;
            }
            catch { return true; }
        }
    }
}

