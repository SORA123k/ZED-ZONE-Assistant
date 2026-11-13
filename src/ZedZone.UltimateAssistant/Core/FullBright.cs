using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 地图全亮：让本地画面呈现"白天"亮度。
    ///
    /// 该游戏是 URP 2D，夜间压暗主要来自 **全局 Light2D**（不是环境光）。
    /// 因此本实现同时处理两层：
    ///  1. `Light2D`（lightType == Global）：夜间（intensity 低）时把强度抬到白天水平、颜色拉向白色；
    ///  2. `RenderSettings.ambientLight / ambientIntensity`：作为兜底。
    ///
    /// 全部为客户端渲染参数，不修改游戏时间、天气、AI、存档。
    /// </summary>
    internal static class FullBright
    {
        private static bool _saved;
        private static Color _amb;
        private static float _ambIntensity;
        private static Light _sun;
        private static float _sunIntensity;
        private static bool _sunTouched;
        private static float _next;

        private static readonly List<Light2D> _globals = new List<Light2D>();
        private static readonly List<float> _gIntensity = new List<float>();
        private static readonly List<Color> _gColor = new List<Color>();

        internal static void Tick()
        {
            try
            {
                if (!Features.FullBrightOn) { Restore(); return; }
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 0.4f;

                Save();

                // ---- 1) URP 2D 全局光 ----
                if (_globals.Count > 0)
                {
                    for (int i = 0; i < _globals.Count; i++)
                    {
                        var l = _globals[i];
                        if (l == null) continue;
                        try
                        {
                            float cur = l.intensity;
                            // 只在偏暗（夜间）时抬升，白天保持原值避免过曝
                            if (cur < 0.85f)
                            {
                                l.intensity = Mathf.Max(cur, 1f);
                                var c = l.color;
                                l.color = new Color(
                                    Mathf.Lerp(c.r, 1f, 0.85f),
                                    Mathf.Lerp(c.g, 1f, 0.85f),
                                    Mathf.Lerp(c.b, 1f, 0.85f),
                                    c.a);
                            }
                        }
                        catch { }
                    }
                }

                // ---- 2) 环境光兜底 ----
                float ambLum = 0.2126f * _amb.r + 0.7152f * _amb.g + 0.0722f * _amb.b;
                bool night = ambLum < 0.30f;
                if (_sun != null && _sunIntensity > 0.01f && _sun.intensity < _sunIntensity * 0.55f) night = true;

                if (night)
                {
                    RenderSettings.ambientLight = Color.Lerp(_amb, new Color(0.78f, 0.80f, 0.86f), 0.85f);
                    RenderSettings.ambientIntensity = Mathf.Max(_ambIntensity, 2.0f);
                    if (_sun != null && _sun.intensity < _sunIntensity * 0.55f)
                    {
                        _sun.intensity = _sunIntensity * 0.6f;
                        _sunTouched = true;
                    }
                }
                else
                {
                    RenderSettings.ambientLight = _amb;
                    RenderSettings.ambientIntensity = _ambIntensity;
                    if (_sunTouched && _sun != null) { _sun.intensity = _sunIntensity; _sunTouched = false; }
                }
            }
            catch (Exception e) { Log.ErrorOnce("fullbright", e); }
        }

        private static void Save()
        {
            if (_saved) return;
            _saved = true;
            try
            {
                _amb = RenderSettings.ambientLight;
                _ambIntensity = RenderSettings.ambientIntensity;

                Light best = null; float bestI = 0f;
                var lights = Resources.FindObjectsOfTypeAll<Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    var l = lights[i];
                    if (l == null || l.type != LightType.Directional) continue;
                    if (l.intensity > bestI) { bestI = l.intensity; best = l; }
                }
                _sun = best;
                _sunIntensity = bestI > 0.01f ? bestI : 1f;

                _globals.Clear(); _gIntensity.Clear(); _gColor.Clear();
                var l2 = Resources.FindObjectsOfTypeAll<Light2D>();
                for (int i = 0; i < l2.Length; i++)
                {
                    var l = l2[i];
                    if (l == null) continue;
                    try
                    {
                        if (l.lightType != Light2D.LightType.Global) continue;
                        _globals.Add(l);
                        _gIntensity.Add(l.intensity);
                        _gColor.Add(l.color);
                    }
                    catch { }
                }

                Log.Info($"[bright] 基线 ambient={_amb} intensity={_ambIntensity:0.##} sun={(best == null ? "none" : best.name)} globalLight2D={_globals.Count}");
            }
            catch { }
        }

        internal static void Restore()
        {
            if (!_saved) return;
            try
            {
                RenderSettings.ambientLight = _amb;
                RenderSettings.ambientIntensity = _ambIntensity;
                if (_sunTouched && _sun != null) _sun.intensity = _sunIntensity;

                for (int i = 0; i < _globals.Count; i++)
                {
                    var l = _globals[i];
                    if (l == null) continue;
                    try
                    {
                        l.intensity = _gIntensity[i];
                        l.color = _gColor[i];
                    }
                    catch { }
                }
            }
            catch { }
            _sunTouched = false;
            _saved = false;
            _sun = null;
            _globals.Clear(); _gIntensity.Clear(); _gColor.Clear();
        }
    }
}
