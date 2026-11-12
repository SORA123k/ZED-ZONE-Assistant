using System;
using HarmonyLib;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Patch
{
    /// <summary>
    /// 补丁管理器。
    ///
    /// ★ Phase 2 设计决定：**补丁数量必须为 0**。
    ///   项目要求 Phase 2「不安装修改游戏行为的 Harmony Patch」。
    ///   为了把这条约束变成可验证事实，本类在 Phase 2 不注册任何补丁，
    ///   并在安装时打印实际补丁数，供验收核对（日志应出现 patches=0）。
    ///
    /// 继承自上一项目的教训：
    ///   在 IL2CPP 下，"装了补丁但内部 early-return"≠"没装补丁" ——
    ///   Prefix 参数封送发生在补丁体之前，对已销毁对象会抛
    ///   `Handle is not initialized`，try/catch 拦不到。
    ///   所以 Phase 3 一旦启用补丁，必须坚持「功能没开就根本不装补丁」。
    /// </summary>
    internal static class PatchManager
    {
        private static Harmony _harmony;
        private static int _installed;

        internal static int InstalledCount => _installed;

        /// <summary>Phase 2：不安装任何补丁。返回实际安装数（恒为 0）。</summary>
        internal static int InstallAll(string pluginGuid)
        {
            _harmony = new Harmony(pluginGuid);

            int n = 0;
            try
            {
                foreach (var _ in _harmony.GetPatchedMethods()) n++;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("PatchManager.Count", e);
            }
            _installed = n;

            Log.Info($"Phase 2 只读模式：harmony patches installed = {_installed}（预期 0）");

            if (_installed != 0)
                Log.Warn("Phase 2 不应安装任何补丁，但检测到补丁已存在 —— 请检查是否误装了旧的补丁类。");

            return _installed;
        }

        internal static void UninstallAll()
        {
            try
            {
                _harmony?.UnpatchSelf();
                _installed = 0;
                Log.Info("harmony patches removed");
            }
            catch (Exception e)
            {
                Log.ErrorOnce("PatchManager.UninstallAll", e);
            }
        }
    }
}
