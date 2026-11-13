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
        private static BasicRangedWeapon[] _weaponCache;
        private static float _nextWeaponCache;

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
                _nextTick = now + 0.40f;

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
        private static float _nextGunStatus;
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

                // 每帧执行：射击扣减可能发生在任意帧，250ms 轮询会留下可见下降窗口

                HumanCharacterController pl = PlayerAccessor.Resolve(out _, out _);
                if (pl == null) return;
                IntPtr pp = pl.Pointer;

                // 每帧 FindObjectsOfTypeAll 开销大：改为 1 秒刷新一次缓存
                BasicRangedWeapon[] arr = _weaponCache;
                if (arr == null || Time.unscaledTime >= _nextWeaponCache)
                {
                    _nextWeaponCache = Time.unscaledTime + 1f;
                    arr = Resources.FindObjectsOfTypeAll<BasicRangedWeapon>();
                    _weaponCache = arr;
                }
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

                        if (Time.unscaledTime >= _nextGunStatus)
                        {
                            _nextGunStatus = Time.unscaledTime + 1f;
                            try
                            {
                                var ps = wd.properties;
                                float d0 = (ps != null && ps.Length > 0) ? ps[0] : -1f;
                                Log.Info($"[dur] gun-status itemId={wd.itemId} weapon=0x{w.Pointer.ToInt64():X} " +
                                         $"weaponData=0x{wd.Pointer.ToInt64():X} props=0x{(ps == null ? 0 : ps.Pointer.ToInt64()):X} " +
                                         $"props[0]={d0:0.####} df={SafeDf(w)} writes={GunWriteCount}");
                            }
                            catch { }
                        }

                        // 反编译实证（RVA 0x146B8A0）：
                        //   durabilityFloat = weaponData.properties[0] / weaponAttr.durabilityMax
                        // 真实耐久 = ItemData.properties[0]（Single[] 首元素），不是 durability 字段。
                        var props = wd.properties;
                        if (props == null || props.Length <= 0) continue;

                        float cur = props[0];
                        if (cur <= 0f) continue;

                        IntPtr wdp = wd.Pointer;
                        if (wdp == IntPtr.Zero) continue;

                        // maxSeen：该 ItemData 实例观察到过的最高耐久
                        // （不再用 durabilityFloat 反推 —— 它可能被其它补丁覆盖，已被实证骗死过一次）
                        float max;
                        if (!_gunMaxSeen.TryGetValue(wdp, out max))
                        {
                            // 首次见到：用反编译实证的公式反推真实上限
                            //   durabilityFloat = props[0] / durabilityMax  =>  max = props[0] / durabilityFloat
                            // （v1.3.1 的 getter 覆盖已移除，此处 df 为游戏真实值）
                            float df0 = -1f;
                            try { df0 = w.durabilityFloat; } catch { }
                            max = (df0 > 0.001f && df0 <= 1.0001f) ? (cur / df0) : cur;
                            _gunMaxSeen[wdp] = max;
                            if (max > cur + 0.01f)
                                Log.Info($"[dur] gun-seed itemId={wd.itemId} props[0]={cur:0.###} df={df0:0.####} -> max={max:0.###}");
                            else
                                continue;
                        }

                        if (cur > max + 0.0001f)          // 修理/换新枪 → 抬高峰值
                        {
                            _gunMaxSeen[wdp] = cur;
                            continue;
                        }

                        if (cur >= max - 0.0001f) continue;   // 没有下降

                        float df = -1f;
                        try { df = w.durabilityFloat; } catch { }

                        props[0] = max;

                        // 写后立即回读验证（同帧）
                        float afterProps = props[0];
                        float afterDf = -1f;
                        try { afterDf = w.durabilityFloat; } catch { }
                        GunWriteCount++;

                        if (_gunLogBudget > 0)
                        {
                            _gunLogBudget--;
                            Log.Info($"[dur] gun-lock itemId={wd.itemId} " +
                                     $"weapon=0x{w.Pointer.ToInt64():X} weaponData=0x{wd.Pointer.ToInt64():X} props=0x{props.Pointer.ToInt64():X} len={props.Length} | " +
                                     $"props[0] {cur:0.####} -> {afterProps:0.####} | durabilityFloat {df:0.####} -> {afterDf:0.####} | max~{max:0.####}");
                        }

                        if (Math.Abs(afterProps - max) > 0.01f || afterDf < 0.99f)
                        {
                            Log.WarnOnce("dur.gun.noeffect",
                                $"[dur] gun-lock 写入未生效：props[0]={afterProps:0.####} df={afterDf:0.####}（props 类型可能不可写或对象为副本）");
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










