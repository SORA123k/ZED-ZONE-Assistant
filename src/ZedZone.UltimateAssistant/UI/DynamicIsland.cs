using System;
using System.Collections.Generic;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>
    /// 灵动岛（Dynamic Island）v2。
    ///
    /// 改进：
    ///  * 多个不同功能同时切换 → 同时展示（最多 3 条，最新的在最上）；
    ///  * 同一个功能被快速反复切换 → 直接覆盖旧条目并刷新计时（旧的被新的顶掉）；
    ///  * 展开为瞬时（不再慢慢动画），消失时才淡出，响应"跟手"；
    ///  * 左侧标识改为单字母 S。
    /// </summary>
    internal static class DynamicIsland
    {
        private sealed class Item
        {
            internal string Feature;
            internal bool Enabled;
            internal float At;
        }

        private static readonly List<Item> _items = new List<Item>();
        private static float _expand;
        private static Texture2D _circle;
        private static GUIStyle _title, _sub;

        private const float HoldSeconds = 2.4f;
        private const int MaxItems = 3;

        internal static void Push(string feature, bool enabled)
        {
            try
            {
                // 同一功能：覆盖并顶到最前
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    if (_items[i].Feature == feature) _items.RemoveAt(i);
                }
                _items.Insert(0, new Item { Feature = feature, Enabled = enabled, At = Time.unscaledTime });
                while (_items.Count > MaxItems) _items.RemoveAt(_items.Count - 1);

                _expand = 1f;   // 瞬时展开
            }
            catch { }
        }

        internal static void Clear()
        {
            _items.Clear();
            _expand = 0f;
        }

        private static void EnsureAssets()
        {
            if (_circle == null) _circle = MakeCircle(64);
            if (_title != null) return;

            Font f = null;
            try { f = PlayerStatusPanel.instance.daysText.font; } catch { }
            if (f == null) { try { f = GUI.skin.font; } catch { } }

            _title = New(f, 13, FontStyle.Bold);
            _sub = New(f, 11, FontStyle.Normal);
        }

        private static GUIStyle New(Font f, int size, FontStyle fs)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = fs,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            if (f != null) s.font = f;
            return s;
        }

        internal static void Draw()
        {
            try
            {
                EnsureAssets();
                ThemeManager.Init();

                float now = Time.unscaledTime;
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    if (now - _items[i].At > HoldSeconds) _items.RemoveAt(i);
                }

                if (_items.Count == 0)
                {
                    _expand = Mathf.MoveTowards(_expand, 0f, Time.unscaledDeltaTime * 9f);
                    return;
                }
                _expand = 1f;

                var p = ThemeManager.P;
                float scale = Mathf.Clamp(ModConfig.MenuScale?.Value ?? 1f, 0.8f, 1.4f);
                float rowH = 30f * scale;
                float h = rowH * _items.Count + 8f * scale;
                float w = 300f * scale;
                float x = (Screen.width - w) * 0.5f;
                float y = 12f * scale;

                var rect = new Rect(x, y, w, h);
                DrawCapsule(new Rect(rect.x + 1f, rect.y + 2f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.45f));
                DrawCapsule(rect, new Color(p.Window.r, p.Window.g, p.Window.b, 0.95f));

                for (int i = 0; i < _items.Count; i++)
                {
                    var it = _items[i];
                    var accent = it.Enabled ? p.Good : p.Bad;
                    float ry = rect.y + 4f * scale + i * rowH;

                    float dot = rowH * 0.30f;
                    var dotRect = new Rect(rect.x + rowH * 0.34f, ry + (rowH - dot) * 0.5f, dot, dot);
                    GUI.color = accent;
                    GUI.DrawTexture(dotRect, _circle);
                    GUI.color = Color.white;

                    var old = GUI.color;
                    float tx = dotRect.xMax + 8f * scale;
                    float vh = rowH;

                    GUI.color = new Color(p.Text.r, p.Text.g, p.Text.b, 0.9f);
                    GUI.Label(new Rect(tx, ry, 16f * scale, vh), "S", _sub);
                    tx += 18f * scale;

                    GUI.color = p.SubText;
                    GUI.Label(new Rect(tx, ry, 10f * scale, vh), "·", _sub);
                    tx += 12f * scale;

                    GUI.color = p.Text;
                    GUI.Label(new Rect(tx, ry, 130f * scale, vh), it.Feature, _title);
                    tx += 132f * scale;

                    GUI.color = p.SubText;
                    GUI.Label(new Rect(tx, ry, 10f * scale, vh), "·", _sub);
                    tx += 12f * scale;

                    string state = it.Enabled
                        ? (L.Current == Lang.En ? "ON" : "已启用")
                        : (L.Current == Lang.En ? "OFF" : "已关闭");
                    GUI.color = accent;
                    GUI.Label(new Rect(tx, ry, 70f * scale, vh), state, _sub);

                    GUI.color = old;
                }
            }
            catch (Exception e) { Log.ErrorOnce("island", e); }
        }

        private static void DrawCapsule(Rect r, Color c)
        {
            float rad = r.height * 0.5f;
            var old = GUI.color;
            GUI.color = c;
            var mid = new Rect(r.x + rad, r.y, Mathf.Max(0f, r.width - rad * 2f), r.height);
            GUI.DrawTexture(mid, Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, r.height, r.height), _circle);
            GUI.DrawTexture(new Rect(r.xMax - r.height, r.y, r.height, r.height), _circle);
            GUI.color = old;
        }

        private static Texture2D MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float c = (size - 1) * 0.5f;
            float rad = size * 0.5f;
            var buf = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(rad - d);
                    buf[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(buf);
            tex.Apply();
            return tex;
        }
    }
}
