using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 通过 IL2CPP 元数据接口按**字段名**读写实例字段（不猜偏移、不写未知内存）。
    ///
    /// 用途：像 `CharacterStatusData.radiationDose` 这种 interop 没有生成 setter 的字段
    /// （`il2cpp_field_get_offset` / `il2cpp_field_set_value` 都是官方导出 API）。
    ///
    /// 安全边界：
    ///  * 类名/字段名写死为已核对的名字；找不到就返回 false，绝不猜测；
    ///  * 只在调用方给定对象上操作，且全程 try/catch；
    ///  * 解析结果缓存，避免每次查找。
    /// </summary>
    internal static unsafe class Il2CppFields
    {
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
        private static extern void il2cpp_field_get_value(IntPtr obj, IntPtr field, void* value);

        [DllImport("GameAssembly", CallingConvention = CallingConvention.Cdecl)]
        private static extern void il2cpp_field_set_value(IntPtr obj, IntPtr field, void* value);

        private static IntPtr _image = IntPtr.Zero;
        private static readonly System.Collections.Generic.Dictionary<string, IntPtr> _fieldCache =
            new System.Collections.Generic.Dictionary<string, IntPtr>();

        private static string Ptr(IntPtr p)
        {
            try { return p == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(p); } catch { return null; }
        }

        private static IntPtr FindImage()
        {
            if (_image != IntPtr.Zero) return _image;
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
                string nm = Ptr(il2cpp_image_get_name(img));
                if (nm != null && nm.IndexOf("Assembly-CSharp", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _image = img;
                    return img;
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>按类名 + 字段名解析实例字段（缓存）。失败返回 IntPtr.Zero。</summary>
        internal static IntPtr ResolveField(string className, string fieldName)
        {
            string key = className + "." + fieldName;
            if (_fieldCache.TryGetValue(key, out IntPtr cached)) return cached;

            try
            {
                IntPtr img = FindImage();
                if (img == IntPtr.Zero) return IntPtr.Zero;
                IntPtr klass = il2cpp_class_from_name(img, "", className);
                if (klass == IntPtr.Zero) return IntPtr.Zero;

                IntPtr iter = IntPtr.Zero;
                while (true)
                {
                    IntPtr f = il2cpp_class_get_fields(klass, ref iter);
                    if (f == IntPtr.Zero) break;
                    if (Ptr(il2cpp_field_get_name(f)) == fieldName)
                    {
                        _fieldCache[key] = f;
                        return f;
                    }
                }
            }
            catch (Exception e) { Log.WarnOnce("il2cpp.field", "[field] 解析失败: " + e.GetType().Name); }
            _fieldCache[key] = IntPtr.Zero;
            return IntPtr.Zero;
        }

        internal static bool TryGetFloat(IntPtr objPtr, string className, string fieldName, out float value)
        {
            value = 0f;
            try
            {
                var f = ResolveField(className, fieldName);
                if (f == IntPtr.Zero || objPtr == IntPtr.Zero) return false;
                float tmp = 0f;
                il2cpp_field_get_value(objPtr, f, &tmp);
                value = tmp;
                return true;
            }
            catch { return false; }
        }

        internal static bool TrySetFloat(IntPtr objPtr, string className, string fieldName, float value)
        {
            try
            {
                var f = ResolveField(className, fieldName);
                if (f == IntPtr.Zero || objPtr == IntPtr.Zero) return false;
                float tmp = value;
                il2cpp_field_set_value(objPtr, f, &tmp);
                return true;
            }
            catch { return false; }
        }

        /// <summary>字段偏移（诊断/备用写入路径用）。</summary>
        internal static int OffsetOf(string className, string fieldName)
        {
            try
            {
                var f = ResolveField(className, fieldName);
                return f == IntPtr.Zero ? -1 : il2cpp_field_get_offset(f);
            }
            catch { return -1; }
        }
    }
}
