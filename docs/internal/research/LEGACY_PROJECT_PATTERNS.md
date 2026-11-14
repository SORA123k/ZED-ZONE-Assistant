# 旧项目可复用工程模式提取

> 来源：`<仓库目录>\ZED-ZONE-FPS-Optimization-Mod`（已在实机验证加载，v1.0.0）
> 目的：让 ZED ZONE Ultimate Assistant 继承"踩过的坑"与"验证过的写法"，而不是复制代码。
> 本文只读调查，未修改旧项目、未运行游戏。
>
> **证据分级约定**（沿用旧项目 `research/00-verified-facts.md` 的标注法）：
> - `【事实-文件】` —— 直接读到源码/文档原文，附文件名与行号。
> - `【事实-运行】` —— 旧项目文档里记录的运行期日志/截图证据。
> - `【推断】` —— 由事实推出、但未直接验证。
> - `【未确认】` —— 本次调查没有找到证据，**不要当成事实使用**。

---

## 1. BepInEx 初始化与插件加载方式

### 1.1 插件声明与入口

【事实-文件】`src/ZedFpsOptimizer/Plugin.cs`：

```csharp
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]   // :27
public class Plugin : BasePlugin                        // :28
{
    public const string PluginGuid = "zedzone.fpsoptimizer";      // :30
    public const string PluginName = "ZED ZONE FPS Optimizer";    // :31
    public const string PluginVersion = "1.0.0";                  // :32

    public override void Load()                                // :68
```

要点：

1. **`[BepInPlugin]` 是唯一"被加载"的凭据**。GUID（`zedzone.fpsoptimizer`）同时决定两件事：BepInEx 的插件身份，以及配置文件路径 `<游戏目录>\BepInEx\config\zedzone.fpsoptimizer.cfg`（`README.md:58`）。**GUID 一旦发布就不要改** —— 改了等于所有用户配置丢失。
2. **`BasePlugin` + `override Load()`** 是唯一入口，没有 `Awake/Update`。所有初始化写在 `Load()` 里，顺序是：`Instance = this`（:70）→ 版本探测（:71）→ `BindConfig()`（:76）→ Preset（:77）→ 注入 static 开关（:79-84）→ 帧设置（:86）→ **按需装补丁**（:97-113）→ HUD（:119-131）。
3. **`internal static Plugin Instance`（:34）是必需品，不是风格问题**。因为 Harmony `Prefix/Postfix` 必须是 `static`（`PerfTweaks.cs:81`），static 补丁拿不到插件实例，只能通过 `Plugin.LogWarn(...)` / `Plugin.LogInfo(...)` 这类 static 包装（`Plugin.cs:300-308`）回调。同样的原因，`PerfTweaks` 的开关也是 `public static` 字段（`PerfTweaks.cs:24-28`）。
4. **`internal static string GameVersion`（:35）** 由 `Application.version` 探测，探测失败保留 `"unknown"`（:71 `try { } catch { }`）。版本号会被写进基准测试 summary（`BenchmarkRecorder.cs:138`），也打在启动日志里（:74）—— 用户报 bug 时这是第一个要问的东西。

### 1.2 `AddComponent<T>()` 如何把 IL2CPP MonoBehaviour 注入进去

【事实-文件】`Plugin.cs:119-131`：

```csharp
try
{
    _overlay = AddComponent<FpsOverlay>();   // :121
    _overlay.Stats = _stats;                 // :122
    _overlay.Recorder = _recorder;           // :123
    _overlay.Visible = ShowHudOnStart.Value; // :124
    _overlay.ShowDetail = false;             // :125
    LogInfo("HUD component injected");       // :126
}
catch (Exception e) { LogWarn($"HUD injection failed ..."); }  // :128-131
```

机制与约束：

- `FpsOverlay : MonoBehaviour`（`FpsOverlay.cs:14`）是**托管侧**的 Mono 类型。`AddComponent<T>()` 让 Il2CppInterop 把这个托管类型注册到 il2cpp 运行时（生成对应的 native class），再挂到当前 GameObject 上，之后 Unity 就会正常派发 `Awake/Update/OnGUI`。
- 【事实-运行】注册确实发生了：旧项目日志里有 `Registered mono type ZedZone.FpsOptimizer.FpsOverlay`（`docs/DEVELOPMENT_ROADMAP.md:78`）。
- 【推断】之所以能直接 `AddComponent<T>()`，是因为 BepInEx 6 的 `BasePlugin` 自身就是挂在 BepInEx manager GameObject 上的 MonoBehaviour 派生对象，`AddComponent` 是它的成员。**没有读到 BepInEx 源码，此点为推断。**
- **依赖必须手工喂**：Unity 不会调托管构造函数，`AddComponent` 后只有 `Awake()`。所以 `Stats/Recorder/Visible/ShowDetail` 都在 :122-125 手工赋值 —— 漏一个就是 `NullReferenceException`，而 `Awake` 里拿到的是默认值。
- **必须 `DontDestroyOnLoad`**：`FpsOverlay.cs:36-40` 在 `Awake()` 里调，并 `try/catch`（注释原文"某些 IL2CPP 场景下不支持"）。不这么做，第一次切场景 HUD 就消失。
- **注入失败不能让插件挂掉**：整个块包在 `try/catch` 里，失败只 `LogWarn` 并注明"benchmark still available via config"（:130）。这是"单一功能失败不传染"原则的最小实现。

### 1.3 `IL2CPPChainloader` 做什么

【事实-文件】`research/modding-methods.md:384`（反编译社区加载器 `ZedModsLoader` 所得）：

```csharp
var infos = IL2CPPChainloader.Instance.LoadPlugins(dirs.ToArray());
```

`IL2CPPChainloader` 是 **BepInEx 自己的** IL2CPP 插件加载器，不是社区发明的东西。社区加载器只是复用它的一个方法。它做的整条链是：扫描给定目录 → 逐个 `Assembly` 载入 → 反射找带 `[BepInPlugin]` 的 `BasePlugin` 子类 → 实例化 → 调用 `Load()` → 把 `PluginInfo` 登记进 Chainloader 的插件表。

> 【未逐行阅读 BepInEx 源码】上段是 BepInEx 的公开行为描述，证据是 `modding-methods.md:384` 的调用点 + 旧项目自己的运行期日志（`README.md:44` `[Info :ZED ZONE FPS Optimizer] loading ...`）。**对我们来说唯一重要的结论是：插件侧不需要写任何"注册自己"的代码，只要 `[BepInPlugin]` + `BasePlugin` + `Load()`。**

### 1.4 自定义 `ZedModsLoader` 如何从 `Mods\` 捡 DLL

【事实-文件】`research/existing-mod-analysis.md:414-450`、`research/modding-methods.md:375-396`。

