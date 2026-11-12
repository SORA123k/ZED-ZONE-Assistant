using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>配置（中文说明，继承上一项目的写法）。</summary>
    internal static class ModConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ShowOnStart;

        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<float> FontScale;
        internal static ConfigEntry<int> PanelX;
        internal static ConfigEntry<int> StartTab;
        internal static ConfigEntry<int> PanelY;
        internal static ConfigEntry<bool> ShowNotes;

        internal static ConfigEntry<int> RefreshIntervalMs;
        internal static ConfigEntry<bool> LogSnapshotOnChange;
        internal static ConfigEntry<int> LogValuesSeconds;
        internal static ConfigEntry<bool> DumpBuffTable;

        // ---- Phase 3A 只读实验探针 ----
        internal static ConfigEntry<bool> GrowthLog;
        internal static ConfigEntry<int> GrowthLogIntervalSec;
        internal static ConfigEntry<bool> FatigueProbe;
        internal static ConfigEntry<int> FatigueProbeIntervalMs;
        internal static ConfigEntry<int> FatigueProbeSeconds;

        // ---- Phase 3B-0：肢体只读枚举 ----
        internal static ConfigEntry<bool> LimbProbe;
        internal static ConfigEntry<int> LimbProbeIntervalMs;
        internal static ConfigEntry<int> LimbProbeHeartbeatSec;
        internal static ConfigEntry<int> LimbProbeChangeFlushSec;

        internal static ConfigEntry<bool> LimbApiProbe;
