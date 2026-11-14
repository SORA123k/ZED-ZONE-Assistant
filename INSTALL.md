# 安装指南（INSTALL）

本模组需要 **BepInEx 6（IL2CPP）** 作为 Mod 加载器。以下步骤假定你已经拥有正版 ZED ZONE（Steam AppID `1211600`）。

- 完全没装过 Mod → 看 [GUIDE.md](GUIDE.md)（手把手版）
- 配置项含义 → 看 [docs/CONFIG.md](docs/CONFIG.md)
- 游戏版本/适配 → 看 [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)

---

## 0. 备份存档（强烈建议）

```
%USERPROFILE%\AppData\LocalLow\LevenLiu\ZEDZONE\GameData
```

把整个 `GameData` 文件夹复制一份到别处。

---

## 1. 确认游戏版本

- 支持版本：**ZED ZONE 1.0.17**（Unity 2023.1.18f1 / IL2CPP x64）
- 在 Steam 库中右键游戏 → 属性 → 已安装文件 → 查看版本

不同版本可能因 IL2CPP 签名变化而无法运行；启动日志会打印版本不匹配警告，处理办法见
[docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)。

---

## 2. 安装 BepInEx 6（IL2CPP）

> 本模组**不包含** BepInEx，需要先装加载器。两条路任选其一。

### 方式 A（推荐新手）：Steam 创意工坊加载器

> 模组：[ZED ZONE 代码模组加载器 (BepInEx IL2CPP / DLL Mods)](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)（作者 **star fox**）
> **以下步骤整理自该创意工坊页面**，为便于阅读做了转述；如与原文有出入，以创意工坊页面为准。

1. 打开链接 → **订阅**（约 36 MB，Steam 自动下载）。
2. 打开 `...\steamapps\workshop\content\1211600\3834614258\`。
3. 双击 **`install.cmd`**：脚本自动定位游戏目录，把 BepInEx 与加载器装进去
   （**完全离线**；游戏装在 `Program Files` 时需**以管理员身份运行**）。
4. **从 Steam 启动游戏**（不要直接双击 `ZEDZONE.exe`）。
5. 主菜单左上角出现下面这行即成功：

   ```
   ZED MODS: N code mod(s) loaded
   ```

这个加载器会扫描：游戏目录 `Mods\` 下的 DLL（一个模组一个文件夹）、创意工坊里的 DLL
与 `.zedmod` 内的 DLL（解压到 `BepInEx\ZedModsCache`）、
`LocalLow\LevenLiu\ZEDZONE\Mods\Workspace`。安装后还会附带 `ZedModKit\`（写模组用的模板，
**本模组不需要**）。

- 卸载加载器：双击工坊目录里的 `uninstall.cmd`（删 BepInEx / Doorstop / .NET 运行库，不动 `Mods` 数据）。
- 仅支持 Windows x64；内置 BepInEx 6.0.0-be.793（LGPL-2.1）。
- 加载器配置：`BepInEx\config\zedzone.zedmodsloader.cfg`。
- 游戏更新后失效：删掉 `BepInEx\interop` 让它重建，或重跑 `install.cmd`。

### 方式 B：手动装 BepInEx

1. 前往 BepInEx 官方发布页（GitHub `BepInEx/BepInEx` 的 **BleedingEdge** 构建，选择 `BepInEx-Unity.IL2CPP-win-x64` 版本）；
2. 解压到游戏根目录，使目录结构变成：

```
<游戏目录>\
  BepInEx\
    core\
    interop\
  winhttp.dll
  doorstop_config.ini
  .doorstop_version
  ZEDZONE.exe