**它本身也是一个 BepInEx 插件**：`[BepInPlugin("zedzone.zedmodsloader", "ZED ZONE Code Mod Loader", "1.1.0")]`，装在 `<游戏目录>\BepInEx\plugins\`。游戏自带的 Mods 体系只认数据模组（`modding-methods.md:454` 引其 README 原文："游戏自带的 Mods 只认数据模组（.json / .zedmod 里的物品数值），**不能运行代码**"），所以代码必须靠这条链。

**扫描 4 个来源**（`existing-mod-analysis.md:418-422`）：

| # | 来源 | 备注 |
|---|---|---|
| 1 | `<游戏目录>\Mods\` | **我们装插件就放这里** |
| 2 | `%USERPROFILE%\AppData\LocalLow\LevenLiu\ZEDZONE\Mods` | 比官方 Workspace 高一级 |
| 3 | `<Steam库>\steamapps\workshop\content\1211600\` | 工坊裸 DLL |
| 4 | 配置 `ExtraModDirectories`（`;` 分隔） | 自定义 |

**三种被接受的模组写法**（`existing-mod-analysis.md:424-427`）：
- A. 标准 BepInEx 插件 —— 走 `IL2CPPChainloader.LoadPlugins`，完整 BepInEx 生命周期。**这就是旧项目用的，也是新项目应该用的。**
- B. 纯 Harmony 补丁 DLL（无 `[BepInPlugin]`）—— 加载器自己 `PatchAll`（配置键 `AutoHarmonyPatch`）。
- C. 静态入口 —— 反射调 `public static void OnLoad()` / `OnModLoad()`。

**我们依赖的实现细节**（`existing-mod-analysis.md:429-435`）：
- `.zedmod` 里含 DLL 时，解到 `BepInEx\ZedModsCache\<hash>\` 再加载 —— 所以**不要把插件塞进 `.zedmod`**，直接放 `Mods\` 更简单。
- **跳过规则**：`BepInEx*`、`Il2CppInterop*`、`0Harmony*`、`MonoMod*`、`*.resources.dll`、`*.dll.disabled`、`*.disabled\`、`*_disabled\`、`mod.json: {"enabled": false}`。→ 临时禁用插件的最稳做法是把目录改名或删 DLL。
- **幂等**：同名程序集（`SafeFullPath` 归一化）只加载一次。
- 写 `BepInEx\ZedModsLoader-report.txt` 加载报告 —— **这是"我的 DLL 到底有没有被捡起来"的第一手证据**，比 `LogOutput.log` 更早、更明确。

### 1.5 三条环境硬约束（不满足就白干）

| 约束 | 证据 |
|---|---|
| **必须从 Steam 启动**（`steam://rungameid/1211600`），直接双击 `ZEDZONE.exe` 会因 Steam 校验秒退（退出码 53） | `docs/DEVELOPMENT_ROADMAP.md:154`；`existing-mod-analysis.md:458` |
| 代码模组**不参与联机同步**：别人没有你的 DLL | `research/modding-methods.md:457`；`README.md:192-194` |
| 游戏更新后若 interop 不匹配，**删掉 `BepInEx\interop\` 让它重新生成** | `existing-mod-analysis.md:458` |

---

## 2. HarmonyX Patch 实现方式

### 2.1 实际使用的写法

【事实-文件】`PerfTweaks.cs:78-92`（最典型的三个之一）：

```csharp
[HarmonyPatch(typeof(BasicCharacterController), nameof(BasicCharacterController.UpdateShadowEffect))]
internal static class Patch_UpdateShadowEffect
{
    private static bool Prefix(BasicCharacterController __instance)
    {
        if (!PerfTweaks.OffscreenCosmeticSkip) return true;              // :83 双保险
        try
        {
            if ((UnityEngine.Object)__instance == null) return true;     // :86 见 §3.4
            if (__instance.isPlayer) return true;                        // :87
            return __instance.isOnScreen;                                 // :88
        }
        catch (Exception e) { PerfTweaks.NoteFailure("UpdateShadowEffect", e); return true; }
    }
}
```

四个可复用的具体技巧：

1. **`nameof(X.M)` 而不是字符串字面量**（:78/:94/:110/:130/:160）。游戏更新后如果方法被重命名，**编译期**就报错，而不是运行期静默失效。这是 `nameof` 在这个场景下唯一真正的价值。
2. **`Prefix` 直接 `return bool`**（:88 `return __instance.isOnScreen;`）—— Prefix 的返回值**同时**是"是否执行原方法"和"我们的判定结果"。省掉 `__result` 中转，也省掉一次分支。
3. **`Postfix` 用于"写字段"，返回 `void`**（`PerfTweaks.cs:130-153`）：在 `ParticleAutoSleep.OnEnable` 之后把 `idleFrames` 抬到阈值。**Postfix 不能阻止原方法，所以只适合"事后修正"**。
4. **`ref bool __result` 伪造返回值**（`PerfTweaks.cs:163-174`）：
   ```csharp
   private static bool Prefix(bool isPriority, ref bool __result)
   {
       if (isPriority) return true;
       if (ChunkSpawnFrameBudget.s_itemsThisFrame >= max) { __result = false; return false; }
       return true;
   }
   ```
   注意顺序：**先设 `__result` 再 `return false`**，否则原方法被跳过但返回值是 `default(bool)`。

### 2.2 `TryPatch()`：失败降级

【事实-文件】`Plugin.cs:238-251`：

```csharp
private int TryPatch(Type patchType, string label)
{
    try
    {
        _harmony.PatchAll(patchType);   // :242
        return 1;
    }
    catch (Exception e)
    {
        // 预期内的情况：游戏更新后方法改名/改签名。此时该优化项静默失效，其余功能不受影响。
        LogWarn($"patch skipped [{label}]: {e.GetType().Name}: {e.Message}");  // :248
        return 0;
    }
}
```

为什么这层包装是**必需的**：

- `PatchAll(Type)` 在目标方法不存在、签名不匹配、或泛型/重载有歧义时会抛。**一个补丁的类型解析失败会连带该类型下所有补丁**。
- 旧项目装载 5 个补丁用了 5 次独立 `TryPatch`（`Plugin.cs:100-109`），而不是一次 `PatchAll(assembly)` —— **粒度必须细到"一个功能一项"**，否则一个坏的拖垮四个好的。
- 返回值累加成一个计数器（:98 `int patched`），最后打一行日志（:111-113）：
  ```
  harmony patches installed: 0 (no tweaks enabled — pure vanilla behaviour)
  ```
  这行日志是**设计约束的可执行证明**（`DEVELOPMENT_ROADMAP.md:54`），不是装饰。用户/我们自己照着这一行就能判断"现在到底装了几个补丁"。
- `label` 参数是纯人读的（`"BasicCharacterController.UpdateShadowEffect"`），保证日志能直接对应到游戏方法名。

### 2.3 补丁内部的异常策略

【事实-文件】`PerfTweaks.cs:61-66`：

```csharp
public static void NoteFailure(string patch, Exception e)
{
    PatchFailures[patch] = PatchFailures.TryGetValue(patch, out int n) ? n + 1 : 1;
    if (PatchFailures[patch] == 1)
        Plugin.LogWarn($"patch '{patch}' threw, falling back to vanilla: {e.GetType().Name}: {e.Message}");
}
```

规则：**每个补丁体整体 `try/catch`，catch 里 `return true`（退回原版行为），并用 `NoteFailure` 只报第一次。** 三条缺一不可：
- 不 catch → 补丁抛异常会打断游戏原生方法，可能直接崩。
- 不 `return true` → 异常时游戏逻辑被静默跳过，比崩溃更难排查。
- 不"只报一次" → 每帧补丁每帧抛，日志文件一分钟到 GB 级（这正是旧项目分析的第三方模组 `EnemyHealthBar` 的反面教材，`existing-mod-analysis.md:132-134`，其 `ArmorDebug` 默认 `true` 刷屏）。

---

## 3. Il2CppInterop 对象访问方式

### 3.1 字段读写就是普通属性访问

【事实-文件】`PerfTweaks.cs`：

```csharp
GoreBudget.Enabled = true;                                          // :42  静态字段写
if (GoreBudget.MaxConcurrent > GoreMaxConcurrent)
    GoreBudget.MaxConcurrent = GoreMaxConcurrent;                   // :44  只下调不上调
ParticleFx.IdleFramesBeforeSleep = PerfTweaks.ParticleIdleFrames;   // :143
__instance.idleFrames = PerfTweaks.ParticleIdleFrames;              // :149 实例字段写
```

写法上和普通 C# 属性完全一样 —— 这正是 Il2CppInterop 的价值：**interop stub 把 native 字段包装成了托管属性**，不需要 `FieldInfo`，不需要写字符串。

### 3.2 为什么比反射便宜

【事实-文件】新项目已经反编译出的同类 stub 是最好的证据 —— `research/decompiled/CharacterStatusData.cs:117-128`：

```csharp
public unsafe float maxhp
{
    get
    {
        nint num = (nint)IL2CPP.Il2CppObjectBaseToPtrNotNull((Il2CppObjectBase)(object)this)
                 + (int)IL2CPP.il2cpp_field_get_offset(NativeFieldInfoPtr_maxhp);
        return *(float*)num;          // 一次指针加法 + 一次解引用
    }
    set
    {
        *(float*)((nint)IL2CPP.Il2CppObjectBaseToPtrNotNull((Il2CppObjectBase)(object)this)
                + (int)IL2CPP.il2cpp_field_get_offset(NativeFieldInfoPtr_maxhp)) = value;
    }
}
```

关键点：

- **字段偏移在静态构造里缓存一次**（`CharacterStatusData.cs:382-403` 的 `IL2CPP.GetIl2CppField(...)`，存进 `NativeFieldInfoPtr_*` 的 `IntPtr`）。所以每次读写只剩"取缓存偏移 + 指针加法 + `*(float*)` 解引用"。
- 反射的 `FieldInfo.SetValue` 每次都要：字符串/元数据查表 → 装箱 → 访问可见性检查 → 可能分配。在高频路径上是**数量级**的差距。
- **静态字段走另一条路**：`il2cpp_field_static_get_value/set_value`（`CharacterStatusData.cs:61-73`），因为静态字段没有实例偏移。这一点在写 `maxHunger` 之类的时候必须注意 —— 它和实例字段不是同一套 API。
- **方法调用最贵**：`il2cpp_runtime_invoke(...)`（`CharacterStatusData.cs:353-376`），要跨 native 边界、构造 `void**` 参数数组、处理 boxing 返回值。**能用字段就不用方法，能用一次调用就不用循环调用。**
- 【事实-文件】旧项目把这条原则写进了设计理由：`PerfTweaks.cs:32-35` 注释"纯字段写旋钮（不需要 Harmony）……是**渲染预算**，只影响本地画面"。

### 3.3 静态字段会被游戏重置 → 需要周期性重放

【事实-文件】`PerfTweaks.cs:32-35` 注释原文："游戏可能在场景切换时重置静态值，所以需要周期性重放"。
实现：`FpsOverlay.cs:59-60`

```csharp
// 静态字段可能被游戏在切场景时重置，周期性重放一次（很便宜）
if (fc % 300 == 0) PerfTweaks.ApplyFieldKnobs();
```

即每 300 帧（约 5 秒 @60fps）重放一次字段写。这是一个**通用模式**，新项目写 `CharacterStatusData` 的 static 上限值（`maxHunger` 等）时同样会遇到。

### 3.4 `(UnityEngine.Object)x == null` 判销毁

【事实-文件】`PerfTweaks.cs:86, 102, 118, 147`：

```csharp
if ((UnityEngine.Object)__instance == null) return true;
```

为什么必须显式转型：

- `__instance` 是**托管侧的包装对象**。游戏侧 native 对象被 `Destroy` 之后，托管包装对象仍然存在、仍然不是 `null`。
- 直接 `__instance == null` 或 `ReferenceEquals(__instance, null)` 只比较托管引用 → **漏检**。
- 转成 `UnityEngine.Object` 后，`==` 被 Il2CppInterop 重载为走 native 的 `UnityEngine.Object.op_Equality`，它会检查 native 侧的缓存指针是否已失效 → 正确返回 `true`。
- 漏检的后果不是 `NullReferenceException`，而是更难查的 `InvalidOperationException: Handle is not initialized`（见 §4）。

同理，`Plugin.cs:302` 用 `Instance?.Log.LogInfo(msg)` 而不是 `Instance.Log.LogInfo` —— `Instance` 在插件构造完成前是 `null`。

### 3.5 interop 是 stub，不是原始逻辑

【事实-文件】`research/modding-methods.md:398-416` 的醒目警告：

> `BepInEx\interop\*.dll` 由 Il2CppInterop 生成，是**托管镜像（stub）**：
> - ✅ 有正确的类型名、命名空间、方法名、参数类型、返回值、字段名、字段类型、常量名；
> - ❌ **没有**原始方法体 —— 方法体是 `il2cpp_runtime_invoke(...)` 转发跳板。

**推论（对新项目至关重要）**：可以从 interop **枚举 API 表面**，但**绝不能**据此断言某方法的内部行为、算法复杂度、是否有副作用。要判断"这个方法是不是每帧跑"，只能靠运行期实测或反汇编 `GameAssembly.dll`，不能靠读 stub。

---

## 4. IL2CPP 参数封送问题及其解决方案（本项目最重要的教训）

### 4.1 现象

【事实-文件】`docs/DEVELOPMENT_ROADMAP.md:103-112`（原文引用）：

> 初版把 5 个 Harmony 补丁全部装上，即使优化项关闭也会报：
> ```
> [Error :Il2CppInterop] During invoking native->managed trampoline
> Exception: System.InvalidOperationException: Handle is not initialized.
>    at Il2CppInterop.Runtime.Runtime.ClassInjectorBase.GetMonoObjectFromIl2CppPointer(IntPtr pointer)
>    at (il2cpp -> managed) UpdateShadowEffect(IntPtr , Il2CppMethodInfo* )
> ```

注意这条栈的**形状**：`(il2cpp -> managed) UpdateShadowEffect(IntPtr, Il2CppMethodInfo*)` 是 Il2CppInterop 生成的 trampoline，抛点在 `ClassInjectorBase.GetMonoObjectFromIl2CppPointer`，**没有一帧在 `ZedZone.FpsOptimizer` 命名空间里**。

### 4.2 机理（精确版）

1. 我们在 `Prefix` 里声明了 `BasicCharacterController __instance` 参数。
2. Harmony 的 Hybrid patch 在目标方法每次被调用时，由 trampoline 把 native 的 `IntPtr` 翻译成托管类型 —— 这一步调 `ClassInjectorBase.GetMonoObjectFromIl2CppPointer(pointer)`。
3. 对**已销毁 / 未初始化**的实例，这个 `IntPtr` 不再对应有效的 il2cpp 对象，转换抛 `InvalidOperationException: Handle is not initialized`。
4. **这一步发生在我们的 `Prefix` 方法体执行之前**。所以：
   - `Prefix` 里的 `try/catch` 抓不到 —— 它根本没有被进入；
   - `Prefix` 第一行的 `if (!enabled) return true;` 也拦不住 —— early-return 排在封送之后；
   - 这个异常与"功能开没开"**完全无关**，只要补丁存在、只要那个原生方法有一次在坏对象上被调用，就会抛。

### 4.3 为什么"已安装 + early-return" ≠ "未安装"

这是任务里要求必须讲清的一点。逐项对比：

| | 补丁**未安装** | 补丁**已安装 + 首行 early-return** |
|---|---|---|
| 原生方法入口是否有 wrapper | 无 | **有** |
| 每次进入是否要 `IntPtr → 托管` 封送 | **否** | **是**（无条件） |
| 坏实例上是否抛 `Handle is not initialized` | **否** | **是**（封送阶段就抛，早于我们的代码） |
| 每帧的额外开销 | `0` | 一次 trampoline + 一次封送 + 一次托管调用 |
| 能否被 `try/catch` 兜住 | 不适用 | **不能** |

结论：**early-return 只省下了"方法体执行"，付出的代价（封送 + 抛异常的可能）一点没省。** 默认关闭的功能必须默认**不装补丁**，这不是优化，是正确性。

这就是旧项目三条不可妥协约束里的第 3 条（`DEVELOPMENT_ROADMAP.md:56`："**补丁按需安装**"）。

### 4.4 修复

【事实-文件】`Plugin.cs:88-113`：

```csharp
// 优化项没开就**根本不装补丁** —— 默认配置下游戏行为与开销完全等于原版。
_harmony = new Harmony(PluginGuid);                          // :97
int patched = 0;                                             // :98

