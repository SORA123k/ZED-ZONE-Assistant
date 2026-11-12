using System;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.Modules
{
    /// <summary>
    /// 生命值读取模块（Phase 2 只读）。
    ///
    /// 数据源：
    ///   hp      → `HumanCharacterController.hp`（override 属性，与游戏 UI 同源）
    ///   maxHP   → `BasicCharacterController.maxHP`
    ///   失血/体温/辐射 → characterStatusData（仅展示，Phase 2 不写）
    /// </summary>
    internal sealed class HealthModule : StateModuleBase
    {
        internal override string Id => "health";
        internal override string TitleZh => "生命 Health";

        internal override void Read(HumanCharacterController player, PlayerSnapshot snap)
        {
            if (!Enabled || player == null) return;

            // hp 走属性（HumanCharacterController 覆写了它，与游戏 UI 一致）
            Add(snap, ReadValueFor(snap, "hp", "生命值",
                () => player.hp,
                () => player.maxHP,
                "来源：HumanCharacterController.hp（覆写属性）"));

            var sd = PlayerDataAccess.GetStatusData(player, out string err);
            if (sd == null)
            {
                snap.Warnings.Add("生命附属字段不可用：" + err);
                return;
            }

            Add(snap, ReadValueFor(snap, "bloodVolume", "血量（失血系统）",
                () => sd.bloodVolume,
                () => CharacterStatusData.maxBloodVolume,
                "来源：characterStatusData.bloodVolume"));

            Add(snap, ReadValueFor(snap, "bodyTemperature", "体温",
                () => sd.bodyTemperature,
                null,
                "来源：characterStatusData.bodyTemperature"));

            Add(snap, ReadValueFor(snap, "radiationDose", "辐射剂量",
                () => sd.radiationDose,
                null,
                "来源：characterStatusData.radiationDose"));
        }
    }

    /// <summary>饥饿读取模块（Phase 2 只读）。max 来自 statusData —— 控制器没有 maxHunger。</summary>
    internal sealed class HungerModule : StateModuleBase
    {
        internal override string Id => "hunger";
        internal override string TitleZh => "饱食 Hunger";

        internal override void Read(HumanCharacterController player, PlayerSnapshot snap)
        {
            if (!Enabled || player == null) return;

            var sd = PlayerDataAccess.GetStatusData(player, out string err);
            if (sd == null)
            {
                Add(snap, ReadValue.Failed_("hunger", "饱食度", err, "characterStatusData 不可用"));
                return;
            }

            Add(snap, ReadValueFor(snap, "hunger", "饱食度",
                () => player.hunger,
                () => sd.maxHunger,
                "值：控制器属性；上限：statusData.maxHunger"));
        }
    }

    /// <summary>饥渴读取模块（Phase 2 只读）。</summary>
    internal sealed class ThirstModule : StateModuleBase
    {
        internal override string Id => "thirst";
        internal override string TitleZh => "水分 Thirst";

        internal override void Read(HumanCharacterController player, PlayerSnapshot snap)
        {
            if (!Enabled || player == null) return;

            var sd = PlayerDataAccess.GetStatusData(player, out string err);
            if (sd == null)
            {
                Add(snap, ReadValue.Failed_("thirst", "水分", err, "characterStatusData 不可用"));
                return;
            }

            Add(snap, ReadValueFor(snap, "thirst", "水分",
                () => player.thirst,
                () => sd.maxThirst,
                "值：控制器属性；上限：statusData.maxThirst"));
        }
    }
}
