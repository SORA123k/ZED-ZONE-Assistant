using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 自动补充材料（补货）：在修复/建造判定材料前，把**缺的那些材料**补进玩家背包。
    ///
    /// 说明（诚实边界）：
    ///  * 游戏没有暴露"按 itemId 新建物品"的公开工厂；
    ///  * 因此实现方式为：从玩家已有物品中**克隆同 itemId 的 ItemData**，把数量补齐后放入背包；
    ///  * 若背包里连一个该物品都没有（模板缺失），无法凭空创建，会在日志中明确记录而不伪装成功。
    /// </summary>
    internal static class AutoSupply
    {
        private static int _log;
        private static float _nextRefill;

        internal static int LastFilledCount { get; private set; }

        /// <summary>补齐给定材料清单（itemId + 数量）。返回实际补齐的条目数。</summary>
        internal static int EnsureMaterials(List<int> ids, List<int> numbers)
        {
            if (!Features.AutoSupplyOn) return 0;
            if (Time.unscaledTime < _nextRefill) return 0;
            _nextRefill = Time.unscaledTime + 0.2f;

            int filled = 0;
            try
            {
                HumanCharacterController pl = PlayerAccessor.Resolve(out _, out _);
                if (pl == null) return 0;
                var cd = pl.characterData;
                if (cd == null) return 0;
                var inv = cd.inventoryData;
                if (inv == null) return 0;

                for (int i = 0; i < ids.Count && i < numbers.Count; i++)
                {
                    try
                    {
                        int id = ids[i];
                        int need = numbers[i];
                        if (id <= 0 || need <= 0) continue;

                        float have = SumById(inv, id);
                        if (have >= need - 0.0001f) continue;

                        int missing = Mathf.CeilToInt(need - have);
                        if (missing <= 0) continue;

                        // 找一个同 itemId 的现有物品作为克隆模板
                        var list = inv.GetItemListById(id);
                        ItemData src = null;
                        if (list != null && list.Count > 0)
                        {
                            for (int k = 0; k < list.Count; k++)
                            {
                                var it = list[k];
                                if (it != null) { src = it; break; }
                            }
                        }

                        ItemData clone = null;
                        if (src != null)
                        {
                            clone = src.CopyItemData(null);
                        }
                        else
                        {
                            // 背包里没有该物品：尝试用 ItemManager 的物品表按 itemId 生成
                            try
                            {
                                var mgr = ItemManager.instance;
                                if (mgr != null)
                                {
                                    var all = mgr.itemList;
                                    if (all != null)
                                    {
                                        for (int k = 0; k < all.Count; k++)
                                        {
                                            var attr = all[k];
                                            if (attr == null) continue;
                                            if (attr.itemId != id) continue;
                                            clone = mgr.GetRandomDataByAttr(attr, false, true);
                                            break;
                                        }
                                    }
                                }
                            }
                            catch (Exception e) { Log.WarnOnce("supply.mgr", "[supply] ItemManager 生成失败: " + e.GetType().Name); }
                        }

                        if (clone == null)
                        {
                            Log.WarnOnce("supply.tpl." + id,
                                $"[supply] 无法生成 itemId={id}（无模板且物品表不可用），跳过");
                            continue;
                        }
                        if (clone == null) continue;
                        clone.itemNumberFloat = missing;

                        int added = inv.AddItem(clone, true);
                        if (added > 0)
                        {
                            filled++;
                            if (_log < 8)
                            {
                                _log++;
                                Log.Info($"[supply] 自动补货 itemId={id} +{missing}（现有 {have:0.##}/{need}）");
                            }
                        }
                    }
                    catch (Exception e) { Log.WarnOnce("supply.item", "[supply] 补货单条失败: " + e.GetType().Name); }
                }
            }
            catch (Exception e) { Log.ErrorOnce("supply.ensure", e); }

            LastFilledCount = filled;
            return filled;
        }

        /// <summary>把缺少的材料直接生成到玩家脚下（地面物品）。</summary>
        internal static bool TrySpawnGround(int itemId, int number)
        {
            if (!Features.AutoSupplyOn) return false;
            if (itemId <= 0 || number <= 0) return false;
            try
            {
                HumanCharacterController pl = PlayerAccessor.Resolve(out _, out _);
                if (pl == null) return false;
                if (GameController.instance == null) return false;

                ItemData data = MakeItem(itemId, number);
                if (data == null) { Log.WarnOnce("supply.gnd." + itemId, $"[supply] 无模板，无法生成 itemId={itemId}"); return false; }

                var pos = pl.transform.position;
                float ang = UnityEngine.Random.Range(0f, 360f);
                var go = GameController.instance.CreateItem(data, new Vector2(pos.x + 0.6f, pos.y + 0.2f), ang, -128);
                if (go != null)
                {
                    if (_log < 8) { _log++; Log.Info($"[supply] 已在地面生成 itemId={itemId} x{number}"); }
                    return true;
                }
                return false;
            }
            catch (Exception e) { Log.WarnOnce("supply.gnd", "[supply] 地面生成失败: " + e.GetType().Name); return false; }
        }

        /// <summary>优先克隆背包同 id 物品；否则用 ItemManager 物品表按 id 生成。</summary>
        private static ItemData MakeItem(int itemId, int number)
        {
            try
            {
                HumanCharacterController pl = PlayerAccessor.Resolve(out _, out _);
                var inv = pl?.characterData?.inventoryData;
                if (inv != null)
                {
                    var list = inv.GetItemListById(itemId);
                    if (list != null && list.Count > 0)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var it = list[i];
                            if (it == null) continue;
                            var clone = it.CopyItemData(null);
                            if (clone != null) { clone.itemNumberFloat = number; return clone; }
                        }
                    }
                }
            }
            catch { }

            try
            {
                var mgr = ItemManager.instance;
                if (mgr != null)
                {
                    var all = mgr.itemList;
                    if (all != null)
                    {
                        for (int i = 0; i < all.Count; i++)
                        {
                            var attr = all[i];
                            if (attr == null || attr.itemId != itemId) continue;
                            var data = mgr.GetRandomDataByAttr(attr, false, true);
                            if (data != null) { data.itemNumberFloat = number; return data; }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static float _nextBoatTick;
        private static float _nextBoatDiag;
        private static float _nextBoatDiag2;
        private static readonly System.Collections.Generic.Dictionary<int, float> _lastSpawn = new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>
        /// 岛屿船（TOD）修复补货：
        /// 用 MeleeWeapon_RepairHammer.ComputeTodRepairPlan 计算该 TerrainObject 的修复成本，
        /// 再把玩家缺少的材料直接生成到船边地上。
        /// 仅处理玩家 8 米内的 TerrainObject，1 秒节流。
        /// </summary>
        internal static void TickBoat()
        {
            try
            {
                if (!Features.AutoSupplyOn) return;
                if (Time.unscaledTime < _nextBoatTick) return;
                _nextBoatTick = Time.unscaledTime + 1f;

                HumanCharacterController pl = PlayerAccessor.Resolve(out _, out _);
                if (pl == null) return;
                Vector3 ppos = pl.transform.position;

                var tods = Resources.FindObjectsOfTypeAll<TerrainObject>();
                if (tods == null) { Log.WarnOnce("supply.tods", "[supply] TerrainObject 枚举为空"); return; }

                // 诊断：最近 TOD 距离 + 邻近 TOD 的成本数量
                int near = 0; float nearest = 999f; int nearestIdx = -1;
                for (int i = 0; i < tods.Length; i++)
                {
                    var t = tods[i];
                    if (t == null) continue;
                    float d = 999f;
                    try { d = Vector3.Distance(t.transform.position, ppos); } catch { continue; }
                    if (d < nearest) { nearest = d; nearestIdx = i; }
                    if (d <= 8f) near++;
                }
                if (Time.unscaledTime >= _nextBoatDiag)
                {
                    _nextBoatDiag = Time.unscaledTime + 3f;
                    Log.Info($"[supply] TOD 总数={tods.Length} 8米内={near} 最近距离={nearest:0.##}");
                }

                for (int i = 0; i < tods.Length; i++)
                {
                    var tod = tods[i];
                    if (tod == null) continue;
                    try
                    {
                        var tp = tod.transform.position;
                        if (Vector3.Distance(tp, ppos) > 8f) continue;

                        var plan = MeleeWeapon_RepairHammer.ComputeTodRepairPlan(tod);
                        var costs = plan.costs;
                        if (Time.unscaledTime >= _nextBoatDiag2 && i == nearestIdx)
                        {
                            _nextBoatDiag2 = Time.unscaledTime + 3f;
                            Log.Info($"[supply] 最近TOD(0x{tod.Pointer.ToInt64():X}) costs={(costs == null ? -1 : costs.Count)} 距离={Vector3.Distance(tp, ppos):0.##}");
                            if (costs != null)
                            {
                                for (int q = 0; q < costs.Count && q < 10; q++)
                                {
                                    var ce = costs[q];
                                    Log.Info($"[supply]   cost[{q}] itemId={ce.itemId} x{ce.count}");
                                }
                            }
                        }
                        if (costs == null || costs.Count == 0) continue;

                        var inv = pl.characterData?.inventoryData;
                        if (inv == null) continue;

                        for (int k = 0; k < costs.Count; k++)
                        {
                            var e = costs[k];
                            if (e.itemId <= 0 || e.count <= 0) continue;
                            float have = SumById(inv, e.itemId);
                            if (have >= e.count) continue;
                            int need = Mathf.CeilToInt(e.count - have);
                            if (_lastSpawn.TryGetValue(e.itemId, out float last) && Time.unscaledTime - last < 3f) continue;
                            _lastSpawn[e.itemId] = Time.unscaledTime;
                            Log.Info($"[supply] 岛屿船需求 itemId={e.itemId} need={e.count} have={have:0.#} → 生成 +{need}");
                            TrySpawnGround(e.itemId, need);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.WarnOnce("supply.boat", "[supply] 岛屿船补货异常: " + e.GetType().Name); }
        }

        private static float SumById(InventoryData inv, int id)
        {
            try
            {
                var list = inv.GetItemListById(id);
                if (list == null) return 0f;
                float sum = 0f;
                for (int i = 0; i < list.Count; i++)
                {
                    var it = list[i];
                    if (it != null) sum += it.itemNumberFloat;
                }
                return sum;
            }
            catch { return 0f; }
        }
    }
}