if (PerfTweaks.OffscreenCosmeticSkip)                        // :100
{
    patched += TryPatch(typeof(Patch_UpdateShadowEffect), "BasicCharacterController.UpdateShadowEffect");
    patched += TryPatch(typeof(Patch_UpdateWaterReflection), "BasicCharacterController.UpdateWaterReflectionState");
    patched += TryPatch(typeof(Patch_UpdateGrassPress), "BasicCharacterController.UpdateGrassPress");
}
if (PerfTweaks.ParticleIdleFrames > 0)                       // :106
    patched += TryPatch(typeof(Patch_ParticleAutoSleep), "ParticleAutoSleep.OnEnable");
if (PerfTweaks.ChunkSpawnBudgetMax > 0)                      // :108
    patched += TryPatch(typeof(Patch_CanSpawn), "ChunkSpawnFrameBudget.CanSpawn");
```

注意两个细节：

- **门控用的是 `PerfTweaks` 的 static 字段，不是 `ConfigEntry.Value`**。因为 static 字段已经过钳位（`Plugin.cs:80-83` 的 `Math.Max(0, ...)`），且 Preset 覆盖（:77）已经生效 —— 顺序不能反：**先算最终值，再决定装不装。**
- **补丁体内的 early-return 保留了**（`PerfTweaks.cs:83, 99, 135, 166`）。这不是冗余，是**第二道闸**：万一运行期 static 开关被改成 0（未来做 live toggle 时），补丁体立即退化成 no-op，不必等卸载。两道闸各管一件事：安装闸防"封送"，方法体闸防"逻辑"。

修复后指标：`trampoline error = 0`（`DEVELOPMENT_ROADMAP.md:123`）。

### 4.5 教训（直接写进新项目规则）

> 在 IL2CPP 下，**声明 IL2CPP 类型参数本身就是有成本的、且可能在坏对象上失败**。
> 任何"默认关闭"的功能，必须**默认不装补丁**，不能靠 early-return。
> 如果某个补丁必须常驻（例如要检测"玩家进入战斗"事件），那么**参数里不要写 IL2CPP 类型**：能用 `IntPtr` / 无参 / 纯值类型就用，需要实例时在方法体里自己 `Il2CppObjectPool.Get<T>(ptr)` 并判空。

---

## 5. 按需安装与卸载机制

### 5.1 现在有什么

【事实-文件】结构在 `Plugin.Load()` 里（§4.4 已引），形态是：

```
BindConfig() → ApplyPresetIfRequested() → 把 ConfigEntry 拷进 PerfTweaks.static
            → if (开关) TryPatch(type, label)
            → 汇总日志 "harmony patches installed: N"
