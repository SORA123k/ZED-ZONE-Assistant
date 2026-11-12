using System;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// Phase 3B-2A：恢复接口的**只读**校验探针。
    ///
    /// 目的：在**不修改任何肢体数据**的前提下，用游戏自己的只读接口验证
    /// `bodyPartId` 的参数空间、`hpFloat` 的语义、以及部位 → 恢复 Buff 的映射。
    ///
    /// 只调用以下**只读**接口（均由装箱/out 参数表明无副作用）：
    ///   * `HumanCharacterController.ReadWoundInput(int bodyPartId, out float hpFloat, out bool bandaged)`
    ///   * `HumanCharacterController.GetRecoveryBuffId(int bodyPartId)`（static）
    ///   * `HumanCharacterController.IsBodyPartWounded(CharacterBodyPartData)`（static）
    ///
    /// **绝不调用**：HealAllBodyPartInjuries / FullyHealInjuries / SetToDefaultBodyPart /
    /// RefreshBodyPartRecoveryBuff / UseBandage / RemoveBandage / StartBleeding /
    /// StopAllBleeding / OnSleepTimeUpdate / OnAwakeTimeUpdate。
    ///
    /// 默认关闭（`[Diagnostics] LimbApiProbe = false`），联机拒绝执行。
    /// </summary>
    internal static class LimbApiProbe
    {
        private static IntPtr _lastPtr = IntPtr.Zero;
        private static bool _done;
        private static bool _refused;

        internal static bool Enabled => ModConfig.LimbApiProbe?.Value ?? false;

        internal static void Tick()
        {
            if (!Enabled) return;

            if (PlayerWriteGate.IsMultiplayer(out string why))
            {
                if (!_refused)
                {
                    _refused = true;
                    Log.Warn("[limbapi] 联机状态（" + why + "），只读校验探针拒绝执行。");
                }
                return;
            }

            HumanCharacterController p = null;
            try { p = PlayerAccessor.Resolve(out _, out _); } catch { }
            if (p == null) return;

            IntPtr ptr = IntPtr.Zero;
            try { ptr = p.Pointer; } catch { }

            if (_done && ptr == _lastPtr) return;   // 同一玩家只跑一次
            _lastPtr = ptr;
            _done = true;

            Run(p, ptr);
        }

        private static void Run(HumanCharacterController p, IntPtr ptr)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[limbapi] ===== 只读校验（不写任何数据、不调用任何恢复接口）=====");
            sb.AppendLine($"[limbapi] scene={PlayerAccessor.CurrentSceneName()} ptr=0x{ptr.ToString("X")}");

            CharacterBodyPartsData bp = null;
            try { bp = p.characterData?.bodypartsData; } catch { }
            if (bp == null) { sb.AppendLine("[limbapi] bodypartsData == null，跳过"); Log.Info(sb.ToString()); return; }

            Il2CppReferenceArray<CharacterBodyPartData> arr = null;
            try { arr = bp.bodypartDataAry; } catch { }
            if (arr == null) { sb.AppendLine("[limbapi] bodypartDataAry == null，跳过"); Log.Info(sb.ToString()); return; }

            int count = -1;
            try { count = CharacterBodyPartsData.humanBodyPartCount; } catch { }
            sb.AppendLine($"[limbapi] humanBodyPartCount={count} arrayLen={arr.Length}");

            // ---- 真实值（数据侧，作为对照基准） ----
            sb.AppendLine("[limbapi] ---- 数据侧真值 ----");
            for (int i = 0; i < arr.Length; i++)
            {
                try
                {
                    var d = arr[i];
                    if (d == null) { sb.AppendLine($"[limbapi]  data[{i}] = null"); continue; }
                    float hp = float.NaN, max = float.NaN, pct = float.NaN;
                    try { hp = d.hp; } catch { }
                    try { max = d.maxHP; } catch { }
                    try { pct = d.hpFloat; } catch { }
                    bool w = false, b = false, full = false;
                    try { w = d.isWounded; } catch { }
                    try { b = d.isBleeding; } catch { }
                    try { full = d.isFullyRecovered; } catch { }
                    bool sw = false; bool swOk = false;
                    try { sw = HumanCharacterController.IsBodyPartWounded(d); swOk = true; } catch { }

                    sb.AppendLine($"[limbapi]  data[{i}] id={SafeInt(() => d.bodyPartId)} textId={SafeInt(() => d.bodyPartTextId)} " +
                                  $"hp={F(hp)} max={F(max)} hpFloat={F(pct)} wounded={B(w)} bleeding={B(b)} fully={B(full)} " +
                                  $"IsBodyPartWounded={(swOk ? B(sw) : "?")} 一致={(swOk ? (sw == w ? "yes" : "NO") : "?")}");
                }
                catch (Exception e) { sb.AppendLine($"[limbapi]  data[{i}] 读取异常 {e.GetType().Name}"); }
            }

            // ---- 按 bodyPartId 探测（只读接口） ----
            sb.AppendLine("[limbapi] ---- ReadWoundInput(bodyPartId) 探测 ----");
            int probeMax = Math.Max(arr.Length, 8) + 2;   // 故意越界一点，用异常边界确认合法范围
            for (int id = 0; id < probeMax; id++)
            {
                float hpFloat = float.NaN;
                bool bandaged = false;
                string rwErr = null;
                try { p.ReadWoundInput(id, out hpFloat, out bandaged); }
                catch (Exception e) { rwErr = e.GetType().Name; }

                int buffId = int.MinValue;
                string rbErr = null;
                try { buffId = HumanCharacterController.GetRecoveryBuffId(id); }
                catch (Exception e) { rbErr = e.GetType().Name; }

                sb.Append($"[limbapi]  id={id} ReadWoundInput=");
                sb.Append(rwErr == null ? $"{{hpFloat={F(hpFloat)}, bandaged={B(bandaged)}}}" : $"<{rwErr}>");
                sb.Append(" GetRecoveryBuffId=");
                sb.Append(rbErr == null ? buffId.ToString() : "<" + rbErr + ">");
                sb.AppendLine();

                if (rwErr != null && rbErr != null) break;   // 两个都炸，说明已越界，停止
            }

            sb.AppendLine("[limbapi] ===== 结束（以上全部为只读调用）=====");
            Log.Info(sb.ToString());
        }

        private static string B(bool v) => v ? "1" : "0";
        private static string F(float v) => float.IsNaN(v) ? "?" : v.ToString("0.###");
        private static string SafeInt(Func<int> f)
        {
            try { return f().ToString(); } catch { return "?"; }
        }

        internal static string Status() => $"LimbApiProbe={(Enabled ? (_done ? "done" : "on") : "off")}";
    }
}
