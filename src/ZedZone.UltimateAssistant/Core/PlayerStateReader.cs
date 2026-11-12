using System;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 快照读取器：把「解析玩家 → 逐模块读取 → 汇总」串起来。
    ///
    /// Phase 2 只读：本类没有任何写入游戏状态的代码路径。
    /// </summary>
    internal sealed class PlayerStateReader
    {
        private readonly ModuleRegistry _registry;

        // 用于验证"引用未被缓存"：记录上一次看到的指针
        private IntPtr _lastPtr = IntPtr.Zero;
        private string _lastScene = "";
        private bool _lastHadPlayer;

        internal PlayerStateReader(ModuleRegistry registry) => _registry = registry;

        internal PlayerSnapshot Read()
        {
            var snap = new PlayerSnapshot
            {
                FrameCount = Time.frameCount,
                SceneName = PlayerAccessor.CurrentSceneName(),
                At = DateTime.Now
            };

            HumanCharacterController player = null;
            string via = "";
            string err = null;
            try
            {
                player = PlayerAccessor.Resolve(out via, out err);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("Reader.Resolve", e);
                err = "解析玩家异常: " + e.GetType().Name;
            }

            if (player == null)
            {
                snap.HasPlayer = false;
                snap.PlayerTypeName = "(none)";
                if (!string.IsNullOrEmpty(err)) snap.Warnings.Add(err);

                if (_lastHadPlayer)
                {
                    Log.Info($"[state] 玩家对象消失（场景={snap.SceneName}）：{err}");
                    _lastHadPlayer = false;
                }
                _lastPtr = IntPtr.Zero;
                return snap;
            }

            snap.HasPlayer = true;
            snap.PlayerTypeName = "HumanCharacterController";
            snap.PlayerGuid = via;

            // 玩家切换检测（重生/换场景/读档）——证明我们没有缓存旧引用
            IntPtr ptr = IntPtr.Zero;
            try { ptr = player.Pointer; } catch { }
            bool changed = ptr != _lastPtr || snap.SceneName != _lastScene || !_lastHadPlayer;

            if (changed && (ModConfig.LogSnapshotOnChange?.Value ?? true))
            {
                Log.Info($"[state] 玩家引用={(ptr == IntPtr.Zero ? "?" : "0x" + ptr.ToString("X"))} " +
                         $"场景='{snap.SceneName}' 经由={via}");
            }
            _lastPtr = ptr;
            _lastScene = snap.SceneName;
            _lastHadPlayer = true;

            // 死亡标志（读失败不影响其它字段）
            try { snap.IsDead = player.deadFlag; }
            catch (Exception e) { snap.Warnings.Add("deadFlag 读取失败: " + e.GetType().Name); }

            try { snap.IsPlayerFlag = player.isPlayer; }
            catch { }

            // 逐模块读取
            var modules = _registry.All;
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                if (m == null || !m.Enabled) continue;
                try
                {
                    m.Read(player, snap);
                }
                catch (Exception e)
                {
                    Log.ErrorOnce("module." + m.Id, e);
                    snap.Warnings.Add($"{m.TitleZh} 模块异常: {e.GetType().Name}");
                }
            }

            MaybeLogValues(snap);
            return snap;
        }

        private float _nextValueLogAt;

        /// <summary>
        /// 周期性把真实读数写进日志。用途：验收时可以把日志数字与游戏原生状态面板对照，
        /// 不依赖截图。默认 10 秒一条，可在配置 [Read] LogValuesSeconds 调整（0=关闭）。
        /// </summary>
        private void MaybeLogValues(PlayerSnapshot snap)
        {
            int seconds = 10;
            try { seconds = ModConfig.LogValuesSeconds?.Value ?? 10; } catch { }
            if (seconds <= 0) return;

            float now = Time.unscaledTime;
            if (now < _nextValueLogAt) return;
            _nextValueLogAt = now + seconds;

            var sb = new System.Text.StringBuilder();
            sb.Append("[vitals] 场景=").Append(snap.SceneName);
            for (int i = 0; i < snap.Values.Count; i++)
            {
                var v = snap.Values[i];
                sb.Append(' ').Append(v.Key).Append('=');
                if (v.IsUsable) sb.Append(v.HasMax ? $"{v.Value:0.##}/{v.Max:0.##}" : $"{v.Value:0.##}");
                else sb.Append(v.State == ReadState.Failed ? "FAIL" : "-");
            }
            sb.Append(" 失败项=").Append(snap.FailCount());
            Log.Info(sb.ToString());
        }
    }
}