```

特征：
- **一次性、单向**：只在 `Load()` 里做一次，之后没有任何再评估。
- **粒度 = 一个功能一个门**：`OffscreenCosmeticSkip` 一次装 3 个补丁，`ParticleIdleFrames > 0` 装 1 个，`ChunkSpawnBudgetMax > 0` 装 1 个。
- **失败不阻断**：`TryPatch` 返回 0 只是少一个补丁（§2.2）。

### 5.2 缺什么（真卸载）

【事实-文件】对 `src/` 全目录搜索 `Unpatch|UnpatchAll|Patch\(`：**12 处命中全部是 `PatchAll` 与 `[HarmonyPatch]` 特性，没有任何 `Unpatch`**。

所以真实状况是：**只能"启动时装"，运行期完全无法卸载。** 想关掉某项必须改 cfg → 重启游戏。

要做真卸载，缺 4 件东西：

| 缺什么 | 具体说明 | 新项目该怎么做 |
|---|---|---|
| ① `MethodInfo` 原方法引用 | `PatchAll` 只接受 `Type`，不告诉我们它 patch 了哪个 `MethodBase` | 改用手工 `harmony.Patch(original, prefix, postfix)`，`original` 由 `AccessTools.Method(typeof(X), nameof(X.M))` 取得并**存进字典** |
| ② 卸载调用 | 全仓无 `Unpatch` | `harmony.Unpatch(original, HarmonyPatchType.All, PluginGuid)`；卸载后**必须** `Harmony.GetPatchInfo(original)` 断言为 `null`（否则别的 mod 的补丁或我们漏掉的 wrapper 还在） |
| ③ 变更监听 | 没有订阅 `ConfigEntry.SettingChanged`（全仓无 `SettingChanged`） | 订阅 → `PatchManager.Apply(key, enabled)` → live 装/卸；写进 cfg 的值即时生效 |
| ④ 卸载后的状态一致性 | 卸载只撤销 wrapper，**不会还原已写过的字段** | `PatchManager` 不负责还原；字段还原归各 Module 的 `OnDisable()`（见 §12 第 2 条强调） |

### 5.3 一条更稳的默认策略

即使做了 ①②③④，**"live Unpatch 是否立刻消除封送"仍需实测**：

> 【未确认】Harmony 的 Hybrid patch 在 `Unpatch` 后，trampoline 是否在同一帧就不再生效，还是仍有一次已排队的 dispatcher 残留；以及若补丁已被 JIT 内联（IL2CPP 侧不存在 JIT，但 Harmony 的 reverse-patch 机制在 native 侧有其自己的时机），行为如何。**必须在实机上用"卸载后继续跑 5 分钟看 `trampoline error` 计数是否停增"来验证。**

因此建议双轨：
- **默认轨**：配置变更 → 需要增减补丁 → 写日志 `需要重启生效`，不动运行期。
- **增强轨**：实现真 `Unpatch`，用于"检测到联机 → 立即卸载全部补丁"这种**必须在运行期生效**的场景（联机硬禁用，见 §12）。

---

## 6. 配置文件生成与管理

### 6.1 基本写法

【事实-文件】`Plugin.cs:139-201`（节选，覆盖三种形态）：

```csharp
// 形态 1：只要布尔/简单默认值
ShowHudOnStart = Config.Bind("HUD", "ShowOnStart", true, "启动时显示性能 HUD。");           // :139

// 形态 2：带数值范围校验
HudLeftOffset = Config.Bind("HUD", "LeftOffset", 10,
    new ConfigDescription("HUD 左边距（像素）。", new AcceptableValueRange<int>(0, 2000)));   // :141-142

// 形态 3：枚举式白名单
Preset = Config.Bind("Tweaks", "Preset", "none",
    new ConfigDescription("一键预设。none = ...；balanced = ...；performance = ...",
        new AcceptableValueList<string>("none", "balanced", "performance")));                  // :168-173
```

- **分节（section）就是 cfg 的 `[Section]`**：`"HUD"`、`"Benchmark"`、`"Frame"`、`"Tweaks"` 四节。
- **键名用 PascalCase**（`ShowOnStart` / `ToggleKey` / `TargetFrameRate`），因为用户会手改 cfg。
- `AcceptableValueRange<int>` 用于数值（:142,146,162,166,183,189,195,201），`AcceptableValueList<string>` 用于枚举（:173）。**范围校验保护的是"BepInEx 配置管理器 UI + 序列化"，手改 cfg 仍可越界 → 所以读出来后必须再钳位**（`Plugin.cs:80-83` 的 `Math.Max(0, ...)`）。
- `ConfigEntry<KeyboardShortcut>` 直接可用（:147-152），BepInEx 自带 converter（§8）。

### 6.2 中文 description 的写法标准

旧项目的 description 不是"这叫什么"，而是**"这是什么 + 后果 + 建议值 + 风险"**。这是可直接继承的文案规范：

```csharp
// :180-182
"粒子自动休眠阈值（空闲多少帧后休眠）。0 = 不干预（用游戏原值）。" +
"数值越大越省 CPU，但过大会让残余粒子提前消失。建议 30-60。"

// :187-188
"每帧最多生成多少个『非优先』区块。0 = 不干预。建议 1-3。" +
"风险：地形/建筑会晚几帧出现，跑图时可能看到明显 pop-in。联机下仅影响本机表现。"

// :144-146
"HUD 上边距（像素）。默认 64，用来避开游戏自带的左上角 FPS/CPU/GPU 读数；设为 0 会与它叠字。"
```

另外两条隐含约定：
- **`-1` / `0` 固定表示"不干预"**（`Plugin.cs:158-166`），这样"默认值"永远不会意外等于"功能开启"。新项目应沿用（例如 `LockHunger = -1` 表示不锁，`0..100` 表示锁到该值）。
- **把风险写进配置项本身**，不要只写在 README 里 —— 用户在 cfg 里改值的时候看不到 README。

### 6.3 落盘位置与保存时机

| 项 | 值 | 证据 |
|---|---|---|
| 配置文件 | `<游戏目录>\BepInEx\config\<PluginGuid>.cfg`，即 `zedzone.fpsoptimizer.cfg` | `README.md:58` |
| 自动保存 | BepInEx 在正常退出时保存 | 【推断】BepInEx 标准行为；旧项目只在 Preset 时显式存 |
| 显式保存 | `Config.Save()` 只在 Preset 写回时调用一次 | `Plugin.cs:213` |
| 相对路径基准 | `Paths.GameRootPath`（如基准输出目录） | `Plugin.cs:274` |

### 6.4 Preset 模式（写回文件）

【事实-文件】`Plugin.cs:204-215`：

```csharp
private void ApplyPresetIfRequested()
{
    string p = (Preset.Value ?? "none").Trim().ToLowerInvariant();
    if (p == "none") return;

    OffscreenCosmeticSkip.Value = true;                              // :209
    ParticleIdleFrames.Value = 30;                                   // :210
    ChunkSpawnBudgetMax.Value = (p == "performance") ? 2 : 0;        // :211

    try { Config.Save(); } catch { /* 写不进就算了，内存里已生效 */ }  // :213
    LogInfo($"preset '{p}' applied to config.");                     // :214
}
```

模式要点（可复用，但要理解其语义）：

- **直接给别的 `ConfigEntry.Value` 赋值**，不碰内部字段 —— 这样 BepInEx 的事件、脏标记都正常。
- **`Config.Save()` 在 try/catch 里**：文件被占用/只读时不能崩，内存里已生效就够了。
- **语义是"覆盖"而非"增量"**：cfg description 明写"设为非 none 会**覆盖**下面的单项目配置并写回配置文件"（:172）。**副作用**：用户手改 `ParticleIdleFrames = 60` 但 Preset 仍写 `balanced`，下次启动又会被打回 30。所以 Preset 只适合"一次性引导"，最终用户应该被引导回 `none`。
- 这条**不必然适用新项目**：生存辅助的"预设"更可能是"安全/平衡/激进"三档数值倍率，语义不同，但"写回 + 幂等覆盖"的机制可以照搬。

---

## 7. HUD 绘制方式

### 7.1 为什么选 OnGUI

【事实-文件】`FpsOverlay.cs:9-12` 注释原文：

> 为什么用 OnGUI 而不是改游戏 UI：
> - 不依赖游戏的 UI 框架，不会因为游戏 UI 改版而崩；
> - 只在开启时绘制，关闭后零开销（OnGUI 本身仍有每帧调用成本，所以默认关闭）；
> - 属于"纯叠加层"，不修改任何游戏状态。

补充【推断】理由：改游戏的 UGUI 需要摸 `Canvas` / `Text` / `RectTransform` 组件，在 IL2CPP 下每次都是跨边界调用；而 `OnGUI` 是 `UnityEngine.IMGUIModule` 里的托管 API，**只在我们的组件里跑，不碰游戏 UI 树** —— 风险面小一个数量级。

### 7.2 骨架（可直接照搬）

```csharp
private void OnGUI()                                        // :135
{
    if (!Visible || Stats == null) return;                  // :137 早退
    try
    {
        EnsureStyles();                                      // :141 懒建
        if (Screen.width != _lastScreenW || Screen.height != _lastScreenH)
            { _box = null; EnsureStyles(); }                 // :144 分辨率变了重建
        ...
        GUI.Box(rect, GUIContent.none, _box);                // :158
        GUI.Label(new Rect(...), $"{avgFps,6:F1} FPS", _label);  // :173
    }
    catch (Exception e) { Plugin.LogErrorOnce("overlay gui", e); Visible = false; }  // :227-231
}
```

### 7.3 懒建 GUIStyle + 1×1 Texture2D 背景

【事实-文件】`FpsOverlay.cs:109-133`：

```csharp
private void EnsureStyles()
{
    if (_box != null) return;                                  // :111 已经建好就直接返回

    _bg = new Texture2D(1, 1);                                 // :113
    _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));          // :114 半透明黑
    _bg.Apply();                                               // :115 必须有，否则纹理是空的

    _box = new GUIStyle(GUI.skin.box);                         // :117 继承默认皮肤
    _box.normal.background = _bg;                              // :118 换成纯色

    _label = new GUIStyle(GUI.skin.label)                      // :120
    {
        fontSize = Math.Max(12, Mathf.RoundToInt(Screen.height / 80f)),  // :122 字号跟随分辨率
        richText = false,
        alignment = TextAnchor.UpperLeft
    };
    _label.normal.textColor = Color.white;                      // :126

    _title = new GUIStyle(_label) { fontStyle = FontStyle.Bold };   // :128 用 label 派生，少建一个
    _title.normal.textColor = new Color(0.6f, 1f, 0.6f);        // :129

    _lastScreenW = Screen.width; _lastScreenH = Screen.height;
}
```

为什么必须懒建 + 缓存：
- `new GUIStyle(...)` 每次都会分配托管对象并触碰 native 皮肤，**每帧 new 会造成 GC 抖动**（在 Unity 里表现为周期性的掉帧尖峰 —— 一个性能模组最不该做的事）。
- `Texture2D` 更是 native 资源，每帧 new 会泄漏。
- **1×1 纯色纹理**是最便宜的"可控背景"方案：`GUI.backgroundColor` 会影响整组控件，而 `_box.normal.background` 只影响这一个 Box。

【已知小瑕疵，可改进】`:144` 在分辨率变化时只做 `_box = null; EnsureStyles();`，**旧的 `_bg` Texture2D 没有被 `Destroy`** → 每次改分辨率泄漏一个 1×1 纹理。量极小，但新项目应该写成 `if (_bg != null) Destroy(_bg);`。

### 7.4 成本警告（必须继承的纪律）

- `OnGUI` **每帧都会被调用**（还分 `Layout` / `Repaint` 两趟），所以 `:137` 的早退是必需的，但它只能省下布局与绘制 —— **函数调用本身仍在**。
- 想要真正的零成本，正确做法是 **把组件 `enabled = false`**（Unity 对 disabled 的 MonoBehaviour 不派发 `OnGUI`/`Update`）。旧项目没做到这一层（只用 `Visible` 门控）——【事实-文件】`:137` 是 `if (!Visible) return;`，属于"低成本但非零成本"。
- **新项目应采用**：`Visible` 只控制"要不要画"，同时用 `enabled` 控制"要不要被调用"。UI 关闭时 `overlay.enabled = false;`。
- 另外 `OnGUI` 一帧可能调用多次，**绝不能在 `OnGUI` 里做状态变更**（会和 `Update` 里的热键打架，见 §8）。

### 7.5 颜色与可读性技巧

【事实-文件】`FpsOverlay.cs:167-174`：按阈值换色时**临时改 style 的 textColor 再改回**，而不是为每种颜色建一个 style：

```csharp
var oldColor = _label.normal.textColor;
_label.normal.textColor = fpsColor;
GUI.Label(...);
_label.normal.textColor = oldColor;
```

省掉 3 个 GUIStyle 的构建，代价是两次字段写。

---

## 8. F8 / F9 / F10 快捷键处理方式

### 8.1 配置绑定

【事实-文件】`Plugin.cs:147-152`：

```csharp
ToggleHudKey   = Config.Bind("HUD", "ToggleKey", new KeyboardShortcut(KeyCode.F8),  "显示/隐藏 HUD 的快捷键。");
ToggleDetailKey= Config.Bind("HUD", "DetailKey", new KeyboardShortcut(KeyCode.F10), "切换详细信息的快捷键。");
BenchmarkKey   = Config.Bind("Benchmark", "ToggleKey", new KeyboardShortcut(KeyCode.F9), "开始/结束基准测试的快捷键。");
```

类型是 `ConfigEntry<KeyboardShortcut>`，命名空间必须是 **`BepInEx.Unity.IL2CPP.Configuration`**（`Plugin.cs:7`）—— 不是 `BepInEx.Configuration`。引用错命名空间会编译失败，这是第一次接 BepInEx IL2CPP 最常踩的编译坑。

### 8.2 检测

【事实-文件】`FpsOverlay.cs:83-100`：

```csharp
private void HandleHotkeys()
{
    try
    {
        if (Plugin.ToggleHudKey.Value.IsDown())  { Visible = !Visible;  Plugin.LogInfo($"HUD {(Visible ? "shown" : "hidden")}"); }
        if (Plugin.ToggleDetailKey.Value.IsDown()) { ShowDetail = !ShowDetail; }
        if (Plugin.BenchmarkKey.Value.IsDown())  { Plugin.ToggleBenchmark(); }
    }
    catch
    {
        // 输入系统在 IL2CPP 下偶尔会抛，禁用热键但不影响 HUD
        _hotkeysEnabled = false;                          // :104 一次异常，永久关热键
        Plugin.LogWarn("hotkeys disabled (input exception)");  // :105
    }
}
```

调用点在 `Update()`（`:46`），**不在 `OnGUI()`**。

### 8.3 为什么不用 `UnityEngine.Input.GetKeyDown`

4 条理由，按重要性排：

1. **配置化**：`KeyboardShortcut` 能直接序列化进 cfg，用户可改成任意键；`GetKeyDown(KeyCode.F8)` 是硬编码的。旧项目 README 把可配置快捷键当成产品特性（`README.md:33-37`）。
2. **组合键/修饰键**：`KeyboardShortcut` 支持 `Control+F8` 这类组合，`GetKeyDown` 要自己拼一堆 `Input.GetKey(KeyCode.LeftControl) && ...`。
3. **模块/命名空间隔离**：`UnityEngine.Input.GetKeyDown` 依赖 `UnityEngine.InputLegacyModule`（csproj:55-58 专门引了它）。若未来游戏切到新 Input System，Legacy 路径整个失效；BepInEx 的 `IsDown` 是**集中适配点**，将来只改 BepInEx，不改我们。
4. **异常已被 BepInEx 处理过一层**：`IsDown()` 内部已做 IL2CPP 适配；`GetKeyDown` 在 IL2CPP 下偶发 `NullReferenceException`（【推断】，依据是旧项目仍要加 try/catch）。

### 8.4 那个 try/catch 的语义

- **一次异常就永久禁用热键**，而不是重试。理由：输入异常通常在 IL2CPP 下是**确定性的**（同一帧状态下每帧都抛），重试等于每帧刷一条日志。
- **禁用热键不影响 HUD** —— 这是故意的降级：HUD 还能看，只是不能按键切换（用户还可以点 HUD 上的按钮，`:221-225` 提供了 `Benchmark` / `Hide` 两个按钮作为热键的后备）。
- `_hotkeysEnabled` 可以从外部设（`:34 public void SetHotkeysEnabled(bool v)`）—— 为"打开输入框时临时禁用"预留。

### 8.5 为什么必须在 `Update` 里检测

`OnGUI` 一帧会被调用多次（`Layout` + `Repaint`，多控件时更多）。在 `OnGUI` 里判 `IsDown` 会导致**一次按键触发多次 toggle** → 按一下 F8 闪两下回到原状（用户以为坏了）。这条在旧项目里是用"放在 `Update`"隐式规避的，新项目要显式写进规范。

---

## 9. 中文配置项与日志系统

### 9.1 中文配置项

见 §6.2。这里只强调一条**格式约定**：`ConfigDescription` 一律中文、全角标点、句尾带句号；键名与默认值一律英文/数字。这样生成的 cfg 文件是"英文键 + 中文注释"，既能手改又读得懂。

### 9.2 日志包装层

【事实-文件】`Plugin.cs:297-314`：

```csharp
internal static void LogInfo(string msg)  { try { Instance?.Log.LogInfo(msg); }  catch { } }   // :300-303
internal static void LogWarn(string msg)  { try { Instance?.Log.LogWarning(msg); } catch { } } // :305-308

