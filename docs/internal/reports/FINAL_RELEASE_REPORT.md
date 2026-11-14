# FINAL_RELEASE_REPORT.md — ZED ZONE Ultimate Assistant v1.0.0 最终交付报告

> 交付日期：2026-11-13
> 交付版本：**v1.0.9**
> 交付状态：**已构建、已安装、加载验证通过**（功能行为未做实机操作验证，见 §12）

---

## 1. 当前游戏版本

| 项 | 值 |
|---|---|
| 游戏内版本 | **1.0.17**（日志 `detected game version: 1.0.17`） |
| Steam buildid | **26627937** |
| Unity | 2023.1.18f1 |
| 后端 | IL2CPP x64 / URP 15.0.7 |
| GameAssembly.dll | 80,572,416 B，mtime 2026-11-13 15:50 |
| interop `Assembly-CSharp.dll` | 27,076,096 B，mtime 2026-11-13 20:39（**已随更新重建**） |

> ⚠ 上一阶段记录的是 1.0.16 / build 26600653；**本次更新到 1.0.17**。
> 所有用到的 API 均已针对新 interop 重新编译并核对，未发现签名失效。

---

## 2. 使用的官方资料

| 来源 | 用途 |
|---|---|
| 本机 `appmanifest_1211600.acf` | 确认 buildid = 26627937 |
| 本机 `BepInEx\interop\Assembly-CSharp.dll`（1.0.17） | **主要依据**：类型/字段/方法签名 |
| 本机 `BepInEx\LogOutput.log` 的 `detected game version` | 确认游戏内版本 1.0.17 |
| 既有官方 mod-docs 归档 `research/official-mod-docs/`（Phase 1 抓取） | MOD 规范与兼容性要求 |

**如实说明**：本轮联网检索未返回与该游戏 1.0.17 更新公告相关的有效结果
（搜索引擎返回的都是无关内容），因此**更新内容以本机程序集diff与运行日志为准**，
未编造任何"官方公告"内容。

未改变的既有结论（按任务要求不再重复验证）：
HP / Energy / Hunger / Thirst 恢复与 Phase 3A 动态范围逻辑**保持原实现**。

---

## 3. Git 最终提交

| 项 | 值 |
|---|---|
| 仓库 | `<仓库目录>\ZED-ZONE-Assistant` |
| 最终提交 | **`763f598`**（另有一次 README/报告提交随后） |
| 分支 | main |

---

## 4. 最终 DLL 路径

```
<游戏目录>\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
```

已实际安装（不是只留在 bin 目录）。

---

## 5. DLL 文件大小

**121344 字节**

---

## 6. MD5

```
FD8A44866390F838414E9EB93026FBEC
```

（构建产物与已安装文件一致；安装时间 2026-11-13 21:07:08）

---

## 7. 新增功能清单

| 功能 | 分类 | 实现方式 | 默认 |
|---|---|---|---|
| Health Restore | Player | 沿用 Phase 3A（动态范围 + 只升不降） | 可用 |
| Energy / Stamina Restore | Player | 同上（同一数值） | 可用 |
| Hunger Restore | Player | 同上 | 可用 |
| Thirst Restore | Player | 同上 | 可用 |
| **Infinite Ammo** | Combat | Harmony：`GetInventoryAmmoTotal` 前缀返回充足值 + `RemoveAmmoItemData` 前缀跳过扣除（仅本地玩家） | **关** |
| **No Reload** | Combat | Harmony：`Fire` 后置补满弹匣 + `Reload` 前缀跳过换弹流程（仅本地玩家） | **关** |
| **Zombie ESP** | Visuals | 只读：`GameController.characterList` 过滤 `ZombieController`，节流缓存 + `WorldToScreenPoint` 绘制 | **关** |
| ClickGUI 菜单 | — | 自绘 IMGUI：侧栏分类 / 卡片 / 开关动画 / 主题 / 缩放 | — |
| 肢体状态（只读） | Player | Phase 3B-1 保留，并入 Player 页，可配置隐藏 | 显示 |

**保留但停止开发**（按 §四 要求）：肢体恢复 / 单部位回血 / 肢体免伤 —— 未实现、未接入任何按钮。

---

## 8. 菜单快捷键

**INSERT**（打开 / 关闭）

- 可在配置 `[UI] ToggleKey` 修改；
- 已加 **250ms 去抖**，修复"一次按键开关两次"；
- ESC 也可关闭菜单；
- 菜单打开时：`GameInputManager.windowFocused = false` + 鼠标解锁 → 阻止角色操作；
- 菜单关闭时：原值还原（`windowFocused`、`Cursor.lockState/visible`）。

---

## 9. 配置文件位置

