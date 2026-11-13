using System;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>
    /// 菜单打开时的输入守卫（v1.0.3 重做）。
    ///
    /// 前两版失败原因：
    ///  * `Time.timeScale = 0` —— 能挡住操作，但游戏看起来像卡死（不可接受）；
    ///  * `GameInputManager.windowFocused = false` —— 实测**无效**，
    ///    因为游戏的焦点门控 `focusGateEnabled` 默认可能是关闭的，单独改 windowFocused 不起作用。
    ///
    /// 本版手段（三层，逐层生效，关闭时全部还原）：
    ///   1. **禁用 `CharacterInput` 组件** —— 游戏的移动/攻击/使用物品都从该 MonoBehaviour 读取输入，
    ///      禁用它等于切断输入源，而游戏世界继续运行（不冻结时间）；
    ///   2. 同时设置 `focusGateEnabled = true` + `windowFocused = false`，让游戏自身的焦点门控也拦截；
    ///   3. 解锁鼠标（`Cursor.lockState = None` / `visible = true`），供菜单操作。
    ///
    /// 所有原值保存并在释放时还原；任何异常都不会让游戏卡死。
    /// </summary>
    internal static class InputGuard
    {
        private static bool _held;
        private static bool _prevWindowFocused = true;
        private static bool _prevFocusGate;
        private static bool _focusOk;
        private static bool _ciWasEnabled = true;
        private static bool _ciOk;
        private static CursorLockMode _prevLock = CursorLockMode.None;
        private static bool _prevVisible = true;

        internal static bool Held => _held;
        internal static bool BlockOk => _ciOk || _focusOk;

        internal static void Hold()
        {
            if (_held) return;
            _held = true;

            // 1) 切断角色输入源
            try
            {
                var ci = CharacterInput.instance;
                if (ci != null)
                {
                    _ciWasEnabled = ci.enabled;
                    ci.enabled = false;
                    _ciOk = true;
                    Log.Info("[input] CharacterInput 已禁用（角色操作被切断）");
                }
                else _ciOk = false;
            }
            catch (Exception e)
            {
                _ciOk = false;
                Log.WarnOnce("input.ci", "[input] 禁用 CharacterInput 失败: " + e.GetType().Name);
            }

            // 2) 焦点门控
            try
            {
                _prevFocusGate = GameInputManager.focusGateEnabled;
                _prevWindowFocused = GameInputManager.windowFocused;
                GameInputManager.focusGateEnabled = true;
                GameInputManager.windowFocused = false;
                _focusOk = true;
            }
            catch (Exception e)
            {
                _focusOk = false;
                Log.WarnOnce("input.focus", "[input] 焦点门控写入失败: " + e.GetType().Name);
            }

            // 3) 鼠标
            try
            {
                _prevLock = Cursor.lockState;
                _prevVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            catch (Exception e) { Log.WarnOnce("input.cursor", "[input] 解锁鼠标失败: " + e.GetType().Name); }
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
                Cursor.lockState = _prevLock;
                Cursor.visible = _prevVisible;
            }
            catch { }
        }

        /// <summary>每帧维持（游戏可能每帧重置焦点/鼠标状态）。</summary>
        internal static void Tick()
        {
            if (!_held) return;
            if (!_focusOk) return;
            try
            {
                GameInputManager.windowFocused = false;
            }
            catch { }
        }
    }
}
