using System;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 辐射控制。
    ///
    /// 数据位置（1.0.18 interop 已核对）：
    ///  * `CharacterStatusData.radiationDose`（float，实例字段；interop 未生成 setter）
    ///  * `RadiationDose`（静态类）提供 `Step/IntakeRate/Ambient/MaxDose` 等计算
    ///
    /// 因此本模块采用三条写入/拦截路径，逐级降级：
    ///  1. IL2CPP 元数据 API `il2cpp_field_set_value`（按字段名，非猜测）；
    ///  2. 元数据取字段偏移后按偏移写（仍来自 `il2cpp_field_get_offset`）；
    ///  3. 兜底：补丁 `RadiationDose.Step` 返回 0（保持 0 的另一条保险）。
    ///
    /// 只对本地玩家生效，全程 try/catch。
    /// </summary>
    internal static class RadiationControl
    {
        private const string Cls = "CharacterStatusData";
        private const string Fld = "radiationDose";

        private static float _nextTick;
        private static int _log;
        internal static string LastError { get; private set; }

        internal static bool TryRead(out float dose)
        {
            dose = 0f;
            try
            {
                var p = PlayerAccessor.Resolve(out _, out _);
                if (p == null) { LastError = "player == null"; return false; }
                var sd = PlayerDataAccess.GetStatusData(p, out string err);
                if (sd == null) { LastError = err; return false; }
                return Il2CppFields.TryGetFloat(sd.Pointer, Cls, Fld, out dose);
            }
            catch (Exception e) { LastError = e.GetType().Name; return false; }
        }

        /// <summary>立即把辐射值写 0（策略 1 → 策略 2），并回读验证。</summary>
        internal static bool ClearNow()
        {
            try
            {
                var p = PlayerAccessor.Resolve(out _, out _);
                if (p == null) { LastError = "player == null"; return false; }
                var sd = PlayerDataAccess.GetStatusData(p, out string err);
                if (sd == null) { LastError = err; return false; }
                IntPtr ptr = sd.Pointer;
                if (ptr == IntPtr.Zero) { LastError = "statusData pointer == 0"; return false; }

                // 策略 1：元数据字段写入
                if (Il2CppFields.TrySetFloat(ptr, Cls, Fld, 0f))
                {
                    if (Il2CppFields.TryGetFloat(ptr, Cls, Fld, out float v1) && v1 <= 0.0001f)
                    {
                        if (_log < 5) { _log++; Log.Info("[rad] 已清空辐射（il2cpp_field_set_value）"); }
                        return true;
                    }
                    LastError = "写入成功但回读不为 0";
                    Log.WarnOnce("rad.verify1", "[rad] 策略1 写入后回读不为 0，尝试策略2");
                }
                else
                {
                    LastError = "il2cpp_field_set_value 不可用";
                    Log.WarnOnce("rad.s1", "[rad] 策略1 不可用，尝试策略2（偏移写入）");
                }

                // 策略 2：偏移写入
                int off = Il2CppFields.OffsetOf(Cls, Fld);
                if (off >= 0)
                {
                    unsafe
                    {
                        *(float*)(ptr + off) = 0f;
                    }
                    if (Il2CppFields.TryGetFloat(ptr, Cls, Fld, out float v2) && v2 <= 0.0001f)
                    {
                        if (_log < 5) { _log++; Log.Info($"[rad] 已清空辐射（偏移写入 off=0x{off:X}）"); }
                        return true;
                    }
                    LastError = $"偏移写入回读失败 (off=0x{off:X})";
                }
                else
                {
                    LastError = "无法解析 radiationDose 字段偏移";
                }

                Log.WarnOnce("rad.clear.fail", "[rad] 清空辐射失败：" + LastError);
                return false;
            }
            catch (Exception e)
            {
                LastError = e.GetType().Name + ": " + e.Message;
                Log.ErrorOnce("rad.clear", e);
                return false;
            }
        }

        /// <summary>保持 0 辐射：按节流持续清零。</summary>
        internal static void Tick()
        {
            try
            {
                if (!Features.RadiationLockOn) return;
                if (Time.unscaledTime < _nextTick) return;
                _nextTick = Time.unscaledTime + 0.5f;
                ClearNow();
            }
            catch (Exception e) { Log.ErrorOnce("rad.tick", e); }
        }

        /// <summary>
        /// 自检：按三种思路依次验证"能否读写 radiationDose"。
        /// 只在 [Diagnostics] RadiationSelfTest 开启时由面板调用一次。
        /// 写入测试值后立即还原原值，避免留下辐射。
        /// </summary>
        internal static bool SelfTest()
        {
            Log.Info("=== [rad] 自检开始（3 种思路） ===");

            bool okRead = TryRead(out float original);
            Log.Info($"[rad] 思路0 读取: ok={okRead} dose={original:0.####} err={LastError ?? "-"}");

            // 思路1：元数据字段写入 + 回读
            bool s1 = false;
            try
            {
                var p = PlayerAccessor.Resolve(out _, out _);
                var sd = p == null ? null : PlayerDataAccess.GetStatusData(p, out _);
                if (sd != null)
                {
                    float test = Mathf.Min(0.01f, Mathf.Max(0f, original) + 0.01f);
                    s1 = Il2CppFields.TrySetFloat(sd.Pointer, Cls, Fld, test)
                         && Il2CppFields.TryGetFloat(sd.Pointer, Cls, Fld, out float back)
                         && Mathf.Abs(back - test) < 0.0001f;
                    Il2CppFields.TrySetFloat(sd.Pointer, Cls, Fld, original);   // 还原
                    Log.Info($"[rad] 思路1 il2cpp_field_set_value: {(s1 ? "成功" : "失败")}（测试值 {test:0.####}）");
                }
                else Log.Info("[rad] 思路1 跳过：statusData 为空");
            }
            catch (Exception e) { Log.Info("[rad] 思路1 异常: " + e.GetType().Name); }

            // 思路2：偏移写入 + 回读
            bool s2 = false;
            try
            {
                var p = PlayerAccessor.Resolve(out _, out _);
                var sd = p == null ? null : PlayerDataAccess.GetStatusData(p, out _);
                int off = Il2CppFields.OffsetOf(Cls, Fld);
                if (sd != null && off >= 0)
                {
                    float test = Mathf.Min(0.01f, Mathf.Max(0f, original) + 0.01f);
                    unsafe { *(float*)(sd.Pointer + off) = test; }
                    s2 = Il2CppFields.TryGetFloat(sd.Pointer, Cls, Fld, out float back2)
                         && Mathf.Abs(back2 - test) < 0.0001f;
                    unsafe { *(float*)(sd.Pointer + off) = original; }        // 还原
                    Log.Info($"[rad] 思路2 偏移写入 off=0x{off:X}: {(s2 ? "成功" : "失败")}");
                }
                else Log.Info($"[rad] 思路2 跳过：off={off}");
            }
            catch (Exception e) { Log.Info("[rad] 思路2 异常: " + e.GetType().Name); }

            // 思路3：RadiationDose.Step 是否可被补丁拦截（由 Patch/RadiationPatches 提供）
            Log.Info($"[rad] 思路3 RadiationDose.Step 补丁: {(Patch.RadiationPatches.StepPatched ? "已挂载" : "未挂载")}");

            // 最终：清空
            bool cleared = ClearNow();
            TryRead(out float after);
            Log.Info($"=== [rad] 自检结束：读={okRead} 写1={s1} 写2={s2} 清空={cleared} 当前dose={after:0.####} ===");
            return okRead && (s1 || s2) && cleared;
        }

        internal static string StatusText()
        {
            TryRead(out float v);
            return $"辐射={v:0.###} 保持0={(Features.RadiationLockOn ? "开" : "关")}";
        }
    }
}

