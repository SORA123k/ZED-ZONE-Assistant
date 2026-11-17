using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;
using ZedZone.UltimateAssistant.Modules;
using ZedZone.UltimateAssistant.Patch;
using ZedZone.UltimateAssistant.UI;

// 注意：BasePlugin 自带一个 `Log` 属性（ManualLogSource），
// 与我们的静态日志类 Core.Log 同名。这里用别名避免二义性。
using CoreLog = ZedZone.UltimateAssistant.Core.Log;

namespace ZedZone.UltimateAssistant
{
    /// <summary>
    /// ZED ZONE Ultimate Assistant —— Phase 2：插件骨架 + 玩家状态只读读取 + GUI 框架。
    ///
    /// ★ Phase 2 的硬约束（已写进代码结构）：
    ///   1. **只读**：没有任何写玩家属性的代码路径（模块基类只有 Read）；
    ///   2. **不装修改行为的补丁**：PatchManager 安装数必须为 0；
    ///   3. **不缓存玩家引用**：每次读取都经 PlayerAccessor.Resolve 重新解析；
    ///   4. **不伪造数据**：读不到就标记失败并在 GUI 上红色显示。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BasePlugin
    {
        public const string PluginGuid = "zedzone.ultimateassistant";
        public const string PluginName = "ZED ZONE Ultimate Assistant";
        public const string PluginVersion = "1.2.2";

        internal static Plugin Instance;
        internal static ModuleRegistry Registry;
        internal static PlayerStateReader Reader;

        private AssistantPanel _panel;

        public override void Load()
        {
            Instance = this;
            CoreLog.Init(Log);   // Log 这里是 BasePlugin.Log（ManualLogSource）

            CoreLog.Info($"{PluginName} v{PluginVersion} loading");

            string gameVer = "unknown";
            try { gameVer = Application.version; } catch { }
            CoreLog.Info($"detected game version: {gameVer}");
            CheckGameVersion(gameVer);

            // ---- 配置 ----
            ModConfig.Bind(Config);
            ModConfig.BindFeatures(Config);

            if (ModConfig.Enabled != null && !ModConfig.Enabled.Value)
            {
                CoreLog.Info("已由配置禁用（General/Enabled=false），不加载任何模块。");
                return;
            }

            // ---- 模块注册（只读） ----
            Registry = new ModuleRegistry();
            Registry.Add(new HealthModule());
            Registry.Add(new HungerModule());
            Registry.Add(new ThirstModule());
            Registry.Add(new StaminaModule());
            Registry.Add(new EnergyModule());
            Registry.Add(new FatigueModule());
            Registry.Add(new StatusModule());
            ModConfig.ApplyModuleSwitches(Registry);

            CoreLog.Info("已注册模块: " + string.Join(", ", Map(Registry)));

            Reader = new PlayerStateReader(Registry);

            // ---- 只读耐久诊断 ----
            try { UiSound.Init(); } catch (Exception e) { CoreLog.Warn("音效初始化失败: " + e.GetType().Name); }
            try { HotkeyManager.Init(Config); } catch (Exception e) { CoreLog.Warn("热键初始化失败: " + e.GetType().Name); }
            try { DurabilityDiag.Init(); } catch (Exception e) { CoreLog.Warn("耐久诊断初始化失败: " + e.GetType().Name); }
            try { Il2CppMetaDiag.Init(); } catch (Exception e) { CoreLog.Warn("元数据探查初始化失败: " + e.GetType().Name); }

            // ---- 补丁（初始为 0，功能开启后按需安装） ----
            int patches = PatchManager.InstallAll(PluginGuid);

            // ---- GUI ----
            try
            {
                _panel = AddComponent<AssistantPanel>();
                _panel.Reader = Reader;
                _panel.Registry = Registry;
                CoreLog.Info("GUI 组件已注入（快捷键默认 Insert，可在配置中修改）");
            }
            catch (Exception e)
            {
                CoreLog.Warn("GUI 注入失败：" + e.GetType().Name + ": " + e.Message);
            }

            // ---- 自查 ----
            CoreLog.Info("=== 自查 v" + PluginVersion + " ===");
            CoreLog.Info("  阶段              : 最终版（ClickGUI + 状态恢复 + 弹药/ESP）");
            CoreLog.Info("  写入路径          : 唯一入口 PlayerWriteGate（只写属性 setter）");
            CoreLog.Info("  只读功能          : 保留（7 个读取模块）");
            CoreLog.Info($"  Harmony 补丁数    : {patches}（特性开启后按需安装，关闭即卸载）");
            CoreLog.Info("  玩家引用缓存      : 否（每次 Resolve 重新解析）");
            CoreLog.Info($"  联机门禁          : 已移除（不做自动检测/强制关闭）");
            CoreLog.Info($"  写入门总开关      : {(ModConfig.EnableRestore?.Value ?? false ? "开" : "关")}");
            CoreLog.Info($"  恢复项开关        : HP={(ModConfig.RestoreHealth?.Value ?? true)} " +
                          $"Energy={(ModConfig.RestoreEnergy?.Value ?? true)} " +
                          $"Hunger={(ModConfig.RestoreHunger?.Value ?? true)} " +
                          $"Thirst={(ModConfig.RestoreThirst?.Value ?? true)} " +
                          $"Fatigue={(ModConfig.RestoreFatigue?.Value ?? false)}" +
                          $"（方向={ModConfig.FatigueDirection?.Value ?? "unconfirmed"}）");
            CoreLog.Info($"  读取间隔          : {ModConfig.RefreshMs} ms");
            CoreLog.Info($"  GUI 快捷键        : {SafeKeyName()}");
            CoreLog.Info("====================");
        }

        /// <summary>本项目适配并实机验证过的游戏版本（见 docs/COMPATIBILITY.md）。</summary>
        internal const string TestedGameVersion = "1.0.19";

        /// <summary>
        /// 游戏版本兼容性提示（只提示，不阻断）。
        /// 原因：interop 方法签名在游戏大版本更新后可能变化，补丁会失效或行为异常，
        /// 但对版本"硬拦截"会让用户在补丁仍可用时也用不了，故这里只给出明确告警。
        /// </summary>
        private static void CheckGameVersion(string gameVer)
        {
            if (string.IsNullOrEmpty(gameVer) || gameVer == "unknown") return;
            if (gameVer == TestedGameVersion) return;

            CoreLog.Warn("==============================================================");
            CoreLog.Warn($" 游戏版本不匹配：本模组适配 {TestedGameVersion}，当前 {gameVer}");
            CoreLog.Warn(" 可能出现：功能无效 / 开关无效 / 报错。请先看 docs/COMPATIBILITY.md");
            CoreLog.Warn(" （记录适配步骤），并到仓库提交 Issue 附 BepInEx/LogOutput.log");
            CoreLog.Warn("==============================================================");
        }

        private static string SafeKeyName()
        {
            try { return ModConfig.ToggleKey.Value.MainKey.ToString(); }
            catch { return "Insert"; }
        }

        private static System.Collections.Generic.List<string> Map(ModuleRegistry reg)
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var m in reg.All)
                if (m != null) list.Add(m.Id);
            return list;
        }

        public override bool Unload()
        {
            // 顺序：先让功能层卸载自己装的补丁 / 还原运行时改动，再清 PatchManager。
            try { Features.Shutdown(); } catch { }
            try { PatchManager.UninstallAll(); } catch { }
            CoreLog.Info("unloaded");
            return true;
        }
    }
}


























































