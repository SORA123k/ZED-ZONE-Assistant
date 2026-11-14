using System;
using HarmonyLib;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Patch
{
    /// <summary>
    /// 补丁计数与兜底卸载。
    ///
    /// 真正的安装/卸载由各功能层负责（<see cref="WeaponPatches"/> /
    /// <see cref="PlayerPatches"/> / <see cref="PlayerMovePatches"/>），
    /// 且遵循同一条铁律：**功能没开就根本不装补丁**。
    ///
    /// 教训（来自旧项目）：IL2CPP 下"装了补丁但内部 early-return"≠"没装补丁"——
    /// Prefix 参数封送发生在补丁体之前，对已销毁对象会抛
    /// `Handle is not initialized`，try/catch 拦不到。
    ///
    /// 本类只在启动时报告统计，并在插件卸载时用同一个 Harmony Id 做最后一次
    /// 全量卸载（含功能层用同 Id 装的补丁），避免热重载残留。
    /// </summary>
    internal static class PatchManager
    {
        private static Harmony _harmony;

        internal static int InstalledCount { get; private set; }

        internal static int InstallAll(string pluginGuid)
        {
            _harmony = new Harmony(pluginGuid);
            InstalledCount = Count(_harmony);
            Log.Info($"[patches] 启动时已挂载补丁数 = {InstalledCount}（功能开启后按需增加）");
            return InstalledCount;
        }

        private static int Count(Harmony h)
        {
            int n = 0;
            try
            {
                if (h != null) foreach (var _ in h.GetPatchedMethods()) n++;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("PatchManager.Count", e);
            }
            return n;
        }

        internal static void UninstallAll()
        {
            try
            {
                // 同一个 Harmony Id 只应该有一个实例；这里 UnpatchSelf 会把功能层
                // 用相同 Id 装的补丁一并卸掉，作为最外层兜底。
                _harmony?.UnpatchSelf();
                InstalledCount = 0;
                Log.Info("[patches] 已卸载本插件的全部 Harmony 补丁");
            }
            catch (Exception e)
            {
                Log.ErrorOnce("PatchManager.UninstallAll", e);
            }
        }
    }
}
