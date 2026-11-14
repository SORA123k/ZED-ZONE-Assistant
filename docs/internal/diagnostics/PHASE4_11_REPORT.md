# Phase 4–11 连续开发交付报告

- 项目：`<仓库目录>\ZED-ZONE-Assistant`
- 版本：**v1.2.0**
- 游戏：1.0.17 / Unity 2023.1.18f1 / .NET 6.0.7 x64
- BepInEx 6.0.0.0 / HarmonyX 2.10.2.0 / Il2CppInterop 1.5.3.0

---

## A. Git 状态及最终 Commit

- 接手时 HEAD：`f1f6aa7`（诊断报告），工作区干净
- 本次提交：见文末 `git log`
- 未覆盖任何用户已有修改

## B. 修改文件清单

| 文件 | 变更 |
|---|---|
| `Core/DurabilityLock.cs` | **新增**（Phase 7 普通耐久锁定 + Phase 8 枪械保守锁定） |
| `Core/Il2CppMetaDiag.cs` | **新增**（Phase 4 IL2CPP 元数据只读探查） |
| `Core/ModConfig.cs` | 新增 `ItemDurabilityLock`、`MetaDiag` 配置 |
| `Core/Features.cs` | 新增 `ItemDurabilityLockOn`；恢复 `WeaponDurabilityOn` 为枪械锁定开关 |
| `Core/WeaponUtil.cs` | 移除未验证的耐久写入调用（ZeroDurabilityCost / RestoreAllDurability） |
| `Core/DurabilityDiag.cs` | 移除未使用字段 |
| `Patch/WeaponPatches.cs` | 移除三个耐久前缀补丁（暂停耐久写入） |
| `Plugin.cs` | 版本 1.2.0；初始化元数据探查 |
| `UI/AssistantPanel.cs` | 接入 Phase 7/8 与元数据采样 tick |
| `UI/ClickGui.cs` | 新增 Tool / Melee Durability 卡片，更新枪械文案 |

## C. Phase 4 字段元数据探查结果

通过 `GameAssembly.dll` 导出的 IL2CPP 元数据 API（`il2cpp_class_get_fields` 等，**只读**）导出：

- 类型：`BasicRangedWeapon`，继承链 `BasicRangedWeapon <- MonoBehaviour <- Behaviour <- Component <- Object`
- **字段总数 116**，方法总数 128
- 耐久相关：
  - `m_gunpartDurabilityCostFloat : System.Single offset=0x240 flags=0x0001（private）`
  - 方法：`get_durabilityFloat(params=0)`、`NormalizeLegacyNegativeDurability(params=0)`
  - **不存在 `set_durabilityFloat`，也不存在任何耐久存储字段**
- 关键字段偏移（实测）：
  - `character : BasicCharacterController offset=0x20`
  - **`weaponData : ItemData offset=0x28`**
  - `weaponSpriteRenderer offset=0x30`、`breakBarrelSpriteRenderer offset=0x38`
  - `damageFloat 0x1B0`、`recoilFloat 0x228`、`m_gunpartDurabilityCostFloat 0x240`、`reloadMagazineData 0x280`

实例只读采样（`durabilityFloat=0.78632` 时逐字段读取）：
- **没有任何实例字段的值等于 0.78632，也没有 ×100/×1000 的匹配**；
- 唯一变化相关字段 `m_gunpartDurabilityCostFloat = 1.2`（成本，非当前耐久）。

## D. Phase 5 真实存储结构结论

1. **getter 不读取本类型实例字段**（116 字段全部采样无匹配）；
2. 唯一可写作候选是组件持有的 **`weaponData : ItemData`（offset=0x28）**，即武器组件自己引用的 ItemData 实例；
3. 无法确认 getter 是否由原生代码直接读取该 ItemData 或其他对象（无托管实现可反编译）；
4. 未发现继承类覆盖（继承链仅 MonoBehaviour 体系）；
5. 不同枪械字段布局一致（同类实例，偏移稳定）；
6. **未实机确认**（本次会话未进入存档，无法采样持枪状态）。

## E. Phase 6 安全实现方案及依据

| 检查项 | 结论 |
|---|---|
| 官方 setter | **不存在**（128 个方法中无 `set_durabilityFloat`） |
| 可靠游戏 API | **无**（`durabilityFloat` 只读；`HCC.CostItemDurability` 已证与枪械无关） |
| 可验证的耐久消耗事件 | 未找到（`m_gunpartDurabilityCostFloat` 只是成本参数） |
| 安全阻止扣减 | 无公开拦截点 |
| 物品状态管理接口 | `ItemData.durability` 可写（工具/近战已验证） |
| 对其他属性的影响 | 若只写 `weaponData.durability` 且只增不减，影响面最小 |
| 多人同步 | 全部功能仅在离线单机启用（沿用联机门禁） |

