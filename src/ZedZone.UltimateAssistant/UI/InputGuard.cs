using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>
    /// 菜单打开时的输入守卫（v3）。
    ///
    /// 解决三件事：
    ///  1. **阻止角色操作**：禁用 `CharacterInput` 组件 + 焦点门控；
    ///  2. **阻止点击穿透**：关闭 `EventSystem` 与所有 `GraphicRaycaster`，
    ///     使 IMGUI 菜单消费掉的点击不会再触发游戏 uGUI；
    ///  3. **可靠显示鼠标**：`Maintain()` 每帧强制 `Cursor.visible = true` / `lockState = None`，
    ///     并清除自定义光标，避免被游戏每帧重置；关闭时逐项还原。
    /// </summary>
    internal static class InputGuard
    {
        private static bool _held;
        private static bool _ciWasEnabled = true;
        private static bool _ciOk;
        private static bool _prevFocusGate;
        private static bool _prevWindowFocused = true;
        private static bool _focusOk;
        private static CursorLockMode _prevLock = CursorLockMode.None;
        private static bool _prevVisible = true;

        private static EventSystem _es;
        private static bool _esWasEnabled;
        private static bool _esOk;
        private static readonly List<GraphicRaycaster> _raycasters = new List<GraphicRaycaster>();
        private static readonly List<bool> _raycasterStates = new List<bool>();

        internal static bool Held => _held;

        internal static void Hold()
        {
            if (_held) return;
            _held = true;

            // 1) 角色输入
            try
            {
                var ci = CharacterInput.instance;
                if (ci != null)
                {
                    _ciWasEnabled = ci.enabled;
                    ci.enabled = false;
                    _ciOk = true;
                }
                else _ciOk = false;
            }
            catch (Exception e) { _ciOk = false; Log.WarnOnce("input.ci", "[input] 禁用 CharacterInput 失败: " + e.GetType().Name); }

            // 焦点门控
            try
            {
                _prevFocusGate = GameInputManager.focusGateEnabled;
                _prevWindowFocused = GameInputManager.windowFocused;
                GameInputManager.focusGateEnabled = true;
                GameInputManager.windowFocused = false;
                _focusOk = true;
            }
            catch { _focusOk = false; }

            // 2) UI 点击穿透：关闭 EventSystem + 所有 GraphicRaycaster
            try
            {
                _es = EventSystem.current;
                if (_es != null)
                {
                    _esWasEnabled = _es.enabled;
                    _es.enabled = false;
                    _esOk = true;
                    Log.Info("[input] EventSystem 已禁用（菜单接管鼠标）");
                }
            }
            catch { _esOk = false; }

            try
            {
                _raycasters.Clear();
                _raycasterStates.Clear();
                var arr = Resources.FindObjectsOfTypeAll<GraphicRaycaster>();
                for (int i = 0; i < arr.Length; i++)
                {
                    var r = arr[i];
                    if (r == null) continue;
                    _raycasters.Add(r);
                    _raycasterStates.Add(r.enabled);
                    r.enabled = false;
                }
                if (_raycasters.Count > 0)
                    Log.Info($"[input] 已禁用 {_raycasters.Count} 个 GraphicRaycaster（防止点击穿透）");
            }
            catch { }

            // 3) 鼠标
            try
            {
                _prevLock = Cursor.lockState;
                _prevVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
            catch { }
        }

        internal static void Release()
        {
            if (!_held) return;
            _held = false;

            try
            {
                if (_ciOk)
                {
                    var ci = CharacterInput.instance;
                    if (ci != null) ci.enabled = _ciWasEnabled;
                }
            }
            catch { }

            try
            {
                if (_focusOk)
                {
                    GameInputManager.focusGateEnabled = _prevFocusGate;
                    GameInputManager.windowFocused = _prevWindowFocused;
                }
            }
            catch { }

            try
            {
                if (_esOk && _es != null) _es.enabled = _esWasEnabled;
            }
            catch { }

            try
            {
                for (int i = 0; i < _raycasters.Count; i++)
                {
                    if (_raycasters[i] != null && i < _raycasterStates.Count)
                        _raycasters[i].enabled = _raycasterStates[i];
                }
                _raycasters.Clear();
                _raycasterStates.Clear();
            }
            catch { }

            try
            {
                Cursor.lockState = _prevLock;
                Cursor.visible = _prevVisible;
            }
            catch { }
        }

        /// <summary>每帧维持：游戏可能每帧重置光标/焦点状态。</summary>
        internal static void Maintain()
        {
            if (!_held) return;
            try
            {
                if (!Cursor.visible) Cursor.visible = true;
                if (Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                }
            }
            catch { }
        }

        /// <summary>每帧维持焦点门控（旧接口保留）。</summary>
        internal static void Tick()
        {
            if (!_held || !_focusOk) return;
            try { GameInputManager.windowFocused = false; } catch { }
        }
    }
}

