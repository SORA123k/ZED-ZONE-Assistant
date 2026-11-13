using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>
    /// ClickGUI 风格主菜单。
    ///
    /// 设计参考开源 Minecraft 客户端 ClickGUI 的通用范式（分类侧栏 + 模块卡片 + 开关 + 悬浮反馈），
    /// **未复制任何第三方素材/代码**，全部为自绘 IMGUI 图元。
    ///
    /// 特点：
    ///  * 深色背景 + 强调色（默认蓝紫）+ 卡片式布局；
    ///  * 左侧分类：Combat / Player / Visuals / Settings；
    ///  * 每个功能独立开关，开关有过渡动画；
    ///  * Player 页保留已稳定的四项一键恢复（Health/Energy/Hunger/Thirst）；
    ///  * 可选显示肢体状态（只读，来自 Phase 3B-1）；
    ///  * 打开/关闭有淡入缩放动画（可关）；
    ///  * 不创建任何 GameObject，不引入外部依赖。
    /// </summary>
    internal static class ClickGui
    {
        internal static bool Open;

        private static readonly string[] Cats = { "Combat", "Player", "Visuals", "Settings" };
        private static int _cat;
        private static float _openT;
        private static float _time;

        private static readonly Dictionary<string, float> _anim = new Dictionary<string, float>();
        
        // ---- 主题 ----
        private struct Palette
        {
            internal Color Bg, Panel, Card, CardHover, Sidebar, Text, Sub, Accent, AccentDim, Good, Warn, Bad, Line;
        }

        private static Palette _p;

        private static string _paletteTheme;

        private static void BuildPalette()
        {
            string theme = (ModConfig.MenuTheme?.Value ?? "Violet");
            if (_paletteTheme == theme && _p.Accent.a > 0f) return;
            _paletteTheme = theme;
            Color accent;
            switch (theme)
            {
                case "Azure": accent = new Color(0.25f, 0.62f, 1f); break;
                case "Crimson": accent = new Color(0.95f, 0.32f, 0.42f); break;
                case "Emerald": accent = new Color(0.24f, 0.82f, 0.55f); break;
                default: accent = new Color(0.58f, 0.42f, 1f); break;
            }

            _p = new Palette
            {
                Bg = new Color(0.055f, 0.058f, 0.075f, 0.97f),
                Panel = new Color(0.075f, 0.080f, 0.100f, 1f),
                Sidebar = new Color(0.062f, 0.066f, 0.084f, 1f),
                Card = new Color(0.105f, 0.112f, 0.138f, 1f),
                CardHover = new Color(0.135f, 0.145f, 0.180f, 1f),
                Text = new Color(0.93f, 0.94f, 0.97f),
                Sub = new Color(0.60f, 0.63f, 0.72f),
                Accent = accent,
                AccentDim = new Color(accent.r, accent.g, accent.b, 0.18f),
                Good = new Color(0.35f, 0.82f, 0.52f),
                Warn = new Color(0.95f, 0.73f, 0.28f),
                Bad = new Color(0.95f, 0.36f, 0.36f),
                Line = new Color(1f, 1f, 1f, 0.055f),
            };
        }

        // ---- 样式 ----
        private static Vector2 _pos;
        private static bool _posInit;
        private static bool _dragging;
        private static Vector2 _dragOffset;
        private static bool _stylesReady;
        private static GUIStyle _title, _catStyle, _sub, _cardTitle, _cardDesc, _small, _pill, _btn, _btnText, _value;

        private static void EnsureStyles()
        {
            if (_stylesReady) return;
            try
            {
                Font f = ResolveCjkFont();

                _title = New(f, 17, FontStyle.Bold, TextAnchor.MiddleLeft);
                _catStyle = New(f, 13, FontStyle.Normal, TextAnchor.MiddleLeft);
                _sub = New(f, 11, FontStyle.Normal, TextAnchor.MiddleLeft);
                _cardTitle = New(f, 14, FontStyle.Bold, TextAnchor.MiddleLeft);
                _cardDesc = New(f, 11, FontStyle.Normal, TextAnchor.MiddleLeft);
                _small = New(f, 10, FontStyle.Normal, TextAnchor.MiddleLeft);
                _pill = New(f, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
                _btn = New(f, 12, FontStyle.Bold, TextAnchor.MiddleCenter);
                _btnText = New(f, 13, FontStyle.Bold, TextAnchor.MiddleLeft);
                _value = New(f, 15, FontStyle.Bold, TextAnchor.MiddleRight);
                _stylesReady = true;
            }
            catch { _stylesReady = false; }
        }

        private static Font _cjk;
        private static bool _cjkTried;

        /// <summary>取游戏内可用的中文字体（与只读面板同一来源），失败则回退默认字体。</summary>
        private static Font ResolveCjkFont()
        {
            if (_cjkTried && _cjk != null) return _cjk;
            _cjkTried = true;

            try
            {
                var f = PlayerStatusPanel.instance.daysText.font;
                if (f != null && f.HasCharacter('中')) { _cjk = f; Log.Info("[ui] ClickGUI 字体: PlayerStatusPanel.daysText.font"); return _cjk; }
            }
            catch { }

            try
            {
                var texts = Resources.FindObjectsOfTypeAll<Text>();
                for (int i = 0; i < texts.Length && i < 400; i++)
                {
                    var t = texts[i];
                    if (t == null) continue;
                    var f = t.font;
                    if (f != null && f.HasCharacter('中')) { _cjk = f; Log.Info("[ui] ClickGUI 字体: 扫描 UI.Text"); return _cjk; }
                }
            }
            catch { }

            try { _cjk = GUI.skin.font; } catch { }
            if (_cjk == null) { try { _cjk = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            Log.WarnOnce("ui.cjk", "[ui] 未找到中文字体，部分中文可能显示为方框。");
            return _cjk;
        }
        private static GUIStyle New(Font f, int size, FontStyle fs, TextAnchor anchor)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = fs,
                alignment = anchor,
                wordWrap = false,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            if (f != null) s.font = f;
            return s;
        }

        private static float Sc => Mathf.Clamp(ModConfig.MenuScale?.Value ?? 1f, 0.7f, 1.6f);

        // ==================================================================
        internal static void Draw(PlayerSnapshot snap)
        {
            EnsureStyles();
            BuildPalette();
            try { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; } catch { }

            bool anim = ModConfig.MenuAnimations?.Value ?? true;
            _time = Time.unscaledTime;

            // 打开动画
            float target = Open ? 1f : 0f;
            _openT = anim ? Mathf.MoveTowards(_openT, target, Time.unscaledDeltaTime * 7f) : target;
            if (_openT <= 0.001f && !Open) return;

            float s = Sc;
            float w = 760f * s, h = 480f * s;
            if (!_posInit) { _pos = new Vector2(ModConfig.PanelX?.Value ?? 380, ModConfig.PanelY?.Value ?? 40); _posInit = true; }
            float x = Mathf.Clamp(_pos.x, 0f, Mathf.Max(0f, Screen.width - w));
            float y = Mathf.Clamp(_pos.y, 0f, Mathf.Max(0f, Screen.height - h));
            _openRect = new Rect(x, y, w, h);

            // 缩放动画（以中心为锚）
            if (anim && _openT < 0.999f)
            {
                float k = Mathf.Lerp(0.96f, 1f, _openT);
                float nw = w * k, nh = h * k;
                x += (w - nw) * 0.5f;
                y += (h - nh) * 0.5f;
                w = nw; h = nh;
            }

            var area = new Rect(x, y, w, h);

            var oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, anim ? Mathf.Clamp01(_openT * 1.2f) : 1f);

            // 背景遮罩
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.35f * _openT));

            // 主窗体
            Fill(area, _p.Bg);
            Stroke(area, _p.Line, 1f);

            // 顶部标题栏
            var header = new Rect(area.x, area.y, area.width, 42f * s);
            Fill(header, _p.Panel);
            Fill(new Rect(area.x, header.yMax - 1f, area.width, 1f), _p.Line);

            GUI.Label(new Rect(header.x + 16f * s, header.y, 340f * s, header.height),
                "ZED ZONE  ·  ULTIMATE ASSISTANT", _title);
            GUI.color = _p.Sub;
            GUI.Label(new Rect(header.xMax - 210f * s, header.y, 194f * s, header.height),
                "v" + Plugin.PluginVersion + "   INSERT 关闭", _small);
            GUI.color = new Color(1f, 1f, 1f, anim ? Mathf.Clamp01(_openT * 1.2f) : 1f);

            HandleHeaderDrag(header, w, h);

            // 侧栏
            var side = new Rect(area.x, header.yMax, 168f * s, area.height - header.height);
            Fill(side, _p.Sidebar);
            Fill(new Rect(side.xMax - 1f, side.y, 1f, side.height), _p.Line);

            float cy = side.y + 14f * s;
            for (int i = 0; i < Cats.Length; i++)
            {
                var r = new Rect(side.x + 10f * s, cy, side.width - 20f * s, 36f * s);
                bool active = i == _cat;
                bool hover = r.Contains(Event.current.mousePosition);

                if (active) Fill(r, _p.AccentDim);
                else if (hover) Fill(r, _p.CardHover);

                if (active) Fill(new Rect(r.x, r.y + 6f * s, 3f * s, r.height - 12f * s), _p.Accent);

                GUI.color = active ? _p.Text : (hover ? _p.Text : _p.Sub);
                GUI.Label(new Rect(r.x + 14f * s, r.y, r.width - 20f * s, r.height), Cats[i], _catStyle);
                GUI.color = new Color(1f, 1f, 1f, anim ? Mathf.Clamp01(_openT * 1.2f) : 1f);

                if (Hit(r)) { _cat = i; }
                cy += 40f * s;
            }

            // 侧栏底部状态
            GUI.color = _p.Sub;
            GUI.Label(new Rect(side.x + 12f * s, side.yMax - 58f * s, side.width - 20f * s, 14f * s),
                Features.InfiniteAmmoOn ? "● AMMO ON" : "○ ammo off", _small);
            GUI.Label(new Rect(side.x + 12f * s, side.yMax - 44f * s, side.width - 20f * s, 14f * s),
                Features.NoReloadOn ? "● NORELOAD ON" : "○ noreload off", _small);
            GUI.Label(new Rect(side.x + 12f * s, side.yMax - 30f * s, side.width - 20f * s, 14f * s),
                Features.ZombieEspOn ? "● ESP ON (" + ZombieEsp.Count + ")" : "○ esp off", _small);
            GUI.color = new Color(1f, 1f, 1f, anim ? Mathf.Clamp01(_openT * 1.2f) : 1f);

            // 内容区
            var content = new Rect(side.xMax, header.yMax, area.xMax - side.xMax, area.height - header.height);
            DrawContent(content, s, snap);

            // 软件光标：游戏可能每帧隐藏硬件光标，这里自绘一个，保证一定看得到
            DrawCursor();

            GUI.color = oldColor;
        }

        private static void DrawCursor()
        {
            try
            {
                Vector2 m = Event.current != null ? Event.current.mousePosition
                                                  : (Vector2)UnityEngine.Input.mousePosition;

                var old = GUI.color;
                // 黑色描边
                GUI.color = new Color(0f, 0f, 0f, 0.95f);
                GUI.DrawTexture(new Rect(m.x - 1f, m.y - 11f, 2f, 23f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(m.x - 11f, m.y - 1f, 23f, 2f), Texture2D.whiteTexture);
                // 白色内芯
                GUI.color = new Color(1f, 1f, 1f, 1f);
                GUI.DrawTexture(new Rect(m.x - 0.5f, m.y - 10f, 1f, 21f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(m.x - 10f, m.y - 0.5f, 21f, 1f), Texture2D.whiteTexture);
                GUI.color = old;
            }
            catch { }
        }

        private static Rect _openRect;

        /// <summary>标题栏拖动：按住标题栏移动菜单，松手后写回配置坐标。</summary>
        private static void HandleHeaderDrag(Rect header, float w, float h)
        {
            var e = Event.current;
            if (e == null) return;

            if (!_dragging && e.type == EventType.MouseDown && e.button == 0 && header.Contains(e.mousePosition))
            {
                _dragging = true;
                _dragOffset = e.mousePosition - _pos;
                e.Use();
                return;
            }
            if (_dragging && e.type == EventType.MouseDrag)
            {
                var p = e.mousePosition - _dragOffset;
                _pos = new Vector2(
                    Mathf.Clamp(p.x, 0f, Mathf.Max(0f, Screen.width - w)),
                    Mathf.Clamp(p.y, 0f, Mathf.Max(0f, Screen.height - h)));
                e.Use();
                return;
            }
            if (_dragging && e.type == EventType.MouseUp)
            {
                _dragging = false;
                try
                {
                    ModConfig.PanelX.Value = Mathf.RoundToInt(_pos.x);
                    ModConfig.PanelY.Value = Mathf.RoundToInt(_pos.y);
                }
                catch { }
                e.Use();
            }
        }

        private static void DrawContent(Rect content, float s, PlayerSnapshot snap)
        {
            float pad = 16f * s;
            float cw = content.width - pad * 2f;
            float y = content.y + pad;

            try
            {
                switch (_cat)
                {
                    case 0: DrawCombat(content, s, ref y, pad, cw); break;
                    case 1: DrawPlayer(content, s, ref y, pad, cw, snap); break;
                    case 2: DrawVisuals(content, s, ref y, pad, cw); break;
                    case 3: DrawSettings(content, s, ref y, pad, cw); break;
                }
            }
            catch (Exception e)
            {
                Log.ErrorOnce("gui.content." + _cat, e);
            }
        }

        // ---------------- Combat ----------------
        private static void DrawCombat(Rect c, float s, ref float y, float pad, float cw)
        {
            y = Section("战斗辅助", c, s, y, pad);

            y = ModuleCard(c, s, y, pad, cw, "Infinite Ammo", "射击不消耗备用弹药（仅本地玩家）",
                Features.InfiniteAmmoOn, v => { ModConfig.InfiniteAmmo.Value = v; Features.SetInfiniteAmmo(v); });

            y = ModuleCard(c, s, y, pad, cw, "No Reload", "弹匣为 0 也能射击；按 R 仍可正常换弹",
                Features.NoReloadOn, v => { ModConfig.NoReload.Value = v; Features.SetNoReload(v); });

            y = ModuleCard(c, s, y, pad, cw, "Infinite Durability", "工具/武器使用时不消耗耐久（保持当前耐久）",
                Features.WeaponDurabilityOn, v => { ModConfig.WeaponDurability.Value = v; Features.SetWeaponDurability(v); });

            y = ModuleCard(c, s, y, pad, cw, "No Overheat", "取消自己枪械的过热（不影响 NPC）",
                Features.NoOverheatOn, v => { ModConfig.NoOverheat.Value = v; Features.SetNoOverheat(v); });

            y += 4f * s;
            y = ActionButton(c, s, y, pad, cw, "一键修复卡壳", "清除所有己方武器的卡壳状态", () =>
            {
                WeaponUtil.FixJams();
            });

            y += 6f * s;
            GUI.color = _p.Sub;
            GUI.Label(new Rect(c.x + pad, y, cw, 16f * s),
                "全部功能仅作用于本地玩家；联机状态下自动禁用。", _small);
            GUI.color = Color.white;
        }

        // ---------------- Player ----------------
        private static void DrawPlayer(Rect c, float s, ref float y, float pad, float cw, PlayerSnapshot snap)
        {
            y = Section("玩家状态恢复", c, s, y, pad);

            if (snap == null || !snap.HasPlayer)
            {
                GUI.color = _p.Warn;
                GUI.Label(new Rect(c.x + pad, y, cw, 18f * s),
                    "未找到玩家对象（主菜单 / 加载中 / 已死亡未重生）", _cardDesc);
                GUI.color = Color.white;
                y += 22f * s;
            }

            y = RestoreCard(c, s, y, pad, cw, "Health", "生命值", StatId.Hp, snap, "hp");
            y = RestoreCard(c, s, y, pad, cw, "Energy / Stamina", "精力与持久力（同一数值）", StatId.Energy, snap, "energy");
            y = RestoreCard(c, s, y, pad, cw, "Hunger", "饱食度", StatId.Hunger, snap, "hunger");
            y = RestoreCard(c, s, y, pad, cw, "Thirst", "水分", StatId.Thirst, snap, "thirst");

            if (ModConfig.MenuShowLimbs?.Value ?? true)
            {
                y += 8f * s;
                y = Section("肢体状态（只读）", c, s, y, pad);
                y = DrawLimbs(c, s, y, pad, cw);
            }
        }

        private static float RestoreCard(Rect c, float s, float y, float pad, float cw, string name, string desc,
            StatId stat, PlayerSnapshot snap, string key)
        {
            var r = new Rect(c.x + pad, y, cw, 52f * s);
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? _p.CardHover : _p.Card);

            GUI.color = _p.Text;
            GUI.Label(new Rect(r.x + 14f * s, r.y + 8f * s, 220f * s, 18f * s), name, _cardTitle);
            GUI.color = _p.Sub;
            GUI.Label(new Rect(r.x + 14f * s, r.y + 27f * s, 260f * s, 16f * s), desc, _cardDesc);

            // 当前值
            string val = "—";
            if (snap != null && snap.HasPlayer)
            {
                var v = snap.Get(key);
                if (v != null)
                    val = v.HasMax ? $"{v.Value:0.#} / {v.Max:0.#}" : v.ValueText();
            }
            GUI.color = _p.Text;
            GUI.Label(new Rect(r.xMax - 250f * s, r.y + 8f * s, 150f * s, 18f * s), val, _value);

            // 按钮
            var br = new Rect(r.xMax - 92f * s, r.y + 12f * s, 80f * s, 28f * s);
            bool bHover = br.Contains(Event.current.mousePosition);
            Fill(br, bHover ? _p.Accent : _p.AccentDim);
            GUI.color = bHover ? new Color(0.08f, 0.08f, 0.10f) : _p.Text;
            GUI.Label(br, "恢复", _btn);
            GUI.color = Color.white;

            if (Hit(br))
            {
                var res = RestoreService.Restore(stat);
                Log.Info("[gui] restore " + stat + " -> " + res.Outcome + " " + res.Message);
            }

            return y + 58f * s;
        }

        private static float DrawLimbs(Rect c, float s, float y, float pad, float cw)
        {
            if (!LimbData.HasPlayer || LimbData.Items.Count == 0)
            {
                GUI.color = _p.Sub;
                GUI.Label(new Rect(c.x + pad, y, cw, 16f * s),
                    LimbData.Error ?? "无肢体数据（进入存档后自动显示）", _small);
                GUI.color = Color.white;
                return y + 20f * s;
            }

            for (int i = 0; i < LimbData.Items.Count; i++)
            {
                var L = LimbData.Items[i];
                var r = new Rect(c.x + pad, y, cw, 18f * s);
                GUI.color = _p.Card;
                Fill(r, _p.Card);

                GUI.color = _p.Text;
                GUI.Label(new Rect(r.x + 10f * s, r.y, 66f * s, r.height), L.Name, _small);

                GUI.color = _p.Sub;
                GUI.Label(new Rect(r.x + 78f * s, r.y, 110f * s, r.height),
                    L.HpText() + " / " + L.MaxText(), _small);

                // 进度条
                var bar = new Rect(r.x + 196f * s, r.y + 6f * s, 120f * s, 6f * s);
                Fill(bar, new Color(0.2f, 0.2f, 0.24f));
                float pc = L.Percent01();
                if (!float.IsNaN(pc))
                {
                    GUI.color = pc >= 0.999f ? _p.Good : (pc >= 0.5f ? _p.Warn : _p.Bad);
                    Fill(new Rect(bar.x, bar.y, bar.width * pc, bar.height), GUI.color);
                }

                GUI.color = L.Bleeding ? _p.Bad : (L.Wounded ? _p.Warn : _p.Sub);
                GUI.Label(new Rect(r.x + 326f * s, r.y, cw - 330f * s, r.height),
                    L.Bleeding ? "流血" : (L.Wounded ? "受伤" : (L.Recovered ? "完好" : "正常")), _small);

                GUI.color = Color.white;
                y += 20f * s;
            }
            return y + 4f * s;
        }

        // ---------------- Visuals ----------------
        private static void DrawVisuals(Rect c, float s, ref float y, float pad, float cw)
        {
            y = Section("视觉", c, s, y, pad);

            y = ModuleCard(c, s, y, pad, cw, "Zombie ESP", "标记当前已加载的僵尸（含遮挡/视野外）",
                Features.ZombieEspOn, v => { ModConfig.ZombieEsp.Value = v; Features.SetZombieEsp(v); });

            y = Section("ESP 选项", c, s, y + 4f * s, pad);
            y = ToggleRow(c, s, y, pad, cw, "显示名称", ModConfig.EspShowName.Value, v => ModConfig.EspShowName.Value = v);
            y = ToggleRow(c, s, y, pad, cw, "显示距离", ModConfig.EspShowDistance.Value, v => ModConfig.EspShowDistance.Value = v);
            y = ToggleRow(c, s, y, pad, cw, "绘制方框", ModConfig.EspDrawBox.Value, v => ModConfig.EspDrawBox.Value = v);
            y = SliderRow(c, s, y, pad, cw, "最大距离", ModConfig.EspMaxDistance.Value, 20f, 500f,
                v => ModConfig.EspMaxDistance.Value = Mathf.Round(v / 5f) * 5f, "m");
        }

        // ---------------- Settings ----------------
        private static void DrawSettings(Rect c, float s, ref float y, float pad, float cw)
        {
            y = Section("菜单设置", c, s, y, pad);

            // 快捷键
            var r = new Rect(c.x + pad, y, cw, 44f * s);
            Fill(r, _p.Card);
            GUI.color = _p.Text;
            GUI.Label(new Rect(r.x + 14f * s, r.y + 6f * s, 200f * s, 18f * s), "菜单快捷键", _cardTitle);
            GUI.color = _p.Sub;
            GUI.Label(new Rect(r.x + 14f * s, r.y + 24f * s, 300f * s, 14f * s), "在配置文件中修改 [UI] ToggleKey", _small);
            GUI.color = _p.Accent;
            GUI.Label(new Rect(r.xMax - 130f * s, r.y, 116f * s, r.height), KeyName(), _btnText);
            y += 50f * s;

            // 主题
            y = CycleRow(c, s, y, pad, cw, "主题配色", ModConfig.MenuTheme.Value,
                new[] { "Violet", "Azure", "Crimson", "Emerald" },
                v => ModConfig.MenuTheme.Value = v);

            // 缩放
            y = SliderRow(c, s, y, pad, cw, "界面缩放", ModConfig.MenuScale.Value, 0.7f, 1.6f,
                v => ModConfig.MenuScale.Value = Mathf.Round(v * 20f) / 20f, "x");

            // 开关项
            y = ToggleRow(c, s, y, pad, cw, "动画效果", ModConfig.MenuAnimations.Value, v => ModConfig.MenuAnimations.Value = v);
            y = ToggleRow(c, s, y, pad, cw, "打开菜单时阻止游戏操作", ModConfig.BlockGameInput.Value, v => ModConfig.BlockGameInput.Value = v);
            y = ToggleRow(c, s, y, pad, cw, "显示肢体状态", ModConfig.MenuShowLimbs.Value, v => ModConfig.MenuShowLimbs.Value = v);

            y += 10f * s;
            GUI.color = _p.Sub;
            GUI.Label(new Rect(c.x + pad, y, cw, 16f * s),
                "提示：所有功能默认关闭；配置文件修改后按 INSERT 重开菜单生效。", _small);
            GUI.color = Color.white;
        }

        private static string KeyName()
        {
            try { return ModConfig.ToggleKey.Value.MainKey.ToString(); } catch { return "Insert"; }
        }

        // ==================================================================
        // 组件
        // ==================================================================

                
        private static float Section(string title, Rect c, float s, float y, float pad)
        {
            GUI.color = _p.Sub;
            GUI.Label(new Rect(c.x + pad, y, c.width - pad * 2f, 16f * s), title.ToUpperInvariant(), _small);
            GUI.color = Color.white;
            return y + 20f * s;
        }

        private static float ActionButton(Rect c, float s, float y, float pad, float cw, string label, string desc, Action onClick)
        {
            var r = new Rect(c.x + pad, y, cw, 40f * s);
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? _p.Accent : _p.AccentDim);

            GUI.color = hover ? new Color(0.08f, 0.08f, 0.10f) : _p.Text;
            GUI.Label(new Rect(r.x + 14f * s, r.y, 200f * s, r.height), label, _btnText);
            GUI.color = hover ? new Color(0.12f, 0.12f, 0.14f) : _p.Sub;
            GUI.Label(new Rect(r.x + 170f * s, r.y, r.width - 180f * s, r.height), desc, _small);
            GUI.color = Color.white;

            if (Hit(r)) { try { onClick(); } catch (Exception e) { Log.ErrorOnce("gui.action", e); } }
            return y + 46f * s;
        }

        private static float ModuleCard(Rect c, float s, float y, float pad, float cw, string name, string desc,
            bool on, Action<bool> set, string key = null, Action settingsDrawer = null)
        {
            float h = 52f * s;
            var r = new Rect(c.x + pad, y, cw, h);
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? _p.CardHover : _p.Card);

            // 左侧状态条
            Fill(new Rect(r.x, r.y + 8f * s, 3f * s, h - 16f * s), on ? _p.Accent : new Color(1f, 1f, 1f, 0.06f));

            GUI.color = on ? _p.Text : (hover ? _p.Text : new Color(0.86f, 0.88f, 0.93f));
            GUI.Label(new Rect(r.x + 14f * s, r.y + 7f * s, 260f * s, 18f * s), name, _cardTitle);

            GUI.color = _p.Sub;
            GUI.Label(new Rect(r.x + 14f * s, r.y + 26f * s, cw - 130f * s, 16f * s), desc, _cardDesc);

            // 开关
            var sw = new Rect(r.xMax - 56f * s, r.y + 16f * s, 40f * s, 20f * s);
            DrawSwitch(sw, s, on, key ?? name);

            // 点击卡片任意处切换
            if (Hit(new Rect(r.x, r.y, r.width - 70f * s, r.height)))
            {
                set(!on);
            }
            // 点击开关
            if (Hit(sw)) set(!on);

            y += h + 8f * s;

            return y;
        }

        private static void DrawSwitch(Rect r, float s, bool on, string key)
        {
            float target = on ? 1f : 0f;
            float cur;
            if (!_anim.TryGetValue(key, out cur)) cur = target;
            cur = Mathf.MoveTowards(cur, target, Time.unscaledDeltaTime * 8f);
            _anim[key] = cur;

            Fill(r, Color.Lerp(new Color(0.22f, 0.23f, 0.28f), _p.Accent, cur));

            float knobW = r.height - 4f * s;
            float kx = Mathf.Lerp(r.x + 2f * s, r.xMax - knobW - 2f * s, cur);
            Fill(new Rect(kx, r.y + 2f * s, knobW, knobW), new Color(0.96f, 0.97f, 1f, Mathf.Lerp(0.75f, 1f, cur)));
        }

        private static float ToggleRow(Rect c, float s, float y, float pad, float cw, string label, bool on, Action<bool> set)
        {
            var r = new Rect(c.x + pad + 8f * s, y, cw - 8f * s, 34f * s);
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? _p.CardHover : new Color(0.09f, 0.095f, 0.115f));

            GUI.color = _p.Text;
            GUI.Label(new Rect(r.x + 12f * s, r.y, r.width - 90f * s, r.height), label, _cardDesc);

            var sw = new Rect(r.xMax - 52f * s, r.y + 7f * s, 40f * s, 20f * s);
            DrawSwitch(sw, s, on, label);

            if (Hit(r) || Hit(sw)) set(!on);
            return y + 38f * s;
        }

        private static float CycleRow(Rect c, float s, float y, float pad, float cw, string label, string value, string[] options, Action<string> set)
        {
            var r = new Rect(c.x + pad, y, cw, 40f * s);
            Fill(r, _p.Card);
            GUI.color = _p.Text;
            GUI.Label(new Rect(r.x + 12f * s, r.y, r.width - 160f * s, r.height), label, _cardDesc);

            var br = new Rect(r.xMax - 140f * s, r.y + 8f * s, 128f * s, 24f * s);
            bool hover = br.Contains(Event.current.mousePosition);
            Fill(br, hover ? _p.Accent : _p.AccentDim);
            GUI.color = hover ? new Color(0.08f, 0.08f, 0.10f) : _p.Text;
            GUI.Label(br, value, _btn);
            GUI.color = Color.white;

            if (Hit(br))
            {
                int idx = Array.IndexOf(options, value);
                set(options[(idx + 1) % options.Length]);
            }
            return y + 44f * s;
        }

        private static float SliderRow(Rect c, float s, float y, float pad, float cw, string label, float value,
            float min, float max, Action<float> set, string suffix = "")
        {
            var r = new Rect(c.x + pad + 8f * s, y, cw - 8f * s, 34f * s);
            Fill(r, new Color(0.09f, 0.095f, 0.115f));

            GUI.color = _p.Text;
            GUI.Label(new Rect(r.x + 12f * s, r.y, 140f * s, r.height), label, _cardDesc);

            var track = new Rect(r.x + 160f * s, r.y + 14f * s, r.width - 250f * s, 6f * s);
            Fill(track, new Color(0.2f, 0.21f, 0.25f));
            float t = Mathf.InverseLerp(min, max, value);
            Fill(new Rect(track.x, track.y, track.width * t, track.height), _p.Accent);
            Fill(new Rect(track.x + track.width * t - 4f * s, track.y - 4f * s, 8f * s, 14f * s), _p.Text);

            GUI.color = _p.Sub;
            GUI.Label(new Rect(r.xMax - 78f * s, r.y, 66f * s, r.height),
                (suffix == "m" ? value.ToString("0") : value.ToString("0.00")) + suffix, _small);

            // 点击轨道调整
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && track.Contains(Event.current.mousePosition))
            {
                float nt = Mathf.Clamp01((Event.current.mousePosition.x - track.x) / Mathf.Max(1f, track.width));
                set(Mathf.Lerp(min, max, nt));
                Event.current.Use();
            }
            GUI.color = Color.white;
            return y + 38f * s;
        }

        // ==================================================================
        // 图元
        // ==================================================================

        private static bool Hit(Rect r)
        {
            var e = Event.current;
            if (e == null) return false;
            if (e.type != EventType.MouseDown || e.button != 0) return false;
            if (!r.Contains(e.mousePosition)) return false;
            e.Use();
            return true;
        }

        private static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void Stroke(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
    }
}









