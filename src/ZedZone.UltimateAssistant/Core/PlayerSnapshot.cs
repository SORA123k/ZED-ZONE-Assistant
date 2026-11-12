using System;
using System.Collections.Generic;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>单个可读数值的读取状态。绝不伪造数据：读不到就标失败。</summary>
    public enum ReadState
    {
        Ok = 0,
        /// <summary>没有玩家对象（主菜单 / 加载中 / 死亡未重生）。</summary>
        NoPlayer = 1,
        /// <summary>字段访问抛异常（把异常信息带出来）。</summary>
        Failed = 2,
        /// <summary>读到了，但值不在合理范围（不修正，仅标记可疑）。</summary>
        Suspicious = 3,
    }

    /// <summary>
    /// 一次读取得到的单个数值。
    /// Value/Max 都是游戏真实值；Max 无效时 HasMax=false。
    /// </summary>
    public sealed class ReadValue
    {
        public string Key;          // 英文键，如 "energy"
        public string LabelZh;      // 中文标签
        public string Note;         // 说明（如"与 STAMINA 同源"）
        public float Value;
        public float Max;
        public bool HasMax;
        public ReadState State = ReadState.NoPlayer;
        public string Error;

        /// <summary>进度 0..1；无上限或读失败时返回 0。仅用于画条，不参与判定。</summary>
        public float Percent01()
        {
            if (State != ReadState.Ok && State != ReadState.Suspicious) return 0f;
            if (!HasMax || Max <= 0f) return 0f;
            float p = Value / Max;
            if (p < 0f) p = 0f;
            if (p > 1f) p = 1f;
            return p;
        }

        public bool IsUsable => State == ReadState.Ok || State == ReadState.Suspicious;

        public string ValueText()
        {
            switch (State)
            {
                case ReadState.Ok:
                case ReadState.Suspicious:
                    return HasMax
                        ? $"{Value:0.##} / {Max:0.##}"
                        : $"{Value:0.##}";
                case ReadState.NoPlayer:
                    return "—（无玩家对象）";
                case ReadState.Failed:
                    return "读取失败";
                default:
                    return "—";
            }
        }

        public static ReadValue Failed_(string key, string labelZh, string error, string note = null)
            => new ReadValue { Key = key, LabelZh = labelZh, State = ReadState.Failed, Error = error, Note = note };

        public static ReadValue NoPlayer_(string key, string labelZh, string note = null)
            => new ReadValue { Key = key, LabelZh = labelZh, State = ReadState.NoPlayer, Note = note };
    }

    /// <summary>
    /// 一次完整快照。Phase 2 只读：快照中不含任何"写入意图"。
    /// </summary>
    public sealed class PlayerSnapshot
    {
        public DateTime At = DateTime.Now;
        public bool HasPlayer;
        public string PlayerTypeName = "";
        public string PlayerGuid = "";
        public bool IsDead;
        public bool IsPlayerFlag;
        /// <summary>场景名，用于确认"切换场景后引用已重建"。</summary>
        public string SceneName = "";
        public int FrameCount;

        public readonly List<ReadValue> Values = new List<ReadValue>();
        public readonly List<string> Warnings = new List<string>();

        public ReadValue Get(string key)
        {
            for (int i = 0; i < Values.Count; i++)
                if (Values[i].Key == key) return Values[i];
            return null;
        }

        public int OkCount()
        {
            int n = 0;
            for (int i = 0; i < Values.Count; i++)
                if (Values[i].IsUsable) n++;
            return n;
        }

        public int FailCount()
        {
            int n = 0;
            for (int i = 0; i < Values.Count; i++)
                if (Values[i].State == ReadState.Failed) n++;
            return n;
        }
    }
}
