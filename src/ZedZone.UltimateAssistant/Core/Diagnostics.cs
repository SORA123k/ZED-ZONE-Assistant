using System;
using System.Text;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 只读诊断：把游戏自己的 Buff 定义表与全局开关**打印到日志**，用于 Phase 3 的 API 验证。
    ///
    /// 严格只读：
    ///   * 只调用 `BuffManager.instance` / `GetBuffAttrByID(int)` 与读取 `BuffAttr` 字段；
    ///   * 只读 `GameConfigs` 的静态字段，**不写**；
    ///   * 不碰玩家属性、不碰存档、不装补丁。
    ///
    /// 默认关闭（`[Diagnostics] DumpBuffTable = false`）。
    /// 打开后每次启动最多尝试 N 次（BuffManager 可能要到主菜单初始化后才可用）。
    /// </summary>
    internal static class Diagnostics
    {
        private static bool _done;
        private static int _attempts;
        private const int MaxAttempts = 60;

        internal static void Tick()
        {
            if (_done) return;
            if (!(ModConfig.DumpBuffTable?.Value ?? false)) { _done = true; return; }
            if (_attempts++ > MaxAttempts) { _done = true; return; }

            bool haveManager = false;
            try { haveManager = BuffManager.instance != null; }
            catch (Exception e) { Log.WarnOnce("diag.bm", "BuffManager.instance 读取异常: " + e.GetType().Name); }

            if (!haveManager) return;   // 还没初始化，等下一次

            _done = true;
            DumpBuffTable();
            DumpGlobalSwitches();
            DumpPlayerPresence();
        }

        private static void DumpBuffTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[diag] === BuffManager buff table (只读转储) ===");
            sb.AppendLine("[diag] id | name | debuff | lethal | hp/s | hunger/s | thirst/s | energy/s | speedGain | temp/s | armorGain");

            int found = 0;
            for (int id = 1; id <= 64; id++)
            {
                try
                {
                    var a = BuffManager.instance.GetBuffAttrByID(id);
                    if (a == null) continue;
                    found++;
                    sb.Append("[diag] ").Append(id).Append(" | ")
                      .Append(a.buffName).Append(" | ")
                      .Append(a.isDebuff ? "D" : "-").Append(" | ")
                      .Append(a.causeDeathFlag ? "D" : "-").Append(" | ")
                      .Append(a.hpPerSecond.ToString("0.###")).Append(" | ")
                      .Append(a.hungerPerSecond.ToString("0.###")).Append(" | ")
                      .Append(a.thirstPerSecond.ToString("0.###")).Append(" | ")
                      .Append(a.energyPerSecond.ToString("0.###")).Append(" | ")
                      .Append(a.speedGain.ToString("0.###")).Append(" | ")
                      .Append(a.bodyTemperaturePerSecond.ToString("0.###")).Append(" | ")
                      .Append(a.armorPointsGain.ToString("0.###"))
                      .AppendLine();
                }
                catch (Exception e)
                {
                    Log.WarnOnce("diag.buff." + id, "GetBuffAttrByID(" + id + ") 异常: " + e.GetType().Name);
                }
            }
            sb.AppendLine("[diag] 共解析到 " + found + " 条 buff 定义");
            Log.Info(sb.ToString());
        }

        private static void DumpGlobalSwitches()
        {
            try
            {
                Log.Info("[diag] GameConfigs.playerInvincible = " + GameConfigs.playerInvincible);
            }
            catch (Exception e) { Log.WarnOnce("diag.inv", "playerInvincible 读取失败: " + e.GetType().Name); }

            try
            {
                Log.Info("[diag] GameConfigs.playerGodMode = " + GameConfigs.playerGodMode);
            }
            catch (Exception e) { Log.WarnOnce("diag.god", "playerGodMode 读取失败: " + e.GetType().Name); }

            // 联机门禁信号（安全设计的地基）——只读
            try
            {
                Log.Info("[diag] NetworkManager.isOnline = " + ZedZone.Net.NetworkManager.isOnline
                       + " | isHost = " + ZedZone.Net.NetworkManager.isHost
                       + " | isClient = " + ZedZone.Net.NetworkManager.isClient);
            }
            catch (Exception e) { Log.WarnOnce("diag.net", "NetworkManager 联机标志读取失败: " + e.GetType().Name); }

            try
            {
                var nm = ZedZone.Net.NetworkManager.instance;
                Log.Info("[diag] NetworkManager.instance = " + (nm == null ? "null" : "ok")
                       + (nm == null ? "" : " | mode(int)=" + nm.mode));
            }
            catch (Exception e) { Log.WarnOnce("diag.neti", "NetworkManager.instance 读取失败: " + e.GetType().Name); }
        }

        private static void DumpPlayerPresence()
        {
            try
            {
                var gc = GameController.instance;
                Log.Info("[diag] GameController.instance = " + (gc == null ? "null" : "ok"));
                if (gc != null)
                {
                    var p = gc.playerCharacter;
                    Log.Info("[diag] playerCharacter = " + (p == null ? "null" : "ok"));
                }
            }
            catch (Exception e) { Log.WarnOnce("diag.pc", "玩家存在性检查失败: " + e.GetType().Name); }
        }
    }
}
