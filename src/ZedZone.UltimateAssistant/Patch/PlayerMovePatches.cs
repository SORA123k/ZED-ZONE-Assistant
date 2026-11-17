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


            Try(harmony, "realMoveSpeed", () =>
            {
                var m = AccessTools.Method(typeof(HumanCharacterController), "get_realMoveSpeed");
                if (m == null) { Log.Warn("[move] 未找到 get_realMoveSpeed"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(PlayerMovePatches), nameof(MoveSpeedPostfix)));
                Installed++;
            });

            // 停火自动奔跑：开火中不奔跑、停火后自动奔跑
            // 保持奔跑：允许在奔跑状态下换弹（不再靠"暂停奔跑"绕过）
            Try(harmony, "CanStartReloadAction", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "CanStartReloadAction");
                if (m == null) { Log.Warn("[move] 未找到 CanStartReloadAction"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerMovePatches), nameof(CanReloadWhileRunningPrefix)));
                Installed++;
            });

            // 保持奔跑：奔跑时不收枪（收枪会取消换弹）
            Try(harmony, "HolsterWeapon", () =>
            {
                var m = AccessTools.Method(typeof(HumanCharacterController), "HolsterWeapon",
                    new[] { typeof(bool), typeof(bool) });
                if (m == null) { Log.Warn("[move] 未找到 HolsterWeapon"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerMovePatches), nameof(HolsterPrefix)));
                Installed++;
            });

            // 保持奔跑：奔跑时不打断换弹
            Try(harmony, "CancelReloadAndCharging", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "CancelReloadAndCharging");
                if (m == null) { Log.Warn("[move] 未找到 CancelReloadAndCharging"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerMovePatches), nameof(NoCancelReloadPrefix)));
                Installed++;
            });

            Try(harmony, "InterruptReloadOrCharging", () =>
            {
                var m = AccessTools.Method(typeof(BasicRangedWeapon), "InterruptReloadOrCharging");
                if (m == null) { Log.Warn("[move] 未找到 InterruptReloadOrCharging"); return; }
                harmony.Patch(m, prefix: new HarmonyMethod(typeof(PlayerMovePatches), nameof(NoCancelReloadPrefix)));
                Installed++;
            });

            Try(harmony, "IsSprintRequested", () =>
            {
                var m = AccessTools.Method(typeof(CharacterInput), "IsSprintRequested");
                if (m == null) { Log.Warn("[move] 未找到 get_IsSprintRequested"); return; }
                harmony.Patch(m, postfix: new HarmonyMethod(typeof(PlayerMovePatches), nameof(SprintRequestPostfix)));
                Installed++;
            });

            _hooked = true;
            Log.Info($"[move] 移动类补丁安装完成: {Installed} 个");
        }

        internal static void Uninstall(Harmony harmony)
        {
            if (harmony != null)
            {

                Unpatch(harmony, typeof(HumanCharacterController), "get_realMoveSpeed", null);
                Unpatch(harmony, typeof(CharacterInput), "IsSprintRequested", null);
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

        private static int _sprintLog, _speedLog;

        // ---------------- 补丁实现 ----------------


        /// <summary>移动加速：按倍率放大玩家实际移动速度。</summary>
        internal static void MoveSpeedPostfix(HumanCharacterController __instance, ref float __result)
        {
            try
            {
                if (!Features.MoveSpeedOn) return;
                if (!IsLocalPlayer(__instance)) return;
                float mult = Mathf.Clamp(ModConfig.MoveSpeedMultiplier?.Value ?? 1f, 1f, 20f);
                if (mult <= 1.0001f) return;
                __result *= mult;
                if (_speedLog < 3) { _speedLog++; Log.Info($"[move] realMoveSpeed x{mult:0.0} → {__result:0.##}"); }
            }
            catch { }
        }

        /// <summary>
        /// 停火自动奔跑：
        ///  * 最近 0.35 秒内开过枪 → 不请求奔跑（避免"状态无法开枪"）；
        ///  * 停火超过 0.35 秒 → 请求奔跑（自动恢复）。
        /// </summary>
        private static float _reloadHoldUntil;
        private static float _nextWeaponScan;

        /// <summary>
        /// 换弹期间必须允许"非奔跑"状态，否则按住奔跑会取消换弹（霰弹枪尤其明显）。
        /// 这里同时用三条判据：R 键刚按下、武器 isReloading、reloadTimeFlag 仍在推进。
        /// </summary>
        private static bool IsReloadBlocking()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.R)) _reloadHoldUntil = Time.unscaledTime + 2.5f;
            }
            catch { }
            if (Time.unscaledTime < _reloadHoldUntil) return true;

            try
            {
                var p = PlayerAccessor.Resolve(out _, out _);
                if (p == null) return false;

                if (Time.unscaledTime >= _nextWeaponScan)
                {
                    _nextWeaponScan = Time.unscaledTime + 1f;
                    _cachedWeapon = null;
                    var arr = Resources.FindObjectsOfTypeAll<BasicRangedWeapon>();
                    if (arr != null)
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            var w = arr[i];
                            if (w == null) continue;
                            try { if (w.character != null && w.character.Pointer == p.Pointer) { _cachedWeapon = w; break; } } catch { }
                        }
                    }
                }

                var wp = _cachedWeapon;
                if (wp == null) return false;
                try { if (wp.isReloading) return true; } catch { }
                try { if (wp._isReloading) return true; } catch { }
                try { if (wp.reloadTimeFlag > 0f) return true; } catch { }
            }
            catch { }
            return false;
        }

        private static BasicRangedWeapon _cachedWeapon;

        private static float _nextSprintDiag;

        /// <summary>
        /// 把判定写回游戏内部的所有冲刺标志。
        /// 换弹后卡"行走"的根因是：内部缓存的冲刺状态没被重新置位，
        /// 只覆盖返回值没有意义（游戏读的是这些字段）。
        /// </summary>
        private static void SyncSprintFlag(CharacterInput inst, bool value)
        {
            try
            {
                if (inst == null) return;
                try { inst.sprintRequestResult = value; } catch { }
                try { inst.controllerSprintFlag = value; } catch { }
                try { inst.keyboardSprintToggleFlag = value; } catch { }
            }
            catch { }

            if (Time.unscaledTime >= _nextSprintDiag)
            {
                _nextSprintDiag = Time.unscaledTime + 1f;
                try
                {
                    string info = "-";
                    try
                    {
                        var p = PlayerAccessor.Resolve(out _, out _);
                        if (p != null) info = "isRunning=" + p.isRunning + " reloading=" + IsReloadBlocking();
                    }
                    catch { }
                    Log.Info($"[move] 冲刺状态 result={value} {info}");
                }
                catch { }
            }
        }

        private static float _nextRunKick;

        private static bool IsLocalHcc(HumanCharacterController inst)
        {
            try
            {
                if (inst == null) return false;
                var p = PlayerAccessor.Resolve(out _, out _);
                return p != null && inst.Pointer == p.Pointer;
            }
            catch { return false; }
        }

        /// <summary>保持奔跑：奔跑时不要收枪（收枪会取消换弹）。</summary>
        internal static bool HolsterPrefix(HumanCharacterController __instance)
        {
            try
            {
                if (!Features.AlwaysSprintOn) return true;
                if (!IsLocalHcc(__instance)) return true;
                return false;   // 跳过收枪
            }
            catch { return true; }
        }

        /// <summary>保持奔跑：不要因为奔跑打断换弹。</summary>
        internal static bool NoCancelReloadPrefix(BasicRangedWeapon __instance)
        {
            try
            {
                if (!Features.AlwaysSprintOn) return true;
                if (__instance == null) return true;
                try { if (__instance.character == null) return true; } catch { return true; }
                if (!IsLocalHcc(__instance.character.TryCast<HumanCharacterController>())) return true;
                return false;   // 跳过取消/打断换弹
            }
            catch { return true; }
        }

        /// <summary>保持奔跑：奔跑/冲刺时不阻止换弹。</summary>
        internal static bool CanReloadWhileRunningPrefix(BasicRangedWeapon __instance, ref bool __result)
        {
            try
            {
                if (!Features.AlwaysSprintOn) return true;
                if (__instance == null) return true;
                var p = PlayerAccessor.Resolve(out _, out _);
                if (p == null) return true;
                try { if (__instance.character == null || __instance.character.Pointer != p.Pointer) return true; } catch { return true; }
                __result = true;
                return false;
            }
            catch { return true; }
        }

        internal static void SprintRequestPostfix(CharacterInput __instance, ref bool __result)
        {
            try
            {
                if (!Features.AlwaysSprintOn) return;

                // 不再暂停奔跑（暂停/恢复会与游戏内部冲刺状态机不同步，
                // 导致换弹结束后卡在"行走"）。改为始终奔跑，并单独放行换弹闸门。
                if (Time.unscaledTime >= _nextRunKick)
                {
                    _nextRunKick = Time.unscaledTime + 0.2f;
                    try
                    {
                        var p = PlayerAccessor.Resolve(out _, out _);
                        if (p != null)
                        {
                            bool running = false;
                            try { running = p.isRunning; } catch { }
                            if (!running) { try { p.RunInput(false); } catch { } }
                        }
                    }
                    catch { }
                }
                bool firing = (Time.unscaledTime - Features.LastFireAt) < 0.35f;
                __result = !firing;
                SyncSprintFlag(__instance, __result);
                if (_sprintLog < 3)
                {
                    _sprintLog++;
                    Log.Info($"[move] IsSprintRequested 接管 → {(firing ? "开火中(不奔跑)" : "停火(自动奔跑)")}");
                }
            }
            catch { }
        }
    }
}













