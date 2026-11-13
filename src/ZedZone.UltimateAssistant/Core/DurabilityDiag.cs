using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 耐久只读诊断模块（不修改任何游戏对象 / 物品属性）。
    ///
    /// 目的：在无法确定"耐久真实存储字段"与"扣减调用链"的情况下，
    /// 用**证据**代替猜测，把运行时事实记录下来，供后续决策。
    ///
    /// 约束：
    ///  * 只读：不写任何 ItemData / 组件字段（唯一的 Harmony 补丁都是 postfix 纯记录）；
    ///  * 不修改 GameConfigs；
    ///  * 不改动恢复/弹药/ESP/菜单功能；
    ///  * 日志写到 <仓库目录>\ZED-ZONE-Ultimate-Assistant\Diagnostics\。
    ///
    /// 记录内容：
    ///  运行时（Unity / .NET / BepInEx / Harmony / IL2CPP / 位数）、
    ///  玩家装备槽物品（类型、实例指针、itemId、durability、itemNumberFloat、属性表）、
    ///  玩家武器组件（durabilityFloat、m_gunpartDurabilityCostFactor、isMalfunction）、
    ///  数值变化前后（changE）+ 时间戳 + 帧号、
    ///  以及 `durabilityFloat` 被读取时的调用栈（用于证明"调用来源"）。
    /// </summary>
    internal static class DurabilityDiag
    {
        private const string DiagDir = @"<仓库目录>\ZED-ZONE-Ultimate-Assistant\Diagnostics";

        private static bool _inited;
        private static bool _enabled;
        private static StreamWriter _w;
        private static string _path;
        private static float _nextTick;
        private static int _nextStackLog = 20;

        private static readonly Dictionary<IntPtr, SlotSnap> _slotPrev = new Dictionary<IntPtr, SlotSnap>();
        private static readonly Dictionary<IntPtr, WeaponSnap> _weapPrev = new Dictionary<IntPtr, WeaponSnap>();

        private static string _lastScene = "";
        private static Harmony _harmony;

        private sealed class SlotSnap
        {
            internal string Slot = "";
            internal int Id;
            internal float Dur;
            internal float Num;
            internal string Props = "";
        }

        private sealed class WeaponSnap
        {
            internal float Durability = -1f;
            internal float CostFactor = -1f;
            internal bool Malfunction;
            internal string Name = "";
        }

        internal static string FilePath => _path;
        internal static bool Enabled => _enabled;

        // ==================================================================
        internal static void Init()
        {
            if (_inited) return;
            _inited = true;

            try
            {
                _enabled = ModConfig.DurabilityDiag?.Value ?? false;
            }
            catch { _enabled = false; }

            if (!_enabled)
            {
                Log.Info("[diag] 耐久诊断未启用（[Diagnostics] DurabilityDiag=false）");
                return;
            }

            try
            {
                Directory.CreateDirectory(DiagDir);
                string name = "durability_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
                _path = Path.Combine(DiagDir, name);
                _w = new StreamWriter(_path, false, new UTF8Encoding(false)) { AutoFlush = true };
            }
            catch (Exception e)
            {
                _enabled = false;
                Log.ErrorOnce("diag.open", e);
                return;
            }

            Write("================================================================");
            Write("ZED ZONE 耐久只读诊断" + "  start=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            Write("================================================================");
            WriteRuntimeInfo();
            InstallObservers();
        }

        private static void WriteRuntimeInfo()
        {
            Write("[RUNTIME] unityVersion=" + Safe(() => Application.unityVersion));
            Write("[RUNTIME] gameVersion=" + Safe(() => Application.version));
            Write("[RUNTIME] platform=" + Safe(() => Application.platform.ToString()));
            Write("[RUNTIME] dataPath=" + Safe(() => Application.dataPath));
            Write("[RUNTIME] managedRuntime=" + Safe(() => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription));
            Write("[RUNTIME] osDescription=" + Safe(() => System.Runtime.InteropServices.RuntimeInformation.OSDescription));
            Write("[RUNTIME] processBitness=" + (Environment.Is64BitProcess ? "x64" : "x86"));
            Write("[RUNTIME] clrVersion=" + Safe(() => Environment.Version.ToString()));
            Write("[MOD] plugin=" + Plugin.PluginName + " v" + Plugin.PluginVersion);
            Write("[MOD] pluginGuid=" + Plugin.PluginGuid);
            Write("[MOD] bepInEx=" + Safe(() => typeof(Plugin).BaseType.Assembly.GetName().Version.ToString()));
            Write("[MOD] harmony=" + Safe(() => typeof(Harmony).Assembly.GetName().Version.ToString()));
            Write("[MOD] il2cppInterop=" + Safe(() => typeof(Il2CppInterop.Runtime.IL2CPP).Assembly.GetName().Version.ToString()));
            Write("[MOD] probes=只读（postfix 仅记录）");
            Write("");
        }

        private static string Safe(Func<string> f)
        {
            try { return f() ?? "null"; } catch (Exception e) { return "<unavailable: " + e.GetType().Name + ">"; }
        }

        private static void Write(string line)
        {
            try { _w?.WriteLine(line); } catch { }
        }

        // ==================================================================
        /// <summary>只读观察器：postfix 仅记录，不改变任何返回值/状态。</summary>
        private static void InstallObservers()
        {
            try
            {
                _harmony = new Harmony(Plugin.PluginGuid + ".diag");

                // 1) 记录耐久 getter 的调用栈（证明"谁在读耐久"）
                try
                {
                    var must = AccessTools.Method(typeof(BasicRangedWeapon), "get_durabilityFloat");
                    if (must != null)
                    {
                        _harmony.Patch(must, postfix: new HarmonyMethod(typeof(DurabilityDiag), nameof(DurabilityGetterPostfix)));
                        Write("[OBSERVER] 已挂 BasicRangedWeapon.get_durabilityFloat（postfix 只记录）");
                    }
                    else Write("[OBSERVER] 未找到 get_durabilityFloat（不猜测，跳过）");
                }
                catch (Exception e) { Write("[OBSERVER] get_durabilityFloat 挂载失败: " + e.GetType().Name); }

                // 2) 记录控制器的耐久消耗方法被调用的证据（不跳过原逻辑）
                TryObserver("HumanCharacterController.CostItemDurability", typeof(HumanCharacterController),
                    new[] { typeof(ItemData), typeof(float) }, nameof(CostDurabilityPostfix));
                TryObserver("HumanCharacterController.CostItemDurabilityExecute", typeof(HumanCharacterController),
                    new[] { typeof(ItemData), typeof(float) }, nameof(CostDurabilityPostfix));
                TryObserver("InventoryData.CostItemDurability", typeof(InventoryData),
                    new[] { typeof(ItemData), typeof(float) }, nameof(InvCostPostfix));

                Write("");
            }
            catch (Exception e) { Write("[OBSERVER] 安装失败: " + e.GetType().Name + ": " + e.Message); }
        }

        private static void TryObserver(string label, Type type, Type[] args, string postfixName)
        {
            try
            {
                var m = AccessTools.Method(type, label.Substring(label.IndexOf('.') + 1), args);
                if (m == null) { Write("[OBSERVER] 未找到 " + label + "（不猜测，跳过）"); return; }
                _harmony.Patch(m, postfix: new HarmonyMethod(typeof(DurabilityDiag), postfixName));
                Write("[OBSERVER] 已挂 " + label + "（postfix 只记录，不改变行为）");
            }
            catch (Exception e) { Write("[OBSERVER] " + label + " 挂载失败: " + e.GetType().Name); }
        }

        internal static void DurabilityGetterPostfix(BasicRangedWeapon __instance, ref float __result)
        {
            try
            {
                if (_nextStackLog <= 0) return;
                _nextStackLog--;
                Write($"[READ durabilityFloat] value={__result:0.#####} time={Time.unscaledTime:0.###} frame={Time.frameCount} " +
                      $"weapon={Safe(() => __instance.gameObject.name)}");
                Write("[READ-STACK] " + ShortStack());
                if (_nextStackLog == 0) Write("[READ] 已达记录上限，后续 getter 调用不再记录栈");
            }
            catch { }
        }

        internal static void CostDurabilityPostfix(HumanCharacterController __instance, ItemData itemData, bool __result)
        {
            try
            {
                string id = "?";
                string dur = "?";
                try { if (itemData != null) { id = itemData.itemId.ToString(); dur = itemData.durability.ToString("0.###"); } } catch { }
                Write($"[CALL HCC.CostItemDurability] result={__result} itemId={id} itemDur={dur} " +
                      $"time={Time.unscaledTime:0.###} frame={Time.frameCount}");
            }
            catch { }
        }

        internal static void InvCostPostfix(InventoryData __instance, ItemData itemData, bool __result)
        {
            try
            {
                string id = "?";
                try { if (itemData != null) id = itemData.itemId.ToString(); } catch { }
                Write($"[CALL InventoryData.CostItemDurability] result={__result} itemId={id} " +
                      $"time={Time.unscaledTime:0.###} frame={Time.frameCount}");
            }
            catch { }
        }

        private static string ShortStack()
        {
            try
            {
                var st = new System.Diagnostics.StackTrace(true);
                var sb = new StringBuilder();
                int n = 0;
                for (int i = 0; i < st.FrameCount && n < 8; i++)
                {
                    var m = st.GetFrame(i)?.GetMethod();
                    if (m == null) continue;
                    var decl = m.DeclaringType?.FullName ?? "?";
                    if (decl.StartsWith("ZedZone.UltimateAssistant")) continue;   // 跳过本模组帧
                    sb.Append(decl).Append('.').Append(m.Name).Append(" <- ");
                    n++;
                }
                return sb.Length > 0 ? sb.ToString() : "<no managed frames>";
            }
            catch { return "<stack unavailable>"; }
        }

        // ==================================================================
        internal static void Tick()
        {
            if (!_enabled || !_inited) return;

            try
            {
                float now = Time.unscaledTime;
                if (now < _nextTick) return;
                _nextTick = now + 0.5f;

                // 场景切换
                string scene = "";
                try { scene = SceneManager.GetActiveScene().name; } catch { }
                if (scene != _lastScene)
                {
                    _lastScene = scene;
                    _slotPrev.Clear();
                    _weapPrev.Clear();
                    Write("");
                    Write($"[SCENE] '{scene}' t={DateTime.Now:HH:mm:ss.fff}");
                }

                HumanCharacterController p = null;
                try { p = PlayerAccessor.Resolve(out _, out _); } catch { }

                if (p == null)
                {
                    Write("[PLAYER] null（主菜单/加载中/死亡重生窗口）—— 跳过本次采样");
                    return;
                }

                ScanEquipment(p);
                ScanWeapons(p);
            }
            catch (Exception e)
            {
                Write("[ERROR] Tick: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static void ScanEquipment(HumanCharacterController p)
        {
            CharacterData cd = null;
            try { cd = p.characterData; } catch { }
            if (cd == null) { Write("[EQUIP] characterData=null —— 无法读取装备"); return; }

            CharacterEquipmentData eq = null;
            try { eq = cd.characterEquipmentData; } catch { }
            if (eq == null) { Write("[EQUIP] characterEquipmentData=null —— 无法读取装备"); return; }

            Slots(eq, "currentWeapon", () => eq.currentWeaponItemData);
            Slots(eq, "lightingTool", () => eq.lightingToolItemData);
            Slots(eq, "specialEquipment", () => eq.specialEquipmentItemData);
            Slots(eq, "throwable", () => eq.throwableItemData);
            Slots(eq, "hat", () => eq.hatItemData);
            Slots(eq, "torse", () => eq.torseItemData);
            Slots(eq, "leg", () => eq.legItemData);
            Slots(eq, "feet", () => eq.feetItemData);
            Slots(eq, "shield", () => eq.shieldItemData);
            Slots(eq, "backpack", () => eq.backpackItemData);
            Slots(eq, "exoskeleton", () => eq.exoskeletonItemData);
        }


        private static void Slots(CharacterEquipmentData eq, string slot, Func<ItemData> get)
        {
            ItemData it = null;
            try { it = get(); }
            catch (Exception e)
            {
                Write($"[EQUIP {slot}] 读取异常 {e.GetType().Name} —— 该槽位无法读取");
                return;
            }

            if (it == null) return;

            try
            {
                IntPtr ptr = it.Pointer;
                var cur = new SlotSnap
                {
                    Slot = slot,
                    Id = it.itemId,
                    Dur = it.durability,
                    Num = it.itemNumberFloat,
                    Props = Props(it)
                };

                if (_slotPrev.TryGetValue(ptr, out var prev))
                {
                    if (prev.Id != cur.Id || Math.Abs(prev.Dur - cur.Dur) > 0.0001f ||
                        Math.Abs(prev.Num - cur.Num) > 0.0001f || prev.Props != cur.Props)
                    {
                        Write($"[EQUIP-CHANGE] slot={slot} id={cur.Id} ptr=0x{ptr.ToInt64():X} " +
                              $"dur {prev.Dur:0.####}->{cur.Dur:0.####} num {prev.Num:0.####}->{cur.Num:0.####} " +
                              $"props[{prev.Props}] -> [{cur.Props}] t={Time.unscaledTime:0.###} frame={Time.frameCount}");
                    }
                }
                else
                {
                    Write($"[EQUIP] slot={slot} id={cur.Id} ptr=0x{ptr.ToInt64():X} dur={cur.Dur:0.####} " +
                          $"num={cur.Num:0.####} props[{cur.Props}]");
                }
                _slotPrev[ptr] = cur;
            }
            catch (Exception e)
            {
                Write($"[EQUIP {slot}] 对象失效/无法识别（{e.GetType().Name}）—— 不猜测字段");
            }
        }

        private static void ScanWeapons(HumanCharacterController p)
        {
            BasicRangedWeapon[] arr = null;
            try { arr = Resources.FindObjectsOfTypeAll<BasicRangedWeapon>(); }
            catch (Exception e) { Write("[WEAPON] 枚举失败: " + e.GetType().Name); return; }
            if (arr == null) { Write("[WEAPON] 枚举返回 null"); return; }

            IntPtr pp = p.Pointer;

            for (int i = 0; i < arr.Length; i++)
            {
                BasicRangedWeapon w = arr[i];
                if (w == null) continue;
                try
                {
                    BasicCharacterController ch = w.character;
                    if (ch == null || ch.Pointer != pp) continue;

                    IntPtr wp = w.Pointer;
                    float df = -1f, cf = -1f;
                    bool malf = false;
                    string name = "";
                    try { df = w.durabilityFloat; } catch { }
                    try { cf = w.m_gunpartDurabilityCostFloat; } catch { }
                    try { malf = w.isMalfunction; } catch { }
                    try { name = w.gameObject != null ? w.gameObject.name : "?"; } catch { }

                    var cur = new WeaponSnap { Durability = df, CostFactor = cf, Malfunction = malf, Name = name };

                    if (_weapPrev.TryGetValue(wp, out var prev))
                    {
                        if (Math.Abs(prev.Durability - cur.Durability) > 0.0001f ||
                            Math.Abs(prev.CostFactor - cur.CostFactor) > 0.0001f ||
                            prev.Malfunction != cur.Malfunction)
                        {
                            Write($"[WEAPON-CHANGE] ptr=0x{wp.ToInt64():X} name={cur.Name} " +
                                  $"durabilityFloat {prev.Durability:0.####}->{cur.Durability:0.####} " +
                                  $"costFactor {prev.CostFactor:0.####}->{cur.CostFactor:0.####} " +
                                  $"malf {prev.Malfunction}->{cur.Malfunction} t={Time.unscaledTime:0.###} frame={Time.frameCount}");
                        }
                    }
                    else
                    {
                        Write($"[WEAPON] ptr=0x{wp.ToInt64():X} name={cur.Name} durabilityFloat={df:0.####} " +
                              $"costFactor={cf:0.####} malf={malf}");
                    }
                    _weapPrev[wp] = cur;
                }
                catch (Exception e)
                {
                    Write("[WEAPON] 组件失效/无法读取（" + e.GetType().Name + "）—— 跳过，不猜测");
                }
            }
        }

        private static string Props(ItemData it)
        {
            try
            {
                var pairs = it.itemPropertyPairs;
                if (pairs == null || pairs.Count == 0) return "-";
                var sb = new StringBuilder();
                for (int i = 0; i < pairs.Count; i++)
                {
                    var kv = pairs[i];
                    if (kv == null) continue;
                    string k = "?", v = "?";
                    try { k = kv.key; } catch { }
                    try { v = kv.value; } catch { }
                    if (sb.Length > 0) sb.Append(',');
                    sb.Append(k).Append('=').Append(v);
                }
                return sb.ToString();
            }
            catch { return "?"; }
        }

        internal static void Shutdown()
        {
            try
            {
                Write("");
                Write("[END] 诊断结束 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                Write("[END] 日志文件: " + _path);
                _w?.Flush();
                _w?.Dispose();
                _w = null;
            }
            catch { }
            try { _harmony?.UnpatchSelf(); } catch { }
        }

        internal static string StatusText()
            => _enabled ? ("诊断=开 " + (_path ?? "")) : "诊断=关";
    }
}