// ---- Phase 3A: 一键恢复 ----
        internal static ConfigEntry<bool> EnableRestore;
        internal static ConfigEntry<bool> RestoreHealth;
        internal static ConfigEntry<bool> RestoreEnergy;
        internal static ConfigEntry<bool> RestoreHunger;
        internal static ConfigEntry<bool> RestoreThirst;
        internal static ConfigEntry<bool> RestoreFatigue;
        internal static ConfigEntry<string> FatigueDirection;

        internal static ConfigEntry<bool> ModHealth;
        internal static ConfigEntry<bool> ModHunger;
        internal static ConfigEntry<bool> ModThirst;
        internal static ConfigEntry<bool> ModStamina;
        internal static ConfigEntry<bool> ModEnergy;
        internal static ConfigEntry<bool> ModFatigue;
        internal static ConfigEntry<bool> ModStatus;

        internal static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("General", "Enabled", true, "启用模组总开关。");
            ShowOnStart = cfg.Bind("General", "ShowOnStart", true,
                "进入游戏后是否自动显示面板。默认开启便于首次确认模组工作；可按快捷键关闭后保持后台只读。");

            ToggleKey = cfg.Bind("UI", "ToggleKey", new KeyboardShortcut(KeyCode.Insert),
                "打开/关闭面板的快捷键。默认 Insert。若与游戏按键冲突，启动日志会给出警告。");
            FontScale = cfg.Bind("UI", "FontScale", 1.0f,
                new ConfigDescription("界面字号缩放。", new AcceptableValueRange<float>(0.7f, 2.0f)));
            PanelX = cfg.Bind("UI", "PanelX", 380,
                new ConfigDescription("面板左上角 X。默认 380 以避开游戏左上角自带读数与 FPS HUD。",
                    new AcceptableValueRange<int>(0, 4000)));
            PanelY = cfg.Bind("UI", "PanelY", 40,
                new ConfigDescription("面板左上角 Y。默认 40。",
                    new AcceptableValueRange<int>(0, 4000)));
            StartTab = cfg.Bind("UI", "StartTab", 0,
                new ConfigDescription("面板打开时默认显示的标签页：0=玩家 1=生命 2=饱食 3=水分 " +
                                      "4=体力/精力 5=状态 6=肢体 7=设置。默认 0。",
                    new AcceptableValueRange<int>(0, 7)));
            ShowNotes = cfg.Bind("UI", "ShowNotes", true,
                "是否显示每个字段的数据来源说明（排查用，建议开启）。");

            RefreshIntervalMs = cfg.Bind("Read", "RefreshIntervalMs", 100,
                new ConfigDescription(
                    "读取间隔（毫秒）。Phase 2 为只读设计，无需每帧读取；100ms 足够实时且能显著降低 IL2CPP 封送开销。",
                    new AcceptableValueRange<int>(16, 2000)));
            LogSnapshotOnChange = cfg.Bind("Read", "LogOnPlayerChange", true,
                "当玩家引用发生变化（重生/换场景/读档）时写一条日志，用于验证引用未被缓存。");
            LogValuesSeconds = cfg.Bind("Read", "LogValuesSeconds", 10,
                new ConfigDescription(
                    "每隔多少秒把一次真实读数写进日志（0 = 关闭）。" +
                    "用途：验收时可把日志数字与游戏原生状态面板逐项对照，不必依赖截图。",
                    new AcceptableValueRange<int>(0, 600)));

            DumpBuffTable = cfg.Bind("Diagnostics", "DumpBuffTable", false,
                "只读诊断：把游戏自身的 Buff 定义表（id/名称/每秒效果）与 GameConfigs 开关打印到日志。" +
                "仅用于 Phase 3 的 API 验证，为 true 时每次启动转储一次；不修改任何数据。");

            // ---- Phase 3A 只读实验探针（不写任何数值） ----
            GrowthLog = cfg.Bind("Diagnostics", "GrowthLog", true,
                "只读成长探针：定期记录 等级(N/A)/体质成长/HP/三个候选最大生命值/各项加成系数/场景。" +
                "数值变化时输出 CHANGE 行，无变化时输出 heartbeat。用于升级、换装、加点前后对比。");

            GrowthLogIntervalSec = cfg.Bind("Diagnostics", "GrowthLogIntervalSec", 10,
                new ConfigDescription("成长探针心跳间隔（秒）。", new AcceptableValueRange<int>(2, 600)));

            FatigueProbe = cfg.Bind("Diagnostics", "FatigueProbe", true,
                "只读疲劳探针：把 fatigue / energy 与 冲刺请求、移动输入、速度、场景 一起按时间采样，" +
                "用于判定 fatigue 的数值方向。只读，不写任何数值。");

            FatigueProbeIntervalMs = cfg.Bind("Diagnostics", "FatigueProbeIntervalMs", 500,
                new ConfigDescription("疲劳探针采样间隔（毫秒）。", new AcceptableValueRange<int>(100, 5000)));

            FatigueProbeSeconds = cfg.Bind("Diagnostics", "FatigueProbeSeconds", 600,
                new ConfigDescription("疲劳探针最长采样时长（秒），到时会自动停止并输出摘要。",
                    new AcceptableValueRange<int>(30, 7200)));

            // ---- Phase 3B-0：肢体只读枚举（默认关闭） ----
            LimbProbe = cfg.Bind("Diagnostics", "LimbProbe", false,
                "【只读】肢体枚举：打印 humanBodyPartCount、每个肢体的 index / bodyPartId / textId / " +
                "本地化名称 / hp / maxHP / defaultHP / isWounded / isBleeding / isFullyRecovered，" +
                "并在数值变化时输出 CHANGE 行。不写任何数值、不调用恢复接口、不装补丁。");

            LimbProbeIntervalMs = cfg.Bind("Diagnostics", "LimbProbeIntervalMs", 500,
                new ConfigDescription("肢体探针采样间隔（毫秒）。", new AcceptableValueRange<int>(100, 5000)));

            LimbProbeHeartbeatSec = cfg.Bind("Diagnostics", "LimbProbeHeartbeatSec", 30,
                new ConfigDescription("肢体探针心跳间隔（秒）。", new AcceptableValueRange<int>(5, 600)));

            LimbProbeChangeFlushSec = cfg.Bind("Diagnostics", "LimbProbeChangeFlushSec", 1,
                new ConfigDescription("肢体变化日志的合并间隔（秒）。肢体自然恢复会持续变化，" +
                                      "合并输出可避免刷屏。", new AcceptableValueRange<int>(1, 60)));

            LimbApiProbe = cfg.Bind("Diagnostics", "LimbApiProbe", false,
                "【只读】恢复接口参数校验：调用 ReadWoundInput / GetRecoveryBuffId / IsBodyPartWounded " +
                "三个只读接口，打印 bodyPartId 的参数空间与部位→恢复 Buff 映射。" +
                "**不调用任何恢复/写入接口**，不修改任何肢体数据。");

            // ---------------- Phase 3A: 一键恢复 ----------------
            EnableRestore = cfg.Bind("Restore", "EnableRestore", true,
                "【总开关】一键恢复功能。关闭后所有恢复操作都会被拒绝（不会重置你的数值）。");

            RestoreHealth = cfg.Bind("Restore", "Health", true, "允许把生命值恢复到上限。");
            RestoreEnergy = cfg.Bind("Restore", "Energy", true,
                "允许把精力/持久力（energy，两者是同一数值）恢复到上限。");
            RestoreHunger = cfg.Bind("Restore", "Hunger", true, "允许把饱食度恢复到上限。");
            RestoreThirst = cfg.Bind("Restore", "Thirst", true, "允许把水分恢复到上限。");

            RestoreFatigue = cfg.Bind("Restore", "Fatigue", false,
                "允许恢复疲劳值。默认关闭：fatigue 的方向（越高越好 / 越高越糟）尚未确认（G2）。" +
                "确认方向后把下面的 FatigueDirection 改成 resource 或 exhaustion 再打开。");

            FatigueDirection = cfg.Bind("Restore", "FatigueDirection", "unconfirmed",
                new ConfigDescription(
                    "fatigue 的方向。unconfirmed = 未确认（禁止写入）；" +
                    "resource = 疲劳是可消耗资源（越高越好）→ 一键恢复填满；" +
                    "exhaustion = 疲劳是负面积累（越高越糟）→ 一键恢复清零。",
                    new AcceptableValueList<string>("unconfirmed", "resource", "exhaustion")));

            ModHealth = cfg.Bind("Modules", "Health", true, "生命模块（只读）。");
            ModHunger = cfg.Bind("Modules", "Hunger", true, "饱食模块（只读）。");
            ModThirst = cfg.Bind("Modules", "Thirst", true, "水分模块（只读）。");
            ModStamina = cfg.Bind("Modules", "Stamina", true,
                "持久力 STAMINA 模块（只读）。注意：游戏内 STAMINA 与 ENERGY 是同一数值。");
            ModEnergy = cfg.Bind("Modules", "Energy", true,
                "精力 ENERGY 模块（只读）。注意：游戏内与 STAMINA 是同一数值。");
            ModFatigue = cfg.Bind("Modules", "Fatigue", true, "疲劳 FATIGUE 模块（只读）。");
            ModStatus = cfg.Bind("Modules", "Status", true, "状态模块（只读：精神值/死亡标志/Buff 数量）。");
        }

        internal static int RefreshMs => Mathf.Clamp(RefreshIntervalMs?.Value ?? 100, 16, 2000);
        internal static float Scale => Mathf.Clamp(FontScale?.Value ?? 1f, 0.7f, 2f);
        internal static bool Notes => ShowNotes?.Value ?? true;

        internal static void ApplyModuleSwitches(ModuleRegistry reg)
        {
            void Set(string id, ConfigEntry<bool> e)
            {
                var m = reg.Find(id);
                if (m != null && e != null) m.Enabled = e.Value;
            }
            Set("health", ModHealth);
            Set("hunger", ModHunger);
            Set("thirst", ModThirst);
            Set("stamina", ModStamina);
            Set("energy", ModEnergy);
            Set("fatigue", ModFatigue);
            Set("status", ModStatus);
        }
    }
}
