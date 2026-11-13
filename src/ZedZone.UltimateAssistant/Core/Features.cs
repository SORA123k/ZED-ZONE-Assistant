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

        internal static bool InfiniteAmmoOn { get; private set; }
        internal static bool NoReloadOn { get; private set; }
        internal static bool ZombieEspOn { get; private set; }
        internal static bool WeaponDurabilityOn { get; private set; }
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

        internal static void SetWeaponDurability(bool on)
        {
            if (WeaponDurabilityOn == on) return;
            WeaponDurabilityOn = on;
            Log.Info("[features] WeaponDurability = " + on);
            if (on) { try { WeaponUtil.RestoreAllDurability(); } catch { } }
        }

        internal static void SetNoOverheat(bool on)
        {
            if (NoOverheatOn == on) return;
            NoOverheatOn = on;
            Apply();
            Log.Info("[features] NoOverheat = " + on);
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
            bool needPatches = InfiniteAmmoOn || NoReloadOn || NoOverheatOn || JamBypassOn;
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
        }

        internal static void Shutdown()
        {
            try { if (_patchesInstalled) Patch.WeaponPatches.Uninstall(_harmony); } catch { }
            _patchesInstalled = false;
            InfiniteAmmoOn = NoReloadOn = ZombieEspOn = false;
            WeaponDurabilityOn = NoOverheatOn = JamBypassOn = false;
        }

        internal static string StatusText()
            => $"弹药={(InfiniteAmmoOn ? "开" : "关")} 免换弹={(NoReloadOn ? "开" : "关")} ESP={(ZombieEspOn ? "开" : "关")}";
    }
}




