# FINAL_RELEASE_REPORT.md — ZED ZONE Ultimate Assistant v1.0.0 最终交付报告

> 交付日期：2026-11-13
> 交付版本：**v1.0.1**
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
| 仓库 | `<仓库目录>\ZED-ZONE-Ultimate-Assistant` |
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

**115,712 字节**

---

## 6. MD5

```
98CF0B98FF6891F90A7F81D316B12382
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
| 补丁安装 | ✅ `[weapons] 补丁安装完成，成功 5 个` |
| 联机门禁 | ✅ 弹药类功能在 `IsMultiplayer` 为真时强制关闭（fail-closed） |
| 缓存检查 | ✅ 玩家对象每帧重新 `Resolve`，不缓存 |
| 旧 FPS 项目 | ✅ 见 §14 |
| 日志异常 | ⚠ 有 1 条 **游戏自身**的 NRE（见 §12），与本模组无关 |

---

## 12. 未完成或降级的功能

### 12.1 已实现但**未做实机功能验证**（本人按要求未做中途人工测试）

| 功能 | 已完成 | 未验证 |
|---|---|---|
| Infinite Ammo | 补丁 5/5 安装成功、加载无异常 | 实际射击时弹药是否不再减少 |
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

**版本/DLL**：v1.0.1 / 115,712 字节 / MD5 `98CF0B98FF6891F90A7F81D316B12382`

**仍未实机确认**：开火后弹匣数值是否稳定、是否有音效/动画副作用。
诊断方法：开一枪后查看 `BepInEx\LogOutput.log` 是否出现
`[weapons] CanFire 已拦截` / `Fire 后置` 等行。