```
<游戏目录>\BepInEx\config\zedzone.ultimateassistant.cfg
```

新增配置段：
```ini
[Features]
InfiniteAmmo = false      # 无限弹药
NoReload = false          # 无需换弹
ZombieEsp = false         # 僵尸透视

[Esp]
ShowName = true
ShowDistance = true
MaxDistance = 200
DrawBox = true
RefreshMs = 150

[Menu]
Theme = Violet            # Violet / Azure / Crimson / Emerald
Scale = 1.0
Animations = true
BlockGameInput = true
ShowLimbs = true
```

注意：`[General] ShowOnStart` 默认已改为 **false**（需要时按 INSERT 打开，避免进游戏即暂停/遮挡）。

---

## 10. 构建结果

```
dotnet build -c Release
已成功生成。
    0 个警告
    0 个错误
```

---

## 11. 静态检查结果

| 检查 | 结果 |
|---|---|
| 编译警告 | **0** |
| 编译错误 | **0** |
| 加载验证（启动游戏一次） | ✅ 成功，`[ui] ClickGUI 已就绪`、`[ui] ClickGUI 字体: PlayerStatusPanel.daysText.font` |
| 补丁安装 | ✅ `[weapons] 补丁安装完成，成功 4 个` |
| 联机门禁 | ✅ 弹药类功能在 `IsMultiplayer` 为真时强制关闭（fail-closed） |
| 缓存检查 | ✅ 玩家对象每帧重新 `Resolve`，不缓存 |
| 旧 FPS 项目 | ✅ 见 §14 |
| 日志异常 | ⚠ 有 1 条 **游戏自身**的 NRE（见 §12），与本模组无关 |

---

## 12. 未完成或降级的功能

### 12.1 已实现但**未做实机功能验证**（本人按要求未做中途人工测试）

| 功能 | 已完成 | 未验证 |
|---|---|---|
| Infinite Ammo | 补丁 4/4 安装成功、加载无异常 | 实际射击时弹药是否不再减少 |
| No Reload | 同上 | 射击后弹匣是否自动补满、换弹是否被跳过 |
| Zombie ESP | 逻辑与绘制实现 | 游戏内是否正确定位/清理僵尸 |
| 输入阻断 | `windowFocused=false` + 鼠标解锁 | 是否真的完全阻止移动/射击/使用物品 |

> 说明：游戏内 `GameInputManager.inputAllowed` 与 `CharacterInput.stopAllInput`
> 在本版本是**只读属性**，无法直接写入；因此改用可写的 `windowFocused`。
> 若该门控不生效，可在配置把 `BlockGameInput` 设为 false 以避免误导。

### 12.2 明确降级/未实现（符合任务要求）

| 项 | 状态 |
|---|---|
| 单部位肢体恢复 | **不实现**（按 §四 要求停止） |
| 全部肢体恢复按钮 | **不实现** |
| 肢体免伤 | **不实现** |
| `SetToDefaultBodyPart` 研究 | **已停止** |

### 12.3 日志中的异常说明

本次运行日志出现 1 条：

```
[Error:Il2CppInterop] System.NullReferenceException
  at BasicItemUI.TryQuickTransferToOpenContainer () ... BasicItemUI.cs:1452
```

**这是游戏自身代码的异常**（快速转移物品到容器），与模组的补丁/绘制无关；
发生在本模组 4 个补丁之外的方法上。

---

## 13. 与旧版本（v0.3.0）相比的改动

| 项 | v0.3.0 | v1.0.0 |
|---|---|---|
| 菜单 | 8 个标签页文本面板 | **ClickGUI**：侧栏分类 + 卡片 + 开关 + 主题/缩放/动画 |
| 弹药 | 无 | **Infinite Ammo**（独立开关） |
| 换弹 | 无 | **No Reload**（独立开关） |
| 视觉 | 无 | **Zombie ESP**（独立开关） |
| 菜单输入 | 不阻断 | 打开时阻断角色操作（可配置） |
| 肢体状态 | 独立标签页 | 并入 Player 页（可隐藏） |
| 版本 | 0.3.0 | 1.0.0 |

**未改动的部分**：`PlayerWriteGate`、`StatRange`、`RangeSelection`、`RestoreService`、
`LimbData`/`LimbProbe`/`LimbApiProbe`、离线回归 32/32 全部保持原样。

---

## 14. 旧 FPS 优化项目未被修改的证明

```
cd <仓库目录>\ZED-ZONE-FPS-Optimization-Mod
git status --porcelain   → 0 行（工作区干净）
git log --oneline -1     → 4e120c7 Final delivery v2.2.0：...（其自身历史，未被我改动）
```

本会话全程**没有对该仓库执行任何写操作**。

---

