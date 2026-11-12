using System;
using System.Collections.Generic;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>
    /// 面板渲染器（**普通类，不是 MonoBehaviour**）。
    ///
    /// 为什么要拆出来：
    ///   Il2CppInterop 会为 class-injected MonoBehaviour 的**所有**方法生成 il2cpp 包装，
    ///   当参数是本程序集的自定义类型（PlayerSnapshot / ReadValue）时它会打印
    ///   "has unsupported parameter" 警告。这些方法本来就是纯托管逻辑、只在 OnGUI 内部调用，
    ///   拆到普通类后 MonoBehaviour 只保留 Unity 生命周期方法，警告消失。
    ///
    /// 中文字体：Unity 内置 GUI 字体不含 CJK 字形，直接用会显示成 □。
    /// 这里用 <see cref="Font.CreateDynamicFontFromOSFont(string[], int)"/> 动态加载系统字体。
    /// </summary>
    internal sealed class PanelRenderer
    {
        private bool _init;
        private Font _font;

        private GUIStyle _panel, _title, _tab, _tabActive, _label, _value, _note, _fail, _warn, _hint;
        private Texture2D _texPanel, _texBarBg, _texBarOk, _texBarLow, _texBarCrit;

        internal int Tab;
        internal string ConflictMsg = "未检测";
        internal KeyCode Key = KeyCode.Insert;
        internal int PatchCount;
        internal ModuleRegistry Registry;

        private static readonly string[] TabNames = { "玩家", "生命", "饱食", "水分", "体力/精力", "状态", "设置" };

        private static readonly Color ColText = new Color(0.80f, 0.82f, 0.86f);
        private static readonly Color ColValue = new Color(0.94f, 0.96f, 0.99f);
        private static readonly Color ColTitle = new Color(0.55f, 0.86f, 1f);
        private static readonly Color ColNote = new Color(0.52f, 0.56f, 0.62f);
        private static readonly Color ColFail = new Color(1f, 0.42f, 0.38f);
        private static readonly Color ColWarn = new Color(1f, 0.76f, 0.36f);

        private void Init()
        {
            _init = true;

            // ---- CJK 字体 ----
            // 注意：本构建的 Font 没有 string[] 重载的托管包装可用（传 string[] 会 MissingMethodException），
            // 必须用单名重载 CreateDynamicFontFromOSFont(string,int)，并用 HasCharacter('中') 验证真的含 CJK。
            _font = TryBorrowGameFont();
            if (_font == null) _font = TryLoadCjkFont();   // 兜底：万一 OS 字体可用
            if (_font == null) Log.WarnOnce("ui.font.null", "未能加载中文字体，界面中文将显示为方块。");

            _texPanel = MakeTex(new Color(0.045f, 0.055f, 0.07f, 0.96f));
            _texBarBg = MakeTex(new Color(0.17f, 0.18f, 0.20f, 1f));
            _texBarOk = MakeTex(new Color(0.25f, 0.72f, 0.45f, 1f));
            _texBarLow = MakeTex(new Color(0.95f, 0.72f, 0.20f, 1f));
            _texBarCrit = MakeTex(new Color(0.92f, 0.30f, 0.28f, 1f));

            float s = ModConfig.Scale;
            int fs = Mathf.Max(12, Mathf.RoundToInt(13 * s));

            _panel = new GUIStyle(GUI.skin.box) { normal = { background = _texPanel } };
            if (_font != null) _panel.font = _font;

            _title = NewLabel(fs + 3, ColTitle, FontStyle.Bold);
            _tab = NewButton(fs, ColText, FontStyle.Normal);
            _tabActive = NewButton(fs, new Color(1f, 0.85f, 0.35f), FontStyle.Bold);
            _label = NewLabel(fs, ColText, FontStyle.Normal);
            _value = NewLabel(fs, ColValue, FontStyle.Bold);
            _fail = NewLabel(fs, ColFail, FontStyle.Bold);
            _note = NewLabel(Mathf.Max(10, fs - 2), ColNote, FontStyle.Normal);
            _warn = NewLabel(Mathf.Max(10, fs - 1), ColWarn, FontStyle.Normal);
            _warn.wordWrap = true;
            _hint = NewLabel(Mathf.Max(10, fs - 2), ColNote, FontStyle.Normal);
        }

        private static readonly string[] CjkFontCandidates =
        {
            "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "Microsoft JhengHei", "SimSun", "Noto Sans CJK SC"
        };

        /// <summary>尝试逐个系统字体创建动态字体，并用 HasCharacter 验证确实含中文字形。</summary>
        private Font TryLoadCjkFont()
        {
            foreach (var name in CjkFontCandidates)
            {
                try
                {
                    var f = Font.CreateDynamicFontFromOSFont(name, 16);
                    if (f == null) continue;
                    bool ok;
                    try { ok = f.HasCharacter('中'); }
                    catch { ok = true; }   // 校验不可用时不阻断，先用着
                    if (ok)
                    {
                        Log.Info($"[ui] 中文字体已加载: {name}");
                        return f;
                    }
                }
                catch (Exception e)
                {
                    Log.WarnOnce("ui.font." + name, "字体候选失败 " + name + ": " + e.GetType().Name);
                }
            }
            return null;
        }

        /// <summary>
        /// 借用游戏自带的字体。
        ///
        /// 为什么不直接用 OS 字体：本构建里 `Font.CreateDynamicFontFromOSFont` 会抛
        /// NotSupportedException（该 ICall 在 IL2CPP 构建中被剥离），无法动态加载系统字体。
        /// 但游戏界面本身能显示中文，说明工程里必有一个含 CJK 字形的 Font 资源；
        /// 从任意一个已存在的 UI Text 上借来即可，零额外依赖、零资源打包。
        /// </summary>
        private Font TryBorrowGameFont()
        {
            // 路径 1：游戏 HUD 面板（最便宜，通常常驻）
            try
            {
                var panel = PlayerStatusPanel.instance;
                if (panel != null)
                {
                    var txt = panel.daysText;
                    if (txt != null)
                    {
                        var f = txt.font;
                        if (f != null && SafeHasCjk(f)) { Log.Info("[ui] 中文字体来源: PlayerStatusPanel.daysText.font"); return f; }
                    }
                }
            }
            catch (Exception e) { Log.WarnOnce("ui.font.panel", "PlayerStatusPanel 字体不可用: " + e.GetType().Name); }

            // 路径 2：扫描场景内所有 UI Text，取第一个含 CJK 的字体
            try
            {
                var texts = UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Text>(true);
                int n = texts == null ? 0 : texts.Length;
                int limit = Math.Min(n, 400);
                for (int i = 0; i < limit; i++)
                {
                    var t = texts[i];
                    if (t == null) continue;
                    var f = t.font;
                    if (f != null && SafeHasCjk(f))
                    {
                        Log.Info("[ui] 中文字体来源: 场景 UI Text #" + i + " (" + t.font.name + ")");
                        return f;
                    }
                }
                Log.WarnOnce("ui.font.scan", "扫描 " + n + " 个 UI Text 未找到含中文字形的字体。");
            }
            catch (Exception e) { Log.WarnOnce("ui.font.scan.ex", "扫描 UI Text 失败: " + e.GetType().Name); }

            return null;
        }

        private static bool SafeHasCjk(Font f)
        {
            try { return f.HasCharacter('中') && f.HasCharacter('生'); }
            catch { return true; }   // 校验不可用时不阻断
        }

        private GUIStyle NewLabel(int size, Color c, FontStyle fs)
        {
            var st = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs };
            if (_font != null) st.font = _font;
            st.normal.textColor = c;
            return st;
        }

        private GUIStyle NewButton(int size, Color c, FontStyle fs)
        {
            var st = new GUIStyle(GUI.skin.button) { fontSize = size, fontStyle = fs };
            if (_font != null) st.font = _font;
            st.normal.textColor = c;
            return st;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private float LineH => Mathf.RoundToInt((_label?.fontSize ?? 13) * 1.7f);
        private int Pad => Mathf.RoundToInt(12 * ModConfig.Scale);

        // ------------------------------------------------------------------
        internal void Draw(PlayerSnapshot snap)
        {
            if (!_init) Init();

            float s = ModConfig.Scale;
            float w = Mathf.RoundToInt(600 * s);
            float h = Mathf.RoundToInt(600 * s);
            float x = ModConfig.PanelX?.Value ?? 380;
            float y = ModConfig.PanelY?.Value ?? 40;

            // 越界保护
            if (x + w > Screen.width) x = Mathf.Max(0, Screen.width - w - 8);
            if (y + h > Screen.height) y = Mathf.Max(0, Screen.height - h - 8);
            if (ModConfig.PanelX != null && x != ModConfig.PanelX.Value) { /* 仅本次调整，不回写配置 */ }

            var rect = new Rect(x, y, w, h);
            GUI.Box(rect, GUIContent.none, _panel);

            float cx = rect.x + Pad;
            float cy = rect.y + Pad;
            float inner = rect.width - Pad * 2;

            GUI.Label(new Rect(cx, cy, inner, LineH + 6), "Ultimate Assistant · 只读 + 一键恢复 (Phase 3A)", _title);
            cy += LineH + 8;

            string sub = snap == null
                ? "尚未读取"
                : $"场景: {snap.SceneName}   玩家: {(snap.HasPlayer ? "已找到" : "未找到")}   " +
                  $"死亡: {(snap.IsDead ? "是" : "否")}   {snap.At:HH:mm:ss}";
            GUI.Label(new Rect(cx, cy, inner, LineH), sub, _hint);
            cy += LineH + 4;

            // 联机硬禁用提示（fail-closed：检测异常也按联机显示）
            if (PlayerWriteGate.IsMultiplayer(out string mpWhy))
            {
                GUI.Label(new Rect(cx, cy, inner, LineH),
                    "⚠ 联机模式：所有状态修改已被禁用（" + mpWhy + "）", _fail);
                cy += LineH + 2;
            }

            float tabW = inner / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                if (GUI.Button(new Rect(cx + tabW * i, cy, tabW - 2, LineH + 4),
                        (i + 1) + "." + TabNames[i], i == Tab ? _tabActive : _tab))
                {
                    Tab = i;
                }
            }
            cy += LineH + 10;

            DrawTabContent(cx, cy, inner, rect.yMax - Pad - LineH - 6 - cy, snap);

            string hint = $"{Key} 开关 · Esc 关闭 · 数字键 1-7 切页 · 按键冲突: {ConflictMsg}";
            GUI.Label(new Rect(cx, rect.yMax - Pad - LineH, inner, LineH), hint, _hint);
        }

        private void DrawTabContent(float x, float y, float w, float h, PlayerSnapshot snap)
        {
            if (snap == null) { GUI.Label(new Rect(x, y, w, LineH), "尚未读取到数据……", _hint); return; }

            if (!snap.HasPlayer)
            {
                GUI.Label(new Rect(x, y, w, LineH * 2), "未找到玩家对象（主菜单 / 加载中 / 已死亡未重生）", _fail);
                y += LineH * 2;
                for (int i = 0; i < snap.Warnings.Count && i < 4; i++)
                {
                    GUI.Label(new Rect(x, y, w, LineH), "· " + snap.Warnings[i], _warn);
                    y += LineH;
                }
                GUI.Label(new Rect(x, y + LineH * 0.5f, w, LineH),
                    "说明：这是正常状态，不是错误；进入存档后会自动出现数据。", _note);
                return;
            }

            switch (Tab)
            {
                case 0:
                    y = DrawPlayerTab(x, y, w, snap);
                    y = DrawRestoreButtons(x, y, w, new[] { StatId.Hp, StatId.Energy, StatId.Hunger, StatId.Thirst },
                        new[] { "恢复生命", "恢复精力", "恢复饱食", "恢复水分" });
                    y = DrawRestoreButtons(x, y, w, new[] { StatId.Fatigue }, new[] { "恢复疲劳" });
                    break;
                case 1:
                    y = DrawRestoreButtons(x, y, w, new[] { StatId.Hp }, new[] { "恢复生命" });
                    DrawRows(x, y, w, snap, "hp", "bloodVolume", "bodyTemperature", "radiationDose");
                    break;
                case 2:
                    y = DrawRestoreButtons(x, y, w, new[] { StatId.Hunger }, new[] { "恢复饱食" });
                    DrawRows(x, y, w, snap, "hunger");
                    break;
                case 3:
                    y = DrawRestoreButtons(x, y, w, new[] { StatId.Thirst }, new[] { "恢复水分" });
                    DrawRows(x, y, w, snap, "thirst");
                    break;
                case 4:
                    y = DrawRestoreButtons(x, y, w, new[] { StatId.Energy, StatId.Fatigue },
                        new[] { "恢复精力/持久力", "恢复疲劳" });
                    DrawRows(x, y, w, snap, "stamina", "energy", "fatigue");
                    break;
                case 5:
                    DrawRows(x, y, w, snap, "sanity", "casEnergy", "buffCount", "deadFlag", "netDeadFlag", "isPlayer");
                    break;
                case 6:
                    DrawSettingsTab(x, y, w, snap);
                    break;
            }

            DrawWriteFeedback(x, y, w);
        }

        /// <summary>恢复按钮行。点击后由 PlayerWriteGate 决定是否真的写入。</summary>
        private float DrawRestoreButtons(float x, float y, float w, StatId[] stats, string[] labels)
        {
            float gap = 8f;
            float bw = (w - gap * (stats.Length - 1)) / stats.Length;
            float bh = LineH + 6;

            for (int i = 0; i < stats.Length; i++)
            {
                bool avail = RestoreService.IsLikelyAvailable(stats[i], out string reason);
                string label = avail ? labels[i] : labels[i] + "（" + reason + "）";

                // 不可用时也画出来（置灰），点击仍会走门禁并给出精确拒绝原因
                var oldColor = GUI.color;
                if (!avail) GUI.color = new Color(1f, 1f, 1f, 0.45f);

                if (GUI.Button(new Rect(x + i * (bw + gap), y, bw, bh), label, _tab))
                {
                    var r = RestoreService.Restore(stats[i]);
                    Log.Info("[ui] 恢复按钮 " + stats[i] + " -> " + r.Outcome + " " + r.Message);
                }

                GUI.color = oldColor;
            }

            return y + bh + 8;
        }

        /// <summary>上次操作结果反馈（12 秒有效）。</summary>
        private void DrawWriteFeedback(float x, float y, float w)
        {
            float h = LineH * 2;

            if (!string.IsNullOrEmpty(RestoreService.Summary) && RestoreService.SummaryAge < 12f)
            {
                GUI.Label(new Rect(x, y, w, h), RestoreService.Summary, _warn);
                y += LineH;
            }

            var last = RestoreService.Last;
            if (last == null || RestoreService.LastAge > 12f) return;

            string text = (last.IsOk ? "✓ " : "✗ ") + last.Stat + "：" + last.OutcomeZh();
            if (last.IsOk)
                text += $"  {last.Before:0.#} → {last.After:0.#}";
            else if (!string.IsNullOrEmpty(last.Message))
                text += "  " + last.Message;

            GUI.Label(new Rect(x, y, w, h), text, last.IsOk ? _label : _fail);
        }

        private float DrawPlayerTab(float x, float y, float w, PlayerSnapshot snap)
        {
            y = Row(x, y, w, "玩家类型", snap.PlayerTypeName, "HumanCharacterController:BasicCharacterController");
            y = Row(x, y, w, "访问入口", snap.PlayerGuid, "每次读取重新解析，不缓存");
            y = Row(x, y, w, "场景", snap.SceneName, "场景切换后应自动更新");
            y = Row(x, y, w, "isPlayer", snap.IsPlayerFlag ? "true" : "false", null);
            y = Row(x, y, w, "死亡标志", snap.IsDead ? "true" : "false", "读自 deadFlag 属性");
            y = Row(x, y, w, "成功字段", snap.OkCount().ToString() + " 项", null);
            y = Row(x, y, w, "失败字段", snap.FailCount().ToString() + " 项",
                snap.FailCount() > 0 ? "红色项为读取失败，未用假数据填充" : "无失败项");
            y += LineH * 0.4f;
            return DrawWarnings(x, y, w, snap, 4);
        }

        private void DrawSettingsTab(float x, float y, float w, PlayerSnapshot snap)
        {
            y = Row(x, y, w, "模组版本", Plugin.PluginVersion, "Phase 3A = 只读 + 一键恢复");
            y = Row(x, y, w, "写模式", "只读（无任何写值代码路径）", "Phase 3 才实现修改");
            y = Row(x, y, w, "Harmony 补丁", PatchCount + " （要求 0）", "不安装任何修改伤害/行为的补丁");
            y = Row(x, y, w, "读取间隔", ModConfig.RefreshMs + " ms", "配置 [Read] RefreshIntervalMs");
            y = Row(x, y, w, "面板键", Key.ToString(), "配置 [UI] ToggleKey");
            y = Row(x, y, w, "按键冲突", ConflictMsg, "只读调用游戏按键映射表");
            y = Row(x, y, w, "成长探针", GrowthProbe.Status(), "只读；配置 [Diagnostics] GrowthLog");
            y = Row(x, y, w, "疲劳探针", FatigueProbe.Status(), "只读；配置 [Diagnostics] FatigueProbe");
            y += LineH * 0.4f;

            GUI.Label(new Rect(x, y, w, LineH), "模块状态：", _label);
            y += LineH;
            if (Registry != null)
            {
                foreach (var m in Registry.All)
                {
                    if (m == null) continue;
                    GUI.Label(new Rect(x, y, w, LineH),
                        (m.Enabled ? "✓ " : "✗ ") + m.TitleZh + "  (" + m.Id + ")", m.Enabled ? _label : _note);
                    y += LineH - 2;
                }
            }
        }

        private void DrawRows(float x, float y, float w, PlayerSnapshot snap, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                var v = snap.Get(keys[i]);
                if (v != null) y = DrawValueRow(x, y, w, v);
            }
            y += LineH * 0.3f;
            DrawWarnings(x, y, w, snap, 3);
        }

        private float DrawValueRow(float x, float y, float w, ReadValue v)
        {
            bool ok = v.IsUsable;

            GUI.Label(new Rect(x, y, w * 0.40f, LineH), v.LabelZh, _label);

            string txt;
            if (ok) txt = v.HasMax ? $"{v.Value:0.##} / {v.Max:0.##}" : $"{v.Value:0.##}";
            else txt = v.ValueText();
            if (v.State == ReadState.Suspicious) txt += "  (异常值)";

            GUI.Label(new Rect(x + w * 0.40f, y, w * 0.34f, LineH), txt, ok ? _value : _fail);

            if (ok && v.HasMax)
                GUI.Label(new Rect(x + w * 0.75f, y, w * 0.16f, LineH),
                    (v.Percent01() * 100f).ToString("0") + "%", _note);
            else if (!ok && v.State == ReadState.Failed)
                GUI.Label(new Rect(x + w * 0.75f, y, w * 0.25f, LineH), "读取失败", _fail);

            y += LineH;

            if (ok && v.HasMax)
            {
                float bh = Mathf.Max(4f, 6f * ModConfig.Scale);
                GUI.DrawTexture(new Rect(x, y - 2, w, bh), _texBarBg);
                float p = v.Percent01();
                var tex = p > 0.5f ? _texBarOk : (p > 0.2f ? _texBarLow : _texBarCrit);
                GUI.DrawTexture(new Rect(x, y - 2, w * p, bh), tex);
                y += bh + 2;
            }

            if (ModConfig.Notes && !string.IsNullOrEmpty(v.Note))
            {
                GUI.Label(new Rect(x + 4, y, w - 4, LineH), "· " + v.Note, _note);
                y += LineH - 2;
            }
            if (!ok && v.State == ReadState.Failed && !string.IsNullOrEmpty(v.Error))
            {
                GUI.Label(new Rect(x + 4, y, w - 4, LineH), "· " + v.Error, _note);
                y += LineH - 2;
            }

            return y + 2;
        }

        private float Row(float x, float y, float w, string label, string value, string note)
        {
            GUI.Label(new Rect(x, y, w * 0.28f, LineH), label, _label);
            GUI.Label(new Rect(x + w * 0.28f, y, w * 0.72f, LineH), value ?? "", _value);
            y += LineH;
            if (ModConfig.Notes && !string.IsNullOrEmpty(note))
            {
                GUI.Label(new Rect(x + 4, y, w - 4, LineH), "· " + note, _note);
                y += LineH - 2;
            }
            return y;
        }

        private float DrawWarnings(float x, float y, float w, PlayerSnapshot snap, int max)
        {
            if (snap.Warnings.Count == 0) return y;
            GUI.Label(new Rect(x, y, w, LineH), "提示 / 警告（" + snap.Warnings.Count + "）", _warn);
            y += LineH;
            for (int i = 0; i < snap.Warnings.Count && i < max; i++)
            {
                GUI.Label(new Rect(x, y, w, LineH), "· " + snap.Warnings[i], _warn);
                y += LineH;
            }
            if (snap.Warnings.Count > max)
            {
                GUI.Label(new Rect(x, y, w, LineH), "……还有 " + (snap.Warnings.Count - max) + " 条（见日志）", _note);
                y += LineH;
            }
            return y;
        }
    }
}
