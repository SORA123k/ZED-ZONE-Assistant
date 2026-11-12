using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 日志系统（继承上一项目经验）：
    ///  * 每个方法都 try/catch —— 日志本身绝不能成为崩溃源；
    ///  * LogErrorOnce 按 (位置 + 异常类型) 去重，避免每帧刷屏变成新的卡顿源；
    ///  * key 刻意不含 message（消息会变，去重会失效）。
    /// </summary>
    internal static class Log
    {
        private static ManualLogSource _src;
        private static readonly HashSet<string> _onceKeys = new HashSet<string>();

        internal static void Init(ManualLogSource src) => _src = src;

        internal static void Info(string msg)
        {
            try { _src?.LogInfo(msg); } catch { }
        }

        internal static void Warn(string msg)
        {
            try { _src?.LogWarning(msg); } catch { }
        }

        internal static void Error(string msg)
        {
            try { _src?.LogError(msg); } catch { }
        }

        /// <summary>同一个 (where, exceptionType) 只打一次。</summary>
        internal static void ErrorOnce(string where, Exception e)
        {
            if (e == null) return;
            string key = where + "|" + e.GetType().FullName;
            bool first;
            lock (_onceKeys) first = _onceKeys.Add(key);
            if (!first) return;
            try { _src?.LogError($"[{where}] {e.GetType().Name}: {e.Message}"); } catch { }
        }

        /// <summary>用于"读取字段失败"场景：同一字段只报一次，但每次都记录失败状态。</summary>
        internal static void WarnOnce(string key, string msg)
        {
            bool first;
            lock (_onceKeys) first = _onceKeys.Add(key);
            if (!first) return;
            Warn(msg);
        }

        internal static int OnceCount { get { lock (_onceKeys) return _onceKeys.Count; } }
    }
}
