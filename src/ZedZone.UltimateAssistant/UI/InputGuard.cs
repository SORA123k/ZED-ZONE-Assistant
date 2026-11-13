using System;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>
    /// 菜单打开时的输入守卫。
    ///
    /// **重要变更（v1.0.1）**：不再使用 `Time.timeScale = 0`。
    /// 实测冻结时间会让游戏表现成"卡住/假死"，体验很差，也容易与游戏自身的暂停逻辑冲突。
    ///
    /// 现方案（不使用时间冻结）：
    ///   1. `GameInputManager.windowFocused = false` —— 游戏自身的"窗口失去焦点"标志（可写静态字段），
    ///      其焦点门控会停止处理角色输入（移动/射击/使用物品），但游戏世界继续正常运行；
    ///   2. `Cursor.lockState = None; Cursor.visible = true` —— 解放鼠标给菜单用。
    ///
    /// 所有原值都会保存并在释放时逐项还原；任何异常都不会让游戏卡死。
    /// 若 `windowFocused` 写入失败，则退化为"不阻断但记录警告"，绝不再冻结游戏。
    /// </summary>
    internal static class InputGuard
    {
        private static bool _held;
        private static bool _prevWindowFocused = true;
        private static bool _focusedOk;
        private static CursorLockMode _prevLock = CursorLockMode.None;
        private static bool _prevVisible = true;

        internal static bool Held => _held;
        internal static bool BlockOk => _focusedOk;

        internal static void Hold()
        {
            if (_held) return;
            _held = true;

            try
            {
                _prevWindowFocused = GameInputManager.windowFocused;
                GameInputManager.windowFocused = false;   // 让游戏的焦点门控停止处理输入
                _focusedOk = true;
            }
            catch (Exception e)
            {
                _focusedOk = false;
                Log.WarnOnce("input.focus", "[input] 焦点门控写入失败，菜单打开时将不阻断角色操作: " + e.GetType().Name);
            }

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
                if (_focusedOk) GameInputManager.windowFocused = _prevWindowFocused;
            }
            catch { }

            try
            {
                Cursor.lockState = _prevLock;
                Cursor.visible = _prevVisible;
            }
            catch { }
        }
    }
}
