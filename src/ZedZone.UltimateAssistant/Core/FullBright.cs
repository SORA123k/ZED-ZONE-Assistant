using System;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 地图全亮：只提升**画面亮度**（渲染层），不修改游戏时间、天气、AI。
    ///
    /// 做法（全部使用 Unity 公开 API，不碰游戏数据）：
    ///  * 保存开启前的 `RenderSettings.ambientLight / ambientIntensity` 与场景主平行光强度；
    ///  * 判定是否为夜晚（主平行光强度明显低于开启时的基线，或环境光亮度低）；
    ///  * 夜晚：把环境光拉向中性亮色、提高环境光强度，并在必要时补一点主光；
    ///  * 白天：完全恢复原值 → 不产生过曝/偏色；
    ///  * 关闭开关：恢复全部原值。
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

        internal static void Tick()
        {
            try
            {
                if (!Features.FullBrightOn) { Restore(); return; }
                if (Time.unscaledTime < _next) return;
                _next = Time.unscaledTime + 0.5f;

                Save();

                float ambLum = 0.2126f * _amb.r + 0.7152f * _amb.g + 0.0722f * _amb.b;
                bool night;
                if (_sun != null && _sunIntensity > 0.01f)
                    night = _sun.intensity < _sunIntensity * 0.55f;
                else
                    night = ambLum < 0.30f;

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
                    // 白天：确保是原值，避免累积过曝
                    RenderSettings.ambientLight = _amb;
                    RenderSettings.ambientIntensity = _ambIntensity;
                    if (_sunTouched && _sun != null)
                    {
                        _sun.intensity = _sunIntensity;
                        _sunTouched = false;
                    }
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

                Light best = null;
                float bestI = 0f;
                var lights = Resources.FindObjectsOfTypeAll<Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    var l = lights[i];
                    if (l == null || l.type != LightType.Directional) continue;
                    if (l.intensity > bestI) { bestI = l.intensity; best = l; }
                }
                _sun = best;
                _sunIntensity = bestI > 0.01f ? bestI : 1f;
                Log.Info($"[bright] 基线：ambient={_amb} intensity={_ambIntensity:0.##} sun={(best == null ? "none" : best.name)} sunI={_sunIntensity:0.##}");
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
            }
            catch { }
            _sunTouched = false;
            _saved = false;
            _sun = null;
        }
    }
}


