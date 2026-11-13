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


