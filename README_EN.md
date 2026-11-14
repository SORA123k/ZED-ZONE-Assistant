# ZED ZONE Ultimate Assistant (SORA Assistant)

<p align="right"><a href="README.md">简体中文</a> | <b>English</b></p>

A **single-player, offline-only** utility mod for **ZED ZONE** (Steam AppID `1211600`), built on **BepInEx 6 (IL2CPP)** + **HarmonyX**. It ships an in-game ClickGUI-style menu (default hotkey `INSERT`) with durability, ammo, protection, movement and visual helpers.

> **Single-player / offline use only.** All state-modifying features are automatically disabled when the game reports an online session (fail-closed). Do not use it in multiplayer.

---

## Table of contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration](#configuration)
- [Build from source](#build-from-source)
- [Project layout](#project-layout)
- [Known limitations](#known-limitations)
- [Contributing](#contributing)
- [License](#license)

---

## Features

The menu has four categories: **Combat / Player / Visuals / Settings**. Every feature has its own toggle, its own config entry and an optional hotkey.

### Combat

| Feature | Description |
|---|---|
| Infinite Ammo | Shooting does not consume reserve ammo (local player only) |
| No Reload | Fire with an empty magazine; `R` still reloads natively |
| No Jam | Prevents weapon jams and clears the existing `MalfunctionFlag` |
| Tool / Melee Durability | Tools, melee weapons and gear never lose durability |
| Gun Durability | Keeps gun durability at max (writes `ItemData.properties[0]`) |
| No Overheat | Your weapon heat is always 0 (NPCs unaffected) |
| Auto Supply | Crafting/repair treat materials as sufficient; can force-finish the island boat |

### Player

| Feature | Description |
|---|---|
| No HP Loss | Taking damage does not reduce HP |
| No Stamina Loss | Sprinting/jumping costs no stamina |
| No Hunger Loss | Hunger does not drop (eating still works) |
| No Thirst Loss | Thirst does not drop (drinking still works) |
| Always Sprint | Auto-sprint when not firing; firing cancels sprint to avoid "cannot shoot while sprinting" |
| Movement Speed | 1.0x – 20.0x, also applies to rolling |
| One-click restore (buttons) | HP / Energy / Hunger / Thirst — one click, one restore |

### Visuals

| Feature | Description |
|---|---|
| Zombie ESP | Marks loaded zombies (name / distance / box, off-screen edge indicators) |
| Full Bright | Brightens night scenes via URP 2D global light + ambient light; no time/weather/AI changes |

### Settings

- **Theme**: Dark / BlackGold / BlueTech / Purple / Teal / Light / RedBlack
- **Font size**: 12–24 px, applied live
- **Language**: 简体中文 / English / 繁體中文, switches instantly
- UI scale, animations, block game input while the menu is open, show limb status
- **Hotkeys**: menu key supports multiple bindings (`Insert` is always active); each feature can bind one key
- **Sounds**: on/off notification sounds for feature toggles
- **Dynamic Island**: top-screen pill showing "feature X enabled/disabled" in real time

---

## Requirements

| Item | Requirement |
|---|---|
| Game | ZED ZONE **1.0.17** (Steam AppID `1211600`) |
| Engine | Unity 2023.1.18f1 / IL2CPP **x64** |
| Mod loader | **BepInEx 6** (IL2CPP, BleedingEdge 6.0.0-be.*) |
| Runtime | .NET 6.0 (bundled with BepInEx) |
| OS | Windows 10/11 x64 |

---

## Installation

### Option A — download the release (recommended)

1. Open **Releases** and download `ZedZone.UltimateAssistant.dll`.
2. Make sure BepInEx 6 (IL2CPP) is installed and the game has been launched once, so `BepInEx/interop/` exists.
3. Copy the DLL to:
   ```
   <Game>\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
   ```
   (`BepInEx\plugins\` also works — pick **one** location only.)
4. Launch the game, load a save, press **INSERT**.

### Option B — build from source

See [Build from source](#build-from-source).

### Verify

Check `<Game>\BepInEx\LogOutput.log` for:

```
[Info   :ZED ZONE Ultimate Assistant] ZED ZONE Ultimate Assistant v1.0.0 loading
[Info   :ZED ZONE Ultimate Assistant] [weapons] 补丁安装完成，成功 N 个
[Info   :ZED ZONE Ultimate Assistant] [ui] ClickGUI 已就绪（INSERT 开关，当前关闭）
```

A detailed step-by-step guide (including installing BepInEx) is in [INSTALL.md](INSTALL.md) (Chinese) and [GUIDE.md](GUIDE.md) (beginner walkthrough, Chinese).

---

## Usage

| Action | Description |
|---|---|
| `INSERT` | Open / close the menu (always available, cannot be unbound) |
| Left click | Toggle a feature by clicking the card or the switch |
| Drag the title bar | Move the menu (position is saved) |
| Mouse wheel | Scroll long pages |
| Feature hotkey | Button on the right side of each card: click, then press a key; `ESC` clears |
| Menu multi-bind | Settings → Controls → menu keys: bind extra keys besides `Insert` |

While the menu is open the mod blocks character input, disables the game's UI raycasters (prevents click-through) and forces the cursor visible. Closing the menu restores everything.

---

## Configuration

```
<Game>\BepInEx\config\zedzone.ultimateassistant.cfg
```

Key sections: `[General]`, `[UI]`, `[Features]`, `[Hotkeys]`, `[Menu]`, `[Esp]`, `[Diagnostics]`.
See the Chinese README for the full key list.

---

## Build from source

Requirements: .NET SDK 6.0+, ZED ZONE + BepInEx 6 installed (interop assemblies are used as compile references).

```bash
cd src/ZedZone.UltimateAssistant
dotnet build -c Release
```

Output: `bin/ZedZone.UltimateAssistant.dll`

Build and install in one step:

```powershell
pwsh tools/build.ps1
```

Override the game path when needed:

```bash
dotnet build -c Release -p:GameRoot="E:\Steam\steamapps\common\ZED ZONE\"
```

---

## Project layout

```
src/ZedZone.UltimateAssistant/
  Plugin.cs                  entry point
  Core/                      logic (config, features, durability, ESP, services)
  Patch/                     Harmony patches
  UI/                        IMGUI menu (ClickGui, themes, island, input guard)
docs/                        research & verification reports
Diagnostics/                 durability + native reverse-engineering reports
tests/                       offline unit tests
tools/build.ps1              build & install script
tools/release.ps1            bump version → build → tag → GitHub release
```

---

## Known limitations

- **Offline single-player only**; state-modifying features are disabled online.
- Tied to game **1.0.17** interop signatures; a major game update may require re-adaptation.
- Auto Supply handles normal crafting/building; the island escape boat uses a separate path and is handled by "force finish construction", which may affect quest flow — back up your save.
- Full Bright relies on URP 2D global light and ambient light; a custom darkening method would reduce the effect.
- Gun durability is written into `ItemData.properties[0]`, which is persisted in the save.

---

## Contributing

Contributions are welcome — code, translations, docs, bug reports.

- Read [DEVELOPMENT.md](DEVELOPMENT.md) for architecture, patch conventions and the new-feature checklist.
- Read [CONTRIBUTING.md](CONTRIBUTING.md) for the workflow, code style and PR checklist.
- Use the issue templates for bug reports / feature requests.

If you want to become a maintainer (write access), open an issue or contact the owner — see CONTRIBUTING.md.

---

## License

[MIT](LICENSE) © 2026 SORA123k
