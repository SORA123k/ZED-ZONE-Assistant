using System;
using System.Collections.Generic;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>
    /// 灵动岛（Dynamic Island）：屏幕顶部居中的胶囊提示条。
    ///
    /// 参考设计：深色半透明胶囊 + 左侧状态圆点 + 分段文字（SORA · 功能名 · 状态）。
    /// 行为：功能开启/关闭时展开显示 2.2 秒，随后收缩隐藏；多次触发会排队。
    /// 只绘制 HUD，不拦截任何游戏输入。
    /// </summary>
    internal static class DynamicIsland
    {
        private struct Msg { internal string Feature; internal bool Enabled; }

        private static readonly Queue<Msg> _queue = new Queue<Msg>();
        private static Msg? _current;
        private static float _shownAt;
        private static float _expand;      // 0..1
        private static Texture2D _circle;
        private static GUIStyle _title, _sub;

        private const float HoldSeconds = 2.2f;

        internal static void Push(string feature, bool enabled)
        {
            try { _queue.Enqueue(new Msg { Feature = feature, Enabled = enabled }); }
            catch { }
        }

        internal static void Clear()
        {
            _queue.Clear();
            _current = null;
            _expand = 0f;
        }

        private static void EnsureAssets()
        {
            if (_circle != null && _title != null) return;
            if (_circle == null) _circle = MakeCircle(64);

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

                if (_current == null)
                {
                    if (_queue.Count > 0) { _current = _queue.Dequeue(); _shownAt = now; }
                    else
                    {
                        _expand = Mathf.MoveTowards(_expand, 0f, Time.unscaledDeltaTime * 5f);
                        return;
                    }
                }

                float age = now - _shownAt;
                if (age > HoldSeconds)
                {
                    _current = null;
                    _expand = Mathf.MoveTowards(_expand, 0f, Time.unscaledDeltaTime * 5f);
                    return;
                }

                // 展开动画：前 0.25s 展开，最后 0.3s 收缩
                float target = (age < 0.25f || age > HoldSeconds - 0.3f) ? 0.55f : 1f;
                _expand = Mathf.MoveTowards(_expand, target, Time.unscaledDeltaTime * 6f);

                var p = ThemeManager.P;
                var m = _current.Value;

                float scale = Mathf.Clamp(ModConfig.MenuScale?.Value ?? 1f, 0.8f, 1.4f);
                float h = 34f * scale;
                float w = (170f + 90f * _expand) * scale;
                float x = (Screen.width - w) * 0.5f;
                float y = 12f * scale + (1f - _expand) * -6f * scale;

                var rect = new Rect(x, y, w, h);
                var accent = m.Enabled ? p.Good : p.Bad;

                // 阴影 + 胶囊
                DrawCapsule(new Rect(rect.x + 1f, rect.y + 2f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.45f));
                DrawCapsule(rect, new Color(p.Window.r, p.Window.g, p.Window.b, 0.94f));

                // 左侧状态圆点
                float dot = h * 0.34f;
                var dotRect = new Rect(rect.x + h * 0.36f, rect.y + (h - dot) * 0.5f, dot, dot);
                GUI.color = accent;
                GUI.DrawTexture(dotRect, _circle);
                GUI.color = Color.white;

                // 文本：SORA · 功能 · 状态
                string state = m.Enabled ? (L.Current == Lang.En ? "ON" : "已启用")
                                         : (L.Current == Lang.En ? "OFF" : "已关闭");
                var old = GUI.color;
                float tx = dotRect.xMax + h * 0.22f;

                GUI.color = new Color(p.Text.r, p.Text.g, p.Text.b, 0.85f * _expand + 0.15f);
                GUI.Label(new Rect(tx, rect.y, 52f * scale, h), "SORA", _sub);
                tx += 52f * scale;

                GUI.color = p.SubText;
                GUI.Label(new Rect(tx, rect.y, 10f * scale, h), "·", _sub);
                tx += 12f * scale;

                GUI.color = p.Text;
                GUI.Label(new Rect(tx, rect.y, 108f * scale, h), m.Feature, _title);
                tx += 108f * scale;

                GUI.color = p.SubText;
                GUI.Label(new Rect(tx, rect.y, 10f * scale, h), "·", _sub);
                tx += 12f * scale;

                GUI.color = accent;
                GUI.Label(new Rect(tx, rect.y, 60f * scale, h), state, _sub);

                GUI.color = old;
            }
            catch (Exception e) { Log.ErrorOnce("island", e); }
        }

        // ---------------- 图元 ----------------

        private static void DrawCapsule(Rect r, Color c)
        {
            float rad = r.height * 0.5f;
            var old = GUI.color;
            GUI.color = c;

            // 中间矩形
            var mid = new Rect(r.x + rad, r.y, Mathf.Max(0f, r.width - rad * 2f), r.height);
            GUI.DrawTexture(mid, Texture2D.whiteTexture);

            // 两端圆
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
