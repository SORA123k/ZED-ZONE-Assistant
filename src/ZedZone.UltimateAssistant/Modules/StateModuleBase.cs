using System;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 所有"状态读取模块"的基类。
    ///
    /// Phase 2 约束：模块**只有 Read**，没有 Write。
    /// 这是刻意的 —— 让"Phase 2 只读"成为编译期事实，而不是靠运行时开关保证。
    /// Phase 3 才在子类/新模块里增加写入能力，并单独评审。
    /// </summary>
    internal abstract class StateModuleBase
    {
        internal abstract string Id { get; }
        internal abstract string TitleZh { get; }

        /// <summary>模块开关（配置项）。关闭后本模块不出现在快照中。</summary>
        internal bool Enabled { get; set; } = true;

        /// <summary>读取并追加到快照。必须自行处理异常，绝不外抛。</summary>
        internal abstract void Read(HumanCharacterController player, PlayerSnapshot snap);

        // ---------- 供子类使用的安全读取工具 ----------

        protected static float ReadFloat(string key, string labelZh, Func<float> getter, string note = null)
        {
            // getter 抛异常时向上抛，由 ReadValueFor 统一转成 Failed
            return getter();
        }

        /// <summary>
        /// 读取一个 float 并构造 ReadValue。
        /// 任何异常都会被转成 State=Failed，且把异常类型写进 Error —— 不伪造数值。
        /// </summary>
        protected static ReadValue ReadValueFor(
            PlayerSnapshot snap,
            string key,
            string labelZh,
            Func<float> getter,
            Func<float> maxGetter = null,
            string note = null)
        {
            var rv = new ReadValue
            {
                Key = key,
                LabelZh = labelZh,
                Note = note,
                State = ReadState.Ok
            };

            try
            {
                rv.Value = getter();
            }
            catch (Exception e)
            {
                Log.WarnOnce("read." + key, $"[read] {key} 失败: {e.GetType().Name}: {e.Message}");
                rv.State = ReadState.Failed;
                rv.Error = e.GetType().Name + ": " + e.Message;
                return rv;
            }

            if (maxGetter != null)
            {
                try
                {
                    float m = maxGetter();
                    if (m > 0f) { rv.Max = m; rv.HasMax = true; }
                    else { rv.HasMax = false; snap.Warnings.Add($"{labelZh}: 上限值异常({m})"); }
                }
                catch (Exception e)
                {
                    rv.HasMax = false;
                    snap.Warnings.Add($"{labelZh}: 上限读取失败({e.GetType().Name})");
                }
            }

            // 合理性检查：只标记，不修正
            if (float.IsNaN(rv.Value) || float.IsInfinity(rv.Value))
            {
                rv.State = ReadState.Suspicious;
                snap.Warnings.Add($"{labelZh}: 数值非有限({rv.Value})");
            }
            else if (rv.Value < 0f)
            {
                rv.State = ReadState.Suspicious;
                snap.Warnings.Add($"{labelZh}: 数值为负({rv.Value})");
            }

            return rv;
        }

        protected static ReadValue ReadBoolAsValue(PlayerSnapshot snap, string key, string labelZh,
            Func<bool> getter, string note = null)
        {
            var rv = new ReadValue { Key = key, LabelZh = labelZh, Note = note, State = ReadState.Ok };
            try
            {
                rv.Value = getter() ? 1f : 0f;
                rv.HasMax = false;
            }
            catch (Exception e)
            {
                Log.WarnOnce("read." + key, $"[read] {key} 失败: {e.GetType().Name}");
                rv.State = ReadState.Failed;
                rv.Error = e.GetType().Name + ": " + e.Message;
            }
            return rv;
        }

        protected static void Add(PlayerSnapshot snap, ReadValue rv)
        {
            if (rv != null) snap.Values.Add(rv);
        }
    }
}
