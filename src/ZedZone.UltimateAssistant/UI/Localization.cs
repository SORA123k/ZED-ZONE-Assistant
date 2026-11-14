using System;
using System.Collections.Generic;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    internal enum Lang { ZhCN = 0, En = 1, ZhTW = 2 }

    /// <summary>
    /// 统一本地化管理器（简中 / English / 繁中）。
    /// 所有 UI 文案通过 Key 取，禁止在页面里硬编码多语言。
    /// </summary>
    internal static class L
    {
        private static readonly Dictionary<string, string[]> _map = new Dictionary<string, string[]>
        {
            // ---- 通用 ----
            ["title"]        = new[]{ "SORA助手", "SORA Assistant", "SORA助手" },
            ["hint_close"]   = new[]{ "INSERT 关闭", "INSERT to close", "INSERT 關閉" },
            ["combat"]       = new[]{ "战斗", "Combat", "戰鬥" },
            ["player"]       = new[]{ "玩家", "Player", "玩家" },
            ["visuals"]      = new[]{ "视觉", "Visuals", "視覺" },
            ["settings"]     = new[]{ "设置", "Settings", "設定" },

            // ---- 战斗 ----
            ["sec_combat"]   = new[]{ "战斗辅助", "COMBAT", "戰鬥輔助" },
            ["infinite_ammo"]= new[]{ "无限弹药", "Infinite Ammo", "無限彈藥" },
            ["infinite_ammo_d"]= new[]{ "射击不消耗备用弹药（仅本地玩家）", "Shots do not consume reserve ammo (local player only)", "射擊不消耗備用彈藥（僅本地玩家）" },
            ["no_reload"]    = new[]{ "无需换弹", "No Reload", "無需換彈" },
            ["no_reload_d"]  = new[]{ "弹匣为 0 也能射击；按 R 仍可正常换弹", "Fire even with an empty magazine; R still reloads normally", "彈匣為 0 也能射擊；按 R 仍可正常換彈" },
            ["no_jam"]       = new[]{ "不会卡壳", "No Jam", "不會卡殼" },
            ["no_jam_d"]     = new[]{ "阻止枪械卡壳，并清除已有卡壳标记", "Prevents weapon jams and clears existing jam flags", "阻止槍械卡殼，並清除已有卡殼標記" },
            ["item_dur"]     = new[]{ "工具/近战耐久", "Tool / Melee Durability", "工具/近戰耐久" },
            ["item_dur_d"]   = new[]{ "工具/近战/装备耐久不消耗（只增不减）", "Tools, melee and gear never lose durability", "工具/近戰/裝備耐久不消耗（只增不減）" },
            ["gun_dur"]      = new[]{ "枪械耐久", "Gun Durability", "槍械耐久" },
            ["gun_dur_d"]    = new[]{ "枪械耐久保持上限（写入 properties[0]）", "Gun durability kept at max (writes properties[0])", "槍械耐久保持上限（寫入 properties[0]）" },
            ["no_overheat"]  = new[]{ "取消过热", "No Overheat", "取消過熱" },
            ["no_overheat_d"]= new[]{ "取消自己枪械的过热（不影响 NPC）", "Disables your weapon heat (does not affect NPCs)", "取消自己槍械的過熱（不影響 NPC）" },
            ["fix_jam"]      = new[]{ "一键修复卡壳", "Clear Jam Now", "一鍵修復卡殼" },
            ["fix_jam_d"]    = new[]{ "清除所有己方武器的卡壳状态", "Clears jam state on all of your weapons", "清除所有己方武器的卡殼狀態" },

            // ---- 玩家 ----
            ["sec_guard"]    = new[]{ "防护开关（持续生效）", "PROTECTION (continuous)", "防護開關（持續生效）" },
            ["no_hp"]        = new[]{ "不会扣血", "No HP Loss", "不會扣血" },
            ["no_hp_d"]      = new[]{ "受到伤害时生命值不减少", "Taking damage does not reduce HP", "受到傷害時生命值不減少" },
            ["no_sta"]       = new[]{ "不会扣体力", "No Stamina Loss", "不會扣體力" },
            ["no_sta_d"]     = new[]{ "奔跑/跳跃等不消耗体力", "Sprinting and jumping cost no stamina", "奔跑/跳躍等不消耗體力" },
            ["no_hunger"]    = new[]{ "不会扣饱食度", "No Hunger Loss", "不會扣飽食度" },
            ["no_hunger_d"]  = new[]{ "饱食度不减少（进食仍可增加）", "Hunger does not drop (eating still works)", "飽食度不減少（進食仍可增加）" },
            ["no_thirst"]    = new[]{ "不会扣水分", "No Thirst Loss", "不會扣水分" },
            ["no_thirst_d"]  = new[]{ "水分不减少（饮水仍可增加）", "Thirst does not drop (drinking still works)", "水分不減少（飲水仍可增加）" },
            ["sec_restore"]  = new[]{ "一键恢复（点击执行一次）", "RESTORE (one click, one time)", "一鍵恢復（點擊執行一次）" },
            ["health"]       = new[]{ "生命值", "Health", "生命值" },
            ["energy"]       = new[]{ "精力/体力", "Energy / Stamina", "精力/體力" },
            ["hunger"]       = new[]{ "饱食度", "Hunger", "飽食度" },
            ["thirst"]       = new[]{ "水分", "Thirst", "水分" },
            ["restore"]      = new[]{ "恢复", "Restore", "恢復" },
            ["sec_move"]     = new[]{ "移动与时间", "MOVEMENT & TIME", "移動與時間" },
            ["always_sprint"]= new[]{ "强制奔跑", "Always Sprint", "強制奔跑" },
            ["always_sprint_d"]=new[]{ "开枪/攻击时保持奔跑，不被强制切换为走路", "Keeps sprinting while shooting or attacking", "開槍/攻擊時保持奔跑，不被強制切換為走路" },
            ["move_speed"]   = new[]{ "移动加速", "Movement Speed", "移動加速" },
            ["move_speed_d"] = new[]{ "按倍率提高本地玩家移动速度", "Multiplies your movement speed", "按倍率提高本地玩家移動速度" },
            ["auto_supply"]  = new[]{ "自动补充材料", "Auto Supply", "自動補充材料" },
            ["auto_supply_d"]= new[]{ "制造/修复时视为材料充足，不再因缺材料失败", "Crafting/repair treat materials as sufficient", "製造/修復時視為材料充足，不再因缺材料失敗" },
            ["performance"]  = new[]{ "性能模式", "Performance Mode", "效能模式" },
            ["performance_d"]= new[]{ "降低 ESP/耐久/诊断刷新频率以提升帧数（功能不变）", "Lower ESP/durability/diagnostic refresh rates for higher FPS (features unchanged)", "降低 ESP/耐久/診斷刷新頻率以提升幀數（功能不變）" },
            ["fullbright"]   = new[]{ "地图全亮", "Full Bright", "地圖全亮" },
            ["fullbright_d"] = new[]{ "夜间提升场景亮度（不改变时间/天气/AI）", "Brightens the scene at night (no time/weather/AI change)", "夜間提升場景亮度（不改變時間/天氣/AI）" },
            ["multiplier"]   = new[]{ "倍率", "Multiplier", "倍率" },
            ["reset"]        = new[]{ "恢复默认", "Reset", "恢復預設" },
            ["limbs"]        = new[]{ "肢体状态（只读）", "LIMB STATUS (read-only)", "肢體狀態（唯讀）" },
            ["no_limb"]      = new[]{ "无肢体数据（进入存档后显示）", "No limb data (available in-game)", "無肢體資料（進入存檔後顯示）" },
            ["wounded"]      = new[]{ "受伤", "Wounded", "受傷" },
            ["bleeding"]     = new[]{ "流血", "Bleeding", "流血" },
            ["normal"]       = new[]{ "正常", "Normal", "正常" },
            ["intact"]       = new[]{ "完好", "Intact", "完好" },

            // ---- 视觉 ----
            ["sec_visuals"]  = new[]{ "视觉", "VISUALS", "視覺" },
            ["esp"]          = new[]{ "僵尸透视", "Zombie ESP", "殭屍透視" },
            ["esp_d"]        = new[]{ "标记已加载的僵尸（含视野外）", "Marks loaded zombies (including off-screen)", "標記已載入的殭屍（含視野外）" },
            ["esp_name"]     = new[]{ "显示名称", "Show name", "顯示名稱" },
            ["esp_dist"]     = new[]{ "显示距离", "Show distance", "顯示距離" },
            ["esp_box"]      = new[]{ "绘制方框", "Draw box", "繪製方框" },
            ["esp_max"]      = new[]{ "最大距离", "Max distance", "最大距離" },

            // ---- 设置 ----
            ["sec_general"]  = new[]{ "常规", "GENERAL", "一般" },
            ["menu_scale"]   = new[]{ "界面缩放", "Menu scale", "介面縮放" },
            ["animations"]   = new[]{ "动画效果", "Animations", "動畫效果" },
            ["block_input"]  = new[]{ "打开菜单时阻止游戏操作", "Block game input while menu is open", "開啟選單時阻止遊戲操作" },
            ["show_limbs"]   = new[]{ "显示肢体状态", "Show limb status", "顯示肢體狀態" },
            ["sec_appearance"]= new[]{ "外观", "APPEARANCE", "外觀" },
            ["theme"]        = new[]{ "菜单主题", "UI Theme", "選單主題" },
            ["font_size"]    = new[]{ "字体大小", "Font Size", "字體大小" },
            ["sec_language"] = new[]{ "语言", "LANGUAGE", "語言" },
            ["language"]     = new[]{ "语言 / Language", "Language", "語言" },
            ["sec_controls"] = new[]{ "控制", "CONTROLS", "控制" },
            ["hotkey"]       = new[]{ "菜单快捷键", "Menu hotkey", "選單快捷鍵" },
            ["hotkey_hint"]  = new[]{ "在配置文件 [UI] ToggleKey 修改", "Change [UI] ToggleKey in the config file", "在設定檔 [UI] ToggleKey 修改" },
            ["menu_keys"]    = new[]{ "菜单按键", "Menu keys", "選單按鍵" },
            ["bind"]         = new[]{ "绑定", "Bind", "綁定" },
            ["clear"]        = new[]{ "清除", "Clear", "清除" },
            ["press_key"]    = new[]{ "按键…", "Press key…", "按鍵…" },
            ["not_bound"]    = new[]{ "未绑定", "Not bound", "未綁定" },
            ["footer_hint"]  = new[]{ "全部功能仅作用于本地玩家；联机自动禁用", "All features affect the local player only; disabled online", "全部功能僅作用於本地玩家；連線時自動停用" },

            // ---- 状态 ----
            ["st_off"]       = new[]{ "关", "off", "關" },
            ["st_on"]        = new[]{ "开", "on", "開" },
            ["example"]      = new[]{ "示例数据", "Sample data", "範例資料" },
            ["no_player"]    = new[]{ "未找到玩家（主菜单/加载中/已死亡）", "Player not found (menu / loading / dead)", "未找到玩家（主選單/載入中/已死亡）" },
        };

        private static Lang _lang = Lang.ZhCN;

        internal static Lang Current => _lang;

        private static string _lastCfgLang;

        internal static void Init()
        {
            string v;
            try { v = ModConfig.MenuLanguage?.Value ?? "zhCN"; }
            catch { v = "zhCN"; }
            if (v == _lastCfgLang) return;                  // 每帧调用只做字符串比较
            _lastCfgLang = v;
            switch (v)
            {
                case "en": _lang = Lang.En; break;
                case "zhTW": _lang = Lang.ZhTW; break;
                default: _lang = Lang.ZhCN; break;
            }
        }

        internal static void SetLang(string code)
        {
            switch (code)
            {
                case "en": _lang = Lang.En; break;
                case "zhTW": _lang = Lang.ZhTW; break;
                default: _lang = Lang.ZhCN; break;
            }
            try { ModConfig.MenuLanguage.Value = code; } catch { }
        }

        internal static string T(string key)
        {
            if (_map.TryGetValue(key, out var v))
            {
                int i = (int)_lang;
                if (i >= 0 && i < v.Length) return v[i];
                return v[0];
            }
            return key;
        }

        internal static string LangCode()
            => _lang == Lang.En ? "en" : (_lang == Lang.ZhTW ? "zhTW" : "zhCN");

        internal static string LangName()
            => _lang == Lang.En ? "English" : (_lang == Lang.ZhTW ? "繁體中文" : "简体中文");

        internal static readonly string[] LangCodes = { "zhCN", "en", "zhTW" };
        internal static string LangDisplay(string code)
            => code == "en" ? "English" : (code == "zhTW" ? "繁體中文" : "简体中文");
    }
}





