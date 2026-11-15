using System;
using HarmonyLib;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 最终版功能开关的运行时状态与补丁生命周期。
    ///
    /// 设计要点：
    ///  * 每个功能独立开关；
    ///  * **只在开启时装补丁**，关闭立刻卸载（IL2CPP 下"装了补丁+early return"≠"没装补丁"）；
    ///  * **所有修改类功能**（弹药、耐久、过热、卡壳、生命/体力/饱食/水分、材料、移动）
    ///    在联机状态下 fail-closed 拒绝启用，并在联机检测生效时主动关闭；
    ///  * ESP / 全亮为纯视觉，其中 ESP 同样在联机时禁用（项目规则）。
    ///
    /// 门禁的唯一入口是 <see cref="GateOnline"/>：菜单、快捷键、配置文件三条路径
    /// 最终都会经过 Set* 方法，因此只要在这里拦一次即可覆盖全部入口。
    /// </summary>
    internal static class Features
    {
        private static Harmony _harmony;
        private static bool _patchesInstalled;
        private static bool _playerPatchesInstalled;
        private static bool _movePatchesInstalled;
        private static bool _radPatchesInstalled;

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
        internal static bool AutoSupplyOn { get; private set; }
        internal static bool RadiationLockOn { get; private set; }
        internal static bool LeftClickerOn { get; private set; }
        internal static float LastFireAt { get; set; }
        internal static bool NoOverheatOn { get; private set; }
        internal static bool JamBypassOn { get; private set; }

        internal static string LastError { get; private set; }
        internal static int PatchCount => _patchesInstalled ? Patch.WeaponPatches.Installed : 0;

        /// <summary>
        /// 联机门禁（fail-closed）。
        /// 返回 true 表示"这次启用请求被拒绝"，调用方应把 on 改回 false。
        ///
        /// 刻意**不改写配置文件**：检测本身有极小概率误判（异常按联机处理），
        /// 自动写配置会把用户的偏好永久清掉。这里只关运行时开关，
        /// 回到单机后 SyncFromConfig 会按配置自动恢复。
        /// </summary>
        private static bool GateOnline(string feature, bool want)
        {
            if (!want) return false;
            if (!PlayerWriteGate.IsMultiplayer(out string why)) return false;

            Log.WarnOnce("gate.online." + feature,
                "[features] " + feature + " 已拒绝启用：检测到联机状态（" + why + "），fail-closed。" +
                "（你的配置文件不会被改写，回到单机后自动恢复）");
            return true;
        }

        /// <summary>联机时把已开启的修改类功能强制关掉（进会话前没检测到、进会话后检测到的情况）。</summary>
        private static float _nextEnforceAt;
        internal static void EnforceOffline()
        {
            if (Time.unscaledTime < _nextEnforceAt) return;
            _nextEnforceAt = Time.unscaledTime + 1.0f;

            if (!PlayerWriteGate.IsMultiplayer(out string why)) return;

            int closed = 0;
            if (InfiniteAmmoOn) { SetInfiniteAmmo(false); closed++; }
            if (NoReloadOn) { SetNoReload(false); closed++; }
            if (NoJamOn) { SetNoJam(false); closed++; }
            if (JamBypassOn) { SetJamBypass(false); closed++; }
            if (NoOverheatOn) { SetNoOverheat(false); closed++; }
            if (WeaponDurabilityOn) { SetWeaponDurability(false); closed++; }
            if (ItemDurabilityLockOn) { SetItemDurabilityLock(false); closed++; }
            if (NoHpLossOn) { SetNoHpLoss(false); closed++; }
            if (NoStaminaLossOn) { SetNoStaminaLoss(false); closed++; }
            if (NoHungerLossOn) { SetNoHungerLoss(false); closed++; }
            if (NoThirstLossOn) { SetNoThirstLoss(false); closed++; }
            if (AutoSupplyOn) { SetAutoSupply(false); closed++; }
            if (AlwaysSprintOn) { SetAlwaysSprint(false); closed++; }
            if (MoveSpeedOn) { SetMoveSpeed(false); closed++; }
            if (ZombieEspOn) { SetZombieEsp(false); closed++; }

            if (closed > 0)
                Log.WarnOnce("gate.enforce", "[features] 联机状态（" + why + "）：已强制关闭 " + closed + " 个功能。");
        }

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
            bool wantSupply = ModConfig.AutoSupply?.Value ?? false;
            bool wantRad = ModConfig.RadiationLock?.Value ?? false;
            bool wantClicker = ModConfig.LeftClicker?.Value ?? false;
            bool wantAmmo = ModConfig.InfiniteAmmo?.Value ?? false;
            bool wantReload = ModConfig.NoReload?.Value ?? false;
            bool wantEsp = ModConfig.ZombieEsp?.Value ?? false;

            // 联机预检：一次判定，避免每个 Set* 各刷一条日志（细节拦截仍在 GateOnline 里）
            if (PlayerWriteGate.IsMultiplayer(out string why))
            {
                if (wantAmmo || wantReload || wantDur || wantItemDur || wantHeat || wantJam ||
                    wantNoJam || wantNoHpLoss || wantNoSta || wantNoHunger || wantNoThirst ||
                    wantSupply || wantSprint || wantSpeed || wantEsp)
                {
                    Log.WarnOnce("feat.online", "[features] 联机状态（" + why + "），修改类功能与 ESP 已禁用。");
                    wantAmmo = wantReload = wantDur = wantItemDur = wantHeat = wantJam = false;
                    wantNoJam = wantNoHpLoss = wantNoSta = wantNoHunger = wantNoThirst = false;
                    wantSupply = wantSprint = wantSpeed = wantEsp = false;
                }
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
            SetAutoSupply(wantSupply);
            SetRadiationLock(wantRad);
            SetLeftClicker(wantClicker);
            SetZombieEsp(wantEsp);
        }

        internal static void SetInfiniteAmmo(bool on)
        {
            if (GateOnline("InfiniteAmmo", on)) on = false;
            if (InfiniteAmmoOn == on) return;
            InfiniteAmmoOn = on;
            Apply();
            Log.Info("[features] InfiniteAmmo = " + on);
        }

        internal static void SetNoReload(bool on)
        {
            if (GateOnline("NoReload", on)) on = false;
            if (NoReloadOn == on) return;
            NoReloadOn = on;
            Apply();
            Log.Info("[features] NoReload = " + on);
        }

        /// <summary>
        /// 枪械耐久锁定（保守实现，Phase 8）。
        /// 依据：组件持有的 weaponData.durability 是唯一可写且可观测的耐久字段；
        /// 采用"只增不减"策略，只在检测到下降时写回历史最高值。
        /// </summary>
        internal static void SetWeaponDurability(bool on)
        {
            if (GateOnline("WeaponDurability", on)) on = false;
            if (WeaponDurabilityOn == on) return;
            WeaponDurabilityOn = on;
            if (!on) DurabilityLock.Reset();
            Apply();
            Log.Info("[features] WeaponDurability(gun lock) = " + on);
        }

        internal static void SetNoOverheat(bool on)
        {
            if (GateOnline("NoOverheat", on)) on = false;
            if (NoOverheatOn == on) return;
            NoOverheatOn = on;
            Apply();
            Log.Info("[features] NoOverheat = " + on);
        }

        internal static void SetAlwaysSprint(bool on)
        {
            if (GateOnline("AlwaysSprint", on)) on = false;
            if (AlwaysSprintOn == on) return;
            AlwaysSprintOn = on;
            Apply();
            Log.Info("[features] AlwaysSprint = " + on);
        }

        internal static void SetMoveSpeed(bool on)
        {
            if (GateOnline("MoveSpeed", on)) on = false;
            if (MoveSpeedOn == on) return;
            MoveSpeedOn = on;
            Apply();
            Log.Info("[features] MoveSpeed = " + on);
        }

        internal static void SetAutoSupply(bool on)
        {
            if (GateOnline("AutoSupply", on)) on = false;
            if (AutoSupplyOn == on) return;
            AutoSupplyOn = on;
            Apply();
            Log.Info("[features] AutoSupply = " + on);
        }

        internal static void SetRadiationLock(bool on)
        {
            if (RadiationLockOn == on) return;
            RadiationLockOn = on;
            Apply();
            Log.Info("[features] RadiationLock = " + on);
        }

        internal static void SetLeftClicker(bool on)
        {
            if (LeftClickerOn == on) return;
            LeftClickerOn = on;
            try
            {
                if (on) AutoClicker.Start();
                else AutoClicker.Stop();
            }
            catch (Exception e) { Log.ErrorOnce("features.clicker", e); }
            Log.Info("[features] LeftClicker = " + on);
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
            if (GateOnline("NoHungerLoss", on)) on = false;
            if (NoHungerLossOn == on) return;
            NoHungerLossOn = on;
            Apply();
            Log.Info("[features] NoHungerLoss = " + on);
        }

        internal static void SetNoThirstLoss(bool on)
        {
            if (GateOnline("NoThirstLoss", on)) on = false;
            if (NoThirstLossOn == on) return;
            NoThirstLossOn = on;
            Apply();
            Log.Info("[features] NoThirstLoss = " + on);
        }

        internal static void SetNoJam(bool on)
        {
            if (GateOnline("NoJam", on)) on = false;
            if (NoJamOn == on) return;
            NoJamOn = on;
            Apply();
            Log.Info("[features] NoJam = " + on);
        }

        internal static void SetNoHpLoss(bool on)
        {
            if (GateOnline("NoHpLoss", on)) on = false;
            if (NoHpLossOn == on) return;
            NoHpLossOn = on;
            Apply();
            Log.Info("[features] NoHpLoss = " + on);
        }

        internal static void SetNoStaminaLoss(bool on)
        {
            if (GateOnline("NoStaminaLoss", on)) on = false;
            if (NoStaminaLossOn == on) return;
            NoStaminaLossOn = on;
            Apply();
            Log.Info("[features] NoStaminaLoss = " + on);
        }

        internal static void SetItemDurabilityLock(bool on)
        {
            if (GateOnline("ItemDurabilityLock", on)) on = false;
            if (ItemDurabilityLockOn == on) return;
            ItemDurabilityLockOn = on;
            if (!on) DurabilityLock.Reset();
            Apply();
            Log.Info("[features] ItemDurabilityLock = " + on);
        }

        internal static void SetJamBypass(bool on)
        {
            if (GateOnline("JamBypass", on)) on = false;
            if (JamBypassOn == on) return;
            JamBypassOn = on;
            Apply();
            Log.Info("[features] JamBypass = " + on);
        }

        internal static void SetZombieEsp(bool on)
        {
            if (GateOnline("ZombieEsp", on)) on = false;
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
                bool needPlayer = NoHpLossOn || NoStaminaLossOn || NoHungerLossOn || NoThirstLossOn || AutoSupplyOn;
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

            // 移动类补丁（强制奔跑 / 移动加速）
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

            // 辐射补丁（仅"保持 0 辐射"开启时安装）
            try
            {
                if (RadiationLockOn && !_radPatchesInstalled)
                {
                    if (_harmony == null) _harmony = new Harmony(Plugin.PluginGuid);
                    Patch.RadiationPatches.Install(_harmony);
                    _radPatchesInstalled = true;
                }
                else if (!RadiationLockOn && _radPatchesInstalled)
                {
                    Patch.RadiationPatches.Uninstall(_harmony);
                    _radPatchesInstalled = false;
                }
            }
            catch (Exception e) { Log.ErrorOnce("features.apply.rad", e); }
        }

        internal static void Shutdown()
        {
            try { if (_patchesInstalled) Patch.WeaponPatches.Uninstall(_harmony); } catch { }
            try { if (_playerPatchesInstalled) Patch.PlayerPatches.Uninstall(_harmony); } catch { }
            try { if (_movePatchesInstalled) Patch.PlayerMovePatches.Uninstall(_harmony); } catch { }
            try { if (_radPatchesInstalled) Patch.RadiationPatches.Uninstall(_harmony); } catch { }
            try { FullBright.Restore(); } catch { }
            try { ZombieEsp.Clear(); } catch { }
            try { DurabilityLock.Reset(); } catch { }
            _patchesInstalled = false;
            _playerPatchesInstalled = false;
            _movePatchesInstalled = false;
            InfiniteAmmoOn = NoReloadOn = ZombieEspOn = false;
            WeaponDurabilityOn = NoOverheatOn = JamBypassOn = ItemDurabilityLockOn = false;
            NoHpLossOn = NoStaminaLossOn = NoHungerLossOn = NoThirstLossOn = NoJamOn = false;
            AlwaysSprintOn = MoveSpeedOn = FullBrightOn = PerformanceModeOn = AutoSupplyOn = RadiationLockOn = LeftClickerOn = false;
        }

        internal static string StatusText()
            => $"弹药={(InfiniteAmmoOn ? "开" : "关")} 免换弹={(NoReloadOn ? "开" : "关")} ESP={(ZombieEspOn ? "开" : "关")}";
    }
}





