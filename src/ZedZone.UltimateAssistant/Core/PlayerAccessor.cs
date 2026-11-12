using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 玩家对象访问器。
    ///
    /// 设计铁律（Phase 2 验收项）：
    ///  1. **每次调用都重新解析，绝不缓存 HumanCharacterController**。
    ///     依据：`GameController.playerCharacter` 与 `CharacterInput.characterController`
    ///     在 interop 里都带 setter，说明运行期会被重新赋值（重生 / 换场景 / 读档）。
    ///     缓存旧引用会指向已销毁对象 → 触发 IL2CPP 封送异常。
    ///  2. 所有访问包 try/catch，失败返回 null 并让上层标记「读取失败」。
    ///  3. 不修改任何游戏状态。
    /// </summary>
    internal static class PlayerAccessor
    {
        /// <summary>Unity 对象有效性判定：MonoBehaviour 用 UnityEngine.Object 的 == null 重载最可靠。</summary>
        internal static bool IsAlive(UnityEngine.Object o)
        {
            try { return o != null; } catch { return false; }
        }

        internal static string CurrentSceneName()
        {
            try { return SceneManager.GetActiveScene().name ?? ""; }
            catch { return ""; }
        }

        /// <summary>
        /// 解析当前本地玩家。每次都重新取。
        /// 返回 null 表示：主菜单 / 加载中 / 无玩家 —— 调用方必须按「无玩家」处理，不得伪造数据。
        /// </summary>
        internal static HumanCharacterController Resolve(out string via, out string error)
        {
            via = "";
            error = null;

            // 主路径：GameController.instance.playerCharacter
            try
            {
                var gc = GameController.instance;
                if (gc == null) { error = "GameController.instance == null（主菜单或未进入游戏）"; return null; }

                var p = gc.playerCharacter;
                if (p == null) { error = "GameController.playerCharacter == null（尚未生成/已销毁）"; return null; }

                if (!IsAlive((UnityEngine.Object)p)) { error = "playerCharacter 已销毁"; return null; }

                via = "GameController.instance.playerCharacter";
                return p;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("PlayerAccessor.GameController", e);
                error = "GameController 路径异常: " + e.GetType().Name;
            }

            // 备路径：CharacterInput.instance.characterController
            try
            {
                var ci = CharacterInput.instance;
                if (ci != null)
                {
                    var p2 = ci.characterController;
                    if (p2 != null && IsAlive((UnityEngine.Object)p2))
                    {
                        via = "CharacterInput.instance.characterController";
                        error = null;
                        return p2;
                    }
                }
            }
            catch (Exception e)
            {
                Log.ErrorOnce("PlayerAccessor.CharacterInput", e);
            }

            return null;
        }

        /// <summary>取标识（仅用于日志/GUI 显示玩家切换，不参与业务）。</summary>
        internal static string Describe(HumanCharacterController p)
        {
            if (p == null) return "null";
            try
            {
                string t = "HumanCharacterController";
                try { if (p.isPlayer) t += " (isPlayer=true)"; } catch { }
                t += " ptr=0x" + p.Pointer.ToString("X");
                return t;
            }
            catch { return "HumanCharacterController"; }
        }
    }
}