internal static void LogErrorOnce(string where, Exception e)                                   // :310-314
{
    if (!_loggedOnce.Add(where + e.GetType().Name)) return;   // :312 已经报过就 silently return
    try { Instance?.Log.LogError($"[{where}] {e.GetType().Name}: {e.Message}"); } catch { }
}
```

三个设计决策：

1. **`try { } catch { }` 包住日志本身**。理由：日志是诊断手段，**不能成为崩溃源**。插件卸载/关闭过程中 `Log` 可能已失效，这时抛异常会让游戏在退出时崩 —— 用户会以为是存档损坏。
2. **`Instance?.` 空条件**。`Instance` 在 `Load()` 第一行之前是 `null`（静态字段初始化期），此时任何日志都是最后一行防线。
3. **`_loggedOnce` 的 key 是 `where + e.GetType().Name`**（:312），**刻意不含 `e.Message`**。因为 `Message` 可能带会变的内容（对象名、坐标、ID），一旦带进去去重就永久失效。用"位置 + 异常类型"做 key，语义正好是"同一种错只报一次"。

### 9.3 `LogErrorOnce` / `NoteFailure` 双轨去重

旧项目有**两个**去重机制，服务于不同场景，都要继承：

| 机制 | 位置 | key | 用途 |
|---|---|---|---|
| `LogErrorOnce(where, ex)` | `Plugin.cs:310` | `where + 异常类型` | 兜底：UI 崩溃、Update 崩溃等"不该发生"的异常 |
| `NoteFailure(patch, ex)` | `PerfTweaks.cs:61-66` | `patch 名` + 计数 | 补丁体内部异常：**每帧路径**，必须只报第一次 |

`NoteFailure` 还多做了"计数"（`PatchFailures[patch]++`），日志里能看出"抛了 1 次"还是"抛了 1000 次"。

### 9.4 启动自述日志 = 验收单

【事实-文件】`Plugin.cs:73-74, 111-113, 126, 133-134`，最终形成 `README.md:41-50` 里让用户逐行核对的清单：

```
[Info :ZED ZONE FPS Optimizer] loading ZED ZONE FPS Optimizer v1.0.0
[Info :ZED ZONE FPS Optimizer] detected game version: 1.0.14
[Info :ZED ZONE FPS Optimizer] harmony patches installed: 0 (no tweaks enabled — pure vanilla behaviour)
[Info :ZED ZONE FPS Optimizer] HUD component injected
```

**这是本项目最值得抄的一条工程习惯**：让"启动日志"直接成为验收依据 —— 用户不需要理解代码，只要比对这 4 行就知道装没装上、装了几个补丁、有没有踩到默认态。新项目的启动日志应等价地输出：版本、游戏版本、**生效的功能模块列表**、**已安装补丁数**、**是否单人（联机禁用状态）**、UI 是否注入。

---

## 10. 编译、安装、测试与错误处理流程

### 10.1 csproj 引用布局

【事实-文件】`src/ZedFpsOptimizer/ZedFpsOptimizer.csproj`：

```xml
<GameRoot Condition="'$(GameRoot)' == ''"><游戏目录>\</GameRoot>  <!-- :17 -->
<BepInExCore>$(GameRoot)BepInEx\core\</BepInExCore>      <!-- :18 -->
<InteropDir>$(GameRoot)BepInEx\interop\</InteropDir>     <!-- :19 -->
```

两个引用根，职责严格分开：

| 根 | 里的 DLL | 例子 |
|---|---|---|
| `BepInEx\core\` | BepInEx 与第三方框架本体 | `BepInEx.Core.dll`、`BepInEx.Unity.IL2CPP.dll`、`Il2CppInterop.Runtime.dll`、`0Harmony.dll`（:23-38） |
| `BepInEx\interop\` | Il2CppInterop 从游戏生成的 stub | `Il2Cppmscorlib.dll`、`Assembly-CSharp.dll`、`UnityEngine.*Module.dll`（:39-66） |

**每一个 `<Reference>` 都必须 `<Private>false</Private>`**：

- 含义：不要把这 11 个 DLL 复制进输出目录。
- 理由：这些 DLL 运行时由游戏侧的 BepInEx **已经提供**。复制一份进 `Mods\` 会造成**版本分叉** —— 游戏更新后 `BepInEx\interop\` 会重新生成，而 `Mods\<Name>\` 下的旧副本会被优先加载 → 同名类型存在两份不同程序集 → `InvalidCastException` / `MissingMethodException`，且报错栈完全看不出根因。
- 唯一的例外是**我们自己写的辅助 DLL**（旧项目没有，单 DLL 就够）。

其它 PropertyGroup 设置与理由（csproj:3-20）：

| 设置 | 值 | 理由 |
|---|---|---|
| `TargetFramework` | `net6.0` | 对齐 BepInEx 6 的 .NET 6 运行时；写成 net8.0 能编译但加载会失败【推断】 |
| `AppendTargetFrameworkToOutputPath` | `false` | 产物直接在 `bin\`，`build.ps1:42` 的路径才能对上 |
| `GenerateDependencyFile` | `false` | 不生成 `.deps.json`，BepInEx 不需要 |
| `AllowUnsafeBlocks` | `true` | interop 部分类型的签名含 `unsafe`/指针 |
| `NoWarn` | `CS0436;CS1701;CS1702` | interop 程序集常见的"类型冲突/版本统一"警告，噪音必须压掉，否则真正的警告被淹 |
| `OutputPath` | `bin\` | 与 `build.ps1` 硬编码的产物路径配套 —— **改 csproj 必须同步改 build.ps1** |

**引用的 UnityEngine 模块要按需最小集**（:47-66）：`CoreModule`、`IMGUIModule`（OnGUI）、`InputLegacyModule`（KeyboardShortcut 底层）、`TextRenderingModule`（GUI 字体）、`UIModule`。
⚠️ **少引一个模块的后果不是编译错误，而是运行期 `TypeLoadException` / `MissingMethodException`** —— 因为 interop 是 stub，编译期看不出来。新项目如果用到 `UnityEngine.Physics` / `Animation`，**必须显式加引用**。

### 10.2 构建环境

【事实-文件】`docs/DEVELOPMENT_ROADMAP.md:147-165`、`tools/build.ps1:34-36`：

- SDK 在 **`<dotnet>`**（8.0.428），不是系统 `C:\Program Files\dotnet`。
- 必须设 **`$env:DOTNET_ROOT = "<dotnet>"`**，否则 dotnet host 找不到运行时。
- 还要设 `DOTNET_CLI_HOME`（`<dotnet>-home`）与 `DOTNET_CLI_TELEMETRY_OPTOUT=1`（:35-36）。
- 反编译工具 `ilspycmd 8.2.0.7535` 在 `<tools>`。

### 10.3 `tools/build.ps1` 流程

【事实-文件】`tools/build.ps1` 全文 56 行，流程：

1. 参数默认值（:9-13）：`-GameDir`、`-DotnetDir`、`-SkipInstall`。
2. 从脚本位置反推仓库根与 csproj 路径（:17-20），找不到就 `throw`。
3. 解析 dotnet（:22-26）：先看 `$DotnetDir\dotnet.exe`，找不到**回退 PATH 并 warning**（不直接失败）。
4. **游戏在跑就拒绝安装**（:29-32）：
   ```powershell
   if ((Get-Process ZEDZONE -ErrorAction SilentlyContinue) -and -not $SkipInstall) {
       Write-Host "ZED ZONE is running - close the game first (the DLL is locked)." -ForegroundColor Red
       exit 1
   }
   ```
   理由：DLL 被进程占用时 `Copy-Item` 会失败，而失败信息是 `UnauthorizedAccessException`，用户看不懂 → **提前拒，给出人能懂的句子**。
5. 设环境变量（:34-36）→ `dotnet build -c Release -v q -nologo "-p:GameRoot=$GameDir\"`（:39）。注意 `-p:GameRoot` 覆盖 csproj 的默认值，并且**末尾必须带反斜杠**（csproj 里是 `$(GameRoot)BepInEx\core\` 直接字符串拼接）。
6. 检查 `$LASTEXITCODE`（:40）→ 检查产物存在（:42-43）→ `-SkipInstall` 时到此结束（:45-48）。
7. 建 `Mods\ZedFpsOptimizer\` 并 `Copy-Item -Force`（:50-53）。
8. 打印**下一步该做什么**（:55-56）：Steam URL + 日志路径。这一行看似多余，实际是"构建脚本的可用性"—— 用户不需要回翻文档。

### 10.4 离线自测（纯 C# 逻辑）

【事实-文件】`tests/ZedFpsSelfTest/ZedFpsSelfTest.csproj:10-13`：

```xml
<ItemGroup>
  <!-- 直接复用模组的两个纯 C# 文件（它们不依赖 UnityEngine，因此可离线验证） -->
  <Compile Include="..\..\src\ZedFpsOptimizer\FrameStats.cs" Link="FrameStats.cs" />
  <Compile Include="..\..\src\ZedFpsOptimizer\BenchmarkRecorder.cs" Link="BenchmarkRecorder.cs" />
