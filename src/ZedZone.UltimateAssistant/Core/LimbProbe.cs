using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 3B-0：肢体系统**只读枚举**采集器。
    ///
    /// 严格只读：
    ///   * 只读 `CharacterBodyPartsData` / `CharacterBodyPartData` / `CharacterBodyPart` 的字段与派生属性；
    ///   * 不写任何 HP / 状态 / 存档 / GameConfigs；
    ///   * 不调用任何恢复接口（不使用 HealAllBodyPartInjuries / SetToDefaultBodyPart 等）；
    ///   * 不安装任何 Harmony Patch。
    ///
    /// 安全：
    ///   * 默认关闭（`[Diagnostics] LimbProbe = false`）；
    ///   * **联机一律拒绝执行**（fail-closed，沿用已实机验证的 NetworkManager 判定）；
    ///   * 每次采样都重新解析玩家对象，绝不缓存。
    ///
    /// 输出：
    ///   * 首次拿到玩家 → 完整枚举表；
    ///   * 玩家/场景变化 → 重新枚举（证明旧引用失效后能重建）；
    ///   * 某肢体数值/状态变化 → 输出 CHANGE 增量行（用于验证实时性与状态关联）；
    ///   * 周期性心跳摘要。
    /// </summary>
    internal static class LimbProbe
    {
        private sealed class Row
        {
            internal int Index;
            internal int BodyPartId;
            internal int TextId;
            internal string Name = "?";
            internal float Hp = float.NaN;
            internal float MaxHp = float.NaN;
            internal float DefaultHp = float.NaN;
            internal bool Wounded;
            internal bool Bleeding;
            internal bool Recovered;
            internal float HpFloat = float.NaN;
            internal int VisualDataId = int.MinValue;
            internal bool ReadOk;
            internal string Error;
            internal string Sig()
                => $"{Hp:0.###}|{MaxHp:0.###}|{Wounded}|{Bleeding}|{Recovered}|{BodyPartId}|{TextId}";
        }

        private static readonly List<Row> _prev = new List<Row>();
        private static string _prevScene;
        private static IntPtr _prevPtr = IntPtr.Zero;
        private static bool _hadPlayer;
        private static float _nextSampleAt;
        private static float _nextHeartbeatAt;
        private static int _samples;
        private static int _changes;
        private static bool _refusedLogged;

        // 变化合并（避免肢体自然恢复导致日志刷屏）
        private static float _nextFlushAt;
        private static readonly List<string> _pendingChanges = new List<string>();

        internal static bool Enabled => ModConfig.LimbProbe?.Value ?? false;

        internal static void Tick()
        {
            if (!Enabled) return;

            float now = Time.unscaledTime;
            int intervalMs = Mathf.Clamp(ModConfig.LimbProbeIntervalMs?.Value ?? 500, 100, 5000);
            if (now * 1000f < _nextSampleAt) return;
            _nextSampleAt = now * 1000f + intervalMs;

            // ---- 联机拒绝（fail-closed） ----
            if (PlayerWriteGate.IsMultiplayer(out string why))
            {
                if (!_refusedLogged)
                {
                    _refusedLogged = true;
                    Log.Warn("[limb] 联机状态（" + why + "），肢体采集模块拒绝执行。");
                }
                return;
            }

            // ---- 每次重新解析玩家（禁止缓存） ----
            HumanCharacterController p = null;
            try { p = PlayerAccessor.Resolve(out _, out _); } catch { }
            string scene = PlayerAccessor.CurrentSceneName();

            if (p == null)
            {
                if (_hadPlayer)
                {
                    Log.Info("[limb] 玩家对象消失（场景=" + scene + "），等待重新出现。");
                    _hadPlayer = false;
                    _prev.Clear();
                }
                return;
            }

            IntPtr ptr = IntPtr.Zero;
            try { ptr = p.Pointer; } catch { }

            bool identityChanged = !_hadPlayer || ptr != _prevPtr || scene != _prevScene;

            var rows = Enumerate(p, out string enumError);

            if (enumError != null)
            {
                Log.WarnOnce("limb.enum", "[limb] 枚举失败: " + enumError);
                return;
            }

            if (rows == null)
            {
                return;
            }

            if (identityChanged)
            {
                _hadPlayer = true;
                _prevPtr = ptr;
                _prevScene = scene;
                LogFullTable(p, rows, scene, ptr, identityChanged: true);
                _prev.Clear();
                for (int i = 0; i < rows.Count; i++) _prev.Add(Clone(rows[i]));
                _nextHeartbeatAt = now + Mathf.Clamp(ModConfig.LimbProbeHeartbeatSec?.Value ?? 30, 5, 600);
                return;
            }

            _samples++;

            // ---- 增量检测（先累积，按秒合并输出，避免自然恢复刷屏） ----
            for (int i = 0; i < rows.Count; i++)
            {
                if (i >= _prev.Count) { _pendingChanges.Add($"新增肢体 idx={rows[i].Index}"); continue; }
                if (rows[i].Sig() != _prev[i].Sig())
                {
                    _changes++;
                    string d = DescribeChange(_prev[i], rows[i]);
                    if (d != null) _pendingChanges.Add(d);
                }
            }
            for (int i = 0; i < rows.Count; i++) _prev[i] = Clone(rows[i]);

            // ---- 合并输出（默认每秒一次） ----
            int flushSec = Mathf.Clamp(ModConfig.LimbProbeChangeFlushSec?.Value ?? 1, 1, 60);
            if (now >= _nextFlushAt)
            {
                _nextFlushAt = now + flushSec;
                if (_pendingChanges.Count > 0)
                {
                    Log.Info($"[limb] CHANGE({flushSec}s) " + string.Join("; ", _pendingChanges.ToArray()));
                    _pendingChanges.Clear();
                }
            }

            // ---- 心跳 ----
            if (now >= _nextHeartbeatAt)
            {
                _nextHeartbeatAt = now + Mathf.Clamp(ModConfig.LimbProbeHeartbeatSec?.Value ?? 30, 5, 600);
                LogHeartbeat(rows, scene);
            }
        }

        // ==================================================================
        private static List<Row> Enumerate(HumanCharacterController p, out string error)
        {
            error = null;
            var list = new List<Row>();

            CharacterData cd = null;
            try { cd = p.characterData; } catch (Exception e) { error = "characterData: " + e.GetType().Name; return null; }
            if (cd == null) { error = "characterData == null"; return null; }

            CharacterBodyPartsData bp = null;
            try { bp = cd.bodypartsData; } catch (Exception e) { error = "bodypartsData: " + e.GetType().Name; return null; }
            if (bp == null) { error = "bodypartsData == null"; return null; }

            int count = -1;
            try { count = CharacterBodyPartsData.humanBodyPartCount; }
            catch (Exception e) { Log.WarnOnce("limb.count", "[limb] humanBodyPartCount 读取失败: " + e.GetType().Name); }

            Il2CppReferenceArray<CharacterBodyPartData> arr = null;
            try { arr = bp.bodypartDataAry; }
            catch (Exception e) { error = "bodypartDataAry: " + e.GetType().Name; return null; }

            if (arr == null) { error = "bodypartDataAry == null"; return null; }

            if (count >= 0 && count != arr.Length)
                Log.WarnOnce("limb.len-mismatch",
                    $"[limb] humanBodyPartCount({count}) != bodypartDataAry.Length({arr.Length})，以数组为准枚举。");

            for (int i = 0; i < arr.Length; i++)
            {
                var r = new Row { Index = i };
                try
                {
                    var d = arr[i];
                    if (d == null) { r.Error = "null element"; list.Add(r); continue; }

                    r.BodyPartId = d.bodyPartId;
                    r.TextId = d.bodyPartTextId;
                    r.Hp = d.hp;
                    r.MaxHp = d.maxHP;
                    r.DefaultHp = d.defaultHP;
                    r.Name = ResolveName(r.TextId);
                    try { r.Wounded = d.isWounded; } catch { }
                    try { r.Bleeding = d.isBleeding; } catch { }
                    try { r.Recovered = d.isFullyRecovered; } catch { }
                    try { r.HpFloat = d.hpFloat; } catch { }
                    r.ReadOk = true;
                }
                catch (Exception e)
                {
                    r.Error = e.GetType().Name;
                }
                list.Add(r);
            }
            return list;
        }

        /// <summary>通过游戏自身本地化取部位名；失败如实回退，不猜测。</summary>
        private static string ResolveName(int textId)
        {
            if (textId <= 0) return "未知(TextId=" + textId + ")";
            try
            {
                var tm = TextManager.instance;
                if (tm == null) return "未知(TextId=" + textId + ",TextManager=null)";
                try { tm.EnsureInitialized(); } catch { }
                string s = tm.GetText(textId);
                if (string.IsNullOrEmpty(s)) return "未知(TextId=" + textId + ",空文本)";
                return s.Replace('\n', ' ');
            }
            catch (Exception e)
            {
                return "未知(TextId=" + textId + "," + e.GetType().Name + ")";
            }
        }

        private static Row Clone(Row r)
        {
            return new Row
            {
                Index = r.Index, BodyPartId = r.BodyPartId, TextId = r.TextId, Name = r.Name,
                Hp = r.Hp, MaxHp = r.MaxHp, DefaultHp = r.DefaultHp,
                Wounded = r.Wounded, Bleeding = r.Bleeding, Recovered = r.Recovered,
                HpFloat = r.HpFloat, VisualDataId = r.VisualDataId, ReadOk = r.ReadOk, Error = r.Error
            };
        }

        private static void LogFullTable(HumanCharacterController p, List<Row> rows, string scene, IntPtr ptr, bool identityChanged)
        {
            int count = -1;
            try { count = CharacterBodyPartsData.humanBodyPartCount; } catch { }

            int textIds = -1;
            try
            {
                var a = CharacterBodyPartsData.humanBodyPartTextIdAry;
                if (a != null) textIds = a.Length;
            }
            catch { }

            int defHps = -1;
            try
            {
                var a = CharacterBodyPartsData.humanBodyPartDefaultHPAry;
                if (a != null) defHps = a.Length;
            }
            catch { }
            int appliedCon = int.MinValue;
            try { appliedCon = p.characterData.appliedBodyPartConstitution; } catch { }

            var sb = new StringBuilder();
            sb.AppendLine($"[limb] ===== 肢体枚举 {(identityChanged ? "(重新枚举)" : "")} =====");
            sb.AppendLine($"[limb] scene={scene} ptr=0x{ptr.ToString("X")} isPlayer={SafeBool(() => p.isPlayer)}");
            sb.AppendLine($"[limb] humanBodyPartCount={count} arrayLen={rows.Count} textIdAryLen={textIds} defaultHPAryLen={defHps} appliedBodyPartConstitution={appliedCon}");
            sb.AppendLine("[limb] idx | bodyPartId | textId | name | hp | maxHP | defaultHP | wounded | bleeding | recovered | hpFloat");

            int mismatch = 0;   // 保留变量以兼容下方逻辑（当前不再逐行比对视觉索引）
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                sb.Append("[limb] ").Append(r.Index).Append(" | ")
                  .Append(r.BodyPartId).Append(" | ")
                  .Append(r.TextId).Append(" | ")
                  .Append(r.Name).Append(" | ")
                  .Append(F(r.Hp)).Append(" | ")
                  .Append(F(r.MaxHp)).Append(" | ")
                  .Append(F(r.DefaultHp)).Append(" | ")
                  .Append(r.Wounded ? 1 : 0).Append(" | ")
                  .Append(r.Bleeding ? 1 : 0).Append(" | ")
                  .Append(r.Recovered ? 1 : 0).Append(" | ")
                  .Append(F(r.HpFloat))
                  .Append(string.IsNullOrEmpty(r.Error) ? "" : " | err=" + r.Error)
                  .AppendLine();
            }
            sb.AppendLine($"[limb] ===== 枚举结束（{rows.Count} 个肢体）=====");

            // 数据侧静态表（只读）：确认"数据数组顺序"与"各部位默认 HP"
            try
            {
                var t = CharacterBodyPartsData.humanBodyPartTextIdAry;
                if (t != null)
                {
                    var b = new StringBuilder("[limb] humanBodyPartTextIdAry = [");
                    for (int i = 0; i < t.Length; i++) { if (i > 0) b.Append(", "); b.Append(t[i]); }
                    b.Append("]  -> names = [");
                    for (int i = 0; i < t.Length; i++) { if (i > 0) b.Append(", "); b.Append(ResolveName(t[i])); }
                    b.Append(']');
                    sb.AppendLine(b.ToString());
                }
            }
            catch (Exception e) { sb.AppendLine("[limb] humanBodyPartTextIdAry 读取失败: " + e.GetType().Name); }

            try
            {
                var d = CharacterBodyPartsData.humanBodyPartDefaultHPAry;
                if (d != null)
                {
                    var b = new StringBuilder("[limb] humanBodyPartDefaultHPAry = [");
                    for (int i = 0; i < d.Length; i++) { if (i > 0) b.Append(", "); b.Append(d[i].ToString("0.##")); }
                    b.Append(']');
                    sb.AppendLine(b.ToString());
                }
            }
            catch (Exception e) { sb.AppendLine("[limb] humanBodyPartDefaultHPAry 读取失败: " + e.GetType().Name); }

            // ---- 视觉组件清单：核对 GetBodyPartByIndex 与 bodypartDataAry 是否同一索引空间 ----
            var vis = new StringBuilder();
            int visCount = 0;
            for (int i = 0; i < 32; i++)
            {
                CharacterBodyPart v = null;
                try { v = p.GetBodyPartByIndex(i); } catch { break; }
                if (v == null) break;
                visCount++;
                int did = int.MinValue, tid = int.MinValue;
                bool clothing = false, clothingOk = false;
                try { did = v.bodypartDataID; } catch { }
                try { tid = v.nameTextId; } catch { }
                try { clothing = v.IsClothingBodyPart; clothingOk = true; } catch { }
                vis.Append("[limb]   visual[").Append(i).Append("] bodypartDataID=").Append(did)
                   .Append(" nameTextId=").Append(tid)
                   .Append(" name=").Append(ResolveName(tid))
                   .Append(" clothing=").Append(clothingOk ? (clothing ? "1" : "0") : "?")
                   .AppendLine();
            }
            sb.AppendLine($"[limb] 视觉组件数量={visCount}（GetBodyPartByIndex 遍历）");
            sb.Append(vis);

            Log.Info(sb.ToString());
        }

        private static string DescribeChange(Row a, Row b)
        {
            var sb = new StringBuilder();
            sb.Append(b.Index).Append(':').Append(b.Name);
            bool any = false;
            if (!Same(a.Hp, b.Hp)) { sb.Append(" hp ").Append(F(a.Hp)).Append("->").Append(F(b.Hp)); any = true; }
            if (!Same(a.MaxHp, b.MaxHp)) { sb.Append(" maxHP ").Append(F(a.MaxHp)).Append("->").Append(F(b.MaxHp)); any = true; }
            if (a.Wounded != b.Wounded) { sb.Append(" wounded ").Append(a.Wounded ? 1 : 0).Append("->").Append(b.Wounded ? 1 : 0); any = true; }
            if (a.Bleeding != b.Bleeding) { sb.Append(" bleeding ").Append(a.Bleeding ? 1 : 0).Append("->").Append(b.Bleeding ? 1 : 0); any = true; }
            if (a.Recovered != b.Recovered) { sb.Append(" recovered ").Append(a.Recovered ? 1 : 0).Append("->").Append(b.Recovered ? 1 : 0); any = true; }
            if (!Same(a.HpFloat, b.HpFloat)) { sb.Append(" hpFloat ").Append(F(a.HpFloat)).Append("->").Append(F(b.HpFloat)); any = true; }
            return any ? sb.ToString() : null;
        }

        private static void LogHeartbeat(List<Row> rows, string scene)
        {
            var sb = new StringBuilder();
            sb.Append("[limb] heartbeat scene=").Append(scene).Append(" samples=").Append(_samples)
              .Append(" changes=").Append(_changes).Append(" | ");
            for (int i = 0; i < rows.Count && i < 12; i++)
            {
                if (i > 0) sb.Append("; ");
                sb.Append(rows[i].Name).Append('=').Append(F(rows[i].Hp)).Append('/').Append(F(rows[i].MaxHp));
                if (rows[i].Wounded) sb.Append('W');
                if (rows[i].Bleeding) sb.Append('B');
            }
            Log.Info(sb.ToString());
        }

        private static bool Same(float a, float b)
            => (float.IsNaN(a) && float.IsNaN(b)) || Math.Abs(a - b) < 0.005f;

        private static string F(float v)
            => float.IsNaN(v) ? "?" : v.ToString("0.##");

        private static string SafeBool(Func<bool> f)
        {
            try { return f() ? "1" : "0"; } catch { return "?"; }
        }

        internal static string Status()
            => $"LimbProbe={(Enabled ? "on" : "off")} samples={_samples} changes={_changes} parts={_prev.Count}";

        /// <summary>供 GUI/诊断手动触发一次完整枚举（只读）。</summary>
        internal static void ForceEnumerate()
        {
            try
            {
                HumanCharacterController p = PlayerAccessor.Resolve(out _, out _);
                if (p == null) { Log.Info("[limb] 手动枚举：无玩家对象"); return; }
                var rows = Enumerate(p, out string err);
                if (rows == null) { Log.Info("[limb] 手动枚举失败: " + err); return; }
                LogFullTable(p, rows, PlayerAccessor.CurrentSceneName(), SafePtr(p), identityChanged: false);
            }
            catch (Exception e) { Log.ErrorOnce("limb.force", e); }
        }

        private static IntPtr SafePtr(HumanCharacterController p)
        {
            try { return p.Pointer; } catch { return IntPtr.Zero; }
        }
    }
}
