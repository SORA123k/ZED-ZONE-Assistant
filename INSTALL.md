# 安装指南（INSTALL）

本模组需要 **BepInEx 6（IL2CPP）** 作为 Mod 加载器。以下步骤假定你已经拥有正版 ZED ZONE（Steam AppID `1211600`）。

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

不同版本可能因 IL2CPP 签名变化而无法运行，遇到问题请先看 `BepInEx/LogOutput.log`。

---

## 2. 安装 BepInEx 6（IL2CPP）

> 本模组**不包含** BepInEx，需要你自行安装。

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

### 3.2 放置

推荐（模块化目录）：

```
<游戏目录>\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
```

或者（传统 plugins 目录）：

```
<游戏目录>\BepInEx\plugins\ZedZone.UltimateAssistant.dll
```

> 两者任选其一，**不要同时放两份**，否则会重复加载。

### 3.3 启动

启动游戏 → 进入存档 → 按 **INSERT**。

---

## 4. 验证安装

打开：

```
<游戏目录>\BepInEx\LogOutput.log
```

搜索 `Ultimate Assistant`，正常应包含：

```
[Info   :ZED ZONE Ultimate Assistant] ZED ZONE Ultimate Assistant v1.0.0 loading
[Info   :ZED ZONE Ultimate Assistant] [features] ...
[Info   :ZED ZONE Ultimate Assistant] [weapons] 补丁安装完成，成功 N 个
[Info   :ZED ZONE Ultimate Assistant] [ui] ClickGUI 已就绪（INSERT 开关，当前关闭）
```

按 `INSERT` 出现菜单即成功。

---

## 5. 配置文件

首次运行后自动生成：

```
<游戏目录>\BepInEx\config\zedzone.ultimateassistant.cfg
```

- 菜单快捷键 `[UI] ToggleKey`（默认 `Insert`，且 Insert 永久有效）
- 额外菜单键 `[Menu] MenuKeys`（逗号分隔多个 KeyCode 名称）
- 语言 `[Menu] Language`：`zhCN` / `en` / `zhTW`
- 主题 `[Menu] Theme`：`Dark` / `BlackGold` / `BlueTech` / `Purple` / `Teal` / `Light` / `RedBlack`

---

## 6. 卸载

删除以下文件/目录即可，游戏本体不受影响：

```
<游戏目录>\Mods\ZedZone.UltimateAssistant\
<游戏目录>\BepInEx\config\zedzone.ultimateassistant.cfg
```

---

## 7. 常见问题

| 现象 | 排查 |
|---|---|
| 按 INSERT 没反应 | 检查 `LogOutput.log` 是否有加载日志；确认没有把 DLL 放两份；确认 BepInEx 生效 |
| 菜单打开但游戏还在动 | 设置里确认「打开菜单时阻止游戏操作」为开 |
| 菜单里看不到鼠标 | 本模组会强制显示光标；若仍不可见，请在仓库提 Issue 附日志 |
| 游戏启动即闪退 | 大概率是 BepInEx 版本或游戏版本不匹配；先移除本模组确认游戏可启动 |
| 功能无效果 | 确认对应开关为开；部分功能需在存档内（非主菜单）才生效 |
| 存档异常 | 立即用备份还原；这类问题请附上 `LogOutput.log` 提 Issue |

---

## 8. 反馈问题

提 Issue 时请附上：

1. 游戏版本（Steam 库 → 属性）
2. BepInEx 版本
3. `BepInEx/LogOutput.log` 相关片段
4. 复现步骤
5. 是否使用其它 Mod（可能与其它 Mod 冲突）
