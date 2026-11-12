using System;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>
    /// 面板宿主（MonoBehaviour）。
    ///
    /// 刻意保持"薄"：只保留 Unity 生命周期方法（Awake / Start / Update / OnGUI），
    /// 所有绘制与样式逻辑都在 <see cref="PanelRenderer"/>（普通类）里。
    /// 这样 Il2CppInterop 不会为本类生成带自定义参数的方法包装，避免出现
    /// "has unsupported parameter ... PlayerSnapshot" 之类的注入警告。
    /// </summary>
    internal sealed class AssistantPanel : MonoBehaviour
    {
        internal PlayerStateReader Reader;
        internal ModuleRegistry Registry;

        private readonly PanelRenderer _renderer = new PanelRenderer();
        private PlayerSnapshot _snap;
        private bool _visible;
        private float _nextReadAt;
        private bool _gameInputOk = true;
        private bool _conflictChecked;
        private KeyCode _key = KeyCode.Insert;

        private void Awake()
        {
            try { DontDestroyOnLoad(gameObject); } catch { }
        }

        private void Start()
        {
            try { _key = ModConfig.ToggleKey.Value.MainKey; } catch { _key = KeyCode.Insert; }
            try { _visible = ModConfig.ShowOnStart?.Value ?? false; } catch { _visible = false; }
            _renderer.Key = _key;
            _renderer.Registry = Registry;
            _renderer.PatchCount = Patch.PatchManager.InstalledCount;
        }

        private void Update()
        {
            try
            {
                if (!_conflictChecked) CheckKeyConflict();

                if (KeyDown(_key)) _visible = !_visible;
                if (_visible && KeyDown(KeyCode.Escape)) _visible = false;
                if (_visible)
                {
                    for (int i = 0; i < 7; i++)
                        if (KeyDown(KeyCode.Alpha1 + i)) { _renderer.Tab = i; break; }
                }

                // 节流读取（只读，无需每帧封送）
                if (Time.unscaledTime * 1000f >= _nextReadAt)
                {
                    _nextReadAt = Time.unscaledTime * 1000f + ModConfig.RefreshMs;
                    if (Reader != null)
                    {
                        // 每次读取前同步模块开关（配置可能在运行期被改）
                        try { ModConfig.ApplyModuleSwitches(Registry); } catch { }
                        _snap = Reader.Read();
                    }
                    try { Diagnostics.Tick(); } catch (Exception de) { Log.ErrorOnce("diagnostics", de); }
                }
            }
            catch (Exception e)
            {
                Log.ErrorOnce("AssistantPanel.Update", e);
                enabled = false;
            }
        }

        /// <summary>
        /// 按键检测：同时检查游戏输入抽象与 Unity 传统 Input。
        ///
        /// 为什么要 OR 两个源：`GameInputManager.GetKeyDown` 带焦点门控
        /// （focusGateEnabled / windowFocused / inputAllowed），窗口失焦时会返回 false，
        /// 但它能正确处理游戏自己的按键映射；而 `UnityEngine.Input` 不受该门控，
        /// 用于兜底。两者取 OR —— 只要任一路径看到按下就切换，避免重复切换。
        /// </summary>
        private bool KeyDown(KeyCode k)
        {
            bool a = false, b = false;

            if (_gameInputOk)
            {
                try { a = GameInputManager.GetKeyDown(k); }
                catch (Exception e)
                {
                    _gameInputOk = false;
                    Log.WarnOnce("input.fallback", "GameInputManager.GetKeyDown 不可用: " + e.GetType().Name);
                }
            }

            try { b = UnityEngine.Input.GetKeyDown(k); } catch { }

            return a || b;
        }

        /// <summary>快捷键冲突检测：只读调用游戏自身按键映射表。</summary>
        private void CheckKeyConflict()
        {
            _conflictChecked = true;
            try
            {
                var names = GameInputManager.GetButtonNamesByKeycode(_key);
                int n = names == null ? 0 : names.Count;
                if (n > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < n && i < 6; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(names[i]);
                    }
                    _renderer.ConflictMsg = "冲突！已被游戏绑定到 " + sb;
                    Log.Warn("[keybind] " + _key + " 与游戏按键冲突: " + sb +
                             " —— 可在 BepInEx\\config\\zedzone.ultimateassistant.cfg 修改 [UI] ToggleKey");
                }
                else
                {
                    _renderer.ConflictMsg = "无冲突";
                    Log.Info("[keybind] " + _key + " 未被游戏占用，无冲突");
                }
            }
            catch (Exception e)
            {
                _renderer.ConflictMsg = "无法检测";
                Log.WarnOnce("keybind.check", "快捷键冲突检测不可用（不影响使用）: " + e.GetType().Name);
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;
            try
            {
                _renderer.Draw(_snap);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("AssistantPanel.OnGUI", e);
                _visible = false;
            }
        }
    }
}
