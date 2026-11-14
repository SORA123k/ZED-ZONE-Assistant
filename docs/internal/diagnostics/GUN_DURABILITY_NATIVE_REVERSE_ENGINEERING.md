# 枪械耐久原生逆向分析报告（GUN_DURABILITY_NATIVE_REVERSE_ENGINEERING）

- 分析日期：2026-11-13
- 目标：`BasicRangedWeapon.get_durabilityFloat()` 的原生实现与真实数据来源
- 工具：Ghidra 12.1.4（headless）、IL2CPP 元数据 API（运行期只读）、PE 头解析
- 约束：未修改 GameAssembly.dll / global-metadata.dat，全部为只读分析

---

## 1. GameAssembly 文件信息

| 项 | 值 |
|---|---|
| 路径 | `<游戏目录>\GameAssembly.dll` |
| 大小 | 80,572,416 字节 |
| mtime | 2026-11-13 15:50:24 |
| **SHA256** | `D0DD69F381C55E9D5DE894443932358FB574A0AF0BA30D1E8A737D21AE67C548` |
| PE | machine=0x8664 (x64)，sections=7，optMagic=0x20B (PE32+) |

分析副本：`Diagnostics\NativeAnalysis\GameAssembly.dll.copy`（同哈希）

## 2. global-metadata.dat 文件信息

| 项 | 值 |
|---|---|
| 路径 | `ZEDZONE_Data\il2cpp_data\Metadata\global-metadata.dat` |
| 大小 | 18,704,660 字节 |
| mtime | 2026-11-13 15:50:24 |
| **SHA256** | `DEA501711AC1F30AC061E6B9271084681B8C68CBF63F46EE7C2E6F9F6D0CF9EF` |

分析副本：`Diagnostics\NativeAnalysis\global-metadata.dat.copy`（同哈希）

`ZEDZONE.exe`：666,624 字节，FileVersion `2023.1.18.2843550`，SHA256 `2B4E438269BDF795B5CD9B8B28954A8E2D0FE6FD00DE69AFF3A3DB512F190D92`

## 3. IL2CPP 版本信息

| 项 | 值 |
|---|---|
| Unity | 2023.1.18f1 (2b639e48c919) |
| 游戏 | 1.0.17 |
| 托管运行时 | .NET 6.0.7 x64 |
| BepInEx | 6.0.0.0 |
| HarmonyX | 2.10.2.0 |
| Il2CppInterop | 1.5.3.0 |

## 4. getter 原生函数定位证据

运行期（只读）通过 IL2CPP 元数据 API 取得 `MethodInfo*`：

```
[MODULE] GameAssembly base=0x7FFDD1C40000 size=0x4F9B000
[METHOD] get_durabilityFloat MethodInfo*=0x13B826CD750 params=0
  slot+0: ptr=0x7FFDD30AB8A0  rva=0x146B8A0
```

- 方法名：`get_durabilityFloat`
- 所属类：`BasicRangedWeapon`（`Assembly-CSharp`）
- 参数：0；返回：`System.Single`
- 调用约定：x64 Microsoft ABI（`this` = RCX，返回值 = XMM0）
- **RVA：0x146B8A0**（Ghidra 基址 0x180000000 → VA 0x18146B8A0）

前 64 字节机器码（Ghidra 从文件读取，与运行期读到的字节一致）：

```
48 83 EC 28              sub  rsp,0x28
48 83 79 28 00           cmp  qword ptr [rcx+0x28], 0
74 77                    je   ...
48 8B 41 28              mov  rax, [rcx+0x28]
48 83 78 20 00           cmp  qword ptr [rax+0x20], 0
74 6C                    je   ...
48 8B 50 20              mov  rdx, [rax+0x20]
48 83 7A 18 00           cmp  qword ptr [rdx+0x18], 0
74 61                    je   ...
48 8B 81 C8 00 00 00     mov  rax, [rcx+0xC8]
48 85 C0                 test rax, rax
74 68                    je   ...
0F 57 C9                 xorps xmm1, xmm1
0F 2F 88 50 01 00 00     comiss xmm1, [rax+0x150]
73 49                    jnc  ...
```

