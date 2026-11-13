using System;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 最终版功能开关的运行时状态与补丁生命周期。
    ///
    /// 设计要点：
    ///  * 三个功能各自独立开关；
    ///  * **只在开启时装补丁**，关闭立刻卸载（IL2CPP 下"装了补丁+early return"≠"没装补丁"）；
    ///  * 状态修改类功能（弹药）在联机时一律拒绝启用，fail-closed；
    ///  * ESP 为纯只读绘制，联机时也禁用（项目规则）。
    /// </summary>
    internal static class Features
    {
        private static Harmony _harmony;
        private static bool _patchesInstalled;
        private static bool _playerPatchesInstalled;
        private static bool _movePatchesInstalled;

        internal static bool InfiniteAmmoOn { get; private set; }
        internal static bool NoReloadOn { get; private set; }
        internal static bool ZombieEspOn { get; private set; }
        internal static bool WeaponDurabilityOn { get; private set; }
        internal static bool ItemDurabilityLockOn { get; private set; }
        internal static bool NoHpLossOn { get; private set; }
        internal static bool NoStaminaLossOn { get; private set; }
        internal static bool NoHungerLossOn { get; private set; }
        internal static bool NoThirstLossOn { get; private set; }
        internal static bool NoJamOn { get; private set; }
        internal static bool AlwaysSprintOn { get; private set; }
        internal static bool MoveSpeedOn { get; private set; }
        internal static bool FullBrightOn { get; private set; }
        internal static bool PerformanceModeOn { get; private set; }
        internal static float LastFireAt { get; set; }
        internal static bool NoOverheatOn { get; private set; }
        internal static bool JamBypassOn { get; private set; }

        internal static string LastError { get; private set; }
        internal static int PatchCount => _patchesInstalled ? Patch.WeaponPatches.Installed : 0;

        /// <summary>从配置同步一次（可在菜单里调用）。</summary>
        internal static void SyncFromConfig()
        {
            bool wantDur = ModConfig.WeaponDurability?.Value ?? false;
            bool wantHeat = ModConfig.NoOverheat?.Value ?? false;
            bool wantJam = ModConfig.JamBypass?.Value ?? false;
            bool wantItemDur = ModConfig.ItemDurabilityLock?.Value ?? false;
            bool wantNoHpLoss = ModConfig.NoHpLoss?.Value ?? false;
            bool wantNoSta = ModConfig.NoStaminaLoss?.Value ?? false;
            bool wantNoHunger = ModConfig.NoHungerLoss?.Value ?? false;
            bool wantNoThirst = ModConfig.NoThirstLoss?.Value ?? false;
            bool wantNoJam = ModConfig.NoJam?.Value ?? false;
            bool wantSprint = ModConfig.AlwaysSprint?.Value ?? false;
            bool wantSpeed = ModConfig.MoveSpeed?.Value ?? false;
            bool wantFullBright = ModConfig.FullBright?.Value ?? false;
            bool wantPerf = ModConfig.PerformanceMode?.Value ?? false;
            bool wantAmmo = ModConfig.InfiniteAmmo?.Value ?? false;
            bool wantReload = ModConfig.NoReload?.Value ?? false;
            bool wantEsp = ModConfig.ZombieEsp?.Value ?? false;

            // 联机一律拒绝状态修改类功能（fail-closed）
            if ((wantAmmo || wantReload) && PlayerWriteGate.IsMultiplayer(out string why))
            {
                if (wantAmmo || wantReload)
                {
                    Log.WarnOnce("feat.online", "[features] 联机状态（" + why + "），弹药类功能已禁用。");
                    wantAmmo = false;
                    wantReload = false;
                }
            }

            if (wantEsp && PlayerWriteGate.IsMultiplayer(out _))
            {
                Log.WarnOnce("feat.esp.online", "[features] 联机状态，ESP 已禁用。");
                wantEsp = false;
            }

            SetInfiniteAmmo(wantAmmo);
            SetNoReload(wantReload);
            SetWeaponDurability(wantDur);
            SetNoOverheat(wantHeat);
            SetJamBypass(wantJam);
            SetItemDurabilityLock(wantItemDur);
            SetNoHpLoss(wantNoHpLoss);
            SetNoStaminaLoss(wantNoSta);
            SetNoHungerLoss(wantNoHunger);
            SetNoThirstLoss(wantNoThirst);
            SetNoJam(wantNoJam);
            SetAlwaysSprint(wantSprint);
            SetMoveSpeed(wantSpeed);
            SetFullBright(wantFullBright);
            SetPerformanceMode(wantPerf);
            ZombieEspOn = wantEsp;
        }

        internal static void SetInfiniteAmmo(bool on)
        {
            if (InfiniteAmmoOn == on) return;
            InfiniteAmmoOn = on;
            Apply();
            Log.Info("[features] InfiniteAmmo = " + on);
        }

        internal static void SetNoReload(bool on)
        {
            if (NoReloadOn == on) return;
            NoReloadOn = on;
            Apply();
            Log.Info("[features] NoReload = " + on);
        }

        /// <summary>
        /// 耐久锁定：**已暂停**（v1.1.0）。
        /// 多次实机测试仍无法确定真实耐久字段与扣减调用链，
        /// 按用户要求先做只读诊断，此方法不再启用任何写操作。
        /// </summary>
        /// <summary>
        /// 枪械耐久锁定（保守实现，Phase 8）。
        /// 依据：组件持有的 weaponData.durability 是唯一可写且可观测的耐久字段；
        /// 采用"只增不减"策略，只在检测到下降时写回历史最高值。
        /// </summary>
        internal static void SetWeaponDurability(bool on)
        {
            if (WeaponDurabilityOn == on) return;
            WeaponDurabilityOn = on;
            if (!on) DurabilityLock.Reset();
            Log.Info("[features] WeaponDurability(gun lock) = " + on);
        }

        internal static void SetNoOverheat(bool on)
        {
            if (NoOverheatOn == on) return;
            NoOverheatOn = on;
            Apply();
            Log.Info("[features] NoOverheat = " + on);
        }

        internal static void SetAlwaysSprint(bool on)
        {
            if (AlwaysSprintOn == on) return;
            AlwaysSprintOn = on;
            Apply();
            Log.Info("[features] AlwaysSprint = " + on);
        }

        internal static void SetMoveSpeed(bool on)
        {
            if (MoveSpeedOn == on) return;
            MoveSpeedOn = on;
            Apply();
            Log.Info("[features] MoveSpeed = " + on);
        }

        internal static void SetPerformanceMode(bool on)
        {
            if (PerformanceModeOn == on) return;
            PerformanceModeOn = on;
            Log.Info("[features] PerformanceMode = " + on);
        }

        internal static void SetFullBright(bool on)
        {
            if (FullBrightOn == on) return;
            FullBrightOn = on;
            if (!on) FullBright.Restore();
            Log.Info("[features] FullBright = " + on);
        }

        internal static void SetNoHungerLoss(bool on)
        {
            if (NoHungerLossOn == on) return;
            NoHungerLossOn = on;
            Apply();
            Log.Info("[features] NoHungerLoss = " + on);
        }

        internal static void SetNoThirstLoss(bool on)
        {
            if (NoThirstLossOn == on) return;
            NoThirstLossOn = on;
            Apply();
            Log.Info("[features] NoThirstLoss = " + on);
        }

        internal static void SetNoJam(bool on)
        {
            if (NoJamOn == on) return;
            NoJamOn = on;
            Apply();
            Log.Info("[features] NoJam = " + on);
        }

        internal static void SetNoHpLoss(bool on)
        {
            if (NoHpLossOn == on) return;
            NoHpLossOn = on;
            Apply();
            Log.Info("[features] NoHpLoss = " + on);
        }

        internal static void SetNoStaminaLoss(bool on)
        {
            if (NoStaminaLossOn == on) return;
            NoStaminaLossOn = on;
            Apply();
            Log.Info("[features] NoStaminaLoss = " + on);
        }

        internal static void SetItemDurabilityLock(bool on)
        {
            if (ItemDurabilityLockOn == on) return;
            ItemDurabilityLockOn = on;
            if (!on) DurabilityLock.Reset();
            Log.Info("[features] ItemDurabilityLock = " + on);
        }

        internal static void SetJamBypass(bool on)
        {
            if (JamBypassOn == on) return;
            JamBypassOn = on;
            Apply();
            Log.Info("[features] JamBypass = " + on);
        }

        internal static void SetZombieEsp(bool on)
        {
            if (ZombieEspOn == on) return;
            ZombieEspOn = on;
            if (!on) ZombieEsp.Clear();
            Log.Info("[features] ZombieEsp = " + on);
        }

        /// <summary>按当前开关状态安装/卸载补丁。任何异常只记录，不影响游戏。</summary>
        private static void Apply()
        {
            bool needPatches = InfiniteAmmoOn || NoReloadOn || NoOverheatOn || JamBypassOn || WeaponDurabilityOn || NoJamOn;
            try
            {
                if (needPatches && !_patchesInstalled)
                {
                    if (_harmony == null) _harmony = new Harmony(Plugin.PluginGuid);
                    Patch.WeaponPatches.Install(_harmony);
                    _patchesInstalled = true;
                    Log.Info($"[features] 武器补丁已安装（patches={Patch.WeaponPatches.Installed}）");
                }
                else if (!needPatches && _patchesInstalled)
                {
                    Patch.WeaponPatches.Uninstall(_harmony);
                    _patchesInstalled = false;
                    Log.Info("[features] 武器补丁已卸载");
                }
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                Log.ErrorOnce("features.apply", e);
            }

            // 玩家防扣除补丁（独立生命周期）
            try
            {
                bool needPlayer = NoHpLossOn || NoStaminaLossOn || NoHungerLossOn || NoThirstLossOn;
                if (needPlayer && !_playerPatchesInstalled)
                {
                    if (_harmony == null) _harmony = new Harmony(Plugin.PluginGuid);
                    Patch.PlayerPatches.Install(_harmony);
                    _playerPatchesInstalled = true;
                }
                else if (!needPlayer && _playerPatchesInstalled)
                {
                    Patch.PlayerPatches.Uninstall(_harmony);
                    _playerPatchesInstalled = false;
                }
            }
            catch (Exception e) { Log.ErrorOnce("features.apply.player", e); }

            // 移动类补丁（强制奔跑 / 移动加速 / Timer）
            try
            {
                bool needMove = AlwaysSprintOn || MoveSpeedOn;
                if (needMove && !_movePatchesInstalled)
                {
                    if (_harmony == null) _harmony = new Harmony(Plugin.PluginGuid);
                    Patch.PlayerMovePatches.Install(_harmony);
                    _movePatchesInstalled = true;
                }
                else if (!needMove && _movePatchesInstalled)
                {
                    Patch.PlayerMovePatches.Uninstall(_harmony);
                    _movePatchesInstalled = false;
                }
            }
            catch (Exception e) { Log.ErrorOnce("features.apply.move", e); }
        }

        internal static void Shutdown()
        {
            try { if (_patchesInstalled) Patch.WeaponPatches.Uninstall(_harmony); } catch { }
            try { if (_playerPatchesInstalled) Patch.PlayerPatches.Uninstall(_harmony); } catch { }
            try { if (_movePatchesInstalled) Patch.PlayerMovePatches.Uninstall(_harmony); } catch { }
            _patchesInstalled = false;
            InfiniteAmmoOn = NoReloadOn = ZombieEspOn = false;
            WeaponDurabilityOn = NoOverheatOn = JamBypassOn = ItemDurabilityLockOn = false;
            NoHpLossOn = NoStaminaLossOn = NoHungerLossOn = NoThirstLossOn = NoJamOn = false;
            AlwaysSprintOn = MoveSpeedOn = FullBrightOn = PerformanceModeOn = false;
        }

        internal static string StatusText()
            => $"弹药={(InfiniteAmmoOn ? "开" : "关")} 免换弹={(NoReloadOn ? "开" : "关")} ESP={(ZombieEspOn ? "开" : "关")}";
    }
}