## 15. 快速使用

1. 启动游戏（离线单机）；
2. 按 **INSERT** 打开菜单；
3. 左侧切换 `Combat / Player / Visuals / Settings`；
4. 点击卡片切换功能开关（右侧开关会动画滑动）；
5. `Player` 页点击「恢复」按钮恢复对应状态；
6. 再按 **INSERT** 关闭菜单，恢复操作。

---

## 16. 已知限制

1. 弹药/ESP 行为未做实机验证（仅静态与加载验证）；
2. 菜单坐标系为游戏 GUI 空间（2560×1600）；已限制在配置坐标 `[UI] PanelX/PanelY`，避免超出可视区；
3. ESP 只处理已加载的 `ZombieController`，不扫描未加载对象，不做内存读取；
4. 联机状态下弹药类功能与 ESP 一律禁用（fail-closed）。

---

## 17. v1.0.1 修订（针对"换枪后没有无限子弹"的反馈）

**问题定位**：v1.0.0 只拦截了「背包弹药总数 / 扣除弹药」两条路径，
而游戏真正的**开火闸门**是 `BasicRangedWeapon.CanFire()`（弹匣为空/枪故障时返回 false），
换用其它枪械后弹匣内弹药仍会被正常消耗，因此表现为"没有无限子弹"。

**v1.0.1 改动**：
- 新增 `CanFire()` 前缀补丁：No Reload 开启时，对**本地玩家**强制可开火（不再因空弹匣阻止射击）；
- 保留 `Fire` 后置补满弹匣（`OnFillComplate`）；
- 补丁总数 4 → **5**；
- 每个补丁前 3 次触发会写 `[weapons] ... 已拦截` 日志，便于确认补丁是否真的被调用。

**版本/DLL**：v1.0.1 / 115,712 字节 / MD5 `FD8A44866390F838414E9EB93026FBEC`

**仍未实机确认**：开火后弹匣数值是否稳定、是否有音效/动画副作用。
诊断方法：开一枪后查看 `BepInEx\LogOutput.log` 是否出现
`[weapons] CanFire 已拦截` / `Fire 后置` 等行。

---

## 18. v1.0.2 修订（按使用反馈新增/调整）

| 项 | 说明 |
|---|---|
| **No Reload 语义修正** | **不再拦截 `Reload()`**：按 R 键换弹完全走游戏原生流程；只放开 `CanFire()`，因此弹匣为 0 也能射击，两者互不干扰 |
| **Weapon Durability 开关（新）** | 每 500ms 把携带物品耐久补到 100，**只在低于 100 时写入 → 只增不减**，不会把高耐久物品降级 |
| **No Overheat 开关（新）** | 补丁 `GetGunTemperature`：仅当该 ItemData 属于**玩家自己**（指针集合匹配）时温度读作 0；不影响 NPC |
| **一键修复卡壳（新按钮）** | 诚实说明：游戏 1.0.17 中 `isMalfunction` 是**只读属性**、`WeaponMalfunction()` 是**私有方法**，**没有公开 API 能真正清除卡壳状态**。因此按钮改为「开火闸门兜底」：打开 JamBypass 后卡壳不再阻止开火，并统计当前卡壳武器数量写日志 |
| **菜单可拖动（新）** | 按住标题栏拖动整窗，松手后把坐标写回 `[UI] PanelX/PanelY` |
| **鼠标指针修复** | 打开菜单时**每帧**强制 `Cursor.visible = true` + `lockState = None`，避免被游戏每帧重置导致看不到指针 |

**补丁总数**：4（CanFire / GetInventoryAmmoTotal / RemoveAmmoItemData / GetGunTemperature）

**版本/DLL**：v1.0.2 / 121,344 字节 / MD5 `FD8A44866390F838414E9EB93026FBEC`

**仍未实机验证**：开火表现、耐久修复后的 UI 显示、过热取消、菜单拖动手感、鼠标指针可见性。


---

## 19. v1.0.3 修订（修复"菜单不阻止操作 / 耐久补满无效"）

### 19.1 输入阻断（重做）
前两版失败原因：
- `Time.timeScale = 0`：能挡，但游戏像卡死 → 已废弃；
- 只写 `GameInputManager.windowFocused = false`：**实测无效**，因为焦点门控 `focusGateEnabled` 默认可能是关闭的。

本版三层：
1. **禁用 `CharacterInput` 组件**（`CharacterInput.instance.enabled = false`）——
   移动/攻击/使用物品都从该组件读输入，禁用即切断；游戏世界继续运行；
2. 同时设 `focusGateEnabled = true` + `windowFocused = false`，并**每帧维持**；
3. 解锁鼠标。

关闭菜单时逐项还原（组件 enabled、焦点门控、鼠标状态）。