**决策**：枪械采用"**只增不减写回自身历史最高值**"的保守实现，作用于 `weapon.weaponData.durability`。
不猜测偏移、不写未知内存；若该字段与 `durabilityFloat` 无关（当前观测恒 100），则不会产生任何写入。

## F. Phase 7 普通工具与近战实现情况

- 文件：`Core/DurabilityLock.cs::Tick()`
- 开关：`[Features] ItemDurabilityLock`（**默认 true**）
- 策略：每个 `ItemData` 记录"已观察到的最高 durability"，检测到下降时写回该最高值
- 保护：
  - 只处理玩家携带/装备物品（`GetAllItemsIncludeBackpack` + `GetCarriedInventoryList` + 装备槽）
  - `durability <= 0` 跳过
  - 250ms 冷却；仅在检测到下降时写入（避免高频写）
  - 场景切换清空基线；跟踪表超过 4000 条自动清空
  - 不修改最大耐久（游戏未暴露该字段）
- 状态：**已实现并编译通过**；实机表现待测

## G. Phase 8 枪械实现情况

- 文件：`Core/DurabilityLock.cs::TickGuns()`
- 开关：`[Features] WeaponDurability`（默认 false）
- 策略：对玩家每个 `BasicRangedWeapon` 组件的 `weaponData` 实例记录历史最高 durability，下降时写回
- 状态：**已实现，未实机验证**
- 未能完成的证据链：`durabilityFloat` 的实际读取来源仍未实机确认（需持枪存档采样）

## H. Phase 9 兼容性检查结果

- 原有功能**未重写**：Phase 2 读取、Phase 3A 恢复、3B 肢体只读、HP/体力/水分/饱食恢复、无限弹药、无需换弹、ESP、菜单、配置全部保留
- Harmony 补丁：
  - 功能性补丁 4 个：`CanFire`、`GetInventoryAmmoTotal`、`RemoveAmmoItemData`、`GetGunTemperature`
  - 诊断观察补丁 4 个（独立 Harmony id `zedzone.ultimateassistant.diag`，仅 postfix 记录）
  - **已移除** 3 个耐久前缀补丁（暂停耐久写入）
- 空引用/对象失效：所有 IL2CPP 访问均 try/catch 并记录
- 未修改全局 `GameConfigs`
- 生命周期：场景切换清空基线；`OnDestroy/OnApplicationQuit` 释放诊断 writer 与输入守卫

## I. Phase 10 构建结果

```
dotnet build -c Release
已成功生成。  0 个警告  0 个错误
```

- 源码内无未使用字段/变量
- 插件初始化顺序：配置绑定 → 模块注册 → 补丁安装（按需）→ 面板注入 → 诊断初始化

## J. Phase 11 安装结果

```
<游戏目录>\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
```
- 构建产物与已安装文件 MD5 一致
- 未修改任何游戏原始文件
- 配置文件保留（新增键自动落盘）

## K. DLL 文件大小及 MD5

- 大小：**156,672 字节**
- MD5：**`48D2404AFD2F56C5F6E2B57A558E1668`**

## L. 实际测试范围及未解决问题

**已实测（有日志证据）**
- 运行时/框架版本（.NET 6.0.7、BepInEx 6.0.0.0、Harmony 2.10.2.0、Il2CppInterop 1.5.3.0）
- `BasicRangedWeapon` 116 字段元数据与偏移（IL2CPP 元数据 API）
- `durabilityFloat` 无实例字段匹配（0.78632 时全字段采样）
- 工具/近战耐久字段 `ItemData.durability` 真实有效（100→92、95.74、54.29）

**未验证（不得视为已成功）**
- 工具/近战锁定在游戏内是否真的不消耗（本次未进入存档）
- 枪械 `weaponData.durability` 是否等于真实耐久来源
- 枪械锁定是否生效
- 高频写回是否引起 UI 抖动/存档异常

**未解决**
- `durabilityFloat` 的最终数据来源仍未取得**实机采样证据**
- 若 `weaponData.durability` 恒为 100，则枪械锁定不会生效，需要持枪存档采样后再定方案

**下一步建议**：进入存档、持枪开火 ≥10 枪，然后读取
`Diagnostics\il2cpp_meta_*.log` 中 `[SAMPLE] ... weaponData:` 与 `ratio` 行，
即可确认 `weaponData.durability/100 == durabilityFloat` 是否成立。