</ItemGroup>
```

**关键设计：用 `<Compile Include>` 直接编译被测源文件，而不是引用编译出的 DLL。** 好处：自测与被测代码永远是同一份，不存在"测的是旧 DLL"这种假通过。代价：被测文件必须**不依赖 UnityEngine**（`FrameStats.cs` 只 `using System; / System.Collections.Generic;`，`BenchmarkRecorder.cs` 只 `System.*`）。

断言框架是**手写的**（`Program.cs:9-13`）：

```csharp
private static void Check(string name, bool ok, string detail = "")
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  -> " + detail : "")}");
    if (!ok) _failures++;
}
// Main 返回 _failures == 0 ? 0 : 1   (:27) → 可直接接 CI
```

覆盖范围（`Program.cs:19-23`，共 19 项）：恒定 10ms → 100fps、1% low 数学、非法输入（`0`/负数/`NaN`/`Infinity`）、超大值钳位到 2000ms、p50/p99、CSV/summary 格式。

**分层理由**（`DEVELOPMENT_ROADMAP.md:141`）：

> `FrameStats` / `BenchmarkRecorder` 刻意**不依赖** `UnityEngine`，因此可以在没有游戏的情况下用 `dotnet run` 验证数学与文件输出 —— 这是本项目唯一能做到"自动化验证"的部分，必须利用。

→ **新项目的对应物**：把所有"数值策略 / 状态机 / 开关判定 / 配置迁移 / 联机判定输入"抽成不依赖 UnityEngine 的纯类，放进同一套自测。

### 10.5 实机验证流程（实际怎么做的）

【事实-文件】`DEVELOPMENT_ROADMAP.md:72-83` + `README.md:162-174`，六步：

| 步 | 动作 | 判定 |
|---|---|---|
| 1 | `steam://rungameid/1211600` 启动 | 必须走 Steam，直接跑 exe 秒退（:154） |
| 2 | 读 `<游戏目录>\BepInEx\LogOutput.log` | 看到 §9.4 的 4 行 |
| 3 | 截图确认 HUD 真的画出来了 | 显示 `170.9 FPS / 1% low 95.2 / 5.6 ms` |
| 4 | **与游戏自带读数交叉验证** | 去重后 `170.9 vs 179` 一致；去重前 `176 vs 87` 是 2 倍错误（§11 坑 1） |
| 5 | 数 `trampoline error` / `UpdateShadowEffect error` | `= 0`（§11 坑 2） |
| 6 | 跑离线自测 | `19 项全通过` |

另外还有一条**辅助证据**：`BepInEx\ZedModsLoader-report.txt`（加载器自己写的报告）能证明 DLL 被捡起来了，比 `LogOutput.log` 更早、更明确（§1.4）。

### 10.6 诚实边界（必须继承的写法）

【事实-文件】`README.md:172-174`：

> 诚实的边界：**"能加载、能测量、不报错"已证明；"能提升多少 FPS"未证明。**
> 后者依赖你的硬件与场景，必须实测。

新项目同样要有这一节：**"能加载、不崩、配置生效"是一类结论；"真的改善了生存体验"是另一类结论，后者必须由用户实测。** 不要把前者写成后者。

---

## 11. 两个必须避免的坑（机制 + 修复）

### 坑 1：`Update()` 一帧内被多次派发 → FPS / 数值翻倍

**现象**【事实-文件】`DEVELOPMENT_ROADMAP.md:87-91`：HUD 显示 176 FPS，游戏自带 87 FPS，恰好 2 倍。

**机制**：这款游戏（Unity 2023.1.18f1）在**一帧内会多次派发 `Update()`**。如果每次收到 `Update` 就 `1 / Time.unscaledDeltaTime`，同一帧的帧时间被切成两半，`1/dt` 变成两倍。

**修复**【事实-文件】`FpsOverlay.cs:51-57`：

```csharp
int fc = Time.frameCount;
if (fc == _lastFrameCount)
{
    _updatePassesPerFrame++;    // 顺手可以观测"这游戏一帧到底派发几次"
    return;
}
_lastFrameCount = fc;
```

**教训**【事实-文件】`docs/DEVELOPMENT_ROADMAP.md:101`："在别人的 Unity 项目里做 HUD，**永远不要假设 `Update()` 每帧只调用一次**。"

**对新项目的直接影响（重要）**：生存辅助的所有"周期性写值"都走 tick。如果 tick 挂在 `Update()` 上，**同一个逻辑帧会写两次** —— 对"锁到 100"这种幂等写无害，但对"每 tick 扣 0.5 饥饿"这种**增量**写，就是速度翻倍。所以：
- 任何 `Tick()` 都必须 `Time.frameCount` 去重；
- 更稳的做法是**让所有写值都幂等**（"设为 X"而不是"加 X"），这样即使去重漏了也不会错。

### 坑 2：IL2CPP 已销毁对象的封送异常

**现象**【事实-文件】`DEVELOPMENT_ROADMAP.md:107-112`：

```
[Error :Il2CppInterop] During invoking native->managed trampoline
Exception: System.InvalidOperationException: Handle is not initialized.
   at Il2CppInterop.Runtime.Runtime.ClassInjectorBase.GetMonoObjectFromIl2CppPointer(IntPtr pointer)
```

**机制**：Prefix 声明了 IL2CPP 类型参数（`BasicCharacterController __instance`）→ 每次进原生方法都要 `IntPtr → 托管代理` 封送 → 已销毁实例的 `IntPtr` 无效 → 在 `GetMonoObjectFromIl2CppPointer` 抛异常，**早于我们的代码执行**（详见 §4.2）。

**修复**：`PatchManager` 里**按需安装**补丁；默认关闭的功能默认不装（`Plugin.cs:100-109`），并保留补丁体首行 early-return 作第二道闸（`PerfTweaks.cs:83`）。

**教训**【事实-文件】`docs/DEVELOPMENT_ROADMAP.md:125`："IL2CPP 下'装了补丁但内部 early-return'不等于'没有开销'，**参数封送本身就会失败**。默认关闭的功能必须默认不装补丁。"

**对新项目的直接影响**：这条对新项目的杀伤力**比对旧项目更大**。因为生存辅助必然要碰 `BasicCharacterController` / `CharacterData` / `CharacterStatusData` —— 全是 IL2CPP 类型。所以：
- 优先用**字段写**（不装补丁，零封送风险）；
- 必须拦截时，**Prefix 参数尽量避免 IL2CPP 类型**；实在要用，就必须接受"该功能一开就不能在有坏实例的场景里跑"，并配 `(UnityEngine.Object)x == null` 判空（`PerfTweaks.cs:86`）；
- **绝不允许**"为了以后可能用到"而常驻安装补丁。

---

## 12. 新项目架构建议（生存辅助，与性能监控本质不同）

### 12.0 为什么不能沿用旧结构

旧项目的"功能"是**被动观测 + 全局节流**：不关心玩家是谁、在哪个存档、是不是联机，只关心"这一帧画了什么"。所以它是一个 `Plugin` + 一堆 static 补丁的**扁结构**，合理。

生存辅助是**主动写玩家状态**，特性完全不同：

| 维度 | 旧项目（perf） | 新项目（survival assistant） |
|---|---|---|
| 作用对象 | 全局（所有角色/区块/粒子） | **单一玩家实例**（必须定位到 `BasicCharacterController`） |
| 出错后果 | 掉帧、pop-in，**可逆、可忽略** | **改坏数值、坏存档、判定作弊** |
| 关闭语义 | 停止 patch，无事发生 | 必须**停止写**，且不能写"默认值" |
| 联机 | 本地画面，理论安全 | **直接违规**，必须硬禁用 |
| 生命周期 | 进程级 | **存档/场景级**（换档、读档、死亡都要重置状态） |
| 主要手段 | Harmony 为主 | **字段写为主，Harmony 为辅** |

结论：必须模块化。下面是建议的 11 个模块。

### 12.1 模块总表

| 模块 | 职责 | 生命周期 | 注册/依赖方式 | **必须不做** |
|---|---|---|---|---|
| **`Plugin`** | 唯一 `[BepInPlugin]`；`Load()` 里只做装配：建 Logger → 建 Config → 建各 Module → 交给 `PatchManager` 装补丁 → 注入 `UI` | 进程（`BasePlugin`） | 被谁都不依赖；自己持有所有 Module 引用 | ❌ 不写玩家数值、❌ 不直接 `Harmony.Patch`、❌ 不画 GUI、❌ 不含业务判断 |
| **`Config`** | 所有 `Config.Bind` 集中一处；对外只暴露不可变快照 `ConfigSnapshot`；订阅 `SettingChanged` 并转发给 `PatchManager`/各 Module | 进程；只读快照每帧可取 | 构造时被 `Plugin` 建；其他模块通过构造参数拿 `ConfigSnapshot` | ❌ 不在 `Bind` 时执行业务副作用；❌ 其他模块不许各自 `Config.Bind`（分散后无法产出一致的 cfg 文档） |
| **`Logger`** | 包装 `ManualLogSource`：`Info/Warn/Error/ErrorOnce/Verbose`；`ErrorOnce` 按 `where+异常类型` 去重；`Verbose` 受 `[Debug] Verbose` 控制 | 进程 | `Plugin` 建后注入所有模块 | ❌ 每帧路径不许 `Info`；❌ 不用 `UnityEngine.Debug.Log`（不进 BepInEx 日志文件）；❌ 不许 `throw` |
| **`PlayerModule`** | 定位并缓存"当前玩家" `BasicCharacterController`；提供 `TryGetPlayer(out bcc)` / `IsReady` / `IsSinglePlayer` / `IsSessionHost`；换档、死亡、切场景时失效重置 | 随存档/场景；`OnSceneChanged` / 惰性重查 | 被 Health/Hunger/Thirst/Stamina/Status/UI 依赖（构造注入） | ❌ 不缓存已销毁对象（必须每次用 `(UnityEngine.Object)x` 判空）；❌ 不在 `Update` 里 `FindObjectsOfType`；❌ 不写任何数值 |
| **`HealthModule`** | `hp` / `bloodVolume` 的读取与策略写回（锁定、下限保护、缓降） | 无状态服务 | 实现 `IStatModule`，由 `StatusModule` 调度 | ❌ 不在关闭时"补满血"；❌ 不通过 Harmony 写值 |
| **`HungerModule`** | `hunger` / `maxHunger` 同上 | 无状态服务 | 同上 | 同上 |
| **`ThirstModule`** | `thirst` / `maxThirst` 同上 | 无状态服务 | 同上 | 同上 |
| **`StaminaModule`** | `energy` / `fatigue` / `casEnergy` 同上 | 无状态服务 | 同上 | 同上 |
| **`StatusModule`** | 协调者：读 `StatusSnapshot`（血/饿/渴/体力/体温/辐射）→ 决定"本 tick 谁需要干预"→ 调各 `IStatModule`；管理暂停/恢复；对外只暴露只读快照 | 无状态服务；`Tick()` 由 `UI` 或 `PlayerModule` 的帧回调驱动 | 持有全部 `IStatModule` + `ConfigSnapshot` | ❌ **自己不直接写字段**（必须经各 Module 的公开方法，否则无法实现"关闭即停止"）；❌ 不做 Harmony 拦截 |
| **`UI`** | OnGUI 叠加层：只读显示 `StatusSnapshot`、功能开关、热键、状态提示（"联机已禁用"）；`enabled=false` 实现真零成本 | `AddComponent` + `DontDestroyOnLoad`（`FpsOverlay.cs:36-40`） | 由 `Plugin` 注入 `StatusModule`（只读）+ `Config` + 回调 | ❌ **不写游戏状态**（UI 只读）；❌ 不在 `Update`/`OnGUI` 里做数值计算；❌ 不在 `OnGUI` 里调游戏方法（一帧多次）；❌ 不在 `OnGUI` 里改开关状态 |
| **`PatchManager`** | 唯一持有 `Harmony` 实例；`Install(key)` / `Uninstall(key)` / `IsInstalled(key)` / `UninstallAll()`；保存每个补丁的 `MethodInfo original`；失败只 `Warn` 一次并标记 `Degraded` | 进程 | 由 `Plugin` 建；`Config` 变更时被调 | ❌ 不用 `PatchAll()`（无法逐项卸载）；❌ 不在 Prefix 里声明 IL2CPP 类型参数（除非该功能确定开启）；❌ 不做业务逻辑 |

