using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 敌人识别与目标选择（ESP / 自瞄 / 自动开火共用）。
    ///
    /// 识别方式参考创意工坊 mod「Enemy Health Bar」(3835884410)：
    ///   `Object.FindObjectsByType&lt;BasicCharacterController&gt;(FindObjectsSortMode.None)`
    ///   → 排除 `HumanCharacterController`（人类/玩家/友方 NPC）
    ///   → 排除 `isPlayer`、排除 `hp &lt;= 0`（死亡）
    ///   → 血量用 `hp` / `FullHP`
    ///
    /// 这样不需要去找"僵尸专用类型"，也不依赖怪物生成器。
    /// </summary>
    internal static class Targeting
    {
        internal sealed class Target
        {
            internal BasicCharacterController C;
            internal Vector3 Pos;
            internal float Dist;
            internal float Hp;
            internal float MaxHp;
            internal string Name = "Zombie";
            internal bool IsMonster;
            internal float Anim;   // 血条平滑用
        }

        private static readonly List<Target> _list = new List<Target>(128);
        private static float _next;
        private static int _lastCount;

        internal static IReadOnlyList<Target> All => _list;
        internal static int Count => _lastCount;

        internal static void Refresh(float maxDist)
        {
            float now = Time.unscaledTime * 1000f;
            if (now < _next) return;
            _next = now + (Features.PerformanceModeOn ? 400f : 180f);

            var keep = new List<Target>(_list.Count);
            try
            {
                HumanCharacterController player = null;
                Vector3 ppos = Vector3.zero;
                try
                {
                    player = PlayerAccessor.Resolve(out _, out _);
                    if (player != null) ppos = player.transform.position;
                }
                catch { }

                BasicCharacterController[] arr = null;
                try { arr = UnityEngine.Object.FindObjectsByType<BasicCharacterController>(FindObjectsSortMode.None); }
                catch (Exception e) { Log.ErrorOnce("target.find", e); return; }
                if (arr == null) { _list.Clear(); _lastCount = 0; return; }

                float lim = Mathf.Max(5f, maxDist);
                for (int i = 0; i < arr.Length; i++)
                {
                    var c = arr[i];
                    if (c == null) continue;
                    try
                    {
                        if (c.isPlayer) continue;
                        // 人类（含玩家/友方 NPC）不算敌人
                        if (c.TryCast<HumanCharacterController>() != null) continue;
                        // 友方判定（宠物/同伴等）
                        try { if (BasicCharacterController.IsPlayerSideCharacter(c)) continue; } catch { }

                        float hp = c.hp;
                        if (hp <= 0f) continue;

                        Vector3 pos = c.transform.position;
                        float d = Vector3.Distance(pos, ppos);
                        if (d > lim) continue;

                        float maxHp = 0f;
                        try { maxHp = c.FullHP; } catch { }
                        if (maxHp <= 0f) maxHp = Mathf.Max(hp, 1f);

                        bool monster = false;
                        try { if (c.TryCast<ZombieController>() != null) monster = true; } catch { }
                        try
                        {
                            if (!monster)
                            {
                                var tn = c.GetType().Name;
                                if (tn != null && tn.IndexOf("Zombie", StringComparison.OrdinalIgnoreCase) >= 0) monster = true;
                            }
                        }
                        catch { }
                        string nm = "Zombie";
                        try { nm = c.gameObject.name; } catch { }
                        if (nm != null && nm.Length > 24) nm = nm.Substring(0, 24);

                        keep.Add(new Target
                        {
                            C = c,
                            Pos = pos,
                            Dist = d,
                            Hp = hp,
                            MaxHp = maxHp,
                            Name = nm,
                            IsMonster = monster,
                            Anim = hp / maxHp
                        });
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.ErrorOnce("target.refresh", e); }

            _list.Clear();
            _list.AddRange(keep);
            _lastCount = _list.Count;
        }

        /// <summary>最近的敌人（按距离升序找第一个）。</summary>
        internal static bool TryGetNearest(Vector3 from, float maxDist, out Target best)
        {
            best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < _list.Count; i++)
            {
                var t = _list[i];
                if (t == null || t.C == null) continue;
                if (t.Dist > maxDist) continue;
                if (t.Dist < bd) { bd = t.Dist; best = t; }
            }
            return best != null;
        }

        /// <summary>最近的"怪物"（僵尸类），用于自瞄/自动开火，避免打到动物等中立生物。</summary>
        internal static bool TryGetNearestMonster(Vector3 from, float maxDist, out Target best)
        {
            best = null;
            float bd = float.MaxValue;
            for (int i = 0; i < _list.Count; i++)
            {
                var t = _list[i];
                if (t == null || t.C == null || !t.IsMonster) continue;
                if (t.Dist > maxDist) continue;
                if (t.Dist < bd) { bd = t.Dist; best = t; }
            }
            return best != null;
        }

        internal static void Clear()
        {
            _list.Clear();
            _lastCount = 0;
        }
    }
}