## 5. getter 反汇编与反编译分析

Ghidra 反编译（`Diagnostics\NativeAnalysis\getter_dump.txt`）：

```c
float get_durabilityFloat(BasicRangedWeapon* this)
{
    if (this->weaponData == NULL) return 1.0f;                       // [rcx+0x28]
    if (this->weaponData->properties == NULL) return 1.0f;           // [rax+0x20]
    if (this->weaponData->properties->length == 0) return 1.0f;      // [rdx+0x18]
    if (this->weaponAttr == NULL) throw;                             // [rcx+0xC8]
    if (!(0.0f < this->weaponAttr->durabilityMax)) return 1.0f;      // [rax+0x150]

    int len = this->weaponData->properties->length;
    if (len == 0) throw /0;
    float v = this->weaponData->properties->data[0]                  // [rdx+0x20]
            / this->weaponAttr->durabilityMax;
    if (v < 0.0f) return 0.0f;
    if (v > 1.0f) return 1.0f;                                       // 常量 0x183A9EAEC = 1.0f
    return v;
}
```

结论（逐条对应任务提出的 9 个确认点）：

| 检查点 | 结论 |
|---|---|
| 是否直接读实例字段 | 是（`this+0x28`、`this+0xC8`） |
| 是否通过指针访问原生对象 | 是（ItemData、ItemAttr_RangedWeapon、Single[] 数组） |
| 是否调用其他原生函数 | 仅空引用/除零抛异常辅助，无业务调用 |
| 是否读配件数据 | 未直接读配件；读的是武器自身 `weaponData` 与 `weaponAttr` |
| 是否读共享状态 | 分母 `weaponAttr->durabilityMax` 来自 `ItemAttr_RangedWeapon`（配置对象） |
| 是否计算/归一化 | 是：`current / max`，并 clamp 到 `[0,1]` |
| 是否有条件分支 | 有（4 处空检查 + 除零检查 + 2 处 clamp） |
| 是否返回缓存值 | 否，每次实时计算 |
| 不同枪械是否不同实现 | 无：单一实现，差异只来自数据（properties[0] 与 durabilityMax） |

字段偏移实证（来自 IL2CPP 元数据导出，见 `il2cpp_meta_*.log`）：

| 偏移 | 归属 | 字段 | 类型 |
|---|---|---|---|
| `this+0x28` | `BasicRangedWeapon` | **`weaponData`** | `ItemData` |
| `this+0xC8` | `BasicRangedWeapon` | **`weaponAttr`** | `ItemAttr_RangedWeapon` |
| `weaponData+0x20` | `ItemData` | **`properties`** | `System.Single[]` |
| `+0x18` | Il2CppArray | length | int |
| `+0x20` | Il2CppArray | data[0] | float |
| `weaponAttr+0x150` | `ItemAttr_RangedWeapon` | 最大耐久（float） | float |
| `0x183A9EAEC` | 只读常量 | clamp 上限 | `1.0f` |

## 6. 关键交叉引用 / 数据来源

**枪械真实耐久 = `ItemData.properties[0]`（Single[] 首元素）**

- 不是 `ItemData.durability`（该字段在枪械上恒为 100，与 UI 无关）
- 不是 `BasicRangedWeapon` 的任何实例字段（108 个字段全采样零匹配）
- 不是 `weaponData.durability`
- 最大耐久 = `weaponAttr.durabilityMax`（`ItemAttr_RangedWeapon + 0x150`）

**为什么托管侧一直找不到**：`properties` 是 `System.Single[]`，之前的探针只比对**字段值**（数组对象指针），没有读取 `数组[0]` 元素，因此永远不匹配。

## 7. 原生耐久读取机制

```
UI / 游戏逻辑  →  durabilityFloat 值
                 = weaponData.properties[0] / weaponAttr.durabilityMax (clamp 0..1)
```

- 读取路径短，纯数据运算，无副作用；
- `properties` 属于**每件武器自己的 ItemData 实例**，因此是逐枪独立状态（可存档）。

## 8. 耐久变化调用链证据

**已确认**：存储位置（`properties[0]`）、公式、最大耐久来源。

