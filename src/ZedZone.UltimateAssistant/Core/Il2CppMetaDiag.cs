using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// Phase 4 / 5：IL2CPP 私有字段元数据探查（**只读**）。
    ///
    /// 目的：`BasicRangedWeapon.durabilityFloat` 只有 getter（赋值得 CS0200），
    /// 且 interop 未暴露任何同名后备字段。本模块通过 GameAssembly.dll 导出的
    /// 标准 IL2CPP 元数据 API，列出该类型的**全部字段（含 private/protected）**、
    /// 类型、偏移、字段属性，以及**全部方法**（用于确认是否存在 set_durabilityFloat）。
    ///
    /// 同时，在游戏内对玩家当前武器的**实例内存做只读采样**：
    /// 逐个实例字段读取原始 float/int/指针值，与 `durabilityFloat` 对照，
    /// 用于验证"getter 是否读取某个实例字段"。
    ///
    /// 约束：
    ///  * 只读：不写任何字段、不调用任何 setter、不修改 GameConfigs；
    ///  * 只读取当前存活对象（同帧获取的指针），并全程 try/catch；
    ///  * 输出到 Diagnostics\il2cpp_meta_yyyyMMdd_HHmmss.log。
    /// </summary>
    internal static class Il2CppMetaDiag
    {
        private const string DiagDir = @"<仓库目录>\ZED-ZONE-Ultimate-Assistant\Diagnostics";
        private const string TargetClass = "BasicRangedWeapon";

        // ---- IL2CPP 导出函数 ----
        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_domain_get();

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_domain_get_assemblies(IntPtr domain, out UIntPtr size);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_assembly_get_image(IntPtr assembly);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_image_get_name(IntPtr image);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_class_from_name(IntPtr image, string ns, string name);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_class_get_fields(IntPtr klass, ref IntPtr iter);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_field_get_name(IntPtr field);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern int il2cpp_field_get_offset(IntPtr field);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint il2cpp_field_get_flags(IntPtr field);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_field_get_type(IntPtr field);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_type_get_name(IntPtr type);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_class_get_methods(IntPtr klass, ref IntPtr iter);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_method_get_name(IntPtr method);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint il2cpp_method_get_param_count(IntPtr method);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_class_get_parent(IntPtr klass);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_class_get_name(IntPtr klass);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr il2cpp_class_get_namespace(IntPtr klass);

        // ---- 状态 ----
        private static StreamWriter _w;
        private static string _path;
        private static bool _inited;
        private static IntPtr _klass;
        private static float _nextSample;
        private static bool _dumpedClass;
        private static int _sampleBudget = 12;

        internal static string FilePath => _path;

        private static string Str(IntPtr p)
        {
            try { return p == IntPtr.Zero ? "?" : (Marshal.PtrToStringAnsi(p) ?? "?"); }
            catch { return "?"; }
        }

        internal static void Init()
        {
            if (_inited) return;
            _inited = true;

            bool enabled = false;
            try { enabled = ModConfig.MetaDiag?.Value ?? false; } catch { }
            if (!enabled) { Log.Info("[meta] IL2CPP 元数据探查未启用"); return; }

            try
            {
                Directory.CreateDirectory(DiagDir);
                _path = Path.Combine(DiagDir, "il2cpp_meta_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
                _w = new StreamWriter(_path, false, new UTF8Encoding(false)) { AutoFlush = true };
            }
            catch (Exception e) { Log.ErrorOnce("meta.open", e); return; }

            W("ZED ZONE IL2CPP 元数据探查（只读）  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            W("target=" + TargetClass + "  plugin=" + Plugin.PluginVersion);
            W("");

            try
            {
                _klass = FindClass(TargetClass);
                if (_klass == IntPtr.Zero)
                {
                    W("[FATAL] 未找到类型 " + TargetClass + "（不猜测）");
                    return;
                }
                DumpClass(_klass, TargetClass);
                _dumpedClass = true;
            }
            catch (Exception e)
            {
                W("[FATAL] 元数据枚举异常: " + e.GetType().Name + ": " + e.Message);
            }

            W("");
            W("=== 说明 ===");
            W("offset 为 IL2CPP 实例字段偏移（0x10 起为对象头后首个字段区）；static 字段 offset 无意义。");
            W("flags: 0x0006=public, 0x0001=private, 0x0004=protected, 0x0010=static, 0x0020=readonly。");
        }

        private static IntPtr FindImage()
        {
            IntPtr dom = il2cpp_domain_get();
            if (dom == IntPtr.Zero) return IntPtr.Zero;

            IntPtr arr = il2cpp_domain_get_assemblies(dom, out UIntPtr count);
            if (arr == IntPtr.Zero) return IntPtr.Zero;

            int n = (int)count.ToUInt64();
            for (int i = 0; i < n; i++)
            {
                IntPtr asm = Marshal.ReadIntPtr(arr, i * IntPtr.Size);
                if (asm == IntPtr.Zero) continue;
                IntPtr img = il2cpp_assembly_get_image(asm);
                if (img == IntPtr.Zero) continue;
                string nm = Str(il2cpp_image_get_name(img));
                if (nm.IndexOf("Assembly-CSharp", StringComparison.OrdinalIgnoreCase) >= 0)
                    return img;
            }
            return IntPtr.Zero;
        }

        private static IntPtr FindClass(string name)
        {
            IntPtr img = FindImage();
            if (img == IntPtr.Zero)
            {
                W("[FATAL] 未找到 Assembly-CSharp 映像");
                return IntPtr.Zero;
            }
            W("[IMAGE] Assembly-CSharp 已定位");

            IntPtr k = il2cpp_class_from_name(img, "", name);
            if (k != IntPtr.Zero) return k;

            // 兜底：不指定命名空间再试一次
            k = il2cpp_class_from_name(img, "ZedZone", name);
            return k;
        }

        private static void DumpClass(IntPtr klass, string label)
        {
            W("================================================================");
            W("[CLASS] " + label);
            W("[CLASS] name=" + Str(il2cpp_class_get_name(klass)));
            W("[CLASS] namespace=" + Str(il2cpp_class_get_namespace(klass)));

            // 继承链
            var chain = new StringBuilder();
            IntPtr p = klass;
            int depth = 0;
            while (p != IntPtr.Zero && depth < 8)
            {
                if (chain.Length > 0) chain.Append(" <- ");
                chain.Append(Str(il2cpp_class_get_name(p)));
                p = il2cpp_class_get_parent(p);
                depth++;
            }
            W("[CLASS] chain=" + chain);
            W("");

            W("---- 实例/静态字段 ----");
            IntPtr iter = IntPtr.Zero;
            int idx = 0;
            while (true)
            {
                IntPtr f = il2cpp_class_get_fields(klass, ref iter);
                if (f == IntPtr.Zero) break;

                string fname = Str(il2cpp_field_get_name(f));
                int off = -1;
                try { off = il2cpp_field_get_offset(f); } catch { }
                uint flags = 0;
                try { flags = il2cpp_field_get_flags(f); } catch { }
                string ftype = "?";
                try { ftype = Str(il2cpp_type_get_name(il2cpp_field_get_type(f))); } catch { }

                W($"  [{idx:D3}] {fname} : {ftype} offset=0x{off:X} flags=0x{flags:X4}");
                idx++;
                if (idx > 2000) { W("  ... 超过 2000 字段，停止"); break; }
            }
            W($"  ---- 共 {idx} 个字段 ----");
            W("");

            W("---- 方法（含 private） ----");
            IntPtr miter = IntPtr.Zero;
            int midx = 0;
            while (true)
            {
                IntPtr m = il2cpp_class_get_methods(klass, ref miter);
                if (m == IntPtr.Zero) break;

                string mname = Str(il2cpp_method_get_name(m));
                uint pc = 0;
                try { pc = il2cpp_method_get_param_count(m); } catch { }

                // 只关心耐久/属性相关，避免刷屏
                if (mname.IndexOf("urab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mname.IndexOf("get_", StringComparison.Ordinal) == 0 && mname.IndexOf("urab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mname.IndexOf("set_", StringComparison.Ordinal) == 0 && mname.IndexOf("urab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mname.IndexOf("Malfunction", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    mname.IndexOf("Temperature", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    W($"  {mname}(params={pc})");
                }
                midx++;
                if (midx > 4000) break;
            }
            W($"  ---- 方法总数 {midx}（仅筛选耐久/故障/温度相关打印） ----");
            W("");
        }

        /// <summary>对当前武器的实例内存做只读采样，用于把字段值与 durabilityFloat 对照。</summary>
        internal static void Tick()
        {
            if (!_inited || _w == null || _klass == IntPtr.Zero) return;
            if (!_dumpedClass) return;
            if (_sampleBudget <= 0) return;

            float now = Time.unscaledTime;
            if (now < _nextSample) return;
            _nextSample = now + 2f;

            try
            {
                HumanCharacterController pl = PlayerAccessor.Resolve(out _, out _);
                if (pl == null) return;

                var arr = Resources.FindObjectsOfTypeAll<BasicRangedWeapon>();
                if (arr == null) return;

                for (int i = 0; i < arr.Length; i++)
                {
                    var wpn = arr[i];
                    if (wpn == null) continue;
                    BasicCharacterController ch = null;
                    try { ch = wpn.character; } catch { }
                    if (ch == null || ch.Pointer != pl.Pointer) continue;

                    float df = -1f;
                    try { df = wpn.durabilityFloat; } catch { }
                    IntPtr basePtr = wpn.Pointer;
                    if (basePtr == IntPtr.Zero) continue;

                    _sampleBudget--;
                    W("");
                    W($"[SAMPLE] t={Time.unscaledTime:0.###} frame={Time.frameCount} ptr=0x{basePtr.ToInt64():X} " +
                      $"durabilityFloat={df:0.######}（预算剩余 {_sampleBudget}）");

                    // 关键：组件自带的 weaponData 实例（可能与装备槽 currentWeaponItemData 不同）
                    try
                    {
                        var wd = wpn.weaponData;
                        if (wd == null) W("  weaponData=null");
                        else
                        {
                            string props = "-";
                            try
                            {
                                var pairs = wd.itemPropertyPairs;
                                if (pairs != null && pairs.Count > 0)
                                {
                                    var sb = new StringBuilder();
                                    for (int k = 0; k < pairs.Count; k++)
                                    {
                                        var kv = pairs[k];
                                        if (kv == null) continue;
                                        string kk = "?", vv = "?";
                                        try { kk = kv.key; } catch { }
                                        try { vv = kv.value; } catch { }
                                        if (sb.Length > 0) sb.Append(',');
                                        sb.Append(kk).Append('=').Append(vv);
                                    }
                                    props = sb.ToString();
                                }
                            }
                            catch { props = "?"; }

                            W($"  weaponData: ptr=0x{wd.Pointer.ToInt64():X} id={wd.itemId} " +
                              $"durability={wd.durability:0.######} num={wd.itemNumberFloat:0.######} props[{props}]");
                            W($"  ratio durability/100={wd.durability / 100f:0.######}  vs durabilityFloat={df:0.######}  " +
                              $"match={(Math.Abs(wd.durability / 100f - df) < 0.01f ? "YES" : "no")}");
                        }
                    }
                    catch (Exception e) { W("  weaponData 读取失败: " + e.GetType().Name); }

                    DumpInstanceValues(basePtr, df);
                    break;
                }
            }
            catch (Exception e)
            {
                W("[SAMPLE] 异常: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static void DumpInstanceValues(IntPtr basePtr, float df)
        {
            IntPtr iter = IntPtr.Zero;
            int idx = 0;
            while (true)
            {
                IntPtr f = il2cpp_class_get_fields(_klass, ref iter);
                if (f == IntPtr.Zero) break;

                string fname = Str(il2cpp_field_get_name(f));
                int off = -1;
                try { off = il2cpp_field_get_offset(f); } catch { }
                uint flags = 0;
                try { flags = il2cpp_field_get_flags(f); } catch { }
                string ftype = "?";
                try { ftype = Str(il2cpp_type_get_name(il2cpp_field_get_type(f))); } catch { }

                idx++;
                if ((flags & 0x0010) != 0) continue;        // static 跳过
                if (off < 0x10 || off > 0x4000) continue;

                try
                {
                    IntPtr addr = new IntPtr(basePtr.ToInt64() + off);
                    int i32 = Marshal.ReadInt32(addr);
                    float f32 = BitConverter.Int32BitsToSingle(i32);
                    long i64 = Marshal.ReadInt64(addr);

                    string mark = "";
                    if (Math.Abs(f32 - df) < 0.0005f) mark = "  <<< 与 durabilityFloat 相等";
                    else if (df > 0.0001f && Math.Abs(f32 - df * 100f) < 0.5f) mark = "  <<< = durabilityFloat*100";
                    else if (df > 0.0001f && Math.Abs(f32 - df * 1000f) < 5f) mark = "  <<< = durabilityFloat*1000";

                    W($"  {fname} : {ftype} off=0x{off:X} float={f32:0.######} int={i32} long=0x{i64:X}{mark}");
                }
                catch { }
                if (idx > 2000) break;
            }
        }

        private static void W(string s)
        {
            try { _w?.WriteLine(s); } catch { }
        }

        internal static void Shutdown()
        {
            try
            {
                if (_w != null)
                {
                    W("");
                    W("[END] " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    W("[END] 文件: " + _path);
                    _w.Flush();
                    _w.Dispose();
                    _w = null;
                }
            }
            catch { }
        }
    }
}

