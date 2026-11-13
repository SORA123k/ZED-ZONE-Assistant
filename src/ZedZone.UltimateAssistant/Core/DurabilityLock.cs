using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// Phase 7：普通工具 / 近战武器 / 装备的耐久锁定。
    ///
    /// 依据（已实测，见 Diagnostics\DURABILITY_DIAGNOSTIC_REPORT.md）：
    ///   `ItemData.durability` 是这类物品的**真实耐久**：
    ///   - 近战 itemId 321：100 → 99 → 98 → 96 … → 92
    ///   - lightingTool itemId 92：95.7391
    ///   - feet itemId 10013：54.2904
    ///   而枪械（461/410/420/460）该字段恒为 100，说明锁定对枪械不产生写入（天然安全）。
    ///
    /// 策略：**只增不减**
    ///   * 为每个物品实例记录"已观察到的最高耐久"；
    ///   * 当当前值低于该最高值时，写回最高值（不会超过玩家曾拥有过的耐久）；
    ///   * 自然修理/恢复抬高耐久时，同步抬高峰值；
    ///   * 冷却 250ms，只在检测到下降时写入，避免高频写。
    ///
    /// 安全边界：
    ///   * 只处理玩家携带/装备的物品；
    ///   * 不修改任何"最大耐久"（游戏未暴露该字段）；
    ///   * 场景切换清空基线；物品失效/销毁时跳过并清理；
    ///   * 写回值永远来自实测值，不猜测、不放大。
    /// </summary>
    internal static class DurabilityLock
    {
        private static readonly Dictionary<IntPtr, float> _maxSeen = new Dictionary<IntPtr, float>();
        private static float _nextTick;
        private static string _lastScene = "";
        private static int _logBudget = 20;

        internal static int RestoredCount { get; private set; }
        internal static int TrackedCount => _maxSeen.Count;
        internal static int WriteCount { get; private set; }

        internal static void Reset()
        {
            _maxSeen.Clear();
            _gunMaxSeen.Clear();
            RestoredCount = 0;
        }

        internal static void Tick()
        {
            try
            {
                if (!Features.ItemDurabilityLockOn) return;

                float now = Time.unscaledTime;
                if (now < _nextTick) return;
                _nextTick = now + 0.25f;

                // 场景切换：基线作废
                try
                {
                    string sc = SceneManager.GetActiveScene().name;
                    if (sc != _lastScene)
                    {
                        _lastScene = sc;
                        _maxSeen.Clear();
                    }
                }
                catch { }

                if (_maxSeen.Count > 4000) _maxSeen.Clear();   // 防御性上限

                var items = WeaponUtil.CollectPlayerItems();
                int restored = 0;

                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    try
                    {
                        float d = it.durability;
                        if (d <= 0f) continue;                 // 无耐久 / 已损坏物品不处理

                        IntPtr ptr = it.Pointer;
                        if (ptr == IntPtr.Zero) continue;

                        if (!_maxSeen.TryGetValue(ptr, out float max))
                        {
                            _maxSeen[ptr] = d;                  // 首次见到：以当前值为基线
                            continue;
                        }

                        if (d > max + 0.0001f)
                        {
                            _maxSeen[ptr] = d;                  // 修理/自然恢复 → 抬高峰值
                            continue;
                        }

                        if (d < max - 0.0001f)
                        {
                            it.durability = max;                // 写回曾观察到的最高值
                            restored++;
                            WriteCount++;
                        }
                    }
                    catch { }
                }

                if (restored > 0)
                {
                    RestoredCount += restored;
                    if (_logBudget > 0)
                    {
                        _logBudget--;
                        Log.Info($"[dur] 工具/近战耐久锁定：本次恢复 {restored} 件（累计 {RestoredCount} 次写入，跟踪 {_maxSeen.Count} 件）");
                    }
                }
            }
            catch (Exception e)
            {
                Log.ErrorOnce("dur.lock", e);
            }
        }

        // ---------------- Phase 8：枪械耐久（基于组件持有的 weaponData） ----------------
        private static readonly Dictionary<IntPtr, float> _gunMaxSeen = new Dictionary<IntPtr, float>();
        private static float _nextGunTick;
        private static int _gunLogBudget = 20;

        internal static int GunWriteCount { get; private set; }

        /// <summary>
        /// 枪械耐久锁定（保守实现）。
        ///
        /// 证据基础：
        ///  * `BasicRangedWeapon.durabilityFloat` 只有 getter（CS0200），116 个实例字段中无耐久存储字段；
        ///  * 组件持有 `weaponData : ItemData`（offset=0x28），这是武器组件自己引用的 ItemData 实例；
        ///  * `ItemData.durability` 是可写字段，且在近战/工具上已实测有效。
        ///
        /// 因此策略与 Phase 7 完全一致：**只增不减**
        ///  * 记录每个 weaponData 实例曾出现过的最高 durability；
        ///  * 仅当当前值低于该最高值时写回（不会超过玩家曾拥有过的值）；
        ///  * 若该字段与 durabilityFloat 无关（当前观测恒 100），则不会产生任何写入 —— 天然无害。
        /// </summary>
        internal static void TickGuns()
        {
            try
            {
                if (!Features.WeaponDurabilityOn) return;

                float now = Time.unscaledTime;
                if (now < _nextGunTick) return;
                _nextGunTick = now + 0.25f;

                HumanCharacterController pl = PlayerAccessor.Resolve(out _, out _);
                if (pl == null) return;
                IntPtr pp = pl.Pointer;

                var arr = Resources.FindObjectsOfTypeAll<BasicRangedWeapon>();
                if (arr == null) return;

                for (int i = 0; i < arr.Length; i++)
                {
                    var w = arr[i];
                    if (w == null) continue;
                    try
                    {
                        BasicCharacterController ch = w.character;
                        if (ch == null || ch.Pointer != pp) continue;

                        ItemData wd = w.weaponData;
                        if (wd == null) continue;

                        float d = wd.durability;
                        if (d <= 0f) continue;

                        IntPtr wdp = wd.Pointer;
                        if (wdp == IntPtr.Zero) continue;

                        if (!_gunMaxSeen.TryGetValue(wdp, out float max))
                        {
                            _gunMaxSeen[wdp] = d;
                            continue;
                        }
                        if (d > max + 0.0001f) { _gunMaxSeen[wdp] = d; continue; }
                        if (d < max - 0.0001f)
                        {
                            wd.durability = max;
                            GunWriteCount++;
                            if (_gunLogBudget > 0)
                            {
                                _gunLogBudget--;
                                Log.Info($"[dur] 枪械耐久锁定：itemId={wd.itemId} {d:0.##} -> {max:0.##}" +
                                         $"（durabilityFloat={SafeDf(w)}）");
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.ErrorOnce("dur.gun", e); }
        }

        private static string SafeDf(BasicRangedWeapon w)
        {
            try { return w.durabilityFloat.ToString("0.####"); } catch { return "?"; }
        }

        internal static string StatusText()
            => $"普通耐久锁定={(Features.ItemDurabilityLockOn ? "开" : "关")} 跟踪={_maxSeen.Count}";
    }
}

