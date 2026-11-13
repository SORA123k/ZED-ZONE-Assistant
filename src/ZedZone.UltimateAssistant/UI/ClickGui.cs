using System;
using System.Collections.Generic;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>
    /// ClickGUI 主界面（v2）。
    ///
    /// 结构：固定外框 + 侧栏分类 + 右侧可滚动内容区。
    /// 支持：7 套主题、字体大小、三语本地化、鼠标滚轮滚动、可拖动滑条。
    /// </summary>
    internal static class ClickGui
    {
        internal static bool Open;

        private static readonly string[] Cats = { "combat", "player", "visuals", "settings" };
        private static int _cat;
        private static readonly float[] _scroll = new float[4];
        private static readonly float[] _lastContentH = new float[4];

        private static float _openT, _time;
        private static readonly Dictionary<string, float> _anim = new Dictionary<string, float>();
        private static string _activeSlider;

        // 位置/拖动
        private static Vector2 _pos;
        private static bool _posInit, _dragging;
        private static Vector2 _dragOffset;

        // 样式
        private static bool _stylesReady;
        private static int _styleFont = -1;
        private static string _styleTheme;
        private static GUIStyle _title, _catLbl, _small, _cardTitle, _cardDesc, _btn, _value, _section;

        private static float Sc => Mathf.Clamp(ModConfig.MenuScale?.Value ?? 1f, 0.7f, 1.6f);
        private static int Fs => Mathf.Clamp(ModConfig.MenuFontSize?.Value ?? 14, 12, 24);
        private static UiPalette P => ThemeManager.P;

        // ==================================================================
        internal static void Draw(PlayerSnapshot snap)
        {
            try
            {
                ThemeManager.Init();
                L.Init();
                EnsureStyles();
                try { Cursor.visible = true; Cursor.lockState = CursorLockMode.None; } catch { }

                _time = Time.unscaledTime;
                bool anim = ModConfig.MenuAnimations?.Value ?? true;
                _openT = anim ? Mathf.MoveTowards(_openT, Open ? 1f : 0f, Time.unscaledDeltaTime * 7f) : (Open ? 1f : 0f);
                if (_openT <= 0.001f && !Open) return;

                // 全局：任何 MouseUp 都释放滑条
                var ev = Event.current;
                if (ev != null && ev.type == EventType.MouseUp && _activeSlider != null)
                {
                    _activeSlider = null;
                }

                float s = Sc;
                float w = 780f * s, h = 500f * s;
                if (!_posInit)
                {
                    _pos = new Vector2(ModConfig.PanelX?.Value ?? 340, ModConfig.PanelY?.Value ?? 40);
                    _posInit = true;
                }
                float x = Mathf.Clamp(_pos.x, 0f, Mathf.Max(0f, Screen.width - w));
                float y = Mathf.Clamp(_pos.y, 0f, Mathf.Max(0f, Screen.height - h));

                var area = new Rect(x, y, w, h);
                var oldColor = GUI.color;
                float alpha = anim ? Mathf.Clamp01(_openT * 1.25f) : 1f;

                Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.35f * _openT));

                GUI.color = new Color(1f, 1f, 1f, alpha);
                Fill(area, P.Window);
                Stroke(area, P.Line, 1f);

                // ---- 标题栏 ----
                float hh = (Fs + 26f) * s;
                var header = new Rect(area.x, area.y, area.width, hh);
                Fill(header, P.Header);
                Fill(new Rect(area.x, header.yMax - 1f, area.width, 1f), P.Line);

                GUI.color = P.Text;
                GUI.Label(new Rect(header.x + 16f * s, header.y, 420f * s, header.height), L.T("title"), _title);

                GUI.color = P.SubText;
                GUI.Label(new Rect(header.xMax - 200f * s, header.y, 184f * s, header.height), L.T("hint_close"), _small);

                HandleHeaderDrag(header, w, h);

                // ---- 侧栏 ----
                float sw = 170f * s;
                var side = new Rect(area.x, header.yMax, sw, area.height - header.height);
                Fill(side, P.Sidebar);
                Fill(new Rect(side.xMax - 1f, side.y, 1f, side.height), P.Line);

                float cy = side.y + 12f * s;
                float rowH = (Fs + 16f) * s;
                for (int i = 0; i < Cats.Length; i++)
                {
                    var r = new Rect(side.x + 10f * s, cy, side.width - 20f * s, rowH);
                    bool active = i == _cat;
                    bool hover = r.Contains(Event.current.mousePosition);
                    if (active) Fill(r, P.CardActive);
                    else if (hover) Fill(r, P.CardHover);
                    if (active) Fill(new Rect(r.x, r.y + 5f * s, 3f * s, r.height - 10f * s), P.Accent);

                    GUI.color = active ? P.Text : P.SubText;
                    GUI.Label(new Rect(r.x + 14f * s, r.y, r.width - 18f * s, r.height), L.T(Cats[i]), _catLbl);
                    if (Hit(r)) _cat = i;
                    cy += rowH + 4f * s;
                }

                // 侧栏底部状态
                GUI.color = P.SubText;
                float sy = side.yMax - 58f * s;
                GUI.Label(new Rect(side.x + 12f * s, sy, side.width - 20f * s, 14f * s),
                    (Features.InfiniteAmmoOn ? "● " : "○ ") + L.T("infinite_ammo"), _small);
                GUI.Label(new Rect(side.x + 12f * s, sy + 15f * s, side.width - 20f * s, 14f * s),
                    (Features.WeaponDurabilityOn ? "● " : "○ ") + L.T("gun_dur"), _small);
                GUI.Label(new Rect(side.x + 12f * s, sy + 30f * s, side.width - 20f * s, 14f * s),
                    (Features.ZombieEspOn ? "● " : "○ ") + L.T("esp"), _small);

                // ---- 内容区（滚动容器）----
                var content = new Rect(side.xMax, header.yMax, area.xMax - side.xMax, area.height - header.height);
                DrawScrollingContent(content, s, snap);

                GUI.color = oldColor;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("gui.draw", e);
            }
        }

        // ==================================================================
        private static void DrawScrollingContent(Rect clip, float s, PlayerSnapshot snap)
        {
            float pad = 14f * s;
            float contentH = _lastContentH[_cat];
            float viewH = clip.height;

            // 滚轮
            var e = Event.current;
            if (e != null && e.type == EventType.ScrollWheel && clip.Contains(e.mousePosition))
            {
                _scroll[_cat] = Mathf.Clamp(_scroll[_cat] + e.delta.y * 34f * s, 0f, Mathf.Max(0f, contentH - viewH + pad * 2f));
                e.Use();
            }

            float maxScroll = Mathf.Max(0f, contentH + pad * 2f - viewH);
            _scroll[_cat] = Mathf.Clamp(_scroll[_cat], 0f, maxScroll);

            GUI.BeginClip(clip);
            float y = pad - _scroll[_cat];
            float cw = clip.width - pad * 2f - 8f * s;   // 给滚动条留空间
            float cx = pad;

            switch (_cat)
            {
                case 0: DrawCombat(cx, cw, s, ref y); break;
                case 1: DrawPlayer(cx, cw, s, ref y, snap); break;
                case 2: DrawVisuals(cx, cw, s, ref y); break;
                default: DrawSettings(cx, cw, s, ref y); break;
            }

            _lastContentH[_cat] = Mathf.Max(0f, y - pad + _scroll[_cat]);

            // 滚动条（容器内右侧）
            float total = contentH + pad * 2f;
            if (total > viewH)
            {
                float trackX = clip.width - 7f * s;
                Fill(new Rect(trackX, 4f * s, 3f * s, clip.height - 8f * s), P.ScrollTrack);
                float th = Mathf.Max(24f * s, (clip.height - 8f * s) * (viewH / total));
                float tpos = Mathf.Clamp01(_scroll[_cat] / Mathf.Max(1f, total - viewH));
                float ty = 4f * s + tpos * (clip.height - 8f * s - th);
                Fill(new Rect(trackX, ty, 3f * s, th), P.ScrollThumb);
            }
            GUI.EndClip();
        }

        // ==================================================================
        // 组件
        // ==================================================================
        private static float Section(string key, float cx, float cw, float s, ref float y)
        {
            y += 6f * s;
            GUI.color = P.Accent;
            GUI.Label(new Rect(cx, y, cw, (Fs + 2) * s), L.T(key), _section);
            GUI.color = P.Line;
            Fill(new Rect(cx, y + (Fs + 6) * s, cw, 1f), P.Line);
            y += (Fs + 14) * s;
            return y;
        }

        private static float Card(string titleKey, string descKey, bool on, Action<bool> set,
            float cx, float cw, float s, ref float y)
        {
            float ch = (Fs + 30f) * s;
            var r = new Rect(cx, y, cw, ch);
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? P.CardHover : P.Card);
            Fill(new Rect(r.x, r.y + 6f * s, 3f * s, ch - 12f * s), on ? P.Accent : new Color(1f, 1f, 1f, 0.06f));

            GUI.color = P.Text;
            GUI.Label(new Rect(r.x + 12f * s, r.y + 4f * s, cw - 90f * s, (Fs + 4) * s), L.T(titleKey), _cardTitle);
            GUI.color = P.SubText;
            GUI.Label(new Rect(r.x + 12f * s, r.y + (Fs + 8) * s, cw - 90f * s, (Fs + 2) * s), L.T(descKey), _cardDesc);

            var sw = new Rect(r.xMax - 52f * s, r.y + (ch - 20f * s) * 0.5f, 40f * s, 20f * s);
            DrawSwitch(sw, s, on, titleKey);

            if (Hit(new Rect(r.x, r.y, r.width - 64f * s, r.height)) || Hit(sw)) set(!on);
            y += ch + 6f * s;
            return y;
        }

        private static void DrawSwitch(Rect r, float s, bool on, string key)
        {
            float cur;
            if (!_anim.TryGetValue(key, out cur)) cur = on ? 1f : 0f;
            cur = Mathf.MoveTowards(cur, on ? 1f : 0f, Time.unscaledDeltaTime * 9f);
            _anim[key] = cur;

            Fill(r, Color.Lerp(P.Track, P.Accent, cur));
            float kw = r.height - 4f * s;
            float kx = Mathf.Lerp(r.x + 2f * s, r.xMax - kw - 2f * s, cur);
            Fill(new Rect(kx, r.y + 2f * s, kw, kw), P.Knob);
        }

        private static float ActionCard(string titleKey, string descKey, Action act, float cx, float cw, float s, ref float y)
        {
            float ch = (Fs + 26f) * s;
            var r = new Rect(cx, y, cw, ch);
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? P.Accent : P.CardHover);
            GUI.color = hover ? P.AccentText : P.Text;
            GUI.Label(new Rect(r.x + 12f * s, r.y, cw * 0.45f, ch), L.T(titleKey), _btn);
            GUI.color = hover ? P.AccentText : P.SubText;
            GUI.Label(new Rect(r.x + cw * 0.45f, r.y, cw * 0.53f, ch), L.T(descKey), _small);
            if (Hit(r)) { try { act(); } catch (Exception e) { Log.ErrorOnce("gui.act", e); } }
            y += ch + 6f * s;
            return y;
        }

        private static float ToggleRow(string labelKey, bool on, Action<bool> set, float cx, float cw, float s, ref float y)
        {
            float rh = (Fs + 16f) * s;
            var r = new Rect(cx + 6f * s, y, cw - 6f * s, rh);
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, hover ? P.CardHover : P.Card);
            GUI.color = P.Text;
            GUI.Label(new Rect(r.x + 10f * s, r.y, r.width - 80f * s, rh), L.T(labelKey), _cardDesc);
            var sw = new Rect(r.xMax - 50f * s, r.y + (rh - 20f * s) * 0.5f, 40f * s, 20f * s);
            DrawSwitch(sw, s, on, labelKey);
            if (Hit(r) || Hit(sw)) set(!on);
            y += rh + 4f * s;
            return y;
        }

        /// <summary>可点击 + 可连续拖动的滑条（修复原有"无法拖动"问题）。</summary>
        private static float SliderRow(string labelKey, string id, float value, float min, float max, float step,
            Action<float> set, string fmt, float cx, float cw, float s, ref float y)
        {
            float rh = (Fs + 20f) * s;
            var r = new Rect(cx + 6f * s, y, cw - 6f * s, rh);
            Fill(r, P.Card);

            GUI.color = P.Text;
            GUI.Label(new Rect(r.x + 10f * s, r.y, 150f * s, rh), L.T(labelKey), _cardDesc);

            var track = new Rect(r.x + 150f * s, r.y + rh * 0.5f - 3f * s, r.width - 260f * s, 6f * s);
            float t = Mathf.InverseLerp(min, max, value);
            Fill(track, P.Track);
            Fill(new Rect(track.x, track.y, track.width * t, track.height), P.Accent);
            Fill(new Rect(track.x + track.width * t - 5f * s, track.y - 5f * s, 10f * s, 16f * s), P.Knob);

            GUI.color = P.SubText;
            GUI.Label(new Rect(r.xMax - 96f * s, r.y, 86f * s, rh), value.ToString(fmt), _value);

            var e = Event.current;
            bool over = track.Contains(e.mousePosition) || r.Contains(e.mousePosition);
            if (e.type == EventType.MouseDown && e.button == 0 && over)
            {
                _activeSlider = id;
                e.Use();
            }
            if (_activeSlider == id && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
            {
                float nt = Mathf.Clamp01((e.mousePosition.x - track.x) / Mathf.Max(1f, track.width));
                float nv = Mathf.Lerp(min, max, nt);
                if (step > 0f) nv = Mathf.Round(nv / step) * step;
                nv = Mathf.Clamp(nv, min, max);
                set(nv);
                e.Use();
            }
            if (e.type == EventType.MouseUp && _activeSlider == id) _activeSlider = null;

            y += rh + 4f * s;
            return y;
        }

        private static float CycleRow(string labelKey, string value, string[] options, Func<string, string> display,
            Action<string> set, float cx, float cw, float s, ref float y)
        {
            float rh = (Fs + 20f) * s;
            var r = new Rect(cx + 6f * s, y, cw - 6f * s, rh);
            Fill(r, P.Card);
            GUI.color = P.Text;
            GUI.Label(new Rect(r.x + 10f * s, r.y, r.width - 190f * s, rh), L.T(labelKey), _cardDesc);

            var br = new Rect(r.xMax - 176f * s, r.y + 4f * s, 166f * s, rh - 8f * s);
            bool hover = br.Contains(Event.current.mousePosition);
            Fill(br, hover ? P.Accent : P.CardHover);
            GUI.color = hover ? P.AccentText : P.Text;
            GUI.Label(br, display(value), _btn);
            if (Hit(br))
            {
                int i = Array.IndexOf(options, value);
                set(options[(i + 1) % options.Length]);
            }
            y += rh + 4f * s;
            return y;
        }

        // ==================================================================
        // 页面
        // ==================================================================
        private static void DrawCombat(float cx, float cw, float s, ref float y)
        {
            Section("sec_combat", cx, cw, s, ref y);
            Card("infinite_ammo", "infinite_ammo_d", Features.InfiniteAmmoOn,
                v => { ModConfig.InfiniteAmmo.Value = v; Features.SetInfiniteAmmo(v); }, cx, cw, s, ref y);
            Card("no_reload", "no_reload_d", Features.NoReloadOn,
                v => { ModConfig.NoReload.Value = v; Features.SetNoReload(v); }, cx, cw, s, ref y);
            Card("no_jam", "no_jam_d", Features.NoJamOn,
                v => { ModConfig.NoJam.Value = v; Features.SetNoJam(v); }, cx, cw, s, ref y);
            Card("item_dur", "item_dur_d", Features.ItemDurabilityLockOn,
                v => { ModConfig.ItemDurabilityLock.Value = v; Features.SetItemDurabilityLock(v); }, cx, cw, s, ref y);
            Card("gun_dur", "gun_dur_d", Features.WeaponDurabilityOn,
                v => { ModConfig.WeaponDurability.Value = v; Features.SetWeaponDurability(v); }, cx, cw, s, ref y);
            Card("no_overheat", "no_overheat_d", Features.NoOverheatOn,
                v => { ModConfig.NoOverheat.Value = v; Features.SetNoOverheat(v); }, cx, cw, s, ref y);
            ActionCard("fix_jam", "fix_jam_d", () => WeaponUtil.ClearMalfunction(), cx, cw, s, ref y);
        }

        private static void DrawPlayer(float cx, float cw, float s, ref float y, PlayerSnapshot snap)
        {
            Section("sec_guard", cx, cw, s, ref y);
            Card("no_hp", "no_hp_d", Features.NoHpLossOn,
                v => { ModConfig.NoHpLoss.Value = v; Features.SetNoHpLoss(v); }, cx, cw, s, ref y);
            Card("no_sta", "no_sta_d", Features.NoStaminaLossOn,
                v => { ModConfig.NoStaminaLoss.Value = v; Features.SetNoStaminaLoss(v); }, cx, cw, s, ref y);
            Card("no_hunger", "no_hunger_d", Features.NoHungerLossOn,
                v => { ModConfig.NoHungerLoss.Value = v; Features.SetNoHungerLoss(v); }, cx, cw, s, ref y);
            Card("no_thirst", "no_thirst_d", Features.NoThirstLossOn,
                v => { ModConfig.NoThirstLoss.Value = v; Features.SetNoThirstLoss(v); }, cx, cw, s, ref y);

            Section("sec_move", cx, cw, s, ref y);
            Card("always_sprint", "always_sprint_d", Features.AlwaysSprintOn,
                v => { ModConfig.AlwaysSprint.Value = v; Features.SetAlwaysSprint(v); }, cx, cw, s, ref y);
            Card("move_speed", "move_speed_d", Features.MoveSpeedOn,
                v => { ModConfig.MoveSpeed.Value = v; Features.SetMoveSpeed(v); }, cx, cw, s, ref y);
            SliderRow("multiplier", "move_speed", ModConfig.MoveSpeedMultiplier.Value, 1.0f, 3.0f, 0.1f,
                v => ModConfig.MoveSpeedMultiplier.Value = v, "0.0'x'", cx, cw, s, ref y);
            ActionCard("reset", "move_speed_d", () => ModConfig.MoveSpeedMultiplier.Value = 1.0f, cx, cw, s, ref y);

            Card("timer", "timer_d", Features.TimerOn,
                v => { ModConfig.Timer.Value = v; Features.SetTimer(v); }, cx, cw, s, ref y);
            SliderRow("multiplier", "timer", ModConfig.TimerMultiplier.Value, 1.0f, 5.0f, 0.1f,
                v => ModConfig.TimerMultiplier.Value = v, "0.0'x'", cx, cw, s, ref y);
            ActionCard("reset", "timer_d", () => ModConfig.TimerMultiplier.Value = 1.0f, cx, cw, s, ref y);

            Section("sec_restore", cx, cw, s, ref y);
            if (snap == null || !snap.HasPlayer)
            {
                GUI.color = P.Warn;
                GUI.Label(new Rect(cx, y, cw, 18f * s), L.T("no_player"), _small);
                GUI.color = P.Text;
                y += 22f * s;
            }
            RestoreCard("health", "hp", StatId.Hp, snap, cx, cw, s, ref y);
            RestoreCard("energy", "energy", StatId.Energy, snap, cx, cw, s, ref y);
            RestoreCard("hunger", "hunger", StatId.Hunger, snap, cx, cw, s, ref y);
            RestoreCard("thirst", "thirst", StatId.Thirst, snap, cx, cw, s, ref y);

            if (ModConfig.MenuShowLimbs?.Value ?? true)
            {
                Section("limbs", cx, cw, s, ref y);
                DrawLimbs(cx, cw, s, ref y);
            }
        }

        private static void RestoreCard(string labelKey, string statKey, StatId stat, PlayerSnapshot snap,
            float cx, float cw, float s, ref float y)
        {
            float ch = (Fs + 22f) * s;
            var r = new Rect(cx, y, cw, ch);
            Fill(r, P.Card);
            GUI.color = P.Text;
            GUI.Label(new Rect(r.x + 12f * s, r.y, 160f * s, ch), L.T(labelKey), _cardDesc);

            string val = "—";
            if (snap != null && snap.HasPlayer)
            {
                var v = snap.Get(statKey);
                if (v != null) val = v.HasMax ? $"{v.Value:0.#} / {v.Max:0.#}" : v.ValueText();
            }
            GUI.color = P.SubText;
            GUI.Label(new Rect(r.x + 170f * s, r.y, 140f * s, ch), val, _small);

            var br = new Rect(r.xMax - 96f * s, r.y + (ch - 26f * s) * 0.5f, 86f * s, 26f * s);
            bool hov = br.Contains(Event.current.mousePosition);
            Fill(br, hov ? P.Accent : P.CardHover);
            GUI.color = hov ? P.AccentText : P.Text;
            GUI.Label(br, L.T("restore"), _btn);
            if (Hit(br)) { try { RestoreService.Restore(stat); } catch (Exception e) { Log.ErrorOnce("gui.restore", e); } }
            y += ch + 4f * s;
        }

        private static void DrawLimbs(float cx, float cw, float s, ref float y)
        {
            if (!LimbData.HasPlayer || LimbData.Items.Count == 0)
            {
                GUI.color = P.SubText;
                GUI.Label(new Rect(cx, y, cw, 16f * s), LimbData.Error ?? L.T("no_limb"), _small);
                GUI.color = P.Text;
                y += 20f * s;
                return;
            }
            for (int i = 0; i < LimbData.Items.Count; i++)
            {
                var L2 = LimbData.Items[i];
                float rh = (Fs + 6f) * s;
                var r = new Rect(cx, y, cw, rh);
                Fill(r, P.Card);
                GUI.color = P.Text;
                GUI.Label(new Rect(r.x + 8f * s, r.y, 60f * s, rh), L2.Name, _small);
                GUI.color = P.SubText;
                GUI.Label(new Rect(r.x + 70f * s, r.y, 110f * s, rh), L2.HpText() + " / " + L2.MaxText(), _small);

                var bar = new Rect(r.x + 186f * s, r.y + rh * 0.5f - 3f * s, 110f * s, 6f * s);
                Fill(bar, P.Track);
                float pc = L2.Percent01();
                if (!float.IsNaN(pc))
                {
                    GUI.color = pc >= 0.999f ? P.Good : (pc >= 0.5f ? P.Warn : P.Bad);
                    Fill(new Rect(bar.x, bar.y, bar.width * pc, bar.height), GUI.color);
                }
                GUI.color = L2.Bleeding ? P.Bad : (L2.Wounded ? P.Warn : P.SubText);
                GUI.Label(new Rect(r.x + 306f * s, r.y, cw - 310f * s, rh),
                    L.T(L2.Bleeding ? "bleeding" : (L2.Wounded ? "wounded" : (L2.Recovered ? "intact" : "normal"))), _small);
                GUI.color = P.Text;
                y += rh + 2f * s;
            }
        }

        private static void DrawVisuals(float cx, float cw, float s, ref float y)
        {
            Section("sec_visuals", cx, cw, s, ref y);
            Card("esp", "esp_d", Features.ZombieEspOn,
                v => { ModConfig.ZombieEsp.Value = v; Features.SetZombieEsp(v); }, cx, cw, s, ref y);
            ToggleRow("esp_name", ModConfig.EspShowName.Value, v => ModConfig.EspShowName.Value = v, cx, cw, s, ref y);
            ToggleRow("esp_dist", ModConfig.EspShowDistance.Value, v => ModConfig.EspShowDistance.Value = v, cx, cw, s, ref y);
            ToggleRow("esp_box", ModConfig.EspDrawBox.Value, v => ModConfig.EspDrawBox.Value = v, cx, cw, s, ref y);
            SliderRow("esp_max", "esp_dist", ModConfig.EspMaxDistance.Value, 20f, 500f, 5f,
                v => ModConfig.EspMaxDistance.Value = v, "0'm'", cx, cw, s, ref y);
        }

        private static void DrawSettings(float cx, float cw, float s, ref float y)
        {
            Section("sec_general", cx, cw, s, ref y);
            SliderRow("menu_scale", "scale", ModConfig.MenuScale.Value, 0.7f, 1.6f, 0.05f,
                v => ModConfig.MenuScale.Value = v, "0.00", cx, cw, s, ref y);
            ToggleRow("animations", ModConfig.MenuAnimations.Value, v => ModConfig.MenuAnimations.Value = v, cx, cw, s, ref y);
            ToggleRow("block_input", ModConfig.BlockGameInput.Value, v => ModConfig.BlockGameInput.Value = v, cx, cw, s, ref y);
            ToggleRow("show_limbs", ModConfig.MenuShowLimbs.Value, v => ModConfig.MenuShowLimbs.Value = v, cx, cw, s, ref y);

            Section("sec_appearance", cx, cw, s, ref y);
            CycleRow("theme", ThemeManager.Current, ThemeManager.Keys, k => ThemeManager.Display(k),
                k => ThemeManager.Set(k), cx, cw, s, ref y);
            SliderRow("font_size", "font", Fs, 12f, 24f, 1f,
                v => ModConfig.MenuFontSize.Value = Mathf.RoundToInt(v), "0", cx, cw, s, ref y);
            ActionCard("reset", "font_size", () => ModConfig.MenuFontSize.Value = 14, cx, cw, s, ref y);

            Section("sec_language", cx, cw, s, ref y);
            CycleRow("language", L.LangCode(), L.LangCodes, c => L.LangDisplay(c),
                c => L.SetLang(c), cx, cw, s, ref y);

            Section("sec_controls", cx, cw, s, ref y);
            GUI.color = P.SubText;
            GUI.Label(new Rect(cx, y, cw, 18f * s), L.T("hotkey") + ": " + KeyName(), _small);
            y += 20f * s;
            GUI.Label(new Rect(cx, y, cw, 18f * s), L.T("hotkey_hint"), _small);
            y += 24f * s;
            GUI.color = P.SubText;
            GUI.Label(new Rect(cx, y, cw, 16f * s), L.T("footer_hint"), _small);
            y += 20f * s;
        }

        private static string KeyName()
        {
            try { return ModConfig.ToggleKey.Value.MainKey.ToString(); } catch { return "Insert"; }
        }

        // ==================================================================
        // 样式 / 图元
        // ==================================================================
        private static void EnsureStyles()
        {
            int fs = Fs;
            string th = ThemeManager.Current;
            if (_stylesReady && _styleFont == fs && _styleTheme == th) return;

            try
            {
                Font f = ResolveCjkFont();
                _title = New(f, fs + 3, FontStyle.Bold, TextAnchor.MiddleLeft);
                _catLbl = New(f, fs, FontStyle.Normal, TextAnchor.MiddleLeft);
                _small = New(f, Mathf.Max(10, fs - 3), FontStyle.Normal, TextAnchor.MiddleLeft);
                _cardTitle = New(f, fs, FontStyle.Bold, TextAnchor.MiddleLeft);
                _cardDesc = New(f, Mathf.Max(10, fs - 3), FontStyle.Normal, TextAnchor.MiddleLeft);
                _btn = New(f, Mathf.Max(10, fs - 2), FontStyle.Bold, TextAnchor.MiddleCenter);
                _value = New(f, Mathf.Max(10, fs - 2), FontStyle.Normal, TextAnchor.MiddleRight);
                _section = New(f, Mathf.Max(10, fs - 3), FontStyle.Bold, TextAnchor.MiddleLeft);

                _styleFont = fs;
                _styleTheme = th;
                _stylesReady = true;
            }
            catch { _stylesReady = false; }
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

        private static Font _cjk;
        private static bool _cjkTried;

        private static Font ResolveCjkFont()
        {
            if (_cjkTried && _cjk != null) return _cjk;
            _cjkTried = true;
            try
            {
                var f = PlayerStatusPanel.instance.daysText.font;
                if (f != null && f.HasCharacter('中')) { _cjk = f; return _cjk; }
            }
            catch { }
            try
            {
                var texts = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Text>();
                for (int i = 0; i < texts.Length && i < 400; i++)
                {
                    var t = texts[i];
                    if (t == null) continue;
                    var f = t.font;
                    if (f != null && f.HasCharacter('中')) { _cjk = f; return _cjk; }
                }
            }
            catch { }
            try { _cjk = GUI.skin.font; } catch { }
            return _cjk;
        }

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
                _pos = new Vector2(Mathf.Clamp(p.x, 0f, Mathf.Max(0f, Screen.width - w)),
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
