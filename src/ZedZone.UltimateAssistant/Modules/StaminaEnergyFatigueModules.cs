using System;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Modules
{
    /// <summary>
    /// 持久力 STAMINA 读取模块（Phase 2 只读）。
    ///
    /// ★ 重要事实（Phase 1 已核实，本地化 id=5431）：
    ///   原文 `Adjusts the natural recovery rate of character stamina (energy).`
    ///   即**游戏把 STAMINA 与 ENERGY 视为同一个数值 `energy`**。
    ///   游戏内不存在独立的 `stamina` 字段（全字段扫描仅有 staminaRegenScale 等倍率）。
    ///
    /// 因此本模块与 <see cref="EnergyModule"/> **读取同一底层属性**。
    /// 两者保留为独立模块是为了满足"分开管理"的要求，并在 GUI 上明确标注同源，
    /// **绝不伪造出第二个数值**。
    /// </summary>
    internal sealed class StaminaModule : StateModuleBase
    {
        internal override string Id => "stamina";
        internal override string TitleZh => "持久力 STAMINA";

        internal override void Read(HumanCharacterController player, PlayerSnapshot snap)
        {
            if (!Enabled || player == null) return;

            var sd = PlayerDataAccess.GetStatusData(player, out string err);
            if (sd == null)
            {
                Add(snap, ReadValue.Failed_("stamina", "持久力", err, "characterStatusData 不可用"));
                return;
            }

            Add(snap, ReadValueFor(snap, "stamina", "持久力",
                () => player.energy,
                () => sd.maxEnergy,
                "★与「精力 ENERGY」为同一数值（本地化 5431）"));
        }
    }

    /// <summary>
    /// 精力 ENERGY 读取模块（Phase 2 只读）。
    /// 与 <see cref="StaminaModule"/> 同源：`energy` / `statusData.maxEnergy`。
    /// </summary>
    internal sealed class EnergyModule : StateModuleBase
    {
        internal override string Id => "energy";
        internal override string TitleZh => "精力 ENERGY";

        internal override void Read(HumanCharacterController player, PlayerSnapshot snap)
        {
            if (!Enabled || player == null) return;

            var sd = PlayerDataAccess.GetStatusData(player, out string err);
            if (sd == null)
            {
                Add(snap, ReadValue.Failed_("energy", "精力", err, "characterStatusData 不可用"));
                return;
            }

            Add(snap, ReadValueFor(snap, "energy", "精力",
                () => player.energy,
                () => sd.maxEnergy,
                "★与「持久力 STAMINA」为同一数值（本地化 5431）"));
        }
    }

    /// <summary>
    /// 疲劳 FATIGUE 读取模块（Phase 2 只读）。
    /// 独立于 energy —— 这是第三个**真正独立**的属性。
    /// </summary>
    internal sealed class FatigueModule : StateModuleBase
    {
        internal override string Id => "fatigue";
        internal override string TitleZh => "疲劳 FATIGUE";

        internal override void Read(HumanCharacterController player, PlayerSnapshot snap)
        {
            if (!Enabled || player == null) return;

            var sd = PlayerDataAccess.GetStatusData(player, out string err);
            if (sd == null)
            {
                Add(snap, ReadValue.Failed_("fatigue", "疲劳", err, "characterStatusData 不可用"));
                return;
            }

            Add(snap, ReadValueFor(snap, "fatigue", "疲劳",
                () => player.fatigue,
                () => sd.maxFatigue,
                "值：控制器属性；上限：statusData.maxFatigue（独立属性）"));
        }
    }

    /// <summary>
    /// 状态模块（Phase 2 只读）：精神值、CAS 能量、死亡标志、Buff 数量。
    /// Phase 3 的"清除负面状态"会落在 Buff 相关 API 上，本阶段只统计数量。
    /// </summary>
    internal sealed class StatusModule : StateModuleBase
    {
        internal override string Id => "status";
        internal override string TitleZh => "状态 Status";

        internal override void Read(HumanCharacterController player, PlayerSnapshot snap)
        {
            if (!Enabled || player == null) return;

            var sd = PlayerDataAccess.GetStatusData(player, out string err);
            if (sd != null)
            {
                Add(snap, ReadValueFor(snap, "sanity", "精神值",
                    () => sd.sanity,
                    () => sd.maxSanity,
                    "来源：characterStatusData.sanity"));

                Add(snap, ReadValueFor(snap, "casEnergy", "CAS 能量",
                    () => player.casEnergy,
                    null,
                    "来源：控制器属性 casEnergy（原生面板另有 AP 条，Phase 2 未映射）"));
            }
            else
            {
                snap.Warnings.Add("状态模块：characterStatusData 不可用（" + err + "）");
            }

            // 死亡标志：deadFlag 是属性；netDeadFlag 是字段（联机影子）
            Add(snap, ReadBoolAsValue(snap, "deadFlag", "死亡标志 deadFlag",
                () => player.deadFlag, "属性")); 
            Add(snap, ReadBoolAsValue(snap, "netDeadFlag", "联机死亡标志 netDeadFlag",
                () => player.netDeadFlag, "字段（联机影子）"));
            Add(snap, ReadBoolAsValue(snap, "isPlayer", "isPlayer",
                () => player.isPlayer, "玩家身份标志"));

            // Buff 数量：只统计，不增删
            int buffCount = PlayerDataAccess.GetBuffCount(player, out string buffErr);
            if (buffCount >= 0)
            {
                snap.Values.Add(new ReadValue
                {
                    Key = "buffCount",
                    LabelZh = "当前 Buff 数量",
                    Note = "只读统计；Phase 3 才涉及清除",
                    Value = buffCount,
                    HasMax = false,
                    State = ReadState.Ok
                });
            }
            else
            {
                Add(snap, ReadValue.Failed_("buffCount", "当前 Buff 数量", buffErr, "只读统计"));
            }
        }
    }
}
