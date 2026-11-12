using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 单个肢体的只读快照（纯数据，UI 与日志共用）。
    /// </summary>
    internal sealed class LimbInfo
    {
        internal int Index;
        internal int BodyPartId = int.MinValue;
        internal int TextId = int.MinValue;
        internal string Name = "未知";

        internal float Hp = float.NaN;
        internal float MaxHp = float.NaN;

        internal bool Wounded;
        internal bool Bleeding;
        internal bool Recovered;

        internal bool ReadOk;
        internal string Error;

        /// <summary>0..1；无法计算时返回 NaN（UI 必须显示"未知"，不得填 0）。</summary>
        internal float Percent01()
        {
            if (float.IsNaN(Hp) || float.IsNaN(MaxHp) || MaxHp <= 0f) return float.NaN;
            float p = Hp / MaxHp;
            if (p < 0f) return 0f;
            if (p > 1f) return 1f;
            return p;
        }

        internal bool PercentKnown => !float.IsNaN(Percent01());
        internal string HpText() => float.IsNaN(Hp) ? "?" : Hp.ToString("0.##");
        internal string MaxText() => float.IsNaN(MaxHp) ? "?" : MaxHp.ToString("0.##");
        internal string PercentText()
        {
            float p = Percent01();
            return float.IsNaN(p) ? "?" : (p * 100f).ToString("0") + "%";
        }
    }

    /// <summary>
    /// 肢体数据只读读取器（Phase 3B-1）。
    ///
    /// 数据链（严格按已验证结构）：
    ///   CharacterData.bodypartsData → bodypartDataAry → CharacterBodyPartData
    ///
    /// 只使用数据侧字段：bodyPartId / hp / maxHP / isWounded / isBleeding / isFullyRecovered。
    /// 部位名称：humanBodyPartTextIdAry[i] → TextManager.GetText()（失败时回退到
    /// CharacterBodyPartData.bodyPartTextId，再失败显示"未知(TextId=x)"）。
    ///
    /// 明确禁止（本类不做的）：
    ///   * 使用视觉组件下标作为数据索引；
    ///   * 直接相信 bodypartDataID；
    ///   * 使用 defaultHP 作为最大 HP；
    ///   * 缓存玩家/数据对象；
    ///   * 写入任何游戏字段。
    /// </summary>
    internal static class LimbData
    {
        internal static List<LimbInfo> Items { get; private set; } = new List<LimbInfo>();
        internal static string Error { get; private set; }
        internal static bool HasPlayer { get; private set; }
        internal static string SceneName { get; private set; } = "?";

        internal static void Refresh()
        {
            Items = new List<LimbInfo>();
            Error = null;
            HasPlayer = false;
            try { SceneName = PlayerAccessor.CurrentSceneName(); } catch { SceneName = "?"; }

            HumanCharacterController p = null;
            try { p = PlayerAccessor.Resolve(out _, out _); } catch { }
            if (p == null)
            {
                Error = "未找到玩家对象（主菜单 / 加载中 / 已死亡未重生）";
                return;
            }
            HasPlayer = true;

            CharacterData cd = null;
            try { cd = p.characterData; } catch (Exception e) { Error = "characterData 读取失败: " + e.GetType().Name; }
            if (cd == null) { Error = Error ?? "characterData == null"; return; }

            CharacterBodyPartsData bp = null;
            try { bp = cd.bodypartsData; } catch (Exception e) { Error = "bodypartsData 读取失败: " + e.GetType().Name; }
            if (bp == null) { Error = Error ?? "bodypartsData == null"; return; }

            Il2CppReferenceArray<CharacterBodyPartData> arr = null;
            try { arr = bp.bodypartDataAry; } catch (Exception e) { Error = "bodypartDataAry 读取失败: " + e.GetType().Name; }
            if (arr == null) { Error = Error ?? "bodypartDataAry == null"; return; }

            // 名称表（数据侧静态数组，长度应为部位数）
            Il2CppStructArray<int> textIds = null;
            try { textIds = CharacterBodyPartsData.humanBodyPartTextIdAry; } catch { }

            var list = new List<LimbInfo>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                var info = new LimbInfo { Index = i };
                try
                {
                    var d = arr[i];
                    if (d == null) { info.Error = "该部位数据为 null"; list.Add(info); continue; }

                    try { info.BodyPartId = d.bodyPartId; } catch { }
                    try { info.Hp = d.hp; } catch { }
                    try { info.MaxHp = d.maxHP; } catch { }
                    try { info.Wounded = d.isWounded; } catch { }
                    try { info.Bleeding = d.isBleeding; } catch { }
                    try { info.Recovered = d.isFullyRecovered; } catch { }

                    // 文本 ID：优先数据侧静态数组；不可用时回退到数据对象的 bodyPartTextId
                    int tid = int.MinValue;
                    if (textIds != null && i < textIds.Length) { try { tid = textIds[i]; } catch { } }
                    if (tid <= 0) { try { tid = d.bodyPartTextId; } catch { } }
                    info.TextId = tid;
                    info.Name = ResolveName(tid);

                    info.ReadOk = true;
                }
                catch (Exception e)
                {
                    info.Error = e.GetType().Name;
                }
                list.Add(info);
            }

            Items = list;
            if (list.Count == 0) Error = "部位数组为空";
        }

        private static string ResolveName(int textId)
        {
            if (textId <= 0) return "未知(TextId=" + textId + ")";
            try
            {
                var tm = TextManager.instance;
                if (tm == null) return "未知(TextId=" + textId + ")";
                try { tm.EnsureInitialized(); } catch { }
                string s = tm.GetText(textId);
                if (string.IsNullOrEmpty(s)) return "未知(TextId=" + textId + ")";
                return s.Replace('\n', ' ');
            }
            catch
            {
                return "未知(TextId=" + textId + ")";
            }
        }

        // ---- 概览统计（只读） ----
        internal static int CountWounded()
        {
            int n = 0;
            for (int i = 0; i < Items.Count; i++) if (Items[i].Wounded) n++;
            return n;
        }

        internal static int CountBleeding()
        {
            int n = 0;
            for (int i = 0; i < Items.Count; i++) if (Items[i].Bleeding) n++;
            return n;
        }

        internal static int CountRecovered()
        {
            int n = 0;
            for (int i = 0; i < Items.Count; i++) if (Items[i].Recovered) n++;
            return n;
        }

        internal static int CountUnknown()
        {
            int n = 0;
            for (int i = 0; i < Items.Count; i++) if (!Items[i].PercentKnown) n++;
            return n;
        }
    }
}
