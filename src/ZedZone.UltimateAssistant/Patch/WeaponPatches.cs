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
    ///   * `BasicRangedWeapon.CanFire()`                —— **开火闸门**（virtual bool），弹匣空/故障时 false；
    ///   * `BasicRangedWeapon.GetInventoryAmmoTotal(...)` —— 背包可用弹药总数（换弹来源判断）；
    ///   * `HumanCharacterController.RemoveAmmoItemData(...)` —— 实际扣除弹药物品；
    ///   * `BasicRangedWeapon.GetGunTemperature(ItemData)` —— 枪械温度（过热相关）。
    ///
    /// 设计取舍（按用户要求）：
    ///   * **No Reload**：只放开 `CanFire`，**不再拦截 `Reload`**，
    ///     因此弹匣为 0 也能射击，但按 R 仍然走游戏原生换弹流程；
    ///   * **Infinite Ammo**：不消耗备用弹药（总数虚报 + 扣除跳过）；
    ///   * **No Overheat**：只把**玩家自己武器**的温度读取置 0（用 ItemData 指针集合精确匹配，
    ///     不影响 NPC 武器）。
    ///
    /// 安全：只对本地玩家生效；每个补丁 try/catch 放行原逻辑；前 3 次触发写日志便于确认。
    /// </summary>
    internal static class WeaponPatches
    {
        internal static int Installed;
        private static bool _hooked;

        private const int FakeAmmo = 9999;

        private static int _logCanFire, _logAmmoTotal, _logRemove, _logTemp, _logDur, _logDf;

        // ==================================================================
        internal static void Install(Harmony harmony)
        {
            if (_hooked) return;
            Installed = 0;

            Try(harmony, "CanFire", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "CanFire");
                if (m == null) { Log.Warn("[weapons] 未找到 CanFire"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(WeaponPatches), nameof(CanFirePrefix)));
                Installed++;
            });

            // 无限弹药（弹匣层）：直接把"弹匣里已装子弹数"读成无限，不再依赖背包弹药
            Try(harmony, "GetLoadedAmmo", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "GetLoadedAmmo", new[] { typeof(ItemData) });
                if (m == null) { Log.Warn("[weapons] 未找到 GetLoadedAmmo"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(WeaponPatches), nameof(LoadedAmmoPostfix)));
                Installed++;
            });

            Try(harmony, "RemoveAmmoItemData", () =>
            {
                var m = AccessTools.Method(typeof(HumanCharacterController), "RemoveAmmoItemData");
                if (m == null) { Log.Warn("[weapons] 未找到 RemoveAmmoItemData"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(WeaponPatches), nameof(RemoveAmmoPrefix)));
                Installed++;
            });



            // 枪械耐久：读取层覆盖（原生层维护，无托管存储字段可写）
            // 不会卡壳：卡壳判定入口 get_isMalfunction
            Try(harmony, "get_isMalfunction", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "get_isMalfunction");
                if (m == null) { Log.Warn("[weapons] 未找到 get_isMalfunction"); return; }
                harmony.Patch(m,
                    prefix: new HarmonyMethod(typeof(WeaponPatches), nameof(IsMalfunctionPrefix)),
                    postfix: new HarmonyMethod(typeof(WeaponPatches), nameof(IsMalfunctionPostfix)));
                Installed++;
            });

            // 诊断：Fire 前后耐久变化（定位扣减是否发生在 Fire 内部）
            Try(harmony, "Fire.probe", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "Fire", new[] { typeof(float) });
                if (m == null) return;
                harmony.Patch(m,
                    prefix: new HarmonyMethod(typeof(WeaponPatches), nameof(FireProbePrefix)),
                    postfix: new HarmonyMethod(typeof(WeaponPatches), nameof(FireProbePostfix)));
                Installed++;
            });


            Try(harmony, "GetGunTemperature", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "GetGunTemperature", new[] { typeof(ItemData) });
                if (m == null) { Log.Warn("[weapons] 未找到 GetGunTemperature"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(WeaponPatches), nameof(GunTempPostfix)));
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
                Unpatch(harmony, typeof(InventoryData), "CostItemDurability", new[] { typeof(ItemData), typeof(float) });
                Unpatch(harmony, typeof(HumanCharacterController), "CostItemDurability", new[] { typeof(ItemData), typeof(float) });
                Unpatch(harmony, typeof(HumanCharacterController), "CostItemDurabilityExecute", new[] { typeof(ItemData), typeof(float) });

                Unpatch(harmony, typeof(BasicRangedWeapon), "GetGunTemperature", new[] { typeof(ItemData) });
                Unpatch(harmony, typeof(BasicRangedWeapon), "get_isMalfunction", null);
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

        /// <summary>无需换弹：弹匣为 0 也允许开火（不拦截 Reload，R 键换弹保持原生）。</summary>
        internal static bool CanFirePrefix(BasicRangedWeapon __instance, ref bool __result)
        {
            try
            {
                if (!Features.NoReloadOn && !Features.JamBypassOn) return true;
                if (__instance == null) return true;
                if (!IsLocalPlayer(__instance.character)) return true;

                Once(ref _logCanFire, "CanFire 已拦截 → 空弹匣/卡壳也可开火（R 换弹仍生效）");
                __result = true;
                return false;
            }
            catch { return true; }
        }

        /// <summary>无限弹药：背包弹药视为充足。</summary>
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

        private static int _logLoadedAmmo;
        private const int InfiniteMag = 999;

        /// <summary>
        /// 无限弹药（弹匣层）：把弹匣内已装子弹数读成 999。
        /// 这样 HUD 显示的是"弹匣里很多子弹"，不需要换弹，也不再依赖背包弹药。
        /// </summary>
        internal static void LoadedAmmoPostfix(ItemData m_weaponData, ref int __result)
        {
            try
            {
                if (!Features.InfiniteAmmoOn) return;
                if (m_weaponData == null) return;
                if (!WeaponUtil.IsPlayerItem(m_weaponData.Pointer)) return;
                __result = InfiniteMag;
                Once(ref _logLoadedAmmo, "GetLoadedAmmo 已拦截 → 弹匣内子弹视为无限");
            }
            catch { }
        }

        /// <summary>无限弹药：不真正扣除弹药物品。</summary>
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

        /// <summary>
        /// 无限耐久：拦截玩家物品的耐久扣除，使工具/武器使用时不掉耐久。
        /// `InventoryData.CostItemDurability(ItemData, float)` 是扣耐久入口，
        /// 跳过原逻辑并返回 true（表示"已扣"，让上层流程继续）。
        /// </summary>
        internal static bool CostDurabilityPrefix(InventoryData __instance, ItemData itemData, ref bool __result)
        {
            try
            {
                if (!Features.WeaponDurabilityOn) return true;
                if (itemData == null) return true;
                if (!WeaponUtil.IsPlayerItem(itemData.Pointer)) return true;

                Once(ref _logDur, "CostItemDurability 已拦截 → 不消耗耐久");
                __result = true;
                return false;
            }
            catch { return true; }
        }

        /// <summary>
        /// 无限耐久（真正入口）：`HumanCharacterController.CostItemDurability`。
        /// 跳过原逻辑并返回 true（视为已扣），使工具/武器使用不掉耐久。
        /// </summary>
        internal static bool HccCostDurPrefix(HumanCharacterController __instance, ItemData itemData, ref bool __result)
        {
            try
            {
                if (!Features.WeaponDurabilityOn) return true;
                if (!IsLocalPlayer(__instance)) return true;
                Once(ref _logDur, "HCC.CostItemDurability 已拦截 → 不消耗耐久");
                __result = true;
                return false;
            }
            catch { return true; }
        }

        /// <summary>无限耐久：Execute 变体同样拦截。</summary>
        internal static bool HccCostDurExecPrefix(HumanCharacterController __instance, ItemData itemData, ref bool __result)
        {
            try
            {
                if (!Features.WeaponDurabilityOn) return true;
                if (!IsLocalPlayer(__instance)) return true;
                Once(ref _logDur, "HCC.CostItemDurabilityExecute 已拦截 → 不消耗耐久");
                __result = true;
                return false;
            }
            catch { return true; }
        }

        private static int _jamLog;

        /// <summary>
        /// 不会卡壳：卡壳判定入口。get_isMalfunction 是 BasicRangedWeapon 的公开 getter，
        /// 游戏用它判断"当前是否卡壳"。开启后对本地玩家武器恒返回 false。
        /// </summary>
        /// <summary>
        /// 前缀守卫：游戏原 get_isMalfunction 在 weaponData 未初始化时会抛 NRE。
        /// 这里提前判空直接返回 false，避免异常穿透 il2cpp trampoline 刷日志。
        /// </summary>
        internal static bool IsMalfunctionPrefix(BasicRangedWeapon __instance, ref bool __result)
        {
            try
            {
                if (__instance == null) { __result = false; return false; }
                var wd = __instance.weaponData;
                if (wd == null) { __result = false; return false; }
                return true;
            }
            catch { __result = false; return false; }
        }

        internal static void IsMalfunctionPostfix(BasicRangedWeapon __instance, ref bool __result)
        {
            try
            {
                if (!Features.NoJamOn) return;
                if (__result == false) return;
                if (__instance == null) return;
                if (!IsLocalPlayer(__instance.character)) return;

                if (_jamLog < 3) { _jamLog++; Log.Info("[weapons] get_isMalfunction 已覆盖 → 视为未卡壳"); }
                __result = false;
            }
            catch { }
        }

        private static readonly System.Collections.Generic.Dictionary<IntPtr, float> _fireBefore =
            new System.Collections.Generic.Dictionary<IntPtr, float>();
        private static int _fireProbeBudget = 40;

        internal static void FireProbePrefix(BasicRangedWeapon __instance)
        {
            Features.LastFireAt = Time.unscaledTime;
            try
            {
                if (_fireProbeBudget <= 0) return;
                if (__instance == null) return;
                var wd = __instance.weaponData;
                if (wd == null) return;
                var pr = wd.properties;
                if (pr == null || pr.Length <= 0) return;
                _fireBefore[__instance.Pointer] = pr[0];
            }
            catch { }
        }

        internal static void FireProbePostfix(BasicRangedWeapon __instance)
        {
            Features.LastFireAt = Time.unscaledTime;
            try
            {
                if (_fireProbeBudget <= 0) return;
                if (__instance == null) return;
                var wd = __instance.weaponData;
                if (wd == null) return;
                var pr = wd.properties;
                if (pr == null || pr.Length <= 0) return;

                if (!_fireBefore.TryGetValue(__instance.Pointer, out float before))
                {
                    _fireBefore[__instance.Pointer] = pr[0];
                    return;
                }
                float after = pr[0];

                int loaded = -1;
                try { loaded = BasicRangedWeapon.GetLoadedAmmo(wd); } catch { }
                bool malf = false;
                try { malf = __instance.isMalfunction; } catch { }
                string mflag = "?", temp = "?", plusOne = "?";
                try { mflag = wd.GetProperty("MalfunctionFlag") ?? "-"; } catch { }
                try { temp = wd.GetProperty("GunTemperature") ?? "-"; } catch { }
                try { plusOne = wd.GetProperty("GunPlusOne") ?? "-"; } catch { }
                float fireFlag = -1f;
                try { fireFlag = __instance.fireTimeFlag; } catch { }

                if (_fireProbeBudget > 0)
                {
                    _fireProbeBudget--;
                    Log.Info($"[jam-probe] itemId={wd.itemId} t={Time.unscaledTime:0.###} frame={Time.frameCount} | " +
                             $"dur {before:0.##}->{after:0.##} | loaded={loaded} | malf={malf} MalfunctionFlag={mflag} | " +
                             $"GunPlusOne={plusOne} temp={temp} | fireTimeFlag={fireFlag:0.###} | weapon=0x{__instance.Pointer.ToInt64():X}");
                }
            }
            catch { }
        }

        /// <summary>
        /// 枪械耐久：**读取层覆盖**。
        ///
        /// 实测依据（Diagnostics\il2cpp_meta_*.log）：
        ///  * BasicRangedWeapon 108 个实例字段全部与 durabilityFloat 不匹配；
        ///  * ItemData 29 个字段同样零匹配；
        ///  * weapon.weaponData.durability 恒 100，与 durabilityFloat 无关。
        ///  => 枪械耐久由原生层维护，托管侧**没有可写的存储字段**。
        ///
        /// 因此不猜存储、不写未知内存，改为在 getter 返回值上覆盖：
        /// 对本地玩家武器恒返回 1（满耐久），使 UI 与游戏内判定都视作完好。
        /// 关闭开关即自动卸载，恢复原生读数。
        /// </summary>
        internal static void DurabilityPostfix(BasicRangedWeapon __instance, ref float __result)
        {
            try
            {
                if (!Features.WeaponDurabilityOn) return;
                if (__instance == null) return;
                if (!IsLocalPlayer(__instance.character)) return;

                if (__result < 0.999f)
                    Once(ref _logDf, $"get_durabilityFloat 已覆盖：{__result:0.####} -> 1（满耐久）");

                __result = 1f;
            }
            catch { }
        }

        /// <summary>取消过热：只把玩家自己的武器温度读作 0。</summary>
        internal static void GunTempPostfix(ItemData m_weaponData, ref float __result)
        {
            try
            {
                if (!Features.NoOverheatOn) return;
                if (m_weaponData == null) return;
                if (!WeaponUtil.IsPlayerWeapon(m_weaponData.Pointer)) return;
                Once(ref _logTemp, "GetGunTemperature 已拦截 → 温度视为 0");
                __result = 0f;
            }
            catch { }
        }
    }
}
















