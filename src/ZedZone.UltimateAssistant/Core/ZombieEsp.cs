using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// Zombie ESP：只读地把"当前已加载的僵尸"标记在屏幕上。
    ///
    /// 实现要点：
    ///  * 数据源：`GameController.instance.characterList`（全局角色列表），
    ///    再按 `ZombieController` 类型过滤 —— 不扫描、不修改任何角色属性；
    ///  * **节流刷新**（默认 150ms），不是每帧遍历，避免 GC 与卡顿；
    ///  * **缓存**世界坐标与名称，绘制阶段只做投影，不重复访问 IL2CPP 对象；
    ///  * **清理**：已销毁/已死亡/已卸载的对象在刷新时剔除；
    ///  * **联机禁用**：状态修改类功能在联机下拒绝，ESP 同样按项目规则禁用；
    ///  * 绘制全部用 IMGUI 图元，不创建任何 GameObject。
    /// </summary>
    internal static class ZombieEsp
    {
        private sealed class Entry
        {
            internal BasicCharacterController Z;
            internal string Name = "Zombie";
            internal Vector3 World;
            internal float Distance;
            internal bool Alive = true;
        }

        private static readonly List<Entry> _entries = new List<Entry>(64);
        private static float _nextRefresh;
        private static Camera _cam;
        private static int _lastCount;

        private static readonly Color ColBox = new Color(1f, 0.28f, 0.30f, 0.95f);
        private static readonly Color ColBoxFar = new Color(1f, 0.55f, 0.30f, 0.85f);
        private static readonly Color ColText = new Color(1f, 0.85f, 0.85f, 1f);
        private static readonly Color ColBg = new Color(0f, 0f, 0f, 0.45f);

        internal static int Count => _entries.Count;

        internal static void Clear()
        {
            _entries.Clear();
            _lastCount = 0;
        }

        /// <summary>节流刷新僵尸缓存。由面板的 tick 调用。</summary>
        internal static void Refresh()
        {
            if (!Features.ZombieEspOn) return;

            float now = Time.unscaledTime * 1000f;
            if (now < _nextRefresh) return;
            int ms = Mathf.Clamp(ModConfig.EspRefreshMs?.Value ?? 150, 50, 2000);
            if (Features.PerformanceModeOn) ms = Mathf.Max(ms, 350);
            _nextRefresh = now + ms;

            var result = new List<Entry>(_entries.Count + 8);

            try
            {
                var gc = GameController.instance;
                if (gc == null) { _entries.Clear(); return; }
                var list = gc.characterList;
                if (list == null) { _entries.Clear(); return; }

                HumanCharacterController player = null;
                try { player = PlayerAccessor.Resolve(out _, out _); } catch { }
                Vector3 ppos = Vector3.zero;
                if (player != null) { try { ppos = player.transform.position; } catch { } }

                float maxDist = ModConfig.EspMaxDistance?.Value ?? 200f;
                int n = list.Count;
                int cap = Features.PerformanceModeOn ? 120 : 300;
                for (int i = 0; i < n && result.Count < cap; i++)
                {
                    BasicCharacterController c = null;
                    try { c = list[i]; } catch { continue; }
                    if (c == null) continue;

                    // 类型过滤：ZombieController 及其它僵尸类型（Giant/Horde 等可能不派生自它）
                    bool isPlayer = false;
                    try { isPlayer = c.isPlayer; } catch { }
                    if (isPlayer) continue;

                    bool isZombie = false;
                    try { if (c.TryCast<ZombieController>() != null) isZombie = true; } catch { }
                    if (!isZombie)
                    {
                        try
                        {
                            var tn = c.GetType().Name;
                            if (tn != null && tn.IndexOf("Zombie", StringComparison.OrdinalIgnoreCase) >= 0) isZombie = true;
                        }
                        catch { }
                    }
                    if (!isZombie) continue;

                    bool dead = false;
                    try { dead = c.deadFlag; } catch { }
                    if (dead) continue;

                    Vector3 wp = Vector3.zero;
                    try { wp = c.transform.position; } catch { continue; }

                    float dist = 0f;
                    try { dist = Vector3.Distance(ppos, wp); } catch { }
                    if (dist > maxDist) continue;

                    string nm = "Zombie";
                    try { nm = CleanName(c.gameObject.name); } catch { }

                    result.Add(new Entry { Z = c, Name = nm, World = wp, Distance = dist, Alive = true });
                }
            }
            catch (Exception e)
            {
                Log.ErrorOnce("esp.refresh", e);
                return;
            }

            _entries.Clear();
            _entries.AddRange(result);
            _lastCount = _entries.Count;
        }

        private static string CleanName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Zombie";
            // 去掉 Unity 常见的 "(Clone)" 与多余后缀
            int idx = raw.IndexOf("(Clone)", StringComparison.OrdinalIgnoreCase);
            if (idx > 0) raw = raw.Substring(0, idx);
            raw = raw.Trim();
            return raw.Length == 0 ? "Zombie" : raw;
        }

        /// <summary>绘制（每帧调用，只做投影与画图）。</summary>
        internal static void Draw()
        {
            if (!Features.ZombieEspOn) return;
            if (_entries.Count == 0) return;

            if (_cam == null) { try { _cam = Camera.main; } catch { _cam = null; } }
            if (_cam == null) return;

            bool showName = ModConfig.EspShowName?.Value ?? true;
            bool showDist = ModConfig.EspShowDistance?.Value ?? true;
            bool drawBox = ModConfig.EspDrawBox?.Value ?? true;

            float maxDist = Mathf.Max(1f, ModConfig.EspMaxDistance?.Value ?? 200f);

            int w = Screen.width, h = Screen.height;
            var style = EspStyle();

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                Vector3 sp;
                try
                {
                    if (e.Z == null) continue;
                    sp = _cam.WorldToScreenPoint(e.World);
                }
                catch { continue; }

                if (sp.z <= 0f) continue;          // 在相机背后
                float sx = sp.x, sy = h - sp.y;    // GUI 坐标 y 轴向下

                // 视野外不丢弃：夹到屏幕边缘，仍然给出方向提示
                bool offscreen = sx < 8f || sx > w - 8f || sy < 8f || sy > h - 8f;
                sx = Mathf.Clamp(sx, 10f, w - 10f);
                sy = Mathf.Clamp(sy, 10f, h - 10f);

                float t = Mathf.Clamp01(e.Distance / maxDist);
                Color col = Color.Lerp(ColBox, ColBoxFar, t);

                // 方框：距离越远越小
                float size = Mathf.Lerp(46f, 14f, t);
                var r = new Rect(sx - size * 0.5f, sy - size * 0.5f, size, size);

                var old = GUI.color;
                GUI.color = col;
                if (offscreen)
                {
                    // 边缘指示：小方块 + 中心点
                    GUI.DrawTexture(new Rect(sx - 5f, sy - 5f, 10f, 10f), Texture2D.whiteTexture);
                }
                else if (drawBox)
                {
                    // 四条边（比整块填充更轻量）
                    float th = 2f;
                    GUI.DrawTexture(new Rect(r.x, r.y, r.width, th), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.x, r.yMax - th, r.width, th), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.x, r.y, th, r.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax - th, r.y, th, r.height), Texture2D.whiteTexture);
                }
                else
                {
                    GUI.DrawTexture(new Rect(sx - 2f, sy - 2f, 4f, 4f), Texture2D.whiteTexture);
                }
                GUI.color = old;

                // 文本：名称 + 距离
                string label = null;
                if (showName && showDist) label = e.Name + "  " + e.Distance.ToString("0") + "m";
                else if (showName) label = e.Name;
                else if (showDist) label = e.Distance.ToString("0") + "m";

                if (label != null)
                {
                    var tr = new Rect(r.x - 20f, r.yMax + 1f, 140f, 16f);
                    GUI.color = ColBg;
                    GUI.DrawTexture(tr, Texture2D.whiteTexture);
                    GUI.color = ColText;
                    GUI.Label(tr, " " + label, style);
                    GUI.color = old;
                }
            }
        }

        private static GUIStyle _style;
        private static GUIStyle EspStyle()
        {
            if (_style != null) return _style;
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            try
            {
                var f = GUI.skin.font;
                if (f != null) _style.font = f;
            }
            catch { }
            return _style;
        }

        internal static string StatusText()
            => $"ZombieEsp={(Features.ZombieEspOn ? "开" : "关")} 目标={_lastCount}";
    }
}