**未确认**：写入者（扣减点）。原因：
- 扣减发生在原生 IL2CPP 代码中，没有公开托管方法；
- `HCC.CostItemDurability` 实测与枪械无关（105 次调用无一次涉及枪械 id）；
- 未对 `properties[0]` 的写入点做内存断点（本轮未做运行期断点分析）。

**诚实结论**：我们已知"数据在哪里"，但尚未定位"是谁在减它"。这不影响修复——因为可以直接把 `properties[0]` 写回满值。

## 9. v1.2.2 Postfix 失效原因（有证据）

v1.2.2 在 `get_durabilityFloat` 上加 Postfix，把返回值改成 1.0f。实测耐久仍下降，原因有两个，均有证据：

1. **游戏侧几乎不调用这个 getter**
   诊断日志中 `[READ-STACK]` 的全部 20 条最外层帧都是本模组自己的 `AssistantPanelUpdate`；
   说明 UI 耐久条与射击判定**并不通过该 getter 取值**，而是**内联读取 `properties[0] / durabilityMax`**。
   因此覆盖返回值对显示与内部逻辑都没有影响。

2. **Postfix 只改返回值，不改存储**
   `properties[0]` 仍在被扣减；即使某处调用 getter，下一次计算依旧得到降低后的比例。

**结论：读取层覆盖无效，必须写存储层 `properties[0]`。**

## 10. UI 与游戏逻辑是否共用数据源

两者共用同一数据源（`properties[0]` 与 `durabilityMax`），但**取值方式不同**：
- UI/逻辑侧内联计算（不经过托管 getter）；
- getter 只是同公式的托管侧封装。

因此：**写 `properties[0]` 会同时影响 UI 与逻辑**，这是正确的修复切入点。

## 11. 安全实现方案比较

| 方案 | 技术依据 | 适用范围 | 风险 | 能否保证逻辑用满耐久 |
|---|---|---|---|---|
| **A. 覆盖 getter 返回值** | 已实现（v1.2.2） | 仅调用 getter 的代码 | 无 | **不能**（实测无效） |
| **B. 原生 API 写耐久** | 无公开 API | — | — | 不适用 |
| **C. 写 `weaponData.properties[0]`** | **反编译实证 + 元数据偏移一致** | 所有枪械（共用同一公式） | 低：仅改本枪自己的数组首元素 | **能**（UI 与逻辑同源） |
| D. 拦截扣减点 | 扣减点未定位 | — | 未知 | 可能 |

**采纳 C**：
- 写入值由公式反推：`max = properties[0] / durabilityFloat`（df 已被游戏 clamp 到 (0,1]）；
- 只在 `durabilityFloat < 1` 时写入，写入后比例恰为 1；
- 不改结构、不改长度、不触碰其它数组元素；
- 关闭开关立即停止写入。

**未做**：任何未知内存写入、任何偏移猜测。

## 12. 未解决问题

1. **扣减点未定位**：不知道具体哪段原生代码减少 `properties[0]`；
2. **写入时机**：当前采用 250ms 轮询回写，若扣减与回写存在竞态，UI 可能出现一次短暂下降后恢复；
3. **存档影响**：`properties` 属于 ItemData，写入会随存档保存（这正是"耐久保持满"的预期，但属于修改存档数据，需知悉）；
4. **其它武器子类**：`GiantZombie` 等 NPC 武器共用同一 getter 公式，本实现只对本地玩家写入；
5. **未做内存断点验证**：若后续需要精确定位扣减点，应使用运行期硬件断点跟踪 `properties[0]` 的写入者。

---

## 附：分析文件清单

| 文件 | 说明 |
|---|---|
| `GameAssembly.dll.copy` | 分析副本（SHA256 见 §1） |
| `global-metadata.dat.copy` | 元数据副本（SHA256 见 §2） |
| `ghidra_scripts\DumpDurabilityGetter.java` | Ghidra 反编译脚本 |
| `getter_dump.txt` | 反编译原始输出（含字节、指令、C 伪码） |
| `ghidraproj\` | Ghidra 工程（可删除） |
