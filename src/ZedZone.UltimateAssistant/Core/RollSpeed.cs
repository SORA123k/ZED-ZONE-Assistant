using System;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 翻滚加速：让翻滚的实际位移速度也跟随「移动加速」倍率。
    ///
    /// 依据：`HumanCharacterController.rollingMoveSpeed` 是翻滚期间的移动速度字段（interop 已确认存在），
    /// `isRolling` 是翻滚状态。
    ///
    /// 做法：仅当移动加速开启且倍率 &gt; 1 时，在翻滚期间把该字段设为「基线 × 倍率」；
    /// 翻滚结束立即还原基线，关闭开关也会还原。不改动画速度、不改体力消耗与冷却。
    /// </summary>
    internal static class RollSpeed
    {
        private static float _base = -1f;
        private static bool _modified;

        internal static void Tick()
        {
            try
            {
                if (!Features.MoveSpeedOn) { Restore(); return; }

                float mult = Mathf.Clamp(ModConfig.MoveSpeedMultiplier?.Value ?? 1f, 1f, 3f);
                if (mult <= 1.001f) { Restore(); return; }

                var p = PlayerAccessor.Resolve(out _, out _);
                if (p == null) { Restore(); return; }

                bool rolling = false;
                try { rolling = p.isRolling; } catch { }

                if (rolling)
                {
                    if (_base < 0f)
                    {
                        float cur = p.rollingMoveSpeed;
                        if (cur > 0f) _base = cur;
                    }
                    if (_base > 0f)
                    {
                        p.rollingMoveSpeed = _base * mult;
                        if (!_modified)
                        {
                            _modified = true;
                            Log.Info($"[move] 翻滚速度加速 x{mult:0.0}（base={_base:0.##}）");
                        }
                    }
                }
                else
                {
                    if (_modified && _base > 0f) p.rollingMoveSpeed = _base;
                    _modified = false;
                    try
                    {
                        float cur = p.rollingMoveSpeed;
                        if (cur > 0f) _base = cur;
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.ErrorOnce("rollspeed", e); }
        }

        internal static void Restore()
        {
            if (!_modified) return;
            try
            {
                var p = PlayerAccessor.Resolve(out _, out _);
                if (p != null && _base > 0f) p.rollingMoveSpeed = _base;
            }
            catch { }
            _modified = false;
        }
    }
}
