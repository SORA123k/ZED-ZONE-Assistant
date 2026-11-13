using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using ZedZone.UltimateAssistant.UI;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 每个功能的独立快捷键 + 开关音效 + 灵动岛通知。
    ///
    /// 设计：
    ///  * 每个功能一个 KeyCode（默认 None），配置独立保存到 [Hotkeys] 段；
    ///  * 按下快捷键时切换该功能，播放启用/关闭音效，并推送到灵动岛；
    ///  * 只处理已注册的功能，未知项不猜测。
    /// </summary>
    internal static class HotkeyManager
    {
        internal sealed class Entry
        {
            internal string Id;
            internal string LabelKey;
            internal Func<bool> Get;
            internal Action<bool> Set;
            internal ConfigEntry<string> Cfg;
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly Dictionary<string, KeyCode> _keys = new Dictionary<string, KeyCode>();
        private static string _capturingId;
        private static float _captureStart;
        private static int _lastFrame = -1;

        internal static IReadOnlyList<Entry> Entries => _entries;

        internal static void Init(ConfigFile cfg)
        {
            _entries.Clear();
            _keys.Clear();

            Add(cfg, "infinite_ammo", "infinite_ammo", () => Features.InfiniteAmmoOn,
                v => { ModConfig.InfiniteAmmo.Value = v; Features.SetInfiniteAmmo(v); });
            Add(cfg, "no_reload", "no_reload", () => Features.NoReloadOn,
                v => { ModConfig.NoReload.Value = v; Features.SetNoReload(v); });
            Add(cfg, "no_jam", "no_jam", () => Features.NoJamOn,
                v => { ModConfig.NoJam.Value = v; Features.SetNoJam(v); });
            Add(cfg, "item_dur", "item_dur", () => Features.ItemDurabilityLockOn,
                v => { ModConfig.ItemDurabilityLock.Value = v; Features.SetItemDurabilityLock(v); });
            Add(cfg, "gun_dur", "gun_dur", () => Features.WeaponDurabilityOn,
                v => { ModConfig.WeaponDurability.Value = v; Features.SetWeaponDurability(v); });
            Add(cfg, "no_overheat", "no_overheat", () => Features.NoOverheatOn,
                v => { ModConfig.NoOverheat.Value = v; Features.SetNoOverheat(v); });
            Add(cfg, "no_hp", "no_hp", () => Features.NoHpLossOn,
                v => { ModConfig.NoHpLoss.Value = v; Features.SetNoHpLoss(v); });
            Add(cfg, "no_sta", "no_sta", () => Features.NoStaminaLossOn,
                v => { ModConfig.NoStaminaLoss.Value = v; Features.SetNoStaminaLoss(v); });
            Add(cfg, "no_hunger", "no_hunger", () => Features.NoHungerLossOn,
                v => { ModConfig.NoHungerLoss.Value = v; Features.SetNoHungerLoss(v); });
            Add(cfg, "no_thirst", "no_thirst", () => Features.NoThirstLossOn,
                v => { ModConfig.NoThirstLoss.Value = v; Features.SetNoThirstLoss(v); });
            Add(cfg, "always_sprint", "always_sprint", () => Features.AlwaysSprintOn,
                v => { ModConfig.AlwaysSprint.Value = v; Features.SetAlwaysSprint(v); });
            Add(cfg, "move_speed", "move_speed", () => Features.MoveSpeedOn,
                v => { ModConfig.MoveSpeed.Value = v; Features.SetMoveSpeed(v); });
            Add(cfg, "esp", "esp", () => Features.ZombieEspOn,
                v => { ModConfig.ZombieEsp.Value = v; Features.SetZombieEsp(v); });
            Add(cfg, "fullbright", "fullbright", () => Features.FullBrightOn,
                v => { ModConfig.FullBright.Value = v; Features.SetFullBright(v); });

            Log.Info($"[hotkey] 已注册 {_entries.Count} 个功能快捷键");
        }

        private static void Add(ConfigFile cfg, string id, string labelKey, Func<bool> get, Action<bool> set)
        {
            var entry = new Entry
            {
                Id = id,
                LabelKey = labelKey,
                Get = get,
                Set = set,
                Cfg = cfg.Bind("Hotkeys", id, "None", "该功能的快捷键（Unity KeyCode 名称，None 表示未绑定）。")
            };
            KeyCode k = KeyCode.None;
            try { k = (KeyCode)Enum.Parse(typeof(KeyCode), entry.Cfg.Value, true); } catch { k = KeyCode.None; }
            _keys[id] = k;
            _entries.Add(entry);
        }

        internal static KeyCode GetKey(string id)
            => _keys.TryGetValue(id, out var k) ? k : KeyCode.None;

        internal static void SetKey(string id, KeyCode k)
        {
            _keys[id] = k;
            foreach (var e in _entries)
            {
                if (e.Id != id) continue;
                try { e.Cfg.Value = k.ToString(); } catch { }
                break;
            }
        }

        internal static void BeginCapture(string id) { _capturingId = id; _captureStart = Time.unscaledTime; }
        internal static bool IsCapturing(string id) => _capturingId == id;
        internal static void CancelCapture() => _capturingId = null;

        internal static void Tick()
        {
            if (Time.frameCount == _lastFrame) return;
            _lastFrame = Time.frameCount;

            // 捕获模式：任意按键即绑定（Escape 取消）
            if (_capturingId != null)
            {
                // 开绑的那次左键/同帧输入不算绑定，避免绑成 Mouse0
                bool guard = (Time.unscaledTime - _captureStart) < 0.25f;

                foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
                {
                    if (kc == KeyCode.None) continue;
                    if (guard && (kc == KeyCode.Mouse0 || kc == KeyCode.Mouse1)) continue;
                    if (Input.GetKeyDown(kc))
                    {
                        if (kc == KeyCode.Escape)
                        {
                            SetKey(_capturingId, KeyCode.None);
                            Log.Info($"[hotkey] {_capturingId} 已清空绑定（ESC）");
                            _capturingId = null;
                            return;
                        }
                        SetKey(_capturingId, kc);
                        Log.Info($"[hotkey] {_capturingId} 绑定到 {kc}");
                        _capturingId = null;
                        return;
                    }
                }
                return;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                var k = GetKey(e.Id);
                if (k == KeyCode.None) continue;
                if (!Input.GetKeyDown(k)) continue;

                bool now = !e.Get();
                try { e.Set(now); } catch (Exception ex) { Log.ErrorOnce("hotkey." + e.Id, ex); }

                UiSound.PlayToggle(now);
                var name = UI.L.T(e.LabelKey);
                DynamicIsland.Push(name, now);
            }
        }
    }
}