```

3. **首次启动一次游戏**再退出。这一步会生成 `BepInEx/interop/` 下的 IL2CPP 程序集（本模组编译时依赖它们）。
   - 如果 `interop` 目录为空，说明 BepInEx 没生效，请检查 `winhttp.dll` 是否在游戏根目录、Steam 启动项是否带参数。

---

## 3. 安装本模组

### 3.1 下载

从本仓库 **Releases** 下载：

```
ZedZone.UltimateAssistant.dll
```

### 3.2 校验（可选，推荐）

Release 说明里给了 SHA256，本地核对：

```powershell
Get-FileHash .\ZedZone.UltimateAssistant.dll -Algorithm SHA256
```

不一致说明下载/传输有问题，别装。

### 3.3 放置

推荐（模块化目录）：

```
<游戏目录>\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
```

或者（传统 plugins 目录）：

```
<游戏目录>\BepInEx\plugins\ZedZone.UltimateAssistant.dll
```

> 两者任选其一，**不要同时放两份**，否则会重复加载。
> 用方式 A 时，`Mods\` 正是创意工坊加载器扫描的位置。

### 3.4 启动

启动游戏 → 进入存档 → 按 **INSERT**。

---

## 4. 验证安装

打开：

```
<游戏目录>\BepInEx\LogOutput.log
```

搜索 `Ultimate Assistant`，正常应包含：

```
[Info   :ZED ZONE Ultimate Assistant] ZED ZONE Ultimate Assistant v1.0.1 loading
[Info   :ZED ZONE Ultimate Assistant] detected game version: 1.0.17
[Info   :ZED ZONE Ultimate Assistant] [patches] 启动时已挂载补丁数 = 0（功能开启后按需增加）
[Info   :ZED ZONE Ultimate Assistant] [ui] ClickGUI 已就绪（INSERT 开关，当前关闭）
```

按 `INSERT` 出现菜单即成功。若日志里出现"游戏版本不匹配"，见
[docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)。

---

## 5. 配置文件

首次运行后自动生成：

```
<游戏目录>\BepInEx\config\zedzone.ultimateassistant.cfg
```

常用项：

- 菜单快捷键 `[UI] ToggleKey`（默认 `Insert`，且 Insert 永久有效）
- 额外菜单键 `[Menu] MenuKeys`（逗号分隔多个 KeyCode 名称）
- 语言 `[Menu] Language`：`zhCN` / `en` / `zhTW`
- 主题 `[Menu] Theme`：`Dark` / `BlackGold` / `BlueTech` / `Purple` / `Teal` / `Light` / `RedBlack`

**完整 85 项（69 配置 + 16 快捷键）见 [docs/CONFIG.md](docs/CONFIG.md)。**

> 菜单里改的项立即生效；手动编辑文件后需重启游戏。

---

## 6. 卸载

删除以下文件/目录即可，游戏本体不受影响：

```
<游戏目录>\Mods\ZedZone.UltimateAssistant\
<游戏目录>\BepInEx\config\zedzone.ultimateassistant.cfg
```

加载器卸载：方式 A 跑 `uninstall.cmd`；方式 B 删 `winhttp.dll`、`doorstop_config.ini`、
`.doorstop_version` 与 `BepInEx\`。

---

## 7. 常见问题

| 现象 | 排查 |
|---|---|
| 按 INSERT 没反应 | 检查 `LogOutput.log` 是否有加载日志；确认没有把 DLL 放两份；确认 BepInEx 生效 |
| 主菜单没有 `ZED MODS: N code mod(s) loaded` | 创意工坊加载器没装成功：重跑 `install.cmd`（必要时管理员），并从 Steam 启动 |
| 菜单打开但游戏还在动 | 设置里确认「打开菜单时阻止游戏操作」为开 |
| 菜单里看不到鼠标 | 本模组会强制显示光标；若仍不可见，请在仓库提 Issue 附日志 |
| 游戏启动即闪退 | 大概率是 BepInEx 版本或游戏版本不匹配；先移除本模组确认游戏可启动 |
| 功能无效果 | 确认对应开关为开；部分功能需在存档内（非主菜单）才生效 |
| 联机时开关点不开 | 预期行为：联机状态下修改类功能 fail-closed，会自动拒绝并关闭 |
| 存档异常 | 立即用备份还原；这类问题请附上 `LogOutput.log` 提 Issue |

---

## 8. 反馈问题

提 Issue 时请附上：

1. 游戏版本（Steam 库 → 属性）
2. BepInEx 版本（或创意工坊加载器版本）
3. `BepInEx/LogOutput.log` 相关片段
4. 复现步骤
5. 是否使用其它 Mod（可能与其它 Mod 冲突）

安全问题（存档损坏、联机门禁失效、发布物被篡改）请走
[SECURITY.md](SECURITY.md) 里的私有渠道。
