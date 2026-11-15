using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 连点器（系统级左键连点）。
    ///
    /// 性能要点（解决掉帧）：
    ///  * 进程 ID、前台窗口判断全部**缓存/节流**（每 0.25 秒一次），不在每帧做进程查询；
    ///  * 用 `Stopwatch` 控制节奏，避免 `Thread.Sleep` 抖动累积；
    ///  * `timeBeginPeriod(1)` 提升 Sleep 精度（否则 10ms 会睡成 15.6ms）；
    ///  * 最小间隔 5ms（0 视为 5ms），防止把游戏输入队列打爆造成掉帧；
    ///  * 只在"前台窗口属于本游戏进程"时点击，切换窗口立即停止；
    ///  * 线程优先级降到 BelowNormal，避免抢主线程时间片。
    /// </summary>
    internal static class AutoClicker
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint ms);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint ms);

        private static Thread _thread;
        private static volatile bool _run;
        private static volatile bool _focused;
        private static int _selfPid = -1;
        private static int _fires;

        internal static int Fires => _fires;

        /// <summary>主线程每帧调用：只更新"游戏是否在前台"（0.25s 节流）。</summary>
        private static float _nextFocusCheck;

        internal static void UpdateFocus()
        {
            try
            {
                if (Time.unscaledTime < _nextFocusCheck) return;
                _nextFocusCheck = Time.unscaledTime + 0.25f;

                if (!Application.isFocused) { _focused = false; return; }

                if (_selfPid < 0)
                {
                    try { _selfPid = Process.GetCurrentProcess().Id; } catch { _selfPid = 0; }
                }

                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) { _focused = false; return; }
                GetWindowThreadProcessId(fg, out uint pid);
                _focused = _selfPid > 0 && pid == (uint)_selfPid;
            }
            catch { _focused = false; }
        }

        internal static void Start()
        {
            if (_thread != null && _thread.IsAlive) return;
            _run = true;
            try { TimeBeginPeriod(1); } catch { }
            _thread = new Thread(Loop) { IsBackground = true, Name = "ZedUaAutoClicker", Priority = System.Threading.ThreadPriority.BelowNormal };
            _thread.Start();
            Log.Info("[clicker] 连点线程已启动（系统级左键，节流监控前台窗口）");
        }

        internal static void Stop()
        {
            _run = false;
            try { if (_thread != null && _thread.IsAlive) _thread.Join(300); } catch { }
            _thread = null;
            try { TimeEndPeriod(1); } catch { }
            Log.Info("[clicker] 连点线程已停止");
        }

        private static void Loop()
        {
            var sw = Stopwatch.StartNew();
            double next = 0;

            while (_run)
            {
                try
                {
                    if (!Features.LeftClickerOn || !_focused)
                    {
                        Thread.Sleep(40);
                        next = sw.Elapsed.TotalMilliseconds;
                        continue;
                    }

                    int ms = ModConfig.LeftClickInterval?.Value ?? 50;
                    if (ms < 5) ms = 5;        // 最小 5ms，避免打爆输入队列
                    if (ms > 1000) ms = 1000;

                    double now = sw.Elapsed.TotalMilliseconds;
                    if (now < next) { Thread.Sleep(1); continue; }

                    MouseNative.LeftClick();
                    _fires++;

                    next += ms;
                    // 长时间卡顿后不要补点（避免瞬间连发造成掉帧）
                    if (now - next > ms * 3) next = now + ms;
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("clicker.loop", e);
                    Thread.Sleep(200);
                }
            }
        }

        internal static void Reset() => _fires = 0;
    }
}