接口最小化建议：

```csharp
interface IStatModule                      // Health/Hunger/Thirst/Stamina 统一形态
{
    string Name { get; }
    bool   AnyFeatureOn { get; }           // 关闭判定：false → StatusModule 不再调 Tick
    void   Tick(PlayerContext ctx);         // 幂等写；不累加
    void   RestoreOnDisable(PlayerContext ctx);  // 只还原"我们改过的全局/static 值"
}
```

### 12.2 四条硬性设计原则

#### ① 状态写入 vs 事件拦截 分离

**两件事必须在代码结构上分开，不能混在一个补丁里。**

| | 状态写入（write-through） | 事件拦截（intercept） |
|---|---|---|
| 手段 | 在我们自己的 tick 里 `status.hunger = x` | Harmony Prefix/Postfix |
| 成本 | 一次指针加法（§3.2） | 每帧封送 + 可能抛异常（§4） |
| 可停性 | 立即（下次 tick 不写就行） | 需要 `Unpatch`（§5.2，旧项目没有） |
| 适用 | "我想让 hunger 保持在 100" | "游戏要扣 hunger，我想阻止这次扣减" |
| 风险 | 低（幂等、可观测） | **高**（§4） |

**默认一律先做写入。** 只有这三种情况才用拦截：
1. 必须在**动作发生的那一帧**阻止（例如"阻止某次致命伤害"），事后修正会因为游戏已经触发死亡/掉血动画而失效；
2. 需要知道**事件本身**（"玩家被咬了"），纯轮询看不出原因；
3. 需要一个**游戏方法本来会返回的错误结果**（如让某个 `CanXxx()` 返回 `true`）。

**绝不允许**在一个补丁里既拦截又写值 —— 一旦出问题，无法判断根因是封送、是拦截逻辑、还是写值越界。旧项目已经把这条做对了：`PerfTweaks.cs` 里补丁只做"是否执行"和"写阈值字段"，业务性写值全在 `ApplyFieldKnobs()`（无 Harmony，:36-59）。新项目要显式保持这个分层。

#### ② 关闭功能不得重置玩家数值

这是与 perf mod 最大的语义差异，也是新项目**最容易写出"毁存档"bug 的地方**。

- perf mod 关闭 = 不装补丁 = **什么都不发生**，天然安全。
- 生存辅助关闭 = 我们之前**一直在写** `hunger = 100`。关闭时"写什么"变成一个必须明确回答的问题。

**错误做法**（会造成数值跳变或疑似作弊痕迹）：
```csharp
// ❌ 关闭时写"默认满值"
status.hunger = status.maxHunger;
// ❌ 关闭时写"我们记下的初始值"（游戏自己这几分钟一直在正常扣，写回去等于把玩家冻结的时间点复活）
status.hunger = _initialHunger;
```

**正确做法**：

1. **关闭 = 什么都不写。** 下一个 tick 不再赋值，游戏自己的 `UpdateStatus()`（新项目已确认存在，`research/decompiled/BasicCharacterController.cs:12817`）会从**当前值**继续推进。当前值就是我们最后一次写的值 —— 这在语义上是"玩家在关闭前的最后一刻满饥饿"，完全合理。
2. **唯一需要主动还原的是"我们改过的全局/static 值"**。例如如果我们把 `CharacterStatusData.maxHunger`（static，`research/decompiled/CharacterStatusData.cs:156-167`）从 100 改成 200，关闭时必须写回 100。→ 所以每个 Module 在**第一次写 static 值之前**必须保存 `_originalMaxHunger`，并在 `RestoreOnDisable()` 里还原。
3. **绝不在 `Load()` 里为了让"默认状态干净"而写满值。** 旧项目能"默认零行为改动"是因为它默认不装补丁（`README.md:50`）；新项目对应的是：默认 `AnyFeatureOn == false` → 一个字段都不写。启动日志必须能证明这一点（§9.4）。
4. **死亡/读档时必须重置"是否已写过"的状态**。`PlayerModule` 缓存失效后，`IStatModule` 的 `_original*` 快照也必须失效（新档的 static 值可能已被游戏重置）—— 否则会拿旧档的原值覆盖新档。

#### ③ 联机硬禁用

三条【事实-文件】依据：
- 代码模组**不参与联机同步**（`research/modding-methods.md:457`：README 原文"联机时别人没有同样的 DLL"）。
- 改数值、刷物品在带 Mods 的会话或排位里**容易被判异常**（`research/modding-methods.md:341` 引加载器 README 原文）。
- 旧项目自己的 README 也承认联机场景特殊（`README.md:192-194`），只是它的改动是纯本地画面所以风险低 —— **新项目的改动不是本地画面**。

实现要求：
- `Config` 里有 `[Safety] AllowMultiplayer = false`，且**不提供** UI 上的便捷绕过（要绕过必须手改 cfg，让"故意"这一步足够重）。
- `PlayerModule.IsSinglePlayer` 的判据 —— **【未确认】**：本次调查没有确认"如何可靠判断当前是单人/联机"。候选证据点在 `research/decompiled/` 的 `ZedZone.Net.NetModSession`、`GameController`、`ModSessionOverride`（旧项目 `research/decompiled/interop/` 里有同名文件）。**必须在 Phase 1 补上这一条，否则整个联机禁用是空的。**
- 一旦判定为联机（或判定不出来）→ **fail-safe**：`PatchManager.UninstallAll()`（这里需要 §5.2 的真卸载，因为不能要求用户重启）→ 所有 `IStatModule.AnyFeatureOn = false` → `UI` 显示"联机模式：功能已禁用"。
- 冻结状态下**不得继续写值**（`StatusModule.Tick` 直接返回，不是"写 0 次"而是"不进入"）。
- 判定应当是**周期性的**（例如每次场景切换 + 每 N 秒），因为玩家可能在游戏中途加入会话。

#### ④ 只在必要时用 Harmony

优先级阶梯（成本从低到高，**默认取能达成目标的最低档**）：

| 档 | 手段 | 成本 | 何时用 |
|---|---|---|---|
| 0 | **纯字段写**（每 tick 幂等覆盖） | 一次指针加法 | 默认选择。绝大多数"保持数值"需求 |
| 1 | **轮询修正**（自己的 tick 里 读→比→写） | 一次读 + 一次比较 | 需要"当它低于阈值时才补" |
| 2 | **Harmony Postfix** | 每帧封送 | 必须知道"某事件发生了"（如升级、受伤结算后） |
| 3 | **Harmony Prefix** | 每帧封送 + 可能抛异常 | 必须阻止原方法（如"这次伤害不要生效"） |

细则：
- 每次决定用 档 2/3，必须在代码注释里写下**理由**和**副作用**（旧项目 `PerfTweaks.cs:73-77` 就是这么注释的："只作用于阴影/水面反射/草地压痕这三类看不到就无意义的开销，不碰 AI、不碰物理、不碰状态机"）。
- 档 2/3 必须走 `PatchManager`，禁止裸 `Harmony.Patch`。
- 档 2/3 的补丁参数**尽量不用 IL2CPP 类型**（§11 坑 2）。

### 12.3 从旧项目**不要抄**的东西（明确清单）

