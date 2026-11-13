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

            // 自动补充材料：材料是否足够的判定 + 数量查询 + 实际扣除
            // 修复（船只/车辆/建筑）材料可负担判定
            Try(harmony, "CanAfford(WallRepairPlan)", () =>
            {
                var m = AccessTools.Method(typeof(MeleeWeapon_RepairHammer), "CanAfford",
                    new[] { typeof(BuildingWallDamageSystem.WallRepairPlan) });
                if (m == null) { Log.Warn("[supply] 未找到 CanAfford(WallRepairPlan)"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerPatches), nameof(AffordPlanPrefix)));
                Installed++;
            });

            Try(harmony, "CanAfford(List<RepairCostEntry>)", () =>
            {
                var m = AccessTools.Method(typeof(MeleeWeapon_RepairHammer), "CanAfford",
                    new[] { typeof(Il2CppSystem.Collections.Generic.List<BuildingWallDamageSystem.RepairCostEntry>) });
                if (m == null) { Log.Warn("[supply] 未找到 CanAfford(List<RepairCostEntry>)"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerPatches), nameof(AffordListPrefix)));
                Installed++;
            });

            Try(harmony, "HasItem", () =>
            {
                var m = AccessTools.Method(typeof(InventoryData), "HasItem", new[] { typeof(int), typeof(float) });
                if (m == null) { Log.Warn("[supply] 未找到 HasItem"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(PlayerPatches), nameof(HasItemPostfix)));
                Installed++;
            });

            Try(harmony, "GetTotalItemNumberByID", () =>
            {
                var m = AccessTools.Method(typeof(InventoryData), "GetTotalItemNumberByID", new[] { typeof(int) });
                if (m == null) { Log.Warn("[supply] 未找到 GetTotalItemNumberByID"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(PlayerPatches), nameof(CountPostfix)));
                Installed++;
            });

            Try(harmony, "RemoveItems", () =>
            {
                var m = AccessTools.Method(typeof(InventoryData), "RemoveItems", new[] { typeof(int), typeof(float) });
                if (m == null) { Log.Warn("[supply] 未找到 RemoveItems"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerPatches), nameof(RemoveItemsPrefix)));
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
                Unpatch(harmony, typeof(MeleeWeapon_RepairHammer), "CanAfford",
                    new[] { typeof(BuildingWallDamageSystem.WallRepairPlan) });
                Unpatch(harmony, typeof(MeleeWeapon_RepairHammer), "CanAfford",
                    new[] { typeof(Il2CppSystem.Collections.Generic.List<BuildingWallDamageSystem.RepairCostEntry>) });
                Unpatch(harmony, typeof(InventoryData), "HasItem", new[] { typeof(int), typeof(float) });
                Unpatch(harmony, typeof(InventoryData), "GetTotalItemNumberByID", new[] { typeof(int) });
                Unpatch(harmony, typeof(InventoryData), "RemoveItems", new[] { typeof(int), typeof(float) });
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

        private static int _supplyLog;

        /// <summary>修复/建造：先自动补齐材料，再让原逻辑判断。</summary>
        internal static bool AffordListPrefix(Il2CppSystem.Collections.Generic.List<BuildingWallDamageSystem.RepairCostEntry> costs)
        {
            try
            {
                if (!Features.AutoSupplyOn) return true;
                if (costs == null) return true;
                var ids = new System.Collections.Generic.List<int>();
                var nums = new System.Collections.Generic.List<int>();
                for (int i = 0; i < costs.Count; i++)
                {
                    var e = costs[i];
                    ids.Add(e.itemId);
                    nums.Add(e.count);
                }
                AutoSupply.EnsureMaterials(ids, nums);
            }
            catch (Exception e) { Log.WarnOnce("supply.affordlist", "[supply] 补齐失败: " + e.GetType().Name); }
            return true;
        }

        /// <summary>修复/建造（墙体计划）：先自动补齐材料，再让原逻辑判断。</summary>
        internal static bool AffordPlanPrefix(BuildingWallDamageSystem.WallRepairPlan plan)
        {
            try
            {
                if (!Features.AutoSupplyOn) return true;
                var costs = plan.costs;
                if (costs == null) return true;
                var ids = new System.Collections.Generic.List<int>();
                var nums = new System.Collections.Generic.List<int>();
                for (int i = 0; i < costs.Count; i++)
                {
                    var e = costs[i];
                    ids.Add(e.itemId);
                    nums.Add(e.count);
                }
                AutoSupply.EnsureMaterials(ids, nums);
            }
            catch (Exception e) { Log.WarnOnce("supply.affordplan", "[supply] 补齐失败: " + e.GetType().Name); }
            return true;
        }

        /// <summary>自动补充材料：修复（船只/车辆/建筑）材料检查直接通过。</summary>
        internal static bool AffordPrefix(ref bool __result)
        {
            try
            {
                if (!Features.AutoSupplyOn) return true;
                __result = true;
                if (_supplyLog < 3) { _supplyLog++; Log.Info("[supply] CanAfford 已拦截 → 修复材料视为充足"); }
                return false;
            }
            catch { return true; }
        }

        /// <summary>自动补充材料：HasItem 恒定返回 true（制造/修复的材料检查通过）。</summary>
        internal static void HasItemPostfix(ref bool __result)
        {
            try
            {
                if (!Features.AutoSupplyOn) return;
                if (__result) return;
                __result = true;
                if (_supplyLog < 3) { _supplyLog++; Log.Info("[supply] HasItem 已拦截 → 视为材料充足"); }
            }
            catch { }
        }

        /// <summary>自动补充材料：数量查询返回充足值。</summary>
        internal static void CountPostfix(ref float __result)
        {
            try
            {
                if (!Features.AutoSupplyOn) return;
                if (__result < 9999f) __result = 9999f;
            }
            catch { }
        }

        /// <summary>自动补充材料：跳过实际扣除，返回空列表（视为已消耗）。</summary>
        internal static bool RemoveItemsPrefix(InventoryData __instance, int m_itemId, float m_itemNumber, ref Il2CppSystem.Collections.Generic.List<ItemData> __result)
        {
            try
            {
                if (!Features.AutoSupplyOn) return true;
                __result = new Il2CppSystem.Collections.Generic.List<ItemData>();
                if (_supplyLog < 3) { _supplyLog++; Log.Info($"[supply] RemoveItems 已拦截 item={m_itemId} n={m_itemNumber}"); }
                return false;
            }
            catch { return true; }
        }

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





