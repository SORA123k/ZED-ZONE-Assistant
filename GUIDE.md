# 新手安装教程（萌新必看，纯文字版）

> 这份教程面向"完全没装过 Mod"的玩家。照着做即可，不需要任何编程基础。
> 如果你只想快速安装，请看 [INSTALL.md](INSTALL.md)。

---

## 第 0 步：先备份存档（1 分钟，很重要）

按 `Win + R`，输入：

```
%USERPROFILE%\AppData\LocalLow\LevenLiu\ZEDZONE
```

回车后会打开一个文件夹，把里面的 **`GameData`** 整个复制一份到桌面（或任意位置）。

> 如果以后出问题，把备份复制回去就能恢复。装任何 Mod 之前都建议这么做。

---

## 第 1 步：确认游戏版本

1. 打开 Steam → 库 → 右键 **ZED ZONE（隔离区）** → **属性** → **已安装文件**；
2. 查看版本号，应为 **1.0.17**。

> 版本不一致也能试试，但可能因为游戏内部接口变化而失效。

---

## 第 2 步：找到游戏安装目录

Steam → 右键游戏 → **管理** → **浏览本地文件**。

你会看到类似这样的目录：

```
...\steamapps\common\ZED ZONE\
    ZEDZONE.exe
    GameAssembly.dll
    ZEDZONE_Data\
```

**记住这个路径**，后面所有"放到游戏目录"都是指这里。

---

## 第 3 步：安装 BepInEx 6（IL2CPP 版）

本模组本身**不包含** BepInEx，需要你先装它（BepInEx 是 Mod 加载器）。

### 3.1 下载

1. 浏览器打开：`https://github.com/BepInEx/BepInEx/releases`
2. 找到 **BleedingEdge** 构建（文件名通常形如 `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.xxx.zip`）；
3. 下载 **`BepInEx-Unity.IL2CPP-win-x64`** 那个压缩包（**注意：必须带 `IL2CPP` 和 `x64`**）。

> 如果找不到 BleedingEdge，可以搜索 "BepInEx 6 IL2CPP build" —— 关键是 **IL2CPP** 和 **win-x64**，不要下 Mono 版。

### 3.2 解压到游戏目录

把压缩包里的内容**全部解压到游戏根目录**（和 `ZEDZONE.exe` 同一层）。

解压后目录应该长这样：

```
...\ZED ZONE\
    ZEDZONE.exe
    GameAssembly.dll
    winhttp.dll            ← 必须有
    doorstop_config.ini    ← 必须有
    .doorstop_version
    BepInEx\
        core\
        interop\
        plugins\
```

### 3.3 先启动一次游戏

1. 从 Steam 正常启动游戏；
2. 看到主菜单就可以**退出**。

这一步是为了让 BepInEx 生成 `BepInEx\interop\` 里的游戏程序集（后续所有 Mod 都依赖它）。

**检查点**：打开 `...\ZED ZONE\BepInEx\interop\`，里面应该有很多 `.dll` 文件（例如 `Assembly-CSharp.dll`）。
如果是空的 → 说明 BepInEx 没生效，见文末【常见问题】。

---

## 第 4 步：下载本模组

1. 打开本仓库的 **Releases** 页面；
2. 下载最新版本的 **`ZedZone.UltimateAssistant.dll`**（就一个文件）。

---

## 第 5 步：放置 DLL

在游戏目录里新建文件夹：

```
...\ZED ZONE\Mods\ZedZone.UltimateAssistant\
```

把下载的 `ZedZone.UltimateAssistant.dll` 放进去：

```
...\ZED ZONE\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
```

> 也可以放到 `...\ZED ZONE\BepInEx\plugins\` 下。
> **两种位置只选一种**，不要同时放两份（会重复加载）。

---

## 第 6 步：启动并验证

1. 从 Steam 启动游戏；
2. 进入你的存档（不是主菜单）；
3. 按键盘 **`INSERT`** 键。

菜单出现 → **安装成功**。

如果没反应，打开日志检查：

```
...\ZED ZONE\BepInEx\LogOutput.log
```

用记事本搜索 `Ultimate Assistant`，应该能看到：

```
[Info   :ZED ZONE Ultimate Assistant] ZED ZONE Ultimate Assistant v1.0.0 loading
[Info   :ZED ZONE Ultimate Assistant] [ui] ClickGUI 已就绪（INSERT 开关，当前关闭）
```

---

## 第 7 步：基本使用

| 操作 | 说明 |
|---|---|
| `INSERT` | 打开 / 关闭菜单 |
| 鼠标左键点卡片 | 开 / 关该功能 |
| 点卡片右侧小按钮 | 绑定该功能的快捷键（按 ESC 清空绑定） |
| 拖动顶部标题栏 | 移动菜单 |
| 滚轮 | 上下滚动页面 |
| 设置 → 外观 | 换主题、改字体大小 |
| 设置 → 语言 | 简体中文 / English / 繁體中文 |

> 功能开关会播放提示音，屏幕顶部会出现"灵动岛"提示。

**重要**：`INSERT` 永远可以打开菜单，即使你改了其它菜单按键。

---

## 常见问题对照表

| 现象 | 原因 | 解决 |
|---|---|---|
| 按 `INSERT` 没反应 | Mod 没加载 | 查 `LogOutput.log` 是否有 `Ultimate Assistant v1.0.0 loading` |
| 日志里完全没有本模组 | DLL 放错位置 | 确认路径是 `Mods\ZedZone.UltimateAssistant\` 或 `BepInEx\plugins\` |
| 日志出现 `Failed to load` | BepInEx 版本不对 | 必须用 **BepInEx 6 IL2CPP win-x64** |
| `BepInEx\interop` 是空的 | BepInEx 没生效 | 检查 `winhttp.dll`、`doorstop_config.ini` 是否在游戏根目录 |
| 游戏启动就闪退 | 冲突/版本不符 | 先移除本模组确认游戏能启动，再逐个排查其它 Mod |
| 菜单打开了但人物还在动 | 设置项关了 | 设置 → 常规 → 打开"打开菜单时阻止游戏操作" |
| 看不到鼠标指针 | 极少见 | 模组会强制显示光标；仍有问题请带日志提 Issue |
| 功能没效果 | 开关没开 / 不在存档内 | 确认开关为开，且在存档里（主菜单无效） |
| 装完游戏更新后失效 | 游戏版本变了 | 等本模组适配新版本，或用 Steam 回退版本 |

---

## 卸载方法

删除这两处即可，游戏本体不受影响：

```
...\ZED ZONE\Mods\ZedZone.UltimateAssistant\        （删除整个文件夹）
...\ZED ZONE\BepInEx\config\zedzone.ultimateassistant.cfg   （可选，删除后配置重置）
```

---

## 安全提醒

- 本模组**只能用于单人离线**，联机时状态修改功能会自动禁用；
- 使用前请备份存档（见第 0 步）；
- 杀毒软件可能误报 `winhttp.dll`（BepInEx 的文件），这是正常的 Mod 加载器文件；
- 不要在未备份的情况下使用"自动补充材料 / 强制完成建造"这类会改存档的功能。

---

## 关于这个项目

本项目是 **vibe coding**（AI 编写代码、人类提需求与验证）的产物，代码可能不够完美，也可能有 AI 特有的疏漏。

如果遇到问题，欢迎到仓库提 Issue 并附上日志 —— **友善指出即可**，不用客气，指出问题就是最大的帮助。

