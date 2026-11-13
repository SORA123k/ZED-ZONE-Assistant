using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;
using ZedZone.UltimateAssistant.Modules;
using ZedZone.UltimateAssistant.Patch;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>
    /// 面板宿主（刻意保持"薄"）：只做生命周期、热键、节流读取与绘制调度。
    /// 具体绘制在 ClickGui / ZombieEsp 内完成，避免把 IL2CPP 类型放进 MonoBehaviour 签名。
    /// </summary>
    internal class AssistantPanel : MonoBehaviour
    {
        public AssistantPanel(IntPtr ptr) : base(ptr) { }

        internal ModuleRegistry Registry;
        internal PlayerStateReader Reader;

        private PlayerSnapshot _snap;
        private float _nextReadAt;
        private bool _gameInputOk = true;
        private bool _conflictChecked;
        private bool _firstSync = true;
        private KeyCode _key = KeyCode.Insert;
        private float _lastToggleAt = -10f;

        private void Awake()
        {
            try { DontDestroyOnLoad(gameObject); } catch { }
        }

        private void Start()
        {
            try { _key = ModConfig.ToggleKey.Value.MainKey; } catch { _key = KeyCode.Insert; }
            try { ClickGui.Open = ModConfig.ShowOnStart?.Value ?? false; } catch { ClickGui.Open = false; }
            if (ClickGui.Open) InputGuard.Hold();
            try { Features.SyncFromConfig(); } catch (Exception e) { Log.ErrorOnce("features.start", e); }
            Log.Info("[ui] ClickGUI 已就绪（INSERT 开关，当前" + (ClickGui.Open ? "打开" : "关闭") + "）");
        }

        private void Update()
        {
            try
            {
                if (!_conflictChecked) CheckKeyConflict();
                if (_firstSync) { _firstSync = false; try { Features.SyncFromConfig(); } catch { } }

                // 热键
                if (KeyDown(_key)) ToggleMenu();
                if (ClickGui.Open && KeyDown(KeyCode.Escape)) ToggleMenu();
                if (ClickGui.Open) { try { InputGuard.Tick(); } catch { } }

                // 节流读取
                if (Time.unscaledTime * 1000f >= _nextReadAt)
                {
                    _nextReadAt = Time.unscaledTime * 1000f + ModConfig.RefreshMs;
                    if (Reader != null)
                    {
                        try { ModConfig.ApplyModuleSwitches(Registry); } catch { }
                        try { _snap = Reader.Read(); } catch (Exception e) { Log.ErrorOnce("panel.read", e); }
                    }
                    try { LimbData.Refresh(); } catch (Exception e) { Log.ErrorOnce("limbdata", e); }
                    try { ZombieEsp.Refresh(); } catch (Exception e) { Log.ErrorOnce("esp", e); }
                    try { WeaponUtil.Refresh(); } catch (Exception e) { Log.ErrorOnce("weaponutil", e); }
                    try { Diagnostics.Tick(); } catch (Exception e) { Log.ErrorOnce("diagnostics", e); }
                    try { GrowthProbe.Tick(); } catch (Exception e) { Log.ErrorOnce("growth", e); }
                    try { FatigueProbe.Tick(); } catch (Exception e) { Log.ErrorOnce("fatigue", e); }
                    try { LimbProbe.Tick(); } catch (Exception e) { Log.ErrorOnce("limb", e); }
                    try { LimbApiProbe.Tick(); } catch (Exception e) { Log.ErrorOnce("limbapi", e); }
                    try { DurabilityDiag.Tick(); } catch (Exception e) { Log.ErrorOnce("duragdiag", e); }
                    try { DurabilityLock.Tick(); } catch (Exception e) { Log.ErrorOnce("durlock", e); }
                    try { DurabilityLock.TickGuns(); } catch (Exception e) { Log.ErrorOnce("durgun", e); }
                    try { Il2CppMetaDiag.Tick(); } catch (Exception e) { Log.ErrorOnce("metadiag", e); }
                }
            }
            catch (Exception e)
            {
                Log.ErrorOnce("AssistantPanel.Update", e);
            }
        }

        private void ToggleMenu()
        {
            // 去抖：两套输入源（GameInputManager / Unity Input）可能在不同帧各报一次，
            // 250ms 内只接受一次切换，避免"一次按键开关两下"。
            if (Time.unscaledTime - _lastToggleAt < 0.25f) return;
            _lastToggleAt = Time.unscaledTime;

            bool open = !ClickGui.Open;
            ClickGui.Open = open;

            bool block = true;
            try { block = ModConfig.BlockGameInput?.Value ?? true; } catch { }

            if (open) { if (block) InputGuard.Hold(); }
            else { InputGuard.Release(); }

            Log.Info("[ui] 菜单 " + (open ? "打开（输入已阻断）" : "关闭（输入已恢复）"));
        }

        private void OnGUI()
        {
            try
            {
                // 世界叠层：无论菜单是否打开都绘制（只要功能开启）
                ZombieEsp.Draw();

                if (ClickGui.Open) ClickGui.Draw(_snap);
                else if (InputGuard.Held) InputGuard.Release();
            }
            catch (Exception e)
            {
                Log.ErrorOnce("AssistantPanel.OnGUI", e);
                try { ClickGui.Open = false; InputGuard.Release(); } catch { }
            }
        }

        private void OnDestroy()
        {
            try { InputGuard.Release(); } catch { }
            try { DurabilityDiag.Shutdown(); } catch { }
            try { Il2CppMetaDiag.Shutdown(); } catch { }
            try { Features.Shutdown(); } catch { }
        }

        private void OnApplicationQuit()
        {
            try { InputGuard.Release(); } catch { }
            try { DurabilityDiag.Shutdown(); } catch { }
            try { Il2CppMetaDiag.Shutdown(); } catch { }
            try { Features.Shutdown(); } catch { }
        }

        /// <summary>同时检查游戏输入抽象与 Unity 传统 Input，任一命中即算按下。</summary>
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

        private void CheckKeyConflict()
        {
            _conflictChecked = true;
            try
            {
                var names = GameInputManager.GetButtonNamesByKeycode(_key);
                if (names == null || names.Count == 0)
                {
                    Log.Info($"[keybind] {_key} 未被游戏占用，无冲突");
                    return;
                }
                var sb = string.Empty;
                for (int i = 0; i < names.Count && i < 3; i++)
                    sb += (i > 0 ? ", " : "") + names[i];
                Log.Warn($"[keybind] {_key} 已被游戏绑定到: {sb}（可能冲突）");
            }
            catch (Exception e)
            {
                Log.WarnOnce("keybind.check", "快捷键冲突检测不可用（不影响使用）: " + e.GetType().Name);
            }
        }
    }
}






