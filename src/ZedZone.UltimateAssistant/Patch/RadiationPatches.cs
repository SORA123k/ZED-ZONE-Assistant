using System;
using HarmonyLib;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Patch
{
    /// <summary>
    /// 辐射相关补丁（保险层）：
    /// `RadiationDose.Step(dose, intakePerDay, decayScale, days)` 是游戏计算"新辐射值"的纯函数，
    /// 返回值就是写回 `CharacterStatusData.radiationDose` 的数值。
    /// 开启"保持 0 辐射"时把返回值强制为 0，即使字段写入路径失效也能保证辐射不增长。
    /// </summary>
    internal static class RadiationPatches
    {
        internal static bool StepPatched { get; private set; }
        private static bool _installed;

        internal static int Install(Harmony harmony)
        {
            if (_installed) return 0;
            int n = 0;
            try
            {
                var m = AccessTools.Method(typeof(RadiationDose), "Step",
                    new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
                if (m == null)
                {
                    Log.Warn("[rad] 未找到 RadiationDose.Step(float,float,float,float)");
                }
                else
                {
                    harmony.Patch(m, postfix: new HarmonyMethod(typeof(RadiationPatches), nameof(StepPostfix)));
                    StepPatched = true;
                    n++;
                }
            }
            catch (Exception e) { Log.Warn("[rad] Step 补丁失败: " + e.GetType().Name); }

            _installed = true;
            Log.Info($"[rad] 辐射补丁安装: {n} 个（StepPatched={StepPatched}）");
            return n;
        }

        internal static void Uninstall(Harmony harmony)
        {
            try
            {
                var m = AccessTools.Method(typeof(RadiationDose), "Step",
                    new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
                if (m != null && harmony != null)
                    harmony.Unpatch(m, HarmonyPatchType.All, Plugin.PluginGuid);
            }
            catch { }
            StepPatched = false;
            _installed = false;
        }

        /// <summary>保持 0 辐射：把游戏算出的新剂量压成 0。</summary>
        internal static void StepPostfix(ref float __result)
        {
            try
            {
                if (!Features.RadiationLockOn) return;
                __result = 0f;
            }
            catch { }
        }
    }
}
