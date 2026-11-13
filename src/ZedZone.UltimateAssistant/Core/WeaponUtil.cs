using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 武器相关的只读/低风险工具（玩家自己的武器数据）。
    ///
    /// 提供：
    ///  * 枚举玩家背包内的所有物品（用于耐久修复）；
    ///  * 耐久修复（**单调增加**：只在当前值低于阈值时提升，绝不降低）；
    ///  * 一键清除卡壳（遍历玩家武器组件，把 isMalfunction 置 false）；
    ///  * 维护"玩家武器 ItemData 指针集合"，供温度拦截精确判断归属。
    ///
    /// 安全：只处理本地玩家；不触碰 NPC/僵尸武器；所有操作 try/catch。
    /// </summary>
    internal static class WeaponUtil
    {
        /// <summary>耐久修复目标。只在当前值低于它时写入，因此只会变大。</summary>
        private const float DurabilityTarget = 100f;

        private static readonly HashSet<IntPtr> _playerWeapons = new HashSet<IntPtr>();
        private static float _nextRefresh;
        private static float _nextProbe;
        private static float _nextTemp;
        private static float _nextGunProbe;
        private static float _nextZeroCost;

        internal static int PlayerWeaponCount => _playerWeapons.Count;
        internal static int LastRepairCount { get; private set; }
        internal static int LastJamFixCount { get; private set; }

        /// <summary>节流刷新玩家武器指针集合（用于温度补丁精确匹配）。</summary>
        internal static void Refresh()
        {
            float now = Time.unscaledTime * 1000f;
            if (now < _nextRefresh) return;
            _nextRefresh = now + 1000f;

            try
            {
                _playerWeapons.Clear();
                var items = CollectPlayerItems();
                if (items == null) return;
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    try
                    {
                        if (it.durability > 0f || LooksLikeWeapon(it))
                            _playerWeapons.Add(it.Pointer);
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.ErrorOnce("weaponutil.refresh", e); }

            // 耐久开关：持续把携带物耐久补回（每次刷新最多执行一次）
                        try { ProbeDurabilityFields(); } catch { }
            try { ResetGunTemperature(); } catch { }
            try { ProbeHeldWeapon(); } catch { }
                    }

        /// <summary>该 ItemData 是否属于玩家携带物（用于耐久扣除拦截）。</summary>
        internal static bool IsPlayerItem(IntPtr ptr)
        {
            try { return ptr != IntPtr.Zero && _playerWeapons.Contains(ptr); }
            catch { return false; }
        }

        internal static bool IsPlayerWeapon(IntPtr ptr)
        {
            try { return ptr != IntPtr.Zero && _playerWeapons.Contains(ptr); }
            catch { return false; }
        }

        private static bool LooksLikeWeapon(ItemData it)
        {
            try
            {
                // 有耐久且非 0 的物品大多是可损坏装备/武器；这里只做"是否纳入集合"的宽松判断
                return it.durability > 0f;
            }
            catch { return false; }
        }

        /// <summary>收集玩家携带物品（含背包递归）。</summary>
        internal static List<ItemData> CollectPlayerItems()
        {
            var result = new List<ItemData>();
            try
            {
                HumanCharacterController p = PlayerAccessor.Resolve(out _, out _);
                if (p == null) return result;

                var cd = p.characterData;
                if (cd == null) return result;

                // 1) 主背包 + 背包内嵌套（游戏自带递归方法）
                try
                {
                    var root = cd.inventoryData;
                    if (root != null)
                    {
                        var all = root.GetAllItemsIncludeBackpack();
                        if (all != null)
                            for (int i = 0; i < all.Count; i++) if (all[i] != null) result.Add(all[i]);
                    }
                }
                catch (Exception e) { Log.WarnOnce("weaponutil.all", "[weapons] GetAllItemsIncludeBackpack 失败: " + e.GetType().Name); }

                // 1.5) 装备槽（手持武器/照明/特殊装备等不一定在背包列表里）
                try
                {
                    var eq = cd.characterEquipmentData;
                    if (eq != null)
                    {
                        void AddEq(ItemData x) { if (x != null) result.Add(x); }
                        AddEq(eq.currentWeaponItemData);
                        AddEq(eq.lightingToolItemData);
                        AddEq(eq.specialEquipmentItemData);
                        AddEq(eq.throwableItemData);
                        AddEq(eq.hatItemData);
                        AddEq(eq.torseItemData);
                        AddEq(eq.legItemData);
                        AddEq(eq.feetItemData);
                        AddEq(eq.shieldItemData);
                    }
                }
                catch { }

                // 2) 所有携带容器（含装备容器）
                try
                {
                    var invs = cd.GetCarriedInventoryList();
                    if (invs != null)
                    {
                        for (int i = 0; i < invs.Count; i++)
                        {
                            var inv = invs[i];
                            if (inv == null) continue;
                            var list = inv.itemList;
                            if (list == null) continue;
                            for (int j = 0; j < list.Count; j++)
                            {
                                var it = list[j];
                                if (it != null) result.Add(it);
                            }
                        }
                    }
                }
                catch { }
            }
            catch (Exception e) { Log.ErrorOnce("weaponutil.items", e); }
            return result;
        }

        /// <summary>修复玩家携带物品的耐久（单调增加，绝不降低）。</summary>
        
        private static float _nextDiag;

        /// <summary>
        /// 用游戏**原生接口**恢复耐久：
        /// `InventoryData.RestoreItemDurability(itemId, amount, inventories)`。
        /// （实测 ItemData.durability 字段对 271 件物品恒为 100，不是真实耐久来源，
        ///   因此改为调用游戏自己的修复函数。）
        /// </summary>
        /// <summary>
        /// 耐久字段探针：每 3 秒打印一次前 12 件物品的候选耐久字段，
        /// 用于定位"游戏真正使用的耐久存储位置"（ItemData.durability 恒为 100，已证伪）。
        /// </summary>
        private sealed class ItemSig
        {
            internal int Id;
            internal float Dur;
            internal float Num;
            internal string Props = "";
            internal string Text => $"#{Id}(dur={Dur:0.###},num={Num:0.###},props={Props})";
        }

        private static readonly Dictionary<IntPtr, ItemSig> _prevSigs = new Dictionary<IntPtr, ItemSig>();
        private static bool _propsDumped;

        internal static void ProbeDurabilityFields()
        {
            try
            {
                if (!Features.WeaponDurabilityOn) return;
                if (Time.unscaledTime < _nextProbe) return;
                _nextProbe = Time.unscaledTime + 2f;

                var items = CollectPlayerItems();
                int changed = 0;
                var sb = new System.Text.StringBuilder();

                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    try
                    {
                        var sig = new ItemSig
                        {
                            Id = it.itemId,
                            Dur = it.durability,
                            Num = it.itemNumberFloat,
                            Props = PropsOf(it)
                        };
                        IntPtr ptr = it.Pointer;

                        // 首次：导出前 5 件的属性键，便于发现耐久存在 key/value 里
                        if (!_propsDumped && i < 5)
                            Log.Info($"[durprobe] props item#{sig.Id}: {sig.Props}");

                        if (_prevSigs.TryGetValue(ptr, out var oldSig))
                        {
                            if (oldSig.Id != sig.Id || Math.Abs(oldSig.Dur - sig.Dur) > 0.0001f ||
                                Math.Abs(oldSig.Num - sig.Num) > 0.0001f || oldSig.Props != sig.Props)
                            {
                                changed++;
                                if (changed <= 8)
                                    sb.Append("CHANGE ").Append(oldSig.Text).Append(" -> ").Append(sig.Text).Append(" | ");
                            }
                        }
                        _prevSigs[ptr] = sig;
                    }
                    catch { }
                }

                if (!_propsDumped && items.Count > 0) _propsDumped = true;

                if (changed > 0)
                    Log.Info($"[durprobe] 变化 {changed} 件 | " + sb.ToString());
            }
            catch (Exception e) { Log.ErrorOnce("weaponutil.probe", e); }
        }

        /// <summary>
        /// 无限耐久（组件层）：把玩家武器的「每枪耐久消耗」字段 m_gunpartDurabilityCostFloat 置 0。
        /// 这是 BasicRangedWeapon 上唯一可写的耐久相关字段（durabilityFloat 只有 getter）。
        /// </summary>
        internal static int ZeroDurabilityCost()
        {
            int n = 0;
            try
            {
                if (!Features.WeaponDurabilityOn) return 0;
                if (Time.unscaledTime < _nextZeroCost) return 0;
                _nextZeroCost = Time.unscaledTime + 0.2f;

                HumanCharacterController pl = PlayerAccessor.Resolve(out _, out _);
                if (pl == null) return 0;
                var pp = pl.Pointer;

                var arr = _jamWeaponCache;
                if (arr == null || Time.unscaledTime >= _nextJamCache)
                {
                    _nextJamCache = Time.unscaledTime + 1f;
                    arr = Resources.FindObjectsOfTypeAll<BasicRangedWeapon>();
                    _jamWeaponCache = arr;
                }
                if (arr == null) return 0;

                for (int i = 0; i < arr.Length; i++)
                {
                    var w = arr[i];
                    if (w == null) continue;
                    try
                    {
                        var ch = w.character;
                        if (ch == null || ch.Pointer != pp) continue;
                        if (Math.Abs(w.m_gunpartDurabilityCostFloat) > 0.0001f)
                        {
                            w.m_gunpartDurabilityCostFloat = 0f;
                            n++;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.ErrorOnce("weaponutil.zerocost", e); }
            if (n > 0) Log.Info($"[weapons] 已把 {n} 把武器的耐久消耗置 0");
            return n;
        }

        private static float _nextJamClear;
        private static BasicRangedWeapon[] _jamWeaponCache;
        private static float _nextJamCache;
        private static int _jamClearLog;

        /// <summary>
        /// 不会卡壳：清除已存在的卡壳状态。
        ///
        /// 依据（实机诊断日志，非猜测）：
        ///  * 卡壳状态存放在武器 ItemData 的属性 `MalfunctionFlag`（实测在卡壳时变成 "true"，
        ///    恢复后回到 "false"）；
        ///  * `BasicRangedWeapon.CoolDownOnMalfunctionCleared(ItemData)` 是公开静态方法，
        ///    语义为"卡壳清除后的冷却处理"。
        /// 因此：检测到 MalfunctionFlag=true → 置回 "False" 并调用冷却方法。
        /// </summary>
        internal static int ClearMalfunction()
        {
            if (!Features.NoJamOn) return 0;
            if (Time.unscaledTime < _nextJamClear) return 0;
            _nextJamClear = Time.unscaledTime + 0.2f;

            int n = 0;
            try
            {
                HumanCharacterController pl = PlayerAccessor.Resolve(out _, out _);
                if (pl == null) return 0;
                IntPtr pp = pl.Pointer;

                var arr = _jamWeaponCache;
                if (arr == null || Time.unscaledTime >= _nextJamCache)
                {
                    _nextJamCache = Time.unscaledTime + 1f;
                    arr = Resources.FindObjectsOfTypeAll<BasicRangedWeapon>();
                    _jamWeaponCache = arr;
                }
                if (arr == null) return 0;

                for (int i = 0; i < arr.Length; i++)
                {
                    var w = arr[i];
                    if (w == null) continue;
                    try
                    {
                        var ch = w.character;
                        if (ch == null || ch.Pointer != pp) continue;

                        var wd = w.weaponData;
                        if (wd == null) continue;

                        string flag = null;
                        try { flag = wd.GetProperty("MalfunctionFlag"); } catch { }
                        if (string.IsNullOrEmpty(flag)) continue;
                        if (!flag.Equals("true", StringComparison.OrdinalIgnoreCase)) continue;

                        wd.SetProperty("MalfunctionFlag", "False");
                        try { BasicRangedWeapon.CoolDownOnMalfunctionCleared(wd); } catch { }
                        n++;
                        if (_jamClearLog < 5)
                        {
                            _jamClearLog++;
                            Log.Info($"[jam] 已清除卡壳标记：itemId={wd.itemId}（MalfunctionFlag true -> False）");
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.ErrorOnce("jam.clear", e); }
            return n;
        }

        /// <summary>专盯手持武器的探针：全部属性 + 组件 durabilityFloat。</summary>
        internal static void ProbeHeldWeapon()
        {
            try
            {
                if (!Features.WeaponDurabilityOn) return;
                if (Time.unscaledTime < _nextGunProbe) return;
                _nextGunProbe = Time.unscaledTime + 2f;

                HumanCharacterController pl = PlayerAccessor.Resolve(out _, out _);
                if (pl == null) return;
                var cd = pl.characterData;
                var eq = cd?.characterEquipmentData;
                var w = eq?.currentWeaponItemData;

                string itemInfo = "none";
                if (w != null)
                {
                    string props = AllProps(w);
                    itemInfo = $"id={w.itemId} dur={w.durability:0.###} num={w.itemNumberFloat:0.###} props[{props}]";
                }

                string comp = "none";
                try
                {
                    var arr = Resources.FindObjectsOfTypeAll<BasicRangedWeapon>();
                    if (arr != null)
                    {
                        for (int i = 0; i < arr.Length; i++)
                        {
                            var wr = arr[i];
                            if (wr == null) continue;
                            var ch = wr.character;
                            if (ch == null || ch.Pointer != pl.Pointer) continue;
                            float df = -1f;
                            try { df = wr.durabilityFloat; } catch { }
                            comp = $"durabilityFloat={df:0.####} malf={SafeBool(wr)} name={wr.gameObject.name}";
                            break;
                        }
                    }
                }
                catch { }

                Log.Info($"[gun] {itemInfo} | {comp}");
            }
            catch (Exception e) { Log.ErrorOnce("weaponutil.gun", e); }
        }

        private static string SafeBool(BasicRangedWeapon w)
        {
            try { return w.isMalfunction.ToString(); } catch { return "?"; }
        }

        private static string AllProps(ItemData it)
        {
            try
            {
                var pairs = it.itemPropertyPairs;
                if (pairs == null || pairs.Count == 0) return "-";
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < pairs.Count && i < 12; i++)
                {
                    var kv = pairs[i];
                    if (kv == null) continue;
                    string k = "?", v = "?";
                    try { k = kv.key; } catch { }
                    try { v = kv.value; } catch { }
                    if (sb.Length > 0) sb.Append(',');
                    sb.Append(k).Append('=').Append(v);
                }
                return sb.ToString();
            }
            catch { return "?"; }
        }

        private static string PropsOf(ItemData it)
        {
            try
            {
                var pairs = it.itemPropertyPairs;
                if (pairs == null || pairs.Count == 0) return "-";
                var sb = new System.Text.StringBuilder();
                int n = 0;
                for (int i = 0; i < pairs.Count && n < 6; i++)
                {
                    var kv = pairs[i];
                    if (kv == null) continue;
                    string k = "?", v = "?";
                    try { k = kv.key; } catch { }
                    try { v = kv.value; } catch { }
                    if (n > 0) sb.Append(',');
                    sb.Append(k).Append('=').Append(v);
                    n++;
                }
                return sb.ToString();
            }
            catch { return "?"; }
        }

                /// <summary>取消过热：把玩家武器的 GunTemperature 属性写回 0。</summary>
        internal static void ResetGunTemperature()
        {
            try
            {
                if (!Features.NoOverheatOn) return;
                if (Time.unscaledTime < _nextTemp) return;
                _nextTemp = Time.unscaledTime + 1f;

                var items = CollectPlayerItems();
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    try
                    {
                        string t = it.GetProperty("GunTemperature");
                        if (string.IsNullOrEmpty(t)) continue;
                        if (float.TryParse(t, out float v) && v > 0.01f)
                            it.SetProperty("GunTemperature", "0");
                    }
                    catch { }
                }
            }
            catch { }
        }

        internal static int RestoreAllDurability()
        {
            int touched = 0;
            float totalRestored = 0f;
            try
            {
                HumanCharacterController p = PlayerAccessor.Resolve(out _, out _);
                if (p == null) return 0;
                var cd = p.characterData;
                if (cd == null) return 0;

                var invs = cd.GetCarriedInventoryList();
                if (invs == null) return 0;

                var items = CollectPlayerItems();
                var seen = new HashSet<int>();

                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    int id;
                    try { id = it.itemId; } catch { continue; }
                    if (!seen.Add(id)) continue;

                    try
                    {
                        float got = InventoryData.RestoreItemDurability(id, 999f, invs);
                        if (got > 0.001f) { touched++; totalRestored += got; }
                    }
                    catch (Exception e) { Log.WarnOnce("weaponutil.restore." + id, "[weapons] 修复失败 id=" + id + ": " + e.GetType().Name); }
                }
            }
            catch (Exception e) { Log.ErrorOnce("weaponutil.restore", e); }

            LastRepairCount = touched;
            Log.Info($"[weapons] 原生耐久修复：涉及 {touched} 种物品，共恢复 {totalRestored:0.###}");
            return touched;
        }

        internal static int RepairDurability()
        {
            int n = 0, scanned = 0, withDur = 0;
            try
            {
                var items = CollectPlayerItems();
                scanned = items.Count;

                bool wantDiag = Time.unscaledTime >= _nextDiag;
                if (wantDiag) _nextDiag = Time.unscaledTime + 10f;

                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    try
                    {
                        float d = it.durability;
                        int id = it.itemId;
                        if (d > 0f) withDur++;

                        if (wantDiag && i < 10)
                        {
                            string prop = "n/a";
                            try { prop = it.GetProperty("durability") ?? "null"; } catch { }
                            Log.Info($"[weapons] 诊断 item[{i}] id={id} durability={d:0.###} prop(durability)={prop}");
                        }

                        if (d > 0f && d < DurabilityTarget)
                        {
                            it.durability = DurabilityTarget;
                            float after = it.durability;
                            if (wantDiag && i < 10)
                                Log.Info($"[weapons] 诊断写入 item[{i}] id={id} {d:0.###} → {after:0.###}");
                            n++;
                        }
                    }
                    catch (Exception e) { Log.WarnOnce("weaponutil.item", "[weapons] 单件处理异常: " + e.GetType().Name); }
                }

                if (wantDiag)
                    Log.Info($"[weapons] 诊断：扫描 {scanned} 件，含耐久 {withDur} 件，本次修复 {n} 件（目标 {DurabilityTarget}）");
            }
            catch (Exception e) { Log.ErrorOnce("weaponutil.repair", e); }

            LastRepairCount = n;
            return n;
        }

        /// <summary>
        /// 一键修复卡壳（兜底方案）。
        ///
        /// 说明：游戏 1.0.17 中 `isMalfunction` 是**只读属性**，`WeaponMalfunction()` 是**私有方法**，
        /// 没有公开 API 可以直接清除卡壳标志。因此这里采用「开火闸门兜底」：
        /// 打开 JamBypass 后，卡壳状态不再阻止开火（按 R 换弹、其它逻辑不受影响）。
        /// </summary>
        internal static int FixJams()
        {
            int jammed = 0;
            try
            {
                HumanCharacterController p = PlayerAccessor.Resolve(out _, out _);
                if (p != null)
                {
                    var pp = p.Pointer;
                    var weapons = Resources.FindObjectsOfTypeAll<BasicRangedWeapon>();
                    if (weapons != null)
                    {
                        for (int i = 0; i < weapons.Length; i++)
                        {
                            var w = weapons[i];
                            if (w == null) continue;
                            try
                            {
                                var c = w.character;
                                if (c == null || c.Pointer != pp) continue;
                                if (w.isMalfunction) jammed++;
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception e) { Log.ErrorOnce("weaponutil.jam.scan", e); }

                        // 真正清除卡壳：卡壳状态存在 ItemData 属性 MalfunctionFlag 中
            int cleared = 0;
            try
            {
                var items = CollectPlayerItems();
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    if (it == null) continue;
                    try
                    {
                        string flag = null;
                        try { flag = it.GetProperty("MalfunctionFlag"); } catch { }
                        if (string.IsNullOrEmpty(flag)) continue;
                        if (flag.Equals("true", StringComparison.OrdinalIgnoreCase))
                        {
                            it.SetProperty("MalfunctionFlag", "False");
                            cleared++;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.WarnOnce("weaponutil.jam.prop", "[weapons] 属性清卡壳失败: " + e.GetType().Name); }
            if (cleared > 0) Log.Info($"[weapons] 已通过属性清除卡壳：{cleared} 件");

            try { Features.SetJamBypass(true); } catch { }

            LastJamFixCount = jammed;
            Log.Info($"[weapons] 修复卡壳（兜底）：检测到 {jammed} 把卡壳武器；已开启「卡壳不阻止开火」。" +
                     "游戏未提供清除卡壳状态的公开接口，故以开火闸门兜底。");
            return jammed;
        }

        internal static string StatusText()
            => $"武器={_playerWeapons.Count} 耐久修复={(Features.WeaponDurabilityOn ? "开" : "关")} 过热={(Features.NoOverheatOn ? "已取消" : "正常")}";
    }
}



















