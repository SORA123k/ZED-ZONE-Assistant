using System;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 数据链访问封装：
    ///   playerCharacter → characterData → characterStatusData
    ///
    /// 关键事实（Phase 1 已核实）：
    ///  * `CharacterStatusData` 是唯一真实存储，且只被 `CharacterData` 引用；
    ///  * 控制器上的 hp/energy/fatigue/hunger/thirst 是**属性**；
    ///  * 控制器只有 `maxHP`，**没有** maxEnergy/maxFatigue/maxHunger/maxThirst
    ///    → 这些上限必须从 characterStatusData 取。
    /// </summary>
    internal static class PlayerDataAccess
    {
        /// <summary>安全获取 CharacterStatusData。失败返回 null，绝不上抛。</summary>
        internal static CharacterStatusData GetStatusData(HumanCharacterController p, out string error)
        {
            error = null;
            if (p == null) { error = "player == null"; return null; }

            try
            {
                var cd = p.characterData;
                if (cd == null) { error = "characterData == null"; return null; }

                var sd = cd.characterStatusData;
                if (sd == null) { error = "characterStatusData == null"; return null; }

                return sd;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("PlayerDataAccess.GetStatusData", e);
                error = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }

        internal static int GetBuffCount(HumanCharacterController p, out string error)
        {
            error = null;
            try
            {
                var cd = p.characterData;
                if (cd == null) { error = "characterData == null"; return -1; }
                var list = cd.buffDataList;
                if (list == null) { error = "buffDataList == null"; return -1; }
                return list.Count;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("PlayerDataAccess.GetBuffCount", e);
                error = e.GetType().Name + ": " + e.Message;
                return -1;
            }
        }
    }

    /// <summary>模块注册表：集中管理模块启用状态，便于 GUI 与配置联动。</summary>
    internal sealed class ModuleRegistry
    {
        private readonly System.Collections.Generic.List<StateModuleBase> _modules =
            new System.Collections.Generic.List<StateModuleBase>();

        internal void Add(StateModuleBase m) { if (m != null) _modules.Add(m); }

        internal System.Collections.Generic.IReadOnlyList<StateModuleBase> All => _modules;

        internal StateModuleBase Find(string id)
        {
            for (int i = 0; i < _modules.Count; i++)
                if (_modules[i].Id == id) return _modules[i];
            return null;
        }
    }
}