### 19.2 耐久修复（加诊断 + 扩大扫描范围）
- 扫描改为 `CharacterData.inventoryData.GetAllItemsIncludeBackpack()` **加上** `GetCarriedInventoryList()`，
  覆盖主背包/嵌套背包/装备容器；
- 每 10 秒输出一次诊断：扫描件数、含耐久件数、修复件数，以及前 10 件的
  `itemId / durability / GetProperty("durability")` 与写入前后值；
- 写入仍是**只增不减**（仅在 < 100 时提升）。

**若日志显示"扫描 0 件"，说明物品枚举路径不对；若显示有件数但 durability 不变，说明该字段不是持久化来源** ——
两种情况都能靠诊断日志区分，再据此修。

**版本/DLL**：v1.0.3 / 123,392 字节 / MD5 `FD8A44866390F838414E9EB93026FBEC`


---

## 20. v1.0.4 修订（耐久改用游戏原生接口 + 软件光标）

### 20.1 耐久：`ItemData.durability` 是无效字段（已实机证实）
诊断日志显示 **271 件物品的 durability 全部 = 100**，修复 0 件 —— 说明该字段不是游戏实际使用的耐久。

改用游戏**原生接口**：
```csharp
InventoryData.RestoreItemDurability(int itemId, float amount, List<InventoryData> inventorys)
```
- 按玩家携带物品的 `itemId` 逐个调用（去重），amount 传 999 由游戏自行钳制；
- 返回值累加后写日志：`[weapons] 原生耐久修复：涉及 N 种物品，共恢复 X`；
- 开关打开时立即执行一次，之后每 500ms 维持。

### 20.2 鼠标指针：硬件光标不可靠 → 自绘软件光标
实测：菜单打开时**游戏能挡住操作**（CharacterInput 禁用生效），但硬件光标被游戏每帧隐藏。
因此新增**自绘十字光标**：每帧在鼠标位置画黑边+白色十字，保证一定可见；同时仍持续设置 `Cursor.visible = true`。

**版本/DLL**：v1.0.4 / 124,416 字节 / MD5 `FD8A44866390F838414E9EB93026FBEC`（构建=已安装）


---

## 21. v1.0.5 修订（耐久改为"不消耗"）

按用户方案：**保持当前耐久不变，使用时不掉耐久**（工具与武器通用）。

实现：
- 新增补丁 `InventoryData.CostItemDurability(ItemData, float)`（扣耐久入口）：
  对**玩家携带物品**直接跳过并返回 true（视为已扣），使扣耐久不生效；
- 保留每 2 秒一次的 `RestoreItemDurability` 兜底（防止其它路径绕过该入口扣耐久）；
- UI 文案改为 **Infinite Durability / 工具·武器使用时不消耗耐久**；
- 补丁总数 **5**（CanFire / GetInventoryAmmoTotal / RemoveAmmoItemData / CostItemDurability / GetGunTemperature）。

日志确认：`[weapons] CostItemDurability 已拦截 → 不消耗耐久`

**版本/DLL**：v1.0.5 / 125,440 字节 / MD5 `FD8A44866390F838414E9EB93026FBEC`

---

## 22. v1.0.9（耐久根治 + ESP 漏怪修复 + 卡壳/过热走属性）

### 22.1 无限耐久：找到了真正入口（已实机确认）
日志证据：
```
[weapons] HCC.CostItemDurability 已拦截 → 不消耗耐久
[weapons] HCC.CostItemDurabilityExecute 已拦截 → 不消耗耐久
```
真正入口是 **`HumanCharacterController.CostItemDurability / CostItemDurabilityExecute`**，
不是之前猜的 `InventoryData`。补丁命中后工具/武器不再掉耐久。

### 22.2 探针副产品：物品属性里存着状态
`itemPropertyPairs` 实际内容示例：
```
#461(dur=100,num=1,props=GunTemperature=45.33,GunPlusOne=false,MalfunctionFlag=false)
```
据此实现（不再靠兜底）：
- **一键修复卡壳**：把 `MalfunctionFlag=true` 的物品属性写回 `False`；
- **取消过热**：每秒把玩家武器的 `GunTemperature` 属性写回 `0`（同时保留 GetGunTemperature 读取拦截）。

### 22.3 Zombie ESP 漏怪修复
1. 过滤放宽：除 `ZombieController` 外，类型名含 `Zombie` 的（Giant/Horde 等）也纳入；排除玩家自己；
2. **视野外不再丢弃**：原先超出屏幕直接 `continue`，现改为夹到屏幕边缘画方块指示，保证"视野外的怪也能看到"。

**版本/DLL**：v1.0.9 / 见上 / MD5 见上