| 不要抄 | 原因 |
|---|---|
| `FrameStats` 的 ring buffer + 1%/0.1% low + p50/p95/p99 | 为 FPS 测量设计。生存辅助不需要百分位统计【若 UI 要做"最近 60 秒趋势"，只借环形缓冲那 30 行，不借整个类】 |
| `BenchmarkRecorder` 的 CSV + summary 输出 | 性能基准专用格式（frame/elapsed_s/frame_ms/fps）。生存辅助若要记录历史，字段集完全不同，必须重写 |
| `F9` 基准录制 + `ZedFpsBench\` 输出目录 | 与生存辅助无关 |
| `Application.targetFrameRate` / `QualitySettings.vSyncCount` 干预（`Plugin.cs:217-236`） | 性能模组的职责；生存辅助不该动帧率 |
| 离屏视觉跳过三补丁（`UpdateShadowEffect` / `UpdateWaterReflectionState` / `UpdateGrassPress`） | 纯性能补丁，与本项目目标无关，且是**最大的封送风险源** |
| `ChunkSpawnFrameBudget.CanSpawn` 区块预算 | 同上，且会让地形 pop-in |
| `GoreBudget` / `BloodDecalBudget` 预算写入 | 同上 |
| `Preset` 的 `none/balanced/performance` 三档语义 | 那是"性能激进程度"，生存辅助的预设是"数值策略强度"，语义不可复用【机制可复用，枚举必须重定义】 |
| 名称 `ZedFpsOptimizer` / GUID `zedzone.fpsoptimizer` / 命名空间 `ZedZone.FpsOptimizer` | GUID 是身份，绝不可复用 |
| 旧项目的 `research/` 全部性能研究、`docs/PERFORMANCE_RESEARCH.md`、`performance-changelog.md` | 内容与本项目无关 |
| `Visible` 门控代替 `enabled`（`FpsOverlay.cs:137`） | 这是旧项目的一个**未做到的优化**；新项目应直接用 `enabled` 实现真零成本 |

### 12.4 强烈**要抄**的（虽然不在"可复用文件"里）

1. `Time.frameCount` 去重（§11 坑 1）—— 语义改为"本帧是否已 tick"。
2. 启动自述日志 = 验收单（§9.4）。
3. "默认零行为改动 + 每项可单独关 + 按需装补丁"三条不可妥协约束（`DEVELOPMENT_ROADMAP.md:52-56`）。
4. 诚实边界章节（§10.6）。
5. `README` 里的"第一次启动应该看到什么"（`README.md:39-50`）—— 让用户自己能验收。

---

## 13. 可复用 vs 需重写 清单

分类定义：
- **直接复用** = 文件可整份拷过来，只改名字/路径/Include 列表。
- **改造复用** = 结构、模式、约定原样保留，内容/具体项全部替换。
- **不要复用** = 与本项目目标无关，复用只会带进噪音或错误假设。

| 旧文件 | 分类 | 理由 |
|---|---|---|
| `src/ZedFpsOptimizer/Plugin.cs` | **改造复用** | 骨架全部有效：`[BepInPlugin]`+`BasePlugin`+`Instance` 单例（:27-34）、`Load()` 的装配顺序（:68-135）、`BindConfig` 的分节+范围+中文文案（:137-202）、`TryPatch` 降级（:238-251）、`LogInfo/LogWarn/LogErrorOnce`（:297-314）。**必须删除**：`ApplyFrameSettings`（:217-236）、`ToggleBenchmark`/`DescribeActiveTweaks`（:256-295）、Preset 的游戏语义（:204-215 机制留、枚举换）、5 个补丁门控（:100-109 改成 PatchManager 调用） |
| `src/ZedFpsOptimizer/PerfTweaks.cs` | **改造复用** | 复用"开关 static 字段 + 每项独立 + 补丁体 try/catch + `NoteFailure` 只报一次 + `ApplyFieldKnobs` 无 Harmony 字段写"这五件事（:23-71）。**全部 5 个补丁类删除**（:78-179），换成生存辅助的补丁集（如果有）。`AnyActive()`（:68-70）的模式保留 |
| `src/ZedFpsOptimizer/FpsOverlay.cs` | **改造复用** | OnGUI 骨架、懒建 GUIStyle（:109-133）、1×1 背景纹理（:113-118）、分辨率变化重建（:144）、`Update` 里做热键（:42-46,83-107）、异常即自禁（:75-81, 227-231）、底部按钮兜底（:221-225）—— 全部保留。**删除**：所有 FPS/1%low/p50/p99 显示（:163-219）、`Stats`/`Recorder` 字段。`Time.frameCount` 去重保留，语义改为 tick 去重。改进：用 `enabled` 而非仅 `Visible` |
| `src/ZedFpsOptimizer/FrameStats.cs` | **不要复用** | 整个类为"帧时间分布"设计（环形缓冲 + 排序取百分位）。生存辅助没有这个数据形态。若 UI 需要"最近 N 秒趋势"，只要环形缓冲那 20 行（:33-50 `Push` + :99-104 `ResetWindow`），其余（`LowFps`/`PercentileMs`/`Snapshot`）全删 |
| `src/ZedFpsOptimizer/BenchmarkRecorder.cs` | **不要复用** | CSV 列固定为 `frame,elapsed_s,frame_ms,fps`（:83），summary 全是性能指标（:143-158）。若要做"生存数据历史"必须重写字段集与格式。**唯一可借**：`Sanitize` 文件名净化（:178-187）、`MaxFrames` 上限防内存失控（:28, 45-49）、`WriteCsv` 用 `CultureInfo.InvariantCulture`（:91-94，防止区域设置把小数点变成逗号把 CSV 搞坏） |
| `src/ZedFpsOptimizer/ZedFpsOptimizer.csproj` | **改造复用** | 引用布局（`BepInEx\core` + `BepInEx\interop` 双根，:18-19）、`<Private>false</Private>` 全部（:22-67）、`GameRoot` 可覆盖（:17）、`net6.0`/`OutputPath=bin`/`AppendTargetFrameworkToOutputPath=false`/`GenerateDependencyFile=false`/`AllowUnsafeBlocks`/`NoWarn`（:3-15）—— 全部保留。改 `AssemblyName`/`RootNamespace`。UnityEngine 引用按新 UI 需求裁（OnGUI 需要 `IMGUIModule`+`TextRenderingModule`；若用 `Time`/`Object` 需要 `CoreModule`；若不再用 `KeyboardShortcut` 的 Legacy 路径可考虑去掉 `InputLegacyModule`，但 §8.3 建议保留） |
| `tests/ZedFpsSelfTest/Program.cs` | **改造复用** | 复用"手写 `Check()` + `_failures` 计数 + `Main` 返回 0/1 + 分组测试方法"这套骨架（:9-28）。**19 个用例全部替换**，新用例建议：数值钳位（`hunger` 超上限/负值）、`AnyFeatureOn` 状态机（开→关→再开不残留）、"关闭后不再写"（用可记录写入的 fake context 断言 0 次写）、联机判定输入→禁用、配置迁移、`Time.frameCount` 去重计数 |
| `tests/ZedFpsSelfTest/ZedFpsSelfTest.csproj` | **直接复用** | 只改 `<Compile Include>` 列表（:11-12）。`OutputType=Exe`/`net8.0`/`ImplicitUsings=disable` 原样保留。**前提**：被测文件必须不依赖 `UnityEngine` |
| `tools/build.ps1` | **改造复用** | 几乎可直接用：进程名同为 `ZEDZONE`（:29）、`DOTNET_ROOT`/`DOTNET_CLI_HOME`（:34-36）、`-p:GameRoot`（:39）、`$LASTEXITCODE` 检查（:40）、产物检查（:42-43）、`-SkipInstall`（:45-48）、复制到 `Mods\<Name>\`（:50-53）、结尾打印 Steam URL + 日志路径（:55-56）。**必改**：`$proj`（:18）、`$dll`（:42）、`$dest`（:50）三个路径；建议顺手加：构建后打印 `BepInEx\ZedModsLoader-report.txt` 路径 |
| `README.md` | **改造复用** | 章节骨架全部有效：定位与边界 → 怎么用 → 快捷键 → 默认状态（"只测量不改游戏"→"默认不写任何数值"）→ 配置项表（含风险列）→ 构建 → **验证状态表**（:164-174）→ 卸载 → 已知限制 → 许可。内容全换 |
| `docs/DEVELOPMENT_ROADMAP.md` | **改造复用** | §2.3 实机验证记录表（:74-83）、§2.4 "真实踩到的坑"写法（:85-125）、§2.5 代码结构 + 分层理由（:127-141）、§2.6 构建命令、§2.7 本机环境变更表 —— 这套"把过程变成可交接文档"的写法必须继承。§2.1/2.2 的性能结论删除 |
| `docs/MODDING_METHODS.md` | **改造复用** | BepInEx/HarmonyX/Il2CppInterop 的技术事实、三种模组写法、加载链分析、interop 是 stub 的警告 —— **对新项目完全有效**。性能相关的 2.4/性能编年史章节删除 |
| `docs/EXISTING_MOD_ANALYSIS.md` / `research/existing-mod-analysis.md` | **改造复用** | §5.1-5.4 的 ZedModsLoader 行为分析（扫描源/三种写法/跳过规则/配置键全表）是**装载环节的唯一依据**，必留。性能量化章节（`ArmorDebug` 刷屏等）删除，但"日志刷屏是真实风险"这个结论要留（→ §9） |
| `docs/GAME_TECHNICAL_OVERVIEW.md` | **改造复用** | 引擎/架构/BepInEx 链路部分有效；渲染性能部分删除 |
| `research/00-verified-facts.md` | **改造复用（方法论）** | 内容大部分是性能相关，删除。但**`【事实-文件】/【事实-运行】/【推断】`分级标注法**必须继承到本项目全部研究文档 —— 这是旧项目最被低估的资产 |
| `research/game-internals-*.md` / `api-surface-modloader.md` | **部分复用** | `api-surface-modloader.md` 的"如何从 interop 枚举 API 表面"方法论有效；具体类型清单需重查（生存辅助关心的是 `BasicCharacterController` / `CharacterData` / `CharacterStatusData`，不是渲染预算类） |
| `docs/BENCHMARK_PLAN.md` / `research/performance-changelog.md` / `research/sources.md`（性能部分） | **不要复用** | 纯性能基准方法论与性能更新编年史，与生存辅助无关 |
| `src/ZedFpsOptimizer/obj/`、`bin/`、`.gitignore` | 视情况 | `obj`/`bin` 不入库（旧项目 `.gitignore` 应已处理，本次未读该文件，【未确认】其内容）。`.gitignore` 需按新项目调整 |

### 13.1 一句话总结

> 旧项目真正值钱的是**四件事**：`TryPatch` 的降级封装、配置项的"中文 + 范围 + 风险 + 建议值"规范、`LogErrorOnce`/`NoteFailure` 的双轨日志去重、以及 **§11 那两个坑的机制性理解**。
> 代码层面能整份搬走的只有 `ZedFpsSelfTest.csproj`；其余全是"留骨架换血肉"。
> 而**最大的风险点从旧项目的"接到哪个方法"变成了新项目的"什么时候、写哪个字段"** —— 架构必须围绕这个转变重排。

---

## 附：本次调查的不确定项（必须在 Phase 1 补齐）

| # | 未确认项 | 影响 | 建议补齐方式 |
|---|---|---|---|
| 1 | **如何可靠判定"当前是单人还是联机"** | 整个"联机硬禁用"无法实现 | 反编译 `ZedZone.Net.NetModSession` / `GameController` / `ModSessionOverride` 的成员，找 session 状态字段 |
| 2 | `BasicCharacterController` 与 `CharacterStatusData` 之间的**访问路径** | 写值代码写不出来 | 已确认 `BasicCharacterController.characterData`（`research/decompiled/BasicCharacterController.cs:4059`）与 `CharacterData.characterStatusData`（`research/decompiled/CharacterData.cs:1537`）→ **链路：`bcc.characterData.characterStatusData.<field>`**；但**未验证运行期这条链是否始终非空**（换档瞬间、加载中可能为 null） |
| 3 | `UpdateStatus()`（`BasicCharacterController.cs:12817`）的**调用频率与时机** | 决定我们写值的 tick 频率和"覆盖是否会被立刻反向覆盖" | 实机 dump 调用计数；或反汇编 `GameAssembly.dll` |
| 4 | Harmony `Unpatch` 后**封送是否立即停止** | 决定联机硬禁用能否在运行期生效 | 实机：`Unpatch` 后继续跑 5 分钟，看 `trampoline error` 计数是否停增 |
| 5 | 游戏是否在**场景切换时重置** `CharacterStatusData.maxHunger` 等 static 字段 | 决定是否需要 `% 300` 周期性重放（§3.3 模式） | 实机：写值 → 换场景 → 读回 |
| 6 | `BepInEx` 的 `BasePlugin` 为何可直接 `AddComponent<T>()` | 只影响解释，不影响用法 | 读 BepInEx 6 源码 `BasePlugin` / `BaseChainloaderImpl` |
| 7 | 旧项目 `.gitignore` 是否已排除 `bin/obj` | 影响新项目模板 | 读 `<仓库目录>\ZED-ZONE-FPS-Optimization-Mod\.gitignore` |
